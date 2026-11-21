using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// Pictures of the base's two screens for a look by eye: on day 1's afternoon the battle HUD with the way home under the day plate,
    /// then the rocket page and the workshop (its body and skills tabs), in the language the game started in (Chinese with the art,
    /// English in whitebox). The pictures go to the copy's Logs/base-*.png; the test checks only that each screen showed.
    /// </summary>
    public class BaseRedesignCapturesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        CampaignRules rulesCopy;
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
            if (rulesCopy) Object.Destroy(rulesCopy);
            rulesCopy = null;
            Art.Enabled = savedArt;
            ArenaSaves.ResetForTests();
            TestStartLanguage.Reset();
            Time.timeScale = 1f;
        }

        [TestCase(StartupLanguage.Chinese)]
        [TestCase(StartupLanguage.English)]
        [BuildScene(Scene), Category("Integration"), Timeout(150000)]
        public async Task Day1sAfternoon_TheHudTheRocketAndTheWorkshop_AreCaptured(string language)
        {
            bool zh = language == StartupLanguage.Chinese;
            TestStartLanguage.Use(language);
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var game = await ReadyScene();
            Assume.That(V5HudKit.On, Is.True, "the fifth pass's look is on outside the Alpha");
            Assume.That(ArenaText.IsChinese, Is.EqualTo(zh), "the game started in the language asked for");
            string tag = $"{(zh ? "zh" : "en")}-{(Art.Enabled ? "art" : "whitebox")}";

            var campaign = await StartStory(game);
            BeginDay1(game, campaign);
            await FinishTheMorning(game, campaign);
            campaign.DebugGrant(40, 30, 37);
            await Frames(20);
            Assert.That(V2CampaignUi.StoryHomeShown, Is.True, "the way home stands under the day plate in the afternoon");
            Capture(game.cam, $"base-hud-{tag}");

            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            await Frames(20);
            var ui = campaign.Ui;
            Assert.That(ui.BaseScreenShown, Is.EqualTo(V2Campaign.BaseScreen.Rocket), "the rocket page shows");
            Capture(game.cam, $"base-rocket-{tag}");

            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Workshop);
            await Frames(20);
            Assert.That(ui.BaseScreenShown, Is.EqualTo(V2Campaign.BaseScreen.Workshop), "the workshop shows");
            Capture(game.cam, $"base-workshop-{tag}");
            if (ui.DebugBaseParts().Contains("c.tab.eng.skill"))
            {
                ui.DebugPressBase("c.tab.eng.skill");
                await Frames(10);
                Capture(game.cam, $"base-workshop-skills-{tag}");
            }

            // The other art: the same two pages, as the switch redraws them.
            Art.Enabled = !Art.Enabled;
            await Frames(20);
            string other = $"{(zh ? "zh" : "en")}-{(Art.Enabled ? "art" : "whitebox")}";
            Capture(game.cam, $"base-workshop-{other}");
            campaign.RequestBaseScreen(V2Campaign.BaseScreen.Rocket);
            await Frames(20);
            Assert.That(ui.BaseScreenShown, Is.EqualTo(V2Campaign.BaseScreen.Rocket), "the rocket page shows in the other art");
            Capture(game.cam, $"base-rocket-{other}");
        }

        // ───── Helpers (as CampaignBaseRedesignTest's and ResultScreenTest's) ─────

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
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!game.CampaignRunning)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The story never started: {game.RunPhase}.");
                game.ExecuteUiCommand(ArenaUiCommand.StartCampaign);
                await Awaitable.NextFrameAsync();
            }
            await Until(() => campaign.Stage == CampaignStage.Tutorial && game.State == GameState.Playing, 15f, "Day 0's walk begins");
            return campaign;
        }

        /// <summary>Day 0's night begun and ended at once: its dawn starts day 1's morning, the player protected from its monsters.</summary>
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

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for: {operation}");
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }

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
    }
}
