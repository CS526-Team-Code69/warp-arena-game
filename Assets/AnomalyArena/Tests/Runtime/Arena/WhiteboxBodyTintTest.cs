using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class WhiteboxBodyTintTest
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

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MeleeWindup_Whitebox_BodyTurnsYellowThenBack()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var grunt = targets[0];
            var data = game.waves.DefinitionOf(EnemyKind.Grunt);
            grunt.Configure(data, 1f, false);
            var origin = game.player.Position;
            V3CombatSmokeTest.Teleport(grunt, origin + Vector3.forward * (game.player.Radius + grunt.radius + .1f));
            grunt.moveSpeed = 0f;
            grunt.enabled = true;
            Physics.SyncTransforms();
            Assert.That(Diff(BodyColour(grunt), data.colour), Is.LessThan(.01f), "kind colour before the wind-up");

            await Until(() => grunt.WindingUp, 4f, "the Grunt starts its punch");
            Assert.That(Diff(BodyColour(grunt), Enemy.WindupBodyColour), Is.LessThan(.01f), "yellow while winding up");
            await Until(() => !grunt.WindingUp, 4f, "the Grunt punches");
            await Awaitable.NextFrameAsync();
            Assert.That(Diff(BodyColour(grunt), data.colour), Is.LessThan(.01f), "kind colour again after the punch");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpecialWindupAndHitFlash_Whitebox_KeepTheRightColour()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            var origin = game.player.Position;
            var stomper = targets[0];
            var stomperData = game.waves.DefinitionOf(EnemyKind.Stomper);
            stomper.Configure(stomperData, 1f, false);
            V3CombatSmokeTest.Teleport(stomper, origin + Vector3.forward * 4f);
            stomper.moveSpeed = 0f;
            stomper.enabled = true;
            var target = targets[1];
            var targetData = game.waves.DefinitionOf(EnemyKind.Warden);                                                                              
            target.Configure(targetData, 1f, false);
            target.moveSpeed = 0f;
            target.enabled = true;
            V3CombatSmokeTest.Teleport(target, origin + Vector3.right * 6f);
            targets[2].DiscardWithoutReward();
            Physics.SyncTransforms();

            await Until(() => stomper.WindingUp, 4f, "the Stomper starts its wind-up");
            Assert.That(Diff(BodyColour(stomper), Enemy.WindupBodyColour), Is.LessThan(.01f), "a special turns yellow while winding up");

            Assert.That(target.ReceiveDamage(DamageInfo.Attack(1f, game.player, Vector3.right, 0f)), Is.True, "the hit lands");

            await Until(() => Diff(BodyColour(target), Color.white) < .01f, 1f, "the hit flashes white");
            float flash = game.feedback.flashTime + .2f;
            float until = Time.realtimeSinceStartup + flash;
            await Until(() => Time.realtimeSinceStartup > until, flash + 2f, "the hit flash ends");
            Assert.That(Diff(BodyColour(target), targetData.colour), Is.LessThan(.01f), "after the flash the body returns to its kind colour");
        }

        static Color BodyColour(Enemy enemy)
        {
            var block = new MaterialPropertyBlock();
            enemy.bodyRenderer.GetPropertyBlock(block);
            return block.GetColor("_BaseColor");
        }

        static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);

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
