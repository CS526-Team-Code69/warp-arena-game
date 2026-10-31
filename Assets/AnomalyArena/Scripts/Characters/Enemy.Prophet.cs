using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                 
                                                                                                                                                
                                                                                                                                            
                                                                                                                                           
                                                                                                                                            
                                                                                                                                             
                                                                                                                                      
                                                                                         
                                                                                                                                             
                      
                  
    public partial class Enemy
    {
                                                                                                                                     
                                                                                                                             
        public const int VoidSummonMove = 5, VoidPillarMove = 6, PredictiveVolleyMove = 7, GravityPullMove = 8;

                                                                                                                                                    
        public struct VoidPillar
        {
            public Vector3 centre;
            public float radius, age, warning, life;
            public bool Standing => age >= warning;
            public float Warning01 => warning > 0f ? Mathf.Clamp01(age / warning) : 1f;
        }

        readonly List<VoidPillar> voidPillars = new();
        public IReadOnlyList<VoidPillar> VoidPillars => voidPillars;

                                                                                                                                       
                                                       
        float nashRest, nashSummonRest, nashPullLeft, nashRingClock, nashVolleyClock, nashRingAim;
        int nashRingsLeft, nashRingIndex, nashVolleyLeft;

        bool IsProphet => definition && definition.kind == EnemyKind.Hollow;

                                                                                                              
        public bool GravityPulling => IsProphet && IsAlive && nashPullLeft > 0f;

                                                                                                          
        public float GravityPullSpeed => bossPhase >= 3 ? Boss.nashPhase3PullSpeed : Boss.nashPullSpeed;

                                                                                                                         
        public bool ProphetWarded => IsProphet && IsAlive && WorldMap.Active && WorldMap.Active.TrialActive && LivingProphetSummons() > 0;

                                                                                                           
        public int WardBlockSequence { get; private set; }

                                                                                                                   
        public bool WardsProphet(Enemy prophet) => this != prophet && IsTrialSummon && IsAlive;

        int LivingProphetSummons()
        {
            if (!GM || !GM.waves) return 0;
            int living = 0;
            foreach (var enemy in GM.waves.Alive)
                if (enemy && enemy.WardsProphet(this)) living++;
            return living;
        }

                                                                                                                                           
        bool TurnedAwayByWard()
        {
            if (!ProphetWarded) return false;
            WardBlockSequence++;
            V5WeaponAudio.At(V5Sfx.RingDeflect, Position, .7f, .8f);                                 
            return true;
        }

                                                                                                                                             
                                                                                                                                         
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

                                                                                                          
        void ClearNashFight()
        {
            voidPillars.Clear();
            nashRest = nashSummonRest = nashPullLeft = nashRingClock = nashVolleyClock = nashRingAim = 0f;
            nashRingsLeft = nashRingIndex = nashVolleyLeft = 0;
        }

                                                                                                                        
        float NashDamage(float share) => (definition ? definition.damage : 0f) * Mathf.Max(0f, Boss.nashDamageFactor) * Mathf.Max(0f, share);

                                                                                                                                          
                                                                                                                                      
        bool TickProphet(float dt, PlayerController player)
        {
            Stop();
            var world = WorldMap.Active;
            if (!world || !world.TrialActive) return true;                                 
            var b = Boss;
            TickSummonWarning(dt);                                                     
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

                                                                                                                                              
                                                                                    
        void TickNashPhase(BossTuning b)
        {
            float share = maxHp > 0f ? Hp / maxHp : 1f;
            int next = share < b.nashPhase3HpShare ? 3 : share < b.nashPhase2HpShare ? 2 : 1;
            if (next > bossPhase) bossPhase = next;
        }

                                                                                                                                             
                                                                        
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

                                                                                                                                             
                                                                                                                                                 
                                                                                                                                             
                                                                                                                     
        public static int NashMoveFor(int phase, bool near, bool canSummon, bool pillarsStanding, float roll, BossTuning b)
        {
            if (near) return NashPick(roll, PulseMove, b.nashPulseWeight, VoidSummonMove, canSummon ? b.nashSummonWeight : 0f, RingMove, b.nashNearRingWeight, RingMove, 0f);
            bool late = phase >= 2;
            float pull = b.nashPullWeight * (pillarsStanding ? Mathf.Max(1f, b.nashPullPillarBonus) : 1f);
            return NashPick(roll, RingMove, b.nashRingWeight, PredictiveVolleyMove, late ? b.nashVolleyWeight : 0f,
                VoidPillarMove, late ? b.nashPillarWeight : 0f, GravityPullMove, late ? pull : 0f);
        }

                                                                                                                                 
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
            windupTime = time;                                               
                                                                                                                                           
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
                    V5WeaponAudio.Instance?.TrackPull(this);                                         
                    break;
                default:
                    nashRingIndex = 0;
                    nashRingsLeft = NashRingCount(b);
                    FireNashRing(player, b);
                    break;
            }
        }

                                                                                                                                             
                                                                                                                                                
                                                                                              
        public static float NashPulsePush(int phase, BossTuning b) =>
            phase >= 3 ? b.nashPhase3PulsePush : phase == 2 ? b.nashPhase2PulsePush : b.nashPulsePush;

                                                                                                                                            
                                                                                                                                    
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

                                                                                                                                                   
        void FireVolleyShot(PlayerController player, BossTuning b)
        {
            int shot = NashVolleyShots(b) - nashVolleyLeft;
            nashVolleyLeft--;
            nashVolleyClock = b.nashVolleyInterval;
            if (!player || !player.IsAlive) return;
            var body = player.GetComponent<Rigidbody>();
            Vector3 aim = VolleyLeadPoint(Position, player.Position, body ? body.linearVelocity : Vector3.zero, b.nashVolleySpeed);
            NashShot(aim - Position, NashDamage(b.nashVolleyDamage), b.nashVolleySpeed, b.nashVolleyTurnRate, b.nashVolleyTrackSeconds);
            V5WeaponAudio.At(V5Sfx.NashVolley, Position, 1f, 1f + .025f * shot);                                                        
        }

                                                                                                                                             
                                                                                                                         
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
