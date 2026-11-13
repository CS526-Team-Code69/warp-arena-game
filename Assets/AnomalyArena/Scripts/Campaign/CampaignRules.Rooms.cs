using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>An element the Energy Lab's infusion research can put on a weapon, and the rarity (colour and price) of that infusion.</summary>
    [Serializable]
    public struct InfusionChoice
    {
        public ElementKind element;
        public CardRarity rarity;
        public InfusionChoice(ElementKind element, CardRarity rarity) { this.element = element; this.rarity = rarity; }
    }

    /// <summary>
    /// Rooms, levels and the base's study window (V2 doc section E, 2026-11-10): what each room level costs, what it gives besides what
    /// it unlocks, the weapon Mk shared by every anomaly weapon, armor levels, the base's levels and shield, the Energy Lab's three
    /// research lines, and the last day's overload. These fields replace the older price tables of CampaignRules.cs for the campaign
    /// (armamentLevels, energyLevels, shipLevels, durabilityLevels, shieldLevels, baseHp, shieldShare, the room repair formula, the player
    /// level prices and fuelUnits x darkPerFuelUnit), which stay in the asset but are no longer read by the base.
    /// The numbers of the V2 doc's table are the defaults; the ones marked "placeholder" in a tooltip were filled in to make the rules
    /// complete and wait for the number ledger.
    /// </summary>
    public sealed partial class CampaignRules
    {
        [Header("Rooms (V2 doc section E)")]
        [Tooltip("Ship Launch Bay levels 1, 2, 3, in alloy. Level 1: afternoon ore x(1 + shipOreBonus) from the next day; 2: raids hurt the base less and lamps light further; 3: skills cool down faster and cost less energy.")]
        public int[] shipRoomAlloy = { 20, 45, 85 };
        [Tooltip("Armament Bay levels 1, 2, 3, in alloy. Level 1: weapon Mk and armor; 2: new weapons and Tame; 3: Twist.")]
        public int[] armamentRoomAlloy = { 10, 35, 75 };
        [Tooltip("Energy Lab levels 1, 2, 3, in alien energy. Level 1: body research; 2: skill research; 3: infusion research.")]
        public int[] energyRoomEnergy = { 15, 35, 70 };
        [Tooltip("Base shield levels 1, 2, 3 at Central Control, in alien energy.")]
        public int[] shieldEnergy = { 20, 45, 85 };
        [Tooltip("The shield's cap at shield levels 1, 2, 3, as a share of the base's max HP (read back from the V2 doc's base table; placeholder).")]
        public float[] shieldCapShares = { .33f, .45f, .58f };
        [Tooltip("The base's max HP at base levels 1 to 7 (the V2 doc's base table).")]
        public float[] baseLevelHp = { 500f, 750f, 1100f, 1600f, 2400f, 3600f, 5500f };
        [Tooltip("Alloy to raise the base to levels 2 to 7: the V2 doc's 200 in all, shared by the HP each level adds (250, 350, 500, 800, 1200 and 1900 of 5000). The doc has the base rise a level a day, and so each level costs about the same share, 45 to 50%, of the afternoon alloy of the day before it.")]
        public int[] baseLevelAlloy = { 10, 14, 20, 32, 48, 76 };
        [Min(0), Tooltip("Alloy to repair a broken room, any room, every time.")]
        public int roomRepairAlloy = 15;
        [Tooltip("Armor levels 1 to 5 at the Armament Bay, in alloy (the doc gives 150 in all; the split is a placeholder). Each level is one player level: max HP and max energy as before.")]
        public int[] armorAlloy = { 10, 20, 30, 40, 50 };

        [Header("Weapon Mk (Armament Bay level 1)")]
        [Tooltip("On: the Mk is the Armament Bay's level for every anomaly weapon, bought once for all of them, held now or taken later. Off: each weapon rises on its own (the weapon in hand first). The starting pistol never takes a Mk either way.")]
        public bool sharedWeaponMk = true;
        [Tooltip("Alloy to raise the Mk from 1 to 2, 2 to 3, ... 9 to 10. Mk 11 is bought with void alloy elsewhere.")]
        public int[] weaponMkAlloy = { 5, 8, 12, 16, 22, 30, 40, 52, 65 };
        [Tooltip("Damage factor at Mk 1 to Mk 11: about +26% a level (2 at Mk 4, 4 at Mk 7, 8 at Mk 10) and 11 at the void Mk 11.")]
        public float[] weaponMkDamage = { 1f, 1.26f, 1.587f, 2f, 2.52f, 3.175f, 4f, 5.04f, 6.35f, 8f, 11f };
        [Tooltip("The Mk levels that each add mkMilestoneDamage and mkMilestonePenetration.")]
        public int[] weaponMkMilestones = { 4, 7, 10 };
        [Min(0f), Tooltip("Fixed damage added at each milestone Mk, summed (three milestones: x1.45).")]
        public float mkMilestoneDamage = .15f;
        [Range(0f, 1f), Tooltip("Damage Penetration against a boss's element immunity added at each milestone Mk, summed (three milestones: 45%).")]
        public float mkMilestonePenetration = .15f;
        [Min(0), Tooltip("Alloy to tame one weapon (Armament Bay level 2).")]
        public int tameAlloy = 10;
        [Min(0), Tooltip("Alloy to twist one weapon (Armament Bay level 3).")]
        public int twistAlloy = 10;

        [Header("Ship Launch Bay effects")]
        [Min(0f), Tooltip("Level 1: extra afternoon alloy from the day after it is built (0.3 = +30%).")]
        public float shipOreBonus = .3f;
        [Range(0f, 1f), Tooltip("Level 2: share of each raid strike the base no longer takes.")]
        public float shipBaseDamageCut = .25f;
        [Min(0f), Tooltip("Level 2: extra radius of the mining lamps (0.25 = +25%).")]
        public float shipLampRadiusBonus = .25f;
        [Range(0f, 1f), Tooltip("Level 3: skill cooldowns shorter by this share.")]
        public float shipSkillCooldownCut = .3f;
        [Range(0f, 1f), Tooltip("Level 3: skill energy costs lower by this share.")]
        public float shipSkillCostCut = .3f;

        [Header("Central Control: the shield guards the player")]
        [Range(0f, 1f), Tooltip("While the base has shield left, this share of each hit on a player near the base goes to the base's shield instead.")]
        public float shieldGuardShare = .3f;
        [Min(0f), Tooltip("Cells from the base's walls within which the shield guards the player (placeholder).")]
        public float shieldGuardRadius = 12f;

        [Header("Energy Lab research")]
        [Tooltip("Alien energy to raise a research line from tier 1 to 2, and from 2 to 3. Each line (body, skills, infusion) is at tier 1 as soon as its Energy Lab level is built.")]
        public int[] researchTierEnergy = { 45, 90 };
        [Tooltip("Alien energy of one study item by its rarity: white, green, blue, purple, orange (placeholder).")]
        public int[] rarityEnergy = { 5, 8, 12, 18, 28 };
        [Tooltip("The research tier that unlocks each rarity: white, green, blue, purple, orange. Tier 1 white and green, tier 2 blue and purple, tier 3 orange (the doc's pools: white-green, blue-purple, purple-orange).")]
        public int[] rarityTier = { 1, 1, 2, 2, 3 };
        [Tooltip("The skill research tier that unlocks new skills of each category: basic element, basic mechanic, advanced element, advanced mechanic (placeholder, after the classic run's wave bands).")]
        public int[] skillCategoryTier = { 1, 1, 2, 3 };
        [Tooltip("The elements infusion research offers and the rarity of each (the rarity sets the price and the research tier it needs; placeholder). Void comes from void alloy, not from here.")]
        public InfusionChoice[] infusionChoices =
        {
            new(ElementKind.Fire, CardRarity.Common), new(ElementKind.Poison, CardRarity.Rare), new(ElementKind.Frost, CardRarity.Exceptional),
            new(ElementKind.Lightning, CardRarity.Epic), new(ElementKind.Earth, CardRarity.Legendary), new(ElementKind.Light, CardRarity.Legendary)
        };
        // 2026-11-10, the number ledger (params.json): 0, as the growth by day is in dayCombatScale (CampaignRules.Days.cs).
        [Min(0f), Tooltip("Energy Lab level 2: skill damage grows by this share each day after the first (0.12 = +12% a day). 0: the skills grow with dayCombatScale alone.")]
        public float skillDamagePerDay = 0f;
        [Min(0f), Tooltip("Energy Lab level 3, only with infusionFloorByDay empty: a burn or poison the player puts on an enemy deals at least this x the day x the infusion research tier a second.")]
        public float infusionFloorPerDay = 25f;

        [Header("Final overload (Central Control, last day)")]
        [Min(1), Tooltip("The day from which Central Control sells the overload for the Tyrant's fight.")]
        public int overloadDay = 7;
        [Min(0)] public int overloadAlloy = 10;
        [Min(0)] public int overloadEnergy = 15;
        [Range(0f, 1f), Tooltip("Shield the player starts the Tyrant's fight with, per purchase, as a share of max HP.")]
        public float overloadShieldShare = .05f;
        [Range(0f, 1f), Tooltip("Damage dealt in the Tyrant's fight, per purchase (summed).")]
        public float overloadDamageShare = .03f;
        [Min(0), Tooltip("Purchases of each kind (shield, damage), whichever currency paid.")]
        public int overloadMaxBuys = 10;

        [Header("Ship fuel")]
        [Min(1), Tooltip("Dark matter that fills the fuel tank (the V2 doc: 100).")]
        public int fuelTankDark = 100;
        [Min(1), Tooltip("Dark matter one fuel synthesis takes; the tank holds fuelTankDark / this units.")]
        public int fuelDarkPerSynthesis = 10;

        // ───── Pure rules (tested without a scene) ─────

        /// <summary>The price of a room's level `nextLevel` (1 to 3), or null past the top. Central Control has no levels to buy.</summary>
        public CampaignCost? RoomLevelPrice(CampaignRoom room, int nextLevel)
        {
            int i = nextLevel - 1;
            return room switch
            {
                CampaignRoom.Ship when In(shipRoomAlloy, i) => new CampaignCost(shipRoomAlloy[i]),
                CampaignRoom.Armament when In(armamentRoomAlloy, i) => new CampaignCost(armamentRoomAlloy[i]),
                CampaignRoom.Energy when In(energyRoomEnergy, i) => new CampaignCost(0, energyRoomEnergy[i]),
                _ => null
            };
        }

        public int MaxBaseLevel => baseLevelHp is { Length: > 0 } ? baseLevelHp.Length : 1;
        /// <summary>The base's max HP at a level (1 to MaxBaseLevel; outside, the nearest).</summary>
        public float BaseHpAt(int level) => baseLevelHp is { Length: > 0 } ? baseLevelHp[Mathf.Clamp(level - 1, 0, baseLevelHp.Length - 1)] : baseHp;
        /// <summary>The price of the level after `level`, or null at the top.</summary>
        public CampaignCost? BaseLevelPrice(int level) => level < MaxBaseLevel && In(baseLevelAlloy, level - 1) ? new CampaignCost(baseLevelAlloy[level - 1]) : null;

        public int MaxShieldLevel => shieldEnergy?.Length ?? 0;
        /// <summary>The shield's cap at a shield level as a share of the base's max HP; 0 without a shield.</summary>
        public float ShieldCapShare(int shieldLevel) => shieldLevel <= 0 || shieldCapShares is not { Length: > 0 } ? 0f
            : shieldCapShares[Mathf.Clamp(shieldLevel - 1, 0, shieldCapShares.Length - 1)];
        /// <summary>The price of the shield level after `shieldLevel`, or null at the top.</summary>
        public CampaignCost? ShieldPrice(int shieldLevel) => In(shieldEnergy, shieldLevel) ? new CampaignCost(0, shieldEnergy[shieldLevel]) : null;

        public int MaxArmorLevel => armorAlloy?.Length ?? 0;
        /// <summary>The price of the armor level after `armorLevel` (0 = none bought yet), or null at the top.</summary>
        public CampaignCost? ArmorPrice(int armorLevel) => In(armorAlloy, armorLevel) ? new CampaignCost(armorAlloy[armorLevel]) : null;

        /// <summary>The highest Mk there is (the void Mk 11).</summary>
        public int MaxWeaponMk => weaponMkDamage is { Length: > 0 } ? weaponMkDamage.Length : 1;
        /// <summary>The highest Mk alloy buys (Mk 10).</summary>
        public int MaxAlloyMk => Mathf.Min(MaxWeaponMk, (weaponMkAlloy?.Length ?? 0) + 1);
        /// <summary>The price of the Mk after `mk`, or null when alloy buys no higher Mk.</summary>
        public CampaignCost? MkPrice(int mk) => mk >= 1 && mk < MaxAlloyMk && In(weaponMkAlloy, mk - 1) ? new CampaignCost(weaponMkAlloy[mk - 1]) : null;
        /// <summary>How many milestone Mk levels `mk` has reached.</summary>
        public int MkMilestones(int mk)
        {
            int count = 0;
            if (weaponMkMilestones != null) foreach (int milestone in weaponMkMilestones) if (mk >= milestone) count++;
            return count;
        }
        /// <summary>A weapon's damage factor at `mk`: the Mk's own factor times 1 + the milestones' fixed damage.</summary>
        public float MkDamage(int mk)
        {
            float table = weaponMkDamage is { Length: > 0 } ? weaponMkDamage[Mathf.Clamp(mk - 1, 0, weaponMkDamage.Length - 1)] : 1f;
            return table * (1f + mkMilestoneDamage * MkMilestones(mk));
        }
        /// <summary>Damage Penetration against a boss's element immunity at `mk`.</summary>
        public float MkPenetration(int mk) => Mathf.Clamp01(mkMilestonePenetration * MkMilestones(mk));

        /// <summary>Alien energy of one study item of this rarity.</summary>
        public int RarityEnergy(CardRarity rarity) => RarityRow(rarityEnergy, rarity, 0);
        /// <summary>The research tier that unlocks this rarity (Void: never by research).</summary>
        public int RarityTier(CardRarity rarity) => rarity == CardRarity.Void ? int.MaxValue : RarityRow(rarityTier, rarity, 1);
        /// <summary>The price of raising a research line from `tier` to the next, or null at the top.</summary>
        public CampaignCost? ResearchPrice(int tier) => tier >= 1 && In(researchTierEnergy, tier - 1) ? new CampaignCost(0, researchTierEnergy[tier - 1]) : null;
        public int MaxResearchTier => (researchTierEnergy?.Length ?? 0) + 1;
        /// <summary>The skill research tier that unlocks new skills of a category.</summary>
        public int SkillCategoryTier(SkillCategory category) => skillCategoryTier is { Length: > 0 } ? skillCategoryTier[Mathf.Clamp((int)category, 0, skillCategoryTier.Length - 1)] : 1;

        /// <summary>Skill damage on a day once the Energy Lab's level 2 is built.</summary>
        public float SkillDamageScale(int day) => 1f + skillDamagePerDay * Mathf.Max(0, day - 1);
        /// <summary>The least a burn or poison deals a second on a day (or in the Tyrant's fight) at an infusion research tier, 0 without
        /// infusion research: the day's infusionFloorByDay (tyrantInfusionFloor in the Tyrant's fight) x the tier, or with that table empty
        /// infusionFloorPerDay x the day x the tier.</summary>
        public float InfusionFloor(int day, int tier, bool tyrantFight = false)
        {
            if (tier <= 0) return 0f;
            if (tyrantFight) return Mathf.Max(0f, tyrantInfusionFloor) * tier;
            return infusionFloorByDay is { Length: > 0 } ? Mathf.Max(0f, ByDay(infusionFloorByDay, Mathf.Max(1, day), 0f)) * tier
                : infusionFloorPerDay * Mathf.Max(1, day) * tier;
        }
        /// <summary>The part of a hit on a guarded player that the base's shield takes (never more than the shield has).</summary>
        public float ShieldGuardTakes(float hit, float shieldLeft) => Mathf.Clamp(Mathf.Max(0f, hit) * shieldGuardShare, 0f, Mathf.Max(0f, shieldLeft));

        /// <summary>Fuel units that fill the tank, and the dark matter of one.</summary>
        public int FuelUnitsFull => Mathf.Max(1, Mathf.CeilToInt(fuelTankDark / (float)Mathf.Max(1, fuelDarkPerSynthesis)));
        public int DarkPerFuel => Mathf.Max(1, fuelDarkPerSynthesis);

        /// <summary>The colour a weapon form shows in the study window, after the wave it unlocks on (later weapons look rarer).</summary>
        public static CardRarity WeaponRarity(int unlockWave) => unlockWave <= 2 ? CardRarity.Common : unlockWave <= 3 ? CardRarity.Rare
            : unlockWave <= 6 ? CardRarity.Exceptional : unlockWave <= 10 ? CardRarity.Epic : CardRarity.Legendary;

        static bool In(Array table, int index) => table != null && index >= 0 && index < table.Length;
        static int RarityRow(int[] row, CardRarity rarity, int fallback) =>
            row is { Length: > 0 } ? row[Mathf.Clamp(ArenaRarity.Rank(rarity), 0, row.Length - 1)] : fallback;
    }
}
