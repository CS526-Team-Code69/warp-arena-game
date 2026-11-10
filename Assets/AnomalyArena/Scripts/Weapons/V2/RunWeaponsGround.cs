using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Ground left by the Scatter Blast, Shatter and Blizzard (weapon section 5): hot shells that flare up when a
    /// reload starts, slippery ice, and snow at your feet while you spray. Numbers live in the 'upgrades' tuning block; the hazards list
    /// holds shells and snow, the saved actor pool the shell fires.</summary>
    public sealed partial class RunWeapons
    {
        readonly object shellStrideKey = new(), iceStrideKey = new(), snowSlowKey = new();
        HashSet<Combatant> snowSlowed = new(), snowNow = new();
        static readonly float[] ShellSides = { -1f, 1f };
        float shellStrideLeft, snowClock;
        /// <summary>Blizzard Tame: you stand in your own snow and take less damage (V4CombatVfx marks it).</summary>
        public bool SnowGuardActive { get; private set; }
        /// <summary>Multiplier on the damage the player takes (Combatant.ReceiveDamage): Blizzard Tame snow under you.</summary>
        public float PlayerDamageTakenFactor => SnowGuardActive ? Mathf.Clamp01(upgrades.snowTameDamageTaken) : 1f;
        /// <summary>Scatter Blast Tame: seconds left of the speed bonus a burning shell gave you.</summary>
        public float ShellStrideLeft => shellStrideLeft;

        /// <summary>Called every frame: the shell, ice and snow effects that follow the bodies standing on them.</summary>
        void TickGround(float dt,float worldDt)
        {
            TickShells(dt);
            TickIceStride();
            TickSnow(dt,worldDt);
        }

        // ───── Scatter Blast shells ─────

        /// <summary>Scatter Blast backfire: the clip ran dry and the reload starts, so two hot shells land left and right of you (the aim is
        /// ahead), short of a wall, a hole or the map's edge. Each flares up after scatterShellFuse (TickHazards, IgniteShell).</summary>
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
                // The ring grows to the fire it will become, like the Bombard landing markers.
                Fx.Ring(at.Value,radius,new Color(1f,.6f,.25f,.8f),Mathf.Max(.05f,fuse));
            }
        }

        /// <summary>Shell fire radius: Tame widens it. A trait area like the ice patches, spore pods and fuel tank, so the Reach card does not
        /// grow it (it would reach your feet between the shells and burn you without a step).</summary>
        float ShellRadius(RunWeapon w)=>Mathf.Max(0f,upgrades.scatterShellRadius)*(w.Has(UpgradeAxis.Tame)?Mathf.Max(1f,upgrades.scatterShellTameRadius):1f);

        /// <summary>A shell's fuse ran out: it flares up into a small fire for scatterShellBurn. Each 0.25 s tick hits the enemies touching it
        /// for a share of one full blast, with the weapon's element; you take a flat amount, only with your centre on it (you stepped on
        /// it), and never once tamed.</summary>
        void IgniteShell(GroundHazard h)
        {
            var w=h.weapon;var pool=AttackPool.Instance;
            if(w==null||!player||!player.IsAlive||!pool)return;
            var fire=pool.Acquire();if(!fire)return;
            // Configured here instead of through Field, so the flare plays no firing feedback.
            fire.Configure(AttackMode.Field,player,w,h.position,player.AimDirection,HitDamage(w)*upgrades.scatterShellShare,0,0,0,ShellRadius(w),Mathf.Max(.01f,upgrades.scatterShellBurn));
            fire.hurtOwner=!w.Has(UpgradeAxis.Tame);fire.ownerDamage=Mathf.Max(0f,upgrades.scatterShellSelfDamage);fire.ownerStepsOn=true;
            // Without an element the whitebox fire would be white: it glows hot instead.
            if(w.element==ElementKind.None&&fire.visual&&GameManager.Instance)fire.visual.sharedMaterial=GameManager.Instance.Mat(new Color(1f,.55f,.2f));
            WeaponSound(w,WeaponAudioPhase.Explosion);
            Fx.Pop(Query.AtCastHeight(h.position),new Color(1f,.6f,.25f),fire.radius*1.6f);
        }

        /// <summary>Scatter Blast Tame: your centre on a burning shell of a Scatter Blast that is reloading finishes that reload at once (both
        /// rounds, and the last shot's cooldown, so it fires at once as the old quick reload did) and speeds you up for a moment. The reload
        /// is then done, so it counts once per reload. (A shell lit before the card stops burning you in AttackActor.TickField.)</summary>
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

        // ───── Shatter ice ─────

        bool Touches(GroundHazard h,Combatant body)=>Query.Flat(body.Position-h.position).magnitude<=h.radius+body.radius&&!WallBlocks(h.position,body.Position);

        /// <summary>Shatter ice: how fast the walk of this body on ice eases toward the walk it asks for, per second; 0 off
        /// the ice, where it walks as usual (Combatant.SlideOnIce). Every patch counts, tamed or not, but you keep your footing on your own
        /// tamed ice. Bosses and the Spire never slip. Set from the body's own walking speed, so letting go at that speed slides it
        /// iceSlideDistance.</summary>
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

        /// <summary>Shatter Tame: while you stand on your own ice you move faster; stepping off restores it at once.</summary>
        void TickIceStride()
        {
            if(!player)return;
            bool on=false;
            if(player.IsAlive)foreach(var h in hazards)if(h.kind==GroundHazardKind.IcePatch&&h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame)&&Touches(h,player)){on=true;break;}
            if(on)player.AddMovementModifier(iceStrideKey,upgrades.shatterTameSpeed);else player.RemoveMovementModifier(iceStrideKey);
        }

        // ───── Blizzard snow ─────

        /// <summary>Blizzard backfire: while you spray, a snow patch lands at your feet at once and then every snowInterval, so walking
        /// leaves a trail. Only the Blizzard in your hand runs the clock (a second one stowed does not reset it).</summary>
        void LaySnow(RunWeapon w,float dt)
        {
            if(!player||w!=Active||w.heldTime<=0f||!player.IsAlive)return;
            if(w.heldTime<=dt+1e-4f)snowClock=0f; // a new spray
            snowClock-=dt;if(snowClock>0f)return;
            snowClock=Mathf.Max(.05f,upgrades.snowInterval);
            AddHazard(GroundHazardKind.Snow,player.Position,upgrades.snowRadius,upgrades.snowLife,w);
        }

        /// <summary>Blizzard: your snow. Untamed it builds Frost on you up to snowFrostCap (it only slows). Tamed it spares you, cuts the damage
        /// you take while you stand in it, and slows the enemies standing in it and builds extra Frost on them; they speed up again as they
        /// leave it. Several patches count once.</summary>
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
                // Like the old ice patch: it stops short of the freeze threshold.
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

        /// <summary>Wave end and void trial: the snow slow and the speed bonuses end; the shells and snow go with the hazards.</summary>
        void ClearGround()
        {
            foreach(var body in snowSlowed)if(body)body.RemoveMovementModifier(snowSlowKey);
            snowSlowed.Clear();snowNow.Clear();SnowGuardActive=false;snowClock=0f;shellStrideLeft=0f;
            if(player){player.RemoveMovementModifier(shellStrideKey);player.RemoveMovementModifier(iceStrideKey);}
        }
    }
}
