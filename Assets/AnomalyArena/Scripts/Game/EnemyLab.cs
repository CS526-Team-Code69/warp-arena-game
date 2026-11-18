using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// Enemy lab (scene EnemyLab: the Chinese arena with WaveManager.labMode on): no waves, every enemy kind spawned by hand, so a
    /// designer can edit an EnemyDefinition asset (V2/Data/Enemies) in the Inspector and feel the change at once. The panel on the
    /// right lists the kinds, the switches, the player's output and the live numbers of the selected kind. Keys: F5 spawn, F6 respawn
    /// (clear first), F7 clear, F8 player invulnerable, F9 slow motion, F10 elite, PageUp / PageDown select the kind, [ and ] step the
    /// scale on the player's damage (PlayerOutput), whose rolling DPS and time to kill the panel shows. The Hollow is fought only in the
    /// void trial, so spawning it enters the trial on its platform; clearing kills it and ends the trial as a win.
    /// </summary>
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
        [Tooltip("Scale on everything the player deals to a monster while the lab runs (PlayerOutput); [ and ] step it through OutputScales.")]
        public float playerOutputScale = 1f;

        static readonly float[] OutputScales = { .25f, .5f, .75f, 1f, 1.25f, 1.5f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 24f, 32f, 48f, 64f, 96f };

        // The difficulty plan's expected output on day 0 to 7 and in the Tyrant's fight (work/gold-1117/difficulty-1117): the scale on a
        // Mk1 weapon with no upgrade (the weapons' own x1.25 already in the scene) and the damage a second it should then deal.
        static readonly float[] DayPresetScales = { 1f, 1f, 1.6f, 3.5f, 7f, 13f, 24f, 58f, 93f };
        static readonly float[] DayPresetDps = { 7.5f, 12.5f, 20f, 43.75f, 87.5f, 162.5f, 300f, 725f, 1162.5f };
        /// <summary>The day preset in use (0 to 7, 8 the Tyrant's fight), or -1 when the scale was set by hand.</summary>
        int dayPreset = -1;
        /// <summary>Seconds of game time the panel's DPS averages over.</summary>
        const float DpsWindow = 5f;

        static readonly EnemyKind[] Kinds =
        {
            EnemyKind.Grunt, EnemyKind.Brute, EnemyKind.Skitter, EnemyKind.Splitter, EnemyKind.Spitter, EnemyKind.Warden,
            EnemyKind.Chanter, EnemyKind.Rammer, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Stomper, EnemyKind.Shade, EnemyKind.Hollow,
            EnemyKind.Tyrant,
        };

        static string Name(EnemyKind kind) => kind switch
        {
            EnemyKind.Grunt => ArenaText.Pair("小怪", "Grunt"),
            EnemyKind.Brute => ArenaText.Pair("重兵", "Brute"),
            EnemyKind.Skitter => ArenaText.Pair("疾行虫", "Skitter"),
            EnemyKind.Splitter => ArenaText.Pair("分裂者", "Splitter"),
            EnemyKind.Spitter => ArenaText.Pair("喷吐者", "Spitter"),
            EnemyKind.Warden => ArenaText.Pair("护卫者", "Warden"),
            EnemyKind.Chanter => ArenaText.Pair("吟唱者", "Chanter"),
            EnemyKind.Rammer => ArenaText.Pair("冲撞者", "Rammer"),
            EnemyKind.Bloater => ArenaText.Pair("自爆者", "Bloater"),
            EnemyKind.Spire => ArenaText.Pair("尖塔", "Spire"),
            EnemyKind.Stomper => ArenaText.Pair("撼地者", "Stomper"),
            EnemyKind.Shade => ArenaText.Pair("影虫", "Shade"),
            EnemyKind.Hollow => ArenaText.Pair("纳什先知（虚空试炼）", "Nash Prophet (void trial)"),
            EnemyKind.Tyrant => ArenaText.Pair("暴虐君王", "Tyrant"),
            _ => kind.ToString()
        };

        GameManager gm;
        V2Hud hud;
        Font font;
        Text readout, eliteLabel, rewardsLabel, invulnerableLabel, slowLabel;
        Canvas panelCanvas;
        readonly Dictionary<EnemyKind, Text> kindLabels = new();
        // Localized text is empty until the string tables load, so every label is rewritten each frame from its source.
        readonly List<(Text label, System.Func<string> text)> liveLabels = new();
        Enemy last;

        void Start() => BuildPanel();

        void OnDisable() => PlayerOutput.Scale = 1f;

        void Update()
        {
            gm = GameManager.Instance;
            if (!gm || !gm.waves) return;
            gm.waves.labMode = true;
            // Skip the title, the crate and the weapon card: commands are ignored until the menu input lock releases, so keep asking.
            // The starting weapon is still the player's pick (1 / 2 / 3 on the loadout screen).
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
            PlayerOutput.Scale = Mathf.Max(0f, playerOutputScale);
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
                if (kb.leftBracketKey.wasPressedThisFrame) StepOutput(-1);
                if (kb.rightBracketKey.wasPressedThisFrame) StepOutput(1);
                if (kb.commaKey.wasPressedThisFrame) StepDay(-1);
                if (kb.periodKey.wasPressedThisFrame) StepDay(1);
            }
            RefreshPanel();
        }

        void LateUpdate()
        {
            if (gm && hud && hud.waveLabel && gm.IsCombatActive)
                hud.waveLabel.text = ArenaText.Pair("敌人实验室 · 没有波次", "ENEMY LAB · NO WAVES");
        }

        /// <summary>One enemy of the selected kind, spawnDistance cells ahead of the player (or at a valid ring point when that spot is blocked).
        /// The Hollow enters the void trial instead; nothing else spawns while the trial runs.</summary>
        public Enemy Spawn()
        {
            if (!gm || !gm.player || !gm.waves || !gm.IsCombatActive) return null;
            var player = gm.player;
            var world = WorldMap.Active;
            if (world && world.TrialActive) return null;
            if (selected == EnemyKind.Hollow) return last = world && world.EnterLabTrial(player) ? world.hollow : null;
            Vector3 at = player.Position + Query.Flat(player.AimDirection).normalized * spawnDistance;
            var data = gm.waves.DefinitionOf(selected);
            float body = data ? data.radius : 1f;
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

        /// <summary>The next or previous step of OutputScales from the nearest one; the count starts again so the DPS shows the new scale.</summary>
        void StepOutput(int delta)
        {
            int index = 0;
            for (int i = 1; i < OutputScales.Length; i++)
                if (Mathf.Abs(OutputScales[i] - playerOutputScale) < Mathf.Abs(OutputScales[index] - playerOutputScale)) index = i;
            playerOutputScale = OutputScales[Mathf.Clamp(index + delta, 0, OutputScales.Length - 1)];
            dayPreset = -1;
            PlayerOutput.Reset();
        }

        /// <summary>The next or previous day preset (day 0 to 7, then the Tyrant's fight): its scale, and the count starts again.</summary>
        void StepDay(int delta)
        {
            dayPreset = dayPreset < 0 ? (delta > 0 ? 0 : DayPresetScales.Length - 1) : Mathf.Clamp(dayPreset + delta, 0, DayPresetScales.Length - 1);
            playerOutputScale = DayPresetScales[dayPreset];
            PlayerOutput.Reset();
        }

        static string DayName(int preset) => preset >= 8 ? ArenaText.Pair("暴虐君王之战", "the Tyrant's fight") : ArenaText.Pair($"第 {preset} 天", $"day {preset}");

        void ResetOutput()
        {
            playerOutputScale = 1f;
            dayPreset = -1;
            PlayerOutput.Reset();
        }

        void BuildPanel()
        {
            hud = FindFirstObjectByType<V2Hud>(FindObjectsInactive.Include);
            font = hud && hud.hpLabel ? hud.hpLabel.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvasObject = new GameObject("EnemyLabPanel", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above the HUD's edge arrows; hidden outside combat so card panels stay clear
            panelCanvas = canvas;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = .5f;
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            panel.SetParent(canvasObject.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1f, 1f);
            panel.anchoredPosition = new Vector2(-12f, -64f); // below the HUD's top bar
            panel.sizeDelta = new Vector2(340f, 0f);
            panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, .62f);
            var layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(10, 10, 8, 8);
            layout.spacing = 3f;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Live(MakeLabel(panel, "", 20, FontStyle.Bold), () => ArenaText.Pair("敌人实验室", "ENEMY LAB"));
            Live(MakeLabel(panel, "", 13), () => ArenaText.Pair("F5 生成一只，F6 清场后重新生成，F7 清场，PgUp 和 PgDn 切换怪物，[ 和 ] 调整玩家输出，, 和 . 按天数模拟。",
                "F5 spawns one, F6 clears and spawns again, F7 clears, PgUp and PgDn switch the monster, [ and ] change the player's output, and , and . play as a given day."));
            foreach (var kind in Kinds)
            {
                var chosen = kind;
                kindLabels[chosen] = Live(MakeButton(panel, "", () => { selected = chosen; Clear(); Spawn(); }), () => Name(chosen));
            }
            eliteLabel = MakeButton(panel, "", () => elite = !elite);
            rewardsLabel = MakeButton(panel, "", () => rewards = !rewards);
            invulnerableLabel = MakeButton(panel, "", () => invulnerable = !invulnerable);
            slowLabel = MakeButton(panel, "", () => slowMotion = !slowMotion);
            Live(MakeButton(panel, "", () => Spawn()), () => ArenaText.Pair("再生成一只（F5）", "Spawn one more (F5)"));
            Live(MakeButton(panel, "", Clear), () => ArenaText.Pair("清场（F7）", "Clear (F7)"));
            Live(MakeLabel(panel, "", 15, FontStyle.Bold), () => ArenaText.Pair($"玩家输出 ×{PlayerOutput.Scale:0.##}", $"PLAYER OUTPUT ×{PlayerOutput.Scale:0.##}"));
            Live(MakeButton(panel, "", () => StepOutput(-1)), () => ArenaText.Pair("降低玩家输出（[）", "Lower the player's output ([)"));
            Live(MakeButton(panel, "", () => StepOutput(1)), () => ArenaText.Pair("提高玩家输出（]）", "Raise the player's output (])"));
            Live(MakeButton(panel, "", ResetOutput), () => ArenaText.Pair("恢复原样，重新计算输出", "Back to ×1 and count again"));
            Live(MakeButton(panel, "", () => StepDay(1)), () => dayPreset < 0
                ? ArenaText.Pair("按天数模拟玩家输出（, 和 . 切换）", "Play as a given day's player (, and . switch)")
                : ArenaText.Pair($"正在模拟{DayName(dayPreset)}，点一下换下一天", $"Playing as {DayName(dayPreset)}; click for the next"));
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
            if (eliteLabel) eliteLabel.text = (elite ? "■ " : "□ ") + ArenaText.Pair("精英（F10）· 词条 ", "Elite (F10) · affix ") + ArenaTerms.Element(affix);
            if (rewardsLabel) rewardsLabel.text = (rewards ? "■ " : "□ ") + ArenaText.Pair("掉经验和掉落", "Experience and drops");
            if (invulnerableLabel) invulnerableLabel.text = (invulnerable ? "■ " : "□ ") + ArenaText.Pair("玩家不会死（F8）", "Player cannot die (F8)");
            if (slowLabel) slowLabel.text = (slowMotion ? "■ " : "□ ") + ArenaText.Pair("慢动作（F9）", "Slow motion (F9)");
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
                text.AppendLine(ArenaText.Pair($"生命 {data.hp:0.#} · 移速 {data.speed:0.##}（实际 {walk:0.#}）· 半径 {data.radius:0.##}",
                    $"HP {data.hp:0.#} · speed {data.speed:0.##} (actual {walk:0.#}) · radius {data.radius:0.##}"));
                text.AppendLine(ArenaText.Pair($"伤害 {data.damage:0.#} · 蓄势 {data.windup:0.##} 秒 · 间隔 {data.interval:0.##} 秒",
                    $"damage {data.damage:0.#} · wind-up {data.windup:0.##} s · interval {data.interval:0.##} s"));
                switch (selected)
                {
                    case EnemyKind.Spitter:
                        text.AppendLine(ArenaText.Pair($"保持距离 {data.rangedNear:0.#}–{data.rangedFar:0.#} · 弹速 {data.projectileSpeed:0.#} 格/秒",
                            $"keeps {data.rangedNear:0.#}–{data.rangedFar:0.#} away · spit speed {data.projectileSpeed:0.#} cells/s"));
                        break;
                    case EnemyKind.Rammer:
                        text.AppendLine(ArenaText.Pair($"触发距离 {data.triggerDistance:0.#} · 冲锋 {data.chargeDistance:0.#} 格 · 速度 {data.chargeSpeed:0.#}（实际 {data.chargeSpeed * rush:0.#} 格/秒）· 眩晕 {data.chargeStun:0.##} 秒",
                            $"trigger {data.triggerDistance:0.#} · charge {data.chargeDistance:0.#} cells at {data.chargeSpeed:0.#} (actual {data.chargeSpeed * rush:0.#} cells/s) · stun {data.chargeStun:0.##} s"));
                        break;
                    case EnemyKind.Spire:
                        text.AppendLine(ArenaText.Pair($"哨戒范围 {data.pulseRadius:0.#} · 光波 {data.waveSpeed:0.#} 格/秒 · 激光延迟 {data.laserDelay:0.##} 秒 · 长 {data.laserLength:0.#} · 半宽 {data.laserHalfWidth:0.##}",
                            $"sentry range {data.pulseRadius:0.#} · light wave {data.waveSpeed:0.#} cells/s · laser delay {data.laserDelay:0.##} s · length {data.laserLength:0.#} · half width {data.laserHalfWidth:0.##}"));
                        break;
                    case EnemyKind.Stomper:
                        text.AppendLine(ArenaText.Pair($"触发距离 {data.triggerDistance:0.#} · 扇形长 {data.pulseRadius:0.#} · 角度 {data.coneAngle:0}°",
                            $"trigger {data.triggerDistance:0.#} · cone length {data.pulseRadius:0.#} · width {data.coneAngle:0}°"));
                        break;
                    case EnemyKind.Bloater:
                        text.AppendLine(ArenaText.Pair($"爆炸半径 {data.pulseRadius:0.#}", $"blast radius {data.pulseRadius:0.#}"));
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
            text.AppendLine(ArenaText.Pair($"场上 {living} 只", $"{living} alive"));
            if (nearest)
            {
                string state = nearest.Charging ? ArenaText.Pair("冲锋中", "charging")
                    : nearest.WindingUp ? ArenaText.Pair($"蓄力中 {nearest.WindupSecondsRemaining:0.00} 秒", $"winding up {nearest.WindupSecondsRemaining:0.00} s")
                    : nearest.CooldownRemaining > 0f ? ArenaText.Pair($"冷却 {nearest.CooldownRemaining:0.00} 秒", $"cooldown {nearest.CooldownRemaining:0.00} s")
                    : ArenaText.Pair("接近中", "approaching");
                string kind = Name(nearest.definition ? nearest.definition.kind : selected);
                text.AppendLine(ArenaText.Pair($"最近一只：{kind} · 距离 {Mathf.Sqrt(best):0.0} · {state}", $"nearest: {kind} · {Mathf.Sqrt(best):0.0} away · {state}"));
            }
            float dps = PlayerOutput.Dps(DpsWindow);
            if (dayPreset >= 0)
                text.AppendLine(ArenaText.Pair($"正在模拟{DayName(dayPreset)}的玩家：输出 ×{DayPresetScales[dayPreset]:0.##}，按方案应在每秒 {DayPresetDps[dayPreset]:0.#} 点伤害左右（拿着一把没有强化的 Mk1 武器时）。",
                    $"Playing as the player of {DayName(dayPreset)}: output ×{DayPresetScales[dayPreset]:0.##}, about {DayPresetDps[dayPreset]:0.#} damage a second by the plan (with a Mk1 weapon and no upgrade)."));
            if (gm && gm.waves && gm.runWeapons)
                text.AppendLine(ArenaText.Pair($"全局倍率：怪物伤害 ×{gm.waves.EnemyDamageScale:0.##}，武器伤害 ×{gm.runWeapons.WeaponDamageScale:0.##}，自伤和撞墙 ×{gm.runWeapons.SelfDamageScale:0.##}。",
                    $"Global scales: monster damage ×{gm.waves.EnemyDamageScale:0.##}, weapon damage ×{gm.runWeapons.WeaponDamageScale:0.##}, self-damage and wall slams ×{gm.runWeapons.SelfDamageScale:0.##}."));
            text.AppendLine(ArenaText.Pair($"玩家输出 ×{PlayerOutput.Scale:0.##}：最近 {DpsWindow:0} 秒平均每秒造成 {dps:0.#} 点伤害，{PlayerOutput.Since:0} 秒里一共 {PlayerOutput.Total:0} 点。",
                $"Player output ×{PlayerOutput.Scale:0.##}: {dps:0.#} damage a second over the last {DpsWindow:0} s, {PlayerOutput.Total:0} in all over {PlayerOutput.Since:0} s."));
            var target = nearest ? nearest : last && last.IsAlive ? last : null;
            if (target)
            {
                float hp = target.maxHp;
                string who = Name(target.definition ? target.definition.kind : selected);
                text.AppendLine(dps > 0f
                    ? ArenaText.Pair($"按这个输出，打倒一只{who}（最大生命 {hp:0}）大约要 {hp / dps:0.0} 秒；带护盾的会再久一些。",
                        $"At this output a {who} with {hp:0} max HP falls in about {hp / dps:0.0} s; one with a shield takes a little longer.")
                    : ArenaText.Pair("还没有打出伤害。朝怪物开火以后，这里会显示打倒它要多久。", "No damage yet. Fire at a monster to see how long it takes to fall."));
            }
            text.AppendLine(ArenaText.Pair("要改怪物的数值，在 Project 窗口里选中 V2/Data/Enemies 下的资产，改完按 F6 重新生成。",
                "To tune a monster, select its asset under V2/Data/Enemies in the Project window, edit it, and press F6 to spawn it again."));
            return text.ToString();
        }
    }
}
