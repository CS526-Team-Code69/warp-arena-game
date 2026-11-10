using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>V2 doc section E (2026-11-10): the rooms' prices and effects, the shared weapon Mk, armor, the base's levels and shield, the
    /// Energy Lab's research and the overload, as the pure rules of CampaignRules.Rooms.cs give them with their default values, and the hooks
    /// the campaign sets on weapons and skills (neutral when unset, so the open mode is untouched).</summary>
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

        // ───── Rooms ─────

        [TestCase(CampaignRoom.Ship, 1, 20, 0)]
        [TestCase(CampaignRoom.Ship, 2, 45, 0)]
        [TestCase(CampaignRoom.Ship, 3, 85, 0)]
        [TestCase(CampaignRoom.Armament, 1, 10, 0)]
        [TestCase(CampaignRoom.Armament, 2, 35, 0)]
        [TestCase(CampaignRoom.Armament, 3, 75, 0)]
        [TestCase(CampaignRoom.Energy, 1, 0, 15)]
        [TestCase(CampaignRoom.Energy, 2, 0, 35)]
        [TestCase(CampaignRoom.Energy, 3, 0, 70)]
        public void RoomLevelPrice_EachLevel_IsTheDocTable(CampaignRoom room, int level, int alloy, int energy)
        {
            var sut = CreateRules();

            var actual = sut.RoomLevelPrice(room, level);

            Assert.That(actual, Is.EqualTo(new CampaignCost(alloy, energy)));
        }

        [TestCase(CampaignRoom.Ship)]
        [TestCase(CampaignRoom.Armament)]
        [TestCase(CampaignRoom.Energy)]
        public void RoomLevelPrice_PastLevelThree_IsNull(CampaignRoom room)
        {
            var sut = CreateRules();

            var actual = sut.RoomLevelPrice(room, 4);

            Assert.That(actual, Is.Null);
        }

        [Test]
        public void RoomLevelPrice_CentralControl_IsNullBecauseItsShieldIsPricedApart()
        {
            var sut = CreateRules();

            var actual = sut.RoomLevelPrice(CampaignRoom.Control, 1);

            Assert.That(actual, Is.Null);
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

        // ───── Armor, base, shield ─────

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

        [Test]
        public void BaseLevelPrice_AtLevelSeven_IsNull()
        {
            var sut = CreateRules();

            var actual = sut.BaseLevelPrice(7);

            Assert.That(actual, Is.Null);
        }

        [TestCase(0, 0f)]
        [TestCase(1, .33f)]
        [TestCase(3, .58f)]
        public void ShieldCapShare_ShieldLevel_IsTheShare(int level, float expected)
        {
            var sut = CreateRules();

            float actual = sut.ShieldCapShare(level);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [TestCase(0, 20)]
        [TestCase(2, 85)]
        public void ShieldPrice_NextShieldLevel_IsInEnergy(int level, int energy)
        {
            var sut = CreateRules();

            var actual = sut.ShieldPrice(level);

            Assert.That(actual, Is.EqualTo(new CampaignCost(0, energy)));
        }

        [TestCase(100f, 1000f, 30f)]
        [TestCase(100f, 10f, 10f)]
        [TestCase(100f, 0f, 0f)]
        public void ShieldGuardTakes_Hit_TakesThirtyPercentUpToTheShieldLeft(float hit, float shield, float expected)
        {
            var sut = CreateRules();

            float actual = sut.ShieldGuardTakes(hit, shield);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [Test]
        public void FuelUnitsFull_TimesDarkPerFuel_IsAHundredDarkMatter()
        {
            var sut = CreateRules();

            int actual = sut.FuelUnitsFull * sut.DarkPerFuel;

            Assert.That(actual, Is.EqualTo(100));
        }

        // ───── Energy Lab ─────

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

        [TestCase(3, 2, 14f)]
        [TestCase(7, 3, 183f)]
        [TestCase(9, 1, 61f)]
        [TestCase(5, 0, 0f)]
        public void InfusionFloor_DayAndTier_IsTheDaysTableTimesTier(int day, int tier, float expected)
        {
            var sut = CreateRules();

            float actual = sut.InfusionFloor(day, tier);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [Test]
        public void InfusionFloor_InTheTyrantsFight_IsNinetyTimesTier()
        {
            var sut = CreateRules();

            float actual = sut.InfusionFloor(7, 2, true);

            Assert.That(actual, Is.EqualTo(180f).Within(.0001f));
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
