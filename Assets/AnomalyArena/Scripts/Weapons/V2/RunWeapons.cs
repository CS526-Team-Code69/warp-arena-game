using System;
using UnityEngine;

namespace AnomalyArena
{
    public sealed class WeaponUpgradeOffer
    {
        public RunWeapon Weapon { get; }
        public UpgradeAxis Axis { get; }
        public ElementKind Element { get; }
        public WeaponUpgradeOffer(RunWeapon weapon, UpgradeAxis axis, ElementKind element)
        { Weapon = weapon; Axis = axis; Element = element; }
    }
                                                                                                                                         
    public sealed partial class RunWeapons : MonoBehaviour
    {
        public WeaponRoster roster;
        [Header("Starting forms — feedback tuning")]
        [Range(0f,1f), Tooltip("Independent starting Rifle variant A chance; 0.5 gives 50/50. Armory deduplication is separate.")]
        public float startingRifleVariantAChance = .5f;
        [Range(0f,1f)] public float startingKnifeVariantAChance = .5f;
        [Range(0f,1f)] public float startingRocketVariantAChance = .5f;
        [Min(.01f), Tooltip("Damage multiplier for Rifle, Knife and Rocket Launcher; range and knockback are preserved.")]
        public float startingDamageFactor = .5f;
        [Min(.01f), Tooltip("Crowd-mode weapon damage multiplier, applied after cycle and upgrade factors; retains card growth.")]
        public float crowdDamageMultiplier = 1f;
        [Min(.01f), Tooltip("Complete attack duration multiplier for the three starting forms; .5 doubles firing and motion cadence.")]
        public float startingCycleFactor = .5f;
        [field: SerializeField, HideInInspector, Tooltip("Inactive legacy falloff field. Every target hit by a human rocket receives the same damage.")]
        public float HumanRocketImpactRetention { get; set; } = .5f;
        [Header("Knife motion — original behaviour")]
        [Min(.01f)] public float knifeOutboundSpeed = 18f;
        [Tooltip("Speed at which the knife returns to the player (cells per second); 14 -> 9 on 2026-10-21 after playtesting.")] [Min(.01f)] public float knifeReturnSpeed = 9f;
        [Tooltip("Rounds per clip for Rifle · Reverse Shot; 4 -> 3 on 2026-10-21 (update the \"three rounds per clip\" text in WeaponGuide when changing this).")] [Min(1)] public int rifleClipSize = 3;
        [Min(.01f)] public float knifeSeekRange = 7f;
        [Min(.01f)] public float hookExtendSpeed = 40f;
        [Min(.01f)] public float hookPullSpeed = 25f;
        [Min(.01f)] public float hookRetractSpeed = 60f;
        [Min(0f), Tooltip("Seconds before the hook can go out again after it comes back empty or the caught enemy is flung; the starting-form cycle factor does not shorten it.")] public float hookCooldown = .8f;
        [field: SerializeField, Tooltip("Candidate Phase Dagger flight speed in units/second; GDD does not prescribe it.")]
        public float DaggerSpeed { get; set; } = 20f;
        [field: SerializeField, Tooltip("Candidate maximum Recall arc height, above the interpolated start/end height.")]
        public float RecallArcHeight { get; set; } = 2f;
        [field: SerializeField, Tooltip("Candidate fireball/spore pod horizontal flight speed in units/second.")]
        public float LobSpeed { get; set; } = 12f;
        [field: SerializeField, Tooltip("Candidate fireball/spore pod arc height; hard walls still block the real swept trajectory.")]
        public float LobArcHeight { get; set; } = 3f;
        public RunWeapon[] Hand={null,null};
        public const int AutoSlotCount=2;
        public RunWeapon[] Auto={null,null};
        public const int ArmoryOfferCount=3;
        [Range(0f,1f), Tooltip("Chance that each of armory slots 2 and 3 shows a stronger version of a held weapon (a new weapon of that form at a higher Mk, which replaces the held one) instead of a new form. Slot 1 is always a new form.")]
        public float armoryUpgradeChance=.5f;
        public RunWeapon[] Offers={null,null,null};
        public int ActiveIndex { get; private set; }
        public Combatant HeldEnemy => Active?.hooked && !BattlefieldFrozen(Active.hooked.gameObject) ? Active.hooked : null;
        public RunWeapon Active => Hand[ActiveIndex];
                                                                                                                                                               
        public bool TriggerHeld => inputHeld;
        public bool HandHookBusy
        {
            get
            {
                if (Active?.hookBusy != true || !AttackPool.Instance) return false;
                foreach (var actor in AttackPool.Instance.actors)
                    if (actor && actor.gameObject.activeSelf && actor.weapon == Active && actor.mode == AttackMode.Hook && !BattlefieldFrozen(actor.gameObject)) return true;
                return false;
            }
        }
                                                                                                                        
        public float Charge01 => Active==null?0:Active.form==WeaponForm.ArcCoil&&Active.variant==1?Mathf.Clamp01(Active.charge/CapacitorMax):Active.form==WeaponForm.Minigun?MinigunBar(Active):Mathf.Clamp01(Active.charge);
                                                                                                                                                     
        static float MinigunBar(RunWeapon w)=>w.variant==0?Mathf.Clamp01(w.heldTime/3f):Mathf.Clamp01(w.heat/4f);
        public float MoveMultiplier
        {
            get
            {
                var w=Active;if(w==null)return 1;
                                                                                                                                                  
                if(w.charging&&w.form!=WeaponForm.ScatterCannon&&!(w.form==WeaponForm.Rifle&&w.Has(UpgradeAxis.Tame)))return .5f;
                                                                                                                           
                                                                                                                    
                if(w.form==WeaponForm.ArcCoil&&w.variant==0&&w.Has(UpgradeAxis.Tame)&&w.chainStoredJumps>0)return Mathf.Max(.1f,upgrades.chainTameMoveSpeed);
                return 1;
            }
        }
                                                                                                                           
        public float AimTurnRateLimit
        {
            get
            {
                var w=Active;
                if(w==null||w.form!=WeaponForm.Minigun||w.variant!=0||w.heldTime<=0||w.Has(UpgradeAxis.Tame))return 0;
                return Mathf.Lerp(upgrades.spinUpFreeTurn,upgrades.spinUpFullTurn,Mathf.Clamp01(w.heldTime/3));
            }
        }
        PlayerController player;
        RunProgression progression;
        Vector3 lastPosition;
        bool inputHeld;
        bool lastDamageAccepted;
        float dashLeft,dashRange;
        Vector3 dashDirection;
        RunWeapon dashWeapon;
        float dashContactDamage;
        readonly System.Collections.Generic.HashSet<(Combatant body, int spawnVersion)> dashHits=new();
        (Combatant body,int spawnVersion)[] dashContacts=new (Combatant,int)[512];
        float[] dashContactDistances=new float[512];
        readonly System.Collections.Generic.Dictionary<RunWeapon,RunWeapon> trialWeaponStates=new();
        readonly System.Collections.Generic.HashSet<AttackActor> frozenBattlefieldAttacks=new();
        readonly System.Collections.Generic.HashSet<Combatant> frozenHeldBodies=new();
        readonly System.Collections.Generic.Dictionary<Combatant,float>[] frozenAutoHitTimes={new(),new(),new()};
        int trialActiveIndex;
        bool trialInputHeld;
        float frozenAutoClock;
        public event Action<RunWeapon> Revealed;
        void Awake(){player=GetComponent<PlayerController>();progression=GetComponent<RunProgression>();lastPosition=transform.position;}
        public void ResetRun(){ClearWaveEffects();Hand=new RunWeapon[2];Auto=new RunWeapon[AutoSlotCount];Offers=new RunWeapon[ArmoryOfferCount];ActiveIndex=0;}
        public void GiveStarting(WeaponForm form)
        {
            float chance=form switch{WeaponForm.Rifle=>startingRifleVariantAChance,WeaponForm.Knife=>startingKnifeVariantAChance,WeaponForm.RocketLauncher=>startingRocketVariantAChance,_=>.5f};
            Hand[0]=CreateWeapon(form,UnityEngine.Random.value<Mathf.Clamp01(chance)?0:1,1);
        }
        RunWeapon Roll(WeaponForm form,int wave)
        {
            int v=UnityEngine.Random.Range(0,2);if(Owned(form,v))v=1-v;
            var w=CreateWeapon(form,v,wave);
                                                                                                                                                         
            if(roster)w.mk=Mathf.Max(w.mk,Mathf.Min(roster.MaxMk-1,w.mk+MkBonus(wave)));
            return w;
        }
                                                                                                                                           
                                                                                                    
        int MkBonus(int wave)=>roster&&UnityEngine.Random.Range(0,10000)<roster.MkBonusChance(wave)*10000f?1:0;
                                                                                                                                           
                                                                                                                                           
                                                                                               
        public bool DrainOfferable=>!progression||progression.EnergyUnlocked;
        RunWeapon CreateWeapon(WeaponForm form,int v,int wave)
        {
            if(form==WeaponForm.ShieldRing&&!DrainOfferable)v=0;
            var w=new RunWeapon{form=form,variant=v,rounds=ClipSize(form,v)};
            w.element=form switch{WeaponForm.Flamethrower=>ElementKind.Fire,WeaponForm.CryoCannon=>ElementKind.Frost,WeaponForm.ArcCoil=>ElementKind.Lightning,WeaponForm.SporeLauncher=>ElementKind.Poison,_=>ElementKind.None};
            w.builtInElement=w.element!=ElementKind.None;if(w.builtInElement)w.axes=UpgradeAxis.Infusion;
                                                                                           
            w.mk=roster?roster.ParMk(wave):1;
            return w;
        }
        bool Owned(WeaponForm f,int v){foreach(var w in Hand)if(w!=null&&w.form==f&&w.variant==v)return true;foreach(var w in Auto)if(w!=null&&w.form==f&&w.variant==v)return true;return false;}
        public bool OwnsForm(WeaponForm form)
        {
            foreach(var w in Hand)if(w!=null&&w.form==form)return true;
            foreach(var w in Auto)if(w!=null&&w.form==form)return true;
            return false;
        }
        public void RollArmory(int wave)
        {
            if(Offers==null||Offers.Length!=ArmoryOfferCount)Offers=new RunWeapon[ArmoryOfferCount];
            bool handMissing=Array.Exists(Hand,w=>w==null),autoMissing=wave>=3&&Array.Exists(Auto,w=>w==null);
            var offeredForms=new System.Collections.Generic.HashSet<WeaponForm>();
            var upgradable=UpgradableWeapons(wave);
            for(int i=0;i<Offers.Length;i++)
            {
                Offers[i]=null;
                                                                                    
                bool? autoKind=null;
                if(i==0){if(handMissing)autoKind=false;else if(autoMissing)autoKind=true;}
                else if(i==1&&handMissing&&autoMissing)autoKind=true;
                                                                                                                            
                bool guaranteed=i==0||autoKind.HasValue;
                if(!guaranteed&&upgradable.Count>0&&UnityEngine.Random.value<armoryUpgradeChance){Offers[i]=TakeUpgradeOffer(upgradable,wave);offeredForms.Add(Offers[i].form);continue;}
                var eligible=ArmoryForms(wave,autoKind);
                eligible.RemoveAll(offeredForms.Contains);
                if(eligible.Count==0){eligible=ArmoryForms(wave,null);eligible.RemoveAll(offeredForms.Contains);}
                if(i==0&&wave<=5)
                {
                    var newHandForms=eligible.FindAll(form=>(int)form<(int)WeaponForm.OrbitBlades&&form is not (WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher));
                    if(newHandForms.Count>0)eligible=newHandForms;
                }
                Offers[i]=eligible.Count>0?Roll(eligible[UnityEngine.Random.Range(0,eligible.Count)],wave):null;
                if(Offers[i]==null&&i>0&&upgradable.Count>0)Offers[i]=TakeUpgradeOffer(upgradable,wave);
                if(Offers[i]!=null)offeredForms.Add(Offers[i].form);
            }
        }
                                                                                         
        System.Collections.Generic.List<RunWeapon> UpgradableWeapons(int wave)
        {
            var list=new System.Collections.Generic.List<RunWeapon>();if(!roster)return list;
            int ceiling=Mathf.Min(roster.MaxMk,roster.ParMk(wave)+1);
            foreach(var slots in new[]{Hand,Auto})foreach(var w in slots)if(w!=null&&w.mk<ceiling)list.Add(w);
            return list;
        }
                                                                                                                                                                   
        public int UpgradeTargetMk(RunWeapon owned,int wave)=>roster?Mathf.Min(roster.MaxMk,Mathf.Max(owned.mk+1,roster.ParMk(wave)+1)):owned.mk+1;
                                                                                                                                             
                                                                                                                                             
                                                                                                                                         
                                                         
        RunWeapon TakeUpgradeOffer(System.Collections.Generic.List<RunWeapon> upgradable,int wave)
        {
            int pick=UnityEngine.Random.Range(0,upgradable.Count);var owned=upgradable[pick];upgradable.RemoveAt(pick);
            var version=new RunWeapon{form=owned.form,variant=owned.variant,revealed=owned.revealed,axes=owned.axes,element=owned.element,
                builtInElement=owned.builtInElement,rounds=ClipSize(owned.form,owned.variant)};
            int target=roster?Mathf.Min(roster.MaxMk,UpgradeTargetMk(owned,wave)+MkBonus(wave)):owned.mk+1;
                                                                                                                                      
            if(roster&&target>=roster.MaxMk&&owned.mk<roster.MaxMk-1)target=roster.MaxMk-1;
            version.mk=target;
            version.upgradeOf=owned;
            return version;
        }
        public float MkDamage(RunWeapon w)=>w==null||!roster?1f:roster.MkDamage(w.mk);
        System.Collections.Generic.List<WeaponForm> ArmoryForms(int wave,bool? autoKind)
        {
            var forms=new System.Collections.Generic.List<WeaponForm>();
            foreach(var d in roster.definitions)
            {
                bool isAuto=(int)d.form>=(int)WeaponForm.OrbitBlades;
                if(d.retired||d.unlockWave>wave||OwnsForm(d.form)||autoKind.HasValue&&autoKind.Value!=isAuto)continue;
                forms.Add(d.form);
            }
            return forms;
        }
        public bool TakeOffer(int index,int replace=-1)
        {
            if(index<0||index>=Offers.Length||Offers[index]==null)return false;var w=Offers[index];
            if(w.upgradeOf!=null)
            {
                                                                                                                                         
                                                                      
                var owned=w.upgradeOf;
                if(Array.IndexOf(Hand,owned)<0&&Array.IndexOf(Auto,owned)<0)return false;
                owned.mk=Mathf.Max(owned.mk,w.mk);Offers=new RunWeapon[ArmoryOfferCount];return true;
            }
            if(OwnsForm(w.form))return false;var slots=w.IsAuto?Auto:Hand;
            int free=Array.FindIndex(slots,x=>x==null);int at=free>=0?free:replace;if(at<0||at>=slots.Length)return false;slots[at]=w;Offers=new RunWeapon[ArmoryOfferCount];return true;
        }
        static bool BattlefieldFrozen(GameObject go)=>WorldMap.Active&&WorldMap.Active.IsBattlefieldFrozen(go);
        bool CanUseHand=>player&&player.IsAlive&&player.State==CharacterState.Normal&&!player.ActionsBlocked&&dashLeft<=0;
        void CancelInput(RunWeapon w,bool cancelHook=false)
        {
            inputHeld=false;if(w==null)return;
            if(w.charging)WeaponSound(w,WeaponAudioPhase.Cancel);
            w.charging=false;StopTrigger(w);if(w.form!=WeaponForm.ArcCoil||w.variant!=1)w.charge=0;
                                                                                                                                   
            if(cancelHook&&AttackPool.Instance)
                foreach(var actor in AttackPool.Instance.actors)
                    if(actor&&actor.gameObject.activeSelf&&actor.weapon==w&&actor.mode==AttackMode.Hook&&!BattlefieldFrozen(actor.gameObject)&&!(actor.HookReeling&&w.Has(UpgradeAxis.Tame)))actor.Release();
        }
        public void Switch()
        {
                                                                                                                                      
            if(!CanUseHand||HandHookBusy||Hand[1-ActiveIndex]==null)return;var w=Active;
                                                                                                                         
            DeployTurret(w);
            if(w!=null){if(w.hooked&&!BattlefieldFrozen(w.hooked.gameObject))Fling(w);CancelInput(w);}
            ActiveIndex=1-ActiveIndex;
        }
        public void HandleInput(bool pressed,bool held,bool released)
        {
            var w=Active;
            if(!CanUseHand){CancelInput(w,true);return;}
            inputHeld=held;if(w==null||HandHookBusy)return;
            if(pressed&&w.form==WeaponForm.PlasmaLauncher&&w.variant==0&&w.Has(UpgradeAxis.Twist)&&w.stickyActor&&!BattlefieldFrozen(w.stickyActor.gameObject)&&w.stickyActor.TryManualDetonate())return;
            if(w.hooked&&!BattlefieldFrozen(w.hooked.gameObject)){if(pressed)Fling(w);return;}
            if(released&&w.charging&&(w.form is WeaponForm.Rifle or WeaponForm.ScatterCannon)){w.charging=false;Fire(w,true);w.charge=0;return;}
            if(released)
            {
                                                                                                                                      
                if(w.form==WeaponForm.CryoCannon&&w.variant==1&&w.Has(UpgradeAxis.Twist)&&w.heldTime>0)BlizzardShatter(w);
                StopTrigger(w);
            }
            if(pressed&&w.reload>0)V5WeaponAudio.Instance?.Play(V5Sfx.DryClick);                                                                   
            if(!held&&!pressed||w.cooldown>0||w.reload>0||w.inFlight||w.charging)return;
            bool charge=(w.form==WeaponForm.Rifle&&w.variant==0)||(w.form==WeaponForm.Railgun&&w.variant==0)||(w.form==WeaponForm.ScatterCannon&&w.variant==1);
            if(charge){if(pressed){w.charging=true;w.charge=0;Reveal(w);WeaponSound(w,WeaponAudioPhase.ChargeStart);if(w.form==WeaponForm.ScatterCannon)V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponFocusCharge,player.Position);}return;}
            bool repeat=w.form==WeaponForm.Rifle&&w.variant==1||w.form==WeaponForm.Flamethrower&&w.variant==0||w.form==WeaponForm.CryoCannon&&w.variant==1||w.form==WeaponForm.SporeLauncher&&w.variant==1||w.form==WeaponForm.Minigun;
            if(pressed||repeat&&held)Fire(w,false);
        }
                                                                                                            
        public void RevealNow(RunWeapon w)=>Reveal(w);
                                                                                                                   
        public float HitDamage(RunWeapon w){var d=roster.Get(w.form);return d==null?0f:(w.variant==0?d.damageA:d.damageB)*DamageScale(w);}
                                                                                                                                
                                                                                                                                                                    
        public float TheoreticalDps(RunWeapon w){float cycle=CycleSeconds(w);return cycle>0f?HitDamage(w)/cycle:0f;}
        public float CycleSeconds(RunWeapon w){var d=roster.Get(w.form);return d==null?0f:(w.variant==0?d.cooldownA:d.cooldownB)*CooldownScale(w);}
        void Reveal(RunWeapon w){if(w.revealed)return;w.revealed=true;PlayerPrefs.SetInt("anomaly."+(int)w.form+"."+w.variant,1);Revealed?.Invoke(w);}
        public void Tick(float dt)
        {
            float distance=Query.Flat(player.Position-lastPosition).magnitude;lastPosition=player.Position;
            if(!CanUseHand)CancelInput(Active,true);
            WatchPlayerHits();
            PruneTracers();
                                                                               
            float chill=ElementStatus.For(player)?.PlayerChillMultiplier??1f;
            foreach(var w in Hand)
            {
                if(w==null)continue;
                w.cooldown=Mathf.Max(0,w.cooldown-dt*chill*WeaponCooldownRate);w.reload=Mathf.Max(0,w.reload-dt*chill);if(w.cooldown<=0)w.holeReturnSpan=0;
                if(w.charging)
                {
                    w.charge=Mathf.Clamp01(w.charge+dt/(ChargeSeconds(w)*CooldownScale(w)));
                    if(w.form==WeaponForm.Railgun&&w.charge>=1&&CanUseHand&&w==Active){w.charging=false;Fire(w,true);w.charge=0;}
                }
                                                                                                                                                                     
                if(w.form==WeaponForm.ArcCoil&&w.variant==1){if(w==Active)TickCapacitor(w,distance,dt);else DrainStowedCapacitor(w,dt);}
                if(w.hooked&&!BattlefieldFrozen(w.hooked.gameObject))
                {
                    if(!w.hooked.IsAlive)w.hooked=null;
                    else w.hooked.SetMovePosition(HookHoldPoint(w.hooked));
                }
                if(w==Active&&inputHeld&&CanUseHand)
                {w.heldTime+=dt;}
                else{w.heldTime=0;w.heat=Mathf.Max(0,w.heat-dt);}
                if(w.form==WeaponForm.CryoCannon&&w.variant==1)LaySnow(w,dt);
                if(w.burstLeft>0)TickBurst(w,dt);
                if(w.vacuumTimer>0)TickScatterVacuum(w,dt);
                                                                                                                  
                TickLanceTrail(w,dt);TickTurret(w,dt);if(w.bombardLeft>0)TickBombard(w,dt);
            }
            TickChargeLine();
            TickAutoWeapons(GameManager.Instance.WorldDeltaTime);
            TickSporeClouds();TickNeedlerMists(dt);TickRailgunEffects(dt);
            TickHazards(GameManager.Instance.WorldDeltaTime);
            TickExecuteMarks(GameManager.Instance.WorldDeltaTime);TickAnchor(dt);TickMeltdownHaste(dt);TickVoidEnchant(dt);
            TickGround(dt,GameManager.Instance.WorldDeltaTime);
        }
        void FixedUpdate()
        {
            if(dashLeft<=0||dashWeapon==null||!GameManager.Instance.IsCombatActive)return;
            var weapon=dashWeapon;Vector3 from=player.Position;
            float step=Mathf.Min(dashLeft,RocketSpeed(dashLeft)*Time.fixedDeltaTime);
                                                                                                                                      
                                                                                
            const float castBack=.1f;
            bool wallHit=Physics.SphereCast(Query.AtCastHeight(from)-dashDirection*castBack,player.radius,dashDirection,out var hit,step+castBack,GameManager.WallMask);
            float travel=wallHit?Mathf.Min(step,Mathf.Max(0f,hit.distance-castBack)):step;
            DamageHumanRocketContacts(from,travel,weapon);
            if(!player.IsAlive){dashLeft=0;dashWeapon=null;return;}
            if(wallHit)
            {
                player.BounceFromWall(Query.Flat(hit.point)-dashDirection*player.radius,hit.normal,3,!weapon.Has(UpgradeAxis.Tame));
                                                                                      
                if(weapon.Has(UpgradeAxis.Tame))weapon.cooldown=0;
                WeaponSound(weapon,WeaponAudioPhase.Impact);dashLeft=0;dashWeapon=null;return;
            }
                                                                                                                                         
                                                                                                                                       
            bool holeProof=weapon.Has(UpgradeAxis.Tame);
            Vector3 drift=WorldMap.Active&&!holeProof?Query.Flat(WorldMap.Active.GetPull(from,player))*Time.fixedDeltaTime:Vector3.zero;
            Vector3 next=from+dashDirection*travel+drift;
            player.SetMovePosition(next);dashLeft-=travel;
                                                                                                                                          
            if(dashLeft<=0&&holeProof&&WorldMap.Active&&(WorldMap.Active.IsHole(from)||WorldMap.Active.IsHole(next)))dashLeft=RocketSpeed(0f)*Time.fixedDeltaTime;
            if(dashLeft<=0)
            {
                player.SetState(CharacterState.Normal);
                dashWeapon=null;
            }
        }
        void DamageHumanRocketContacts(Vector3 from,float distance,RunWeapon weapon)
        {
            if(Combatant.All.Count>dashContacts.Length)
            {
                                                                                                                        
                int capacity=Mathf.NextPowerOfTwo(Combatant.All.Count);
                Array.Resize(ref dashContacts,capacity);Array.Resize(ref dashContactDistances,capacity);
            }
            int count=0;
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||dashHits.Contains((body,body.SpawnVersion)))continue;
                Vector3 delta=Query.Flat(body.Position-from);float along=Vector3.Dot(delta,dashDirection);
                float combined=body.radius+player.radius+.3f;
                float perpendicularSq=Mathf.Max(0f,delta.sqrMagnitude-along*along);
                if(perpendicularSq>combined*combined||along+combined<0f)continue;
                float enter=Mathf.Max(0f,along-Mathf.Sqrt(combined*combined-perpendicularSq));
                if(enter>distance)continue;
                dashContacts[count]=(body,body.SpawnVersion);dashContactDistances[count++]=enter;
            }
                                                                                                                                
            Array.Sort(dashContactDistances,dashContacts,0,count);
                                                                                                                  
            float contactDamage=dashContactDamage;
            for(int i=0;i<count;i++)
            {
                var contact=dashContacts[i];dashContacts[i]=default;var body=contact.body;
                if(!body||!body.IsAlive||body.SpawnVersion!=contact.spawnVersion||!dashHits.Add(contact))continue;
                                                                                                                                                     
                float push=!weapon.Has(UpgradeAxis.Twist)||body is Enemy{IsBoss:true}?0f:upgrades.rocketCueMax*RocketSpeedShare(Mathf.Max(0f,dashLeft-dashContactDistances[i]));
                Damage(weapon,body,contactDamage,push,dashDirection);
            }
        }
                                                                                                                                              
                                                                                                                                                          
        public float RocketSpeed(float left)
        {
            float cruise=25f/StartingFormCycleFactor(WeaponForm.RocketLauncher),end=Mathf.Clamp(upgrades.rocketEndSpeedShare,.1f,1f);
                                                                                                                                     
            float launch=end>.999f?cruise:cruise*Mathf.Log(1f/end)/(1f-end);
            return launch*RocketSpeedShare(left);
        }
                                                                                                                                         
                                                                    
        public float RocketSpeedShare(float left)=>Mathf.Lerp(1f,Mathf.Clamp(upgrades.rocketEndSpeedShare,.1f,1f),dashRange>0f?Mathf.Clamp01(1f-left/dashRange):0f);
        public float StartingFormDamageFactor(WeaponForm form)=>form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?Mathf.Max(.01f,startingDamageFactor):1f;
        public float StartingFormCycleFactor(WeaponForm form)=>form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?Mathf.Max(.01f,startingCycleFactor):1f;
        float DamageScale(RunWeapon w)=>StartingFormDamageFactor(w.form)*w.Power*MkDamage(w)*(progression?progression.Stats.DamageMultiplier:1)*Mathf.Max(.01f,crowdDamageMultiplier)*SkillDamageMultiplier;
        static SkillLoadout Skills=>GameManager.Instance?GameManager.Instance.skills:null;
                                                                                                              
        static float SkillDamageMultiplier=>Skills?Skills.SkillDamageMultiplier:1f;
                                                                                                       
        static float WeaponCooldownRate=>Skills?Skills.WeaponCooldownRate:1f;
        float ChargeSeconds(RunWeapon w)=>w.form==WeaponForm.Railgun?.5f:w.form==WeaponForm.ScatterCannon?Mathf.Max(.01f,upgrades.focusChargeTime):1f;
        float CooldownScale(RunWeapon w)=>StartingFormCycleFactor(w.form)*w.Tempo*(progression?progression.Stats.CooldownMultiplier:1)/(GameManager.Instance.skills?GameManager.Instance.skills.AttackSpeedMultiplier:1)/MeltdownHaste;
                                                                                                                                                                                 
        public float HookCooldown(RunWeapon w)=>hookCooldown*w.Tempo*(progression?progression.Stats.CooldownMultiplier:1)/(GameManager.Instance.skills?GameManager.Instance.skills.AttackSpeedMultiplier:1)/MeltdownHaste;
        public float GetCooldownDuration(RunWeapon w)
        {
            if(w==null||!roster)return 0f;
            var definition=roster.Get(w.form);if(definition==null)return 0f;
            if(w.holeReturnSpan>0f)return w.holeReturnSpan;                                                                
            if(w.form==WeaponForm.Knife&&w.variant==1)return HookCooldown(w);                                           
            float duration=w.form==WeaponForm.Minigun&&w.variant==0?1f/SpinUpRate(w):w.variant==0?definition.cooldownA:definition.cooldownB;
            return duration*CooldownScale(w);
        }
        float KnockScale=>(progression?progression.Stats.KnockbackMultiplier:1)*(GameManager.Instance.skills?GameManager.Instance.skills.KnockbackMultiplier:1);
        public float UpgradePushScale=>KnockScale;
                                                                                                                                                       
        static void WeaponSound(RunWeapon weapon,WeaponAudioPhase phase=WeaponAudioPhase.Fire,bool strike=false)
        {
            if(weapon==null)return;
            if(!weapon.IsAuto && phase is WeaponAudioPhase.Fire or WeaponAudioPhase.Launch)
            {
                var playerBody=GameManager.Instance?GameManager.Instance.player:null;
                if(playerBody){playerBody.GetComponent<V2ModelPresentation>()?.MarkAttack();FireFeedback(weapon,playerBody);}
            }
            V5WeaponAudio.Weapon(weapon,phase,strike);
        }
                                                                                                                                                    
        static void FireFeedback(RunWeapon weapon,PlayerController body)
        {
            var gm=GameManager.Instance;if(!gm)return;
            var profile=gm.feedback.For(WeaponFeel.Of(weapon));
            Vector3 aim=Query.Flat(body.AimDirection);if(aim.sqrMagnitude<1e-4f)return;aim.Normalize();
            body.VisualKick(-aim,profile.fireRecoil,.1f);
            gm.Kick(-aim,profile.fireKick);
            if(profile.muzzleFlash>0f&&V5Vfx.On){V5Vfx.I.Muzzle(Query.AtCastHeight(body.Position)+aim*(body.radius+.45f),aim,new Color(1f,.82f,.5f),profile.muzzleFlash*1.25f);return;}
            var pool=FxPool.Active;
            if(profile.muzzleFlash>0f&&pool&&pool.muzzleFlash)
                pool.EmitDirectional(pool.muzzleFlash,Query.AtCastHeight(body.Position)+aim*(body.radius+.45f),aim,profile.muzzleFlash,.07f,new Color(1f,.88f,.6f));
        }
        void Fire(RunWeapon w,bool charged)
        {
            Reveal(w);
                                                                                          
            if(w.form==WeaponForm.ArcCoil&&w.variant==1&&w.charge<upgrades.capacitorMinCharge){V5WeaponAudio.Instance?.Play(V5Sfx.DryClick);return;}
            var d=roster.Get(w.form);float damage=(w.variant==0?d.damageA:d.damageB)*DamageScale(w);float range=(w.variant==0?d.rangeA:d.rangeB)*w.Reach;w.cooldown=(w.variant==0?d.cooldownA:d.cooldownB)*CooldownScale(w);
            Vector3 aim=player.AimDirection,at=player.Position,point=at+Vector3.ClampMagnitude(Query.Flat(player.AimPoint-at),range);
            if(w.form==WeaponForm.Rifle&&w.variant==0)
            {
                float hit=Mathf.Lerp(Mathf.Clamp(roster.RifleMinimumChargeDamage,0f,d.damageA),d.damageA,w.charge)*DamageScale(w),push=Mathf.Lerp(5,10,w.charge);
                                                                                                                                      
                                                                                                                                         
                bool full=w.Has(UpgradeAxis.Twist)&&w.charge>=.999f;
                var round=Bolt(w,aim,hit,push,range,80,true);
                if(round){round.pierceGrowth=full?upgrades.chargedPierceGrowth:0f;round.pierceGrowthCap=upgrades.chargedPierceGrowthCap;}
                if(full)ChargedMuzzleBlast(w,aim);
            }
            else if(w.form==WeaponForm.Rifle)
            {
                                                                                                                                          
                                                                                                                               
                Vector3 dir=-aim;
                if(w.Has(UpgradeAxis.Tame)){var front=(ClipSize(w.form,w.variant)-w.rounds)%2==0?FrontGuardTarget():null;if(front)dir=Query.Flat(front.Position-player.Position).normalized;player.Push(aim,upgrades.reverseTameRecoil);}
                Bolt(w,dir,damage,0,range,30,false,1);
                SpendRound(w,.6f);
            }
            else if(w.form==WeaponForm.Knife&&w.variant==0)
            {
                var a=Bolt(w,aim,damage,0,range,knifeOutboundSpeed/StartingFormCycleFactor(w.form)*KnifeCatchSpeed(w));if(a)a.knifeSpeedFactor=KnifeCatchSpeed(w);
                                                                                    
                                                                                                                     
                if(a){a.mode=AttackMode.Knife;a.targetsLeft=(w.Has(UpgradeAxis.Reach)?6:4)+(w.knifeCaught?Mathf.Max(0,upgrades.knifeCatchTargets):0);a.remaining=float.PositiveInfinity;a.ownerDamage=d.damageA;w.inFlight=true;w.knifeCaught=false;}
            }
            else if(w.form==WeaponForm.Knife){var a=AttackPool.Instance.Acquire();if(a){a.Configure(AttackMode.Hook,player,w,SpawnPoint(aim,player.radius+.2f),aim,Mathf.Max(1f,DamageScale(w)),0,range,hookExtendSpeed/StartingFormCycleFactor(w.form),0,float.PositiveInfinity);w.hookBusy=true;w.hookChainUsed=false;WeaponSound(w,WeaponAudioPhase.Launch);}}
            else if(w.form==WeaponForm.RocketLauncher&&w.variant==0){float cycle=StartingFormCycleFactor(w.form);var strike=Field(w,AttackMode.Blast,point,damage,5,3*w.Reach,1.1f*cycle,cycle,!w.Has(UpgradeAxis.Tame));if(strike)strike.ownerDamage=d.damageA;}
            else if(w.form==WeaponForm.RocketLauncher){dashLeft=dashRange=range;dashDirection=aim;dashWeapon=w;dashContactDamage=Mathf.Max(1f,d.damageB*DamageScale(w));dashHits.Clear();player.SetState(CharacterState.Dashing);WeaponSound(w,WeaponAudioPhase.Launch);}
            else if(w.form==WeaponForm.ScatterCannon&&w.variant==0)ScatterBlast(w,range,damage);
            else if(w.form==WeaponForm.ScatterCannon)Focus(w,aim,damage);
            else if(w.form==WeaponForm.PhaseDagger&&w.variant==0)
            {
                var dagger=Bolt(w,aim,damage,0,range,Mathf.Max(1f,DaggerSpeed));
                if(dagger){dagger.mode=AttackMode.Swap;dagger.targetsLeft=w.Has(UpgradeAxis.Twist)?2:1;}
            }
            else if(w.form==WeaponForm.PhaseDagger)
            {
                if(w.recallPlanted&&w.recallActor&&w.recallActor.gameObject.activeSelf)
                {
                    if(!ValidTeleportPoint(w.recallPoint,player.radius)){GameManager.Instance.hud?.Toast(ArenaText.Pair("Recall point is blocked","Recall point is blocked"),new Color(.8f,.4f,1f));return;}
                    Vector3 departure=player.Position;TeleportBody(player,w.recallPoint);
                    RecallBlast(w,departure,w.recallPoint,2*w.Reach,damage);
                    w.recallActor.Release();
                }
                else
                {
                    var dagger=LaunchArc(w,AttackMode.Recall,point,0,0,0,float.PositiveInfinity,Mathf.Max(1f,DaggerSpeed),RecallArcHeight,false);
                    if(dagger){w.recallActor=dagger;w.inFlight=true;w.recallPlanted=false;}
                }
            }
            else if(w.form==WeaponForm.PlasmaLauncher){var a=Bolt(w,aim,damage,5,range,9);if(a){a.mode=w.variant==0?AttackMode.Sticky:AttackMode.Bouncy;a.radius=3;a.ricochets=w.variant==0?0:3;a.hurtOwner=true;a.ownerDamage=w.variant==0?d.damageA:d.damageB;if(w.variant==0)w.stickyActor=a;else a.remaining=3;}}
            else if(w.form==WeaponForm.Flamethrower&&w.variant==0)Stream(w,range,damage);
                                                                                
            else if(w.form==WeaponForm.Flamethrower){var a=LaunchArc(w,AttackMode.Lob,point,damage,0,2.5f*w.Reach,4,LobSpeed,LobArcHeight,!w.Has(UpgradeAxis.Tame));if(a)a.ownerDamage=d.damageB*upgrades.wildfireSelfShare;else LandingBlocked(w);}
            else if(w.form==WeaponForm.CryoCannon&&w.variant==0)ShatterFan(w,aim,range,damage);
            else if(w.form==WeaponForm.CryoCannon)BlizzardSpray(w,range,damage);
            else if(w.form==WeaponForm.ArcCoil&&w.variant==0){Chain(w,range,damage,w.Has(UpgradeAxis.Reach)?7:5);}
            else if(w.form==WeaponForm.ArcCoil)
            {
                                                                                                        
                CapacitorDischarge(w,range,damage);
            }
            else if(w.form==WeaponForm.SporeLauncher&&w.variant==0){var a=LaunchArc(w,AttackMode.Lob,point,damage,0,3.5f*w.Reach,Mathf.Max(0f,upgrades.cloudSeconds),LobSpeed,LobArcHeight,false);if(a){a.tickInterval=.5f;TrackSporeCloud(a,w);}else LandingBlocked(w);}
                                                                                                                         
            else if(w.form==WeaponForm.SporeLauncher){Bolt(w,aim,damage,0,range,35);if(SpendRound(w,2))NeedlerReloadMist(w);}
            else if(w.form==WeaponForm.Railgun&&w.variant==0)
            {
                                                                                                                            
                Vector3 brace=default;bool braced=w.Has(UpgradeAxis.Tame)&&LanceBraced(aim,out brace);
                                                                                                                                      
                var a=Bolt(w,aim,braced?damage*upgrades.lanceBraceDamage:damage,0,range,120,true);if(a){a.ignoreShieldReduction=true;a.pierceDamageRetention=.75f;StartLanceBeam(a);}
                                                                                                                                                         
                if(braced)Fx.Ring(brace,.6f,new Color(1f,.85f,.5f,.9f));else player.Push(-aim,LanceRecoil(w));
            }
            else if(w.form==WeaponForm.Railgun)Execute(w,range,damage);
            else if(w.form==WeaponForm.Minigun){Bolt(w,aim,damage,0,range,40);if(w.variant==0)w.cooldown=1/SpinUpRate(w)*CooldownScale(w);else{float before=w.heat;w.heat+=.1f;MeltdownWarning(w,before);if(w.heat>=4)Meltdown(w);}}
                                                                                                                                              
            else if(w.form==WeaponForm.SwarmLauncher&&w.variant==0)Barrage(w,aim,range,damage,d.damageA);
            else if(w.form==WeaponForm.SwarmLauncher)Bombard(w,aim,range,damage,d.damageB);
        }
                                                                                                                   
        float SpinUpRate(RunWeapon w)=>Mathf.Max(.1f,Mathf.Lerp(upgrades.spinUpStartRate,upgrades.spinUpTopRate,Mathf.Clamp01(w.heldTime/3f)))*(w==Active&&Anchored?1f+Mathf.Max(0f,upgrades.spinBraceRateBonus):1f);
                                                                                                                                 
                                                                                                                                 
        void Bombard(RunWeapon w,Vector3 aim,float range,float damage,float selfDamage)
        {
            w.bombardRefunded=false;
                                                                                                                                      
            if(w.Has(UpgradeAxis.Twist)){w.bombardLeft=Mathf.Max(1,upgrades.bombardBlasts);w.bombardTimer=0f;w.bombardOrigin=Query.Flat(player.Position);WeaponSound(w,WeaponAudioPhase.Launch);TickBombard(w,0f);return;}
            int count=Mathf.Max(1,upgrades.bombardBlasts);float start=upgrades.bombardStart,radius=2*w.Reach;
            float spacing=count>1?Mathf.Max(0f,range-start)/(count-1):0f;
            float wallAt=Physics.Raycast(Query.AtCastHeight(player.Position),aim,out var wall,range+radius,GameManager.WallMask,QueryTriggerInteraction.Ignore)?wall.distance:float.PositiveInfinity;
            Vector3 origin=player.Position;
            for(int i=0;i<count;i++)
            {
                float distance=start+i*spacing;
                if(distance>=wallAt)continue;
                float delay=upgrades.bombardFuse+upgrades.bombardStep*i;
                Vector3 at=origin+aim*distance;
                var blast=Field(w,AttackMode.Blast,at,damage,5,radius,.2f,delay,!w.Has(UpgradeAxis.Tame));
                if(!blast)continue;
                blast.ownerDamage=selfDamage;
                Fx.Ring(at,radius,new Color(1f,.55f,.25f,.8f),Mathf.Max(.05f,delay));
            }
        }
                                                                                                                                               
        void LandingBlocked(RunWeapon w)
        {
            w.cooldown=0;
            GameManager.Instance.hud?.Toast(ArenaText.Pair("Landing spot is blocked","Landing spot is blocked"),new Color(1f,.6f,.3f));
        }
                                                                                                                                                         
        Vector3? LandingPoint(Vector3 desired)
        {
            var world=WorldMap.Active;if(!world||!player)return null;
            Vector3 from=Query.Flat(player.Position);
            desired=Query.Flat(world.TrialActive?world.trialPlatform.position+Vector3.ClampMagnitude(Query.Flat(desired-world.trialPlatform.position),Mathf.Max(0f,world.trialRadius-.2f)):world.ClampPoint(desired,.2f));
            Vector3 delta=desired-from;float length=delta.magnitude;Vector3 dir=length>1e-4f?delta/length:Vector3.zero;
            for(float distance=length;distance>=0f;distance-=.25f){Vector3 at=from+dir*distance;if(ValidTeleportPoint(at,.2f))return at;}
            return null;
        }
                                                                                                                                                                 
        Vector3 SpawnPoint(Vector3 dir,float distance)
        {
            dir=Query.Flat(dir).normalized;Vector3 from=Query.AtCastHeight(player.Position);
            if(Physics.Linecast(from,from+dir*distance,out var wall,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0f,wall.distance-upgrades.spawnWallBackoff);
            return player.Position+dir*distance;
        }
                                                                                   
        Vector3 Muzzle(Vector3 dir)=>Query.AtCastHeight(player.Position)+Query.Flat(dir).normalized*(player.radius+.45f);
                                                                                                                 
        public Vector3 HookHoldPoint(Combatant held)
        {
            Vector3 aim=Query.Flat(player.AimDirection).normalized,from=Query.AtCastHeight(player.Position);float distance=player.radius+held.radius+.3f;
            if(Physics.SphereCast(from,held.radius,aim,out var wall,distance,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0f,Mathf.Min(distance,wall.distance-upgrades.hookWallGap));
                                                                                                                                     
            if(Physics.Raycast(from,aim,out var face,distance+held.radius,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0f,Mathf.Min(distance,face.distance-held.radius-upgrades.hookWallGap));
            return player.Position+aim*distance;
        }
        static bool WallBlocks(Vector3 from,Vector3 to)=>Query.WallBetween(Query.AtCastHeight(from),Query.AtCastHeight(to),out _);
                                                                               
        public int ClipSize(WeaponForm f,int v)=>f==WeaponForm.Rifle&&v==1?Mathf.Max(1,rifleClipSize):f==WeaponForm.ScatterCannon&&v==0?2:f==WeaponForm.SporeLauncher&&v==1?24:int.MaxValue;
                                                                                                              
        bool SpendRound(RunWeapon w,float reload){if(--w.rounds>0)return false;w.reload=reload*CooldownScale(w);w.rounds=ClipSize(w.form,w.variant);return true;}
        void Fling(RunWeapon w){if(!w.hooked||!w.hooked.IsAlive||BattlefieldFrozen(w.hooked.gameObject))return;var target=w.hooked;target.SetState(CharacterState.Normal);target.Throw(player.AimDirection,8,new ThrowImpact{dealer=player,damage=roster.Get(w.form).damageB*DamageScale(w),sizeExponent=1,radius=1.5f*w.Reach,knockback=5*KnockScale,element=w.element,chainHook=w.Has(UpgradeAxis.Twist)&&!w.hookChainUsed?w:null});if(target.State==CharacterState.Thrown)WeaponSound(w,WeaponAudioPhase.Launch);w.hooked=null;w.cooldown=Mathf.Max(w.cooldown,HookCooldown(w));}
        void LimitWildfire(RunWeapon w)
        {
            int count=0;AttackActor oldest=null;
            foreach(var actor in AttackPool.Instance.actors)
            {
                if(!actor||!actor.gameObject.activeSelf||actor.weapon!=w||actor.mode!=AttackMode.Field||BattlefieldFrozen(actor.gameObject))continue;
                count++;if(!oldest||actor.Sequence<oldest.Sequence)oldest=actor;
            }
            if(count>=3&&oldest)oldest.Release();
        }
        public void WildfireLanded(RunWeapon w)=>LimitWildfire(w);
        AttackActor LaunchArc(RunWeapon w,AttackMode mode,Vector3 destination,float hit,float push,float area,float duration,float velocity,float arcHeight,bool self)
        {
            var landing=LandingPoint(destination);if(!landing.HasValue)return null;destination=landing.Value;
            var actor=AttackPool.Instance.Acquire();if(!actor)return null;
            actor.Configure(mode,player,w,SpawnPoint(player.AimDirection,.7f),player.AimDirection,hit,push*KnockScale,0,Mathf.Max(1f,velocity),area,float.PositiveInfinity);
            actor.hurtOwner=self;
            actor.StartArc(destination,Mathf.Max(0f,arcHeight),duration);
            WeaponSound(w,WeaponAudioPhase.Launch);
            return actor;
        }
        public bool CompleteSwap(AttackActor dagger,Combatant target)
        {
            if(!dagger||dagger.owner!=player||!player||!player.IsAlive||!target||!target.IsAlive||target.State==CharacterState.Hooked||target is Enemy{Immovable:true})return false;
            Vector3 playerPoint=player.Position,targetPoint=target.Position;
            if(!ValidTeleportPoint(targetPoint,player.radius)||!ValidTeleportPoint(playerPoint,target.radius))return false;
                                                                                                            
            TeleportBody(player,targetPoint);TeleportBody(target,playerPoint);
            if(dagger.weapon.Has(UpgradeAxis.Tame))player.ProtectFor(upgrades.swapProtection);
            return true;
        }
        static void TeleportBody(Combatant body,Vector3 destination)
        {
            destination.y=0f;body.CancelDisplacement();
            body.GetComponent<Rigidbody>().position=destination;body.transform.position=destination;
        }
        public static bool ValidTeleportPoint(Vector3 destination,float bodyRadius)
        {
            var world=WorldMap.Active;if(!world)return false;
            bool inside=world.TrialActive?Query.Flat(destination-world.trialPlatform.position).magnitude<=world.trialRadius-bodyRadius:world.Contains(destination,bodyRadius);
            return inside&&!world.IsWall(destination,bodyRadius)&&!world.IsHole(destination)
                &&!Physics.CheckSphere(Query.AtCastHeight(destination),bodyRadius,GameManager.WallMask,QueryTriggerInteraction.Ignore);
        }
        Combatant RayTarget(Vector3 dir,float range){Combatant best=null;float distance=range;foreach(var c in Combatant.All){if(c==player||!c.IsAlive)continue;Vector3 d=Query.Flat(c.Position-player.Position);float along=Vector3.Dot(d,dir);if(along<0||along>distance||(d-dir*along).magnitude>c.radius+.3f)continue;if(Physics.Linecast(Query.AtCastHeight(player.Position),Query.AtCastHeight(c.Position),GameManager.WallMask))continue;best=c;distance=along;}return best;}
        bool InCone(Combatant c,float range,float angle){Vector3 d=Query.Flat(c.Position-player.Position);return d.magnitude<=range+c.radius&&Vector3.Angle(player.AimDirection,d)<=angle*.5f;}
        void Cone(RunWeapon w,float range,float angle,float hit,float push){Fx.Cone(player.Position,player.AimDirection,range,angle,w.element);foreach(var c in Combatant.All)if(c!=player&&c.IsAlive&&InCone(c,range,angle)&&!WallBlocks(player.Position,c.Position))Damage(w,c,hit,push,Query.Flat(c.Position-player.Position).normalized);}
                                                                                                                                                                            
        void Area(RunWeapon w,Vector3 at,float radius,float hit,float push,bool self,float selfHit=-1f){foreach(var c in Combatant.All)if(c.IsAlive&&(self||c!=player)&&(Query.Flat(c.Position-at)).magnitude<=radius+c.radius&&!WallBlocks(at,c.Position))Damage(w,c,c==player&&selfHit>=0f?selfHit:hit,push,Query.Flat(c.Position-at).normalized);Fx.Ring(at,radius,ElementCombat.ColorFor(w.element));}
                                                                                                                                                                                 
        void Damage(RunWeapon w,Combatant c,float hit,float push,Vector3 dir,bool noLightningJump=false,bool continuous=false){var info=DamageInfo.Attack(hit,player,push<0?-dir:dir,Mathf.Abs(push)*KnockScale,HitKind.Blast);info.feel=WeaponFeel.Of(w);info.element=w.element;info.builtInElement=w.builtInElement;info.weapon=w;info.noLightningJump=noLightningJump;info.continuousSource=continuous?w:null;lastDamageAccepted=c.ReceiveDamage(info);if(lastDamageAccepted)WeaponSound(w,WeaponAudioPhase.Impact);}
        AttackActor Bolt(RunWeapon w,Vector3 dir,float hit,float push,float reach,float velocity,bool pierce=false,int ricochet=0){var a=AttackPool.Instance.Acquire();if(!a)return null;a.Configure(AttackMode.Bolt,player,w,SpawnPoint(dir,.7f),dir,hit,push*KnockScale,reach,velocity,0,reach/velocity+3);a.pierce=pierce;a.ricochets=ricochet;WeaponSound(w,w.form is WeaponForm.Knife or WeaponForm.PhaseDagger?WeaponAudioPhase.Launch:WeaponAudioPhase.Fire);return a;}
        AttackActor Field(RunWeapon w,AttackMode mode,Vector3 at,float hit,float push,float radius,float life,float delay,bool self){var a=AttackPool.Instance.Acquire();if(!a)return null;a.Configure(mode,player,w,at,player.AimDirection,hit,push*KnockScale,0,0,radius,life+delay+.1f);a.delay=delay;a.hurtOwner=self;WeaponSound(w,mode==AttackMode.Blast?WeaponAudioPhase.Launch:WeaponAudioPhase.Fire);return a;}
                                                                                                                             
                                                                                                         
        void Chain(RunWeapon w,float range,float hit,int count)
        {
            var first=ChainFirstTarget(range);upgradeHits.Clear();
                                                                                                          
            if(!first){WeaponSound(w,WeaponAudioPhase.Cancel);return;}
                                                                                  
            count+=w.chainStoredJumps;w.chainStoredJumps=0;
            Vector3 at=player.Position,from=Muzzle(Query.Flat(first.Position-player.Position));
            Color arc=ElementCombat.ColorFor(ElementKind.Lightning);
            for(int i=0;i<count;i++)
            {
                Combatant next=i==0?first:null;
                if(i>0)
                {
                                                                                                                                      
                    float best=float.PositiveInfinity;
                    foreach(var body in Combatant.All)
                    {
                        float distance=Query.Flat(body.Position-at).sqrMagnitude;
                        if(body==player||!body.IsAlive||upgradeHits.Contains(body)||distance>16||WallBlocks(at,body.Position))continue;
                        float score=w.Has(UpgradeAxis.Twist)?Query.Flat(body.Position-player.AimPoint).sqrMagnitude:distance;
                        if(score<best){next=body;best=score;}
                    }
                }
                if(!next){ReturnCurrent(w,count-i);break;}
                if(i==0)WeaponSound(w);
                if(V5Vfx.On)V5Vfx.I.Bolt(from,Query.AtCastHeight(next.Position),arc,Mathf.Max(.9f,upgrades.chainArcWidth*2.5f),Mathf.Max(.09f,upgrades.chainArcSeconds));
                AddTracer(from,Query.AtCastHeight(next.Position),upgrades.chainArcWidth,upgrades.chainArcSeconds,arc,V5Vfx.On);
                upgradeHits.Add(next);Damage(w,next,hit,0,Query.Flat(next.Position-at).normalized,true);at=next.Position;from=Query.AtCastHeight(at);Fx.Ring(at,.6f,ElementCombat.ColorFor(w.element));
            }
        }
        Combatant ChainFirstTarget(float range)
        {
            Combatant best=null;float nearest=float.PositiveInfinity;Vector3 aim=Query.Flat(player.AimDirection);
            foreach(var c in Combatant.All)
            {
                if(c==player||!c.IsAlive)continue;
                Vector3 d=Query.Flat(c.Position-player.Position);float distance=d.magnitude;
                if(distance>range+c.radius||distance>=nearest)continue;
                if(distance>c.radius&&Vector3.Angle(aim,d)>upgrades.chainConeAngle*.5f)continue;
                if(WallBlocks(player.Position,c.Position))continue;
                best=c;nearest=distance;
            }
            return best;
        }
                                                                                                                  
        Combatant NearestVisible(float range,Predicate<Combatant> accept=null)
        {
            Combatant best=null;float nearest=range*range;
            foreach(var c in Combatant.All)
            {
                if(c==player||!c.IsAlive)continue;
                float d=Query.Flat(c.Position-player.Position).sqrMagnitude;
                if(d>=nearest||accept!=null&&!accept(c)||WallBlocks(player.Position,c.Position))continue;
                best=c;nearest=d;
            }
            return best;
        }
                                                                                                                                   
                                                                                                                                      
                                                     
        public DraftCard RollUpgradeCard(WeaponUpgradeOffer avoid=null,int wave=0)
        {
            var choices=new System.Collections.Generic.List<RunWeapon>();
            foreach(var slots in new[]{Hand,Auto})foreach(var w in slots)
                if(w!=null&&w.revealed&&AvailableAxes(w).Count>0&&!OnlyOffers(w,avoid))choices.Add(w);
            if(choices.Count==0)return avoid!=null?RollUpgradeCard(null,wave):null;
            var weapon=choices[UnityEngine.Random.Range(0,choices.Count)];var axes=AvailableAxes(weapon);
            bool canReinfuse=CanReinfuse(weapon);
            if(avoid!=null&&avoid.Weapon==weapon){axes.Remove(avoid.Axis);if(avoid.Axis==UpgradeAxis.Infusion)canReinfuse=false;}
                                                                                                                              
            bool reinfuse=canReinfuse&&(axes.Count==0||UnityEngine.Random.value<upgrades.reinfuseChance);
            var upgrade=reinfuse?UpgradeAxis.Infusion:PickDirection(axes,wave>0?wave:CurrentWave);
                                                                                                                
            ElementKind own=OwnElement(weapon),element=upgrade==UpgradeAxis.Infusion?RollElement(own):own;
            string desc=upgrade==UpgradeAxis.Infusion?ArenaText.Pair("Infuse ","Infuse ")+ArenaTerms.Element(element)+(own!=ElementKind.None?ArenaText.Pair(", replaces ",", replaces ")+ArenaTerms.Element(own):""):upgrade switch{UpgradeAxis.Power=>ArenaText.Pair("Damage +40%","Damage +40%"),UpgradeAxis.Tempo=>ArenaText.Pair("Cooldown, charge and reload -30%","Cooldown, charge and reload -30%"),UpgradeAxis.Reach=>ArenaText.Pair("Range, area or targets +40%","Range, area or targets +40%"),UpgradeAxis.Twist=>ArenaText.Pair("Adds a twist to this weapon's attack","Adds a twist to this weapon's attack"),_=>ArenaText.Pair("Improves this pairing's trait","Improves this pairing's trait")};
            if(upgrade is UpgradeAxis.Twist or UpgradeAxis.Tame)desc=(int)weapon.form<3?CoreAnomalyUpgradeDescription(weapon,upgrade):ArenaTerms.ExpandedUpgradeDescription(weapon.form,weapon.variant,upgrade);
                                                                                                                
            if(upgrade==UpgradeAxis.Infusion){string rule=ArenaTerms.ElementRule(element);if(rule.Length>0)desc+="\n"+rule;}
                                                                                                          
            var axesBefore=weapon.axes;var elementBefore=own;
            return new DraftCard(DraftCardKind.Weapon,weapon.Label+" / "+ArenaTerms.Upgrade(upgrade),desc,_=>{weapon.axes|=upgrade;if(upgrade==UpgradeAxis.Infusion)SetOwnElement(weapon,element);return true;},payload:new WeaponUpgradeOffer(weapon,upgrade,element),
                stillFits:()=>(Array.IndexOf(Hand,weapon)>=0||Array.IndexOf(Auto,weapon)>=0)&&weapon.axes==axesBefore&&OwnElement(weapon)==elementBefore);
        }
        static int CurrentWave=>GameManager.Instance&&GameManager.Instance.waves?Mathf.Max(1,GameManager.Instance.waves.WaveIndex+1):1;
                                                                                                                                            
                                                                                                                                         
                                                                           
        UpgradeAxis PickDirection(System.Collections.Generic.List<UpgradeAxis> axes,int wave)
        {
            var rules=progression?progression.Rules:null;
            float total=0f;if(rules)foreach(var axis in axes)total+=rules.DirectionWeight(wave,axis);
            if(total<=0f)return axes[UnityEngine.Random.Range(0,axes.Count)];
            float roll=UnityEngine.Random.value*total;
            var picked=axes[0];
            foreach(var axis in axes)
            {
                float weight=rules.DirectionWeight(wave,axis);if(weight<=0f)continue;
                picked=axis;if(roll<weight)break;roll-=weight;
            }
            return picked;
        }
                                                                                                
        bool OnlyOffers(RunWeapon w,WeaponUpgradeOffer avoid)
        {
            if(avoid==null||avoid.Weapon!=w)return false;
            var axes=AvailableAxes(w);axes.Remove(avoid.Axis);
            return axes.Count==0&&(!CanReinfuse(w)||avoid.Axis==UpgradeAxis.Infusion);
        }
                                                                                                                                                        
        string CoreAnomalyUpgradeDescription(RunWeapon w,UpgradeAxis axis)
        {
            bool twist=axis==UpgradeAxis.Twist;var u=upgrades;
            string P(float share)=>$"{share*100f:0.#}";
            return (w.form,w.variant,twist) switch
            {
                                                                                                                                                                     
                (WeaponForm.Rifle,0,true)=>ArenaText.Pair(                    $"A full-charge release also blasts the cone ahead, knocking enemies within {u.chargedBlastRange:0.#} cells back {u.chargedBlastPush:0.#} cells; that round deals +{P(u.chargedPierceGrowth)}% damage for each enemy it has passed (up to +{P(u.chargedPierceGrowthCap)}%).",                    $"A full-charge release also blasts the cone ahead, knocking enemies within {u.chargedBlastRange:0.#} cells back {u.chargedBlastPush:0.#} cells; that round deals +{P(u.chargedPierceGrowth)}% damage for each enemy it has passed (up to +{P(u.chargedPierceGrowthCap)}%)."),
                (WeaponForm.Rifle,0,false)=>ArenaText.Pair(                    $"Charging no longer slows you, and enemy attacks hit you for {P(1f-u.chargeTameDamageTaken)}% less instead of more; non-boss enemies on your aim line move {P(1f-u.chargeLineSlowFactor)}% slower.",                    $"Charging no longer slows you, and enemy attacks hit you for {P(1f-u.chargeTameDamageTaken)}% less instead of more; non-boss enemies on your aim line move {P(1f-u.chargeLineSlowFactor)}% slower."),
                (WeaponForm.Rifle,1,true)=>ArenaText.Pair($"On its first wall bounce a round splits in two, each at {P(u.reverseSplitShare)}% damage.",$"On its first wall bounce a round splits in two, each at {P(u.reverseSplitShare)}% damage."),
                (WeaponForm.Rifle,1,false)=>ArenaText.Pair(                    $"Rounds still leave from your back, and each one's recoil pushes you {u.reverseTameRecoil:0.#} cells along your aim; while an enemy is within {u.reverseGuardRange:0.#} cells in front, every other round flies forward at it.",                    $"Rounds still leave from your back, and each one's recoil pushes you {u.reverseTameRecoil:0.#} cells along your aim; while an enemy is within {u.reverseGuardRange:0.#} cells in front, every other round flies forward at it."),
                (WeaponForm.Knife,0,true)=>ArenaText.Pair($"When the blade comes home it spins: enemies within {u.knifeSpinRadius:0.#} cells that this return did not cut take {P(u.knifeSpinShare)}% damage.",$"When the blade comes home it spins: enemies within {u.knifeSpinRadius:0.#} cells that this return did not cut take {P(u.knifeSpinShare)}% damage."),
                (WeaponForm.Knife,0,false)=>ArenaText.Pair(                    $"The returning blade no longer cuts you; sweeping through you is a catch: {P(1f-u.knifeCatchCooldownShare)}% shorter cooldown, and the next throw bounces to {u.knifeCatchTargets} more enemies and flies {P(u.knifeCatchSpeed-1f)}% faster.",                    $"The returning blade no longer cuts you; sweeping through you is a catch: {P(1f-u.knifeCatchCooldownShare)}% shorter cooldown, and the next throw bounces to {u.knifeCatchTargets} more enemies and flies {P(u.knifeCatchSpeed-1f)}% faster."),
                (WeaponForm.Knife,1,true)=>ArenaText.Pair("A flung enemy that crashes into another hooks the one it hit; once per manual hook.","A flung enemy that crashes into another hooks the one it hit; once per manual hook."),
                (WeaponForm.Knife,1,false)=>ArenaText.Pair(                    $"During the two-way pull nothing interrupts you and attacks cannot hurt you; as you meet, other enemies within {u.hookShockRadius:0.#} cells are knocked back {u.hookShockPush:0.#} cells. An empty hook that hits a wall pulls you to it.",                    $"During the two-way pull nothing interrupts you and attacks cannot hurt you; as you meet, other enemies within {u.hookShockRadius:0.#} cells are knocked back {u.hookShockPush:0.#} cells. An empty hook that hits a wall pulls you to it."),
                (WeaponForm.RocketLauncher,0,true)=>ArenaText.Pair($"Enemies the strike kills explode where they fall: radius {u.strikeChainRadius:0.#}, {P(u.strikeChainShare)}% damage, no further chain.",$"Enemies the strike kills explode where they fall: radius {u.strikeChainRadius:0.#}, {P(u.strikeChainShare)}% damage, no further chain."),
                (WeaponForm.RocketLauncher,0,false)=>ArenaText.Pair($"Your own blast no longer hurts you; it pushes you {u.strikeJumpDistance:0.#} cells instead, with no wall damage.",$"Your own blast no longer hurts you; it pushes you {u.strikeJumpDistance:0.#} cells instead, with no wall damage."),
                (WeaponForm.RocketLauncher,1,true)=>ArenaText.Pair($"Enemies you hit are knocked along your flight, farther the faster you still fly: {u.rocketCueMax:0.#} cells at launch, {u.rocketCueMax*Mathf.Clamp(u.rocketEndSpeedShare,.1f,1f):0.#} at the end.",$"Enemies you hit are knocked along your flight, farther the faster you still fly: {u.rocketCueMax:0.#} cells at launch, {u.rocketCueMax*Mathf.Clamp(u.rocketEndSpeedShare,.1f,1f):0.#} at the end."),
                (WeaponForm.RocketLauncher,1,false)=>ArenaText.Pair("In flight black holes neither drag nor swallow you; hitting a wall no longer costs you HP and resets this flight's cooldown.","In flight black holes neither drag nor swallow you; hitting a wall no longer costs you HP and resets this flight's cooldown."),
                _=>ArenaText.Pair("Adds a twist to this weapon's attack","Adds a twist to this weapon's attack")
            };
        }
        bool CanReinfuse(RunWeapon w)=>!w.builtInElement&&OwnElement(w)!=ElementKind.None;
                                                                                                                     
        static readonly UpgradeAxis[] HandDraftAxes={UpgradeAxis.Infusion,UpgradeAxis.Twist,UpgradeAxis.Tame};
        static readonly UpgradeAxis[] AutoDraftAxes={UpgradeAxis.Infusion};
        static System.Collections.Generic.List<UpgradeAxis> AvailableAxes(RunWeapon w)
        {
            var axes=new System.Collections.Generic.List<UpgradeAxis>();
            foreach(var axis in w.IsAuto?AutoDraftAxes:HandDraftAxes)
                if(!w.Has(axis)&&(axis!=UpgradeAxis.Infusion||!w.builtInElement))axes.Add(axis);
                                                                                                                                                   
            return axes;
        }
        static ElementKind RollElement(ElementKind previous)
        {
            int count=previous==ElementKind.None?7:6;
            int value=UnityEngine.Random.Range(1,count+1);
            if(previous!=ElementKind.None&&value>=(int)previous)value++;
            return (ElementKind)value;
        }
        public void FreezeBattlefieldForTrial(bool freeze)
        {
            if(freeze)
            {
                if(trialWeaponStates.Count>0)return;
                trialActiveIndex=ActiveIndex;trialInputHeld=inputHeld;frozenAutoClock=autoClock;
                FreezeElementWeaponsForTrial();FreezeAutoWeaponsForTrial(true);
                frozenBattlefieldAttacks.Clear();frozenHeldBodies.Clear();
                if(AttackPool.Instance)foreach(var actor in AttackPool.Instance.actors)
                    if(actor&&(actor.gameObject.activeSelf||BattlefieldFrozen(actor.gameObject)))frozenBattlefieldAttacks.Add(actor);
                for(int i=0;i<autoHitTimes.Length;i++)
                {
                    frozenAutoHitTimes[i].Clear();foreach(var pair in autoHitTimes[i])frozenAutoHitTimes[i].Add(pair.Key,pair.Value);
                    autoHitTimes[i].Clear();
                }
                foreach(var slots in new[]{Hand,Auto})foreach(var w in slots)
                {
                    if(w==null)continue;
                    var saved=new RunWeapon
                    {
                        cooldown=w.cooldown,reload=w.reload,heldTime=w.heldTime,heat=w.heat,charge=w.charge,chargeFullTime=w.chargeFullTime,
                        executeNextShot=w.executeNextShot,chainStoredJumps=w.chainStoredJumps,holeReturnSpan=w.holeReturnSpan,rounds=w.rounds,charging=w.charging,inFlight=w.inFlight,recallPlanted=w.recallPlanted,
                        recallPoint=w.recallPoint,recallActor=w.recallActor,hooked=w.hooked,whirled=w.whirled,hookBusy=w.hookBusy,channelledField=w.channelledField,
                        whirlLeft=w.whirlLeft,orbitAngle=w.orbitAngle
                    };
                    saved.stickyActor=w.stickyActor;
                    saved.turretLeft=w.turretLeft;saved.turretClock=w.turretClock;saved.turretPoint=w.turretPoint;saved.turretDir=w.turretDir;
                    trialWeaponStates.Add(w,saved);
                    if(w.hooked)frozenHeldBodies.Add(w.hooked);if(w.whirled)frozenHeldBodies.Add(w.whirled);
                                                                                                              
                    w.hooked=w.whirled=null;w.channelledField=w.recallActor=w.stickyActor=null;w.recallPlanted=false;w.hookBusy=w.inFlight=w.charging=false;w.heldTime=0;w.burstLeft=0;
                    w.hookChainUsed=false;w.vacuumTimer=0;
                    if(w.form!=WeaponForm.ArcCoil||w.variant!=1)w.charge=0;
                                                                                                                                               
                    w.executeNextShot=0f;w.chainStoredJumps=0;
                    ResetPlacedShots(w);
                }
                FreezeExecuteMarks(true);
                inputHeld=false;lastPosition=player.Position;
                return;
            }
            if(trialWeaponStates.Count==0)return;
                                                                                                                          
            dashLeft=0;dashWeapon=null;dashContactDamage=0;dashHits.Clear();
            if(player&&player.State==CharacterState.Dashing)player.SetState(CharacterState.Normal);
                                                                                                                      
            if(AttackPool.Instance)foreach(var actor in AttackPool.Instance.actors)
                if(actor&&!frozenBattlefieldAttacks.Contains(actor))actor.Release();
            foreach(var pair in trialWeaponStates)
            {
                var w=pair.Key;var saved=pair.Value;
                if(w.hooked&&!frozenHeldBodies.Contains(w.hooked)&&w.hooked.IsAlive)w.hooked.SetState(CharacterState.Normal);
                if(w.whirled&&!frozenHeldBodies.Contains(w.whirled)&&w.whirled.IsAlive)w.whirled.SetState(CharacterState.Normal);
                w.cooldown=saved.cooldown;w.reload=saved.reload;w.heldTime=saved.heldTime;w.heat=saved.heat;
                w.charge=saved.charge;w.chargeFullTime=saved.chargeFullTime;w.executeNextShot=saved.executeNextShot;w.chainStoredJumps=saved.chainStoredJumps;w.holeReturnSpan=saved.holeReturnSpan;w.rounds=saved.rounds;
                w.charging=saved.charging;w.inFlight=saved.inFlight;w.recallPlanted=saved.recallPlanted;w.recallPoint=saved.recallPoint;w.recallActor=saved.recallActor;
                w.hooked=saved.hooked;w.whirled=saved.whirled;w.hookBusy=saved.hookBusy;w.channelledField=saved.channelledField;
                w.whirlLeft=saved.whirlLeft;w.orbitAngle=saved.orbitAngle;
                w.stickyActor=saved.stickyActor;
                                                                                  
                ResetPlacedShots(w);w.turretLeft=saved.turretLeft;w.turretClock=saved.turretClock;w.turretPoint=saved.turretPoint;w.turretDir=saved.turretDir;
            }
            FreezeExecuteMarks(false);FreezeAutoWeaponsForTrial(false);
            for(int i=0;i<autoHitTimes.Length;i++)
            {
                autoHitTimes[i].Clear();foreach(var pair in frozenAutoHitTimes[i])autoHitTimes[i].Add(pair.Key,pair.Value);
                frozenAutoHitTimes[i].Clear();
            }
            ActiveIndex=trialActiveIndex;inputHeld=trialInputHeld;autoClock=frozenAutoClock;lastPosition=player.Position;
            trialWeaponStates.Clear();frozenBattlefieldAttacks.Clear();frozenHeldBodies.Clear();
        }
                                                                                                                                     
        readonly RunWeapon[] voidEnchanted=new RunWeapon[2];readonly ElementKind[] voidEnchantFrom=new ElementKind[2];float voidEnchantLeft;
        public float VoidEnchantLeft=>voidEnchantLeft;
        public void VoidEnchantHands(float seconds)
        {
            EndVoidEnchant();
            if(seconds<=0f)return;
            voidEnchantLeft=seconds;
            for(int i=0;i<Hand.Length&&i<voidEnchanted.Length;i++){var w=Hand[i];if(w==null)continue;voidEnchanted[i]=w;voidEnchantFrom[i]=w.element;w.element=ElementKind.Void;}
        }
        void TickVoidEnchant(float dt){if(voidEnchantLeft<=0f)return;voidEnchantLeft=Mathf.Max(0f,voidEnchantLeft-dt);if(voidEnchantLeft<=0f)EndVoidEnchant();}
                                                                                                                                              
        void EndVoidEnchant()
        {
            voidEnchantLeft=0f;
            for(int i=0;i<voidEnchanted.Length;i++){var w=voidEnchanted[i];if(w!=null&&w.element==ElementKind.Void)w.element=voidEnchantFrom[i];voidEnchanted[i]=null;}
        }
                                                                                                          
        ElementKind OwnElement(RunWeapon w){for(int i=0;i<voidEnchanted.Length;i++)if(voidEnchantLeft>0f&&voidEnchanted[i]==w)return voidEnchantFrom[i];return w.element;}
                                                                                                                              
        void SetOwnElement(RunWeapon w,ElementKind element){for(int i=0;i<voidEnchanted.Length;i++)if(voidEnchantLeft>0f&&voidEnchanted[i]==w){voidEnchantFrom[i]=element;return;}w.element=element;}
        public void ClearWaveEffects()
        {
            EndVoidEnchant();
            trialWeaponStates.Clear();frozenBattlefieldAttacks.Clear();frozenHeldBodies.Clear();
            foreach(var times in frozenAutoHitTimes)times.Clear();
            if(AttackPool.Instance)AttackPool.Instance.Clear();
            ClearAutoMotion();
            foreach(var w in Hand)
            {
                if(w==null)continue;
                if(w.hooked&&w.hooked.IsAlive)w.hooked.SetState(CharacterState.Normal);
                w.hooked=null;w.charging=false;w.inFlight=false;w.hookBusy=false;w.channelledField=w.recallActor=w.stickyActor=null;w.recallPlanted=false;w.heldTime=0;w.charge=0;w.chargeFullTime=0;w.burstLeft=0;
                w.knifeCaught=w.hookChainUsed=false;w.vacuumTimer=0;
                if(player)player.RemoveMovementModifier(w);
                ResetPlacedShots(w);
            }
            ClearChargeLine();
            ClearElementWeapons();
            ElementCombat.ClearContinuousHits();
            executeMarks.Clear();frozenExecuteMarks.Clear();lanceBeams.Clear();
            hazards.Clear();sporeClouds.Clear();tracers.Clear();
            dashLeft=0;dashWeapon=null;dashContactDamage=0;dashHits.Clear();inputHeld=false;if(player&&player.State==CharacterState.Dashing)player.SetState(CharacterState.Normal);
        }
                                                                                                                                            
        void ResetPlacedShots(RunWeapon w)
        {
            RemoveLanceTrailTracer(w);w.lanceTrailLeft=0f;w.executeAuraLeft=0f;
            w.turretLeft=w.turretClock=0f;w.bombardLeft=0;w.bombardTimer=0f;
        }
                                                                                                                              
                                                                   
        public void KnifeLostToHole(RunWeapon w)
        {
            if(w==null)return;
            float wait=Mathf.Max(0f,upgrades.knifeHoleReturn);
            w.inFlight=false;w.cooldown=Mathf.Max(w.cooldown,wait);w.holeReturnSpan=wait;
        }
    }
}
