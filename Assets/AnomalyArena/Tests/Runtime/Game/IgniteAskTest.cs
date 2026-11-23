using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// The ignition asks first. Once it begins the run cannot go back: the final battle follows and its save is the only one left.
    /// So each of the player's own steps into it shows a question that says so, in whole sentences in both languages, its last line
    /// clear of its two answers: the rocket page's ignition button on any day (here on day 1's afternoon, days before the last planned
    /// one), "Wait for dawn" when the dawn it brings starts the ignition (the last planned day is over and the ship is ready), and the
    /// dawn after a fall in the dark on such a night. The answer that waits leaves the run where it was: the base page still open, the
    /// ship still ready; after a fall, the night going on with the player on their feet at the base door. The answer that ignites
    /// starts the failed ignition, and the final battle's save follows it. A final battle lost after that is retried from itself only:
    /// the afternoon its slot still keeps is not offered. A dawn that brings an ordinary day asks nothing. The pictures go to the copy's
    /// Logs/ignite-ask-*.png. Day 0's walk is skipped with the campaign's own calls for checks; the saves are kept in memory.
    /// </summary>
    [TestFixture]
    public class IgniteAskTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const int Slot = 0;
        CampaignRules rulesCopy;
        bool savedArt;

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaSaves.InMemoryForTests, Is.True, "the saves are kept in memory during the tests");
            savedArt = Art.Enabled;
            ArenaSaves.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ArenaSaves.ResetForTests();
            WorldMap.NextMapIndex = -1;
            if (rulesCopy) Object.Destroy(rulesCopy);
            rulesCopy = null;
            Art.Enabled = savedArt;
            TestStartLanguage.Reset();
            Time.timeScale = 1f;
        }

        [TestCase(StartupLanguage.Chinese)]
        [TestCase(StartupLanguage.English)]
        [BuildScene(Scene), Category("Integration"), Timeout(300000)]
        public async Task TheIgnitionButton_AsksFirst_WaitingKeepsThePage_IgnitingStartsIt_AndTheLostBattleOffersNoAfternoon(string language)
        {
            bool zh = language == StartupLanguage.Chinese;
            string Words(string chinese, string english) => zh ? chinese : english;
            TestStartLanguage.Use(language);
            ArenaSaves.Bind(SaveMode.Story, Slot);
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var game = await ReadyScene();
            Assume.That(V5HudKit.On, Is.True, "the fifth pass's look is on outside the Alpha");
            Assume.That(ArenaText.IsChinese, Is.EqualTo(zh), "the game started in the language asked for");
            string tag = $"{(zh ? "zh" : "en")}-{(Art.Enabled ? "art" : "whitebox")}";

            // Day 1's afternoon (its save written), and the ship made ready there: days before the last planned one.
            var campaign = await StartStory(game);
            BeginDay1(game, campaign);
            await FinishTheMorning(game, campaign);
            var saved = ArenaSaves.List(SaveMode.Story)[Slot];
            Assume.That((saved.empty, saved.day, saved.phase), Is.EqualTo((false, 1, SavePhase.Afternoon)), "the afternoon's save is in the slot");
            Assume.That(campaign.Day, Is.LessThan(campaign.rules.days), "long before the last planned day");
            MakeTheShipReady(campaign);
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            var ui = campaign.Ui;
            await Until(() => ui.BaseScreenShown == V2Campaign.BaseScreen.Rocket && ui.DebugBaseParts().Contains("c.ignite"), 5f, "the rocket page with its ignition button");
            await Frames(5);

            // The button asks first.
            Assert.That(ui.DebugPressBase("c.ignite"), Is.True, "the ignition button presses");
            await Frames(2);
            Assert.That((ui.ConfirmOpen, campaign.Stage, game.RunPhase), Is.EqualTo((true, CampaignStage.Base, ArenaUiState.Base)),
                "the button asks first: the question is up, nothing has ignited and the visit is still open");
            var panel = ui.transform.Find("Confirm/Panel") as RectTransform;
            Assume.That(panel, Is.Not.Null, "the question's panel");
            var title = panel.Find("Title").GetComponent<Text>();
            var body = panel.Find("Body").GetComponent<Text>();
            var answers = panel.GetComponentsInChildren<Button>(true);
            Assume.That(answers.Length, Is.EqualTo(2), "the question's two answers");
            var yes = answers[0].transform.Find("Label").GetComponent<Text>();
            var no = answers[1].transform.Find("Label").GetComponent<Text>();
            Assert.That(title.text, Is.EqualTo(Words("点火后就回不来了", "No way back after ignition")), "the title says there is no way back");
            Assert.That(body.text, Is.EqualTo(Words(
                "飞船一点火，最终决战马上开始，不能再回基地升级和加固。\n\n如果决战失败，只能从决战开头重来，回不到点火之前。现在点火吗？",
                "Once the ship ignites, the final battle begins at once, and you cannot return to the base to upgrade or fortify.\n\n"
                + "If you lose, you can only retry that battle. Ignite now?")),
                "the question says what follows and what a lost battle goes back to");
            Assert.That((yes.text, no.text), Is.EqualTo((Words("点火", "Ignite"), Words("先不点火", "Not yet"))), "the answers are the two actions");
            await Frames(20);
            Canvas.ForceUpdateCanvases();
            Rect panelBox = Box(panel), bodyLetters = Letters(body), yesBox = Box(answers[0]), noBox = Box(answers[1]);
            Assert.That(bodyLetters.yMin, Is.GreaterThanOrEqualTo(Mathf.Max(yesBox.yMax, noBox.yMax)), "the question's last line stands above its answers");
            Assert.That(bodyLetters.xMin >= panelBox.xMin && bodyLetters.xMax <= panelBox.xMax && Box(title).yMin >= bodyLetters.yMax - 1f, Is.True,
                "inside the panel, under the title");
            Assert.That(Inside(Box(yes), yesBox) && Inside(Box(no), noBox) && Inside(yesBox, panelBox) && Inside(noBox, panelBox), Is.True,
                "each answer's words inside its button, both buttons inside the panel");
            Capture(game.cam, $"ignite-ask-{tag}");

            // Not yet: the page stays as it was.
            ui.DebugAnswerConfirm(false);
            await Frames(2);
            Assert.That((ui.ConfirmOpen, campaign.Stage, ui.BaseOpen, campaign.CanIgnite, game.State), Is.EqualTo((false, CampaignStage.Base, true, true, GameState.Playing)),
                "waiting leaves the visit open and the ship ready");
            Assert.That(ArenaSaves.List(SaveMode.Story)[Slot].phase, Is.EqualTo(SavePhase.Afternoon), "and the slot's save as it was");

            // Ignite: the failed ignition plays, the final battle's save is written and the battle begins.
            Assert.That(ui.DebugPressBase("c.ignite"), Is.True, "the ignition button presses again");
            await Frames(2);
            Assume.That(ui.ConfirmOpen, Is.True, "it asks again");
            ui.DebugAnswerConfirm(true);
            await Until(() => campaign.Stage is CampaignStage.Story or CampaignStage.FinalBoss, 3f, "the ignition begins");
            Assert.That(ui.BaseOpen, Is.False, "the base page is put away");
            await TheFinalBattle(campaign);
            var tyrant = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((tyrant.day, tyrant.phase, tyrant.cleared), Is.EqualTo((1, SavePhase.Tyrant, false)), "the final battle's save is the slot's latest");

            // The battle is lost: it is retried from itself, and the afternoon the slot still keeps is not offered.
            Assume.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player falls");
            await Until(() => game.State == GameState.Lost && game.RunPhase == ArenaUiState.Result && V5ResultScreen.IsShown, 8f, "the lost run's result page");
            await Frames(3);
            var page = V5ResultScreen.PageObject;
            var retry = page.GetComponentsInChildren<V5Plate>(true).FirstOrDefault(p => p.name == "Retry");
            Assert.That(retry && retry.gameObject.activeInHierarchy, Is.True, "the lost battle has its retry");
            Assert.That(retry.GetComponentsInChildren<Text>(true).First(t => t.name == "RetryLabel").text, Is.EqualTo(Words("从最终决战重来", "Retry the final battle")),
                "from the final battle");
            var afternoon = page.GetComponentsInChildren<V5Plate>(true).FirstOrDefault(p => p.name == "RetryAfternoon");
            Assert.That(afternoon && afternoon.gameObject.activeInHierarchy, Is.False, "and no way back to the afternoon before the ignition");
        }

        /// <summary>A night before the last planned day, the ship ready already: the dawn brings the next day, so it asks nothing.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task AnOrdinaryDawn_AsksNothing_EvenWithTheShipReady()
        {
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            var ui = campaign.Ui;
            campaign.DebugBeginNight(1, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 2);
            MakeTheShipReady(campaign);
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            await Until(() => ui.BaseOpen && ui.DebugBaseParts().Contains("f.go"), 5f, "the night visit's main way out");
            await Frames(15);
            Assume.That((campaign.BaseExits().Last().label, campaign.BaseExits().Last().note), Is.EqualTo((ArenaText.Pair("等待天亮", "Wait for dawn"), "")),
                "wait for dawn, with nothing said of an ignition");
            Assert.That(ui.DebugPressBase("f.go"), Is.True, "wait for dawn presses");
            await Until(() => campaign.Stage == CampaignStage.Day || ui.ConfirmOpen, 5f, "the dawn");
            Assert.That((ui.ConfirmOpen, campaign.Day, campaign.Stage), Is.EqualTo((false, 2, CampaignStage.Day)), "an ordinary dawn asks nothing: day 2 begins");
        }

        /// <summary>The last planned day's night with the ship ready: that dawn starts the ignition, so "Wait for dawn" asks first.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(240000)]
        public async Task WaitForDawn_WhenThatDawnIgnites_AsksFirst_AndWaitingKeepsTheNight()
        {
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            var ui = campaign.Ui;
            campaign.DebugBeginNight(campaign.rules.days, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 2);
            MakeTheShipReady(campaign);
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            await Until(() => ui.BaseOpen && ui.DebugBaseParts().Contains("f.go"), 5f, "the night visit's main way out");
            await Frames(15);
            Assume.That((campaign.BaseExits().Last().label, campaign.BaseExits().Last().note),
                Is.EqualTo((ArenaText.Pair("等待天亮", "Wait for dawn"), ArenaText.Pair("随后点火返航", "Then the ignition starts"))), "wait for dawn, then the ignition");
            Assert.That(ui.DebugPressBase("f.go"), Is.True, "wait for dawn presses");
            await Frames(2);
            Assert.That((ui.ConfirmOpen, campaign.Stage), Is.EqualTo((true, CampaignStage.Base)), "it asks first: no dawn yet, the visit still open");

            ui.DebugAnswerConfirm(false);
            await Frames(2);
            Assert.That((ui.ConfirmOpen, campaign.Stage, ui.BaseOpen, campaign.Day), Is.EqualTo((false, CampaignStage.Base, true, campaign.rules.days)),
                "waiting leaves the visit open on the same night");
            campaign.BaseExits()[0].go();
            Assert.That((campaign.Stage, game.RunPhase), Is.EqualTo((CampaignStage.Night, ArenaUiState.Night)), "and the night can be gone back out into");

            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            await Until(() => ui.BaseOpen && ui.DebugBaseParts().Contains("f.go"), 5f, "the night visit again");
            await Frames(15);
            Assert.That(ui.DebugPressBase("f.go"), Is.True, "wait for dawn presses again");
            await Frames(2);
            Assume.That(ui.ConfirmOpen, Is.True, "it asks again");
            ui.DebugAnswerConfirm(true);
            await Until(() => campaign.Stage is CampaignStage.Story or CampaignStage.FinalBoss, 3f, "the dawn brings the ignition");
            Assert.That(ui.BaseOpen, Is.False, "the base page is put away");
            await TheFinalBattle(campaign);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(240000)]
        public async Task AFallInTheDark_BeforeADawnThatIgnites_AsksFirst_AndWaitingLeavesTheNightGoingOn()
        {
            var game = await ReadyScene();
            var campaign = await StartStory(game);
            var ui = campaign.Ui;
            campaign.DebugBeginNight(campaign.rules.days, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<EnemyKind>(), 2);
            MakeTheShipReady(campaign);

            Assume.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player falls in the dark");
            await Until(() => ui.ConfirmOpen || campaign.Stage != CampaignStage.Night, 10f, "the fall is over");
            Assert.That((ui.ConfirmOpen, campaign.Stage), Is.EqualTo((true, CampaignStage.Night)), "the dawn that would ignite asks first");
            Assert.That(game.player.IsAlive, Is.True, "with the player on their feet again");

            ui.DebugAnswerConfirm(false);
            await Frames(3);
            Assert.That((ui.ConfirmOpen, campaign.Stage, game.State, game.RunPhase, game.player.IsAlive),
                Is.EqualTo((false, CampaignStage.Night, GameState.Playing, ArenaUiState.Night, true)), "waiting leaves the night going on, the player alive");
            Assert.That(Vector3.Distance(Query.Flat(game.player.Position), Query.Flat(campaign.DoorPoint)), Is.LessThan(3f), "at the base door");
            Assert.That((campaign.Day, campaign.CanIgnite), Is.EqualTo((campaign.rules.days, true)), "on the same night, the ship still ready");

            Assume.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player falls again");
            await Until(() => ui.ConfirmOpen || campaign.Stage != CampaignStage.Night, 10f, "the second fall is over");
            Assume.That(ui.ConfirmOpen, Is.True, "it asks again");
            ui.DebugAnswerConfirm(true);
            await Until(() => campaign.Stage is CampaignStage.Story or CampaignStage.FinalBoss, 3f, "the dawn brings the ignition");
            await TheFinalBattle(campaign);
        }

        // ───── Helpers (as SaveStoryTest's and ResultScreenTest's) ─────

        /// <summary>The engine, the hull and the fuel tank bought and the tank filled: the ship can ignite.</summary>
        static void MakeTheShipReady(V2Campaign campaign)
        {
            campaign.DebugGrant(2000, 0, 2000);
            foreach (V2Campaign.ShipPartKind part in Enum.GetValues(typeof(V2Campaign.ShipPartKind)))
                Assume.That(campaign.TryBuyShipPart(part), Is.True, $"the ship's {part} is bought");
            Assume.That(campaign.TryAddFuel(), Is.True, "the fuel goes in");
            Assume.That(campaign.CanIgnite, Is.True, "the ship can ignite");
        }

        /// <summary>The failed ignition's cutscene skipped, until the final battle has begun.</summary>
        static async Task TheFinalBattle(V2Campaign campaign)
        {
            float deadline = Time.realtimeSinceStartup + 120f;
            while (campaign.Stage != CampaignStage.FinalBoss)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The final battle never began: {campaign.Stage}.");
                if (campaign.Ui) campaign.Ui.SkipCg();
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>The story mode from the title, up to Day 0's walk, with mornings of 3 seconds and a fall in the dark over in half a
        /// second (a copy of the rules).</summary>
        async Task<V2Campaign> StartStory(GameManager game)
        {
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            rulesCopy = Object.Instantiate(campaign.rules);
            rulesCopy.morningSeconds = 3f;
            rulesCopy.nightDeathSeconds = .5f;
            campaign.rules = rulesCopy;
            await CommandUntil(game, ArenaUiCommand.StartCampaign, () => game.CampaignRunning);
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            return campaign;
        }

        /// <summary>Day 0's night begun and ended at once (the campaign's calls for checks): its dawn writes day 1's morning save and
        /// starts the morning. The player is protected from the morning's monsters.</summary>
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

        /// <summary>A part's box on the screen (its world corners).</summary>
        static Rect Box(Component part)
        {
            var corners = new Vector3[4];
            ((RectTransform)part.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>The box of a centred paragraph's letters on the screen: as wide and as tall as the text itself, from its box's top.</summary>
        static Rect Letters(Text label)
        {
            Rect box = Box(label);
            float scale = label.rectTransform.lossyScale.x;
            float width = Mathf.Min(box.width, label.preferredWidth * scale), height = label.preferredHeight * scale;
            return new Rect(box.center.x - width * .5f, box.yMax - height, width, height);
        }

        static bool Inside(Rect inner, Rect outer) =>
            inner.xMin >= outer.xMin - .5f && inner.xMax <= outer.xMax + .5f && inner.yMin >= outer.yMin - .5f && inner.yMax <= outer.yMax + .5f;

        /// <summary>The camera's frame with every overlay canvas drawn into it (as GddCaptureTest does), 1920 x 1080, into the copy's Logs.</summary>
        static void Capture(Camera cam, string name)
        {
            const int width = 1920, height = 1080;
            var rt = new RenderTexture(width, height, 24);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            var overlays = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(c => c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceOverlay).ToArray();
            var saved = overlays.Select(c => (canvas: c, camera: c.worldCamera, plane: c.planeDistance)).ToArray();
            foreach (var c in overlays)
            {
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + .05f;
            }

            Canvas.ForceUpdateCanvases();
            cam.Render();
            foreach (var s in saved)
            {
                s.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                s.canvas.worldCamera = s.camera;
                s.canvas.planeDistance = s.plane;
            }

            cam.targetTexture = previous;
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = active;
            var folder = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            Object.Destroy(texture);
            rt.Release();
            Object.Destroy(rt);
        }

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
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

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                if (GameManager.Instance && GameManager.Instance.RunPhase == ArenaUiState.Cutscene && WorldMap.Active) WorldMap.Active.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
