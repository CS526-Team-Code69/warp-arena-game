using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The Spire's light wave.
    /// After its wind-up the Spire sends a wave from its centre out to its sentry range, 30% smaller than before, at waveSpeed. The wave
    /// hurts nobody; the step its front reaches the living player with no wall between them, the laser locks on them, and the laser is
    /// the attack. A player beyond the range or behind a wall is never caught. The wave runs on the battlefield's time, and its ring on
    /// the ground draws its front, dark behind walls, and leaves nothing once the wave has stopped or the player has died.</summary>
    [TestFixture]
    [Category("V3Smoke")]
    public class SpireLightWaveTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PlayerInsideTheSentryRange_TheFrontCatchesThemAndTheLaserLocks_OnlyTheLaserHurts()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var origin = player.Position;
            var spire = targets[0];
            var bystander = targets[1];
            targets[2].DiscardWithoutReward();
            V3CombatSmokeTest.Teleport(bystander, origin + Vector3.forward * 7f);
            Arm(spire, game.waves.DefinitionOf(EnemyKind.Spire), origin + Vector3.forward * 4f);
            float bystanderHp = bystander.Hp, playerHp = player.Hp;
            int catches = spire.LightWaveCatches, shots = spire.LaserSequence;
            float nearEdge = Query.Flat(player.Position - spire.Position).magnitude - player.Radius;

            await Until(() => spire.WindingUp, 4f, "the Spire starts its wind-up");
            Assert.That(spire.TelegraphActive, Is.True, "the Spire draws its sentry circle while it winds up");
            await UntilStep(() => spire.LaserLocking, 4f, "the light wave reaches the player and the laser locks");
            float reached = spire.LightWaveRadius, caughtHp = player.Hp, bystanderCaughtHp = bystander.Hp;
            Vector3 aim = spire.LaserDirection;
            await Until(() => spire.LaserSequence > shots, 4f, "the laser fires");

            Assert.That(reached, Is.GreaterThanOrEqualTo(nearEdge), "the laser locks only once the front has reached the player's body");
            Assert.That(reached, Is.LessThan(nearEdge + spire.definition.waveSpeed * Time.fixedDeltaTime * 3f), "in the step the front gets there");
            Assert.That(Vector3.Dot(aim, Query.Flat(player.Position - spire.Position).normalized), Is.GreaterThan(.999f), "aimed at the player");
            Assert.That(spire.LightWaveCatches, Is.EqualTo(catches + 1), "one catch for the wave");
            Assert.That(caughtHp, Is.EqualTo(playerHp), "the light wave itself does the player no damage");
            Assert.That(bystanderCaughtHp, Is.EqualTo(bystanderHp), "nor the enemy it passes over");
            Assert.That(player.Hp, Is.LessThan(playerHp), "the laser hits the player who stayed on its line");
            Assert.That(bystander.Hp, Is.EqualTo(bystanderHp), "an enemy inside the sentry range is never hurt");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PlayerBeyondTheSentryRange_TheWaveStopsShortOfThem_NoLaser()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var spire = targets[0];
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var data = game.waves.DefinitionOf(EnemyKind.Spire);
            Arm(spire, data, player.Position + Vector3.forward * (data.pulseRadius + player.Radius + 1.5f));
            float playerHp = player.Hp;
            int catches = spire.LightWaveCatches, shots = spire.LaserSequence;

            await Until(() => spire.LightWaveActive, 6f, "the wind-up sends the light wave out");
            await Until(() => spire.WindingUp, 6f, "the wave runs out and the next wind-up begins");

            Assert.That(spire.LightWaveRadius, Is.EqualTo(data.pulseRadius).Within(1e-4f), "the wave went all the way to the sentry range");
            Assert.That(spire.LightWaveCatches, Is.EqualTo(catches), "and stopped short of the player");
            Assert.That(spire.LaserLocking || spire.LaserSequence > shots, Is.False, "so no laser locks or fires");
            Assert.That(player.Hp, Is.EqualTo(playerHp), "and nothing hurts the player");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WallBetweenTheSpireAndThePlayer_TheWaveDoesNotCatchThem_AndItsRingStaysDarkBehindTheWall()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var origin = player.Position;
            var spire = targets[0];
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            Arm(spire, game.waves.DefinitionOf(EnemyKind.Spire), origin + Vector3.forward * 4.5f);
            var wall = Wall(origin + Vector3.forward * 2f); // after the Spire has moved, so no body ever overlaps it
            try
            {
                Assume.That(Query.WallBetween(Query.AtCastHeight(spire.Position), Query.AtCastHeight(player.Position), out _), Is.True,
                    "the wall stands between the Spire and the player");
                var presentation = spire.GetComponent<SpireWavePresentation>();
                Assume.That(presentation, Is.Not.Null, "a Spire carries its light wave's presentation");
                Vector3 toPlayer = Query.Flat(player.Position - spire.Position);
                float nearEdge = toPlayer.magnitude - player.Radius;
                float playerHp = player.Hp;
                int catches = spire.LightWaveCatches, shots = spire.LaserSequence;

                await Until(() => spire.LightWaveActive && spire.LightWaveRadius > nearEdge + player.Radius * 2f, 6f,
                    "the front sweeps past the player's body");
                var ring = presentation.Ring;
                bool drawn = ring && ring.Visible;
                bool darkWherePlayerStands = drawn && !ring.LitAt(toPlayer, nearEdge);
                bool litWhereNothingBlocks = drawn && ring.LitAt(-toPlayer, nearEdge);
                await Until(() => spire.WindingUp, 6f, "the wave runs out and the next wind-up begins");

                Assert.That(spire.LightWaveCatches, Is.EqualTo(catches), "the wave never catches a player behind a wall");
                Assert.That(spire.LaserLocking || spire.LaserSequence > shots, Is.False, "so no laser locks or fires");
                Assert.That(player.Hp, Is.EqualTo(playerHp), "and the wave hurt nobody");
                Assert.That(drawn, Is.True, "the ring is drawn while the wave is out");
                Assert.That(darkWherePlayerStands, Is.True, "the ring stays dark in the wall's shadow, where the player stands");
                Assert.That(litWhereNothingBlocks, Is.True, "and is lit where nothing blocks it");
            }
            finally
            {
                UnityEngine.Object.Destroy(wall);
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightWave_GrowsAtItsSpeedToTheSentryRangeAndStops_TheRingDrawsItsFrontAndLeavesNothing()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var spire = targets[0];
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var data = game.waves.DefinitionOf(EnemyKind.Spire);
            // The player stands beyond the range, so nothing interrupts the wave's whole run.
            Arm(spire, data, player.Position + Vector3.forward * (data.pulseRadius + player.Radius + 1.5f));
            var presentation = spire.GetComponent<SpireWavePresentation>();
            Assume.That(presentation, Is.Not.Null, "a Spire carries its light wave's presentation");

            await Until(() => spire.LightWaveActive, 6f, "the wind-up sends the light wave out");
            float started = Time.fixedTime, sample = spire.LightWaveRadius, most = sample;
            bool grows = true, ringOnTheFront = true;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (spire.LightWaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: the wave reaches the sentry range");
                await Awaitable.NextFrameAsync();
                // No physics step falls between a frame's Update and its LateUpdate, so the ring drawn last frame shows last frame's sample.
                var drawn = presentation.Ring;
                ringOnTheFront &= drawn && drawn.Visible && Mathf.Abs(drawn.Radius - sample) < 1e-4f;
                float now = spire.LightWaveRadius;
                grows &= now >= sample;
                most = Mathf.Max(most, now);
                sample = now;
            }

            float lasted = Time.fixedTime - started;
            await Until(() => !presentation.Ring.Visible, 2f, "the ring fades out at the sentry range and nothing is left on the ground");

            // The sentry range 15 → 10.5 and the laser 20 → 14 with it, so the laser still
            // reaches anyone the wave can find.
            Assert.That(data.pulseRadius, Is.EqualTo(15f * .7f).Within(1e-4f), "the sentry range is 30% smaller");
            Assert.That(data.laserLength, Is.EqualTo(20f * .7f).Within(1e-4f), "the laser is 30% shorter");
            Assert.That(most, Is.EqualTo(data.pulseRadius).Within(1e-4f), "the front reaches the sentry range");
            Assert.That(spire.LightWaveRadius, Is.EqualTo(data.pulseRadius).Within(1e-4f), "and stops there");
            Assert.That(grows, Is.True, "it only ever grows on its way out");
            Assert.That(lasted, Is.EqualTo(data.pulseRadius / data.waveSpeed).Within(.15f), "at waveSpeed cells a second");
            Assert.That(ringOnTheFront, Is.True, "every frame the ring's leading edge is the wave's front");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BattlefieldTimeStandsStill_TheWaveAndItsRingHold_ThenGoOnFromThere()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var spire = targets[0];
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var data = game.waves.DefinitionOf(EnemyKind.Spire);
            Arm(spire, data, player.Position + Vector3.forward * (data.pulseRadius + player.Radius + 1.5f));
            var presentation = spire.GetComponent<SpireWavePresentation>();
            Assume.That(presentation, Is.Not.Null, "a Spire carries its light wave's presentation");
            await Until(() => spire.LightWaveActive && spire.LightWaveRadius > 2f, 6f, "the light wave is on its way out");
            float held = 0f;
            bool stillOut, ringHeld;
            try
            {
                // The battlefield's own clock at zero, as in the freeze on the way back from the void trial: enemies tick with no time.
                SkillWorldAPI.TimeSlip?.Invoke(0f, 30f);
                Assume.That(game.WorldTimeScale, Is.Zero, "the battlefield's time stands still");
                await Awaitable.FixedUpdateAsync();
                held = spire.LightWaveRadius;
                for (int i = 0; i < 25; i++) await Awaitable.FixedUpdateAsync();
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                stillOut = spire.LightWaveActive && spire.LightWaveRadius == held;
                ringHeld = presentation.Ring && presentation.Ring.Visible && presentation.Ring.Radius == held;
            }
            finally
            {
                SkillWorldAPI.TimeSlip?.Invoke(1f, 0f);
            }

            await Until(() => spire.LightWaveRadius > held + .5f, 3f, "once time moves again the wave goes on");

            Assert.That(stillOut, Is.True, "the front holds still while the battlefield's time does, and the wave stays out");
            Assert.That(ringHeld, Is.True, "the ring on the ground holds with it");
            Assert.That(held, Is.LessThan(data.pulseRadius), "it held part way out, not at the sentry range");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PlayerDiesWhileTheWaveIsOut_TheWaveEnds_ItsRingLeavesNothingBehindTheResult()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var spire = targets[0];
            targets[1].DiscardWithoutReward();
            targets[2].DiscardWithoutReward();
            var data = game.waves.DefinitionOf(EnemyKind.Spire);
            // The player stands beyond the range, so the wave is still on its way out, having caught nobody, when they die.
            Arm(spire, data, player.Position + Vector3.forward * (data.pulseRadius + player.Radius + 1.5f));
            var presentation = spire.GetComponent<SpireWavePresentation>();
            Assume.That(presentation, Is.Not.Null, "a Spire carries its light wave's presentation");
            await Until(() => spire.LightWaveActive && spire.LightWaveRadius > 2f && presentation.Ring && presentation.Ring.Visible, 6f,
                "the light wave and its ring are on their way out");
            Assume.That(spire.LightWaveRadius, Is.LessThan(data.pulseRadius), "part way out, not at the sentry range");

            // Wall damage is not an attack, so nothing in the sandbox turns it away (as in RunAnalyticsTest).
            Assert.That(player.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Wall }), Is.True, "the player dies");
            await Until(() => game.RunPhase == ArenaUiState.Result, 5f, "the run ends");
            // The result screen stops time in the frame the player dies, so the Spire may not step again; the ring is drawn in LateUpdate.
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();

            Assert.That(spire.LightWaveActive, Is.False, "a wave with nobody left to find is over");
            Assert.That(presentation.Ring.Visible, Is.False, "and its ring goes at once instead of staying frozen on the ground behind the result");
        }

        /// <summary>Turns a frozen sandbox target into a live enemy of this kind at a point; it keeps still and attacks on its own.</summary>
        static void Arm(Enemy enemy, EnemyDefinition data, Vector3 at)
        {
            enemy.Configure(data, 1f, false);
            V3CombatSmokeTest.Teleport(enemy, at);
            enemy.moveSpeed = 0f;
            enemy.enabled = true;
            Physics.SyncTransforms();
        }

        /// <summary>A wall across the lane at a point: a box collider on the wall layer, the one the wall checks (Query.WallBetween) see.</summary>
        static GameObject Wall(Vector3 at)
        {
            var wall = new GameObject("SpireLightWaveTestWall") { layer = GameManager.WallLayer };
            wall.transform.position = new Vector3(at.x, 1.5f, at.z);
            wall.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, .4f);
            Physics.SyncTransforms();
            return wall;
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

        /// <summary>Waits physics step by physics step, for a state to be seen in the step it starts.</summary>
        static async Task UntilStep(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.FixedUpdateAsync();
            }
        }
    }
}
