using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The night's shadow rules (V2 doc section 5): how many shadows a region holds, how fast a hunting shadow is against the
    /// player's walk, when the shadows at a lamp are a crowd that goes into its light, and how light burns a shadow away. The numbers are
    /// the rules' defaults.</summary>
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
        public void ShadowChaseSpeed_IsALittleSlowerThanTheWalk()
        {
            const float walk = 6f;

            float chase = rules.ShadowChaseSpeed(walk);

            Assert.That(chase, Is.LessThan(walk), "a walking player is never caught from behind");
            Assert.That(chase, Is.GreaterThan(walk * .75f), "but a player who stands still is soon reached");
        }

        [TestCase(6f, TestName = "Against the usual walk")]
        [TestCase(1f, TestName = "Against a very slow player")]
        public void ShadowRoamSpeed_IsNeverAboveTheHuntOrTheWalk(float walk)
        {
            Assert.That(rules.ShadowRoamSpeed(walk), Is.LessThanOrEqualTo(rules.ShadowChaseSpeed(walk)), "roaming is no faster than hunting");
            Assert.That(rules.ShadowRoamSpeed(walk), Is.LessThan(walk), "and no shadow moves faster than the player walks");
        }

        [Test]
        public void ShadowChaseSpeed_AFactorAboveOne_IsHeldToTheWalk()
        {
            rules.shadowChaseFactor = 1.5f;

            Assert.That(rules.ShadowChaseSpeed(6f), Is.LessThanOrEqualTo(6f), "a share above 1 set by hand still cannot outrun the walk");
        }

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

        [Test]
        public void TutorialShadowHitScale_HurtsButLessThanANormalTouch() =>
            Assert.That(rules.tutorialShadowHitScale, Is.GreaterThan(0f).And.LessThan(1f));

        // ───── Burning in light (CampaignRules.ShadowExposure, ShadowBurnedAway, ShadowBurnShare) ─────

        [Test]
        public void ShadowLightSeconds_AreThree() => Assert.That(rules.shadowLightSeconds, Is.EqualTo(3f));

        [Test]
        public void ShadowExposure_InLightWithoutABreak_BurnsAShadowAwayAtTheLightSeconds()
        {
            const float frame = 1f / 60f;
            float exposure = 0f, seconds = 0f;

            while (!rules.ShadowBurnedAway(exposure) && seconds < 10f)
            {
                exposure = CampaignRules.ShadowExposure(exposure, true, frame);
                seconds += frame;
            }

            Assert.That(seconds, Is.EqualTo(rules.shadowLightSeconds).Within(frame * 1.5f), "it dissolves after three seconds in light, frame by frame");
        }

        [TestCase(0f, ExpectedResult = false, TestName = "A shadow the light has not touched is whole")]
        [TestCase(2.9f, ExpectedResult = false, TestName = "After 2.9 seconds in light it is still there")]
        [TestCase(3f, ExpectedResult = true, TestName = "After 3 seconds in light it is gone")]
        public bool ShadowBurnedAway_OnlyOnceItHasTakenTheLightSeconds(float exposure) => rules.ShadowBurnedAway(exposure);

        [Test]
        public void ShadowExposure_OutOfTheLight_DoesNotHeal() =>
            Assert.That(CampaignRules.ShadowExposure(1.5f, false, 20f), Is.EqualTo(1.5f), "twenty seconds in the dark give nothing back");

        [Test]
        public void ShadowExposure_ABackwardStepOfTime_GivesNothingBack() =>
            Assert.That(CampaignRules.ShadowExposure(2f, true, -1f), Is.EqualTo(2f));

        [Test]
        public void ShadowExposure_ThreeShortStaysInLight_AddUp_AndTheThirdBurnsItAway()
        {
            float exposure = 0f;

            for (int stay = 1; stay <= 3; stay++)
            {
                Assert.That(rules.ShadowBurnedAway(exposure), Is.False, $"still there before stay {stay}");
                exposure = CampaignRules.ShadowExposure(exposure, true, 1f);   // a second in a lamp's light
                exposure = CampaignRules.ShadowExposure(exposure, false, 10f); // then ten in the dark
            }

            Assert.That(rules.ShadowBurnedAway(exposure), Is.True, "three separate seconds of light burn it away like three in one go");
        }

        [TestCase(0f, ExpectedResult = 0f, TestName = "Untouched, the body is whole")]
        [TestCase(1.5f, ExpectedResult = .5f, TestName = "Half the light seconds fade it halfway")]
        [TestCase(3f, ExpectedResult = 1f, TestName = "All of them and it is gone")]
        [TestCase(6f, ExpectedResult = 1f, TestName = "Never more than gone")]
        public float ShadowBurnShare_FollowsTheLightTaken(float exposure) => rules.ShadowBurnShare(exposure);

        [Test]
        public void LampCrowdWarning_ByDefault_GivesThreeSecondsBeforeACrowdGoesIn() =>
            Assert.That(rules.lampCrowdWarning, Is.EqualTo(3f), "a crowd at a lamp shows itself for three seconds, time to move or set another lamp");

        [TestCase(2, ExpectedResult = false, TestName = "Two shadows at the player's lamp are not named yet")]
        [TestCase(3, ExpectedResult = true, TestName = "Three are named in the note")]
        [TestCase(5, ExpectedResult = true, TestName = "A full crowd is named too")]
        public bool LampCrowdNoticed_FromThreeShadows_TheNoteNamesThem(int shadows) => rules.LampCrowdNoticed(shadows);

        [TestCase(0, ExpectedResult = 0f, TestName = "No shadow: the ring keeps its warm colour")]
        [TestCase(3, ExpectedResult = .6f, TestName = "Three of five: the ring is three fifths of the way to violet")]
        [TestCase(5, ExpectedResult = 1f, TestName = "A crowd: the ring is violet")]
        [TestCase(8, ExpectedResult = 1f, TestName = "Never past violet")]
        public float LampCrowdShare_ShadowsGathered_AreTheirShareOfACrowd(int shadows) => rules.LampCrowdShare(shadows);

        [Test]
        public void LampEdgeColour_FromNoShadowToACrowd_TurnsFromWarmToViolet()
        {
            Color none = V2Campaign.LampEdgeColour(0f), some = V2Campaign.LampEdgeColour(rules.LampCrowdShare(3)), crowd = V2Campaign.LampEdgeColour(1f);

            Assert.That(none.r > none.b && crowd.b > crowd.r, Is.True, "warm with no shadow, violet with a crowd");
            Assert.That(some.b, Is.GreaterThan(none.b).And.LessThan(crowd.b), "three of five on the way: bluer than warm, not yet the crowd's violet");
            Assert.That(some.g, Is.LessThan(none.g).And.GreaterThan(crowd.g), "and its green fading on the way");
        }

        [Test]
        public void Crowd_ThatGoesIntoALampTogether_WearsItOutBeforeTheLightBurnsItAway() =>
            Assert.That(V2Campaign.LampSecondsAgainst(rules.lampHp, rules.overrunShadows), Is.LessThan(rules.shadowLightSeconds),
                "the smallest crowd puts a lamp out before it dissolves, so a crowd still puts lamps out");

        // ───── Keeping out of the light (V2Campaign.KeptOutOf), a lamp of radius 6.5 at the origin ─────

        [Test]
        public void KeptOutOf_AStepOutsideTheLight_IsUnchanged()
        {
            Vector3 from = new(10f, 0f, 0f), next = new(10f, 0f, 1f);

            Assert.That(V2Campaign.KeptOutOf(Vector3.zero, 6.5f, from, next, 4f), Is.EqualTo(next));
        }

        [Test]
        public void KeptOutOf_AStepIntoTheLightFromItsEdge_GoesRoundTheEdgeByTheWholeStep()
        {
            // On the edge, one cell nearly straight in, leaning a little to one side (as toward a player behind the light).
            Vector3 from = new(6.5f, 0f, 0f), next = from + new Vector3(-.95f, 0f, .31f);

            Vector3 kept = V2Campaign.KeptOutOf(Vector3.zero, 6.5f, from, next, 4f);

            Assert.That(kept.magnitude, Is.EqualTo(6.5f).Within(1e-3f), "it stays on the edge");
            Assert.That(Vector3.Distance(kept, from), Is.EqualTo(1f).Within(.05f), "by about the whole step, not only its sideways part");
            Assert.That(kept.z, Is.GreaterThan(0f), "to the side the step leaned to");
        }

        [Test]
        public void KeptOutOf_CaughtWellInside_BacksStraightOut()
        {
            Vector3 from = new(2f, 0f, 0f);

            Vector3 kept = V2Campaign.KeptOutOf(Vector3.zero, 6.5f, from, from, 4f);

            Assert.That(kept.x, Is.EqualTo(2f + 4f * 1.6f).Within(1e-4f));
            Assert.That(kept.z, Is.EqualTo(0f).Within(1e-6f));
        }
    }
}
