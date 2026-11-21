using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>What the story says about the way home after the base's redesign, in a running story. Day 1's afternoon: the draws offer
    /// only what is open by today (Tame yes, Twist and Infusion not yet); the character page lists a research line only once a level past
    /// its first is bought, at the workshop's level; the way home under the day plate shows each ship part's price until it is bought and
    /// a check after; standing beside the ship, E opens the base on the rocket page. Day 1's night: with no lamp lit and none in hand the
    /// goal sends the player back to the base to wait for dawn, and a lamp set down sends the player to the dark matter again. The
    /// afternoon and the night are opened with the campaign's own calls for checks, so no morning is played.</summary>
    [TestFixture]
    public class CampaignHomeWordsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Day1sAfternoon_OpenKinds_ResearchLines_TheWayHome_AndTheShipsE()
        {
            var (game, campaign) = await StartStory();
            campaign.DebugBeginAfternoon(1);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Dusk)), "day 1's afternoon");
            game.player.ProtectFor(600f);

            // The draws (the void trial's reward among them) offer only what is open by today.
            Assert.That((campaign.StatCards, campaign.SkillCards, campaign.TameCards), Is.EqualTo((true, true, true)), "the body, skills and Tame are open on day 1");
            Assert.That((campaign.InfusionCards, campaign.TwistCards), Is.EqualTo((false, false)), "Infusion (day 2) and Twist (day 4) are not");

            // The character page's research lines.
            var upgrades = new List<CharacterEntry>();
            campaign.CharacterUpgrades(upgrades);
            Assert.That(upgrades.Select(e => e.id), Has.None.StartsWith("study.research."), "a line at level 1, the day it opened, makes no line");
            campaign.DebugGrant(300, 200, 0);
            Assert.That(campaign.TryBuyResearchTier(V2Campaign.ResearchLine.Body), Is.True, "the body research bought to level 2");
            upgrades.Clear();
            campaign.CharacterUpgrades(upgrades);
            var body = upgrades.Find(e => e.id == "study.research." + V2Campaign.ResearchLine.Body);
            Assert.That(body, Is.Not.Null, "the body research shows once a level is bought");
            Assert.That(body.value, Is.EqualTo(ArenaText.Pair("2 级", "Level 2")), "at the level the workshop shows");

            // The way home under the day plate (the fifth pass's HUD).
            if (V5HudKit.On)
            {
                var plate = Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "DayPlate");
                Assert.That(plate, Is.Not.Null, "the day plate is up");
                Text Mark(string part) => plate.Find("Home/" + part + "Mark").GetComponent<Text>();
                await Until(() => Mark("Engine").text == "26", 3f, "the way home shows the engine's price");
                Assert.That((Mark("Hull").text, Mark("Tank").text), Is.EqualTo(("54", "85")), "the hull's and the fuel tank's prices");
                Assert.That(Mark("Fuel").text, Does.StartWith("0").And.Contain("/ 100"), "the fuel, x / 100");
                Assert.That(campaign.TryBuyShipPart(V2Campaign.ShipPartKind.Engine), Is.True, "the engine bought");
                await Until(() => Mark("Engine").text == "✓", 3f, "the engine's check");
                Assert.That(plate.Find("Home/EngineCoin").gameObject.activeSelf, Is.False, "a part done shows no price");
            }

            // E beside the ship opens the base on the rocket page.
            var asked = new List<V2Campaign.BaseScreen>();
            campaign.BaseScreenRequested += asked.Add;
            campaign.DebugPlacePlayer(V2Campaign.ShipCentre + Vector3.back * (V2Campaign.ShipHalfWidth + 1.2f));
            string prompt = ArenaText.Pair($"按 {ArenaKeys.InteractKey} 查看飞船", $"Press {ArenaKeys.InteractKey} to check the ship");
            await Until(() => campaign.PromptText == prompt, 3f, "the ship's prompt beside the ship");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (campaign.Stage != CampaignStage.Base)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"E beside the ship opened nothing ({campaign.Stage}, prompt \"{campaign.PromptText}\")");
                campaign.debugPressE = true;
                await Awaitable.NextFrameAsync();
            }
            campaign.debugPressE = false;
            Assert.That((campaign.BaseFromDusk, campaign.BaseScreenNow), Is.EqualTo((true, V2Campaign.BaseScreen.Rocket)), "a visit from the afternoon, on the rocket page");
            Assert.That(asked, Is.EqualTo(new[] { V2Campaign.BaseScreen.Rocket }), "the screen layer was asked for the rocket page");
            campaign.BaseExits()[0].go();
            Assert.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "back out into the afternoon");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Day1sNight_NoLampLeft_TheGoalSendsHome_ALampSendsBackToTheDarkMatter()
        {
            var (game, campaign) = await StartStory();
            campaign.DebugBeginNight(1, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 0);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Night)), "day 1's night");
            game.player.ProtectFor(600f);
            Assume.That(campaign.TryNearestNode(V2Campaign.NodeKind.Dark, campaign.DoorPoint, out _), Is.True, "the night has dark matter to mine");

            var goal = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None).First(t => t.name == "Objective");
            string home = ArenaText.Pair("可以回基地等天亮", "You can head back to the base and wait for dawn");
            await Until(() => goal.text.Contains(home), 3f, "with no lamp lit and none in hand the goal sends the player home");
            Assert.That(campaign.ObjectivePoint, Is.EqualTo(campaign.DoorPoint), "its marker on the base door");

            campaign.DebugPlaceLamp(campaign.DoorPoint + Vector3.back * 3f);
            await Until(() => !goal.text.Contains(home), 3f, "a lamp lit on the ground sends the player to the dark matter again");
            Assert.That(goal.text, Does.Contain(ArenaText.Pair("在灯光里采集暗物质", "Mine dark matter in the lamplight")), "the night's goal");
            Assert.That(campaign.ObjectivePoint, Is.Not.EqualTo(campaign.DoorPoint), "its marker on the dark matter");
        }

        // ───── Helpers (as CampaignBaseRedesignTest's) ─────

        static async Task<(GameManager, V2Campaign)> StartStory()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
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
            return (game, campaign);
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
