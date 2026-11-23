using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>What the monsters chase may stand on a cell that paths do not use (its centre within half a cell of a wall) whose four
    /// neighbours are such cells too: the corner cell of an inside corner of two walls, a gap one cell wide. The chase field still
    /// reaches the map from there: a monster on open ground some cells off is given a way, and it walks up to the player.</summary>
    [TestFixture]
    public class ChaseFieldCornerTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (WorldMap.Active) WorldMap.Active.SetExtraWalls(null);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task ATargetInAnInsideCornerOfTwoWalls_StillGivesTheMonstersAWay()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            Assume.That(BuildCorner(world, out Vector3 inCorner, out string walls), Is.True, "the map has a square of open ground 12 cells wide for the two walls");
            Vector3 beside = inCorner + new Vector3(1f, 0f, 1f), from = inCorner + new Vector3(8f, 0f, 8f);

            // The control: a target on the free cell diagonal to the corner cell.
            world.Grid.Integrate(beside);
            Vector3 control = world.GetChaseDirection(from, beside, false, MapGrid.SlimReach);
            Assume.That(control.sqrMagnitude, Is.GreaterThan(.5f), $"a monster at {from} is given a way to a target on the free ground beside the corner ({beside})");

            world.Grid.Integrate(inCorner);
            Vector3 way = world.GetChaseDirection(from, inCorner, false, MapGrid.SlimReach);
            Assert.That(way.sqrMagnitude, Is.GreaterThan(.5f),
                $"A monster at {from} is given no way to a target standing in the corner cell at {inCorner} ({walls}): its chase direction is {way}. With the target one cell out, at {beside}, it is {control}.");
            Vector3 wide = world.GetChaseDirection(from, inCorner, false, MapGrid.WideReach);
            Assert.That(wide.sqrMagnitude, Is.GreaterThan(.5f), $"a wide body at {from} is given a way to the target in the corner cell too; it got {wide}");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task ThePlayerInAnInsideCornerOfTwoWalls_IsWalkedUpTo()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            Assume.That(BuildCorner(world, out Vector3 inCorner, out string walls), Is.True, "the map has a square of open ground 12 cells wide for the two walls");
            Vector3 from = inCorner + new Vector3(8f, 0f, 8f);
            V3CombatSmokeTest.Teleport(game.player, inCorner);
            Physics.SyncTransforms();
            game.player.ProtectFor(60f);
            var grunt = game.waves.SpawnLab(EnemyKind.Grunt, from, false, ElementKind.None);
            Assume.That(grunt, Is.Not.Null, "the pool has a free Grunt");
            grunt.SuppressRewards = true;
            await Awaitable.FixedUpdateAsync();
            await Awaitable.NextFrameAsync();
            Vector3 began = grunt.Position;
            float apart = Query.Flat(began - game.player.Position).magnitude;

            float deadline = Time.realtimeSinceStartup + 12f;
            while (grunt.IsAlive && Query.Flat(grunt.Position - game.player.Position).magnitude > 3f)
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"The Grunt did not come to a player standing in the corner cell at {game.player.Position} ({walls}): after 12 s it is at {grunt.Position}, {Query.Flat(grunt.Position - began).magnitude:0.0} cells from where it began ({began}, {apart:0.0} cells from the player).");
                await Awaitable.NextFrameAsync();
            }
            Assert.That(grunt.IsAlive, Is.True, "the Grunt is alive");
        }

        /// <summary>Two walls meeting in an L on whole cells, on open ground: one seven cells along x, one six cells along z from its
        /// end. The corner cell lies beside both, and so do the two cells next to it along the walls.</summary>
        static bool BuildCorner(WorldMap world, out Vector3 inCorner, out string walls)
        {
            inCorner = Vector3.zero;
            walls = "";
            if (!world || world.Grid == null) return false;
            int halfWidth = world.Grid.Width / 2, halfHeight = world.Grid.Height / 2;
            for (int z = -halfHeight + 3; z <= halfHeight - 15; z += 2)
            for (int x = -halfWidth + 3; x <= halfWidth - 15; x += 2)
            {
                if (!OpenSquare(world, x - 1, z - 1, 12)) continue;
                var along = new Rect(x, z, 7f, 1f);
                var up = new Rect(x, z + 1f, 1f, 6f);
                world.SetExtraWalls(new[] { along, up });
                inCorner = new Vector3(x + 1.5f, 0f, z + 1.5f);
                walls = $"walls {along} and {up}";
                return true;
            }
            return false;
        }

        static bool OpenSquare(WorldMap world, int x0, int z0, int cells)
        {
            for (int dz = 0; dz < cells; dz++)
            for (int dx = 0; dx < cells; dx++)
            {
                var point = new Vector3(x0 + dx + .5f, 0f, z0 + dz + .5f);
                if (!world.Contains(point, 1.5f) || world.IsWall(point, 1.5f) || world.IsHole(point) || world.GetPull(point).sqrMagnitude > .01f) return false;
            }
            return true;
        }
    }
}
