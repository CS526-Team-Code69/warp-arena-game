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

            float maxHp = game.progression.Stats.maxHp, cooldown = rail.cooldown;
            LeaveHp(marked, marked.maxHp * .3f);
            Assert.That(marked.IsAlive, Is.False, "a poison tick that drops a marked enemy under 40% executes it");

            Assert.That(weapons.ExecuteThreshold(rail), Is.EqualTo(tuning.executeThreshold).Within(.0001f), "the line stays where it was");
            Assert.That(game.progression.Stats.maxHp, Is.EqualTo(maxHp).Within(.001f), "and max HP does not change");
            Assert.That(rail.executeNextShot, Is.Zero, "it arms no next shot");
            Assert.That(rail.cooldown, Is.EqualTo(cooldown).Within(.05f), "and gives no cooldown back");

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
        public async Task ExecuteTame_ShotThatKillsNothing_SparesYouAndCirclesYouWithAnArcShield()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var player = game.player;
            var rail = Equip(game, WeaponForm.Railgun, 1, UpgradeAxis.Tame);
            float shot = weapons.HitDamage(rail), zap = shot * tuning.executeAuraShare;

            var near = targets[1];
            Vector3 beside = player.Position + Vector3.forward * 1.2f + Vector3.right * (near.radius + .35f);
            Assume.That(RunWeapons.ValidTeleportPoint(beside, near.radius), Is.True, "room beside the lane");
            Place(near, beside);
            Assume.That(Query.Flat(beside - player.Position).magnitude, Is.LessThanOrEqualTo(tuning.executeAuraRadius + near.radius));
            ElementStatus.For(player).SetShield(0f);
            float hp = player.Hp;

            weapons.HandleInput(true, true, false);
            Assume.That(targets[0].IsAlive, Is.True);
            Assert.That(player.Hp, Is.EqualTo(hp), "Tame: the backflow no longer shocks you");
            Assert.That(rail.cooldown, Is.EqualTo(weapons.CycleSeconds(rail)).Within(.05f), "nor forces the long cooldown");
            Assert.That(rail.executeAuraLeft, Is.EqualTo(tuning.executeAuraSeconds).Within(.05f), "it becomes the arc shield");
            Assert.That(game.UiSnapshot.handSlots[weapons.ActiveIndex].detail, Does.Contain(ArenaText.Pair("Arc shield","Arc shield")), "the slot shows it");

            await Seconds(tuning.executeAuraInterval * 2f + .2f);
            float zapped = near.maxHp - near.Hp;
            Assert.That(zapped, Is.GreaterThanOrEqualTo(zap * 2f - .01f), "it shocks an enemy beside you every interval");
            Assert.That(Mathf.Abs(zapped / zap - Mathf.Round(zapped / zap)), Is.LessThan(.01f), "for its share of the shot each time");
            Assert.That(targets[2].Hp, Is.EqualTo(targets[2].maxHp), "only within its radius");

            await Seconds(.5f);
            targets[0].Heal(targets[0].maxHp);                                        
            rail.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            Assume.That(targets[0].IsAlive, Is.True);
            Assert.That(rail.executeAuraLeft, Is.EqualTo(tuning.executeAuraSeconds).Within(.05f), "restarted, not stacked");
            await Until(() => rail.executeAuraLeft <= 0f, tuning.executeAuraSeconds + 1f, "the shield runs out");
            float after = near.Hp;
            await Seconds(tuning.executeAuraInterval * 2f);
            Assert.That(near.Hp, Is.EqualTo(after), "then it stops");
            Assert.That(player.Hp, Is.EqualTo(hp), "it never hurts you");
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

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task SpinUp_HoldingFire_PushesYouSlowlyBackFasterWithTheSpin_AWallStopsItHarmlessly()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            Vector3 origin = player.Position;
            player.DebugSetAim(Vector3.back);                                                   
            player.enabled = true;                                                        
            var gun = Equip(game, WeaponForm.Minigun, 0);
            float hp = player.Hp;

            var (early, earlyKnocked) = await HoldFire(game, gun, 0f, .6f);
            await SetBack(player, origin);
            var (full, fullKnocked) = await HoldFire(game, gun, 3f, 2f);

            Assert.That(early, Is.LessThan(.2f * tuning.spinUpRetreatSpeed), "a fresh spin hardly moves you");
            Assert.That(full, Is.EqualTo(2f * tuning.spinUpRetreatSpeed).Within(.45f), "about spinUpRetreatSpeed cells a second at full spin");
            Assert.That(earlyKnocked || fullKnocked, Is.False, "the retreat is a push, never a knockback");
            Assert.That(player.Hp, Is.EqualTo(hp));

            Vector3 stopped = player.Position;
            await Seconds(.5f);
            Assert.That(Query.Flat(player.Position - stopped).magnitude, Is.LessThan(.05f), "only while you hold fire");

            await SetBack(player, origin);
            float gap = .2f;
            WorldMap.Active.CreateWall(origin + Vector3.forward * (BodyRadius(player) + .5f + gap), 4f, 60f, 0f, player);
            Physics.SyncTransforms();
            await SetBack(player, origin);                                
            var (blocked, blockedKnocked) = await HoldFire(game, gun, 3f, 2f);
            Assert.That(blocked, Is.LessThan(Mathf.Min(.5f * full, gap + .25f)), "the wall stops the retreat");
            Assert.That(blockedKnocked, Is.False, "never a knockback");
            Assert.That(player.Hp, Is.EqualTo(hp), "and running into the wall costs no HP");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpinUpTame_WallAtYourBackAtFullSpin_BracesYou_OpenGroundOnlyFreesTurning()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var gun = Equip(game, WeaponForm.Minigun, 0, UpgradeAxis.Tame);
            ClearLane(game, targets);

            Vector3 origin = game.player.Position + Vector3.forward * 8f;
            Place(game.player, origin);
            void Spin() { weapons.HandleInput(false, true, false); gun.heldTime = tuning.spinAnchorSpinSeconds; }

            Spin();
            Assert.That(weapons.AimTurnRateLimit, Is.Zero, "turning is free");

            Assert.That(weapons.MoveMultiplier, Is.EqualTo(1f), "the spin does not slow your feet");
            gun.axes &= ~UpgradeAxis.Tame;
            Assert.That(weapons.MoveMultiplier, Is.EqualTo(1f), "nor untamed");
            gun.axes |= UpgradeAxis.Tame;
            Assert.That(weapons.Anchored, Is.False, "open ground: nothing braces you");
            float openCycle = weapons.GetCooldownDuration(gun);
            Assert.That(openCycle, Is.EqualTo(1f / tuning.spinUpTopRate).Within(.0005f), "the plain top rate");
            float free = await KnockSpeed(game, origin);

            WorldMap.Active.CreateWall(origin + Vector3.back * (BodyRadius(game.player) + .5f + .1f), 4f, 60f, 0f, game.player);
            Physics.SyncTransforms();
            game.player.CancelDisplacement(); Place(game.player, origin);
            Spin();
            Assert.That(weapons.Anchored, Is.True, "a wall at your back at full spin braces you");
            Assert.That(weapons.GetCooldownDuration(gun), Is.EqualTo(openCycle / (1f + tuning.spinBraceRateBonus)).Within(.0005f), "braced, it fires 30% faster");
            float braced = await KnockSpeed(game, origin);
            Assume.That(free, Is.GreaterThan(1f), "a knockback moves the disabled test body");
            Assert.That(braced / free, Is.EqualTo(tuning.spinAnchorKnockFactor).Within(.05f), "braced, knockback is halved");

            Spin();
            gun.heldTime = tuning.spinAnchorSpinSeconds * .5f;
            Assert.That(weapons.Anchored, Is.False, "below full spin the wall does not brace you");
            weapons.HandleInput(false, false, true);
            gun.heldTime = tuning.spinAnchorSpinSeconds;
            Assert.That(weapons.Anchored, Is.False, "letting go ends it at once");
            gun.axes = UpgradeAxis.None;
            Spin();
            Assert.That(weapons.Anchored, Is.False, "untamed, a wall never braces you");
            gun.axes = UpgradeAxis.Tame;

            WorldMap.Active.CreateVoidHole(origin + Vector3.forward * 4f, 1f, 5f, 3f, null, false);
            Spin();
            for (int i = 0; i < 5; i++) await Awaitable.FixedUpdateAsync();
            Assert.That(Query.Flat(game.player.Position - origin).magnitude, Is.LessThan(.02f), "braced, the hole does not drag you");
            weapons.HandleInput(false, false, true);
            for (int i = 0; i < 3; i++) await Awaitable.FixedUpdateAsync();
            float dragged = Vector3.Dot(Query.Flat(game.player.Position - origin), Vector3.forward);
            game.player.CancelDisplacement(); Place(game.player, origin);
            Assert.That(dragged, Is.GreaterThan(.02f), "let go and the pull takes you again");
        }

        static async Task<(float distance, bool knocked)> HoldFire(GameManager game, RunWeapon gun, float spin, float seconds)
        {
            var player = game.player;
            Vector3 from = player.Position, back = -Query.Flat(player.AimDirection).normalized;
            bool knocked = false;
            game.runWeapons.HandleInput(false, true, false);
            gun.heldTime = spin;
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                await Awaitable.NextFrameAsync();
                game.runWeapons.HandleInput(false, true, false);
                knocked |= player.State == CharacterState.Knocked;
            }
            game.runWeapons.HandleInput(false, false, true);
            float settle = Time.time + .4f;
            while (Time.time < settle) { knocked |= player.State == CharacterState.Knocked; await Awaitable.NextFrameAsync(); }
            return (Vector3.Dot(Query.Flat(player.Position - from), back), knocked);
        }

        static async Task<float> KnockSpeed(GameManager game, Vector3 origin)
        {
            game.player.Knockback(Vector3.forward, 4f, KnockKind.Hit);
            await Awaitable.FixedUpdateAsync(); await Awaitable.FixedUpdateAsync();
            float speed = Query.Flat(game.player.GetComponent<Rigidbody>().linearVelocity).magnitude;
            game.player.CancelDisplacement(); Place(game.player, origin);
            return speed;
        }

        static float BodyRadius(Combatant body)
        {
            var capsule = body.GetComponent<CapsuleCollider>();
            return Mathf.Max(body.radius, capsule ? capsule.radius : 0f);
        }

        static async Task SetBack(Combatant body, Vector3 at)
        {
            body.CancelDisplacement();
            Place(body, at);
            await Awaitable.FixedUpdateAsync();
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
        public async Task MeltdownTame_Overheat_KnocksNearbyEnemiesBackAndSpeedsUpYourFeetOnEveryWeapon()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            float seconds = tuning.meltdownTameHasteSeconds;
            try
            {
                tuning.meltdownTameHasteSeconds = .6f;
                var near = targets[0];
                ClearLane(game, targets[1], targets[2]);
                Place(near, game.player.Position + Vector3.forward * 2.5f);

                near.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

                var plain = Equip(game, WeaponForm.Minigun, 1);
                plain.heat = 3.95f;
                weapons.HandleInput(true, true, false);
                Assert.That(near.Hp, Is.LessThan(near.maxHp), "the untamed blast hits it");
                Assert.That(near.State, Is.Not.EqualTo(CharacterState.Knocked), "but pushes nobody");
                Assert.That(game.player.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "and gives you no speed");

                near.ResetForSpawn(); near.enabled = false;
                Place(near, game.player.Position + Vector3.forward * 2.5f);
                game.player.Heal(game.player.maxHp);
                var gun = Equip(game, WeaponForm.Minigun, 1, UpgradeAxis.Tame);
                weapons.Hand[1 - weapons.ActiveIndex] = new RunWeapon { form = WeaponForm.Railgun, variant = 1, revealed = true, rounds = weapons.ClipSize(WeaponForm.Railgun, 1) };
                float before = Query.Flat(near.Position - game.player.Position).magnitude, hp = game.player.Hp;
                gun.heat = 3.95f;
                weapons.HandleInput(true, true, false);

                Assert.That(game.player.Hp, Is.EqualTo(hp), "the tamed blast spares you");
                Assert.That(near.State, Is.EqualTo(CharacterState.Knocked), "and knocks the enemy next to you back");
                Assert.That(game.player.ExternalMoveMultiplier, Is.EqualTo(1f + tuning.meltdownTameMoveBonus).Within(.001f), "your feet speed up");
                for (int i = 0; i < 3; i++) await Awaitable.FixedUpdateAsync();
                Assert.That(Query.Flat(near.Position - game.player.Position).magnitude, Is.GreaterThan(before + .2f), "away from you");

                weapons.Switch();
                await Awaitable.NextFrameAsync();
                Assert.That(weapons.Active.form, Is.EqualTo(WeaponForm.Railgun), "the other weapon is in hand");
                Assert.That(game.player.ExternalMoveMultiplier, Is.EqualTo(1f + tuning.meltdownTameMoveBonus).Within(.001f), "the speed holds on it");
                Assert.That(weapons.AnomalySlotDetail(gun), Does.Contain($"+{tuning.meltdownTameMoveBonus * 100f:0}%"), "the Meltdown slot shows the move bonus too");
                await Until(() => weapons.MeltdownHaste == 1f, 2f, "the bonus runs out");
                await Awaitable.NextFrameAsync();
                Assert.That(game.player.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "then your feet are back to their own pace");
            }
            finally { tuning.meltdownTameHasteSeconds = seconds; }
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
        public async Task Barrage_LockedEnemiesOffTheAim_MissilesHomeInAndOneWhoseTargetDiesTakesTheNearest()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            float spread = tuning.barrageLaunchSpread;
            try
            {

                tuning.barrageLaunchSpread = 20f;

                var swarm = Equip(game, WeaponForm.SwarmLauncher, 0, UpgradeAxis.Tame);
                Vector3 origin = game.player.Position;
                float hit = weapons.HitDamage(swarm), range = weapons.roster.Get(WeaponForm.SwarmLauncher).rangeA;
                ClearLane(game, targets[1], targets[2]);
                var first = targets[0]; var second = targets[1];
                Place(first, origin + Vector3.forward * 6f);

                game.player.DebugSetAim(Vector3.right);

                weapons.HandleInput(true, true, false);
                var missiles = Actors(swarm, AttackMode.Swarm);
                Assert.That(missiles, Has.Length.EqualTo(8));
                Assert.That(missiles.All(a => a.HomeTarget == first && !a.hurtOwner), Is.True, "every missile locks the one enemy in range");
                await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0, 4f, "the volley lands");
                Assert.That(first.maxHp - first.Hp, Is.EqualTo(8f * hit).Within(.01f), "all eight curve in and hit it, 90 degrees off the aim");

                first.ResetForSpawn(); first.enabled = false;
                second.ResetForSpawn(); second.enabled = false;
                Place(first, origin + Vector3.forward * 4f);
                Place(second, origin + Vector3.forward * 8f);
                swarm.cooldown = 0f;
                weapons.HandleInput(true, true, false);
                missiles = Actors(swarm, AttackMode.Swarm);
                Assert.That(missiles.Count(a => a.HomeTarget == first), Is.EqualTo(4), "the spares take turns on the locked enemies");
                Assert.That(missiles.Count(a => a.HomeTarget == second), Is.EqualTo(4));
                await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0, 4f, "the volley lands");

                first.ResetForSpawn(); first.enabled = false;
                second.ResetForSpawn(); second.enabled = false;
                Place(first, origin + Vector3.forward * 4f);
                Place(second, origin + Vector3.forward * (range + 6f));
                swarm.cooldown = 0f;
                weapons.HandleInput(true, true, false);
                Assert.That(Actors(swarm, AttackMode.Swarm).All(a => a.HomeTarget == first), Is.True, "only the enemy in range is locked");
                LeaveHp(first, 0f);
                Assume.That(first.IsAlive, Is.False);
                await Awaitable.FixedUpdateAsync(); await Awaitable.FixedUpdateAsync();
                Assert.That(Actors(swarm, AttackMode.Swarm).All(a => a.HomeTarget == second), Is.True, "the dead one's missiles take the enemy nearest them");
                await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0, 4f, "the volley lands");
                Assert.That(second.maxHp - second.Hp, Is.EqualTo(8f * hit).Within(.01f), "so all eight hit the survivor");
            }
            finally { tuning.barrageLaunchSpread = spread; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task BarrageBackfire_TooFewEnemies_TwoSpareMissilesComeBackForYou_EnoughEnemiesNone()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;

            var swarm = Equip(game, WeaponForm.SwarmLauncher, 0, UpgradeAxis.Twist, mk: 3);
            float baseDamage = weapons.roster.Get(WeaponForm.SwarmLauncher).damageA;

            Vector3 mid = game.player.Position + Vector3.forward * 8f;
            Place(game.player, mid);
            game.player.DebugSetAim(Vector3.back);
            Place(targets[0], mid + Vector3.forward * 1.6f);                     
            Place(targets[1], mid + Vector3.forward * 3.5f);
            Place(targets[2], mid + Vector3.forward * 5.5f);
            float hp = game.player.Hp;

            weapons.HandleInput(true, true, false);
            var missiles = Actors(swarm, AttackMode.Swarm);
            Assert.That(missiles, Has.Length.EqualTo(8));
            foreach (var target in targets) Assert.That(missiles.Count(a => a.HomeTarget == target), Is.EqualTo(1), "one missile per enemy");
            var backfire = missiles.Where(a => a.HomeTarget == game.player).ToArray();
            Assert.That(backfire, Has.Length.EqualTo(tuning.barrageSelfLockMax), "two of the five spares lock onto you");
            Assert.That(backfire.All(a => a.hurtOwner && Mathf.Approximately(a.ownerDamage, baseDamage)), Is.True, "they hurt you for the base damage");
            Assert.That(missiles.Count(a => a.HomeTarget == null), Is.EqualTo(8 - targets.Length - tuning.barrageSelfLockMax), "the rest fly straight on");
            Assert.That(missiles.Where(a => a.HomeTarget != game.player).All(a => !a.hurtOwner), Is.True, "no other missile can hurt you");

            await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0 && Actors(swarm, AttackMode.Sticky).Length == 0, 5f, "every missile is spent");
            float taken = game.progression.Stats.DamageTakenMultiplier;
            Assert.That(game.player.Hp, Is.EqualTo(hp - tuning.barrageSelfLockMax * baseDamage * taken).Within(.01f), "exactly the two that came back hurt you");
            Assert.That(targets[0].Hp, Is.LessThan(targets[0].maxHp), "the merged blast beside you went off");

            var extra = MoreTargets(game, targets, 5);
            var all = targets.Concat(extra).ToArray();
            float[] spots = { 1.6f, 2.8f, 4f, 5.2f, 6.4f, -2.2f, -3.4f, -4.6f };
            for (int i = 0; i < all.Length; i++) { all[i].ResetForSpawn(); all[i].enabled = false; Place(all[i], mid + Vector3.forward * spots[i] + Vector3.right * (i % 2 == 0 ? .3f : -.3f)); }
            Place(game.player, mid);
            hp = game.player.Hp;
            swarm.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            missiles = Actors(swarm, AttackMode.Swarm);
            Assert.That(missiles.Select(a => a.HomeTarget).Distinct().Count(), Is.EqualTo(8), "each missile locks an enemy of its own");
            Assert.That(missiles.All(a => a.HomeTarget != game.player && a.HomeTarget != null && !a.hurtOwner), Is.True, "none locks onto you");
            await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0 && Actors(swarm, AttackMode.Sticky).Length == 0, 5f, "every missile is spent");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "so none hurts you");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BarrageTame_NoEnemyInRange_GoesForTheNearestVisible_NoEnemyAtAll_EscortsYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var swarm = Equip(game, WeaponForm.SwarmLauncher, 0, UpgradeAxis.Tame);
            Vector3 origin = game.player.Position;
            float range = weapons.roster.Get(WeaponForm.SwarmLauncher).rangeA;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            Place(enemy, origin + Vector3.forward * (range + 3f));                                                   
            float hp = game.player.Hp;

            weapons.HandleInput(true, true, false);
            var missiles = Actors(swarm, AttackMode.Swarm);
            Assert.That(missiles, Has.Length.EqualTo(8));
            Assert.That(missiles.All(a => a.HomeTarget == enemy && !a.Escorting && !a.hurtOwner), Is.True, "all go for the nearest visible enemy, out of range as it is");
            await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0, 4f, "the volley lands");
            Assert.That(enemy.Hp, Is.LessThan(enemy.maxHp), "and hits it");

            enemy.DiscardWithoutReward();
            swarm.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            var escort = Actors(swarm, AttackMode.Swarm);
            Assert.That(escort, Has.Length.EqualTo(8));
            Assert.That(escort.All(a => a.Escorting && a.HomeTarget == null), Is.True, "nothing to lock: they escort you");
            await Seconds(.3f);
            Assert.That(escort.All(a => a.gameObject.activeSelf && Query.Flat(a.transform.position - game.player.Position).magnitude < tuning.barrageEscortRadius + .2f), Is.True, "close around you");

            enemy.ResetForSpawn(); enemy.enabled = false;
            Place(enemy, origin + Vector3.forward * 5f);
            await Until(() => escort.All(a => !a.gameObject.activeSelf || a.HomeTarget == enemy && !a.Escorting), 1f, "the escort locks the newcomer");
            await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0, 4f, "the volley lands");
            Assert.That(enemy.Hp, Is.LessThan(enemy.maxHp), "and strikes it");

            enemy.DiscardWithoutReward();
            WorldMap.Active.CreateWall(origin + Vector3.forward * (BodyRadius(game.player) + .5f + .1f), 4f, 60f, 0f, game.player);
            Physics.SyncTransforms();
            game.player.CancelDisplacement(); Place(game.player, origin);
            float face = BodyRadius(game.player) + .1f;
            swarm.cooldown = 0f;
            weapons.HandleInput(true, true, false);
            float riding = Time.time + tuning.barrageEscortSeconds - .4f;
            while (Time.time < riding)
            {
                await Awaitable.FixedUpdateAsync();
                Assert.That(Actors(swarm, AttackMode.Swarm).All(a => Vector3.Dot(Query.Flat(a.transform.position - origin), Vector3.forward) < face), Is.True, "no missile passes the wall");
            }
            Assert.That(Actors(swarm, AttackMode.Swarm), Has.Length.EqualTo(8), "still escorting, none lost on the wall");
            await Until(() => Actors(swarm, AttackMode.Swarm).Length == 0, 1.5f, "the escort ends");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "tamed, no missile ever hurts you");
        }

        static Enemy[] MoreTargets(GameManager game, Enemy[] targets, int count)
        {
            var extra = game.waves.preparedEnemies
                .Where(e => e && !e.gameObject.activeSelf && e.definition && !e.IsBoss && !targets.Contains(e))
                .OrderBy(e => e.definition.kind == EnemyKind.Grunt ? 0 : 1).Take(count).ToArray();
            Assume.That(extra, Has.Length.EqualTo(count), "the wave pool has spare bodies");
            foreach (var e in extra)
            {
                e.Configure(e.definition, 1f, false);
                e.maxHp = 100f;
                e.ResetForSpawn();
                e.enabled = false;
                e.moveSpeed = 0f;
                e.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
            }
            return extra;
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

            var tuning = GameManager.Instance.runWeapons.upgrades;
            string P(float share) => (share * 100f).ToString("0.#") + "%";
            string spinTame = ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Minigun, 0, UpgradeAxis.Tame);
            Assert.That(spinTame, Does.Contain("backing into a wall").And.Contain("braces you").And.Contain("+" + P(tuning.spinBraceRateBonus)).And.Contain(P(tuning.spinAnchorKnockFactor)), "Spin-up Tame: the wall brace and what it gives");
            string meltTame = ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Minigun, 1, UpgradeAxis.Tame);
            Assert.That(meltTame, Does.Contain($"knocks nearby enemies back {tuning.meltdownTamePush:0.##}").And.Contain("" + P(tuning.meltdownTameMoveBonus)).And.Contain("" + P(tuning.meltdownTameHaste)).And.Contain("3 s lockout"), "Meltdown Tame: push, both speeds, the lockout");
            string barrageTame = ArenaTerms.ExpandedUpgradeDescription(WeaponForm.SwarmLauncher, 0, UpgradeAxis.Tame);
            Assert.That(barrageTame, Does.Contain("circle you").And.Contain($"{tuning.barrageEscortSeconds:0.##} s").And.Contain("take turns on the locked enemies"), "Barrage Tame: focus fire and the escort");
            foreach (var text in new[] { spinTame, meltTame, barrageTame }) Assert.That(text, Does.Not.Contain("{"), "every number is filled in");
            Assert.That(WeaponGuide.How(WeaponForm.SwarmLauncher, 0), Does.Contain("micro-missiles").And.Contain("home in"), "Barrage card: homing micro-missiles");
            Assert.That(WeaponGuide.Backfire(WeaponForm.SwarmLauncher, 0), Does.Contain($"up to {tuning.barrageSelfLockMax} spare").And.Contain("lock onto you"), "Barrage card: the missiles that come back for you");
            Assert.That(WeaponGuide.Backfire(WeaponForm.Minigun, 0), Does.Contain("pushes you back").And.Not.Contain("移速"), "Spin-up card: the slow retreat, no slow walk (P20)");
        }
    }
}
