using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The story's pacing rules that need no scene: the kill energy's chance and pity, the lamps' alloy by day, a map hole's wider ring and
    /// its pull against the player's walk, and the monsters' walk under that pull.
    /// </summary>
    [TestFixture]
    public class StoryPacingRulesTest
    {
        CampaignRules rules;
        GameObject holeObject;

        [SetUp]
        public void SetUp() => rules = ScriptableObject.CreateInstance<CampaignRules>();

        [TearDown]
        public void TearDown()
        {
            if (rules) Object.DestroyImmediate(rules);
            if (holeObject) Object.DestroyImmediate(holeObject);
        }

        [TestCase(0, .99f, ExpectedResult = false, TestName = "A roll over the chance drops nothing")]
        [TestCase(0, .1f, ExpectedResult = true, TestName = "A roll under the chance drops")]
        [TestCase(2, .99f, ExpectedResult = false, TestName = "Two misses in a row: still by chance")]
        [TestCase(3, .99f, ExpectedResult = true, TestName = "Three misses in a row: the next always drops")]
        public bool KillDrops_ByChance_AndAlwaysAfterThePity(int missesInARow, float roll) => CampaignRules.KillDrops(.35f, missesInARow, 3, roll);

        [Test]
        public void DropChance_AnEliteAlways_ASupportMoreOftenThanABasic_ASmallSplitterLessOften()
        {
            Assert.That(rules.DropChance(EnemyKind.Grunt, true, false), Is.EqualTo(1f), "an elite always drops");
            Assert.That(rules.DropChance(EnemyKind.Warden, false, false), Is.GreaterThan(rules.DropChance(EnemyKind.Grunt, false, false)),
                "a support monster drops more often than a basic one");
            Assert.That(rules.DropChance(EnemyKind.Splitter, false, true), Is.LessThan(rules.DropChance(EnemyKind.Splitter, false, false)),
                "a Splitter's small one drops less often than a whole one");
        }

        [Test]
        public void LampAlloy_NoneOnDayZero_WholeLampsRisingByDay()
        {
            Assert.That(rules.LampAlloyOn(0), Is.Zero, "day 0's tutorial night has its lamps in the store");
            Assert.That(rules.dailyFreeLamps, Is.Zero, "no lamp is handed out after the tutorial night");
            int previous = 0;
            for (int day = 1; day <= 7; day++)
            {
                int alloy = rules.LampAlloyOn(day);
                Assert.That(alloy % rules.lampCost, Is.Zero, $"day {day}: alloy for whole lamps");
                Assert.That(alloy, Is.GreaterThanOrEqualTo(previous), $"day {day}: never fewer lamps than the day before");
                previous = alloy;
            }
            Assert.That(rules.LampAlloyOn(7), Is.GreaterThan(rules.LampAlloyOn(1)), "more lamps on the last day than on the first");
        }

        [Test]
        public void MapHole_RingIsWiderThanAMomentsHole()
        {
            holeObject = new GameObject("StoryPacingRulesTest hole");
            var hole = holeObject.AddComponent<BlackHole>();
            hole.size = 7f;
            hole.gravityCells = 3f;

            Assert.That(hole.GravityRadius, Is.EqualTo(3.5f + 3f * BlackHole.MapRingScale).Within(.001f), "a map hole's ring is MapRingScale times as wide");

            hole.MakeTemporary(Vector3.zero, 7f, 5f, null, false, 3f);

            Assert.That(hole.GravityRadius, Is.EqualTo(3.5f + 3f).Within(.001f), "a hole made for a moment keeps its own width");
        }

        [TestCase(6f, TestName = "At the starting walk")]
        [TestCase(7.5f, TestName = "At a faster walk")]
        public void Pull_FasterThanAWalk_SlowerThanASprint_FasterThanEveryMonster(float walk)
        {
            float pull = BlackHole.MapPull(walk);

            Assert.That(pull, Is.GreaterThan(walk), "walking out of the ring loses ground");
            Assert.That(pull, Is.LessThan(walk * 2f), "a sprint (twice the walk) gets out");
            Assert.That(rules.monsterWalkCap * walk, Is.LessThan(pull), "no monster walks out of the pull");
        }
    }
}
