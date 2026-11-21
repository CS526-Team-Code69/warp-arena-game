using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The base's interface for its two pages (V2Campaign.Redesign.cs) in a running story, on day 1's afternoon. The workshop lists
    /// what opens on which day (no tab sells new weapons, the body research leaves max HP and max charge to armor, a locked tab says when
    /// it opens) and what opens tomorrow. The ship's E opens a base visit on the rocket page; its two ways out are the afternoon and the
    /// night. The ship's three parts are bought once each, in any order, the fuel tank first letting the dark matter held into the fuel up
    /// to a full tank, and the four steps let the ship ignite. The base's fortification raises its max HP. Nothing opens in the morning.
    /// On the screen (the fifth pass's pages): the visit shows the rocket page, no part of the old four-zone page and no study window; the
    /// rocket page's buttons buy (a lamp), its button at the top right switches to the workshop, which shows the weapon Mk, armor, the
    /// research tabs, the fortification and the ways out.
    /// Day 0's walk is skipped with the campaign's own calls for checks, and the mornings last a few seconds (a copy of the rules).</summary>
    [TestFixture]
    public class CampaignBaseRedesignTest
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
        public async Task Day1sAfternoon_TheWorkshopTheRocketAndTheFortification_FollowTheBaseRules()
        {
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            var asked = new List<V2Campaign.BaseScreen>();
            campaign.BaseScreenRequested += asked.Add;
            BeginDay1(game, campaign);

            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            Assert.That((campaign.Stage, asked.Count), Is.EqualTo((CampaignStage.Day, 0)), "the morning opens no page");

            await FinishTheMorning(game, campaign);
            Assume.That((campaign.Day, campaign.Stage), Is.EqualTo((1, CampaignStage.Dusk)), "day 1's afternoon");

            // What opens on which day, as the workshop lists it on day 1.
            var tabs = campaign.BuildStudy();
            V2Campaign.StudyTab Tab(string key) => tabs.Find(t => t.key == key);
            Assert.That(tabs.Exists(t => t.key == "arm.new"), Is.False, "no tab sells new weapons");
            Assert.That(new[] { "arm.upgrade", "arm.tame", "eng.body", "eng.skill" }.Select(key => Tab(key)?.locked), Is.All.False,
                "the weapon Mk, armor, Tame, the body and skills are open on day 1");
            Assert.That((Tab("eng.infuse").locked, Tab("eng.infuse").lockedNote), Is.EqualTo((true, ArenaText.Pair("明天开放", "Opens tomorrow"))),
                "infusion opens tomorrow");
            Assert.That((Tab("arm.twist").locked, Tab("arm.twist").lockedNote), Is.EqualTo((true, ArenaText.Pair("第 4 天开放", "Opens on day 4"))),
                "Twist opens on day 4");
            Assert.That(campaign.TomorrowOpens(), Is.EqualTo(ArenaText.Pair("明天开放：附魔", "Opens tomorrow: Infusion")), "what opens tomorrow, in one sentence");
            var body = Tab("eng.body").entries.Select(e => e.id).ToArray();
            Assert.That(body, Has.No.Member("stat.MaxHp"), "the body research sells no max HP");
            Assert.That(body, Has.No.Member("stat.MaxEnergy"), "nor max charge");
            Assert.That(body.Last(), Is.EqualTo("tier.Body"), "its research level comes last");

            // The ship's E: a base visit opens on the rocket page, with two ways out. The player stands beside the ship, well away from the
            // base door, so that the way back out can be told apart from the door's.
            V3CombatSmokeTest.Teleport(game.player, V2Campaign.ShipCentre + new Vector3(10f, 0f, 0f));
            await Awaitable.NextFrameAsync();
            var shipSide = game.player.Position;
            Assert.That(Vector3.Distance(shipSide, campaign.DoorPoint), Is.GreaterThan(5f), "the ship's side is not the base door");
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            Assert.That((campaign.Stage, campaign.BaseFromDusk, campaign.BaseScreenNow), Is.EqualTo((CampaignStage.Base, true, V2Campaign.BaseScreen.Rocket)),
                "the visit is open, from the afternoon, on the rocket page");
            Assert.That(asked, Is.EqualTo(new[] { V2Campaign.BaseScreen.Rocket }), "the screen layer was asked for it");
            var ui = campaign.Ui;
            Assert.That(ui.BaseScreenShown, Is.EqualTo(V2Campaign.BaseScreen.Rocket), "the screen shows the rocket page");
            Assert.That(ui.transform.Find("BasePanel/Page5/ZoneControl"), Is.Null, "no zone of the old four-zone page");
            ui.OpenStudy("eng.body");
            Assert.That(ui.StudyOpen, Is.False, "the study window does not open over the two pages");
            var exits = campaign.BaseExits();
            Assert.That(exits.Select(e => (e.label, e.primary)), Is.EqualTo(new[]
            {
                (ArenaText.Pair("出门 · 继续下午", "Head out · back to the afternoon"), false), (ArenaText.Pair("入夜", "Nightfall"), true),
            }), "back to the afternoon, and into the night as the main way out");
            Assert.That(exits[1].note, Is.EqualTo(ArenaText.Pair("带上矿灯去采暗物质", "Take your lamps and mine dark matter")), "the night's way out says what it is for");

            // The ship's three parts in any order, and the fuel.
            campaign.DebugGrant(300, 0, 150);
            int alloy = campaign.Alloy, dark = campaign.Dark;
            Assert.That(campaign.ShipParts().Select(p => (p.kind, p.done, p.price.alloy)), Is.EqualTo(new[]
            {
                (V2Campaign.ShipPartKind.Engine, false, 26), (V2Campaign.ShipPartKind.Hull, false, 54), (V2Campaign.ShipPartKind.Tank, false, 85),
            }), "three parts to do, with their prices");
            Assert.That((campaign.HomeStepsLeft, campaign.CanAddFuel, campaign.TryAddFuel()), Is.EqualTo((4, false, false)), "no dark matter goes in before the fuel tank");
            Assert.That(campaign.TryBuyShipPart(V2Campaign.ShipPartKind.Tank), Is.True, "the fuel tank first");
            Assert.That(campaign.TryBuyShipPart(V2Campaign.ShipPartKind.Tank), Is.False, "each part once");
            Assert.That(campaign.TryAddFuel(), Is.True, "then the dark matter held goes in");
            Assert.That((campaign.Fuel, campaign.FuelTop, campaign.Dark), Is.EqualTo((100, 100, dark - 100)), "up to a full tank, the rest kept");
            Assert.That((campaign.HomeStepsLeft, campaign.CanIgnite), Is.EqualTo((2, false)), "the engine and the hull still to do");
            Assert.That(campaign.TryBuyShipPart(V2Campaign.ShipPartKind.Hull) && campaign.TryBuyShipPart(V2Campaign.ShipPartKind.Engine), Is.True, "then the hull and the engine");
            Assert.That((campaign.HomeStepsLeft, campaign.CanIgnite, campaign.Alloy), Is.EqualTo((0, true, alloy - 165)), "four steps done for 165 alloy: the ship can ignite");
            Assert.That(campaign.ShipParts().Select(p => p.done), Is.All.True, "every part shows done");

            // The base's fortification.
            var price = campaign.FortifyPrice;
            Assume.That(price.HasValue, Is.True, "the base is not at its top level");
            int before = campaign.Alloy;
            Assert.That((campaign.FortifyLevel, campaign.FortifyHpNow, campaign.FortifyHpNext, price.Value.alloy), Is.EqualTo((1, 500f, 750f, 10)),
                "level 1 holds 500 HP; level 2, 750, costs 10 alloy");
            Assert.That(campaign.TryFortify(), Is.True, "fortified");
            Assert.That((campaign.FortifyLevel, campaign.BaseMaxHp, campaign.Alloy), Is.EqualTo((2, 750f, before - 10)), "level 2 and its max HP, paid for");

            // The rocket page on the screen, drawn again from the stock within a quarter second: its buttons buy, and the one at the top
            // right switches to the workshop on the same visit.
            await Seconds(.4f);
            Assert.That(ui.DebugBaseParts(), Is.SupersetOf(new[] { "c.lamp", "c.ignite", "h.switch", "f.go", "f.exit.0" }),
                "the lamp's and the ignition's buttons, the button to the workshop and the two ways out");
            Assert.That(ui.DebugBaseParts(), Has.None.StartsWith("c.part."), "no part's button once all three are done");
            int lamps = campaign.LampStock;
            Assert.That(ui.DebugPressBase("c.lamp"), Is.True, "the lamp's button presses");
            Assert.That(campaign.LampStock, Is.EqualTo(lamps + 1), "a lamp made through the page");
            Assert.That(ui.DebugPressBase("h.switch"), Is.True, "the button to the workshop presses");
            Assert.That((ui.BaseScreenShown, campaign.BaseScreenNow), Is.EqualTo(((V2Campaign.BaseScreen?)V2Campaign.BaseScreen.Workshop, V2Campaign.BaseScreen.Workshop)),
                "the workshop on the same visit");
            Assert.That(ui.DebugBaseParts(), Is.SupersetOf(new[] { "c.mk", "c.armor", "c.tab.eng.body", "c.tab.eng.skill", "h.fortify", "h.switch", "f.go" }),
                "the weapon Mk, armor, the research tabs, the fortification, the button to the rocket page and the main way out");

            // The workshop's button on the same visit, and the way back out.
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Workshop);
            Assert.That((campaign.Stage, campaign.BaseScreenNow, asked.Last()), Is.EqualTo((CampaignStage.Base, V2Campaign.BaseScreen.Workshop, V2Campaign.BaseScreen.Workshop)),
                "the other page, on the same visit");
            campaign.BaseExits()[0].go();
            Assert.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the first way out leads back into the afternoon");
            Assert.That(Vector3.Distance(game.player.Position, shipSide), Is.LessThan(1.5f), "out again where the visit began, beside the ship, not at the door");
        }

        // ───── Helpers (as SaveStoryTest's) ─────

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
                if (game.waves.TimeExpired) KillAll(game);
                await Awaitable.NextFrameAsync();
            }
        }

        static void KillAll(GameManager game)
        {
            foreach (var enemy in game.waves.Alive.ToArray())
            {
                if (!enemy || !enemy.IsAlive) continue;
                enemy.SuppressRewards = true;
                enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
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
