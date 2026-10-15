using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Missile Strike")]
    public class MissileStrikeEffect : WeaponEffect
    {
        [Tooltip("Seconds between marking the point and the rocket landing on it")]
        public float strikeDelay = 1f;

        [Tooltip(
            "Furthest point from the user that can be marked; a cursor further away is pulled in to this distance")]
        public float maxRange = 20f;

        public float explosionRadius = 3f;
        public float damage = 10f;
        public float knockback = 5f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = new GameObject("RocketStrike");
            go.AddComponent<RocketStrike>().Launch(this, user, StrikePoint(user.Position, user.AimPoint));
        }

        public Vector3 StrikePoint(Vector3 from, Vector3 aimPoint)
        {
            Vector3 offset = Vector3.ClampMagnitude(Query.Flat(aimPoint - from), maxRange);
            return Query.Flat(from) + offset;
        }
    }
}