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

        [Test]
        public void StatRarityFromRoll_ZeroLuck_PreservesOriginalHighBandAndExposesFourStatTiers()
        {
            var counts = new int[5];

            for (int roll = 0; roll < 100; roll++) counts[(int)sut.StatRarityFromRoll(roll + .5f, 0f, false)]++;

            Assert.That(counts, Is.EqualTo(new[] { 70, 25, 4, 1, 0 }));
        }

        [Test]
        public void StatRarityFromRoll_MaximumLuck_PreservesCapsAndLegendaryShare()
        {
            var counts = new int[5];

            for (int roll = 0; roll < 100; roll++) counts[(int)sut.StatRarityFromRoll(roll + .5f, 100f, false)]++;

            Assert.That(counts, Is.EqualTo(new[] { 25, 55, 16, 4, 0 }));
        }

        [Test]
        public void StatRarityFromRoll_Pity_AlwaysReturnsBlueOrOrange()
        {
            var counts = new int[5];

            for (int roll = 0; roll < 100; roll++) counts[(int)sut.StatRarityFromRoll(roll + .5f, 0f, true)]++;

            Assert.That(counts, Is.EqualTo(new[] { 0, 0, 80, 20, 0 }));
        }
    }
}
