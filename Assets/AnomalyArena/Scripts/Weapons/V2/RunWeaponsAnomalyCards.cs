using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                    
                                                                                                                  
    public sealed partial class RunWeapons
    {
        HashSet<Combatant> chargeSlowed = new(), chargeLineNow = new();
        readonly object chargeLineKey = new();
        readonly List<Combatant> cardTargets = new();
        float focusGuardUntil;

                                                                                                                             
        public bool IsChargeSlowed(Combatant body) => body && chargeSlowed.Contains(body);
                                                                                                                       
        public bool FocusGuardActive => player && player.Protected && Time.time < focusGuardUntil;
                                                                                                                                           
                                             
        public bool HookLocksMovement => HandHookReeling;
                                                                                                     
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
                                                                                                                                                 
                                          
        public bool ReelGuarded => Active != null && Active.form == WeaponForm.Knife && Active.variant == 1 && Active.Has(UpgradeAxis.Tame) && HandHookReeling;
                                                                                                                                             
                                                                                                                                                       
        public float ChargeDamageTakenMultiplier{get{var w=Active;return w!=null&&w.charging&&w.form==WeaponForm.Rifle&&w.variant==0?Mathf.Max(0f,w.Has(UpgradeAxis.Tame)?upgrades.chargeTameDamageTaken:upgrades.chargeDamageTaken):1f;}}
                                                                                                                                            
                                                                                                                
        public bool MissileHoleProof=>dashLeft>0f&&dashWeapon!=null&&dashWeapon.Has(UpgradeAxis.Tame)&&player&&player.State==CharacterState.Dashing;
                                                                                                                           
        float KnifeCatchSpeed(RunWeapon w)=>w.knifeCaught&&w.Has(UpgradeAxis.Tame)?Mathf.Max(1f,upgrades.knifeCatchSpeed):1f;

                                                                                                                                               
                                                                                                                            
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

                                                                                                                                             
                                                                                                                                               
                                                                                                                                                 
                                                               
        public Vector3 ReelPlayer(RunWeapon w,Combatant held,float step)
        {
            if(!player||!player.IsAlive||!held)return Vector3.zero;
            GuardReel(w);
                                                                                                               
            if(player.State!=CharacterState.Normal)return Vector3.zero;
            Vector3 from=Query.Flat(player.Position),gap=Query.Flat(held.Position)-from;
                                                                                                                               
            float move=Mathf.Min(step,Mathf.Max(0f,gap.magnitude-(player.radius+held.radius+.3f))*.5f);
            if(move<=1e-4f)return Vector3.zero;
            Vector3 dir=gap.normalized;
            const float castBack=.1f;
            if(Physics.SphereCast(Query.AtCastHeight(from)-dir*castBack,player.radius,dir,out var wall,move+castBack,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                move=Mathf.Min(move,Mathf.Max(0f,wall.distance-castBack));
                                                                                                                                          
                                                                                                                                           
                                                                                                                      
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

                                                                                                                                            
                                                                            
        public void GuardReel(RunWeapon w){if(w!=null&&w.Has(UpgradeAxis.Tame)&&player&&player.IsAlive)player.ProtectFor(Time.fixedDeltaTime*2f);}

                                                                                                                                             
                                                                                                                                           
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

                                                                                                                                            
                                                                                                                 
        public void ChainHook(RunWeapon w,Combatant target)
        {
            if(w==null||w!=Active||w.form!=WeaponForm.Knife||w.variant!=1||!CanUseHand||w.hookBusy||w.hooked||w.hookChainUsed)return;
            if(!target||!target.IsAlive||target.State==CharacterState.Hooked||target.Team==Team.Player||target is Enemy{Immovable:true}||!roster)return;
            var d=roster.Get(w.form);float range=(d!=null?d.rangeB:10f)*w.Reach;
            Vector3 delta=Query.Flat(target.Position-player.Position);
            if(delta.magnitude>range||WallBlocks(player.Position,target.Position))return;
            var a=AttackPool.Instance?AttackPool.Instance.Acquire():null;if(!a)return;
            Vector3 dir=delta.sqrMagnitude>.0001f?delta.normalized:Query.Flat(player.AimDirection).normalized;
                                                                               
            a.Configure(AttackMode.Hook,player,w,SpawnPoint(dir,player.radius+.2f),dir,0,0,range,hookExtendSpeed/StartingFormCycleFactor(w.form),0,float.PositiveInfinity);
            a.BeginCarry(target);
            w.hookBusy=true;w.hookChainUsed=true;
            WeaponSound(w,WeaponAudioPhase.Launch);
        }

                                                                                                                                       
                                                                                                         
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
            Fx.Cone(player.Position,aim,range,angle,w.element);
                                                                                        
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
                                                                         
                Damage(w,body,hit,upgrades.scatterKnockback,delta.sqrMagnitude>.0001f?delta.normalized:aim);
            }
            cardTargets.Clear();
        }

                                                                                                                                        
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

                                                                                                                        
        void GuardFor(float seconds)
        {
            if(!player||seconds<=0f)return;
            player.ProtectFor(seconds);focusGuardUntil=Mathf.Max(focusGuardUntil,Time.time+seconds);
        }

                                                                                                                                                  
        public string AnomalySlotDetail(RunWeapon w)
        {
            if(w==null)return "";
            if(w.IsAuto)return AutoSlotDetail(w);
            if(w.form==WeaponForm.Knife&&w.variant==0&&w.knifeCaught)
                return ArenaText.Pair($" · Caught +{upgrades.knifeCatchTargets}",$" · Caught +{upgrades.knifeCatchTargets}");
            if(w.form==WeaponForm.Minigun&&w.variant==1&&MeltdownHasteLeft>0f)
                return ArenaText.Pair($" · Attack speed +{upgrades.meltdownTameHaste*100f:0}% move +{upgrades.meltdownTameMoveBonus*100f:0}% {MeltdownHasteLeft:0.0}s",$" · Attack speed +{upgrades.meltdownTameHaste*100f:0}% move +{upgrades.meltdownTameMoveBonus*100f:0}% {MeltdownHasteLeft:0.0}s");
                                                                                                         
            if(w.form==WeaponForm.Railgun&&w.variant==1)
                return (w.executeNextShot>0f?ArenaText.Pair($" · Next shot +{w.executeNextShot*100f:0}%",$" · Next shot +{w.executeNextShot*100f:0}%"):"")
                    +(w.executeAuraLeft>0f?ArenaText.Pair($" · Arc shield {w.executeAuraLeft:0.0}s",$" · Arc shield {w.executeAuraLeft:0.0}s"):"");
            return "";
        }
    }
}
