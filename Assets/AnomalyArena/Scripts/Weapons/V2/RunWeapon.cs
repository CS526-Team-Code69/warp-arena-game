using System;
using UnityEngine;

namespace AnomalyArena
{
    public enum WeaponForm { Rifle, Knife, RocketLauncher, GravGun, ScatterCannon, PhaseDagger, PlasmaLauncher, Flamethrower, CryoCannon, ArcCoil, SporeLauncher, Railgun, Minigun, SwarmLauncher, OrbitBlades, SentryDrone, ShieldRing, Pistol }
    // Pistol (since 2026-11-06, appended so no saved form number moves): the V2 campaign's starting sidearm, plain single shots, no anomaly, no upgrades.
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
        /// <summary>Set on an armory offer that is a stronger version of a weapon the player holds: a new weapon of that form which
        /// replaces it when taken (RunWeapons.TakeOffer clears this then).</summary>
        [NonSerialized] public RunWeapon upgradeOf;
        public ElementKind element;
        public bool builtInElement;
        [NonSerialized] public float cooldown, reload, heldTime, heat, charge, chargeFullTime;
        /// <summary>Execute: damage bonus of the next shot, set by a shot that killed and spent by the next one.</summary>
        [NonSerialized] public float executeNextShot;
        [NonSerialized] public int rounds;
        /// <summary>Scatter Focus burst: pellets still to fire, seconds to the next one, damage per pellet and the aim locked at release.</summary>
        [NonSerialized] public int burstLeft;
        [NonSerialized] public float burstTimer, burstDamage;
        [NonSerialized] public Vector3 burstDirection;
        [NonSerialized] public bool charging, inFlight, recallPlanted;
        [NonSerialized] public Vector3 recallPoint;
        [NonSerialized] public Combatant hooked;
        [NonSerialized] public Combatant whirled;
        [NonSerialized] public bool hookBusy;
        [NonSerialized] public AttackActor channelledField;
        [NonSerialized] public AttackActor recallActor;
        [NonSerialized] public AttackActor stickyActor;
        /// <summary>Lance Twist: the last slug's path on the ground and the seconds it stays; the next slug bursts where it crosses it.</summary>
        [NonSerialized] public Vector3 lanceTrailFrom, lanceTrailTo;
        [NonSerialized] public float lanceTrailLeft;
        /// <summary>Execute Tame: seconds left of the arc shield around you, time to its next shock and the damage of each.</summary>
        [NonSerialized] public float executeAuraLeft, executeAuraClock, executeAuraDamage;
        /// <summary>Spin-up Twist: seconds the deployed minigun keeps firing, where it stands, the way it faces and the time to its next shot.</summary>
        [NonSerialized] public float turretLeft, turretClock;
        [NonSerialized] public Vector3 turretPoint, turretDir;
        /// <summary>Bombard Twist: blasts still to place, seconds to the next one and where you stood when you fired.</summary>
        [NonSerialized] public int bombardLeft;
        [NonSerialized] public float bombardTimer;
        [NonSerialized] public Vector3 bombardOrigin;
        /// <summary>Bombard Tame: this shot already shortened its cooldown when you touched one of its blasts.</summary>
        [NonSerialized] public bool bombardRefunded;
        /// <summary>Capacitor Twist: recent path points (a ring buffer) and the game time each was recorded.</summary>
        [NonSerialized] public Vector3[] trailPoints;
        [NonSerialized] public float[] trailTimes;
        [NonSerialized] public int trailHead, trailCount;
        /// <summary>Chain Tame: unused jumps kept for the next discharge.</summary>
        [NonSerialized] public int chainStoredJumps;
        [NonSerialized] public float whirlLeft, orbitAngle;
        [NonSerialized] public float tankReadyAt;
        /// <summary>Gunner: rounds fired since the magazine was last full; while it lies on the ground cooling down,
        /// where it lies and the seconds before it flies back by itself (0 = flying).</summary>
        [NonSerialized] public int droneShots;
        [NonSerialized] public Vector3 dronePoint;
        [NonSerialized] public float droneDownLeft;
        /// <summary>Whirl: where the dragged enemy left from and lands (your afterimage), and the throw's length in seconds.</summary>
        [NonSerialized] public Vector3 whirlFrom, whirlTo;
        [NonSerialized] public float whirlTotal;
        /// <summary>Drain: the Energy Shield is up; seconds since the last enemy attack; seconds left of the overload
        /// after it broke, and whether that overload already cost Energy.</summary>
        [NonSerialized] public bool drainShieldUp, drainOverloadPaid;
        [NonSerialized] public float drainQuiet, drainOverloadLeft;
        /// <summary>Throwing Knife lost to a black hole: the whole wait before it is back in its slot, so the slot shows it as a cooldown.</summary>
        [NonSerialized] public float holeReturnSpan;
        /// <summary>Flying Blade Tame: a return swept through you (a catch), so the next throw bounces to more targets and flies faster once.</summary>
        [NonSerialized] public bool knifeCaught;
        /// <summary>Hook Twist: this manual hook already chained once; the next manual hook resets it.</summary>
        [NonSerialized] public bool hookChainUsed;
        /// <summary>Scatter Blast Twist: seconds until the pulled-in crowd is blasted, and the aim locked when the round was fired.</summary>
        [NonSerialized] public float vacuumTimer;
        [NonSerialized] public Vector3 vacuumDirection;
        public bool IsAuto => IsAutoForm(form);
        /// <summary>Every weapon but the V2 campaign's starting Pistol is an anomaly weapon (its attack and trait are learned by using it).</summary>
        public bool IsAnomalous => IsAnomalousForm(form);
        /// <summary>The three auto weapons, by name, so a form added after them (the Pistol) stays a hand weapon.</summary>
        public static bool IsAutoForm(WeaponForm form) => form is WeaponForm.OrbitBlades or WeaponForm.SentryDrone or WeaponForm.ShieldRing;
        /// <summary>Since 2026-11-06: the armory and the base research offer only anomaly weapons, and only they take Mk upgrades, Infusion,
        /// Twist and Tame. The Pistol is none of these: plain shots and no upgrades.</summary>
        public static bool IsAnomalousForm(WeaponForm form) => form != WeaponForm.Pistol;
        public bool Has(UpgradeAxis axis) => (axes & axis) != 0;
        public int Level { get { int n=1; for(int i=0;i<6;i++) if(Has((UpgradeAxis)(1<<i))) n++; return n; } }
        public float Power => Has(UpgradeAxis.Power) ? 1.4f : 1f;
        public float Tempo => Has(UpgradeAxis.Tempo) ? 0.7f : 1f;
        public float Reach => Has(UpgradeAxis.Reach) ? 1.4f : 1f;
        public string FormName => ArenaTerms.WeaponForm(form);
        public string MkLabel => "Mk "+Mathf.Max(1,mk);
        // The Pistol has no anomaly to reveal: its label is its name alone, never "?" (since 2026-11-06).
        public string Label => (!IsAnomalous ? FormName : revealed ? FormName+" · "+ArenaTerms.WeaponAnomaly(form,variant) : FormName+" ?")+(mk>1?" · "+MkLabel:"");
    }
}
