using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class V4BackfireTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant, UpgradeAxis axes = UpgradeAxis.None)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, axes = axes };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ArcChain_FewerTargetsThanJumps_ReturnsLightningToShooter()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.ArcCoil, 0);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();

            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(targets.Count(t => t.Hp < t.maxHp), Is.EqualTo(3), "all three lane targets are chained");
            Assert.That(status.Lightning + (status.Paralysed ? 99f : 0f), Is.GreaterThanOrEqualTo(1.5f), "two unused jumps return as Lightning on the shooter");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ShatterFan_AimedAwayFromEnemies_LeavesThreeIcePatches()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.CryoCannon, 0);
            game.player.DebugSetAim(Vector3.back);

            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.IcePatch), Is.EqualTo(3));
            Equip(game, WeaponForm.CryoCannon, 0, UpgradeAxis.Tame);
            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();
            Assert.That(game.runWeapons.Hazards.Count, Is.Zero, "Tame shards leave no patches");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Execute_KillingShot_LaunchesSlugThatReturnsAlongItsPath()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.Railgun, 1);
            var victim = targets[0];
            victim.ReceiveDamage(new DamageInfo { amount = victim.maxHp * .7f, source = DamageSource.Poison, ignoreShield = true });

            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(victim.IsAlive, Is.False, "the low target is executed");
            Assert.That(AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.returnAlongPath), Is.True, "a rebounding slug is in flight");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpinUp_FullSpin_LimitsAimTurningUnlessTamed()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var gun = Equip(game, WeaponForm.Minigun, 0);
            gun.heldTime = 3f;
            Assert.That(game.runWeapons.AimTurnRateLimit, Is.EqualTo(game.runWeapons.upgrades.spinUpFullTurn).Within(.01f));
            gun.axes = UpgradeAxis.Tame;
            Assert.That(game.runWeapons.AimTurnRateLimit, Is.Zero);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FuelTank_HitWhileStreaming_BurnsShooterAndNearbyEnemy()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var stream = Equip(game, WeaponForm.Flamethrower, 0);
            var near = targets[0];
            V3CombatSmokeTest.Teleport(near, game.player.Position + Vector3.forward * 1.5f);
            Physics.SyncTransforms();
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();
            stream.streamFiredAt = Time.time;

            game.player.ReceiveDamage(DamageInfo.Attack(1f, near));

            Assert.That(status.Fire, Is.GreaterThan(0f), "the burst sets the shooter alight");
            Assert.That(near.Hp, Is.LessThan(near.maxHp), "the burst burns the enemy inside the ring");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task PlayerFreeze_LastsTheFullMonsterDuration()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();
            status.Add(ElementKind.Frost, status.FrostThreshold + 1f);
            Assert.That(status.Frozen, Is.True);
            float until = Time.time + status.Rules.frostDuration - .2f;
            while (Time.time < until) await Awaitable.NextFrameAsync();
            Assert.That(status.Frozen, Is.True, "no one-second cap on the player any more");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task ResetRun_HasTwoAutoSlots()
        {
            await Awaitable.NextFrameAsync();
            var game = GameManager.Instance;
            game.runWeapons.ResetRun();
            Assert.That(game.runWeapons.Auto, Has.Length.EqualTo(2));
            Assert.That(game.UiSnapshot.autoSlots, Has.Length.EqualTo(2));
        }
    }
}
