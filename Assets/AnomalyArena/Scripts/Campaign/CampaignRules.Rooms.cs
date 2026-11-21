using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>An element the infusion research can put on a weapon, and the rarity (colour and price) of that infusion.</summary>
    [Serializable]
    public struct InfusionChoice
    {
        public ElementKind element;
        public CardRarity rarity;
        public InfusionChoice(ElementKind element, CardRarity rarity) { this.element = element; this.rarity = rarity; }
    }

    /// <summary>
    /// The base's numbers. Nothing at the base is built: the workshop at the base door sells the weapon Mk shared by every anomaly
    /// weapon, armor levels and Tame and Twist per hand weapon for alloy, and the energy research's body, skill and infusion items with
    /// the research levels that open rarer ones for alien energy; skills, Tame, infusion and Twist open on their own days. Its top bar
    /// fortifies the base, raising its max HP a level at a time. The rocket page at the ship sells the ship's three parts, each once and
    /// in any order, takes dark matter into the fuel and crafts the mining lamps. From the overload day the workshop also sells the
    /// overload for the Tyrant's fight. Every afternoon's ore alloy is scaled by alloyIncomeScale.
    /// The numbers of the V2 doc's table are the defaults; the ones marked "placeholder" in a tooltip were filled in to make the rules
    /// complete and wait for the number ledger.
    /// </summary>
    public sealed partial class CampaignRules
    {
        [Header("What opens on which day")]
        [Min(0), Tooltip("The day skills open in the workshop's energy research (0: already on Day 0's arrival at the base). The body research, the weapon Mk and armor are open from Day 0 always.")]
        public int skillsOpenDay = 0;
        [Min(0), Tooltip("The day Tame opens for the anomaly weapons in hand (each weapon tamed once, for tameAlloy).")]
        public int tameOpenDay = 1;
        [Min(0), Tooltip("The day weapon infusion opens in the energy research (alien energy, by the element's rarity).")]
        public int infuseOpenDay = 2;
        [Min(0), Tooltip("The day Twist opens for the anomaly weapons in hand (each weapon twisted once, for twistAlloy).")]
        public int twistOpenDay = 4;

        [Header("Alloy income")]
        [Min(0f), Tooltip("Multiplies the alloy of each afternoon's ore (afternoonAlloy, with the engine's bonus once it is repaired). It does not multiply Day 0's alloy (dayZeroAlloy), nor the part of the afternoon's ore that pays for its mining lamps (dayLampsToCraft x lampCost), nor the scattered nodes' fixed scatterYield.")]
        public float alloyIncomeScale = .85f;

        [Header("Base fortification and armor")]
        [Tooltip("The base's max HP at fortification levels 1 to 7 (the V2 doc's base table). A run starts at level 1.")]
        public float[] baseLevelHp = { 500f, 750f, 1100f, 1600f, 2400f, 3600f, 5500f };
        [Tooltip("Alloy to fortify the base to levels 2 to 7: the V2 doc's 200 in all, shared by the HP each level adds (250, 350, 500, 800, 1200 and 1900 of 5000), so each costs about 0.04 alloy a point of max HP. Fortifying keeps the share of HP the base has left.")]
        public int[] baseLevelAlloy = { 10, 14, 20, 32, 48, 76 };
        [Tooltip("Armor levels 1 to 5 in the workshop, in alloy (the doc gives 150 in all; the split is a placeholder). Each level is one player level: max HP and max charge as before.")]
        public int[] armorAlloy = { 10, 20, 30, 40, 50 };

        [Header("Weapon Mk, Tame and Twist (the workshop)")]
        [Tooltip("On: the Mk is one level for every anomaly weapon, bought once for all of them, held now or taken later. Off: each weapon rises on its own (the weapon in hand first). The starting pistol never takes a Mk either way.")]
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
        [Min(0), Tooltip("Alloy to tame one anomaly weapon in hand (from tameOpenDay).")]
        public int tameAlloy = 10;
        [Min(0), Tooltip("Alloy to twist one anomaly weapon in hand (from twistOpenDay).")]
        public int twistAlloy = 10;

        [Header("The ship's three parts (the rocket page)")]
        [Min(0), Tooltip("Alloy to repair the engine, bought once, in any order with the hull and the fuel tank. From the day after, each afternoon's ore gives shipOreBonus more alloy.")]
        public int engineAlloy = 26;
        [Min(0), Tooltip("Alloy to repair the hull, bought once, in any order. Then each raid strike takes shipBaseDamageCut less off the base, and the mining lamps light shipLampRadiusBonus further.")]
        public int hullAlloy = 54;
        [Min(0), Tooltip("Alloy to fit the fuel tank, bought once, in any order. Then skill cooldowns and charge costs fall by shipSkillCooldownCut and shipSkillCostCut, and dark matter can go into the fuel.")]
        public int fuelTankAlloy = 85;
        [Min(0f), Tooltip("The repaired engine: extra afternoon alloy from the day after it was repaired (0.3 = +30%), on top of alloyIncomeScale.")]
        public float shipOreBonus = .3f;
        [Range(0f, 1f), Tooltip("The repaired hull: the share of each raid strike the base no longer takes.")]
        public float shipBaseDamageCut = .25f;
        [Min(0f), Tooltip("The repaired hull: extra radius of the mining lamps' light (0.25 = +25%).")]
        public float shipLampRadiusBonus = .25f;
        [Range(0f, 1f), Tooltip("The fitted fuel tank: skill cooldowns shorter by this share.")]
        public float shipSkillCooldownCut = .3f;
        [Range(0f, 1f), Tooltip("The fitted fuel tank: skill charge costs lower by this share.")]
        public float shipSkillCostCut = .3f;
        [Min(1), Tooltip("Dark matter that fills the fuel (the V2 doc: 100). Once the fuel tank is fitted, refuelling pours in all the dark matter held, up to this. The three parts and a full tank let the ship ignite, on any day.")]
        public int fuelTankDark = 100;

        [Header("Energy research (the workshop)")]
        [Tooltip("Alien energy to raise a research line (body, skills, infusion) from level 1 to 2, and from 2 to 3. A line stands at level 1 as soon as it opens.")]
        public int[] researchTierEnergy = { 45, 90 };
        [Tooltip("Alien energy of one research item by its rarity: white, green, blue, purple, orange (placeholder).")]
        public int[] rarityEnergy = { 5, 8, 12, 18, 28 };
        [Tooltip("The research level that opens each rarity: white, green, blue, purple, orange. Level 1 white and green, level 2 blue and purple, level 3 orange (the doc's pools: white-green, blue-purple, purple-orange).")]
        public int[] rarityTier = { 1, 1, 2, 2, 3 };
        [Tooltip("The skill research level that opens new skills of each category: basic element, basic mechanic, advanced element, advanced mechanic (placeholder, after the classic run's wave bands).")]
        public int[] skillCategoryTier = { 1, 1, 2, 3 };
        [Tooltip("The elements infusion research offers and the rarity of each (the rarity sets the price and the research level it needs; placeholder). Void comes from void alloy, not from here.")]
        public InfusionChoice[] infusionChoices =
        {
            new(ElementKind.Fire, CardRarity.Common), new(ElementKind.Poison, CardRarity.Rare), new(ElementKind.Frost, CardRarity.Exceptional),
            new(ElementKind.Lightning, CardRarity.Epic), new(ElementKind.Earth, CardRarity.Legendary), new(ElementKind.Light, CardRarity.Legendary)
        };
        [Tooltip("Stats the body research does not sell: max HP and max charge come with the armor levels only.")]
        public StatId[] bodyLeftOut = { StatId.MaxHp, StatId.MaxEnergy };
        // The number ledger (params.json): 0, as the growth by day is in dayCombatScale (CampaignRules.Days.cs).
        [Min(0f), Tooltip("Once skills are open, skill damage grows by this share each day after the first (0.12 = +12% a day). 0: the skills grow with dayCombatScale alone.")]
        public float skillDamagePerDay = 0f;
        [Min(0f), Tooltip("Once infusion is open, and only with infusionFloorByDay empty: a burn or poison the player puts on an enemy deals at least this x the day x the infusion research level a second.")]
        public float infusionFloorPerDay = 25f;

        [Header("Final overload (the workshop, from overloadDay)")]
        [Min(1), Tooltip("The day from which the workshop sells the overload for the Tyrant's fight.")]
        public int overloadDay = 7;
        [Min(0)] public int overloadAlloy = 10;
        [Min(0)] public int overloadEnergy = 15;
        [Range(0f, 1f), Tooltip("Shield the player starts the Tyrant's fight with, per purchase, as a share of max HP.")]
        public float overloadShieldShare = .05f;
        [Range(0f, 1f), Tooltip("Damage dealt in the Tyrant's fight, per purchase (summed).")]
        public float overloadDamageShare = .03f;
        [Min(0), Tooltip("Purchases of each kind (shield, damage), whichever currency paid.")]
        public int overloadMaxBuys = 10;

        // ───── Pure rules (tested without a scene) ─────

        /// <summary>The day a workshop feature opens: skills, Tame, infusion and Twist on their days, the body research on Day 0.</summary>
        public int OpenDay(V2Campaign.StudyFeature feature) => feature switch
        {
            V2Campaign.StudyFeature.Skills => Mathf.Max(0, skillsOpenDay),
            V2Campaign.StudyFeature.Tame => Mathf.Max(0, tameOpenDay),
            V2Campaign.StudyFeature.Infuse => Mathf.Max(0, infuseOpenDay),
            V2Campaign.StudyFeature.Twist => Mathf.Max(0, twistOpenDay),
            _ => 0
        };

        /// <summary>Whether the body research sells this stat (bodyLeftOut names the ones it does not).</summary>
        public bool BodySells(StatId stat) => bodyLeftOut == null || Array.IndexOf(bodyLeftOut, stat) < 0;

        /// <summary>The factor on an afternoon's ore alloy: alloyIncomeScale, times 1 + shipOreBonus with the engine's bonus.</summary>
        public float AfternoonAlloyScale(bool engineBonus) => Mathf.Max(0f, alloyIncomeScale) * (engineBonus ? 1f + Mathf.Max(0f, shipOreBonus) : 1f);

        public int MaxBaseLevel => baseLevelHp is { Length: > 0 } ? baseLevelHp.Length : 1;
        /// <summary>The base's max HP at a fortification level (1 to MaxBaseLevel; outside, the nearest).</summary>
        public float BaseHpAt(int level) => baseLevelHp is { Length: > 0 } ? baseLevelHp[Mathf.Clamp(level - 1, 0, baseLevelHp.Length - 1)] : baseHp;
        /// <summary>The price of the fortification level after `level`, or null at the top.</summary>
        public CampaignCost? BaseLevelPrice(int level) => level < MaxBaseLevel && In(baseLevelAlloy, level - 1) ? new CampaignCost(baseLevelAlloy[level - 1]) : null;

        public int MaxArmorLevel => armorAlloy?.Length ?? 0;
        /// <summary>The price of the armor level after `armorLevel` (0 = none bought yet), or null at the top.</summary>
        public CampaignCost? ArmorPrice(int armorLevel) => In(armorAlloy, armorLevel) ? new CampaignCost(armorAlloy[armorLevel]) : null;

        /// <summary>The alloy price of one of the ship's parts.</summary>
        public CampaignCost ShipPartPrice(V2Campaign.ShipPartKind kind) => new(kind switch
        {
            V2Campaign.ShipPartKind.Engine => Mathf.Max(0, engineAlloy),
            V2Campaign.ShipPartKind.Hull => Mathf.Max(0, hullAlloy),
            _ => Mathf.Max(0, fuelTankAlloy)
        });

        /// <summary>The dark matter of a full tank.</summary>
        public int FuelTop => Mathf.Max(1, fuelTankDark);
        /// <summary>How much of `held` dark matter one refuelling pours into fuel that stands at `fuel` of `top`: all of it, up to a full tank.</summary>
        public static int FuelPoured(int held, int fuel, int top) => Mathf.Max(0, Mathf.Min(held, top - fuel));
        /// <summary>Steps left before the ship can ignite: the engine, the hull, the fuel tank and a full tank, one each (0 to 4).</summary>
        public static int HomeSteps(bool engine, bool hull, bool tank, int fuel, int top) =>
            (engine ? 0 : 1) + (hull ? 0 : 1) + (tank ? 0 : 1) + (fuel >= top ? 0 : 1);

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

        /// <summary>Alien energy of one research item of this rarity.</summary>
        public int RarityEnergy(CardRarity rarity) => RarityRow(rarityEnergy, rarity, 0);
        /// <summary>The research level that opens this rarity (Void: never by research).</summary>
        public int RarityTier(CardRarity rarity) => rarity == CardRarity.Void ? int.MaxValue : RarityRow(rarityTier, rarity, 1);
        /// <summary>The price of raising a research line from level `tier` to the next, or null at the top.</summary>
        public CampaignCost? ResearchPrice(int tier) => tier >= 1 && In(researchTierEnergy, tier - 1) ? new CampaignCost(0, researchTierEnergy[tier - 1]) : null;
        public int MaxResearchTier => (researchTierEnergy?.Length ?? 0) + 1;
        /// <summary>The skill research level that opens new skills of a category.</summary>
        public int SkillCategoryTier(SkillCategory category) => skillCategoryTier is { Length: > 0 } ? skillCategoryTier[Mathf.Clamp((int)category, 0, skillCategoryTier.Length - 1)] : 1;

        /// <summary>Skill damage on a day once skills are open.</summary>
        public float SkillDamageScale(int day) => 1f + skillDamagePerDay * Mathf.Max(0, day - 1);
        /// <summary>The least a burn or poison deals a second on a day (or in the Tyrant's fight) at an infusion research level, 0 before
        /// infusion opens: the day's infusionFloorByDay (tyrantInfusionFloor in the Tyrant's fight) x the level, or with that table empty
        /// infusionFloorPerDay x the day x the level.</summary>
        public float InfusionFloor(int day, int tier, bool tyrantFight = false)
        {
            if (tier <= 0) return 0f;
            if (tyrantFight) return Mathf.Max(0f, tyrantInfusionFloor) * tier;
            return infusionFloorByDay is { Length: > 0 } ? Mathf.Max(0f, ByDay(infusionFloorByDay, Mathf.Max(1, day), 0f)) * tier
                : infusionFloorPerDay * Mathf.Max(1, day) * tier;
        }

        /// <summary>The colour a weapon form shows with, after the wave it unlocks on (later weapons look rarer).</summary>
        public static CardRarity WeaponRarity(int unlockWave) => unlockWave <= 2 ? CardRarity.Common : unlockWave <= 3 ? CardRarity.Rare
            : unlockWave <= 6 ? CardRarity.Exceptional : unlockWave <= 10 ? CardRarity.Epic : CardRarity.Legendary;

        static bool In(Array table, int index) => table != null && index >= 0 && index < table.Length;
        static int RarityRow(int[] row, CardRarity rarity, int fallback) =>
            row is { Length: > 0 } ? row[Mathf.Clamp(ArenaRarity.Rank(rarity), 0, row.Length - 1)] : fallback;
    }
}
