using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class HoleIntroTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task FirstSight_ActiveUnfedHoleOnScreen_CutscenePlaysFeedsTheHoleAndResumes()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 4f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(30f);
            await Until(() => game.RunPhase == ArenaUiState.WaveActive || game.RunPhase == ArenaUiState.Cutscene, 10f, "the first wave starts");

            var map = WorldMap.Active;
            var hole = map.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active);
            Assume.That(hole, Is.Not.Null, "the arena has an active fixed hole");
            if (game.RunPhase != ArenaUiState.Cutscene)
            {

                Vector3 away = Query.Flat(game.player.Position - hole.Centre);
                away = away.sqrMagnitude > .01f ? away.normalized : Vector3.forward;
                Vector3 spot = hole.Centre + away * (hole.GravityRadius + 3f);
                game.player.GetComponent<Rigidbody>().position = spot;
                game.player.transform.position = spot;
                Physics.SyncTransforms();
            }

            await Until(() => game.RunPhase == ArenaUiState.Cutscene, 6f, "the cutscene starts on first sight of the hole");
            Assert.That(Time.timeScale, Is.Zero, "the battlefield is frozen during the cutscene");
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 12f, "the cutscene ends and play resumes");
            Assert.That(map.holes.Any(h => h && h.Feeds >= 3), Is.True, "the three fodder fed the hole");
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
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
