using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{
    public enum WavePhase { Idle, WaveIntro, WaveActive, WaitingBuild, FinalBoss }
                 
                                                                                               
                                                                                              
                                                                                    
                                                                                                                       
                                                                                   
                  
    [DefaultExecutionOrder(100)]
    public class WaveManager : MonoBehaviour
    {
        [Header("Saved pools and data")]
        public WaveSet waveSet;
        [Tooltip("Indexed by EnemyKind (Chanter is index 13).")]
        public EnemyDefinition[] roster;
        public Enemy[] preparedEnemies;
        public WaveSpawnMarker[] preparedMarkers;
        public EnemyBolt[] preparedBolts;
        public Enemy tyrant;
        [Header("Spawn counts and movement tuning.")]
        [Min(0f), Tooltip("Multiply regular, elite and summoned enemy counts, then round up. Standalone bosses remain singular.")] public float spawnCountMultiplier = 1f;
        [Min(0f), Tooltip("Chase and fixed charge-speed multiplier; attack intervals, projectiles and knockback are unchanged.")] public float movementSpeedMultiplier = 1f;
        [SerializeField] int pressurePickupRevision;
        public int PressurePickupRevision => pressurePickupRevision;
        public void MarkPressurePickupApplied() => pressurePickupRevision = 1;
        public float MovementSpeedScale => Mathf.Max(0f, movementSpeedMultiplier);
        int ScaledOrdinaryCount(int count) => Mathf.CeilToInt(Mathf.Max(0, count) * Mathf.Max(0f, spawnCountMultiplier));
        [Header("Batch composition")]
        [Range(0f,1f), Tooltip("Melee swarm share of a full batch.")] public float swarmShare = .6f;
        [Range(0f,1f), Tooltip("Spitter, Warden and ordinary specials share of a full batch (ramps in over waves 1-5).")] public float functionShare = .2f;
        [Range(0f,1f), Tooltip("Elite share of a full batch from firstEliteWave; capped by maxConcurrentElites, the rest becomes swarm.")] public float eliteShare = .2f;
        [SerializeField] int crowdBalanceRevision;
        int swarmRemaining, functionRemaining, eliteRemaining;
        [Header("Experience (system doc §9.2)")]
        [Min(0f), Tooltip("Fixed XP of every non-elite melee swarm enemy (§9.2: 0.5). Not scaled by experienceRateMultiplier.")]
        public float swarmExperienceReward = .5f;
        [Min(0f), Tooltip("Fixed XP of a mini Splitter. Not scaled by experienceRateMultiplier.")]
        public float miniExperienceReward = .25f;
        [Min(0f), Tooltip("XP per second of reference DPS it takes to kill a non-swarm enemy; 0.5/0.65 ties it to the swarm reward")]
        public float xpPerReferenceSecond = .77f;
        [Min(0f), Tooltip("Extra XP of a non-swarm enemy that carries an element affix (every elite does).")]
        public float affixExperienceBonus = 1f;
        [Min(0f), Tooltip("Global multiplier on every non-swarm reward (Spitter, Warden, specials, elites). Swarm and mini rewards stay fixed.")]
        public float experienceRateMultiplier = 1f;
        [Header("Continuous crowd")]
        [Min(1)] public int maxConcurrentOrdinary = 300;
        [Header("Roster and elites (system doc §10.6, §11.2)")]
        [Min(1), Tooltip("First wave in which a special can spawn as an elite (a special with an element affix).")] public int firstEliteWave = 6;
        [Min(0), Tooltip("Alive elites plus pending elite markers never exceed this; capped elite slots become melee fodder.")] public int maxConcurrentElites = 3;
        [Min(1), Tooltip("First wave with the ranged basic (Spitter); the wave weights encode it as well.")] public int rangedFirstWave = 2;
        [Min(1), Tooltip("First wave with the support basic (Warden); the wave weights encode it as well.")] public int supportFirstWave = 3;
        [Min(1), Tooltip("From this wave two Shades may be alive or pending at once; before it, one.")] public int shadePairWave = 14;
        [SerializeField, HideInInspector] int v5RosterRevision;
        [Tooltip("Enemy lab: the wave never spawns on its own, never times out and never ends; the lab spawns by hand.")] public bool labMode;
        [Header("Enemy behaviours (system doc §10.2) — candidate values")]
        [Min(.1f), Tooltip("Seconds between a Chanter's chants (cue and animation only; its aura is always on).")] public float chantInterval = 3f;
        [Min(0f), Tooltip("Radius around a living Chanter in which other non-boss enemies deal bonus damage.")] public float chantRadius = 6f;
        [Min(0f), Tooltip("Damage bonus of enemies inside a Chanter aura: 0.3 = +30%. Several Chanters do not stack.")] public float chantDamageBonus = .3f;
        [Min(0f), Tooltip("Radius of the radiation zone a Bloater leaves where it explodes.")] public float radiationRadius = 3f;
        [Min(0f), Tooltip("Seconds a radiation zone lasts.")] public float radiationLife = 4f;
        [Min(.05f), Tooltip("Seconds of exposure per radiation tick while the player stands inside any zone (zones do not stack).")] public float radiationTickSeconds = .5f;
        [Min(0f), Tooltip("HP lost per radiation tick; ignores Shield and is not an attack.")] public float radiationTickDamage = 1f;
        [Min(0), Tooltip("Mini Splitters left by a full Splitter killed by HP.")] public int splitterMiniCount = 2;
        [Min(0f), Tooltip("Sideways offset of each mini Splitter from the dead parent.")] public float splitterMiniOffset = .6f;
        [Range(.1f,1f), Tooltip("Mini Splitter body radius and model scale as a share of the full Splitter (HP share lives in EncounterBalance).")] public float splitterMiniScale = .6f;
        [Range(0f,1f), Tooltip("Mini Splitter attack damage as a share of the full Splitter.")] public float splitterMiniDamageShare = .6f;
        [Min(1), Tooltip("Valid spawn points a Stomper compares (one per ring sector, so at most 8); it takes the one with the fewest living enemies nearby.")] public int stomperSpawnCandidates = 8;
        [Min(0f), Tooltip("Radius used to count living enemies around a Stomper spawn candidate.")] public float stomperCrowdRadius = 6f;
        [Range(.1f,1f), Tooltip("Attack interval multiplier of an enemy carrying the Void affix (system doc §7.3: it acts faster).")] public float voidAffixIntervalFactor = .7f;
        [Header("Bosses (system doc §13)")]
        [Tooltip("Move numbers of the Nash Prophet (void trial) and the Tyrant (final boss).")] public BossTuning bosses = new();
        static readonly ElementKind[] EliteElements = { ElementKind.Fire, ElementKind.Poison, ElementKind.Frost, ElementKind.Lightning, ElementKind.Earth, ElementKind.Light, ElementKind.Void };
        static readonly ElementKind[] TyrantImmunityPool = { ElementKind.Fire, ElementKind.Poison, ElementKind.Frost, ElementKind.Lightning };
                                                                                       
        public int ActiveEliteCount { get { int count = 0; foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.Elite) count++; if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.elite) count++; return count; } }
                                                                                                                     
        public ElementKind PickEliteAffix()
        {
            var free = new List<ElementKind>(EliteElements);
            foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.Elite) free.Remove(enemy.Affix);
            if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.elite) free.Remove(marker.affix);
            return free.Count > 0 ? free[Random.Range(0, free.Count)] : ElementKind.None;
        }
        [Range(.05f,.5f)] public float refillMissingShare = .2f;
        [SerializeField] int continuousCrowdRevision;
        [Tooltip("First five waves as a fraction of full density; later waves use full density.")] public float[] earlyPopulationScale = { .3f,.4f,.5f,.65f,.8f };
        [SerializeField] int quickFeedbackRevision;
        int spawnSectorCursor;
        public Vector3 ActiveRoleShares
        {
            get
            {
                int wave = WaveIndex + 1;
                float functional = functionShare * Mathf.Clamp01((wave - 1) / 4f);
                float elite = wave < firstEliteWave ? 0f : eliteShare * (wave < 8 ? .25f : wave < 10 ? .5f : 1f);
                return new Vector3(Mathf.Max(0f,1f-functional-elite),functional,elite);
            }
        }
        int PopulationTarget => PopulationTargetForWave(WaveIndex);
        public int PopulationTargetForWave(int index)
        {
            index = Mathf.Clamp(index,0,waveSet.waves.Length-1);
            float ramp = earlyPopulationScale != null && index < earlyPopulationScale.Length ? Mathf.Clamp01(earlyPopulationScale[index]) : 1f;
            return Mathf.Min(Mathf.Max(1,maxConcurrentOrdinary),Mathf.Max(1,Mathf.CeilToInt(waveSet.waves[index].aliveTarget*Mathf.Max(0f,spawnCountMultiplier)*ramp)));
        }
                     
                                                                                                                                                       
                                                                                                                                                                     
                                                                                                                              
                      
        public float ExperienceFor(Enemy enemy)
        {
            if (!enemy || !enemy.definition || enemy.IsBoss) return 0f;
            if (enemy.Mini) return Mathf.Max(0f, miniExperienceReward);
            if (!enemy.Elite && IsSwarm(enemy.definition)) return Mathf.Max(0f, swarmExperienceReward);
            var balance = waveSet ? waveSet.encounterBalance : null;
            float body = experienceTable.TryGetValue((enemy.definition.kind, enemy.Elite), out float tabled) ? tabled
                : balance ? Mathf.Max(0f, xpPerReferenceSecond) * balance.TargetSeconds(enemy.definition, enemy.Elite)
                : enemy.definition.xp * (enemy.Elite ? 5f : 1f);                                                   
            float affix = enemy.Affix != ElementKind.None ? Mathf.Max(0f, affixExperienceBonus) : 0f;
            return (body + affix) * Mathf.Max(0f, experienceRateMultiplier);
        }
        [Header("Player-near batch spawning")]
        [Tooltip("Inner radius of the regular spawn ring; does not change per-wave stat growth.")] public float nearbySpawnMin = 8f;
        [Tooltip("Spawn candidates stay within this player-relative distance; distant map anchors are excluded.")] public float nearbySpawnMax = 18f;
        [Tooltip("Minimum safe distance for onscreen spawns when an offscreen point cannot be found.")] public float visibleSpawnSafety = 10f;
        [Tooltip("Maximum random candidates per spawn-location search.")] public int spawnPointCandidates = 80;
        [Tooltip("Retry interval when valid spawn locations or pooled enemies are unavailable.")] public float batchSpawnRetry = .1f;
        public int BatchIndex { get; private set; }
        public bool TimeExpired { get; private set; }
        public int LivingCount { get { int count = 0; foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy) count++; return count; } }
        public int PendingCount { get { int count = batchSpawnsRemaining + pendingSplits.Count * Mathf.Max(0, splitterMiniCount); if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf) count++; return count; } }
                                                                                                                                                           
        public ElementKind[] TyrantImmunities { get; private set; } = new ElementKind[2];
        public WavePhase Phase { get; private set; }
        public float TimeLeft { get; private set; }
        public float HpMultiplier => waveSet && WaveIndex >= 0 ? waveSet.waves[Mathf.Min(WaveIndex, waveSet.waves.Length - 1)].hpMultiplier * waveSet.ExtraHp(WaveIndex+1) : 1f;
        public float EnemyDamageFactor => waveSet ? waveSet.DamageFactor(WaveIndex+1) : 1f;
                                                                                                                                                                           
        public float CombatScale => waveSet && waveSet.encounterBalance && WaveIndex >= 0 ? waveSet.encounterBalance.DpsAt(WaveIndex + 1) / waveSet.encounterBalance.DpsAt(1) : 1f;
        public float EnemySpeedFactor => (waveSet ? waveSet.SpeedFactor(WaveIndex+1) : 1f) * MovementSpeedScale;
        public float BuildTime => waveSet && WaveIndex >= 0 ? waveSet.waves[Mathf.Min(WaveIndex, waveSet.waves.Length - 1)].buildTime : 20f;
        public event Action<int> WaveCompleted;
        public event Action<Enemy> EnemySpawned;
        public event Action<Enemy> EnemyEliminated;
        bool trialSuspended;
        bool finalBossPending;
        ElementKind waveAffixA, waveAffixB;
        int batchSpawnsRemaining, deathSequence;
        float radiationClock;
        readonly Dictionary<Enemy, int> corpseOrder = new();
        struct PendingSplit { public EnemyDefinition data; public Vector3 at, side; public bool suppressRewards; }
        readonly List<PendingSplit> pendingSplits = new();

        public int WaveIndex { get; private set; } = -1;
                                                                                                                  
        public int WaveCount => waveSet ? waveSet.waves.Length : 0;
        public bool BetweenWaves { get; private set; }
        public int Remaining => LivingCount + PendingCount;
        public IReadOnlyList<Enemy> Alive => alive;
        public int Kills { get; private set; }

        readonly List<Enemy> alive = new List<Enemy>();
        float spawnTimer;
        bool running;

        static GameManager GM => GameManager.Instance;

        void Awake() => ApplyV5Defaults();
        void OnValidate() => ApplyV5Defaults();
                                                                                                                                                                         
        void ApplyV5Defaults()
        {
            if (v5RosterRevision >= 1) return;
            v5RosterRevision = 1;
            swarmExperienceReward = .5f;
            rangedFirstWave = 2;
            supportFirstWave = 3;
        }

                                                                                                       
        public EnemyDefinition DefinitionOf(EnemyKind kind)
        {
            if (roster == null) return null;
            int index = (int)kind;
            if (index < roster.Length && roster[index] && roster[index].kind == kind) return roster[index];
            foreach (var data in roster) if (data && data.kind == kind) return data;
            return null;
        }

        public void StartWaves()
        {
            running = true;
            var pool = (ElementKind[])TyrantImmunityPool.Clone();
            for (int i = 0; i < TyrantImmunities.Length; i++)
            {
                int choice = Random.Range(i, pool.Length);
                (pool[i], pool[choice]) = (pool[choice], pool[i]);
                TyrantImmunities[i] = pool[i];
            }
            pendingSplits.Clear();
            Enemy.ClearRadiationZones(); radiationClock = 0f;
            BeginV2Wave(0);
        }

        public void BeginNextWave()
        {
            if (WaveIndex + 1 >= WaveCount) { BeginFinalBoss(); return; }
            BeginV2Wave(WaveIndex + 1);
        }

        static float OrdinaryAffixShare(int index) => index < 4 ? 0f : index < 7 ? .1f : index < 10 ? .15f : .25f;

                                                                                                                                            
                                                                                                                                            
        readonly Dictionary<(EnemyKind kind, bool elite), float> experienceTable = new();
        void BuildExperienceTable()
        {
            experienceTable.Clear();
            var balance = waveSet ? waveSet.encounterBalance : null;
            if (!balance || roster == null) return;
            var rows = new List<string>();
            foreach (var data in roster)
            {
                if (!data || IsSwarm(data) || data.kind is EnemyKind.Hollow or EnemyKind.Tyrant) continue;                             
                for (int e = 0; e < 2; e++)
                {
                    bool elite = e == 1;
                    if (elite && !data.canElite) continue;
                    float xp = Mathf.Max(0f, xpPerReferenceSecond) * balance.TargetSeconds(data, elite);
                    experienceTable[(data.kind, elite)] = xp;
                    rows.Add($"{data.kind}{(elite ? "*" : "")}={xp:0.00}");
                }
            }
            if (rows.Count > 0) Debug.Log($"[Waves] wave {WaveIndex + 1} experience table: {string.Join(" ", rows)}");
        }

        void BeginV2Wave(int index)
        {
            WaveIndex = index;
            BuildExperienceTable();
            Phase = WavePhase.WaveIntro;
            TimeLeft = 3f;
            BetweenWaves = false;
            spawnTimer = 0f;
            BatchIndex = 0; TimeExpired = false; batchSpawnsRemaining = 0;
            spawnSectorCursor = Random.Range(0,8);
                                                                                                                         
            int available = index < 4 ? 0 : index < 7 ? 3 : index < 10 ? 5 : 7;
            waveAffixA = available > 0 ? (ElementKind)Random.Range(1, available + 1) : ElementKind.None;
            waveAffixB = available > 0 ? (ElementKind)Random.Range(1, available + 1) : ElementKind.None;
            if (GM.hud) GM.hud.Banner(ArenaText.Pair($"Wave {index + 1} / {WaveCount}",$"Wave {index + 1} / {WaveCount}"), available > 0 ? $"{ArenaTerms.Element(waveAffixA)} · {ArenaTerms.Element(waveAffixB)}" : ArenaText.Pair("Interstellar Battlefield","Interstellar Battlefield"));
        }

        public void BeginFinalBoss()
        {
            TimeExpired = false; batchSpawnsRemaining = 0;
            Phase = WavePhase.FinalBoss;
            BetweenWaves = false;
            TimeLeft = 0f;
            if (WorldMap.Active) WorldMap.Active.ClosePortals();
            if (!tyrant) return;
            finalBossPending = true;
            TrySpawnFinalBoss();
        }

        void TrySpawnFinalBoss()
        {
            if (!TryFindV2SpawnPoint(tyrant.definition, out var position)) { spawnTimer = 0.5f; return; }
            tyrant.gameObject.SetActive(true);
            tyrant.transform.position = position;
            tyrant.Configure(tyrant.definition, waveSet ? Mathf.Max(1f,waveSet.finalBossHpFactor) : 1f, false);
            var board = ElementStatus.For(tyrant);
            foreach (var element in TyrantImmunities) board?.SetImmunity(element, 1f);
            tyrant.HoldAttacks(bosses.tyrantOpeningGrace);                                  
            Track(tyrant);
            finalBossPending = false;
            if (GM.hud) GM.hud.Banner(ArenaText.Pair("THE TYRANT KING","THE TYRANT KING"), ArenaText.Pair("Break its Shield. Make it charge into a wall.","Break its Shield. Make it charge into a wall."));
        }

        void TickV2(float dt)
        {
            if (dt <= 0f || trialSuspended || Phase == WavePhase.WaitingBuild || Phase == WavePhase.Idle) return;
            SpawnPendingSplits();
            TickRadiation(dt);
            if (labMode)
            {
                if (Phase == WavePhase.WaveIntro) { Phase = WavePhase.WaveActive; TimeLeft = 3599f; }
                TickPreparedMarkers(dt);
                return;
            }
            if (Phase == WavePhase.WaveIntro)
            {
                TimeLeft -= dt;
                if (TimeLeft <= 0f) { Phase = WavePhase.WaveActive; TimeLeft = waveSet.waves[WaveIndex].duration; BeginBatch(); IssueBatchMarkers(); }
                return;
            }
            if (Phase == WavePhase.FinalBoss)
            {
                if (finalBossPending) { spawnTimer -= dt; if (spawnTimer <= 0f) TrySpawnFinalBoss(); }
                TickPreparedMarkers(dt);
                return;
            }
            TimeLeft = Mathf.Max(0f, TimeLeft - dt);
            if (TimeLeft <= 0f)
            {
                if (!TimeExpired)
                {
                    TimeExpired = true; batchSpawnsRemaining = 0;
                    foreach (var marker in preparedMarkers) if (marker) marker.gameObject.SetActive(false);
                }
                if (LivingCount == 0) EndV2Wave();
                return;                                                                            
            }
            spawnTimer -= dt;
            if (LivingCount == 0 && PendingCount == 0) BeginBatch();
            else if (batchSpawnsRemaining == 0) RefillPopulation();
            if (batchSpawnsRemaining > 0 && spawnTimer <= 0f) IssueBatchMarkers();
                                                                                                                               
            TickPreparedMarkers(dt);
        }

                                                                                                                                                                     
        void TickRadiation(float dt)
        {
            var player = GM ? GM.player : null;
            bool inside = Enemy.TickRadiationZones(dt, player ? player.Position : Vector3.positiveInfinity);
            if (!inside || !player || !player.IsAlive) return;
            radiationClock += dt;
            float tick = Mathf.Max(.05f, radiationTickSeconds);
            while (radiationClock >= tick && player.IsAlive)
            {
                radiationClock -= tick;
                player.ReceiveDamage(new DamageInfo { amount = Mathf.Max(0f, radiationTickDamage), source = DamageSource.Burn, ignoreShield = true });
            }
        }

        void BeginBatch()
        {
            if (TimeExpired || TimeLeft <= 0f) return;
            BatchIndex++;
            batchSpawnsRemaining = PopulationTarget;
            var shares = ActiveRoleShares;
            swarmRemaining = Mathf.Clamp(Mathf.RoundToInt(batchSpawnsRemaining * shares.x), 0, batchSpawnsRemaining);
            int eliteWanted = Mathf.Clamp(Mathf.RoundToInt(batchSpawnsRemaining * shares.z), 0, batchSpawnsRemaining - swarmRemaining);
            functionRemaining = batchSpawnsRemaining - swarmRemaining - eliteWanted;
                                                                                                                              
            if (functionRemaining == 0 && shares.y > 0f && swarmRemaining > 1) { swarmRemaining--; functionRemaining++; }
            eliteRemaining = Mathf.Min(eliteWanted, Mathf.Max(0, maxConcurrentElites - ActiveEliteCount));
            swarmRemaining += eliteWanted - eliteRemaining;
            spawnTimer = 0f;
        }

        void RefillPopulation()
        {
            if (TimeExpired || TimeLeft <= 0f) return;
            int small = 0, functional = 0, elite = 0;
            foreach (var enemy in alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.definition || enemy.IsBoss) continue;
                if (enemy.Elite) elite++; else if (IsSwarm(enemy.definition)) small++; else functional++;
            }
            foreach (var marker in preparedMarkers)
            {
                if (!marker || !marker.gameObject.activeSelf || !marker.definition) continue;
                if (marker.elite) elite++; else if (IsSwarm(marker.definition)) small++; else functional++;
            }
            int target = PopulationTarget;
            var shares = ActiveRoleShares;
            int eliteShareCount = Mathf.RoundToInt(target * shares.z), eliteTarget = Mathf.Min(eliteShareCount, Mathf.Max(0, maxConcurrentElites));
            int smallTarget = Mathf.RoundToInt(target * shares.x) + eliteShareCount - eliteTarget;
            if (shares.y > 0f && target - smallTarget - eliteTarget <= 0 && smallTarget > 1) smallTarget--;                                      
            int missingSmall = Mathf.Max(0,smallTarget-small), missingElite = Mathf.Max(0,eliteTarget-elite);
            int missingFunctional = Mathf.Max(0,target-smallTarget-eliteTarget-functional);
            int missing = missingSmall+missingFunctional+missingElite;
            if (missing < Mathf.Max(1,Mathf.CeilToInt(target*refillMissingShare))) return;
                                                                                                       
            swarmRemaining = missingSmall; functionRemaining = missingFunctional; eliteRemaining = missingElite;
            batchSpawnsRemaining = missing; BatchIndex++; spawnTimer = 0f;
        }

        void IssueBatchMarkers()
        {
            float affixShare = OrdinaryAffixShare(WaveIndex);
            while (batchSpawnsRemaining > 0 && !TimeExpired && TimeLeft > 0f)
            {
                int roll = Random.Range(0, batchSpawnsRemaining);
                int quota = roll < swarmRemaining ? 0 : roll < swarmRemaining + functionRemaining ? 1 : 2;
                int group = quota == 2 && ActiveEliteCount >= maxConcurrentElites ? 0 : quota;                                                
                var data = RollBatchEnemy(group);
                if (!data && group > 0) data = RollBatchEnemy(group = 0);                                                                            
                if (!data) data = DefinitionOf(EnemyKind.Grunt);
                if (!data) break;
                bool elite = group == 2;
                                                                                                                                 
                                                                  
                var affix = !data.canAffix ? ElementKind.None
                    : elite ? PickEliteAffix()
                    : EnemyRoles.IsSpecial(data.kind) ? ElementKind.None
                    : Random.value < affixShare ? Random.value < .5f ? waveAffixA : waveAffixB : ElementKind.None;
                if (!PlaceMarker(data, elite, affix)) break;
                if (quota == 0) swarmRemaining--; else if (quota == 1) functionRemaining--; else eliteRemaining--;
                batchSpawnsRemaining--;
            }
            spawnTimer = Mathf.Max(.02f, batchSpawnRetry);
        }

        bool IsSwarm(EnemyDefinition data) => EnemyRoles.IsMelee(data.kind);
        int ShadeCap => WaveIndex + 1 >= shadePairWave ? 2 : 1;
        int ShadeCount()
        {
            int count = 0;
            foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Shade) count++;
            if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.definition && marker.definition.kind == EnemyKind.Shade) count++;
            return count;
        }
                     
                                                                                                                                       
                                                                                                          
                      
        int BatchWeight(WaveSet.Definition wave, int index, int group, bool constrainActive = true)
        {
            if (roster == null || index >= roster.Length || !roster[index] || EnemyRoles.IsBoss(roster[index].kind)) return 0;
            var data = roster[index];
            bool allowed = group == 0 ? IsSwarm(data) : group == 1 ? !IsSwarm(data) : EnemyRoles.IsSpecial(data.kind) && data.canElite;
            if (!allowed) return 0;
            int wave1 = WaveIndex + 1;
            if (data.kind == EnemyKind.Spitter && wave1 < rangedFirstWave || data.kind == EnemyKind.Warden && wave1 < supportFirstWave
                || group == 2 && wave1 < firstEliteWave) return 0;
            int weight = TiltedWeight(wave, (int)data.kind);
            if (weight <= 0) return 0;
            if (constrainActive && data.kind == EnemyKind.Shade && ShadeCount() >= ShadeCap) return 0;
            if (constrainActive && !HasSpawnCapacity(data)) return 0;
            return weight;
        }
        bool HasSpawnCapacity(EnemyDefinition data)
        {
            if (preparedEnemies == null) return false;
            int slots = 0, incoming = 0;
            foreach (var enemy in preparedEnemies)
            {
                if (!enemy || enemy.definition != data || alive.Contains(enemy) || enemy.CorpseCarried) continue;
                if (!enemy.gameObject.activeSelf && !enemy.CorpseActive || enemy.CorpseActive && (!WorldMap.Active || !WorldMap.Active.IsBattlefieldFrozen(enemy.gameObject))) slots++;
            }
            foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.definition == data) incoming++;
            return slots > incoming;
        }
        EnemyDefinition RollBatchEnemy(int group)
        {
            var wave = waveSet.waves[WaveIndex];
            int total = 0;
            for (int i = 0; i < roster.Length; i++) total += BatchWeight(wave, i, group);
            if (total <= 0) return null;
            int roll = Random.Range(0, total);
            for (int i = 0; i < roster.Length; i++) { roll -= BatchWeight(wave, i, group); if (roll < 0) return roster[i]; }
            return null;
        }

        EnemyDefinition RollEnemy(WaveSet.Definition wave)
        {
            int total = 0;
            for (int i = 0; i < roster.Length; i++) total += SummonWeight(wave, i);
            int roll = Random.Range(0, Mathf.Max(1, total));
            for (int i = 0; i < roster.Length; i++) { roll -= SummonWeight(wave, i); if (roll < 0) return roster[i]; }
            return DefinitionOf(EnemyKind.Grunt);
        }
                                                                                                                   
        int SummonWeight(WaveSet.Definition wave, int index) => index < roster.Length && roster[index] && !EnemyRoles.IsSpecial(roster[index].kind)
            && !EnemyRoles.IsBoss(roster[index].kind) ? TiltedWeight(wave, (int)roster[index].kind) : 0;

        int TiltedWeight(WaveSet.Definition wave, int kindIndex)
        {
            if (wave.weights == null || kindIndex < 0 || kindIndex >= wave.weights.Length || wave.weights[kindIndex] <= 0) return 0;
            var tilt = WorldMap.Active && WorldMap.Active.biome ? WorldMap.Active.biome.mixTilt : null;
            return Mathf.Max(0, wave.weights[kindIndex] + (tilt != null && kindIndex < tilt.Length ? Mathf.Clamp(tilt[kindIndex], -10, 10) : 0));
        }

        bool PlaceMarker(EnemyDefinition data, bool elite, ElementKind affix)
        {
            if (!HasSpawnCapacity(data)) return false;
            if (data.kind == EnemyKind.Shade && ShadeCount() >= ShadeCap)
            {
                data = DefinitionOf(EnemyKind.Grunt); elite = false; affix = ElementKind.None;
                if (!data) return false;
            }
            foreach (var marker in preparedMarkers)
                if (!marker.gameObject.activeSelf)
                {
                    if (!TryFindV2SpawnPoint(data, out var point)) return false;
                    marker.Begin(point, data, elite, affix);
                    return true;
                }
            return false;
        }

        void TickPreparedMarkers(float dt)
        {
            foreach (var marker in preparedMarkers)
            {
                if (!marker.gameObject.activeSelf) continue;
                if (!ValidSpawnPoint(marker.transform.position, marker.definition))
                {
                    if (TryFindV2SpawnPoint(marker.definition, out var replacement)) marker.Begin(replacement, marker.definition, marker.elite, marker.affix, marker.bossSummon);
                    continue;
                }
                marker.timer -= dt;
                if (marker.timer > 0f) continue;
                var spawned = SpawnPrepared(marker.definition, marker.transform.position, marker.elite, marker.affix);
                if (spawned)
                {
                    if (marker.bossSummon) spawned.SuppressRewards = true;                                   
                    marker.gameObject.SetActive(false);
                }
                else if (marker.bossSummon) marker.gameObject.SetActive(false);                                              
                else marker.timer = Mathf.Max(.02f, batchSpawnRetry);                                                           
            }
        }

                                                                                                              
                                                                                                                                         
        Enemy SpawnPrepared(EnemyDefinition data, Vector3 at, bool elite, ElementKind affix = ElementKind.None, bool mini = false, bool ignoreClock = false)
        {
            if (!data || preparedEnemies == null) return null;
            if (!trialSuspended && (Phase is not (WavePhase.WaveActive or WavePhase.FinalBoss)
                || !ignoreClock && Phase == WavePhase.WaveActive && (TimeExpired || TimeLeft <= 0f))) return null;
            foreach (var enemy in preparedEnemies)
            {
                                                                                                     
                if (!enemy || enemy.gameObject.activeSelf || enemy.CorpseActive || alive.Contains(enemy) || enemy.definition != data) continue;
                enemy.transform.position = new Vector3(at.x, 0f, at.z);
                enemy.gameObject.SetActive(true);
                enemy.Configure(data, HpMultiplier, elite, affix, mini);
                enemy.IsTrialSummon = trialSuspended;
                corpseOrder.Remove(enemy);
                Track(enemy);
                return enemy;
            }
            Enemy oldest = null;
            int oldestOrder = int.MaxValue;
            foreach (var enemy in preparedEnemies)
            {
                if (!enemy || enemy.definition != data || !enemy.CorpseActive || enemy.CorpseCarried || alive.Contains(enemy)
                    || WorldMap.Active && WorldMap.Active.IsBattlefieldFrozen(enemy.gameObject)) continue;
                int order = corpseOrder.TryGetValue(enemy, out int recorded) ? recorded : 0;
                if (order < oldestOrder) { oldest = enemy; oldestOrder = order; }
            }
            if (!oldest) return null;
            oldest.DespawnCorpse(oldest.SpawnVersion);
            if (oldest.CorpseActive) return null;
            corpseOrder.Remove(oldest);
            oldest.transform.position = new Vector3(at.x, 0f, at.z);
            oldest.gameObject.SetActive(true); oldest.Configure(data, HpMultiplier, elite, affix, mini);
            oldest.IsTrialSummon = trialSuspended; Track(oldest); return oldest;
        }
        void Track(Enemy enemy)
        {
            enemy.Eliminated -= OnEnemyEliminated;
            enemy.Eliminated += OnEnemyEliminated;
            if (!alive.Contains(enemy)) alive.Add(enemy);
            EnemySpawned?.Invoke(enemy);
        }
        public void TrackBoss(Enemy enemy) => Track(enemy);

                     
                                                                                                                            
                                                                                                                    
                      
        public void QueueSplit(Enemy parent)
        {
            if (!parent || !parent.definition || splitterMiniCount <= 0) return;
            Vector3 side = Vector3.Cross(Vector3.up, Query.Flat(parent.AimDirection));
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            pendingSplits.Add(new PendingSplit { data = parent.definition, at = Query.Flat(parent.Position), side = side.normalized, suppressRewards = parent.SuppressRewards });
        }
                                                                                                                                                                    
        public int SpawnPendingSplits()
        {
            if (pendingSplits.Count == 0) return 0;
            var queue = pendingSplits.ToArray();
            pendingSplits.Clear();
            int spawned = 0, count = Mathf.Max(0, splitterMiniCount);
            foreach (var split in queue)
            {
                float body = split.data.radius * splitterMiniScale;
                for (int i = 0; i < count; i++)
                {
                    Vector3 at = split.at + Quaternion.Euler(0f, 360f * i / count, 0f) * split.side * splitterMiniOffset;
                    var world = WorldMap.Active;
                    if (world && !world.TrialActive) { at = world.ClampPoint(at, body); if (!world.IsWalkable(at, body, true)) at = split.at; }
                    var mini = SpawnPrepared(split.data, at, false, ElementKind.None, true, true);
                    if (!mini) continue;                                                                                                      
                    mini.SuppressRewards = split.suppressRewards;
                    mini.IsTrialSummon = false;
                    spawned++;
                }
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSplit, split.at);
            }
            return spawned;
        }

        public void SpawnExtras(EnemyKind kind, int count, Vector3 centre, bool bossSummon)
        {
            var data = DefinitionOf(kind);
            if (!data) return;
            count = ScaledOrdinaryCount(count);
            if (!trialSuspended && Phase == WavePhase.WaveActive && (TimeExpired || TimeLeft <= 0f)) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = centre + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 4f;
                if (!trialSuspended && WorldMap.Active) at = WorldMap.Active.ClampPoint(at, 1f);
                if (!trialSuspended && !ValidSpawnPoint(at, data) && !TryFindV2SpawnPoint(data, out at)) continue;
                var enemy = SpawnPrepared(data, at, false);
                if (enemy) { enemy.SuppressRewards = bossSummon; enemy.IsTrialSummon = trialSuspended; }
            }
        }
                                                                                                                            
        public void SpawnExtrasAt(EnemyKind kind, IReadOnlyList<Vector3> points, bool bossSummon)
        {
            var data = DefinitionOf(kind);
            if (!data || points == null) return;
            if (!trialSuspended && Phase == WavePhase.WaveActive && (TimeExpired || TimeLeft <= 0f)) return;
            foreach (var at in points)
            {
                var enemy = SpawnPrepared(data, at, false);
                if (enemy) { enemy.SuppressRewards = bossSummon; enemy.IsTrialSummon = trialSuspended; }
            }
        }
                                                                                                                                      
        public WaveSpawnMarker BorrowMarker()
        {
            if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && !marker.gameObject.activeSelf) return marker;
            return null;
        }
                                                                                                                                                                     
        public Enemy SpawnLab(EnemyKind kind, Vector3 at, bool elite, ElementKind affix)
        {
            var data = DefinitionOf(kind);
            if (!data) return null;
            bool asElite = elite && data.canElite;
            return SpawnPrepared(data, at, asElite, asElite || data.canAffix ? affix : ElementKind.None);
        }
                                                                                                             
        public Enemy SpawnLabTyrant(Vector3 at)
        {
            if (!tyrant || tyrant.IsAlive) return tyrant;
            tyrant.gameObject.SetActive(true);
            tyrant.transform.position = new Vector3(at.x, 0f, at.z);
            tyrant.Configure(tyrant.definition, waveSet ? Mathf.Max(1f, waveSet.finalBossHpFactor) : 1f, false);
            var board = ElementStatus.For(tyrant);
            foreach (var element in TyrantImmunities) board?.SetImmunity(element, 1f);
            tyrant.HoldAttacks(bosses.tyrantOpeningGrace);
            Track(tyrant);
            return tyrant;
        }
                                                                                     
        public void ClearLab()
        {
            foreach (var enemy in alive.ToArray())
                if (enemy && enemy.IsAlive) { enemy.SuppressRewards = true; enemy.Kill(); }
        }
                                                                                                                                                                  
        public Enemy SpawnIntroFodder(Vector3 at)
        {
            var data = DefinitionOf(EnemyKind.Grunt);
            if (!data) return null;
            var enemy = SpawnPrepared(data, at, false);
            if (enemy) enemy.SuppressRewards = true;
            return enemy;
        }

        public void SpawnTyrantSummons(int count, Vector3 centre)
        {
            count = ScaledOrdinaryCount(count);
            var mix = waveSet.waves[waveSet.waves.Length - 1];
            for (int i = 0; i < count; i++)
            {
                var data = RollEnemy(mix);                                                                            
                if (!data) continue;
                Vector3 at = centre + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 4f;
                if (WorldMap.Active) at = WorldMap.Active.ClampPoint(at, 1f);
                if (!ValidSpawnPoint(at, data) && !TryFindV2SpawnPoint(data, out at)) continue;
                                                                                                                  
                var marker = BorrowMarker();
                if (marker) { marker.Begin(at, data, false, ElementKind.None, true); continue; }
                var enemy = SpawnPrepared(data, at, false);
                if (enemy) enemy.SuppressRewards = true;
            }
        }
        public void LaunchSpit(Enemy owner, Vector3 at, Vector3 direction)
        {
            var bolt = FreeBolt();
            if (bolt) bolt.Launch(owner, at, direction);
        }
                                                                                                                                              
        public bool LaunchBossBolt(Enemy owner, Vector3 at, Vector3 direction, float damage, float knockback, float speed)
        {
            var bolt = FreeBolt();
            if (!bolt) return false;
            bolt.Launch(owner, at, direction, damage, knockback, speed);
            return true;
        }
        EnemyBolt FreeBolt()
        {
            foreach (var bolt in preparedBolts) if (!bolt.gameObject.activeSelf && (!WorldMap.Active || !WorldMap.Active.IsBattlefieldFrozen(bolt.gameObject))) return bolt;
            return null;
        }
        public Vector3 FindV2SpawnPoint(EnemyDefinition data) => TryFindV2SpawnPoint(data, out var point) ? point : GM.player.Position;

                     
                                                                                                                         
                                                                                                                                            
                      
        public bool TryFindV2SpawnPoint(EnemyDefinition data, out Vector3 point)
        {
            float minimum = Mathf.Max(nearbySpawnMin, data.kind == EnemyKind.Shade ? 12f : 1f);
            float maximum = Mathf.Max(minimum + .5f, nearbySpawnMax);
            int perSector = Mathf.Clamp(spawnPointCandidates / 8, 2, 20);
            bool stomper = data.kind == EnemyKind.Stomper;
            int wanted = stomper ? Mathf.Clamp(stomperSpawnCandidates, 1, 8) : 1, found = 0, bestCrowd = int.MaxValue, bestSector = 0;
            Vector3 best = Vector3.zero;
                                                                                                                                           
            for (int offset = 0; offset < 8 && found < wanted; offset++)
            {
                int sector = (spawnSectorCursor + offset) % 8;
                for (int pass = 0; pass < 2; pass++)
                {
                    bool taken = false;
                    for (int attempt = 0; attempt < perSector + 4 && !taken; attempt++)
                    {
                        float angle = sector * 45f + (attempt < perSector ? Random.Range(-18f,18f) : 0f);
                        float distance = attempt < perSector ? Random.Range(minimum,maximum) : Mathf.Lerp(minimum,maximum,(attempt-perSector+.5f)/4f);
                        Vector3 candidate = GM.player.Position + Quaternion.Euler(0f,angle,0f) * Vector3.forward * distance;
                        if (pass == 0 && !OffscreenSpawn(candidate)) continue;                               
                        if (!ValidSpawnPoint(candidate, data)) continue;
                        int crowd = stomper ? CrowdAround(candidate, stomperCrowdRadius) : 0;
                        if (crowd < bestCrowd) { bestCrowd = crowd; best = candidate; bestSector = sector; }
                        found++;
                        taken = true;                                                                              
                    }
                    if (taken) break;
                }
            }
            if (found == 0) { point = Vector3.zero; return false; }
            spawnSectorCursor = (bestSector + 1) % 8;
            point = best;
            return true;
        }
        int CrowdAround(Vector3 point, float radius)
        {
            int count = 0;
            float limit = radius * radius;
            foreach (var enemy in alive) if (enemy && enemy.IsAlive && Query.Flat(enemy.Position - point).sqrMagnitude <= limit) count++;
            return count;
        }

        bool ValidSpawnPoint(Vector3 point, EnemyDefinition data)
        {
            float distance = Query.Flat(point - GM.player.Position).magnitude;
            float minimum = Mathf.Max(nearbySpawnMin, data.kind == EnemyKind.Shade ? 12f : GM.player.Radius + data.radius + 1f);
            if (distance < minimum || distance > Mathf.Max(minimum + .5f, nearbySpawnMax)) return false;
            if (!WorldMap.Active || !WorldMap.Active.IsWalkable(point, data.radius)) return false;
            if (!OffscreenSpawn(point) && distance < Mathf.Max(minimum, visibleSpawnSafety)) return false;
            foreach (var other in preparedMarkers)
            {
                if (!other || !other.gameObject.activeSelf || (other.transform.position - point).sqrMagnitude < .001f) continue;
                if (Query.Flat(other.transform.position - point).magnitude < data.radius + other.definition.radius + .5f) return false;
            }
            foreach (var enemy in alive) if (enemy && enemy.IsAlive && Query.Flat(enemy.Position - point).magnitude < data.radius + enemy.radius + .5f) return false;
            return data.kind != EnemyKind.Spire || WorldMap.Active.IsWalkable(point, 8f) && !WorldMap.Active.ClosestHole(point, 8f).HasValue;
        }
        bool OffscreenSpawn(Vector3 point)
        {
            if (!GM.cam) return false;
            var view = GM.cam.WorldToViewportPoint(point);
            return view.z <= 0f || view.x < 0f || view.x > 1f || view.y < 0f || view.y > 1f;
        }
        void EndV2Wave()
        {
            Phase = WavePhase.WaitingBuild;
            BetweenWaves = true;
            TimeLeft = 0f;
            foreach (var marker in preparedMarkers) marker.gameObject.SetActive(false);
            foreach (var bolt in preparedBolts) bolt.gameObject.SetActive(false);
            alive.Clear();
            batchSpawnsRemaining = 0; corpseOrder.Clear(); pendingSplits.Clear();
            Enemy.ClearRadiationZones(); radiationClock = 0f;
            GM.ClearTransients();
                                                                                                              
            WorldMap.Active?.OnWaveEnded(WaveIndex + 1);
            WaveCompleted?.Invoke(WaveIndex + 1);
        }
        public void SuspendForTrial() => trialSuspended = true;
        public void ResumeAfterTrial() => trialSuspended = false;
        public void ClearTrialSummons()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
                if (alive[i] && alive[i].IsTrialSummon) { alive[i].DiscardWithoutReward(); alive.RemoveAt(i); }
        }

        void Update()
        {
            if (!running || GM.State != GameState.Playing) return;
            TickV2(GM.WorldDeltaTime);
        }

        void OnEnemyEliminated(Combatant c)
        {
            var e = c as Enemy;
            alive.Remove(e);
            Kills++;
            if (e) corpseOrder[e] = ++deathSequence;
            EnemyEliminated?.Invoke(e);
        }
    }
}
