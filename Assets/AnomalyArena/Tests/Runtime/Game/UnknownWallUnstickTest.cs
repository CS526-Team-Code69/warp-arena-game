using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// A monster that walks straight into a wall's collider the map's walls do not cover. The map allows every step whole, the physics
    /// holds the body where it stands, and the progress watch (Enemy.NoProgress.cs) used to start again on every whole step, so the body
    /// pushed at the wall for good. A step into a wall the body touches now counts as held (Combatant.RecentWallTouch), and the body is set
    /// down on open ground clear of the wall's collider after the usual limit. The watch itself is checked first without the physics.
    /// </summary>
    [TestFixture]
    public class UnknownWallUnstickTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        WaveSet originalWaves;
        GameObject hiddenWall;

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            if (hiddenWall) Object.Destroy(hiddenWall);
            hiddenWall = null;
            var game = GameManager.Instance;
            if (game && originalWaves) game.waves.waveSet = originalWaves;
            originalWaves = null;
        }

        [Test]
        public void WholeStepsInPlace_CountOnlyWhenHeld()
        {
            const float dt = .02f, limit = 2.5f;
            Vector3 at = Vector3.zero, v = Vector3.forward * 4f;
            var free = new WalkProgressWatch();
            var held = new WalkProgressWatch();
            bool freeFired = false, heldFired = false;
            float heldAt = -1f;
            for (float t = 0f; t < 4f; t += dt)
            {
                freeFired |= free.Note(at, v, v, dt, t, limit);
                if (!heldFired && held.Note(at, v, v, dt, t, limit, true)) { heldFired = true; heldAt = t; }
            }
            Assert.That(freeFired, Is.False, "whole steps that are not held never count, as before");
            Assert.That(heldFired, Is.True, "whole steps held by a wall count");
            Assert.That(heldAt, Is.EqualTo(limit).Within(.05f), "after the usual limit");
        }

        [Test]
        public void HeldStepsThatMoveOn_NeverCount()
        {
            const float dt = .02f;
            Vector3 v = Vector3.forward * 4f;
            var watch = new WalkProgressWatch();
            bool fired = false;
            // Held, but the body slides on along the wall at a walking pace: it leaves Reach behind, so it is getting somewhere.
            for (float t = 0f; t < 4f; t += dt) fired |= watch.Note(Vector3.right * (t * 2f), v, v, dt, t, 2.5f, true);
            Assert.That(fired, Is.False);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task GruntAgainstAnUnknownWall_IsSetDownClearOfIt()
        {
            var game = await StartOpenRun();
            var map = WorldMap.Active;
            Assume.That(FindOpenGround(map, game.player.Position, out Vector3 centre), Is.True, "the map has open ground 10 cells round");
            var player = game.player;
            player.Invulnerable = true;
            Vector3 playerAt = centre + new Vector3(0f, 0f, 3f);
            Place(player, playerAt);

            // A wall the map does not know: a collider on the Wall layer and nothing in layout.walls, the extra walls or the Bulwarks.
            hiddenWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hiddenWall.name = "UnknownWall (test)";
            hiddenWall.layer = GameManager.WallLayer;
            hiddenWall.transform.position = centre + Vector3.up * 1.5f;
            hiddenWall.transform.localScale = new Vector3(8f, 3f, 1f);
            Physics.SyncTransforms();
            Assume.That(map.IsWall(centre, .5f), Is.False, "the map does not know the wall");

            var grunt = game.waves.SpawnLab(EnemyKind.Grunt, centre + new Vector3(0f, 0f, -4f), false, ElementKind.None);
            Assume.That(grunt, Is.Not.Null, "a Grunt could be set down in front of the wall");
            int setDowns = grunt.SetDownCount;
            float clock = 0f;
            while (clock < 8f && grunt.IsAlive && grunt.SetDownCount == setDowns)
            {
                clock += game.WorldDeltaTime;
                await Hold(game, player, playerAt, grunt);
            }
            Assert.That(grunt.SetDownCount, Is.GreaterThan(setDowns), $"the Grunt held at the wall was set down (waited {clock:0.0} s, it stood at {grunt.Position})");
            Assert.That(clock, Is.LessThan(Enemy.WalkRefusedSeconds + 2f), "within about the usual limit");
            // Let the fade finish, then the body must stand clear of the wall's collider.
            for (float wait = 0f; grunt.UnstickFading && wait < 2f; wait += game.WorldDeltaTime) await Hold(game, player, playerAt, grunt);
            var box = hiddenWall.GetComponent<Collider>().bounds;
            Vector3 p = grunt.Position;
            bool inside = p.x > box.min.x - grunt.Radius && p.x < box.max.x + grunt.Radius && p.z > box.min.z - grunt.Radius && p.z < box.max.z + grunt.Radius;
            Assert.That(inside, Is.False, $"set down clear of the wall: {p}, wall {box.min}..{box.max}");
            grunt.DiscardWithoutReward();
        }

        /// <summary>One frame with the player held where it stands, unhurt, and every other monster gone.</summary>
        static async Task Hold(GameManager game, PlayerController player, Vector3 at, Enemy kept)
        {
            Place(player, at);
            player.Heal(player.maxHp);
            foreach (var other in game.waves.Alive.ToArray())
                if (other && other != kept && other.IsAlive) other.DiscardWithoutReward();
            await Awaitable.NextFrameAsync();
        }

        static void Place(Combatant body, Vector3 at)
        {
            at.y = body.transform.position.y;
            if (body.TryGetComponent(out Rigidbody rb))
            {
                rb.position = at;
                if (!rb.isKinematic) rb.linearVelocity = Vector3.zero;
            }
            body.transform.position = at;
        }

        /// <summary>A point on a 2-cell lattice with ground a wide body can stand on 10 cells round it, away from the player.</summary>
        static bool FindOpenGround(WorldMap map, Vector3 player, out Vector3 centre)
        {
            for (float x = -40f; x <= 40f; x += 2f)
            for (float z = -30f; z <= 30f; z += 2f)
            {
                var c = new Vector3(x, 0f, z);
                if (Query.Flat(player - c).magnitude < 14f) continue;
                bool clear = true;
                for (float dx = -10f; dx <= 10f && clear; dx += .5f)
                for (float dz = -10f; dz <= 10f && clear; dz += .5f)
                    if (dx * dx + dz * dz <= 100f) clear = map.IsWalkable(c + new Vector3(dx, 0f, dz), 1.2f);
                if (!clear) continue;
                centre = c;
                return true;
            }
            centre = Vector3.zero;
            return false;
        }

        /// <summary>The open mode's first wave, made long so it does not end meanwhile, with every black hole fed once (no first-sight
        /// scene).</summary>
        async Task<GameManager> StartOpenRun()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map");
            var game = GameManager.Instance;
            originalWaves = game.waves.waveSet;
            var waves = Object.Instantiate(originalWaves);
            foreach (var wave in waves.waves) wave.duration = 600f;
            game.waves.waveSet = waves;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            await UntilWaveActive(game);
            foreach (var hole in WorldMap.Active.holes)
                if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, game.waves.WaveIndex + 1);
            await Awaitable.NextFrameAsync();
            await UntilWaveActive(game);
            return game;
        }

        static async Task UntilWaveActive(GameManager game)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) WorldMap.Active.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
