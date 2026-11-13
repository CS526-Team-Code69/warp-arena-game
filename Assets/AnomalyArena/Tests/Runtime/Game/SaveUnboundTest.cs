using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>A run no slot was bound for writes no save: the open mode's run passes its wave 1 save point, the story's passes day 1's
    /// morning save point, and every slot of both modes stays empty. A slot bound for the other mode does not reach the run that starts
    /// (the binding is used up). The saves are kept in memory.</summary>
    [TestFixture]
    public class SaveUnboundTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaSaves.InMemoryForTests, Is.True, "the saves are kept in memory during the tests");
            ArenaSaves.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ArenaSaves.ResetForTests();
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task AnUnboundOpenRun_WritesNoSave()
        {
            var game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 8f, "the supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            Assume.That(game.State, Is.EqualTo(GameState.Playing), "the run is on, past wave 1's save point");
            await Awaitable.NextFrameAsync();

            Assert.That(AllSlotsEmpty(), Is.True, "no slot of either mode holds a save");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task AnUnboundStoryRun_WritesNoSave_EvenWithASlotBoundForTheOpenMode()
        {
            ArenaSaves.Bind(SaveMode.Open, 0);
            var game = await ReadyScene();
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            await CommandUntil(game, ArenaUiCommand.StartCampaign, () => game.CampaignRunning);
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            Assert.That(ArenaSaves.BoundSlot(SaveMode.Open), Is.EqualTo(-1), "the open mode's binding is used up by the run that started");

            // Day 0's night begun and ended at once (the campaign's calls for checks): day 1's morning save point is passed.
            campaign.DebugBeginNight(0, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            campaign.EndNight(false);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Day)), "day 1's morning has begun");
            await Awaitable.NextFrameAsync();

            Assert.That(AllSlotsEmpty(), Is.True, "no slot of either mode holds a save");
        }

        static bool AllSlotsEmpty() => ArenaSaves.List(SaveMode.Story).Concat(ArenaSaves.List(SaveMode.Open)).All(s => s.empty)
            && Enumerable.Range(0, ArenaSaves.SlotCount).All(slot => !ArenaSaves.HasEntryForTests(SaveMode.Story, slot) && !ArenaSaves.HasEntryForTests(SaveMode.Open, slot));

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
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
