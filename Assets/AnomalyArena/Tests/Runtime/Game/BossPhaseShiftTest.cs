using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// A boss's change of phase (Enemy.PhaseShift.cs): a hit past the line of its next phase stops on the line and the rest is lost, the
    /// boss enters the phase at once and shows the change, taking no damage and starting no move meanwhile, its burst throws a close player
    /// back unhurt (a push: a wall behind them stops them without a slam), and after the change it is hit and moves again. The line holds only while the boss fights: a body merely configured as
    /// the Prophet outside the void trial takes its hits whole.
    /// </summary>
    [TestFixture]
    [Category("V3Smoke")]
    public class BossPhaseShiftTest
    {
        const string ArenaScene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";

        [TearDown]
        public void TearDown() => Time.timeScale = 1f;

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Tyrant_AHitPastItsLine_StopsOnIt_EntersPhaseTwo_AndTakesNothingUntilTheChangeEnds()
        {
            var (game, targets, tyrant) = await TyrantRedesignTest.TyrantSandbox(8f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            var board = ElementStatus.For(tyrant);
            board.SetShield(0f);
            float line = tyrant.maxHp * b.tyrantPhase2At;
            Assert.That(tyrant.PhaseLine(2), Is.EqualTo(b.tyrantPhase2At).Within(1e-4f), "its phase-2 line");
            Assert.That(tyrant.PhaseLine(3), Is.Zero, "and no third phase");

            Assert.That(tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .9f, source = DamageSource.Wall, ignoreShield = true }), Is.True);
            Assert.That(tyrant.Hp, Is.EqualTo(line).Within(.01f), "the hit stops on the phase-2 line and the rest is lost");
            Assert.That(tyrant.BossPhase, Is.EqualTo(2), "it is in phase 2 at once");
            Assert.That(tyrant.PhaseShifting, Is.True, "and shows the change");
            Assert.That(board.Shield, Is.GreaterThan(0f), "its shield is refilled as it enters phase 2");

            float shield = board.Shield;
            int guards = tyrant.PhaseGuardSequence;
            Assert.That(tyrant.ReceiveDamage(DamageInfo.Attack(40f, game.player, Vector3.forward)), Is.False, "a hit during the change is turned away");
            Assert.That(tyrant.ReceiveDamage(new DamageInfo { amount = 40f, source = DamageSource.Burn }), Is.False, "so is a burn tick");
            Assert.That(tyrant.Hp, Is.EqualTo(line).Within(.01f), "no HP is lost");
            Assert.That(board.Shield, Is.EqualTo(shield).Within(.01f), "nor shield");
            Assert.That(tyrant.PhaseGuardSequence, Is.GreaterThan(guards), "the glancing hits are counted for the art layer");

            float started = Time.time;
            await Until(() => !tyrant.PhaseShifting, b.tyrantShiftSeconds + 2f, "the change ends");
            Assert.That(Time.time - started, Is.EqualTo(b.tyrantShiftSeconds).Within(.35f), "it lasts its seconds");
            Assert.That(tyrant.PhaseBurstSequence, Is.EqualTo(1), "with one burst");
            board.SetShield(0f);
            Assert.That(tyrant.ReceiveDamage(new DamageInfo { amount = 40f, source = DamageSource.Wall, ignoreShield = true }), Is.True, "after it, hits land again");
            Assert.That(tyrant.Hp, Is.EqualTo(line - 40f).Within(.01f), "below the line: its last phase has none");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Tyrant_DuringTheChange_DropsItsWindupAndStartsNoMove_ThenMovesAgain()
        {
            var (game, targets, tyrant) = await TyrantRedesignTest.TyrantSandbox(5f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            TyrantRedesignTest.Force(tyrant, Enemy.TailSweepMove);
            await Until(() => tyrant.WindingUp, 3f, "a sweep winding up");
            ElementStatus.For(tyrant).SetShield(0f);
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .9f, source = DamageSource.Wall, ignoreShield = true });
            Assume.That(tyrant.PhaseShifting, Is.True, "the change begins");
            await Awaitable.FixedUpdateAsync();
            await Awaitable.FixedUpdateAsync();
            Assert.That(tyrant.WindingUp, Is.False, "the wind-up is dropped");

            TyrantRedesignTest.Force(tyrant, Enemy.TailSweepMove);
            while (tyrant.PhaseShifting)
            {
                Assert.That(tyrant.WindingUp || tyrant.TailSweeping, Is.False, "no move starts during the change");
                await Awaitable.FixedUpdateAsync();
            }

            await Until(() => tyrant.WindingUp, b.shiftResumeHold + 2f, "the forced sweep after the change");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Tyrant_TheBurst_ThrowsAClosePlayerBack_WithoutHurtingThem()
        {
            var (game, targets, tyrant) = await TyrantRedesignTest.TyrantSandbox(8f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            var player = game.player;
            Vector3 away = Query.Flat(player.Position - tyrant.Position);
            if (away.sqrMagnitude < .01f) away = Vector3.back;
            TyrantRedesignTest.Place(player, tyrant.Position + away.normalized * (tyrant.radius + 1.5f));
            float hp = player.Hp, before = Query.Flat(player.Position - tyrant.Position).magnitude;
            Assume.That(before - player.Radius, Is.LessThan(b.shiftPushRadius), "the player stands within the burst's reach");
            ElementStatus.For(tyrant).SetShield(0f);
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .9f, source = DamageSource.Wall, ignoreShield = true });
            await Until(() => tyrant.PhaseBurstSequence > 0, b.tyrantShiftSeconds + 1f, "the burst");
            await Seconds(.6f);
            float after = Query.Flat(player.Position - tyrant.Position).magnitude;
            Assert.That(after - before, Is.GreaterThan(b.shiftPushDistance * .5f), "the burst throws the close player back");
            Assert.That(player.Hp, Is.EqualTo(hp), "without hurting them");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(60000)]
        public async Task Tyrant_TheBurst_PushesAPlayerIntoAWallBehindThem_WithoutAWallSlam()
        {
            var (game, targets, tyrant) = await TyrantRedesignTest.TyrantSandbox(8f);
            foreach (var target in targets) target.DiscardWithoutReward();
            var b = game.waves.bosses;
            var player = game.player;
            Vector3 away = Query.Flat(player.Position - tyrant.Position);
            away = away.sqrMagnitude < .01f ? Vector3.back : away.normalized;
            TyrantRedesignTest.Place(player, tyrant.Position + away * (tyrant.radius + 1.5f));
            float before = Query.Flat(player.Position - tyrant.Position).magnitude;
            Assume.That(before - player.Radius, Is.LessThan(b.shiftPushRadius), "the player stands within the burst's reach");
            const float gap = .4f;
            Assume.That(gap, Is.LessThan(b.shiftPushDistance * player.knockbackScale * .5f), "the wall stands well inside the push");
            var wall = new GameObject("BossPhaseShiftTestWall") { layer = GameManager.WallLayer };
            wall.transform.SetPositionAndRotation(player.Position + away * (player.Radius + gap + .2f) + Vector3.up * 1.5f, Quaternion.LookRotation(away));
            wall.AddComponent<BoxCollider>().size = new Vector3(4f, 3f, .4f);
            Physics.SyncTransforms();
            int slams = 0;
            void Count(DamageInfo info, float amount) { if (info.source == DamageSource.Wall) slams++; }
            player.DamageTaken += Count;
            try
            {
                float hp = player.Hp;
                ElementStatus.For(tyrant).SetShield(0f);
                tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .9f, source = DamageSource.Wall, ignoreShield = true });
                await Until(() => tyrant.PhaseBurstSequence > 0, b.tyrantShiftSeconds + 1f, "the burst");
                await Seconds(.6f);
                float moved = Query.Flat(player.Position - tyrant.Position).magnitude - before;
                Assert.That(moved, Is.GreaterThan(gap * .5f), "the burst pushes the player up to the wall");
                Assert.That(moved, Is.LessThan(gap + .3f), "and the wall stops them");
                Assert.That(slams, Is.Zero, "without a wall slam");
                Assert.That(player.Hp, Is.EqualTo(hp), "so no HP is lost");
            }
            finally
            {
                player.DamageTaken -= Count;
                UnityEngine.Object.Destroy(wall);
            }
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(90000)]
        public async Task Prophet_InTheTrial_AHitPastBothLines_StopsOnTheFirst_AndALaterHitOnTheSecond()
        {
            var (game, _, hollow) = await NashProphetTest.StartTrial();
            var b = game.waves.bosses;
            hollow.HoldAttacks(60f);
            Assert.That(hollow.ReceiveDamage(new DamageInfo { amount = hollow.maxHp * .95f, source = DamageSource.Wall, ignoreShield = true }), Is.True);
            Assert.That(hollow.Hp, Is.EqualTo(hollow.maxHp * b.nashPhase2HpShare).Within(.01f), "it stops on the phase-2 line");
            Assert.That(hollow.BossPhase, Is.EqualTo(2), "in phase 2");
            Assert.That(hollow.PhaseShifting, Is.True, "showing the change");
            await Until(() => !hollow.PhaseShifting, b.nashShiftSeconds + 2f, "the first change ends");

            Assert.That(hollow.ReceiveDamage(new DamageInfo { amount = hollow.maxHp * .95f, source = DamageSource.Wall, ignoreShield = true }), Is.True);
            Assert.That(hollow.Hp, Is.EqualTo(hollow.maxHp * b.nashPhase3HpShare).Within(.01f), "then on the phase-3 line");
            Assert.That(hollow.BossPhase, Is.EqualTo(3), "in phase 3");
            Assert.That(hollow.PhaseShifting, Is.True, "showing that change too");
        }

        [Test, LoadScene(ArenaScene), Category("Integration"), Timeout(40000)]
        public async Task AProphetOutsideTheTrial_HasNoLine_ItsHitsLandWhole()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var boss = targets[0];
            boss.Configure(game.waves.DefinitionOf(EnemyKind.Hollow), 1f, false);
            boss.Heal(boss.maxHp);
            Assume.That(boss.IsBoss, Is.True, "the scene keeps the Nash Prophet");
            Assume.That(WorldMap.Active.TrialActive, Is.False, "outside the void trial");
            boss.ReceiveDamage(new DamageInfo { amount = boss.maxHp * .5f, source = DamageSource.Wall, ignoreShield = true });
            Assert.That(boss.Hp, Is.EqualTo(boss.maxHp * .5f).Within(.01f), "the hit lands whole, past the phase-2 line");
            Assert.That(boss.PhaseShifting, Is.False, "and no change begins");
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
