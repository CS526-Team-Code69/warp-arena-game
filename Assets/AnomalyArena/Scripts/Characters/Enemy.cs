using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Enemy: always chases the player and only starts winding up a punch when almost touching (body turns yellow). During wind-up its facing is locked and it stands still.
    /// After the wind-up it punches along the locked direction: the player only takes damage if still inside the punch arc, so walking away on yellow dodges it. Touching the player does no damage.
    /// If an enemy the player is holding with the hook is inside the punch arc, it is hit first (cover). Enemies do not pick up weapons in this version.
    /// </summary>
    public partial class Enemy : Combatant
    {
        [Header("Attack")] public float attackDamage = 5f;

        [Tooltip(
            "Authored walk speed at or above which this enemy follows the player into a gravity zone. Compared before the global movement multiplier, so slowing everyone down does not switch the rule off.")]
        public float gravityFollowSpeed = 5f; // 2026-10-27 (P35): 5, so the 4.5 kinds (Stomper, Warden, Chanter) wait at the edge

        [Tooltip("How close to the player's body edge before winding up (almost touching)")]
        public float triggerRange = 0.3f;

        [Tooltip(
            "Hit distance when the punch lands, from the body edge; the player dodges by leaving this range during the wind-up")]
        public float attackRange = 0.8f;

        [Tooltip("Arc of the punch in degrees; facing is locked when the wind-up starts")]
        public float attackArc = 120f;

        public float windupTime = 0.5f;
        public float attackInterval = 1f;

        [Tooltip("How far the player is knocked back when hit")]
        public float attackKnockback = 5f;

        [Header("Look")] public Material normalMaterial;
        public Material windupMaterial;

        [Header("Type")]
        [Tooltip("Large enemy: drops weapons, wider HP bar, uses the large textures (no longer decided by radius)")]
        public bool large;

        [Header("v2 content")] public EnemyDefinition definition;
        public bool pooled;
        public Transform telegraph;
        public ElementKind Affix { get; private set; }
        public bool Elite { get; private set; }

        /// <summary>A mini Splitter left by a dead Splitter: smaller, weaker, fixed XP, never splits again.</summary>
        public bool Mini { get; private set; }

        public bool IsBoss => definition && EnemyRoles.IsBoss(definition.kind);
        public bool IsBossOrElite => IsBoss || Elite;
        /// <summary>M116: a living elite wears a pure white outline round its body (V2CharacterIllustration draws it in
        /// both styles); off once it dies, while a Shade is cloaked, and when the pooled body comes back as an ordinary enemy.</summary>
        public bool ShowsEliteOutline => Elite && IsAlive && !Cloaked;

        /// <summary>An elite or one of the special kinds (Rammer, Bloater, Spire, Stomper, Shade, Chanter): heavier hit feedback on the player.</summary>
        public bool IsSpecial => Elite || definition && EnemyRoles.IsSpecial(definition.kind);

        public override bool Immovable => IsBoss || definition && definition.kind == EnemyKind.Spire;
        public bool IsTrialSummon { get; set; }
        public bool SuppressRewards { get; set; }
        /// <summary>V2 day campaign: this enemy raids the base instead of chasing the player (V2Campaign rolls it at spawn).</summary>
        public bool Raider { get; set; }
        /// <summary>V2 day campaign: where a raider stands to strike the base (null: no raid now), the strike itself and the base centre it faces.</summary>
        public static System.Func<Enemy, Vector3?> RaidSpot;
        public static System.Action<Enemy, float> RaidStrike;
        public static Vector3 RaidCentre;
        /// <summary>V2 day campaign: a raider within this many cells of the player fights the player instead (0: never).</summary>
        public static float RaidAggroRadius;
        /// <summary>Cells the player must get beyond RaidAggroRadius before a raider that turned on the player goes back to the base, so a
        /// raider near the line does not flip between the two every frame.</summary>
        public const float RaidAggroRelease = 2f;
        /// <summary>V2 day campaign: this raider is fighting the player (RaidAggroRadius) instead of raiding the base.</summary>
        public bool RaidTurnedOnPlayer => raidAggro;
        bool raidAggro, raidWindup;
        public bool Cloaked => definition && definition.kind == EnemyKind.Shade && revealTimer <= 0f;

        float spawnVisible,
            revealTimer,
            supportTimer,
            summonTimer,
            chargeLeft,
            inhaleLeft,
            bossRecovery,
            laserTimer;

        public bool Inhaling => IsBoss && definition.kind == EnemyKind.Hollow && inhaleLeft > 0f;
        bool charging, rushing, dyingExplosion;
        const float TyrantChargeDistance = 20f;
        float explosionTimer;

        public bool BloaterWarningActive => definition && definition.kind == EnemyKind.Bloater &&
                                            (IsAlive && windup || dyingExplosion && explosionTimer > 0f);
        /// <summary>Document 9.2: a Bloater swells before it blows, on its own fuse at the player and in the delay after its death; the
        /// model grows with it and so does the illustration (V2CharacterIllustration), which otherwise draws a fixed size.</summary>
        public float Swell { get; private set; } = 1f;
        const float BloaterFuseSwell = .3f; // share the body grows over its fuse at the player

        // Boss move numbers (BossAttackIndex). Nash Prophet (Hollow):
        public const int PulseMove = 0, InhaleMove = 1, DoubleShotMove = 2, RingMove = 3, ProphecyMove = 4;
        // Tyrant:
        public const int CleaveMove = 0, SlamMove = 1, ChargeMove = 2, BackSlashMove = 3, LeapMove = 4, RoyalShieldMove = 5;

        int bossPhase = 1, bossAttack;
        public int BossPhase => bossPhase;
        public int BossAttackIndex => bossAttack;
        public bool TelegraphActive => (windup || leaping) && telegraph && telegraph.gameObject.activeSelf;
        /// <summary>Body tint during a special or boss wind-up, whether or not it draws a ground warning, and while a Bloater rushes in.</summary>
        public bool WindupTinted => windup && specialWindup || rushing;
        /// <summary>Bloater: running at the player before lighting its fuse.</summary>
        public bool Rushing => rushing;
        bool specialWindup;

        public bool LineTelegraph => definition && (definition.kind == EnemyKind.Rammer ||
                                                    definition.kind == EnemyKind.Tyrant && bossAttack is ChargeMove or RampageChargeMove);

        public bool CleaveTelegraph => definition && definition.kind == EnemyKind.Tyrant &&
                                       bossAttack is CleaveMove or BackSlashMove or TailSweepMove;

        /// <summary>Boss moves that hit a circle draw it on the ground (bosses are the exception to the Spire-only rule).</summary>
        public bool CircleTelegraph => IsBoss && (definition.kind == EnemyKind.Hollow && bossAttack == PulseMove ||
                                                  definition.kind == EnemyKind.Tyrant && bossAttack is SlamMove or LeapMove);

        static BossTuning Boss => GM && GM.waves && GM.waves.bosses != null ? GM.waves.bosses : BossTuning.Defaults;

        // Boss state beyond the wind-up: follow-ups, volleys, the leap and the Prophet's own summon warnings and landing circles.
        int forcedMove = -1, followUp = -1, chargesLeft, ringsLeft, ringIndex;
        float ringTimer, volleyTimer, leapTimer;
        // The first ring's aim; later rings turn from it, not from where the player has moved since.
        float ringBaseAngle;
        // A phase change during a charge run or a pending back slash still gets its royal shield cast once the boss is free.
        bool royalCastPending;
        bool leaping, royalShield, openingMove, inhaleHit;
        Vector3 leapFrom, leapTarget;

        /// <summary>Tyrant in the air: the landing circle stays on the ground until it lands.</summary>
        public bool Leaping => leaping;
        public Vector3 LeapTarget => leapTarget;

        /// <summary>A prophecy landing circle: it fills from the edge inward and becomes a hole when Fill01 reaches 1.</summary>
        public struct LandingZone
        {
            public Vector3 centre;
            public float radius, age, fill;
            public float Fill01 => fill > 0f ? Mathf.Clamp01(age / fill) : 1f;
        }

        readonly List<LandingZone> landingZones = new();
        public IReadOnlyList<LandingZone> LandingZones => landingZones;
        readonly List<WaveSpawnMarker> summonMarkers = new();
        readonly List<Vector3> summonPoints = new();
        EnemyKind summonKind;
        float summonWarning;

        /// <summary>Enemy lab and tests: the boss's next move is this one instead of its own choice (-1 clears it).</summary>
        public void ForceNextBossMove(int move) => forcedMove = move;
        public float TelegraphRange { get; private set; }
        public Vector3 TelegraphFacing => facing;
        ElementStatus statusBoard;
        Vector3 savedBodyScale;
        float savedCapsuleRadius = -1f;

        // ───── Spire laser (system doc §10.2) ─────
        /// <summary>A Spire whose light wave caught the player is aiming its laser along LaserDirection; it fires when the delay ends.</summary>
        public bool LaserLocking => laserTimer > 0f;

        /// <summary>Flat unit direction the Spire locked the moment its light wave caught the player.</summary>
        public Vector3 LaserDirection { get; private set; } = Vector3.forward;

        /// <summary>Laser reach from the Spire's centre, already cut short by the first wall.</summary>
        public float LaserLength { get; private set; }

        /// <summary>Increments each time a laser fires (VFX trigger).</summary>
        public int LaserSequence { get; private set; }

        // ───── Spire light wave ─────
        // The wind-up no longer ends in a pulse that hurts the
        // player inside the circle. It sends a light wave from the Spire's centre out to its sentry range (pulseRadius); the wave hurts
        // nobody, and when its front reaches the living player with no wall between them the laser locks on them. The laser is the attack.
        bool waveOut, waveCaught;
        float waveRadius;
        Vector3 waveCentre;

        /// <summary>The Spire's light wave is on its way out to the sentry range. It ends there, or when the Spire dies, is stunned or
        /// frozen, loses the player to Void Walk or to death, or is configured again for a new spawn. The player's death is read here as
        /// well: the result screen stops time in the frame the player dies, so no step of the Spire's may follow to end the wave, and its
        /// ring would stay frozen on the ground behind the result.</summary>
        public bool LightWaveActive => waveOut && IsAlive && GM && GM.player && GM.player.IsAlive;

        /// <summary>Distance of the wave's front from LightWaveCentre: what the wave has reached so far. The ring on the ground draws its
        /// leading edge exactly here (SpireWavePresentation).</summary>
        public float LightWaveRadius => waveRadius;

        /// <summary>Where the wave started: the Spire's centre on the ground.</summary>
        public Vector3 LightWaveCentre => waveCentre;

        /// <summary>The sentry range the wave grows to (pulseRadius, the size of the warning circle).</summary>
        public float LightWaveReach => definition ? Mathf.Max(0f, definition.pulseRadius) : 0f;

        /// <summary>Increments each time a light wave starts (VFX trigger).</summary>
        public int LightWaveSequence { get; private set; }

        /// <summary>Increments each time a light wave catches the player and the laser locks (VFX trigger: the flash on the player).</summary>
        public int LightWaveCatches { get; private set; }

        // ───── Chanter aura (system doc §10.2) ─────
        float chantStamp = -1f;
        bool chantCached;

        /// <summary>Radius of every Chanter's damage aura (for UI/VFX).</summary>
        public static float ChantRadius => GM && GM.waves ? Mathf.Max(0f, GM.waves.chantRadius) : 6f;

        static float ChantDamageBonus => GM && GM.waves ? Mathf.Max(0f, GM.waves.chantDamageBonus) : .3f;

        /// <summary>True while this living non-boss enemy stands within ChantRadius of another living Chanter. Cached once per physics step.</summary>
        public bool Chanted
        {
            get
            {
                float now = Time.fixedTime;
                if (chantStamp != now)
                {
                    chantStamp = now;
                    chantCached = ScanChanters();
                }

                return chantCached;
            }
        }

        /// <summary>Multiplier on every hit this enemy deals (Chanter aura). Spitter spikes read it when fired.</summary>
        public float OutgoingDamageFactor => Chanted ? 1f + ChantDamageBonus : 1f;

        bool ScanChanters()
        {
            if (!definition || !IsAlive || IsBoss || definition.kind == EnemyKind.Chanter || !GM || !GM.waves)
                return false;
            float limit = ChantRadius * ChantRadius;
            var others = GM.waves.Alive;
            for (int i = 0; i < others.Count; i++)
            {
                var chanter = others[i];
                if (!chanter || chanter == this || !chanter.definition ||
                    chanter.definition.kind != EnemyKind.Chanter || !chanter.IsAlive) continue;
                if (Query.Flat(chanter.Position - Position).sqrMagnitude <= limit) return true;
            }

            return false;
        }

        // ───── Bloater radiation (system doc §10.2) ─────
        /// <summary>Ground left by a Bloater explosion; the player loses HP while standing inside (WaveManager ticks it). Drawn by the art layer.</summary>
        [Serializable]
        public struct RadiationZone
        {
            public Vector3 position;
            public float radius;
            public float timeLeft;
            public float life;
            public float Remaining01 => life > 0f ? Mathf.Clamp01(timeLeft / life) : 0f;
        }

        static readonly List<RadiationZone> radiationZones = new();
        public static IReadOnlyList<RadiationZone> RadiationZones => radiationZones;

        public static void AddRadiationZone(Vector3 at, float radius, float life)
        {
            if (radius <= 0f || life <= 0f) return;
            radiationZones.Add(new RadiationZone
                { position = Query.Flat(at), radius = radius, timeLeft = life, life = life });
        }

        /// <summary>Ages every zone, drops expired ones and returns whether point stands inside any remaining zone.</summary>
        public static bool TickRadiationZones(float dt, Vector3 point)
        {
            bool inside = false;
            for (int i = radiationZones.Count - 1; i >= 0; i--)
            {
                var zone = radiationZones[i];
                zone.timeLeft -= dt;
                if (zone.timeLeft <= 0f)
                {
                    radiationZones.RemoveAt(i);
                    continue;
                }

                radiationZones[i] = zone;
                inside |= Query.Flat(point - zone.position).sqrMagnitude <= zone.radius * zone.radius;
            }

            return inside;
        }

        public static void ClearRadiationZones() => radiationZones.Clear();

        // Special-enemy numbers live on the EnemyDefinition asset (design document 10.2) so the enemy lab can tune each kind.
        float SpireLaserDelay => definition ? Mathf.Max(.01f, definition.laserDelay) : .6f;
        float SpireLaserLength => definition ? Mathf.Max(0f, definition.laserLength) : 12f;
        float SpireLaserHalfWidth => definition ? Mathf.Max(0f, definition.laserHalfWidth) : .6f;
        float SpireWaveSpeed => definition ? Mathf.Max(.1f, definition.waveSpeed) : 12f;
        static float ChantInterval => GM && GM.waves ? Mathf.Max(.1f, GM.waves.chantInterval) : 3f;

        static float VoidIntervalFactor =>
            GM && GM.waves ? Mathf.Clamp(GM.waves.voidAffixIntervalFactor, .1f, 1f) : .7f;

        static float MiniScale => GM && GM.waves ? Mathf.Clamp(GM.waves.splitterMiniScale, .1f, 1f) : .6f;
        static float MiniDamageShare => GM && GM.waves ? Mathf.Clamp01(GM.waves.splitterMiniDamageShare) : .6f;

        /// <param name="mini">Mini Splitter: max HP from EncounterBalance's mini share, radius/model/damage scaled down, never splits.</param>
        public void Configure(EnemyDefinition data, float hpMultiplier, bool elite,
            ElementKind affix = ElementKind.None, bool mini = false)
        {
            CorpseActive = CorpseCarried = CorpseFeedConsumed = false;
            corpseSeconds = 0;
            Raider = false;
            raidAggro = raidWindup = false;
            corpseVelocity = Vector3.zero;
            float bodyScale = mini ? MiniScale : 1f;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule)
            {
                capsule.enabled = true;
                if (savedCapsuleRadius < 0f) savedCapsuleRadius = capsule.radius;
                capsule.radius = savedCapsuleRadius * bodyScale;
            }

            definition = data;
            ArenaCodexProgress.DiscoverEnemy(data.kind);
            Elite = elite;
            Mini = mini;
            Affix = data.canAffix ? affix : ElementKind.None;
            bool fodder = !elite && EnemyRoles.IsMelee(data.kind);
            var balance = GM && GM.waves && GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
            maxHp = fodder ? data.hp : Mathf.Round(data.hp * hpMultiplier * (elite ? 3f : 1f));
            if (mini) maxHp = Mathf.Max(1f, Mathf.Round(maxHp * (balance ? balance.MiniShare(true) : .4f)));
            if (balance) maxHp = balance.MaxHp(data, GM.waves.WaveIndex + 1, elite, Affix, mini);
            ApplyCampaignBudget(data, elite, mini); // Enemy.CampaignBudget.cs: the V2 day campaign's own HP (since 2026-11-10)
            moveSpeed = data.speed * (GM && GM.waves ? GM.waves.EnemySpeedFactor : 1f);
            radius = data.radius * bodyScale;
            attackDamage = data.damage * (elite ? 1.5f : 1f) * (mini ? MiniDamageShare : 1f) *
                           (GM && GM.waves ? data.kind == EnemyKind.Tyrant ? GM.waves.FinalBossDamageFactor : GM.waves.EnemyDamageFactor : 1f);
            windupTime = data.windup;
            attackInterval =
                data.interval * (Affix == ElementKind.Void ? VoidIntervalFactor : 1f); // §7.3: Void acts faster.
            attackRange = data.reach;
            // §5.3: the Rammer pushes the player by the light tier and the Stomper by the heavy tier; a boss's plain hit keeps its own number.
            var knock = KnockbackRules.Active;
            attackKnockback = !EnemyRoles.KnocksBackPlayer(data.kind) ? 0f
                : IsBoss ? data.knockback : knock.Distance(KnockbackRules.AttackTier(data.kind));
            // §5.3, §10.6: the bigger the body, the shorter its slide, and an elite slides less than its kind; bosses and the Spire never move.
            knockbackScale = Immovable ? 0f : knock.ScaleFor(radius, elite);
            large = data.kind == EnemyKind.Stomper || IsBoss; // Brute is a heavier melee variant, not a large enemy.
            ResetForSpawn();
            AttackSequence = 0;
            windup = false;
            charging = rushing = false;
            dyingExplosion = false;
            SuppressRewards = false;
            cooldown = 0.5f;
            spawnVisible = data.kind == EnemyKind.Shade ? 2f : 0f;
            revealTimer = spawnVisible;
            supportTimer = 0f;
            summonTimer = data.kind == EnemyKind.Hollow ? Boss.prophetSummonInterval : Boss.tyrantSummonInterval;
            bossPhase = 1;
            inhaleLeft = bossRecovery = 0f;
            ClearBossMarks();
            forcedMove = followUp = -1;
            chargesLeft = ringsLeft = ringIndex = 0;
            ringTimer = volleyTimer = leapTimer = ringBaseAngle = 0f;
            royalCastPending = false;
            leaping = royalShield = inhaleHit = false;
            openingMove = data.kind == EnemyKind.Hollow;
            if (EnemyRoles.IsBoss(data.kind) && !GetComponent<V2BossGroundMarks>()) gameObject.AddComponent<V2BossGroundMarks>();
            // The Spire's light wave ring and the flash on the player it catches, made at runtime like the marks above.
            if (data.kind == EnemyKind.Spire && !GetComponent<SpireWavePresentation>()) gameObject.AddComponent<SpireWavePresentation>();
            laserTimer = 0f;
            LaserLength = 0f;
            waveOut = waveCaught = false;
            waveRadius = 0f;
            chantStamp = -1f;
            statusBoard = ElementStatus.For(this);
            if (statusBoard != null)
            {
                statusBoard.ClearStatuses();
                statusBoard.CarriesVoid = Affix == ElementKind.Void;
                statusBoard.SetShield(maxHp * StartShieldShare(data));
                statusBoard.StatusApplied -= OnStatusApplied;
                statusBoard.StatusApplied += OnStatusApplied;
                statusBoard.ImmuneToControl = IsBoss;
                statusBoard.ControlThresholdFactor = (large ? 2f : 1f) * (elite ? 2f : 1f);
                if (data.kind == EnemyKind.Hollow)
                {
                    // §7.6, §13.1: the Hollow always ignores Frost and Lightning, damage and statuses alike (and stays immune to control).
                    foreach (var element in ElementImmunity.Boss) statusBoard.SetImmunity(element, 1f);
                }
            }

            ConfigureTyrant(data); // Enemy.Tyrant.cs: its own state, immunity and ground marks, on every Configure so nothing carries over

            if (bodyRenderer)
            {
                if (savedBodyScale == Vector3.zero) savedBodyScale = bodyRenderer.transform.localScale;
                bodyRenderer.transform.localScale = savedBodyScale * bodyScale;
                bodyRenderer.enabled = true;
                RefreshBodyTint();
            }
            Swell = 1f;

            if (telegraph) telegraph.gameObject.SetActive(false);
            if (rb)
            {
                rb.isKinematic = data.kind == EnemyKind.Spire;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        void OnStatusApplied(ElementKind element) => revealTimer = 2f;

        public override bool ReceiveDamage(DamageInfo info)
        {
            if (IsBoss)
            {
                // System doc §5.3: bosses are wholly immune to knockback; it neither moves them nor builds toward a stun.
                info.knockDistance = 0f;
            }
            else if (Immovable) info.knockDistance = 0f;

            if (TurnedAwayByWard()) return false; // the Nash Prophet takes no damage while a summon lives (Enemy.Prophet.cs)
            bool accepted = base.ReceiveDamage(info);
            // Execute Twist: any accepted damage may leave a marked enemy under the line; checked only after the hit has fully
            // resolved, so its stacks never land on a dead body.
            if (accepted && IsAlive && GM && GM.runWeapons) GM.runWeapons.CheckExecuteMark(this);
            return accepted;
        }

        protected override void FixedUpdate()
        {
            if (ActionsBlocked)
            {
                CancelWindup();
                charging = false;
                laserTimer = 0f;
                waveOut = false; // a stunned or frozen Spire drops its light wave as it drops its laser
            }

            base.FixedUpdate();
        }

        public void SetAffix(ElementKind element)
        {
            Affix = element;
            if (statusBoard) statusBoard.CarriesVoid = element == ElementKind.Void;
            if (definition)
                attackInterval = definition.interval * (element == ElementKind.Void ? VoidIntervalFactor : 1f);
        }

        void Update()
        {
            // The landing circle is a child of the body: keep it on the landing point while the body turns (wind-up) and flies.
            if (telegraph && (leaping || windup && IsBoss && definition.kind == EnemyKind.Tyrant && bossAttack == LeapMove))
                telegraph.position = leapTarget + Vector3.up * .03f;
            if (!dyingExplosion || !GM.IsCombatActive) return;
            float dt = GM.WorldDeltaTime;
            explosionTimer -= dt;
            if (bodyRenderer) bodyRenderer.transform.localScale *= 1f + dt * 0.6f;
            Swell *= 1f + dt * 0.6f;
            if (explosionTimer > 0f) return;
            Explode();
            dyingExplosion = false;
            BeginCorpse();
        }

        public bool IsLarge => large;
        public bool WindingUp => windup;
        /// <summary>Whether this enemy's hits may push the player (EnemyRoles.KnocksBackPlayer).</summary>
        public bool PushesPlayer => definition && EnemyRoles.KnocksBackPlayer(definition.kind);
        public bool Charging => charging;
        public float CooldownRemaining => Mathf.Max(0f, cooldown);
        /// <summary>No attack starts for at least this many seconds (the void trial's opening grace).</summary>
        public void HoldAttacks(float seconds) => cooldown = Mathf.Max(cooldown, seconds);
        public float WindupSecondsRemaining => windup ? Mathf.Max(0f, windupTimer) : 0f;

        /// <summary>Counts attacks, support casts (Warden shield) and chants (Chanter) for the art layer.</summary>
        public int AttackSequence { get; private set; }

        public override Team Team => Team.Enemy;
        public override Vector3 AimDirection => facing;
        public override float CorpseDamage => attackDamage;

        Vector3 facing = Vector3.back;
        bool windup;
        float windupTimer;
        float cooldown;

        protected override void TickNormal(float dt)
        {
            if (cooldown > 0f)
                cooldown -= dt * (IsBoss && bossPhase == 3 ? definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f : 1f);
            var p = GM.player;
            if (!GM.IsCombatActive || p == null || !p.IsAlive)
            {
                Stop();
                CancelWindup();
                // A Spire's light wave has nobody left to find once the player is dead; a card pick or the pause only holds it still.
                if (p == null || !p.IsAlive) waveOut = false;
                return;
            }

            // Void Walk: the player cannot be targeted, so an ordinary enemy drops what it was doing, walks to where
            // the player vanished and looks around there. The Tyrant finishes a move it began and starts no new one (Enemy.Tyrant.cs).
            // V2 day campaign: a raider walks to the base and strikes it; within RaidAggroRadius of the player it fights the player as any
            // enemy does (RaidTurnsOnPlayer). The Spire never raids: it cannot walk, and its body does not take a walking speed.
            if (Raider && !Immovable && RaidSpot != null && !RaidTurnsOnPlayer(p) && RaidSpot(this) is Vector3 raid) { TickRaid(dt, raid); return; }
            if (raidWindup) CancelWindup(); // a strike wound up on the base (the raid just ended) is not let loose on the player

            if (!IsBoss && WorldMap.Active && WorldMap.Active.PlayerHidden)
            {
                SearchForHiddenPlayer(dt);
                return;
            }

            if (definition && TickSpecial(dt, p)) return;

            if (windup)
            {
                // Facing is locked; it no longer turns with the player
                Stop();
                if (definition && definition.kind == EnemyKind.Bloater)
                {
                    float grow = 1f + dt * BloaterFuseSwell / Mathf.Max(.05f, windupTime);
                    if (bodyRenderer) bodyRenderer.transform.localScale *= grow;
                    Swell *= grow;
                }
                windupTimer -= dt;
                if (windupTimer <= 0f)
                {
                    PerformAttack();
                    CancelWindup();
                    cooldown = attackInterval;
                }

                return;
            }

            Vector3 to = Query.Flat(p.Position - Position);
            if (WorldMap.Active && WorldMap.Active.DecoyActive) to = Query.Flat(WorldMap.Active.EnemyTarget - Position);
            float dist = to.magnitude;
            if (dist > 0.01f) facing = to / dist;
            float edge = dist - radius - p.Radius;

            if (edge <= triggerRange && cooldown <= 0f)
            {
                windup = true;
                windupTimer = windupTime;
                V5WeaponAudio.Windup(definition ? definition.kind : EnemyKind.Grunt, Position);
                if (definition && definition.kind == EnemyKind.Shade) revealTimer = windupTime + 1f;
                SetLook(true);
                Stop();
                return;
            }

            Vector3 direction = WorldMap.Active
                ? WorldMap.Active.GetChaseDirection(Position, WorldMap.Active.EnemyTarget, FollowingIntoGravity)
                : facing;
            Vector3 v = edge > triggerRange ? direction * moveSpeed : Vector3.zero;
            v = KeepOffEdges(v, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        const float SearchTurnDegrees = 90f;

        /// <summary>Void Walk: walk to where the player vanished, then stand there turning to look around.</summary>
        void SearchForHiddenPlayer(float dt)
        {
            if (windup) CancelWindup();
            charging = false;
            waveOut = false; // a Spire's light wave has nobody left to find
            Vector3 spot = WorldMap.Active.EnemyTarget;
            if (Query.Flat(spot - Position).magnitude > radius + 1f)
            {
                Vector3 direction = WorldMap.Active.GetChaseDirection(Position, spot, FollowingIntoGravity);
                if (direction.sqrMagnitude > 0.0001f) facing = Query.Flat(direction).normalized;
                Vector3 v = KeepOffEdges(direction * moveSpeed, dt);
                rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
                return;
            }

            Stop();
            facing = Quaternion.Euler(0f, SearchTurnDegrees * dt, 0f) * facing;
        }

        /// <summary>
        /// V2 day campaign: whether this raider fights the player now instead of raiding. It turns on a player within RaidAggroRadius and
        /// goes back to the base once the player is past RaidAggroRadius + RaidAggroRelease. A strike, charge or rush already under way
        /// finishes on the target it was aimed at before the raider turns, and a player out of sight (Void Walk) sends it back to the base.
        /// </summary>
        bool RaidTurnsOnPlayer(PlayerController player)
        {
            bool wanted = false;
            if (RaidAggroRadius > 0f && !(WorldMap.Active && WorldMap.Active.PlayerHidden))
                wanted = Query.Flat(player.Position - Position).magnitude <= RaidAggroRadius + (raidAggro ? RaidAggroRelease : 0f);
            if (wanted != raidAggro && !windup && !charging && !rushing) raidAggro = wanted;
            return raidAggro;
        }

        /// <summary>
        /// Whether this raider stands against the base wall its stand point faces: within reach of the wall, on the stand point's side and
        /// in front of the wall rather than past its corner. The base is a square round RaidCentre and the stand point lies a body and a
        /// little out from its side (V2Campaign.RaidSpotFor), so the side and its width follow from the point. Lets a raider whose own
        /// point is held by other raiders strike from where it stands at the wall instead of pushing at them for ever.
        /// </summary>
        bool AgainstRaidWall(Vector3 spot, out Vector3 normal)
        {
            Vector3 outward = Query.Flat(spot - RaidCentre);
            normal = Mathf.Abs(outward.x) > Mathf.Abs(outward.z) ? new Vector3(Mathf.Sign(outward.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(outward.z));
            Vector3 along = new Vector3(normal.z, 0f, normal.x);
            float standOut = Vector3.Dot(outward, normal), depth = Vector3.Dot(Query.Flat(Position - RaidCentre), normal);
            return depth <= standOut + .6f && depth >= standOut - radius - .5f
                   && Mathf.Abs(Vector3.Dot(Query.Flat(Position - RaidCentre), along)) <= standOut - radius;
        }

        /// <summary>V2 day campaign raid: walk to the stand point next to the base (around walls by the raid flow field), wind up as for
        /// a normal strike and hit the base. The player only matters within RaidAggroRadius (RaidTurnsOnPlayer). Every walking kind raids
        /// this way, the ranged and support kinds included: their own attacks are aimed at the player, so at the base they strike as a
        /// melee enemy does.</summary>
        void TickRaid(float dt, Vector3 spot)
        {
            // A charge or rush begun on the player before the raid started does not carry on into it.
            if (charging || rushing) { charging = rushing = false; RefreshBodyTint(); }
            // A Shade keeps its own clock on a raid: seen for a moment after its birth and while it strikes, cloaked otherwise.
            if (definition && definition.kind == EnemyKind.Shade)
            {
                revealTimer -= dt;
                RefreshBodyTint();
                if (spawnVisible > 0f) { spawnVisible -= dt; Stop(); return; }
            }
            // A wind-up begun on the player before the raid started is dropped, never let loose on the base from afar.
            if (windup && !raidWindup) CancelWindup();
            Vector3 to = Query.Flat(spot - Position);
            float dist = to.magnitude;
            if (windup)
            {
                Stop();
                windupTimer -= dt;
                if (windupTimer <= 0f)
                {
                    AttackSequence++;
                    V5WeaponAudio.Swing(definition ? definition.kind : EnemyKind.Grunt, Position);
                    Fx.Telegraph(Position, facing, radius + attackRange, attackArc, new Color(1f, .3f, .2f, .55f));
                    RaidStrike?.Invoke(this, attackDamage * OutgoingDamageFactor);
                    CancelWindup();
                    cooldown = attackInterval;
                }
                return;
            }
            // At the stand point, or against the wall in front of it while other raiders hold the point itself: face the wall and strike.
            if (AgainstRaidWall(spot, out var wallNormal) || dist <= .6f)
            {
                Stop();
                facing = -wallNormal;
                if (cooldown <= 0f)
                {
                    windup = raidWindup = true;
                    windupTimer = windupTime;
                    V5WeaponAudio.Windup(definition ? definition.kind : EnemyKind.Grunt, Position);
                    if (definition && definition.kind == EnemyKind.Shade) revealTimer = windupTime + 1f; // seen while it strikes
                    SetLook(true);
                }
                return;
            }
            Vector3 direction = WorldMap.Active ? WorldMap.Active.GetRaidDirection(Position, spot) : to / Mathf.Max(.01f, dist);
            if (direction.sqrMagnitude > .0001f) facing = Query.Flat(direction).normalized;
            Vector3 v = KeepOffEdges(direction * moveSpeed, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        void PerformAttack()
        {
            AttackSequence++;
            if (!definition || definition.kind is EnemyKind.Grunt or EnemyKind.Brute or EnemyKind.Skitter
                    or EnemyKind.Splitter or EnemyKind.Shade)
                V5WeaponAudio.Swing(definition ? definition.kind : EnemyKind.Grunt, Position);
            if (definition && PerformSpecialAttack()) return;
            // An affixed enemy's punch shows its element (V5 art); the shape stays the true reach.
            Color punch = V5Vfx.On && Affix != ElementKind.None ? V5Vfx.ElementTint(Affix) : new Color(1f, 0.3f, 0.2f, 1f);
            Fx.Telegraph(Position, facing, radius + attackRange, attackArc, new Color(punch.r, punch.g, punch.b, 0.55f));

            var p = GM.player;
            var shield = p.HeldShield;
            if (shield != null && shield != this && InFist(shield))
            {
                // Hitting cover does not knock it back (distance 0), but a direction is passed so shards fly outward
                DealHit(shield, attackDamage, shield.Position - Position, 0f);
                return;
            }

            if (p.IsAlive && InFist(p))
                DealHit(p, attackDamage, p.Position - Position, attackKnockback);
        }

        /// <summary>
        /// The single place an enemy damages a target. Applies the Chanter aura, keeps non-boss hits from pushing the player (§5.2),
        /// applies the affix after an accepted hit and lets a Shade's landed punch kill the player outright (unless protected or flying).
        /// </summary>
        bool DealHit(Combatant target, float damage, Vector3 direction, float knock)
        {
            var player = target as PlayerController;
            if (player && !PushesPlayer) knock = 0f;
            bool lethal = player && definition && definition.kind == EnemyKind.Shade && !player.Protected &&
                          player.State != CharacterState.Dashing;
            float beforeHp = target.Hp;
            var board = ElementStatus.For(target);
            float beforeShield = board ? board.Shield : 0f;
            var hit = DamageInfo.Attack(damage * OutgoingDamageFactor, this, direction, knock);
            hit.element = Affix; // Combatant applies Light-versus-Void once before resolving Shield.
            bool landed = target.ReceiveDamage(hit);
            if (landed && Affix != ElementKind.None)
                ElementCombat.ApplyAffix(this, target, Affix,
                    Mathf.Max(0f, beforeHp - target.Hp) + Mathf.Max(0f, beforeShield - (board ? board.Shield : 0f)));
            if (landed && lethal && target.IsAlive) target.Kill(this); // §10.2: the Shade assassinates.
            return landed;
        }

        bool TickSpecial(float dt, PlayerController player)
        {
            revealTimer -= dt;
            if (definition.kind == EnemyKind.Shade)
            {
                RefreshBodyTint();
                if (spawnVisible > 0f)
                {
                    spawnVisible -= dt;
                    Stop();
                    return true;
                }
            }

            if (definition.kind == EnemyKind.Tyrant) return TickTyrant(dt, player); // Enemy.Tyrant.cs (system doc 13.2, 2026-10-25)

            if (IsBoss)
            {
                if (leaping)
                {
                    TickLeap(dt);
                    return true;
                }

                if (definition.kind == EnemyKind.Hollow) return TickProphet(dt, player); // the Nash Prophet: Enemy.Prophet.cs
                TickLandingZones(dt);
                TickSummonWarning(dt);
                TickSummonClock(dt);
                TickBossPhase();
                if (definition.kind == EnemyKind.Hollow)
                {
                    TickPlatform(dt);
                    TickVolleys(dt, player);
                }

                if (inhaleLeft > 0f)
                {
                    inhaleLeft -= dt;
                    if (inhaleLeft <= 0f)
                    {
                        // An inhale always ends in a repulse pulse.
                        bossAttack = PulseMove;
                        BeginWindup(Boss.inhaleFollowWindup, PulseReach, false);
                    }

                    Stop();
                    return true;
                }

                if (bossRecovery > 0f)
                {
                    bossRecovery -= dt;
                    Stop();
                    return true;
                }

                if (followUp >= 0 && !windup && !charging)
                {
                    int move = followUp;
                    followUp = -1;
                    StartBossMove(move, player);
                    return true;
                }

                if (royalCastPending && !windup && !charging)
                {
                    royalCastPending = false;
                    StartBossMove(RoyalShieldMove, player);
                    return true;
                }
            }

            bool aiming = laserTimer > 0f;
            if (aiming)
            {
                // Spire: the direction locked when the wave caught the player holds; the laser fires when the delay runs out.
                laserTimer -= dt;
                if (laserTimer <= 0f) FireLaser(player);
            }

            // Spire: the light wave runs on to the sentry range while the laser it set off aims and fires. A catch in this step starts
            // the laser's delay, which counts from the next step as it did when the pulse locked it.
            if (waveOut) TickLightWave(dt, player);
            if (aiming || laserTimer > 0f) return true;

            if (charging)
            {
                float speed = IsBoss ? 18f * (bossPhase == 3 ? 1.25f : 1f) : definition.chargeSpeed;
                speed *= GM && GM.waves ? GM.waves.MovementSpeedScale : 1f;
                float step = speed * dt;
                bool wall = Query.WallBetween(Query.AtCastHeight(Position),
                    Query.AtCastHeight(Position + facing * (step + radius)), out _);
                bool hole = IsBoss && WorldMap.Active && WorldMap.Active.IsHole(Position + facing * (step + radius));
                if (wall || hole)
                {
                    charging = false;
                    Stop();
                    if (IsBoss)
                    {
                        // Fixed order: clear the shield, then the HP share (so the shield cannot eat it).
                        chargesLeft = 0;
                        statusBoard?.SetShield(0f);
                        ReceiveDamage(new DamageInfo
                            { amount = maxHp * Boss.wallHitHpShare, source = DamageSource.Wall, kind = HitKind.Slam });
                    }
                    else
                    {
                        ReceiveDamage(new DamageInfo
                            { amount = GM.rules.wallDamage * GM.waves.HpMultiplier * 2, kind = HitKind.Slam });
                        V5WeaponAudio.At(V5Sfx.RamCrash, Position);
                        cooldown = 1.5f;
                        Stun(1.5f);
                    }

                    return true;
                }

                rb.linearVelocity = facing * speed + Vector3.up * rb.linearVelocity.y;
                chargeLeft -= step;
                if (InFist(player))
                {
                    // §10.2: a Rammer's charge pushes the player a little (the light tier, §5.3) and stuns; the Tyrant's charge knocks back.
                    bool landed = DealHit(player, IsBoss ? attackDamage * Boss.chargeDamage : attackDamage, facing,
                        IsBoss ? Boss.chargePush : attackKnockback);
                    if (landed && !IsBoss && player.IsAlive)
                    {
                        player.Stun(definition.chargeStun);
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.PlayerStunned, player.Position);
                    }
                    charging = false;
                    if (IsBoss)
                    {
                        chargesLeft = 0; // hitting the player ends a run of charges
                        bossRecovery = 1.5f;
                    }
                }

                if (chargeLeft <= 0f)
                {
                    bool ranOut = charging;
                    charging = false;
                    cooldown = 1.5f;
                    if (IsBoss && ranOut && --chargesLeft > 0)
                    {
                        // Phases 2-3: re-aim at the player, redraw the line, charge again.
                        FacePlayer(player);
                        bossAttack = ChargeMove;
                        BeginWindup(Boss.chargeReaim, ChargeLineLength(), false);
                    }
                    else if (IsBoss) bossRecovery = 1.5f;
                }

                return true;
            }

            Vector3 to = Query.Flat(player.Position - Position);
            float distance = to.magnitude;
            if (windup) return false;
            if (distance > 0.01f) facing = to / distance;
            switch (definition.kind)
            {
                case EnemyKind.Spitter:
                    MoveTowards(player.Position, distance < definition.rangedNear ? -1f : distance > definition.rangedFar ? 1f : 0f, dt);
                    if (cooldown <= 0f && !Query.WallBetween(Query.AtCastHeight(Position),
                            Query.AtCastHeight(player.Position), out _)) BeginWindup(windupTime, definition.rangedFar);
                    return true;
                case EnemyKind.Warden:
                    supportTimer -= dt;
                    if (supportTimer <= 0f)
                    {
                        bool shieldAdded = false;
                        foreach (var enemy in GM.waves.Alive)
                            // Bosses carry their own shield budget (EncounterBalance); a Warden summon must not refill it.
                            if (enemy && enemy != this && !enemy.IsBoss && Query.Flat(enemy.Position - Position).magnitude < 5f)
                            {
                                var board = ElementStatus.For(enemy);
                                if (board != null)
                                {
                                    float before = board.Shield;
                                    board.SetShield(Mathf.Max(board.Shield, enemy.maxHp * 0.3f));
                                    shieldAdded |= board.Shield > before;
                                    if (V5Vfx.On && board.Shield > before) V5Vfx.I.WardLink(Position, enemy.Position, enemy.radius);
                                }
                            }

                        if (shieldAdded)
                        {
                            AttackSequence++; // The saved support-cast clip follows an actual shield grant.
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShield, Position);
                        }

                        supportTimer = 4f;
                    }

                    HoldBehindAllies(player, distance, dt);
                    return true;
                case EnemyKind.Chanter:
                    // Never attacks. The aura is always on (see Chanted); the chant is the readable rhythm.
                    supportTimer -= dt;
                    if (supportTimer <= 0f)
                    {
                        AttackSequence++;
                        V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChant, Position);
                        supportTimer = ChantInterval;
                    }

                    HoldBehindAllies(player, distance, dt);
                    return true;
                case EnemyKind.Spire:
                    // One light wave at a time: the next wind-up waits until the last wave has reached the sentry range.
                    if (cooldown <= 0f && !waveOut) BeginWindup(windupTime, definition.pulseRadius);
                    return true;
                case EnemyKind.Stomper:
                    if (distance <= definition.triggerDistance && cooldown <= 0f)
                    {
                        BeginWindup(windupTime, definition.pulseRadius);
                        return true;
                    }

                    break;
                case EnemyKind.Rammer:
                    if (distance <= definition.triggerDistance && cooldown <= 0f)
                    {
                        BeginWindup(windupTime, definition.chargeDistance);
                        return true;
                    }

                    break;
                case EnemyKind.Bloater:
                    // §10.2: walks in; from triggerDistance it rushes the player (chargeSpeed, at most chargeDistance), and once its
                    // edge is within reach of the player it lights a short fuse (windup) and blows up.
                    if (distance <= radius + player.Radius + definition.reach)
                    {
                        rushing = false;
                        BeginWindup(windupTime, definition.pulseRadius);
                        return true;
                    }

                    if (!rushing && cooldown <= 0f && distance <= definition.triggerDistance)
                    {
                        rushing = true;
                        chargeLeft = definition.chargeDistance;
                        V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
                        RefreshBodyTint(); // document 9.2: the whitebox body turns yellow as it starts to run in, as the illustration does
                    }

                    if (rushing)
                    {
                        float rush = definition.chargeSpeed * (GM && GM.waves ? GM.waves.MovementSpeedScale : 1f);
                        MoveAt(player.Position, rush, dt);
                        chargeLeft -= rush * dt;
                        if (chargeLeft <= 0f)
                        {
                            rushing = false;
                            cooldown = attackInterval;
                            RefreshBodyTint(); // the rush ran out short of the player: back to its kind colour
                        }

                        return true;
                    }

                    MoveTowards(player.Position, 1f, dt);
                    return true;
                case EnemyKind.Hollow:
                    if (cooldown <= 0f) StartBossMove(ChooseBossMove(distance), player);
                    Stop();
                    return true;
                case EnemyKind.Tyrant:
                    // Every distance has a move, so it never falls back to the plain punch; between moves it walks.
                    if (cooldown <= 0f)
                    {
                        StartBossMove(ChooseBossMove(distance), player);
                        return true;
                    }

                    break;
            }

            return false;
        }

        // ───── Bosses (system doc §13) ─────

        /// <summary>Nash Prophet move by phase and distance band: close it only pulses or inhales, far it fires (no unavoidable
        /// point-blank shots, no pulse that only shoos a distant player away). roll is uniform in [0, 1).</summary>
        public static int ProphetMoveFor(int phase, bool close, float roll, BossTuning b)
        {
            if (close) return phase == 2 && roll >= b.phase2ClosePulseShare ? InhaleMove : PulseMove;
            if (phase <= 1) return roll < b.phase1DoubleShotShare ? DoubleShotMove : RingMove;
            if (phase == 2)
                return roll < b.phase2DoubleShotShare ? DoubleShotMove
                    : roll < b.phase2DoubleShotShare + b.phase2RingShare ? RingMove : InhaleMove;
            return roll < b.phase3DoubleShotShare ? DoubleShotMove
                : roll < b.phase3DoubleShotShare + b.phase3RingShare ? RingMove : ProphecyMove;
        }

        /// <summary>Tyrant move by distance band, each band edge matching a move's real reach so a chosen move can land; a far
        /// or off-screen player gets the leap, so circling at range is no longer free.</summary>
        public static int TyrantMoveFor(float distance, bool onScreen, float roll, BossTuning b)
        {
            if (distance > b.tyrantFarRange || !onScreen) return LeapMove;
            if (distance <= b.tyrantCloseRange)
                return roll < b.closeCleaveShare ? CleaveMove
                    : roll < b.closeCleaveShare + b.closeSlamShare ? SlamMove : ChargeMove;
            if (distance <= b.tyrantMidRange) return roll < b.midSlamShare ? SlamMove : ChargeMove;
            return roll < b.farChargeShare ? ChargeMove : LeapMove;
        }

        int ChooseBossMove(float distance)
        {
            float roll = UnityEngine.Random.value;
            int move;
            if (definition.kind == EnemyKind.Hollow)
            {
                bool close = distance <= (bossPhase == 3 ? Boss.prophetPhase3CloseRange : Boss.prophetCloseRange);
                move = ProphetMoveFor(bossPhase, close, roll, Boss);
                // The first move teaches the rhythm: turn the moment the body stops glowing.
                if (openingMove && !close) move = DoubleShotMove;
                openingMove = false;
            }
            else move = TyrantMoveFor(distance, SkillWorldAPI.IsOnScreen?.Invoke(Position) ?? true, roll, Boss);

            if (forcedMove >= 0)
            {
                move = forcedMove;
                forcedMove = -1;
            }

            return move;
        }

        float PulseReach => bossPhase == 3 ? Boss.phase3PulseReach : Boss.pulseReach;

        /// <summary>Radius the trial platform shrinks toward in this phase (drawn as a dashed line until it gets there).</summary>
        public float PlatformGoal => bossPhase == 3 ? Boss.prophetPlatformRadii.z
            : bossPhase == 2 ? Boss.prophetPlatformRadii.y : Boss.prophetPlatformRadii.x;

        void FacePlayer(PlayerController player)
        {
            if (!player) return;
            Vector3 to = Query.Flat(player.Position - Position);
            if (to.sqrMagnitude > 1e-4f) facing = to.normalized;
        }

        void StartBossMove(int move, PlayerController player)
        {
            FacePlayer(player);
            bossAttack = move;
            var b = Boss;
            if (definition.kind == EnemyKind.Hollow)
            {
                switch (move)
                {
                    case PulseMove: BeginWindup(b.pulseWindup, PulseReach); break;
                    case InhaleMove: BeginWindup(b.inhaleWindup, 0f); break;
                    case DoubleShotMove: BeginWindup(b.shotWindup, 0f); break;
                    case RingMove: BeginWindup(b.ringWindup, 0f); break;
                    default: BeginWindup(b.prophecyWindup, 0f); break;
                }

                return;
            }

            switch (move)
            {
                case CleaveMove: BeginWindup(b.cleaveWindup, b.cleaveRange); break;
                case BackSlashMove: BeginWindup(b.backSlashWindup, b.cleaveRange); break;
                case SlamMove: BeginWindup(b.slamWindup, b.slamRange); break;
                case ChargeMove:
                    chargesLeft = bossPhase == 3 ? b.phase3Charges : bossPhase == 2 ? b.phase2Charges : 1;
                    BeginWindup(b.chargeWindup, ChargeLineLength());
                    break;
                case LeapMove:
                    // The landing point locks where the player stands now; it does not follow them.
                    leapTarget = LeapLanding(player ? player.Position : Position);
                    BeginWindup(b.leapWindup, b.leapRadius);
                    break;
                default: BeginWindup(b.royalShieldCast, 0f, false); break; // RoyalShieldMove: stands yellow, no attack
            }
        }

        /// <summary>How far the charge line reaches: up to the first wall, bulwark or hole along the facing, at most the full charge.</summary>
        float ChargeLineLength()
        {
            float length = TyrantChargeDistance;
            if (Query.WallBetween(Query.AtCastHeight(Position), Query.AtCastHeight(Position + facing * length), out var hit))
                length = Query.Flat(hit.point - Position).magnitude;
            var world = WorldMap.Active;
            if (world)
                for (float d = .5f; d < length; d += .5f)
                    if (world.IsHole(Position + facing * d))
                    {
                        length = d;
                        break;
                    }

            return Mathf.Max(.5f, length);
        }

        /// <summary>Nearest point to aim, walking back toward the Tyrant, where it can walk again: clear of walls, of hole cores (its
        /// front edge included) and of gravity rings, which it could not walk out of because it is never pulled.</summary>
        Vector3 LeapLanding(Vector3 aim)
        {
            aim = Query.Flat(aim);
            var world = WorldMap.Active;
            if (!world) return aim;
            Vector3 from = Query.Flat(Position);
            Vector3 delta = aim - from;
            Vector3 dir = delta.sqrMagnitude > 1e-4f ? delta.normalized : facing;
            for (float d = delta.magnitude; d >= 0f; d -= .5f)
            {
                Vector3 at = from + dir * d;
                if (world.IsWalkable(at, radius) && !world.IsHole(at + dir * radius)) return at;
            }

            return from;
        }

        void BeginLeap()
        {
            leaping = true;
            leapTimer = 0f;
            leapFrom = Query.Flat(Position);
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true; // flies over walls and holes
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = false;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position);
        }

        void TickLeap(float dt)
        {
            leapTimer += dt;
            float t = Mathf.Clamp01(leapTimer / Mathf.Max(.1f, Boss.leapFlight));
            rb.MovePosition(Vector3.Lerp(leapFrom, leapTarget, t) + Vector3.up * (Boss.leapHeight * 4f * t * (1f - t)));
            if (t < 1f) return;
            leaping = false;
            rb.position = leapTarget;
            transform.position = leapTarget;
            rb.isKinematic = false;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = true;
            if (telegraph) telegraph.gameObject.SetActive(false);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, leapTarget);
            if (V5Vfx.On) V5Vfx.I.GroundSlam(leapTarget, Boss.leapRadius, definition.colour);
            else Fx.Ring(leapTarget, Boss.leapRadius, definition.colour);
            PlayerBlast(leapTarget, Boss.leapRadius, attackDamage * Boss.leapDamage, Boss.leapPush);
            bossRecovery = Boss.leapRecovery;
        }

        /// <summary>Boss area hit on the player only: its own summons are never hit or pushed (system doc §13).</summary>
        bool PlayerBlast(Vector3 centre, float reach, float damage, float push)
        {
            var player = GM ? GM.player : null;
            if (!player || !player.IsAlive) return false;
            Vector3 to = Query.Flat(player.Position - centre);
            if (to.magnitude - player.Radius > reach) return false;
            return DealHit(player, damage, to.sqrMagnitude > 1e-4f ? to : facing, push);
        }

        void TickBossPhase()
        {
            float hpRatio = Hp / maxHp;
            int nextPhase = hpRatio < (definition.kind == EnemyKind.Hollow ? 0.3f : 0.33f) ? 3 :
                hpRatio < (definition.kind == EnemyKind.Hollow ? 0.6f : 0.66f) ? 2 : 1;
            if (nextPhase <= bossPhase) return;
            bossPhase = nextPhase;
            if (definition.kind != EnemyKind.Tyrant) return;
            // Royal shield: refilled at each phase change; while it holds, summons come twice as fast.
            var balance = GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
            statusBoard?.SetShield(maxHp * (balance ? balance.tyrantPhaseShieldShare : .15f));
            royalShield = true;
            var immunities = GM.waves.TyrantImmunities;
            if (Affix == ElementKind.None && immunities.Length > 0)
                SetAffix(immunities[UnityEngine.Random.Range(0, immunities.Length)]);
            // Cast once the boss is free: after a charge run, and after a back slash already queued.
            royalCastPending = true;
        }

        void TickSummonClock(float dt)
        {
            var b = Boss;
            float rate = bossPhase == 3 ? definition.kind == EnemyKind.Hollow ? b.prophetPhase3SummonRate : b.tyrantPhase3SummonRate : 1f;
            if (royalShield && statusBoard && statusBoard.Shield > 0f) rate *= b.royalShieldSummonRate;
            summonTimer -= dt * rate;
            if (summonTimer > 0f) return;
            if (definition.kind == EnemyKind.Hollow)
            {
                BeginProphetSummon();
                summonTimer = b.prophetSummonInterval;
            }
            else
            {
                GM.waves.SpawnTyrantSummons(bossPhase == 3 ? b.tyrantPhase3SummonCount : b.tyrantSummonCount, Position);
                summonTimer = b.tyrantSummonInterval;
            }
        }

        /// <summary>The Prophet's summons: red crosses on the points first (the wave's marker flow is paused during the trial), at most
        /// prophetSummonCap alive.</summary>
        void BeginProphetSummon()
        {
            if (summonPoints.Count > 0 || !GM || !GM.waves) return;
            int living = 0;
            foreach (var enemy in GM.waves.Alive)
                if (enemy && enemy != this && enemy.IsAlive && enemy.IsTrialSummon) living++;
            int count = Mathf.Min(Boss.prophetSummonCount, Boss.prophetSummonCap - living);
            if (count <= 0) return;
            summonKind = bossPhase == 3 ? EnemyKind.Skitter : EnemyKind.Grunt;
            var data = GM.waves.DefinitionOf(summonKind);
            if (!data) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = Query.Flat(Position) + Quaternion.Euler(0f, 360f * i / count, 0f) * Vector3.forward * Boss.prophetSummonRingRadius;
                summonPoints.Add(at);
                var marker = GM.waves.BorrowMarker();
                if (!marker) continue;
                marker.Begin(at, data, false, ElementKind.None);
                summonMarkers.Add(marker);
            }

            summonWarning = Boss.prophetSummonWarning;
        }

        void TickSummonWarning(float dt)
        {
            if (summonPoints.Count == 0) return;
            summonWarning -= dt;
            if (summonWarning > 0f) return;
            foreach (var marker in summonMarkers)
                if (marker)
                    marker.gameObject.SetActive(false);
            summonMarkers.Clear();
            GM.waves.SpawnExtrasAt(summonKind, summonPoints, true);
            V5WeaponAudio.At(V5Sfx.NashSummon, Position, .6f, 1.25f); // the crosses turn into bodies
            summonPoints.Clear();
        }

        /// <summary>Removes the Prophet's pending summon crosses and landing circles (trial end, respawn).</summary>
        public void ClearBossMarks()
        {
            foreach (var marker in summonMarkers)
                if (marker)
                    marker.gameObject.SetActive(false);
            summonMarkers.Clear();
            summonPoints.Clear();
            landingZones.Clear();
            ClearNashFight();
        }

        void TickPlatform(float dt)
        {
            var world = WorldMap.Active;
            if (!world || !world.TrialActive || !world.trialPlatform) return;
            world.trialRadius = Mathf.MoveTowards(world.trialRadius, PlatformGoal, dt * Boss.platformShrinkSpeed);
            world.trialPlatform.localScale = new Vector3(world.trialRadius * 2, 0.3f, world.trialRadius * 2);
        }

        void TickVolleys(float dt, PlayerController player)
        {
            if (volleyTimer > 0f)
            {
                volleyTimer -= dt;
                if (volleyTimer <= 0f) FirePredictiveVolley(player);
            }

            if (ringsLeft > 0)
            {
                ringTimer -= dt;
                if (ringTimer <= 0f) FireRing(player);
            }
        }

        /// <summary>Predictive shot: one at where the player stands, one at where they will be if they keep going; from phase 2 a
        /// third at where they would be if they turned back. Turning sharply when the glow ends dodges all of them.</summary>
        void FirePredictiveVolley(PlayerController player)
        {
            if (!player || !player.IsAlive) return;
            var b = Boss;
            Vector3 from = Query.Flat(Position);
            Vector3 now = Query.Flat(player.Position);
            var body = player.GetComponent<Rigidbody>();
            Vector3 velocity = body ? Query.Flat(body.linearVelocity) : Vector3.zero;
            float flight = Query.Flat(now - from).magnitude / b.shotSpeed;
            flight = Query.Flat(now + velocity * flight - from).magnitude / b.shotSpeed;
            float damage = attackDamage * b.shotDamage;
            Shoot(now, damage, b.shotSpeed);
            Shoot(now + velocity * flight, damage, b.shotSpeed);
            if (bossPhase >= 2) Shoot(now - velocity * flight, damage, b.shotSpeed);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSpit, Position);
        }

        /// <summary>Equilibrium ring: evenly spaced shots, the first aimed at the player; later rings turn by half (phase 2) or a third
        /// (phase 3) of the spacing to close the earlier gaps.</summary>
        void FireRing(PlayerController player)
        {
            var b = Boss;
            int count = Mathf.Max(1, bossPhase == 3 ? b.phase3RingShots : b.ringShots);
            float spacing = 360f / count;
            // Each later ring turns by the spacing over the ring count: a half in phase 2, a third in phase 3.
            float turn = bossPhase >= 2 ? spacing / Mathf.Max(1, bossPhase == 3 ? b.phase3Rings : b.phase2Rings) : 0f;
            if (ringIndex == 0)
            {
                Vector3 aim = player ? Query.Flat(player.Position - Position) : facing;
                if (aim.sqrMagnitude < 1e-4f) aim = facing;
                ringBaseAngle = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            }

            // Later rings turn from the first ring, so they close its gaps even if the player stepped into one.
            float baseAngle = ringBaseAngle + turn * ringIndex;
            float damage = attackDamage * b.ringDamage;
            for (int i = 0; i < count; i++)
            {
                float angle = (baseAngle + spacing * i) * Mathf.Deg2Rad;
                Shoot(Query.Flat(Position) + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 10f, damage, b.ringSpeed);
            }

            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSpit, Position);
            ringIndex++;
            ringsLeft--;
            ringTimer = b.ringInterval;
        }

        void Shoot(Vector3 point, float damage, float speed)
        {
            Vector3 direction = Query.Flat(point - Position);
            if (direction.sqrMagnitude < 1e-4f) direction = facing;
            direction.Normalize();
            GM.waves.LaunchBossBolt(this, Position + direction * (radius + .3f), direction, damage, 0f, speed);
        }

        /// <summary>Prophecy landing: one circle where the player will be when it fills, one between the player and the Prophet. Never
        /// within prophecyEdgeMargin of the edge and never on its own body.</summary>
        void PlaceLandingZones(PlayerController player)
        {
            var world = WorldMap.Active;
            if (!world || !world.TrialActive || !player || !player.IsAlive) return;
            var b = Boss;
            float zone = b.prophecyDiameter * .5f;
            var body = player.GetComponent<Rigidbody>();
            Vector3 velocity = body ? Query.Flat(body.linearVelocity) : Vector3.zero;
            AddLandingZone(Query.Flat(player.Position) + velocity * b.prophecyFill, zone, world);
            AddLandingZone(Vector3.Lerp(Query.Flat(Position), Query.Flat(player.Position), UnityEngine.Random.Range(b.prophecyBetweenMin, Mathf.Max(b.prophecyBetweenMin, b.prophecyBetweenMax))), zone,
                world);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position);
        }

        void AddLandingZone(Vector3 point, float zone, WorldMap world)
        {
            Vector3 centre = Query.Flat(world.trialPlatform.position);
            // Measured from the edge the platform is shrinking toward, so a circle placed early is not left near the final edge.
            float limit = Mathf.Max(0f, Mathf.Min(world.trialRadius, PlatformGoal) - Boss.prophecyEdgeMargin);
            Vector3 off = point - centre;
            if (off.magnitude > limit) point = centre + off.normalized * limit;
            Vector3 fromBody = point - Query.Flat(Position);
            float clear = radius + zone + .2f;
            if (fromBody.magnitude < clear) point = Query.Flat(Position) + (fromBody.sqrMagnitude > 1e-4f ? fromBody.normalized : -facing) * clear;
            landingZones.Add(new LandingZone { centre = point, radius = zone, fill = Boss.prophecyFill });
        }

        void TickLandingZones(float dt)
        {
            for (int i = landingZones.Count - 1; i >= 0; i--)
            {
                var zone = landingZones[i];
                zone.age += dt;
                if (zone.age < zone.fill)
                {
                    landingZones[i] = zone;
                    continue;
                }

                landingZones.RemoveAt(i);
                // No gravity ring: the hole only takes what walks or is pushed into it.
                if (WorldMap.Active) WorldMap.Active.CreateBossHole(zone.centre, zone.radius * 2f, Boss.prophecyHoleLife, this);
            }
        }

        /// <summary>Inhale contact (WorldMap): the player touching the Prophet is hit once per inhale; its summons are only pulled.</summary>
        public void InhaleContact(Combatant body)
        {
            if (!Inhaling || inhaleHit || body is not PlayerController player || !player.IsAlive) return;
            inhaleHit = DealHit(player, attackDamage * Boss.inhaleContactDamage, player.Position - Position, Boss.inhaleContactPush);
        }

        /// <summary>One of the Prophet's summons fell off the platform or into a hole: a fixed share of its max HP (not a player hit, so
        /// no crit and no stun bonus).</summary>
        public void SummonFell()
        {
            if (!IsAlive) return;
            ReceiveDamage(new DamageInfo { amount = maxHp * Boss.summonFallHpShare, source = DamageSource.Wall, kind = HitKind.Slam });
        }

        /// <summary>Support positioning (Warden, Chanter): flee a close player, otherwise stand five units behind the nearest non-support ally.</summary>
        void HoldBehindAllies(PlayerController player, float distance, float dt)
        {
            Enemy nearestGroup = null;
            float nearestDistance = float.MaxValue;
            foreach (var ally in GM.waves.Alive)
            {
                if (!ally || ally == this || !ally.IsAlive || !ally.definition ||
                    EnemyRoles.IsSupport(ally.definition.kind)) continue;
                float d = Query.Flat(ally.Position - Position).sqrMagnitude;
                if (d < nearestDistance)
                {
                    nearestDistance = d;
                    nearestGroup = ally;
                }
            }

            Vector3 supportPoint = nearestGroup
                ? nearestGroup.Position + Query.Flat(nearestGroup.Position - player.Position).normalized * 5f
                : player.Position;
            MoveTowards(distance < 6f ? player.Position : supportPoint,
                distance < 6f ? -1f : Query.Flat(supportPoint - Position).magnitude > 1f ? 1f : 0f, dt);
        }

        void MoveTowards(Vector3 point, float sign, float dt) => MoveAt(point, moveSpeed * sign, dt);

        /// <summary>Chases a point at this speed (negative backs away).</summary>
        void MoveAt(Vector3 point, float speed, float dt)
        {
            Vector3 direction = WorldMap.Active
                ? WorldMap.Active.GetChaseDirection(Position, point, FollowingIntoGravity)
                : Query.Flat(point - Position).normalized;
            Vector3 v = KeepOffEdges(direction * speed, dt);
            rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
        }

        /// <param name="scaled">Phase 3 bosses wind up faster (never under 0.8 s); follow-ups with their own fixed time pass false.</param>
        void BeginWindup(float time, float range, bool scaled = true)
        {
            if (scaled && IsBoss && bossPhase == 3)
                time = Mathf.Max(0.8f, time / (definition.kind == EnemyKind.Hollow ? 1.3f : 1.25f));
            windup = true;
            specialWindup = true;
            windupTimer = time;
            if (definition.kind == EnemyKind.Bloater)
                V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
            else if (definition.kind == EnemyKind.Hollow)
                V5WeaponAudio.At(V5Sfx.NashWindup, Position, 1f, NashWindupPitch(bossAttack));
            else if (definition.kind == EnemyKind.Rammer || definition.kind == EnemyKind.Tyrant && bossAttack == ChargeMove)
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChargeWarning, Position);
            else
                V5WeaponAudio.Windup(definition.kind, Position);
            SetLook(true);
            Stop();
            if (telegraph)
            {
                bool line = definition.kind == EnemyKind.Rammer ||
                            definition.kind == EnemyKind.Tyrant && bossAttack == ChargeMove;
                TelegraphRange = range;
                // The leap circle is drawn where it will land; every other warning starts at the body.
                Vector3 origin = definition.kind == EnemyKind.Tyrant && bossAttack == LeapMove ? leapTarget : Position;
                telegraph.position = origin + (line ? facing * range * .5f : Vector3.zero) + Vector3.up * .03f;
                telegraph.rotation = Quaternion.LookRotation(facing);
                telegraph.localScale =
                    line ? new Vector3(radius * 2f, 0.025f, range) : new Vector3(range * 2f, 0.025f, range * 2f);
                // Only the Spire and boss circle moves draw a warning circle (document 10.2); the Rammer and
                // Tyrant charge lines and the Tyrant cleave sector are not circles and stay. Every other wind-up shows only the yellow body
                // and its sound.
                telegraph.gameObject.SetActive(range > 0f &&
                                               (definition.kind == EnemyKind.Spire || line || CleaveTelegraph || CircleTelegraph));
            }

            if (definition.kind == EnemyKind.Shade)
            {
                revealTimer = 1.6f;
                V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShadeSpawn, Position);
            }
        }

        bool PerformSpecialAttack()
        {
            switch (definition.kind)
            {
                case EnemyKind.Spitter:
                    GM.waves.LaunchSpit(this, Position, facing);
                    return true;
                case EnemyKind.Rammer:
                    charging = true;
                    chargeLeft = definition.chargeDistance;
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChargeRush, Position);
                    return true;
                case EnemyKind.Bloater:
                    dyingExplosion = true;
                    Explode();
                    Kill();
                    dyingExplosion = false;
                    return true;
                case EnemyKind.Spire:
                    // The wind-up sends the light wave out (its sound is the old pulse's); the wave hurts nobody.
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcPulse, Position);
                    StartLightWave();
                    return true;
                case EnemyKind.Stomper:
                    V5WeaponAudio.At(V5Sfx.StomperSlam, Position);
                    ConeSlam(definition.pulseRadius, definition.coneAngle, attackDamage, attackKnockback);
                    return true;
                case EnemyKind.Hollow:
                    var prophet = Boss;
                    switch (bossAttack)
                    {
                        case PulseMove:
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
                            Fx.Ring(Position, PulseReach, definition.colour);
                            PlayerBlast(Position, PulseReach, attackDamage * prophet.pulseDamage, prophet.pulsePush);
                            break;
                        case InhaleMove:
                            inhaleLeft = prophet.inhaleSeconds;
                            inhaleHit = false;
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position);
                            break;
                        case DoubleShotMove:
                            FirePredictiveVolley(GM.player);
                            if (bossPhase == 3) volleyTimer = prophet.shotSecondVolleyDelay;
                            break;
                        case RingMove:
                            ringIndex = 0;
                            ringsLeft = bossPhase == 3 ? prophet.phase3Rings : bossPhase == 2 ? prophet.phase2Rings : 1;
                            FireRing(GM.player);
                            break;
                        default:
                            PlaceLandingZones(GM.player);
                            break;
                    }

                    return true;
                case EnemyKind.Tyrant:
                    var tyrant = Boss;
                    switch (bossAttack)
                    {
                        case ChargeMove:
                            charging = true;
                            chargeLeft = TyrantChargeDistance;
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position);
                            break;
                        case SlamMove:
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
                            Fx.Ring(Position, tyrant.slamRange, definition.colour);
                            PlayerBlast(Position, tyrant.slamRange, attackDamage * tyrant.slamDamage, tyrant.slamPush);
                            break;
                        case LeapMove:
                            BeginLeap();
                            break;
                        case RoyalShieldMove:
                            break;
                        default:
                            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCleave, Position);
                            float share = bossAttack == BackSlashMove ? tyrant.backSlashDamage : tyrant.cleaveDamage;
                            foreach (var target in Query.InSector(this, Position, facing, tyrant.cleaveRange, tyrant.cleaveArc))
                                if (target.Team != Team.Enemy) // never its own summons
                                    DealHit(target, attackDamage * share, target.Position - Position, tyrant.cleavePush);
                            // Phase 2 on: a player who got round the cut gets a second one, turned toward them.
                            if (bossAttack == CleaveMove && bossPhase >= 2 && OutsideCut(GM.player, tyrant)) followUp = BackSlashMove;
                            break;
                    }

                    return true;
            }

            return false;
        }

        /// <summary>Spire light wave: the front starts at the Spire's centre.</summary>
        void StartLightWave()
        {
            waveOut = true;
            waveCaught = false;
            waveRadius = 0f;
            waveCentre = Query.Flat(Position);
            LightWaveSequence++;
        }

        /// <summary>The front grows at waveSpeed with this enemy's own time step, so it stands still whenever the battlefield does (a
        /// level-up pick, a cutscene, the pause, the void trial's freeze), and stops at the sentry range. The first step in which the front
        /// sweeps over the living player's body with no wall between the Spire and them, the laser locks on them: at most once a wave. The
        /// wave itself hurts nobody, enemies or player.</summary>
        void TickLightWave(float dt, PlayerController player)
        {
            if (dt <= 0f) return;
            float reach = LightWaveReach;
            float before = waveRadius;
            waveRadius = Mathf.Min(reach, waveRadius + SpireWaveSpeed * dt);
            if (!waveCaught && FrontSweepsOver(player, before, waveRadius))
            {
                waveCaught = true;
                LightWaveCatches++;
                LockLaser(player);
            }

            if (waveRadius >= reach) waveOut = false;
        }

        /// <summary>The front moved from before to after this step and passed over some part of the player's body (its near edge within
        /// after, its far edge not yet behind before), and no wall stands between the Spire and the player: walls stop the light as they
        /// cut the laser. A player who keeps behind a wall until the front has gone past is never caught by that wave.</summary>
        bool FrontSweepsOver(PlayerController player, float before, float after)
        {
            if (!player || !player.IsAlive) return false;
            float distance = Query.Flat(player.Position - waveCentre).magnitude;
            if (distance - player.Radius > after || distance + player.Radius < before) return false;
            return !Query.WallBetween(Query.AtCastHeight(waveCentre), Query.AtCastHeight(player.Position), out _);
        }

        /// <summary>Spire: its light wave caught the living player; lock the direction to them, cut the laser at the first wall, and fire
        /// after laserDelay.</summary>
        void LockLaser(PlayerController player)
        {
            Vector3 to = Query.Flat(player.Position - Position);
            LaserDirection = to.sqrMagnitude > 1e-4f ? to.normalized : Query.Flat(facing).normalized;
            float length = SpireLaserLength;
            if (Query.WallBetween(Query.AtCastHeight(Position), Query.AtCastHeight(Position + LaserDirection * length),
                    out var wallHit))
                length = Query.Flat(wallHit.point - Position).magnitude;
            LaserLength = length;
            facing = LaserDirection;
            laserTimer = SpireLaserDelay;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcLaserLock, Position);
        }

        void FireLaser(PlayerController player)
        {
            laserTimer = 0f;
            LaserSequence++;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcLaserFire, Position);
            if (!player || !player.IsAlive) return;
            Vector3 to = Query.Flat(player.Position - Position);
            float along = Vector3.Dot(to, LaserDirection);
            float across = (to - LaserDirection * along).magnitude;
            if (along >= -player.Radius && along <= LaserLength + player.Radius &&
                across <= SpireLaserHalfWidth + player.Radius)
                DealHit(player, attackDamage, LaserDirection, 0f);
        }

        /// <summary>The player is within the cleave's reach but outside its arc (beside or behind the Tyrant).</summary>
        bool OutsideCut(PlayerController player, BossTuning b)
        {
            if (!player || !player.IsAlive) return false;
            Vector3 to = Query.Flat(player.Position - Position);
            float d = to.magnitude;
            return d - player.Radius <= b.cleaveRange && d > player.Radius && Vector3.Angle(facing, to) > b.cleaveArc * .5f;
        }

        void AreaAttack(float reach, float damage, float push)
        {
            // A Bloater bursts for real; other area attacks raise dust at their exact reach.
            if (V5Vfx.On)
            {
                Color? tint = Affix != ElementKind.None ? V5Vfx.ElementTint(Affix) : null;
                if (definition.kind == EnemyKind.Bloater) V5Vfx.I.Explosion(Position, reach, tint);
                else V5Vfx.I.GroundSlam(Position, reach, tint ?? definition.colour);
            }
            else Fx.Ring(Position, reach, definition.colour);
            foreach (var target in Query.Characters(Position, reach))
                if (target != this && target.IsAlive)
                    DealHit(target, damage, target.Position - Position, push);
        }

        /// <summary>Stomper slam: a small cone in front, along the facing locked toward the player when the wind-up began;
        /// everything inside it is hit and pushed by the heavy tier (§5.3), the player included.</summary>
        void ConeSlam(float reach, float arcDeg, float damage, float push)
        {
            Fx.Sector(Position, facing, reach, arcDeg, definition.colour);
            if (V5Vfx.On) V5Vfx.I.GroundSlam(Position + Query.Flat(facing).normalized * reach * .55f, reach * .5f, Affix != ElementKind.None ? V5Vfx.ElementTint(Affix) : definition.colour, false);
            foreach (var target in Query.InSector(this, Position, facing, reach, arcDeg))
                if (target.IsAlive) DealHit(target, damage, target.Position - Position, push);
        }

        /// <summary>Bloater blast (contact or death): area hit, then a radiation zone on the spot.</summary>
        void Explode()
        {
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcBloaterBlast, Position);
            // §5.3: the medium tier; DealHit keeps it off the player.
            AreaAttack(definition ? definition.pulseRadius : 3f, attackDamage, KnockbackRules.Active.Distance(KnockbackRules.AttackTier(EnemyKind.Bloater)));
            var waves = GM ? GM.waves : null;
            AddRadiationZone(Position, waves ? waves.radiationRadius : 3f, waves ? waves.radiationLife : 4f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcRadiation, Position);
        }

        /// <summary>Target is inside the punch arc: edge distance within attackRange and within attackArc/2 of the locked direction (angle ignored when touching).</summary>
        bool InFist(Combatant c)
        {
            Vector3 to = Query.Flat(c.Position - Position);
            float d = to.magnitude;
            if (d - radius - c.Radius > attackRange) return false;
            return d <= radius + c.Radius * 0.5f || Vector3.Angle(facing, to) <= attackArc * 0.5f;
        }

        /// <summary>Document 9.4: this kind runs fast enough to follow what the enemies chase (the player, or a Mirror Image) into a gravity
        /// ring; slower kinds wait at the ring's edge. The kind's authored speed is compared with gravityFollowSpeed, not moveSpeed: the
        /// scene's global movement multiplier (0.5) and the wave's speed growth would leave every kind below it.</summary>
        public bool FollowsIntoGravity => definition && definition.speed >= gravityFollowSpeed;

        /// <summary>A fast kind (FollowsIntoGravity) while what the enemies chase stands in a gravity ring: its own walking may then cross
        /// into rings (GetChaseDirection, KeepOffEdges). Otherwise every enemy walks round the rings and never into one.</summary>
        bool FollowingIntoGravity => FollowsIntoGravity && WorldMap.Active &&
                                     WorldMap.Active.GetPull(WorldMap.Active.EnemyTarget).sqrMagnitude > 0.01f;

        /// <summary>
        /// When walking on its own it never walks off into a gap; it only falls when knocked back or thrown.
        /// If the next step would leave the platform (less than a body from the edge), it first tries moving along x only or z only (sliding along the edge), and stops if neither works.
        /// Dunes quicksand slows this own walking first (system doc 12.1); bosses ignore it as they ignore black-hole pull.
        /// </summary>
        Vector3 KeepOffEdges(Vector3 v, float dt)
        {
            if (!WorldMap.Active) return v;
            if (WorldMap.Active.TrialActive) return WorldMap.Active.KeepOnTrialGround(Position, v, dt, radius); // never off into the void
            if (!IsBoss) v *= Quicksand.SpeedFactor(Position);
            return WorldMap.Active.IsWalkable(Position + v * dt, radius, FollowingIntoGravity) ? v : Vector3.zero;
        }

        /// <summary>Shatter ice: a slide never carries this enemy into a black hole or a gravity ring it would not walk into,
        /// nor off the platform: the same check as its own walking (KeepOffEdges, whose quicksand slow is already in the walk).</summary>
        protected override Vector3 GuardSlide(Vector3 v, float dt)
        {
            if (!WorldMap.Active) return v;
            if (WorldMap.Active.TrialActive) return WorldMap.Active.KeepOnTrialGround(Position, v, dt, radius);
            return WorldMap.Active.IsWalkable(Position + v * dt, radius, FollowingIntoGravity) ? v : Vector3.zero;
        }

        /// <summary>Shatter ice: a Rammer's charge and a Bloater's rush are attack moves with a set reach, not walks, so they keep their full
        /// speed and reach on ice and stop as they do elsewhere (only the walk slips).</summary>
        protected override bool SlipsOnIce => !charging && !rushing;

        void Stop()
        {
            if (!rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        }

        void CancelWindup()
        {
            if (!windup) return;
            windup = false;
            raidWindup = false;
            specialWindup = false;
            SetLook(false);
            if (telegraph && !leaping) telegraph.gameObject.SetActive(false); // the leap keeps its landing circle until it lands
        }

        void SetLook(bool yellow)
        {
            if (!bodyRenderer) return;
            if (windupMaterial) bodyRenderer.sharedMaterial = yellow ? windupMaterial : normalMaterial;
            RefreshBodyTint();
        }

        /// <summary>Whitebox body colour while winding up: every enemy, melee included, turns yellow until it strikes.
        /// A Bloater is yellow from the start of its rush, as its illustration is (document 9.2).</summary>
        public static readonly Color WindupBodyColour = new Color(1f, .85f, .25f);
        static readonly Color ShadeCloakColour = new Color(0.38f, 0.45f, 0.7f, 0.22f);

        protected override Color? BodyTint => !definition ? null
            : windup || rushing ? WindupBodyColour
            : definition.kind == EnemyKind.Shade && Cloaked ? ShadeCloakColour
            : definition.colour;

        protected override void OnStateChanged(CharacterState from, CharacterState to)
        {
            if (to != CharacterState.Normal)
            {
                CancelWindup();
                laserTimer = 0f;
                waveOut = false; // death ends a Spire's light wave; nothing stays on the ground
            }
        }

        [Header("可吸入黑洞的尸体")] public float corpseLifetime = 8f;
        public float corpseDamping = 3f;
        public bool CorpseActive { get; private set; }
        public bool CorpseCarried { get; private set; }
        public bool CorpseFeedConsumed { get; private set; }
        float corpseSeconds;
        Vector3 corpseVelocity;

        void BeginCorpse()
        {
            if (!GM || !pooled || IsBoss || DeathCause != DeathCause.HpDepleted)
            {
                if (pooled) gameObject.SetActive(false);
                else Destroy(gameObject);
                return;
            }

            CorpseActive = true;
            CorpseCarried = false;
            corpseSeconds = corpseLifetime;
            corpseVelocity = Vector3.ClampMagnitude(DeathVelocity, 12f);
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = false;
            gameObject.SetActive(true);
        }

        public bool TryClaimCorpse(int version)
        {
            if (version != SpawnVersion || !CorpseActive || CorpseCarried || CorpseFeedConsumed) return false;
            CorpseCarried = true;
            gameObject.SetActive(false);
            return true;
        }

        public void DropCorpse(int version, Vector3 at)
        {
            if (version != SpawnVersion || !CorpseActive || !CorpseCarried || CorpseFeedConsumed) return;
            CorpseCarried = false;
            transform.position = new Vector3(at.x, 0, at.z);
            rb.position = transform.position;
            corpseVelocity = Vector3.zero;
            gameObject.SetActive(true);
        }

        /// <summary>Closed Blade: a blade tip knocks a lying corpse, which slides distance cells more along dir.</summary>
        public void KnockCorpse(Vector3 dir, float distance)
        {
            if (!CorpseActive || CorpseCarried || CorpseFeedConsumed) return;
            dir = Query.Flat(dir);
            if (dir.sqrMagnitude < 1e-6f || distance <= 0f) return;
            // The slide decays by corpseDamping, so a speed of distance x damping covers exactly distance.
            corpseVelocity += dir.normalized * (distance * corpseDamping);
        }

        public bool TryConsumeCorpseFeed(int version)
        {
            if (version != SpawnVersion || !CorpseActive || CorpseFeedConsumed) return false;
            CorpseFeedConsumed = true;
            DespawnCorpse(version);
            return true;
        }

        public void DespawnCorpse(int version)
        {
            if (version != SpawnVersion || !CorpseActive) return;
            CorpseActive = CorpseCarried = false;
            gameObject.SetActive(false);
        }

        public void TickCorpse(float dt, Vector3 pull)
        {
            if (!CorpseActive || CorpseCarried || dt <= 0) return;
            corpseSeconds -= dt;
            if (corpseSeconds <= 0)
            {
                DespawnCorpse(SpawnVersion);
                return;
            }

            corpseVelocity *= Mathf.Exp(-corpseDamping * dt);
            Vector3 next = Position + (corpseVelocity + pull) * dt;
            var world = WorldMap.Active;
            if (world && !world.TrialActive)
            {
                next = world.ClampPoint(next, radius);
                if (world.IsWall(next, radius * .5f)) next = Position;
            }

            transform.position = new Vector3(next.x, 0, next.z);
            rb.position = transform.position;
        }

        protected override void OnDied()
        {
            if (definition && (IsBoss || !SuppressRewards || BossSummon)) // a Tyrant's summon has no reward but dies as its kind (doc 10.2)
            {
                if (definition.kind == EnemyKind.Bloater && !dyingExplosion)
                {
                    dyingExplosion = true;
                    explosionTimer = 0.4f;
                    V2Audio.Instance?.PlayWorld(ArenaSoundCue.BloaterWarning, Position);
                    return;
                }

                if (definition.kind == EnemyKind.Hollow) WorldMap.Active.CompleteTrial();
                if (definition.kind == EnemyKind.Tyrant) GM.NotifyFinalBossDefeated();
            }

            // §10.2: a full Splitter killed by HP leaves minis (swallowed bodies take the fall path and never reach here).
            if (definition && definition.splitsOnDeath && !Mini && !IsTrialSummon &&
                DeathCause == DeathCause.HpDepleted && GM && GM.waves)
                GM.waves.QueueSplit(this);
            if (V5Vfx.On)
            {
                if (IsBoss && definition.kind == EnemyKind.Hollow) V5Vfx.I.VoidExplosion(Position, 8f);
                else if (IsBoss) V5Vfx.I.Explosion(Position, 7f);
                else V5Vfx.I.Death(Position + Vector3.up * radius, radius, definition ? definition.colour : Color.red);
            }
            else Fx.Pop(Position + Vector3.up * radius, bodyRenderer ? bodyRenderer.sharedMaterial.color : Color.red,
                radius * 3f);
            BeginCorpse();
        }

        protected override void OnDamaged(float amount) => revealTimer = 2f;

        protected override void OnFallFinished()
        {
            if (pooled) gameObject.SetActive(false);
            else Destroy(gameObject);
        }
    }
}
