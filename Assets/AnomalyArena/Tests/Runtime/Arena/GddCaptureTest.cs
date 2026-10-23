using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    [Category("VisualVerification")]
    public class GddCaptureTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        bool savedArt;

        [SetUp]
        public void SetUp() => savedArt = Art.Enabled;

        [TearDown]
        public void TearDown()
        {
            Art.Enabled = savedArt;
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Timeout(180000)]
        [Description("GDD screenshots with the HUD: loadout, weapon card, combat, level-up pick, black-hole cutscene, Armory.")]
        public async Task Capture_GddScreens()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map");
            var game = GameManager.Instance;
            var runtimeWaves = Object.Instantiate(game.waves.waveSet);
            foreach (var wave in runtimeWaves.waves) wave.duration = 25f;
            game.waves.waveSet = runtimeWaves;
            var cam = game.cam ? game.cam : Camera.main;

            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await Frames(10);
            Capture(cam, "gdd-loadout");

            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await Seconds(2.5f);                                                                          
            Capture(cam, "gdd-weapon-card");
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);

            await Until(() => game.RunPhase is ArenaUiState.WaveActive or ArenaUiState.Cutscene, 10f, "the first wave starts");
            bool cutsceneSeen = false;
            if (game.RunPhase == ArenaUiState.Cutscene) cutsceneSeen = await CaptureCutscene(game, cam);
            await Until(() => game.RunPhase == ArenaUiState.WaveActive && game.waves.LivingCount >= 10, 15f, "enemies fill the arena");
            await Seconds(2f);
            if (game.RunPhase == ArenaUiState.WaveActive)
            {
                Art.Enabled = true;                                                                           
                await Frames(3);
                Capture(cam, "gdd-combat");
                Art.Enabled = false;
                await Frames(3);
                Capture(cam, "gdd-combat-whitebox");
                Art.Enabled = true;
                await Frames(3);
            }

            game.progression.GainXp(16f);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 4f, "a level-up opens the card pick");
            await Frames(10);
            Capture(cam, "gdd-levelup");
            await CommandUntil(game, ArenaUiCommand.Cancel, () => game.RunPhase == ArenaUiState.WaveActive);

            if (!cutsceneSeen && !WorldMap.Active.HoleIntroPlaying)
            {
                var hole = WorldMap.Active.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active && h.Feeds == 0);
                if (hole)
                {
                    Vector3 away = Query.Flat(game.player.Position - hole.Centre);
                    away = away.sqrMagnitude > .01f ? away.normalized : Vector3.forward;
                    Vector3 spot = hole.Centre + away * (hole.GravityRadius + 3f);
                    game.player.GetComponent<Rigidbody>().position = spot;
                    game.player.transform.position = spot;
                    Physics.SyncTransforms();
                    float deadline = Time.realtimeSinceStartup + 6f;
                    while (game.RunPhase != ArenaUiState.Cutscene && Time.realtimeSinceStartup < deadline) await Awaitable.NextFrameAsync();
                    if (game.RunPhase == ArenaUiState.Cutscene) await CaptureCutscene(game, cam);
                }
            }

            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 15f, "play resumes");
            await Until(() => game.waves.TimeExpired, 40f, "the wave timer runs out");
            foreach (var enemy in game.waves.Alive.ToArray())
            {
                if (!enemy || !enemy.IsAlive) continue;
                enemy.SuppressRewards = true;
                enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
            }
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 8f, "the post-wave crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            await Frames(10);
            Capture(cam, "gdd-armory");
        }

        static async Task<bool> CaptureCutscene(GameManager game, Camera cam)
        {
            await Seconds(2.1f);
            if (game.RunPhase != ArenaUiState.Cutscene) return false;
            Capture(cam, "gdd-hole-cutscene");
            return true;
        }

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
            Debug.Log($"[GddCapture] saved {name}.png");
        }

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
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
