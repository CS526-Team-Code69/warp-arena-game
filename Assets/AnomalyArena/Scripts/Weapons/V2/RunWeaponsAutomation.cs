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
        public float whirlDuration = 2f;
        public float whirlOrbitRadius = 2.5f;
        float autoClock;
        readonly Dictionary<Combatant,float>[] autoHitTimes = { new(), new(), new() };
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
        void TickAutoWeapons(float dt)
        {
            autoClock+=dt;

            float rate=Mathf.Max(.01f,WeaponCooldownRate);
            for(int slot=0;slot<Auto.Length;slot++)
            {
                var w=Auto[slot];if(w==null)continue;

                if(w.whirled&&BattlefieldFrozen(w.whirled.gameObject))continue;
                w.cooldown=Mathf.Max(0,w.cooldown-dt*rate);w.orbitAngle+=orbitAngularSpeed*dt;
                var definition=roster.Get(w.form);
                float hit=(w.variant==0?definition.damageA:definition.damageB)*DamageScale(w);
                float range=(w.variant==0?definition.rangeA:definition.rangeB)*w.Reach;
                if(w.form==WeaponForm.OrbitBlades)
                {
                    Reveal(w);
                    for(int blade=0;blade<2;blade++)
                    {
                        Vector3 position=GetOrbitPosition(slot,blade);
                        foreach(var body in Combatant.All)
                        {
                            if(body==player||!body.IsAlive)continue;
                            if(autoHitTimes[slot].TryGetValue(body,out float next)&&next>autoClock)continue;
                            if(Query.Flat(body.Position-position).magnitude>body.radius+orbitBladeRadius||WallBlocks(player.Position,body.Position))continue;
                            autoHitTimes[slot][body]=autoClock+orbitHitInterval*CooldownScale(w)/rate;
                            Damage(w,body,hit,w.variant==0?3:-1.5f,Query.Flat(body.Position-player.Position).normalized);
                            if(lastDamageAccepted)WeaponSound(w);
                        }
                        if(w.variant==0&&w.cooldown<=0)OrbPool.Instance?.Repel(position,orbitBladeRadius);
                    }
                    if(w.cooldown<=0)w.cooldown=.2f;
                    continue;
                }
                if(w.whirled)
                {
                    if(!w.whirled.IsAlive){EndWhirl(w);continue;}
                    w.whirlLeft-=dt;
                    float radians=w.orbitAngle*Mathf.Deg2Rad;
                    Vector3 at=player.Position+new Vector3(Mathf.Cos(radians),0,Mathf.Sin(radians))*whirlOrbitRadius*w.Reach;

                    if(!ValidTeleportPoint(at,w.whirled.radius)){EndWhirl(w);continue;}
                    w.whirled.SetMovePosition(at);
                    foreach(var body in Combatant.All)
                    {
                        if(body==player||body==w.whirled||!body.IsAlive)continue;
                        if(autoHitTimes[slot].TryGetValue(body,out float next)&&next>autoClock)continue;
                        if(Query.Flat(body.Position-at).magnitude>body.radius+w.whirled.radius||WallBlocks(at,body.Position))continue;
                        autoHitTimes[slot][body]=autoClock+.5f;
                        Damage(w,body,hit,5,Query.Flat(body.Position-at).normalized);
                    }
                    if(w.whirlLeft<=0)EndWhirl(w);
                    continue;
                }
                if(w.cooldown>0)continue;
                if(w.form==WeaponForm.SentryDrone)
                {

                    var target=w.variant==0?NearestVisible(range):NearestVisible(range,WhirlCandidate);
                    if(!target){if(w.variant==1)w.cooldown=upgrades.whirlRetry;continue;}

                    if(w.variant==0&&progression)
                    {
                        if(!progression.EnergyUnlocked)progression.UnlockEnergy();
                        if(!progression.TrySpendEnergy(upgrades.gunnerEnergyCost))continue;
                    }
                    Reveal(w);
                    w.cooldown=(w.variant==0?definition.cooldownA:definition.cooldownB)*CooldownScale(w);
                    if(w.variant==0)
                    {
                        Vector3 dir=Query.Flat(target.Position-player.Position).normalized;
                        Bolt(w,dir,hit,0,range,30);
                    }
                    else
                    {
                        w.whirled=target;w.whirlLeft=whirlDuration;target.SetState(CharacterState.Hooked);
                        WeaponSound(w,WeaponAudioPhase.Launch);
                        autoHitTimes[slot].Clear();
                    }
                }
                else if(w.form==WeaponForm.ShieldRing)
                {
                    Reveal(w);w.cooldown=(w.variant==0?definition.cooldownA:definition.cooldownB)*CooldownScale(w);
                    if(w.variant==0)
                    {
                        bool affected=false;
                        foreach(var body in Combatant.All)if(body!=player&&body.IsAlive&&Query.Flat(body.Position-player.Position).magnitude<=range+body.radius){affected=true;break;}
                        var orbs=OrbPool.Instance;
                        if(orbs&&orbs.pool!=null)foreach(var orb in orbs.pool)if(orb&&orb.InUse&&Query.Flat(orb.Position-player.Position).sqrMagnitude<=range*range){affected=true;break;}
                        Area(w,player.Position,range,hit,5,false);orbs?.Repel(player.Position,range);
                        if(affected)WeaponSound(w);
                    }
                    else
                    {
                        float drained=0;Combatant weakest=null;
                        foreach(var body in Combatant.All)
                        {
                            if(body==player||!body.IsAlive||Query.Flat(body.Position-player.Position).magnitude>range+body.radius||WallBlocks(player.Position,body.Position))continue;
                            float before=body.Hp;
                            Damage(w,body,hit,0,Vector3.zero);
                            if(lastDamageAccepted)WeaponSound(w);
                            float taken=Mathf.Max(0,before-body.Hp);
                            w.leechDamage+=taken;drained+=taken;
                            if(body.IsAlive&&body.Hp<body.maxHp&&(!weakest||body.Hp<weakest.Hp))weakest=body;
                        }
                        while(w.leechDamage>=5){w.leechDamage-=5;progression.GainEnergy(1);}

                        if(weakest&&drained>0){weakest.Heal(drained*upgrades.leechHealShare);Fx.Ring(weakest.Position,.6f,new Color(.55f,1f,.5f,.8f));}
                    }
                }
            }
        }
        static bool WhirlCandidate(Combatant body)=>body is not Enemy{Immovable:true}&&body.State!=CharacterState.Hooked;
        void EndWhirl(RunWeapon w)
        {
            if(w.whirled && w.whirled.IsAlive)w.whirled.SetState(CharacterState.Normal);
            w.whirled=null;w.whirlLeft=0;
        }
        void ClearAutoMotion()
        {
            foreach(var w in Auto)if(w!=null)EndWhirl(w);
            foreach(var times in autoHitTimes)times.Clear();
        }
    }
}
