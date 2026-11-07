using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    [Category("VisualVerification")]
    public class ModelVisibilityCaptureTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        static readonly EnemyKind[] Kinds = { EnemyKind.Brute, EnemyKind.Grunt, EnemyKind.Skitter, EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Stomper };
        bool savedArt;

        [SetUp]
        public void SetUp() => savedArt = Art.Enabled;

        [TearDown]
        public void TearDown() => Art.Enabled = savedArt;

        [Test, LoadScene(Scene), Timeout(90000)]
        public async Task SixKinds_ShowTheirBodies_InTheArtAndInTheWhitebox()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map");
            var game = GameManager.Instance;
            var cam = game.cam ? game.cam : Camera.main;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) WorldMap.Active.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }

            Vector3 centre = game.player.Position;
            var lineup = new List<Enemy>();
            for (int i = 0; i < Kinds.Length; i++)
            {
                float angle = i * Mathf.PI * 2f / Kinds.Length;
                Vector3 at = centre + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 4.5f;
                var enemy = game.waves.SpawnLab(Kinds[i], at, false, ElementKind.None);
                Assume.That(enemy, Is.Not.Null, $"the pool has a free {Kinds[i]}");
                enemy.SuppressRewards = true;
                enemy.enabled = false;                                     
                lineup.Add(enemy);
            }

            var report = new StringBuilder();
            Art.Enabled = true;
            await Frames(10);
            Capture(cam, "models-art");
            var missingArt = Check(lineup, cam, true, report);
            Art.Enabled = false;
            await Frames(10);
            Capture(cam, "models-whitebox");
            var missingWhitebox = Check(lineup, cam, false, report);
            Debug.Log("[ModelVisibility]\n" + report);

            Assert.That(missingArt, Is.Empty, "every enemy shows its 3D model in the illustrated art:\n" + report);
            Assert.That(missingWhitebox, Is.Empty, "every enemy shows its body in the whitebox:\n" + report);
        }

        static List<string> Check(List<Enemy> lineup, Camera cam, bool art, StringBuilder report)
        {
            var missing = new List<string>();
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            foreach (var enemy in lineup)
            {
                var presentation = enemy.GetComponent<V2ModelPresentation>();
                bool usingModel = presentation && presentation.UsingModel;
                IEnumerable<Renderer> shown = art && presentation && presentation.modelRenderers != null
                    ? presentation.modelRenderers
                    : enemy.GetComponentsInChildren<Renderer>(false).Where(r => !(presentation && presentation.modelRenderers != null && presentation.modelRenderers.Contains(r)));
                var visible = shown.Where(r => r && r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)
                                               && GeometryUtility.TestPlanesAABB(planes, r.bounds)).ToList();
                string kind = enemy.definition ? enemy.definition.kind.ToString() : enemy.name;
                report.AppendLine($"{(art ? "art" : "whitebox")} {kind}: usingModel={usingModel} visibleRenderers={visible.Count}" +
                                  (visible.Count > 0 ? $" bounds={visible.Aggregate(visible[0].bounds, (b, r) => { b.Encapsulate(r.bounds); return b; }).size}" : ""));
                if (visible.Count == 0 || art && !usingModel) missing.Add(kind);
            }
            return missing;
        }

        static void Capture(Camera cam, string name)
        {
            const int width = 1600, height = 900;
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
            Debug.Log($"[ModelVisibility] saved {name}.png");
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
