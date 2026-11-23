using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>A monster set down on open ground (Enemy.SetDownOnOpenGround through EnemyUnstickFade) does not jump in sight: it shrinks
    /// where it stands, is moved only once it is out of sight, grows back where it is set down, and its look is its own size again at the
    /// end; it holds still meanwhile.</summary>
    [TestFixture]
    public class UnstickFadeTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task SetDown_FadesOutWhereItStands_MovesOutOfSight_FadesInWhereItIsSetDown()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var enemy = targets[0];
            Assume.That(enemy && enemy.IsAlive && enemy.transform.childCount > 0, Is.True, "a monster with a look to fade");
            var parts = Enumerable.Range(0, enemy.transform.childCount).Select(i => enemy.transform.GetChild(i)).ToArray();
            var sizes = parts.Select(p => p.localScale).ToArray();
            Vector3 from = enemy.Position, to = from + Vector3.right * 4f;

            var fade = EnemyUnstickFade.Begin(enemy, to);
            Assert.That(fade.Active, Is.True, "the fade runs");
            await Seconds(.15f);
            Assert.That(Query.Flat(enemy.Position - from).magnitude, Is.LessThan(.5f), "first it fades out where it stands");
            Assert.That(parts[0].localScale.magnitude, Is.LessThan(sizes[0].magnitude * .9f), "its look is shrinking");
            await Until(() => Query.Flat(enemy.Position - to).magnitude < .3f, 2f, "it is moved, out of sight");
            Assert.That(parts[0].localScale.magnitude, Is.LessThan(sizes[0].magnitude * .5f), "it is moved while its look is small");
            await Until(() => !fade.Active, 2f, "the fade ends");
            Assert.That(Query.Flat(enemy.Position - to).magnitude, Is.LessThan(.5f), "it ends where it was set down");
            for (int i = 0; i < parts.Length; i++)
                Assert.That(parts[i].localScale, Is.EqualTo(sizes[i]), $"{parts[i].name} is its own size again");
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
