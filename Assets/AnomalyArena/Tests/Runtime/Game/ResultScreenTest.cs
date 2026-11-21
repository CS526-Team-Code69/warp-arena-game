using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// The end of a run in the fifth pass's look (V5ResultScreen): after a defeat its page shows over the old result screen with the
    /// result, where the run ended, what ended it and the run's numbers, in both art modes; a run bound to no slot has only the orange way
    /// back to the title. A lost run whose slot holds a save gets the orange retry from that save (Enter) named by the save, and the way
    /// back becomes a plain plate (R) beside it, in both languages. The looks are saved to the copy's Logs/result-*.png for a look by eye.
    /// The saves are kept in memory.
    /// </summary>
    [TestFixture]
    public class ResultScreenTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const int Slot = 0;
        bool savedArt;

        [SetUp]
        public void SetUp()
        {
            savedArt = Art.Enabled;
            ArenaSaves.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            Art.Enabled = savedArt;
            ArenaSaves.ResetForTests();
            TestStartLanguage.Reset();
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Defeat_ShowsTheFifthPassPage_WithTheResultWhereTheCauseAndTheNumbers()
        {
            var game = await StartRun();
            Assume.That(V5HudKit.On, Is.True, "the fifth pass's look is on outside the Alpha");
            Vector3 side = Query.Flat(game.cam.transform.right).normalized;
            var spitter = game.waves.SpawnLab(EnemyKind.Spitter, game.player.Position + side * 4f, false, ElementKind.None);
            Assert.That(spitter, Is.Not.Null, "a lab Spitter");
            spitter.enabled = false;
            var player = game.player;
            player.Invulnerable = false;
            typeof(Combatant).GetField("protectTimer", BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(player, 0f);
            player.ReceiveDamage(DamageInfo.Attack(player.maxHp * 10f, spitter));
            await Until(() => game.RunPhase == ArenaUiState.Result, 12f, "the result screen");
            await Until(() => V5ResultScreen.IsShown, 3f, "the fifth pass's result page");
            await Frames(3);

            var s = game.UiSnapshot;
            var page = V5ResultScreen.PageObject;
            Assert.That(page, Is.Not.Null, "the page exists");
            Assert.That(page.activeSelf, Is.True, "the page is up");
            Text Part(string name) => page.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.name == name);
            Assert.That(Part("Title").text, Is.EqualTo(s.resultTitle), "the result on the banner");
            Assert.That(Part("Where").text, Is.EqualTo(s.resultWhere).And.Not.Empty, "where the run ended");
            Assert.That(s.resultCause, Is.Not.Empty, "a defeat by a monster has a cause");
            Assert.That(Part("Cause").text, Is.EqualTo(s.resultCause.Split('\n')[0].Trim()), "the cause's first line");
            Assert.That(Part("Stats").text, Does.Contain(s.kills.ToString()), "the run's numbers");
            Assert.That(Part("Label").text, Is.Not.Empty, "the way back to the title");
            Assert.That(game.RetrySave, Is.Null, "a run bound to no slot has no save to retry");
            var retry = page.GetComponentsInChildren<V5Plate>(true).FirstOrDefault(p => p.name == "Retry");
            Assert.That(retry && retry.gameObject.activeInHierarchy, Is.False, "so the page has no retry");
            Assert.That(Plate(page, "BackToTitle").Kind, Is.EqualTo(V5Plate.Look.Go), "and the way back to the title stays the orange button");
            Capture(game.cam, "result-defeat-art");

            Art.Enabled = false;
            // The font's letters are drawn again for the plain look; give them time before the picture is taken.
            await Frames(30);
            Capture(game.cam, "result-defeat-whitebox");
        }

        /// <summary>A lost open-mode run bound to a slot: its page offers the retry from the slot's wave 1 save as the orange button with
        /// Enter on its key, and the way back to the title is a plain plate with R right of it, each word and key inside its button, in the
        /// language the game started in (Chinese with the art, English in whitebox). The story's saves are named by their day and part of
        /// the day, the final battle by itself.</summary>
        [TestCase(StartupLanguage.Chinese)]
        [TestCase(StartupLanguage.English)]
        [BuildScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task LostBoundRun_OffersTheRetryFromItsLastSave_InTheStartLanguage(string language)
        {
            Assume.That(ArenaSaves.InMemoryForTests, Is.True, "the saves are kept in memory during the tests");
            bool zh = language == StartupLanguage.Chinese;
            string Words(string chinese, string english) => zh ? chinese : english;
            TestStartLanguage.Use(language);
            ArenaSaves.Bind(SaveMode.Open, Slot);
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var game = await StartRun();
            Assume.That(V5HudKit.On, Is.True, "the fifth pass's look is on outside the Alpha");
            Assume.That(ArenaText.IsChinese, Is.EqualTo(zh), "the game started in the language asked for");
            var saved = ArenaSaves.List(SaveMode.Open)[Slot];
            Assume.That((saved.empty, saved.wave), Is.EqualTo((false, 1)), "wave 1 was saved as the run began");

            // Wall damage is no attack, so the protection does not hold it back.
            Assume.That(game.player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player falls");
            await Until(() => game.RunPhase == ArenaUiState.Result && V5ResultScreen.IsShown, 12f, "the fifth pass's result page");
            await Frames(3);

            var retrySave = game.RetrySave;
            Assert.That(retrySave, Is.Not.Null, "a lost run whose slot holds a save has a retry");
            Assert.That((retrySave.mode, retrySave.slot, retrySave.wave, retrySave.cleared), Is.EqualTo((SaveMode.Open, Slot, 1, false)), "from its own slot's last save");
            Assert.That(game.CanRetryFromSave, Is.True, "and the page offers it");
            var page = V5ResultScreen.PageObject;
            var retry = Plate(page, "Retry");
            var back = Plate(page, "BackToTitle");
            Assert.That(retry.gameObject.activeInHierarchy, Is.True, "the retry is on the page");
            Assert.That((retry.Kind, back.Kind), Is.EqualTo((V5Plate.Look.Go, V5Plate.Look.Plate)), "the retry is the orange button, the way back a plain plate");
            var retryLabel = Label(retry, "RetryLabel");
            var backLabel = Label(back, "Label");
            var retryKey = retry.GetComponentInChildren<V5HudKey>(true);
            var backKey = back.GetComponentInChildren<V5HudKey>(true);
            Assert.That(retryLabel.text, Is.EqualTo(Words("从第 1 波重来", "Retry wave 1")), "the retry names the saved wave");
            Assert.That(retryKey.Label.text, Is.EqualTo(Words("回车", "Enter")), "its key is Enter");
            Assert.That(backLabel.text, Is.EqualTo(Words("回到标题", "Back to Title")), "the way back to the title stays");
            Assert.That(backKey.Label.text, Is.EqualTo("R"), "with R on its key");

            Canvas.ForceUpdateCanvases();
            Rect retryBox = Box(retry), backBox = Box(back);
            Assert.That(retryBox.xMax, Is.LessThan(backBox.xMin), "the retry left of the way back, apart");
            Assert.That(retryBox.center.y, Is.EqualTo(backBox.center.y).Within(.5f), "on one row");
            Assert.That(Inside(Box(retryLabel), retryBox) && Inside(Box(retryKey), retryBox), Is.True, "the retry's words and key inside its button");
            Assert.That(Inside(Box(backLabel), backBox) && Inside(Box(backKey), backBox), Is.True, "the way back's words and key inside its button");
            Rect windowBox = Box(page.GetComponentsInChildren<RectTransform>(true).First(r => r.name == "Window"));
            Assert.That(Inside(retryBox, windowBox) && Inside(backBox, windowBox), Is.True, "both inside the window");

            Assert.That(V5ResultScreen.RetryWords(new SaveSummary { mode = SaveMode.Story, day = 1, phase = SavePhase.Morning }), Is.EqualTo(Words("从第 1 天上午重来", "Retry day 1 morning")));
            Assert.That(V5ResultScreen.RetryWords(new SaveSummary { mode = SaveMode.Story, day = 3, phase = SavePhase.Afternoon }), Is.EqualTo(Words("从第 3 天下午重来", "Retry day 3 afternoon")));
            Assert.That(V5ResultScreen.RetryWords(new SaveSummary { mode = SaveMode.Story, day = 2, phase = SavePhase.Night }), Is.EqualTo(Words("从第 2 天夜晚重来", "Retry day 2 night")));
            Assert.That(V5ResultScreen.RetryWords(new SaveSummary { mode = SaveMode.Story, day = 7, phase = SavePhase.Tyrant }), Is.EqualTo(Words("从最终决战重来", "Retry the final battle")));
            Assert.That(V5ResultScreen.RetryWords(new SaveSummary { mode = SaveMode.Open, wave = 15, waveCount = 15 }), Is.EqualTo(Words("从第 15 波重来", "Retry wave 15")));
            Assert.That(V5ResultScreen.RetryWords(null), Is.Empty, "no save, no words");
            Capture(game.cam, $"result-retry-{(zh ? "zh" : "en")}-{(Art.Enabled ? "art" : "whitebox")}");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Pause_AndItsSettings_WearTheFifthPassLook()
        {
            var game = await StartRun();
            Assume.That(V5HudKit.On, Is.True, "the fifth pass's look is on outside the Alpha");
            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await Frames(20);
            var banner = GameObject.Find("HeadingBanner");
            Assert.That(banner, Is.Not.Null, "the pause heading stands on the hull-plate banner");
            Capture(game.cam, "pause-art");
            var settings = Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault(b => b.name == V2Hud.PauseSettingsButton);
            Assert.That(settings, Is.Not.Null, "the pause screen's Settings button");
            settings.onClick.Invoke();
            await Until(() => ArenaSettingsPanel.IsOpen, 3f, "the settings panel");
            await Frames(20);
            Capture(game.cam, "pause-settings-art");
        }

        static async Task<GameManager> StartRun()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop || game.RunPhase == ArenaUiState.Loadout);
            if (game.RunPhase == ArenaUiState.SupplyDrop)
            {
                await Until(() => game.SupplyDropReady || game.RunPhase != ArenaUiState.SupplyDrop, 5f, "supply crate lands");
                await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            }

            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            // Lab monsters come once the first wave runs; a black hole's first-sight cutscene may come first and is skipped.
            await Until(() =>
            {
                if (game.RunPhase == ArenaUiState.Cutscene && WorldMap.Active) WorldMap.Active.SkipHoleIntro();
                return game.RunPhase == ArenaUiState.WaveActive;
            }, 20f, "the first wave runs");
            return game;
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

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
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
