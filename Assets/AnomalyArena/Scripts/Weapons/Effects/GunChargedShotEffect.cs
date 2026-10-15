using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Charged Shot")]
    public class GunChargedShotEffect : WeaponEffect
    {
        [Header("Tap (no charge)")] public float minDamage = 6f;

        [Tooltip("How far a hit enemy is pushed along the shot direction")]
        public float minKnockback = 5f;

        [Header("Full charge")] public float maxDamage = 15f;
        public float maxKnockback = 10f;

        [Header("Shot")] public float shotSpeed = 40f;
        public float shotRange = 40f;

        [Tooltip("How close to the shot's path an enemy has to be to get hit")]
        public float shotRadius = 0.25f;

        [Tooltip("Size of a fully charged shot relative to a tap (visual only)")]
        public float maxVisualScale = 2f;

        [Header("Charging")] [Tooltip("Seconds to reach full charge")]
        public float maxChargeTime = 1f;

        [Tooltip("Movement speed multiplier while charging")]
        public float chargeMoveMultiplier = 0.4f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            new GameObject("ShotCharge").AddComponent<ShotCharge>().Begin(this, weapon, user);
        }

        public float DamageAt(float k) => Mathf.Round(Mathf.Lerp(minDamage, maxDamage, k));

        public void Fire(IWeaponHolder user, float k)
        {
            Vector3 aim = user.AimDirection;
            Vector3 muzzle = user.Position + aim * (user.Radius + 0.3f);
            var shot = new Bullet.Shot
            {
                damage = DamageAt(k),
                knockback = Mathf.Lerp(minKnockback, maxKnockback, k),
                speed = shotSpeed,
                range = shotRange,
                radius = shotRadius,
                pierce = true,
                visualScale = Mathf.Lerp(1f, maxVisualScale, k),
            };
            new GameObject("ChargedShot").AddComponent<Bullet>().Launch(shot, user, muzzle, aim);
            Fx.Pop(Query.AtCastHeight(muzzle), new Color(1f, 0.85f, 0.3f), 0.6f + k);
        }
    }
}