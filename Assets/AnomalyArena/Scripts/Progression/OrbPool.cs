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
        [field: SerializeField, Tooltip("Energy orb material; falls back to xpMaterial when empty.")]
        public Material energyMaterial { get; set; }
        [field: SerializeField] public Texture2D xpTexture { get; set; }
        [field: SerializeField] public Texture2D hpTexture { get; set; }
        [field: SerializeField, Tooltip("Energy orb texture; falls back to xpTexture when empty.")]
        public Texture2D energyTexture { get; set; }
        [field: SerializeField, Tooltip("HP restored by one dropped HP orb; tuned separately from the drop chance.")]
        public float hpPerOrb { get; set; } = 10f;
        [field: SerializeField, Range(0f,1f), Tooltip("Chance that an elite drops an HP orb. Candidate 15%, saved in the scene.")]
        public float eliteHpDropChance { get; set; } = .15f;
        [field: SerializeField, Min(0f), Tooltip("Energy restored by one dropped energy orb. Candidate 25.")]
        public float energyPerOrb { get; set; } = 25f;
        [field: SerializeField, Range(0f,1f), Tooltip("Chance that an elite drops an energy orb. Candidate 10%.")]
        public float eliteEnergyDropChance { get; set; } = .1f;
        [field: SerializeField, Tooltip("Candidate pickup flight speed in units/s.")]
        public float magnetSpeed { get; set; } = 14f;
        [field: SerializeField, Min(0f), Tooltip("Base XP magnet radius; run pickup-range gains add on top. HP and energy orbs use the run pickup range.")]
        public float xpMagnetBaseRadius { get; set; } = 2f;
        [field: SerializeField, Min(.05f), Tooltip("An orb is collected once it is this close to the player's centre.")]
        public float collectionRadius { get; set; } = 1f;
        [SerializeField] int pressurePickupRevision;
        public int PressurePickupRevision => pressurePickupRevision;
        public void MarkPressurePickupApplied() => pressurePickupRevision = 1;
        [field: SerializeField] public float repelDistance { get; set; } = 3f;
        public static OrbPool Instance { get; private set; }
        bool frozenForTrial;

        void Awake() => Instance = this;
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void Drop(Vector3 position, float xpValue, bool dropHp = false, bool dropEnergy = false)
        {
            if (xpValue > 0f) SpawnOrMerge(position, OrbKind.Xp, xpValue);
            if (dropHp) SpawnOrMerge(position + new Vector3(0.45f, 0f, 0f), OrbKind.Hp, hpPerOrb);
            if (dropEnergy && progression && progression.EnergyUnlocked)
                SpawnOrMerge(position + new Vector3(-0.45f, 0f, 0f), OrbKind.Energy, energyPerOrb);
        }

        bool SpawnOrMerge(Vector3 position, OrbKind kind, float value)
        {
            if (pool == null) return false;
            Material material = kind switch { OrbKind.Hp => hpMaterial, OrbKind.Energy when energyMaterial != null => energyMaterial, _ => xpMaterial };
            Texture2D texture = kind switch { OrbKind.Hp => hpTexture, OrbKind.Energy when energyTexture != null => energyTexture, _ => xpTexture };
            ExperienceOrb nearest = null;
            float distance = float.MaxValue;
            foreach (var orb in pool)
            {
                if (!orb) continue;
                if (!orb.InUse)
                {
                    orb.Begin(position, kind, value, material, texture); return true;
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
