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
    /// <summary>Two hand slots and three auto slots. Definitions and effect actors are inspectable saved assets/scene objects.</summary>
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
        [Min(.01f), Tooltip("群怪版本的玩家武器伤害倍率；在既有半伤/双频率和升级乘区之后作用，不重置卡片成长。")]
        public float crowdDamageMultiplier = 1f;
        [Min(.01f), Tooltip("Complete attack duration multiplier for the three starting forms; .5 doubles firing and motion cadence.")]
        public float startingCycleFactor = .5f;
        [field: SerializeField, HideInInspector, Tooltip("旧版衰减保存字段，现已停用。人形火箭本次发射的所有目标使用同一伤害。")]
        public float HumanRocketImpactRetention { get; set; } = .5f;
        [Header("Pistol (V2 campaign, since 2026-11-06)")]
        [Min(1f), Tooltip("Flight speed of a Pistol round, cells per second (the Rifle's charged round flies 80, a Reverse Shot round 30). Its damage, interval and range are WeaponRoster.pistol.")]
        public float pistolRoundSpeed = 45f;
        [Header("Knife motion — original behaviour")]
        [Min(.01f)] public float knifeOutboundSpeed = 18f;
        [Tooltip("飞刀飞回玩家的速度（格/秒）；2026-10-21 按试玩反馈从 14 调到 9。")] [Min(.01f)] public float knifeReturnSpeed = 9f;
        [Tooltip("步枪·反向射击一匣的子弹数；2026-10-21 从 4 改为 3（改这里时 WeaponGuide 里的“3 发一匣”文字要跟着改）。")] [Min(1)] public int rifleClipSize = 3;
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
        /// <summary>The fire button is down on the weapon in hand, as HandleInput last saw it (the spray and spin loops of V5WeaponAudio read this).</summary>
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
        /// <summary>HUD charge bar: a charge 0-1, or the Capacitor's stored charge as a share of its maximum.</summary>
        public float Charge01 => Active==null?0:Active.form==WeaponForm.ArcCoil&&Active.variant==1?Mathf.Clamp01(Active.charge/CapacitorMax):Active.form==WeaponForm.Minigun?MinigunBar(Active):Mathf.Clamp01(Active.charge);
        /// <summary>The minigun reuses the charge bar: Spin-up shows the spin (full after 3 s), Meltdown shows its heat (full = meltdown).</summary>
        static float MinigunBar(RunWeapon w)=>w.variant==0?Mathf.Clamp01(w.heldTime/3f):Mathf.Clamp01(w.heat/4f);
        public float MoveMultiplier
        {
            get
            {
                var w=Active;if(w==null)return 1;
                // Charging slows you, except Scatter Focus whose backfire is its recoil; only Charged Shot's Tame lifts it (Lance Tame keeps it).
                if(w.charging&&w.form!=WeaponForm.ScatterCannon&&!(w.form==WeaponForm.Rifle&&w.Has(UpgradeAxis.Tame)))return .5f;
                // The 10-27 doc (document 0155, P20): Spin-up slows your aim and pushes you back, never your feet.
                // Arc Jump Tame: stored jumps speed you up while the Arc Cannon is in your hand.
                if(w.form==WeaponForm.ArcCoil&&w.variant==0&&w.Has(UpgradeAxis.Tame)&&w.chainStoredJumps>0)return Mathf.Max(.1f,upgrades.chainTameMoveSpeed);
                return 1;
            }
        }
        /// <summary>Degrees per second the aim may turn; 0 means unlimited. Spin-up inertia grows with the spin.</summary>
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
        /// <summary>A continued save: the weapons go back into their hand and auto slots (null: an empty slot) with full clips, no offer
        /// waits, and the saved hand is the one in use (the other while that one is empty).</summary>
        public void RestoreSlots(RunWeapon[] hand,RunWeapon[] auto,int activeIndex)
        {
            ResetRun();
            for(int i=0;hand!=null&&i<hand.Length&&i<Hand.Length;i++)Hand[i]=Refilled(hand[i]);
            for(int i=0;auto!=null&&i<auto.Length&&i<Auto.Length;i++)Auto[i]=Refilled(auto[i]);
            ActiveIndex=activeIndex is 0 or 1&&Hand[activeIndex]!=null?activeIndex:Hand[0]==null&&Hand[1]!=null?1:0;
        }
        RunWeapon Refilled(RunWeapon w){if(w==null)return null;w.upgradeOf=null;w.rounds=ClipSize(w.form,w.variant);return w;}
        public void GiveStarting(WeaponForm form)
        {
            float chance=form switch{WeaponForm.Rifle=>startingRifleVariantAChance,WeaponForm.Knife=>startingKnifeVariantAChance,WeaponForm.RocketLauncher=>startingRocketVariantAChance,_=>.5f};
            Hand[0]=CreateWeapon(form,UnityEngine.Random.value<Mathf.Clamp01(chance)?0:1,1);
        }
        /// <summary>V2 day campaign (since 2026-11-06): a weapon put straight into one hand at Mk 1, with no crate and no card: the starting
        /// Pistol (known at once, see CreateWeapon), and weapons unlocked at the base, such as the rocket launcher bought at the Armament Bay
        /// (it takes the other hand's place). Returns what it replaced.</summary>
        public RunWeapon GiveToHand(int hand,WeaponForm form)
        {
            if(hand<0||hand>=Hand.Length)return null;
            float chance=form switch{WeaponForm.Rifle=>startingRifleVariantAChance,WeaponForm.Knife=>startingKnifeVariantAChance,WeaponForm.RocketLauncher=>startingRocketVariantAChance,_=>.5f};
            var old=Hand[hand];Hand[hand]=CreateWeapon(form,UnityEngine.Random.value<Mathf.Clamp01(chance)?0:1,1);if(form==WeaponForm.Pistol)ArenaCodexProgress.DiscoverWeapon(form);return old;
        }
        /// <summary>V2 day campaign (since 2026-11-10): an auto weapon of a chosen form put straight into one auto slot, as GiveToHand does
        /// for a hand (the base's study window sells weapons by form). Returns what it replaced.</summary>
        public RunWeapon GiveToAuto(int slot,WeaponForm form)
        {
            if(slot<0||slot>=Auto.Length||!RunWeapon.IsAutoForm(form))return null;
            var old=Auto[slot];Auto[slot]=CreateWeapon(form,UnityEngine.Random.Range(0,2),1);return old;
        }
        /// <summary>The weapon is in one of the hand or auto slots.</summary>
        public bool Holds(RunWeapon w)=>w!=null&&(Array.IndexOf(Hand,w)>=0||Array.IndexOf(Auto,w)>=0);
        RunWeapon Roll(WeaponForm form,int wave)
        {
            int v=UnityEngine.Random.Range(0,2);if(Owned(form,v))v=1-v;
            var w=CreateWeapon(form,v,wave);
            // Later waves make a higher Mk more likely; a new form never arrives at the top Mk, which only a stronger version reaches.
            if(roster&&!CampaignMk)w.mk=Mathf.Max(w.mk,Mathf.Min(roster.MaxMk-1,w.mk+MkBonus(wave)));
            return w;
        }
        /// <summary>One extra Mk on an armory offer, with this wave band's chance (WeaponRoster.mkBonusChance). Random.value can return 1,
        /// so the draw is in ten-thousandths: a chance of 1 always hits and 0 never does.</summary>
        int MkBonus(int wave)=>roster&&UnityEngine.Random.Range(0,10000)<roster.MkBonusChance(wave)*10000f?1:0;
        /// <summary>Q98: the Drain's backfire costs Energy, so before the first skill brings the energy bar it is never
        /// offered or drawn; an Energy Ring made then is always the Deflect. With the bar it is drawn as usual. The Sentry Drone is a fair
        /// coin from the start, since the Gunner no longer costs Energy (Q86, Q103).</summary>
        public bool DrainOfferable=>!progression||progression.EnergyUnlocked;
        /// <summary>The element a weapon of this form is born with: the four elemental forms' own, none for the others (the title's
        /// collection shows it too).</summary>
        internal static ElementKind BuiltInElement(WeaponForm form)=>form switch{WeaponForm.Flamethrower=>ElementKind.Fire,WeaponForm.CryoCannon=>ElementKind.Frost,WeaponForm.ArcCoil=>ElementKind.Lightning,WeaponForm.SporeLauncher=>ElementKind.Poison,_=>ElementKind.None};
        RunWeapon CreateWeapon(WeaponForm form,int v,int wave)
        {
            if(form==WeaponForm.ShieldRing&&!DrainOfferable)v=0;
            // The Pistol (since 2026-11-06) has one plain attack and nothing to learn: always variant 0 and known from the start, so its name
            // never shows "?" and taking it raises no reveal toast.
            bool plain=!RunWeapon.IsAnomalousForm(form);if(plain)v=0;
            var w=new RunWeapon{form=form,variant=v,rounds=ClipSize(form,v),revealed=plain};
            w.element=BuiltInElement(form);
            w.builtInElement=w.element!=ElementKind.None;if(w.builtInElement)w.axes=UpgradeAxis.Infusion;
            // New forms arrive at the wave's standard tier instead of random numeric axes; in the V2 day campaign an anomaly weapon arrives
            // at the Armament Bay's Mk instead (NewWeaponMk, since 2026-11-10).
            int campaignMk=!plain&&NewWeaponMk!=null?NewWeaponMk():0;
            w.mk=campaignMk>0?campaignMk:roster?roster.ParMk(wave):1;
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
            var rule=ArmoryRule?.Invoke()??(true,true);
            if(!rule.Item1)
            {
                // V2 day campaign before Armament Bay level 2: the air drop brings only higher Mk versions of held weapons (level 1).
                var held=rule.Item2?UpgradableWeapons(wave):new System.Collections.Generic.List<RunWeapon>();
                for(int i=0;i<Offers.Length;i++)Offers[i]=held.Count>0?TakeUpgradeOffer(held,wave):null;
                return;
            }
            bool handMissing=Array.Exists(Hand,w=>w==null),autoMissing=wave>=3&&Array.Exists(Auto,w=>w==null);
            var offeredForms=new System.Collections.Generic.HashSet<WeaponForm>();
            var upgradable=UpgradableWeapons(wave);
            for(int i=0;i<Offers.Length;i++)
            {
                Offers[i]=null;
                // Both guarantees consume separate offers: hand first, auto second.
                bool? autoKind=null;
                if(i==0){if(handMissing)autoKind=false;else if(autoMissing)autoKind=true;}
                else if(i==1&&handMissing&&autoMissing)autoKind=true;
                // Slot 1 is always a new form; the other two may show a held weapon's next Mk, never the same weapon twice.
                bool guaranteed=i==0||autoKind.HasValue;
                if(!guaranteed&&upgradable.Count>0&&UnityEngine.Random.value<armoryUpgradeChance){Offers[i]=TakeUpgradeOffer(upgradable,wave);offeredForms.Add(Offers[i].form);continue;}
                var eligible=ArmoryForms(wave,autoKind);
                eligible.RemoveAll(offeredForms.Contains);
                if(eligible.Count==0){eligible=ArmoryForms(wave,null);eligible.RemoveAll(offeredForms.Contains);}
                if(i==0&&wave<=5)
                {
                    var newHandForms=eligible.FindAll(form=>!RunWeapon.IsAutoForm(form)&&form is not (WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher));
                    if(newHandForms.Count>0)eligible=newHandForms;
                }
                Offers[i]=eligible.Count>0?Roll(eligible[UnityEngine.Random.Range(0,eligible.Count)],wave):null;
                if(Offers[i]==null&&i>0&&upgradable.Count>0)Offers[i]=TakeUpgradeOffer(upgradable,wave);
                if(Offers[i]!=null)offeredForms.Add(Offers[i].form);
            }
        }
        /// <summary>Held weapons below one tier above this wave's standard Mk.</summary>
        System.Collections.Generic.List<RunWeapon> UpgradableWeapons(int wave)
        {
            // V2 day campaign (since 2026-11-10): the Mk is bought at the Armament Bay, so no offer raises a held weapon's Mk.
            var list=new System.Collections.Generic.List<RunWeapon>();if(!roster||CampaignMk)return list;
            int ceiling=Mathf.Min(roster.MaxMk,roster.ParMk(wave)+1);
            // The Pistol never takes a higher Mk (since 2026-11-06): no stronger version in an air drop, no weapon damage boost at the base.
            foreach(var slots in new[]{Hand,Auto})foreach(var w in slots)if(w!=null&&w.IsAnomalous&&w.mk<ceiling)list.Add(w);
            return list;
        }
        /// <summary>Upgrade target follows the fodder EHP curve: at least one Mk, and a lagging weapon catches up to one tier above the wave's standard.</summary>
        public int UpgradeTargetMk(RunWeapon owned,int wave)=>roster?Mathf.Min(roster.MaxMk,Mathf.Max(owned.mk+1,roster.ParMk(wave)+1)):owned.mk+1;
        /// <summary>A stronger version of a held weapon upgrades that very weapon to UpgradeTargetMk (one more Mk with the
        /// wave band's bonus chance); taking it raises only the held weapon's Mk (TakeOffer). Its anomaly, upgrades and infusion stay, so it
        /// never turns into another pairing (on 10-25 it had been a new weapon with a fresh anomaly). The offer mirrors the held weapon,
        /// so its card shows what it upgrades.</summary>
        RunWeapon TakeUpgradeOffer(System.Collections.Generic.List<RunWeapon> upgradable,int wave)
        {
            int pick=UnityEngine.Random.Range(0,upgradable.Count);var owned=upgradable[pick];upgradable.RemoveAt(pick);
            var version=new RunWeapon{form=owned.form,variant=owned.variant,revealed=owned.revealed,axes=owned.axes,element=owned.element,
                builtInElement=owned.builtInElement,rounds=ClipSize(owned.form,owned.variant)};
            int target=roster?Mathf.Min(roster.MaxMk,UpgradeTargetMk(owned,wave)+MkBonus(wave)):owned.mk+1;
            // Document 5.7: the top Mk only comes from upgrading the Mk just below it; a weapon further behind stops one short of it.
            if(roster&&target>=roster.MaxMk&&owned.mk<roster.MaxMk-1)target=roster.MaxMk-1;
            version.mk=target;
            version.upgradeOf=owned;
            return version;
        }
        public float MkDamage(RunWeapon w)
        {
            if(w==null)return 1f;
            float rule=MkDamageRule?.Invoke(w)??0f;
            return rule>0f?rule:!roster?1f:roster.MkDamage(w.mk);
        }
        /// <summary>V2 day campaign (since 2026-11-10): a weapon's Mk damage factor by the campaign's own Mk table and the Armament Bay's
        /// shared Mk; 0 or null outside a running campaign, where the roster's table applies.</summary>
        public static Func<RunWeapon,float> MkDamageRule;
        /// <summary>V2 day campaign: the Mk a new anomaly weapon arrives at (the Armament Bay's); 0 or null outside a running campaign.</summary>
        public static Func<int> NewWeaponMk;
        /// <summary>V2 day campaign: extra damage on every weapon hit (the last day's overload, in the Tyrant's fight); 1 or null elsewhere.</summary>
        public static Func<float> DamageBonus;
        /// <summary>V2 day campaign (since 2026-11-10): one weapon's own damage factor (Twist) and attack speed factor (Tame); null or 1
        /// outside a running campaign.</summary>
        public static Func<RunWeapon,float> WeaponDamageRule,WeaponSpeedRule;
        static float WeaponSpeed(RunWeapon w)=>Mathf.Max(.01f,WeaponSpeedRule?.Invoke(w)??1f);
        /// <summary>A running campaign sets weapon Mk itself: no armory offer raises it and no bonus Mk comes with a new weapon.</summary>
        static bool CampaignMk=>(NewWeaponMk?.Invoke()??0)>0;
        System.Collections.Generic.List<WeaponForm> ArmoryForms(int wave,bool? autoKind)
        {
            var forms=new System.Collections.Generic.List<WeaponForm>();
            foreach(var d in roster.definitions)
            {
                bool isAuto=RunWeapon.IsAutoForm(d.form);
                // Only anomaly weapons are offered: never the Pistol, even from a roster that lists it (since 2026-11-06).
                if(d.retired||!RunWeapon.IsAnomalousForm(d.form)||d.unlockWave>wave||OwnsForm(d.form)||autoKind.HasValue&&autoKind.Value!=isAuto)continue;
                forms.Add(d.form);
            }
            return forms;
        }
        public bool TakeOffer(int index,int replace=-1)
        {
            if(index<0||index>=Offers.Length||Offers[index]==null)return false;var w=Offers[index];
            if(w.upgradeOf!=null)
            {
                // A stronger version upgrades the held weapon where it is: only its Mk rises. upgradeOf stays set, so
                // the weapon card that follows shows the held weapon.
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
            // Chain Blade Tame: nothing breaks the two-way pull, so a forced cancel lets a reeling hook finish.
            if(cancelHook&&AttackPool.Instance)
                foreach(var actor in AttackPool.Instance.actors)
                    if(actor&&actor.gameObject.activeSelf&&actor.weapon==w&&actor.mode==AttackMode.Hook&&!BattlefieldFrozen(actor.gameObject)&&!(actor.HookReeling&&w.Has(UpgradeAxis.Tame)))actor.Release();
        }
        public void Switch()
        {
            // Document 5.4: with the other hand slot empty, right-click does nothing (no switching to bare hands).
            if(!CanUseHand||HandHookBusy||Hand[1-ActiveIndex]==null)return;var w=Active;
            // Spin-up Twist: switching away at full spin with the trigger still held leaves the minigun firing in place.
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
                // Blizzard Twist shatters on a real release only; a forced cancel (knockback, stun, switch) goes through CancelInput.
                if(w.form==WeaponForm.CryoCannon&&w.variant==1&&w.Has(UpgradeAxis.Twist)&&w.heldTime>0)BlizzardShatter(w);
                StopTrigger(w);
            }
            if(pressed&&w.reload>0)V5WeaponAudio.Instance?.Play(V5Sfx.DryClick); // pressing fire on an empty clip: heard, and nothing else happens
            if(!held&&!pressed||w.cooldown>0||w.reload>0||w.inFlight||w.charging)return;
            bool charge=(w.form==WeaponForm.Rifle&&w.variant==0)||(w.form==WeaponForm.Railgun&&w.variant==0)||(w.form==WeaponForm.ScatterCannon&&w.variant==1);
            if(charge){if(pressed){w.charging=true;w.charge=0;Reveal(w);WeaponSound(w,WeaponAudioPhase.ChargeStart);if(w.form==WeaponForm.ScatterCannon)V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponFocusCharge,player.Position);}return;}
            // The Pistol (since 2026-11-06) fires again at its fixed interval for as long as the button is held.
            bool repeat=w.form==WeaponForm.Rifle&&w.variant==1||w.form==WeaponForm.Flamethrower&&w.variant==0||w.form==WeaponForm.CryoCannon&&w.variant==1||w.form==WeaponForm.SporeLauncher&&w.variant==1||w.form==WeaponForm.Minigun||w.form==WeaponForm.Pistol;
            if(pressed||repeat&&held)Fire(w,false);
        }
        /// <summary>Reveals a weapon at once: the weapon card explains it the moment it is taken.</summary>
        public void RevealNow(RunWeapon w)=>Reveal(w);
        /// <summary>One hit with the current Mk, cards and multipliers, as the weapon would deal it now.</summary>
        public float HitDamage(RunWeapon w){var d=roster.Get(w.form);return d==null?0f:(w.variant==0?d.damageA:d.damageB)*DamageScale(w);}
        /// <summary>Seconds between uses (cooldown, or an auto weapon's trigger interval) with the current modifiers.</summary>
        /// <summary>The weapon's theoretical DPS, one hit's damage times hits a second (what its card shows: HitDamage / CycleSeconds).</summary>
        public float TheoreticalDps(RunWeapon w){float cycle=CycleSeconds(w);return cycle>0f?HitDamage(w)/cycle:0f;}
        public float CycleSeconds(RunWeapon w){var d=roster.Get(w.form);return d==null?0f:(w.variant==0?d.cooldownA:d.cooldownB)*CooldownScale(w);}
        void Reveal(RunWeapon w){if(w.revealed)return;w.revealed=true;ArenaCodexProgress.DiscoverWeapon(w.form);if(!w.IsAnomalous)return;PlayerPrefs.SetInt("anomaly."+(int)w.form+"."+w.variant,1);Revealed?.Invoke(w);}
        public void Tick(float dt)
        {
            float distance=Query.Flat(player.Position-lastPosition).magnitude;lastPosition=player.Position;
            if(!CanUseHand)CancelInput(Active,true);
            WatchPlayerHits();
            PruneTracers();
            // Frost on the player slows its weapon timers as well as its feet.
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
                // Only the Capacitor in your hand charges and counts its full timer; stowed it drains and its full timer runs back (document 5.4).
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
                // Lance trail, deployed Spin-up minigun and Bombard Twist blasts run whichever weapon is in hand.
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
            // Cast from a little behind: a step that ends exactly touching a wall would start the next cast inside it, and SphereCast
            // ignores colliders it starts in, so the missile would fly through.
            const float castBack=.1f;
            bool wallHit=Physics.SphereCast(Query.AtCastHeight(from)-dashDirection*castBack,player.radius,dashDirection,out var hit,step+castBack,GameManager.WallMask);
            float travel=wallHit?Mathf.Min(step,Mathf.Max(0f,hit.distance-castBack)):step;
            DamageHumanRocketContacts(from,travel,weapon);
            if(!player.IsAlive){dashLeft=0;dashWeapon=null;return;}
            if(wallHit)
            {
                player.BounceFromWall(Query.Flat(hit.point)-dashDirection*player.radius,hit.normal,3,!weapon.Has(UpgradeAxis.Tame));
                // Tame: a wall bounce costs no HP and refunds this flight's cooldown.
                if(weapon.Has(UpgradeAxis.Tame))weapon.cooldown=0;
                WeaponSound(weapon,WeaponAudioPhase.Impact);dashLeft=0;dashWeapon=null;return;
            }
            // Design document 4.5: the flight ignores knockback, not gravity; a black hole still drags the missile sideways. Tame
            // (document 5.8): the flight is neither dragged nor swallowed by a black hole (WorldMap asks MissileHoleProof).
            bool holeProof=weapon.Has(UpgradeAxis.Tame);
            Vector3 drift=WorldMap.Active&&!holeProof?Query.Flat(WorldMap.Active.GetPull(from,player))*Time.fixedDeltaTime:Vector3.zero;
            Vector3 next=from+dashDirection*travel+drift;
            player.SetMovePosition(next);dashLeft-=travel;
            // Q26: a tamed flight that would end over a hole flies on at its end speed until both ends of its step are clear of the hole.
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
                // Grow cached contact data only if a larger authored pool is added; no runtime GameObjects are created.
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
            // A fast dash can meet several bodies in one physics step: contact order decides the first hit, not registry order.
            Array.Sort(dashContactDistances,dashContacts,0,count);
            // Damage is captured at launch, so every new target in this flight shares the same raw contact value.
            float contactDamage=dashContactDamage;
            for(int i=0;i<count;i++)
            {
                var contact=dashContacts[i];dashContacts[i]=default;var body=contact.body;
                if(!body||!body.IsAlive||body.SpawnVersion!=contact.spawnVersion||!dashHits.Add(contact))continue;
                // Twist: a real knockback along the flight, farther the faster the missile still flies at the contact; a boss is never knocked back.
                float push=!weapon.Has(UpgradeAxis.Twist)||body is Enemy{IsBoss:true}?0f:upgrades.rocketCueMax*RocketSpeedShare(Mathf.Max(0f,dashLeft-dashContactDistances[i]));
                Damage(weapon,body,contactDamage,push,dashDirection);
            }
        }
        /// <summary>Launch Yourself: the missile's speed with this much of its flight left. It slows evenly over the distance from its launch
        /// speed to rocketEndSpeedShare of it; the launch speed is raised so that a full flight lasts as long as one at the old constant speed.</summary>
        public float RocketSpeed(float left)
        {
            float cruise=25f/StartingFormCycleFactor(WeaponForm.RocketLauncher),end=Mathf.Clamp(upgrades.rocketEndSpeedShare,.1f,1f);
            // A flight slowing evenly from v to end*v over its range lasts range*ln(1/end)/(v*(1-end)); this v matches range/cruise.
            float launch=end>.999f?cruise:cruise*Mathf.Log(1f/end)/(1f-end);
            return launch*RocketSpeedShare(left);
        }
        /// <summary>Launch Yourself: the missile's speed with this much of its flight left, as a share of its launch speed (1 at launch,
        /// rocketEndSpeedShare at the end of the flight).</summary>
        public float RocketSpeedShare(float left)=>Mathf.Lerp(1f,Mathf.Clamp(upgrades.rocketEndSpeedShare,.1f,1f),dashRange>0f?Mathf.Clamp01(1f-left/dashRange):0f);
        public float StartingFormDamageFactor(WeaponForm form)=>form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?Mathf.Max(.01f,startingDamageFactor):1f;
        public float StartingFormCycleFactor(WeaponForm form)=>form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?Mathf.Max(.01f,startingCycleFactor):1f;
        float DamageScale(RunWeapon w)=>StartingFormDamageFactor(w.form)*w.Power*MkDamage(w)*(progression?progression.Stats.DamageMultiplier:1)*Mathf.Max(.01f,crowdDamageMultiplier)*SkillDamageMultiplier*Mathf.Max(0f,DamageBonus?.Invoke()??1f)*Mathf.Max(0f,WeaponDamageRule?.Invoke(w)??1f);
        static SkillLoadout Skills=>GameManager.Instance?GameManager.Instance.skills:null;
        /// <summary>Overcharge: weapon damage multiplier from skills (hand and auto weapons alike).</summary>
        static float SkillDamageMultiplier=>Skills?Skills.SkillDamageMultiplier:1f;
        /// <summary>Overcharge: how fast weapon cooldowns run (hand and auto weapons alike).</summary>
        static float WeaponCooldownRate=>Skills?Skills.WeaponCooldownRate:1f;
        float ChargeSeconds(RunWeapon w)=>w.form==WeaponForm.Railgun?.5f:w.form==WeaponForm.ScatterCannon?Mathf.Max(.01f,upgrades.focusChargeTime):1f;
        float CooldownScale(RunWeapon w)=>StartingFormCycleFactor(w.form)*w.Tempo*(progression?progression.Stats.CooldownMultiplier:1)/(GameManager.Instance.skills?GameManager.Instance.skills.AttackSpeedMultiplier:1)/MeltdownHaste/WeaponSpeed(w);
        /// <summary>Hook re-throw lock: stat and skill cooldown modifiers apply, the starting-form cycle factor does not (the hook's flight is already the fast part).</summary>
        public float HookCooldown(RunWeapon w)=>hookCooldown*w.Tempo*(progression?progression.Stats.CooldownMultiplier:1)/(GameManager.Instance.skills?GameManager.Instance.skills.AttackSpeedMultiplier:1)/MeltdownHaste/WeaponSpeed(w);
        public float GetCooldownDuration(RunWeapon w)
        {
            if(w==null||!roster)return 0f;
            var definition=roster.Get(w.form);if(definition==null)return 0f;
            if(w.holeReturnSpan>0f)return w.holeReturnSpan; // a knife lost to a black hole fills the slot until it is back
            if(w.form==WeaponForm.Knife&&w.variant==1)return HookCooldown(w); // the slot fill follows the re-throw lock
            float duration=w.form==WeaponForm.Minigun&&w.variant==0?1f/SpinUpRate(w):w.variant==0?definition.cooldownA:definition.cooldownB;
            return duration*CooldownScale(w);
        }
        float KnockScale=>(progression?progression.Stats.KnockbackMultiplier:1)*(GameManager.Instance.skills?GameManager.Instance.skills.KnockbackMultiplier:1);
        public float UpgradePushScale=>KnockScale;
        /// <param name="strike">A Fire that is the shot landing rather than leaving (a Shatter shard on a body): same feedback, its own sound.</param>
        static void WeaponSound(RunWeapon weapon,WeaponAudioPhase phase=WeaponAudioPhase.Fire,bool strike=false)
        {
            if(weapon==null)return;
            if((phase is WeaponAudioPhase.Fire or WeaponAudioPhase.Launch)&&GameManager.Instance)GameManager.Instance.NoteWeaponAttack(weapon);
            if(!weapon.IsAuto && phase is WeaponAudioPhase.Fire or WeaponAudioPhase.Launch)
            {
                var playerBody=GameManager.Instance?GameManager.Instance.player:null;
                if(playerBody){playerBody.GetComponent<V2ModelPresentation>()?.MarkAttack();FireFeedback(weapon,playerBody);}
            }
            V5WeaponAudio.Weapon(weapon,phase,strike);
        }
        /// <summary>Shooting feel: model recoil (or a lunge for melee), a camera push against the aim and a muzzle flash. Nothing pauses.</summary>
        static void FireFeedback(RunWeapon weapon,PlayerController body)
        {
            var gm=GameManager.Instance;if(!gm)return;
            var profile=gm.feedback.For(WeaponFeel.Of(weapon));
            Vector3 aim=Query.Flat(body.AimDirection);if(aim.sqrMagnitude<1e-4f)return;aim.Normalize();
            if(V5Vfx.On){V5Vfx.I.FireKick(weapon,body,aim,profile);return;}
            body.VisualKick(-aim,profile.fireRecoil,.1f);
            gm.Kick(-aim,profile.fireKick);
            var pool=FxPool.Active;
            if(profile.muzzleFlash>0f&&pool&&pool.muzzleFlash)
                pool.EmitDirectional(pool.muzzleFlash,Query.AtCastHeight(body.Position)+aim*(body.radius+.45f),aim,profile.muzzleFlash,.07f,new Color(1f,.88f,.6f));
        }
        void Fire(RunWeapon w,bool charged)
        {
            Reveal(w);
            // A Capacitor click with almost no charge does nothing and costs no cooldown.
            if(w.form==WeaponForm.ArcCoil&&w.variant==1&&w.charge<upgrades.capacitorMinCharge){V5WeaponAudio.Instance?.Play(V5Sfx.DryClick);return;}
            var d=roster.Get(w.form);float damage=(w.variant==0?d.damageA:d.damageB)*DamageScale(w);float range=(w.variant==0?d.rangeA:d.rangeB)*w.Reach;w.cooldown=(w.variant==0?d.cooldownA:d.cooldownB)*CooldownScale(w);
            Vector3 aim=player.AimDirection,at=player.Position,point=at+Vector3.ClampMagnitude(Query.Flat(player.AimPoint-at),range);
            if(w.form==WeaponForm.Rifle&&w.variant==0)
            {
                float hit=Mathf.Lerp(Mathf.Clamp(roster.RifleMinimumChargeDamage,0f,d.damageA),d.damageA,w.charge)*DamageScale(w),push=Mathf.Lerp(5,10,w.charge);
                // Twist (document 5.8): a full-charge release also blasts the close cone ahead, and its round grows
                // stronger with every enemy it passes through (AttackActor reads the growth on rifle rounds only, so it is set on each).
                bool full=w.Has(UpgradeAxis.Twist)&&w.charge>=.999f;
                var round=Bolt(w,aim,hit,push,range,80,true);
                if(round){round.pierceGrowth=full?upgrades.chargedPierceGrowth:0f;round.pierceGrowthCap=upgrades.chargedPierceGrowthCap;}
                if(full)ChargedMuzzleBlast(w,aim);
            }
            else if(w.form==WeaponForm.Rifle)
            {
                // Backfire: the round leaves from your back. Tame (document 5.8) keeps that: each round's recoil pushes
                // you along the aim, and while an enemy is close in front the 1st, 3rd... round of a clip goes for it instead.
                Vector3 dir=-aim;
                if(w.Has(UpgradeAxis.Tame)){var front=(ClipSize(w.form,w.variant)-w.rounds)%2==0?FrontGuardTarget():null;if(front)dir=Query.Flat(front.Position-player.Position).normalized;player.Push(aim,upgrades.reverseTameRecoil);}
                Bolt(w,dir,damage,0,range,30,false,1);
                SpendRound(w,.6f);
            }
            else if(w.form==WeaponForm.Knife&&w.variant==0)
            {
                var a=Bolt(w,aim,damage,0,range,knifeOutboundSpeed/StartingFormCycleFactor(w.form)*KnifeCatchSpeed(w));if(a)a.knifeSpeedFactor=KnifeCatchSpeed(w);
                // Tame: a catch adds bounce targets (and speed) to this throw only.
                // Every return cuts its thrower for the base value (rule: self-damage never grows with Mk or cards).
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
                    if(!ValidTeleportPoint(w.recallPoint,player.radius)){GameManager.Instance.hud?.Toast(ArenaText.Pair("返回落点被挡住了","Recall point is blocked"),new Color(.8f,.4f,1f));return;}
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
            // Wildfire backfire: your own patches burn you (Tame: they do not).
            else if(w.form==WeaponForm.Flamethrower){var a=LaunchArc(w,AttackMode.Lob,point,damage,0,2.5f*w.Reach,4,LobSpeed,LobArcHeight,!w.Has(UpgradeAxis.Tame));if(a)a.ownerDamage=d.damageB*upgrades.wildfireSelfShare;else LandingBlocked(w);}
            else if(w.form==WeaponForm.CryoCannon&&w.variant==0)ShatterFan(w,aim,range,damage);
            else if(w.form==WeaponForm.CryoCannon)BlizzardSpray(w,range,damage);
            else if(w.form==WeaponForm.ArcCoil&&w.variant==0){Chain(w,range,damage,w.Has(UpgradeAxis.Reach)?7:5);}
            else if(w.form==WeaponForm.ArcCoil)
            {
                // Discharge grows with the stored share of damageB; a full Capacitor hits harder still.
                CapacitorDischarge(w,range,damage);
            }
            else if(w.form==WeaponForm.SporeLauncher&&w.variant==0){var a=LaunchArc(w,AttackMode.Lob,point,damage,0,3.5f*w.Reach,Mathf.Max(0f,upgrades.cloudSeconds),LobSpeed,LobArcHeight,false);if(a){a.tickInterval=.5f;TrackSporeCloud(a,w);}else LandingBlocked(w);}
            // Needler backfire: the reload that an emptied clip starts bursts a mist where you stand.
            else if(w.form==WeaponForm.SporeLauncher){Bolt(w,aim,damage,0,range,35);if(SpendRound(w,2))NeedlerReloadMist(w);}
            else if(w.form==WeaponForm.Railgun&&w.variant==0)
            {
                // Lance Tame: a wall close behind braces the shot (no recoil, more damage); otherwise the recoil is halved.
                Vector3 brace=default;bool braced=w.Has(UpgradeAxis.Tame)&&LanceBraced(aim,out brace);
                // Document 6.4: the slug skips the Shield's damage cut but still empties the Shield before the HP.
                var a=Bolt(w,aim,braced?damage*upgrades.lanceBraceDamage:damage,0,range,120,true);if(a){a.ignoreShieldReduction=true;a.pierceDamageRetention=.75f;StartLanceBeam(a);}
                // Document 4.4: the recoil is a push you keep control through, never a knockback, so a wall behind you does not hurt.
                if(braced)Fx.Ring(brace,.6f,new Color(1f,.85f,.5f,.9f));else player.Push(-aim,LanceRecoil(w));
            }
            else if(w.form==WeaponForm.Railgun)Execute(w,range,damage);
            else if(w.form==WeaponForm.Minigun){Bolt(w,aim,damage,0,range,40);if(w.variant==0)w.cooldown=1/SpinUpRate(w)*CooldownScale(w);else{float before=w.heat;w.heat+=.1f;MeltdownWarning(w,before);if(w.heat>=4)Meltdown(w);}}
            // Barrage: homing missiles; with too few enemies to lock, some come back for you (RunWeaponsUpgrades.Barrage).
            else if(w.form==WeaponForm.SwarmLauncher&&w.variant==0)Barrage(w,aim,range,damage,d.damageA);
            else if(w.form==WeaponForm.SwarmLauncher)Bombard(w,aim,range,damage,d.damageB);
            // Pistol (since 2026-11-06): one plain round along the aim that stops at the first enemy: no pierce, no bounce, no push.
            else if(w.form==WeaponForm.Pistol)Bolt(w,aim,damage,0,range,Mathf.Max(1f,pistolRoundSpeed));
        }
        // Spin-up Tame: braced against a wall, the gun in hand fires spinBraceRateBonus faster.
        float SpinUpRate(RunWeapon w)=>Mathf.Max(.1f,Mathf.Lerp(upgrades.spinUpStartRate,upgrades.spinUpTopRate,Mathf.Clamp01(w.heldTime/3f)))*(w==Active&&Anchored?1f+Mathf.Max(0f,upgrades.spinBraceRateBonus):1f);
        /// <summary>Bombard: blasts along the aim from bombardStart to the range, each after a short fuse with a landing marker;
        /// none are placed past the first wall. Standing still is safe; walking forward into the line is the backfire.</summary>
        void Bombard(RunWeapon w,Vector3 aim,float range,float damage,float selfDamage)
        {
            w.bombardRefunded=false;
            // Twist: the blasts are placed one at a time along the aim of that moment (TickBombard), so a mouse sweep bends the line.
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
        /// <summary>Wildfire and Cloud found no landing spot: nothing is thrown, so the cooldown is refunded and the player is told.</summary>
        void LandingBlocked(RunWeapon w)
        {
            w.cooldown=0;
            GameManager.Instance.hud?.Toast(ArenaText.Pair("落点被挡住了","Landing spot is blocked"),new Color(1f,.6f,.3f));
        }
        /// <summary>The landing point nearest the aimed one on the line from the player, inside the map and off walls and holes; null if none.</summary>
        Vector3? LandingPoint(Vector3 desired)
        {
            var world=WorldMap.Active;if(!world||!player)return null;
            Vector3 from=Query.Flat(player.Position);
            desired=Query.Flat(world.TrialActive?world.trialPlatform.position+Vector3.ClampMagnitude(Query.Flat(desired-world.trialPlatform.position),Mathf.Max(0f,world.trialRadius-.2f)):world.ClampPoint(desired,.2f));
            Vector3 delta=desired-from;float length=delta.magnitude;Vector3 dir=length>1e-4f?delta/length:Vector3.zero;
            for(float distance=length;distance>=0f;distance-=.25f){Vector3 at=from+dir*distance;if(ValidTeleportPoint(at,.2f))return at;}
            return null;
        }
        /// <summary>Where a shot leaves the body: this far along dir, or just in front of a wall the body touches, so it never starts inside the wall.</summary>
        Vector3 SpawnPoint(Vector3 dir,float distance)
        {
            dir=Query.Flat(dir).normalized;Vector3 from=Query.AtCastHeight(player.Position);
            if(Physics.Linecast(from,from+dir*distance,out var wall,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0f,wall.distance-upgrades.spawnWallBackoff);
            return player.Position+dir*distance;
        }
        /// <summary>Muzzle point at cast height, where shot lines start.</summary>
        Vector3 Muzzle(Vector3 dir)=>Query.AtCastHeight(player.Position)+Query.Flat(dir).normalized*(player.radius+.45f);
        /// <summary>Where the hook holds a caught enemy: in front of the aim, short of any wall there.</summary>
        public Vector3 HookHoldPoint(Combatant held)
        {
            Vector3 aim=Query.Flat(player.AimDirection).normalized,from=Query.AtCastHeight(player.Position);float distance=player.radius+held.radius+.3f;
            if(Physics.SphereCast(from,held.radius,aim,out var wall,distance,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0f,Mathf.Min(distance,wall.distance-upgrades.hookWallGap));
            // A big body facing a close wall starts the sphere inside it, which SphereCast does not report: the ray still clamps it.
            if(Physics.Raycast(from,aim,out var face,distance+held.radius,GameManager.WallMask,QueryTriggerInteraction.Ignore))
                distance=Mathf.Max(0f,Mathf.Min(distance,face.distance-held.radius-upgrades.hookWallGap));
            return player.Position+aim*distance;
        }
        static bool WallBlocks(Vector3 from,Vector3 to)=>Query.WallBetween(Query.AtCastHeight(from),Query.AtCastHeight(to),out _);
        // Reverse Shot 4, Scatter Blast 2 (two shots then reload), Needler 24.
        public int ClipSize(WeaponForm f,int v)=>f==WeaponForm.Rifle&&v==1?Mathf.Max(1,rifleClipSize):f==WeaponForm.ScatterCannon&&v==0?2:f==WeaponForm.SporeLauncher&&v==1?24:int.MaxValue;
        /// <summary>Spends one round; returns true when that emptied the clip and a reload started.</summary>
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
            // Validate both destinations before moving either body; a partial swap would strand the player.
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
        /// <summary>Area hit around a point; walls between the centre and a body shield it. selfHit (when not negative) is what the wielder takes instead of hit.</summary>
        void Area(RunWeapon w,Vector3 at,float radius,float hit,float push,bool self,float selfHit=-1f){foreach(var c in Combatant.All)if(c.IsAlive&&(self||c!=player)&&(Query.Flat(c.Position-at)).magnitude<=radius+c.radius&&!WallBlocks(at,c.Position))Damage(w,c,c==player&&selfHit>=0f?selfHit:hit,push,Query.Flat(c.Position-at).normalized);Fx.Ring(at,radius,ElementCombat.ColorFor(w.element));}
        /// <summary>continuous: a spray or drain hitting every tick; for element stacks it counts at most once per StatusRules.continuousStackInterval on each target.</summary>
        void Damage(RunWeapon w,Combatant c,float hit,float push,Vector3 dir,bool noLightningJump=false,bool continuous=false){var info=DamageInfo.Attack(hit,player,push<0?-dir:dir,Mathf.Abs(push)*KnockScale,HitKind.Blast);info.feel=WeaponFeel.Of(w);info.element=w.element;info.builtInElement=w.builtInElement;info.weapon=w;info.noLightningJump=noLightningJump;info.continuousSource=continuous?w:null;lastDamageAccepted=c.ReceiveDamage(info);if(lastDamageAccepted)WeaponSound(w,WeaponAudioPhase.Impact);}
        AttackActor Bolt(RunWeapon w,Vector3 dir,float hit,float push,float reach,float velocity,bool pierce=false,int ricochet=0){var a=AttackPool.Instance.Acquire();if(!a)return null;a.Configure(AttackMode.Bolt,player,w,SpawnPoint(dir,.7f),dir,hit,push*KnockScale,reach,velocity,0,reach/velocity+3);a.pierce=pierce;a.ricochets=ricochet;WeaponSound(w,w.form is WeaponForm.Knife or WeaponForm.PhaseDagger?WeaponAudioPhase.Launch:WeaponAudioPhase.Fire);return a;}
        AttackActor Field(RunWeapon w,AttackMode mode,Vector3 at,float hit,float push,float radius,float life,float delay,bool self){var a=AttackPool.Instance.Acquire();if(!a)return null;a.Configure(mode,player,w,at,player.AimDirection,hit,push*KnockScale,0,0,radius,life+delay+.1f);a.delay=delay;a.hurtOwner=self;WeaponSound(w,mode==AttackMode.Blast?WeaponAudioPhase.Launch:WeaponAudioPhase.Fire);return a;}
        /// <summary>Chain: the arc goes to the nearest visible enemy in a cone ahead, then jumps between visible neighbours.
        /// Its hits only add Lightning; the element system's own jumps would double the chain.</summary>
        void Chain(RunWeapon w,float range,float hit,int count)
        {
            var first=ChainFirstTarget(range);upgradeHits.Clear();
            // Nobody to arc to: no discharge and no backlash, just a dry click (the cooldown still runs).
            if(!first){WeaponSound(w,WeaponAudioPhase.Cancel);return;}
            // Tame: the jumps stored by the last discharge are spent on this one.
            count+=w.chainStoredJumps;w.chainStoredJumps=0;
            Vector3 at=player.Position,from=Muzzle(Query.Flat(first.Position-player.Position));
            Color arc=ElementCombat.ColorFor(ElementKind.Lightning);
            for(int i=0;i<count;i++)
            {
                Combatant next=i==0?first:null;
                if(i>0)
                {
                    // Jumps stay within 4 cells and never through a wall. Twist: the one nearest your aim point, not nearest the arc.
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
        /// <summary>Nearest living enemy within range that the player can see (and that accept allows).</summary>
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
        /// <summary>avoid: the card a reroll replaces (document 9.4). Its weapon and direction are not offered again while another
        /// weapon or direction is open; it repeats only when it is the last one. wave: the 1-based wave whose direction weights apply
        /// (0 = the wave now in progress).</summary>
        public DraftCard RollUpgradeCard(WeaponUpgradeOffer avoid=null,int wave=0)
        {
            var choices=new System.Collections.Generic.List<RunWeapon>();
            foreach(var slots in new[]{Hand,Auto})foreach(var w in slots)
                if(w!=null&&w.revealed&&AvailableAxes(w).Count>0&&!OnlyOffers(w,avoid))choices.Add(w);
            if(choices.Count==0)return avoid!=null?RollUpgradeCard(null,wave):null;
            var weapon=choices[UnityEngine.Random.Range(0,choices.Count)];var axes=AvailableAxes(weapon);
            bool canReinfuse=CanReinfuse(weapon);
            if(avoid!=null&&avoid.Weapon==weapon){axes.Remove(avoid.Axis);if(avoid.Axis==UpgradeAxis.Infusion)canReinfuse=false;}
            // A change-element card only stands in for an axis the weapon could still take; a finished weapon offers no card.
            bool reinfuse=canReinfuse&&(axes.Count==0||UnityEngine.Random.value<upgrades.reinfuseChance);
            var upgrade=reinfuse?UpgradeAxis.Infusion:PickDirection(axes,wave>0?wave:CurrentWave);
            // Under a Void Walk enchant the card reads the element the weapon gets back, not the borrowed Void.
            ElementKind own=OwnElement(weapon),element=upgrade==UpgradeAxis.Infusion?RollElement(own):own;
            string desc=upgrade==UpgradeAxis.Infusion?ArenaText.Pair("灌注 ","Infuse ")+ArenaTerms.Element(element)+(own!=ElementKind.None?ArenaText.Pair("，替换 ",", replaces ")+ArenaTerms.Element(own):""):upgrade switch{UpgradeAxis.Power=>ArenaText.Pair("伤害 +40%","Damage +40%"),UpgradeAxis.Tempo=>ArenaText.Pair("冷却、蓄力和装填 -30%","Cooldown, charge and reload -30%"),UpgradeAxis.Reach=>ArenaText.Pair("射程、范围或目标数 +40%","Range, area or targets +40%"),UpgradeAxis.Twist=>ArenaText.Pair("给这把武器的攻击方式加一个变招","Adds a twist to this weapon's attack"),_=>ArenaText.Pair("改良这个搭配的特性","Improves this pairing's trait")};
            if(upgrade is UpgradeAxis.Twist or UpgradeAxis.Tame)desc=weapon.form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?CoreAnomalyUpgradeDescription(weapon,upgrade):ArenaTerms.ExpandedUpgradeDescription(weapon.form,weapon.variant,upgrade);
            // The Infusion card also says what the element does, from the live element rules.
            if(upgrade==UpgradeAxis.Infusion){string rule=ArenaTerms.ElementRule(element);if(rule.Length>0)desc+="\n"+rule;}
            // A card put off fits only while its weapon is still held with the same upgrades and element.
            var axesBefore=weapon.axes;var elementBefore=own;
            return new DraftCard(DraftCardKind.Weapon,weapon.Label+" / "+ArenaTerms.Upgrade(upgrade),desc,_=>{weapon.axes|=upgrade;if(upgrade==UpgradeAxis.Infusion)SetOwnElement(weapon,element);return true;},payload:new WeaponUpgradeOffer(weapon,upgrade,element),
                stillFits:()=>(Array.IndexOf(Hand,weapon)>=0||Array.IndexOf(Auto,weapon)>=0)&&weapon.axes==axesBefore&&OwnElement(weapon)==elementBefore);
        }
        static int CurrentWave=>GameManager.Instance&&GameManager.Instance.waves?Mathf.Max(1,GameManager.Instance.waves.WaveIndex+1):1;
        /// <summary>V2 day campaign (since 2026-11-10, the base's study window): the text a Tame or Twist card of this weapon shows.</summary>
        public string UpgradeDescription(RunWeapon w,UpgradeAxis axis)=>w==null?"":w.form is WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher?CoreAnomalyUpgradeDescription(w,axis):ArenaTerms.ExpandedUpgradeDescription(w.form,w.variant,axis);
        /// <summary>The weapon's own element (under a Void Walk enchant, the one it gets back).</summary>
        public ElementKind ElementOf(RunWeapon w)=>w==null?ElementKind.None:OwnElement(w);
        /// <summary>V2 day campaign (the study window): infuses the weapon with this element at once, as an Infusion card does.</summary>
        public void Infuse(RunWeapon w,ElementKind element){if(w==null)return;w.axes|=UpgradeAxis.Infusion;SetOwnElement(w,element);}
        /// <summary>Document 9.6: one of the open directions, drawn by this wave's weights in ProgressionRules, so later
        /// waves favour Twist (blue) and Infusion (purple) over Tame (green). Weights only bias the draw: a lone open direction, or open
        /// directions that all weigh nothing, are still offered.</summary>
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
        /// <summary>True when the only direction w could offer is the avoided card's.</summary>
        bool OnlyOffers(RunWeapon w,WeaponUpgradeOffer avoid)
        {
            if(avoid==null||avoid.Weapon!=w)return false;
            var axes=AvailableAxes(w);axes.Remove(avoid.Axis);
            return axes.Count==0&&(!CanReinfuse(w)||avoid.Axis==UpgradeAxis.Infusion);
        }
        /// <summary>Twist/Tame card text of the three starting forms; the numbers are read from the live tuning so the card matches the code.</summary>
        string CoreAnomalyUpgradeDescription(RunWeapon w,UpgradeAxis axis)
        {
            bool twist=axis==UpgradeAxis.Twist;var u=upgrades;
            string P(float share)=>$"{share*100f:0.#}";
            return (w.form,w.variant,twist) switch
            {
                // Document 5.8: the Charged Shot Twist and Tame, Reverse Shot Tame, Flying Blade Tame, Chain Blade Tame and Launch Yourself Tame.
                (WeaponForm.Rifle,0,true)=>ArenaText.Pair($"蓄满松手时枪口喷出气浪，把前方 {u.chargedBlastRange:0.#} 格内的敌人击退 {u.chargedBlastPush:0.#} 格；这一发每穿过 1 个敌人，伤害 +{P(u.chargedPierceGrowth)}%（最多 +{P(u.chargedPierceGrowthCap)}%）。",
                    $"A full-charge release also blasts the cone ahead, knocking enemies within {u.chargedBlastRange:0.#} cells back {u.chargedBlastPush:0.#} cells; that round deals +{P(u.chargedPierceGrowth)}% damage for each enemy it has passed (up to +{P(u.chargedPierceGrowthCap)}%)."),
                (WeaponForm.Rifle,0,false)=>ArenaText.Pair($"蓄力时你不再减速，受到敌人攻击的伤害改为 -{P(1f-u.chargeTameDamageTaken)}%；准星线上的非 Boss 敌人减速 {P(1f-u.chargeLineSlowFactor)}%。",
                    $"Charging no longer slows you, and enemy attacks hit you for {P(1f-u.chargeTameDamageTaken)}% less instead of more; non-boss enemies on your aim line move {P(1f-u.chargeLineSlowFactor)}% slower."),
                (WeaponForm.Rifle,1,true)=>ArenaText.Pair($"子弹第一次弹墙时分裂成 2 颗，各 {P(u.reverseSplitShare)}% 伤害。",$"On its first wall bounce a round splits in two, each at {P(u.reverseSplitShare)}% damage."),
                (WeaponForm.Rifle,1,false)=>ArenaText.Pair($"仍从背后射击，每发后坐把你往准星方向推 {u.reverseTameRecoil:0.#} 格；身前 {u.reverseGuardRange:0.#} 格内有敌人时，隔一发改为向前打它。",
                    $"Rounds still leave from your back, and each one's recoil pushes you {u.reverseTameRecoil:0.#} cells along your aim; while an enemy is within {u.reverseGuardRange:0.#} cells in front, every other round flies forward at it."),
                (WeaponForm.Knife,0,true)=>ArenaText.Pair($"飞刃回到出手点时旋斩，{u.knifeSpinRadius:0.#} 格内这次回程没砍到的敌人各受 {P(u.knifeSpinShare)}% 伤害。",$"When the blade comes home it spins: enemies within {u.knifeSpinRadius:0.#} cells that this return did not cut take {P(u.knifeSpinShare)}% damage."),
                (WeaponForm.Knife,0,false)=>ArenaText.Pair($"折返的飞刃不再伤你；从你身上扫过算接住：冷却缩短 {P(1f-u.knifeCatchCooldownShare)}%，下一刀多弹 {u.knifeCatchTargets} 个敌人、飞行速度 +{P(u.knifeCatchSpeed-1f)}%。",
                    $"The returning blade no longer cuts you; sweeping through you is a catch: {P(1f-u.knifeCatchCooldownShare)}% shorter cooldown, and the next throw bounces to {u.knifeCatchTargets} more enemies and flies {P(u.knifeCatchSpeed-1f)}% faster."),
                (WeaponForm.Knife,1,true)=>ArenaText.Pair("甩出的敌人撞到别人后，自动钩住被撞者；每次出钩限连 1 次。","A flung enemy that crashes into another hooks the one it hit; once per manual hook."),
                (WeaponForm.Knife,1,false)=>ArenaText.Pair($"双向拉扯时不会被打断、不受攻击伤害；会合时把 {u.hookShockRadius:0.#} 格内的其他敌人震退 {u.hookShockPush:0.#} 格；空钩打中墙时把你拉到墙边。",
                    $"During the two-way pull nothing interrupts you and attacks cannot hurt you; as you meet, other enemies within {u.hookShockRadius:0.#} cells are knocked back {u.hookShockPush:0.#} cells. An empty hook that hits a wall pulls you to it."),
                (WeaponForm.RocketLauncher,0,true)=>ArenaText.Pair($"炸死的敌人原地再炸一次：半径 {u.strikeChainRadius:0.#}、伤害 {P(u.strikeChainShare)}%，不再连锁。",$"Enemies the strike kills explode where they fall: radius {u.strikeChainRadius:0.#}, {P(u.strikeChainShare)}% damage, no further chain."),
                (WeaponForm.RocketLauncher,0,false)=>ArenaText.Pair($"自己的爆炸不再伤你，改为把你弹开 {u.strikeJumpDistance:0.#} 格，不会撞墙受伤。",$"Your own blast no longer hurts you; it pushes you {u.strikeJumpDistance:0.#} cells instead, with no wall damage."),
                (WeaponForm.RocketLauncher,1,true)=>ArenaText.Pair($"撞到的敌人沿飞行方向击退，导弹飞得越快推得越远：刚起飞时 {u.rocketCueMax:0.#} 格，飞到尽头 {u.rocketCueMax*Mathf.Clamp(u.rocketEndSpeedShare,.1f,1f):0.#} 格。",$"Enemies you hit are knocked along your flight, farther the faster you still fly: {u.rocketCueMax:0.#} cells at launch, {u.rocketCueMax*Mathf.Clamp(u.rocketEndSpeedShare,.1f,1f):0.#} at the end."),
                (WeaponForm.RocketLauncher,1,false)=>ArenaText.Pair("飞行中不会被黑洞拉偏或吞掉；撞墙不再扣你的血，这一飞的冷却立刻清零。","In flight black holes neither drag nor swallow you; hitting a wall no longer costs you HP and resets this flight's cooldown."),
                _=>ArenaText.Pair("给这把武器的攻击方式加一个变招","Adds a twist to this weapon's attack")
            };
        }
        bool CanReinfuse(RunWeapon w)=>!w.builtInElement&&OwnElement(w)!=ElementKind.None&&(AxisAllowed==null||AxisAllowed(UpgradeAxis.Infusion));
        /// <summary>V2 day campaign: the weapon card directions the base rooms allow (Tame and Twist: Armament Bay 2 and 3; Infusion: Energy
        /// Lab 3). Null in the classic run: all.</summary>
        public static Func<UpgradeAxis,bool> AxisAllowed;
        /// <summary>V2 day campaign: what the air drop may hold (new weapons from Armament Bay 2, Mk upgrades from level 1). Null: both.</summary>
        public static Func<(bool newWeapons,bool upgrades)> ArmoryRule;
        public bool HasOffers=>Offers!=null&&Array.Exists(Offers,offer=>offer!=null);
        // V4: draft cards change behaviour and element only; Power, Tempo and Reach growth moved to armory Mk tiers.
        static readonly UpgradeAxis[] HandDraftAxes={UpgradeAxis.Infusion,UpgradeAxis.Twist,UpgradeAxis.Tame};
        static readonly UpgradeAxis[] AutoDraftAxes={UpgradeAxis.Infusion};
        static System.Collections.Generic.List<UpgradeAxis> AvailableAxes(RunWeapon w)
        {
            var axes=new System.Collections.Generic.List<UpgradeAxis>();
            // The Pistol takes no Infusion, Twist or Tame (since 2026-11-06), so no draw or base purchase ever picks it.
            if(!w.IsAnomalous)return axes;
            foreach(var axis in w.IsAuto?AutoDraftAxes:HandDraftAxes)
                if(!w.Has(axis)&&(axis!=UpgradeAxis.Infusion||!w.builtInElement)&&(AxisAllowed==null||AxisAllowed(axis)))axes.Add(axis);
            // Document 5.7: the Drain takes Infusion too; each block releases the element (EnergyShieldBlocks).
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
                    // The battlefield owns the suspended references; trial input gets separate runtime state.
                    w.hooked=w.whirled=null;w.channelledField=w.recallActor=w.stickyActor=null;w.recallPlanted=false;w.hookBusy=w.inFlight=w.charging=false;w.heldTime=0;w.burstLeft=0;
                    w.hookChainUsed=false;w.vacuumTimer=0;
                    if(w.form!=WeaponForm.ArcCoil||w.variant!=1)w.charge=0;
                    // The Execute's armed shot and the Arc Jump's stored jumps (and their speed-up) stay on the battlefield.
                    w.executeNextShot=0f;w.chainStoredJumps=0;
                    ResetPlacedShots(w);
                }
                FreezeExecuteMarks(true);
                inputHeld=false;lastPosition=player.Position;
                return;
            }
            if(trialWeaponStates.Count==0)return;
            // Portals require Normal state, so no battlefield dash is suspended. An unfinished trial dash must stop here.
            dashLeft=0;dashWeapon=null;dashContactDamage=0;dashHits.Clear();
            if(player&&player.State==CharacterState.Dashing)player.SetState(CharacterState.Normal);
            // WorldMap has already reactivated the battlefield, so use the captured set rather than its current flag.
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
                // Trial leftovers go; the deployed minigun picks up where it was.
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
        // Void Walk: when it runs out, both hand weapons carry Void for a while, then get their own element back.
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
        /// <summary>Gives each enchanted weapon its own element back; one whose element a card changed meanwhile keeps the new one.</summary>
        void EndVoidEnchant()
        {
            voidEnchantLeft=0f;
            for(int i=0;i<voidEnchanted.Length;i++){var w=voidEnchanted[i];if(w!=null&&w.element==ElementKind.Void)w.element=voidEnchantFrom[i];voidEnchanted[i]=null;}
        }
        /// <summary>The element w gets back after a running Void enchant, else its element now.</summary>
        ElementKind OwnElement(RunWeapon w){for(int i=0;i<voidEnchanted.Length;i++)if(voidEnchantLeft>0f&&voidEnchanted[i]==w)return voidEnchantFrom[i];return w.element;}
        /// <summary>An Infusion taken during the enchant waits under the Void and takes over when the enchant ends.</summary>
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
        /// <summary>Lance trail, deployed Spin-up minigun and unplaced Bombard Twist blasts end with the wave or the battlefield.</summary>
        void ResetPlacedShots(RunWeapon w)
        {
            RemoveLanceTrailTracer(w);w.lanceTrailLeft=0f;w.executeAuraLeft=0f;
            w.turretLeft=w.turretClock=0f;w.bombardLeft=0;w.bombardTimer=0f;
        }
        /// <summary>A throwing knife lost to a black hole is gone for a moment, not for the run: it is back in its slot after
        /// knifeHoleReturn seconds, shown as a cooldown.</summary>
        public void KnifeLostToHole(RunWeapon w)
        {
            if(w==null)return;
            float wait=Mathf.Max(0f,upgrades.knifeHoleReturn);
            w.inFlight=false;w.cooldown=Mathf.Max(w.cooldown,wait);w.holeReturnSpan=wait;
        }
    }
}
