using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The story mode's black hole is fed before it can be attacked (V2Campaign.HoleFeed.cs): until it has swallowed
    /// rules.holeFeedGoal monsters a hit starts no check; then it is awake and the first hit starts the timed check. Its pull is a
    /// little faster than the player's walk. The afternoon is opened by the campaign's own call for checks, so the test need not play a
    /// morning, and the hole's first-sight cutscene is ended at once (WorldMap.SkipHoleIntro).</summary>
    [TestFixture]
    public class CampaignHoleFeedTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Hole_TakesNoHitsUntilItIsFed_ThenTheFirstHitStartsItsCheck()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");

            campaign.DebugBeginAfternoon(1);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            var map = WorldMap.Active;
            var hole = map ? map.CampaignHole : null;
            Assume.That(hole, Is.Not.Null, "the campaign's map has its black hole");
            Assume.That(campaign.HoleFeedGoal, Is.GreaterThan(1), "the rules ask for the hole to be fed");

            // Beside the hole, outside its pull: the first sight plays the cutscene, which the test ends at once.
            campaign.DebugPlacePlayer(hole.Centre + new Vector3(-(hole.GravityRadius + 7f), 0f, 0f));
            float sight = Time.realtimeSinceStartup + 20f;
            while (!campaign.HoleSeen)
            {
                if (Time.realtimeSinceStartup > sight) Assert.Fail("the black hole was never met");
                if (map.HoleIntroPlaying) map.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
            await Awaitable.NextFrameAsync();

            Assert.That(campaign.HoleAwake, Is.False, "met, the hole is not awake yet");
            Assert.That(campaign.HoleFed, Is.Zero, "the cutscene's own three do not count");
            Assert.That(hole.pullSpeed, Is.EqualTo(game.player.moveSpeed * campaign.rules.holePullFactor).Within(.01f), "it pulls a little faster than the player walks");
            Assert.That(hole.pullSpeed, Is.GreaterThan(game.player.moveSpeed).And.LessThan(game.player.moveSpeed * 2f), "faster than a walk, slower than a sprint");

            Assume.That(campaign.HoleTarget, Is.Not.Null, "the hole has its body for hits");
            campaign.HoleTarget.ReceiveDamage(new DamageInfo { amount = 50f, dealer = game.player, source = DamageSource.Hit });
            Assert.That(campaign.CheckState, Is.EqualTo(HoleCheckState.Idle), "a hit while it is being fed starts no check");

            campaign.DebugFeedHole(campaign.HoleFeedGoal - 1);
            Assert.That((campaign.HoleFed, campaign.HoleAwake), Is.EqualTo((campaign.HoleFeedGoal - 1, false)), "one short of the goal it is still being fed");

            campaign.DebugFeedHole(1);
            Assert.That(campaign.HoleAwake, Is.True, "with the goal swallowed it is awake");
            Assert.That(campaign.HoleLureAlive, Is.Zero, "its packs have left");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.HoleTarget.gameObject.activeInHierarchy, Is.True, "awake, its body shows for hits");

            campaign.HoleTarget.ReceiveDamage(new DamageInfo { amount = 5f, dealer = game.player, source = DamageSource.Hit });
            Assert.That(campaign.CheckState, Is.EqualTo(HoleCheckState.Running), "the first hit once it is awake starts the timed check");
            Assert.That(campaign.CheckDealt, Is.EqualTo(5f).Within(.01f), "and counts");
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
