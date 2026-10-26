using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public partial class Enemy
    {

        public const int TailSweepMove = 10, GroundPoundMove = 11, RampageChargeMove = 12, RoyalScepterMove = 13, StarThrowMove = 14,
            AstralGuardMove = 15;

        public const int TyrantPhaseCount = 2;

        const float RushProbeStep = .5f;

        static readonly Color MeteorColour = new(1f, .6f, .25f), AstralColour = new(.6f, .75f, 1f);

        enum TyrantStage { Idle, Windup, Sweep, Run, Leap, Recover }

        TyrantStage tyrantStage;
        float tyrantRecover, scepterWait;

        float sweepClock, sweepAngle, sweepReach;
        bool sweepLanded;

        Vector3 poundOrigin, poundFacing;
        float poundLength, poundClock;
        int poundStep = -1;
        bool poundLanded;

        Vector3 rushFrom, rushLanding;
        float rushRun, rushRunLeft, rushLeapClock, rushSlamReach;
        bool rushContact;

        Vector3 meteorFrom, meteorTarget;
        float meteorClock, meteorReach, meteorDamage;
        int meteorPieces;
        bool meteorFlying;

        bool IsTyrant => definition && definition.kind == EnemyKind.Tyrant;

        public bool BossSummon { get; set; }

        public bool TailSweeping => IsTyrant && tyrantStage == TyrantStage.Sweep;

        public float TailAngle => sweepAngle;

        public Vector3 TailDirection => Quaternion.Euler(0f, sweepAngle, 0f) * facing;
        public float TailReach => sweepReach;
        public float TailHalfWidth => Mathf.Max(0f, Boss.sweepTailHalfWidth);
        public float SweepArc => Mathf.Clamp(Boss.sweepArc, 10f, 360f);

        public bool PoundShown => IsTyrant && (windup && bossAttack == GroundPoundMove || poundStep >= 0);

        public int PoundStep => poundStep;

        public int PoundSteps => Mathf.Max(1, Boss.poundSteps);

        public Vector3 PoundOrigin => poundOrigin;

        public Vector3 PoundFacing => poundFacing;
        public float PoundLength => poundLength;
        public float PoundWidth => Mathf.Max(0f, Boss.poundWidth);

        public bool RushLandingShown => IsTyrant && bossAttack == RampageChargeMove &&
                                        (windup || tyrantStage is TyrantStage.Run or TyrantStage.Leap);

        public Vector3 RushLanding => rushLanding;
        public float RushSlamReach => rushSlamReach;
        public bool RushLeaping => IsTyrant && tyrantStage == TyrantStage.Leap;

        public bool MeteorFlying => IsTyrant && meteorFlying;

        public Vector3 MeteorTarget => meteorTarget;
        public float MeteorReach => meteorReach;

        public float MeteorFill01 => meteorFlying ? Mathf.Clamp01(meteorClock / Mathf.Max(.05f, Boss.meteorFlight)) : 0f;

        public Vector3 MeteorPosition
        {
            get
            {
                float t = MeteorFill01;
                return Vector3.Lerp(meteorFrom, meteorTarget, t) + Vector3.up * (Mathf.Max(0f, Boss.meteorHeight) * 4f * t * (1f - t));
            }
        }

        public bool AstralChanneling => IsTyrant && windup && bossAttack == AstralGuardMove;

        public float AstralReach => Mathf.Max(0f, Boss.astralRadius);

        public static int TyrantMoveChoice(int phase, float distance, float roll, bool scepterReady, bool astralReady, BossTuning b)
        {
            bool enraged = phase >= TyrantPhaseCount;
            if (distance <= TyrantCloseReach(phase, b))
                return enraged && roll < b.poundShare ? GroundPoundMove : TailSweepMove;
            if (distance <= TyrantMiddleReach(phase, b))
                return enraged && scepterReady && roll < b.scepterShare ? RoyalScepterMove : RampageChargeMove;
            return enraged && astralReady && roll < b.astralShare ? AstralGuardMove : StarThrowMove;
        }

        public static float TyrantCloseReach(int phase, BossTuning b) => phase >= TyrantPhaseCount ? b.sweepRangePhase2 : b.sweepRange;

        public static float TyrantMiddleReach(int phase, BossTuning b) =>
            (phase >= TyrantPhaseCount ? b.rushMaxRunPhase2 : b.rushMaxRun) + b.rushLeapDistance;

        float TyrantHit(float share) =>
            attackDamage * share * (bossPhase >= TyrantPhaseCount ? Mathf.Max(1f, Boss.tyrantEnragedDamage) : 1f);

        float TyrantGap => Mathf.Max(0f, Boss.tyrantMoveGap) *
                           (bossPhase >= TyrantPhaseCount ? Mathf.Clamp(Boss.tyrantEnragedGapShare, .1f, 1f) : 1f);

        float EnragedTime(float seconds) => Mathf.Max(0f, seconds) *
                                            (bossPhase >= TyrantPhaseCount ? Mathf.Clamp(Boss.tyrantEnragedWindupShare, .1f, 1f) : 1f);

        static float TierPush(KnockTier tier) => KnockbackRules.Active.Distance(tier);

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

            if (statusBoard != null)
                foreach (var element in ElementImmunity.Boss)
                    statusBoard.SetImmunity(element, 1f);
            if (!GetComponent<V2TyrantGroundMarks>()) gameObject.AddComponent<V2TyrantGroundMarks>();

            if (WorldMap.Active && WorldMap.Active.TryGetComponent(out TyrantTerrain terrain)) terrain.ResetTerrain();
        }

        bool TickTyrant(float dt, PlayerController player)
        {
            var b = Boss;

            TickTyrantPhase();

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

            Vector3 to = Query.Flat(player.Position - Position);
            float distance = to.magnitude;
            if (distance > 0.01f) facing = to / distance;
            if (cooldown <= 0f)
            {
                StartTyrantMove(ChooseTyrantMove(distance), player);
                return true;
            }

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

        void BeginTyrantWindup(float time, float range)
        {
            windup = true;
            specialWindup = true;
            windupTimer = Mathf.Max(0f, time);
            V3WorldAudio.Instance?.Play(bossAttack == RampageChargeMove ? ArenaWorldCue.NpcChargeWarning : ArenaWorldCue.NpcWindup, Position);
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

        void StrikeTyrantMove(PlayerController player)
        {
            AttackSequence++;
            switch (bossAttack)
            {
                case TailSweepMove:
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCleave, Position);
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

        void EndTyrantMove()
        {
            tyrantStage = TyrantStage.Idle;
            charging = false;
            RunThroughBodies(false);                            
            cooldown = Mathf.Max(cooldown, TyrantGap);
        }

        void TickTyrantPhase()
        {
            if (bossPhase >= TyrantPhaseCount || maxHp <= 0f || Hp >= maxHp * Boss.tyrantPhase2At) return;
            bossPhase = TyrantPhaseCount;
            var balance = GM && GM.waves && GM.waves.waveSet ? GM.waves.waveSet.encounterBalance : null;

            float share = balance ? balance.tyrantPhaseShieldShare : definition.startShieldShare;
            if (statusBoard) statusBoard.SetShield(Mathf.Max(statusBoard.Shield, maxHp * share));
        }

        void TickSweep(float dt, PlayerController player)
        {
            var b = Boss;
            Stop();
            float half = SweepArc * .5f, before = sweepAngle;
            sweepClock += dt;
            float t = b.sweepSeconds > 0f ? Mathf.Clamp01(sweepClock / b.sweepSeconds) : 1f;
            sweepAngle = Mathf.Lerp(-half, half, t);

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

            Vector3 swing = Quaternion.Euler(0f, 90f, 0f) * radial;
            sweepLanded = DealHit(player, TyrantHit(b.sweepDamage), radial + swing, TierPush(b.sweepKnock));
        }

        static bool BulwarkBetween(Vector3 from, Vector3 to) =>
            Query.WallBetween(Query.AtCastHeight(from), Query.AtCastHeight(to), out var hit) && hit.collider &&
            hit.collider.GetComponentInParent<TemporaryWall>();

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

            Fx.Ring(centre, Mathf.Max(band, PoundWidth) * .5f, definition.colour);
            if (poundLanded || !player || !player.IsAlive) return;
            Vector3 offset = Query.Flat(player.Position - poundOrigin);
            float along = Vector3.Dot(offset, poundFacing), across = Mathf.Abs(Vector3.Dot(offset, Vector3.Cross(Vector3.up, poundFacing)));
            if (along < near - player.Radius || along > far + player.Radius || across > PoundWidth * .5f + player.Radius) return;
            poundLanded = DealHit(player, TyrantHit(b.poundDamage), poundFacing, TierPush(b.poundKnock));
        }

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

        bool RushStandable(WorldMap world, Vector3 at, Vector3? runEnd) =>
            runEnd.HasValue
                ? TyrantTerrain.For(world).WalkableAfterRun(at, radius, Position, runEnd.Value, radius, Boss.rushWallPiece)
                : world.IsWalkable(at, radius);

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

            if (!charging)
            {
                EndTyrantMove();
                return;
            }

            if (rushRunLeft <= 0f)
            {

                charging = false;
                Stop();
                BeginRushLeap();
                return;
            }

            float speed = b.rushSpeed * (GM && GM.waves ? GM.waves.MovementSpeedScale : 1f);
            float pace = speed * dt * Mathf.Max(0f, ExternalMoveMultiplier), move = Mathf.Min(pace, rushRunLeft);
            if (move <= 0f)
            {
                Stop();                           
                return;
            }

            rushRunLeft -= move;

            var world = WorldMap.Active;
            if (world)
            {
                Vector3 at = Query.Flat(Position);
                int broken = TyrantTerrain.For(world).BreakAlong(at, at + facing * move, radius, b.rushWallPiece);
                if (broken > 0) V2Audio.Instance?.PlayWorld(ArenaSoundCue.Impact, Position + facing * radius);
            }

            rb.linearVelocity = facing * (speed * move / pace) + Vector3.up * rb.linearVelocity.y;
            if (!rushContact && player && player.IsAlive && InFist(player))
            {

                Vector3 side = Vector3.Cross(Vector3.up, facing);
                if (Vector3.Dot(Query.Flat(player.Position - Position), side) < 0f) side = -side;
                rushContact = DealHit(player, TyrantHit(b.rushRunDamage), side, TierPush(b.rushRunKnock));
            }
        }

        void RunThroughBodies(bool on)
        {
            if (!TryGetComponent(out CapsuleCollider capsule)) return;
            int bodies = GameManager.CharacterMask;
            capsule.excludeLayers = on ? capsule.excludeLayers | bodies : capsule.excludeLayers & ~bodies;
        }

        void BeginRushLeap()
        {
            rushFrom = Query.Flat(Position);
            RunThroughBodies(false);

            var world = WorldMap.Active;
            if (world && (!world.IsWalkable(rushLanding, radius) || world.IsHole(rushLanding + facing * radius)))
                rushLanding = RushLandingFrom(rushFrom, rushFrom + facing * Boss.rushLeapDistance, null);
            rushLeapClock = 0f;
            tyrantStage = TyrantStage.Leap;
            if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            rb.isKinematic = true;                              
            var capsule = GetComponent<CapsuleCollider>();
            if (capsule) capsule.enabled = false;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossCharge, Position);
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
            Fx.Ring(rushLanding, rushSlamReach, definition.colour);

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

        void RaiseScepter()
        {
            var b = Boss;
            scepterWait = Mathf.Max(0f, b.scepterCooldown);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcChant, Position);
            int crowd = ScepterCrowd();
            if (crowd > 0 && GM && GM.waves) GM.waves.SpawnBossWave(crowd, Position, b.scepterRingRadius);
        }

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
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcSwing, Position);
        }

        void TickMeteor(float dt, PlayerController player)
        {
            if (!meteorFlying) return;
            var b = Boss;
            meteorClock += dt;
            if (meteorClock < b.meteorFlight) return;
            meteorFlying = false;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, meteorTarget);
            V2Audio.Instance?.PlayWorld(ArenaSoundCue.Explosion, meteorTarget);
            Fx.Ring(meteorTarget, meteorReach, MeteorColour);
            Fx.Pop(meteorTarget + Vector3.up, MeteorColour, meteorReach);
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

        void DevourMinions()
        {
            var b = Boss;
            var waves = GM ? GM.waves : null;
            var meal = AstralMeal();
            if (!waves || meal.Count == 0) return;
            foreach (var minion in meal)
            {
                Fx.Pop(minion.Position + Vector3.up * minion.Radius, AstralColour, minion.Radius * 3f);
                waves.DevourSummon(minion);
            }

            Heal(maxHp * Mathf.Clamp01(b.astralHealPerMinion) * meal.Count);
            if (statusBoard)
            {
                float gained = statusBoard.Shield + maxHp * Mathf.Clamp01(b.astralShieldPerMinion) * meal.Count;
                statusBoard.SetShield(Mathf.Max(statusBoard.Shield, Mathf.Min(maxHp * Mathf.Clamp01(b.astralShieldCap), gained)));
            }

            Fx.Ring(Position, AstralReach, AstralColour);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position);
        }
    }
}
