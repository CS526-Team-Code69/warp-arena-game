using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The campaign's numbers by day (2026-11-10, CampaignRules.Days.cs) as pure rules with their default values, checked against
    /// the number ledger's tables (work/v2-doc-1110/numbers/params.json): each day's kinds, the HP of every kind by day, the two bosses and
    /// the black hole's requirement, the skill scale and the payoff by day, the day's kill energy, the night's energy, the stat lines and
    /// Day 0's gifts.</summary>
    [TestFixture]
    public class CampaignDaysRulesTest
    {
        // The shield share of the Stomper's definition (V2/Data/Enemies/Stomper.asset) and the shield's direct damage factor (StatusRules).
        const float StomperShield = .5f, ShieldDirect = .7f;

        CampaignRules rules;

        CampaignRules CreateRules() => rules = ScriptableObject.CreateInstance<CampaignRules>();

        [TearDown]
        public void Release()
        {
            if (rules) Object.DestroyImmediate(rules);
        }

        // ───── Each day's kinds (the implementation spec's section F) ─────

        [TestCase(1, EnemyKind.Grunt, 60, 60)]
        [TestCase(1, EnemyKind.Spitter, 15, 0)]
        [TestCase(1, EnemyKind.Skitter, 20, 0)]
        [TestCase(2, EnemyKind.Warden, 6, 6)]
        [TestCase(2, EnemyKind.Chanter, 6, 0)]
        [TestCase(3, EnemyKind.Spire, 6, 6)]
        [TestCase(4, EnemyKind.Bloater, 0, 6)]
        [TestCase(4, EnemyKind.Stomper, 6, 0)]
        [TestCase(5, EnemyKind.Stomper, 6, 6)]
        [TestCase(6, EnemyKind.Stomper, 0, 0)]
        [TestCase(6, EnemyKind.Shade, 0, 6)]
        [TestCase(6, EnemyKind.Brute, 18, 0)]
        [TestCase(7, EnemyKind.Skitter, 25, 25)]
        [TestCase(7, EnemyKind.Splitter, 20, 20)]
        [TestCase(9, EnemyKind.Brute, 18, 18)]
        public void DayKindWeight_DayAndKind_FollowsTheDaysTable(int day, EnemyKind kind, int saved, int expected)
        {
            var sut = CreateRules();

            int actual = sut.DayKindWeight(day, kind, saved);

            Assert.That(actual, Is.EqualTo(expected));
        }

        // ───── HP by day ─────

        static readonly Dictionary<EnemyKind, int[]> PlainHp = new()
        {
            [EnemyKind.Grunt] = new[] { 4, 6, 13, 26, 49, 90, 218 },
            [EnemyKind.Skitter] = new[] { 4, 6, 13, 26, 49, 90, 218 },
            [EnemyKind.Splitter] = new[] { 4, 6, 13, 26, 49, 90, 218 },
            [EnemyKind.Brute] = new[] { 4, 6, 13, 26, 49, 90, 218 },
            [EnemyKind.Spitter] = new[] { 15, 24, 51, 103, 191, 353, 853 },
            [EnemyKind.Warden] = new[] { 19, 30, 66, 132, 244, 451, 1090 },
            [EnemyKind.Chanter] = new[] { 19, 30, 66, 132, 244, 451, 1090 },
            [EnemyKind.Rammer] = new[] { 20, 31, 69, 137, 255, 470, 1137 },
            [EnemyKind.Bloater] = new[] { 13, 21, 46, 92, 170, 314, 760 },
            [EnemyKind.Spire] = new[] { 24, 39, 86, 172, 318, 588, 1421 },
            [EnemyKind.Stomper] = new[] { 13, 21, 47, 94, 174, 321, 775 },
            [EnemyKind.Shade] = new[] { 12, 18, 40, 80, 150, 276, 667 },
        };

        static readonly Dictionary<EnemyKind, int[]> EliteHp = new()
        {
            [EnemyKind.Chanter] = new[] { 62, 99, 217, 435, 807, 1490, 3602 },
            [EnemyKind.Rammer] = new[] { 62, 99, 217, 435, 807, 1490, 3602 },
            [EnemyKind.Bloater] = new[] { 54, 86, 188, 377, 699, 1291, 3120 },
            [EnemyKind.Spire] = new[] { 66, 106, 232, 463, 861, 1589, 3840 },
            [EnemyKind.Stomper] = new[] { 39, 62, 135, 270, 502, 927, 2240 },
            [EnemyKind.Shade] = new[] { 50, 80, 174, 348, 646, 1193, 2883 },
        };

        static IEnumerable<TestCaseData> HpCases(Dictionary<EnemyKind, int[]> table)
        {
            foreach (var pair in table)
                for (int day = 1; day <= pair.Value.Length; day++)
                    yield return new TestCaseData(pair.Key, day, pair.Value[day - 1]).SetName($"{{m}}({pair.Key},day {day})");
        }

        static IEnumerable<TestCaseData> PlainHpCases() => HpCases(PlainHp);
        static IEnumerable<TestCaseData> EliteHpCases() => HpCases(EliteHp);

        [TestCaseSource(nameof(PlainHpCases))]
        public void BodyHp_PlainBodyOnDay_IsTheLedgersEnemyHpByDay(EnemyKind kind, int day, int expected)
        {
            var sut = CreateRules();

            float actual = sut.BodyHp(kind, day, false, kind == EnemyKind.Stomper ? StomperShield : 0f, ShieldDirect);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCaseSource(nameof(EliteHpCases))]
        public void BodyHp_EliteOnDay_IsTheLedgersEliteHpByDay(EnemyKind kind, int day, int expected)
        {
            var sut = CreateRules();

            float actual = sut.BodyHp(kind, day, true, kind == EnemyKind.Stomper ? StomperShield : 0f, ShieldDirect);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void BodyHp_MiniSplitterOnDaySeven_IsItsShareOfASplitter()
        {
            var sut = CreateRules();

            float actual = sut.BodyHp(EnemyKind.Splitter, 7, false, 0f, ShieldDirect, .4f);

            Assert.That(actual, Is.EqualTo(87f));
        }

        [Test]
        public void BodyHp_DayZero_IsDayOnes()
        {
            var sut = CreateRules();

            float actual = sut.BodyHp(EnemyKind.Grunt, 0, false, 0f, ShieldDirect);

            Assert.That(actual, Is.EqualTo(4f));
        }

        [Test]
        public void BodyHp_InTheTyrantsFight_UsesItsReferenceDps()
        {
            var sut = CreateRules();

            float actual = sut.BodyHp(EnemyKind.Grunt, 7, false, 0f, ShieldDirect, 1f, true);

            Assert.That(actual, Is.EqualTo(Mathf.Round(930f * .375f)));
        }

        [TestCase(EnemyKind.Hollow, 1, 8200f)]
        [TestCase(EnemyKind.Hollow, 7, 8200f)]
        [TestCase(EnemyKind.Tyrant, 7, 43000f)]
        public void BodyHp_Boss_IsFixedWhateverTheDay(EnemyKind kind, int day, float expected)
        {
            var sut = CreateRules();

            float actual = sut.BodyHp(kind, day, false, 0f, ShieldDirect);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void HoleCheckRequirement_ByDefault_IsHalfTheCampaignsProphet()
        {
            var sut = CreateRules();

            float actual = CampaignRules.HoleCheckRequirement(sut.holeCheckDamage, sut.holeCheckProphetShare, sut.BodyHp(EnemyKind.Hollow, 5, false, 0f, 1f));

            Assert.That(actual, Is.EqualTo(4100f));
        }

        [Test]
        public void TyrantShields_BothShares_AddUpToAboutTheLedgersShield()
        {
            var sut = CreateRules();

            float actual = sut.tyrantHp * (sut.tyrantStartShieldShare + sut.tyrantPhaseShieldShare);

            Assert.That(actual, Is.EqualTo(12509f).Within(10f));
        }

        // ───── Skills, statuses, payoffs ─────

        [TestCase(0, false, 0f)]
        [TestCase(1, false, 1.1f)]
        [TestCase(4, false, 2.7f)]
        [TestCase(7, false, 16f)]
        [TestCase(9, false, 16f)]
        [TestCase(7, true, 24f)]
        public void CombatScaleOn_DayOrTyrantFight_IsTheTable(int day, bool tyrantFight, float expected)
        {
            var sut = CreateRules();

            float actual = sut.CombatScaleOn(day, tyrantFight);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        [TestCase(0, 1f)]
        [TestCase(1, .85f)]
        [TestCase(3, .457f)]
        [TestCase(9, .064f)]
        public void PayoffScaleOn_Day_IsTheTable(int day, float expected)
        {
            var sut = CreateRules();

            float actual = sut.PayoffScaleOn(day);

            Assert.That(actual, Is.EqualTo(expected).Within(.0001f));
        }

        // ───── The day's kill energy ─────

        // A day's bodies in the order they die, as seconds to kill: a melee swarm with specials and elites among it.
        static float[] DayBodies(int count)
        {
            var bodies = new float[count];
            for (int i = 0; i < count; i++) bodies[i] = i % 11 == 10 ? 6f : i % 4 == 3 ? 1.8f : .375f;
            return bodies;
        }

        /// <summary>Pays a day's kills: each kill sees the bodies still to come as their true weight times misjudge (the last kill sees none).</summary>
        static int PayDay(int purse, float[] bodies, float misjudge, int seed)
        {
            var random = new System.Random(seed);
            int paid = 0, left = purse;
            for (int i = 0; i < bodies.Length; i++)
            {
                float rest = 0f;
                for (int j = i + 1; j < bodies.Length; j++) rest += bodies[j];
                int share = CampaignRules.KillEnergyShare(left, bodies[i], rest * misjudge, (float)random.NextDouble());
                left -= share;
                paid += share;
            }
            return paid;
        }

        [TestCase(1, 1f)]
        [TestCase(2, 1f)]
        [TestCase(3, 3f)]
        [TestCase(4, .3f)]
        [TestCase(5, 1f)]
        [TestCase(6, 2f)]
        [TestCase(7, .5f)]
        public void KillEnergyShare_EveryMonsterOfTheDayKilled_PaysExactlyTheDaysTable(int day, float misjudge)
        {
            var sut = CreateRules();
            int purse = sut.DayKillEnergy(day);

            int actual = PayDay(purse, DayBodies(40 + 15 * day), misjudge, day);

            Assert.That(actual, Is.EqualTo(new[] { 20, 32, 52, 80, 120, 176, 240 }[day - 1]));
        }

        [Test]
        public void KillEnergyShare_NothingStillToCome_DropsAllThatIsLeft()
        {
            int actual = CampaignRules.KillEnergyShare(17, .375f, 0f, .99f);

            Assert.That(actual, Is.EqualTo(17));
        }

        [Test]
        public void KillEnergyShare_HeavierBody_DropsMoreThanALighterOne()
        {
            int light = CampaignRules.KillEnergyShare(100, .375f, 50f, 0f);
            int heavy = CampaignRules.KillEnergyShare(100, 6f, 50f, 0f);

            Assert.That(heavy, Is.GreaterThan(light));
        }

        [TestCase(1, 5)]
        [TestCase(7, 60)]
        [TestCase(9, 60)]
        public void NightEnergy_SharedOverEightNodes_AddsUpToTheTable(int night, int expected)
        {
            var sut = CreateRules();

            int actual = System.Linq.Enumerable.Sum(CampaignRules.Split(sut.NightEnergy(night), 8));

            Assert.That(actual, Is.EqualTo(expected));
        }

        // ───── Stat lines ─────

        [TestCase(0, CardRarity.Common)]
        [TestCase(2, CardRarity.Exceptional)]
        [TestCase(3, CardRarity.Epic)]
        [TestCase(4, CardRarity.Legendary)]
        public void StatLineRarity_StepsBought_IsTheNextColour(int steps, CardRarity expected)
        {
            var actual = CampaignRules.StatLineRarity(steps);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void StatLineRarity_LineFull_IsNull()
        {
            var actual = CampaignRules.StatLineRarity(5);

            Assert.That(actual, Is.Null);
        }

        [TestCase(StatId.Damage, .5f)]
        [TestCase(StatId.CritChance, .2f)]
        [TestCase(StatId.MaxEnergy, 40f)]
        public void StatLineStep_EveryStepFromWhiteToOrange_EndsAtTheOrangeValueNotTheSum(StatId stat, float white)
        {
            var values = new StatCardValues(stat, white);
            var stats = new RunStats { damageBonus = 0f, critChance = 0f, maxEnergy = 0f, critCap = 10f };
            stats.caps = null;
            float from = 0f;
            for (int steps = 0; steps < 5; steps++)
            {
                var rarity = CampaignRules.StatLineRarity(steps) ?? CardRarity.Legendary;
                stats.Add(stat, CampaignRules.StatLineStep(stat, from, values.At(rarity)));
                from = values.At(rarity);
            }

            float actual = stats.Value(stat);

            Assert.That(actual, Is.EqualTo(values.legendary).Within(.0001f));
        }

        [Test]
        public void StatLineMode_ByDefault_IsReplace()
        {
            var sut = CreateRules();

            var actual = sut.statLineMode;

            Assert.That(actual, Is.EqualTo(StatLineMode.Replace));
        }

        // ───── Day 0's gifts ─────

        [Test]
        public void DayZeroOreAlloy_TwoOresOnTheWay_ShareTenLessTheArrivalsTwo()
        {
            var sut = CreateRules();

            var actual = sut.DayZeroOreAlloy(2);

            Assert.That(actual, Is.EqualTo(new[] { 4, 4 }));
        }

        [Test]
        public void DayZeroArrival_BothOresMinedAndFourEnergyPickedUp_GivesTwoAlloyAndElevenEnergy()
        {
            var sut = CreateRules();

            var actual = sut.DayZeroArrival(8, 4);

            Assert.That(actual, Is.EqualTo(new CampaignCost(2, 11)));
        }

        [Test]
        public void DayZeroArrival_SecondOreSkipped_StillMakesTenAlloy()
        {
            var sut = CreateRules();

            var actual = sut.DayZeroArrival(4, 4);

            Assert.That(actual, Is.EqualTo(new CampaignCost(6, 11)));
        }

        [Test]
        public void TutorialSecondCrateWave_ByDefault_IsWaveTwo()
        {
            var sut = CreateRules();

            int actual = sut.tutorialSecondCrateWave;

            Assert.That(actual, Is.EqualTo(2));
        }
    }
}
