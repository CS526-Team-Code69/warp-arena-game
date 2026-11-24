using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// The story's max HP by day (since 2026-11-24): the starting max HP grows with the monsters' damage (CampaignRules.DayMaxHpBonus),
    /// so a monster of one kind takes as many hits to bring the player down on every day; dawn applies the day's growth and fills the HP.
    /// </summary>
    [TestFixture]
    public class StoryDayHpTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test]
        public void StartingMaxHp_FollowsTheMonstersDamageByDay()
        {
            var rules = ScriptableObject.CreateInstance<CampaignRules>();
            try
            {
                float[] expected = { 25f, 26f, 28f, 32f, 37f, 44f, 52f };
                for (int day = 1; day <= 7; day++)
                    Assert.That(25f + rules.DayMaxHpBonus(day, 25f), Is.EqualTo(expected[day - 1]), $"day {day}");
                Assert.That(rules.DayMaxHpBonus(0, 25f), Is.Zero, "Day 0 adds nothing");
                Assert.That(rules.DayMaxHpBonus(9, 25f), Is.EqualTo(rules.DayMaxHpBonus(7, 25f)), "a day past the table keeps the last day's");
                rules.playerHpFollowsMonsters = 0f;
                Assert.That(rules.DayMaxHpBonus(7, 25f), Is.Zero, "0 turns the growth off");
            }
            finally { Object.DestroyImmediate(rules); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Dawn_RaisesTheMaxHp_AndFillsIt()
        {
            var (game, campaign) = await StartStory();
            float starting = game.progression.Rules.startingStats.maxHp;
            campaign.DebugBeginNight(1, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            float before = game.player.maxHp;
            float bonus = campaign.rules.DayMaxHpBonus(2, starting);
            Assume.That(bonus, Is.GreaterThan(0f), "day 2 adds max HP");

            campaign.EndNight(false);
            await Until(() => campaign.Stage == CampaignStage.Day && campaign.Day == 2, 30f, "day 2's morning");
            Assert.That(game.player.maxHp, Is.EqualTo(before + bonus).Within(.01f), "dawn adds the day's max HP");
            Assert.That(game.player.Hp, Is.EqualTo(game.player.maxHp).Within(.01f), "and the HP is full");
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
