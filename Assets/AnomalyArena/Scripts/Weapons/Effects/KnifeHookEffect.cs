using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Effects/Knife Hook")]
    public class KnifeHookEffect : WeaponEffect
    {
        public float range = 10f;
        public float extendSpeed = 40f;
        public float retractSpeed = 60f;
        public float pullSpeed = 25f;
        public float throwDistance = 8f;
        public float hookRadius = 0.15f;

        [Header("Crash of the flung enemy into another one")]
        [Tooltip(
            "Damage when a small enemy crashes at full launch speed; it falls with the square of the speed left (kinetic energy)")]
        public float impactDamage = 12f;

        [Tooltip("How strongly the flung enemy's size raises damage and push: 1 = twice the radius, twice the effect")]
        public float impactSizeExponent = 1f;

        [Tooltip("How far beyond the flung enemy's edge the crash reaches")]
        public float impactRadius = 1.5f;

        [Tooltip(
            "How far a small enemy crashing at full launch speed pushes what it hits; it falls linearly with the speed left")]
        public float impactKnockback = 5f;

        public override void Use(Weapon weapon, IWeaponHolder user)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(go.GetComponent<Collider>());
            go.name = "Hook";
            go.GetComponent<Renderer>().sharedMaterial = GameManager.Instance.Mat(new Color(0.82f, 0.88f, 0.94f));
            go.AddComponent<Hook>().Begin(this, weapon, user);
        }

        public ThrowImpact Impact(IDamageDealer dealer) => new ThrowImpact
        {
            dealer = dealer,
            damage = impactDamage,
            sizeExponent = impactSizeExponent,
            radius = impactRadius,
            knockback = impactKnockback,
        };
    }
}