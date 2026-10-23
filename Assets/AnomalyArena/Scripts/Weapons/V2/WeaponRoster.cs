using System;
using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName="Anomaly Arena/V2 Weapon Roster")]
    public sealed class WeaponRoster : ScriptableObject
    {
        [Serializable] public class Definition
        {
            public WeaponForm form;
            public int unlockWave=1;
            [Tooltip("Retired forms stay for saved data but are never offered by the armory.")]
            public bool retired;
            public float damageA=5, damageB=5, cooldownA=1, cooldownB=1, rangeA=10, rangeB=10;
            [TextArea] public string backfireA, backfireB;
        }
        public Definition[] definitions;
        [field: SerializeField, Min(0f), Tooltip("Uncharged rifle base damage. Full charge uses Rifle.damageA. Candidate 7/3 and full-charge 9 each yield about 10 DPS at the reference cycle.")]
        public float RifleMinimumChargeDamage { get; set; } = 6f;
        [field: SerializeField, HideInInspector] public int StarterBalanceRevision { get; set; }
        [Tooltip("V4 armory Mk damage multipliers, index 0 = Mk1. Derived from fodder EHP: M(k) = R(c)·h(c) / (h(1)·S(c)) at each tier window centre c; see Docs/tools/v4_numbers.py.")]
        public float[] mkDamage = { 1f, 1.4f, 1.65f, 1.9f, 2.1f, 2.3f };
        [Min(1), Tooltip("Every this many waves, the standard Mk of new weapons rises by one.")]
        public int mkWaveSpacing = 3;
        public int MaxMk => mkDamage != null && mkDamage.Length > 0 ? mkDamage.Length : 1;
        public float MkDamage(int mk) => mkDamage == null || mkDamage.Length == 0 ? 1f : mkDamage[Mathf.Clamp(mk - 1, 0, mkDamage.Length - 1)];

        public int ParMk(int wave) => Mathf.Clamp(1 + (Mathf.Max(1, wave) - 1) / Mathf.Max(1, mkWaveSpacing), 1, Mathf.Max(1, MaxMk - 1));
        public Definition Get(WeaponForm form) { foreach(var d in definitions) if(d.form==form) return d; return null; }
        public static int DefaultUnlockWave(WeaponForm form) => form switch
        {
            WeaponForm.Rifle or WeaponForm.Knife or WeaponForm.RocketLauncher => 1,
            WeaponForm.GravGun or WeaponForm.ScatterCannon => 2,
            WeaponForm.PhaseDagger or WeaponForm.Flamethrower or WeaponForm.CryoCannon or WeaponForm.ArcCoil or WeaponForm.SporeLauncher or WeaponForm.OrbitBlades or WeaponForm.SentryDrone or WeaponForm.ShieldRing => 3,
            WeaponForm.PlasmaLauncher => 4,
            WeaponForm.Railgun => 11,
            _ => 6
        };

        public static bool IsRetired(WeaponForm form) => form is WeaponForm.GravGun or WeaponForm.PhaseDagger or WeaponForm.PlasmaLauncher;
        public static string FormName(WeaponForm f) => f switch { WeaponForm.RocketLauncher=>"Rocket Launcher",WeaponForm.GravGun=>"Grav Gun",WeaponForm.ScatterCannon=>"Scatter Cannon",WeaponForm.PhaseDagger=>"Phase Dagger",WeaponForm.PlasmaLauncher=>"Plasma Launcher",WeaponForm.CryoCannon=>"Cryo Cannon",WeaponForm.ArcCoil=>"Arc Coil",WeaponForm.SporeLauncher=>"Venom Launcher",WeaponForm.SwarmLauncher=>"Swarm Launcher",WeaponForm.OrbitBlades=>"Orbit Blades",WeaponForm.SentryDrone=>"Sentry Drone",WeaponForm.ShieldRing=>"Shield Ring",_=>f.ToString()};
        static readonly string[] Names={"Charged Shot","Reverse Shot","Throwing Knife","Hook","Strike","Launch Yourself","Attract","Repel","Blast","Focus","Swap","Recall","Sticky","Bouncy","Stream","Wildfire","Shatter","Blizzard","Chain","Capacitor","Cloud","Needler","Lance","Execute","Spin-up","Meltdown","Barrage","Bombard","Closed Blade","Open Blade","Gunner","Whirl","Repulsor","Leech"};
        public static string AnomalyName(WeaponForm f,int variant)=>Names[(int)f*2+Mathf.Clamp(variant,0,1)];
        public void SetDefaults()
        {
            definitions=new Definition[17];
            float[] da={9,6.100529f,10,0,8,5,12,1.25f,18,6,1,36,2.5f,4,4,5,3};

            float[] db={1.8f,12,7,0,16,8,12,1,1.5f,15,2,24,3,6,4,5,1};
            float[] ca={.35f,.3f,1.5f,2,1,.6f,1.6f,.25f,1,.9f,3,2.2f,.25f,1.8f,.5f,1.2f,4};
            float[] cb={.12f,.2f,1.8f,2,1,1,1.8f,1.5f,.25f,1,.125f,1.2f,.1f,2.5f,.5f,5,1};
            float[] ra={40,12,20,4,5,12,12,6,14,8,10,30,14,9,2.5f,10,3};
            float[] rb={40,10,15,4,20,12,12,10,10,5,12,20,14,12.75f,2.5f,6,3};
            for(int i=0;i<17;i++) definitions[i]=new Definition{form=(WeaponForm)i,unlockWave=DefaultUnlockWave((WeaponForm)i),retired=IsRetired((WeaponForm)i),damageA=da[i],damageB=db[i],cooldownA=ca[i],cooldownB=cb[i],rangeA=ra[i],rangeB=rb[i]};
            RifleMinimumChargeDamage=7f/3f;StarterBalanceRevision=1;
        }
    }
}
