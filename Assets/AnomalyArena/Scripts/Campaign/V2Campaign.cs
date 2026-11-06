using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    public enum CampaignRoom { Control, Armament, Energy, Ship }

    public enum CampaignStage { Off, TitleCard, Loadout, Tutorial, Day, Lull, Dusk, Night, Base, Build, Story, FinalBoss, Core, Ending }

                 
                                                                                                                                       
                                                                                                                                           
                                                                                                   
                                                                                                                                           
                                                                                                                                         
                                                                                                                                        
                                                                                                         
                                                                                                                                         
                                                                                                                             
                  
    [DefaultExecutionOrder(150)]
    public sealed partial class V2Campaign : MonoBehaviour
    {
        public CampaignRules rules;
        public CampaignArt art;
                                                                                                                                           
                                                                                                       
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

        GameManager gm;
        V2CampaignUi ui;
        float stageClock, stageLength;
        bool buildAllowed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Boot() { Active = null; ClearHooks(); }

        void Awake()
        {
            gm = GetComponent<GameManager>();
        }

        void Start()
        {
            if (!gm || !rules) return;
            ui = V2CampaignUi.Create(this);
            if (startButtonOpensCampaign) ui.RouteTitleStart();
        }

        void OnDestroy()
        {
            if (Active == this) { Active = null; ClearHooks(); }
            if (ui) Destroy(ui.gameObject);
        }

        static void ClearHooks()
        {
            Enemy.RaidSpot = null;
            Enemy.RaidStrike = null;
            RunWeapons.AxisAllowed = null;
            RunWeapons.ArmoryRule = null;
        }

                                           

                                                                                                                                                    
        public void Begin()
        {
            if (Running || !gm || !rules) return;
            Active = this; Running = true; Day = 0; WaveOfDay = 0; Overtime = false; BaseDestroyed = false; EndingDone = false;
            pickRooms.Clear(); researchQueued = 0; armoryRoundsLeft = 0; ArmoryRoundActive = false;
            ResetEconomy(); ResetBase(); ResetNight();
            InstallHooks();
            BuildWorld();
            PlacePlayer(CrashSite);
            Stage = CampaignStage.TitleCard; stageClock = 0f; stageLength = 3.2f;
            ui.ShowTitleCard(ArenaText.Pair("DAY 0","DAY 0"), ArenaText.Pair("After the fall","After the fall"), stageLength);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WaveEnd, gm.player.Position, .7f);
            gm.CampaignHoldTitle();
        }

        void InstallHooks()
        {
            Enemy.RaidSpot = RaidSpotFor;
            Enemy.RaidCentre = BaseCentre;
            Enemy.RaidStrike = OnRaidStrike;
            RunWeapons.AxisAllowed = axis => axis switch
            {
                UpgradeAxis.Tame => TameCards,
                UpgradeAxis.Twist => TwistCards,
                UpgradeAxis.Infusion => InfusionCards,
                _ => PickFrom(CampaignRoom.Armament)                                                           
            };
            RunWeapons.ArmoryRule = () => (ArmoryNewWeapons, ArmoryUpgrades);
            gm.progression.StatCardsAllowed = () => StatCards;
                                                                                                                                         
                                                                                                 
            gm.progression.LevelingAllowed = null;
            gm.progression.XpIgnored = () => Running;
            gm.progression.DraftConsumed -= OnPickUsed; gm.progression.DraftConsumed += OnPickUsed;
            gm.waves.EnemySpawned -= OnEnemySpawned; gm.waves.EnemySpawned += OnEnemySpawned;
            gm.waves.EnemyEliminated -= OnEnemyEliminated; gm.waves.EnemyEliminated += OnEnemyEliminated;
        }

                                                                                                                                   
        public void AfterLoadout()
        {
            gm.CampaignEnterPlay();
            BeginTutorial();
        }

                                                                       

        bool Works(CampaignRoom room, int level) => rooms[(int)room].level >= level && !rooms[(int)room].damaged;
                                                                                                                                    
        public bool PlayerLevelsForSale => Running && Works(CampaignRoom.Armament, 1);
        public bool ArmoryUpgrades => !Running || Works(CampaignRoom.Armament, 1);
        public bool ArmoryNewWeapons => !Running || Works(CampaignRoom.Armament, 2);
        public bool TameCards => !Running || Works(CampaignRoom.Armament, 2) && PickFrom(CampaignRoom.Armament);
        public bool TwistCards => !Running || Works(CampaignRoom.Armament, 3) && PickFrom(CampaignRoom.Armament);
        public bool StatCards => !Running || Works(CampaignRoom.Energy, 1) && PickFrom(CampaignRoom.Energy);
        public bool SkillCards => !Running || Works(CampaignRoom.Energy, 2) && PickFrom(CampaignRoom.Energy);
        public bool InfusionCards => !Running || Works(CampaignRoom.Energy, 3) && PickFrom(CampaignRoom.Energy);

                                                                                                                                           
                                                                                                             
        readonly Queue<CampaignRoom> pickRooms = new();
        bool PickFrom(CampaignRoom room) => pickRooms.Count == 0 || pickRooms.Peek() == room;
        void OnPickUsed() { if (pickRooms.Count > 0) pickRooms.Dequeue(); }
        public bool LampsCraftable => !rooms[(int)CampaignRoom.Ship].damaged;
        bool AnyCardKind => StatCards || SkillCards || TameCards || TwistCards || InfusionCards;

                            

        void Update()
        {
            if (!Running || !gm) return;
            if (gm.State is GameState.Won or GameState.Lost) { if (Stage != CampaignStage.Off) Finish(); return; }
            float dt = Time.deltaTime, unscaled = Time.unscaledDeltaTime;
            switch (Stage)
            {
                case CampaignStage.TitleCard:
                    stageClock += unscaled;
                    if (stageClock >= stageLength) { Stage = CampaignStage.Loadout; gm.CampaignBeginLoadout(); }
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
        }

                          

        void TickDay(float dt)
        {
            TickPickups(dt);
            TickDayInteraction(dt);
        }

                                                                                                                                                      
        public void StartNextWave()
        {
            var waves = gm.waves;
            if (Stage == CampaignStage.Build)
            {
                Day++; WaveOfDay = 0; Stage = CampaignStage.Day;
                if (Day > rules.days) Overtime = true;
                CampaignEvents.RaiseDayStarted(Day, Overtime);
                                                                                                                                               
                                                                                             
                pickRooms.Clear();
                regionKills.Clear();
                ApplyDaylight();
                Say(Line.S07);
                Objective(ArenaText.Pair("Hold the base: some monsters go straight for it. Stay alive too","Hold the base: some monsters go straight for it. Stay alive too"), null);
            }
                                                                                                                                        
                                                                                                                                           
                                                    
            int index = Mathf.Clamp(rules.WaveNumber(Overtime ? rules.days : Day, WaveOfDay) - 1, 0, waves.WaveCount - 1);
            waves.BeginWaveAt(index);
            string affixes = waves.WaveAffixA != ElementKind.None ? $"{ArenaTerms.Element(waves.WaveAffixA)} · {ArenaTerms.Element(waves.WaveAffixB)}" : ArenaText.Pair("Hold the base and stay alive","Hold the base and stay alive");
            gm.hud?.Banner(Dot(Overtime ? ArenaText.Pair($"DAY {Day} (OVERTIME)",$"DAY {Day} (OVERTIME)") : ArenaText.Pair($"DAY {Day}",$"DAY {Day}"), AttackText(WaveOfDay + 1)), affixes);
        }

                                                                                                                 
        string AttackText(int k) => rules.wavesPerDay > 1 ? ArenaText.Pair($"ATTACK {k}/{rules.wavesPerDay}",$"ATTACK {k}/{rules.wavesPerDay}") : "";

                                                                               
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
                gm.hud?.Banner(ArenaText.Pair($"DAY {Day} · LULL",$"DAY {Day} · LULL"), ArenaText.Pair("The next attack is coming. Hold the base.","The next attack is coming. Hold the base."));
                return true;
            }
            BeginDusk();
            return true;
        }

        public void OpenDawnBuild()
        {
            Stage = CampaignStage.Build;
            buildAllowed = true;
                                                                                                                                           
            armoryRoundsLeft = researchQueued + rules.armoryRoundsPerDawn;
            researchQueued = 0;
            ArmoryRoundActive = armoryRoundsLeft > 0;
            if (ArmoryRoundActive) armoryRoundsLeft--;
            gm.CampaignOpenBuild();
        }

        int armoryRoundsLeft, researchQueued;
                                                                                                                            
        public bool ArmoryRoundActive { get; private set; }
                                                                                                                             
        public int ResearchQueued => researchQueued;
                                                                                                           
        public bool TakeArmoryRound()
        {
            if (!Running || Stage != CampaignStage.Build || armoryRoundsLeft <= 0 || !ArmoryUpgrades) { ArmoryRoundActive = false; return false; }
            armoryRoundsLeft--;
            ArmoryRoundActive = true;
            return true;
        }

                                                                                                                               
        public void OnTyrantDefeated(Vector3 at)
        {
            Stage = CampaignStage.Core;
            SpawnCore(at);
            Say(Line.E00);
        }

                                                                

        public void FillSnapshot(ArenaUiSnapshot s)
        {
            s.hideXp = Running;
            string phase = Stage switch
            {
                CampaignStage.Day or CampaignStage.Lull => ArenaText.Pair("DAY","DAY"),
                CampaignStage.Dusk => ArenaText.Pair("DUSK","DUSK"),
                CampaignStage.Night => ArenaText.Pair("NIGHT","NIGHT"),
                CampaignStage.Tutorial => ArenaText.Pair("AFTER THE FALL","AFTER THE FALL"),
                _ => ""
            };
            string dayText = Overtime ? ArenaText.Pair($"DAY {Day} (OVERTIME)",$"DAY {Day} (OVERTIME)") : Day == 0 ? ArenaText.Pair("DAY 0","DAY 0") : ArenaText.Pair($"DAY {Day} / {rules.days}",$"DAY {Day} / {rules.days}");
            float left = Stage is CampaignStage.Dusk or CampaignStage.Night ? Mathf.Max(0f, stageLength - stageClock) : gm.waves ? gm.waves.TimeLeft : 0f;
            if (Stage is CampaignStage.Day or CampaignStage.Lull)
                s.campaignLabel = Dot(dayText, phase, AttackText(WaveOfDay + 1), s.waveTimeExpired ? ArenaText.Pair("CLEAR SURVIVORS","CLEAR SURVIVORS") : $"{Mathf.CeilToInt(left):00}");
            else if (Stage is CampaignStage.Dusk or CampaignStage.Night) s.campaignLabel = $"{dayText} · {phase} · {Mathf.CeilToInt(left):00}";
            else if (Stage == CampaignStage.Tutorial) s.campaignLabel = $"{ArenaText.Pair("DAY 0","DAY 0")} · {phase}";
            else if (Stage == CampaignStage.Build) s.campaignLabel = Day + 1 > rules.days ? ArenaText.Pair($"DAY {Day + 1} (OVERTIME) · DAWN BUILD",$"DAY {Day + 1} (OVERTIME) · DAWN BUILD") : ArenaText.Pair($"DAY {Day + 1} / {rules.days} · DAWN BUILD",$"DAY {Day + 1} / {rules.days} · DAWN BUILD");
            else s.campaignLabel = "";
                                                                                                                          
            if (Stage is CampaignStage.Dusk or CampaignStage.Night) s.waveTime = left;
            else if (Stage is CampaignStage.Tutorial or CampaignStage.Build or CampaignStage.Lull) s.waveTime = 99f;
            if (s.state == ArenaUiState.WaveIntro && Stage is CampaignStage.Lull)
                s.banner = ArenaText.Pair($"DAY {Day} · ATTACK {WaveOfDay + 2}/{rules.wavesPerDay} INCOMING",$"DAY {Day} · ATTACK {WaveOfDay + 2}/{rules.wavesPerDay} INCOMING");
            else if (s.state == ArenaUiState.WaveIntro && Stage is CampaignStage.Day)
                s.banner = Overtime ? ArenaText.Pair($"DAY {Day} · OVERTIME",$"DAY {Day} · OVERTIME") : Dot(ArenaText.Pair($"DAY {Day}",$"DAY {Day}"), AttackText(WaveOfDay + 1));
            if (Stage is CampaignStage.Dusk or CampaignStage.Night) s.hint = NightHint();
            else if (Stage == CampaignStage.Tutorial && !string.IsNullOrEmpty(tutorialHint)) s.hint = tutorialHint;
            else if (Stage is CampaignStage.Day or CampaignStage.Lull && s.bankedDrafts > 0 && !AnyCardKind)
                s.hint = ArenaText.Pair($"PICKS ×{s.bankedDrafts} · NO CARD KIND UNLOCKED AT THE BASE YET",$"PICKS ×{s.bankedDrafts} · NO CARD KIND UNLOCKED AT THE BASE YET");
            if (gm.State == GameState.Won)
            {
                s.resultTitle = ArenaText.Pair("EARTH IS WITHIN REACH","EARTH IS WITHIN REACH");
                s.resultDetails = ArenaText.Pair($"Day {Day} · Level {s.level} · {s.kills} kills\nThe ship jumps home with the dark matter core",$"Day {Day} · Level {s.level} · {s.kills} kills\nThe ship jumps home with the dark matter core");
            }
            else if (gm.State == GameState.Lost)
            {
                s.resultTitle = BaseDestroyed ? ArenaText.Pair("THE BASE IS DESTROYED","THE BASE IS DESTROYED") : ArenaText.Pair("EXPLORER LOST","EXPLORER LOST");
                s.resultDetails = ArenaText.Pair(                    $"Day {Day} · Level {s.level} · {s.kills} kills\nShip: engine {(engineFixed ? "✓" : "✗")} armor {(armorFixed ? "✓" : "✗")} fuel {fuel}/{rules.fuelUnits}",                    $"Day {Day} · Level {s.level} · {s.kills} kills\nShip: engine {(engineFixed ? "✓" : "✗")} armor {(armorFixed ? "✓" : "✗")} fuel {fuel}/{rules.fuelUnits}");
            }
        }

                              

        static Keyboard Keys => Keyboard.current;
                                                                                                                             
        [System.NonSerialized] public bool debugHoldE, debugPressE, debugPressF;
        bool EHeld => debugHoldE || Keys != null && Keys.eKey.isPressed;
        bool EPressed => Take(ref debugPressE) || Keys != null && Keys.eKey.wasPressedThisFrame;
        bool FPressed => Take(ref debugPressF) || Keys != null && Keys.fKey.wasPressedThisFrame;
        static bool Take(ref bool flag) { bool was = flag; flag = false; return was; }
        public V2CampaignUi Ui => ui;
                                                                                     
        public void DebugGrant(int alloyAmount, int energyAmount, int darkAmount) => Gain(new CampaignCost(alloyAmount, energyAmount, darkAmount));

        void PlacePlayer(Vector3 at)
        {
            var p = gm.player; if (!p) return;
            at = FindOpen(at, p.Radius + .2f);
            var body = p.GetComponent<Rigidbody>();
            if (body) { body.position = at; body.linearVelocity = Vector3.zero; }
            p.transform.position = at;
        }

                                                                                                                                               
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

        float MapHalfWidth => WorldMap.Active ? WorldMap.Active.Bounds.extents.x : 90f;
        Vector3 CrashSite => new(-MapHalfWidth + 9f, 0f, 0f);
    }
}
