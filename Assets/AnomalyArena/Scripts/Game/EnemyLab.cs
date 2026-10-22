using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{

    public sealed class EnemyLab : MonoBehaviour
    {
        [Tooltip("Cells from the player, in the aim direction, where a spawned enemy appears.")] public float spawnDistance = 10f;
        [Tooltip("Spawned enemies give experience and pickups only when this is on.")] public bool rewards;
        [Tooltip("Specials spawn as elites with the affix below.")] public bool elite;
        public ElementKind affix = ElementKind.Fire;
        [Tooltip("The player still takes hits (damage, stun) but cannot die; HP refills when it drops below a fifth.")] public bool invulnerable = true;
        [Tooltip("Slow motion for watching a wind-up.")] public bool slowMotion;
        [Range(.1f, 1f)] public float slowMotionScale = .35f;
        public EnemyKind selected = EnemyKind.Grunt;

        static readonly EnemyKind[] Kinds =
        {
            EnemyKind.Grunt, EnemyKind.Brute, EnemyKind.Skitter, EnemyKind.Splitter, EnemyKind.Spitter, EnemyKind.Warden,
            EnemyKind.Chanter, EnemyKind.Rammer, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Stomper, EnemyKind.Shade, EnemyKind.Tyrant,
        };

        static string Name(EnemyKind kind) => kind switch
        {
            EnemyKind.Grunt => ArenaText.Pair("Grunt","Grunt"),
            EnemyKind.Brute => ArenaText.Pair("Brute","Brute"),
            EnemyKind.Skitter => ArenaText.Pair("Skitter","Skitter"),
            EnemyKind.Splitter => ArenaText.Pair("Splitter","Splitter"),
            EnemyKind.Spitter => ArenaText.Pair("Spitter","Spitter"),
            EnemyKind.Warden => ArenaText.Pair("Warden","Warden"),
            EnemyKind.Chanter => ArenaText.Pair("Chanter","Chanter"),
            EnemyKind.Rammer => ArenaText.Pair("Rammer","Rammer"),
            EnemyKind.Bloater => ArenaText.Pair("Bloater","Bloater"),
            EnemyKind.Spire => ArenaText.Pair("Spire","Spire"),
            EnemyKind.Stomper => ArenaText.Pair("Stomper","Stomper"),
            EnemyKind.Shade => ArenaText.Pair("Shade","Shade"),
            EnemyKind.Tyrant => ArenaText.Pair("Tyrant","Tyrant"),
            _ => kind.ToString()
        };

        GameManager gm;
        V2Hud hud;
        Font font;
        Text readout, eliteLabel, rewardsLabel, invulnerableLabel, slowLabel;
        Canvas panelCanvas;
        readonly Dictionary<EnemyKind, Text> kindLabels = new();

        readonly List<(Text label, System.Func<string> text)> liveLabels = new();
        Enemy last;

        void Start() => BuildPanel();

        void Update()
        {
            gm = GameManager.Instance;
            if (!gm || !gm.waves) return;
            gm.waves.labMode = true;

            if (gm.RunPhase == ArenaUiState.Title) gm.ExecuteUiCommand(ArenaUiCommand.Start);
            else if (gm.RunPhase == ArenaUiState.SupplyDrop) gm.ExecuteUiCommand(ArenaUiCommand.OpenSupply);
            else if (gm.RunPhase == ArenaUiState.WeaponIntro) gm.ExecuteUiCommand(ArenaUiCommand.DismissWeaponIntro);
            else if (gm.RunPhase == ArenaUiState.Cutscene && WorldMap.Active) WorldMap.Active.SkipHoleIntro();
            if (gm.player)
            {
                gm.player.Invulnerable = invulnerable;
                if (invulnerable && gm.player.IsAlive && gm.player.Hp <= gm.player.maxHp * .2f) gm.player.Heal(gm.player.maxHp);
            }
            if (gm.IsCombatActive) Time.timeScale = slowMotion ? slowMotionScale : 1f;
            var kb = Keyboard.current;
            if (kb != null && gm.IsCombatActive)
            {
                if (kb.f5Key.wasPressedThisFrame) Spawn();
                if (kb.f6Key.wasPressedThisFrame) { Clear(); Spawn(); }
                if (kb.f7Key.wasPressedThisFrame) Clear();
                if (kb.f8Key.wasPressedThisFrame) invulnerable = !invulnerable;
                if (kb.f9Key.wasPressedThisFrame) slowMotion = !slowMotion;
                if (kb.f10Key.wasPressedThisFrame) elite = !elite;
                if (kb.pageUpKey.wasPressedThisFrame) Step(-1);
                if (kb.pageDownKey.wasPressedThisFrame) Step(1);
            }
            RefreshPanel();
        }

        void LateUpdate()
        {
            if (gm && hud && hud.waveLabel && gm.IsCombatActive)
                hud.waveLabel.text = ArenaText.Pair("ENEMY LAB · NO WAVES","ENEMY LAB · NO WAVES");
        }

        public Enemy Spawn()
        {
            if (!gm || !gm.player || !gm.waves || !gm.IsCombatActive) return null;
            var player = gm.player;
            Vector3 at = player.Position + Query.Flat(player.AimDirection).normalized * spawnDistance;
            var data = gm.waves.DefinitionOf(selected);
            float body = data ? data.radius : 1f;
            var world = WorldMap.Active;
            if (world)
            {
                at = world.ClampPoint(at, body);
                if (!world.IsWalkable(at, body, true) && data && !gm.waves.TryFindV2SpawnPoint(data, out at)) return null;
            }
            last = selected == EnemyKind.Tyrant ? gm.waves.SpawnLabTyrant(at) : gm.waves.SpawnLab(selected, at, elite, affix);
            if (last) last.SuppressRewards = !rewards;
            return last;
        }

        public void Clear()
        {
            if (gm && gm.waves) gm.waves.ClearLab();
            last = null;
        }

        void Step(int delta)
        {
            int index = System.Array.IndexOf(Kinds, selected);
            selected = Kinds[((index < 0 ? 0 : index) + delta + Kinds.Length) % Kinds.Length];
        }

        void BuildPanel()
        {
            hud = FindFirstObjectByType<V2Hud>(FindObjectsInactive.Include);
            font = hud && hud.hpLabel ? hud.hpLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("EnemyLabPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;                                                                                
            panelCanvas = canvas;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-12f, -64f);                           
            panel.sizeDelta = new Vector2(340f, 0f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, .62f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 3f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Live(MakeLabel(panel, "", 20, FontStyle.Bold), () => ArenaText.Pair("ENEMY LAB","ENEMY LAB"));
            Live(MakeLabel(panel, "", 13), () => ArenaText.Pair("F5 spawn · F6 respawn · F7 clear · PgUp / PgDn pick","F5 spawn · F6 respawn · F7 clear · PgUp / PgDn pick"));
            foreach (var kind in Kinds)
            {
                var chosen = kind;
                kindLabels[chosen] = Live(MakeButton(panel, "", () => { selected = chosen; Clear(); Spawn(); }), () => Name(chosen));
            }
            eliteLabel = MakeButton(panel, "", () => elite = !elite);
            rewardsLabel = MakeButton(panel, "", () => rewards = !rewards);
            invulnerableLabel = MakeButton(panel, "", () => invulnerable = !invulnerable);
            slowLabel = MakeButton(panel, "", () => slowMotion = !slowMotion);
            Live(MakeButton(panel, "", () => Spawn()), () => ArenaText.Pair("Spawn one more (F5)","Spawn one more (F5)"));
            Live(MakeButton(panel, "", Clear), () => ArenaText.Pair("Clear (F7)","Clear (F7)"));
            readout = MakeLabel(panel, "", 13);
        }

        Text Live(Text label, System.Func<string> text)
        {
            liveLabels.Add((label, text));
            return label;
        }

        Text MakeLabel(Transform parent, string text, int size, FontStyle style = FontStyle.Normal)
        {
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            label.transform.SetParent(parent, false);
            label.font = font;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = Color.white;
            label.text = text;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        Text MakeButton(Transform parent, string text, UnityEngine.Events.UnityAction onClick)
        {
            var button = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            button.transform.SetParent(parent, false);
            button.GetComponent<Image>().color = new Color(1f, 1f, 1f, .12f);
            button.GetComponent<LayoutElement>().preferredHeight = 24f;
            button.GetComponent<Button>().onClick.AddListener(onClick);
            var label = MakeLabel(button.transform, text, 14);
            var rect = label.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(8f, 0f);
            rect.offsetMax = new Vector2(-8f, 0f);
            label.alignment = TextAnchor.MiddleLeft;
            return label;
        }

        void RefreshPanel()
        {
            if (panelCanvas) panelCanvas.enabled = gm && gm.IsCombatActive;
            foreach (var (label, text) in liveLabels) if (label) label.text = text();
            foreach (var pair in kindLabels) pair.Value.color = pair.Key == selected ? new Color(1f, .85f, .35f) : Color.white;
            if (eliteLabel) eliteLabel.text = (elite ? "■ " : "□ ") + ArenaText.Pair("Elite (F10) · affix ","Elite (F10) · affix ") + ArenaTerms.Element(affix);
            if (rewardsLabel) rewardsLabel.text = (rewards ? "■ " : "□ ") + ArenaText.Pair("Experience and drops","Experience and drops");
            if (invulnerableLabel) invulnerableLabel.text = (invulnerable ? "■ " : "□ ") + ArenaText.Pair("Player cannot die (F8)","Player cannot die (F8)");
            if (slowLabel) slowLabel.text = (slowMotion ? "■ " : "□ ") + ArenaText.Pair("Slow motion (F9)","Slow motion (F9)");
            if (readout) readout.text = Readout();
        }

        string Readout()
        {
            var text = new StringBuilder();
            var data = gm && gm.waves ? gm.waves.DefinitionOf(selected) : null;
            if (!data && last && last.definition && last.definition.kind == selected) data = last.definition;
            text.AppendLine(Name(selected) + (data ? "  (" + data.name + ".asset)" : ""));
            if (data)
            {
                float walk = data.speed * gm.waves.EnemySpeedFactor, rush = gm.waves.MovementSpeedScale;
                text.AppendLine(ArenaText.Pair(                    $"HP {data.hp:0.#} · speed {data.speed:0.##} (actual {walk:0.#}) · radius {data.radius:0.##}",                    $"HP {data.hp:0.#} · speed {data.speed:0.##} (actual {walk:0.#}) · radius {data.radius:0.##}"));
                text.AppendLine(ArenaText.Pair(                    $"damage {data.damage:0.#} · wind-up {data.windup:0.##} s · interval {data.interval:0.##} s",                    $"damage {data.damage:0.#} · wind-up {data.windup:0.##} s · interval {data.interval:0.##} s"));
                switch (selected)
                {
                    case EnemyKind.Spitter:
                        text.AppendLine(ArenaText.Pair(                            $"keeps {data.rangedNear:0.#}–{data.rangedFar:0.#} away · spit speed {data.projectileSpeed:0.#} cells/s",                            $"keeps {data.rangedNear:0.#}–{data.rangedFar:0.#} away · spit speed {data.projectileSpeed:0.#} cells/s"));
                        break;
                    case EnemyKind.Rammer:
                        text.AppendLine(ArenaText.Pair(                            $"trigger {data.triggerDistance:0.#} · charge {data.chargeDistance:0.#} cells at {data.chargeSpeed:0.#} (actual {data.chargeSpeed * rush:0.#} cells/s) · stun {data.chargeStun:0.##} s",                            $"trigger {data.triggerDistance:0.#} · charge {data.chargeDistance:0.#} cells at {data.chargeSpeed:0.#} (actual {data.chargeSpeed * rush:0.#} cells/s) · stun {data.chargeStun:0.##} s"));
                        break;
                    case EnemyKind.Spire:
                        text.AppendLine(ArenaText.Pair(                            $"pulse radius {data.pulseRadius:0.#} · laser delay {data.laserDelay:0.##} s · length {data.laserLength:0.#} · half width {data.laserHalfWidth:0.##}",                            $"pulse radius {data.pulseRadius:0.#} · laser delay {data.laserDelay:0.##} s · length {data.laserLength:0.#} · half width {data.laserHalfWidth:0.##}"));
                        break;
                    case EnemyKind.Stomper:
                        text.AppendLine(ArenaText.Pair(                            $"trigger {data.triggerDistance:0.#} · slam radius {data.pulseRadius:0.#}",                            $"trigger {data.triggerDistance:0.#} · slam radius {data.pulseRadius:0.#}"));
                        break;
                    case EnemyKind.Bloater:
                        text.AppendLine(ArenaText.Pair($"blast radius {data.pulseRadius:0.#}",$"blast radius {data.pulseRadius:0.#}"));
                        break;
                }
            }
            int living = 0;
            Enemy nearest = null;
            float best = float.MaxValue;
            if (gm && gm.waves && gm.player)
                foreach (var enemy in gm.waves.Alive)
                {
                    if (!enemy || !enemy.IsAlive) continue;
                    living++;
                    float d = Query.Flat(enemy.Position - gm.player.Position).sqrMagnitude;
                    if (d < best) { best = d; nearest = enemy; }
                }
            text.AppendLine(ArenaText.Pair($"{living} alive",$"{living} alive"));
            if (nearest)
            {
                string state = nearest.Charging ? ArenaText.Pair("charging","charging")
                    : nearest.WindingUp ? ArenaText.Pair($"winding up {nearest.WindupSecondsRemaining:0.00} s",$"winding up {nearest.WindupSecondsRemaining:0.00} s")
                    : nearest.CooldownRemaining > 0f ? ArenaText.Pair($"cooldown {nearest.CooldownRemaining:0.00} s",$"cooldown {nearest.CooldownRemaining:0.00} s")
                    : ArenaText.Pair("approaching","approaching");
                string kind = Name(nearest.definition ? nearest.definition.kind : selected);
                text.AppendLine(ArenaText.Pair($"nearest: {kind} · {Mathf.Sqrt(best):0.0} away · {state}",$"nearest: {kind} · {Mathf.Sqrt(best):0.0} away · {state}"));
            }
            text.AppendLine(ArenaText.Pair(                "Tune: select the asset under V2/Data/Enemies, edit it, press F6 to respawn.",                "Tune: select the asset under V2/Data/Enemies, edit it, press F6 to respawn."));
            return text.ToString();
        }
    }
}
