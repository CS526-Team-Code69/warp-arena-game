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
                                                                                                                                      
        public bool EveningsOpenEnded => rules && rules.openEndedEvenings;

        GameManager gm;
        V2CampaignUi ui;
        float stageClock, stageLength;
        bool buildAllowed;
                                                                                                       
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
            ui = V2CampaignUi.Create(this);
            if (startButtonOpensCampaign) ui.RouteTitleStart();
        }

        void OnDestroy()
        {
            GiveMapBack();
            if (Active == this) { Active = null; ClearHooks(); }
            if (ui) Destroy(ui.gameObject);
        }

        static void ClearHooks()
        {
            Enemy.RaidSpot = null;
            Enemy.RaidStrike = null;
            Enemy.RaidAggroRadius = 0f;
            WaveManager.SpawnPointOverride = null;
            RunWeapons.AxisAllowed = null;
            RunWeapons.ArmoryRule = null;
        }

                                                                                                                         
        void GiveMapBack()
        {
            if (!mapSetUp) return;
            mapSetUp = false;
            RestoreClassicMap();
        }

                                           

                                                                                                                                                          
        public void Begin()
        {
            if (Running || !gm || !rules) return;
            Active = this; Running = true; Day = 0; WaveOfDay = 0; Overtime = false; BaseDestroyed = false; EndingDone = false;
            armoryRoundsLeft = 0; ArmoryRoundActive = false; armoryMode = ArmoryMode.Rooms; activeUpgrade = null;
            ResetEconomy(); ResetBase(); ResetNight();
            InstallHooks();
                                                                                                                                           
                                                                  
            SetupCampaignMap(); mapSetUp = true;
            trailTarget = HoleCentre;                                                                        
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
                                                                                                                                        
            Enemy.RaidAggroRadius = rules.raiderAggroRadius;
                                                                                                                      
            WaveManager.SpawnPointOverride = CampaignSpawnPoint;
            RunWeapons.AxisAllowed = axis => axis switch
            {
                UpgradeAxis.Tame => TameCards,
                UpgradeAxis.Twist => TwistCards,
                UpgradeAxis.Infusion => InfusionCards,
                                                                                                                                       
                                                                           
                _ => !Running || activeUpgrade == null
            };
            RunWeapons.ArmoryRule = () => armoryMode switch
            {
                ArmoryMode.UpgradesOnly => (false, true),
                ArmoryMode.NewWeaponsOnly => (true, false),
                _ => (ArmoryNewWeapons, ArmoryUpgrades)
            };
            gm.progression.StatCardsAllowed = () => StatCards;
                                                                                                                                           
                                                                                                                                         
                                                          
            gm.progression.LevelingAllowed = null;
            gm.progression.XpIgnored = () => Running;
            gm.waves.EnemySpawned -= OnEnemySpawned; gm.waves.EnemySpawned += OnEnemySpawned;
            gm.waves.EnemyEliminated -= OnEnemyEliminated; gm.waves.EnemyEliminated += OnEnemyEliminated;
        }

                                                                                                                
        public void AfterLoadout()
        {
            gm.CampaignEnterPlay();
            BeginTutorial();
        }

                                                                                                                                              
                                                                                                                                                
        void BeginWithStartingWeapons()
        {
            Stage = CampaignStage.Loadout;
            gm.runWeapons.GiveToHand(0, WeaponForm.Pistol);
            AfterLoadout();
        }

                                                                       

        bool Works(CampaignRoom room, int level) => rooms[(int)room].level >= level && !rooms[(int)room].damaged;
                                                                                                                                      
                                          
        public bool PlayerLevelsForSale => Running && Works(CampaignRoom.Armament, 1);
        public bool ArmoryUpgrades => !Running || Works(CampaignRoom.Armament, 1);
        public bool ArmoryNewWeapons => !Running || Works(CampaignRoom.Armament, 2);
                                                                                                                                        
                                                                             
        public bool TameCards => KindOpen(CampaignUpgrade.Tame, CampaignRoom.Armament, 2);
        public bool TwistCards => KindOpen(CampaignUpgrade.Twist, CampaignRoom.Armament, 3);
        public bool StatCards => KindOpen(CampaignUpgrade.Stat, CampaignRoom.Energy, 1);
        public bool SkillCards => KindOpen(CampaignUpgrade.Skill, CampaignRoom.Energy, 2);
        public bool InfusionCards => KindOpen(CampaignUpgrade.Infusion, CampaignRoom.Energy, 3);
        bool KindOpen(CampaignUpgrade kind, CampaignRoom room, int level) => !Running || (activeUpgrade.HasValue ? activeUpgrade.Value == kind : Works(room, level));

                                                                                           
        CampaignUpgrade? activeUpgrade;
                                                                                                                                           
        enum ArmoryMode { Rooms, UpgradesOnly, NewWeaponsOnly }
        ArmoryMode armoryMode;
        public bool LampsCraftable => !rooms[(int)CampaignRoom.Ship].damaged;

                            

        void Update()
        {
            if (!Running || !gm) return;
            if (gm.State is GameState.Won or GameState.Lost) { if (Stage != CampaignStage.Off) Finish(); return; }
                                                                                                                                     
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
                regionKills.Clear();
                ApplyDaylight();
                Say(Line.S07);
                Objective(ArenaText.Pair("Morning: hold the base. Monsters walk in from all around for it; those near you attack you first","Morning: hold the base. Monsters walk in from all around for it; those near you attack you first"), null);
            }
                                                                                                                                           
                                                    
            int index = Mathf.Clamp(rules.WaveNumber(Overtime ? rules.days : Day, WaveOfDay) - 1, 0, waves.WaveCount - 1);
            waves.BeginWaveAt(index);
            string affixes = waves.WaveAffixA != ElementKind.None ? $"{ArenaTerms.Element(waves.WaveAffixA)} · {ArenaTerms.Element(waves.WaveAffixB)}" : ArenaText.Pair("Hold the base and stay alive","Hold the base and stay alive");
            gm.hud?.Banner(Dot(Overtime ? ArenaText.Pair($"DAY {Day} (OVERTIME)",$"DAY {Day} (OVERTIME)") : ArenaText.Pair($"DAY {Day}",$"DAY {Day}"), ArenaText.Pair("MORNING","MORNING"), AttackText(WaveOfDay + 1)), affixes);
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
                                                                                                                                       
                                                                                                                                                 
            armoryRoundsLeft = rules.armoryRoundsPerDawn;
            ArmoryRoundActive = armoryRoundsLeft > 0;
            if (ArmoryRoundActive) armoryRoundsLeft--;
            gm.CampaignOpenBuild();
        }

        int armoryRoundsLeft;
                                                                                                                            
        public bool ArmoryRoundActive { get; private set; }
                                                                                                        
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
                CampaignStage.Day or CampaignStage.Lull => ArenaText.Pair("MORNING","MORNING"),
                CampaignStage.Dusk => ArenaText.Pair("AFTERNOON","AFTERNOON"),
                CampaignStage.Night => ArenaText.Pair("NIGHT","NIGHT"),
                CampaignStage.Tutorial => ArenaText.Pair("AFTER THE FALL","AFTER THE FALL"),
                _ => ""
            };
            string dayText = Overtime ? ArenaText.Pair($"DAY {Day} (OVERTIME)",$"DAY {Day} (OVERTIME)") : Day == 0 ? ArenaText.Pair("DAY 0","DAY 0") : ArenaText.Pair($"DAY {Day} / {rules.days}",$"DAY {Day} / {rules.days}");
            bool evening = Stage is CampaignStage.Dusk or CampaignStage.Night;
                                                                                         
            bool eveningClock = evening && !EveningsOpenEnded;
            float left = eveningClock ? Mathf.Max(0f, stageLength - stageClock) : gm.waves ? gm.waves.TimeLeft : 0f;
            if (Stage is CampaignStage.Day or CampaignStage.Lull)
                s.campaignLabel = Dot(dayText, phase, AttackText(WaveOfDay + 1), s.waveTimeExpired ? ArenaText.Pair("CLEAR SURVIVORS","CLEAR SURVIVORS") : $"{Mathf.CeilToInt(left):00}");
            else if (evening) s.campaignLabel = eveningClock ? Dot(dayText, phase, $"{Mathf.CeilToInt(left):00}") : Dot(dayText, phase);
            else if (Stage == CampaignStage.Tutorial) s.campaignLabel = $"{ArenaText.Pair("DAY 0","DAY 0")} · {phase}";
            else if (Stage == CampaignStage.Build) s.campaignLabel = Day + 1 > rules.days ? ArenaText.Pair($"DAY {Day + 1} (OVERTIME) · DAWN",$"DAY {Day + 1} (OVERTIME) · DAWN") : ArenaText.Pair($"DAY {Day + 1} / {rules.days} · DAWN",$"DAY {Day + 1} / {rules.days} · DAWN");
            else s.campaignLabel = "";
                                                                                                                                      
            if (eveningClock) s.waveTime = left;
            else if (evening || Stage is CampaignStage.Tutorial or CampaignStage.Build or CampaignStage.Lull) s.waveTime = 99f;
            if (s.state == ArenaUiState.WaveIntro && Stage is CampaignStage.Lull)
                s.banner = ArenaText.Pair($"DAY {Day} · ATTACK {WaveOfDay + 2}/{rules.wavesPerDay} INCOMING",$"DAY {Day} · ATTACK {WaveOfDay + 2}/{rules.wavesPerDay} INCOMING");
            else if (s.state == ArenaUiState.WaveIntro && Stage is CampaignStage.Day)
                s.banner = Overtime ? ArenaText.Pair($"DAY {Day} · OVERTIME",$"DAY {Day} · OVERTIME") : Dot(ArenaText.Pair($"DAY {Day}",$"DAY {Day}"), ArenaText.Pair("MORNING","MORNING"), AttackText(WaveOfDay + 1));
            if (evening) s.hint = NightHint();
            else if (Stage == CampaignStage.Tutorial && !string.IsNullOrEmpty(tutorialHint)) s.hint = tutorialHint;
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
                                                                                                               
        bool QuestionOpen => ui && ui.ConfirmOpen;
        bool EHeld => !QuestionOpen && (debugHoldE || Keys != null && Keys.eKey.isPressed);
        bool EPressed => !QuestionOpen && (Take(ref debugPressE) || Keys != null && Keys.eKey.wasPressedThisFrame);
        bool FPressed => !QuestionOpen && (Take(ref debugPressF) || Keys != null && Keys.fKey.wasPressedThisFrame);
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

        const float SpawnPlayerClearance = 9f, SpawnRingMapMargin = 3f;

                                                                                                                                        
                                                                                                                                         
                                                                                                                                        
                                                                                                                                           
                                                                  
        Vector3 CampaignSpawnPoint(Vector3 picked)
        {
            var map = WorldMap.Active;
            if (!Running || !map || map.TrialActive || Stage is not (CampaignStage.Day or CampaignStage.Lull)) return picked;
            Vector3 d = Query.Flat(picked - BaseCentre);
            float angle = d.sqrMagnitude > .01f ? Mathf.Atan2(d.z, d.x) : Random.value * Mathf.PI * 2f;
                                                                          
            float step = SpawnPlayerClearance * 1.2f / Mathf.Max(4f, rules.daySpawnRing);
            Vector3 player = gm && gm.player ? Query.Flat(gm.player.Position) : new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3? best = null; float bestDistance = -1f;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                                                                                                                                
                float a = angle + Random.Range(-.06f, .06f) + (attempt + 1) / 2 * (attempt % 2 == 0 ? 1f : -1f) * step;
                Vector3 dir = new(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p = FindOpen(BaseCentre + dir * SpawnRingRadius(dir), .6f);
                if (!map.IsWalkable(p, .6f)) continue;
                float distance = Query.Flat(p - player).magnitude;
                if (distance >= SpawnPlayerClearance) return p;
                if (distance > bestDistance) { bestDistance = distance; best = p; }
            }
            return best ?? picked;
        }

                                                                                                                                              
        float SpawnRingRadius(Vector3 dir)
        {
            Vector2 half = MapHalfSize;
            float r = Mathf.Max(BaseHalf + 4f, rules.daySpawnRing);
            if (Mathf.Abs(dir.x) > .001f) r = Mathf.Min(r, Mathf.Max(1f, half.x - SpawnRingMapMargin - Mathf.Abs(BaseCentre.x)) / Mathf.Abs(dir.x));
            if (Mathf.Abs(dir.z) > .001f) r = Mathf.Min(r, Mathf.Max(1f, half.y - SpawnRingMapMargin - Mathf.Abs(BaseCentre.z)) / Mathf.Abs(dir.z));
            return r;
        }

                                                                                                                                             
                                                                
        Bounds CampaignBounds
        {
            get
            {
                Vector2 half = MapHalfSize;
                return new Bounds(Vector3.zero, new Vector3(Mathf.Max(2f, half.x * 2f), 20f, Mathf.Max(2f, half.y * 2f)));
            }
        }

                                                                                                                                           
                                                                                                                                            
                                                                                                                                            
                                                                                                             
        const float CrashDistance = 81f, TutorialCornerEast = 20f, TutorialCornerSouth = 33f;
        Vector3 CrashSite => BaseCentre + Vector3.left * Mathf.Min(CrashDistance, Mathf.Max(10f, MapHalfSize.x - 9f));
                                                                                                                                    
        Vector3 TutorialCorner => new(CrashSite.x + TutorialCornerEast, 0f, BaseCentre.z - Mathf.Min(TutorialCornerSouth, Mathf.Max(4f, MapHalfSize.y - 6f)));
    }
}
