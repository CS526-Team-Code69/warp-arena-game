using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Gun Reverse Shot")]
    public class GunReverseShotEffect : WeaponEffect
    {
        public float damage = 10f;
        public float bulletSpeed = 25f;
        public float bulletRange = 40f;

        [Tooltip(
            "How far each shot's recoil pushes the shooter (stacks per shot; no wall damage, but can push you into a gap)")]
        public float recoilDistance = 0.8f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            Vector3 aim = user.AimDirection;
            Vector3 start = user.Position - aim * (user.Radius + 0.3f);
            var shot = new Bullet.Shot
            {
                damage = damage,
                speed = bulletSpeed,
                range = bulletRange,
                radius = Bullet.DefaultRadius,
                visualScale = 1f,
            };
            new GameObject("Bullet").AddComponent<Bullet>().Launch(shot, user, start, -aim);
            user.Push(aim, recoilDistance);
        }
    }
}