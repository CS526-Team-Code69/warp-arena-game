using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class ProgressionRulesTest
    {
        ProgressionRules sut;

        [SetUp]
        public void SetUp() => sut = ScriptableObject.CreateInstance<ProgressionRules>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(sut);

        [TestCase(1, CardRarity.Common, CardRarity.Rare)]
        [TestCase(2, CardRarity.Common, CardRarity.Rare)]
        [TestCase(3, CardRarity.Rare, CardRarity.Exceptional)]
        [TestCase(8, CardRarity.Rare, CardRarity.Exceptional)]
        [TestCase(9, CardRarity.Exceptional, CardRarity.Epic)]
        [TestCase(11, CardRarity.Exceptional, CardRarity.Epic)]
        [TestCase(12, CardRarity.Epic, CardRarity.Legendary)]
        [TestCase(14, CardRarity.Epic, CardRarity.Legendary)]
        public void RarityBand_WaveJustFinished_ReturnsDecisionListTiers(int wave, CardRarity expectedLow, CardRarity expectedHigh)
        {
            ProgressionRules.RarityBand(wave, out var low, out var high);

            Assert.That(low, Is.EqualTo(expectedLow), "low tier");
            Assert.That(high, Is.EqualTo(expectedHigh), "high tier");
        }

        [TestCase(5, 0f, CardRarity.Exceptional)]
        [TestCase(5, 24.9f, CardRarity.Exceptional)]
        [TestCase(5, 25f, CardRarity.Rare)]
        [TestCase(12, 99.9f, CardRarity.Epic)]
        public void StatRarity_RollAgainstHighTierPercent_SplitsBandAtTwentyFive(int wave, float roll, CardRarity expected)
        {
            var actual = sut.StatRarity(wave, roll, false);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void StatRarity_PityOnHighestRoll_ReturnsHighTier()
        {
            var actual = sut.StatRarity(10, 99.9f, true);

            Assert.That(actual, Is.EqualTo(CardRarity.Epic));
        }

        [Test]
        public void Defaults_LevelCurve_MaxLevelTwelveWithOneAscendingCostPerLevelBelowMax()
        {
            Assert.That(sut.maxLevel, Is.EqualTo(12), "max level");
            Assert.That(sut.levelCosts, Has.Length.EqualTo(sut.maxLevel - 1), "one cost per level below max");
            Assert.That(sut.levelCosts, Is.Ordered.Ascending, "costs rise");
        }

        [TestCase(CardRarity.Common, 25f)]
        [TestCase(CardRarity.Rare, 37.5f)]
        [TestCase(CardRarity.Exceptional, 52.5f)]
        [TestCase(CardRarity.Epic, 70f)]
        [TestCase(CardRarity.Legendary, 90f)]
        public void At_FiveTiers_ScalesCommonValueByPlaceholderFactors(CardRarity rarity, float expected)
        {
            var values = new StatCardValues(StatId.Protection, 25f, StatBonus.CritMultiplier, 25f);

            Assert.That(values.At(rarity), Is.EqualTo(expected).Within(.001f), "main value");
            Assert.That(values.BonusAt(rarity), Is.EqualTo(expected).Within(.001f), "bonus value");
        }
    }
}
