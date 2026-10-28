using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                                    
                                                                                                                                                                        
                                                                                                                        
    [TestFixture]
    [Category("V3Smoke")]
    public class EnemyFixesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Bloater_WithinTriggerDistance_RushesInAndBlowsUpQuickly()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var data = game.waves.DefinitionOf(EnemyKind.Bloater);
            var bloater = targets[0];
            float playerHp = game.player.Hp;
            Release(bloater, data, game.player.Position + Vector3.forward * (data.triggerDistance - 1f));

            await Until(() => bloater.Rushing, 1f, "the Bloater starts rushing");
            Assert.That(bloater.WindupTinted, Is.True, "it turns yellow while it rushes");
            float start = Time.time;
            await Until(() => game.player.Hp < playerHp || !bloater.IsAlive, 4f, "the Bloater reaches the player and blows up");
            float seconds = Time.time - start;

            Assert.That(game.player.Hp, Is.LessThan(playerHp), "the blast hurts the player");
                                                                                                                       
            Assert.That(seconds, Is.LessThan(1.5f), "rush and fuse are quick");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Bloater_BeyondTriggerDistance_WalksWithoutRushing()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var data = game.waves.DefinitionOf(EnemyKind.Bloater);
            var bloater = targets[0];
            Release(bloater, data, game.player.Position + Vector3.forward * (data.triggerDistance + 4f));

            await Seconds(.6f);
            Assert.That(bloater.Rushing, Is.False, "far away it only walks");
            Assert.That(bloater.WindingUp, Is.False);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Warden_NextToABoss_ShieldsOthersButNotTheBoss()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.player.Invulnerable = true;
            try
            {
                var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * 12f);
                Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");
                tyrant.enabled = false;
                float bossShield = ElementStatus.For(tyrant).Shield;
                var ally = targets[1];
                V3CombatSmokeTest.Teleport(ally, tyrant.Position + Vector3.right * 2f);
                float allyShield = ElementStatus.For(ally).Shield;
                targets[2].DiscardWithoutReward();
                var warden = targets[0];
                warden.Configure(game.waves.DefinitionOf(EnemyKind.Warden), 1f, false);
                V3CombatSmokeTest.Teleport(warden, tyrant.Position + Vector3.left * 2f);
                warden.moveSpeed = 0f;
                warden.enabled = true;
                Physics.SyncTransforms();

                await Until(() => ElementStatus.For(ally).Shield > allyShield, 3f, "the Warden shields the ordinary enemy next to it");
                Assert.That(ElementStatus.For(tyrant).Shield, Is.LessThanOrEqualTo(bossShield + .01f), "the boss keeps only its own shield budget");
            }
            finally { game.player.Invulnerable = false; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Elite_WearsAWhiteOutlineInEveryLook_GoneWhenReusedAsAnOrdinaryEnemy()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            bool artWas = Art.Enabled;
            Enemy chanter = null;
            try
            {
                targets[1].DiscardWithoutReward();
                targets[2].DiscardWithoutReward();
                var enemy = targets[0];
                var data = game.waves.DefinitionOf(EnemyKind.Grunt);
                var look = enemy.GetComponent<V2CharacterIllustration>();
                var model = enemy.GetComponent<V2ModelPresentation>();
                Assume.That(look && model && model.HasAssignedModel, Is.True, "the pooled melee minion carries its saved 3D model");
                enemy.Configure(data, 1f, true);
                Assert.That(enemy.ShowsEliteOutline, Is.True, "a living elite shows the outline");

                                                                                                                                          
                Art.Enabled = true;
                await Frames(3);
                Assume.That(model.UsingModel, Is.True, "the illustrated style shows the model");
                Assert.That(model.EliteOutlined, Is.True, "illustrated with a model: the outline material is on");
                foreach (var renderer in model.modelRenderers)
                {
                    if (!renderer) continue;
                    var last = renderer.sharedMaterials[^1];
                    Assert.That(last && last.shader.name == "AnomalyArena/EliteOutline", Is.True, $"{renderer.name} draws the white outline");
                }
                CaptureCloseUp(game.cam, enemy.Position, "elite-outline-model");

                                                                              
                Art.Enabled = false;
                await Frames(3);
                Assert.That(model.EliteOutlined, Is.False, "whitebox: the model and its outline are hidden");
                var shell = look.EliteShell;
                Assert.That(shell && shell.enabled, Is.True, "whitebox: the white shell is on");
                Assert.That(shell.bounds.size.y, Is.GreaterThan(enemy.bodyRenderer.bounds.size.y), "the shell stands round the body");
                Assert.That(shell.sharedMaterial.GetColor("_BaseColor"), Is.EqualTo(Color.white), "pure white");

                                                                           
                enemy.Configure(data, 1f, false);
                await Frames(3);
                Assert.That(shell.enabled, Is.False, "reused as an ordinary enemy: no shell");
                Art.Enabled = true;
                await Frames(3);
                Assert.That(model.EliteOutlined, Is.False, "nor the outline material on the model");
                foreach (var renderer in model.modelRenderers)
                    if (renderer) Assert.That(renderer.sharedMaterials[^1].shader.name, Is.Not.EqualTo("AnomalyArena/EliteOutline"), "its own materials are back");
                CaptureCloseUp(game.cam, enemy.Position, "elite-outline-none");

                                                                                                                              
                                                                                                     
                chanter = game.waves.SpawnLab(EnemyKind.Chanter, enemy.Position + Vector3.right * 3f, true, ElementKind.None);
                Assume.That(chanter, Is.Not.Null, "the pool has a free Chanter");
                chanter.SuppressRewards = true;
                await Frames(3);
                var chanterModel = chanter.GetComponent<V2ModelPresentation>();
                if (chanterModel && chanterModel.UsingModel)
                    Assert.That(chanterModel.EliteOutlined, Is.True, "the spawned elite's model wears the outline");
                else
                {
                    var drawn = chanter.GetComponent<V2CharacterIllustration>();
                    Assume.That(drawn && drawn.illustration && drawn.illustration.enabled, Is.True, "the spawned elite is drawn by its illustration");
                    var outline = drawn.EliteOutline;
                    Assert.That(outline && outline.enabled, Is.True, "illustrated: the white silhouette is on");
                    Assert.That(outline.sprite, Is.SameAs(drawn.illustration.sprite), "it copies the figure");
                    Assert.That(outline.sortingOrder, Is.EqualTo(drawn.illustration.sortingOrder - 1), "drawn just behind the figure");
                    Assert.That(outline.color, Is.EqualTo(Color.white), "pure white, whatever the affix");
                }
                CaptureCloseUp(game.cam, chanter.Position, "elite-outline-chanter");
            }
            finally
            {
                Art.Enabled = artWas;
                if (chanter && chanter.IsAlive) chanter.DiscardWithoutReward();
            }
        }

                                                                                                                                            
        static void CaptureCloseUp(Camera cam, Vector3 at, string name)
        {
            var t = cam.transform;
            var position = t.position;
            float size = cam.orthographicSize;
            t.position = at + Vector3.up * .8f - t.forward * 30f;
            cam.orthographicSize = 2.5f;
            try { CaptureView(cam, name); }
            finally { t.position = position; cam.orthographicSize = size; }
        }

                                                                                                                                     
        static void CaptureView(Camera cam, string name)
        {
            const int width = 1280, height = 720;
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
            var folder = System.IO.Path.Combine(Application.dataPath, "..", "Logs");
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            UnityEngine.Object.Destroy(texture);
            rt.Release();
            UnityEngine.Object.Destroy(rt);
        }

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }

                                                                                                                
        static void Release(Enemy enemy, EnemyDefinition data, Vector3 at)
        {
            enemy.Configure(data, 1f, false);
            V3CombatSmokeTest.Teleport(enemy, at);
            enemy.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            enemy.enabled = true;
            Physics.SyncTransforms();
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
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
