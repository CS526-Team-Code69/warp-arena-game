using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>A flash over the base's page (a purchase's line) stands in the free room along the page's foot, between the keys' plate
    /// and the ways out: on neither page does it lie over a button, a card or a way out, a short line and a long one alike, and it stays
    /// on the screen. Out in the field the flash keeps its place in the middle of the screen.
    /// Day 0's walk is skipped with the campaign's own calls for checks, and the morning lasts a few seconds (a copy of the rules).</summary>
    [TestFixture]
    public class BasePageFlashPlaceTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        CampaignRules rulesCopy;

        [TearDown]
        public void TearDown()
        {
            if (rulesCopy) Object.Destroy(rulesCopy);
            rulesCopy = null;
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(150000)]
        public async Task AFlashOverTheBasePage_StandsInTheFoot_ClearOfEveryPartOfThePage()
        {
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            BeginDay1(game, campaign);
            await FinishTheMorning(game, campaign);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Dusk)), "day 1's afternoon");
            var ui = campaign.Ui;

            // Out in the field the flash stands in the middle of the screen, above its centre.
            ui.Flash(ArenaText.Pair("技能：风暴召唤升到 2 级", "Skill: Storm Call is now level 2"), 30f);
            await Seconds(.2f);
            var field = ui.DebugFlashRect();
            Assert.That(field.HasValue, Is.True, "the flash shows in the field");
            Assert.That(field.Value.center.y, Is.GreaterThan(Screen.height * .5f), "in the field it stands above the screen's middle");

            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Workshop);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Base), "the base visit is open");
            await Seconds(.4f);
            Assume.That(ui.BaseScreenShown, Is.EqualTo((V2Campaign.BaseScreen?)V2Campaign.BaseScreen.Workshop), "on the workshop page");

            string[] lines =
            {
                ArenaText.Pair("技能：风暴召唤升到 2 级", "Skill: Storm Call is now level 2"),
                ArenaText.Pair("护甲升到 3 级：生命上限 +5、充能上限 +10，决战过载：最终决战开局护盾 +30%，武器型号已经到 Mk 10，用合金最多只能升到这一级",
                    "Armor is now level 3: max HP +5, max charge +10. Final overload: the Final Battle opens with a shield of +30%. Your weapons are already at Mk 10, the highest alloy can take them"),
            };
            foreach (var screen in new[] { V2Campaign.BaseScreen.Workshop, V2Campaign.BaseScreen.Rocket })
            {
                if (ui.BaseScreenShown != screen)
                {
                    Assert.That(ui.DebugPressBase("h.switch"), Is.True, "the button to the other page presses");
                    await Seconds(.4f);
                }
                Assume.That(ui.BaseScreenShown, Is.EqualTo((V2Campaign.BaseScreen?)screen), "the page asked for shows");
                foreach (string line in lines)
                {
                    ui.Flash(line, 30f);
                    await Seconds(.2f);
                    var flash = ui.DebugFlashRect();
                    Assert.That(flash.HasValue, Is.True, $"the flash shows over the {screen} page");
                    var plate = flash.Value;
                    var parts = ui.DebugBaseParts();
                    Assert.That(parts, Is.Not.Empty, "the page has its parts");
                    var covered = parts.Where(id => ui.DebugBasePartRect(id) is Rect part && part.Overlaps(plate)).ToArray();
                    Assert.That(covered, Is.Empty, $"over the {screen} page the flash ({plate}) lies over no button, card or way out (\"{line}\")");
                    Assert.That(plate.xMin >= 0f && plate.xMax <= Screen.width && plate.yMin >= 0f && plate.yMax <= Screen.height, Is.True,
                        $"and stays on the {Screen.width} x {Screen.height} screen ({plate})");
                    var go = ui.DebugBasePartRect("f.go");
                    Assume.That(go.HasValue, Is.True, "the main way out is on the page");
                    Assert.That(plate.center.y, Is.EqualTo(go.Value.center.y).Within(go.Value.height * .5f), "level with the ways out, in the page's foot");
                }
            }
            ui.ClearFlash();
        }

        // ───── Helpers (as CampaignBaseRedesignTest's) ─────

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>The story mode from the title, up to Day 0's walk, with mornings of 3 seconds (a copy of the rules).</summary>
        async Task<V2Campaign> StartStory(GameManager game)
        {
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            rulesCopy = Object.Instantiate(campaign.rules);
            rulesCopy.morningSeconds = 3f;
            campaign.rules = rulesCopy;
            await CommandUntil(game, ArenaUiCommand.StartCampaign, () => game.CampaignRunning);
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            return campaign;
        }

        /// <summary>Day 0's night begun and ended at once (the campaign's calls for checks): its dawn starts day 1's morning. The player is
        /// protected from the morning's monsters.</summary>
        static void BeginDay1(GameManager game, V2Campaign campaign)
        {
            campaign.DebugBeginNight(0, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Night), "Day 0's night has begun");
            campaign.EndNight(false);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Day)), "the dawn began day 1's morning");
            game.player.ProtectFor(600f);
        }

        /// <summary>The morning's wave runs out and what is left of it is taken away without rewards, until the afternoon begins.</summary>
        static async Task FinishTheMorning(GameManager game, V2Campaign campaign)
        {
            float deadline = Time.realtimeSinceStartup + 45f;
            while (campaign.Stage != CampaignStage.Dusk)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The morning never ended: {campaign.Stage}, {game.RunPhase}, {game.waves.Remaining} enemies left.");
                if (game.RunPhase == ArenaUiState.Cutscene && WorldMap.Active) WorldMap.Active.SkipHoleIntro();
                if (game.waves.TimeExpired)
                    foreach (var enemy in game.waves.Alive.ToArray())
                    {
                        if (!enemy || !enemy.IsAlive) continue;
                        enemy.SuppressRewards = true;
                        enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                    }
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
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
