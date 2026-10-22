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
        [Range(0f,1f), Tooltip("Chance that each of armory slots 2 and 3 shows the next Mk of a held weapon instead of a new form. Slot 1 is always a new form.")]
        public float armoryUpgradeChance=.5f;
        public RunWeapon[] Offers={null,null,null};
        public int ActiveIndex { get; private set; }
        public Combatant HeldEnemy => Active?.hooked && !BattlefieldFrozen(Active.hooked.gameObject) ? Active.hooked : null;
        public RunWeapon Active => Hand[ActiveIndex];
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
        public float Charge01 => Active!=null?Mathf.Clamp01(Active.charge):0;
        public float MoveMultiplier
        {
            get
            {
                var w=Active;if(w==null)return 1;
                                                                                         
                if(w.charging&&w.form!=WeaponForm.ScatterCannon&&!w.Has(UpgradeAxis.Tame))return .5f;
                if(w.form==WeaponForm.Minigun&&w.variant==0&&w.heldTime>0&&!w.Has(UpgradeAxis.Tame))return Mathf.Lerp(1,upgrades.spinUpMoveFloor,w.heldTime/3);
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
        float dashLeft;
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
            return CreateWeapon(form,v,wave);
        }
        RunWeapon CreateWeapon(WeaponForm form,int v,int wave)
        {
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
            return new RunWeapon{form=owned.form,variant=owned.variant,revealed=owned.revealed,axes=owned.axes,element=owned.element,builtInElement=owned.builtInElement,
                mk=UpgradeTargetMk(owned,wave),rounds=ClipSize(owned.form,owned.variant),upgradeOf=owned};
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
                if(Array.IndexOf(Hand,w.upgradeOf)<0&&Array.IndexOf(Auto,w.upgradeOf)<0)return false;
                w.upgradeOf.mk=Mathf.Max(w.upgradeOf.mk,w.mk);Offers=new RunWeapon[ArmoryOfferCount];return true;
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
                    if(actor&&actor.gameObject.activeSelf&&actor.weapon==w&&actor.mode==AttackMode.Hook&&!BattlefieldFrozen(actor.gameObject))actor.Release();
        }
        public void Switch()
        {
            if(!CanUseHand||HandHookBusy)return;var w=Active;
            if(w!=null){if(w.hooked&&!BattlefieldFrozen(w.hooked.gameObject))Fling(w);CancelInput(w);}
            ActiveIndex=1-ActiveIndex;
        }
        public void HandleInput(bool pressed,bool held,bool released)
        {
            var w=Active;
            if(!CanUseHand){CancelInput(w,true);return;}
            inputHeld=held;if(w==null||HandHookBusy)return;
            if((held||pressed)&&w.form==WeaponForm.Minigun&&w.variant==0&&w.spinRetentionLeft>0)
            {w.heldTime=Mathf.Max(w.heldTime,w.spinStored);w.spinRetentionLeft=0;}
            if(pressed&&w.form==WeaponForm.PlasmaLauncher&&w.variant==0&&w.Has(UpgradeAxis.Twist)&&w.stickyActor&&!BattlefieldFrozen(w.stickyActor.gameObject)&&w.stickyActor.TryManualDetonate())return;
            if(w.hooked&&!BattlefieldFrozen(w.hooked.gameObject)){if(pressed)Fling(w);return;}
            if(released&&w.charging&&(w.form is WeaponForm.Rifle or WeaponForm.ScatterCannon)){w.charging=false;Fire(w,true);w.charge=0;return;}
            if(released)StopTrigger(w);
            if(!held&&!pressed||w.cooldown>0||w.reload>0||w.inFlight||w.charging)return;
            bool charge=(w.form==WeaponForm.Rifle&&w.variant==0)||(w.form==WeaponForm.Railgun&&w.variant==0)||(w.form==WeaponForm.ScatterCannon&&w.variant==1);
            if(charge){if(pressed){w.charging=true;w.charge=0;Reveal(w);WeaponSound(w,WeaponAudioPhase.ChargeStart);if(w.form==WeaponForm.ScatterCannon)V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponFocusCharge,player.Position);}return;}
            bool repeat=w.form==WeaponForm.Rifle&&w.variant==1||w.form==WeaponForm.Flamethrower&&w.variant==0||w.form==WeaponForm.CryoCannon&&w.variant==1||w.form==WeaponForm.SporeLauncher&&w.variant==1||w.form==WeaponForm.Minigun;
            if(pressed||repeat&&held)Fire(w,false);
        }
                                                                                                            
        public void RevealNow(RunWeapon w)=>Reveal(w);
                                                                                                                   
        public float HitDamage(RunWeapon w){var d=roster.Get(w.form);return d==null?0f:(w.variant==0?d.damageA:d.damageB)*DamageScale(w);}
                                                                                                                                
        public float CycleSeconds(RunWeapon w){var d=roster.Get(w.form);return d==null?0f:(w.variant==0?d.cooldownA:d.cooldownB)*CooldownScale(w);}
        void Reveal(RunWeapon w){if(w.revealed)return;w.revealed=true;PlayerPrefs.SetInt("anomaly."+(int)w.form+"."+w.variant,1);Revealed?.Invoke(w);}
        public void Tick(float dt)
        {
            float distance=Query.Flat(player.Position-lastPosition).magnitude;lastPosition=player.Position;
            if(!CanUseHand)CancelInput(Active,true);
            WatchPlayerHits();
                                                                               
            float chill=ElementStatus.For(player)?.PlayerChillMultiplier??1f;
            foreach(var w in Hand)
            {
                if(w==null)continue;
                w.cooldown=Mathf.Max(0,w.cooldown-dt*chill*WeaponCooldownRate);w.reload=Mathf.Max(0,w.reload-dt*chill);
                if(w.reload<=0&&w.jammed){w.jammed=false;V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair,player.Position);}
                if(w.charging)
                {
                    w.charge=Mathf.Clamp01(w.charge+dt/(ChargeSeconds(w)*CooldownScale(w)));
                    if(w.form==WeaponForm.Railgun&&w.charge>=1&&CanUseHand&&w==Active){w.charging=false;Fire(w,true);w.charge=0;}
                }
                if(w.form==WeaponForm.ArcCoil&&w.variant==1)TickCapacitor(w,distance,dt);
                if(w.hooked&&!BattlefieldFrozen(w.hooked.gameObject))
                {
                    if(!w.hooked.IsAlive)w.hooked=null;
                    else w.hooked.SetMovePosition(player.Position+player.AimDirection*(player.radius+w.hooked.radius+.3f));
                }
                if(w==Active&&inputHeld&&CanUseHand)
                {if(w.form==WeaponForm.Minigun&&w.variant==0&&w.spinRetentionLeft>0){w.heldTime=Mathf.Max(w.heldTime,w.spinStored);w.spinRetentionLeft=0;}w.heldTime+=dt;}
                else{w.heldTime=0;w.heat=Mathf.Max(0,w.heat-dt);w.spinRetentionLeft=Mathf.Max(0,w.spinRetentionLeft-dt);if(w.spinRetentionLeft<=0)w.spinStored=0;}
                if(w.form==WeaponForm.CryoCannon&&w.variant==1)BlizzardChill(w);
                if(w.burstLeft>0)TickBurst(w,dt);
            }
            TickAutoWeapons(GameManager.Instance.WorldDeltaTime);
            TickSporeClouds();
            TickHazards(GameManager.Instance.WorldDeltaTime);
        }
        void FixedUpdate()
        {
            if(dashLeft<=0||dashWeapon==null||!GameManager.Instance.IsCombatActive)return;
            var weapon=dashWeapon;Vector3 from=player.Position;
            float step=Mathf.Min(dashLeft,25/StartingFormCycleFactor(WeaponForm.RocketLauncher)*Time.fixedDeltaTime);
            bool wallHit=Physics.SphereCast(Query.AtCastHeight(from),player.radius,dashDirection,out var hit,step,GameManager.WallMask);
            float travel=wallHit?Mathf.Min(step,Mathf.Max(0f,hit.distance)):step;
            DamageHumanRocketContacts(from,travel,weapon);
            if(!player.IsAlive){dashLeft=0;dashWeapon=null;return;}
            if(wallHit)
            {
                player.BounceFromWall(Query.Flat(hit.point)-dashDirection*player.radius,hit.normal,3,!weapon.Has(UpgradeAxis.Tame));
                WeaponSound(weapon,WeaponAudioPhase.Impact);dashLeft=0;dashWeapon=null;return;
            }
                                                                                                                             
            Vector3 drift=WorldMap.Active?Query.Flat(WorldMap.Active.GetPull(from,player))*Time.fixedDeltaTime:Vector3.zero;
            player.SetMovePosition(from+dashDirection*travel+drift);dashLeft-=travel;
            if(dashLeft<=0)
            {
                player.SetState(CharacterState.Normal);
                if(weapon.Has(UpgradeAxis.Twist))Area(weapon,player.Position,3,0,8,false);
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
                Damage(weapon,body,contactDamage,0,dashDirection);
            }
        }
        public float StartingFormDamageFactor(WeaponForm form)=>form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?Mathf.Max(.01f,startingDamageFactor):1f;
        public float StartingFormCycleFactor(WeaponForm form)=>form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?Mathf.Max(.01f,startingCycleFactor):1f;
        float DamageScale(RunWeapon w)=>StartingFormDamageFactor(w.form)*w.Power*MkDamage(w)*(progression?progression.Stats.DamageMultiplier:1)*Mathf.Max(.01f,crowdDamageMultiplier)*SkillDamageMultiplier;
        static SkillLoadout Skills=>GameManager.Instance?GameManager.Instance.skills:null;
                                                                                                              
        static float SkillDamageMultiplier=>Skills?Skills.SkillDamageMultiplier:1f;
                                                                                                       
        static float WeaponCooldownRate=>Skills?Skills.WeaponCooldownRate:1f;
        float ChargeSeconds(RunWeapon w)=>w.form==WeaponForm.Railgun?.5f:w.form==WeaponForm.ScatterCannon?Mathf.Max(.01f,upgrades.focusChargeTime):1f;
        float CooldownScale(RunWeapon w)=>StartingFormCycleFactor(w.form)*w.Tempo*(progression?progression.Stats.CooldownMultiplier:1)/(GameManager.Instance.skills?GameManager.Instance.skills.AttackSpeedMultiplier:1);
                                                                                                                                                                                 
        public float HookCooldown(RunWeapon w)=>hookCooldown*w.Tempo*(progression?progression.Stats.CooldownMultiplier:1)/(GameManager.Instance.skills?GameManager.Instance.skills.AttackSpeedMultiplier:1);
        public float GetCooldownDuration(RunWeapon w)
        {
            if(w==null||!roster)return 0f;
            var definition=roster.Get(w.form);if(definition==null)return 0f;
            if(w.form==WeaponForm.Knife&&w.variant==1)return HookCooldown(w);                                           
            float duration=w.form==WeaponForm.Minigun&&w.variant==0?1f/Mathf.Lerp(4f,12f,Mathf.Clamp01(w.heldTime/3f)):w.variant==0?definition.cooldownA:definition.cooldownB;
            return duration*CooldownScale(w);
        }
        float KnockScale=>(progression?progression.Stats.KnockbackMultiplier:1)*(GameManager.Instance.skills?GameManager.Instance.skills.KnockbackMultiplier:1);
        public float UpgradePushScale=>KnockScale;
        static void WeaponSound(RunWeapon weapon,WeaponAudioPhase phase=WeaponAudioPhase.Fire)
        {
            if(weapon==null)return;
            if(!weapon.IsAuto && phase is WeaponAudioPhase.Fire or WeaponAudioPhase.Launch)
            {
                var playerBody=GameManager.Instance?GameManager.Instance.player:null;
                if(playerBody){playerBody.GetComponent<V2ModelPresentation>()?.MarkAttack();FireFeedback(weapon,playerBody);}
            }
            V2Audio.Instance?.PlayWeapon(weapon.form,weapon.variant,phase);
        }
                                                                                                                                                    
        static void FireFeedback(RunWeapon weapon,PlayerController body)
        {
            var gm=GameManager.Instance;if(!gm)return;
            var profile=gm.feedback.For(WeaponFeel.Of(weapon));
            Vector3 aim=Query.Flat(body.AimDirection);if(aim.sqrMagnitude<1e-4f)return;aim.Normalize();
            body.VisualKick(-aim,profile.fireRecoil,.1f);
            gm.Kick(-aim,profile.fireKick);
            var pool=FxPool.Active;
            if(profile.muzzleFlash>0f&&pool&&pool.muzzleFlash)
                pool.EmitDirectional(pool.muzzleFlash,Query.AtCastHeight(body.Position)+aim*(body.radius+.45f),aim,profile.muzzleFlash,.07f,new Color(1f,.88f,.6f));
        }
        void Fire(RunWeapon w,bool charged)
        {
            Reveal(w);var d=roster.Get(w.form);float damage=(w.variant==0?d.damageA:d.damageB)*DamageScale(w);float range=(w.variant==0?d.rangeA:d.rangeB)*w.Reach;w.cooldown=(w.variant==0?d.cooldownA:d.cooldownB)*CooldownScale(w);
            Vector3 aim=player.AimDirection,at=player.Position,point=at+Vector3.ClampMagnitude(Query.Flat(player.AimPoint-at),range);
            if(w.form==WeaponForm.Rifle&&w.variant==0){Bolt(w,aim,Mathf.Lerp(Mathf.Clamp(roster.RifleMinimumChargeDamage,0f,d.damageA),d.damageA,w.charge)*DamageScale(w),Mathf.Lerp(5,10,w.charge),range,80,true,w.Has(UpgradeAxis.Twist)&&w.charge>=.99f?2:0);}
            else if(w.form==WeaponForm.Rifle)
            {
                                                                                                                        
                var a=Bolt(w,-aim,damage,0,range,30,false,w.Has(UpgradeAxis.Twist)?3:1);
                if(a&&w.Has(UpgradeAxis.Tame)){a.steerAfter=upgrades.reverseTurnDelay;a.steerDirection=aim;}
                SpendRound(w,.6f);
            }
            else if(w.form==WeaponForm.Knife&&w.variant==0)
            {
                var a=Bolt(w,aim,damage,0,range,knifeOutboundSpeed/StartingFormCycleFactor(w.form));
                if(a){a.mode=AttackMode.Knife;a.targetsLeft=w.Has(UpgradeAxis.Reach)?6:4;a.ricochets=w.Has(UpgradeAxis.Twist)?2:0;a.remaining=float.PositiveInfinity;w.inFlight=true;}
            }
            else if(w.form==WeaponForm.Knife){var a=AttackPool.Instance.Acquire();if(a){a.Configure(AttackMode.Hook,player,w,at+aim*(player.radius+.2f),aim,Mathf.Max(1f,DamageScale(w)),0,range,hookExtendSpeed/StartingFormCycleFactor(w.form),0,float.PositiveInfinity);w.hookBusy=true;WeaponSound(w,WeaponAudioPhase.Launch);}}
            else if(w.form==WeaponForm.RocketLauncher&&w.variant==0){float cycle=StartingFormCycleFactor(w.form);Field(w,AttackMode.Blast,point,damage,w.Has(UpgradeAxis.Twist)?-5:5,3*w.Reach,1.1f*cycle,cycle,!w.Has(UpgradeAxis.Tame));}
            else if(w.form==WeaponForm.RocketLauncher){dashLeft=range;dashDirection=aim;dashWeapon=w;dashContactDamage=Mathf.Max(1f,d.damageB*DamageScale(w));dashHits.Clear();player.SetState(CharacterState.Dashing);WeaponSound(w,WeaponAudioPhase.Launch);}
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
            else if(w.form==WeaponForm.PlasmaLauncher){var a=Bolt(w,aim,damage,5,range,9);if(a){a.mode=w.variant==0?AttackMode.Sticky:AttackMode.Bouncy;a.radius=3;a.ricochets=w.variant==0?0:3;a.hurtOwner=true;if(w.variant==0)w.stickyActor=a;else a.remaining=3;}}
            else if(w.form==WeaponForm.Flamethrower&&w.variant==0)Stream(w,range,damage);
                                                                                
            else if(w.form==WeaponForm.Flamethrower)LaunchArc(w,AttackMode.Lob,point,damage,0,2.5f*w.Reach,4,LobSpeed,LobArcHeight,!w.Has(UpgradeAxis.Tame));
            else if(w.form==WeaponForm.CryoCannon&&w.variant==0)ShatterFan(w,aim,range,damage);
            else if(w.form==WeaponForm.CryoCannon)BlizzardSpray(w,range,damage);
            else if(w.form==WeaponForm.ArcCoil&&w.variant==0){Chain(w,range,damage,w.Has(UpgradeAxis.Reach)?7:5);}
            else if(w.form==WeaponForm.ArcCoil){if(w.charge>0)WeaponSound(w);Area(w,at,range,w.charge*.5f*DamageScale(w),0,false);w.charge=0;w.chargeFullTime=0;w.capacitorLeaking=false;}
            else if(w.form==WeaponForm.SporeLauncher&&w.variant==0){var a=LaunchArc(w,AttackMode.Lob,point,damage,0,3.5f*w.Reach,5,LobSpeed,LobArcHeight,false);if(a){a.tickInterval=.5f;TrackSporeCloud(a,w);}}
            else if(w.form==WeaponForm.SporeLauncher){Bolt(w,aim,damage,0,range,35,false,w.Has(UpgradeAxis.Twist)?1:0);SpendRound(w,2);NeedlerCost(w);}
            else if(w.form==WeaponForm.Railgun&&w.variant==0){var a=Bolt(w,aim,damage,0,range,120,true);if(a){a.ignoreShield=true;a.pierceDamageRetention=.75f;a.internalWallsLeft=w.Has(UpgradeAxis.Twist)?1:0;}if(w.Has(UpgradeAxis.Tame))player.Push(-aim,4);else player.Knockback(-aim,4,KnockKind.Hit);}
            else if(w.form==WeaponForm.Railgun)Execute(w,range,damage);
            else if(w.form==WeaponForm.Minigun){Bolt(w,aim,damage,0,range,40);if(w.variant==0)w.cooldown=1/Mathf.Lerp(4,12,Mathf.Clamp01(w.heldTime/3))*CooldownScale(w);else{w.heat+=.1f;if(w.heat>=4)Meltdown(w);}}
            else if(w.form==WeaponForm.SwarmLauncher&&w.variant==0)
            {
                                                                                                                                                 
                int n=w.Has(UpgradeAxis.Reach)?11:8;
                for(int i=0;i<n;i++){var a=Bolt(w,Quaternion.Euler(0,BarrageAngle(),0)*aim,damage,0,range,15);if(a){a.mode=AttackMode.Swarm;a.radius=1.5f;a.hurtOwner=!w.Has(UpgradeAxis.Tame);}}
            }
            else if(w.form==WeaponForm.SwarmLauncher){for(int i=0;i<6;i++){int distance=w.Has(UpgradeAxis.Twist)?12-i*2:2+i*2;Field(w,AttackMode.Blast,at+aim*distance,damage,5,2*w.Reach,.15f*i+.2f,.15f*i,!w.Has(UpgradeAxis.Tame));}}
        }
                                                                               
        public int ClipSize(WeaponForm f,int v)=>f==WeaponForm.Rifle&&v==1?Mathf.Max(1,rifleClipSize):f==WeaponForm.ScatterCannon&&v==0?2:f==WeaponForm.SporeLauncher&&v==1?24:int.MaxValue;
                                                                                                              
        bool SpendRound(RunWeapon w,float reload){if(--w.rounds>0)return false;w.reload=reload*CooldownScale(w);w.rounds=ClipSize(w.form,w.variant);return true;}
        void Fling(RunWeapon w){if(!w.hooked||!w.hooked.IsAlive||BattlefieldFrozen(w.hooked.gameObject))return;var target=w.hooked;target.SetState(CharacterState.Normal);target.Throw(player.AimDirection,8,new ThrowImpact{dealer=player,damage=roster.Get(w.form).damageB*DamageScale(w),sizeExponent=1,radius=1.5f*w.Reach,knockback=5*KnockScale,element=w.element,stunSeconds=w.Has(UpgradeAxis.Tame)?1.5f:0});if(target.State==CharacterState.Thrown)WeaponSound(w,WeaponAudioPhase.Launch);w.hooked=null;w.cooldown=Mathf.Max(w.cooldown,HookCooldown(w));}
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
            if(!ValidTeleportPoint(destination,.2f))return null;
            var actor=AttackPool.Instance.Acquire();if(!actor)return null;
            actor.Configure(mode,player,w,player.Position+player.AimDirection*.7f,player.AimDirection,hit,push*KnockScale,0,Mathf.Max(1f,velocity),area,float.PositiveInfinity);
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
        void Cone(RunWeapon w,float range,float angle,float hit,float push){Fx.Cone(player.Position,player.AimDirection,range,angle,w.element);foreach(var c in Combatant.All)if(c!=player&&c.IsAlive&&InCone(c,range,angle))Damage(w,c,hit,push,Query.Flat(c.Position-player.Position).normalized);}
        void Area(RunWeapon w,Vector3 at,float radius,float hit,float push,bool self){foreach(var c in Combatant.All)if(c.IsAlive&&(self||c!=player)&&(Query.Flat(c.Position-at)).magnitude<=radius+c.radius)Damage(w,c,hit,push,Query.Flat(c.Position-at).normalized);Fx.Ring(at,radius,ElementCombat.ColorFor(w.element));}
        void Damage(RunWeapon w,Combatant c,float hit,float push,Vector3 dir){var info=DamageInfo.Attack(hit,player,push<0?-dir:dir,Mathf.Abs(push)*KnockScale,HitKind.Blast);info.feel=WeaponFeel.Of(w);info.baseDamage=hit;info.element=w.element;info.builtInElement=w.builtInElement;lastDamageAccepted=c.ReceiveDamage(info);if(lastDamageAccepted)WeaponSound(w,WeaponAudioPhase.Impact);}
        AttackActor Bolt(RunWeapon w,Vector3 dir,float hit,float push,float reach,float velocity,bool pierce=false,int ricochet=0){var a=AttackPool.Instance.Acquire();if(!a)return null;a.Configure(AttackMode.Bolt,player,w,player.Position+dir*.7f,dir,hit,push*KnockScale,reach,velocity,0,reach/velocity+3);a.pierce=pierce;a.ricochets=ricochet;WeaponSound(w,w.form is WeaponForm.Knife or WeaponForm.PhaseDagger?WeaponAudioPhase.Launch:WeaponAudioPhase.Fire);return a;}
        AttackActor Field(RunWeapon w,AttackMode mode,Vector3 at,float hit,float push,float radius,float life,float delay,bool self){var a=AttackPool.Instance.Acquire();if(!a)return null;a.Configure(mode,player,w,at,player.AimDirection,hit,push*KnockScale,0,0,radius,life+delay+.1f);a.delay=delay;a.hurtOwner=self;WeaponSound(w,mode==AttackMode.Blast?WeaponAudioPhase.Launch:WeaponAudioPhase.Fire);return a;}
        void Chain(RunWeapon w,float range,float hit,int count)
        {
            var first=RayTarget(player.AimDirection,range);upgradeHits.Clear();Vector3 at=player.Position;
            for(int i=0;i<count;i++)
            {
                Combatant next=i==0?first:null;
                if(i>0)
                {
                    float best=16,highest=-1;
                    foreach(var body in Combatant.All)
                    {
                        float distance=Query.Flat(body.Position-at).sqrMagnitude;
                        if(body==player||!body.IsAlive||upgradeHits.Contains(body)||distance>16)continue;
                        float lightning=ElementStatus.For(body)?.Lightning??0;
                        if(w.Has(UpgradeAxis.Twist)?lightning>highest||Mathf.Approximately(lightning,highest)&&distance<best:distance<best)
                        {next=body;best=distance;highest=lightning;}
                    }
                }
                if(!next){ReturnCurrent(w,count-i);break;}
                if(i==0)WeaponSound(w);
                upgradeHits.Add(next);Damage(w,next,hit,0,Query.Flat(next.Position-at).normalized);at=next.Position;Fx.Ring(at,.6f,ElementCombat.ColorFor(w.element));
            }
        }
        void FireAuto(RunWeapon w){var d=roster.Get(w.form);w.cooldown=(w.variant==0?d.cooldownA:d.cooldownB)*CooldownScale(w);float hit=(w.variant==0?d.damageA:d.damageB)*DamageScale(w),range=(w.variant==0?d.rangeA:d.rangeB)*w.Reach;var target=Nearest(range);if(w.form==WeaponForm.OrbitBlades){foreach(var c in Combatant.All)if(c!=player&&c.IsAlive&&Mathf.Abs(Vector3.Distance(c.Position,player.Position)-range)<1)Damage(w,c,hit,w.variant==0?2:-1.5f,Query.Flat(c.Position-player.Position).normalized);}else if(w.form==WeaponForm.SentryDrone&&target){if(w.variant==0){var dir=Query.Flat(target.Position-player.Position).normalized;Bolt(w,dir,hit,0,range,30);player.Push(-dir,.5f);}else{Area(w,target.Position,2.5f,hit,5,false);target.Push(Query.Flat(target.Position-player.Position).normalized,2);}}else if(w.form==WeaponForm.ShieldRing){Area(w,player.Position,range,hit,w.variant==0?5:0,false);if(w.variant==1&&target)progression.GainEnergy(.2f);}}
        Combatant Nearest(float range){Combatant best=null;float distance=range*range;foreach(var c in Combatant.All){if(c==player||!c.IsAlive)continue;float d=(c.Position-player.Position).sqrMagnitude;if(d<distance){distance=d;best=c;}}return best;}
        public DraftCard RollUpgradeCard()
        {
            var choices=new System.Collections.Generic.List<RunWeapon>();
            foreach(var slots in new[]{Hand,Auto})foreach(var w in slots)
                if(w!=null&&w.revealed&&(AvailableAxes(w).Count>0||CanReinfuse(w)))choices.Add(w);
            if(choices.Count==0)return null;
            var weapon=choices[UnityEngine.Random.Range(0,choices.Count)];var axes=AvailableAxes(weapon);
            bool reinfuse=CanReinfuse(weapon)&&(axes.Count==0||UnityEngine.Random.value<.15f);
            var upgrade=reinfuse?UpgradeAxis.Infusion:axes[UnityEngine.Random.Range(0,axes.Count)];
            ElementKind element=upgrade==UpgradeAxis.Infusion?RollElement(weapon.element):weapon.element;
            string desc=upgrade==UpgradeAxis.Infusion?ArenaText.Pair("Infuse ","Infuse ")+ArenaTerms.Element(element)+(weapon.element!=ElementKind.None?ArenaText.Pair(", replaces ",", replaces ")+ArenaTerms.Element(weapon.element):""):upgrade switch{UpgradeAxis.Power=>ArenaText.Pair("Damage +40%","Damage +40%"),UpgradeAxis.Tempo=>ArenaText.Pair("Cooldown, charge and reload -30%","Cooldown, charge and reload -30%"),UpgradeAxis.Reach=>ArenaText.Pair("Range, area or targets +40%","Range, area or targets +40%"),UpgradeAxis.Twist=>ArenaText.Pair("Adds a new variant to this anomaly","Adds a new variant to this anomaly"),_=>ArenaText.Pair("Removes or softens this anomaly's backfire","Removes or softens this anomaly's backfire")};
            if(upgrade is UpgradeAxis.Twist or UpgradeAxis.Tame)desc=(int)weapon.form<3?CoreAnomalyUpgradeDescription(weapon,upgrade):ArenaTerms.ExpandedUpgradeDescription(weapon.form,weapon.variant,upgrade);
            return new DraftCard(DraftCardKind.Weapon,weapon.Label+" / "+ArenaTerms.Upgrade(upgrade),desc,_=>{weapon.axes|=upgrade;if(upgrade==UpgradeAxis.Infusion)weapon.element=element;return true;},payload:new WeaponUpgradeOffer(weapon,upgrade,element));
        }
        static string CoreAnomalyUpgradeDescription(RunWeapon w,UpgradeAxis axis)
        {
            bool twist=axis==UpgradeAxis.Twist;
            return (w.form,w.variant,twist) switch
            {
                (WeaponForm.Rifle,0,true)=>ArenaText.Pair("Fully charged shots can bounce off walls up to 2 times.","Fully charged shots can bounce off walls up to 2 times."),
                (WeaponForm.Rifle,0,false)=>ArenaText.Pair("Charging no longer slows you.","Charging no longer slows you."),
                (WeaponForm.Rifle,1,true)=>ArenaText.Pair("Reverse shots can bounce off walls 3 times instead of 1.","Reverse shots can bounce off walls 3 times instead of 1."),
                (WeaponForm.Rifle,1,false)=>ArenaText.Pair("Reverse rounds turn around after 0.3 s and fly the way you were aiming.","Reverse rounds turn around after 0.3 s and fly the way you were aiming."),
                (WeaponForm.Knife,0,true)=>ArenaText.Pair("The throwing knife can bounce off walls up to 2 times.","The throwing knife can bounce off walls up to 2 times."),
                (WeaponForm.Knife,0,false)=>ArenaText.Pair("The knife flying back after a kill no longer cuts you; it still cuts enemies on the way.","The knife flying back after a kill no longer cuts you; it still cuts enemies on the way."),
                (WeaponForm.Knife,1,true)=>ArenaText.Pair("A hook that hits a wall moves you to the wall.","A hook that hits a wall moves you to the wall."),
                (WeaponForm.Knife,1,false)=>ArenaText.Pair("The flung enemy is stunned for 1.5 seconds when it lands.","The flung enemy is stunned for 1.5 seconds when it lands."),
                (WeaponForm.RocketLauncher,0,true)=>ArenaText.Pair("The explosion pulls characters toward its centre instead of knocking them outward.","The explosion pulls characters toward its centre instead of knocking them outward."),
                (WeaponForm.RocketLauncher,0,false)=>ArenaText.Pair("Your explosion no longer damages you, but still displaces you.","Your explosion no longer damages you, but still displaces you."),
                (WeaponForm.RocketLauncher,1,true)=>ArenaText.Pair("Ending the flight on open ground pushes enemies within radius 3 outward by 8 units.","Ending the flight on open ground pushes enemies within radius 3 outward by 8 units."),
                (WeaponForm.RocketLauncher,1,false)=>ArenaText.Pair("Hitting a wall no longer damages you; you still bounce back 3 units.","Hitting a wall no longer damages you; you still bounce back 3 units."),
                _=>ArenaText.Pair("Adds a new variant to this anomaly","Adds a new variant to this anomaly")
            };
        }
        static bool CanReinfuse(RunWeapon w)=>!w.builtInElement&&w.element!=ElementKind.None;
                                                                                                                     
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
                        executeBonus=w.executeBonus,rounds=w.rounds,jammed=w.jammed,charging=w.charging,inFlight=w.inFlight,recallPlanted=w.recallPlanted,
                        recallPoint=w.recallPoint,recallActor=w.recallActor,hooked=w.hooked,whirled=w.whirled,hookBusy=w.hookBusy,channelledField=w.channelledField,
                        whirlLeft=w.whirlLeft,orbitAngle=w.orbitAngle,leechDamage=w.leechDamage
                    };
                    saved.stickyActor=w.stickyActor;saved.spinStored=w.spinStored;saved.spinRetentionLeft=w.spinRetentionLeft;saved.capacitorLeaking=w.capacitorLeaking;
                    trialWeaponStates.Add(w,saved);
                    if(w.hooked)frozenHeldBodies.Add(w.hooked);if(w.whirled)frozenHeldBodies.Add(w.whirled);
                                                                                                              
                    w.hooked=w.whirled=null;w.channelledField=w.recallActor=w.stickyActor=null;w.spinStored=w.spinRetentionLeft=0;w.capacitorLeaking=false;w.recallPlanted=false;w.hookBusy=w.inFlight=w.charging=false;w.heldTime=0;w.burstLeft=0;
                    if(w.form!=WeaponForm.ArcCoil||w.variant!=1)w.charge=0;
                }
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
                w.charge=saved.charge;w.chargeFullTime=saved.chargeFullTime;w.executeBonus=saved.executeBonus;w.rounds=saved.rounds;w.jammed=saved.jammed;
                w.charging=saved.charging;w.inFlight=saved.inFlight;w.recallPlanted=saved.recallPlanted;w.recallPoint=saved.recallPoint;w.recallActor=saved.recallActor;
                w.hooked=saved.hooked;w.whirled=saved.whirled;w.hookBusy=saved.hookBusy;w.channelledField=saved.channelledField;
                w.whirlLeft=saved.whirlLeft;w.orbitAngle=saved.orbitAngle;w.leechDamage=saved.leechDamage;
                w.stickyActor=saved.stickyActor;w.spinStored=saved.spinStored;w.spinRetentionLeft=saved.spinRetentionLeft;w.capacitorLeaking=saved.capacitorLeaking;
            }
            for(int i=0;i<autoHitTimes.Length;i++)
            {
                autoHitTimes[i].Clear();foreach(var pair in frozenAutoHitTimes[i])autoHitTimes[i].Add(pair.Key,pair.Value);
                frozenAutoHitTimes[i].Clear();
            }
            ActiveIndex=trialActiveIndex;inputHeld=trialInputHeld;autoClock=frozenAutoClock;lastPosition=player.Position;
            trialWeaponStates.Clear();frozenBattlefieldAttacks.Clear();frozenHeldBodies.Clear();
        }
        public void ClearWaveEffects()
        {
            trialWeaponStates.Clear();frozenBattlefieldAttacks.Clear();frozenHeldBodies.Clear();
            foreach(var times in frozenAutoHitTimes)times.Clear();
            if(AttackPool.Instance)AttackPool.Instance.Clear();
            ClearAutoMotion();
            foreach(var w in Hand)
            {
                if(w==null)continue;
                if(w.hooked&&w.hooked.IsAlive)w.hooked.SetState(CharacterState.Normal);
                w.hooked=null;w.charging=false;w.inFlight=false;w.hookBusy=false;w.channelledField=w.recallActor=w.stickyActor=null;w.recallPlanted=false;w.heldTime=0;w.charge=0;w.chargeFullTime=0;w.spinStored=w.spinRetentionLeft=0;w.capacitorLeaking=false;w.burstLeft=0;
                if(player)player.RemoveMovementModifier(w);
            }
            hazards.Clear();sporeClouds.Clear();
            dashLeft=0;dashWeapon=null;dashContactDamage=0;dashHits.Clear();inputHeld=false;if(player&&player.State==CharacterState.Dashing)player.SetState(CharacterState.Normal);
        }
                                                                                                                              
                                                                                                            
        public void LoseWeapon(RunWeapon w)
        {
            if(w==null)return;
            int hand=Array.IndexOf(Hand,w);
            if(hand>=0)
            {
                if(hand==ActiveIndex)CancelInput(w,true);
                if(w.hooked&&w.hooked.IsAlive&&!BattlefieldFrozen(w.hooked.gameObject))w.hooked.SetState(CharacterState.Normal);
                Hand[hand]=null;
                if(hand==ActiveIndex&&Hand[1-hand]!=null)ActiveIndex=1-hand;
            }
            int auto=Array.IndexOf(Auto,w);
            if(auto>=0){EndWhirl(w);Auto[auto]=null;}
            w.hooked=null;w.inFlight=w.charging=w.hookBusy=false;w.burstLeft=0;
            if(player)player.RemoveMovementModifier(w);
            if(AttackPool.Instance)foreach(var actor in AttackPool.Instance.actors)
                if(actor&&actor.gameObject.activeSelf&&actor.weapon==w&&!BattlefieldFrozen(actor.gameObject))actor.Release();
        }
    }
}
