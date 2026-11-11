using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The night's shadow rules (V2 doc section 5): how many shadows a region holds, how fast a hunting shadow is against the
    /// player's walk, and when the shadows at a lamp are a crowd that goes into its light. The numbers are the rules' defaults.</summary>
    [TestFixture]
    public class NightShadowRulesTest
    {
        CampaignRules rules;

        [SetUp]
        public void Create() => rules = ScriptableObject.CreateInstance<CampaignRules>();

        [TearDown]
        public void Remove() => Object.DestroyImmediate(rules);

        [TestCase(1, ExpectedResult = 1, TestName = "Day 1: one shadow in every region")]
        [TestCase(4, ExpectedResult = 1, TestName = "Day 4: still one")]
        [TestCase(7, ExpectedResult = 1, TestName = "Day 7: still one")]
        public int AmbientShadows_EveryRegionHoldsOne_OnEveryDay(int day) => rules.AmbientShadows(day);

        [Test]
        public void AmbientShadows_WithGrowthByDay_AreMoreOnLaterDays()
        {
            rules.ambientShadowsPerDay = .5f;

            Assert.That(rules.AmbientShadows(1), Is.EqualTo(1), "the first day has the base number");
            Assert.That(rules.AmbientShadows(5), Is.EqualTo(3), "four days later it has grown by two");
        }

        [Test]
        public void AmbientShadows_OfThreeNeighbouringRegions_AreNoCrowd() =>
            Assert.That(rules.Crowd(rules.AmbientShadows(7) * 3), Is.False, "a lamp where nothing was killed holds on the last day too");

        [TestCase(0, ExpectedResult = 0, TestName = "No kills raise no shadow")]
        [TestCase(3, ExpectedResult = 0, TestName = "Three kills are not yet one shadow")]
        [TestCase(4, ExpectedResult = 1, TestName = "Four kills raise one shadow")]
        [TestCase(9, ExpectedResult = 2, TestName = "Nine kills raise two")]
        [TestCase(400, ExpectedResult = 8, TestName = "A region's kills raise at most eight")]
        public int KillShadows_MoreKillsRaiseMoreShadows_UpToTheRegionsCap(int kills) => rules.KillShadows(kills);

        [Test]
        public void ShadowChaseSpeed_IsALittleFasterThanTheWalk_AndSlowerThanTheSprint()
        {
            const float walk = 6f, sprint = walk * 2f;

            float chase = rules.ShadowChaseSpeed(walk);

            Assert.That(chase, Is.GreaterThan(walk), "a walking player is caught");
            Assert.That(chase, Is.LessThan(walk * 1.3f), "but only a little faster");
            Assert.That(chase, Is.LessThan(sprint), "and a sprint opens the distance");
        }

        [Test]
        public void ShadowChaseSpeed_AgainstAVerySlowPlayer_IsNeverSlowerThanRoaming() =>
            Assert.That(rules.ShadowChaseSpeed(1f), Is.EqualTo(rules.shadowSpeed));

        [TestCase(4, ExpectedResult = false, TestName = "Four shadows at a lamp wait at its edge")]
        [TestCase(5, ExpectedResult = true, TestName = "Five are a crowd and go in")]
        [TestCase(9, ExpectedResult = true, TestName = "Nine are a crowd")]
        public bool Crowd_EnoughShadowsAtOneLamp_GoIntoItsLight(int shadows) => rules.Crowd(shadows);

        [Test]
        public void Lights_TheBasesLightIsWiderThanALamps_AndReachesPastTheBaseWalls()
        {
            Assert.That(rules.baseLightRadius, Is.GreaterThan(rules.lampRadius), "the base's light is the larger one");
            Assert.That(rules.baseLightRadius, Is.GreaterThan(V2Campaign.BaseHalf * 1.42f + 5f), "it lights the ground well past the base's corners");
        }

        [Test]
        public void ShadowSense_ReachesPastEveryLight_SoShadowsComeToItsEdge() =>
            Assert.That(rules.shadowSense, Is.GreaterThan(rules.baseLightRadius + rules.lampGatherBand), "a shadow standing by a light senses a player at its middle");

        [TestCase(10f, 12f, ExpectedResult = true, TestName = "A player it senses inside its territory is hunted")]
        [TestCase(10f, 30f, ExpectedResult = false, TestName = "A player outside its territory is left alone, however near the shadow is")]
        [TestCase(40f, 12f, ExpectedResult = false, TestName = "A player it cannot sense is left alone inside its territory too")]
        public bool ShadowHunts_OnlyAPlayerItSensesInsideItsTerritory(float shadowToPlayer, float homeToPlayer) => rules.ShadowHunts(shadowToPlayer, homeToPlayer);

        [Test]
        public void ShadowTerritory_IsNarrowerThanARegion_AndReachesALampSetDownInIt()
        {
            float regionWidth = rules.mapSize.x / (float)rules.regions.x;

            Assert.That(rules.shadowTerritory, Is.LessThan(regionWidth), "a shadow does not follow the player across a whole region");
            Assert.That(rules.shadowTerritory, Is.GreaterThan(rules.lampRadius + rules.lampGatherBand), "it reaches a lamp set down near the place it rose at");
        }

        // A grid six regions wide: region 0 is the first of the first row, region 6 the first of the second.
        [TestCase(0, 1, ExpectedResult = true, TestName = "Regions side by side touch")]
        [TestCase(0, 6, ExpectedResult = true, TestName = "Regions one above the other touch")]
        [TestCase(0, 7, ExpectedResult = true, TestName = "Regions corner to corner touch")]
        [TestCase(0, 2, ExpectedResult = false, TestName = "Regions with one between them do not touch")]
        [TestCase(5, 6, ExpectedResult = false, TestName = "The end of one row and the start of the next do not touch")]
        [TestCase(8, 8, ExpectedResult = false, TestName = "A region does not touch itself")]
        public bool RegionsTouch_OnlyNeighboursOfTheGrid(int a, int b) => CampaignRules.RegionsTouch(a, b, 6);
    }
}
