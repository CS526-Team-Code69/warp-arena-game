using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    [Category("V3Smoke")]
    public class EnemyAttackShapeTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StomperSlam_EnemiesInFrontAndBehind_OnlyTheConeTowardThePlayerIsHit()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var origin = game.player.Position;
            var stomper = targets[0];
            var inFront = targets[1];
            var behind = targets[2];
            V3CombatSmokeTest.Teleport(inFront, origin + Vector3.forward * 2f);
            V3CombatSmokeTest.Teleport(behind, origin + Vector3.forward * 7f);
            Arm(stomper, game.waves.DefinitionOf(EnemyKind.Stomper), origin + Vector3.forward * 4f);
            float frontHp = inFront.Hp, behindHp = behind.Hp;

            await Until(() => stomper.WindingUp, 4f, "the Stomper starts its wind-up");
            Assert.That(stomper.TelegraphActive, Is.False, "the Stomper draws no warning circle");
            Assert.That(stomper.WindupTinted, Is.True, "its body still turns yellow while winding up");
            await Until(() => !stomper.WindingUp, 4f, "the Stomper slams");
            await Awaitable.FixedUpdateAsync();

            Assert.That(inFront.Hp, Is.LessThan(frontHp), "an enemy in the cone toward the player is hit");
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Knocked), "the slam throws the player back");
            Assert.That(behind.Hp, Is.EqualTo(behindHp), "an enemy behind the Stomper is not hit");
        }

        static void Arm(Enemy enemy, EnemyDefinition data, Vector3 at)
        {
            enemy.Configure(data, 1f, false);
            V3CombatSmokeTest.Teleport(enemy, at);
            enemy.moveSpeed = 0f;
            enemy.enabled = true;
            Physics.SyncTransforms();
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
