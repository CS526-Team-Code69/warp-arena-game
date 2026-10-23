using System;
using UnityEngine;

namespace AnomalyArena
{
    public enum WeaponForm { Rifle, Knife, RocketLauncher, GravGun, ScatterCannon, PhaseDagger, PlasmaLauncher, Flamethrower, CryoCannon, ArcCoil, SporeLauncher, Railgun, Minigun, SwarmLauncher, OrbitBlades, SentryDrone, ShieldRing }
    [Flags] public enum UpgradeAxis { None=0, Infusion=1, Power=2, Tempo=4, Reach=8, Twist=16, Tame=32 }

    [Serializable]
    public sealed class RunWeapon
    {
        public WeaponForm form;
        public int variant;
        public bool revealed;
        public UpgradeAxis axes;
        [Tooltip("V4 armory tier; damage × WeaponRoster.MkDamage(mk). Draft cards no longer change numbers.")]
        public int mk = 1;

        [NonSerialized] public RunWeapon upgradeOf;
        public ElementKind element;
        public bool builtInElement;
        [NonSerialized] public float cooldown, reload, heldTime, heat, charge, chargeFullTime;

        [NonSerialized] public float executeBonus;
        [NonSerialized] public int rounds;

        [NonSerialized] public bool jammed;

        [NonSerialized] public int burstLeft;
        [NonSerialized] public float burstTimer, burstDamage;
        [NonSerialized] public Vector3 burstDirection;

        [NonSerialized] public int needlesFired;
        [NonSerialized] public bool charging, inFlight, recallPlanted;
        [NonSerialized] public Vector3 recallPoint;
        [NonSerialized] public Combatant hooked;
        [NonSerialized] public Combatant whirled;
        [NonSerialized] public bool hookBusy;
        [NonSerialized] public AttackActor channelledField;
        [NonSerialized] public AttackActor recallActor;
        [NonSerialized] public AttackActor stickyActor;

        [NonSerialized] public Vector3 lanceTrailFrom, lanceTrailTo;
        [NonSerialized] public float lanceTrailLeft;

        [NonSerialized] public float executeHpGained;

        [NonSerialized] public float turretLeft, turretClock;
        [NonSerialized] public Vector3 turretPoint, turretDir;

        [NonSerialized] public int bombardLeft;
        [NonSerialized] public float bombardTimer;
        [NonSerialized] public Vector3 bombardOrigin;

        [NonSerialized] public bool bombardRefunded;

        [NonSerialized] public Vector3[] trailPoints;
        [NonSerialized] public float[] trailTimes;
        [NonSerialized] public int trailHead, trailCount;

        [NonSerialized] public int chainStoredJumps;
        [NonSerialized] public float whirlLeft, orbitAngle, leechDamage;
        [NonSerialized] public float tankReadyAt;

        [NonSerialized] public float holeReturnSpan;

        [NonSerialized] public bool knifeCaught;

        [NonSerialized] public bool hookChainUsed;

        [NonSerialized] public float vacuumTimer;
        [NonSerialized] public Vector3 vacuumDirection;

        [NonSerialized] public float reloadTotal;
        [NonSerialized] public bool quickReloadTried;
        public bool IsAuto => (int)form >= (int)WeaponForm.OrbitBlades;
        public bool Has(UpgradeAxis axis) => (axes & axis) != 0;
        public int Level { get { int n=1; for(int i=0;i<6;i++) if(Has((UpgradeAxis)(1<<i))) n++; return n; } }
        public float Power => Has(UpgradeAxis.Power) ? 1.4f : 1f;
        public float Tempo => Has(UpgradeAxis.Tempo) ? 0.7f : 1f;
        public float Reach => Has(UpgradeAxis.Reach) ? 1.4f : 1f;
        public string FormName => ArenaTerms.WeaponForm(form);
        public string MkLabel => "Mk "+Mathf.Max(1,mk);
        public string Label => (revealed ? FormName+" · "+ArenaTerms.WeaponAnomaly(form,variant) : FormName+" ?")+(mk>1?" · "+MkLabel:"");
    }
}
