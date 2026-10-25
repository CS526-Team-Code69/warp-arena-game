using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class RailMinigunSwarmCardsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant, UpgradeAxis axes = UpgradeAxis.None, int mk = 1)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, axes = axes, mk = mk, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        static void Place(Combatant body, Vector3 at)
        {
            V3CombatSmokeTest.Teleport(body, at);
            Physics.SyncTransforms();
        }

        static void ClearLane(GameManager game, params Enemy[] bodies)
        {
            for (int i = 0; i < bodies.Length; i++) Place(bodies[i], game.player.Position + Vector3.forward * (60f + 3f * i));
        }

        static void LeaveHp(Combatant body, float hp) =>
            body.ReceiveDamage(new DamageInfo { amount = body.Hp - hp, source = DamageSource.Poison, ignoreShield = true });

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
        }

        static AttackActor[] Actors(RunWeapon w, AttackMode mode) =>
            AttackPool.Instance.actors.Where(a => a && a.gameObject.activeSelf && a.weapon == w && a.mode == mode).ToArray();

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LanceTwist_SlugCrossesThePreviousTrail_BurstsOnceAtTheCrossingAndAParallelShotDoesNot()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;

            var rail = Equip(game, WeaponForm.Railgun, 0, UpgradeAxis.Twist | UpgradeAxis.Tame);
            Vector3 origin = game.player.Position;
            ClearLane(game, targets[1], targets[2]);

            var near = targets[0];
            var capsule = near.GetComponent<CapsuleCollider>();
            float body = Mathf.Max(near.radius, capsule ? capsule.radius : 0f);
            float side = Mathf.Min(1.15f, body + .16f + .25f);
            Assume.That(side - body - .16f, Is.GreaterThan(.1f), "room beside the slug inside the clear lane");
            Place(near, origin + Vector3.forward * 6f + Vector3.right * side);

            rail.lanceTrailFrom = origin + Vector3.forward * 6f + Vector3.left * 5f;
            rail.lanceTrailTo = origin + Vector3.forward * 6f + Vector3.right * 5f;
            rail.lanceTrailLeft = tuning.lanceTrailSeconds;
            float playerHp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => near.Hp < near.maxHp, 3f, "the slug crosses the trail and bursts");

            Assert.That(near.Hp, Is.EqualTo(near.maxHp - game.runWeapons.HitDamage(rail) * tuning.lanceCrossShare).Within(.01f), "60% of the base hit");
            await Until(() => Actors(rail, AttackMode.Bolt).Length == 0, 2f, "the slug stops");
            await Seconds(.3f);
            Assert.That(near.Hp, Is.EqualTo(near.maxHp - game.runWeapons.HitDamage(rail) * tuning.lanceCrossShare).Within(.01f), "one burst per slug");
            Assert.That(game.player.Hp, Is.EqualTo(playerHp), "the burst spares the shooter");
            Assert.That(rail.lanceTrailLeft, Is.GreaterThan(tuning.lanceTrailSeconds - 1f), "the slug left its own trail");
            Assert.That(Vector3.Angle(Query.Flat(rail.lanceTrailTo - rail.lanceTrailFrom), Vector3.forward), Is.LessThan(1f), "along its real path");
            Assert.That(game.runWeapons.Tracers.Any(t => Query.Flat(t.to - rail.lanceTrailTo).magnitude < .01f && t.from.y < .2f), Is.True, "the trail is drawn on the ground");

            float hp = near.Hp;
            rail.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !rail.charging && Actors(rail, AttackMode.Bolt).Length > 0, 2f, "the second slug leaves");
            await Until(() => Actors(rail, AttackMode.Bolt).Length == 0, 2f, "the second slug stops");
            await Seconds(.3f);
            Assert.That(near.Hp, Is.EqualTo(hp), "a parallel shot does not cross the trail");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LanceTame_WallBehind_BracesTheShot_OpenGroundPushesWithoutKnockback()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var rail = Equip(game, WeaponForm.Railgun, 0, UpgradeAxis.Tame);
            Vector3 origin = game.player.Position;

            WorldMap.Active.CreateWall(origin + Vector3.back * 3.5f, 4f, 60f, 0f, game.player);
            Physics.SyncTransforms();
            var first = targets[0];

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => first.Hp < first.maxHp, 3f, "the braced slug hits");
            Assert.That(first.maxHp - first.Hp, Is.EqualTo(game.runWeapons.HitDamage(rail) * tuning.lanceBraceDamage).Within(.01f), "a braced shot hits 40% harder");
            await Seconds(.2f);
            Assert.That(Query.Flat(game.player.Position - origin).magnitude, Is.LessThan(.05f), "a braced shot has no recoil");
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal));

            foreach (var wall in WorldMap.Active.temporaryWalls) if (wall && wall.gameObject.activeSelf) wall.gameObject.SetActive(false);
            Place(game.player, origin + Vector3.forward * 8f);
            ClearLane(game, targets[1], targets[2]);
            first.ResetForSpawn();
            Place(first, origin + Vector3.forward * 11f);
            await Awaitable.FixedUpdateAsync();
            rail.cooldown = 0f;
            bool knocked = false;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => { knocked |= game.player.State == CharacterState.Knocked; return first.Hp < first.maxHp; }, 3f, "the open-ground slug hits");
            Assert.That(first.maxHp - first.Hp, Is.EqualTo(game.runWeapons.HitDamage(rail)).Within(.01f), "no bonus without a wall");
            Assert.That(knocked, Is.False, "the recoil is a push, never a knockback");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ExecuteTwist_HitEnemy_IsMarkedAndExecutedByAnyLaterDamageUnderTheLine()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var rail = Equip(game, WeaponForm.Railgun, 1, UpgradeAxis.Twist);
            var marked = targets[0];

            weapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();
            Assert.That(marked.IsAlive, Is.True, "one shot leaves it well above the line");
            Assert.That(weapons.IsExecuteMarked(marked), Is.True, "the survivor is marked");

            float maxHp = game.progression.Stats.maxHp;
            LeaveHp(marked, marked.maxHp * .3f);
            Assert.That(marked.IsAlive, Is.False, "a poison tick that drops a marked enemy under 40% executes it");
            Assert.That(weapons.ExecuteThreshold(rail), Is.EqualTo(tuning.executeThreshold + tuning.executeThresholdPerKill).Within(.0001f), "it counts as this gun's kill");
            Assert.That(game.progression.Stats.maxHp, Is.EqualTo(maxHp - tuning.executeMaxHpLoss).Within(.001f), "and costs max HP like any execution");

            LeaveHp(targets[1], targets[1].maxHp * .3f);
            Assert.That(targets[1].IsAlive, Is.True, "an unmarked enemy is not executed by other damage");

            int max = tuning.executeMarkMax;
            try
            {
                tuning.executeMarkMax = 1;
                targets[1].ResetForSpawn();
                rail.cooldown = 0f;
                weapons.HandleInput(true, true, false);
                Assert.That(weapons.IsExecuteMarked(targets[1]), Is.True);

                Place(targets[1], game.player.Position + Vector3.forward * 60f);
                rail.cooldown = 0f;
                weapons.HandleInput(true, true, false);
                Assert.That(weapons.IsExecuteMarked(targets[2]), Is.True);
                Assert.That(weapons.IsExecuteMarked(targets[1]), Is.False, "a new mark over the cap replaces the oldest");
                LeaveHp(targets[1], targets[1].maxHp * .3f);
                Assert.That(targets[1].IsAlive, Is.True, "the replaced mark no longer executes");
            }
            finally { tuning.executeMarkMax = max; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ExecuteTame_Executions_AddMaxHpAndHealUpToTheCap()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var rail = Equip(game, WeaponForm.Railgun, 1, UpgradeAxis.Tame);

            rail.executeHpGained = tuning.executeTameHpCap - .5f;
            LeaveHp(game.player, game.player.maxHp - 10f);
            float maxHp = game.progression.Stats.maxHp, hp = game.player.Hp;
            LeaveHp(targets[0], weapons.HitDamage(rail) + targets[0].maxHp * .2f);

            weapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();

            Assert.That(targets[0].IsAlive, Is.False, "executed");
            Assert.That(game.progression.Stats.maxHp, Is.EqualTo(maxHp + .5f).Within(.001f), "max HP rises, but only up to the cap");
            Assert.That(game.player.Hp, Is.GreaterThanOrEqualTo(hp + .5f - .01f), "the gain is healed");
            Assert.That(rail.executeHpGained, Is.EqualTo(tuning.executeTameHpCap).Within(.001f));

            maxHp = game.progression.Stats.maxHp;
            LeaveHp(targets[1], weapons.HitDamage(rail) + targets[1].maxHp * .2f);
            rail.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();
            Assert.That(targets[1].IsAlive, Is.False);
            Assert.That(game.progression.Stats.maxHp, Is.EqualTo(maxHp).Within(.001f), "nothing more past the cap, and no loss");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpinUpTwist_SwitchAtFullSpinWithFireHeld_LeavesTheMinigunFiringInPlace()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var gun = Equip(game, WeaponForm.Minigun, 0, UpgradeAxis.Twist);
            int gunSlot = weapons.ActiveIndex;
            weapons.Hand[1 - gunSlot] = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
            Vector3 origin = game.player.Position;
            var target = targets[0];

            target.maxHp = 1000f; target.Heal(target.maxHp);

            gun.cooldown = 99f;

            weapons.HandleInput(false, true, false);
            gun.heldTime = 1f;
            weapons.Switch();
            Assert.That(gun.turretLeft, Is.Zero, "below full spin nothing stays behind");
            weapons.Switch();

            weapons.HandleInput(false, true, false);
            gun.heldTime = tuning.turretSpinSeconds;
            float hp = target.Hp;
            weapons.Switch();
            Assert.That(weapons.ActiveIndex, Is.Not.EqualTo(gunSlot), "the other weapon is in hand");
            Assert.That(gun.turretLeft, Is.EqualTo(tuning.turretSeconds).Within(.001f));
            Assert.That(Query.Flat(gun.turretPoint - origin).magnitude, Is.LessThan(.01f), "it stays where you stood");

            game.player.DebugSetAim(Vector3.back);
            await Until(() => gun.turretLeft <= 0f, 5f, "the deployed minigun runs out");
            await Seconds(.3f);                         

            float hit = weapons.HitDamage(gun);
            int shots = Mathf.RoundToInt(tuning.turretSeconds * tuning.spinUpTopRate);
            Assert.That(hp - target.Hp, Is.InRange((shots - 2) * hit - .01f, (shots + 1) * hit + .01f), "about 3 s at the top rate, along the old aim");
            Assert.That(gun.heldTime, Is.Zero, "back in its slot with no spin");

            game.player.DebugSetAim(Vector3.forward);
            weapons.Switch();
            weapons.HandleInput(false, true, false);
            gun.heldTime = tuning.turretSpinSeconds;
            weapons.Switch();
            Assert.That(gun.turretLeft, Is.GreaterThan(0f));
            weapons.Switch();
            await Awaitable.NextFrameAsync();
            Assert.That(gun.turretLeft, Is.Zero, "switching back recalls it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpinUpTame_FiringAtFullSpin_FreesTurningKeepsTheSlowHalvesKnockbackAndIgnoresHolePull()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var gun = Equip(game, WeaponForm.Minigun, 0, UpgradeAxis.Tame);
            ClearLane(game, targets);
            var body = game.player.GetComponent<Rigidbody>();
            Vector3 origin = game.player.Position;

            weapons.HandleInput(false, true, false);
            gun.heldTime = tuning.spinAnchorSpinSeconds;
            Assert.That(weapons.Anchored, Is.True);
            Assert.That(weapons.AimTurnRateLimit, Is.Zero, "turning is free");
            Assert.That(weapons.MoveMultiplier, Is.EqualTo(tuning.spinUpMoveFloor).Within(.01f), "the spin still slows your feet");

            game.player.Knockback(Vector3.forward, 4f, KnockKind.Hit);
            await Awaitable.FixedUpdateAsync(); await Awaitable.FixedUpdateAsync();
            float anchored = Query.Flat(body.linearVelocity).magnitude;
            game.player.CancelDisplacement(); Place(game.player, origin);

            weapons.HandleInput(false, false, true);
            Assert.That(weapons.Anchored, Is.False, "letting go ends it at once");
            game.player.Knockback(Vector3.forward, 4f, KnockKind.Hit);
            await Awaitable.FixedUpdateAsync(); await Awaitable.FixedUpdateAsync();
            float free = Query.Flat(body.linearVelocity).magnitude;
            game.player.CancelDisplacement(); Place(game.player, origin);
            Assume.That(free, Is.GreaterThan(1f), "a knockback moves the disabled test body");
            Assert.That(anchored / free, Is.EqualTo(tuning.spinAnchorKnockFactor).Within(.05f), "knockback is halved at full spin");

            WorldMap.Active.CreateVoidHole(origin + Vector3.forward * 4f, 1f, 5f, 3f, null, false);
            weapons.HandleInput(false, true, false);
            gun.heldTime = tuning.spinAnchorSpinSeconds;
            for (int i = 0; i < 5; i++) await Awaitable.FixedUpdateAsync();
            Assert.That(Query.Flat(game.player.Position - origin).magnitude, Is.LessThan(.02f), "anchored, the hole does not drag you");

            weapons.HandleInput(false, false, true);
            for (int i = 0; i < 3; i++) await Awaitable.FixedUpdateAsync();
            float dragged = Vector3.Dot(Query.Flat(game.player.Position - origin), Vector3.forward);
            game.player.CancelDisplacement(); Place(game.player, origin);
            Assert.That(dragged, Is.GreaterThan(.02f), "let go and the pull takes you again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MeltdownTwist_BulletKills_VentHeatAndAnEliteKillClearsIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;

            var plain = Equip(game, WeaponForm.Minigun, 1);
            plain.heat = 2f;
            LeaveHp(targets[0], 1f);
            weapons.HandleInput(true, true, false);
            await Until(() => !targets[0].IsAlive, 2f, "the bullet kills the first target");
            Assert.That(plain.heat, Is.EqualTo(2.1f).Within(.001f), "one shot of heat, no vent");

            var gun = Equip(game, WeaponForm.Minigun, 1, UpgradeAxis.Twist);
            gun.heat = 2f;
            LeaveHp(targets[1], 1f);
            weapons.HandleInput(true, true, false);
            await Until(() => !targets[1].IsAlive, 2f, "the bullet kills the second target");
            Assert.That(gun.heat, Is.EqualTo(2.1f - tuning.meltdownKillVent).Within(.001f), "a kill vents 5% of full heat");

            var elite = targets[2];
            elite.Configure(elite.definition, 1f, true);
            elite.ResetForSpawn();
            elite.enabled = false;
            Place(elite, game.player.Position + Vector3.forward * 9f);
            Assume.That(elite.Elite, Is.True);
            LeaveHp(elite, 1f);
            gun.heat = 3f; gun.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            await Until(() => !elite.IsAlive, 2f, "the bullet kills the elite");
            Assert.That(gun.heat, Is.Zero, "an elite kill clears the heat");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MeltdownTame_Overheat_SpeedsUpEveryWeaponInsteadOfHurtingYouAndStillLocksOut()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var gun = Equip(game, WeaponForm.Minigun, 1, UpgradeAxis.Tame);
            var other = new RunWeapon { form = WeaponForm.Railgun, variant = 1, revealed = true, rounds = weapons.ClipSize(WeaponForm.Railgun, 1) };
            weapons.Hand[1 - weapons.ActiveIndex] = other;
            var status = ElementStatus.For(game.player);
            status.ClearStatuses(); status.SetShield(0f);
            float hp = game.player.Hp, cycle = weapons.CycleSeconds(other);
            gun.heat = 3.95f;

            weapons.HandleInput(true, true, false);

            Assert.That(game.player.Hp, Is.EqualTo(hp), "the meltdown spares you");
            Assert.That(status.Shield, Is.Zero, "no Shield any more");
            Assert.That(weapons.CycleSeconds(other), Is.EqualTo(cycle / (1f + tuning.meltdownTameHaste)).Within(.001f), "the other weapon attacks faster too");
            Assert.That(weapons.MeltdownHasteLeft, Is.EqualTo(tuning.meltdownTameHasteSeconds).Within(.1f), "for the tuned time");
            Assert.That(targets[0].Hp, Is.LessThan(targets[0].maxHp), "the blast still hits enemies");
            Assert.That(gun.reload, Is.EqualTo(3f).Within(.001f), "the lockout remains");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BarrageTwist_RocketsOnOneEnemy_MergeIntoOneBiggerBlastAfterTheFuse()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var swarm = Equip(game, WeaponForm.SwarmLauncher, 0, UpgradeAxis.Twist);
            var host = targets[0];
            var bystander = targets[1];

            float merged = Mathf.Min(1.5f + 2f * tuning.swarmHivePerRocketRadius, tuning.swarmHiveMaxRadius);
            Place(bystander, host.Position + Vector3.forward * (merged + bystander.radius - .15f));
            ClearLane(game, targets[2]);
            for (int i = 0; i < 3; i++)
            {
                var rocket = AttackPool.Instance.Acquire();
                Assume.That(rocket, Is.Not.Null);
                rocket.Configure(AttackMode.Bolt, game.player, swarm, game.player.Position + Vector3.forward * .7f, Vector3.forward, 4f, 0f, 9f, 15f, 0f, 9f / 15f + 3f);
                rocket.mode = AttackMode.Swarm; rocket.radius = 1.5f; rocket.hurtOwner = false; rocket.ownerDamage = 4f;
            }

            await Until(() => Actors(swarm, AttackMode.Sticky).Length == 3, 1f, "all three rockets stick to the first enemy");
            Assert.That(host.Hp, Is.EqualTo(host.maxHp), "nothing bursts on contact");
            await Until(() => Actors(swarm, AttackMode.Sticky).Length == 0, 2f, "the fuse runs out");

            Assert.That(host.Hp, Is.EqualTo(host.maxHp - 12f).Within(.01f), "one blast with the three rockets' damage");
            Assert.That(bystander.Hp, Is.EqualTo(bystander.maxHp - 12f).Within(.01f), "the merged blast reaches further than one rocket");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BarrageTame_RocketsFlyingBackward_TurnToTheNearestEnemyBehindAndSpareYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            float exponent = tuning.barrageAngleExponent;
            try
            {

                tuning.barrageAngleExponent = .01f;

                game.player.DebugSetAim(Quaternion.Euler(0f, 135f, 0f) * Vector3.forward);
                Vector3 toTarget = Query.Flat(targets[0].Position - game.player.Position).normalized;

                var plain = Equip(game, WeaponForm.SwarmLauncher, 0);
                game.runWeapons.HandleInput(true, true, false);
                var wild = Actors(plain, AttackMode.Swarm);
                Assert.That(wild, Is.Not.Empty);
                Assert.That(wild.All(a => Vector3.Angle(a.direction, toTarget) > 20f), Is.True, "untamed, backward rockets keep their random angle");

                var swarm = Equip(game, WeaponForm.SwarmLauncher, 0, UpgradeAxis.Tame);
                game.runWeapons.HandleInput(true, true, false);
                var rockets = Actors(swarm, AttackMode.Swarm);
                Assert.That(rockets, Has.Length.EqualTo(8));
                Assert.That(rockets.All(a => Vector3.Angle(a.direction, toTarget) < 2f), Is.True, "each backward rocket heads for the nearest enemy behind you");
                Assert.That(rockets.All(a => !a.hurtOwner), Is.True, "tamed rockets never hurt you");
            }
            finally { tuning.barrageAngleExponent = exponent; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BombardTwist_AimMovedAfterFiring_EachBlastLandsAlongTheAimOfItsMoment()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var bombard = Equip(game, WeaponForm.SwarmLauncher, 1, UpgradeAxis.Twist);
            float spacing = (game.runWeapons.roster.Get(WeaponForm.SwarmLauncher).rangeB - tuning.bombardStart) / (tuning.bombardBlasts - 1);

            game.runWeapons.HandleInput(true, true, false);
            Vector3 origin = bombard.bombardOrigin;
            var firstBlast = Actors(bombard, AttackMode.Blast);
            Assert.That(firstBlast, Has.Length.EqualTo(1), "one blast at a time");
            Assert.That(Query.Flat(firstBlast[0].point - (origin + Vector3.forward * tuning.bombardStart)).magnitude, Is.LessThan(.01f));
            Assert.That(firstBlast[0].delay, Is.EqualTo(tuning.bombardFuse).Within(.001f), "with its fuse and marker");

            Vector3 swept = Quaternion.Euler(0f, 10f, 0f) * Vector3.forward;
            game.player.DebugSetAim(swept);
            await Until(() => bombard.bombardLeft <= tuning.bombardBlasts - 2, 1f, "the second blast is placed");
            var second = AttackPool.Instance.actors.Where(a => a && a.gameObject.activeSelf && a.weapon == bombard && a.mode == AttackMode.Blast && a != firstBlast[0])
                .OrderBy(a => a.Sequence).First();
            Vector3 offset = Query.Flat(second.point - origin);
            Assert.That(Vector3.Angle(offset, swept), Is.LessThan(.5f), "along the aim of that moment");
            Assert.That(offset.magnitude, Is.EqualTo(tuning.bombardStart + spacing).Within(.01f), "at the second distance");
            await Until(() => bombard.bombardLeft == 0 && Actors(bombard, AttackMode.Blast).Length == 0, 3f, "the run of blasts ends");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BombardTame_WalkingIntoYourOwnBlasts_SparesYouAndCutsTheCooldownOncePerShot()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var bombard = Equip(game, WeaponForm.SwarmLauncher, 1, UpgradeAxis.Tame);
            ClearLane(game, targets);
            Vector3 origin = game.player.Position;
            float hp = game.player.Hp, cycle = weapons.CycleSeconds(bombard);

            weapons.HandleInput(true, true, false);
            float fired = Time.time;
            Assert.That(bombard.cooldown, Is.EqualTo(cycle).Within(.001f));
            Vector3 firstSpot = origin + Vector3.forward * tuning.bombardStart;
            Place(game.player, firstSpot);
            await Until(() => Actors(bombard, AttackMode.Blast).Length == tuning.bombardBlasts - 1, 2f, "the first blast goes off on you");
            float firstAt = Time.time, afterFirst = bombard.cooldown;
            Assert.That(afterFirst, Is.EqualTo(cycle - (firstAt - fired) - tuning.bombardTameCooldownCut * cycle).Within(.1f), "the first touch cuts a quarter of the cooldown");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "no damage");
            Assert.That(Query.Flat(game.player.Position - firstSpot).magnitude, Is.LessThan(.05f), "no push");
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal));

            float spacing = (weapons.roster.Get(WeaponForm.SwarmLauncher).rangeB - tuning.bombardStart) / (tuning.bombardBlasts - 1);
            Place(game.player, origin + Vector3.forward * (tuning.bombardStart + spacing));
            await Until(() => Actors(bombard, AttackMode.Blast).Length == tuning.bombardBlasts - 2, 2f, "the second blast goes off on you");
            Assert.That(bombard.cooldown, Is.EqualTo(afterFirst - (Time.time - firstAt)).Within(.1f), "a second touch in the same shot cuts nothing");
            Assert.That(game.player.Hp, Is.EqualTo(hp));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task CardTexts_RailMinigunSwarm_DescribeTheNewForms()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            foreach (var form in new[] { WeaponForm.Railgun, WeaponForm.Minigun, WeaponForm.SwarmLauncher })
                for (int variant = 0; variant < 2; variant++)
                foreach (var axis in new[] { UpgradeAxis.Twist, UpgradeAxis.Tame })
                    Assert.That(ArenaTerms.ExpandedUpgradeDescription(form, variant, axis), Is.Not.Empty, $"{form} {variant} {axis}");
            string lance = ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Railgun, 0, UpgradeAxis.Twist);
            Assert.That(lance, Does.Contain("2.5").And.Contain("60%"), "Lance Twist names its blast");
            string spin = ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Minigun, 0, UpgradeAxis.Twist);
            Assert.That(spin, Does.Contain("3").And.Not.Contain("12"), "Spin-up Twist: 3 s in place, no stale 12 shots/s");
        }
    }
}
