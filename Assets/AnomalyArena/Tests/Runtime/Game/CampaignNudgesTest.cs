using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The afternoon's reminders (V2Campaign.Nudges.cs): from Day 1 the goal names the night's lamps while the stock is short of
    /// the day's lamps, and the base's fortification until it has been fortified once; each sentence goes once that is done. The
    /// afternoon is opened by the campaign's own call for checks, so the test need not play a morning.</summary>
    [TestFixture]
    public class CampaignNudgesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task AfternoonGoal_NamesTheLampsAndTheFortification_UntilTheyAreDone()
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
            await Awaitable.NextFrameAsync();
            int wanted = campaign.rules.NightLampsOn(1);
            Assume.That(campaign.LampsCraftable, Is.True, "the rocket can make lamps on Day 1");
            Assume.That(campaign.LampStock, Is.LessThan(wanted), "the stock starts short of the day's lamps");

            string lamp = ArenaText.Pair("火箭页造矿灯", "rocket page"), fortify = ArenaText.Pair("基地可以加固", "can be fortified");
            Assert.That(campaign.LampsWanted, Is.True, "the lamps are wanted");
            Assert.That(campaign.ObjectiveText, Does.Contain(lamp), "the afternoon's goal says where the lamps are made");
            Assert.That(campaign.FortifyWanted, Is.True, "a base never fortified is worth fortifying");
            Assert.That(campaign.ObjectiveText, Does.Contain(fortify), "the afternoon's goal says the base can be fortified");
            Assert.That(campaign.ObjectiveText, Does.Not.Contain("..").And.Not.Contain("。。"), "no sentence ends twice: " + campaign.ObjectiveText);

            // Enough alloy for the day's lamps and a fortification.
            campaign.DebugGrant(campaign.rules.lampCost * wanted + 2000, 0, 0);
            while (campaign.LampStock < wanted) Assert.That(campaign.TryCraftLamp(), Is.True, "a lamp is made");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.LampsWanted, Is.False, "with the day's lamps made none is wanted");
            Assert.That(campaign.ObjectiveText, Does.Not.Contain(lamp), "and the goal no longer names them");
            Assert.That(campaign.ObjectiveText, Does.Contain(fortify), "the fortification is still named");

            Assert.That(campaign.TryFortify(), Is.True, "the base is fortified once");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.FortifyWanted, Is.False, "fortified once and unhurt, it is not pointed at again");
            Assert.That(campaign.ObjectiveText, Does.Not.Contain(fortify), "and the goal no longer names it");
        }

        /// <summary>Since 2026-11-24 the base gives rules.dailyFreeLamps at each dusk, and lamps go down only after dark: the lamp key
        /// sets none down in the afternoon and one at night.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Lamps_TheBaseGivesSomeAtDusk_AndTheyGoDownOnlyAfterDark()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            Assume.That(campaign.rules.dailyFreeLamps, Is.GreaterThan(0), "the rules give lamps each day");

            float deadline = Time.realtimeSinceStartup + 5f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"StartCampaign timed out in {game.RunPhase}");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign, -1);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");

            int before = campaign.LampStock;
            campaign.DebugBeginAfternoon(1);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            Assert.That(campaign.LampStock, Is.EqualTo(before + campaign.rules.dailyFreeLamps), "the base gives its lamps at dusk");
            await Awaitable.NextFrameAsync();

            int stock = campaign.LampStock;
            campaign.debugPressF = true;
            for (int i = 0; i < 5; i++) await Awaitable.NextFrameAsync();
            Assert.That(campaign.debugPressF, Is.False, "the afternoon read the lamp key");
            Assert.That(campaign.LampStock, Is.EqualTo(stock), "and set no lamp down");

            campaign.DebugBeginNight(1, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 2);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Night), "the night has begun");
            float pressing = Time.realtimeSinceStartup + 5f;
            while (campaign.LampStock == 2 && Time.realtimeSinceStartup < pressing)
            {
                campaign.debugPressF = true;
                await Awaitable.NextFrameAsync();
            }
            Assert.That(campaign.LampStock, Is.EqualTo(1), "at night the lamp key sets one down");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task AfternoonGoal_NamesTheFortification_WhenTheLastMorningCostAFifthOfTheBase_EvenAtFullHp()
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
            campaign.DebugBeginAfternoon(2);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            campaign.DebugGrant(4000, 0, 0);
            Assert.That(campaign.TryFortify(), Is.True, "the base is fortified once");
            await Awaitable.NextFrameAsync();
            Assume.That(campaign.BaseHp, Is.EqualTo(campaign.BaseMaxHp).Within(.5f), "the base stands at full HP");
            Assume.That(campaign.FortifyPrice, Is.Not.Null, "a higher level is for sale");
            string fortify = ArenaText.Pair("基地可以加固", "can be fortified");
            Assert.That(campaign.FortifyWanted, Is.False, "fortified once, at full HP and with no morning's loss, it is not pointed at");

            campaign.DebugMorningLoss(campaign.BaseMaxHp * .25f);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.FortifyWanted, Is.True, "a morning that took 25% of the max HP points at it, though the repair filled the base");
            Assert.That(campaign.ObjectiveText, Does.Contain(fortify), "and the afternoon's goal names it");

            campaign.DebugMorningLoss(campaign.BaseMaxHp * .15f);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.FortifyWanted, Is.False, "a morning that took 15% does not");
            Assert.That(campaign.ObjectiveText, Does.Not.Contain(fortify), "and the goal no longer names it");
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
