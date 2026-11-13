using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    public enum CampaignRoom { Control, Armament, Energy, Ship }

    public enum CampaignStage { Off, TitleCard, Loadout, Tutorial, Day, Lull, Dusk, Night, Base, Build, Story, FinalBoss, Core, Ending }

    /// <summary>
    /// The V2 day campaign (the V2 design doc, Docs/zh/V2改版.docx): waves become days. In the morning (CampaignStage.Day) the player fights
    /// the day's wave of the wave table (one wave a day since 2026-11-06; CampaignRules.wavesPerDay still allows more) and guards the base;
    /// kills drop alien energy only. The afternoon (CampaignStage.Dusk) is rest and mining time: the day's limited alloy ore surfaces on the
    /// campaign's large map, part of it a trail that leads toward the black hole. The night cannot be fought: lamps light the ground for
    /// mining, mostly dark matter, while shadow monsters, more of them where the morning's kills were, hunt the player in the dark.
    /// Since 2026-11-06 the afternoon and the night have no clock (CampaignRules.openEndedEvenings): the base panel at the door leads into
    /// the night and on to the next day. In the base the three currencies buy rooms, repairs, the ship and the upgrades the rooms sell, each
    /// taking effect at once. A full fuel tank fails to ignite, the Tyrant comes, its dark matter core lets the ship jump home. Day 0 teaches
    /// all of it on the way from the crash site to the base. The run starts with a plain pistol; every weapon bought at the Armament Bay is
    /// an anomaly weapon.
    /// It sits beside the GameManager and stays dormant until the title's Start opens a campaign run (ArenaUiCommand.StartCampaign); the
    /// classic 15-wave run is untouched. Numbers: CampaignRules; art: CampaignArt (whitebox stand-ins wherever it is empty).
    /// </summary>
    [DefaultExecutionOrder(150)]
    public sealed partial class V2Campaign : MonoBehaviour
    {
        public CampaignRules rules;
        public CampaignArt art;
        // Since 2026-11-06 the title has no button for the classic 15-wave run: it stays in the code for the tests (ArenaUiCommand.Start),
        // and only monster attacks hurt the base (player shots stop at its walls and never damage it).
        [Tooltip("The title's Start button opens the V2 campaign. The classic 15-wave run has no button; tests start it with ArenaUiCommand.Start.")]
        public bool startButtonOpensCampaign = true;

        public static V2Campaign Active { get; private set; }
        public bool Running { get; private set; }
        public CampaignStage Stage { get; private set; }
        public int Day { get; private set; }
        public int WaveOfDay { get; private set; }
        public bool Overtime { get; private set; }
        public bool BaseDestroyed { get; private set; }
        public bool EndingDone { get; private set; }
        public string PromptText => prompt;
        public int LampStock => lampStock;
        /// <summary>The afternoon and the night have no clock (CampaignRules.openEndedEvenings, the rule since 2026-11-06).</summary>
        public bool EveningsOpenEnded => rules && rules.openEndedEvenings;
        /// <summary>Seconds the current stage has run (its clock waits where the stage waits) and how long it is planned to last; the
        /// story layer's day dial reads them.</summary>
        public float StageClock => stageClock;
        public float StageLength => stageLength;

        GameManager gm;
        V2CampaignUi ui;
        float stageClock, stageLength;
        bool buildAllowed;
        // The campaign's own map is in place (SetupCampaignMap ran and RestoreClassicMap has not yet).
        bool mapSetUp;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot() { Active = null; ClearHooks(); }

        void Awake()
        {
            gm = GetComponent<GameManager>();
        }

        void Start()
        {
            if (!gm || !rules) return;
            // The campaign's sound effects (2026-11-10): silent until a campaign runs and wherever CampaignArt.sounds has no clip.
            if (!GetComponent<CampaignAudio>()) gameObject.AddComponent<CampaignAudio>();
            ui = V2CampaignUi.Create(this);
            if (startButtonOpensCampaign) ui.RouteTitleStart();
        }

        void OnDestroy()
        {
            GiveMapBack();
            if (Active == this) { Active = null; ClearHooks(); }
            if (ui) Destroy(ui.gameObject);
            if (cutscene) Destroy(cutscene.gameObject);
        }

        static void ClearHooks()
        {
            Enemy.RaidSpot = null;
            Enemy.RaidStrike = null;
            Enemy.RaidAggroRadius = 0f;
            WaveManager.SpawnPointOverride = null;
            RunWeapons.AxisAllowed = null;
            RunWeapons.ArmoryRule = null;
            // The rooms' hooks and the tutorial's input gate (since 2026-11-10): none outlives the campaign that set it.
            RunWeapons.MkDamageRule = null;
            RunWeapons.NewWeaponMk = null;
            RunWeapons.DamageBonus = null;
            SkillLoadout.CooldownScale = SkillLoadout.CostScale = SkillLoadout.DamageScale = null;
            ElementStatus.ExtraPenetration = null;
            ElementStatus.StatusDamageFloor = null;
            PlayerController.HitShare = null;
            PlayerController.InputGate = null;
            ClearDayHooks(); // the numbers by day (V2Campaign.Days.cs, since 2026-11-10)
        }

        /// <summary>The classic run gets its own map back once the campaign is over (or its object goes away).</summary>
        void GiveMapBack()
        {
            if (!mapSetUp) return;
            mapSetUp = false;
            RestoreClassicMap();
        }

        // ───── Entry from the title ─────

        /// <summary>ArenaUiCommand.StartCampaign on the title: a black "Day 0" card, then the starting pistol and the walk from the crash site.</summary>
        public void Begin()
        {
            if (!StartRun()) return;
            gm.ClaimSaveSlot(SaveMode.Story); // a slot the title bound for this run takes its saves (ArenaSaves)
            PlacePlayer(CrashSite);
            Stage = CampaignStage.TitleCard; stageClock = 0f; stageLength = 3.2f;
            ui.ShowTitleCard(ArenaText.Pair("第 0 天", "DAY 0"), ArenaText.Pair("坠落之后", "After the fall"), stageLength);
            // The black card's own sting (2026-11-10); the wave sound stands in while it is not installed.
            if (!CampaignAudio.Play(CampaignSfx.DayTitleStinger)) V3WorldAudio.Instance?.Play(ArenaWorldCue.WaveEnd, gm.player.Position, .7f);
            gm.CampaignHoldTitle();
        }

        /// <summary>What every run starts from, a new one (Begin) or a continued save (BeginFromSave, V2Campaign.Save.cs): the run's state
        /// made new, the hooks, the campaign's map (laid from a continued save's seed when one is pinned) and the base and the ship on it.
        /// False when no run can start.</summary>
        bool StartRun()
        {
            if (Running || !gm || !rules) return false;
            Active = this; Running = true; Day = 0; WaveOfDay = 0; Overtime = false; BaseDestroyed = false; EndingDone = false;
            armoryRoundsLeft = 0; ArmoryRoundActive = false; armoryMode = ArmoryMode.Rooms; activeUpgrade = null;
            ResetEconomy(); ResetBase(); ResetNight();
            InstallHooks();
            // Since 2026-11-06 the campaign plays on its own large map with terrain and a far black hole (V2Campaign.Map.cs). It is put in
            // place before the base and the ship are built on it.
            SetupCampaignMap(); mapSetUp = true;
            trailTarget = HoleCentre; // the afternoon's ore trail heads here (V2Campaign.Night.cs PlanTrail)
            BuildWorld();
            return true;
        }

        void InstallHooks()
        {
            Enemy.RaidSpot = RaidSpotFor;
            Enemy.RaidCentre = BaseCentre;
            Enemy.RaidStrike = OnRaidStrike;
            // Since 2026-11-06 every day monster raids the base, but one that comes within this radius of the player fights the player.
            Enemy.RaidAggroRadius = rules.raiderAggroRadius;
            // ...and the morning's monsters are born on a ring round the base instead of appearing around the player.
            WaveManager.SpawnPointOverride = CampaignSpawnPoint;
            RunWeapons.AxisAllowed = axis => axis switch
            {
                UpgradeAxis.Tame => TameCards,
                UpgradeAxis.Twist => TwistCards,
                UpgradeAxis.Infusion => InfusionCards,
                // The other axes: none during a purchase (it allows only its own kind); outside one (since 2026-11-06 the void trial's
                // extra level, drawn as in the classic run) they may come.
                _ => !Running || activeUpgrade == null
            };
            RunWeapons.ArmoryRule = () => armoryMode switch
            {
                ArmoryMode.UpgradesOnly => (false, true),
                ArmoryMode.NewWeaponsOnly => (true, false),
                _ => (ArmoryNewWeapons, ArmoryUpgrades)
            };
            gm.progression.StatCardsAllowed = () => StatCards;
            // Since 2026-11-06 every upgrade is bought in its room (the V2 doc) and takes effect the moment it is bought: armor levels and
            // weapon damage at the Armament Bay, new weapons, Tame and Twist there from levels 2 and 3, body, skills and Infusion at the
            // Energy Lab. Kills drop materials and no XP.
            gm.progression.LevelingAllowed = null;
            gm.progression.XpIgnored = () => Running;
            gm.waves.EnemySpawned -= OnEnemySpawned; gm.waves.EnemySpawned += OnEnemySpawned;
            gm.waves.EnemyEliminated -= OnEnemyEliminated; gm.waves.EnemyEliminated += OnEnemyEliminated;
            InstallDayHooks(); // each day's monsters, HP, skill scale and payoffs (V2Campaign.Days.cs, since 2026-11-10)
        }

        /// <summary>The starting weapon is in hand: the run is on and Day 0 begins at the crash site.</summary>
        public void AfterLoadout()
        {
            gm.CampaignEnterPlay();
            BeginTutorial();
        }

        /// <summary>Since 2026-11-06 the run starts with the pistol alone, in the first hand: plain single shots, no anomaly and no upgrades.
        /// The second hand stays empty for the first anomaly weapon bought at the Armament Bay (a new weapon or the rocket launcher).</summary>
        void BeginWithStartingWeapons()
        {
            Stage = CampaignStage.Loadout;
            gm.runWeapons.GiveToHand(0, WeaponForm.Pistol);
            AfterLoadout();
        }

        // ───── What the rooms unlock (the V2 doc's level table) ─────

        bool Works(CampaignRoom room, int level) => rooms[(int)room].level >= level && !rooms[(int)room].damaged;
        /// <summary>Armament level 1: the armor upgrade (the V2 doc's "玩家升级") — since 2026-11-06 the player's levels are bought there
        /// (TryBuyPlayerLevel).</summary>
        public bool PlayerLevelsForSale => Running && Works(CampaignRoom.Armament, 1);
        public bool ArmoryUpgrades => !Running || Works(CampaignRoom.Armament, 1);
        public bool ArmoryNewWeapons => !Running || Works(CampaignRoom.Armament, 2);
        // The upgrade kinds the draws may offer. During a purchase (TryBuyUpgrade) only the bought kind; otherwise every kind the rooms
        // have unlocked (an upgrade from elsewhere, such as the void trial).
        public bool TameCards => KindOpen(CampaignUpgrade.Tame, CampaignRoom.Armament, 2);
        public bool TwistCards => KindOpen(CampaignUpgrade.Twist, CampaignRoom.Armament, 3);
        public bool StatCards => KindOpen(CampaignUpgrade.Stat, CampaignRoom.Energy, 1);
        public bool SkillCards => KindOpen(CampaignUpgrade.Skill, CampaignRoom.Energy, 2);
        public bool InfusionCards => KindOpen(CampaignUpgrade.Infusion, CampaignRoom.Energy, 3);
        bool KindOpen(CampaignUpgrade kind, CampaignRoom room, int level) => !Running || (activeUpgrade.HasValue ? activeUpgrade.Value == kind : Works(room, level));

        /// <summary>The upgrade being bought right now (TryBuyUpgrade), or null.</summary>
        CampaignUpgrade? activeUpgrade;
        /// <summary>What RollArmory may offer: the rooms' rule, or for one weapon purchase only Mk upgrades or only new weapons.</summary>
        enum ArmoryMode { Rooms, UpgradesOnly, NewWeaponsOnly }
        ArmoryMode armoryMode;
        public bool LampsCraftable => !rooms[(int)CampaignRoom.Ship].damaged;

        // ───── Frame ─────

        void Update()
        {
            if (!Running || !gm) return;
            if (gm.State is GameState.Won or GameState.Lost) { if (Stage != CampaignStage.Off) Finish(); return; }
            // A yes / no question (V2CampaignUi.Confirm) holds the run still: no stage reads a key or moves on until it is answered.
            if (ui.ConfirmOpen) { ui.Refresh(); return; }
            float dt = Time.deltaTime, unscaled = Time.unscaledDeltaTime;
            switch (Stage)
            {
                case CampaignStage.TitleCard:
                    stageClock += unscaled;
                    if (stageClock >= stageLength) BeginWithStartingWeapons();
                    break;
                case CampaignStage.Tutorial: TickTutorial(dt); break;
                case CampaignStage.Day: TickDay(dt); break;
                case CampaignStage.Lull:
                    TickDay(dt);
                    stageClock += dt;
                    if (stageClock >= stageLength) { Stage = CampaignStage.Day; WaveOfDay++; gm.CampaignNextWave(); }
                    break;
                case CampaignStage.Dusk: TickDusk(dt); break;
                case CampaignStage.Night: TickNight(dt); break;
                case CampaignStage.FinalBoss: TickPickups(dt); break;
                case CampaignStage.Core: TickCore(dt); break;
            }
            TickBaseVisuals(unscaled);
            ui.Refresh();
        }

        void LateUpdate()
        {
            if (!Running) return;
            UpdateDarkness();
        }

        void Finish()
        {
            CampaignEvents.RaiseEnded(gm.State == GameState.Won ? "won" : BaseDestroyed ? "base_destroyed" : "died_by_day", Day);
            Stage = CampaignStage.Off;
            ClearNight(true);
            darkness?.SetActive(false);
            ui.HideAll();
            GiveMapBack();
        }

        // ───── Morning ─────

        void TickDay(float dt)
        {
            TickPickups(dt);
            TickDayInteraction(dt);
        }

        /// <summary>BeginNextV2Wave in a campaign run: the first wave of a new day after the dawn, or the second wave after the lull.</summary>
        public void StartNextWave()
        {
            var waves = gm.waves;
            if (Stage == CampaignStage.Build)
            {
                Day++; WaveOfDay = 0; Stage = CampaignStage.Day;
                if (Day > rules.days) Overtime = true;
                CampaignEvents.RaiseDayStarted(Day, Overtime);
                regionKills.Clear(); regionKindKills.Clear();
                BeginDayNumbers(); // the energy bar fills, the day's kill energy is ready (V2Campaign.Days.cs)
                ApplyDaylight();
                Say(Line.S07);
                // The first morning says how the monsters behave; later mornings only say what to do (since 2026-11-10).
                Objective(Day <= 1
                    ? ArenaText.Pair("守住基地。怪物会从四面八方涌向基地，离你近的会先冲着你来", "Hold the base. Monsters close in on it from every side, and any that get near you will come for you first")
                    : ArenaText.Pair("守住基地", "Hold the base"), null);
            }
            // The table's wave for today (rules.WaveNumber: since 2026-11-06 day d fights wave 2d, so day 7 is wave 14 before the Tyrant);
            // overtime repeats the last day's wave.
            int index = Mathf.Clamp(rules.WaveNumber(Overtime ? rules.days : Day, WaveOfDay) - 1, 0, waves.WaveCount - 1);
            waves.BeginWaveAt(index);
            string affixes = waves.WaveAffixA != ElementKind.None ? $"{ArenaTerms.Element(waves.WaveAffixA)} · {ArenaTerms.Element(waves.WaveAffixB)}" : ArenaText.Pair("守住基地，也保护好自己", "Hold the base and stay alive");
            gm.hud?.Banner(Dot(Overtime ? ArenaText.Pair($"第 {Day} 天（超出预定天数）", $"DAY {Day} (OVERTIME)") : ArenaText.Pair($"第 {Day} 天", $"DAY {Day}"), ArenaText.Pair("上午", "MORNING"), AttackText(WaveOfDay + 1)), affixes);
        }

        /// <summary>"Attack k of n" for days of more than one wave; empty when a day is a single wave.</summary>
        string AttackText(int k) => rules.wavesPerDay > 1 ? ArenaText.Pair($"敌袭 {k}/{rules.wavesPerDay}", $"ATTACK {k}/{rules.wavesPerDay}") : "";

        /// <summary>The parts that are not empty, joined with " · ".</summary>
        static string Dot(params string[] parts)
        {
            var text = new System.Text.StringBuilder();
            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (text.Length > 0) text.Append(" · ");
                text.Append(part);
            }
            return text.ToString();
        }

        /// <summary>OpenBuild after a wave's end: the lull before the day's next wave, or the afternoon after its last. Only OpenDawnBuild
        /// lets the build phase through (unused since 2026-11-06: dawn starts the next day).</summary>
        public bool InterceptBuild()
        {
            if (!Running) return false;
            if (buildAllowed) { buildAllowed = false; return false; }
            raidSpots.Clear();
            if (Stage == CampaignStage.Tutorial) return true;
            if (WaveOfDay + 1 < rules.wavesPerDay)
            {
                Stage = CampaignStage.Lull; stageClock = 0f; stageLength = rules.lullSeconds;
                gm.CampaignSetPhase(ArenaUiState.WaveIntro);
                gm.hud?.Banner(ArenaText.Pair($"第 {Day} 天 · 间歇", $"DAY {Day} · LULL"), ArenaText.Pair("下一轮敌袭马上就到，守住基地。", "The next attack is coming. Hold the base."));
                return true;
            }
            DropKillEnergyLeft(); // what no kill dropped of the day's kill energy (V2Campaign.Days.cs)
            SaveStoryPhase(SavePhase.Afternoon); // the afternoon's save, before its upkeep, ore and crate (V2Campaign.Save.cs)
            BeginDusk();
            return true;
        }

        public void OpenDawnBuild()
        {
            Stage = CampaignStage.Build;
            buildAllowed = true;
            // This dawn's rounds of weapon offers: free air drops only (0 since 2026-11-06; weapons are bought at the Armament Bay and
            // arrive at once). Since the same day the dawn starts the next day directly (GameManager.CampaignStartDay), so this build is unused.
            armoryRoundsLeft = rules.armoryRoundsPerDawn;
            ArmoryRoundActive = armoryRoundsLeft > 0;
            if (ArmoryRoundActive) armoryRoundsLeft--;
            gm.CampaignOpenBuild();
        }

        int armoryRoundsLeft;
        /// <summary>A round of weapon offers is due in the dawn build (GameManager.CampaignSkipsArmory reads it).</summary>
        public bool ArmoryRoundActive { get; private set; }
        /// <summary>The dawn build's next round of weapon offers, while free rounds are left.</summary>
        public bool TakeArmoryRound()
        {
            if (!Running || Stage != CampaignStage.Build || armoryRoundsLeft <= 0 || !ArmoryUpgrades) { ArmoryRoundActive = false; return false; }
            armoryRoundsLeft--;
            ArmoryRoundActive = true;
            return true;
        }

        /// <summary>The Tyrant fell (GameManager.NotifyFinalBossDefeated): its dark matter core drops where it died.</summary>
        public void OnTyrantDefeated(Vector3 at)
        {
            Stage = CampaignStage.Core;
            SpawnCore(at);
            Say(Line.E00);
        }

        // ───── HUD text the GameManager's snapshot shows ─────

        public void FillSnapshot(ArenaUiSnapshot s)
        {
            s.hideXp = Running;
            // Since 2026-11-10 the bottom row shows its auto-weapon slots and its skill slots only once the run holds one of that kind.
            s.hideAutoSlots = Running && !HoldsAutoWeapon;
            s.hideSkillSlots = Running && !(gm.skills && gm.skills.OwnedCount > 0);
            // Since 2026-11-06 the screen calls the day's stages morning (the wave), afternoon (rest and mining) and night.
            string phase = Stage switch
            {
                CampaignStage.Day or CampaignStage.Lull => ArenaText.Pair("上午", "MORNING"),
                CampaignStage.Dusk => ArenaText.Pair("下午", "AFTERNOON"),
                CampaignStage.Night => ArenaText.Pair("夜晚", "NIGHT"),
                CampaignStage.Tutorial => ArenaText.Pair("坠落之后", "AFTER THE FALL"),
                _ => ""
            };
            string dayText = Overtime ? ArenaText.Pair($"第 {Day} 天（超时）", $"DAY {Day} (OVERTIME)") : Day == 0 ? ArenaText.Pair("第 0 天", "DAY 0") : ArenaText.Pair($"第 {Day} / {rules.days} 天", $"DAY {Day} / {rules.days}");
            bool evening = Stage is CampaignStage.Dusk or CampaignStage.Night;
            // With open-ended evenings the afternoon and the night show no clock at all.
            bool eveningClock = evening && !EveningsOpenEnded;
            float left = eveningClock ? Mathf.Max(0f, stageLength - stageClock) : gm.waves ? gm.waves.TimeLeft : 0f;
            if (Stage is CampaignStage.Day or CampaignStage.Lull)
                s.campaignLabel = Dot(dayText, phase, AttackText(WaveOfDay + 1), s.waveTimeExpired ? ArenaText.Pair("清场中", "CLEAR SURVIVORS") : $"{Mathf.CeilToInt(left):00}");
            else if (evening) s.campaignLabel = eveningClock ? Dot(dayText, phase, $"{Mathf.CeilToInt(left):00}") : Dot(dayText, phase);
            else if (Stage == CampaignStage.Tutorial) s.campaignLabel = $"{ArenaText.Pair("第 0 天", "DAY 0")} · {phase}";
            else if (Stage == CampaignStage.Build) s.campaignLabel = Day + 1 > rules.days ? ArenaText.Pair($"第 {Day + 1} 天（超时）· 天亮", $"DAY {Day + 1} (OVERTIME) · DAWN") : ArenaText.Pair($"第 {Day + 1} / {rules.days} 天 · 天亮", $"DAY {Day + 1} / {rules.days} · DAWN");
            else s.campaignLabel = "";
            // The label turns orange in its last 5 seconds: with a clock on the afternoon and the night, that clock; elsewhere never.
            if (eveningClock) s.waveTime = left;
            else if (evening || Stage is CampaignStage.Tutorial or CampaignStage.Build or CampaignStage.Lull) s.waveTime = 99f;
            if (s.state == ArenaUiState.WaveIntro && Stage is CampaignStage.Lull)
                s.banner = ArenaText.Pair($"第 {Day} 天 · 敌袭 {WaveOfDay + 2}/{rules.wavesPerDay} 即将到来", $"DAY {Day} · ATTACK {WaveOfDay + 2}/{rules.wavesPerDay} INCOMING");
            else if (s.state == ArenaUiState.WaveIntro && Stage is CampaignStage.Day)
                s.banner = Overtime ? ArenaText.Pair($"第 {Day} 天 · 超出预定天数", $"DAY {Day} · OVERTIME") : Dot(ArenaText.Pair($"第 {Day} 天", $"DAY {Day}"), ArenaText.Pair("上午", "MORNING"), AttackText(WaveOfDay + 1));
            // Since 2026-11-10 the campaign writes nothing on the HUD's hint line (it keeps the weapon's name): its notes stand in the
            // goal panel (CurrentNote), and the lamps left in their own box.
            // The story mode has no levels: its results name the day and the kills.
            if (gm.State == GameState.Won)
            {
                s.resultTitle = ArenaText.Pair("即将重返地球", "EARTH IS WITHIN REACH");
                s.resultDetails = ArenaText.Pair($"第 {Day} 天 · 消灭 {s.kills} 个敌人\n飞船带着暗物质核心跃迁回家", $"Day {Day} · {s.kills} kills\nThe ship jumps home with the dark matter core");
            }
            else if (gm.State == GameState.Lost)
            {
                s.resultTitle = BaseDestroyed ? ArenaText.Pair("基地被摧毁", "THE BASE IS DESTROYED") : ArenaText.Pair("探索者陨落", "EXPLORER LOST");
                s.resultDetails = ArenaText.Pair($"第 {Day} 天 · 消灭 {s.kills} 个敌人\n飞船修复：引擎{(engineFixed ? "✓" : "×")} 装甲{(armorFixed ? "✓" : "×")} 燃料 {fuel}/{FuelUnits}",
                    $"Day {Day} · {s.kills} kills\nShip: engine {(engineFixed ? "✓" : "×")} armor {(armorFixed ? "✓" : "×")} fuel {fuel}/{FuelUnits}");
            }
        }

        // ───── Helpers ─────

        static Keyboard Keys => Keyboard.current;
        /// <summary>For automated checks: replaces the interact and lamp keys (ArenaKeys: E and F unless changed in the settings):
        /// interact held, or one press of either, used up when read.</summary>
        [System.NonSerialized] public bool debugHoldE, debugPressE, debugPressF;
        // While a yes / no question is open no key reaches the campaign (its answer is Enter, Esc or a click).
        bool QuestionOpen => ui && ui.ConfirmOpen;
        bool EHeld => !QuestionOpen && (debugHoldE || ArenaKeys.Held(GameAction.Interact));
        bool EPressed => !QuestionOpen && (Take(ref debugPressE) || ArenaKeys.Pressed(GameAction.Interact));
        bool FPressed => !QuestionOpen && (Take(ref debugPressF) || ArenaKeys.Pressed(GameAction.Lamp));
        static bool Take(ref bool flag) { bool was = flag; flag = false; return was; }
        public V2CampaignUi Ui => ui;
        /// <summary>For automated checks: currency as if mined or dropped.</summary>
        public void DebugGrant(int alloyAmount, int energyAmount, int darkAmount) => Gain(new CampaignCost(alloyAmount, energyAmount, darkAmount));

        void PlacePlayer(Vector3 at)
        {
            var p = gm.player; if (!p) return;
            at = FindOpen(at, p.Radius + .2f);
            var body = p.GetComponent<Rigidbody>();
            if (body) { body.position = at; body.linearVelocity = Vector3.zero; }
            p.transform.position = at;
        }

        /// <summary>The nearest open ground (no wall, hole, gravity ring or quicksand) to desired, searched in rings up to maxCells.</summary>
        public static Vector3 FindOpen(Vector3 desired, float radius, float maxCells = 14f)
        {
            var map = WorldMap.Active;
            desired.y = 0f;
            if (!map) return desired;
            bool Ok(Vector3 p) => map.IsWalkable(p, radius) && Quicksand.SpeedFactor(p) > .99f;
            if (Ok(desired)) return desired;
            for (float r = 1f; r <= maxCells; r += 1f)
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2f / 16f;
                    var p = desired + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (Ok(p)) return p;
                }
            return desired;
        }

        const float SpawnPlayerClearance = 9f, SpawnRingMapMargin = 3f;

        /// <summary>WaveManager.SpawnPointOverride while a campaign runs. Since 2026-11-06 a morning wave's monsters are born on a ring
        /// rules.daySpawnRing cells round the base (not on the far rim of the large map) and walk in, so the player can meet them on the
        /// way: the point lies the way the wave's own pick lies from the base, kept inside the map and pulled onto open ground. A point
        /// that ends up next to the player moves round the ring; one that finds no open ground keeps the wave's pick. Off by night, in the
        /// tutorial, the final boss and the void trial.</summary>
        Vector3 CampaignSpawnPoint(Vector3 picked)
        {
            var map = WorldMap.Active;
            if (!Running || !map || map.TrialActive || Stage is not (CampaignStage.Day or CampaignStage.Lull)) return picked;
            Vector3 d = Query.Flat(picked - BaseCentre);
            float angle = d.sqrMagnitude > .01f ? Mathf.Atan2(d.z, d.x) : Random.value * Mathf.PI * 2f;
            // Part of the morning is born on the near ring, the rest on the far one (rules.nearSpawnShare).
            float ring = Random.value < rules.nearSpawnShare ? rules.nearSpawnRing : rules.daySpawnRing;
            // One step round the ring is about the player clearance long.
            float step = SpawnPlayerClearance * 1.2f / Mathf.Max(4f, ring);
            Vector3 player = gm && gm.player ? Query.Flat(gm.player.Position) : new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3? best = null; float bestDistance = -1f;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                // The first try at the pick's bearing, later ones one, two, three steps round the ring, alternately either way.
                float a = angle + Random.Range(-.06f, .06f) + (attempt + 1) / 2 * (attempt % 2 == 0 ? 1f : -1f) * step;
                Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p = FindOpen(BaseCentre + dir * SpawnRingRadius(dir, ring), .6f);
                if (!map.IsWalkable(p, .6f)) continue;
                float distance = Query.Flat(p - player).magnitude;
                if (distance >= SpawnPlayerClearance) return p;
                if (distance > bestDistance) { bestDistance = distance; best = p; }
            }
            return best ?? picked;
        }

        /// <summary>A spawn ring's radius along a bearing from the base: ring, shortened where the map ends sooner.</summary>
        float SpawnRingRadius(Vector3 dir, float ring)
        {
            Vector2 half = MapHalfSize;
            float r = Mathf.Max(BaseHalf + 4f, ring);
            if (Mathf.Abs(dir.x) > .001f) r = Mathf.Min(r, Mathf.Max(1f, half.x - SpawnRingMapMargin - Mathf.Abs(BaseCentre.x)) / Mathf.Abs(dir.x));
            if (Mathf.Abs(dir.z) > .001f) r = Mathf.Min(r, Mathf.Max(1f, half.y - SpawnRingMapMargin - Mathf.Abs(BaseCentre.z)) / Mathf.Abs(dir.z));
            return r;
        }

        /// <summary>The campaign map's area, centred on the world origin like WorldMap.Bounds (MapHalfSize: the large map since 2026-11-06).
        /// Night regions, ore and dark matter use it.</summary>
        Bounds CampaignBounds
        {
            get
            {
                Vector2 half = MapHalfSize;
                return new Bounds(Vector3.zero, new Vector3(Mathf.Max(2f, half.x * 2f), 20f, Mathf.Max(2f, half.y * 2f)));
            }
        }

        // Day 0's route: since 2026-11-06 the campaign map is larger than the classic arena (180 x 120 cells), so the crash site no longer
        // sits at the map's west edge but where it sat on the classic arena: 81 cells west of the base (the classic half width 90, less 9),
        // with the glowing corner 20 cells east of it and 33 south of the base (55% of the classic half depth 60). The tutorial's walk, its
        // markers (14 and 40 cells east of the crash site) and its fight keep their places and their length.
        const float CrashDistance = 81f, TutorialCornerEast = 20f, TutorialCornerSouth = 33f;
        Vector3 CrashSite => BaseCentre + Vector3.left * Mathf.Min(CrashDistance, Mathf.Max(10f, MapHalfSize.x - 9f));

        /// <summary>An auto weapon sits in one of the auto slots.</summary>
        bool HoldsAutoWeapon
        {
            get
            {
                var weapons = gm ? gm.runWeapons : null;
                if (!weapons) return false;
                foreach (var weapon in weapons.Auto) if (weapon != null) return true;
                return false;
            }
        }

        // ───── For the campaign's own screen layer (V2CampaignUi.Feedback.cs, 2026-11-10) ─────

        /// <summary>The short note under the goal: the keys learned so far on Day 0, the night's rule and what a death would cost, how
        /// the afternoon ends. The screen shows it for a few seconds whenever it changes. Empty in the void trial.</summary>
        public string CurrentNote()
        {
            if (!Running || WorldMap.Active && WorldMap.Active.TrialActive) return "";
            if (Stage == CampaignStage.Tutorial) return tutorialHint ?? "";
            return Stage is CampaignStage.Dusk or CampaignStage.Night ? NightHint() : "";
        }

        /// <summary>The lamps left are counted on screen in the afternoon and at night (F sets one down there), not in the void trial.</summary>
        public bool LampCounterShown => Running && Stage is CampaignStage.Dusk or CampaignStage.Night && !(WorldMap.Active && WorldMap.Active.TrialActive);

        /// <summary>E is held on a node: how far the mining has come (0..1), what it gives, and whether it is dark matter.</summary>
        public bool MiningProgress(out float progress, out string what, out bool darkMatter)
        {
            progress = 0f; what = ""; darkMatter = false;
            if (!Running || mining == null) return false;
            progress = Mathf.Clamp01(mineProgress);
            darkMatter = mining.kind == NodeKind.Dark;
            what = mining.kind switch
            {
                NodeKind.Ore => ArenaText.Pair($"采集异星合金 +{mining.amount}", $"Mining alien alloy +{mining.amount}"),
                NodeKind.VoidAlloy => ArenaText.Pair($"采集虚空合金 +{mining.amount}", $"Mining void alloy +{mining.amount}"),
                _ when mining.energy > 0 => ArenaText.Pair($"采集暗物质 +{mining.amount} · 能量 +{mining.energy}", $"Mining dark matter +{mining.amount} · energy +{mining.energy}"),
                _ => ArenaText.Pair($"采集暗物质 +{mining.amount}", $"Mining dark matter +{mining.amount}")
            };
            return true;
        }


        // ───── For the campaign's sounds (CampaignAudio, 2026-11-10) ─────
        // CampaignAudio reads these each frame and plays a sound when they change. They read the night's lamps, shadows and mining
        // (V2Campaign.Night.cs: lamps, shadows, mining, InLampLight) and change nothing.

        /// <summary>Where Day 0 starts; the crash site's ambience is loudest here.</summary>
        internal Vector3 CrashSitePoint => CrashSite;

        /// <summary>At night the player stands outside every lamp's light and the base's (the darkness loop plays there).</summary>
        internal bool PlayerInDarkness => Running && Stage == CampaignStage.Night && gm && gm.player && !InLampLight(Query.Flat(gm.player.Position));

        /// <summary>Cells from a point to the nearest shadow; infinity with none.</summary>
        internal float NearestShadowDistance(Vector3 from)
        {
            float best = float.PositiveInfinity;
            foreach (var s in shadows) best = Mathf.Min(best, Query.Flat(s.position - from).magnitude);
            return best;
        }

        /// <summary>The lamps' HP left in all (it falls while shadows eat a lamp), and where the weakest one stands.</summary>
        internal float LampHpTotal(out Vector3 weakest)
        {
            float total = 0f, low = float.MaxValue;
            weakest = BaseCentre;
            foreach (var lamp in lamps)
            {
                total += lamp.hp;
                if (lamp.hp < low) { low = lamp.hp; weakest = lamp.position; }
            }
            return total;
        }

        /// <summary>E is held on a node: where it stands.</summary>
        internal bool MiningAt(out Vector3 at)
        {
            at = mining != null ? mining.position : default;
            return mining != null;
        }
        /// <summary>The glowing corner of the tutorial's first ore: south-east of the crash site, on the way to the base.</summary>
        Vector3 TutorialCorner => new(CrashSite.x + TutorialCornerEast, 0f, BaseCentre.z - Mathf.Min(TutorialCornerSouth, Mathf.Max(4f, MapHalfSize.y - 6f)));
    }
}
