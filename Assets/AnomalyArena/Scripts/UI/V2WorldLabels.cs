using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                         
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
        Enemy[] bindings;
        float refresh;
        RectTransform canvasRect;
        readonly List<Vector2> placedEdges = new();
        static readonly Color XpEdgeColor = new(.45f, .8f, 1f);
                                                                                                            
        UnityEngine.UI.Image[] executeTicks;
        Color[] hpColors;
        bool[] executeBelow;

        void Awake()
        {
            bindings = new Enemy[enemyLabels.Length];
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
                if (show) show = PlaceWorld(view.root, enemy.HeadPoint(gm.cam), gm.cam);
                if (view.root.gameObject.activeSelf != show) view.root.gameObject.SetActive(show);
            }
            int edges = 0;
            placedEdges.Clear();
            var world = WorldMap.Active;
            if (world && world.TrialActive) PlaceTrialPortalLabel(world, gm);
            else if (world && world.holes != null)
            {
                for (int i = 0; i < Mathf.Min(holeLabels.Length, world.holes.Length); i++)
                {
                    var hole = world.holes[i]; var label = holeLabels[i];
                    bool show = hole && hole.Phase != HolePhase.Gone && !world.TrialActive;
                    if (!show) { label.gameObject.SetActive(false); continue; }
                                                                                                                                
                    bool canEnter = world.CanEnterPortal(gm.player, hole);
                    string text = hole.Phase == HolePhase.Portal ? ArenaText.Text(canEnter ? "ui.hole.enter" : "ui.hole.portal") + "\n" + ArenaText.Text("ui.hole.remaining", hole.WavesLeft)
                        : world.CanFeed && hole.Phase == HolePhase.Active ? $"{hole.Feeds} / {hole.feedsToOpen}" : "";
                    if (label.text != text) label.text = text;
                    bool visible = PlaceWorld(label.rectTransform, hole.Centre + gm.cam.transform.up * (hole.size * .5f + 1f), gm.cam);
                    label.gameObject.SetActive(visible && text.Length > 0);
                    if (Offscreen(hole.Centre, gm.cam) && edges < edgeLabels.Length)
                    {
                        float distance = gm.player ? Query.Flat(hole.Centre - gm.player.Position).magnitude : 0f;
                        if (hole.Phase == HolePhase.Portal)
                            PlaceEdge(edgeLabels[edges++], hole.Centre, gm.cam, ArenaText.Text("ui.hole.offscreen", hole.WavesLeft), true);
                        else
                            PlaceHoleEdge(edgeLabels[edges++], hole.Centre, gm.cam, ArenaText.Pair("BLACK HOLE","BLACK HOLE") + $"  {distance:0}m");
                    }
                }
            }
                                                                                            
                                                                                       
            if (gm.waves.preparedMarkers != null)
                foreach (var marker in gm.waves.preparedMarkers)
                {
                    if (!marker || !marker.gameObject.activeInHierarchy || !marker.definition || marker.definition.kind != EnemyKind.Shade
                        || edges >= edgeLabels.Length || !Offscreen(marker.transform.position, gm.cam)) continue;
                    PlaceEdge(edgeLabels[edges++], marker.transform.position, gm.cam, ArenaText.Text("ui.shade.incoming"), true);
                }
                                                                                                                                         
                                                                                                                                     
            var orbs = OrbPool.Instance;
            bool orbLabelOn = false;
            if (orbs && gm.player && gm.IsCombatActive)
            {
                var cam = gm.cam;
                var nearest = orbs.NearestXp(gm.player.Position);
                if (orbs.XpUncollected && nearest && orbHintLabel)
                {
                    string text = ArenaText.Pair("XP · PICK UP TO LEVEL UP","XP · PICK UP TO LEVEL UP");
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
                            PlaceEdge(edgeLabels[edges++], away.Position, cam, ArenaText.Pair($"XP ×{onGround}",$"XP ×{onGround}"), true);
                            edgeLabels[edges - 1].color = XpEdgeColor;
                        }
                    }
                }
            }
            if (orbHintLabel && orbHintLabel.gameObject.activeSelf != orbLabelOn) orbHintLabel.gameObject.SetActive(orbLabelOn);
            for (int i = edges; i < edgeLabels.Length; i++) edgeLabels[i].gameObject.SetActive(false);
        }

                                                                                                                                              
                                                                                                                                                     
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
                    string text = ArenaText.Pair("E · TRAVEL","E · TRAVEL");
                    if (label.text != text) label.text = text;
                    show = PlaceWorld(label.rectTransform, portal.Centre + gm.cam.transform.up * (portal.size * .5f + 1f), gm.cam);
                }

                if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
            }
        }

                                                                                                                                                                        
        static void NarrowBars(EnemyLabel view, Enemy enemy)
        {
            float width = enemy.IsBoss || enemy.Elite ? 1f : Mathf.Clamp(enemy.radius * .85f, .42f, .78f);
            NarrowBar(view.hp ? view.hp.rectTransform.parent as RectTransform : null, width);
            NarrowBar(view.shield ? view.shield.rectTransform.parent as RectTransform : null, width);
        }

                                                                                     
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
                bindings[index] = enemy;
                ShowExecuteLine(index, enemy, executeLine);
                var view = enemyLabels[index++]; var status = ElementStatus.For(enemy);
                view.hp.fillAmount = enemy.maxHp > 0f ? Mathf.Clamp01(enemy.Hp / enemy.maxHp) : 0f;
                view.shield.fillAmount = status && enemy.maxHp > 0f ? Mathf.Clamp01(status.Shield / enemy.maxHp) : 0f;
                view.shield.gameObject.SetActive(status && status.Shield > 0f);
                                                                                                                                               
                                                                                                                             
                string kind = enemy.definition ? KindName(enemy.definition.kind) : "";
                string name = enemy.IsBoss ? enemy.definition.kind == EnemyKind.Hollow ? ArenaText.Pair("THE NASH PROPHET","THE NASH PROPHET") : ArenaText.Pair("THE TYRANT KING","THE TYRANT KING")
                    : enemy.Elite ? kind + " · " + ArenaText.Text("ui.elite") + (enemy.Affix != ElementKind.None ? " · " + ArenaTerms.Element(enemy.Affix) : "") : kind;
                Color nameColor = enemy.IsBoss || enemy.Elite ? Color.white : CommonNameColor;
                if (view.name.color != nameColor) view.name.color = nameColor;
                if (enemy.Chanted) name = (name.Length > 0 ? name + " · " : "") + ArenaText.Pair("CHANTED","CHANTED");
                                                                       
                if (weapons && weapons.IsExecuteMarked(enemy)) name = (name.Length > 0 ? name + " · " : "") + ArenaText.Pair("MARKED","MARKED");
                                                                                                     
                if (enemy.ProphetWarded) name += " · " + ArenaText.Pair("INVULNERABLE","INVULNERABLE");
                if (view.name.text != name) view.name.text = name;
                V2Hud.SetElementIcon(view.affix, art, (int)enemy.Affix - 1);
                for (int j = 0; j < 4; j++)
                {
                    float count = status ? j switch { 0 => status.Fire, 1 => status.Poison, 2 => status.Frost, _ => status.Lightning } : 0f;
                                                                                                                                  
                    bool burning = j == 0 && status && status.Burning;
                    bool on = count >= 1f || burning || (j == 2 && status && status.Frozen) || (j == 3 && status && status.Paralysed);
                    V2Hud.SetElementIcon(view.statusIcons[j], art, on ? j : -1);
                    view.statusCounts[j].gameObject.SetActive(on);
                    string value = j == 2 && status && status.Frozen ? ArenaText.Text("ui.freeze.short") : j == 3 && status && status.Paralysed ? ArenaText.Text("ui.paralysis.short")
                        : burning ? Mathf.Max(1, Mathf.RoundToInt(count)).ToString() : Mathf.FloorToInt(count).ToString();
                    if (on && view.statusCounts[j].text != value) view.statusCounts[j].text = value;
                }
            }
            for (int i = index; i < bindings.Length; i++) bindings[i] = null;
        }

        static readonly Color CommonNameColor = new(.86f, .89f, .94f, .92f);
        static readonly string[] kindNames = new string[Enum.GetValues(typeof(EnemyKind)).Length];
        static bool kindNamesChinese;

                                                                                                                              
        static string KindName(EnemyKind kind)
        {
            if (kindNamesChinese != ArenaText.IsChinese) { Array.Clear(kindNames, 0, kindNames.Length); kindNamesChinese = ArenaText.IsChinese; }
            int i = (int)kind;
            if (i < 0 || i >= kindNames.Length) return "";
            return kindNames[i] ??= ArenaText.Text("enemy." + kind);
        }

                                                                                            
        static void AddNameShadow(UnityEngine.UI.Text label)
        {
            var shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        bool PlaceWorld(RectTransform label, Vector3 world, Camera cam)
        {
            Vector3 screen = cam.WorldToScreenPoint(world);
            if (screen.z <= 0f || screen.x < 0f || screen.y < 0f || screen.x > Screen.width || screen.y > Screen.height) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out var local);
            label.position = canvasRect.TransformPoint(local);
            return true;
        }

                                                                                                          
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
