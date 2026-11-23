using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The saved Tyrant is switched on where it is asked to stand (WaveManager.SetDown). Its body is interpolated, and its transform
    /// used to be moved after the object was switched on, so the body came back to where the scene saved it: the map's middle, which in
    /// the story is inside the base (it stood on the base's roof for the whole final battle, out of every shot's reach). The first test
    /// reads only where the Tyrant stands, so it fails on the game as it was. The second holds the same for the Nash Prophet on the
    /// story's map, whose trial platform lies farther out than the scene saved it; that one stood right before as well.
    /// </summary>
    [TestFixture]
    public class BossBirthPlaceTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Story_TyrantSetDownBesideThePlayer_StandsThere_NotInsideTheBase()
        {
            var (game, _) = await StartStory();
            game.player.Invulnerable = true;
            Vector3 asked = V2Campaign.FindOpen(game.player.Position + new Vector3(12f, 0f, 3f), 2.6f);
            var tyrant = game.waves.SpawnLabTyrant(asked);
            Assume.That(tyrant && tyrant.IsAlive, Is.True, "the Tyrant was set down");
            for (int frame = 0; frame < 12; frame++) await Awaitable.NextFrameAsync();

            Vector3 at = tyrant.Position;
            Assert.That(Query.Flat(at - asked).magnitude, Is.LessThan(4f), $"the Tyrant asked to stand at {asked} stands there (a few steps on at most); it is at {at}");
            Assert.That(Mathf.Abs(at.y), Is.LessThan(.5f), $"it stands on the ground, not on a roof (height {at.y:0.0})");
            Assert.That(Mathf.Max(Mathf.Abs(at.x - V2Campaign.BaseCentre.x), Mathf.Abs(at.z - V2Campaign.BaseCentre.z)), Is.GreaterThan(V2Campaign.BaseHalf),
                "and not inside the base");
            tyrant.DiscardWithoutReward();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Story_VoidTrial_TheProphetStandsAtTheMiddleOfThePlatformWhereItIsNow()
        {
            var (game, campaign) = await StartStory();
            campaign.DebugBeginNight(0, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            campaign.EndNight(false);
            await Until(() => campaign.Stage == CampaignStage.Day && game.IsCombatActive && game.waves.Phase == WavePhase.WaveActive, 30f, "day 1's morning");
            game.player.Invulnerable = true;
            var map = WorldMap.Active;
            Assume.That(map.hollow && map.trialPlatform, Is.True, "the map has its trial platform and the Nash Prophet");
            Assume.That(map.EnterLabTrial(game.player), Is.True, "the void trial began");
            for (int frame = 0; frame < 8; frame++) await Awaitable.NextFrameAsync();

            Vector3 middle = map.trialPlatform.position, prophet = map.hollow.Position;
            Assert.That(Query.Flat(prophet - middle).magnitude, Is.LessThan(.5f),
                $"the Nash Prophet stands at the platform's middle {middle}; it is at {prophet}");
            Assert.That(Query.Flat(game.player.Position - prophet).magnitude, Is.GreaterThan(map.trialEntryDistance - 1.5f),
                "and the player is set down the entry distance from it, not beside it");
        }

        static async Task<(GameManager, V2Campaign)> StartStory()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 20f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            float deadline = Time.realtimeSinceStartup + 8f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 30f, "Day 0's walk begins");
            return (game, campaign);
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for " + operation + ".");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
