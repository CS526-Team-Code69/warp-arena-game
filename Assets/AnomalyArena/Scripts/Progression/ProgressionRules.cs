using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Progression Rules")]
    public sealed class ProgressionRules : ScriptableObject
    {
        [Tooltip("Editable base player stats and caps; cloned per run so gameplay never writes back into this asset.")]
        public RunStats startingStats = new();
        [Tooltip("GDD §7.1. Candidate curve, explicit to keep the documented rounding.")]
        public int[] levelCosts = { 10, 16, 27, 45, 74, 122, 202, 333, 549 };
        public int maxLevel = 10;
        public int xpPerStatDraft = 549;
        public float energyMax = 100f;
        [Header("Smooth level growth; stat cards add separately.")]
        [Min(0f), Tooltip("Max HP gained per level. Candidate 6: 25 at level 1 to 79 at level 10.")] public float maxHpPerLevel = 6f;
        [Min(0f), Tooltip("Max energy gained per level. Candidate 8: 100 at level 1 to 172 at level 10; energy cards remain additive.")] public float maxEnergyPerLevel = 8f;
        public float voidEnergyRegeneration = 4f;
        public float regenerationInterval = 5f;
        public int epicPityCount = 8;
        public float rareBasePercent = 25f;
        public float rareCapPercent = 55f;
        public float epicBasePercent = 5f;
        public float epicCapPercent = 20f;
        [Range(0f, 1f), Tooltip("Legendary share within high-tier stat cards. 0.2 times 5 percent gives a 1 percent base rate; blue and orange share pity.")] public float legendaryShareOfHighTier = .2f;
        [Min(1f), Tooltip("Legendary stat-card multiplier relative to the former high tier; other tiers are unchanged.")] public float legendaryStatMultiplier = 1.2f;

        [Tooltip("Rows follow StatId: white Common / green Rare / blue Exceptional. Orange uses the blue value × legendaryStatMultiplier. "
            + "V4 merges small stats into a bonus on a stronger card and retires the rest; see work/balance/v4_numbers.py.")]
        public StatCardValues[] statCards =
        {
            new(StatId.MaxHp, 15f, 25f, 40f, StatBonus.Regeneration, 1f, 1.5f, 2.5f), new(StatId.Regeneration, 1f, 1.5f, 2.5f, retired: true),
            new(StatId.MoveSpeed, 0.08f, 0.12f, 0.16f, StatBonus.Stamina, .2f, .3f, .45f), new(StatId.Stamina, 0.15f, 0.25f, 0.35f, retired: true),
            new(StatId.Damage, 0.12f, 0.18f, 0.25f, StatBonus.CritChance, .08f, .12f, .16f), new(StatId.AttackSpeed, 0.15f, 0.22f, 0.32f),
            new(StatId.Knockback, 0.2f, 0.35f, 0.5f, StatBonus.Impact, 3f, 5f, 8f), new(StatId.Impact, 3f, 5f, 8f, retired: true),
            new(StatId.PickupRange, 1.5f, 2.5f, 4f, retired: true), new(StatId.Protection, 25f, 40f, 60f),
            new(StatId.Luck, 4f, 7f, 10f, retired: true), new(StatId.MaxEnergy, 25f, 40f, 60f, StatBonus.EnergySiphon, .4f, .7f, 1f),
            new(StatId.EnergySiphon, 0.3f, 0.6f, 0.9f, retired: true), new(StatId.ElementPenetration, 0.20f, 0.35f, 0.50f)
        };

        public CardRarity StatRarityFromRoll(float percentRoll, float luck, bool highTierPity)
        {
            float high = Mathf.Clamp(epicBasePercent + luck * .5f, 0f, Mathf.Min(100f, epicCapPercent));
            float rare = Mathf.Clamp(rareBasePercent + luck, 0f, Mathf.Min(100f - high, rareCapPercent));
            if (highTierPity || percentRoll < high)
            {

                float highRoll = highTierPity ? percentRoll / 100f : percentRoll / high;
                return highRoll < Mathf.Clamp01(legendaryShareOfHighTier) ? CardRarity.Legendary : CardRarity.Epic;
            }
            return percentRoll < high + rare ? CardRarity.Rare : CardRarity.Common;
        }
    }

    [System.Serializable]
    public sealed class StatCardValues
    {
        public StatId stat;
        public float common, rare, epic;
        [Tooltip("Second effect granted by the same card, at the same rarity.")]
        public StatBonus bonus;
        public float bonusCommon, bonusRare, bonusEpic;
        [Tooltip("Retired rows stay for serialized IDs and old saves but are never offered.")]
        public bool retired;
        public StatCardValues(StatId stat, float common, float rare, float epic, StatBonus bonus = StatBonus.None,
            float bonusCommon = 0f, float bonusRare = 0f, float bonusEpic = 0f, bool retired = false)
        {
            this.stat = stat; this.common = common; this.rare = rare; this.epic = epic;
            this.bonus = bonus; this.bonusCommon = bonusCommon; this.bonusRare = bonusRare; this.bonusEpic = bonusEpic; this.retired = retired;
        }
        public float At(CardRarity rarity) => rarity switch
        { CardRarity.Rare => rare, CardRarity.Epic or CardRarity.Legendary => epic, _ => common };
        public float BonusAt(CardRarity rarity) => rarity switch
        { CardRarity.Rare => bonusRare, CardRarity.Epic or CardRarity.Legendary => bonusEpic, _ => bonusCommon };
    }
}
