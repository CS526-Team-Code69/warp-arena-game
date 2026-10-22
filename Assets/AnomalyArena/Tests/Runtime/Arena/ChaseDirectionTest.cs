using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class ChaseDirectionTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        UnityEngine.Random.State savedRandom;

        [SetUp]
        public void SetUp()
        {
            savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(5261);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Random.state = savedRandom;

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task OpenGround_TargetInSight_ChasesStraightAtTarget()
        {
            var map = await LoadedMap();
            var centre = OpenCentre(map, 8f);
            var target = centre + new Vector3(-5f, 0f, -2f);
            var enemy = centre + new Vector3(5f, 0f, 2f);

            map.Grid.Integrate(target);
            var direction = map.Grid.Direction(enemy, target, false);

            Assert.That(map.Grid.HasClearPath(enemy, target, false), Is.True, "open ground gives a clear line");
            Assert.That(Vector3.Angle(direction, Query.Flat(target - enemy)), Is.LessThan(1f),
                "a visible target is chased in a straight line, not along the grid's 45-degree and axis lanes");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task WallBetween_ChaseRoutesAroundOnWalkableGroundAndReachesTarget()
        {
            var map = await LoadedMap();
            Assume.That(FindWallCrossing(map, out var enemy, out var target), Is.True,
                "the saved layout has a wall with open ground on both sides");
            map.Grid.Integrate(target);
            Assert.That(map.Grid.HasClearPath(enemy, target, false), Is.False, "the wall blocks the straight line");

            var at = enemy;
            for (int step = 0; step < 600 && Query.Flat(target - at).magnitude > 1f; step++)
            {
                var direction = map.Grid.Direction(at, target, false);
                Assert.That(direction.sqrMagnitude, Is.GreaterThan(.5f), $"chase keeps moving at {at}");
                at += direction * .25f;
                Assert.That(map.IsWalkable(at, .3f, true), Is.True, $"chase stays clear of walls and holes at {at}");
            }

            Assert.That(Query.Flat(target - at).magnitude, Is.LessThanOrEqualTo(1f), "chase reaches the target around the wall");
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

        static Vector3 OpenCentre(WorldMap map, float halfSize)
        {
            var extents = map.Bounds.extents;
            for (float x = -extents.x + halfSize + 1f; x <= extents.x - halfSize - 1f; x += 2f)
            for (float z = -extents.z + halfSize + 1f; z <= extents.z - halfSize - 1f; z += 2f)
            {
                var centre = new Vector3(x, 0f, z);
                if (SquareWalkable(map, centre, halfSize)) return centre;
            }
            Assert.Fail("The saved map has no open square for the chase scenario.");
            return Vector3.zero;
        }

        static bool SquareWalkable(WorldMap map, Vector3 centre, float halfSize)
        {
            for (float dx = -halfSize; dx <= halfSize; dx += .5f)
            for (float dz = -halfSize; dz <= halfSize; dz += .5f)
                if (!map.IsWalkable(centre + new Vector3(dx, 0f, dz), .6f)) return false;
            return true;
        }

        static bool FindWallCrossing(WorldMap map, out Vector3 enemy, out Vector3 target)
        {
            enemy = target = Vector3.zero;
            foreach (var wall in map.layout.walls)
            {
                bool horizontal = wall.width >= wall.height;
                if (Math.Max(wall.width, wall.height) < 6f) continue;
                var centre = new Vector3(wall.center.x, 0f, wall.center.y);
                var across = horizontal ? Vector3.forward : Vector3.right;
                foreach (float gap in new[] { 3f, 2.5f, 4f, 5f })
                {
                    float offset = (horizontal ? wall.height : wall.width) * .5f + gap;
                    var a = centre + across * offset;
                    var b = centre - across * offset;
                    if (!SquareWalkable(map, a, 1f) || !SquareWalkable(map, b, 1f)) continue;
                    enemy = a;
                    target = b;
                    return true;
                }
            }
            return false;
        }
    }
}
