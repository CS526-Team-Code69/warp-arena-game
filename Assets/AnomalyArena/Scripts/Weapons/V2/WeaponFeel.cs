using System;
using UnityEngine;

namespace AnomalyArena
{

    public enum HitFeel { Auto, Melee, HeavyShot, LightShot, Area }

    [Serializable]
    public class HitFeelProfile
    {
        [Min(0f), Tooltip("Seconds the struck enemy holds its pose on a normal hit (0 = no hold).")] public float victimHold;
        [Min(0f), Tooltip("Pose hold on a heavy or critical hit.")] public float victimHeavyHold;
        [Min(0f), Tooltip("How far the struck model is jolted along the hit direction (units); it springs back by itself.")] public float victimJolt;
        [Min(0f), Tooltip("Seconds the attacker's own animation holds when this lands (melee 'bite'); movement is never held.")] public float attackerHold;
        [Min(0f), Tooltip("Camera push toward the hit direction on a normal hit (0 = none).")] public float hitKick;
        [Min(0f), Tooltip("Camera push on a heavy or critical hit.")] public float heavyHitKick;
        [Min(0f), Tooltip("Shard count multiplier for the hit splash.")] public float splash = 1f;
        [Header("When the player fires")]
        [Tooltip("Model offset on firing, against the aim (negative = lunge forward).")] public float fireRecoil;
        [Min(0f), Tooltip("Camera push against the aim on firing.")] public float fireKick;
        [Min(0f), Tooltip("Muzzle flash length on firing (0 = none).")] public float muzzleFlash;
    }

    public static class WeaponFeel
    {
        public static HitFeel Of(RunWeapon weapon) => weapon == null ? HitFeel.Auto : Of(weapon.form, weapon.variant);

        public static HitFeel Of(WeaponForm form, int variant) => form switch
        {
            WeaponForm.Knife or WeaponForm.PhaseDagger or WeaponForm.OrbitBlades or WeaponForm.ShieldRing => HitFeel.Melee,
            WeaponForm.RocketLauncher => variant == 1 ? HitFeel.Melee : HitFeel.Area,                                                     
            WeaponForm.Rifle or WeaponForm.Railgun => HitFeel.HeavyShot,
            WeaponForm.CryoCannon => variant == 0 ? HitFeel.HeavyShot : HitFeel.Area,                                  
            WeaponForm.Minigun or WeaponForm.SentryDrone => HitFeel.LightShot,
            WeaponForm.ScatterCannon => variant == 0 ? HitFeel.LightShot : HitFeel.HeavyShot,                              
            WeaponForm.SporeLauncher => variant == 1 ? HitFeel.LightShot : HitFeel.Area,                  
            _ => HitFeel.Area,                                                                    
        };

        public static HitFeel FromKind(HitKind kind) => kind switch
        {
            HitKind.Bullet => HitFeel.LightShot,
            HitKind.Pierce => HitFeel.HeavyShot,
            HitKind.Slam or HitKind.Blunt => HitFeel.Melee,
            _ => HitFeel.Area,
        };
    }
}
