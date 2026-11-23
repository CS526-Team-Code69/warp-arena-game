using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The afternoon goal's reminders (V2Campaign.Nudges.cs) name numbers: the lamps held, and the fortification's price and
    /// HP. While a reminder stays (the stock still short of the day's lamps after one more is made, a hurt base fortified one level) the
    /// goal is written again with the numbers as they are now. The afternoon is opened by the campaign's own call for checks.</summary>
    [TestFixture]
    public class CampaignNudgeNumbersTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task AfternoonGoal_NamesTheLampsHeldAndTheFortificationAsTheyAreNow()
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

            // A day whose lamps are at least two more than the stock, so one more made leaves the stock short still.
            int day = 1;
            while (day < campaign.rules.days && CampaignRules.ByDay(campaign.rules.dayLampsToCraft, day) < campaign.LampStock + 2) day++;
            campaign.DebugBeginAfternoon(day);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            await Awaitable.NextFrameAsync();
            int wanted = CampaignRules.ByDay(campaign.rules.dayLampsToCraft, day), stock = campaign.LampStock;
            Assume.That(campaign.LampsCraftable, Is.True, "the rocket can make lamps");
            Assume.That(stock + 2, Is.LessThanOrEqualTo(wanted), $"day {day} wants two lamps more than the stock holds");

            string Held(int lamps) => ArenaText.Pair($"现有 {lamps} 盏", $"you have {lamps}");
            Assert.That(campaign.ObjectiveText, Does.Contain(Held(stock)), "the goal names the lamps held");

            campaign.DebugGrant(campaign.rules.lampCost + 5000, 0, 0);
            Assert.That(campaign.TryCraftLamp(), Is.True, "one lamp is made");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(campaign.LampsWanted, Is.True, "the stock is still short of the day's lamps");
            Assert.That(campaign.ObjectiveText, Does.Contain(Held(stock + 1)), "the goal names the lamps held now: " + campaign.ObjectiveText);
            Assert.That(campaign.ObjectiveText, Does.Not.Contain(Held(stock)), "and no longer the old count");

            // A base that has lost more than a quarter of its HP stays worth fortifying after one level: the goal names the next level.
            campaign.DamageBase(0, campaign.BaseMaxHp * .6f, V2Campaign.BaseCentre);
            await Awaitable.NextFrameAsync();
            Assume.That(campaign.FortifyWanted, Is.True, "a hurt base is worth fortifying");
            string Levels() => $"{campaign.FortifyHpNow:0} → {campaign.FortifyHpNext:0}";
            string before = Levels();
            Assert.That(campaign.ObjectiveText, Does.Contain(before), "the goal names the fortification's HP");
            Assert.That(campaign.TryFortify(), Is.True, "the base is fortified one level");
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assume.That(campaign.FortifyWanted, Is.True, "still hurt, it is still worth fortifying");
            Assume.That(Levels(), Is.Not.EqualTo(before), "the next level has its own HP");
            Assert.That(campaign.ObjectiveText, Does.Contain(Levels()), "the goal names the next level's HP: " + campaign.ObjectiveText);
            Assert.That(campaign.ObjectiveText, Does.Not.Contain(before), "and no longer the level already bought");
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
