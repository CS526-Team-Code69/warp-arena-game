using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the enemies chase stands deep in a dead-end gap a little wider than a body, whose middle line runs between two rows of cell
    /// centres, so its cell and the four round it are closed to a body's centre on the path grid. The chase field then used to hold that
    /// one cell and nothing else, and every chaser on the map stood still. Now the field starts from the open cells nearest it that see
    /// it (MapGrid.SeedAround): every chaser round it is given a way, and one walking that way by the walk's own rules
    /// (WorldMap.WalkStep) reaches it in the gap, from the open end and from behind the closed one.
    /// </summary>
    [TestFixture]
    public class GridNookTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const float Gap = 1.4f, Length = 6f, Thick = 1f, WallHeight = 3f, Speed = 4f, Step = 1f / 30f, Body = .5f;

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task TargetDeepInANarrowDeadEnd_EveryChaserIsGivenAWay_AndOneWalkingItReachesTheTarget()
        {
            var map = await LoadedMap();
            Vector3 centre = OpenCentre(map, 10f);
            float halfWidth = map.Grid.Width * .5f, halfHeight = map.Grid.Height * .5f;
            // A line between two rows of cell centres: every cell centre in the gap lies .2 from one of its sides, closer than a body's
            // margin, while a body walking its middle has .2 to spare on either side.
            float zMid = Mathf.Floor(centre.z + halfHeight) - halfHeight;
            float x0 = Mathf.Round(centre.x) - Length * .5f;
            var rects = new List<Rect>
            {
                new(x0, zMid + Gap * .5f, Length, Thick),
                new(x0, zMid - Gap * .5f - Thick, Length, Thick),
                new(x0 + Length, zMid - Gap * .5f - Thick, Thick, Gap + 2f * Thick),
            };
            foreach (var rect in rects) Block(rect);
            Physics.SyncTransforms();
            map.SetExtraWalls(rects);
            Vector3 target = new(x0 + Length - 1.5f, 0f, zMid);
            Assume.That(map.WallDepth(target, Body), Is.Zero, "a body fits where the target stands");
            Vector3 cell = new(Mathf.Floor(target.x + halfWidth) - halfWidth + .5f, 0f, Mathf.Floor(target.z + halfHeight) - halfHeight + .5f);
            foreach (var offset in new[] { Vector3.zero, Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                Assume.That(map.IsWalkable(cell + offset, MapGrid.SlimReach), Is.False,
                    $"the target's cell and the four round it are closed to a body's centre ({cell + offset})");

            map.Grid.Integrate(target);
            var chasers = new[]
            {
                new Vector3(x0 - 4f, 0f, zMid), // before the open end
                new Vector3(x0 + Length + 4f, 0f, zMid), // behind the closed end
                new Vector3(target.x, 0f, zMid + 5f),
                new Vector3(target.x, 0f, zMid - 5f),
                new Vector3(x0 - 5f, 0f, zMid + 7f),
            };
            foreach (var at in chasers)
            {
                Assume.That(map.IsWalkable(at, .6f), Is.True, $"{at} is open ground");
                Assert.That(map.Grid.Direction(at, target, false).sqrMagnitude, Is.GreaterThan(.5f), $"a chaser at {at} is given a way");
            }

            Assert.That(Walk(map, chasers[0], target), Is.LessThanOrEqualTo(2f * Body), "a chaser from the open end reaches the target in the gap");
            Assert.That(Walk(map, chasers[1], target), Is.LessThanOrEqualTo(2f * Body), "a chaser from behind the closed end goes round and reaches it");
        }

        /// <summary>Walks a slim body from a point along the chase way for up to 60 s at a walking speed, each step as Enemy.KeepOffEdges
        /// takes it; returns how far from the target it ends.</summary>
        static float Walk(WorldMap map, Vector3 from, Vector3 target)
        {
            Vector3 at = from;
            for (int i = 0; i < 1800 && Query.Flat(target - at).magnitude > 2f * Body; i++)
            {
                Vector3 way = map.Grid.Direction(at, target, false);
                at += map.WalkStep(at, way * Speed, Step, Body, false) * Step;
            }
            return Query.Flat(target - at).magnitude;
        }

        /// <summary>A wall body on the walls' layer, so lines of sight see it as they see the map's own walls.</summary>
        static void Block(Rect rect)
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "GridNookTest wall";
            wall.layer = GameManager.WallLayer;
            wall.transform.position = new Vector3(rect.center.x, WallHeight * .5f, rect.center.y);
            wall.transform.localScale = new Vector3(rect.width, WallHeight, rect.height);
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
                if (SquareOpen(map, centre, halfSize)) return centre;
            }
            Assert.Fail("The saved map has no open square for the gap.");
            return Vector3.zero;
        }

        static bool SquareOpen(WorldMap map, Vector3 centre, float halfSize)
        {
            for (float dx = -halfSize; dx <= halfSize; dx += .5f)
            for (float dz = -halfSize; dz <= halfSize; dz += .5f)
                if (!map.IsWalkable(centre + new Vector3(dx, 0f, dz), .6f)) return false;
            return true;
        }
    }
}
