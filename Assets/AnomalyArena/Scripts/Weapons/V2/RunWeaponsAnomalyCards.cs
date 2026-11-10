using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Twist and Tame cards of the Rifle, Knife, Rocket Launcher and Scatter Cannon (10-23 card redesign).
    /// Numbers live in the 'upgrades' tuning block; every extra attack comes from the saved actor pool.</summary>
    public sealed partial class RunWeapons
    {
        HashSet<Combatant> chargeSlowed = new(), chargeLineNow = new();
        readonly object chargeLineKey = new();
        readonly List<Combatant> cardTargets = new();
        float focusGuardUntil;

        /// <summary>Charged Shot Tame: this enemy stands on the charging aim line and is slowed (its model glows).</summary>
        public bool IsChargeSlowed(Combatant body) => body && chargeSlowed.Contains(body);
        /// <summary>Scatter Focus Tame: the recoil guard is still blocking attacks (the player model glows).</summary>
        public bool FocusGuardActive => player && player.Protected && Time.time < focusGuardUntil;
        /// <summary>Chain Blade (Q19): you walk freely while the hook flies out and back, tamed or not; only the two-way
        /// pull carries you along.</summary>
        public bool HookLocksMovement => HandHookReeling;
        /// <summary>The hook in your hand is reeling a caught enemy in, and you toward it.</summary>
        public bool HandHookReeling
        {
            get
            {
                if (Active?.hookBusy != true || !AttackPool.Instance) return false;
                foreach (var actor in AttackPool.Instance.actors)
                    if (actor && actor.gameObject.activeSelf && actor.weapon == Active && actor.HookReeling && !BattlefieldFrozen(actor.gameObject)) return true;
                return false;
            }
        }
        /// <summary>Chain Blade Tame (Q22): the tamed hook in your hand is pulling, so you are unstoppable (Combatant.Knockback) and attacks are
        /// blocked (GuardReel).</summary>
        public bool ReelGuarded => Active != null && Active.form == WeaponForm.Knife && Active.variant == 1 && Active.Has(UpgradeAxis.Tame) && HandHookReeling;
        /// <summary>Charged Shot (Q1/Q4): enemy attacks that hit you while you charge it deal more (its backfire), or less
        /// once it is tamed. Combatant.ReceiveDamage applies it to enemy attacks only; burn and poison ticks and wall hits stay as they are.</summary>
        public float ChargeDamageTakenMultiplier{get{var w=Active;return w!=null&&w.charging&&w.form==WeaponForm.Rifle&&w.variant==0?Mathf.Max(0f,w.Has(UpgradeAxis.Tame)?upgrades.chargeTameDamageTaken:upgrades.chargeDamageTaken):1f;}}
        /// <summary>Launch Yourself Tame (Q25): while the tamed missile flies, no black hole (map, void-trial or your own
        /// Pocket Void) drags or swallows you; the void off the trial platform and map gaps still do.</summary>
        public bool MissileHoleProof=>dashLeft>0f&&dashWeapon!=null&&dashWeapon.Has(UpgradeAxis.Tame)&&player&&player.State==CharacterState.Dashing;
        /// <summary>Flying Blade Tame (Q14): the throw after a catch flies out and back this many times as fast.</summary>
        float KnifeCatchSpeed(RunWeapon w)=>w.knifeCaught&&w.Has(UpgradeAxis.Tame)?Mathf.Max(1f,upgrades.knifeCatchSpeed):1f;

        /// <summary>Charged Shot Twist (Q2): a full-charge release blasts the close cone ahead. Every enemy in it is knocked
        /// back (a real knockback, so bosses and Spires stand) and none is hurt; walls shield bodies behind them.</summary>
        void ChargedMuzzleBlast(RunWeapon w,Vector3 aim)
        {
            aim=Query.Flat(aim).normalized;float range=upgrades.chargedBlastRange,angle=upgrades.chargedBlastAngle;
            V5WeaponAudio.Instance?.Play(V5Sfx.ScatterBlast,.55f,1.15f);
            Fx.Cone(player.Position,aim,range,angle,w.element);
            foreach(var c in Combatant.All)
            {
                if(c==player||!c.IsAlive||c.Team==Team.Player)continue;
                Vector3 delta=Query.Flat(c.Position-player.Position);
                if(delta.magnitude>range+c.radius||delta.sqrMagnitude>.0001f&&Vector3.Angle(aim,delta)>angle*.5f||WallBlocks(player.Position,c.Position))continue;
                c.Knockback(delta.sqrMagnitude>.0001f?delta:aim,upgrades.chargedBlastPush*KnockScale,KnockKind.Hit);
            }
        }

        /// <summary>Chain Blade (document 5.5, Q16/Q18/Q21): while a hook reels a caught enemy in, it drags you toward the
        /// enemy as fast, so the two of you meet halfway. A wall stops you, and so does the edge of a black hole's pull: you are never dragged
        /// into one. Tame (Q22): meanwhile attacks are blocked (burn and poison ticks still land) and nothing interrupts the pull (CancelInput).
        /// Returns how far you were moved this step.</summary>
        public Vector3 ReelPlayer(RunWeapon w,Combatant held,float step)
        {
            if(!player||!player.IsAlive||!held)return Vector3.zero;
            GuardReel(w);
            // A knockback in flight carries you instead (an untamed pull is cancelled by it on the next tick).
            if(player.State!=CharacterState.Normal)return Vector3.zero;
            Vector3 from=Query.Flat(player.Position),gap=Query.Flat(held.Position)-from;
            // Each side closes at most half of what is left down to the holding distance, so neither passes the meeting point.
            float move=Mathf.Min(step,Mathf.Max(0f,gap.magnitude-(player.radius+held.radius+.3f))*.5f);
            if(move<=1e-4f)return Vector3.zero;
            Vector3 dir=gap.normalized;
            const float castBack=.1f;
            if(Physics.SphereCast(Query.AtCastHeight(from)-dir*castBack,player.radius,dir,out var wall,move+castBack,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                move=Mathf.Min(move,Mathf.Max(0f,wall.distance-castBack));
            // Q18: dragged up to a black hole's edge, never into it. You cannot walk while you are dragged, so the edge is where its pull
            // begins; already inside a pull ring, the drag never takes you nearer its core (WorldMap.DragIntoHole). The step is checked in
            // slices of at most 0.1 cells, so it cannot jump across a ring's edge, and stops at the last clear slice.
            var world=WorldMap.Active;
            if(world)
            {
                float clear=0f;
                for(int i=1,slices=Mathf.CeilToInt(move/.1f);i<=slices;i++){float along=move*i/slices;if(world.DragIntoHole(from,from+dir*along,player))break;clear=along;}
                move=clear;
                if(move<=1e-4f)return Vector3.zero;
            }
            player.SetMovePosition(new Vector3(from.x,player.Position.y,from.z)+dir*move);
            return dir*move;
        }

        /// <summary>Chain Blade Tame (Q22): attacks are blocked from the catch until you meet; called on the catch and on every tick of the
        /// pull, a little longer than one physics tick each time.</summary>
        public void GuardReel(RunWeapon w){if(w!=null&&w.Has(UpgradeAxis.Tame)&&player&&player.IsAlive)player.ProtectFor(Time.fixedDeltaTime*2f);}

        /// <summary>Chain Blade Tame (Q23): the moment you and the caught enemy meet, a shockwave knocks the other enemies
        /// around you back (a real knockback: bosses and Spires stand). It deals no damage, and walls shield bodies behind them.</summary>
        public void HookMet(RunWeapon w,Combatant held)
        {
            if(w==null||!w.Has(UpgradeAxis.Tame)||!player||!player.IsAlive)return;
            Vector3 at=player.Position;float radius=upgrades.hookShockRadius;
            foreach(var c in Combatant.All)
            {
                if(c==player||c==held||!c.IsAlive||c.Team==Team.Player)continue;
                Vector3 away=Query.Flat(c.Position-at);
                if(away.magnitude>radius+c.radius||WallBlocks(at,c.Position))continue;
                c.Knockback(away.sqrMagnitude>.0001f?away:Query.Flat(player.AimDirection),upgrades.hookShockPush*KnockScale,KnockKind.Hit);
            }
            Fx.Ring(at,radius,new Color(.8f,.9f,1f,.8f));
        }

        /// <summary>Charged Shot Tame: while the tamed rifle in your hand charges, non-boss enemies on the aim line up to the first
        /// wall move slower. Recomputed every tick, so any way out of the charge lifts the slow on the next tick.</summary>
        void TickChargeLine()
        {
            chargeLineNow.Clear();
            var w=Active;
            if(w!=null&&w.charging&&w.form==WeaponForm.Rifle&&w.variant==0&&w.Has(UpgradeAxis.Tame)&&CanUseHand&&roster)
            {
                var d=roster.Get(w.form);
                Vector3 aim=Query.Flat(player.AimDirection).normalized,from=player.Position;
                float range=(d!=null?d.rangeA:40f)*w.Reach;
                float length=Physics.Raycast(Query.AtCastHeight(from),aim,out var wall,range,GameManager.WallMask,QueryTriggerInteraction.Ignore)?wall.distance:range;
                foreach(var c in Combatant.All)
                {
                    if(c is not Enemy{IsBoss:false}||!c.IsAlive)continue;
                    Vector3 delta=Query.Flat(c.Position-from);float along=Vector3.Dot(delta,aim);
                    if(along<0f||along>length||(delta-aim*along).magnitude>upgrades.chargeLineHalfWidth+c.radius||WallBlocks(from,c.Position))continue;
                    chargeLineNow.Add(c);c.AddMovementModifier(chargeLineKey,upgrades.chargeLineSlowFactor);
                }
                // There is no aim line otherwise: draw this one for about a frame, every tick.
                AddTracer(Muzzle(aim),Query.AtCastHeight(from+aim*length),.08f,Time.unscaledDeltaTime+.02f,new Color(.55f,.85f,1f,.35f));
            }
            foreach(var c in chargeSlowed)if(c&&!chargeLineNow.Contains(c))c.RemoveMovementModifier(chargeLineKey);
            (chargeSlowed,chargeLineNow)=(chargeLineNow,chargeSlowed);
        }

        void ClearChargeLine()
        {
            foreach(var c in chargeSlowed)if(c)c.RemoveMovementModifier(chargeLineKey);
            chargeSlowed.Clear();chargeLineNow.Clear();
        }

        /// <summary>Reverse Shot Tame (Q9): the nearest visible enemy inside the guarded cone in front of you, or null.</summary>
        Combatant FrontGuardTarget()
        {
            Vector3 front=Query.Flat(player.AimDirection).normalized;
            Combatant best=null;float nearest=float.PositiveInfinity;
            foreach(var c in Combatant.All)
            {
                if(c==player||!c.IsAlive||c.Team==Team.Player)continue;
                Vector3 delta=Query.Flat(c.Position-player.Position);float distance=delta.magnitude;
                if(distance<.01f||distance>upgrades.reverseGuardRange+c.radius||distance>=nearest)continue;
                if(Vector3.Angle(front,delta)>upgrades.reverseGuardAngle*.5f||WallBlocks(player.Position,c.Position))continue;
                best=c;nearest=distance;
            }
            return best;
        }

        /// <summary>Strike Twist: a small blast at each enemy the strike killed. They never chain again, never hurt or push you, and
        /// skip Field so they play no firing feedback.</summary>
        public void StrikeChain(RunWeapon w,List<Vector3> points,float damage)
        {
            var pool=AttackPool.Instance;if(!pool||w==null||!player)return;
            int count=Mathf.Min(points.Count,Mathf.Max(0,upgrades.strikeChainMax));
            float delay=Mathf.Max(0f,upgrades.strikeChainDelay);
            for(int i=0;i<count;i++)
            {
                var a=pool.Acquire();if(!a)return;
                a.Configure(AttackMode.Blast,player,w,points[i],player.AimDirection,damage*upgrades.strikeChainShare,upgrades.strikeChainPush*KnockScale,0,0,upgrades.strikeChainRadius,delay+.1f);
                a.delay=delay;a.hurtOwner=false;a.chainChild=true;
            }
        }

        /// <summary>Hook Twist: a flung enemy crashed into target, so the hook goes straight out and reels target in, once per manual hook.
        /// Runs inside the crash's collision callback: it only changes state and takes a pooled actor.</summary>
        public void ChainHook(RunWeapon w,Combatant target)
        {
            if(w==null||w!=Active||w.form!=WeaponForm.Knife||w.variant!=1||!CanUseHand||w.hookBusy||w.hooked||w.hookChainUsed)return;
            if(!target||!target.IsAlive||target.State==CharacterState.Hooked||target.Team==Team.Player||target is Enemy{Immovable:true}||!roster)return;
            var d=roster.Get(w.form);float range=(d!=null?d.rangeB:10f)*w.Reach;
            Vector3 delta=Query.Flat(target.Position-player.Position);
            if(delta.magnitude>range||WallBlocks(player.Position,target.Position))return;
            var a=AttackPool.Instance?AttackPool.Instance.Acquire():null;if(!a)return;
            Vector3 dir=delta.sqrMagnitude>.0001f?delta.normalized:Query.Flat(player.AimDirection).normalized;
            // No capture hit and no re-throw lock: the chain goes out at once.
            a.Configure(AttackMode.Hook,player,w,SpawnPoint(dir,player.radius+.2f),dir,0,0,range,hookExtendSpeed/StartingFormCycleFactor(w.form),0,float.PositiveInfinity);
            a.BeginCarry(target);
            w.hookBusy=true;w.hookChainUsed=true;
            WeaponSound(w,WeaponAudioPhase.Launch);
        }

        /// <summary>Scatter Blast Twist: pulls the enemies in the cone ahead to just in front of you (a real knockback, so they cannot
        /// punch on the way); the blast follows after scatterVacuumDelay along the locked aim.</summary>
        void ScatterVacuum(RunWeapon w)
        {
            Vector3 aim=Query.Flat(player.AimDirection).normalized,from=player.Position;
            float range=upgrades.scatterVacuumRange*w.Reach,half=upgrades.scatterVacuumAngle*.5f;
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||body.Team==Team.Player||body is Enemy{Immovable:true})continue;
                Vector3 delta=Query.Flat(body.Position-from);float distance=delta.magnitude;
                if(distance<.01f||distance>range+body.radius||Vector3.Angle(aim,delta)>half||WallBlocks(from,body.Position))continue;
                float pull=Mathf.Max(0f,distance-body.radius-player.radius-upgrades.scatterVacuumGap);
                // A Hit knockback is shortened by body size and for an elite (documents 5.3, 10.6); the pull lands every body just in front
                // of you (document 6.7), so that scale is divided back out.
                if(pull>0f)body.Knockback(-delta/distance,pull/Mathf.Max(.01f,body.knockbackScale),KnockKind.Hit);
            }
            w.vacuumTimer=Mathf.Max(.0001f,upgrades.scatterVacuumDelay);w.vacuumDirection=aim;
            WeaponSound(w,WeaponAudioPhase.ChargeStart);
            VacuumFx(from,aim,range,upgrades.scatterVacuumAngle,w.element);
        }

        void TickScatterVacuum(RunWeapon w,float dt)
        {
            w.vacuumTimer-=dt;if(w.vacuumTimer>0f)return;
            w.vacuumTimer=0f;
            if(!player||!player.IsAlive||!roster)return;
            var d=roster.Get(w.form);float range=(d!=null?d.rangeA:5f)*w.Reach,hit=HitDamage(w)*upgrades.scatterVacuumShare;
            Vector3 aim=w.vacuumDirection.sqrMagnitude>.0001f?w.vacuumDirection:Query.Flat(player.AimDirection).normalized;
            WeaponSound(w);
            float angle=upgrades.scatterVacuumAngle;
            if(V5Vfx.On)V5Vfx.I.ScatterShot(player.Position,aim,range,angle,w.element);
            else Fx.Cone(player.Position,aim,range,angle,w.element);
            // Collect first: a kill can wake a pooled enemy, which joins Combatant.All.
            cardTargets.Clear();
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive)continue;
                Vector3 delta=Query.Flat(body.Position-player.Position);
                if(delta.magnitude>range+body.radius||Vector3.Angle(aim,delta)>angle*.5f||WallBlocks(player.Position,body.Position))continue;
                cardTargets.Add(body);
            }
            foreach(var body in cardTargets)
            {
                Vector3 delta=Query.Flat(body.Position-player.Position);
                // No distance falloff: the crowd was pulled in for this.
                Damage(w,body,hit,upgrades.scatterKnockback,delta.sqrMagnitude>.0001f?delta.normalized:aim);
            }
            cardTargets.Clear();
        }

        /// <summary>The pull shown as motes flying from the cone's far edge back to the muzzle (Fx.Cone only points outward).</summary>
        static void VacuumFx(Vector3 origin,Vector3 aim,float range,float angle,ElementKind element)
        {
            var pool=FxPool.Active;if(!pool)return;
            Color color=element==ElementKind.None?new Color(.75f,.9f,1f,.85f):ElementCombat.ColorFor(element);
            Fx.Sector(origin,aim,range,angle,new Color(color.r,color.g,color.b,.35f));
            const int motes=8;const float life=.25f;
            for(int i=0;i<motes;i++)
            {
                Vector3 dir=Quaternion.Euler(0f,((i+.5f)/motes*2f-1f)*angle*.5f,0f)*aim;
                pool.Emit(Query.AtCastHeight(origin)+dir*range,color,.4f,life,-dir*(range*.9f/life));
            }
        }

        /// <summary>Scatter Focus Tame: blocks enemy attacks for a moment (burn and poison ticks still land).</summary>
        void GuardFor(float seconds)
        {
            if(!player||seconds<=0f)return;
            player.ProtectFor(seconds);focusGuardUntil=Mathf.Max(focusGuardUntil,Time.time+seconds);
        }

        /// <summary>Extra hand-slot text of these cards: a caught knife, and the attack and move speed bonus a tamed Meltdown gave you.</summary>
        public string AnomalySlotDetail(RunWeapon w)
        {
            if(w==null)return "";
            if(w.IsAuto)return AutoSlotDetail(w);
            if(w.form==WeaponForm.Knife&&w.variant==0&&w.knifeCaught)
                return ArenaText.Pair($" · 接刀 +{upgrades.knifeCatchTargets}",$" · Caught +{upgrades.knifeCatchTargets}");
            if(w.form==WeaponForm.Minigun&&w.variant==1&&MeltdownHasteLeft>0f)
                return ArenaText.Pair($" · 攻速 +{upgrades.meltdownTameHaste*100f:0}% 移速 +{upgrades.meltdownTameMoveBonus*100f:0}% {MeltdownHasteLeft:0.0} 秒",$" · Attack speed +{upgrades.meltdownTameHaste*100f:0}% move +{upgrades.meltdownTameMoveBonus*100f:0}% {MeltdownHasteLeft:0.0}s");
            // Execute: the shot a kill armed, and the Tame arc shield while it lasts.
            if(w.form==WeaponForm.Railgun&&w.variant==1)
                return (w.executeNextShot>0f?ArenaText.Pair($" · 下一发 +{w.executeNextShot*100f:0}%",$" · Next shot +{w.executeNextShot*100f:0}%"):"")
                    +(w.executeAuraLeft>0f?ArenaText.Pair($" · 电弧护盾 {w.executeAuraLeft:0.0} 秒",$" · Arc shield {w.executeAuraLeft:0.0}s"):"");
            return "";
        }
    }
}
