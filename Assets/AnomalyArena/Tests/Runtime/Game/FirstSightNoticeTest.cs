using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>A monster kind met for the first time (V2WorldLabels.FirstMeeting): every kind a first sight introduces has its one
    /// sentence, naming the kind and short enough for the notice's plate; and in the open mode a Spitter the collection has never met is
    /// named over its bar for the wave and, once its body is on the screen, brings its notice.</summary>
    [TestFixture]
    public class FirstSightNoticeTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        static string SpitterKey => ArenaCodexProgress.Key("enemy", (int)EnemyKind.Spitter);

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaPrefs.MemoryForTests, Is.True, "the test run keeps the saved choices in memory (TestSavedSettings)");
            ArenaPrefs.Delete(SpitterKey);
            ArenaCodexProgress.ForgetCache();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            ArenaPrefs.Delete(SpitterKey);
            ArenaCodexProgress.ForgetCache();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task FirstSpitter_IsNamedForTheWave_AndItsNoticeShows()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                string words = V2WorldLabels.FirstSightNotice(kind);
                if (!V2WorldLabels.IntroducesOnFirstSight(kind))
                {
                    Assert.That(words, Is.Empty, $"{kind} gets no notice");
                    continue;
                }
                Assert.That(words, Does.StartWith(ArenaText.Pair("新怪物 · ", "New monster · ") + ArenaText.Text("enemy." + kind)), $"{kind}'s notice names it");
                Assert.That(words.Length, Is.LessThanOrEqualTo(ArenaText.IsChinese ? 40 : 95), $"{kind}'s notice fits its plate: {words}");
            }

            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 4f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(60f);
            // The black hole's first-sight cutscene may come first; the wave runs once it is over.
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 20f, "the first wave runs");
            Assume.That(ArenaCodexProgress.EnemyDiscovered(EnemyKind.Spitter), Is.False, "no Spitter has come in the first wave");

            Vector3 side = Query.Flat(game.cam.transform.right).normalized;
            var spitter = game.waves.SpawnLab(EnemyKind.Spitter, game.player.Position + side * 3f, false, ElementKind.None);
            Assert.That(spitter, Is.Not.Null, "a Spitter can be set down beside the player");
            Assert.That(V2WorldLabels.NamedForFirstSight(EnemyKind.Spitter), Is.True, "a kind never met is named over its bar for the wave");
            string notice = V2WorldLabels.FirstSightNotice(EnemyKind.Spitter);
            // Notices show one at a time: a kind met earlier in the wave may still be ahead of it.
            await Until(() => V2WorldLabels.LastShown == notice, 25f, "its notice shows once its body is on the screen");
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
