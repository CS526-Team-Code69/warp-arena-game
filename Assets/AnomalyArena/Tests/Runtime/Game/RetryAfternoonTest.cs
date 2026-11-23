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
    /// A morning lost from day 2 on can be retried from that morning or from the afternoon before it: the story's slot keeps that
    /// afternoon's save beside its latest one. The lost run's result page shows the orange retry of the morning (Enter), beside it a
    /// plate back to the afternoon before (Tab) with a line under the two saying what going back is for, and the way back to the title
    /// (R) on a row of its own, every word and key inside its button and no button on another, in both languages. Tab loads the scene
    /// again in that afternoon as it began: the day, the base's fortification and HP, the currencies and what that morning had taken
    /// from the base, with nothing bought after it. The slot then holds that afternoon as its latest save, which is what the title's
    /// Continue loads; played on, the next morning is saved again and, lost again, offers the same afternoon. An afternoon's save
    /// keeps what its own morning took from the base, so the afternoon it continues into reminds of the fortification as the first one
    /// did. Day 1's morning has no afternoon before it and offers only itself. The pictures go to the copy's
    /// Logs/result-afternoon-*.png. Day 0's walk is skipped with the campaign's own calls for checks and the mornings last a few
    /// seconds (a copy of the rules); the saves are kept in memory.
    /// </summary>
    [TestFixture]
    public class RetryAfternoonTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const int Slot = 2;
        // What the test takes from the base in day 1's morning: more than a fifth of its 500 HP, less than would bring it down.
        const float MorningHit = 150f;
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
        public async Task ABaseLostInDay2sMorning_OffersTheAfternoonBefore_AndTabGoesBackToItAsItBegan(string language)
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

            // Day 1: the morning takes a good piece of the base, and the afternoon begins (its save is written as it does).
            var campaign = await StartStory(game, true);
            BeginDay1(game, campaign);
            campaign.DamageBase(2, MorningHit, campaign.DoorPoint);
            await FinishTheMorning(game, campaign);
            campaign.DebugCollectPickups();
            await Until(() => campaign.PickupCount == 0, 5f, "the day's last energy is picked up");
            var began = AfternoonState.Of(campaign);
            Assume.That((began.day, began.fortify), Is.EqualTo((1, 1)), "day 1's afternoon, the base never fortified");
            Assume.That(began.morningLoss, Is.GreaterThanOrEqualTo(MorningHit), "the afternoon knows what its morning took from the base");
            var saved = ArenaSaves.List(SaveMode.Story)[Slot];
            Assume.That((saved.empty, saved.day, saved.phase), Is.EqualTo((false, 1, SavePhase.Afternoon)), "the afternoon's save is in the slot");

            // What the player does after that afternoon began is not in its save: alloy found, a fortification bought, the night skipped.
            campaign.DebugGrant(5000, 0, 0);
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Workshop);
            Assume.That(campaign.Stage, Is.EqualTo(CampaignStage.Base), "the base visit is open");
            Assume.That(campaign.TryFortify(), Is.True, "the base is fortified a level");
            campaign.WaitForDawn();
            await Until(() => campaign.Day == 2 && campaign.Stage == CampaignStage.Day, 10f, "day 2's morning");
            var morning = ArenaSaves.List(SaveMode.Story)[Slot];
            Assume.That((morning.day, morning.phase), Is.EqualTo((2, SavePhase.Morning)), "day 2's morning is the slot's latest save");

            // The base falls in that morning.
            await LoseTheBase(game, campaign);
            var page = V5ResultScreen.PageObject;
            var retry = Plate(page, "Retry");
            var back = Plate(page, "BackToTitle");
            var afternoon = Plate(page, "RetryAfternoon");
            Assert.That(afternoon.gameObject.activeInHierarchy, Is.True, "the page offers the afternoon before");
            Assert.That(retry.gameObject.activeInHierarchy && back.gameObject.activeInHierarchy, Is.True, "beside the morning's retry and the way back to the title");
            Assert.That((retry.Kind, afternoon.Kind, back.Kind), Is.EqualTo((V5Plate.Look.Go, V5Plate.Look.Plate, V5Plate.Look.Plate)),
                "the morning's retry stays the orange button; the afternoon and the title are plain plates");
            var retryLabel = Label(retry, "RetryLabel");
            var afternoonLabel = Label(afternoon, "AfternoonLabel");
            var backLabel = Label(back, "Label");
            var hint = Label(page.transform, "AfternoonHint");
            var retryKey = retry.GetComponentInChildren<V5HudKey>(true);
            var afternoonKey = afternoon.GetComponentInChildren<V5HudKey>(true);
            var backKey = back.GetComponentInChildren<V5HudKey>(true);
            Assert.That(retryLabel.text, Is.EqualTo(Words("从第 2 天上午重来", "Retry day 2 morning")), "the retry names the lost morning");
            Assert.That(afternoonLabel.text, Is.EqualTo(Words("回到第 1 天下午", "Back to day 1 afternoon")), "the other names the afternoon before it");
            Assert.That(backLabel.text, Is.EqualTo(Words("回到标题", "Back to Title")), "the way back to the title stays");
            Assert.That((retryKey.Label.text, afternoonKey.Label.text, backKey.Label.text), Is.EqualTo((Words("回车", "Enter"), "Tab", "R")), "each with its key");
            Assert.That(hint.gameObject.activeInHierarchy, Is.True, "a line says what going back is for");
            Assert.That(hint.text, Is.EqualTo(Words("下午可以先加固基地、升级武器和护甲，再迎战第 2 天上午的怪物。",
                "In the afternoon you can fortify the base and upgrade before day 2's morning.")), "in a whole sentence that names the morning to come");

            Canvas.ForceUpdateCanvases();
            Rect retryBox = Box(retry), afternoonBox = Box(afternoon), backBox = Box(back), hintBox = Letters(hint);
            Rect windowBox = Box(page.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Window"));
            Assert.That(retryBox.xMax, Is.LessThan(afternoonBox.xMin), "the retry left of the afternoon, apart");
            Assert.That(retryBox.center.y, Is.EqualTo(afternoonBox.center.y).Within(.5f), "the two on one row");
            Assert.That(retryBox.width, Is.EqualTo(afternoonBox.width).Within(.5f), "as wide as each other");
            Assert.That(hintBox.yMax, Is.LessThan(retryBox.yMin), "the line under the two");
            Assert.That(backBox.yMax, Is.LessThan(hintBox.yMin), "and the way back to the title under the line, on a row of its own");
            Assert.That(backBox.center.x, Is.EqualTo(windowBox.center.x).Within(1f), "in the middle");
            Assert.That(Inside(Box(retryLabel), retryBox) && Inside(Box(retryKey), retryBox), Is.True, "the retry's words and key inside its button");
            Assert.That(Inside(Box(afternoonLabel), afternoonBox) && Inside(Box(afternoonKey), afternoonBox), Is.True, "the afternoon's words and key inside its button");
            Assert.That(Inside(Box(backLabel), backBox) && Inside(Box(backKey), backBox), Is.True, "the way back's words and key inside its button");
            Assert.That(Inside(retryBox, windowBox) && Inside(afternoonBox, windowBox) && Inside(backBox, windowBox) && Inside(hintBox, windowBox), Is.True,
                "all of it inside the window");
            Assert.That(hint.cachedTextGenerator.lineCount, Is.EqualTo(1), "the line fits on one line");
            await Frames(20);
            Capture(game.cam, $"result-afternoon-{tag}");

            // Tab, the key the plate shows: the scene loads again in day 1's afternoon, as it began.
            int seed = campaign.MapSeed, map = WorldMap.Active.BiomeIndex;
            await PressUntil(game, KeyCode.Tab, () => ArenaSaves.ContinueWaiting, 5f);
            var next = await NextScene(game, 25f);
            var resumed = next.GetComponent<V2Campaign>();
            await Until(() => resumed.Running && resumed.Stage == CampaignStage.Dusk && next.State == GameState.Playing, 15f, "the run is back in its afternoon");
            var back1 = AfternoonState.Of(resumed);
            Assert.That((back1.day, back1.fortify), Is.EqualTo((began.day, began.fortify)), "day 1's afternoon, the base as it was before the fortification bought later");
            Assert.That(back1.baseHp, Is.EqualTo(began.baseHp).Within(.5f), "the base's HP as that afternoon began");
            Assert.That(back1.currencies, Is.EqualTo(began.currencies), "the currencies as they were then, without the alloy found later");
            Assert.That(back1.morningLoss, Is.EqualTo(began.morningLoss).Within(.5f), "and what that morning took from the base");
            Assert.That((resumed.MapSeed, WorldMap.Active.BiomeIndex), Is.EqualTo((seed, map)), "on the same map and terrain");
            Assert.That((next.player.IsAlive, Time.timeScale, V5ResultScreen.IsShown), Is.EqualTo((true, 1f, false)), "a live run, its clock running, no result page left");
            var latest = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((latest.empty, latest.cleared, latest.day, latest.phase), Is.EqualTo((false, false, 1, SavePhase.Afternoon)),
                "the slot's latest save is that afternoon now: the title's Continue loads it");
            Assert.That(ArenaSaves.List(SaveMode.Story).Where(s => s.slot != Slot).Select(s => s.empty), Is.All.True, "the other story slots stay empty");

            // Played on from there, the next morning is saved again; lost again, it offers the same afternoon.
            next.player.ProtectFor(600f);
            resumed.RequestBaseScreen(V2Campaign.BaseScreen.Workshop);
            Assume.That(resumed.Stage, Is.EqualTo(CampaignStage.Base), "the base visit is open");
            resumed.WaitForDawn();
            await Until(() => resumed.Day == 2 && resumed.Stage == CampaignStage.Day, 10f, "day 2's morning again");
            var again = ArenaSaves.List(SaveMode.Story)[Slot];
            Assert.That((again.day, again.phase), Is.EqualTo((2, SavePhase.Morning)), "day 2's morning is the latest save again");
            await LoseTheBase(next, resumed);
            var second = Plate(V5ResultScreen.PageObject, "RetryAfternoon");
            Assert.That((second.gameObject.activeInHierarchy, Label(second, "AfternoonLabel").text), Is.EqualTo((true, Words("回到第 1 天下午", "Back to day 1 afternoon"))),
                "the afternoon before is offered again");
        }

        /// <summary>The afternoon's save is taken before the afternoon counts what its morning took from the base (BeginDusk), so it
        /// has to carry that itself: continued from the title, the afternoon knows its morning's loss as the first one did.</summary>
        [Test, LoadScene(Scene), Category("Integration"), Timeout(180000)]
        public async Task ContinuingAnAfternoonSave_KeepsWhatItsMorningTookFromTheBase()
        {
            ArenaSaves.Bind(SaveMode.Story, Slot);
            var game = await ReadyScene();
            var campaign = await StartStory(game, true);
            BeginDay1(game, campaign);
            campaign.DamageBase(2, MorningHit, campaign.DoorPoint);
            await FinishTheMorning(game, campaign);
            float loss = campaign.LastMorningLoss;
            Assume.That(loss, Is.GreaterThanOrEqualTo(MorningHit), "the afternoon knows what its morning took from the base");
            Assume.That(loss, Is.GreaterThan(campaign.BaseMaxHp * .2f), "more than the share that asks for a fortification");

            var title = await QuitToTitle(game);
            ArenaSaves.Continue(SaveMode.Story, Slot);
            var next = await NextScene(title, 25f);
            var resumed = next.GetComponent<V2Campaign>();
            await Until(() => resumed.Running && resumed.Stage == CampaignStage.Dusk && next.State == GameState.Playing, 15f, "the continued run is in its afternoon");

            Assert.That(resumed.LastMorningLoss, Is.EqualTo(loss).Within(.5f), "the continued afternoon knows what its morning took");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task AFallInDay1sMorning_OffersOnlyThatMorning()
        {
            ArenaSaves.Bind(SaveMode.Story, Slot);
            var game = await ReadyScene();
            var campaign = await StartStory(game, false);
            BeginDay1(game, campaign);
            Assume.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player falls");
            await Until(() => game.State == GameState.Lost && game.RunPhase == ArenaUiState.Result && V5ResultScreen.IsShown, 8f, "the lost run's result page");
            await Frames(3);

            var page = V5ResultScreen.PageObject;
            var retry = Plate(page, "Retry");
            Assert.That((retry.gameObject.activeInHierarchy, Label(retry, "RetryLabel").text), Is.EqualTo((true, ArenaText.Pair("从第 1 天上午重来", "Retry day 1 morning"))),
                "day 1's morning is retried from itself");
            var afternoon = page.GetComponentsInChildren<V5Plate>(true).FirstOrDefault(p => p.name == "RetryAfternoon");
            Assert.That(afternoon && afternoon.gameObject.activeInHierarchy, Is.False, "day 1 has no afternoon before it: no such plate");
            var hint = page.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == "AfternoonHint");
            Assert.That(hint && hint.gameObject.activeInHierarchy, Is.False, "nor its line");
            Canvas.ForceUpdateCanvases();
            Assert.That(Box(retry).center.y, Is.EqualTo(Box(Plate(page, "BackToTitle")).center.y).Within(.5f), "the retry and the way back share their row, as before");
        }

        // ───── Helpers (as SaveStoryTest's and ResultScreenTest's) ─────

        /// <summary>What going back to an afternoon must bring back, read from the running campaign.</summary>
        sealed class AfternoonState
        {
            public int day, fortify;
            public float baseHp, morningLoss;
            public int[] currencies;

            public static AfternoonState Of(V2Campaign campaign) => new()
            {
                day = campaign.Day,
                fortify = campaign.FortifyLevel,
                baseHp = campaign.BaseHp,
                morningLoss = campaign.LastMorningLoss,
                currencies = new[] { campaign.Alloy, campaign.Energy, campaign.Dark, campaign.LampStock },
            };
        }

        /// <summary>The base is brought down, and the lost run's result page shows.</summary>
        static async Task LoseTheBase(GameManager game, V2Campaign campaign)
        {
            campaign.DamageBase(2, campaign.BaseMaxHp * 10f, campaign.DoorPoint);
            await Until(() => game.State == GameState.Lost && game.RunPhase == ArenaUiState.Result && V5ResultScreen.IsShown, 8f, "the lost run's result page");
            Assume.That(campaign.BaseDestroyed, Is.True, "the run was lost with its base");
            await Frames(3);
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>The story mode from the title, up to Day 0's walk. shortMornings: the mornings last 3 seconds (a copy of the rules).</summary>
        async Task<V2Campaign> StartStory(GameManager game, bool shortMornings)
        {
            Assume.That(game.TryGetComponent(out V2Campaign campaign) && campaign.isActiveAndEnabled && campaign.rules, Is.True, "the scene has the V2 campaign");
            if (shortMornings)
            {
                rulesCopy = Object.Instantiate(campaign.rules);
                rulesCopy.morningSeconds = 3f;
                campaign.rules = rulesCopy;
            }
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

        /// <summary>Quit to title (the scene loads again) and wait for its title.</summary>
        static async Task<GameManager> QuitToTitle(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.QuitToTitle, () => !game, 8f);
            var title = await NextScene(game);
            await Until(() => title.RunPhase == ArenaUiState.Title, 5f, "the title");
            return title;
        }

        /// <summary>The GameManager of the scene loaded after old's, once it has started.</summary>
        static async Task<GameManager> NextScene(GameManager old, float timeout = 20f)
        {
            await Until(() => GameManager.Instance && !ReferenceEquals(GameManager.Instance, old) && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active,
                timeout, "the scene loaded again");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>The key pressed as the keyboard presses it (GameManager.PressKey), every frame until done.</summary>
        static async Task PressUntil(GameManager game, KeyCode key, Func<bool> done, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"{key} did nothing in {game.RunPhase}.");
                game.PressKey(key);
                await Awaitable.NextFrameAsync();
            }
        }

        /// <summary>The page's button of this name.</summary>
        static V5Plate Plate(GameObject page, string name)
        {
            var plate = page.GetComponentsInChildren<V5Plate>(true).FirstOrDefault(p => p.name == name);
            Assert.That(plate, Is.Not.Null, $"the page's {name}");
            return plate;
        }

        static Text Label(Component parent, string name)
        {
            var label = parent.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == name);
            Assert.That(label, Is.Not.Null, $"{parent.name}'s {name}");
            return label;
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
