using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The story's raiders on its own map with its terrain: a body of each width, set down on every bearing of both spawn rings and
    /// walked by the game's own rules (the raid flow field, then WorldMap.WalkStep), reaches the base without ever standing still. The
    /// walk is stepped here without physics, so one test covers the whole map of a run in a moment; the run's map seed is in every
    /// message. The same walk under the rule before the fix (the first refused step stops the body) is counted and logged beside it.
    /// </summary>
    [TestFixture]
    public class RaidWalkTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const float Step = .02f, Speed = 5f;
        const int MaxSteps = 9000, StallSteps = 25;

        [TestCase(.35f), TestCase(.5f), TestCase(.7f), TestCase(1f)]
        [LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Raiders_FromEveryBearingOfBothRings_ReachTheBaseWithoutStandingStill(float radius)
        {
            var campaign = await StartStory(0);
            var map = WorldMap.Active;
            string where = $"map seed {campaign.MapSeed}, radius {radius}";

            int starts = 0, stoodBefore = 0;
            foreach (float ring in new[] { campaign.rules.nearSpawnRing, campaign.rules.daySpawnRing })
                for (int bearing = 0; bearing < 360; bearing += 10)
                {
                    Vector3 dir = Quaternion.Euler(0f, bearing, 0f) * Vector3.forward;
                    Vector3 half = map.Bounds.extents;
                    float reach = Mathf.Min(ring, Mathf.Min(Mathf.Abs(dir.x) > .001f ? (half.x - 3f) / Mathf.Abs(dir.x) : float.MaxValue,
                        Mathf.Abs(dir.z) > .001f ? (half.z - 3f) / Mathf.Abs(dir.z) : float.MaxValue));
                    Vector3 start = V2Campaign.FindOpen(V2Campaign.BaseCentre + dir * reach, radius + .2f);
                    if (!map.IsWalkable(start, radius + .1f)) continue;
                    starts++;
                    Vector3 spot = StandPoint(start, radius);

                    if (!Walk(map, start, spot, radius, false, out Vector3 end, out int stall, out int steps))
                        Assert.Fail($"{where}: a raider set down at {start} (ring {ring}, bearing {bearing}) for the stand point {spot} "
                                    + (stall >= StallSteps ? $"stood still at {end}" : $"did not reach the base in {steps} steps (it is at {end})"));
                    if (!Walk(map, start, spot, radius, true, out _, out _, out _)) stoodBefore++;
                }
            Assert.That(starts, Is.GreaterThan(40), $"{where}: most bearings give a place to set a raider down");
            Debug.Log($"[RaidWalk] {where}: {starts} raiders walked to the base; under the rule before the fix {stoodBefore} of them stood still on the way");
        }

        // A play-through of the story on this map (2026-11-22) found these raiders standing still at a wall, far from the base and out of
        // the player's sight, so that the morning could not end: a Brute on day 1, a Brute and a Spitter on day 2.
        const int PlayedSeed = 47316146;
        static readonly (float radius, Vector3 at, Vector3 spot)[] PlayedRaiders =
        {
            (1f, new Vector3(-31.6f, 0f, -21.2f), new Vector3(-6.4f, 0f, -4f)),
            (1f, new Vector3(48.4f, 0f, -25.8f), new Vector3(6.4f, 0f, -4f)),
            (.5f, new Vector3(8.7f, 0f, 44.5f), new Vector3(4f, 0f, 5.9f)),
        };

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Raiders_WhereAPlayedMorningLeftThemStanding_WalkOnToTheBase()
        {
            var campaign = await StartStory(PlayedSeed);
            Assume.That(campaign.MapSeed, Is.EqualTo(PlayedSeed), "the story's map is the played one");
            var map = WorldMap.Active;
            int stoodBefore = 0;
            foreach (var (radius, at, spot) in PlayedRaiders)
            {
                if (!Walk(map, at, spot, radius, false, out Vector3 end, out int stall, out int steps))
                    Assert.Fail($"map seed {PlayedSeed}: the raider of radius {radius} that stood at {at} for the stand point {spot} "
                                + (stall >= StallSteps ? $"stands still again at {end}" : $"did not reach the base in {steps} steps (it is at {end})"));
                if (!Walk(map, at, spot, radius, true, out _, out _, out _)) stoodBefore++;
            }
            Debug.Log($"[RaidWalk] map seed {PlayedSeed}: the {PlayedRaiders.Length} raiders a played morning left standing walked on to the base; under the rule before the fix {stoodBefore} of them stood still");
        }

        // A wide raider on this map walked up a column of cells that lies a hundredth of a cell outside a wall's margin, came to the
        // margin's end a hair inside it and was allowed no step: straight on was refused, and its way had too little sideways in it
        // for a slide. It stood at (-22.49, -28.78), 23 cells short of the base.
        const int MarginEndSeed = 181297074;
        const float MarginEndRing = 56f, MarginEndBearing = 200f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task WideRaider_CatchingTheEndOfAWallsMarginByAHair_StepsAsideAndWalksOnToTheBase()
        {
            var campaign = await StartStory(MarginEndSeed);
            Assume.That(campaign.MapSeed, Is.EqualTo(MarginEndSeed), "the story's map is the one with that wall");
            var map = WorldMap.Active;
            const float radius = 1f;
            Vector3 dir = Quaternion.Euler(0f, MarginEndBearing, 0f) * Vector3.forward;
            Vector3 start = V2Campaign.FindOpen(V2Campaign.BaseCentre + dir * MarginEndRing, radius + .2f);
            Vector3 spot = StandPoint(start, radius);
            if (!Walk(map, start, spot, radius, false, out Vector3 end, out int stall, out int steps))
                Assert.Fail($"map seed {MarginEndSeed}: the raider of radius {radius} set down at {start} (ring {MarginEndRing}, bearing {MarginEndBearing}) for the stand point {spot} "
                            + (stall >= StallSteps ? $"stood still at {end}" : $"did not reach the base in {steps} steps (it is at {end})"));
        }

        /// <summary>The story from the title up to Day 0's walk, with its map in place; seed above 0 lays that map (the campaign's own
        /// field for a continued save's seed).</summary>
        static async Task<V2Campaign> StartStory(int seed)
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 20f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            if (seed > 0)
            {
                var pin = typeof(V2Campaign).GetField("pinnedMapSeed", BindingFlags.Instance | BindingFlags.NonPublic);
                Assume.That(pin, Is.Not.Null, "the campaign can be given a map seed");
                pin.SetValue(campaign, seed);
            }
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 30f, "Day 0's walk begins");
            Assume.That(WorldMap.Active.CampaignMapApplied, Is.True, "the story's map is in place");
            return campaign;
        }

        /// <summary>Where V2Campaign.RaidSpotFor stands a raider that is first asked at this point: off the base side nearest to it, a body
        /// and a little out.</summary>
        static Vector3 StandPoint(Vector3 from, float radius)
        {
            Vector3 d = Query.Flat(from - V2Campaign.BaseCentre);
            Vector3 normal = Mathf.Abs(d.x) > Mathf.Abs(d.z) ? new Vector3(Mathf.Sign(d.x), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign(d.z));
            Vector3 tangent = new(Mathf.Abs(normal.z), 0f, Mathf.Abs(normal.x));
            float along = Mathf.Clamp(Vector3.Dot(d, tangent), -V2Campaign.BaseHalf + 1f, V2Campaign.BaseHalf - 1f);
            return V2Campaign.BaseCentre + normal * (V2Campaign.BaseHalf + radius + .35f) + tangent * along;
        }

        /// <summary>Steps a raider from start until it stands at the base (at its stand point, or within reach of a base wall as
        /// Enemy.TickRaid accepts it). False when it stands still for StallSteps steps or runs out of steps. oldRule walks as before the
        /// fix: every body follows the slim field, and a step the map does not count as walkable is no step.</summary>
        static bool Walk(WorldMap map, Vector3 start, Vector3 spot, float radius, bool oldRule, out Vector3 at, out int stall, out int steps)
        {
            at = start;
            stall = 0;
            for (steps = 0; steps < MaxSteps; steps++)
            {
                Vector3 own = Query.Flat(at - V2Campaign.BaseCentre);
                float front = V2Campaign.BaseHalf + .35f, reach = radius + .6f;
                float dx = Mathf.Max(0f, Mathf.Abs(own.x) - front), dz = Mathf.Max(0f, Mathf.Abs(own.z) - front);
                if (dx * dx + dz * dz <= reach * reach || Query.Flat(spot - at).magnitude <= .6f) return true;
                Vector3 wanted = map.GetRaidDirection(at, spot, oldRule ? MapGrid.SlimReach : radius) * Speed;
                Vector3 v = oldRule ? (map.IsWalkable(at + wanted * Step, radius) ? wanted : Vector3.zero) : map.WalkStep(at, wanted, Step, radius, false);
                stall = v.sqrMagnitude < .0001f ? stall + 1 : 0;
                if (stall >= StallSteps) return false;
                at += v * Step;
            }
            return false;
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for " + operation + ".");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
