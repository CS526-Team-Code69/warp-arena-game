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
        [Serializable]
        public class Wave
        {
            public int small;
            public int large;
        }

        public Wave[] waves =
        {
            new Wave { small = 4, large = 1 },
            new Wave { small = 8, large = 2 },
            new Wave { small = 15, large = 5 },
        };
        [Tooltip("Maximum number of enemies alive at once (to be decided)")] public int maxAlive = 12;
        public float spawnTelegraph = 1f;
        public float minSpawnDistance = 6f;
        [Tooltip("Interval between consecutive X markers")] public float spawnInterval = 0.25f;
        public float betweenWaveDelay = 2.5f;
        [Tooltip("Refill the player's HP before each wave")] public bool refillHpEachWave = true;
        [Tooltip("When not refilling, HP restored between waves (capped at max)")] public float waveHeal = 20f;

        public Enemy smallPrefab;
        public Enemy largePrefab;

        [Header("v2 saved pools and data")]
        public bool v2;
        public WaveSet waveSet;
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
        public float MovementSpeedScale => v2 ? Mathf.Max(0f, movementSpeedMultiplier) : 1f;
        int ScaledOrdinaryCount(int count) => v2 ? Mathf.CeilToInt(Mathf.Max(0, count) * Mathf.Max(0f, spawnCountMultiplier)) : count;
        [Header("Swarm and status roles: batch composition.")]
        [Range(0f,1f)] public float swarmShare = .6f;
        [Range(0f,1f)] public float functionShare = .2f;
        [Range(0f,1f)] public float eliteShare = .2f;
        [Min(.01f), Tooltip("XP calibration adjustment. One uses the estimated old HP/XP budget and new composition, not measured pacing.")] public float experienceRateMultiplier = 1f;
        public float ExperienceRewardScale { get; private set; } = 1f;
        [SerializeField] int crowdBalanceRevision;
        int swarmRemaining, functionRemaining, eliteRemaining;
        [Header("Candidate sustainable swarm density.")]
        [Min(1)] public int maxConcurrentOrdinary = 300;
        [HideInInspector] public float swarmExperienceMultiplier = .03f;                                                              
        [Min(0f), Tooltip("Fixed XP per fodder enemy; unaffected by wave XP correction or affix bonuses.")] public float swarmExperienceReward = .1f;
        [Min(1)] public int firstEliteWave = 6;
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
        public float ExperienceFor(Enemy enemy) => !enemy.Elite && enemy.definition && IsSwarm(enemy.definition) ? Mathf.Max(0f,swarmExperienceReward) : enemy.XpValue * ExperienceRewardScale;
        [Header("Player-near batch spawning")]
        [Tooltip("Inner radius of the regular spawn ring; does not change per-wave stat growth.")] public float nearbySpawnMin = 8f;
        [Tooltip("Spawn candidates stay within this player-relative distance; distant map anchors are excluded.")] public float nearbySpawnMax = 18f;
        [Tooltip("Minimum safe distance for onscreen spawns when an offscreen point cannot be found.")] public float visibleSpawnSafety = 10f;
        [Tooltip("Maximum random candidates per spawn-location search.")] public int spawnPointCandidates = 80;
        [Tooltip("Retry interval when valid spawn locations or pooled enemies are unavailable.")] public float batchSpawnRetry = .1f;
        public int BatchIndex { get; private set; }
        public bool TimeExpired { get; private set; }
        public int LivingCount { get { int count = 0; foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy) count++; return count; } }
        public int PendingCount { get { int count = batchSpawnsRemaining; if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf) count++; return count; } }
        public ElementKind[] TyrantImmunities { get; private set; } = new ElementKind[3];
        public WavePhase Phase { get; private set; }
        public float TimeLeft { get; private set; }
        public float HpMultiplier => v2 && waveSet && WaveIndex >= 0 ? waveSet.waves[Mathf.Min(WaveIndex, waveSet.waves.Length - 1)].hpMultiplier * waveSet.ExtraHp(WaveIndex+1) : 1f;
        public float EnemyDamageFactor => v2 && waveSet ? waveSet.DamageFactor(WaveIndex+1) : 1f;

        public float CombatScale => v2 && waveSet && waveSet.encounterBalance && WaveIndex >= 0 ? waveSet.encounterBalance.DpsAt(WaveIndex + 1) / waveSet.encounterBalance.DpsAt(1) : 1f;
        public float EnemySpeedFactor => (v2 && waveSet ? waveSet.SpeedFactor(WaveIndex+1) : 1f) * MovementSpeedScale;
        public float BuildTime => v2 && waveSet && WaveIndex >= 0 ? waveSet.waves[WaveIndex].buildTime : 20f;
        public event Action<int> WaveCompleted;
        public event Action<Enemy> EnemySpawned;
        public event Action<Enemy> EnemyEliminated;
        bool trialSuspended;
        bool finalBossPending;
        bool[] eliteIssued;
        int spiresIssued;
        ElementKind waveAffixA, waveAffixB;
        int batchSpawnsRemaining, deathSequence;
        readonly Dictionary<Enemy, int> corpseOrder = new();

        public int WaveIndex { get; private set; } = -1;
        public int WaveCount => v2 && waveSet ? waveSet.waves.Length : waves.Length;
        public bool BetweenWaves { get; private set; }
        public float BetweenTimer { get; private set; }
        public int Remaining => v2 ? LivingCount + PendingCount : queue.Count + markers.Count + alive.Count;
        public IReadOnlyList<Enemy> Alive => alive;
        public int Kills { get; private set; }

        readonly List<bool> queue = new List<bool>();                
        readonly List<SpawnMarker> markers = new List<SpawnMarker>();
        readonly List<Enemy> alive = new List<Enemy>();
        float spawnTimer;
        bool running;

        static GameManager GM => GameManager.Instance;

        public void StartWaves()
        {
            running = true;
            if (v2)
            {
                var elements = new[] { ElementKind.Fire, ElementKind.Poison, ElementKind.Frost, ElementKind.Lightning, ElementKind.Earth, ElementKind.Light, ElementKind.Void };
                for (int i = 0; i < TyrantImmunities.Length; i++)
                {
                    int choice = Random.Range(i, elements.Length);
                    (elements[i], elements[choice]) = (elements[choice], elements[i]);
                    TyrantImmunities[i] = elements[i];
                }
                BeginV2Wave(0);
                return;
            }
            BeginWave(0);
        }

        public void BeginNextWave()
        {
            if (WaveIndex + 1 >= WaveCount) { BeginFinalBoss(); return; }
            BeginV2Wave(WaveIndex + 1);
        }

        void BeginV2Wave(int index)
        {
            WaveIndex = index;
            Phase = WavePhase.WaveIntro;
            TimeLeft = 3f;
            BetweenWaves = false;
            spawnTimer = 0f;
            BatchIndex = 0; TimeExpired = false; batchSpawnsRemaining = 0;
            spawnSectorCursor = Random.Range(0,8);
            spiresIssued = 0;

            eliteIssued = new bool[waveSet.waves[index].eliteCount];
            ExperienceRewardScale = CalculateExperienceRewardScale();
            int available = index < 4 ? 0 : index < 7 ? 3 : index < 10 ? 5 : 7;
            waveAffixA = available > 0 ? (ElementKind)Random.Range(1, available + 1) : ElementKind.None;
            waveAffixB = available > 0 ? (ElementKind)Random.Range(1, available + 1) : ElementKind.None;
            if (GM.hud) GM.hud.Banner(ArenaText.Pair($"Wave {index + 1} / 15",$"Wave {index + 1} / 15"), available > 0 ? $"{ArenaTerms.Element(waveAffixA)} · {ArenaTerms.Element(waveAffixB)}" : ArenaText.Pair("Interstellar Battlefield","Interstellar Battlefield"));
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
            Track(tyrant);
            finalBossPending = false;
            if (GM.hud) GM.hud.Banner(ArenaText.Pair("THE TYRANT","THE TYRANT"), ArenaText.Pair("Break its Shield. Make it charge into a wall.","Break its Shield. Make it charge into a wall."));
        }

        void TickV2(float dt)
        {
            if (dt <= 0f || trialSuspended || Phase == WavePhase.WaitingBuild || Phase == WavePhase.Idle) return;
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
                if (LivingCount == 0) { EndV2Wave(); return; }
                TickDistantSurvivors(dt);
                return;                                                                            
            }
            spawnTimer -= dt;
            if (LivingCount == 0 && PendingCount == 0) BeginBatch();
            else if (batchSpawnsRemaining == 0) RefillPopulation();
            if (batchSpawnsRemaining > 0 && spawnTimer <= 0f) IssueBatchMarkers();

            TickPreparedMarkers(dt);
            TickDistantSurvivors(dt);
        }

        void BeginBatch()
        {
            if (TimeExpired || TimeLeft <= 0f) return;
            BatchIndex++;
            batchSpawnsRemaining = PopulationTarget;
            var shares = ActiveRoleShares;
            swarmRemaining = Mathf.Clamp(Mathf.RoundToInt(batchSpawnsRemaining * shares.x), 0, batchSpawnsRemaining);
            eliteRemaining = Mathf.Clamp(Mathf.RoundToInt(batchSpawnsRemaining * shares.z), 0, batchSpawnsRemaining - swarmRemaining);
            functionRemaining = batchSpawnsRemaining - swarmRemaining - eliteRemaining;
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
            int smallTarget = Mathf.RoundToInt(target * shares.x), eliteTarget = Mathf.RoundToInt(target * shares.z);
            int missingSmall = Mathf.Max(0,smallTarget-small), missingElite = Mathf.Max(0,eliteTarget-elite);
            int missingFunctional = Mathf.Max(0,target-smallTarget-eliteTarget-functional);
            int missing = missingSmall+missingFunctional+missingElite;
            if (missing < Mathf.Max(1,Mathf.CeilToInt(target*refillMissingShare))) return;

            swarmRemaining = missingSmall; functionRemaining = missingFunctional; eliteRemaining = missingElite;
            batchSpawnsRemaining = missing; BatchIndex++; spawnTimer = 0f;
        }

        void IssueBatchMarkers()
        {
            float affixShare = WaveIndex < 4 ? 0f : WaveIndex < 7 ? .1f : WaveIndex < 10 ? .15f : .25f;
            while (batchSpawnsRemaining > 0 && !TimeExpired && TimeLeft > 0f)
            {
                int roll = Random.Range(0, batchSpawnsRemaining);
                int group = roll < swarmRemaining ? 0 : roll < swarmRemaining + functionRemaining ? 1 : 2;
                var data = RollBatchEnemy(group);
                var affix = data.canAffix && (group == 2 || Random.value < affixShare) ? Random.value < .5f ? waveAffixA : waveAffixB : ElementKind.None;
                if (!PlaceMarker(data, group == 2, affix)) break;
                if (group == 0) swarmRemaining--; else if (group == 1) functionRemaining--; else eliteRemaining--;
                if (data.kind == EnemyKind.Spire) spiresIssued++;
                batchSpawnsRemaining--;
            }
            spawnTimer = Mathf.Max(.02f, batchSpawnRetry);
        }

        void TickDistantSurvivors(float dt)
        {
            foreach (var enemy in alive)
            {
                if (!enemy || !enemy.IsAlive) continue;
                enemy.TickDistanceRecycle(dt, GM.player.Position);
            }
        }

        bool IsSwarm(EnemyDefinition data) => data.kind is EnemyKind.Grunt or EnemyKind.Skitter;
        int BatchWeight(WaveSet.Definition wave, int index, int group, bool constrainActive = true)
        {
            if (index >= roster.Length || !roster[index] || roster[index].kind is EnemyKind.Hollow or EnemyKind.Tyrant) return 0;
            var data = roster[index];
            bool allowed = group == 0 ? IsSwarm(data) : group == 1 ? !IsSwarm(data) : !IsSwarm(data) && data.canElite;
            if (!allowed) return 0;
            int unlockWave = data.kind switch {
                EnemyKind.Brute => 2,
                EnemyKind.Spitter or EnemyKind.Bloater => 3,
                EnemyKind.Rammer or EnemyKind.Warden or EnemyKind.Splitter => 4,
                EnemyKind.Stomper or EnemyKind.Shade => 5,
                EnemyKind.Spire => 10,
                _ => 1
            };
            if (WaveIndex + 1 < unlockWave || group == 2 && WaveIndex + 1 < firstEliteWave) return 0;
            int weight = index < wave.weights.Length ? TiltedWeight(wave, index) : 0;
            if (group > 0) weight = Mathf.Max(3,weight);
            if (group == 1 && data.kind == EnemyKind.Spire && WaveIndex + 1 >= 10)
                weight = !constrainActive || spiresIssued < ScaledOrdinaryCount(2) ? Mathf.Max(3, weight) : 0;
            if (constrainActive && data.kind == EnemyKind.Shade)
            {
                int shades = 0;
                foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.definition.kind == EnemyKind.Shade) shades++;
                foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.definition.kind == EnemyKind.Shade) shades++;
                if (shades >= (WaveIndex < 8 ? 1 : 2)) return 0;
            }
            if (constrainActive && !HasSpawnCapacity(data)) return 0;
            return weight;
        }
        bool HasSpawnCapacity(EnemyDefinition data)
        {
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
            if (total <= 0) return roster[(int)(group > 0 ? EnemyKind.Brute : EnemyKind.Grunt)];
            int roll = Random.Range(0, total);
            for (int i = 0; i < roster.Length; i++) { roll -= BatchWeight(wave, i, group); if (roll < 0) return roster[i]; }
            return roster[0];
        }
        Vector2 AverageHpXp(WaveSet.Definition wave, int group, bool elite, float affixChance, bool currentRewards = false)
        {
            float hp = 0f, xp = 0f, total = 0f;
            for (int i = 0; i < roster.Length; i++)
            {
                var data = roster[i]; if (!data || data.kind is EnemyKind.Hollow or EnemyKind.Tyrant) continue;
                int weight = group < 0 ? i < wave.weights.Length ? TiltedWeight(wave, i) : 0 : BatchWeight(wave, i, group, false);
                if (weight <= 0) continue;

                hp += weight * data.hp * (elite ? 3f : 1f) * (1f + data.startShieldShare);
                float reward = data.xp * (elite ? 5f : 1f) + (data.canAffix ? affixChance : 0f);
                if (currentRewards && !elite && IsSwarm(data)) reward = Mathf.Max(0f,swarmExperienceReward);
                xp += weight * reward; total += weight;
            }
            if (total <= 0)
            {
                var fallback = roster[(int)(group > 0 ? EnemyKind.Brute : EnemyKind.Grunt)];
                return new Vector2(fallback.hp * (elite ? 3f : 1f), fallback.xp * (elite ? 5f : 1f));
            }
            return new Vector2(hp / total, xp / total);
        }
        public float CalculateExperienceRewardScale()
        {
            if (!v2 || !waveSet || WaveIndex < 0) return 1f;
            var wave = waveSet.waves[WaveIndex];
            float affix = WaveIndex < 4 ? 0f : WaveIndex < 7 ? .1f : WaveIndex < 10 ? .15f : .25f;
            var oldRegular = AverageHpXp(wave, -1, false, affix);
            var oldElite = AverageHpXp(wave, 2, true, WaveIndex < 4 ? 0f : 1f);
            float oldEliteRatio = Mathf.Clamp01((eliteIssued?.Length ?? wave.eliteCount) / (float)Mathf.Max(1, wave.aliveTarget + wave.eliteCount));
            var oldMean = Vector2.Lerp(oldRegular, oldElite, oldEliteRatio);
            var shares = ActiveRoleShares;
            if (shares.y + shares.z <= 0f) return 1f;                                                                                   
            var newMean = AverageHpXp(wave, 0, false, affix, true) * shares.x + AverageHpXp(wave, 1, false, affix, true) * shares.y + AverageHpXp(wave, 2, true, 1f, true) * shares.z;
            float damageGain = GM && GM.runWeapons ? GM.runWeapons.crowdDamageMultiplier : 1f;
            float oldRate = oldMean.y / Mathf.Max(.01f, oldMean.x);
            float newRate = newMean.y / Mathf.Max(.01f, newMean.x) * Mathf.Max(.01f, damageGain);
            float legacyScale = Mathf.Clamp(experienceRateMultiplier * oldRate / Mathf.Max(.001f, newRate), .05f, 6f);
            if (!waveSet.encounterBalance) return legacyScale;

            float oldHp = 0f, newHp = 0f;
            for (int group = 0; group < 3; group++)
            {
                float weightSum = 0f, before = 0f, after = 0f;
                for (int i = 0; i < roster.Length; i++)
                {
                    var data = roster[i]; if (!data) continue;
                    int weight = BatchWeight(wave,i,group,false); if (weight <= 0) continue;
                    bool elite = group == 2;
                    float oldBody = group == 0 ? data.hp : Mathf.Round(data.hp * HpMultiplier * (elite ? 3f : 1f));
                    before += weight * oldBody * (1f + data.startShieldShare / waveSet.encounterBalance.ShieldDirectFactor);
                    after += weight * waveSet.encounterBalance.EffectiveHp(data,WaveIndex+1,elite);
                    weightSum += weight;
                }
                if (weightSum <= 0f) continue;
                float share = group == 0 ? shares.x : group == 1 ? shares.y : shares.z;
                oldHp += share * before / weightSum; newHp += share * after / weightSum;
            }
            float fixedFodderXp = shares.x * Mathf.Max(0f,swarmExperienceReward);
            float variableXp = shares.y * AverageHpXp(wave,1,false,affix,true).y
                + shares.z * AverageHpXp(wave,2,true,1f,true).y;
            float previousXp = fixedFodderXp + variableXp * legacyScale;
            return Mathf.Clamp((previousXp * newHp / Mathf.Max(.01f,oldHp) - fixedFodderXp)
                / Mathf.Max(.001f,variableXp), .05f, 6f);
        }

        EnemyDefinition RollEnemy(WaveSet.Definition wave)
        {
            int total = 0;
            for (int i = 0; i < wave.weights.Length; i++) total += TiltedWeight(wave, i);
            int roll = Random.Range(0, Mathf.Max(1, total));
            for (int i = 0; i < wave.weights.Length; i++) { roll -= TiltedWeight(wave, i); if (roll < 0) return roster[i]; }
            return roster[0];
        }

        int TiltedWeight(WaveSet.Definition wave, int index)
        {
            if (wave.weights[index] <= 0) return 0;
            var tilt = WorldMap.Active && WorldMap.Active.biome ? WorldMap.Active.biome.mixTilt : null;
            return Mathf.Max(0, wave.weights[index] + (tilt != null && index < tilt.Length ? Mathf.Clamp(tilt[index], -10, 10) : 0));
        }

        bool PlaceMarker(EnemyDefinition data, bool elite, ElementKind affix)
        {
            if (!HasSpawnCapacity(data)) return false;
            if (data.kind == EnemyKind.Shade)
            {
                int count = 0;
                foreach (var enemy in alive) if (enemy && enemy.definition.kind == EnemyKind.Shade) count++;
                foreach (var marker in preparedMarkers) if (marker.gameObject.activeSelf && marker.definition.kind == EnemyKind.Shade) count++;
                if (count >= (WaveIndex < 8 ? 1 : 2)) data = roster[0];
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
                    if (TryFindV2SpawnPoint(marker.definition, out var replacement)) marker.Begin(replacement, marker.definition, marker.elite, marker.affix);
                    continue;
                }
                marker.timer -= dt;
                if (marker.timer > 0f) continue;
                if (SpawnPrepared(marker.definition, marker.transform.position, marker.elite, marker.affix)) marker.gameObject.SetActive(false);
                else marker.timer = Mathf.Max(.02f, batchSpawnRetry);                                                           
            }
        }

        Enemy SpawnPrepared(EnemyDefinition data, Vector3 at, bool elite, ElementKind affix = ElementKind.None)
        {
            if (!trialSuspended && (Phase is not (WavePhase.WaveActive or WavePhase.FinalBoss) || Phase == WavePhase.WaveActive && (TimeExpired || TimeLeft <= 0f))) return null;
            foreach (var enemy in preparedEnemies)
            {

                if (enemy.gameObject.activeSelf || enemy.CorpseActive || alive.Contains(enemy) || enemy.definition != data) continue;
                enemy.transform.position = new Vector3(at.x, 0f, at.z);
                enemy.gameObject.SetActive(true);
                enemy.Configure(data, HpMultiplier, elite, affix);
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
            oldest.gameObject.SetActive(true); oldest.Configure(data, HpMultiplier, elite, affix);
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
        public void SpawnExtras(EnemyKind kind, int count, Vector3 centre, bool bossSummon)
        {
            count = ScaledOrdinaryCount(count);
            if (!trialSuspended && Phase == WavePhase.WaveActive && (TimeExpired || TimeLeft <= 0f)) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = centre + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 4f;
                if (!trialSuspended && WorldMap.Active) at = WorldMap.Active.ClampPoint(at, 1f);
                if (!trialSuspended && !ValidSpawnPoint(at, roster[(int)kind]) && !TryFindV2SpawnPoint(roster[(int)kind], out at)) continue;
                var enemy = SpawnPrepared(roster[(int)kind], at, false);
                if (enemy) { enemy.SuppressRewards = bossSummon; enemy.IsTrialSummon = trialSuspended; }
            }
        }
        public void SpawnTyrantSummons(int count, Vector3 centre)
        {
            count = ScaledOrdinaryCount(count);
            var mix = waveSet.waves[waveSet.waves.Length - 1];
            for (int i = 0; i < count; i++)
            {
                var data = RollEnemy(mix);                                                                   
                Vector3 at = centre + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 4f;
                if (WorldMap.Active) at = WorldMap.Active.ClampPoint(at, 1f);
                if (!ValidSpawnPoint(at, data) && !TryFindV2SpawnPoint(data, out at)) continue;
                var enemy = SpawnPrepared(data, at, false);
                if (enemy) enemy.SuppressRewards = true;
            }
        }
        public void LaunchSpit(Enemy owner, Vector3 at, Vector3 direction)
        {
            foreach (var bolt in preparedBolts) if (!bolt.gameObject.activeSelf && (!WorldMap.Active || !WorldMap.Active.IsBattlefieldFrozen(bolt.gameObject))) { bolt.Launch(owner, at, direction); return; }
        }
        public Vector3 FindV2SpawnPoint(EnemyDefinition data) => TryFindV2SpawnPoint(data, out var point) ? point : GM.player.Position;

        public bool TryFindV2SpawnPoint(EnemyDefinition data, out Vector3 point)
        {
            float minimum = Mathf.Max(nearbySpawnMin, data.kind == EnemyKind.Shade ? 12f : 1f);
            float maximum = Mathf.Max(minimum + .5f, nearbySpawnMax);
            int perSector = Mathf.Clamp(spawnPointCandidates / 8, 2, 20);

            for (int offset = 0; offset < 8; offset++)
            {
                int sector = (spawnSectorCursor + offset) % 8;
                for (int attempt = 0; attempt < perSector + 4; attempt++)
                {
                    float angle = sector * 45f + (attempt < perSector ? Random.Range(-18f,18f) : 0f);
                    float distance = attempt < perSector ? Random.Range(minimum,maximum) : Mathf.Lerp(minimum,maximum,(attempt-perSector+.5f)/4f);
                    point = GM.player.Position + Quaternion.Euler(0f,angle,0f) * Vector3.forward * distance;
                    if (!ValidSpawnPoint(point, data)) continue;
                    spawnSectorCursor = (sector+1)%8;
                    return true;
                }
            }
            point = Vector3.zero;
            return false;
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
            batchSpawnsRemaining = 0; corpseOrder.Clear();
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

        void BeginWave(int i)
        {
            WaveIndex = i;
            BetweenWaves = false;
            queue.Clear();
            for (int k = 0; k < waves[i].small; k++) queue.Add(false);
            for (int k = 0; k < waves[i].large; k++) queue.Add(true);
            for (int k = queue.Count - 1; k > 0; k--)
            {
                int j = Random.Range(0, k + 1);
                (queue[k], queue[j]) = (queue[j], queue[k]);
            }
            spawnTimer = 0.5f;
            GM.hud.Banner($"Wave {i + 1}", i == waves.Length - 1 ? "Final wave!" : $"{queue.Count} enemies incoming");
        }

        void Update()
        {
            if (!running || GM.State != GameState.Playing) return;
            if (v2) { TickV2(GM.WorldDeltaTime); return; }
            float dt = Time.deltaTime;

            if (BetweenWaves)
            {
                BetweenTimer -= dt;
                if (BetweenTimer <= 0f) BeginWave(WaveIndex + 1);
                return;
            }

            spawnTimer -= dt;
            if (queue.Count > 0 && alive.Count + markers.Count < maxAlive && spawnTimer <= 0f)
            {
                bool large = queue[queue.Count - 1];
                queue.RemoveAt(queue.Count - 1);
                markers.Add(SpawnMarker.Create(FindSpawnPoint(large), large, spawnTelegraph, GM.spawnMarkerMaterial));
                spawnTimer = spawnInterval;
            }

            TickMarkers(dt);

            if (queue.Count == 0 && markers.Count == 0 && alive.Count == 0) OnWaveCleared();
        }

        void TickMarkers(float dt)
        {
            var p = GM.player;
            for (int i = markers.Count - 1; i >= 0; i--)
            {
                var m = markers[i];
                float r = m.large ? largePrefab.radius : smallPrefab.radius;

                if (p.IsAlive && Query.Flat(p.Position - m.Position).magnitude < r + p.Radius)
                {
                    m.MoveTo(FindSpawnPoint(m.large));
                    m.timer = spawnTelegraph;
                    continue;
                }
                m.timer -= dt;
                if (m.timer > 0f) continue;
                markers.RemoveAt(i);
                var e = Instantiate(m.large ? largePrefab : smallPrefab, new Vector3(m.Position.x, 0f, m.Position.z), Quaternion.identity);
                Destroy(m.gameObject);
                e.Eliminated += OnEnemyEliminated;
                alive.Add(e);
            }
        }

        void OnEnemyEliminated(Combatant c)
        {
            var e = c as Enemy;
            alive.Remove(e);
            Kills++;
            if (v2) { if (e) corpseOrder[e] = ++deathSequence; EnemyEliminated?.Invoke(e); return; }
            if (e != null && e.IsLarge) GM.weapons.TryLargeDrop(e.Position);
        }

        void OnWaveCleared()
        {
            if (WaveIndex >= waves.Length - 1)
            {
                running = false;
                GM.NotifyFinalWaveCleared();
                return;
            }

            float before = GM.player.Hp;
            GM.player.Heal(refillHpEachWave ? GM.player.maxHp : waveHeal);
            GM.weapons.SpawnForWave(WaveIndex + 1);                                                              
            BetweenWaves = true;
            BetweenTimer = betweenWaveDelay;
            GM.hud.Banner($"Wave {WaveIndex + 1} cleared!", refillHpEachWave ? "HP fully restored" : $"HP +{Mathf.RoundToInt(GM.player.Hp - before)}");
        }

        Vector3 FindSpawnPoint(bool large)
        {
            float r = large ? largePrefab.radius : smallPrefab.radius;
            var p = GM.player;
            Vector3 best = Vector3.zero;
            float bestScore = -1f;
            for (int i = 0; i < 50; i++)
            {
                var pt = ArenaShape.RandomInside(r + 0.5f);
                float dp = Query.Flat(pt - p.Position).magnitude;
                float dOther = 99f;
                foreach (var m in markers) dOther = Mathf.Min(dOther, Query.Flat(m.Position - pt).magnitude - r);
                foreach (var e in alive) if (e) dOther = Mathf.Min(dOther, Query.Flat(e.Position - pt).magnitude - r - e.radius);
                if (dp >= minSpawnDistance && dOther > 0.5f) return pt;
                float score = Mathf.Min(dp, minSpawnDistance) + Mathf.Min(dOther, 0.5f);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = pt;
                }
            }
            return best;
        }
    }
}
