using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>A Spitter in its range band (EnemyDefinition.rangedNear to rangedFar from the player) with one of the map's walls between
    /// it and the player does not stand there for ever: it closes in round the wall until it sees the player. With a clear line it keeps
    /// its range as before (it stays where it is and winds up its spit).</summary>
    [TestFixture]
    public class SpitterSightTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task ASpitterInItsRangeBand_BehindAWall_ComesRoundToWhereItSeesThePlayer()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var world = WorldMap.Active;
            var definition = game.waves.SpawnLab(EnemyKind.Spitter, game.player.Position + Vector3.forward * 9f, false, ElementKind.None);
            Assume.That(definition, Is.Not.Null, "the pool has a free Spitter");
            var spitter = definition;
            spitter.SuppressRewards = true;
            float band = (spitter.definition.rangedNear + spitter.definition.rangedFar) * .5f;

            Assume.That(FindWallBetween(world, band, out Vector3 playerAt, out Vector3 spitterAt), Is.True,
                $"the map has a wall with open ground {band} cells apart on its two sides");
            V3CombatSmokeTest.Teleport(game.player, playerAt);
            V3CombatSmokeTest.Teleport(spitter, spitterAt);
            Physics.SyncTransforms();
            game.player.ProtectFor(60f);
            await Awaitable.FixedUpdateAsync();
            await Awaitable.NextFrameAsync();
            Assume.That(Walled(spitter, game.player), Is.True, "the wall stands between the Spitter and the player");
            float apart = Query.Flat(spitter.Position - game.player.Position).magnitude;
            Assume.That(apart, Is.InRange(spitter.definition.rangedNear, spitter.definition.rangedFar), "the Spitter stands in its range band");

            Vector3 from = spitter.Position;
            float deadline = Time.realtimeSinceStartup + 25f;
            while (spitter.IsAlive && Walled(spitter, game.player))
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"The Spitter stood behind the wall for 25 s: it is at {spitter.Position}, {Query.Flat(spitter.Position - from).magnitude:0.0} cells from where it began ({from}); the player is at {game.player.Position}.");
                await Awaitable.NextFrameAsync();
            }
            Assert.That(spitter.IsAlive, Is.True, "the Spitter is alive");
            Assert.That(Query.Flat(spitter.Position - from).magnitude, Is.GreaterThan(1.5f), "it walked round the wall to see the player");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task ASpitterInItsRangeBand_WithAClearLine_KeepsItsRangeAndSpits()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            Vector3 origin = game.player.Position;
            var spitter = game.waves.SpawnLab(EnemyKind.Spitter, origin + Vector3.forward * 9f, false, ElementKind.None);
            Assume.That(spitter, Is.Not.Null, "the pool has a free Spitter");
            spitter.SuppressRewards = true;
            game.player.ProtectFor(60f);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            Assume.That(Walled(spitter, game.player), Is.False, "the lane between them is clear");
            Vector3 from = spitter.Position;

            float deadline = Time.realtimeSinceStartup + 12f;
            while (!spitter.WindingUp)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The Spitter with a clear line never wound up its spit.");
                await Awaitable.NextFrameAsync();
            }
            Assert.That(Query.Flat(spitter.Position - from).magnitude, Is.LessThan(1.5f), "it kept its range: it spits from where it stood");
        }

        static bool Walled(Combatant a, Combatant b) => Query.WallBetween(Query.AtCastHeight(a.Position), Query.AtCastHeight(b.Position), out _);

        /// <summary>Open ground on the two sides of one of the map's walls, apart cells from each other across it, with the wall in the
        /// line between them and no gravity ring or quicksand under either.</summary>
        static bool FindWallBetween(WorldMap world, float apart, out Vector3 a, out Vector3 b)
        {
            a = b = Vector3.zero;
            if (!world || !world.layout || world.layout.walls == null) return false;
            foreach (var wall in world.layout.walls)
                foreach (bool alongX in new[] { false, true })
                {
                    float thick = alongX ? wall.width : wall.height, wide = alongX ? wall.height : wall.width;
                    if (thick > apart - 4f || wide < 3f) continue;
                    float side = (apart - thick) * .5f;
                    Vector2 mid = wall.center;
                    Vector3 one = alongX ? new Vector3(wall.xMin - side, 0f, mid.y) : new Vector3(mid.x, 0f, wall.yMin - side);
                    Vector3 other = alongX ? new Vector3(wall.xMax + side, 0f, mid.y) : new Vector3(mid.x, 0f, wall.yMax + side);
                    if (!Open(world, one) || !Open(world, other)) continue;
                    if (!Query.WallBetween(Query.AtCastHeight(one), Query.AtCastHeight(other), out _)) continue;
                    a = one; b = other;
                    return true;
                }
            return false;
        }

        static bool Open(WorldMap world, Vector3 at) => world.IsWalkable(at, 1.2f) && !Quicksand.IsQuicksand(at);
    }
}
