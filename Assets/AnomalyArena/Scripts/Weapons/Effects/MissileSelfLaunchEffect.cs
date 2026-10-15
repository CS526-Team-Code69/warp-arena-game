using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Missile Self Launch")]
    public class MissileSelfLaunchEffect : WeaponEffect
    {
        public float distance = 15f;
        public float speed = 25f;
        [Tooltip("Distance bounced back after hitting a wall (the bounce itself deals no further wall damage)")] public float wallBounce = 3f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = new GameObject("SelfLaunch");
            go.AddComponent<SelfLaunch>().Begin(this, weapon, user);
        }
    }
}
