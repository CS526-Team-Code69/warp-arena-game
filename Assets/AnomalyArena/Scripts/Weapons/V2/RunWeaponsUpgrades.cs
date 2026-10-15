using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    [Serializable]
    public sealed class WeaponUpgradeTuning
    {
        public float attractEndPush = 3f;
        public float suctionReleasePush = 5f, suctionSafeGap = 2f;
        public float swapProtection = .5f, recallTimeout = 6f, stickyManualDelay = .3f;
        public float wildfireEndPush = 3f, blizzardLinger = 2f;
        [Min(1)] public int shatterLimit = 3;
        public float capacitorLeak = 1f, cloudDrift = 1f;
        public float executeFollowRange = 4f, executeFollowDamage = .5f;
        public float spinRetainTime = 1f, meltdownAngle = 90f, swarmMineDuration = 1f;
    }

    public sealed partial class RunWeapons
    {
        [Header("Approved anomaly upgrades — tuning")]
        public WeaponUpgradeTuning upgrades = new();
        readonly HashSet<Combatant> upgradeHits = new();
        readonly List<Combatant> shatterCenters = new();

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
            w.heldTime=0;
            ReleaseSuction(w);
            ReleaseChannel(w);
        }

        void ReleaseSuction(RunWeapon w)
        {
            if(w.form!=WeaponForm.ScatterCannon||w.variant!=1||w.suctionTargets.Count==0)return;
            bool released=false;
            if(w.Has(UpgradeAxis.Twist))foreach(var body in w.suctionTargets)
                if(body&&body.IsAlive&&!BattlefieldFrozen(body.gameObject)){body.Knockback(player.AimDirection,upgrades.suctionReleasePush*KnockScale,KnockKind.Hit);released=true;}
            if(released)WeaponSound(w,WeaponAudioPhase.Launch);
            w.suctionTargets.Clear();
        }

        void Suction(RunWeapon w,float range)
        {
            w.suctionTargets.Clear();
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||body is Enemy{Immovable:true}||!InCone(body,range,60))continue;
                Vector3 toward=Query.Flat(player.Position-body.Position);
                float stop=player.radius+body.radius+(w.Has(UpgradeAxis.Tame)?upgrades.suctionSafeGap:.1f);
                float step=Mathf.Min(1f,Mathf.Max(0,toward.magnitude-stop));
                if(step>0){body.SetMovePosition(body.Position+toward.normalized*step);w.suctionTargets.Add(body);}
            }
            if(w.suctionTargets.Count>0)WeaponSound(w);
        }

        void Repel(RunWeapon w,float range)
        {
            bool affected=false;
            foreach(var body in Combatant.All)if(body!=player&&body.IsAlive&&Query.Flat(body.Position-player.Position).magnitude<=range+body.radius&&(!w.Has(UpgradeAxis.Twist)||InCone(body,range,180))){affected=true;break;}
            if(w.Has(UpgradeAxis.Twist))Cone(w,range,180,0,10);else Area(w,player.Position,range,0,10,false);
            var orbs=OrbPool.Instance;
            if(orbs&&orbs.pool!=null)foreach(var orb in orbs.pool)
            {
                if(!orb||!orb.InUse)continue;
                Vector3 to=Query.Flat(orb.Position-player.Position);
                if(to.magnitude>range||w.Has(UpgradeAxis.Twist)&&Vector3.Dot(to,player.AimDirection)<0)continue;
                orb.Repel(player.Position,orbs.repelDistance);
                affected=true;
            }
            if(affected)WeaponSound(w);
            if(!w.Has(UpgradeAxis.Tame))player.Push(-player.AimDirection,2);
        }

        void ScatterBlast(RunWeapon w,float range,float hit)
        {
            WeaponSound(w);
            Fx.Sector(player.Position,player.AimDirection,range,60,ElementCombat.ColorFor(w.element));
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||!InCone(body,range,60))continue;
                Vector3 delta=Query.Flat(body.Position-player.Position),push=delta.normalized;
                if(w.Has(UpgradeAxis.Twist))
                {push=player.AimDirection*Vector3.Dot(delta,player.AimDirection)-delta;push=push.sqrMagnitude>.0001f?push.normalized:player.AimDirection;}
                Damage(w,body,hit,10,push);
            }
            if(w.Has(UpgradeAxis.Tame))player.Push(-player.AimDirection,3);else player.Knockback(-player.AimDirection,3,KnockKind.Hit);
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
            Fx.Sector(origin,dir,range,40,ElementCombat.ColorFor(w.element));
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
            if(!frozen)return;
            if(!w.Has(UpgradeAxis.Twist)){Area(w,first.Position,2.5f,8*DamageScale(w),0,false);return;}
            upgradeHits.Clear();upgradeHits.Add(first);shatterCenters.Clear();shatterCenters.Add(first);
            int limit=w.Has(UpgradeAxis.Twist)?Mathf.Max(1,upgrades.shatterLimit):1;
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

        void Execute(RunWeapon w,float range,float hit)
        {
            var target=RayTarget(player.AimDirection,range);if(!target)return;
            WeaponSound(w);
            float primary=hit+w.executeBonus;
            Damage(w,target,primary,0,player.AimDirection);
            if(target.IsAlive&&target.Hp/target.maxHp<.4f&&target is not Enemy{IsBossOrElite:true})target.Kill();
            if(target.IsAlive){if(!w.Has(UpgradeAxis.Tame))w.cooldown+=1.5f;return;}
            w.executeBonus=Mathf.Min(24,w.executeBonus+1);
            if(!w.Has(UpgradeAxis.Twist))return;
            Combatant next=null;float nearest=upgrades.executeFollowRange*upgrades.executeFollowRange;
            foreach(var body in Combatant.All)
            {
                if(body==player||body==target||!body.IsAlive)continue;
                float distance=Query.Flat(body.Position-target.Position).sqrMagnitude;
                if(distance>=nearest||Physics.Linecast(Query.AtCastHeight(target.Position),Query.AtCastHeight(body.Position),GameManager.WallMask))continue;
                next=body;nearest=distance;
            }
            if(next)Damage(w,next,primary*upgrades.executeFollowDamage,0,Query.Flat(next.Position-target.Position).normalized);
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
