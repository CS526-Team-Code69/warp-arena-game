using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    [Category("V3Smoke")]
    public class EnemyLabHollowTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task SpawnHollow_ThenClear_EntersTheTrialAndCanEnterItAgain()
        {
            var (game, world, lab) = await StartLab();
            Vector3 stood = game.player.Position;
            int holes = world.holes.Count(h => h && h.Phase != HolePhase.Gone);

            lab.selected = EnemyKind.Hollow;
            var boss = lab.Spawn();
            Assert.That(boss, Is.SameAs(world.hollow), "spawning the Nash Prophet returns the trial boss");
            Assert.That(world.TrialActive, Is.True, "it is fought inside the void trial");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial));
            Assert.That(Query.Flat(game.player.Position - world.trialPlatform.position).magnitude, Is.LessThan(world.trialRadius), "the player stands on the trial platform");
            Assert.That(boss.IsAlive, Is.True);
            await VoidTrialPlatformTest.StandStill(game, 2f);
            Assert.That(game.player.State, Is.Not.EqualTo(CharacterState.Falling), "standing still, the player does not slide off the platform (user 2026-10-22: died on entering)");
            Assert.That(world.TrialActive, Is.True);

            lab.Clear();
            await Awaitable.NextFrameAsync();
            Assert.That(world.TrialActive, Is.False, "clearing kills the boss and ends the trial");
            Assert.That(world.TrialWon, Is.False, "a lab win does not lock the trial");
            Assert.That(world.holes.Count(h => h && h.Phase != HolePhase.Gone), Is.EqualTo(holes), "a lab win leaves the black holes");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Draft), "the greater void reward is offered as in a run");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (game.RunPhase == ArenaUiState.Draft)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The greater void reward could not be skipped.");
                game.ExecuteUiCommand(ArenaUiCommand.StartNextWave);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 8f, "play resumes after the reward");
            Assert.That(Query.Flat(game.player.Position - stood).magnitude, Is.LessThan(1f), "the player returns to where they stood");

            Assert.That(lab.Spawn(), Is.SameAs(world.hollow), "the trial can be entered again");
            Assert.That(world.TrialActive, Is.True);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task LabTrial_PlayerKnockedOffThePlatform_IsSetBackOnItAndTheTrialGoesOn()
        {
            var (game, world, lab) = await StartLab();
            lab.selected = EnemyKind.Hollow;
            lab.Spawn();
            Assert.That(world.TrialActive, Is.True);

            V3CombatSmokeTest.Teleport(game.player, world.trialPlatform.position + Vector3.right * (world.trialRadius + 1f));
            game.player.Knockback(Vector3.right, 8f, KnockKind.Hit);
            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();

            Assert.That(game.player.IsAlive, Is.True, "the lab player does not die from the fall (user 2026-10-22: every fall restarted the run)");
            Assert.That(game.player.State, Is.Not.EqualTo(CharacterState.Falling));
            Assert.That(world.TrialActive, Is.True, "the trial goes on");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial));
            Vector3 fromCentre = Query.Flat(game.player.Position - world.trialPlatform.position);
            Assert.That(fromCentre.magnitude, Is.LessThan(world.trialRadius - 2f), "set back well inside the edge");
            Assert.That(fromCentre.x, Is.GreaterThan(0f), "on the side it went over");
            await Awaitable.FixedUpdateAsync();
            Assert.That(Query.Flat(game.player.Position - world.trialPlatform.position).magnitude, Is.LessThan(world.trialRadius - 2f), "the knockback does not carry on after landing");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task TrialEntry_HollowHoldsItsFirstAttackForTheOpeningGrace()
        {
            var (game, world, lab) = await StartLab();
            lab.selected = EnemyKind.Hollow;
            lab.Spawn();
            float grace = world.trialOpeningGrace;
            Assert.That(grace, Is.GreaterThan(1.5f), "the grace outlasts the old 0.5 s cooldown plus the 0.8 s wind-up");

            float start = Time.time, firstWindup = -1f;
            while (Time.time - start < grace + 2f && world.TrialActive)
            {
                if (world.hollow.WindingUp) { firstWindup = Time.time - start; break; }
                await Awaitable.NextFrameAsync();
            }
            Assert.That(firstWindup, Is.GreaterThanOrEqualTo(grace - .1f), "no wind-up starts during the opening grace");
        }

        static async Task<(GameManager game, WorldMap world, EnemyLab lab)> StartLab()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active && Object.FindAnyObjectByType<EnemyLab>(), 12f, "scene, localization, map and lab");
            var game = GameManager.Instance;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The lab never reached combat; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Loadout) game.ExecuteUiCommand(ArenaUiCommand.ChooseLoadout, 0);
                await Awaitable.NextFrameAsync();
            }
            return (game, WorldMap.Active, Object.FindAnyObjectByType<EnemyLab>());
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
