using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Progression Rules")]
    public sealed class ProgressionRules : ScriptableObject
    {
        [Tooltip("Editable base player stats and caps; cloned per run so gameplay never writes back into this asset.")]
        public RunStats startingStats = new();
        [Tooltip("XP needed to go from level n to n+1; one entry per level below maxLevel (maxLevel - 1 entries). "
            + "Placeholder ascending curve, to be replaced by the modelled curve.")]
        public int[] levelCosts = { 10, 16, 24, 34, 46, 60, 78, 100, 128, 160, 200 };
        [Min(1), Tooltip("Highest level. After it every full XP bar grants a stat-only draft.")]
        public int maxLevel = 12;
        [Min(1), Tooltip("XP per stat-only draft once the player is at maxLevel.")]
        public int xpPerStatDraft = 549;
        public float energyMax = 100f;
        [Header("Level growth; stat cards add on top")]
        [Min(0f), Tooltip("Max HP gained on every real level-up. Candidate 6.")] public float maxHpPerLevel = 6f;
        [Min(0f), Tooltip("Max Energy gained on every real level-up; the energy card budget is kept on top. Candidate 8.")] public float maxEnergyPerLevel = 8f;
        public float voidEnergyRegeneration = 4f;
        public float regenerationInterval = 5f;
        [Header("Stat card rarity (wave bands)")]
        [Range(0f, 100f), Tooltip("Percent chance that a stat card rolls its wave band's high tier instead of the low tier. Candidate 25.")]
        public float highTierPercent = 25f;
        [Min(1), Tooltip("After this many consecutive stat cards without the band's high tier, the next one is high tier. Candidate 4.")]
        public int highTierPity = 4;
        [Header("Skill card rarity (wave bands)")]
        [Tooltip("Owner 2026-10-25 (document 9.6): a skill card rolls a colour by the wave, and the colour is the level it gives. White learns "
            + "the skill at level 1 or adds one level, green puts it straight at level 2, blue straight at level 3 (an upgrade still adds at "
            + "least one level; never above 3). Each row holds from its wave until a later row starts; the percent left over is white.")]
        public SkillRarityBand[] skillRarityBands = { new(1, 20f, 0f), new(3, 30f, 10f), new(9, 40f, 25f), new(12, 40f, 40f) };
        [Header("Weapon card direction (wave bands)")]
        [Tooltip("Owner 2026-10-25 (document 9.6): a weapon card is coloured by its direction: Tame green, Twist blue, Infusion purple. When "
            + "the weapon on the card has more than one direction open, these weights pick one, so later rows favour blue and purple. Each "
            + "row holds from its wave until a later row starts; a direction that is the only one open is still offered.")]
        public WeaponDirectionBand[] weaponDirectionBands = { new(1, 50f, 30f, 20f), new(3, 40f, 35f, 25f), new(9, 30f, 35f, 35f), new(12, 20f, 40f, 40f) };

        [Tooltip("Rows follow StatId. Five tiers: white Common / green Rare / blue Exceptional / purple Epic / orange Legendary. "
            + "Each offered card has a main effect and an optional bonus at the same tier; retired rows are never offered.")]
        public StatCardValues[] statCards =
        {
            new(StatId.MaxHp, 15f, StatBonus.Regeneration, 1f), new(StatId.Regeneration, 1f, retired: true),
            new(StatId.MoveSpeed, .08f, StatBonus.Stamina, .2f), new(StatId.Stamina, .15f, retired: true),
            new(StatId.Damage, .12f, StatBonus.CritChance, .08f), new(StatId.AttackSpeed, .15f),
            new(StatId.Knockback, .2f, StatBonus.Impact, 3f, retired: true), new(StatId.Impact, 3f, retired: true),
            new(StatId.PickupRange, 1.5f, retired: true), new(StatId.Protection, 25f),
            new(StatId.Luck, 4f, retired: true), new(StatId.MaxEnergy, 25f, StatBonus.EnergySiphon, .4f),
            new(StatId.EnergySiphon, .3f, retired: true), new(StatId.ElementPenetration, .2f),
            new(StatId.CritChance, .1f, StatBonus.CritMultiplier, .15f)
        };

        public static void RarityBand(int wave, out CardRarity low, out CardRarity high)
        {
            if (wave <= 2) { low = CardRarity.Common; high = CardRarity.Rare; }
            else if (wave <= 8) { low = CardRarity.Rare; high = CardRarity.Exceptional; }
            else if (wave <= 11) { low = CardRarity.Exceptional; high = CardRarity.Epic; }
            else { low = CardRarity.Epic; high = CardRarity.Legendary; }
        }

        public CardRarity StatRarity(int wave, float percentRoll, bool pity)
        {
            RarityBand(wave, out var low, out var high);
            return pity || percentRoll < Mathf.Clamp(highTierPercent, 0f, 100f) ? high : low;
        }

        public CardRarity SkillRarity(int wave, float percentRoll)
        {
            SkillRarityBand band = default; int from = int.MinValue;
            if (skillRarityBands != null)
                foreach (var row in skillRarityBands)
                    if (row.fromWave <= Mathf.Max(1, wave) && row.fromWave > from) { band = row; from = row.fromWave; }
            float blue = Mathf.Clamp(band.bluePercent, 0f, 100f), green = Mathf.Clamp(band.greenPercent, 0f, 100f - blue);
            return percentRoll < blue ? CardRarity.Exceptional : percentRoll < blue + green ? CardRarity.Rare : CardRarity.Common;
        }

        public float DirectionWeight(int wave, UpgradeAxis axis)
        {
            WeaponDirectionBand band = default; int from = int.MinValue;
            if (weaponDirectionBands != null)
                foreach (var row in weaponDirectionBands)
                    if (row.fromWave <= Mathf.Max(1, wave) && row.fromWave > from) { band = row; from = row.fromWave; }
            return Mathf.Max(0f, band.Weight(axis));
        }
    }

    [System.Serializable]
    public struct SkillRarityBand
    {
        [Min(1), Tooltip("First wave (1-based) of this row; it holds until a later row starts.")] public int fromWave;
        [Range(0f, 100f), Tooltip("Percent of green skill cards: a new skill starts at level 2, an owned one rises to at least level 2.")] public float greenPercent;
        [Range(0f, 100f), Tooltip("Percent of blue skill cards: a new skill starts at level 3, an owned one rises to level 3.")] public float bluePercent;
        public SkillRarityBand(int fromWave, float greenPercent, float bluePercent)
        { this.fromWave = fromWave; this.greenPercent = greenPercent; this.bluePercent = bluePercent; }
    }

    [System.Serializable]
    public struct WeaponDirectionBand
    {
        [Min(1), Tooltip("First wave (1-based) of this row; it holds until a later row starts.")] public int fromWave;
        [Min(0f), Tooltip("Weight of a Tame card (green).")] public float tame;
        [Min(0f), Tooltip("Weight of a Twist card (blue).")] public float twist;
        [Min(0f), Tooltip("Weight of an Infusion card (purple).")] public float infusion;
        public WeaponDirectionBand(int fromWave, float tame, float twist, float infusion)
        { this.fromWave = fromWave; this.tame = tame; this.twist = twist; this.infusion = infusion; }
        public float Weight(UpgradeAxis axis) => axis switch
        {
            UpgradeAxis.Tame => tame, UpgradeAxis.Twist => twist, UpgradeAxis.Infusion => infusion, _ => 0f
        };
    }

    [System.Serializable]
    public sealed class StatCardValues
    {

        public static readonly float[] PlaceholderTierScale = { 1f, 1.5f, 2.1f, 2.8f, 3.6f };

        public StatId stat;
        [Tooltip("Main effect per tier: white Common, green Rare, blue Exceptional, purple Epic, orange Legendary.")]
        public float common, rare, exceptional, epic, legendary;
        [Tooltip("Second effect granted by the same card, at the same rarity.")]
        public StatBonus bonus;
        public float bonusCommon, bonusRare, bonusExceptional, bonusEpic, bonusLegendary;
        [Tooltip("Retired rows stay for serialized IDs and old saves but are never offered.")]
        public bool retired;

        public StatCardValues(StatId stat, float common, StatBonus bonus = StatBonus.None, float bonusCommon = 0f, bool retired = false)
        {
            this.stat = stat; this.bonus = bonus; this.retired = retired;
            var s = PlaceholderTierScale;
            this.common = common; rare = common * s[1]; exceptional = common * s[2]; epic = common * s[3]; legendary = common * s[4];
            this.bonusCommon = bonusCommon; bonusRare = bonusCommon * s[1]; bonusExceptional = bonusCommon * s[2];
            bonusEpic = bonusCommon * s[3]; bonusLegendary = bonusCommon * s[4];
        }

        public float At(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => rare, CardRarity.Exceptional => exceptional, CardRarity.Epic => epic,
            CardRarity.Legendary => legendary, _ => common
        };
        public float BonusAt(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => bonusRare, CardRarity.Exceptional => bonusExceptional, CardRarity.Epic => bonusEpic,
            CardRarity.Legendary => bonusLegendary, _ => bonusCommon
        };
    }
}
