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

        static async Task Until(System.Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }

        static void LeaveHp(Combatant body, float hp) =>
            body.ReceiveDamage(new DamageInfo { amount = body.Hp - hp, source = DamageSource.Poison, ignoreShield = true });

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
        public async Task Execute_TargetLeftUnderThreshold_ExecutesRaisesThresholdAndCostsMaxHpUnlessTamed()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var rail = Equip(game, WeaponForm.Railgun, 1);
            var victim = targets[0];

            LeaveHp(victim, game.runWeapons.HitDamage(rail) + victim.maxHp * .2f);
            float maxHp = game.progression.Stats.maxHp;

            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(victim.IsAlive, Is.False, "the low target is executed");
            Assert.That(game.progression.Stats.maxHp, Is.EqualTo(maxHp - tuning.executeMaxHpLoss).Within(.001f), "an execution costs max HP");
            Assert.That(game.runWeapons.ExecuteThreshold(rail), Is.EqualTo(tuning.executeThreshold + tuning.executeThresholdPerKill).Within(.0001f), "the kill raises the threshold");

            var tamed = Equip(game, WeaponForm.Railgun, 1, UpgradeAxis.Tame);
            var second = targets[1];
            LeaveHp(second, game.runWeapons.HitDamage(tamed) + second.maxHp * .2f);
            maxHp = game.progression.Stats.maxHp;
            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(second.IsAlive, Is.False, "the tamed gun still executes");
            Assert.That(game.progression.Stats.maxHp, Is.EqualTo(maxHp).Within(.001f), "Tame executions keep max HP");
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
        public async Task FuelTank_HitOnlyWhileSpraying_BurnsShooterAndNearbyEnemy()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var stream = Equip(game, WeaponForm.Flamethrower, 0);
            var near = targets[0];
            V3CombatSmokeTest.Teleport(near, game.player.Position + Vector3.forward * 1.5f);
            Physics.SyncTransforms();
            game.player.DebugSetAim(Vector3.back);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();

            game.player.ReceiveDamage(DamageInfo.Attack(1f, near));
            Assert.That(status.Fire, Is.Zero, "a hit between sprays leaves the tank intact");

            game.player.ClearWaveState();
            game.runWeapons.HandleInput(true, true, false);
            stream.heldTime = .1f;
            game.player.ReceiveDamage(DamageInfo.Attack(1f, near));

            Assert.That(status.Fire, Is.GreaterThan(0f), "the burst sets the shooter alight");
            Assert.That(near.Hp, Is.LessThan(near.maxHp), "the burst burns the enemy inside the ring");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterBlast_ClipEmptied_ReloadJamsUnlessTamed()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            float chance = tuning.scatterJamChance;
            try
            {
                tuning.scatterJamChance = 1f;
                var blast = Equip(game, WeaponForm.ScatterCannon, 0);
                blast.rounds = 1;
                game.runWeapons.HandleInput(true, true, false);
                Assert.That(blast.jammed, Is.True, "the reload jams");
                float jammedReload = blast.reload;

                var tamed = Equip(game, WeaponForm.ScatterCannon, 0, UpgradeAxis.Tame);
                tamed.rounds = 1;
                game.runWeapons.HandleInput(true, true, false);
                Assert.That(tamed.jammed, Is.False, "Tame never jams");
                Assert.That(tamed.reload, Is.GreaterThan(0f).And.LessThan(jammedReload), "a jam adds repair time to the reload");
            }
            finally { tuning.scatterJamChance = chance; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterFocus_FullChargeReleased_FiresWholeBurstAndKnocksShooterBack()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var focus = Equip(game, WeaponForm.ScatterCannon, 1);
            var target = targets[0];
            float burst = game.runWeapons.HitDamage(focus);

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(focus.charging, Is.True, "pressing starts the charge");
            focus.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);

            Assert.That(game.player.State, Is.EqualTo(CharacterState.Knocked), "the recoil is a real knockback");
            Assert.That(focus.burstLeft, Is.EqualTo(game.runWeapons.upgrades.focusPellets - 1), "the first bolt leaves at once");
            await Until(() => focus.burstLeft == 0 && !AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == focus), 3f, "the burst lands");
            Assert.That(target.Hp, Is.EqualTo(target.maxHp - burst).Within(.01f), "a full charge delivers the whole burst");

            game.player.CancelDisplacement();
            var tamed = Equip(game, WeaponForm.ScatterCannon, 1, UpgradeAxis.Tame);
            game.runWeapons.HandleInput(true, true, false);
            tamed.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal), "Tame recoil is a push, not a knockback");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnife_KillsTarget_ReturnsAtOnceAndCutsShooterUnlessTamed()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var knife = Equip(game, WeaponForm.Knife, 0);
            LeaveHp(targets[0], 1f);
            float hp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !knife.inFlight, 4f, "the knife comes back");

            Assert.That(targets[0].IsAlive, Is.False, "the knife kills the weak target");
            Assert.That(targets[1].Hp, Is.EqualTo(targets[1].maxHp), "a kill stops the bounce");
            Assert.That(game.player.Hp, Is.LessThan(hp), "the kill return cuts the shooter");

            game.player.ClearWaveState();
            var tamed = Equip(game, WeaponForm.Knife, 0, UpgradeAxis.Tame);
            LeaveHp(targets[1], 1f);
            hp = game.player.Hp;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !tamed.inFlight, 4f, "the tamed knife comes back");

            Assert.That(targets[1].IsAlive, Is.False, "the tamed knife still kills");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "Tame: the return spares the shooter");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Needler_EveryEighthNeedle_CostsOneHpUnlessTamed()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            game.player.DebugSetAim(Vector3.back);
            float hp = game.player.Hp;

            void FireNeedles(RunWeapon needler, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    needler.cooldown = needler.reload = 0f;
                    needler.rounds = 24;
                    game.runWeapons.HandleInput(true, true, false);
                }
            }

            var gun = Equip(game, WeaponForm.SporeLauncher, 1);
            FireNeedles(gun, tuning.needlerHpPerNeedles - 1);
            Assert.That(game.player.Hp, Is.EqualTo(hp), "no cost before the eighth needle");
            FireNeedles(gun, 1);
            Assert.That(game.player.Hp, Is.EqualTo(hp - tuning.needlerHpCost).Within(.001f), "the eighth needle costs 1 HP");

            hp = game.player.Hp;
            FireNeedles(Equip(game, WeaponForm.SporeLauncher, 1, UpgradeAxis.Tame), tuning.needlerHpPerNeedles);
            Assert.That(game.player.Hp, Is.EqualTo(hp), "Tame needles are free");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReverseShot_Tamed_TurnsAroundAndHitsTargetAhead()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.Rifle, 1, UpgradeAxis.Tame);

            game.runWeapons.HandleInput(true, true, false);

            await Until(() => targets[0].Hp < targets[0].maxHp, 3f, "the reverse round turns around and hits the target ahead");
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
