using UnityEngine;

namespace AnomalyArena
{

    public sealed class OrbPool : MonoBehaviour
    {
        [field: SerializeField, Tooltip("Preplaced pickups, normally 256. No runtime object creation.")]
        public ExperienceOrb[] pool { get; set; }
        [field: SerializeField] public Combatant player { get; set; }
        [field: SerializeField] public RunProgression progression { get; set; }
        [field: SerializeField] public Material xpMaterial { get; set; }
        [field: SerializeField] public Material hpMaterial { get; set; }
        [field: SerializeField] public Texture2D xpTexture { get; set; }
        [field: SerializeField] public Texture2D hpTexture { get; set; }
        [field: SerializeField, Tooltip("Healing per health-orb drop. Candidate value 3; tuned independently from drop probability.")]
        public float hpPerOrb { get; set; } = 10f;
        [field: SerializeField, Range(0f,1f), Tooltip("Health-orb drop chance for enhanced enemies. Candidate 15 percent, saved per scene.")]
        public float eliteHpDropChance { get; set; } = .15f;
        [field: SerializeField, Tooltip("Candidate pickup flight speed in units/s.")]
        public float magnetSpeed { get; set; } = 14f;
        [field: SerializeField, Min(0f), Tooltip("Base XP attraction radius. Pickup-range cards add distance; health orbs retain their range.")]
        public float xpMagnetBaseRadius { get; set; } = 2f;
        [field: SerializeField, Min(.05f), Tooltip("Collect an XP or health orb within this distance of the player's center.")]
        public float collectionRadius { get; set; } = 1f;
        [SerializeField] int pressurePickupRevision;
        public int PressurePickupRevision => pressurePickupRevision;
        public void MarkPressurePickupApplied() => pressurePickupRevision = 1;
        [field: SerializeField] public float repelDistance { get; set; } = 3f;
        public static OrbPool Instance { get; private set; }
        bool frozenForTrial;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Drop(Vector3 position, float xpValue, bool dropHp = false)
        {
            if (xpValue > 0f) SpawnOrMerge(position, OrbKind.Xp, xpValue);
            if (dropHp) SpawnOrMerge(position + new Vector3(0.45f, 0f, 0f), OrbKind.Hp, hpPerOrb);
        }

        bool SpawnOrMerge(Vector3 position, OrbKind kind, float value)
        {
            if (pool == null) return false;
            ExperienceOrb nearest = null;
            float distance = float.MaxValue;
            foreach (var orb in pool)
            {
                if (!orb) continue;
                if (!orb.InUse)
                {
                    orb.Begin(position, kind, value, kind == OrbKind.Xp ? xpMaterial : hpMaterial,
                        kind == OrbKind.Xp ? xpTexture : hpTexture); return true;
                }
                if (orb.Kind != kind) continue;
                float d = Query.Flat(orb.Position - position).sqrMagnitude;
                if (d < distance) { nearest = orb; distance = d; }
            }
            if (!nearest) return false;
            nearest.Merge(value); return true;
        }

        public void Tick(float dt)
        {
            if (frozenForTrial || pool == null || !player || !progression) return;
            var game = GameManager.Instance;
            if (game) dt = game.WorldDeltaTime;
            float baseRange = progression.Rules ? progression.Rules.startingStats.pickupRange : 2f;
            float xpRange = Mathf.Max(0f, xpMagnetBaseRadius + progression.Stats.pickupRange - baseRange);
            foreach (var orb in pool) if (orb && orb.InUse) orb.Tick(dt, player, progression, orb.Kind == OrbKind.Xp ? xpRange : progression.Stats.pickupRange, magnetSpeed);
        }

        public void CollectAll()
        {
            if (pool == null) return;
            foreach (var orb in pool) if (orb && orb.InUse) orb.Collect(player, progression);
        }

        public void Repel(Vector3 center, float radius)
        {
            if (pool == null) return;
            foreach (var orb in pool)
                if (orb && orb.InUse && Query.Flat(orb.Position - center).sqrMagnitude <= radius * radius)
                    orb.Repel(center, repelDistance);
        }

        public void Clear()
        {
            frozenForTrial = false;
            if (pool == null) return;
            foreach (var orb in pool) if (orb && orb.InUse) orb.Release();
        }
        public void FreezeForTrial(bool frozen) => frozenForTrial = frozen;
    }
}
