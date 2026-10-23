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
    public class WhiteboxCaptureTest
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

        [Test, LoadScene(Scene), Timeout(60000)]
        [Description("Whitebox and art captures of the same frame: enemy bodies readable, Spire warning a thin circle, Rammer charge line, no stray rings.")]
        public async Task Capture_SameFrame_WhiteboxAndArtModes()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var origin = game.player.Position;
            var waves = game.waves;
            var spire = targets[0];
            var rammer = targets[1];
            targets[2].DiscardWithoutReward();
            Arm(spire, waves.DefinitionOf(EnemyKind.Spire), origin + new Vector3(-6f, 0f, 4f));
            Arm(rammer, waves.DefinitionOf(EnemyKind.Rammer), origin + new Vector3(8f, 0f, 3f));
            Place(waves.SpawnLab(EnemyKind.Stomper, origin + new Vector3(-4f, 0f, -4f), false, ElementKind.None));
            Place(waves.SpawnLab(EnemyKind.Spitter, origin + new Vector3(5f, 0f, -4f), false, ElementKind.None));
            Place(waves.SpawnLab(EnemyKind.Skitter, origin + new Vector3(2f, 0f, 5f), false, ElementKind.None));
            Place(waves.SpawnLab(EnemyKind.Grunt, origin + new Vector3(-1f, 0f, -5f), false, ElementKind.None));
            Physics.SyncTransforms();
            var cam = game.cam ? game.cam : Camera.main;
            Assume.That(cam, Is.Not.Null, "the scene has a game camera");

            Capture(cam, "warmup");                                                                            
            Art.Enabled = false;
            for (int i = 0; i < 5; i++) await Awaitable.NextFrameAsync();
            Capture(cam, "whitebox-idle");
            Art.Enabled = true;
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Capture(cam, "art-idle");

            await Until(() => spire.WindingUp && rammer.WindingUp, 4f, "the Spire and the Rammer wind up");
            Art.Enabled = false;
            await Awaitable.NextFrameAsync();
            Capture(cam, "whitebox-windup");
            Art.Enabled = true;
            await Awaitable.NextFrameAsync();
            Capture(cam, "art-windup");
        }

        [Test, LoadScene(Scene), Timeout(60000)]
        [Description("Whitebox capture of a black hole at work: the player just outside its gravity ring, enemies on the far side cutting across the ring toward the player and dragged toward the core.")]
        public async Task Capture_BlackHolePull_Whitebox()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var map = WorldMap.Active;
            var hole = map.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active);
            Assume.That(hole, Is.Not.Null, "the arena has an active black hole");
            map.FeedNearestHole(hole.Centre, 1f, 1);                                                   
            Vector3 centre = hole.Centre;
            Vector3 toPlayer = Query.Flat(game.player.Position - centre);
            toPlayer = toPlayer.sqrMagnitude > .01f ? toPlayer.normalized : Vector3.back;
            V3CombatSmokeTest.Teleport(game.player, centre + toPlayer * (hole.GravityRadius + 1.5f));
            var kinds = new[] { EnemyKind.Grunt, EnemyKind.Skitter, EnemyKind.Brute, EnemyKind.Grunt, EnemyKind.Splitter, EnemyKind.Skitter };
            for (int i = 0; i < kinds.Length; i++)
            {
                                                                                                                
                Vector3 direction = Quaternion.Euler(0f, 110f + i * 28f, 0f) * toPlayer;
                float radius = hole.KillRadius + .8f + (i % 2) * 1f;
                var enemy = game.waves.SpawnLab(kinds[i], centre + direction * radius, false, ElementKind.None);
                if (enemy) enemy.moveSpeed = Mathf.Max(enemy.moveSpeed, 3f);
            }
            Physics.SyncTransforms();
            var cam = game.cam ? game.cam : Camera.main;
            Assume.That(cam, Is.Not.Null, "the scene has a game camera");
            Art.Enabled = false;
            float settle = Time.realtimeSinceStartup + .6f;                                                    
            while (Time.realtimeSinceStartup < settle) await Awaitable.NextFrameAsync();
            Capture(cam, "warmup");
            await Awaitable.NextFrameAsync();
            Capture(cam, "whitebox-hole");
        }

        static void Arm(Enemy enemy, EnemyDefinition data, Vector3 at)
        {
            enemy.Configure(data, 1f, false);
            V3CombatSmokeTest.Teleport(enemy, at);
            enemy.moveSpeed = 0f;
            enemy.enabled = true;
        }

        static void Place(Enemy enemy)
        {
            if (!enemy) return;
            enemy.enabled = false;
            enemy.moveSpeed = 0f;
            var body = enemy.GetComponent<Rigidbody>();
            if (body) body.constraints = RigidbodyConstraints.FreezeAll;
        }

        static void Capture(Camera cam, string name)
        {
            const int width = 1600, height = 900;
            var rt = new RenderTexture(width, height, 24);
            var previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
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
            Debug.Log($"[WhiteboxCapture] saved {name}.png");
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
