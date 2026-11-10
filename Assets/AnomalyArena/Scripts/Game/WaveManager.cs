using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AnomalyArena
{
    public enum WavePhase { Idle, WaveIntro, WaveActive, WaitingBuild, FinalBoss }
    /// <summary>
    /// V2: one numbered wave owns a clock; clearing each batch schedules the next saved batch.
    /// Timer expiry stops births, then living survivors must be cleared before WaveCompleted.
    /// Scene-authored warning markers and prepared enemies provide all V2 spawning.
    /// V5 (system doc §10–11): 14 ordinary waves whose weights list the allowed kinds, then the final boss as wave 15.
    /// No far-enemy recycling: survivors stay where they are (§10.4 abolished it).
    /// </summary>
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
        [Header("生成量和移动调节")]
        [Min(0f), Tooltip("普通批次、精英和召唤数量乘此值后向上取整；独立首领仍仅一个。")] public float spawnCountMultiplier = 1f;
        [Min(0f), Tooltip("追逐和固定冲撞速度倍率；攻击间隔、弹速、击退不变。")] public float movementSpeedMultiplier = 1f;
        [SerializeField] int pressurePickupRevision;
        public int PressurePickupRevision => pressurePickupRevision;
        public void MarkPressurePickupApplied() => pressurePickupRevision = 1;
        public float MovementSpeedScale => Mathf.Max(0f, movementSpeedMultiplier);
        int ScaledOrdinaryCount(int count) => Mathf.CeilToInt(Mathf.Max(0, count) * Mathf.Max(0f, spawnCountMultiplier));
        [Header("Batch composition")]
        [Range(0f,1f), Tooltip("Melee swarm share of a full batch.")] public float swarmShare = .6f;
        [Range(0f,1f), Tooltip("Share of a full batch taken by ordinary specials (Rammer, Bloater, Spire, Stomper, Shade, Chanter) once they have ramped in: " +
            "0.12 = 12%. Basics (melee kinds, Spitter, Warden) fill whatever specials and elites leave.")] public float specialShare = .12f;
        [Min(1), Tooltip("Waves the special share takes to reach specialShare, counted from the first wave whose table lists a special (wave 4 lists the Chanter): " +
            "5 = a fifth of it in that wave, all of it four waves later (system doc 11.2: specials join gradually).")] public int specialRampWaves = 5;
        [Range(0f,1f), Tooltip("Elite share of a full batch from firstEliteWave; capped by maxConcurrentElites, the rest becomes basics.")] public float eliteShare = .2f;
        [Range(0f,.5f), Tooltip("Smallest share of its group's spawn weight (basic, special or elite, after the map tilt) that every kind the wave lists keeps " +
            "while it can spawn: 0.1 = 10%. A map may shift the mix but never removes a listed kind; a group with too many kinds for this share splits evenly.")]
        public float minKindShare = .1f;
        [SerializeField] int crowdBalanceRevision;
        int basicRemaining, specialRemaining, eliteRemaining;
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
        // §7.6, §13.2: the Tyrant ignores the same two elements as the Nash Prophet, fixed for every run (no draw).
        static readonly ElementKind[] TyrantImmunityPool = new List<ElementKind>(ElementImmunity.Boss).ToArray();
        /// <summary>Elites alive plus elite warnings still waiting to spawn.</summary>
        public int ActiveEliteCount { get { int count = 0; foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.Elite) count++; if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.elite) count++; return count; } }
        /// <summary>A random element not carried by any alive or pending elite (all seven are candidates).</summary>
        public ElementKind PickEliteAffix()
        {
            var free = new List<ElementKind>(EliteElements);
            foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.Elite) free.Remove(enemy.Affix);
            if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.elite) free.Remove(marker.affix);
            return free.Count > 0 ? free[Random.Range(0, free.Count)] : ElementKind.None;
        }
        [Range(.05f,.5f)] public float refillMissingShare = .2f;
        [SerializeField] int continuousCrowdRevision;
        [Tooltip("前五关数量占最终密度的比例；之后使用完整密度。")] public float[] earlyPopulationScale = { .3f,.4f,.5f,.65f,.8f };
        [SerializeField] int quickFeedbackRevision;
        int spawnSectorCursor;
        /// <summary>
        /// Batch shares of the three spawn groups this wave (system doc 11.2): x = basics (melee kinds, Spitter, Warden), y = ordinary specials,
        /// z = elites. Specials ramp in from the first wave whose table lists one and reach specialShare specialRampWaves waves later; elites start
        /// at firstEliteWave and grow at waves 8 and 10. Basics fill the rest, so the late waves settle on fixed proportions.
        /// </summary>
        public Vector3 ActiveRoleShares => RoleShares(WaveIndex + 1);
        /// <summary>The same shares for any 1-based ordinary wave (the Tyrant's Royal Scepter calls a crowd mixed like wave 14).</summary>
        public Vector3 RoleShares(int wave)
        {
            int first = FirstSpecialWave();
            float special = first > 0 && wave >= first ? specialShare * Mathf.Clamp01((wave - first + 1f) / Mathf.Max(1, specialRampWaves)) : 0f;
            float elite = wave < firstEliteWave ? 0f : eliteShare * (wave < 8 ? .25f : wave < 10 ? .5f : 1f);
            return new Vector3(Mathf.Max(0f,1f-special-elite),special,elite);
        }
        /// <summary>The first 1-based ordinary wave whose table lists an ordinary special; 0 when none does.</summary>
        int FirstSpecialWave()
        {
            if (!waveSet || waveSet.waves == null) return 0;
            for (int i = 0; i < waveSet.waves.Length; i++)
            {
                var weights = waveSet.waves[i].weights;
                if (weights != null) for (int kind = 0; kind < weights.Length; kind++) if (weights[kind] > 0 && EnemyRoles.IsSpecial((EnemyKind)kind)) return i + 1;
            }
            return 0;
        }
        int PopulationTarget => PopulationTargetForWave(WaveIndex);
        public int PopulationTargetForWave(int index)
        {
            index = Mathf.Clamp(index,0,waveSet.waves.Length-1);
            float ramp = earlyPopulationScale != null && index < earlyPopulationScale.Length ? Mathf.Clamp01(earlyPopulationScale[index]) : 1f;
            return Mathf.Min(Mathf.Max(1,maxConcurrentOrdinary),Mathf.Max(1,Mathf.CeilToInt(waveSet.waves[index].aliveTarget*Mathf.Max(0f,spawnCountMultiplier)*ramp)));
        }
        /// <summary>
        /// System doc §9.2: melee swarm gives a fixed swarmExperienceReward and a mini Splitter miniExperienceReward. Every other non-boss enemy gives
        /// xpPerReferenceSecond × its TTK budget in seconds (EncounterBalance.TargetSeconds) plus affixExperienceBonus when affixed, times experienceRateMultiplier
        /// and this wave's WaveExperienceScale. Inside a wave each kind pays the same XP per share of its HP, and the wave scale follows enemy HP
        /// up the difficulty curve, so a share of enemy HP pays about the same XP on every wave. Bosses give none (the fight ends the trial or
        /// the run).
        /// </summary>
        public float ExperienceFor(Enemy enemy)
        {
            if (!enemy || !enemy.definition || enemy.IsBoss) return 0f;
            if (enemy.Mini) return Mathf.Max(0f, miniExperienceReward);
            if (!enemy.Elite && IsSwarm(enemy.definition)) return Mathf.Max(0f, swarmExperienceReward);
            var balance = waveSet ? waveSet.encounterBalance : null;
            float body = experienceTable.TryGetValue((enemy.definition.kind, enemy.Elite), out float tabled) ? tabled
                : balance ? Mathf.Max(0f, xpPerReferenceSecond) * balance.TargetSeconds(enemy.definition, enemy.Elite)
                : enemy.definition.xp * (enemy.Elite ? 5f : 1f); // Legacy fallback when no TTK budget is assigned.
            float affix = enemy.Affix != ElementKind.None ? Mathf.Max(0f, affixExperienceBonus) : 0f;
            return (body + affix) * Mathf.Max(0f, experienceRateMultiplier) * WaveExperienceScale;
        }

        /// <summary>Design document 9.2: this wave's multiplier on the XP of special minions and elites. Their HP grows with the wave's
        /// difficulty (CombatScale, the reference DPS against wave 1's), and so does their XP; the wave's experienceScale is an extra
        /// per-wave factor on top (1 keeps the curve). 1 without a wave table.</summary>
        public float WaveExperienceScale => waveSet && waveSet.waves is { Length: > 0 } && WaveIndex >= 0
            ? CombatScale * Mathf.Max(0f, waveSet.waves[Mathf.Min(WaveIndex, waveSet.waves.Length - 1)].experienceScale) : 1f;
        [Header("Player-near batch spawning")]
        [Tooltip("普通出生环内半径；不改变每关属性成长。")] public float nearbySpawnMin = 8f;
        [Tooltip("出生点离玩家的最大距离；旧远处固定点不参与。")] public float nearbySpawnMax = 18f;
        [Tooltip("找不到屏外点时，镜头内出生必须保持的安全距离。")] public float visibleSpawnSafety = 10f;
        [Tooltip("一次出生点搜索的随机候选上限。")] public int spawnPointCandidates = 80;
        [Tooltip("合法点或保存对象池暂不足时，重试预告的间隔。")] public float batchSpawnRetry = .1f;
        public int BatchIndex { get; private set; }
        public bool TimeExpired { get; private set; }
        public int LivingCount { get { int count = 0; foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.gameObject.activeInHierarchy) count++; return count; } }
        public int PendingCount { get { int count = batchSpawnsRemaining + pendingSplits.Count * Mathf.Max(0, splitterMiniCount); if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf) count++; return count; } }
        /// <summary>The two elements the Tyrant ignores (system doc §7.6, §13.2): always Frost and Lightning (ElementImmunity.Boss), the same
        /// in every run; nothing is drawn and no wave announces them. Only the player's Damage Penetration share of their damage gets
        /// through, and never a status.</summary>
        public ElementKind[] TyrantImmunities { get; } = (ElementKind[])TyrantImmunityPool.Clone();
        public WavePhase Phase { get; private set; }
        public float TimeLeft { get; private set; }
        public float HpMultiplier => waveSet && WaveIndex >= 0 ? waveSet.waves[Mathf.Min(WaveIndex, waveSet.waves.Length - 1)].hpMultiplier * waveSet.ExtraHp(WaveIndex+1) : 1f;
        public float EnemyDamageFactor => waveSet ? waveSet.DamageFactor(WaveIndex+1) : 1f;
        /// <summary>R(w): this wave's reference DPS over wave 1's, i.e. how much fodder EHP has grown. Status stacks, damage over time and skill hits follow it.</summary>
        /// The final boss fight counts as wave 15 here (number table row 59): WaveIndex stays on the last ordinary wave.
        public float CombatScale
        {
            get
            {
                float campaign = CombatScaleRule?.Invoke() ?? 0f; // the V2 day campaign's scale by day (since 2026-11-10)
                if (campaign > 0f) return campaign;
                return waveSet && waveSet.encounterBalance && WaveIndex >= 0 ? waveSet.encounterBalance.DpsAt(Phase == WavePhase.FinalBoss ? WaveCount + 1 : WaveIndex + 1) / waveSet.encounterBalance.DpsAt(1) : 1f;
            }
        }
        /// <summary>V2 day campaign (since 2026-11-10): CombatScale by the campaign's day instead of the wave; null, or 0 or less, keeps
        /// the classic R(w).</summary>
        public static Func<float> CombatScaleRule;
        /// <summary>V2 day campaign (since 2026-11-10): a batch kind's weight by the campaign's day, given its wave table weight (0 when the
        /// table does not list it); 0 or less keeps the kind out of the wave. Null in the classic run: the table alone decides.</summary>
        public static Func<EnemyKind, int, int> KindWeightRule;
        /// <summary>The Tyrant's own damage growth, that of the final boss wave (wave 15). Its scepter's summons keep the
        /// last ordinary wave's (EnemyDamageFactor), as document 12.2 sizes them on wave 14.</summary>
        public float FinalBossDamageFactor => waveSet ? waveSet.DamageFactor(WaveCount + 1) : 1f;
        public float EnemySpeedFactor => (waveSet ? waveSet.SpeedFactor(WaveIndex+1) : 1f) * MovementSpeedScale;
        public float BuildTime => waveSet && WaveIndex >= 0 ? waveSet.waves[Mathf.Min(WaveIndex, waveSet.waves.Length - 1)].buildTime : 20f;
        public event Action<int> WaveCompleted;
        public event Action<Enemy> EnemySpawned;
        public event Action<Enemy> EnemyEliminated;
        bool trialSuspended;
        bool finalBossPending;
        ElementKind waveAffixA, waveAffixB;
        /// <summary>The two elements ordinary enemies of this wave may carry (doc 7.6), shown on the wave banner; None before wave 5.</summary>
        public ElementKind WaveAffixA => waveAffixA;
        public ElementKind WaveAffixB => waveAffixB;
        // Kinds whose warning was placed this wave (elites included), and kinds whose warning found no ground in the current marker pass.
        readonly HashSet<EnemyKind> seenThisWave = new(), unplaceable = new();
        float[] rollWeights;
        bool[] rollListed;
        int batchSpawnsRemaining, deathSequence;
        float radiationClock;
        readonly Dictionary<Enemy, int> corpseOrder = new();
        struct PendingSplit { public EnemyDefinition data; public Vector3 at, side; public bool suppressRewards; }
        readonly List<PendingSplit> pendingSplits = new();

        public int WaveIndex { get; private set; } = -1;
        /// <summary>Ordinary waves in the saved set (14); the final boss follows as wave WaveCount + 1.</summary>
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
        /// <summary>One-time move of scene values saved before the V5 roster (swarm XP 0.1, Spitter from wave 3, Warden from wave 5) to the system doc values.</summary>
        void ApplyV5Defaults()
        {
            if (v5RosterRevision >= 1) return;
            v5RosterRevision = 1;
            swarmExperienceReward = .5f;
            rangedFirstWave = 2;
            supportFirstWave = 3;
        }

        /// <summary>Saved definition of a kind; null when the roster has no slot for it yet.</summary>
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
            pendingSplits.Clear();
            Enemy.ClearRadiationZones(); radiationClock = 0f;
            BeginV2Wave(0);
        }

        public void BeginNextWave()
        {
            if (WaveIndex + 1 >= WaveCount) { BeginFinalBoss(); return; }
            BeginV2Wave(WaveIndex + 1);
        }

        bool campaignSpawn;
        /// <summary>V2 day campaign: where a wave enemy is born instead of the point the wave picked (null in the classic run).</summary>
        public static System.Func<Vector3, Vector3> SpawnPointOverride;
        /// <summary>V2 day campaign: ordinary wave enemies are warned of and born where SpawnPointOverride puts them (the rim of the map)
        /// instead of on the ring round the player, and walk in from there. Off in the classic run (no override), in the void trial and in
        /// the final boss fight, whose summons keep their ring round the boss.</summary>
        bool RimSpawns => SpawnPointOverride != null && !trialSuspended && Phase != WavePhase.FinalBoss;
        /// <summary>Kinds that walk in from the rim. The Spire cannot walk: born on the rim it would never reach the fight and would hold
        /// the wave open (a wave ends only when every enemy is dead), so it keeps its birth on the ring round the player.</summary>
        static bool WalksInFromRim(EnemyDefinition data) => data && data.kind != EnemyKind.Spire && !EnemyRoles.IsBoss(data.kind);
        /// <summary>Cells a rim point may be pulled in toward the map's centre to find ground the body can walk from.</summary>
        const int RimSettleCells = 8;

        /// <summary>V2 day campaign: a warning point on the rim for a wave enemy. SpawnPointOverride is asked with the wave's own pick near
        /// the player; later tries turn that pick round the player, so the override can answer with other ground. False when no try gives
        /// open ground clear of other warnings and enemies.</summary>
        bool TryRimSpawnPoint(EnemyDefinition data, out Vector3 point)
        {
            Vector3 centre = GM.player.Position;
            Vector3 seed = TryFindV2SpawnPoint(data, out var near) ? near : centre + Vector3.forward * nearbySpawnMax;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                point = SpawnPointOverride(centre + Quaternion.Euler(0f, 90f * attempt, 0f) * Query.Flat(seed - centre));
                if (SettleRimPoint(data, ref point, true)) return true;
            }
            point = Vector3.zero;
            return false;
        }

        /// <summary>V2 day campaign: the birth point SpawnPointOverride gives for a wave enemy that reaches SpawnPrepared without a rim
        /// warning; the wave's own point when the override finds no ground the body can walk from.</summary>
        Vector3 RimBirthPoint(EnemyDefinition data, Vector3 at)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                Vector3 point = SpawnPointOverride(at);
                if (SettleRimPoint(data, ref point, false)) return point;
            }
            return at;
        }

        /// <summary>
        /// Pulls a rim point inside the map and then toward its centre, a cell at a time, until the body stands on walkable ground: a
        /// body born where its own walking is refused never moves (Enemy.KeepOffEdges refuses every step that is not walkable). With
        /// clearOfOthers the point must also keep clear of other warnings and living enemies. False when no such ground is within
        /// RimSettleCells cells.
        /// </summary>
        bool SettleRimPoint(EnemyDefinition data, ref Vector3 point, bool clearOfOthers)
        {
            point = new Vector3(point.x, 0f, point.z);
            var world = WorldMap.Active;
            if (!world) return true;
            float body = data.radius + .1f;
            point = world.ClampPoint(point, body + .15f);
            Vector3 inward = Query.Flat(world.Bounds.center - point);
            inward = inward.sqrMagnitude > .01f ? inward.normalized : Vector3.forward;
            for (int step = 0; step < RimSettleCells && !world.IsWalkable(point, body); step++) point += inward;
            return world.IsWalkable(point, body) && (!clearOfOthers || !RimCrowded(point, data.radius, null, true));
        }

        /// <summary>Whether a body of this radius at the point would overlap a living enemy or, with markers, another active warning.</summary>
        bool RimCrowded(Vector3 point, float bodyRadius, WaveSpawnMarker self, bool markers)
        {
            if (markers && preparedMarkers != null)
                foreach (var other in preparedMarkers)
                    if (other && other != self && other.gameObject.activeSelf && other.definition
                        && Query.Flat(other.transform.position - point).magnitude < bodyRadius + other.definition.radius + .5f) return true;
            foreach (var enemy in alive)
                if (enemy && enemy.IsAlive && Query.Flat(enemy.Position - point).magnitude < bodyRadius + enemy.radius + .5f) return true;
            return false;
        }

        /// <summary>V2 day campaign: one enemy at an exact point outside any wave (the Day 0 tutorial's first monsters); it gives the usual drops.</summary>
        public Enemy SpawnCampaign(EnemyKind kind, Vector3 at)
        {
            var data = DefinitionOf(kind);
            if (!data) return null;
            campaignSpawn = true;
            try { return SpawnPrepared(data, at, false); }
            finally { campaignSpawn = false; }
        }

        /// <summary>V2 day campaign: starts the wave of this 0-based index (a day holds two; the overtime days repeat the last ones).</summary>
        public void BeginWaveAt(int index)
        {
            if (!running) { running = true; pendingSplits.Clear(); Enemy.ClearRadiationZones(); radiationClock = 0f; }
            BeginV2Wave(Mathf.Clamp(index, 0, WaveCount - 1));
        }

        static float OrdinaryAffixShare(int index) => index < 4 ? 0f : index < 7 ? .1f : index < 10 ? .15f : .25f;

        // Design document 9.2: at the start of each wave the experience of every non-swarm kind (plain and elite) is fixed from its HP budget,
        // so inside the wave each kind pays the same experience per share of its HP; ExperienceFor applies the wave's scale on top. Swarm,
        // minis and bosses keep their fixed values.
        readonly Dictionary<(EnemyKind kind, bool elite), float> experienceTable = new();
        void BuildExperienceTable()
        {
            experienceTable.Clear();
            var balance = waveSet ? waveSet.encounterBalance : null;
            if (!balance || roster == null) return;
            var rows = new List<string>();
            foreach (var data in roster)
            {
                if (!data || IsSwarm(data) || data.kind is EnemyKind.Hollow or EnemyKind.Tyrant) continue; // bosses give no experience
                for (int e = 0; e < 2; e++)
                {
                    bool elite = e == 1;
                    if (elite && !data.canElite) continue;
                    float xp = Mathf.Max(0f, xpPerReferenceSecond) * balance.TargetSeconds(data, elite);
                    experienceTable[(data.kind, elite)] = xp;
                    rows.Add($"{data.kind}{(elite ? "*" : "")}={xp:0.00}");
                }
            }
            if (rows.Count > 0) Debug.Log($"[Waves] wave {WaveIndex + 1} experience table (x{WaveExperienceScale:0.00} this wave): {string.Join(" ", rows)}");
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
            seenThisWave.Clear();
            // Two wave affixes: none before wave 5, three elements for waves 5-7, five for 8-10, all seven from wave 11.
            // Doc 7.6: a wave brings two affix kinds, so B is drawn from the open elements other than A.
            int available = index < 4 ? 0 : index < 7 ? 3 : index < 10 ? 5 : 7;
            waveAffixA = available > 0 ? (ElementKind)Random.Range(1, available + 1) : ElementKind.None;
            waveAffixB = available > 1 ? (ElementKind)(1 + ((int)waveAffixA - 1 + Random.Range(1, available)) % available) : waveAffixA;
            if (GM.hud) GM.hud.Banner(ArenaText.Pair($"第 {index + 1} / {WaveCount} 波", $"Wave {index + 1} / {WaveCount}"), available > 0 ? $"{ArenaTerms.Element(waveAffixA)} · {ArenaTerms.Element(waveAffixB)}" : ArenaText.Pair("星际战场", "Interstellar Battlefield"));
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
            tyrant.HoldAttacks(bosses.tyrantOpeningGrace); // no move while the banner is up
            Track(tyrant);
            finalBossPending = false;
            if (GM.hud) GM.hud.Banner(ArenaText.Pair("暴虐君王", "THE TYRANT KING"), ArenaText.Pair("击破护盾。它的冲锋会撞碎墙体和壁垒。", "Break its Shield. Its charge smashes through walls and Bulwarks."));
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
                return; // Keep WaveActive and all combat input/enemy actions alive during cleanup.
            }
            spawnTimer -= dt;
            if (LivingCount == 0 && PendingCount == 0) BeginBatch();
            else if (batchSpawnsRemaining == 0) RefillPopulation();
            if (batchSpawnsRemaining > 0 && spawnTimer <= 0f) IssueBatchMarkers();
            // Elites and specials are included in the batch, so timed additions cannot exceed the basic/special/elite shares.
            TickPreparedMarkers(dt);
        }

        /// <summary>Ages Bloater radiation zones; while the player stands inside any, every radiationTickSeconds of exposure costs radiationTickDamage HP.</summary>
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
            basicRemaining = Mathf.Clamp(Mathf.RoundToInt(batchSpawnsRemaining * shares.x), 0, batchSpawnsRemaining);
            int eliteWanted = Mathf.Clamp(Mathf.RoundToInt(batchSpawnsRemaining * shares.z), 0, batchSpawnsRemaining - basicRemaining);
            specialRemaining = batchSpawnsRemaining - basicRemaining - eliteWanted;
            // A small batch would round its special share to zero; keep one slot so the specials of an early wave really appear.
            if (specialRemaining == 0 && shares.y > 0f && basicRemaining > 1) { basicRemaining--; specialRemaining++; }
            ReserveFirstAppearances(ref basicRemaining, ref specialRemaining);
            eliteRemaining = Mathf.Min(eliteWanted, Mathf.Max(0, maxConcurrentElites - ActiveEliteCount));
            basicRemaining += eliteWanted - eliteRemaining;
            spawnTimer = 0f;
        }

        void RefillPopulation()
        {
            if (TimeExpired || TimeLeft <= 0f) return;
            int basic = 0, special = 0, elite = 0;
            foreach (var enemy in alive)
            {
                if (!enemy || !enemy.IsAlive || !enemy.definition || enemy.IsBoss) continue;
                if (enemy.Elite) elite++; else if (IsBasic(enemy.definition.kind)) basic++; else special++;
            }
            foreach (var marker in preparedMarkers)
            {
                if (!marker || !marker.gameObject.activeSelf || !marker.definition) continue;
                if (marker.elite) elite++; else if (IsBasic(marker.definition.kind)) basic++; else special++;
            }
            int target = PopulationTarget;
            var shares = ActiveRoleShares;
            int eliteShareCount = Mathf.RoundToInt(target * shares.z), eliteTarget = Mathf.Min(eliteShareCount, Mathf.Max(0, maxConcurrentElites));
            int basicTarget = Mathf.RoundToInt(target * shares.x) + eliteShareCount - eliteTarget;
            if (shares.y > 0f && target - basicTarget - eliteTarget <= 0 && basicTarget > 1) basicTarget--; // Same one-slot floor as BeginBatch.
            int specialTarget = Mathf.Max(0, target - basicTarget - eliteTarget);
            int missingBasic = Mathf.Max(0,basicTarget-basic), missingElite = Mathf.Max(0,eliteTarget-elite);
            int missingSpecial = Mathf.Max(0,specialTarget-special);
            // A group above its share (specials reserved for a first appearance, a slot that fell back to a basic) still holds its places:
            // queue only the free places of the population, taking the cut from the basics first.
            int excess = missingBasic + missingSpecial + missingElite - Mathf.Max(0, target - basic - special - elite);
            if (excess > 0) { int cut = Mathf.Min(excess, missingBasic); missingBasic -= cut; excess -= cut; }
            if (excess > 0) { int cut = Mathf.Min(excess, missingSpecial); missingSpecial -= cut; excess -= cut; }
            if (excess > 0) missingElite -= Mathf.Min(excess, missingElite);
            int missing = missingBasic+missingSpecial+missingElite;
            if (missing < Mathf.Max(1,Mathf.CeilToInt(target*refillMissingShare))) return;
            ReserveFirstAppearances(ref missingBasic, ref missingSpecial, Mathf.Max(0, special - specialTarget));
            // Count warnings as incoming bodies, so repeated ticks never queue the same deficit twice.
            basicRemaining = missingBasic; specialRemaining = missingSpecial; eliteRemaining = missingElite;
            batchSpawnsRemaining = missing; BatchIndex++; spawnTimer = 0f;
        }

        /// <summary>
        /// Doc 11.2: every kind the wave lists appears in it. Each special the wave lists that has not appeared yet this wave takes a special slot
        /// from the basics of the batch, so a wave whose special share rounds to one slot still brings all of its specials. Specials already on the
        /// field above their share (<paramref name="surplus"/>: a reserved slot another special took while the unseen kind found no ground) count as
        /// reserved, so the specials never exceed their share by more than one body per kind still unseen.
        /// </summary>
        void ReserveFirstAppearances(ref int basics, ref int specials, int surplus = 0)
        {
            int unseen = 0;
            if (roster != null)
                for (int i = 0; i < roster.Length; i++)
                    if (roster[i] && !seenThisWave.Contains(roster[i].kind) && ListedWeight(WaveIndex + 1, i, 1, false, out _)) unseen++;
            int moved = Mathf.Clamp(unseen - specials - Mathf.Max(0, surplus), 0, basics);
            basics -= moved; specials += moved;
        }

        void IssueBatchMarkers()
        {
            float affixShare = OrdinaryAffixShare(WaveIndex);
            unplaceable.Clear();
            while (batchSpawnsRemaining > 0 && !TimeExpired && TimeLeft > 0f)
            {
                int roll = Random.Range(0, batchSpawnsRemaining);
                int quota = roll < basicRemaining ? 0 : roll < basicRemaining + specialRemaining ? 1 : 2;
                int group = quota == 2 && ActiveEliteCount >= maxConcurrentElites ? 0 : quota; // Elite cap reached: this slot becomes fodder.
                var data = RollBatchEnemy(group);
                if (!data && group > 0) data = RollBatchEnemy(group = 0); // Nothing eligible (not in this wave or pool empty): a basic instead.
                if (!data) data = DefinitionOf(EnemyKind.Grunt);
                if (!data) break;
                bool elite = group == 2;
                // An elite is a special with an element no other alive/pending elite carries; ordinary specials never carry one,
                // and ordinary basics roll the wave's affix pair.
                var affix = !data.canAffix ? ElementKind.None
                    : elite ? PickEliteAffix()
                    : EnemyRoles.IsSpecial(data.kind) ? ElementKind.None
                    : Random.value < affixShare ? Random.value < .5f ? waveAffixA : waveAffixB : ElementKind.None;
                if (!PlaceMarker(data, elite, affix))
                {
                    // A kind drawn first only because it has not appeared this wave (a Spire with no open ground near the player) must not hold up
                    // the batch: it waits for the next pass and this slot is drawn again.
                    if (group < 2 && !seenThisWave.Contains(data.kind) && unplaceable.Add(data.kind)) continue;
                    break;
                }
                if (quota == 0) basicRemaining--; else if (quota == 1) specialRemaining--; else eliteRemaining--;
                batchSpawnsRemaining--;
            }
            spawnTimer = Mathf.Max(.02f, batchSpawnRetry);
        }

        bool IsSwarm(EnemyDefinition data) => EnemyRoles.IsMelee(data.kind);
        /// <summary>Spawn group 0 of system doc 11.2, the basic enemies: the melee kinds plus the ranged Spitter and the support Warden (doc 10.2).
        /// Experience still follows IsSwarm: only the melee kinds give the fixed swarm reward.</summary>
        public static bool IsBasic(EnemyKind kind) => EnemyRoles.IsMelee(kind) || kind is EnemyKind.Spitter or EnemyKind.Warden;
        int ShadeCap => WaveIndex + 1 >= shadePairWave ? 2 : 1;
        int ShadeCount()
        {
            int count = 0;
            foreach (var enemy in alive) if (enemy && enemy.IsAlive && enemy.definition && enemy.definition.kind == EnemyKind.Shade) count++;
            if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && marker.gameObject.activeSelf && marker.definition && marker.definition.kind == EnemyKind.Shade) count++;
            return count;
        }
        /// <summary>
        /// Group 0 = basics (melee kinds, Spitter, Warden), 1 = ordinary specials, 2 = elites (specials with an affix, from firstEliteWave).
        /// A kind is listed only when the 1-based wave's saved weight is above zero (system doc §11.2); with constrainActive it must also be able
        /// to spawn now (Shade cap, a free pool slot). The weight is the map-tilted one and may be zero for a listed kind (a tilt of -10 on a 10).
        /// </summary>
        bool ListedWeight(int wave1, int index, int group, bool constrainActive, out int weight)
        {
            weight = 0;
            if (roster == null || index >= roster.Length || !roster[index] || EnemyRoles.IsBoss(roster[index].kind)) return false;
            var data = roster[index];
            bool allowed = group == 0 ? IsBasic(data.kind) : EnemyRoles.IsSpecial(data.kind) && (group == 1 || data.canElite);
            if (!allowed) return false;
            if (data.kind == EnemyKind.Spitter && wave1 < rangedFirstWave || data.kind == EnemyKind.Warden && wave1 < supportFirstWave
                || group == 2 && wave1 < firstEliteWave) return false;
            var wave = waveSet.waves[Mathf.Clamp(wave1 - 1, 0, waveSet.waves.Length - 1)];
            int kind = (int)data.kind;
            int saved = wave.weights != null && kind < wave.weights.Length ? wave.weights[kind] : 0;
            int listed = KindWeightRule != null ? KindWeightRule(data.kind, saved) : saved;
            if (listed <= 0) return false;
            if (constrainActive && data.kind == EnemyKind.Shade && ShadeCount() >= ShadeCap) return false;
            if (constrainActive && !HasSpawnCapacity(data)) return false;
            weight = TiltedWeight(listed, kind);
            return true;
        }
        /// <summary>
        /// One weight per roster slot for a batch group: the map-tilted weight of every listed kind, raised so each keeps at least minKindShare
        /// of the group total (a map may reweight the mix but no kind the table lists may vanish). Slots not listed, or unable
        /// to spawn now under constrainActive, stay zero and are left out of the floor.
        /// </summary>
        void FillGroupWeights(int wave1, int group, bool constrainActive, float[] weights, bool[] listed)
        {
            for (int i = 0; i < weights.Length; i++)
            {
                listed[i] = ListedWeight(wave1, i, group, constrainActive, out int weight);
                weights[i] = weight;
            }
            ApplyShareFloor(weights, listed, minKindShare);
        }
        /// <summary>Spawn weights per roster slot of one batch group (0 basic, 1 special, 2 elite) in a 1-based ordinary wave on the current map,
        /// after the map tilt and the minKindShare floor, as at the start of the wave (the live Shade cap and pool are not applied). For tools and tests.</summary>
        public float[] GroupWeights(int wave, int group)
        {
            var weights = new float[roster != null ? roster.Length : 0];
            if (!waveSet || waveSet.waves == null || waveSet.waves.Length == 0) return weights;
            FillGroupWeights(wave, group, false, weights, new bool[weights.Length]);
            return weights;
        }
        /// <summary>
        /// Raises every listed weight under minShare of the listed total to exactly minShare of the new total; the others keep their weight.
        /// Raising grows the total, so the floor line is recomputed until no kept kind falls under it. When the floor cannot hold for every listed
        /// kind (minShare times their count reaches 1) or the listed weights are all zero, the listed kinds split the group evenly.
        /// </summary>
        public static void ApplyShareFloor(float[] weights, bool[] listed, float minShare)
        {
            int count = 0;
            float total = 0f;
            for (int i = 0; i < weights.Length; i++) if (listed[i]) { count++; total += Mathf.Max(0f, weights[i]); }
            if (count == 0 || minShare <= 0f) return;
            if (total <= 0f || minShare * count >= 1f)
            {
                for (int i = 0; i < weights.Length; i++) if (listed[i]) weights[i] = total > 0f ? total / count : 1f;
                return;
            }
            // The raised set is every kind under minShare of the final total. Each pass adds the kinds under the current line and grows the total
            // to what the kept weights need (kept / (1 - raised * minShare)); a pass that adds nobody has found the final total.
            float floorTotal = total;
            int raisedBefore = 0;
            for (int pass = 0; pass <= count; pass++)
            {
                float line = minShare * floorTotal, kept = 0f;
                int raised = 0;
                for (int i = 0; i < weights.Length; i++) if (listed[i]) { if (weights[i] < line) raised++; else kept += weights[i]; }
                if (raised == raisedBefore) break;
                if (kept <= 0f) { for (int i = 0; i < weights.Length; i++) if (listed[i]) weights[i] = total / count; return; }
                raisedBefore = raised;
                floorTotal = kept / (1f - raised * minShare);
            }
            float lifted = minShare * floorTotal;
            for (int i = 0; i < weights.Length; i++) if (listed[i] && weights[i] < lifted) weights[i] = lifted;
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
            if (roster == null) return null;
            if (rollWeights == null || rollWeights.Length != roster.Length) { rollWeights = new float[roster.Length]; rollListed = new bool[roster.Length]; }
            FillGroupWeights(WaveIndex + 1, group, true, rollWeights, rollListed);
            // Every kind the wave table lists appears in the wave. Basic and special rolls take a listed kind that has not
            // appeared yet first (elites count as appeared); a kind whose warning found no ground in this pass goes back to the plain mix.
            if (group < 2)
            {
                bool unseen = false;
                for (int i = 0; i < roster.Length; i++) unseen |= rollWeights[i] > 0f && FirstAppearance(roster[i].kind);
                if (unseen) for (int i = 0; i < roster.Length; i++) if (rollWeights[i] > 0f && !FirstAppearance(roster[i].kind)) rollWeights[i] = 0f;
            }
            float total = 0f;
            for (int i = 0; i < roster.Length; i++) total += rollWeights[i];
            if (total <= 0f) return null;
            float roll = Random.value * total;
            int last = -1;
            for (int i = 0; i < roster.Length; i++)
            {
                if (rollWeights[i] <= 0f) continue;
                last = i;
                roll -= rollWeights[i];
                if (roll < 0f) return roster[i];
            }
            return roster[last]; // Random.value can return exactly 1.
        }
        bool FirstAppearance(EnemyKind kind) => !seenThisWave.Contains(kind) && !unplaceable.Contains(kind);

        EnemyDefinition RollEnemy(WaveSet.Definition wave)
        {
            int total = 0;
            for (int i = 0; i < roster.Length; i++) total += SummonWeight(wave, i);
            int roll = Random.Range(0, Mathf.Max(1, total));
            for (int i = 0; i < roster.Length; i++) { roll -= SummonWeight(wave, i); if (roll < 0) return roster[i]; }
            return DefinitionOf(EnemyKind.Grunt);
        }
        /// <summary>Tyrant summons: the basic crowd of the last ordinary wave, never specials or bosses.</summary>
        int SummonWeight(WaveSet.Definition wave, int index) => index < roster.Length && roster[index] && !EnemyRoles.IsSpecial(roster[index].kind)
            && !EnemyRoles.IsBoss(roster[index].kind) ? TiltedWeight(wave, (int)roster[index].kind) : 0;

        int TiltedWeight(WaveSet.Definition wave, int kindIndex)
        {
            if (wave.weights == null || kindIndex < 0 || kindIndex >= wave.weights.Length || wave.weights[kindIndex] <= 0) return 0;
            return TiltedWeight(wave.weights[kindIndex], kindIndex);
        }

        /// <summary>A listed weight after the map's tilt of that kind.</summary>
        static int TiltedWeight(int listed, int kindIndex)
        {
            if (listed <= 0) return 0;
            var tilt = WorldMap.Active && WorldMap.Active.biome ? WorldMap.Active.biome.mixTilt : null;
            return Mathf.Max(0, listed + (tilt != null && kindIndex >= 0 && kindIndex < tilt.Length ? Mathf.Clamp(tilt[kindIndex], -10, 10) : 0));
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
                    // V2 day campaign: the warning stands where the enemy will really be born, on the rim of the map.
                    Vector3 point;
                    if (RimSpawns && WalksInFromRim(data)) { if (!TryRimSpawnPoint(data, out point)) return false; }
                    else if (!TryFindV2SpawnPoint(data, out point)) return false;
                    marker.Begin(point, data, elite, affix);
                    seenThisWave.Add(data.kind);
                    return true;
                }
            return false;
        }

        void TickPreparedMarkers(float dt)
        {
            foreach (var marker in preparedMarkers)
            {
                if (!marker.gameObject.activeSelf) continue;
                // V2 day campaign: a warning on the rim only needs its ground to stay walkable; the ring rules round the player do not apply.
                bool rim = RimSpawns && !marker.bossSummon && WalksInFromRim(marker.definition);
                bool holds = rim ? !WorldMap.Active || WorldMap.Active.IsWalkable(marker.transform.position, marker.definition.radius)
                    : ValidSpawnPoint(marker.transform.position, marker.definition);
                if (!holds)
                {
                    Vector3 replacement;
                    if (rim ? TryRimSpawnPoint(marker.definition, out replacement) : TryFindV2SpawnPoint(marker.definition, out replacement))
                        marker.Begin(replacement, marker.definition, marker.elite, marker.affix, marker.bossSummon);
                    continue;
                }
                marker.timer -= dt;
                if (marker.timer > 0f) continue;
                // An enemy still standing on a rim warning holds the birth back: two bodies born into each other can push one off the map.
                if (rim && RimCrowded(marker.transform.position, marker.definition.radius, marker, false)) { marker.timer = Mathf.Max(.02f, batchSpawnRetry); continue; }
                var spawned = SpawnPrepared(marker.definition, marker.transform.position, marker.elite, marker.affix, exactPoint: rim || marker.bossSummon);
                if (spawned)
                {
                    if (marker.bossSummon) spawned.SuppressRewards = spawned.BossSummon = true; // boss summons give no experience but die as their kind
                    marker.gameObject.SetActive(false);
                }
                else if (marker.bossSummon) marker.gameObject.SetActive(false); // a summon the pool cannot supply is dropped
                else marker.timer = Mathf.Max(.02f, batchSpawnRetry); // Pool pressure does not silently erase a pending member.
            }
        }

        /// <param name="mini">Configure as a mini Splitter (smaller, weaker, fixed XP, never splits).</param>
        /// <param name="ignoreClock">Also spawn after the wave timer expired (minis are part of a dying enemy, not a new birth).</param>
        /// <param name="exactPoint">Born exactly at <paramref name="at"/> even while SpawnPointOverride is set: a rim warning already
        /// stands there, or the point belongs to a boss summon, the enemy lab or a teaching moment.</param>
        Enemy SpawnPrepared(EnemyDefinition data, Vector3 at, bool elite, ElementKind affix = ElementKind.None, bool mini = false, bool ignoreClock = false, bool exactPoint = false)
        {
            if (!data || preparedEnemies == null) return null;
            if (!trialSuspended && !campaignSpawn && (Phase is not (WavePhase.WaveActive or WavePhase.FinalBoss)
                || !ignoreClock && Phase == WavePhase.WaveActive && (TimeExpired || TimeLeft <= 0f))) return null;
            // V2 day campaign: a wave enemy is born where the campaign puts it (the rim of the map) and walks in, not next to the player.
            // The Day 0 tutorial's monsters (campaignSpawn), a dead Splitter's minis, the void trial, the final boss fight and the Spire
            // keep their own points (RimSpawns, WalksInFromRim).
            if (!exactPoint && !campaignSpawn && !mini && RimSpawns && WalksInFromRim(data)) at = RimBirthPoint(data, at);
            foreach (var enemy in preparedEnemies)
            {
                // Frozen battlefield actors stay registered: they are reserved, not free pool slots.
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

        /// <summary>
        /// Queues the minis of a Splitter killed by HP. They hatch at the start of the next wave tick (SpawnPendingSplits),
        /// so a kill inside a weapon's damage loop never wakes pooled bodies while Combatant.All is being iterated.
        /// </summary>
        public void QueueSplit(Enemy parent)
        {
            if (!parent || !parent.definition || splitterMiniCount <= 0) return;
            Vector3 side = Vector3.Cross(Vector3.up, Query.Flat(parent.AimDirection));
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            pendingSplits.Add(new PendingSplit { data = parent.definition, at = Query.Flat(parent.Position), side = side.normalized, suppressRewards = parent.SuppressRewards });
        }
        /// <summary>Hatches queued mini Splitters through the prepared pool at ±splitterMiniOffset across the parent's facing; returns how many appeared.</summary>
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
                    if (!mini) continue; // Pool exhausted (no free or recyclable Splitter body): the mini is lost rather than queued forever.
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
                var enemy = SpawnPrepared(data, at, false, exactPoint: bossSummon);
                if (enemy) { enemy.SuppressRewards = bossSummon; enemy.IsTrialSummon = trialSuspended; }
            }
        }
        /// <summary>Boss summons at exact points (the Nash Prophet places its own warnings first); no experience.</summary>
        public void SpawnExtrasAt(EnemyKind kind, IReadOnlyList<Vector3> points, bool bossSummon)
        {
            var data = DefinitionOf(kind);
            if (!data || points == null) return;
            if (!trialSuspended && Phase == WavePhase.WaveActive && (TimeExpired || TimeLeft <= 0f)) return;
            foreach (var at in points)
            {
                var enemy = SpawnPrepared(data, at, false, exactPoint: true);
                if (enemy) { enemy.SuppressRewards = bossSummon; enemy.IsTrialSummon = trialSuspended; }
            }
        }
        /// <summary>A saved warning marker not in use, for a boss that shows its own summon points; null when all are busy.</summary>
        public WaveSpawnMarker BorrowMarker()
        {
            if (preparedMarkers != null) foreach (var marker in preparedMarkers) if (marker && !marker.gameObject.activeSelf) return marker;
            return null;
        }
        /// <summary>Enemy lab: one enemy of this kind at a point, an elite with the affix when asked and the kind allows it. The boss uses SpawnLabTyrant.</summary>
        public Enemy SpawnLab(EnemyKind kind, Vector3 at, bool elite, ElementKind affix)
        {
            var data = DefinitionOf(kind);
            if (!data) return null;
            bool asElite = elite && data.canElite;
            return SpawnPrepared(data, at, asElite, asElite || data.canAffix ? affix : ElementKind.None, exactPoint: true);
        }
        /// <summary>Enemy lab: the final boss at a point, with its usual HP factor and immunities.</summary>
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
        /// <summary>Enemy lab: removes every living enemy without rewards or death blasts (no Bloater lights its fuse, a Tyrant's summon neither).</summary>
        public void ClearLab()
        {
            foreach (var enemy in alive.ToArray())
                if (enemy && enemy.IsAlive) { enemy.SuppressRewards = true; enemy.BossSummon = false; enemy.Kill(); }
        }
        /// <summary>Document 12 teaching moment: one basic fodder spawned at an exact point, worth no reward, for the black-hole feeding demonstration.</summary>
        public Enemy SpawnIntroFodder(Vector3 at)
        {
            var data = DefinitionOf(EnemyKind.Grunt);
            if (!data) return null;
            var enemy = SpawnPrepared(data, at, false, exactPoint: true);
            if (enemy) enemy.SuppressRewards = true;
            return enemy;
        }

        /// <summary>
        /// The Tyrant's Royal Scepter (system doc 13.2): one crowd mixed like the last ordinary wave of the table (wave 14): its basic,
        /// special and elite shares and listed kinds, the elite and Shade caps, the pool and its affixes. Each body shows a red cross first,
        /// on a ring of ringRadius around centre (or on the usual spawn ring around the player where that ground is taken), and, as every boss
        /// summon, gives no experience (WaveSpawnMarker.bossSummon). Returns how many crosses were placed.
        /// </summary>
        public int SpawnBossWave(int count, Vector3 centre, float ringRadius)
        {
            if (count <= 0 || roster == null || !waveSet || waveSet.waves == null || waveSet.waves.Length == 0) return 0;
            int wave = waveSet.waves.Length;
            var shares = RoleShares(wave);
            int basic = Mathf.Clamp(Mathf.RoundToInt(count * shares.x), 0, count);
            int eliteWanted = Mathf.Clamp(Mathf.RoundToInt(count * shares.z), 0, count - basic);
            int special = count - basic - eliteWanted;
            int elite = Mathf.Min(eliteWanted, Mathf.Max(0, maxConcurrentElites - ActiveEliteCount));
            basic += eliteWanted - elite; // capped elite slots become basics, as in a batch
            if (rollWeights == null || rollWeights.Length != roster.Length) { rollWeights = new float[roster.Length]; rollListed = new bool[roster.Length]; }
            float affixShare = OrdinaryAffixShare(wave - 1), turn = Random.Range(0f, 360f);
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                int group = i < basic ? 0 : i < basic + special ? 1 : 2;
                if (group == 2 && ActiveEliteCount >= maxConcurrentElites) group = 0;
                var data = RollWaveKind(wave, group);
                if (!data && group > 0) data = RollWaveKind(wave, group = 0);
                if (!data) data = DefinitionOf(EnemyKind.Grunt);
                if (!data) break;
                bool isElite = group == 2;
                var affix = !data.canAffix ? ElementKind.None
                    : isElite ? PickEliteAffix()
                    : EnemyRoles.IsSpecial(data.kind) ? ElementKind.None
                    : Random.value < affixShare ? Random.value < .5f ? waveAffixA : waveAffixB : ElementKind.None;
                Vector3 at = centre + Quaternion.Euler(0f, turn + 360f * i / count, 0f) * Vector3.forward * ringRadius;
                if (WorldMap.Active) at = WorldMap.Active.ClampPoint(at, data.radius);
                if (!ValidSpawnPoint(at, data) && !TryFindV2SpawnPoint(data, out at)) continue;
                var marker = BorrowMarker();
                if (!marker) break; // every warning is busy: the rest of this crowd is not called
                marker.Begin(at, data, isElite, affix, true);
                placed++;
            }
            return placed;
        }
        /// <summary>A kind of this batch group, weighted as the 1-based wave lists it and able to spawn now (caps, pool); null when none.</summary>
        EnemyDefinition RollWaveKind(int wave, int group)
        {
            FillGroupWeights(wave, group, true, rollWeights, rollListed);
            float total = 0f;
            for (int i = 0; i < roster.Length; i++) total += rollWeights[i];
            if (total <= 0f) return null;
            float roll = Random.value * total;
            int last = -1;
            for (int i = 0; i < roster.Length; i++)
            {
                if (rollWeights[i] <= 0f) continue;
                last = i;
                roll -= rollWeights[i];
                if (roll < 0f) return roster[i];
            }
            return roster[last]; // Random.value can return exactly 1.
        }
        /// <summary>The Tyrant's Astral Guard (system doc 13.2) swallows a minion: it vanishes without dying, so there is no kill, corpse, split
        /// or reward, and its pool body is free again.</summary>
        public void DevourSummon(Enemy summon)
        {
            if (!summon || summon.IsBoss) return;
            summon.DiscardWithoutReward();
            alive.Remove(summon);
            corpseOrder.Remove(summon);
        }

        public void SpawnTyrantSummons(int count, Vector3 centre)
        {
            count = ScaledOrdinaryCount(count);
            var mix = waveSet.waves[waveSet.waves.Length - 1];
            for (int i = 0; i < count; i++)
            {
                var data = RollEnemy(mix); // The last ordinary wave's basic crowd; specials stay out of boss summons.
                if (!data) continue;
                Vector3 at = centre + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * 4f;
                if (WorldMap.Active) at = WorldMap.Active.ClampPoint(at, 1f);
                if (!ValidSpawnPoint(at, data) && !TryFindV2SpawnPoint(data, out at)) continue;
                // A red cross first (the marker's own one-second delay); the body it becomes gives no experience.
                var marker = BorrowMarker();
                if (marker) { marker.Begin(at, data, false, ElementKind.None, true); continue; }
                var enemy = SpawnPrepared(data, at, false, exactPoint: true);
                if (enemy) enemy.SuppressRewards = true;
            }
        }
        public void LaunchSpit(Enemy owner, Vector3 at, Vector3 direction)
        {
            var bolt = FreeBolt();
            if (bolt) bolt.Launch(owner, at, direction);
        }
        /// <summary>A boss shot with its own damage, push and speed (not the owner's definition); false when no saved bolt is free.</summary>
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

        /// <summary>
        /// Visits the eight ring sectors round-robin and returns the first valid point. A Stomper instead compares up to
        /// stomperSpawnCandidates valid points (one per sector) and takes the one with the fewest living enemies within stomperCrowdRadius.
        /// </summary>
        public bool TryFindV2SpawnPoint(EnemyDefinition data, out Vector3 point)
        {
            float minimum = Mathf.Max(nearbySpawnMin, data.kind == EnemyKind.Shade ? 12f : 1f);
            float maximum = Mathf.Max(minimum + .5f, nearbySpawnMax);
            int perSector = Mathf.Clamp(spawnPointCandidates / 8, 2, 20);
            bool stomper = data.kind == EnemyKind.Stomper;
            int wanted = stomper ? Mathf.Clamp(stomperSpawnCandidates, 1, 8) : 1, found = 0, bestCrowd = int.MaxValue, bestSector = 0;
            Vector3 best = Vector3.zero;
            // Visit sectors in round-robin order so every direction is used; inside a sector off-screen points come first (document 11.2).
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
                        if (pass == 0 && !OffscreenSpawn(candidate)) continue; // first pass: off-screen only
                        if (!ValidSpawnPoint(candidate, data)) continue;
                        int crowd = stomper ? CrowdAround(candidate, stomperCrowdRadius) : 0;
                        if (crowd < bestCrowd) { bestCrowd = crowd; best = candidate; bestSector = sector; }
                        found++;
                        taken = true; // One candidate per sector keeps a Stomper's choices spread around the ring.
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
            // WorldMap owns saved corpse/delayed-death cleanup; no additional kill or reward is emitted here.
            WorldMap.Active?.OnWaveEnded(WaveIndex + 1);
            WaveCompleted?.Invoke(WaveIndex + 1);
        }
        /// <summary>A boss shot that steers toward where the player will be for trackSeconds at up to turnRate degrees a second (the Nash
        /// Prophet's predictive volley; 0 flies straight); it never pushes. False when no saved bolt is free.</summary>
        public bool LaunchTrackingBossBolt(Enemy owner, Vector3 at, Vector3 direction, float damage, float speed, float turnRate, float trackSeconds)
        {
            var bolt = FreeBolt();
            if (!bolt) return false;
            bolt.Launch(owner, at, direction, damage, 0f, speed);
            bolt.Track(turnRate, trackSeconds);
            return true;
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
