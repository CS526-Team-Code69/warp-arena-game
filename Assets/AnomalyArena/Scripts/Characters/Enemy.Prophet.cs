using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The Nash Prophet, boss of the void trial (system doc §13.1). It stands still in the middle of the platform and picks a
    /// move by how far the player is. Close: a repulse pulse that only pushes away a player close to it but throws one in the outer part of
    /// its circle off the platform, pushing harder each phase (in phase 3 only a band about two player bodies wide stays safe), and breaks
    /// every Bulwark wall in its circle, a void summon of Wardens (Shades from phase 3) that keeps it invulnerable while any of them lives,
    /// and equilibrium rings. Far: equilibrium rings (more of them each phase), and from phase 2 a predictive volley of fast shots that lead
    /// and track the player, void pillars that kill a player standing inside one, and a gravity pull that draws the player toward the
    /// middle, into the pillars (document 10-27). Three phases by HP share; the platform
    /// keeps its size through all three. Its HP budget and its damage do not follow the wave, and only the player is ever
    /// hit or pulled.
    /// </summary>
    public partial class Enemy
    {
        // Its moves since 2026-10-25 (BossAttackIndex). The repulse pulse keeps PulseMove, so CircleTelegraph still draws its ground
        // circle, and the equilibrium ring keeps RingMove; InhaleMove, DoubleShotMove and ProphecyMove are no longer chosen.
        public const int VoidSummonMove = 5, VoidPillarMove = 6, PredictiveVolleyMove = 7, GravityPullMove = 8;

        /// <summary>A void pillar: its circle fills during the warning, then the pillar stands for its life and kills a player inside it.</summary>
        public struct VoidPillar
        {
            public Vector3 centre;
            public float radius, age, warning, life;
            public bool Standing => age >= warning;
            public float Warning01 => warning > 0f ? Mathf.Clamp01(age / warning) : 1f;
        }

        readonly List<VoidPillar> voidPillars = new();
        public IReadOnlyList<VoidPillar> VoidPillars => voidPillars;

        // Rest before the next move, rest before the next summon, pull time left, the clocks of the ring and volley sequences, and the
        // first ring's aim (later rings turn from it).
        float nashRest, nashSummonRest, nashPullLeft, nashRingClock, nashVolleyClock, nashRingAim;
        int nashRingsLeft, nashRingIndex, nashVolleyLeft;

        bool IsProphet => definition && definition.kind == EnemyKind.Hollow;

        /// <summary>The gravity pull is drawing the player toward the middle (WorldMap moves them).</summary>
        public bool GravityPulling => IsProphet && IsAlive && nashPullLeft > 0f;

        /// <summary>Cells per second the gravity pull draws the player in; stronger in phase 3.</summary>
        public float GravityPullSpeed => bossPhase >= 3 ? Boss.nashPhase3PullSpeed : Boss.nashPullSpeed;

        /// <summary>While any of its summons lives the Prophet takes no damage of any kind (system doc §13.1).</summary>
        public bool ProphetWarded => IsProphet && IsAlive && WorldMap.Active && WorldMap.Active.TrialActive && LivingProphetSummons() > 0;

        /// <summary>Counts the hits its ward turned away; the ward ring flashes when it changes.</summary>
        public int WardBlockSequence { get; private set; }

        /// <summary>A living summon of the void trial: one of the bodies that keep prophet invulnerable.</summary>
        public bool WardsProphet(Enemy prophet) => this != prophet && IsTrialSummon && IsAlive;

        int LivingProphetSummons()
        {
            if (!GM || !GM.waves) return 0;
            int living = 0;
            foreach (var enemy in GM.waves.Alive)
                if (enemy && enemy.WardsProphet(this)) living++;
            return living;
        }

        /// <summary>ReceiveDamage hook: a hit on the warded Prophet does nothing at all (no HP, no stacks) and flashes the ward.</summary>
        bool TurnedAwayByWard()
        {
            if (!ProphetWarded) return false;
            WardBlockSequence++;
            V5WeaponAudio.At(V5Sfx.RingDeflect, Position, .7f, .8f); // the hit is heard turning away
            return true;
        }

        /// <summary>Void trial entry, after Configure (WorldMap.BeginTrial): fixed in the middle of the platform (kinematic, so bumping into
        /// it never shoves it off), dealing its own fixed damage instead of the wave's, with every clock of the fight cleared.</summary>
        public void BeginProphetFight()
        {
            ClearNashFight();
            attackDamage = NashDamage(1f);
            Vector3 at = Query.Flat(transform.position);
            if (rb)
            {
                if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.position = at;
            }

            transform.position = at;
        }

        /// <summary>Clears the pillars and every move clock (ClearBossMarks: spawn, trial end).</summary>
        void ClearNashFight()
        {
            voidPillars.Clear();
            nashRest = nashSummonRest = nashPullLeft = nashRingClock = nashVolleyClock = nashRingAim = 0f;
            nashRingsLeft = nashRingIndex = nashVolleyLeft = 0;
        }

        /// <summary>A hit of this share: the asset's damage × the fixed damage factor (Y), whatever the wave.</summary>
        float NashDamage(float share) => (definition ? definition.damage : 0f) * Mathf.Max(0f, Boss.nashDamageFactor) * Mathf.Max(0f, share);

        /// <summary>TickSpecial hook: the whole fight. It never walks. A move starts once the opening hold (cooldown), the rings, volley,
        /// pull or summon still running and the rest after them are over; a forced move (enemy lab, tests) skips that rest.</summary>
        bool TickProphet(float dt, PlayerController player)
        {
            Stop();
            var world = WorldMap.Active;
            if (!world || !world.TrialActive) return true; // fought only in the void trial
            var b = Boss;
            TickSummonWarning(dt); // the red crosses of a summon turn into its summons
            TickNashPhase(b);
            TickVoidPillars(dt, player);
            TickNashSequences(dt, player, b);
            if (LivingProphetSummons() > 0 || summonPoints.Count > 0) nashSummonRest = b.nashSummonCooldown;
            else if (nashSummonRest > 0f) nashSummonRest -= dt;
            if (windup)
            {
                windupTimer -= dt;
                if (windupTimer > 0f) return true;
                ReleaseNashMove(player, b);
                CancelWindup();
                return true;
            }

            if (NashSequenceRunning)
            {
                nashRest = b.nashMoveRest;
                return true;
            }

            if (nashRest > 0f) nashRest -= dt;
            if (cooldown > 0f || nashRest > 0f && forcedMove < 0) return true;
            StartNashMove(ChooseNashMove(player, b), player, b);
            return true;
        }

        bool NashSequenceRunning => nashRingsLeft > 0 || nashVolleyLeft > 0 || nashPullLeft > 0f || summonPoints.Count > 0;

        /// <summary>The phase follows its HP share. A new phase changes its moves only: the platform keeps its size, so no
        /// new edge is shown and nobody standing near the rim is dropped.</summary>
        void TickNashPhase(BossTuning b)
        {
            float share = maxHp > 0f ? Hp / maxHp : 1f;
            int next = share < b.nashPhase3HpShare ? 3 : share < b.nashPhase2HpShare ? 2 : 1;
            if (next > bossPhase) bossPhase = next;
        }

        /// <summary>Ages the pillars; a standing pillar kills a player whose centre is inside it (Kill spares the enemy lab's undying player
        /// and Void Walk). Its own summons are never touched.</summary>
        void TickVoidPillars(float dt, PlayerController player)
        {
            for (int i = voidPillars.Count - 1; i >= 0; i--)
            {
                var pillar = voidPillars[i];
                bool stood = pillar.Standing;
                pillar.age += dt;
                if (pillar.age >= pillar.warning + pillar.life)
                {
                    voidPillars.RemoveAt(i);
                    continue;
                }

                voidPillars[i] = pillar;
                if (!pillar.Standing) continue;
                if (!stood)
                {
                    V5WeaponAudio.At(V5Sfx.NashPillar, pillar.centre);
                    Fx.Ring(pillar.centre, pillar.radius, definition.colour);
                }

                if (player && player.IsAlive && Query.Flat(player.Position - pillar.centre).sqrMagnitude <= pillar.radius * pillar.radius)
                    player.Kill(this);
            }
        }

        void TickNashSequences(float dt, PlayerController player, BossTuning b)
        {
            if (nashRingsLeft > 0)
            {
                nashRingClock -= dt;
                if (nashRingClock <= 0f) FireNashRing(player, b);
            }

            if (nashVolleyLeft > 0)
            {
                nashVolleyClock -= dt;
                if (nashVolleyClock <= 0f) FireVolleyShot(player, b);
            }

            if (nashPullLeft > 0f) nashPullLeft = Mathf.Max(0f, nashPullLeft - dt);
        }

        int ChooseNashMove(PlayerController player, BossTuning b)
        {
            if (forcedMove >= 0)
            {
                int forced = forcedMove;
                forcedMove = -1;
                return forced;
            }

            float distance = Query.Flat(player.Position - Position).magnitude;
            bool canSummon = nashSummonRest <= 0f && summonPoints.Count == 0 && LivingProphetSummons() == 0;
            return NashMoveFor(bossPhase, distance <= b.nashNearRange, canSummon, voidPillars.Count > 0, UnityEngine.Random.value, b);
        }

        /// <summary>Nash Prophet move by phase and distance band (system doc §13.1, document 10-27). Close: repulse pulse, void summon (only
        /// when canSummon: none of its summons lives and its rest is over) and the equilibrium ring. Far: equilibrium ring, and from phase 2 the
        /// predictive volley, void pillars and the gravity pull, the pull likelier while pillars stand so it drags the player into them. The
        /// ring comes in both bands, so a far player in phase 1 still meets it. roll is uniform in [0, 1).</summary>
        public static int NashMoveFor(int phase, bool near, bool canSummon, bool pillarsStanding, float roll, BossTuning b)
        {
            if (near) return NashPick(roll, PulseMove, b.nashPulseWeight, VoidSummonMove, canSummon ? b.nashSummonWeight : 0f, RingMove, b.nashNearRingWeight, RingMove, 0f);
            bool late = phase >= 2;
            float pull = b.nashPullWeight * (pillarsStanding ? Mathf.Max(1f, b.nashPullPillarBonus) : 1f);
            return NashPick(roll, RingMove, b.nashRingWeight, PredictiveVolleyMove, late ? b.nashVolleyWeight : 0f,
                VoidPillarMove, late ? b.nashPillarWeight : 0f, GravityPullMove, late ? pull : 0f);
        }

        /// <summary>One of four moves by its weight (a negative weight counts as 0); the first when every weight is 0.</summary>
        static int NashPick(float roll, int m1, float w1, int m2, float w2, int m3, float w3, int m4, float w4)
        {
            w1 = Mathf.Max(0f, w1); w2 = Mathf.Max(0f, w2); w3 = Mathf.Max(0f, w3); w4 = Mathf.Max(0f, w4);
            float pick = Mathf.Clamp01(roll) * (w1 + w2 + w3 + w4);
            if (pick < w1 || w2 + w3 + w4 <= 0f) return m1;
            pick -= w1;
            if (pick < w2 || w3 + w4 <= 0f) return m2;
            pick -= w2;
            return pick < w3 || w4 <= 0f ? m3 : m4;
        }

        /// <summary>One gathering sound, pitched to the move it leads to: the pull and the summon deep, the volley high.</summary>
        static float NashWindupPitch(int move) => move switch
        {
            PulseMove => .8f,
            GravityPullMove => .62f,
            VoidSummonMove => .7f,
            VoidPillarMove => .9f,
            PredictiveVolleyMove => 1.25f,
            _ => 1.05f,
        };

        void StartNashMove(int move, PlayerController player, BossTuning b)
        {
            FacePlayer(player);
            if (move is not (PulseMove or VoidSummonMove or VoidPillarMove or PredictiveVolleyMove or GravityPullMove)) move = RingMove;
            bossAttack = move;
            float time = move switch
            {
                PulseMove => b.nashPulseWindup,
                VoidSummonMove => b.nashSummonWindup,
                VoidPillarMove => b.nashPillarWindup,
                PredictiveVolleyMove => b.nashVolleyWindup,
                GravityPullMove => b.nashPullWindup,
                _ => b.nashRingWindup,
            };
            windupTime = time; // the warning circle times its flashing by it
            // Only the pulse hits a circle round its body, so only the pulse draws one (CircleTelegraph); the others show the yellow body.
            BeginWindup(time, move == PulseMove ? b.nashPulseReach : 0f, false);
        }

        void ReleaseNashMove(PlayerController player, BossTuning b)
        {
            AttackSequence++;
            nashRest = b.nashMoveRest;
            switch (bossAttack)
            {
                case PulseMove:
                    V5WeaponAudio.At(V5Sfx.NashRepulse, Position);
                    if (V5Vfx.On) V5Vfx.I.VoidExplosion(Position, b.nashPulseReach);
                    else Fx.Ring(Position, b.nashPulseReach, definition.colour);
                    PlayerBlast(Position, b.nashPulseReach, NashDamage(b.nashPulseDamage), NashPulsePush(bossPhase, b));
                    // Document 7.4: only a boss breaks a Bulwark wall. The pulse breaks every wall in its circle, as when its time is up.
                    var world = WorldMap.Active;
                    if (world) world.BreakBulwarksWithin(Position, b.nashPulseReach);
                    break;
                case VoidSummonMove:
                    BeginVoidSummon(player, b);
                    break;
                case VoidPillarMove:
                    PlaceVoidPillars(player, b);
                    break;
                case PredictiveVolleyMove:
                    nashVolleyLeft = NashVolleyShots(b);
                    if (nashVolleyLeft > 0) FireVolleyShot(player, b);
                    break;
                case GravityPullMove:
                    nashPullLeft = Mathf.Max(0f, b.nashPullSeconds);
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.BossInhale, Position);
                    V5WeaponAudio.Instance?.TrackPull(this); // a swirl for as long as the pull lasts
                    break;
                default:
                    nashRingIndex = 0;
                    nashRingsLeft = NashRingCount(b);
                    FireNashRing(player, b);
                    break;
            }
        }

        /// <summary>Cells the repulse pulse pushes the player in this phase, more each phase on a platform that keeps its
        /// size. The platform edge does not stop the push (WorldMap.LimitBossKnockback), so a hit farther out than the platform radius less the
        /// slide throws the player into the void and a hit closer only pushes them.</summary>
        public static float NashPulsePush(int phase, BossTuning b) =>
            phase >= 3 ? b.nashPhase3PulsePush : phase == 2 ? b.nashPhase2PulsePush : b.nashPulsePush;

        /// <summary>Red crosses on the summon points first (the wave's own marker flow is paused during the trial); TickSummonWarning turns
        /// them into Wardens, or Shades from phase 3, through WaveManager's boss-summon path, so they give no experience.</summary>
        void BeginVoidSummon(PlayerController player, BossTuning b)
        {
            if (summonPoints.Count > 0 || !GM || !GM.waves) return;
            summonKind = bossPhase >= 3 ? EnemyKind.Shade : EnemyKind.Warden;
            var data = GM.waves.DefinitionOf(summonKind);
            int count = Mathf.Max(0, bossPhase >= 3 ? b.nashPhase3SummonCount : b.nashSummonCount);
            if (!data || count == 0) return;
            Vector3 body = Query.Flat(Position), away = player ? body - Query.Flat(player.Position) : -facing;
            float start = away.sqrMagnitude > 1e-4f ? Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg : 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 at = body + Quaternion.Euler(0f, start + 360f * i / count, 0f) * Vector3.forward * b.nashSummonRingRadius;
                summonPoints.Add(at);
                var marker = GM.waves.BorrowMarker();
                if (!marker) continue;
                marker.Begin(at, data, false, ElementKind.None, true);
                summonMarkers.Add(marker);
            }

            summonWarning = b.nashSummonWarning;
            V5WeaponAudio.At(V5Sfx.NashSummon, Position, 1f, summonKind == EnemyKind.Shade ? .85f : 1f);
        }

        /// <summary>The first pillar stands on the player; the others step back toward the Prophet on alternate sides, so a gravity pull drags
        /// the player through them. Each is moved off its own body and, last, off the platform portals (a way out must stay open), and every
        /// centre stays on the platform.</summary>
        void PlaceVoidPillars(PlayerController player, BossTuning b)
        {
            var world = WorldMap.Active;
            if (!world || !world.trialPlatform || !player) return;
            bool late = bossPhase >= 3;
            int count = Mathf.Max(0, late ? b.nashPhase3PillarCount : b.nashPillarCount);
            float size = Mathf.Max(.1f, late ? b.nashPhase3PillarRadius : b.nashPillarRadius);
            Vector3 body = Query.Flat(Position), toPlayer = Query.Flat(player.Position) - body;
            Vector3 side = Vector3.Cross(Vector3.up, toPlayer.sqrMagnitude > 1e-4f ? toPlayer.normalized : facing);
            for (int i = 0; i < count; i++)
            {
                Vector3 at = body + toPlayer * (1f - (float)i / count) + side * (i == 0 ? 0f : i % 2 == 0 ? size : -size);
                voidPillars.Add(new VoidPillar
                    { centre = PillarSpot(at, size, world, b), radius = size, warning = b.nashPillarWarning, life = b.nashPillarLife });
            }

            V5WeaponAudio.At(V5Sfx.NashPillarWarn, Position);
        }

        Vector3 PillarSpot(Vector3 at, float size, WorldMap world, BossTuning b)
        {
            Vector3 body = Query.Flat(Position);
            Vector3 off = at - body;
            if (off.magnitude < radius + size) at = body + (off.sqrMagnitude > 1e-4f ? off.normalized : -facing) * (radius + size);
            for (int i = 0; i < world.TrialPortalCount; i++)
            {
                Vector3 portal = world.TrialPortalCentre(i), clear = at - portal;
                float need = size + b.nashPortalTrigger;
                if (clear.magnitude >= need) continue;
                Vector3 outward = clear.sqrMagnitude > 1e-4f ? clear.normalized : Query.Flat(portal - body).normalized;
                at = portal + outward * need;
            }

            Vector3 centre = Query.Flat(world.trialPlatform.position);
            return centre + Vector3.ClampMagnitude(at - centre, world.trialRadius);
        }

        int NashRingCount(BossTuning b) => Mathf.Max(1, bossPhase >= 3 ? b.nashPhase3Rings : bossPhase == 2 ? b.nashPhase2Rings : 1);

        int NashVolleyShots(BossTuning b) => Mathf.Max(0, bossPhase >= 3 ? b.nashPhase3VolleyShots : b.nashVolleyShots);

        /// <summary>Equilibrium ring: evenly spaced shots, the first ring aimed at the player; each later ring turns from the first by the
        /// spacing over the ring count, so together they close every gap of the first even if the player stepped into one.</summary>
        void FireNashRing(PlayerController player, BossTuning b)
        {
            int count = Mathf.Max(1, bossPhase >= 3 ? b.nashPhase3RingShots : b.nashRingShots);
            float spacing = 360f / count;
            if (nashRingIndex == 0)
            {
                Vector3 aim = player ? Query.Flat(player.Position - Position) : facing;
                if (aim.sqrMagnitude < 1e-4f) aim = facing;
                nashRingAim = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
            }

            float turn = spacing / NashRingCount(b) * nashRingIndex;
            float damage = NashDamage(b.nashRingDamage);
            for (int i = 0; i < count; i++)
            {
                float angle = (nashRingAim + turn + spacing * i) * Mathf.Deg2Rad;
                NashShot(new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)), damage, b.nashRingSpeed);
            }

            V5WeaponAudio.At(V5Sfx.NashRing, Position, 1f, 1f + .06f * nashRingIndex);
            nashRingIndex++;
            nashRingsLeft--;
            nashRingClock = b.nashRingInterval;
        }

        /// <summary>One shot of the predictive volley: aimed where the player will be when it arrives, then steered toward them a while.</summary>
        void FireVolleyShot(PlayerController player, BossTuning b)
        {
            int shot = NashVolleyShots(b) - nashVolleyLeft;
            nashVolleyLeft--;
            nashVolleyClock = b.nashVolleyInterval;
            if (!player || !player.IsAlive) return;
            var body = player.GetComponent<Rigidbody>();
            Vector3 aim = VolleyLeadPoint(Position, player.Position, body ? body.linearVelocity : Vector3.zero, b.nashVolleySpeed);
            NashShot(aim - Position, NashDamage(b.nashVolleyDamage), b.nashVolleySpeed, b.nashVolleyTurnRate, b.nashVolleyTrackSeconds);
            V5WeaponAudio.At(V5Sfx.NashVolley, Position, 1f, 1f + .025f * shot); // every shot of the volley is heard, climbing a little
        }

        /// <summary>Where a shot of this speed from from meets a target moving at velocity: the flight time to the target, then once more to
        /// where it will be. The predictive volley aims there and its shots steer toward it (EnemyBolt.Track).</summary>
        public static Vector3 VolleyLeadPoint(Vector3 from, Vector3 target, Vector3 velocity, float speed)
        {
            from = Query.Flat(from);
            target = Query.Flat(target);
            velocity = Query.Flat(velocity);
            speed = Mathf.Max(.1f, speed);
            float flight = (target - from).magnitude / speed;
            flight = (target + velocity * flight - from).magnitude / speed;
            return target + velocity * flight;
        }

        /// <summary>A boss shot along direction from just outside its body; never pushes. turnRate above 0 steers it for trackSeconds.</summary>
        void NashShot(Vector3 direction, float damage, float speed, float turnRate = 0f, float trackSeconds = 0f)
        {
            direction = Query.Flat(direction);
            if (direction.sqrMagnitude < 1e-4f) direction = facing;
            direction.Normalize();
            GM.waves.LaunchTrackingBossBolt(this, Position + direction * (radius + Mathf.Max(0f, Boss.nashMuzzleGap)), direction, damage, speed,
                turnRate, trackSeconds);
        }
    }
}
