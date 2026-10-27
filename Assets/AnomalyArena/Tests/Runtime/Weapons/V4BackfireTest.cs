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
        public async Task ShatterShard_AimedAtALane_FliesAndHitsTheFirstEnemy()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.CryoCannon, 0);
            game.player.DebugSetAim(Vector3.forward);
            float hp = targets[0].Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => targets[0].Hp < hp, 1f, "the middle shard reaches the first enemy in the lane");
            await Until(() => game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.IcePatch && Query.Flat(h.position - targets[0].Position).magnitude < .5f), 1f, "an ice patch where it hit");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ShatterFan_AimedAwayFromEnemies_LeavesThreeIcePatches()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.CryoCannon, 0);
            game.player.DebugSetAim(Vector3.back);

            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(AttackPool.Instance.actors.Count(a => a && a.gameObject.activeSelf && a.mode == AttackMode.Shard), Is.EqualTo(3), "three shards are in flight");
            await Until(() => game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.IcePatch) == 3, 2f, "the three shards land");

            var tamed = Equip(game, WeaponForm.CryoCannon, 0, UpgradeAxis.Tame);
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.mode == AttackMode.Shard), 2f, "the tamed shards land");
            Assert.That(game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.IcePatch && h.weapon == tamed), Is.EqualTo(3), "Tame shards leave their patches too");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Execute_ShotThatKillsNothing_ShocksYouAndForcesTheLongCooldown_AnExecutionDoesNeither()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var player = game.player;
            ElementStatus.For(player).SetShield(0f);
            var rail = Equip(game, WeaponForm.Railgun, 1);
            float cycle = weapons.CycleSeconds(rail);

            player.DebugSetAim(Vector3.back);
            float hp = player.Hp;
            weapons.HandleInput(true, true, false);
            Assert.That(player.Hp, Is.EqualTo(hp - tuning.executeSelfShock).Within(.01f), "a miss shocks you for the flat value");
            Assert.That(rail.cooldown, Is.EqualTo(cycle * tuning.executeMissCooldown).Within(.05f), "and forces the long cooldown");
            Assert.That(weapons.GetCooldownDuration(rail), Is.EqualTo(rail.cooldown).Within(.05f), "the slot bar spans the long cooldown");

            player.DebugSetAim(Vector3.forward);
            rail.cooldown = 0f; rail.holeReturnSpan = 0f;
            hp = player.Hp;
            weapons.HandleInput(true, true, false);
            Assume.That(targets[0].IsAlive, Is.True, "one shot leaves it over the line");
            Assert.That(player.Hp, Is.EqualTo(hp - tuning.executeSelfShock).Within(.01f), "a hit that kills nothing shocks you too");
            Assert.That(rail.cooldown, Is.EqualTo(cycle * tuning.executeMissCooldown).Within(.05f));

            var victim = targets[0];
            LeaveHp(victim, weapons.HitDamage(rail) + victim.maxHp * .2f);
            rail.cooldown = 0f; rail.holeReturnSpan = 0f;
            hp = player.Hp;
            float maxHp = game.progression.Stats.maxHp;
            weapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(victim.IsAlive, Is.False, "the low target is executed");
            Assert.That(player.Hp, Is.EqualTo(hp), "an execution does not shock you");
            Assert.That(game.progression.Stats.maxHp, Is.EqualTo(maxHp).Within(.001f), "nor change your max HP any more");
            Assert.That(weapons.ExecuteThreshold(rail), Is.EqualTo(tuning.executeThreshold).Within(.0001f), "the line stays where it was");
            Assert.That(rail.cooldown, Is.LessThan(cycle), "and gives part of the cooldown back");
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
        public async Task ScatterBlast_ClipEmptied_ShellsLandBesideYouFlareUpAndBurnWhoeverStepsOnThem()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;

            var blast = Equip(game, WeaponForm.ScatterCannon, 0, UpgradeAxis.Reach);
            blast.element = ElementKind.Frost;                                                                      
            var cycle = game.runWeapons.CycleSeconds(blast) / game.runWeapons.roster.Get(WeaponForm.ScatterCannon).cooldownA;
            float tick = game.runWeapons.HitDamage(blast) * tuning.scatterShellShare;
            Vector3 origin = player.Position, right = Vector3.Cross(Vector3.up, Vector3.forward);
            blast.rounds = 1;

            game.runWeapons.HandleInput(true, true, false);

            Assert.That(blast.reload, Is.EqualTo(tuning.scatterReload * cycle).Within(.01f), "no repair time is ever added");
            var shells = game.runWeapons.Hazards.Where(h => h.kind == GroundHazardKind.ScatterShell).ToArray();
            Assert.That(shells.Length, Is.EqualTo(2), "two shells land");
            foreach (var side in new[] { -1f, 1f })
                Assert.That(shells.Any(h => Query.Flat(h.position - (origin + right * side * tuning.scatterShellSide)).magnitude < .05f), Is.True,
                    $"one {tuning.scatterShellSide} cells to the {(side < 0 ? "left" : "right")} of the aim");
            Assert.That(AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == blast), Is.False, "nothing burns before the fuse");

            var enemy = targets[0];
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            V3CombatSmokeTest.Teleport(enemy, origin + right * tuning.scatterShellSide);
            Physics.SyncTransforms();
            ElementStatus.For(enemy).ClearStatuses();
            float hp = player.Hp, enemyHp = enemy.Hp, fired = Time.time;
            await Until(() => AttackPool.Instance.actors.Count(a => a && a.gameObject.activeSelf && a.weapon == blast && a.mode == AttackMode.Field) == 2, 1f, "both shells flare up");
            Assert.That(Time.time - fired, Is.EqualTo(tuning.scatterShellFuse).Within(.1f), "after the short fuse");
            Assert.That(game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.ScatterShell), Is.False, "the shells became the fires");
            var fire = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == blast && a.mode == AttackMode.Field);
            Assert.That(fire.radius, Is.EqualTo(tuning.scatterShellRadius).Within(.001f), "the Reach card does not grow the shell fires");
            await Until(() => enemyHp - enemy.Hp >= tick * 3f - .01f, 1.5f, "the fire keeps burning the enemy standing on it");
            var frost = ElementStatus.For(enemy);
            Assert.That(frost.Frost > 0f || frost.Frozen, Is.True, "with the shotgun's element");
            Assert.That(player.Hp, Is.EqualTo(hp), "you stood between the shells, off both fires");

            V3CombatSmokeTest.Teleport(player, origin - right * tuning.scatterShellSide);
            Physics.SyncTransforms();
            float stepped = Time.time;
            while (Time.time < stepped + .75f) await Awaitable.NextFrameAsync();
            Assert.That(hp - player.Hp, Is.GreaterThanOrEqualTo(1f).And.LessThanOrEqualTo(Mathf.Ceil(tuning.scatterShellSelfDamage * 4f)), "a flat amount, not the scaled hit");
            await Until(() => !AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == blast), 2f, "the fires burn out");
            Assert.That(Time.time - fired, Is.EqualTo(tuning.scatterShellFuse + tuning.scatterShellBurn).Within(.3f), "after their burn time");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterFocus_FullChargeReleased_FiresWholeBurstAndPushesShooterBack()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var focus = Equip(game, WeaponForm.ScatterCannon, 1);
            var target = targets[0];
            float burst = game.runWeapons.HitDamage(focus);

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(focus.charging, Is.True, "pressing starts the charge");
            focus.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);

            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal), "the recoil is a push, not a knockback");
            Assert.That(focus.burstLeft, Is.EqualTo(game.runWeapons.upgrades.focusPellets - 1), "the first bolt leaves at once");
            await Until(() => focus.burstLeft == 0 && !AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == focus), 3f, "the burst lands");
            Assert.That(target.Hp, Is.EqualTo(target.maxHp - burst).Within(.01f), "a full charge delivers the whole burst");

            game.player.CancelDisplacement();
            var tamed = Equip(game, WeaponForm.ScatterCannon, 1, UpgradeAxis.Tame);
            game.runWeapons.HandleInput(true, true, false);
            tamed.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal), "the tamed recoil is a push too");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnife_KillsTarget_BouncesOnAndTheReturnCutsShooterUnlessTamed()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var knife = Equip(game, WeaponForm.Knife, 0);
            LeaveHp(targets[0], 1f);
            float hp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            float cut = AttackPool.Instance.actors.Single(a => a && a.gameObject.activeSelf && a.weapon == knife).ownerDamage
                * game.runWeapons.StartingFormDamageFactor(WeaponForm.Knife);                                                                                       
            await Until(() => !knife.inFlight, 4f, "the knife comes back");

            Assert.That(targets[0].IsAlive, Is.False, "the knife kills the weak target");
            Assert.That(targets[1].Hp, Is.LessThan(targets[1].maxHp), "a kill no longer stops the bounce");
            Assert.That(hp - game.player.Hp, Is.EqualTo(cut).Within(.01f), "the return cuts the shooter once, for the base value");

            game.player.ClearWaveState();
            var tamed = Equip(game, WeaponForm.Knife, 0, UpgradeAxis.Tame);
            hp = game.player.Hp;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !tamed.inFlight, 4f, "the tamed knife comes back");

            Assert.That(game.player.Hp, Is.EqualTo(hp), "Tame: the return spares the shooter");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Needler_ClipEmptied_ReloadMistAtYourFeetHurtsYouFlatAndPoisonsEnemies()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            player.DebugSetAim(Vector3.back);
            ElementStatus.For(player).SetShield(0f);

            var beside = targets[0];
            V3CombatSmokeTest.Teleport(beside, player.Position + Vector3.right * (player.radius + beside.radius + .05f));
            Physics.SyncTransforms();
            var gun = Equip(game, WeaponForm.SporeLauncher, 1);
            gun.element = ElementKind.Poison; gun.builtInElement = true;                          
            var status = ElementStatus.For(player);
            status.ClearStatuses();
            float hp = player.Hp;

            gun.rounds = 3;
            for (int i = 0; i < 2; i++) { gun.cooldown = 0f; game.runWeapons.HandleInput(true, true, false); }
            Assert.That(player.Hp, Is.EqualTo(hp), "needles before the clip runs out cost nothing");
            Assert.That(AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == gun && a.mode == AttackMode.Field), Is.False, "and leave no mist");

            gun.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(gun.reload, Is.GreaterThan(0f), "the last needle starts the reload");
            var mist = AttackPool.Instance.actors.Single(a => a && a.gameObject.activeSelf && a.weapon == gun && a.mode == AttackMode.Field);
            Assert.That(Query.Flat(mist.point - player.Position).magnitude, Is.LessThan(.01f), "the mist bursts where you stand");
            Assert.That(mist.radius, Is.EqualTo(tuning.needlerMistRadius).Within(.001f));

            float until = Time.time + 1.2f;
            while (Time.time < until) await Awaitable.NextFrameAsync();
            float lost = hp - player.Hp;
            Assert.That(lost, Is.GreaterThanOrEqualTo(tuning.needlerMistDamage * 2f - .01f).And.LessThanOrEqualTo(tuning.needlerMistDamage * 4f + .01f), "you lose the flat value each tick you stay in it");
            Assert.That(Mathf.Abs(lost / tuning.needlerMistDamage - Mathf.Round(lost / tuning.needlerMistDamage)), Is.LessThan(.01f), "never scaled by Mk or cards");
            Assert.That(status.Poison, Is.Zero, "it never poisons you");
            Assert.That(beside.Hp, Is.LessThan(beside.maxHp), "an enemy in it takes damage");
            Assert.That(ElementStatus.For(beside).Poison, Is.GreaterThan(0f), "and is poisoned");

            await Until(() => !mist.gameObject.activeSelf, tuning.needlerMistSeconds + 1f, "the mist fades");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReverseShot_Tamed_StillFiresFromYourBackAtTheTargetBehind()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();

            game.player.DebugSetAim(Vector3.back);
            var rifle = Equip(game, WeaponForm.Rifle, 1, UpgradeAxis.Tame);

            game.runWeapons.HandleInput(true, true, false);
            var round = AttackPool.Instance.actors.Single(a => a && a.gameObject.activeSelf && a.weapon == rifle);
            Assert.That(Vector3.Dot(round.direction, Vector3.forward), Is.GreaterThan(.99f), "the tamed round still leaves from your back");

            await Until(() => targets[0].Hp < targets[0].maxHp, 3f, "it hits the target behind you");
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
