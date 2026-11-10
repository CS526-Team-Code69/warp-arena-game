using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    public sealed partial class RunWeapons
    {
        [Header("Auto weapon motion — candidate tuning")]
        [Tooltip("Degrees per world second; GDD specifies two opposite blades but not their angular speed.")]
        public float orbitAngularSpeed = 180f;
        [Tooltip("A body may be touched again after this many world seconds.")]
        public float orbitHitInterval = .5f;
        public float orbitBladeRadius = .35f;
        float autoClock;
        readonly Dictionary<Combatant,float>[] autoHitTimes = { new(), new(), new() };
        // Bodies one auto weapon hits this frame, collected first: a kill can wake a pooled enemy, which joins Combatant.All.
        readonly List<Combatant> autoTargets=new();
        // Closed Blade: when each pickup and each corpse (by spawn version) may be knocked again, on the world clock.
        readonly Dictionary<ExperienceOrb,float> orbKnockReady=new();
        readonly Dictionary<Enemy,(int version,float ready)> corpseKnockReady=new();
        // Deflect: enemies the ring slows (with their spawn version) and the weapon slowing them; enemy shots already rolled (by launch
        // count, so a pooled shot fired again rolls again); your own shots by pool slot, with their launch sequence and whether they were
        // inside the ring when last seen.
        readonly Dictionary<Combatant,int> deflectSlowed=new();
        readonly List<Combatant> deflectPruned=new();
        RunWeapon deflectSlowKey;
        readonly Dictionary<EnemyBolt,int> rolledBolts=new();
        readonly Dictionary<AttackActor,(int sequence,bool inside)> crossWatch=new();
        // Whirl: your recent ground positions on the world clock (the afterimage spot).
        readonly Vector3[] playerTrail=new Vector3[128];readonly float[] playerTrailTimes=new float[128];int playerTrailHead,playerTrailCount;
        const float PlayerTrailSample=.05f;
        // Void trial: the battlefield's drone and throw, kept while the trial runs its own.
        readonly Dictionary<RunWeapon,(Vector3 drop,int shots,float down,Vector3 from,Vector3 to,float total)> trialAutoStates=new();
        V2AutoWeaponMarks autoMarks;
        static readonly Color DeflectColor=new(.6f,.95f,1f,.85f);
        public float StaminaRecoveryMultiplier
        {
            get { return 1; }
        }
        public bool HasOrbitBlades(int slot) => slot >= 0 && slot < Auto.Length && Auto[slot] is { form: WeaponForm.OrbitBlades, revealed: true };
        public Vector3 GetOrbitPosition(int slot,int blade)
        {
            var w=Auto[slot];
            float radians=(w.orbitAngle+180f*blade)*Mathf.Deg2Rad;
            float radius=roster.Get(w.form).rangeA*w.Reach;
            return player.Position+new Vector3(Mathf.Cos(radians)*radius,.65f,Mathf.Sin(radians)*radius);
        }
        public float ReturnCooldown(RunWeapon w) => roster.Get(w.form).cooldownA * CooldownScale(w);
        /// <summary>Gunner: rounds in one magazine.</summary>
        public int GunnerClip=>Mathf.Max(1,upgrades.gunnerClip);
        /// <summary>Where the Sentry Drone hovers now: a fixed offset from your centre (droneHoverOffset; y is its height).</summary>
        public Vector3 DroneHover()=>player?player.Position+upgrades.droneHoverOffset:Vector3.zero;
        /// <summary>The Gunner lies on the ground cooling down (w.dronePoint is where).</summary>
        public bool DroneDown(RunWeapon w)=>w!=null&&w.droneDownLeft>0f;
        /// <summary>Radius of an Energy Ring: the Deflect force field round you.</summary>
        public float FieldRadius(RunWeapon w){var d=w!=null&&roster?roster.Get(w.form):null;return d==null?0f:(w.variant==0?d.rangeA:d.rangeB)*w.Reach;}
        /// <summary>Drain (Q95, Q99): seconds without an enemy attack before the Energy Shield forms; a higher Mk divides
        /// the wait by its damage factor.</summary>
        public float DrainShieldWait(RunWeapon w)=>Mathf.Max(0f,upgrades.drainShieldWait)/Mathf.Max(.01f,MkDamage(w));
        void TickAutoWeapons(float dt)
        {
            autoClock+=dt;
            if(!autoMarks){autoMarks=GetComponent<V2AutoWeaponMarks>();if(!autoMarks)autoMarks=gameObject.AddComponent<V2AutoWeaponMarks>();autoMarks.weapons=this;}
            RecordPlayerTrail();
            // Overcharge speeds auto weapons too: their cooldowns and the blades' re-hit interval.
            float rate=Mathf.Max(.01f,WeaponCooldownRate);
            bool deflect=false;
            for(int slot=0;slot<Auto.Length;slot++)
            {
                var w=Auto[slot];if(w==null)continue;
                // Trial suspension makes a living battlefield target inactive; retain its grab and timer.
                if(w.whirled&&BattlefieldFrozen(w.whirled.gameObject))continue;
                w.cooldown=Mathf.Max(0,w.cooldown-dt*rate);w.orbitAngle+=orbitAngularSpeed*dt;
                var definition=roster.Get(w.form);
                float hit=(w.variant==0?definition.damageA:definition.damageB)*DamageScale(w);
                float range=(w.variant==0?definition.rangeA:definition.rangeB)*w.Reach;
                float cycle=(w.variant==0?definition.cooldownA:definition.cooldownB)*CooldownScale(w);
                if(w.form==WeaponForm.OrbitBlades){TickOrbit(w,slot,hit,rate);continue;}
                if(w.form==WeaponForm.SentryDrone){TickDrone(w,dt,hit,range,cycle);continue;}
                if(w.form!=WeaponForm.ShieldRing)continue;
                Reveal(w);
                if(w.variant==0){deflect=true;TickDeflect(w,hit,range,cycle);}
                else TickDrain(w,dt,hit);
            }
            // A Deflect no longer held leaves nobody slowed.
            if(!deflect)ClearDeflectSlow();
        }

        // ───── Orbit Blades ─────

        /// <summary>Two tips on the circle hit what they touch (Closed pushes out, Open pulls in), each body at most once per
        /// orbitHitInterval. Document 5.5, Q82: the Closed Blade also knocks away every pickup and corpse its tips
        /// touch, checked every frame like the bodies.</summary>
        void TickOrbit(RunWeapon w,int slot,float hit,float rate)
        {
            Reveal(w);
            for(int blade=0;blade<2;blade++)
            {
                Vector3 position=GetOrbitPosition(slot,blade);
                autoTargets.Clear();
                foreach(var body in Combatant.All)
                {
                    if(body==player||!body.IsAlive)continue;
                    if(autoHitTimes[slot].TryGetValue(body,out float next)&&next>autoClock)continue;
                    if(Query.Flat(body.Position-position).magnitude>body.radius+orbitBladeRadius||WallBlocks(player.Position,body.Position))continue;
                    autoHitTimes[slot][body]=autoClock+orbitHitInterval*CooldownScale(w)/rate;
                    autoTargets.Add(body);
                }
                foreach(var body in autoTargets)
                {
                    Damage(w,body,hit,w.variant==0?3:-1.5f,Query.Flat(body.Position-player.Position).normalized);
                    if(lastDamageAccepted)WeaponSound(w);
                }
                autoTargets.Clear();
                if(w.variant==0)KnockLoose(position);
            }
        }

        /// <summary>Closed Blade: pickups (XP, HP, Energy) and lying corpses a tip touches fly outward, away from you (pickups
        /// OrbPool.repelDistance, corpses orbitCorpseKnock cells), each at most once per orbitKnockRepeat seconds; walls shield them as
        /// they shield bodies. A corpse knocked into a black hole feeds it (WorldMap.TickCorpses).</summary>
        void KnockLoose(Vector3 tip)
        {
            float repeat=Mathf.Max(0f,upgrades.orbitKnockRepeat);
            var orbs=OrbPool.Instance;
            if(orbs&&orbs.pool!=null)foreach(var orb in orbs.pool)
            {
                if(!orb||!orb.InUse||Query.Flat(orb.Position-tip).sqrMagnitude>orbitBladeRadius*orbitBladeRadius)continue;
                if(orbKnockReady.TryGetValue(orb,out float ready)&&ready>autoClock||WallBlocks(player.Position,orb.Position))continue;
                orbKnockReady[orb]=autoClock+repeat;
                orb.Repel(player.Position,orbs.repelDistance);
            }
            var prepared=GameManager.Instance&&GameManager.Instance.waves?GameManager.Instance.waves.preparedEnemies:null;
            if(prepared==null)return;
            foreach(var corpse in prepared)
            {
                if(!corpse||!corpse.CorpseActive||corpse.CorpseCarried||!corpse.gameObject.activeSelf)continue;
                if(Query.Flat(corpse.Position-tip).magnitude>corpse.radius+orbitBladeRadius)continue;
                if(corpseKnockReady.TryGetValue(corpse,out var mark)&&mark.version==corpse.SpawnVersion&&mark.ready>autoClock||WallBlocks(player.Position,corpse.Position))continue;
                corpseKnockReady[corpse]=(corpse.SpawnVersion,autoClock+repeat);
                corpse.KnockCorpse(corpse.Position-player.Position,upgrades.orbitCorpseKnock);
            }
        }

        // ───── Sentry Drone ─────

        /// <summary>Sentry Drone (document 5.5): it hovers by you and its shots and beam leave from there. Gunner: rapid
        /// fire at the nearest visible enemy in range, costing no Energy (Q86); after GunnerClip rounds it drops to the ground and cools
        /// down until you step on it or gunnerAutoReturn seconds pass, then flies back with a full magazine. Whirl: TickWhirl.</summary>
        void TickDrone(RunWeapon w,float dt,float hit,float range,float cycle)
        {
            if(w.variant==0&&w.droneDownLeft>0f){TickDroppedDrone(w,dt);return;}
            if(w.whirled){TickWhirl(w,dt,hit);return;}
            if(w.cooldown>0)return;
            if(w.variant==1)
            {
                var lone=WhirlTarget(range);
                if(!lone){w.cooldown=upgrades.whirlRetry;return;}
                BeginWhirl(w,lone,cycle);return;
            }
            var target=NearestVisible(range);if(!target)return;
            Reveal(w);w.cooldown=cycle;
            Vector3 muzzle=DroneMuzzle(),dir=Query.Flat(target.Position-muzzle);
            if(dir.sqrMagnitude<1e-4f)dir=Query.Flat(target.Position-player.Position);
            // The target is picked within range of you, so the round flies that far plus the drone's offset; only a round fired counts.
            if(DroneBolt(w,muzzle,dir.normalized,hit,range+Query.Flat(muzzle-player.Position).magnitude)&&++w.droneShots>=GunnerClip)DropDrone(w);
        }

        /// <summary>The drone's point on the ground, pulled in before any wall between it and you, so a shot never starts inside a wall.</summary>
        Vector3 DroneMuzzle()
        {
            Vector3 from=Query.AtCastHeight(player.Position),to=Query.AtCastHeight(Query.Flat(DroneHover()));
            if(Physics.Linecast(from,to,out var wall,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                to=from+(to-from).normalized*Mathf.Max(0f,wall.distance-upgrades.spawnWallBackoff);
            return Query.Flat(to);
        }

        AttackActor DroneBolt(RunWeapon w,Vector3 at,Vector3 dir,float hit,float reach)
        {
            var a=AttackPool.Instance?AttackPool.Instance.Acquire():null;if(!a)return null;
            float speed=Mathf.Max(1f,upgrades.gunnerBulletSpeed);
            a.Configure(AttackMode.Bolt,player,w,at,dir,hit,0,reach,speed,0,reach/speed+3);
            if(V5Vfx.On)V5Vfx.I.Muzzle(at,dir,w.element==ElementKind.None?new Color(.6f,.95f,1f):V5Vfx.ElementTint(w.element),.5f);
            WeaponSound(w);return a;
        }

        /// <summary>Gunner backfire: the empty drone drops where it hovers, on the nearest ground clear of walls and holes
        /// (Q84), and lies there for gunnerAutoReturn seconds (Q85). It never lands within reach of your body, or you would
        /// pick it up the moment it fell.</summary>
        void DropDrone(RunWeapon w)
        {
            w.droneDownLeft=Mathf.Max(.01f,upgrades.gunnerAutoReturn);
            w.dronePoint=NearestValidPoint(DroneHover(),upgrades.droneRadius,player.Position,player.Position,player.radius+upgrades.droneRadius+.05f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponJam,w.dronePoint);
        }

        /// <summary>A dropped Gunner: touching it with your body sends it back at once (with a sound and a ring), otherwise it goes back
        /// when its time is up; either way with a full magazine.</summary>
        void TickDroppedDrone(RunWeapon w,float dt)
        {
            w.droneDownLeft=Mathf.Max(0f,w.droneDownLeft-dt);
            bool stepped=player.IsAlive&&Query.Flat(player.Position-w.dronePoint).magnitude<=player.radius+upgrades.droneRadius;
            if(!stepped&&w.droneDownLeft>0f)return;
            w.droneDownLeft=0f;w.droneShots=0;
            if(!stepped)return;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair,w.dronePoint);
            Fx.Ring(w.dronePoint,player.radius+upgrades.droneRadius,DeflectColor);
        }

        /// <summary>Whirl target (Q87): the nearest enemy between whirlMinRange and range from you that you can see,
        /// that can be moved (no boss, Spire or hooked body) and that stands alone, with no other enemy's body within whirlLoneRadius of
        /// it. Null when there is none, and the drone looks again after whirlRetry.</summary>
        Combatant WhirlTarget(float range)
        {
            Combatant best=null;float nearest=float.PositiveInfinity;
            foreach(var c in Combatant.All)
            {
                if(c==player||!c.IsAlive||c.Team!=Team.Enemy||!WhirlCandidate(c))continue;
                float d=Query.Flat(c.Position-player.Position).magnitude;
                if(d<upgrades.whirlMinRange||d>range||d>=nearest||WallBlocks(player.Position,c.Position)||!Lone(c))continue;
                best=c;nearest=d;
            }
            return best;
        }
        bool Lone(Combatant c)
        {
            foreach(var other in Combatant.All)
                if(other!=c&&other!=player&&other.IsAlive&&other.Team==Team.Enemy&&Query.Flat(other.Position-c.Position).magnitude<=upgrades.whirlLoneRadius+other.radius)return false;
            return true;
        }
        static bool WhirlCandidate(Combatant body)=>body is not Enemy{Immovable:true}&&body.State!=CharacterState.Hooked;

        /// <summary>The beam locks on: the enemy is hooked (no black hole pulls it, it cannot act) and its throw is set from where it
        /// stands to the afterimage spot; the cooldown starts now.</summary>
        void BeginWhirl(RunWeapon w,Combatant target,float cycle)
        {
            Reveal(w);w.cooldown=cycle;
            w.whirled=target;target.SetState(CharacterState.Hooked);
            w.whirlFrom=Query.Flat(target.Position);
            w.whirlTo=WhirlLanding(target);
            w.whirlTotal=Mathf.Max(upgrades.whirlMinFlight,Query.Flat(w.whirlTo-w.whirlFrom).magnitude/Mathf.Max(1f,upgrades.whirlThrowSpeed));
            w.whirlLeft=w.whirlTotal;
            WeaponSound(w,WeaponAudioPhase.Launch);
        }

        /// <summary>Where the Whirl slams (Q89): where you stood whirlAfterimageSeconds ago, kept off your body as
        /// you stand now, and moved to the nearest legal ground if it is no longer legal (a hole opened, a wall went up).</summary>
        Vector3 WhirlLanding(Combatant target)=>NearestValidPoint(OffYourBody(PlayerPositionAgo(upgrades.whirlAfterimageSeconds),target),target.radius,Query.Flat(target.Position),player.Position,BodyClearance(target));
        /// <summary>Centre distance at which body stands clear of your body.</summary>
        float BodyClearance(Combatant body)=>player.radius+body.radius+.1f;

        /// <summary>A spot for body that does not overlap your body now: a player who stood still gets the enemy beside them.</summary>
        Vector3 OffYourBody(Vector3 spot,Combatant body)
        {
            Vector3 away=Query.Flat(spot-player.Position);float clear=BodyClearance(body);
            if(away.sqrMagnitude>=clear*clear)return Query.Flat(spot);
            if(away.sqrMagnitude<1e-4f)away=Query.Flat(body.Position-player.Position);
            if(away.sqrMagnitude<1e-4f)away=Vector3.forward;
            return Query.Flat(player.Position)+away.normalized*clear;
        }

        /// <summary>Whirl (Q88): the tractor beam drags the hooked enemy along an arc to the afterimage spot and slams
        /// it down there. A wall in the way ends the throw where the enemy is, in front of the wall.</summary>
        void TickWhirl(RunWeapon w,float dt,float hit)
        {
            var body=w.whirled;
            if(!body.IsAlive){EndWhirl(w);return;}
            w.whirlLeft=Mathf.Max(0f,w.whirlLeft-dt);
            float t=w.whirlTotal>0f?Mathf.Clamp01(1f-w.whirlLeft/w.whirlTotal):1f;
            Vector3 now=Query.Flat(body.Position),next=Vector3.Lerp(w.whirlFrom,w.whirlTo,t);
            if(WallBlocks(now,next)){SlamWhirl(w,now,hit);return;}
            body.SetMovePosition(next+Vector3.up*(4f*upgrades.whirlArcHeight*t*(1f-t)));
            if(t>=1f)SlamWhirl(w,next,hit);
        }

        /// <summary>The slam: the thrown enemy lands and it and every other enemy within whirlSlamRadius of it (no wall between) take one
        /// weapon hit; you never do.</summary>
        void SlamWhirl(RunWeapon w,Vector3 at,float hit)
        {
            var body=w.whirled;w.whirled=null;w.whirlLeft=0f;
            if(!body||!body.IsAlive)return;
            at=NearestValidPoint(OffYourBody(at,body),body.radius,w.whirlFrom,player.Position,BodyClearance(body));
            TeleportBody(body,at);
            WeaponSound(w,WeaponAudioPhase.Explosion);
            Fx.Ring(at,upgrades.whirlSlamRadius+body.radius,ElementCombat.ColorFor(w.element));
            Vector3 fall=Query.Flat(at-w.whirlFrom);fall=fall.sqrMagnitude>1e-4f?fall.normalized:Vector3.forward;
            autoTargets.Clear();
            foreach(var c in Combatant.All)
                if(c==body||c!=player&&c.IsAlive&&c.Team==Team.Enemy&&Query.Flat(c.Position-at).magnitude<=upgrades.whirlSlamRadius+c.radius&&!WallBlocks(at,c.Position))autoTargets.Add(c);
            foreach(var c in autoTargets)Damage(w,c,hit,0,c==body?fall:Query.Flat(c.Position-at).normalized);
            autoTargets.Clear();
        }

        /// <summary>Lets go of the Whirl's enemy without a slam (it died, the wave ended): a living one is set down where it is.</summary>
        void EndWhirl(RunWeapon w)
        {
            var body=w.whirled;w.whirled=null;w.whirlLeft=0f;
            if(!body||!body.IsAlive)return;
            TeleportBody(body,player?NearestValidPoint(body.Position,body.radius,w.whirlFrom,player.Position,BodyClearance(body)):NearestValidPoint(body.Position,body.radius,w.whirlFrom));
        }

        /// <summary>at itself when a body of this radius may stand there clear of the circle of avoidRadius round avoid, else the nearest
        /// such point within 4 cells, else fallback.</summary>
        static Vector3 NearestValidPoint(Vector3 at,float radius,Vector3 fallback,Vector3 avoid=default,float avoidRadius=0f)
        {
            at=Query.Flat(at);avoid=Query.Flat(avoid);
            bool Free(Vector3 p)=>ValidTeleportPoint(p,radius)&&Query.Flat(p-avoid).sqrMagnitude>=avoidRadius*avoidRadius;
            if(Free(at))return at;
            for(float ring=.5f;ring<=4f;ring+=.5f)
                for(int i=0;i<16;i++){Vector3 p=at+Quaternion.Euler(0f,i*22.5f,0f)*Vector3.forward*ring;if(Free(p))return p;}
            return Query.Flat(fallback);
        }

        void RecordPlayerTrail()
        {
            if(!player||playerTrailCount>0&&autoClock-playerTrailTimes[playerTrailHead]<PlayerTrailSample)return;
            playerTrailHead=(playerTrailHead+1)%playerTrail.Length;
            playerTrail[playerTrailHead]=Query.Flat(player.Position);playerTrailTimes[playerTrailHead]=autoClock;
            playerTrailCount=Mathf.Min(playerTrail.Length,playerTrailCount+1);
        }

        /// <summary>Where you stood this many world seconds ago (the oldest point kept when the record is shorter).</summary>
        public Vector3 PlayerPositionAgo(float seconds)
        {
            if(playerTrailCount==0)return player?Query.Flat(player.Position):Vector3.zero;
            float when=autoClock-seconds;int n=playerTrail.Length,index=playerTrailHead;
            for(int i=0;i<playerTrailCount;i++)
            {
                index=((playerTrailHead-i)%n+n)%n;
                if(playerTrailTimes[index]<=when)break;
            }
            return playerTrail[index];
        }

        // ───── Energy Ring ─────

        /// <summary>Deflect (Q90-Q93, Q99): an always-on ring of the weapon's range round you. Enemies inside it with no
        /// wall between move at deflectSlow (bosses too) and take one light hit per cycle carrying the weapon's element (Mk and cards
        /// scale it). An enemy projectile coming into the ring is deflected and fizzles with deflectChance, rolled once per shot
        /// (DeflectsShot); one of your own bullets or lobs leaving the ring turns by up to deflectOwnAngle degrees with the same chance,
        /// once per shot.</summary>
        void TickDeflect(RunWeapon w,float hit,float range,float cycle)
        {
            UpdateDeflectSlow(w,range);
            if(w.cooldown<=0f)
            {
                w.cooldown=cycle;
                autoTargets.Clear();
                foreach(var c in Combatant.All)if(c!=player&&c.IsAlive&&c.Team==Team.Enemy&&InField(c,range))autoTargets.Add(c);
                foreach(var c in autoTargets)Damage(w,c,hit,0,Query.Flat(c.Position-player.Position).normalized);
                autoTargets.Clear();
            }
            DeflectOwnShots(range);
        }
        bool InField(Combatant body,float range)=>Query.Flat(body.Position-player.Position).magnitude<=range+body.radius&&!WallBlocks(player.Position,body.Position);

        void UpdateDeflectSlow(RunWeapon w,float range)
        {
            if(deflectSlowKey!=w)ClearDeflectSlow();
            deflectSlowKey=w;deflectPruned.Clear();
            foreach(var pair in deflectSlowed)if(!pair.Key||!pair.Key.IsAlive||pair.Key.SpawnVersion!=pair.Value||!InField(pair.Key,range))deflectPruned.Add(pair.Key);
            foreach(var body in deflectPruned){if(body)body.RemoveMovementModifier(w);deflectSlowed.Remove(body);}
            deflectPruned.Clear();
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||body.Team!=Team.Enemy||deflectSlowed.ContainsKey(body)||!InField(body,range))continue;
                body.AddMovementModifier(w,upgrades.deflectSlow);deflectSlowed.Add(body,body.SpawnVersion);
            }
        }
        void ClearDeflectSlow()
        {
            if(deflectSlowKey!=null)foreach(var body in deflectSlowed.Keys)if(body)body.RemoveMovementModifier(deflectSlowKey);
            deflectSlowed.Clear();deflectSlowKey=null;
        }

        /// <summary>Deflect (Q91): every enemy shot asks this each physics step before it moves (EnemyBolt.FixedUpdate),
        /// so it is judged before it can hit anything. A hostile shot inside the ring with no wall between rolls deflectChance once per
        /// launch; on success it fizzles here and the answer is true.</summary>
        public bool DeflectsShot(EnemyBolt bolt)
        {
            RunWeapon ring=null;
            if(Auto!=null)foreach(var w in Auto)if(w!=null&&w.form==WeaponForm.ShieldRing&&w.variant==0){ring=w;break;}
            if(ring==null||!player||!player.IsAlive||!bolt||!bolt.Hostile)return false;
            if(rolledBolts.TryGetValue(bolt,out int rolled)&&rolled==bolt.Launches)return false;
            Vector3 at=bolt.transform.position;
            if(Query.Flat(at-player.Position).magnitude>FieldRadius(ring)||WallBlocks(player.Position,at))return false;
            rolledBolts[bolt]=bolt.Launches;
            if(!Roll(upgrades.deflectChance))return false;
            Vector3 away=Quaternion.Euler(0f,(UnityEngine.Random.value<.5f?-1f:1f)*UnityEngine.Random.Range(35f,80f),0f)*bolt.Direction;
            Fx.Burst(at,away,DeflectColor,5,9f,20f,.12f);
            bolt.Burn();
            WeaponSound(ring);
            return true;
        }

        /// <summary>How many of your own shots the Deflect ring has knocked off course (a turn of any size, zero included).</summary>
        public int OwnShotsDeflected{get;private set;}

        void DeflectOwnShots(float range)
        {
            if(!AttackPool.Instance)return;
            foreach(var a in AttackPool.Instance.actors)
            {
                if(!a||!a.gameObject.activeSelf||a.owner!=player||!Deflectable(a)||BattlefieldFrozen(a.gameObject))continue;
                bool inside=Query.Flat(a.transform.position-player.Position).magnitude<=range;
                // First sight. A straight shot still carries its spawn point (a.point): one fired inside the ring that already left it
                // between two frames (a fast round at a low frame rate) crosses now; one fired outside it (a deployed minigun) never does.
                if(!crossWatch.TryGetValue(a,out var seen)||seen.sequence!=a.Sequence)
                    crossWatch[a]=seen=(a.Sequence,a.mode==AttackMode.Lob?inside:Query.Flat(a.point-player.Position).magnitude<=range);
                if(!seen.inside||inside)continue;
                crossWatch[a]=(a.Sequence,false);
                if(!Roll(upgrades.deflectChance))continue;
                float angle=UnityEngine.Random.Range(-upgrades.deflectOwnAngle,upgrades.deflectOwnAngle);
                // A lob turns its unflown path and landing about where it is; R7 moves a landing in a wall, a hole or off the map.
                if(a.mode==AttackMode.Lob){var landing=LandingPoint(Query.Flat(a.transform.position)+Quaternion.Euler(0f,angle,0f)*Query.Flat(a.point-a.transform.position));if(!landing.HasValue)continue;a.BendArc(landing.Value);}
                else a.direction=Quaternion.Euler(0f,angle,0f)*a.direction;
                OwnShotsDeflected++;
                Fx.Pop(a.transform.position,DeflectColor,.45f);
            }
        }
        /// <summary>Q93: visible bullets and lobs count (Rifle, Gatling, Needler, Focus pellets, ice shards, Barrage
        /// rockets, Gunner bullets, Wildfire fireballs, Cloud pods); the Railgun's slug, knives and hooks do not.</summary>
        static bool Deflectable(AttackActor a)=>a.weapon!=null&&(a.mode==AttackMode.Lob||(a.mode is AttackMode.Bolt or AttackMode.Swarm or AttackMode.Shard)&&a.weapon.form!=WeaponForm.Railgun);
        /// <summary>True with this chance; in ten-thousandths, so 1 always hits and 0 never does (Random.value can return 1).</summary>
        static bool Roll(float chance)=>UnityEngine.Random.Range(0,10000)<chance*10000f;

        /// <summary>Drain (Q94-Q99): DrainShieldWait seconds without an enemy attack raise an Energy Shield on you;
        /// EnergyShieldBlocks spends it.</summary>
        void TickDrain(RunWeapon w,float dt,float hit)
        {
            if(drainReleaseFrom==w){drainReleaseFrom=null;ReleaseDrainElement(w,hit);}
            w.drainOverloadLeft=Mathf.Max(0f,w.drainOverloadLeft-dt);
            if(w.drainShieldUp)return;
            w.drainQuiet+=dt;
            if(w.drainQuiet<DrainShieldWait(w))return;
            // A shield formed again ends the overload (a high Mk can form it within drainOverloadSeconds): it takes the next attack whole.
            w.drainShieldUp=true;w.drainOverloadLeft=0f;
            V5WeaponAudio.Instance?.Play(V5Sfx.RingShieldUp);
        }

        /// <summary>Drain (Q94-Q97): Combatant.ReceiveDamage asks this for every enemy attack on you before it lands
        /// (the attacks hit protection covers; burn, poison, walls and your own weapons never come here). The attack restarts the
        /// shield's wait. A raised Energy Shield takes it whole (no damage, push, stun or stacks), breaks and gives drainBlockEnergy; the
        /// first attack in the drainOverloadSeconds after that costs drainOverloadEnergy, once per break.</summary>
        public bool EnergyShieldBlocks()
        {
            RunWeapon drain=null;
            foreach(var w in Auto)if(w!=null&&w.form==WeaponForm.ShieldRing&&w.variant==1){drain=w;break;}
            if(drain==null||!player||!player.IsAlive)return false;
            drain.drainQuiet=0f;
            if(drain.drainShieldUp)
            {
                drain.drainShieldUp=false;drain.drainOverloadLeft=Mathf.Max(0f,upgrades.drainOverloadSeconds);drain.drainOverloadPaid=false;
                if(progression)progression.GainEnergy(upgrades.drainBlockEnergy);
                // An infused Drain releases its element on the next tick, outside this ReceiveDamage, so an enemy's reaction to it
                // cannot hit the player again inside the same attack (P23).
                if(drain.element!=ElementKind.None)drainReleaseFrom=drain;
                WeaponSound(drain);
                Fx.Ring(player.Position,player.radius+.6f,new Color(.6f,1f,.95f,.9f));
                return true;
            }
            if(drain.drainOverloadLeft>0f&&!drain.drainOverloadPaid)
            {
                drain.drainOverloadPaid=true;
                if(progression)progression.GainEnergy(-upgrades.drainOverloadEnergy);
                Fx.Pop(Query.AtCastHeight(player.Position),new Color(1f,.45f,.75f,.9f),1.4f);
            }
            return false;
        }

        RunWeapon drainReleaseFrom;
        /// <summary>Drain Infusion (document 5.7): each block releases one hit of the Drain's element (damageB,
        /// scaled like the weapon) on every enemy within drainInfuseRadius with no wall between. Fire, Poison, Frost and Lightning stack
        /// on them; Earth, Light and Void pay you through that hit like any infused hit.</summary>
        void ReleaseDrainElement(RunWeapon w,float hit)
        {
            if(w.element==ElementKind.None||!player||!player.IsAlive)return;
            float radius=Mathf.Max(0f,upgrades.drainInfuseRadius);
            autoTargets.Clear();
            foreach(var c in Combatant.All)if(c!=player&&c.IsAlive&&c.Team==Team.Enemy&&InField(c,radius))autoTargets.Add(c);
            foreach(var c in autoTargets)Damage(w,c,hit,0,Query.Flat(c.Position-player.Position).normalized);
            autoTargets.Clear();
            Fx.Ring(player.Position,radius,ElementCombat.ColorFor(w.element));
        }

        /// <summary>Slot text of the auto weapons: the Gunner's rounds left or its drop countdown, the Drain's shield state.</summary>
        string AutoSlotDetail(RunWeapon w)
        {
            if(w.form==WeaponForm.SentryDrone&&w.variant==0)
                return w.droneDownLeft>0f?ArenaText.Pair($" · 掉落 · 踩它或等 {w.droneDownLeft:0.0} 秒",$" · Down · step on it or {w.droneDownLeft:0.0}s")
                    :ArenaText.Pair($" · 剩 {GunnerClip-w.droneShots} 发",$" · {GunnerClip-w.droneShots} rounds");
            if(w.form==WeaponForm.ShieldRing&&w.variant==1)
                return w.drainOverloadLeft>0f&&!w.drainOverloadPaid?ArenaText.Pair($" · 过载 {w.drainOverloadLeft:0.0} 秒",$" · Overload {w.drainOverloadLeft:0.0}s")
                    :w.drainShieldUp?ArenaText.Pair(" · 能量盾就绪"," · Energy Shield up")
                    :ArenaText.Pair($" · 能量盾 {Mathf.Max(0f,DrainShieldWait(w)-w.drainQuiet):0.0} 秒",$" · Shield in {Mathf.Max(0f,DrainShieldWait(w)-w.drainQuiet):0.0}s");
            return "";
        }

        /// <summary>Void trial: the battlefield keeps its dropped drone and its throw in flight (the thrown body is frozen with it); the
        /// trial gets a drone flying by you with a full magazine. Coming back restores them.</summary>
        void FreezeAutoWeaponsForTrial(bool freeze)
        {
            // The knock memories run on autoClock, which the trial winds back on the way out: start them afresh both ways. The shot
            // memories are keyed by launch, so the battlefield's suspended shots keep theirs and are not rolled twice.
            ClearDeflectSlow();orbKnockReady.Clear();corpseKnockReady.Clear();playerTrailCount=0;drainReleaseFrom=null;
            if(freeze)
            {
                trialAutoStates.Clear();
                foreach(var w in Auto)
                {
                    if(w==null)continue;
                    trialAutoStates[w]=(w.dronePoint,w.droneShots,w.droneDownLeft,w.whirlFrom,w.whirlTo,w.whirlTotal);
                    w.droneShots=0;w.droneDownLeft=0f;
                }
                return;
            }
            foreach(var pair in trialAutoStates)
            {
                var w=pair.Key;var s=pair.Value;
                w.dronePoint=s.drop;w.droneShots=s.shots;w.droneDownLeft=s.down;w.whirlFrom=s.from;w.whirlTo=s.to;w.whirlTotal=s.total;
            }
            trialAutoStates.Clear();
        }

        /// <summary>Wave end: the thrown enemy is set down, the drone comes back to you with a full magazine, the Drain's overload ends
        /// and nobody stays slowed.</summary>
        void ClearAutoMotion()
        {
            foreach(var w in Auto)if(w!=null){EndWhirl(w);w.droneShots=0;w.droneDownLeft=0f;w.drainOverloadLeft=0f;}
            foreach(var times in autoHitTimes)times.Clear();
            ClearDeflectSlow();rolledBolts.Clear();crossWatch.Clear();orbKnockReady.Clear();corpseKnockReady.Clear();trialAutoStates.Clear();
            playerTrailCount=0;
        }
    }
}
