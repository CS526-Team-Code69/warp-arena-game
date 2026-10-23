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
                                                                                                                  
        public bool HookLocksMovement => HandHookBusy && !(Active?.Has(UpgradeAxis.Tame) ?? false);

                                                                                                                                        
        AttackActor TwinBolt(RunWeapon w,Vector3 dir,float hit,float push,float reach,float velocity)
        {
            var a=AttackPool.Instance?AttackPool.Instance.Acquire():null;if(!a)return null;
            a.Configure(AttackMode.Bolt,player,w,SpawnPoint(dir,.7f),dir,hit,push*KnockScale,reach,velocity,0,reach/velocity+3);a.pierce=true;
            return a;
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
                    chargeLineNow.Add(c);c.AddMovementModifier(chargeLineKey,upgrades.chargeLineSlow);
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

                                                                                                                        
        Combatant RearGuardTarget()
        {
            Vector3 back=-Query.Flat(player.AimDirection).normalized;
            Combatant best=null;float nearest=float.PositiveInfinity;
            foreach(var c in Combatant.All)
            {
                if(c==player||!c.IsAlive||c.Team==Team.Player)continue;
                Vector3 delta=Query.Flat(c.Position-player.Position);float distance=delta.magnitude;
                if(distance<.01f||distance>upgrades.reverseGuardRange+c.radius||distance>=nearest)continue;
                if(Vector3.Angle(back,delta)>upgrades.reverseGuardAngle*.5f||WallBlocks(player.Position,c.Position))continue;
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
                if(pull>0f)body.Knockback(-delta/distance,pull,KnockKind.Hit);
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

                                                                                                    
        public float ReloadProgress(RunWeapon w)=>w==null||w.reload<=0f||w.reloadTotal<=0f?1f:Mathf.Clamp01(1f-w.reload/w.reloadTotal);
                                                                                                     
        public bool QuickReloadOpen(RunWeapon w)=>w!=null&&w.form==WeaponForm.ScatterCannon&&w.variant==0&&w.Has(UpgradeAxis.Tame)&&w.reload>0f&&!w.quickReloadTried;
                                                                                                                                
        static bool ShowsReloadBar(RunWeapon w)=>w!=null&&w.form==WeaponForm.ScatterCannon&&w.variant==0&&w.Has(UpgradeAxis.Tame)&&w.reload>0f&&w.reloadTotal>0f;
                                                                                                                                                            
        public bool ChargeWindow(out float start,out float end)
        {
            start=end=0f;var w=Active;
            if(!ShowsReloadBar(w)||!QuickReloadOpen(w))return false;
            start=Mathf.Clamp01(upgrades.scatterQuickStart);end=Mathf.Clamp01(Mathf.Max(upgrades.scatterQuickStart,upgrades.scatterQuickEnd));
            return end>start;
        }

                                                                                                                                                
        bool TryQuickReload(RunWeapon w)
        {
            if(!QuickReloadOpen(w))return false;
            w.quickReloadTried=true;
            float progress=ReloadProgress(w);
            if(progress<upgrades.scatterQuickStart||progress>upgrades.scatterQuickEnd)return false;
            w.reload=0f;w.cooldown=0f;w.jammed=false;w.rounds=ClipSize(w.form,w.variant);
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair,player.Position);
            Fire(w,false);
            return true;
        }

                                                                                                                                                   
        public string AnomalySlotDetail(RunWeapon w)
        {
            if(w==null)return "";
            if(w.form==WeaponForm.Knife&&w.variant==0&&w.knifeCaught)
                return ArenaText.Pair($" · Caught +{upgrades.knifeCatchTargets}",$" · Caught +{upgrades.knifeCatchTargets}");
            return "";
        }
    }
}
