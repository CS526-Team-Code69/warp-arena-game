using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// A monster's own walking next to walls (WorldMap.WalkStep): a step that would enter a wall's margin turns into a slide along the
    /// wall instead of a stop, a body already inside a margin walks out or along and never deeper, and a body of any width that is
    /// sent round a wall keeps walking and arrives. Before, the first refused step stopped the body for good: the direction it was
    /// given never changed, so neither did the refusal.
    /// </summary>
    [TestFixture]
    public class WalkStepTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const float Step = .02f, Speed = 5f;

        [TestCase(.5f), TestCase(1f)]
        [LoadScene(Scene), Timeout(30000)]
        public async Task StepIntoAWallsMargin_SlidesAlongTheWall_AndOnlyAStepStraightAtItStops(float radius)
        {
            var map = await LoadedMap();
            Assume.That(FindFace(map, radius, out Vector3 at, out Vector3 intoWall, out Vector3 along), Is.True, "the saved layout has a long wall face with open ground beside it");
            Assert.That(map.WallDepth(at, radius), Is.Zero, "the body stands just outside the wall's margin");

            Vector3 parallel = map.WalkStep(at, along * Speed, Step, radius, false);
            Assert.That(Vector3.Distance(parallel, along * Speed), Is.LessThan(.001f), "a step along the wall is taken whole");

            Vector3 wanted = (intoWall + along).normalized * Speed;
            Vector3 slide = map.WalkStep(at, wanted, Step, radius, false);
            Assert.That(slide.magnitude, Is.GreaterThan(Speed * .4f), "a step slanting into the margin is not a stop");
            Assert.That(Mathf.Abs(Vector3.Dot(slide, intoWall)), Is.LessThan(.001f), "the body does not walk into the margin");
            Assert.That(Vector3.Dot(slide, along), Is.GreaterThan(0f), "it slides the way it wanted to go");
            Assert.That(map.WallDepth(at + slide * Step, radius), Is.Zero, "and stays on ground it may stand on");

            Assert.That(map.WalkStep(at, intoWall * Speed, Step, radius, false).magnitude, Is.LessThan(.001f), "a step straight at the wall has no side to slide to");
        }

        [TestCase(.5f), TestCase(1f)]
        [LoadScene(Scene), Timeout(30000)]
        public async Task BodyInsideAWallsMargin_WalksOutOrAlong_NeverDeeper(float radius)
        {
            var map = await LoadedMap();
            Assume.That(FindFace(map, radius, out Vector3 edge, out Vector3 intoWall, out Vector3 along), Is.True, "the saved layout has a long wall face with open ground beside it");
            // Pushed 0.3 into the margin, as a knockback or another body's push leaves it.
            Vector3 at = edge + intoWall * .31f;
            float depth = map.WallDepth(at, radius);
            Assert.That(depth, Is.EqualTo(.3f).Within(.02f), "the body stands 0.3 inside the wall's margin");
            Assert.That(map.IsWalkable(at, radius), Is.False, "which the map does not count as walkable ground");

            Vector3 away = map.WalkStep(at, -intoWall * Speed, Step, radius, false);
            Assert.That(Vector3.Distance(away, -intoWall * Speed), Is.LessThan(.001f), "it may walk out");
            Vector3 beside = map.WalkStep(at, along * Speed, Step, radius, false);
            Assert.That(Vector3.Distance(beside, along * Speed), Is.LessThan(.001f), "and along the wall");
            Vector3 deeper = map.WalkStep(at, (intoWall + along).normalized * Speed, Step, radius, false);
            Assert.That(map.WallDepth(at + deeper * Step, radius), Is.LessThanOrEqualTo(depth + .001f), "but never deeper");
            Assert.That(Vector3.Dot(deeper, along), Is.GreaterThan(0f), "a step slanting deeper still slides along");
        }

        [TestCase(.35f), TestCase(.5f), TestCase(.7f), TestCase(1f)]
        [LoadScene(Scene), Timeout(60000)]
        public async Task SentRoundAWall_ABodyOfAnyWidth_KeepsWalkingAndArrives(float radius)
        {
            var map = await LoadedMap();
            int crossings = 0;
            foreach (var wall in map.layout.walls)
            {
                if (!Crossing(map, wall, radius, out Vector3 start, out Vector3 target)) continue;
                crossings++;
                map.Grid.Integrate(target);
                Vector3 at = start;
                int stalled = 0, longestStall = 0, steps = 0;
                for (; steps < 6000 && Query.Flat(target - at).magnitude > radius + 1.2f; steps++)
                {
                    Vector3 way = map.GetChaseDirection(at, target, false, radius);
                    Vector3 v = map.WalkStep(at, way * Speed, Step, radius, false);
                    stalled = v.sqrMagnitude < .0001f ? stalled + 1 : 0;
                    longestStall = Mathf.Max(longestStall, stalled);
                    if (stalled > 100) break; // two seconds of standing still: the old stop
                    at += v * Step;
                }
                Assert.That(longestStall, Is.LessThan(25), $"radius {radius}: walking from {start} round the wall {wall} to {target}, the body stood still at {at}");
                Assert.That(Query.Flat(target - at).magnitude, Is.LessThanOrEqualTo(radius + 1.2f), $"radius {radius}: the body sent from {start} round the wall {wall} reaches {target} (it is at {at} after {steps} steps)");
                Assert.That(map.WallDepth(at, radius), Is.LessThan(.01f), "and it never walked into a wall's margin on the way");
            }
            Assume.That(crossings, Is.GreaterThan(0), "the saved layout has a wall with open ground on both sides");
        }

        /// <summary>A body walking straight along the outside of a wall's margin, a hair inside the line of the margin's end: the step
        /// ahead ends in the margin and its way has nothing sideways in it for a slide, so it used to be allowed no step at all. It steps
        /// aside, to the side from which the step ahead is open, and walks on past the wall's end.</summary>
        [TestCase(.5f), TestCase(1f)]
        [LoadScene(Scene), Timeout(30000)]
        public async Task CatchingTheEndOfAWallsMarginByAHair_StepsAsideAndWalksOn(float radius)
        {
            var map = await LoadedMap();
            Assume.That(FindEnd(map, radius, out Vector3 corner, out Vector3 ahead, out Vector3 inward), Is.True, "the saved layout has a wall's end with open ground round it");
            // Just short of the margin, two thousandths of a cell inside the line of its end.
            Vector3 at = corner - ahead * .05f + inward * .002f;
            Assert.That(map.WallDepth(at, radius), Is.Zero, "the body stands on ground it may stand on");
            Assert.That(map.WallDepth(at + ahead * Speed * Step, radius), Is.GreaterThan(0f), "its step straight ahead ends in the margin");

            Vector3 first = map.WalkStep(at, ahead * Speed, Step, radius, false);
            Assert.That(first.magnitude, Is.GreaterThan(Speed * .4f), "it is not left without a step");
            Assert.That(Vector3.Dot(first, inward), Is.LessThan(0f), "it steps aside, away from the wall");

            int stalled = 0, longest = 0;
            Vector3 from = at;
            for (int step = 0; step < 200 && Vector3.Dot(at - from, ahead) < 2f; step++)
            {
                Vector3 v = map.WalkStep(at, ahead * Speed, Step, radius, false);
                stalled = v.sqrMagnitude < .0001f ? stalled + 1 : 0;
                longest = Mathf.Max(longest, stalled);
                at += v * Step;
            }
            Assert.That(Vector3.Dot(at - from, ahead), Is.GreaterThanOrEqualTo(2f), $"it walks on past the margin's end (it is at {at}, still for {longest} steps at most)");
            Assert.That(longest, Is.LessThan(5), "without standing still");
        }

        /// <summary>The corner of the margin round the end of a long wall, for a body of this radius: ahead runs across the wall's end (the
        /// way a body passing the end walks) and inward along the wall toward its middle. The ground before the corner and beside the
        /// wall's end is open.</summary>
        static bool FindEnd(WorldMap map, float radius, out Vector3 corner, out Vector3 ahead, out Vector3 inward)
        {
            corner = ahead = inward = Vector3.zero;
            foreach (var wall in map.layout.walls)
            {
                bool horizontal = wall.width >= wall.height;
                if (Math.Max(wall.width, wall.height) < 6f) continue;
                foreach (float end in new[] { -1f, 1f })
                foreach (float side in new[] { -1f, 1f })
                {
                    // toMiddle: along the wall from this end toward its middle; across: over the wall, from the side the body comes from.
                    Vector3 toMiddle = (horizontal ? Vector3.right : Vector3.forward) * -end;
                    Vector3 across = (horizontal ? Vector3.forward : Vector3.right) * side;
                    float halfLong = (horizontal ? wall.width : wall.height) * .5f, halfShort = (horizontal ? wall.height : wall.width) * .5f;
                    var centre = new Vector3(wall.center.x, 0f, wall.center.y);
                    Vector3 point = centre - toMiddle * (halfLong + radius) - across * (halfShort + radius);
                    // Open ground before the corner and all along the outside of the wall's end, as far as the body walks.
                    if (!Clear(map, point - across * (radius + 1.6f), 1.5f, radius)) continue;
                    bool open = true;
                    for (float out_ = .15f; out_ <= radius + 2f && open; out_ += .25f)
                    for (float on = -1f; on <= 2f * (halfShort + radius) + 1f && open; on += .5f)
                        open = map.IsWalkable(point - toMiddle * out_ + across * on, radius);
                    if (!open || map.WallDepth(point - across * .05f - toMiddle * .01f, radius) > 0f) continue;
                    corner = point;
                    ahead = across;
                    inward = toMiddle;
                    return true;
                }
            }
            return false;
        }

        static async Task<WorldMap> LoadedMap()
        {
            await Awaitable.NextFrameAsync();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!WorldMap.Active || WorldMap.Active.Grid == null)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for the world map grid.");
                await Awaitable.NextFrameAsync();
            }
            return WorldMap.Active;
        }

        static bool Clear(WorldMap map, Vector3 centre, float halfSize, float radius)
        {
            for (float dx = -halfSize; dx <= halfSize; dx += .5f)
            for (float dz = -halfSize; dz <= halfSize; dz += .5f)
                if (!map.IsWalkable(centre + new Vector3(dx, 0f, dz), radius + .1f)) return false;
            return true;
        }

        /// <summary>A point a hair outside the margin of a long wall face, with open ground behind it; intoWall points at the face and
        /// along runs beside it.</summary>
        static bool FindFace(WorldMap map, float radius, out Vector3 at, out Vector3 intoWall, out Vector3 along)
        {
            at = intoWall = along = Vector3.zero;
            foreach (var wall in map.layout.walls)
            {
                bool horizontal = wall.width >= wall.height;
                if (Math.Max(wall.width, wall.height) < 6f) continue;
                var centre = new Vector3(wall.center.x, 0f, wall.center.y);
                foreach (float side in new[] { 1f, -1f })
                {
                    Vector3 outward = (horizontal ? Vector3.forward : Vector3.right) * side;
                    float half = (horizontal ? wall.height : wall.width) * .5f;
                    Vector3 edge = centre + outward * (half + radius + .01f);
                    if (!Clear(map, edge + outward * (radius + 1.6f), 1.5f, radius) || map.WallDepth(edge, radius) > 0f) continue;
                    at = edge;
                    intoWall = -outward;
                    along = horizontal ? Vector3.right : Vector3.forward;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Two points on either side of a long wall, each on open ground for this body.</summary>
        static bool Crossing(WorldMap map, Rect wall, float radius, out Vector3 start, out Vector3 target)
        {
            start = target = Vector3.zero;
            bool horizontal = wall.width >= wall.height;
            if (Math.Max(wall.width, wall.height) < 6f) return false;
            var centre = new Vector3(wall.center.x, 0f, wall.center.y);
            var across = horizontal ? Vector3.forward : Vector3.right;
            foreach (float gap in new[] { 3f, 4f, 5f })
            {
                float offset = (horizontal ? wall.height : wall.width) * .5f + radius + gap;
                Vector3 a = centre + across * offset, b = centre - across * offset;
                if (!Clear(map, a, 1f, radius) || !Clear(map, b, 1f, radius)) continue;
                start = a;
                target = b;
                return true;
            }
            return false;
        }
    }
}
