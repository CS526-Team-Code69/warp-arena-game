using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>top-C-P35 (2026-11-10): a fall in the campaign's afternoon is no night death. Only a death at night raises
    /// CampaignEvents.NightDeath, which the analytics count as the day's night_death; the afternoon fall still wakes the player at the
    /// base with the afternoon going on. The afternoon and the night are opened by hand (V2Campaign.BeginDusk and BeginNight, as the end
    /// of a day's wave and the base door open them) so the test need not play through Day 0 and a whole morning.</summary>
    [TestFixture]
    public class CampaignNightDeathTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        int nightDeaths;

        void CountNightDeath(CampaignCost lost, int day) => nightDeaths++;

        [SetUp]
        public void Listen()
        {
            nightDeaths = 0;
            CampaignEvents.NightDeath += CountNightDeath;
        }

        [TearDown]
        public void StopListening() => CampaignEvents.NightDeath -= CountNightDeath;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task FallInTheAfternoon_IsNoNightDeath_ButADeathAtNightIs()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            var beginDusk = typeof(V2Campaign).GetMethod("BeginDusk", Private);
            var beginNight = typeof(V2Campaign).GetMethod("BeginNight", Private);
            var knockedOut = typeof(V2Campaign).GetField("knockedOut", Private);
            Assume.That(beginDusk != null && beginNight != null && knockedOut != null, Is.True, "V2Campaign still has BeginDusk, BeginNight and knockedOut");
            bool KnockedOut() => (bool)knockedOut.GetValue(campaign);

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");

            beginDusk.Invoke(campaign, null);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            Assert.That(campaign.CatchesPlayerDeath(), Is.True, "the campaign catches a death in the afternoon");
            // Wall damage is not an attack, so hit protection does not block it.
            Assert.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player falls in the afternoon");
            await Until(KnockedOut, 5f, "the campaign catches the afternoon fall");
            Assert.That(nightDeaths, Is.Zero, "a fall in the afternoon is no night death");
            await Until(() => !KnockedOut() && game.player.IsAlive, 10f, "the player gets up at the base");
            Assert.That((campaign.Stage, game.State), Is.EqualTo((CampaignStage.Dusk, GameState.Playing)), "the afternoon goes on");
            Assert.That(nightDeaths, Is.Zero, "getting up raises no night death either");

            beginNight.Invoke(campaign, null);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Night), "the night has begun");
            Assert.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player dies at night");
            await Until(KnockedOut, 5f, "the campaign catches the death at night");
            Assert.That(nightDeaths, Is.EqualTo(1), "a death at night is a night death");
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
