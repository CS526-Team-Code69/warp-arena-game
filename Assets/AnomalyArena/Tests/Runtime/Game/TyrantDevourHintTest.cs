using System;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The first Astral Guard of a fight that devours anything queues a notice saying why the Tyrant's HP came back and how to
    /// stop it (Enemy.TyrantDevourHint.cs), and it reaches the screen; nothing is said while it channels, and a second guard in the same
    /// fight says nothing more.</summary>
    [TestFixture]
    public class TyrantDevourHintTest
    {
        const string ArenaScene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task FirstDevourOfAFight_SaysWhyAndHowToStopIt_Once()
        {
            var (game, targets, tyrant) = await TyrantRedesignTest.TyrantSandbox(14f);
            var b = game.waves.bosses;
            Assume.That(tyrant.DevourTold, Is.False, "a new fight has said nothing yet");
            string said = ArenaText.Pair("吞噬", "DEVOURS"), how = ArenaText.Pair("靠近它打", "up close");
            foreach (var target in targets) Hold(target);
            V3CombatSmokeTest.Teleport(targets[0], tyrant.Position + Vector3.right * (tyrant.Radius + 1.5f));
            V3CombatSmokeTest.Teleport(targets[1], tyrant.Position + Vector3.back * (b.astralRadius + 8f));
            V3CombatSmokeTest.Teleport(targets[2], tyrant.Position + Vector3.left * (b.astralRadius + 8f));
            Physics.SyncTransforms();

            TyrantRedesignTest.Force(tyrant, Enemy.AstralGuardMove);
            await Until(() => tyrant.AstralChanneling, 3f, "the channel");
            Assert.That(Said(game), Does.Not.Contain(said), "nothing is said while it channels");
            await Until(() => !tyrant.WindingUp, b.astralChannel + 2f, "the channel ends");
            Assume.That(targets[0].gameObject.activeSelf, Is.False, "the minion in reach was devoured");
            Assert.That(tyrant.DevourTold, Is.True, "the fight has said it");
            tyrant.enabled = false; // it starts no other move while the hint waits its turn
            await Until(() => Said(game).Contains(said), V2WorldLabels.FirstSightSeconds + V2WorldLabels.NoticeGap + 2f, "the hint on the screen");
            Assert.That(Said(game), Does.Contain(how), "the line says how to stop it");

            V2WorldLabels.ForgetFirstSights();
            game.hud.v2Hud.ShowBanner(null, 0f);
            tyrant.enabled = true;
            V3CombatSmokeTest.Teleport(targets[2], tyrant.Position + Vector3.left * (tyrant.Radius + 1.5f));
            Physics.SyncTransforms();
            TyrantRedesignTest.Force(tyrant, Enemy.AstralGuardMove);
            await Until(() => tyrant.AstralChanneling, 3f, "the second channel");
            await Until(() => !tyrant.WindingUp, b.astralChannel + 2f, "the second channel ends");
            Assume.That(targets[2].gameObject.activeSelf, Is.False, "the second minion was devoured");
            await Seconds(1.5f);
            Assert.That(Said(game), Does.Not.Contain(said), "a second guard in the same fight says nothing more");
        }

        /// <summary>The minion stays where it is put.</summary>
        static void Hold(Enemy minion)
        {
            minion.enabled = false;
            minion.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
        }

        /// <summary>What the notices' queue last showed and the line the HUD was last asked to show over the battle.</summary>
        static string Said(GameManager game) =>
            V2WorldLabels.LastShown + "\n" +
            ((string)typeof(V2Hud).GetField("transientText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(game.hud.v2Hud) ?? "");

        static async Task Seconds(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until) await Awaitable.NextFrameAsync();
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
