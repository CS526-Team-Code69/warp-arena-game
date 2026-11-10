using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Whitebox body colours (the table's looks since 2026-11-10): every enemy turns yellow while winding up and
    /// returns to its own colour afterwards, a hit flash returns the body to that colour, and an element state tints it. The colour checked
    /// is the one the whitebox look shows (WhiteboxCharacter), with the table's colour as the kind's own.
    /// The enemies are spawned from their own pools in the sandbox's clear lane, never reconfigured from another kind: a body keeps its
    /// own kind's collider, and a Brute's wide capsule once pushed the player out of a reconfigured Grunt's reach (gamedev-lessons T-032).
    /// The black hole's first-sight cutscene, which stops the world for seconds, is skipped whenever it starts.</summary>
    [TestFixture]
    public class WhiteboxBodyTintTest
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

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MeleeWindup_Whitebox_BodyTurnsYellowThenBack()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            foreach (var target in targets) target.DiscardWithoutReward();
            var origin = game.player.Position;
            var grunt = Spawn(game, EnemyKind.Grunt, origin + Vector3.forward * (game.player.Radius + .5f + .1f));
            grunt.moveSpeed = 0f;
            // The player stands still, so the Grunt's punch is in reach as soon as it is free to strike.
            game.player.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            Physics.SyncTransforms();
            var look = await Shown(grunt);
            var own = OwnColour(look);
            Assert.That(Diff(look.BodyColour, own), Is.LessThan(.01f), "the table's colour before the wind-up");

            await Until(() => grunt.WindingUp, 6f, "the Grunt starts its punch");
            await Until(() => Diff(look.BodyColour, Enemy.WindupBodyColour) < .01f || !grunt.WindingUp, 1f, "the look takes the wind-up colour");
            Assert.That(Diff(look.BodyColour, Enemy.WindupBodyColour), Is.LessThan(.01f), "yellow while winding up");
            int mark = MarkPart(look.Look);
            if (mark >= 0) Assert.That(Diff(look.Look.ShownColour(mark), look.Look.TableColour(mark)), Is.LessThan(.01f), "the white front mark stays white");
            await Until(() => !grunt.WindingUp, 4f, "the Grunt punches");
            await Until(() => Diff(look.BodyColour, own) < .01f, 1f, "the table's colour again after the punch");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpecialWindupAndHitFlash_Whitebox_KeepTheRightColour()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            foreach (var target in targets) target.DiscardWithoutReward();
            var origin = game.player.Position;
            var stomper = Spawn(game, EnemyKind.Stomper, origin + Vector3.forward * 4f);
            stomper.moveSpeed = 0f;
            // A Warden never attacks, so it has no wind-up colour; it stays enabled so the flash timer runs. Both stand in the lane the
            // sandbox checked clear of walls and holes.
            var warden = Spawn(game, EnemyKind.Warden, origin + Vector3.forward * 10f);
            warden.moveSpeed = 0f;
            Physics.SyncTransforms();
            var stomperLook = await Shown(stomper);
            var wardenLook = await Shown(warden);

            await Until(() => stomper.WindingUp, 6f, "the Stomper starts its wind-up");
            await Until(() => Diff(stomperLook.BodyColour, Enemy.WindupBodyColour) < .01f || !stomper.WindingUp, 1f, "the look takes the wind-up colour");
            Assert.That(Diff(stomperLook.BodyColour, Enemy.WindupBodyColour), Is.LessThan(.01f), "a special turns yellow while winding up");

            Assert.That(warden.IsAlive, Is.True, "the Warden stands in the lane");
            Assert.That(warden.ReceiveDamage(DamageInfo.Attack(1f, game.player, Vector3.forward, 0f)), Is.True, "the hit lands");
            // The flash is applied on the target's next update, not inside ReceiveDamage.
            await Until(() => Diff(wardenLook.BodyColour, Color.white) < .01f, 1f, "the hit flashes white");
            float flash = game.feedback.flashTime + .2f;
            float until = Time.realtimeSinceStartup + flash;
            await Until(() => Time.realtimeSinceStartup > until, flash + 2f, "the hit flash ends");
            Assert.That(Diff(wardenLook.BodyColour, OwnColour(wardenLook)), Is.LessThan(.01f), "after the flash the body returns to the table's colour");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Frozen_Whitebox_TurnsIcyBlue_ThenItsOwnColourAgain()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Art.Enabled = false;
            foreach (var target in targets) target.DiscardWithoutReward();
            var grunt = Spawn(game, EnemyKind.Grunt, game.player.Position + Vector3.forward * 6f);
            grunt.enabled = false; // no wind-up colour on top of the state
            Physics.SyncTransforms();
            var look = await Shown(grunt);
            var own = OwnColour(look);
            var status = ElementStatus.For(grunt);
            Assume.That(status && status.Rules, Is.True, "the Grunt carries its element status");

            status.Add(ElementKind.Frost, status.FrostThreshold * 2f, game.player);
            Assume.That(status.Frozen, Is.True, "enough Frost freezes it");
            await Until(() => Diff(look.BodyColour, own) > .05f, 1f, "the look changes with the freeze");
            var ice = Whitebox.Colour("ice");
            Assert.That(Diff(look.BodyColour, ice), Is.LessThan(Diff(own, ice)), "frozen, the body is nearer the icy blue than its own colour");

            status.ClearStatuses();
            await Until(() => Diff(look.BodyColour, own) < .01f, 1f, "its own colour once the freeze is gone");
        }

        /// <summary>One enemy of the kind from its own pool, at an exact point, worth nothing.</summary>
        static Enemy Spawn(GameManager game, EnemyKind kind, Vector3 at)
        {
            var enemy = game.waves.SpawnLab(kind, at, false, ElementKind.None);
            Assume.That(enemy, Is.Not.Null, $"the pool has a free {kind}");
            enemy.SuppressRewards = true;
            return enemy;
        }

        static async Task<WhiteboxCharacter> Shown(Enemy enemy)
        {
            var look = enemy.GetComponent<WhiteboxCharacter>();
            Assume.That(look, Is.Not.Null, $"{enemy.name} carries its whitebox look");
            await Until(() => look.Shown, 2f, "the whitebox look shows");
            return look;
        }

        /// <summary>The kind's own colour: the table's colour of the look's body part.</summary>
        static Color OwnColour(WhiteboxCharacter look) => look.Look.TableColour(look.Look.MainPart);

        static int MarkPart(WhiteboxLook look)
        {
            for (int i = 0; i < look.PartCount; i++) if (look.IsMark(i)) return i;
            return -1;
        }

        /// <summary>Colours read back can differ in the last float bits, so compare with a small tolerance.</summary>
        static float Diff(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) + Mathf.Abs(a.a - b.a);

        /// <summary>Waits for completed, skipping the black hole's first-sight cutscene whenever it plays (it stops the world for seconds).</summary>
        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (true)
            {
                var map = WorldMap.Active;
                if (map && map.HoleIntroPlaying) map.SkipHoleIntro();
                if (completed()) return;
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
