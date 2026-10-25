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
