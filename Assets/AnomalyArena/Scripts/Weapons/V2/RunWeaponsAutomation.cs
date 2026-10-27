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

        readonly List<Combatant> autoTargets=new();

        readonly Dictionary<ExperienceOrb,float> orbKnockReady=new();
        readonly Dictionary<Enemy,(int version,float ready)> corpseKnockReady=new();

        readonly Dictionary<Combatant,int> deflectSlowed=new();
        readonly List<Combatant> deflectPruned=new();
        RunWeapon deflectSlowKey;
        readonly Dictionary<EnemyBolt,int> rolledBolts=new();
        readonly Dictionary<AttackActor,(int sequence,bool inside)> crossWatch=new();

        readonly Vector3[] playerTrail=new Vector3[128];readonly float[] playerTrailTimes=new float[128];int playerTrailHead,playerTrailCount;
        const float PlayerTrailSample=.05f;

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

        public int GunnerClip=>Mathf.Max(1,upgrades.gunnerClip);

        public Vector3 DroneHover()=>player?player.Position+upgrades.droneHoverOffset:Vector3.zero;

        public bool DroneDown(RunWeapon w)=>w!=null&&w.droneDownLeft>0f;

        public float FieldRadius(RunWeapon w){var d=w!=null&&roster?roster.Get(w.form):null;return d==null?0f:(w.variant==0?d.rangeA:d.rangeB)*w.Reach;}

        public float DrainShieldWait(RunWeapon w)=>Mathf.Max(0f,upgrades.drainShieldWait)/Mathf.Max(.01f,MkDamage(w));
        void TickAutoWeapons(float dt)
        {
            autoClock+=dt;
            if(!autoMarks){autoMarks=GetComponent<V2AutoWeaponMarks>();if(!autoMarks)autoMarks=gameObject.AddComponent<V2AutoWeaponMarks>();autoMarks.weapons=this;}
            RecordPlayerTrail();

            float rate=Mathf.Max(.01f,WeaponCooldownRate);
            bool deflect=false;
            for(int slot=0;slot<Auto.Length;slot++)
            {
                var w=Auto[slot];if(w==null)continue;

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
                else TickDrain(w,dt);
            }

            if(!deflect)ClearDeflectSlow();
        }

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

            if(DroneBolt(w,muzzle,dir.normalized,hit,range+Query.Flat(muzzle-player.Position).magnitude)&&++w.droneShots>=GunnerClip)DropDrone(w);
        }

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
            WeaponSound(w);return a;
        }

        void DropDrone(RunWeapon w)
        {
            w.droneDownLeft=Mathf.Max(.01f,upgrades.gunnerAutoReturn);
            w.dronePoint=NearestValidPoint(DroneHover(),upgrades.droneRadius,player.Position,player.Position,player.radius+upgrades.droneRadius+.05f);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponJam,w.dronePoint);
        }

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

        Vector3 WhirlLanding(Combatant target)=>NearestValidPoint(OffYourBody(PlayerPositionAgo(upgrades.whirlAfterimageSeconds),target),target.radius,Query.Flat(target.Position),player.Position,BodyClearance(target));

        float BodyClearance(Combatant body)=>player.radius+body.radius+.1f;

        Vector3 OffYourBody(Vector3 spot,Combatant body)
        {
            Vector3 away=Query.Flat(spot-player.Position);float clear=BodyClearance(body);
            if(away.sqrMagnitude>=clear*clear)return Query.Flat(spot);
            if(away.sqrMagnitude<1e-4f)away=Query.Flat(body.Position-player.Position);
            if(away.sqrMagnitude<1e-4f)away=Vector3.forward;
            return Query.Flat(player.Position)+away.normalized*clear;
        }

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

        void EndWhirl(RunWeapon w)
        {
            var body=w.whirled;w.whirled=null;w.whirlLeft=0f;
            if(!body||!body.IsAlive)return;
            TeleportBody(body,player?NearestValidPoint(body.Position,body.radius,w.whirlFrom,player.Position,BodyClearance(body)):NearestValidPoint(body.Position,body.radius,w.whirlFrom));
        }

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

        public int OwnShotsDeflected{get;private set;}

        void DeflectOwnShots(float range)
        {
            if(!AttackPool.Instance)return;
            foreach(var a in AttackPool.Instance.actors)
            {
                if(!a||!a.gameObject.activeSelf||a.owner!=player||!Deflectable(a)||BattlefieldFrozen(a.gameObject))continue;
                bool inside=Query.Flat(a.transform.position-player.Position).magnitude<=range;

                if(!crossWatch.TryGetValue(a,out var seen)||seen.sequence!=a.Sequence)
                    crossWatch[a]=seen=(a.Sequence,a.mode==AttackMode.Lob?inside:Query.Flat(a.point-player.Position).magnitude<=range);
                if(!seen.inside||inside)continue;
                crossWatch[a]=(a.Sequence,false);
                if(!Roll(upgrades.deflectChance))continue;
                float angle=UnityEngine.Random.Range(-upgrades.deflectOwnAngle,upgrades.deflectOwnAngle);

                if(a.mode==AttackMode.Lob){var landing=LandingPoint(Query.Flat(a.transform.position)+Quaternion.Euler(0f,angle,0f)*Query.Flat(a.point-a.transform.position));if(!landing.HasValue)continue;a.BendArc(landing.Value);}
                else a.direction=Quaternion.Euler(0f,angle,0f)*a.direction;
                OwnShotsDeflected++;
                Fx.Pop(a.transform.position,DeflectColor,.45f);
            }
        }

        static bool Deflectable(AttackActor a)=>a.weapon!=null&&(a.mode==AttackMode.Lob||(a.mode is AttackMode.Bolt or AttackMode.Swarm or AttackMode.Shard)&&a.weapon.form!=WeaponForm.Railgun);

        static bool Roll(float chance)=>UnityEngine.Random.Range(0,10000)<chance*10000f;

        void TickDrain(RunWeapon w,float dt)
        {
            w.drainOverloadLeft=Mathf.Max(0f,w.drainOverloadLeft-dt);
            if(w.drainShieldUp)return;
            w.drainQuiet+=dt;
            if(w.drainQuiet<DrainShieldWait(w))return;

            w.drainShieldUp=true;w.drainOverloadLeft=0f;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.NpcShield,player.Position);
        }

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

        string AutoSlotDetail(RunWeapon w)
        {
            if(w.form==WeaponForm.SentryDrone&&w.variant==0)
                return w.droneDownLeft>0f?ArenaText.Pair($" · Down · step on it or {w.droneDownLeft:0.0}s",$" · Down · step on it or {w.droneDownLeft:0.0}s")
                    :ArenaText.Pair($" · {GunnerClip-w.droneShots} rounds",$" · {GunnerClip-w.droneShots} rounds");
            if(w.form==WeaponForm.ShieldRing&&w.variant==1)
                return w.drainOverloadLeft>0f&&!w.drainOverloadPaid?ArenaText.Pair($" · Overload {w.drainOverloadLeft:0.0}s",$" · Overload {w.drainOverloadLeft:0.0}s")
                    :w.drainShieldUp?ArenaText.Pair(" · Energy Shield up"," · Energy Shield up")
                    :ArenaText.Pair($" · Shield in {Mathf.Max(0f,DrainShieldWait(w)-w.drainQuiet):0.0}s",$" · Shield in {Mathf.Max(0f,DrainShieldWait(w)-w.drainQuiet):0.0}s");
            return "";
        }

        void FreezeAutoWeaponsForTrial(bool freeze)
        {

            ClearDeflectSlow();orbKnockReady.Clear();corpseKnockReady.Clear();playerTrailCount=0;
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

        void ClearAutoMotion()
        {
            foreach(var w in Auto)if(w!=null){EndWhirl(w);w.droneShots=0;w.droneDownLeft=0f;w.drainOverloadLeft=0f;}
            foreach(var times in autoHitTimes)times.Clear();
            ClearDeflectSlow();rolledBolts.Clear();crossWatch.Clear();orbKnockReady.Clear();corpseKnockReady.Clear();trialAutoStates.Clear();
            playerTrailCount=0;
        }
    }
}
