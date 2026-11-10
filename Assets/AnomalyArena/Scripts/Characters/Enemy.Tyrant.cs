using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The Tyrant, the final boss (system doc 13.2): a thick single target that starts with a shield and picks its move by
    /// your distance alone. Close it sweeps its tail across a fan from left to right, breaking the walls the tail touches, and from phase 2 it
    /// may pound a shockwave down a rectangle instead; at middle distance it runs down a line and leaps into a slam that throws everything
    /// around, its own minions included, and from phase 2 it may raise its scepter for a crowd like wave 14's instead; far away it throws a
    /// meteor whose fragments stand as walls for a while, and from phase 2 it may devour the minions around it for HP and shield instead.
    /// Nothing stops a run: its body breaks every wall, fragment and bulwark it runs into, knocks the enemies in its way
    /// aside and runs on over holes, and it is never stunned. Two phases by HP: phase 2 refills its shield, grows the
    /// sweep, the charge and the meteor, and enrages it (shorter wind-ups and walks between moves, so its moves come much more often, and
    /// harder hits). It always ignores Frost and Lightning, is never
    /// knocked back, pulled or swallowed, and none of its moves cares about black holes. Enemy.cs only calls in here; the numbers live in
    /// BossTuning's Tyrant blocks.
    /// </summary>
    public partial class Enemy
    {
        // Tyrant moves (BossAttackIndex); the numbers stay clear of its replaced moves (CleaveMove .. RoyalShieldMove).
        public const int TailSweepMove = 10, GroundPoundMove = 11, RampageChargeMove = 12, RoyalScepterMove = 13, StarThrowMove = 14,
            AstralGuardMove = 15;

        /// <summary>The Tyrant fights in two phases, so its shield is refilled once a fight (EncounterBalance budgets it).</summary>
        public const int TyrantPhaseCount = 2;

        // Spacing of the probes along the charge line looking for ground to land on, cells (geometry sampling, not tuning).
        const float RushProbeStep = .5f;

        static readonly Color MeteorColour = new(1f, .6f, .25f), AstralColour = new(.6f, .75f, 1f);

        enum TyrantStage { Idle, Windup, Sweep, Run, Leap, Recover }

        TyrantStage tyrantStage;
        float tyrantRecover, scepterWait;

        // Tail Sweep: the tail's angle from the locked facing, from -arc/2 (its left) to +arc/2 (its right).
        float sweepClock, sweepAngle, sweepReach;
        bool sweepLanded;

        // Ground Pound: the rectangle is locked at the wind-up; its shockwave runs on after the strike while the Tyrant walks again.
        Vector3 poundOrigin, poundFacing;
        float poundLength, poundClock;
        int poundStep = -1;
        bool poundLanded;

        // Rampage Charge: the run, then the leap from its end to the landing point drawn at the wind-up, and the slam there.
        Vector3 rushFrom, rushLanding;
        float rushRun, rushRunLeft, rushLeapClock, rushSlamReach;
        bool rushContact;

        // Star Throw: the meteor flies on after the throw.
        Vector3 meteorFrom, meteorTarget;
        float meteorClock, meteorReach, meteorDamage;
        int meteorPieces;
        bool meteorFlying;

        bool IsTyrant => definition && definition.kind == EnemyKind.Tyrant;

        /// <summary>Called in by the Tyrant's Royal Scepter (a red cross with WaveSpawnMarker.bossSummon): it gives no reward
        /// (SuppressRewards) but still dies as its kind, so a Bloater shot dead still blows up and leaves its radiation (document 10.2).</summary>
        public bool BossSummon { get; set; }

        /// <summary>The Tail Sweep's tail is crossing its fan (after the wind-up).</summary>
        public bool TailSweeping => IsTyrant && tyrantStage == TyrantStage.Sweep;

        /// <summary>Degrees of the tail from the locked facing (TelegraphFacing): -arc/2 is the fan's left edge, +arc/2 its right.</summary>
        public float TailAngle => sweepAngle;

        public Vector3 TailDirection => Quaternion.Euler(0f, sweepAngle, 0f) * facing;
        public float TailReach => sweepReach;
        public float TailHalfWidth => Mathf.Max(0f, Boss.sweepTailHalfWidth);
        public float SweepArc => Mathf.Clamp(Boss.sweepArc, 10f, 360f);

        /// <summary>The Ground Pound's rectangle shows: while it winds up and while its shockwave crosses it.</summary>
        public bool PoundShown => IsTyrant && (windup && bossAttack == GroundPoundMove || poundStep >= 0);

        /// <summary>The band of the rectangle the shockwave has reached (0 at the Tyrant's feet), or -1 while no shockwave runs.</summary>
        public int PoundStep => poundStep;

        public int PoundSteps => Mathf.Max(1, Boss.poundSteps);

        /// <summary>Middle of the rectangle's near side (the Tyrant's front edge at the wind-up); the rectangle runs PoundLength along
        /// PoundFacing and is PoundWidth across.</summary>
        public Vector3 PoundOrigin => poundOrigin;

        public Vector3 PoundFacing => poundFacing;
        public float PoundLength => poundLength;
        public float PoundWidth => Mathf.Max(0f, Boss.poundWidth);

        /// <summary>The Rampage Charge's landing circle shows from the wind-up through the run and the leap: nothing on the way stops the run,
        /// so every charge ends in its leap.</summary>
        public bool RushLandingShown => IsTyrant && bossAttack == RampageChargeMove &&
                                        (windup || tyrantStage is TyrantStage.Run or TyrantStage.Leap);

        public Vector3 RushLanding => rushLanding;
        public float RushSlamReach => rushSlamReach;
        public bool RushLeaping => IsTyrant && tyrantStage == TyrantStage.Leap;

        /// <summary>A Star Throw meteor is in the air; its impact circle shows at MeteorTarget.</summary>
        public bool MeteorFlying => IsTyrant && meteorFlying;

        public Vector3 MeteorTarget => meteorTarget;
        public float MeteorReach => meteorReach;

        /// <summary>0 at the throw, 1 at the impact.</summary>
        public float MeteorFill01 => meteorFlying ? Mathf.Clamp01(meteorClock / Mathf.Max(.05f, Boss.meteorFlight)) : 0f;

        public Vector3 MeteorPosition
        {
            get
            {
                float t = MeteorFill01;
                return Vector3.Lerp(meteorFrom, meteorTarget, t) + Vector3.up * (Mathf.Max(0f, Boss.meteorHeight) * 4f * t * (1f - t));
            }
        }

        /// <summary>The Astral Guard is channelling; it will devour the minions within AstralReach.</summary>
        public bool AstralChanneling => IsTyrant && windup && bossAttack == AstralGuardMove;

        public float AstralReach => Mathf.Max(0f, Boss.astralRadius);

        // ───── Move choice (no scene) ─────

        /// <summary>Tyrant move by your distance alone (system doc 13.2), whether or not it is on screen. Close, within the Tail Sweep's
        /// reach, it sweeps (from phase 2 it may pound instead); at middle distance, within its longest charge run plus the leap, it charges
        /// (from phase 2 it may raise the scepter instead while that is ready); far it throws a meteor (from phase 2 it may devour its minions
        /// instead when enough stand near it). roll is uniform in [0, 1).</summary>
        public static int TyrantMoveChoice(int phase, float distance, float roll, bool scepterReady, bool astralReady, BossTuning b)
        {
            bool enraged = phase >= TyrantPhaseCount;
            if (distance <= TyrantCloseReach(phase, b))
                return enraged && roll < b.poundShare ? GroundPoundMove : TailSweepMove;
            if (distance <= TyrantMiddleReach(phase, b))
                return enraged && scepterReady && roll < b.scepterShare ? RoyalScepterMove : RampageChargeMove;
            return enraged && astralReady && roll < b.astralShare ? AstralGuardMove : StarThrowMove;
        }

        /// <summary>The close band ends at the Tail Sweep's reach of that phase.</summary>
        public static float TyrantCloseReach(int phase, BossTuning b) => phase >= TyrantPhaseCount ? b.sweepRangePhase2 : b.sweepRange;

        /// <summary>The middle band ends where the longest charge run of that phase plus the leap lands.</summary>
        public static float TyrantMiddleReach(int phase, BossTuning b) =>
            (phase >= TyrantPhaseCount ? b.rushMaxRunPhase2 : b.rushMaxRun) + b.rushLeapDistance;

        // ───── Numbers ─────

        /// <summary>A hit of this share of its attack damage, raised while enraged (phase 2).</summary>
        float TyrantHit(float share) =>
            attackDamage * share * (bossPhase >= TyrantPhaseCount ? Mathf.Max(1f, Boss.tyrantEnragedDamage) : 1f);

        /// <summary>Seconds it walks between two moves; enraged (phase 2) the gap is shorter.</summary>
        float TyrantGap => Mathf.Max(0f, Boss.tyrantMoveGap) *
                           (bossPhase >= TyrantPhaseCount ? Mathf.Clamp(Boss.tyrantEnragedGapShare, .1f, 1f) : 1f);

        /// <summary>A wind-up of a move it has in both phases, or its pause after a charge's landing: shorter while enraged (phase 2), so
        /// with the shorter walk between moves its moves come much more often (system doc 13.2: its attack rate rises a lot).</summary>
        float EnragedTime(float seconds) => Mathf.Max(0f, seconds) *
                                            (bossPhase >= TyrantPhaseCount ? Mathf.Clamp(Boss.tyrantEnragedWindupShare, .1f, 1f) : 1f);

        static float TierPush(KnockTier tier) => KnockbackRules.Active.Distance(tier);

        // ───── Turn ─────

        /// <summary>Called by Configure for every kind, so a body configured away from the Tyrant keeps none of its state, and a pooled body
        /// spawned again is no longer one of its summons until a red cross says so.</summary>
        void ConfigureTyrant(EnemyDefinition data)
        {
            BossSummon = false;
            tyrantStage = TyrantStage.Idle;
            tyrantRecover = scepterWait = 0f;
            sweepClock = sweepAngle = sweepReach = 0f;
            sweepLanded = false;
            poundStep = -1;
            poundClock = poundLength = 0f;
            poundLanded = false;
            rushRun = rushRunLeft = rushLeapClock = rushSlamReach = 0f;
            rushContact = false;
            RunThroughBodies(false);
            meteorFlying = false;
            meteorClock = meteorReach = meteorDamage = 0f;
            meteorPieces = 0;
            if (!data || data.kind != EnemyKind.Tyrant) return;
            // §7.6, §13.2: like the Nash Prophet it always ignores Frost and Lightning, damage and statuses alike; nothing is drawn at random.
            if (statusBoard != null)
                foreach (var element in ElementImmunity.Boss)
                    statusBoard.SetImmunity(element, 1f);
            if (!GetComponent<V2TyrantGroundMarks>()) gameObject.AddComponent<V2TyrantGroundMarks>();
            // A new fight starts on the whole map: the walls its last fight broke stand again and its fragments are gone (the enemy lab).
            if (WorldMap.Active && WorldMap.Active.TryGetComponent(out TyrantTerrain terrain)) terrain.ResetTerrain();
        }

        /// <summary>The Tyrant's whole turn (system doc 13.2), called by TickSpecial in place of the replaced boss code.</summary>
        bool TickTyrant(float dt, PlayerController player)
        {
            var b = Boss;
            // The phase follows its HP at once, in the air too, so the phase-2 shield is never late.
            TickTyrantPhase();
            // What is already on the ground or in the air finishes whatever the Tyrant does meanwhile.
            TickPoundWave(dt, player);
            TickMeteor(dt, player);
            if (scepterWait > 0f) scepterWait -= dt;
            if (tyrantStage == TyrantStage.Leap)
            {
                TickRushLeap(dt);
                return true;
            }

            switch (tyrantStage)
            {
                case TyrantStage.Windup:
                    // Cancelled from outside (a blocked action or a state change ran CancelWindup): the move is dropped.
                    if (!windup)
                    {
                        EndTyrantMove();
                        return true;
                    }

                    Stop();
                    windupTimer -= dt;
                    if (windupTimer <= 0f)
                    {
                        CancelWindup();
                        StrikeTyrantMove(player);
                    }

                    return true;
                case TyrantStage.Sweep:
                    TickSweep(dt, player);
                    return true;
                case TyrantStage.Run:
                    TickRushRun(dt, player);
                    return true;
                case TyrantStage.Recover:
                    Stop();
                    tyrantRecover -= dt;
                    if (tyrantRecover <= 0f) EndTyrantMove();
                    return true;
            }

            // Void Walk: it cannot see the player, so it turns to where they vanished and starts no move.
            bool hidden = WorldMap.Active && WorldMap.Active.PlayerHidden;
            Vector3 to = Query.Flat((hidden ? WorldMap.Active.EnemyTarget : player.Position) - Position);
            float distance = to.magnitude;
            if (distance > 0.01f) facing = to / distance;
            if (cooldown <= 0f && !hidden)
            {
                StartTyrantMove(ChooseTyrantMove(distance), player);
                return true;
            }

            // Between moves it walks at you (at a Mirror Image while one stands, like every enemy) and stops once close.
            Vector3 target = WorldMap.Active ? WorldMap.Active.EnemyTarget : player.Position;
            if (Query.Flat(target - Position).magnitude > b.tyrantWalkStop) MoveTowards(target, 1f, dt);
            else Stop();
            return true;
        }

        int ChooseTyrantMove(float distance)
        {
            int move = forcedMove >= 0
                ? forcedMove
                : TyrantMoveChoice(bossPhase, distance, UnityEngine.Random.value, ScepterReady, AstralReady, Boss);
            forcedMove = -1;
            return move;
        }

        void StartTyrantMove(int move, PlayerController player)
        {
            var b = Boss;
            FacePlayer(player);
            bossAttack = move;
            tyrantStage = TyrantStage.Windup;
            // Enraged, the moves it has in both phases wind up faster (EnragedTime); the phase-2 moves keep their own wind-ups.
            switch (move)
            {
                case TailSweepMove:
                    sweepReach = TyrantCloseReach(bossPhase, b);
                    BeginTyrantWindup(EnragedTime(b.sweepWindup), sweepReach);
                    break;
                case GroundPoundMove:
                    PlanPound();
                    BeginTyrantWindup(b.poundWindup, poundLength);
                    break;
                case RampageChargeMove:
                    BeginTyrantWindup(EnragedTime(b.rushWindup), PlanRush(player));
                    break;
                case RoyalScepterMove:
                    BeginTyrantWindup(b.scepterWindup, 0f);
                    break;
                case AstralGuardMove:
                    BeginTyrantWindup(b.astralChannel, 0f);
                    break;
                default:
                    bossAttack = StarThrowMove;
                    BeginTyrantWindup(EnragedTime(b.meteorWindup), 0f);
                    break;
            }
        }

        /// <summary>Wind-up of a Tyrant move: the body turns yellow with its sound. The Rampage Charge's line and the Tail Sweep's fan show
        /// on the saved warning (LineTelegraph, CleaveTelegraph); V2TyrantGroundMarks draws the other shapes.</summary>
        void BeginTyrantWindup(float time, float range)
        {
            windup = true;
            specialWindup = true;
            windupTimer = Mathf.Max(0f, time);
            TyrantWindupSound(windupTimer);
            SetLook(true);
            Stop();
            TelegraphRange = range;
            if (!telegraph) return;
            bool line = bossAttack == RampageChargeMove, fan = bossAttack == TailSweepMove;
            telegraph.position = Position + (line ? facing * range * .5f : Vector3.zero) + Vector3.up * .03f;
            telegraph.rotation = Quaternion.LookRotation(facing);
            telegraph.localScale = line ? new Vector3(radius * 2f, 0.025f, range) : new Vector3(range * 2f, 0.025f, range * 2f);
            telegraph.gameObject.SetActive(range > 0f && (line || fan));
        }

        /// <summary>Each move warns with its own sound, the ones that build to a moment timed to end on it (the warning must fit the move).</summary>
        void TyrantWindupSound(float time)
        {
            switch (bossAttack)
            {
                case TailSweepMove: V5WeaponAudio.At(V5Sfx.TyrantGrowl, Position); break;
                case GroundPoundMove: V5WeaponAudio.At(V5Sfx.TyrantGrowl, Position, 1f, .82f); break;
                case RampageChargeMove: V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChargeWarning, Position, .78f, 1.1f); break;
                case RoyalScepterMove: V5WeaponAudio.AtFit(V5Sfx.ScepterRaise, Position, time); break;
                case AstralGuardMove: V5WeaponAudio.AtFit(V5Sfx.EnergyRise, Position, time); break;
                default: V5WeaponAudio.AtFit(V5Sfx.EnergyRise, Position, time, .85f); break;
            }
        }

        void StrikeTyrantMove(PlayerController player)
        {
            AttackSequence++;
            switch (bossAttack)
            {
                case TailSweepMove:
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCleave, Position);
                    V5WeaponAudio.AtFit(V5Sfx.TyrantSweep, Position, Boss.sweepSeconds);
                    tyrantStage = TyrantStage.Sweep;
                    sweepClock = 0f;
                    sweepAngle = -SweepArc * .5f;
                    sweepLanded = false;
                    break;
                case GroundPoundMove:
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
                    poundStep = 0;
                    poundClock = 0f;
                    poundLanded = false;
                    StrikePoundBand(player);
                    EndTyrantMove();
                    break;
                case RampageChargeMove:
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position);
                    charging = true;
                    rushContact = false;
                    rushRunLeft = rushRun;
                    tyrantStage = TyrantStage.Run;
                    RunThroughBodies(true);
                    break;
                case RoyalScepterMove:
                    RaiseScepter();
                    EndTyrantMove();
                    break;
                case AstralGuardMove:
                    DevourMinions();
                    EndTyrantMove();
                    break;
                default:
                    ThrowMeteor(player);
                    EndTyrantMove();
                    break;
            }
        }

        /// <summary>Back to walking; the next move waits for the gap (shorter while enraged).</summary>
        void EndTyrantMove()
        {
            tyrantStage = TyrantStage.Idle;
            charging = false;
            RunThroughBodies(false); // a run ended from outside
            cooldown = Mathf.Max(cooldown, TyrantGap);
        }

        /// <summary>§13.2: below tyrantPhase2At of its max HP it enters phase 2, once, and its shield is refilled.</summary>
        void TickTyrantPhase()
        {
            if (bossPhase >= TyrantPhaseCount || maxHp <= 0f || Hp >= maxHp * Boss.tyrantPhase2At) return;
            bossPhase = TyrantPhaseCount;
            var balance = GM && GM.waves && GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;
            // The refill is counted in its HP budget (EncounterBalance.ShieldBudgetShare); without a budget it gets its opening shield back.
            // The V2 day campaign budgets its own refill (Enemy.CampaignBudget.cs, since 2026-11-10).
            float share = budgetPhaseShield >= 0f ? budgetPhaseShield : balance ? balance.tyrantPhaseShieldShare : definition.startShieldShare;
            if (statusBoard) statusBoard.SetShield(Mathf.Max(statusBoard.Shield, maxHp * share));
        }

        // ───── Tail Sweep ─────

        void TickSweep(float dt, PlayerController player)
        {
            var b = Boss;
            Stop();
            float half = SweepArc * .5f, before = sweepAngle;
            sweepClock += dt;
            float t = b.sweepSeconds > 0f ? Mathf.Clamp01(sweepClock / b.sweepSeconds) : 1f;
            sweepAngle = Mathf.Lerp(-half, half, t);
            // Walls first, so a map wall the tail breaks this step no longer stands between it and you.
            var world = WorldMap.Active;
            if (world)
            {
                int broken = TyrantTerrain.For(world).BreakSwept(Position, Quaternion.Euler(0f, before, 0f) * facing, TailDirection,
                    sweepReach, TailHalfWidth, b.sweepWallPiece);
                if (broken > 0) V2Audio.Instance?.PlayWorld(ArenaSoundCue.Impact, Position + TailDirection * (sweepReach * .5f));
            }

            if (!sweepLanded) SweepHit(player, before, sweepAngle);
            if (t >= 1f) EndTyrantMove();
        }

        /// <summary>The tail turned from one angle to the next this step: you are hit once if any part of your body lay in between, within
        /// its reach. A bulwark between you and the Tyrant shields you: the tail cannot break it (document 8.4), only a Rampage Charge
        /// can.</summary>
        void SweepHit(PlayerController player, float from, float to)
        {
            if (!player || !player.IsAlive) return;
            var b = Boss;
            Vector3 offset = Query.Flat(player.Position - Position);
            float d = offset.magnitude, body = player.Radius + TailHalfWidth;
            if (d - player.Radius > sweepReach) return;
            if (d > body)
            {
                float angle = Vector3.SignedAngle(facing, offset, Vector3.up), spread = Mathf.Asin(body / d) * Mathf.Rad2Deg;
                if (angle + spread < from || angle - spread > to) return;
            }

            if (BulwarkBetween(Position, player.Position)) return;
            Vector3 radial = d > 0.01f ? offset / d : facing;
            // The tail turns clockwise seen from above (left to right): you are thrown along the swing and away from the Tyrant.
            Vector3 swing = Quaternion.Euler(0f, 90f, 0f) * radial;
            sweepLanded = DealHit(player, TyrantHit(b.sweepDamage), radial + swing, TierPush(b.sweepKnock));
        }

        static bool BulwarkBetween(Vector3 from, Vector3 to) =>
            Query.WallBetween(Query.AtCastHeight(from), Query.AtCastHeight(to), out var hit) && hit.collider &&
            hit.collider.GetComponentInParent<TemporaryWall>();

        // ───── Ground Pound ─────

        /// <summary>The rectangle runs from the Tyrant's front edge along its facing; the first wall ahead cuts it short.</summary>
        void PlanPound()
        {
            poundStep = -1;
            poundFacing = facing;
            poundOrigin = Query.Flat(Position) + facing * radius;
            poundLength = Mathf.Max(0f, Boss.poundLength);
            if (Query.WallBetween(Query.AtCastHeight(poundOrigin), Query.AtCastHeight(poundOrigin + facing * poundLength), out var hit))
                poundLength = Query.Flat(hit.point - poundOrigin).magnitude;
        }

        void TickPoundWave(float dt, PlayerController player)
        {
            if (poundStep < 0) return;
            float step = Mathf.Max(.01f, Boss.poundStepSeconds);
            poundClock += dt;
            while (poundStep >= 0 && poundClock >= step)
            {
                poundClock -= step;
                if (++poundStep >= PoundSteps)
                {
                    poundStep = -1;
                    return;
                }

                StrikePoundBand(player);
            }
        }

        /// <summary>The shockwave reaches band poundStep of the rectangle: a wall standing across its way since the wind-up (a bulwark,
        /// a fragment) stops it; you are hit once per pound, pushed along it. Its own minions are spared.</summary>
        void StrikePoundBand(PlayerController player)
        {
            var b = Boss;
            float band = poundLength / PoundSteps, near = band * poundStep, far = near + band;
            Vector3 centre = poundOrigin + poundFacing * ((near + far) * .5f);
            if (poundStep > 0 && Query.WallBetween(Query.AtCastHeight(poundOrigin + poundFacing * (near - band * .5f)),
                    Query.AtCastHeight(centre), out _))
            {
                poundStep = -1;
                return;
            }

            if (V5Vfx.On) V5Vfx.I.GroundSlam(centre, Mathf.Max(band, PoundWidth) * .5f, definition.colour, false);
            else Fx.Ring(centre, Mathf.Max(band, PoundWidth) * .5f, definition.colour);
            // The pound itself is the slam; each band after it is heard as it reaches on, a little less each time.
            if (poundStep > 0) V5WeaponAudio.At(V5Sfx.PoundBand, centre, Mathf.Max(.45f, 1f - .08f * poundStep), 1f - .03f * poundStep);
            if (poundLanded || !player || !player.IsAlive) return;
            Vector3 offset = Query.Flat(player.Position - poundOrigin);
            float along = Vector3.Dot(offset, poundFacing), across = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, poundFacing)));
            if (along < near - player.Radius || along > far + player.Radius || across > PoundWidth * .5f + player.Radius) return;
            poundLanded = DealHit(player, TyrantHit(b.poundDamage), poundFacing, TierPush(b.poundKnock));
        }

        // ───── Rampage Charge ─────

        /// <summary>Plans the run toward where you stand now: it runs until one leap short of you (between rushMinRun and the phase's longest
        /// run), then leaps rushLeapDistance on to the landing point. Nothing on the way stops it: its body breaks the
        /// walls, fragments and bulwarks it runs into and runs on over holes, so cover is no shelter, and the landing is planned as if what
        /// the run will break were gone already. When there is no ground to stand on past the run's end (you stand in a gravity ring it could
        /// not walk out of, since it is never pulled), it lands on the last good ground before that instead, after a shorter run. Returns the
        /// warning line's length: the whole run and leap, from its centre to the landing point.</summary>
        float PlanRush(PlayerController player)
        {
            var b = Boss;
            float toPlayer = player ? Query.Flat(player.Position - Position).magnitude : 0f;
            float longest = Mathf.Max(0f, bossPhase >= TyrantPhaseCount ? b.rushMaxRunPhase2 : b.rushMaxRun);
            rushRun = Mathf.Clamp(toPlayer - b.rushLeapDistance, Mathf.Min(b.rushMinRun, longest), longest);
            rushSlamReach = Mathf.Max(0f, bossPhase >= TyrantPhaseCount ? b.rushSlamRadiusPhase2 : b.rushSlamRadius);
            Vector3 start = Query.Flat(Position), end = start + facing * rushRun;
            rushLanding = RushLandingFrom(end, end + facing * b.rushLeapDistance, end);
            var world = WorldMap.Active;
            if (world && !RushStandable(world, rushLanding, end))
                // Back along the line from the run's end, down to where it stands: the first point it can stand on after the shorter run
                // that ends one leap short of it (that run breaks less), with no run at all where the leap is longer than the way there.
                for (float d = rushRun;; d = Mathf.Max(0f, d - RushProbeStep))
                {
                    Vector3 at = start + facing * d, shorter = start + facing * Mathf.Max(0f, d - b.rushLeapDistance);
                    if (RushStandable(world, at, shorter) && !world.IsHole(at + facing * radius))
                    {
                        rushLanding = at;
                        rushRun = Mathf.Max(0f, d - b.rushLeapDistance);
                        break;
                    }

                    if (d <= 0f) break;
                }

            return Mathf.Max(radius, Query.Flat(rushLanding - Position).magnitude);
        }

        /// <summary>Whether its body can stand and walk on at the point (WorldMap.IsWalkable). While it plans a run from where it stands to
        /// runEnd, the walls, fragments and bulwarks that run will break no longer count (TyrantTerrain.WalkableAfterRun).</summary>
        bool RushStandable(WorldMap world, Vector3 at, Vector3? runEnd) =>
            runEnd.HasValue
                ? TyrantTerrain.For(world).WalkableAfterRun(at, radius, Position, runEnd.Value, radius, Boss.rushWallPiece)
                : world.IsWalkable(at, radius);

        /// <summary>The landing point: aim, or the nearest point back toward from where its body can stand and walk on (no wall, no hole core
        /// under its front, no gravity ring it could not walk out of, since it is never pulled); from itself when there is none. runEnd as
        /// in RushStandable.</summary>
        Vector3 RushLandingFrom(Vector3 from, Vector3 aim, Vector3? runEnd)
        {
            from = Query.Flat(from);
            aim = Query.Flat(aim);
            var world = WorldMap.Active;
            if (!world) return aim;
            Vector3 delta = aim - from;
            Vector3 dir = delta.sqrMagnitude > 1e-4f ? delta.normalized : facing;
            for (float d = delta.magnitude; d >= 0f; d -= RushProbeStep)
            {
                Vector3 at = from + dir * d;
                if (RushStandable(world, at, runEnd) && !world.IsHole(at + dir * radius)) return at;
            }

            return from;
        }

        void TickRushRun(float dt, PlayerController player)
        {
            var b = Boss;
            // A blocked action ended the run from outside (FixedUpdate clears charging).
            if (!charging)
            {
                EndTyrantMove();
                return;
            }

            if (rushRunLeft <= 0f)
            {
                // The whole planned run is behind it: the leap.
                charging = false;
                Stop();
                BeginRushLeap();
                return;
            }

            // How far its body really runs this step: Combatant.FixedUpdate scales the velocity set below by ExternalMoveMultiplier, and dt
            // already holds the world's time scale and any chill. The last step runs only what is left, so the run ends where the warning
            // line said.
            float speed = b.rushSpeed * (GM && GM.waves ? GM.waves.MovementSpeedScale : 1f);
            float pace = speed * dt * Mathf.Max(0f, ExternalMoveMultiplier), move = Mathf.Min(pace, rushRunLeft);
            if (move <= 0f)
            {
                Stop(); // nothing moves this step
                return;
            }

            rushRunLeft -= move;
            // Nothing stops the run: every wall piece, fragment and bulwark its body runs into on this step breaks first,
            // so its body never meets one standing, and nothing else breaks; it runs on over holes, which never pull or swallow a boss
            // (WorldMap).
            var world = WorldMap.Active;
            if (world)
            {
                Vector3 at = Query.Flat(Position);
                int broken = TyrantTerrain.For(world).BreakAlong(at, at + facing * move, radius, b.rushWallPiece);
                if (broken > 0) V2Audio.Instance?.PlayWorld(ArenaSoundCue.Impact, Position + facing * radius);
            }

            ShoveAside(Query.Flat(Position), facing * move); // the enemies its body runs into on this step go aside
            rb.linearVelocity = facing * (speed * move / pace) + Vector3.up * rb.linearVelocity.y;
            if (!rushContact && player && player.IsAlive && InFist(player))
            {
                // Run down: pushed aside out of its lane, once per charge.
                Vector3 side = Vector3.Cross(Vector3.up, facing);
                if (Vector3.Dot(Query.Flat(player.Position - Position), side) < 0f) side = -side;
                rushContact = DealHit(player, TyrantHit(b.rushRunDamage), side, TierPush(b.rushRunKnock));
            }
        }

        /// <summary>While it runs, its body passes through every other body (the Character layer), so no one holds the run back, not a
        /// Spire, nor a crowd of its own minions, nor you; it knocks the enemies it can move out of its way by its own test (ShoveAside) and
        /// runs you down by another (InFist). Bodies block it again once it leaps, when a run is ended from outside, and on every
        /// Configure.</summary>
        void RunThroughBodies(bool on)
        {
            if (!TryGetComponent(out CapsuleCollider capsule)) return;
            int bodies = GameManager.CharacterMask;
            capsule.excludeLayers = on ? capsule.excludeLayers | bodies : capsule.excludeLayers & ~bodies;
        }

        /// <summary>One step of the run, its centre going from <paramref name="from"/> along <paramref name="step"/>: every other enemy its
        /// body runs into on the way is knocked out of its lane, its own minions included, instead of
        /// being passed through. Each goes straight out to the side of the line it stands on, as far as takes its body rushShoveClearance
        /// clear of the lane. The shove deals no damage, not even into a wall (KnockKind.Push), and holds nothing back: its body still passes
        /// through every body (RunThroughBodies), so one that cannot be moved (a Spire, a body held in place) it runs through as before. A
        /// body behind its centre is one the run leaves behind, and one the hook holds or throws is left alone.</summary>
        void ShoveAside(Vector3 from, Vector3 step)
        {
            var waves = GM ? GM.waves : null;
            if (!waves) return;
            Vector3 side = Vector3.Cross(Vector3.up, facing);
            float clearance = Mathf.Max(0f, Boss.rushShoveClearance), length = step.sqrMagnitude;
            var alive = waves.Alive;
            for (int i = 0; i < alive.Count; i++)
            {
                var enemy = alive[i];
                // Only a body standing on its own: one still sliding from a knock, this shove's or any other, finishes that slide. Pushing it
                // again each step would add Unity's queued velocity change on top of the speed its own FixedUpdate sets for the slide, and it
                // would judder back toward the lane (review 2026-10-26). One that stopped against a wall inside the lane is shoved again.
                if (!enemy || enemy == this || !enemy.IsAlive || enemy.Immovable || enemy.State != CharacterState.Normal) continue;
                // Behind its centre the run leaves a body behind. Ahead of it or beside it, its body reaches the enemy's this step when the
                // nearest point of its centre's way comes closer than the two radii.
                Vector3 offset = Query.Flat(enemy.Position) - from;
                if (Vector3.Dot(offset, facing) < 0f) continue;
                float t = length > 1e-6f ? Mathf.Clamp01(Vector3.Dot(offset, step) / length) : 0f, reach = radius + enemy.Radius;
                if ((offset - step * t).sqrMagnitude > reach * reach) continue;
                float across = Vector3.Dot(offset, side);
                enemy.Knockback(across >= 0f ? side : -side, reach - Mathf.Abs(across) + clearance, KnockKind.Push);
            }
        }

        void BeginRushLeap()
        {
            rushFrom = Query.Flat(Position);
            RunThroughBodies(false);
            // The landing drawn at the wind-up holds unless the ground there is taken now (a fragment or bulwark came since, or the map's
            // edge held the run back short of its end): then the nearest point back along the line where it can stand.
            var world = WorldMap.Active;
            if (world && (!world.IsWalkable(rushLanding, radius) || world.IsHole(rushLanding + facing * radius)))
                rushLanding = RushLandingFrom(rushFrom, rushFrom + facing * Boss.rushLeapDistance, null);
            rushLeapClock = 0f;
            tyrantStage = TyrantStage.Leap;
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true; // flies over walls and holes
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = false;
            V5WeaponAudio.At(V5Sfx.HeavyWhoosh, Position);
        }

        void TickRushLeap(float dt)
        {
            var b = Boss;
            rushLeapClock += dt;
            float t = Mathf.Clamp01(rushLeapClock / Mathf.Max(.05f, b.rushLeapFlight));
            rb.MovePosition(Vector3.Lerp(rushFrom, rushLanding, t) + Vector3.up * (b.rushLeapHeight * 4f * t * (1f - t)));
            if (t < 1f) return;
            rb.position = rushLanding;
            transform.position = rushLanding;
            rb.isKinematic = false;
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = true;
            AttackSequence++;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, rushLanding);
            if (V5Vfx.On) V5Vfx.I.GroundSlam(rushLanding, rushSlamReach, definition.colour);
            else Fx.Ring(rushLanding, rushSlamReach, definition.colour);
            // §13.2: the slam shakes off and hurts everything around the landing point, its own minions and you alike (document 5.3: fly).
            float damage = TyrantHit(b.rushSlamDamage), push = TierPush(b.rushSlamKnock);
            foreach (var target in Query.Characters(Query.AtCastHeight(rushLanding), rushSlamReach + radius))
            {
                if (target == this || !target.IsAlive) continue;
                Vector3 away = Query.Flat(target.Position - rushLanding);
                if (away.magnitude - target.Radius > rushSlamReach) continue;
                DealHit(target, damage, away.sqrMagnitude > 1e-4f ? away : facing, push);
            }

            tyrantStage = TyrantStage.Recover;
            tyrantRecover = EnragedTime(b.rushRecovery);
        }

        // ───── Royal Scepter ─────

        void RaiseScepter()
        {
            var b = Boss;
            scepterWait = Mathf.Max(0f, b.scepterCooldown);
            V5WeaponAudio.At(V5Sfx.ScepterBlast, Position);
            int crowd = ScepterCrowd();
            if (crowd > 0 && GM && GM.waves) GM.waves.SpawnBossWave(crowd, Position, b.scepterRingRadius);
        }

        /// <summary>How many minions the Royal Scepter calls now: scepterCrowdShare of wave 14's crowd (the last ordinary wave's population
        /// target), cut so that the minions alive or on the way never exceed a full wave-14 crowd.</summary>
        public int ScepterCrowd()
        {
            var waves = GM ? GM.waves : null;
            if (!waves || waves.WaveCount <= 0) return 0;
            int full = waves.PopulationTargetForWave(waves.WaveCount - 1), present = waves.PendingCount;
            foreach (var enemy in waves.Alive)
                if (enemy && enemy != this && enemy.IsAlive && !enemy.IsBoss)
                    present++;
            return Mathf.Clamp(Mathf.CeilToInt(full * Mathf.Clamp01(Boss.scepterCrowdShare)), 0, Mathf.Max(0, full - present));
        }

        bool ScepterReady => scepterWait <= 0f && ScepterCrowd() > 0;

        // ───── Star Throw ─────

        void ThrowMeteor(PlayerController player)
        {
            var b = Boss;
            meteorTarget = Query.Flat(player ? player.Position : Position);
            if (WorldMap.Active) meteorTarget = WorldMap.Active.ClampPoint(meteorTarget, 0f);
            meteorFrom = HeadPoint(GM ? GM.cam : null);
            meteorClock = 0f;
            meteorFlying = true;
            meteorReach = Mathf.Max(0f, bossPhase >= TyrantPhaseCount ? b.meteorRadiusPhase2 : b.meteorRadius);
            meteorPieces = Mathf.Max(0, bossPhase >= TyrantPhaseCount ? b.meteorFragmentsPhase2 : b.meteorFragments);
            meteorDamage = TyrantHit(b.meteorDamage);
            V5WeaponAudio.At(V5Sfx.HeavyWhoosh, Position, .9f, 1.15f);
            V5WeaponAudio.AtFit(V5Sfx.MeteorWhistle, meteorTarget, b.meteorFlight); // falls from the throw to the impact, whatever the flight is tuned to
        }

        /// <summary>The meteor lands: you are hit inside its circle (its minions are spared), then it splits into fragments that stand as
        /// walls for a while (TyrantTerrain keeps them off bodies, walls and holes). Black holes never touch it.</summary>
        void TickMeteor(float dt, PlayerController player)
        {
            if (!meteorFlying) return;
            var b = Boss;
            meteorClock += dt;
            if (meteorClock < b.meteorFlight) return;
            meteorFlying = false;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, meteorTarget);
            V2Audio.Instance?.PlayWorld(ArenaSoundCue.Explosion, meteorTarget);
            if (V5Vfx.On) V5Vfx.I.Explosion(meteorTarget, meteorReach);
            else
            {
                Fx.Ring(meteorTarget, meteorReach, MeteorColour);
                Fx.Pop(meteorTarget + Vector3.up, MeteorColour, meteorReach);
            }
            if (player && player.IsAlive)
            {
                Vector3 away = Query.Flat(player.Position - meteorTarget);
                if (away.magnitude - player.Radius <= meteorReach)
                    DealHit(player, meteorDamage, away.sqrMagnitude > 1e-4f ? away : facing, TierPush(b.meteorKnock));
            }

            var world = WorldMap.Active;
            if (!world || meteorPieces <= 0) return;
            var points = new List<Vector3>(meteorPieces);
            float turn = UnityEngine.Random.Range(0f, 360f), ring = meteorReach * Mathf.Clamp01(b.meteorFragmentSpread);
            for (int i = 0; i < meteorPieces; i++)
                points.Add(meteorTarget + Quaternion.Euler(0f, turn + 360f * i / meteorPieces, 0f) * Vector3.forward * ring);
            TyrantTerrain.For(world).AddFragments(points, b.meteorFragmentSize, b.meteorFragmentLife);
        }

        // ───── Astral Guard ─────

        /// <summary>The minions an Astral Guard would devour now: living non-boss enemies within AstralReach of its centre (their body
        /// counts) that the hook is not holding or throwing, nearest first, at most astralMaxDevour.</summary>
        List<Enemy> AstralMeal()
        {
            var meal = new List<Enemy>();
            var waves = GM ? GM.waves : null;
            if (!waves) return meal;
            foreach (var enemy in waves.Alive)
            {
                if (!enemy || enemy == this || !enemy.IsAlive || enemy.IsBoss ||
                    enemy.State is not (CharacterState.Normal or CharacterState.Knocked)) continue;
                if (Query.Flat(enemy.Position - Position).magnitude - enemy.Radius <= AstralReach) meal.Add(enemy);
            }

            meal.Sort((a, c) => Query.Flat(a.Position - Position).sqrMagnitude.CompareTo(Query.Flat(c.Position - Position).sqrMagnitude));
            int most = Mathf.Max(1, Boss.astralMaxDevour);
            if (meal.Count > most) meal.RemoveRange(most, meal.Count - most);
            return meal;
        }

        bool AstralReady => AstralMeal().Count >= Mathf.Max(1, Boss.astralMinMinions);

        /// <summary>The channel ends: the minions in reach vanish into it (no kill, no corpse, no reward) and it restores HP and shield for
        /// each, its shield never past astralShieldCap of its max HP this way.</summary>
        void DevourMinions()
        {
            var b = Boss;
            var waves = GM ? GM.waves : null;
            var meal = AstralMeal();
            if (!waves || meal.Count == 0) return;
            foreach (var minion in meal)
            {
                if (V5Vfx.On) V5Vfx.I.Swallow(minion.Position, Mathf.Max(1f, minion.Radius * 2f));
                else Fx.Pop(minion.Position + Vector3.up * minion.Radius, AstralColour, minion.Radius * 3f);
                waves.DevourSummon(minion);
            }

            Heal(maxHp * Mathf.Clamp01(b.astralHealPerMinion) * meal.Count);
            if (statusBoard)
            {
                float gained = statusBoard.Shield + maxHp * Mathf.Clamp01(b.astralShieldPerMinion) * meal.Count;
                statusBoard.SetShield(Mathf.Max(statusBoard.Shield, Mathf.Min(maxHp * Mathf.Clamp01(b.astralShieldCap), gained)));
            }

            Fx.Ring(Position, AstralReach, AstralColour);
            V5WeaponAudio.At(V5Sfx.AstralDevour, Position);
        }
    }
}
