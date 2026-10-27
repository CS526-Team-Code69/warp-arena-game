using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public sealed partial class RunWeapons
    {
        readonly object shellStrideKey = new(), iceStrideKey = new(), snowSlowKey = new();
        HashSet<Combatant> snowSlowed = new(), snowNow = new();
        static readonly float[] ShellSides = { -1f, 1f };
        float shellStrideLeft, snowClock;

        public bool SnowGuardActive { get; private set; }

        public float PlayerDamageTakenFactor => SnowGuardActive ? Mathf.Clamp01(upgrades.snowTameDamageTaken) : 1f;

        public float ShellStrideLeft => shellStrideLeft;

        void TickGround(float dt,float worldDt)
        {
            TickShells(dt);
            TickIceStride();
            TickSnow(dt,worldDt);
        }

        void DropShells(RunWeapon w)
        {
            if(w==null||!player)return;
            Vector3 aim=Query.Flat(player.AimDirection);aim=aim.sqrMagnitude>1e-4f?aim.normalized:Vector3.forward;
            Vector3 right=Vector3.Cross(Vector3.up,aim);
            float radius=ShellRadius(w),fuse=Mathf.Max(0f,upgrades.scatterShellFuse);
            foreach(float side in ShellSides)
            {
                var at=LandingPoint(SpawnPoint(right*side,w.Has(UpgradeAxis.Tame)?upgrades.scatterShellTameSide:upgrades.scatterShellSide));if(!at.HasValue)continue;
                AddHazard(GroundHazardKind.ScatterShell,at.Value,radius,fuse,w);

                Fx.Ring(at.Value,radius,new Color(1f,.6f,.25f,.8f),Mathf.Max(.05f,fuse));
            }
        }

        float ShellRadius(RunWeapon w)=>Mathf.Max(0f,upgrades.scatterShellRadius)*(w.Has(UpgradeAxis.Tame)?Mathf.Max(1f,upgrades.scatterShellTameRadius):1f);

        void IgniteShell(GroundHazard h)
        {
            var w=h.weapon;var pool=AttackPool.Instance;
            if(w==null||!player||!player.IsAlive||!pool)return;
            var fire=pool.Acquire();if(!fire)return;

            fire.Configure(AttackMode.Field,player,w,h.position,player.AimDirection,HitDamage(w)*upgrades.scatterShellShare,0,0,0,ShellRadius(w),Mathf.Max(.01f,upgrades.scatterShellBurn));
            fire.hurtOwner=!w.Has(UpgradeAxis.Tame);fire.ownerDamage=Mathf.Max(0f,upgrades.scatterShellSelfDamage);fire.ownerStepsOn=true;

            if(w.element==ElementKind.None&&fire.visual&&GameManager.Instance)fire.visual.sharedMaterial=GameManager.Instance.Mat(new Color(1f,.55f,.2f));
            WeaponSound(w,WeaponAudioPhase.Explosion);
            Fx.Pop(Query.AtCastHeight(h.position),new Color(1f,.6f,.25f),fire.radius*1.6f);
        }

        void TickShells(float dt)
        {
            if(shellStrideLeft>0f){shellStrideLeft=Mathf.Max(0f,shellStrideLeft-dt);if(shellStrideLeft<=0f&&player)player.RemoveMovementModifier(shellStrideKey);}
            if(!player||!player.IsAlive||!AttackPool.Instance)return;
            foreach(var w in Hand)
            {
                if(w==null||w.form!=WeaponForm.ScatterCannon||w.variant!=0||!w.Has(UpgradeAxis.Tame))continue;
                foreach(var fire in AttackPool.Instance.actors)
                {
                    if(!fire||!fire.gameObject.activeSelf||fire.weapon!=w||fire.mode!=AttackMode.Field||BattlefieldFrozen(fire.gameObject))continue;
                    if(w.reload<=0f||Query.Flat(player.Position-fire.point).magnitude>fire.radius||WallBlocks(fire.point,player.Position))continue;
                    w.reload=w.cooldown=0f;w.rounds=ClipSize(w.form,w.variant);
                    shellStrideLeft=Mathf.Max(0f,upgrades.scatterShellTameSpeedSeconds);
                    if(shellStrideLeft>0f)player.AddMovementModifier(shellStrideKey,upgrades.scatterShellTameSpeed);
                    V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair,player.Position);
                    Fx.Ring(player.Position,1.2f,new Color(1f,.75f,.35f,.9f));
                }
            }
        }

        bool Touches(GroundHazard h,Combatant body)=>Query.Flat(body.Position-h.position).magnitude<=h.radius+body.radius&&!WallBlocks(h.position,body.Position);

        public float IceGrip(Combatant body)
        {
            if(!body||body.Immovable||hazards.Count==0)return 0f;
            bool on=false;
            foreach(var h in hazards)
            {
                if(h.kind!=GroundHazardKind.IcePatch||!Touches(h,body))continue;
                if(body==(Combatant)player&&h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame))return 0f;
                on=true;
            }
            return on?Mathf.Max(.5f,body.moveSpeed)/Mathf.Max(.05f,upgrades.iceSlideDistance):0f;
        }

        void TickIceStride()
        {
            if(!player)return;
            bool on=false;
            if(player.IsAlive)foreach(var h in hazards)if(h.kind==GroundHazardKind.IcePatch&&h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame)&&Touches(h,player)){on=true;break;}
            if(on)player.AddMovementModifier(iceStrideKey,upgrades.shatterTameSpeed);else player.RemoveMovementModifier(iceStrideKey);
        }

        void LaySnow(RunWeapon w,float dt)
        {
            if(!player||w!=Active||w.heldTime<=0f||!player.IsAlive)return;
            if(w.heldTime<=dt+1e-4f)snowClock=0f;               
            snowClock-=dt;if(snowClock>0f)return;
            snowClock=Mathf.Max(.05f,upgrades.snowInterval);
            AddHazard(GroundHazardKind.Snow,player.Position,upgrades.snowRadius,upgrades.snowLife,w);
        }

        void TickSnow(float dt,float worldDt)
        {
            bool chill=false,guard=false;
            snowNow.Clear();
            foreach(var h in hazards)
            {
                if(h.kind!=GroundHazardKind.Snow)continue;
                bool tame=h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame);
                if(player&&player.IsAlive&&Touches(h,player)){if(tame)guard=true;else chill=true;}
                if(!tame)continue;
                foreach(var body in Combatant.All)
                    if(body!=player&&body.IsAlive&&body.Team!=Team.Player&&!snowNow.Contains(body)&&Touches(h,body))snowNow.Add(body);
            }
            SnowGuardActive=guard;
            if(chill&&!guard)
            {

                var status=ElementStatus.For(player);
                float room=status?Mathf.Min(upgrades.snowFrostCap,status.FrostThreshold-.01f)-status.Frost:0f;
                if(room>0f)status.Add(ElementKind.Frost,Mathf.Min(room,upgrades.snowFrostPerSecond*dt),player);
            }
            foreach(var body in snowSlowed)if(body&&!snowNow.Contains(body))body.RemoveMovementModifier(snowSlowKey);
            foreach(var body in snowNow)
            {
                body.AddMovementModifier(snowSlowKey,upgrades.snowTameSlow);
                ElementStatus.For(body)?.Add(ElementKind.Frost,upgrades.snowTameFrostPerSecond*worldDt,player);
            }
            (snowSlowed,snowNow)=(snowNow,snowSlowed);
        }

        void ClearGround()
        {
            foreach(var body in snowSlowed)if(body)body.RemoveMovementModifier(snowSlowKey);
            snowSlowed.Clear();snowNow.Clear();SnowGuardActive=false;snowClock=0f;shellStrideLeft=0f;
            if(player){player.RemoveMovementModifier(shellStrideKey);player.RemoveMovementModifier(iceStrideKey);}
        }
    }
}
