using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// The end of a run in the fifth pass's look (V5ResultScreen): after a defeat its page shows over the old result screen with the
    /// result, where the run ended, what ended it and the run's numbers, in both art modes. The two looks are saved to the copy's
    /// Logs/result-*.png for a look by eye.
    /// </summary>
    [TestFixture]
    public class ResultScreenTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        bool savedArt;

        [SetUp]
        public void SetUp() => savedArt = Art.Enabled;

        [TearDown]
        public void TearDown()
        {
            Art.Enabled = savedArt;
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
            Capture(game.cam, "result-defeat-art");

            Art.Enabled = false;
            // The font's letters are drawn again for the plain look; give them time before the picture is taken.
            await Frames(30);
            Capture(game.cam, "result-defeat-whitebox");
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
