using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The black hole's timed damage check and the day tables of 2026-11-10 as plain rules, apart from the scene: the check's clock,
    /// requirement and answers (HoleCheck), the requirement's default (half the Nash Prophet's HP), the pillars of an answer, and how the
    /// afternoon's alloy and the night's dark matter are shared over the day's nodes (CampaignRules).
    /// </summary>
    [TestFixture]
    public class HoleCheckRulesTest
    {
        static HoleCheck NewCheck() => new() { CounterDelay = 3f, IntervalStart = 6f, IntervalEnd = 4f };

        [Test]
        public void Check_BeforeTheFirstHit_IsIdleAndItsClockDoesNotRun()
        {
            var check = NewCheck();
            check.Reset();

            Assert.That(check.Tick(100f), Is.EqualTo(HoleCounterMove.None));
            Assert.That(check.State, Is.EqualTo(HoleCheckState.Idle), "no hit, no clock");
        }

        [Test]
        public void Check_FirstHitStartsTheClock_AndReachingTheRequirementInTimePasses()
        {
            var check = NewCheck();
            check.Reset();

            Assert.That(check.Hit(40f, 100f, 45f), Is.False, "the first hit starts the check");
            Assert.That(check.State, Is.EqualTo(HoleCheckState.Running));
            Assert.That(check.Progress, Is.EqualTo(.4f).Within(1e-4f));
            check.Tick(30f);
            Assert.That(check.SecondsLeft, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(check.Hit(60f, 999f, 1f), Is.True, "the hit that reaches the first requirement passes");
            Assert.That(check.State, Is.EqualTo(HoleCheckState.Passed));
            Assert.That(check.Required, Is.EqualTo(100f), "later hits keep the first requirement");
        }

        [Test]
        public void Check_TimeRunsOutShortOfTheRequirement_Fails_AndLaterHitsCountNothing()
        {
            var check = NewCheck();
            check.Reset();
            check.Hit(10f, 100f, 45f);

            check.Tick(45f);

            Assert.That(check.State, Is.EqualTo(HoleCheckState.Failed));
            Assert.That(check.Hit(500f, 100f, 45f), Is.False);
            Assert.That(check.State, Is.EqualTo(HoleCheckState.Failed), "a failed day stays failed");
        }

        [Test]
        public void Check_Fail_EndsOnlyARunningCheck_AndResetMakesItNewForTheNextDay()
        {
            var check = NewCheck();
            check.Reset();
            check.Fail();
            Assert.That(check.State, Is.EqualTo(HoleCheckState.Idle), "a fall before the first hit fails nothing");

            check.Hit(1f, 100f, 45f);
            check.Fail();
            Assert.That(check.State, Is.EqualTo(HoleCheckState.Failed));

            check.Reset();
            Assert.That(check.State, Is.EqualTo(HoleCheckState.Idle));
            Assert.That(check.Dealt, Is.Zero, "the next day's damage starts from zero");
        }

        [Test]
        public void Check_Answers_StartAfterTheDelay_AndTakeTurnsPillarsThenRing()
        {
            var check = NewCheck();
            check.Reset();
            check.Hit(1f, 1000f, 45f);

            Assert.That(check.Tick(2.9f), Is.EqualTo(HoleCounterMove.None), "nothing in the first 3 s");
            Assert.That(check.Tick(.2f), Is.EqualTo(HoleCounterMove.Pillars), "pillars first");
            Assert.That(check.Tick(5.8f), Is.EqualTo(HoleCounterMove.None), "about 6 s apart while hardly any damage is dealt");
            Assert.That(check.Tick(.2f), Is.EqualTo(HoleCounterMove.Ring), "then the ring");
        }

        [TestCase(0f, 6f)]
        [TestCase(.5f, 5f)]
        [TestCase(.99f, 4.02f)]
        public void Check_Interval_ShrinksFromSixToFourWithTheDamageShare(float share, float expected)
        {
            var check = NewCheck();
            check.Reset();
            check.Hit(share * 100f + 1e-4f, 100f, 45f);

            Assert.That(check.Interval, Is.EqualTo(expected).Within(.01f));
        }

        [Test]
        public void Requirement_DefaultsToHalfTheProphetsHp_AndASetDamageWins()
        {
            Assert.That(CampaignRules.HoleCheckRequirement(0f, .5f, 14000f), Is.EqualTo(7000f));
            Assert.That(CampaignRules.HoleCheckRequirement(2500f, .5f, 14000f), Is.EqualTo(2500f));
            Assert.That(CampaignRules.HoleCheckRequirement(0f, .5f, 0f), Is.EqualTo(1f), "never below 1");
        }

        [TestCase(0f, 3)]
        [TestCase(.5f, 4)]
        [TestCase(1f, 5)]
        public void Pillars_GrowFromThreeToFiveWithTheDamageShare(float share, int expected)
        {
            Assert.That(CampaignRules.HolePillarCount(3, 5, share), Is.EqualTo(expected));
        }

        [Test]
        public void PillarSpots_FirstOnThePlayer_TheOthersAtTheSpreadAround()
        {
            var player = new Vector3(4f, 0f, -2f);

            var spots = CampaignRules.HolePillarSpots(player, 5, 5.5f, 30f);

            Assert.That(spots.Count, Is.EqualTo(5));
            Assert.That((spots[0] - player).magnitude, Is.LessThan(1e-4f));
            for (int i = 1; i < spots.Count; i++) Assert.That((spots[i] - player).magnitude, Is.EqualTo(5.5f).Within(1e-3f));
        }

        [TestCase(1, 1f, 20)]
        [TestCase(7, 1f, 240)]
        [TestCase(9, 1f, 240)]
        [TestCase(2, 1.3f, 39)]
        [TestCase(3, 1.3f, 59)]
        [TestCase(5, 1.3f, 137)]
        [TestCase(7, 1.3f, 312)]
        [TestCase(0, 1f, 0)]
        public void AfternoonAlloy_FollowsTheTableByDay_LaterDaysTakeTheLast_TimesTheShipBonus(int day, float scale, int expected)
        {
            int[] table = { 20, 30, 45, 70, 105, 160, 240 };

            Assert.That(CampaignRules.AfternoonAlloyTotal(table, day, scale), Is.EqualTo(expected));
        }

        [TestCase(1, 10)]
        [TestCase(5, 28)]
        [TestCase(7, 45)]
        [TestCase(8, 45)]
        public void NightDark_FollowsTheTable_OvertimeTakesTheSeventhNight(int day, int expected)
        {
            int[] table = { 10, 12, 15, 18, 28, 36, 45 };

            Assert.That(CampaignRules.ByDay(table, day), Is.EqualTo(expected));
        }

        [Test]
        public void Split_SharesEvenly_TheRemainderToTheFirst_AndAddsUpToTheTotal()
        {
            var parts = CampaignRules.Split(20, 6);

            Assert.That(parts, Is.EqualTo(new[] { 4, 4, 3, 3, 3, 3 }));
            int sum = 0;
            foreach (int part in parts) sum += part;
            Assert.That(sum, Is.EqualTo(20));
            Assert.That(CampaignRules.Split(5, 0), Is.Empty);
        }

        [Test]
        public void HolePull_IsALittleFasterThanTheWalk_AndSlowerThanTheSprint()
        {
            const float walk = 6f, sprint = walk * 2f;
            var rules = ScriptableObject.CreateInstance<CampaignRules>();

            float pull = CampaignRules.HolePull(rules.holePullFactor, walk);
            Object.DestroyImmediate(rules);

            Assert.That(pull, Is.GreaterThan(walk), "a walking player in the pull loses ground");
            Assert.That(pull, Is.LessThan(walk * 1.25f), "but slowly");
            Assert.That(pull, Is.LessThan(sprint), "and a sprint gets out");
        }

        [TestCase(4, 6, 0, ExpectedResult = 4, TestName = "No pack monster alive: a full pack")]
        [TestCase(4, 6, 4, ExpectedResult = 2, TestName = "Four alive of six at most: two more")]
        [TestCase(4, 6, 6, ExpectedResult = 0, TestName = "As many alive as may be: none")]
        [TestCase(4, 6, 9, ExpectedResult = 0, TestName = "More alive than may be: none")]
        [TestCase(0, 6, 0, ExpectedResult = 0, TestName = "A pack of none brings none")]
        public int HoleLureCount_FillsUpToTheMostAlive(int pack, int mostAlive, int alive) => CampaignRules.HoleLureCount(pack, mostAlive, alive);

        [Test]
        public void HoleFeedGoal_ByDefault_IsTenMonsters()
        {
            var rules = ScriptableObject.CreateInstance<CampaignRules>();

            int goal = rules.holeFeedGoal;
            Object.DestroyImmediate(rules);

            Assert.That(goal, Is.EqualTo(10));
        }
    }
}
