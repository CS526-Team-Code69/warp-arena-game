using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                                        
                                                                                                                                           
                                                                                         
    [TestFixture]
    [Category("V3Smoke")]
    public class VoidTrialPlatformTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task EnterThroughPortal_StandStill_PlayerStaysOnThePlatform()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map");
            var game = GameManager.Instance;
            var world = WorldMap.Active;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The first wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) world.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }

            var hole = world.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, game.waves.WaveIndex + 1);
            hole.Tick(2f);
            V3CombatSmokeTest.Teleport(game.player, hole.Centre);
            Assert.That(world.TryEnterPortal(game.player), Is.True, "the player enters the open portal");
            Assert.That(world.trialPlatform.GetComponent<CapsuleCollider>(), Is.Null, "the platform no longer stands on a capsule");

            await StandStill(game, 2f);
            Assert.That(game.player.State, Is.Not.EqualTo(CharacterState.Falling), "standing still, the player does not slide off the platform");
            Assert.That(world.TrialActive, Is.True, "the trial is still running");
                                                                                              
            Assert.That(Query.Flat(game.player.Position - world.trialPlatform.position).magnitude, Is.EqualTo(world.trialEntryDistance).Within(.3f), "the player stays where they were set down");
            Assert.That(game.player.Position.y, Is.EqualTo(0f).Within(.3f), "the player stands on the platform's top face");
        }

                                                                                                                                                  
        internal static async Task StandStill(GameManager game, float seconds)
        {
            var hollow = WorldMap.Active.hollow;
            hollow.enabled = false;
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until && game.player.State != CharacterState.Falling) await Awaitable.NextFrameAsync();
            hollow.enabled = true;
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
