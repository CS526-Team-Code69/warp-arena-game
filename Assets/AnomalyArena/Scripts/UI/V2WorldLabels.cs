using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Fixed scene-authored label pool. Cloaked Shades do not reveal themselves through a health bar.</summary>
    public class V2WorldLabels : MonoBehaviour
    {
        [Serializable]
        public struct EnemyLabel
        {
            public RectTransform root;
            public UnityEngine.UI.Image hp, shield, affix;
            public UnityEngine.UI.Text name;
            public UnityEngine.UI.Image[] statusIcons;
            public UnityEngine.UI.Text[] statusCounts;
        }

        public V2ArtSet art;
        public EnemyLabel[] enemyLabels;
        [Tooltip("Ordinary enemies (Grunt, Skitter, Brute, Splitter, Spitter, Warden) also show their name above the bar. " +
                 "Off since 2026-11-06: only specials, elites and bosses are named, so names do not cover the models.")]
        public bool showOrdinaryEnemyNames;
        public UnityEngine.UI.Text[] holeLabels;
        public UnityEngine.UI.Text[] edgeLabels;
        [Tooltip("Follows the nearest XP orb until the player has collected any XP in the run (created by the interface installer).")]
        public UnityEngine.UI.Text orbHintLabel;
        [Tooltip("After the first pickup an edge notice still points at off-screen XP once at least this many XP orbs lie on the ground ...")]
        public int xpEdgeMinOrbs = 6;
        [Tooltip("... or once the XP on the ground is at least this share of the XP the next level needs.")] [Range(0f, 1f)]
        public float xpEdgeMinShareOfLevel = .3f;
        [Header("Offscreen black hole marker")]
        public Color holeEdgeColor = new(.78f, .36f, 1f);
        public Color holeEdgeGlow = new(1f, .84f, 1f);
        [Min(0f)] public float holeEdgePulsePerSecond = 1.8f;
        [Min(1f)] public float holeEdgeScale = 1.35f;
        [Tooltip("Canvas units kept clear above the bottom row and below the top texts for edge labels (the bottom one is set by V2HudLayout).")]
        public float edgeBottomInset = 200f, edgeTopInset = 140f;
        [Header("Execute line (Railgun Execute held)")]
        [Tooltip("Colour of an HP bar whose enemy is already below the held Execute's threshold.")]
        public Color executeBarColor = new(1f, .06f, .1f);
        [Tooltip("Colour of the tick that marks the execute threshold on every non-boss HP bar.")]
        public Color executeTickColor = Color.white;
        [Min(1f), Tooltip("Width of that tick in canvas units.")] public float executeTickWidth = 2f;
        [Header("Enemy label over a 3D model (2026-11-06)")]
        [Tooltip("Canvas units an enemy's label is raised above the top of its 3D model. The label is centred on its point and its status icons hang 17 units below it, so 18 keeps them off the model. Whitebox bodies keep the old head point and get no lift.")]
        public float modelLabelLift = 18f;
        Enemy[] bindings;
        // The 3D model of each bound enemy (looked up when the binding changes), for LabelPoint.
        V2ModelPresentation[] models;
        float refresh;
        RectTransform canvasRect;
        readonly List<Vector2> placedEdges = new();
        static readonly Color XpEdgeColor = new(.45f, .8f, 1f);
        // Created at runtime on first use: one tick per saved label, plus each bar's own colour to restore.
        UnityEngine.UI.Image[] executeTicks;
        Color[] hpColors;
        bool[] executeBelow;

        void Awake()
        {
            bindings = new Enemy[enemyLabels.Length];
            models = new V2ModelPresentation[enemyLabels.Length];
            canvasRect = GetComponentInParent<Canvas>().transform as RectTransform;
            executeTicks = new UnityEngine.UI.Image[enemyLabels.Length];
            hpColors = new Color[enemyLabels.Length];
            executeBelow = new bool[enemyLabels.Length];
            for (int i = 0; i < enemyLabels.Length; i++)
            {
                hpColors[i] = enemyLabels[i].hp ? enemyLabels[i].hp.color : Color.white;
                if (enemyLabels[i].name) AddNameShadow(enemyLabels[i].name);
            }
        }

        /// <summary>Whether this enemy's bar shows the execute tick now, and whether the bar is red (below the line).</summary>
        public bool ShowsExecuteLine(Enemy enemy, out bool belowLine)
        {
            belowLine = false;
            for (int i = 0; i < bindings.Length; i++)
            {
                if (bindings[i] != enemy) continue;
                var tick = executeTicks[i];
                belowLine = executeBelow[i];
                return tick && tick.gameObject.activeSelf;
            }
            return false;
        }

        /// <summary>Execute held: a tick at the threshold on every non-boss bar, and the bar turns red once the enemy is below it.</summary>
        void ShowExecuteLine(int index, Enemy enemy, float threshold)
        {
            var view = enemyLabels[index];
            if (!view.hp) return;
            bool show = threshold > 0f && !enemy.IsBoss;
            var tick = executeTicks[index];
            if (show && !tick)
            {
                var go = new GameObject("ExecuteTick", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                go.transform.SetParent(view.hp.rectTransform, false);
                tick = executeTicks[index] = go.GetComponent<UnityEngine.UI.Image>();
                tick.raycastTarget = false;
            }
            if (tick)
            {
                if (tick.gameObject.activeSelf != show) tick.gameObject.SetActive(show);
                if (show)
                {
                    var rect = tick.rectTransform;
                    rect.anchorMin = new Vector2(threshold, -.25f); rect.anchorMax = new Vector2(threshold, 1.25f);
                    rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(executeTickWidth, 0f); rect.anchoredPosition = Vector2.zero;
                    tick.color = executeTickColor;
                }
            }
            bool below = executeBelow[index] = show && enemy.maxHp > 0f && enemy.Hp / enemy.maxHp < threshold;
            Color color = below ? executeBarColor : hpColors[index];
            if (view.hp.color != color) view.hp.color = color;
        }

        void Update()
        {
            if (!ArenaText.Ready) return;
            var gm = GameManager.Instance;
            if (!gm || !gm.cam || !gm.waves || !canvasRect) return;
            refresh -= Time.unscaledDeltaTime;
            if (refresh <= 0f) { refresh = .1f; RefreshEnemyData(gm); }
            for (int i = 0; i < bindings.Length; i++)
            {
                var enemy = bindings[i]; var view = enemyLabels[i];
                bool show = enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy && !enemy.Cloaked;
                if (show) NarrowBars(view, enemy);
                if (show)
                {
                    // Over the top of the 3D model as this camera sees it, so a lower camera does not lay the label across the body; in
                    // whitebox over the top of the whitebox look (Combatant.TryModelTop, 2026-11-10).
                    bool onModel = LabelPoint(models[i], gm.cam, out var top) || enemy.TryModelTop(gm.cam, out top);
                    show = PlaceWorld(view.root, onModel ? top : enemy.HeadPoint(gm.cam), gm.cam, onModel ? modelLabelLift : 0f);
                }
                if (view.root.gameObject.activeSelf != show) view.root.gameObject.SetActive(show);
            }
            int edges = 0;
            placedEdges.Clear();
            var world = WorldMap.Active;
            if (world && world.TrialActive) PlaceTrialPortalLabel(world, gm);
            else if (world && world.holes != null)
            {
                // The V2 day campaign's black hole (since 2026-11-06): its count over it, and over the hidden gate an E that says where it
                // leads; no notice at the screen's edge (the afternoon's ore trail leads there). Its other labels stay hidden.
                bool campaign = world.CampaignMapApplied;
                for (int i = 0; i < Mathf.Min(holeLabels.Length, world.holes.Length); i++)
                {
                    var hole = world.holes[i]; var label = holeLabels[i];
                    bool show = hole && hole.Phase != HolePhase.Gone && !world.TrialActive;
                    if (!show) { label.gameObject.SetActive(false); continue; }
                    // The label offers E only while E works (on the portal, free to act, in a wave or walking the build phase).
                    bool canEnter = world.CanEnterPortal(gm.player, hole);
                    string text = campaign ? CampaignHoleText(world, hole, canEnter)
                        : hole.Phase == HolePhase.Portal ? ArenaText.Text(canEnter ? "ui.hole.enter" : "ui.hole.portal") + "\n" + ArenaText.Text("ui.hole.remaining", hole.WavesLeft)
                        : world.CanFeed && hole.Phase == HolePhase.Active ? $"{hole.Feeds} / {hole.feedsToOpen}" : "";
                    if (label.text != text) label.text = text;
                    // The campaign's gate is smaller than the hole it was: its label sits lower, clear of the base bar at the screen's top
                    // while the player stands on it (the classic run keeps its height).
                    float lift = campaign && hole.Phase == HolePhase.Portal ? 2.2f : hole.size * .5f + 1f;
                    bool visible = PlaceWorld(label.rectTransform, hole.Centre + gm.cam.transform.up * lift, gm.cam);
                    label.gameObject.SetActive(visible && text.Length > 0);
                    if (!campaign && Offscreen(hole.Centre, gm.cam) && edges < edgeLabels.Length)
                    {
                        float distance = gm.player ? Query.Flat(hole.Centre - gm.player.Position).magnitude : 0f;
                        if (hole.Phase == HolePhase.Portal)
                            PlaceEdge(edgeLabels[edges++], hole.Centre, gm.cam, ArenaText.Text("ui.hole.offscreen", hole.WavesLeft), true);
                        else
                            PlaceHoleEdge(edgeLabels[edges++], hole.Centre, gm.cam, ArenaText.Pair("黑洞", "BLACK HOLE") + $"  {distance:0}m");
                    }
                }
                // The campaign keeps one hole in play (none at night): the labels of the holes put away hide.
                if (campaign)
                    for (int i = world.holes.Length; i < holeLabels.Length; i++)
                        if (holeLabels[i] && holeLabels[i].gameObject.activeSelf) holeLabels[i].gameObject.SetActive(false);
            }
            // Living enemy directions/counts use their dedicated eight saved, merged slots.
            // Hole directions and pre-spawn Shade notices retain this separate source.
            if (gm.waves.preparedMarkers != null)
                foreach (var marker in gm.waves.preparedMarkers)
                {
                    if (!marker || !marker.gameObject.activeInHierarchy || !marker.definition || marker.definition.kind != EnemyKind.Shade
                        || edges >= edgeLabels.Length || !Offscreen(marker.transform.position, gm.cam)) continue;
                    PlaceEdge(edgeLabels[edges++], marker.transform.position, gm.cam, ArenaText.Text("ui.shade.incoming"), true);
                }
            // XP on the ground. Teaching: until the first XP is collected in a run, the nearest orb carries a label (an edge notice when
            // it is off screen). Afterwards an edge notice still points at the nearest off-screen orb while a lot of XP lies around.
            var orbs = OrbPool.Instance;
            bool orbLabelOn = false;
            if (orbs && gm.player && gm.IsCombatActive)
            {
                var cam = gm.cam;
                var nearest = orbs.NearestXp(gm.player.Position);
                if (orbs.XpUncollected && nearest && orbHintLabel)
                {
                    string text = ArenaText.Pair("经验 · 捡起来升级", "XP · PICK UP TO LEVEL UP");
                    if (Offscreen(nearest.Position, cam))
                    {
                        if (edges < edgeLabels.Length) { PlaceEdge(edgeLabels[edges++], nearest.Position, cam, text, true); edgeLabels[edges - 1].color = XpEdgeColor; }
                    }
                    else
                    {
                        if (orbHintLabel.text != text) orbHintLabel.text = text;
                        float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2f * Mathf.PI * 2f);
                        orbHintLabel.color = Color.Lerp(new Color(.45f, .8f, 1f), Color.white, pulse);
                        orbLabelOn = PlaceWorld(orbHintLabel.rectTransform, nearest.Position + cam.transform.up * 1.2f, cam);
                    }
                }
                else if (nearest)
                {
                    int onGround = orbs.CountXp(out float xpOnGround);
                    float needed = orbs.progression ? Mathf.Max(1, orbs.progression.XpRequired) : 1f;
                    if (onGround >= xpEdgeMinOrbs || xpOnGround >= xpEdgeMinShareOfLevel * needed)
                    {
                        var away = orbs.NearestXp(gm.player.Position, p => Offscreen(p, cam));
                        if (away && edges < edgeLabels.Length)
                        {
                            PlaceEdge(edgeLabels[edges++], away.Position, cam, ArenaText.Pair($"经验球 ×{onGround}", $"XP ×{onGround}"), true);
                            edgeLabels[edges - 1].color = XpEdgeColor;
                        }
                    }
                }
            }
            if (orbHintLabel && orbHintLabel.gameObject.activeSelf != orbLabelOn) orbHintLabel.gameObject.SetActive(orbLabelOn);
            for (int i = edges; i < edgeLabels.Length; i++) edgeLabels[i].gameObject.SetActive(false);
        }

        /// <summary>The V2 day campaign's black hole: how many it has swallowed of what opens it, then over the hidden gate the E that takes
        /// the player in while it does (a question follows), else what the gate is.</summary>
        static string CampaignHoleText(WorldMap world, BlackHole hole, bool canEnter) => hole.Phase switch
        {
            HolePhase.Active => hole.campaignLabel ?? (world.CanFeed ? $"{hole.Feeds} / {hole.feedsToOpen}" : ""),
            // 2026-11-10: the portal carries no text of its own; only standing on it shows the key that enters.
            HolePhase.Portal => canEnter ? ArenaText.Pair("按 E 进入", "E · ENTER") : "",
            _ => ""
        };

        /// <summary>Void trial: the map's holes are frozen, so their saved labels are free. The first offers E on the trial portal the player
        /// stands on while E would take it (WorldMap.CanTakeTrialPortal), placed like a portal's label on the map; the others stay hidden.</summary>
        void PlaceTrialPortalLabel(WorldMap world, GameManager gm)
        {
            bool on = world.CanTakeTrialPortal(gm.player, out var portal);
            for (int i = 0; i < holeLabels.Length; i++)
            {
                var label = holeLabels[i];
                if (!label) continue;
                bool show = i == 0 && on;
                if (show)
                {
                    string text = ArenaText.Pair("E · 传送", "E · TRAVEL");
                    if (label.text != text) label.text = text;
                    show = PlaceWorld(label.rectTransform, portal.Centre + gm.cam.transform.up * (portal.size * .5f + 1f), gm.cam);
                }

                if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            }
        }

        /// <summary>A common enemy's bar stays narrower than the body under it; a boss or an elite keeps the full width. The same in whitebox and in the art.</summary>
        static void NarrowBars(EnemyLabel view, Enemy enemy)
        {
            float width = enemy.IsBoss || enemy.Elite ? 1f : Mathf.Clamp(enemy.radius * .85f, .42f, .78f);
            NarrowBar(view.hp ? view.hp.rectTransform.parent as RectTransform : null, width);
            NarrowBar(view.shield ? view.shield.rectTransform.parent as RectTransform : null, width);
        }

        /// <summary>Scales a bar about its own middle, whatever its pivot.</summary>
        static void NarrowBar(RectTransform bar, float width)
        {
            if (!bar || Mathf.Approximately(bar.localScale.x, width)) return;
            float toMiddle = bar.rect.width * (.5f - bar.pivot.x);
            var at = bar.anchoredPosition;
            at.x += toMiddle * (bar.localScale.x - width);
            bar.anchoredPosition = at;
            bar.localScale = new Vector3(width, 1f, 1f);
        }

        static bool Offscreen(Vector3 world, Camera camera)
        {
            Vector3 viewport = camera.WorldToViewportPoint(world);
            return viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f;
        }

        void RefreshEnemyData(GameManager gm)
        {
            int index = 0;
            var weapons = gm.runWeapons; var held = weapons ? weapons.Active : null;
            float executeLine = held != null && held.revealed && held.form == WeaponForm.Railgun && held.variant == 1 ? weapons.ExecuteThreshold(held) : 0f;
            foreach (var enemy in gm.waves.Alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.gameObject.activeInHierarchy || index >= bindings.Length) continue;
                if (bindings[index] != enemy) models[index] = enemy.GetComponent<V2ModelPresentation>();
                bindings[index] = enemy;
                ShowExecuteLine(index, enemy, executeLine);
                var view = enemyLabels[index++]; var status = ElementStatus.For(enemy);
                view.hp.fillAmount = enemy.maxHp > 0f ? Mathf.Clamp01(enemy.Hp / enemy.maxHp) : 0f;
                view.shield.fillAmount = status && enemy.maxHp > 0f ? Mathf.Clamp01(status.Shield / enemy.maxHp) : 0f;
                view.shield.gameObject.SetActive(status && status.Shield > 0f);
                // Elites show their affix beside the bar; bodies inside a Chanter aura show the buff, an elite after its affix (document 9.2).
                // Since 2026-10-28 every other enemy names its kind above its bar too, as the bosses do, in a softer white.
                // Since 2026-11-06 ordinary enemies (the basics in EnemyRoles) show only their bar, so the name no longer covers the
                // model; specials, elites and bosses keep their name. Status words (Chanted, Marked) still show on any enemy.
                string kind = enemy.definition ? KindName(enemy.definition.kind) : "";
                bool named = showOrdinaryEnemyNames || enemy.IsBoss || enemy.IsSpecial;
                string name = enemy.IsBoss ? enemy.definition.kind == EnemyKind.Hollow ? ArenaText.Pair("纳什先知", "THE NASH PROPHET") : ArenaText.Pair("暴虐君王", "THE TYRANT KING")
                    : enemy.Elite ? kind + " · " + ArenaText.Text("ui.elite") + (enemy.Affix != ElementKind.None ? " · " + ArenaTerms.Element(enemy.Affix) : "") : named ? kind : "";
                Color nameColor = enemy.IsBoss || enemy.Elite ? Color.white : CommonNameColor;
                if (view.name.color != nameColor) view.name.color = nameColor;
                if (enemy.Chanted) name = (name.Length > 0 ? name + " · " : "") + ArenaText.Pair("吟唱强化", "CHANTED");
                // Execute Twist: a marked enemy says so above its bar.
                if (weapons && weapons.IsExecuteMarked(enemy)) name = (name.Length > 0 ? name + " · " : "") + ArenaText.Pair("处决标记", "MARKED");
                // The Nash Prophet takes no damage while one of its summons lives (system doc 13.1).
                if (enemy.ProphetWarded) name += " · " + ArenaText.Pair("无敌", "INVULNERABLE");
                if (view.name.text != name) view.name.text = name;
                V2Hud.SetElementIcon(view.affix, art, (int)enemy.Affix - 1);
                for (int j = 0; j < 4; j++)
                {
                    float count = status ? j switch { 0 => status.Fire, 1 => status.Poison, 2 => status.Frost, _ => status.Lightning } : 0f;
                    // An enemy's Fire is its burn per second: shown whenever it burns, rounded and at least 1.
                    bool burning = j == 0 && status && status.Burning;
                    bool on = count >= 1f || burning || (j == 2 && status && status.Frozen) || (j == 3 && status && status.Paralysed);
                    V2Hud.SetElementIcon(view.statusIcons[j], art, on ? j : -1);
                    view.statusCounts[j].gameObject.SetActive(on);
                    string value = j == 2 && status && status.Frozen ? ArenaText.Text("ui.freeze.short") : j == 3 && status && status.Paralysed ? ArenaText.Text("ui.paralysis.short")
                        : burning ? Mathf.Max(1, Mathf.RoundToInt(count)).ToString() : Mathf.FloorToInt(count).ToString();
                    if (on && view.statusCounts[j].text != value) view.statusCounts[j].text = value;
                }
            }
            for (int i = index; i < bindings.Length; i++) { bindings[i] = null; models[i] = null; }
        }

        /// <summary>
        /// Where an enemy's label sits over its shown 3D model (2026-11-10, camera-P47): the highest point, along the camera's up, of the
        /// ellipsoid that fills the model's bounds. Combatant.TryModelTop takes the box's highest corner instead; in the 2.5D view (pitched
        /// 50°, turned 45°) that corner is raised by half the box's width and depth as well as its height, so over a small, wide model
        /// (a Grunt or a Skitter, legs spread) the bar floated about 1.5 m above the head. A rounded body's top is where the ellipsoid's
        /// is; modelLabelLift still raises the label off it. False while no model is shown (whitebox): the caller keeps HeadPoint.
        /// </summary>
        static bool LabelPoint(V2ModelPresentation model, Camera cam, out Vector3 point)
        {
            point = default;
            if (!model || !cam || !model.UsingModel || model.modelRenderers == null) return false;
            bool any = false;
            Bounds bounds = default;
            foreach (var shown in model.modelRenderers)
            {
                if (!shown || !shown.enabled || !shown.gameObject.activeInHierarchy) continue;
                if (any) bounds.Encapsulate(shown.bounds);
                else { bounds = shown.bounds; any = true; }
            }
            if (!any) return false;
            Vector3 up = cam.transform.up, half = bounds.extents;
            float x = up.x * half.x, y = up.y * half.y, z = up.z * half.z;
            point = bounds.center + up * Mathf.Sqrt(x * x + y * y + z * z);
            return true;
        }

        static readonly Color CommonNameColor = new(.86f, .89f, .94f, .92f);
        static readonly string[] kindNames = new string[Enum.GetValues(typeof(EnemyKind)).Length];
        static bool kindNamesChinese;

        /// <summary>The enemy's name from the text tables (enemy.Grunt and so on), kept until the language changes.</summary>
        static string KindName(EnemyKind kind)
        {
            if (kindNamesChinese != ArenaText.IsChinese) { Array.Clear(kindNames, 0, kindNames.Length); kindNamesChinese = ArenaText.IsChinese; }
            int i = (int)kind;
            if (i < 0 || i >= kindNames.Length) return "";
            return kindNames[i] ??= ArenaText.Text("enemy." + kind);
        }

        /// <summary>A dark edge so the small name stays legible on bright ground.</summary>
        static void AddNameShadow(UnityEngine.UI.Text label)
        {
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>Puts a label on the screen point of world, raised by lift canvas units; false when the point is off screen.</summary>
        bool PlaceWorld(RectTransform label, Vector3 world, Camera cam, float lift = 0f)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            local.y += lift;
            label.position = canvasRect.TransformPoint(local);
            return true;
        }

        /// <summary>Danger marker: pulsing purple, bold and larger than the other edge notices.</summary>
        void PlaceHoleEdge(UnityEngine.UI.Text label, Vector3 world, Camera cam, string value)
        {
            PlaceEdge(label, world, cam, value, false);
            float pulse = .5f + .5f * Mathf.Sin(Time.unscaledTime * holeEdgePulsePerSecond * Mathf.PI * 2f);
            label.color = Color.Lerp(holeEdgeColor, holeEdgeGlow, pulse);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.localScale = Vector3.one * holeEdgeScale * (1f + .08f * pulse);
        }

        void PlaceEdge(UnityEngine.UI.Text label, Vector3 world, Camera cam, string value, bool bright)
        {
            label.fontStyle = FontStyle.Normal;
            label.rectTransform.localScale = Vector3.one;
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z < 0f) { screen.x = Screen.width - screen.x; screen.y = Screen.height - screen.y; }
            float unit = canvasRect.lossyScale.x > 0f ? canvasRect.lossyScale.x : 1f;
            screen.x = Mathf.Clamp(screen.x, 110f * unit, Screen.width - 110f * unit);
            screen.y = Mathf.Clamp(screen.y, edgeBottomInset * unit, Screen.height - edgeTopInset * unit);
            // Notices that point the same way stack instead of drawing over each other.
            float step = 30f * unit;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                bool clash = false;
                foreach (var p in placedEdges) if (Mathf.Abs(p.x - screen.x) < 170f * unit && Mathf.Abs(p.y - screen.y) < step * .9f) { clash = true; break; }
                if (!clash) break;
                screen.y += screen.y > Screen.height * .5f ? -step : step;
            }
            placedEdges.Add(new Vector2(screen.x, screen.y));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            label.rectTransform.position = canvasRect.TransformPoint(local);
            string arrow = Mathf.Abs(screen.x - Screen.width * .5f) > Mathf.Abs(screen.y - Screen.height * .5f)
                ? screen.x < Screen.width * .5f ? "◀ " : "▶ " : screen.y < Screen.height * .5f ? "▼ " : "▲ ";
            if (label.text != arrow + value) label.text = arrow + value;
            label.color = bright ? V2Hud.ElementColors[6] : new Color(.63f, .67f, .76f);
            label.gameObject.SetActive(true);
        }
    }
}
