using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    [Serializable]
    public sealed class WeaponUpgradeTuning
    {
        public float swapProtection = .5f, recallTimeout = 6f, stickyManualDelay = .3f;
        public float wildfireEndPush = 3f, blizzardLinger = 2f;
        [Min(1)] public int shatterLimit = 3;
        public float capacitorLeak = 1f, cloudDrift = 1f;
        public float executeFollowRange = 4f, executeFollowDamage = .5f;
        public float spinRetainTime = 1f, meltdownAngle = 90f, swarmMineDuration = 1f;
        [Header("V4 backfires — candidate values")]
        [Tooltip("Flame Stream fuel tank: a hit taken while the trigger is held bursts the tank (radius, damage, Fire stacks on you, seconds before it can burst again).")]
        public float fuelTankRadius = 2.5f;
        public float fuelTankDamage = 4f, fuelTankSelfFire = 3f, fuelTankCooldown = 1.5f;
        [Tooltip("Shatter fires three shards; the side shards leave the aim line by this many degrees.")]
        public float shatterSpread = 12f;
        [Min(1f), Tooltip("Shatter: flight speed of each ice shard, cells per second. The shards are real projectiles that stop on the first body or wall.")]
        public float shardSpeed = 40f;
        public float iceRadius = .9f, iceLife = 5f, iceFrostPerSecond = 4f;
        public float blizzardConeAngle = 50f, blizzardMaxRange = 7f, blizzardFrostPerTick = .8f, blizzardFrozenHold = .6f;
        public float chainReturnRadius = 3f, chainReturnDamage = 3f;
        [Min(0)] public int sporePods = 3;
        public float sporePodRadius = .7f, sporePodLife = 12f, sporeBurstRadius = 1.8f, sporeBurstLife = 3f;
        public float spinUpMoveFloor = .8f, spinUpFreeTurn = 1080f, spinUpFullTurn = 90f;
        public float gunnerEnergyPerShot = 1f;
        [Range(0f, 1f)] public float leechHealShare = .5f;
        [Header("10-21 design alignment — candidate values")]
        [Min(0f), Tooltip("Reverse Shot Tame: seconds after firing before the reverse round turns around and flies along your aim.")]
        public float reverseTurnDelay = .3f;
        [Range(0f, 1f), Tooltip("Scatter Blast: damage share at the far edge of the cone. Point-blank deals 100%, falling off linearly with distance.")]
        public float scatterFalloffMin = .4f;
        [Min(0f), Tooltip("Scatter Blast: reload seconds once the two-round clip is empty (scaled like other reloads).")]
        public float scatterReload = 1.2f;
        [Range(0f, 1f), Tooltip("Scatter Blast backfire: chance that a reload jams. Tame never jams.")]
        public float scatterJamChance = .25f;
        [Min(0f), Tooltip("Scatter Blast backfire: extra repair seconds added to a jammed reload (scaled like other reloads).")]
        public float scatterJamRepair = 2f;
        [Min(.01f), Tooltip("Scatter Focus: seconds of holding for a full charge.")]
        public float focusChargeTime = 1f;
        [Min(1), Tooltip("Scatter Focus: bolts in the released burst; the weapon's damage is split evenly between them.")]
        public int focusPellets = 6;
        [Min(0f), Tooltip("Scatter Focus: seconds between bolts of the burst.")]
        public float focusBurstInterval = .05f;
        [Range(0f, 1f), Tooltip("Scatter Focus: damage share of an uncharged release; a full charge deals 100%.")]
        public float focusMinChargeDamage = .4f;
        [Min(1f), Tooltip("Scatter Focus: bolt speed in units per second.")]
        public float focusPelletSpeed = 60f;
        [Min(0f), Tooltip("Scatter Focus backfire: recoil knockback in units on an uncharged release.")]
        public float focusRecoilMin = 2f;
        [Min(0f), Tooltip("Scatter Focus backfire: recoil knockback in units on a full charge. It is a real knockback (a wall behind you hurts); Tame makes it a harmless push.")]
        public float focusRecoilMax = 6f;
        [Range(0f, 1f), Tooltip("Scatter Focus Twist: damage share a bolt keeps after piercing its first target.")]
        public float focusPierceRetention = .6f;
        [Range(.05f, 1f), Tooltip("Blizzard backfire: your movement multiplier while you spray. Tame removes it.")]
        public float blizzardSelfSlow = .6f;
        [Min(1), Tooltip("Needler backfire: needles fired per HP cost. Tame removes the cost.")]
        public int needlerHpPerNeedles = 8;
        [Min(0f), Tooltip("Needler backfire: HP you lose each time; it ignores Shield.")]
        public float needlerHpCost = 1f;
        [Range(0f, 1f), Tooltip("Execute: starting HP share below which a non-boss enemy is executed.")]
        public float executeThreshold = .4f;
        [Min(0f), Tooltip("Execute: threshold gained for every kill made with this weapon.")]
        public float executeThresholdPerKill = .02f;
        [Range(0f, 1f), Tooltip("Execute: highest threshold the kills can reach.")]
        public float executeThresholdCap = .7f;
        [Min(0f), Tooltip("Execute backfire: max HP you lose per execution. Tame removes it.")]
        public float executeMaxHpLoss = 1f;
        [Min(0f), Tooltip("Execute backfire: max HP never drops below this.")]
        public float executeMaxHpFloor = 10f;
        [Range(0f, 180f), Tooltip("Barrage backfire: widest rocket angle from the aim in degrees (180 = straight back).")]
        public float barrageMaxAngle = 180f;
        [Min(.01f), Tooltip("Barrage backfire: angle = ±max × r^exponent with r uniform in [0,1]. Higher keeps more rockets near the aim (3: about 55% within 30°).")]
        public float barrageAngleExponent = 3f;
    }

    public enum GroundHazardKind { IcePatch, SporePod }

    public struct GroundHazard
    {
        public GroundHazardKind kind;
        public Vector3 position;
        public float radius, timeLeft, life;
        public RunWeapon weapon;
    }

    public sealed partial class RunWeapons
    {
        [Header("Approved anomaly upgrades — tuning")]
        public WeaponUpgradeTuning upgrades = new();
        readonly HashSet<Combatant> upgradeHits = new();
        readonly List<Combatant> shatterCenters = new();
        readonly List<GroundHazard> hazards = new();
        public IReadOnlyList<GroundHazard> Hazards => hazards;
        struct TrackedCloud { public AttackActor actor; public int sequence; public RunWeapon weapon; public Vector3 position; public float radius; public bool landed; }
        readonly List<TrackedCloud> sporeClouds = new();
        Combatant watchedPlayer;

        void ReleaseChannel(RunWeapon w)
        {
            var field=w.channelledField;
            if(!field||BattlefieldFrozen(field.gameObject))return;
            if(w.form==WeaponForm.CryoCannon&&w.variant==1&&w.Has(UpgradeAxis.Twist))
            {
                if(field.followCursor){field.followCursor=false;field.remaining=upgrades.blizzardLinger;}
            }
            else field.Release();
        }

        void StopTrigger(RunWeapon w)
        {
            if(w==null)return;
            if(w.form==WeaponForm.Minigun&&w.variant==0&&w.Has(UpgradeAxis.Twist)&&w.heldTime>0)
            {w.spinStored=w.heldTime;w.spinRetentionLeft=upgrades.spinRetainTime;}

            if(w.form==WeaponForm.CryoCannon&&w.variant==1&&w.Has(UpgradeAxis.Twist)&&w.heldTime>0&&player)
            {
                var d=roster.Get(w.form);
                Field(w,AttackMode.Field,player.Position+player.AimDirection*Mathf.Min(upgrades.blizzardMaxRange,d.rangeB*w.Reach)*.6f,d.damageB*DamageScale(w),0,2.5f*w.Reach,upgrades.blizzardLinger,0,false);
            }
            w.heldTime=0;
            if(player)player.RemoveMovementModifier(w);
            ReleaseChannel(w);
        }

        void ScatterBlast(RunWeapon w,float range,float hit)
        {
            WeaponSound(w);
            Fx.Cone(player.Position,player.AimDirection,range,60,w.element);
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||!InCone(body,range,60))continue;
                Vector3 delta=Query.Flat(body.Position-player.Position),push=delta.normalized;
                if(w.Has(UpgradeAxis.Twist))
                {push=player.AimDirection*Vector3.Dot(delta,player.AimDirection)-delta;push=push.sqrMagnitude>.0001f?push.normalized:player.AimDirection;}
                float falloff=Mathf.Lerp(1f,upgrades.scatterFalloffMin,Mathf.Clamp01(delta.magnitude/Mathf.Max(.01f,range)));
                Damage(w,body,hit*falloff,10,push);
            }
            if(SpendRound(w,upgrades.scatterReload)&&!w.Has(UpgradeAxis.Tame)&&UnityEngine.Random.value<upgrades.scatterJamChance)
            {w.reload+=upgrades.scatterJamRepair*CooldownScale(w);w.jammed=true;V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponJam,player.Position);}
        }

        void Focus(RunWeapon w,Vector3 aim,float damage)
        {
            float charge=Mathf.Clamp01(w.charge);
            int pellets=Mathf.Max(1,upgrades.focusPellets);
            w.burstLeft=pellets;w.burstTimer=0f;w.burstDirection=aim;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponFocusRelease,player.Position);
            w.burstDamage=damage/pellets*Mathf.Lerp(upgrades.focusMinChargeDamage,1f,charge);
            float recoil=Mathf.Lerp(upgrades.focusRecoilMin,upgrades.focusRecoilMax,charge);

            if(w.Has(UpgradeAxis.Tame))player.Push(-aim,recoil);else player.Knockback(-aim,recoil,KnockKind.Hit);
            TickBurst(w,0f);
        }

        void TickBurst(RunWeapon w,float dt)
        {
            if(!player||!player.IsAlive||!roster){w.burstLeft=0;return;}
            var d=roster.Get(w.form);float range=(d!=null?d.rangeB:20f)*w.Reach;
            bool twist=w.Has(UpgradeAxis.Twist);
            w.burstTimer-=dt;
            while(w.burstLeft>0&&w.burstTimer<=0f)
            {
                w.burstLeft--;w.burstTimer+=upgrades.focusBurstInterval;
                var a=Bolt(w,w.burstDirection,w.burstDamage,0,range,upgrades.focusPelletSpeed,twist);

                if(a&&twist){a.piercesLeft=1;a.pierceDamageRetention=upgrades.focusPierceRetention;}
            }
        }

        float BarrageAngle()
        {
            float r=UnityEngine.Random.value;
            return (UnityEngine.Random.value<.5f?-1f:1f)*upgrades.barrageMaxAngle*Mathf.Pow(r,Mathf.Max(.01f,upgrades.barrageAngleExponent));
        }

        void NeedlerCost(RunWeapon w)
        {
            if(w.Has(UpgradeAxis.Tame)||!player||++w.needlesFired<Mathf.Max(1,upgrades.needlerHpPerNeedles))return;
            w.needlesFired=0;
            player.ReceiveDamage(new DamageInfo{amount=upgrades.needlerHpCost,source=DamageSource.Burn,ignoreShield=true,dealer=player});                                                
        }

        void BlizzardChill(RunWeapon w)
        {
            if(!player)return;
            if(w==Active&&w.heldTime>0f&&!w.Has(UpgradeAxis.Tame))player.AddMovementModifier(w,upgrades.blizzardSelfSlow);
            else player.RemoveMovementModifier(w);
        }

        void RecallBlast(RunWeapon w,Vector3 from,Vector3 to,float area,float hit)
        {
            WeaponSound(w,WeaponAudioPhase.Explosion);
            upgradeHits.Clear();
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive)continue;
                bool destination=Query.Flat(body.Position-to).magnitude<=area+body.radius;
                bool departure=w.Has(UpgradeAxis.Twist)&&Query.Flat(body.Position-from).magnitude<=area+body.radius;
                if((destination||departure)&&upgradeHits.Add(body))Damage(w,body,hit,5,Query.Flat(body.Position-(destination?to:from)).normalized);
            }
            Fx.Ring(to,area,ElementCombat.ColorFor(w.element));
            if(w.Has(UpgradeAxis.Twist))Fx.Ring(from,area,ElementCombat.ColorFor(w.element));
        }

        void Stream(RunWeapon w,float range,float hit)
        {
            WeaponSound(w);
            upgradeHits.Clear();
            Vector3 origin=player.Position,dir=player.AimDirection;
            bool wall=Physics.Raycast(Query.AtCastHeight(origin),dir,out var impact,range,GameManager.WallMask,QueryTriggerInteraction.Ignore);
            float first=wall?impact.distance:range;
            StreamSegment(w,origin,dir,first,hit);
            if(wall&&w.Has(UpgradeAxis.Twist)&&range>first)
                StreamSegment(w,Query.Flat(impact.point)+Query.Flat(impact.normal)*.05f,Query.Flat(Vector3.Reflect(dir,impact.normal)).normalized,range-first,hit);
        }

        void StreamSegment(RunWeapon w,Vector3 origin,Vector3 dir,float range,float hit)
        {
            Fx.Cone(origin,dir,range,40,w.element);
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||upgradeHits.Contains(body))continue;
                Vector3 delta=Query.Flat(body.Position-origin);
                if(delta.magnitude>range+body.radius||Vector3.Angle(dir,delta)>20||Physics.Linecast(Query.AtCastHeight(origin),Query.AtCastHeight(body.Position),GameManager.WallMask))continue;
                upgradeHits.Add(body);Damage(w,body,hit,0,delta.normalized);
            }
        }

        void Shatter(RunWeapon w,Combatant first,float hit)
        {
            WeaponSound(w);
            bool frozen=ElementStatus.For(first).Frozen;
            Damage(w,first,hit*(frozen?2:1),0,player.AimDirection);

            if(!frozen||!w.Has(UpgradeAxis.Twist))return;
            upgradeHits.Clear();upgradeHits.Add(first);shatterCenters.Clear();shatterCenters.Add(first);
            int limit=Mathf.Max(1,upgrades.shatterLimit);
            for(int i=0;i<shatterCenters.Count&&i<limit;i++)
            {
                Vector3 center=shatterCenters[i].Position;
                Fx.Ring(center,2.5f,ElementCombat.ColorFor(w.element));
                foreach(var body in Combatant.All)
                {
                    if(body==player||!body.IsAlive||upgradeHits.Contains(body)||Query.Flat(body.Position-center).magnitude>2.5f+body.radius)continue;
                    bool chain=ElementStatus.For(body).Frozen;
                    upgradeHits.Add(body);Damage(w,body,8*DamageScale(w),0,Query.Flat(body.Position-center).normalized);
                    if(chain&&shatterCenters.Count<limit)shatterCenters.Add(body);
                }
            }
        }

        public float ExecuteThreshold(RunWeapon w)=>w==null?upgrades.executeThreshold:Mathf.Min(upgrades.executeThresholdCap,upgrades.executeThreshold+w.executeBonus);
        void RaiseExecuteThreshold(RunWeapon w)=>w.executeBonus=Mathf.Min(w.executeBonus+upgrades.executeThresholdPerKill,Mathf.Max(0f,upgrades.executeThresholdCap-upgrades.executeThreshold));

        void Execute(RunWeapon w,float range,float hit)
        {
            var target=RayTarget(player.AimDirection,range);if(!target)return;
            WeaponSound(w);
            float primary=hit;
            Damage(w,target,primary,0,player.AimDirection);

            if(target.IsAlive&&target is Enemy{IsBoss:false}&&target.Hp/target.maxHp<ExecuteThreshold(w))
            {
                target.Kill();

                if(!target.IsAlive&&!w.Has(UpgradeAxis.Tame)&&progression)progression.AdjustMaxHp(-upgrades.executeMaxHpLoss,upgrades.executeMaxHpFloor);
            }
            if(target.IsAlive)return;
            RaiseExecuteThreshold(w);
            if(!w.Has(UpgradeAxis.Twist))return;
            Combatant next=null;float nearest=upgrades.executeFollowRange*upgrades.executeFollowRange;
            foreach(var body in Combatant.All)
            {
                if(body==player||body==target||!body.IsAlive)continue;
                float distance=Query.Flat(body.Position-target.Position).sqrMagnitude;
                if(distance>=nearest||Physics.Linecast(Query.AtCastHeight(target.Position),Query.AtCastHeight(body.Position),GameManager.WallMask))continue;
                next=body;nearest=distance;
            }
            if(!next)return;
            Damage(w,next,primary*upgrades.executeFollowDamage,0,Query.Flat(next.Position-target.Position).normalized);
            if(!next.IsAlive)RaiseExecuteThreshold(w);
        }

        void ReturnCurrent(RunWeapon w,int jumps)
        {
            if(jumps<=0)return;
            if(!w.Has(UpgradeAxis.Tame))ElementStatus.For(player)?.Add(ElementKind.Lightning,jumps,player);
            foreach(var body in Combatant.All)
                if(body!=player&&body.IsAlive&&Query.Flat(body.Position-player.Position).magnitude<=upgrades.chainReturnRadius+body.radius)
                    Damage(w,body,upgrades.chainReturnDamage*jumps*DamageScale(w),0,Query.Flat(body.Position-player.Position).normalized);
            Fx.Ring(player.Position,upgrades.chainReturnRadius,ElementCombat.ColorFor(ElementKind.Lightning));
        }

        void ShatterFan(RunWeapon w,Vector3 aim,float range,float hit)
        {
            WeaponSound(w);
            float speed=Mathf.Max(1f,upgrades.shardSpeed);
            foreach(float angle in new[]{-upgrades.shatterSpread,0f,upgrades.shatterSpread})
            {
                Vector3 dir=Quaternion.Euler(0f,angle,0f)*aim;
                var a=AttackPool.Instance?AttackPool.Instance.Acquire():null;
                if(a)a.Configure(AttackMode.Shard,player,w,player.Position+dir*.7f,dir,hit,0,range,speed,0,range/speed+3);
            }
        }

        public void ShardStruck(AttackActor shard,Combatant body)
        {
            if(shard.weapon==null)return;
            Shatter(shard.weapon,body,shard.damage);
            ShardLanded(shard,Query.Flat(body.Position));
        }

        public void ShardLanded(AttackActor shard,Vector3 end)
        {
            if(shard.weapon==null)return;
            Fx.Ring(end,.4f,ElementCombat.ColorFor(ElementKind.Frost));
            if(!shard.weapon.Has(UpgradeAxis.Tame))AddHazard(GroundHazardKind.IcePatch,end,upgrades.iceRadius,upgrades.iceLife,shard.weapon);
        }

        void BlizzardSpray(RunWeapon w,float range,float hit)
        {
            float reach=Mathf.Min(range,upgrades.blizzardMaxRange*w.Reach);
            Fx.Cone(player.Position,player.AimDirection,reach,upgrades.blizzardConeAngle,ElementKind.Frost);
            bool any=false;
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||!InCone(body,reach,upgrades.blizzardConeAngle))continue;
                if(Physics.Linecast(Query.AtCastHeight(player.Position),Query.AtCastHeight(body.Position),GameManager.WallMask))continue;
                var status=ElementStatus.For(body);
                bool frozen=status&&status.Frozen;
                Damage(w,body,hit*(frozen?2f:1f),0,player.AimDirection);
                if(status){status.Add(ElementKind.Frost,upgrades.blizzardFrostPerTick,player);status.ExtendFreeze(upgrades.blizzardFrozenHold);}
                any=true;
            }
            if(any)WeaponSound(w);
        }

        void WatchPlayerHits()
        {
            if(watchedPlayer==player)return;
            if(watchedPlayer)watchedPlayer.AttackReceived-=OnPlayerAttacked;
            watchedPlayer=player;
            if(player)player.AttackReceived+=OnPlayerAttacked;
        }

        void OnPlayerAttacked(DamageInfo info)
        {
            var w=Active;
            if(w==null||w.form!=WeaponForm.Flamethrower||w.variant!=0||!player||!player.IsAlive)return;

            if(!inputHeld||w.heldTime<=0f||Time.time<w.tankReadyAt)return;
            w.tankReadyAt=Time.time+upgrades.fuelTankCooldown;
            WeaponSound(w,WeaponAudioPhase.Explosion);
            Area(w,player.Position,upgrades.fuelTankRadius,upgrades.fuelTankDamage*DamageScale(w),0,false);
            if(!w.Has(UpgradeAxis.Tame))ElementStatus.For(player)?.Add(ElementKind.Fire,upgrades.fuelTankSelfFire,player);
            var fx=FxPool.Active;var art=V4CombatVfx.Active;
            if(fx&&art&&art.strikeFlash)fx.Emit(player.Position+Vector3.up*.8f,new Color(1f,.75f,.45f),upgrades.fuelTankRadius*1.8f,.3f,texture:art.strikeFlash,pulse:true);
        }

        void AddHazard(GroundHazardKind kind,Vector3 at,float radius,float life,RunWeapon w)
        {
            hazards.Add(new GroundHazard{kind=kind,position=new Vector3(at.x,0f,at.z),radius=radius,timeLeft=life,life=life,weapon=w});
        }

        void TrackSporeCloud(AttackActor actor,RunWeapon w)=>sporeClouds.Add(new TrackedCloud{actor=actor,sequence=actor.Sequence,weapon=w});

        void TickSporeClouds()
        {
            for(int i=sporeClouds.Count-1;i>=0;i--)
            {
                var cloud=sporeClouds[i];
                bool live=cloud.actor&&cloud.actor.gameObject.activeSelf&&cloud.actor.Sequence==cloud.sequence;
                if(live)
                {
                    if(!cloud.actor.LobInFlight){cloud.landed=true;cloud.position=Query.Flat(cloud.actor.transform.position);cloud.radius=cloud.actor.radius;}
                    sporeClouds[i]=cloud;continue;
                }
                sporeClouds.RemoveAt(i);
                if(!cloud.landed)continue;
                for(int k=0;k<upgrades.sporePods;k++)
                {
                    Vector2 offset=UnityEngine.Random.insideUnitCircle*cloud.radius*.6f;
                    AddHazard(GroundHazardKind.SporePod,cloud.position+new Vector3(offset.x,0f,offset.y),upgrades.sporePodRadius,upgrades.sporePodLife,cloud.weapon);
                }
            }
        }

        void TickHazards(float dt)
        {
            for(int i=hazards.Count-1;i>=0;i--)
            {
                var h=hazards[i];h.timeLeft-=dt;
                if(h.timeLeft<=0f){hazards.RemoveAt(i);continue;}
                hazards[i]=h;
                if(h.kind==GroundHazardKind.IcePatch)
                {
                    if(player&&player.IsAlive&&Query.Flat(player.Position-h.position).magnitude<=h.radius+player.radius)
                        ElementStatus.For(player)?.Add(ElementKind.Frost,upgrades.iceFrostPerSecond*dt,player);
                    continue;
                }
                bool tame=h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame);
                foreach(var body in Combatant.All)
                {
                    if(!body||!body.IsAlive||body==player&&tame)continue;
                    if(Query.Flat(body.Position-h.position).magnitude>h.radius+body.radius)continue;
                    var def=roster.Get(WeaponForm.SporeLauncher);
                    var burst=Field(h.weapon,AttackMode.Field,h.position,(def!=null?def.damageA:1f)*DamageScale(h.weapon),0,upgrades.sporeBurstRadius,upgrades.sporeBurstLife,0,!tame);
                    if(burst)burst.tickInterval=.5f;
                    hazards.RemoveAt(i);break;
                }
            }
        }

        void TickCapacitor(RunWeapon w,float distance,float dt)
        {
            bool moves=player.State==CharacterState.Normal||w.Has(UpgradeAxis.Twist)&&player.State is CharacterState.Knocked or CharacterState.Thrown;
            if(moves&&distance>0){w.charge=Mathf.Min(30,w.charge+distance);if(w.charge>=30&&!w.capacitorLeaking)w.chargeFullTime+=dt;}
            else if(w.charge>=30&&!w.capacitorLeaking)w.chargeFullTime+=dt;
            if(w.chargeFullTime>=4)
            {
                if(w.Has(UpgradeAxis.Tame))w.capacitorLeaking=true;
                else{WeaponSound(w,WeaponAudioPhase.Explosion);Area(w,player.Position,5,7.5f,0,true);w.charge=0;w.chargeFullTime=0;}
            }
            if(w.capacitorLeaking){w.charge=Mathf.Max(0,w.charge-upgrades.capacitorLeak*dt);if(w.charge<=0){w.capacitorLeaking=false;w.chargeFullTime=0;}}
        }

        void Meltdown(RunWeapon w)
        {
            WeaponSound(w,WeaponAudioPhase.Explosion);
            if(w.Has(UpgradeAxis.Twist))
            {Cone(w,4,upgrades.meltdownAngle,20*DamageScale(w),0);if(!w.Has(UpgradeAxis.Tame))Damage(w,player,20*DamageScale(w),0,Vector3.zero);}
            else Area(w,player.Position,4,20*DamageScale(w),0,!w.Has(UpgradeAxis.Tame));
            w.heat=0;w.reload=3;
        }
    }
}
