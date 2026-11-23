using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>On the workshop page a research card's "now → after" line lies neither over its price nor over its button, and its
    /// title fits its box (V5WorkshopPage.LayResearch5). A tab with many cards lays them at their least height, where the line shares its row with the price
    /// and has only the room left of the coins; the void room's long lines did not fit there and ran on under the coin and the number
    /// on every one of its cards. A tab whose lines do not fit is laid with taller cards, the line on a row of its own: there the
    /// button's box reached into the line's box, its top border through the foot of the letters wherever the line was long enough to
    /// reach it (every skill card in English). The void room's enchant cards name the weapon, its anomaly and its Mk: in English
    /// that title was wider than its box at the smallest size it was drawn in and ran past the card's edge. The void room is opened as the trial's win opens it (the campaign's call for checks),
    /// with the gift's void energy and void alloy in the wallet; each open tab of the page is looked at, in Chinese and in English.</summary>
    [TestFixture]
    public class WorkshopCardLinesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            TestStartLanguage.Reset();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(150000)]
        public async Task EveryResearchTab_NoCardsLineRunsUnderItsPrice() => await LookAtEveryTab();

        [Test, BuildScene(Scene), Category("Integration"), Timeout(150000)]
        public async Task EveryResearchTab_InEnglish_NoCardsLineMeetsItsPriceOrItsButton()
        {
            TestStartLanguage.Use(StartupLanguage.English);
            await SceneManagerHelper.LoadSceneAsync(Scene);
            await LookAtEveryTab();
            Assert.That(ArenaText.IsChinese, Is.False, "the page was looked at in English");
        }

        static async Task LookAtEveryTab()
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

            // A late afternoon with every room open, the void room among them, and stock to price everything.
            campaign.DebugBeginAfternoon(campaign.rules.days);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Dusk), "the afternoon has begun");
            campaign.DebugGrant(500, 500, 100);
            // Two weapons in hand and two automatic ones, as a late run has: the void room then offers an enchant for each, and with
            // its skills, its Mk and its armor has more cards than fit at any height but the least.
            game.runWeapons.GiveToHand(0, WeaponForm.RocketLauncher);
            game.runWeapons.GiveToHand(1, WeaponForm.ScatterCannon);
            game.runWeapons.GiveToAuto(0, WeaponForm.OrbitBlades);
            game.runWeapons.GiveToAuto(1, WeaponForm.ShieldRing);
            // Their anomalies known, as after a few fights: the cards then name weapon and anomaly. The rocket launcher's second
            // anomaly gives the longest name a card can show.
            game.runWeapons.Hand[0].variant = 1;
            game.runWeapons.Hand[1].variant = 0;
            game.runWeapons.Auto[0].variant = 0;
            game.runWeapons.Auto[1].variant = 0;
            foreach (var weapon in game.runWeapons.Hand.Concat(game.runWeapons.Auto))
                weapon.revealed = true;
            campaign.DebugBeatProphet();
            await Seconds(.3f);
            if (game.RunPhase == ArenaUiState.Cutscene && WorldMap.Active) WorldMap.Active.SkipHoleIntro();
            var ui = campaign.Ui;
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Workshop);
            await Until(() => campaign.Stage == CampaignStage.Base, 6f, "the base visit opens");
            await Seconds(.5f);
            Assume.That(ui.BaseScreenShown, Is.EqualTo((V2Campaign.BaseScreen?)V2Campaign.BaseScreen.Workshop), "on the workshop page");

            // The weapons' Mk raised on the page as far as it goes (the run's Mk, which every weapon follows): the enchant cards'
            // titles then end in the Mk, their longest form.
            for (int i = 0; i < 12 && ui.DebugBaseParts().Contains("c.mk"); i++)
            {
                ui.DebugPressBase("c.mk");
                await Seconds(.12f);
            }
            Assume.That(game.runWeapons.Hand[0].mk, Is.GreaterThanOrEqualTo(9), "the weapons' Mk was raised on the page");

            var tabs = ui.DebugBaseParts().Where(id => id.StartsWith("c.tab.", StringComparison.Ordinal)).ToList();
            Assume.That(tabs, Does.Contain("c.tab.void"), "the void room has its tab once the Prophet is beaten: " + string.Join(" ", tabs));
            var wrong = new List<string>();
            int looked = 0, buttons = 0, titles = 0;
            string longest = "";
            foreach (string tab in tabs)
            {
                ui.DebugPressBase(tab);
                await Seconds(.35f);
                var lines = ui.DebugResearchCardLines();
                foreach (var (id, letters, price) in lines)
                {
                    looked++;
                    // Over each other by more than a quarter of the price's height each way (boxes that only touch do not count).
                    float across = Mathf.Min(letters.xMax, price.xMax) - Mathf.Max(letters.xMin, price.xMin);
                    float up = Mathf.Min(letters.yMax, price.yMax) - Mathf.Max(letters.yMin, price.yMin);
                    if (letters.width > 1f && across > price.height * .25f && up > price.height * .25f)
                        wrong.Add($"{tab} / {id}: the line's letters {Show(letters)} lie over the price {Show(price)}");
                }
                foreach (var (id, letters, button) in ui.DebugResearchCardButtons())
                {
                    buttons++;
                    // The line's box and the button's box over each other by more than a pixel each way.
                    float across = Mathf.Min(letters.xMax, button.xMax) - Mathf.Max(letters.xMin, button.xMin);
                    float up = Mathf.Min(letters.yMax, button.yMax) - Mathf.Max(letters.yMin, button.yMin);
                    if (letters.width > 1f && across > 1f && up > 1f)
                        wrong.Add($"{tab} / {id}: the line's letters {Show(letters)} lie over the button {Show(button)}");
                }
                foreach (var (id, letters, box) in ui.DebugResearchCardTitles())
                {
                    titles++;
                    if (letters > box + 1f) wrong.Add($"{tab} / {id}: the title's letters are {letters:0} wide in a box {box:0} wide");
                    if (id == "void.enchant.hand0") longest = $"{letters:0} in {box:0}";
                }
                if (tab == "c.tab.void") Assert.That(lines.Count, Is.GreaterThanOrEqualTo(8), "the void room shows its priced cards, more than fit at any height but the least: " + string.Join(" ", lines.Select(l => l.id)));
            }
            Assert.That(looked, Is.GreaterThan(8), "priced cards were looked at on the tabs " + string.Join(" ", tabs));
            Assert.That(buttons, Is.GreaterThan(8), "cards with a button were looked at on the tabs " + string.Join(" ", tabs));
            Assert.That(titles, Is.GreaterThan(8), "cards' titles were looked at on the tabs " + string.Join(" ", tabs));
            Assert.That(longest, Is.Not.Empty, "the main hand's enchant card (the longest title) was looked at");
            Debug.Log($"[WorkshopCardLines] {(ArenaText.IsChinese ? "Chinese" : "English")}: the longest title (\"{game.runWeapons.Hand[0].Label}\") is {longest} wide");
            Assert.That(wrong, Is.Empty, $"{wrong.Count} research cards (of {looked} with a price, {buttons} with a button, {titles} titles) have their \"now → after\" line under their price or their button, or a title wider than its box:\n  " + string.Join("\n  ", wrong));
        }

        static string Show(Rect r) => $"(x {r.xMin:0}..{r.xMax:0}, y {r.yMin:0}..{r.yMax:0})";

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
