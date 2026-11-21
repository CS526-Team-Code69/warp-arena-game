using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The base's rules as the pure rules of CampaignRules.Rooms.cs give them with their default values: what opens on which day,
    /// the alloy income scale, the shared weapon Mk, armor, the base's fortification levels and its daily self-repair, the ship's three
    /// parts and the fuel in dark matter, the energy research and what the body research sells, and the hooks the campaign sets on weapons
    /// and skills (neutral when unset, so the open mode is untouched).</summary>
    [TestFixture]
    public class CampaignRoomsRulesTest
    {
        CampaignRules rules;
        GameObject holder;

        CampaignRules CreateRules() => rules = ScriptableObject.CreateInstance<CampaignRules>();

        [TearDown]
        public void Release()
        {
            if (rules) Object.DestroyImmediate(rules);
            if (holder) Object.DestroyImmediate(holder);
            RunWeapons.MkDamageRule = null;
            RunWeapons.NewWeaponMk = null;
            RunWeapons.DamageBonus = null;
            SkillLoadout.CostScale = null;
            SkillLoadout.CooldownScale = null;
            SkillLoadout.DamageScale = null;
        }

        // ───── What opens on which day, and the alloy income ─────

        [TestCase(V2Campaign.StudyFeature.Body, 0)]
        [TestCase(V2Campaign.StudyFeature.Skills, 0)]
        [TestCase(V2Campaign.StudyFeature.Tame, 1)]
        [TestCase(V2Campaign.StudyFeature.Infuse, 2)]
        [TestCase(V2Campaign.StudyFeature.Twist, 4)]
        public void OpenDay_EachFeature_IsItsDay(V2Campaign.StudyFeature feature, int expected)
        {
            var sut = CreateRules();

            int actual = sut.OpenDay(feature);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void OpenDay_TheBody_IsDayZeroWhateverTheOtherDays()
        {
            var sut = CreateRules();
            sut.skillsOpenDay = 3;
            sut.tameOpenDay = 5;

            int actual = sut.OpenDay(V2Campaign.StudyFeature.Body);

            Assert.That(actual, Is.Zero);
        }

        [TestCase(false, .85f)]
        [TestCase(true, .85f * 1.3f)]
        public void AfternoonAlloyScale_WithOrWithoutTheEngine_IsTheIncomeScaleTimesItsBonus(bool engine, float expected)
        {
            var sut = CreateRules();

            float actual = sut.AfternoonAlloyScale(engine);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [TestCase(1, false, 17)]
        [TestCase(1, true, 22)]
        [TestCase(3, false, 38)]
        [TestCase(7, true, 265)]
        public void AfternoonAlloyTotal_WithTheIncomeScale_IsTheTableScaledAndRounded(int day, bool engine, int expected)
        {
            var sut = CreateRules();

            int actual = CampaignRules.AfternoonAlloyTotal(sut.afternoonAlloy, day, sut.AfternoonAlloyScale(engine));

            Assert.That(actual, Is.EqualTo(expected));
        }

        // ───── Weapon Mk ─────

        [TestCase(1, 1f)]
        [TestCase(4, 2f * 1.15f)]
        [TestCase(7, 4f * 1.3f)]
        [TestCase(10, 8f * 1.45f)]
        [TestCase(11, 11f * 1.45f)]
        public void MkDamage_AtMk_IsTheTableTimesTheMilestones(int mk, float expected)
        {
            var sut = CreateRules();

            float actual = sut.MkDamage(mk);

            Assert.That(actual, Is.EqualTo(expected).Within(.001f));
        }

        [TestCase(3, 0f)]
        [TestCase(4, .15f)]
        [TestCase(9, .30f)]
        [TestCase(10, .45f)]
        public void MkPenetration_AtMk_AddsFifteenPercentPerMilestone(int mk, float expected)
        {
            var sut = CreateRules();

            float actual = sut.MkPenetration(mk);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [TestCase(1, 5)]
        [TestCase(5, 22)]
        [TestCase(9, 65)]
        public void MkPrice_FromMk_IsTheDocPrice(int mk, int alloy)
        {
            var sut = CreateRules();

            var actual = sut.MkPrice(mk);

            Assert.That(actual, Is.EqualTo(new CampaignCost(alloy)));
        }

        [Test]
        public void MkPrice_AtMkTen_IsNullBecauseMkElevenIsTheVoidAlloys()
        {
            var sut = CreateRules();

            var actual = sut.MkPrice(10);

            Assert.That(actual, Is.Null);
        }

        [Test]
        public void WeaponMkAlloy_AllNineSteps_Cost250()
        {
            var sut = CreateRules();

            int actual = System.Linq.Enumerable.Sum(sut.weaponMkAlloy);

            Assert.That(actual, Is.EqualTo(250));
        }

        // ───── Armor and the base's fortification ─────

        [Test]
        public void ArmorAlloy_FiveLevels_Cost150()
        {
            var sut = CreateRules();

            int actual = System.Linq.Enumerable.Sum(sut.armorAlloy);

            Assert.That(actual, Is.EqualTo(150));
        }

        [Test]
        public void ArmorPrice_AfterTheFifthLevel_IsNull()
        {
            var sut = CreateRules();

            var actual = sut.ArmorPrice(5);

            Assert.That(actual, Is.Null);
        }

        [TestCase(1, 500f)]
        [TestCase(4, 1600f)]
        [TestCase(7, 5500f)]
        public void BaseHpAt_Level_IsTheDocTable(int level, float expected)
        {
            var sut = CreateRules();

            float actual = sut.BaseHpAt(level);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void BaseLevelAlloy_SixUpgrades_Cost200()
        {
            var sut = CreateRules();

            int actual = System.Linq.Enumerable.Sum(sut.baseLevelAlloy);

            Assert.That(actual, Is.EqualTo(200));
        }

        [TestCase(1, 10)]
        [TestCase(2, 14)]
        [TestCase(3, 20)]
        [TestCase(4, 32)]
        [TestCase(5, 48)]
        [TestCase(6, 76)]
        public void BaseLevelPrice_FromLevel_SharesTheTwoHundredByTheHpTheLevelAdds(int level, int expected)
        {
            var sut = CreateRules();

            var actual = sut.BaseLevelPrice(level);

            Assert.That(actual?.alloy, Is.EqualTo(expected));
        }

        [TestCase(1)]
        [TestCase(3)]
        [TestCase(6)]
        public void BaseLevelPrice_FromLevel_IsAboutFourHundredthsOfAlloyAPointOfHpAdded(int level)
        {
            var sut = CreateRules();

            float actual = sut.BaseLevelPrice(level).Value.alloy / (sut.BaseHpAt(level + 1) - sut.BaseHpAt(level));

            Assert.That(actual, Is.EqualTo(.04f).Within(.0001f));
        }

        [Test]
        public void BaseLevelPrice_AtLevelSeven_IsNull()
        {
            var sut = CreateRules();

            var actual = sut.BaseLevelPrice(7);

            Assert.That(actual, Is.Null);
        }

        [Test]
        public void NightSelfRepair_ByDefault_IsAQuarterOfTheMaxHp()
        {
            var sut = CreateRules();

            float actual = sut.nightSelfRepair;

            Assert.That(actual, Is.EqualTo(.25f).Within(.0001f));
        }

        // ───── The ship's three parts and the fuel ─────

        [TestCase(V2Campaign.ShipPartKind.Engine, 26)]
        [TestCase(V2Campaign.ShipPartKind.Hull, 54)]
        [TestCase(V2Campaign.ShipPartKind.Tank, 85)]
        public void ShipPartPrice_EachPart_IsItsAlloy(V2Campaign.ShipPartKind kind, int alloy)
        {
            var sut = CreateRules();

            var actual = sut.ShipPartPrice(kind);

            Assert.That(actual, Is.EqualTo(new CampaignCost(alloy)));
        }

        [Test]
        public void ShipPartPrice_AllThreeParts_Cost165Alloy()
        {
            var sut = CreateRules();

            int actual = sut.ShipPartPrice(V2Campaign.ShipPartKind.Engine).alloy + sut.ShipPartPrice(V2Campaign.ShipPartKind.Hull).alloy
                + sut.ShipPartPrice(V2Campaign.ShipPartKind.Tank).alloy;

            Assert.That(actual, Is.EqualTo(165));
        }

        [Test]
        public void FuelTop_ByDefault_IsAHundredDarkMatter()
        {
            var sut = CreateRules();

            int actual = sut.FuelTop;

            Assert.That(actual, Is.EqualTo(100));
        }

        [TestCase(37, 0, 100, 37)]
        [TestCase(150, 37, 100, 63)]
        [TestCase(5, 100, 100, 0)]
        [TestCase(0, 10, 100, 0)]
        public void FuelPoured_DarkMatterHeld_GoesInUpToAFullTank(int held, int fuel, int top, int expected)
        {
            int actual = CampaignRules.FuelPoured(held, fuel, top);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(false, false, false, 0, 4)]
        [TestCase(true, false, true, 37, 2)]
        [TestCase(true, true, true, 99, 1)]
        [TestCase(true, true, true, 100, 0)]
        public void HomeSteps_PartsAndFuel_CountOneStepEach(bool engine, bool hull, bool tank, int fuel, int expected)
        {
            int actual = CampaignRules.HomeSteps(engine, hull, tank, fuel, 100);

            Assert.That(actual, Is.EqualTo(expected));
        }

        // ───── Energy research ─────

        [TestCase(CardRarity.Common, 5, 1)]
        [TestCase(CardRarity.Rare, 8, 1)]
        [TestCase(CardRarity.Exceptional, 12, 2)]
        [TestCase(CardRarity.Epic, 18, 2)]
        [TestCase(CardRarity.Legendary, 28, 3)]
        public void RarityEnergyAndTier_Rarity_PriceAndResearchTier(CardRarity rarity, int energy, int tier)
        {
            var sut = CreateRules();

            var actual = (sut.RarityEnergy(rarity), sut.RarityTier(rarity));

            Assert.That(actual, Is.EqualTo((energy, tier)));
        }

        [TestCase(StatId.MaxHp, false)]
        [TestCase(StatId.MaxEnergy, false)]
        [TestCase(StatId.Damage, true)]
        [TestCase(StatId.CritChance, true)]
        public void BodySells_AStat_LeavesMaxHpAndMaxChargeToArmor(StatId stat, bool expected)
        {
            var sut = CreateRules();

            bool actual = sut.BodySells(stat);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase(1, 45)]
        [TestCase(2, 90)]
        public void ResearchPrice_FromTier_IsInEnergy(int tier, int energy)
        {
            var sut = CreateRules();

            var actual = sut.ResearchPrice(tier);

            Assert.That(actual, Is.EqualTo(new CampaignCost(0, energy)));
        }

        [Test]
        public void ResearchPrice_AtTierThree_IsNull()
        {
            var sut = CreateRules();

            var actual = sut.ResearchPrice(3);

            Assert.That(actual, Is.Null);
        }

        // Since 2026-11-10 (the number ledger) the skills grow with dayCombatScale alone: skillDamagePerDay is 0 by default.
        [TestCase(1)]
        [TestCase(7)]
        public void SkillDamageScale_Day_StaysOneByDefault(int day)
        {
            var sut = CreateRules();

            float actual = sut.SkillDamageScale(day);

            Assert.That(actual, Is.EqualTo(1f).Within(.0001f));
        }

        [TestCase(1, 1f)]
        [TestCase(7, 1.72f)]
        public void SkillDamageScale_WithTwelvePercentADay_GrowsAfterTheFirstDay(int day, float expected)
        {
            var sut = CreateRules();
            sut.skillDamagePerDay = .12f;

            float actual = sut.SkillDamageScale(day);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [TestCase(3, 2, 16f)]
        [TestCase(7, 3, 231f)]
        [TestCase(9, 1, 77f)]
        [TestCase(5, 0, 0f)]
        public void InfusionFloor_DayAndTier_IsTheDaysTableTimesTier(int day, int tier, float expected)
        {
            var sut = CreateRules();

            float actual = sut.InfusionFloor(day, tier);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [Test]
        public void InfusionFloor_InTheTyrantsFight_IsTheTyrantFloorTimesTier()
        {
            var sut = CreateRules();

            float actual = sut.InfusionFloor(7, 2, true);

            Assert.That(actual, Is.EqualTo(224f).Within(.0001f));
        }

        [TestCase(3, 2, 150f)]
        [TestCase(7, 3, 525f)]
        public void InfusionFloor_WithoutTheTable_Is25TimesDayTimesTier(int day, int tier, float expected)
        {
            var sut = CreateRules();
            sut.infusionFloorByDay = new float[0];

            float actual = sut.InfusionFloor(day, tier);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [TestCase(1, CardRarity.Common)]
        [TestCase(3, CardRarity.Rare)]
        [TestCase(6, CardRarity.Exceptional)]
        [TestCase(11, CardRarity.Legendary)]
        public void WeaponRarity_UnlockWave_LaterWeaponsLookRarer(int unlockWave, CardRarity expected)
        {
            var actual = CampaignRules.WeaponRarity(unlockWave);

            Assert.That(actual, Is.EqualTo(expected));
        }

        // ───── Hooks ─────

        RunWeapons CreateWeapons()
        {
            holder = new GameObject("RunWeapons(Test)");
            return holder.AddComponent<RunWeapons>();
        }

        [Test, CreateScene]
        public void MkDamage_WithACampaignRule_UsesTheRule()
        {
            var sut = CreateWeapons();
            RunWeapons.MkDamageRule = _ => 3f;

            float actual = sut.MkDamage(new RunWeapon { form = WeaponForm.Rifle, mk = 1 });

            Assert.That(actual, Is.EqualTo(3f));
        }

        [Test, CreateScene]
        public void MkDamage_WithARuleThatGivesZero_FallsBackToTheRoster()
        {
            var sut = CreateWeapons();
            RunWeapons.MkDamageRule = _ => 0f;

            float actual = sut.MkDamage(new RunWeapon { form = WeaponForm.Rifle, mk = 4 });

            Assert.That(actual, Is.EqualTo(1f));
        }

        [Test, CreateScene]
        public void GiveToHand_WithANewWeaponMk_AnAnomalyWeaponArrivesAtIt()
        {
            var sut = CreateWeapons();
            RunWeapons.NewWeaponMk = () => 6;

            sut.GiveToHand(1, WeaponForm.Railgun);

            Assert.That(sut.Hand[1].mk, Is.EqualTo(6));
        }

        [Test, CreateScene]
        public void GiveToHand_WithANewWeaponMk_ThePistolStaysMkOne()
        {
            var sut = CreateWeapons();
            RunWeapons.NewWeaponMk = () => 6;

            sut.GiveToHand(0, WeaponForm.Pistol);

            Assert.That(sut.Hand[0].mk, Is.EqualTo(1));
        }

        [Test, CreateScene]
        public void GiveToAuto_AutoForm_FillsThatSlot()
        {
            var sut = CreateWeapons();

            sut.GiveToAuto(1, WeaponForm.OrbitBlades);

            Assert.That(sut.Auto[1]?.form, Is.EqualTo(WeaponForm.OrbitBlades));
        }

        [Test]
        public void Cost_WithACampaignCostScale_IsScaled()
        {
            var definition = ScriptableObject.CreateInstance<SkillDefinition>();
            definition.cost = 30f;
            var sut = new SkillSlot { Definition = definition };
            SkillLoadout.CostScale = () => .7f;

            float actual = sut.Cost;

            Object.DestroyImmediate(definition);
            Assert.That(actual, Is.EqualTo(21f).Within(.0001f));
        }

        [Test]
        public void Cooldown_WithoutACampaign_IsTheSkillsOwn()
        {
            var definition = ScriptableObject.CreateInstance<SkillDefinition>();
            definition.cooldown = 8f;
            var sut = new SkillSlot { Definition = definition };

            float actual = sut.Cooldown;

            Object.DestroyImmediate(definition);
            Assert.That(actual, Is.EqualTo(8f).Within(.0001f));
        }
    }
}
