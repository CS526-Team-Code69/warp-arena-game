using System.Linq;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class RunProgressionTest
    {
        ProgressionRules rules;

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rules);

        RunProgression CreateSystemUnderTest()
        {
            rules = ScriptableObject.CreateInstance<ProgressionRules>();
            var sut = new GameObject("Progression").AddComponent<RunProgression>();
            sut.Rules = rules;
            sut.ResetRun();
            return sut;
        }

        [Test, CreateScene]
        public void AdjustMaxHp_LossPastFloor_StopsAtFloorAndRequestsNoHealing()
        {
            var sut = CreateSystemUnderTest();
            float before = sut.Stats.maxHp, changed = 0f, healed = 0f;
            sut.StatChanged += (_, amount) => changed += amount;
            sut.HealingRequested += amount => healed += amount;

            sut.AdjustMaxHp(-1000f, 10f);

            Assert.That(sut.Stats.maxHp, Is.EqualTo(10f), "max HP stops at the floor");
            Assert.That(changed, Is.EqualTo(10f - before).Within(.0001f), "StatChanged carries the actual loss");
            Assert.That(healed, Is.Zero, "a loss requests no healing");
        }

        [Test, CreateScene]
        public void AdjustMaxHp_AlreadyBelowFloor_LeavesMaxHpUnchanged()
        {
            var sut = CreateSystemUnderTest();
            sut.AdjustMaxHp(-1000f, 5f);

            sut.AdjustMaxHp(-1f, 10f);

            Assert.That(sut.Stats.maxHp, Is.EqualTo(5f));
        }

        [Test, CreateScene]
        public void GainXp_AllLevelCosts_ReachesLevelTwelveAndSwitchesToStatDrafts()
        {
            var sut = CreateSystemUnderTest();

            sut.GainXp(rules.levelCosts.Sum());

            Assert.That(sut.Level, Is.EqualTo(12), "max level");
            Assert.That(sut.BankedDrafts, Is.EqualTo(11), "one draft per level-up");
            Assert.That(sut.XpRequired, Is.EqualTo(rules.xpPerStatDraft), "next bar is a stat-only draft");
        }

        [Test, CreateScene]
        public void VoidBadge_MaxLevelAfterVoidTrial_ReturnsTrue()
        {
            var sut = CreateSystemUnderTest();
            sut.GainXp(rules.levelCosts.Sum());

            sut.CompleteVoidTrial();
            sut.GrantVoidLevel();

            Assert.That(sut.VoidBadge, Is.True);
        }

        [TestCase(1, CardRarity.Common, CardRarity.Rare)]
        [TestCase(5, CardRarity.Rare, CardRarity.Exceptional)]
        [TestCase(12, CardRarity.Epic, CardRarity.Legendary)]
        [CreateScene]
        public void RollStatCard_AfterWave_OffersOnlyThatWavesBand(int wave, CardRarity low, CardRarity high)
        {
            var sut = CreateSystemUnderTest();
            sut.BeginBuildPhase(wave);

            var actual = sut.RollStatCard();

            Assert.That(actual.Rarity, Is.EqualTo(low).Or.EqualTo(high));
        }

        [Test, CreateScene]
        public void RollStatCard_FourLowTierCardsInARow_ForcesHighTierOnTheFifth()
        {
            var sut = CreateSystemUnderTest();
            rules.highTierPercent = 0f;
            sut.BeginBuildPhase(9);
            sut.RollStatCard(); sut.RollStatCard(); sut.RollStatCard();
            var fourth = sut.RollStatCard();

            var actual = sut.RollStatCard();

            Assert.That(fourth.Rarity, Is.EqualTo(CardRarity.Exceptional), "fourth card stays low tier");
            Assert.That(actual.Rarity, Is.EqualTo(CardRarity.Epic), "fifth card is the pity high tier");
        }

        [Test, CreateScene]
        public void RollStatCard_EnergyAndPenetrationLocked_OffersOnlyActiveUnlockedRows()
        {
            var sut = CreateSystemUnderTest();

            var actual = sut.RollStatCard(StatId.MaxHp, StatId.MoveSpeed);

            Assert.That(new[] { StatId.Damage, StatId.AttackSpeed, StatId.CritChance, StatId.Protection }, Does.Contain(actual.Stat));
        }
    }
}
