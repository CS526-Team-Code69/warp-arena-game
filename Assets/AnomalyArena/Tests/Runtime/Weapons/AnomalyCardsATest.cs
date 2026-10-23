using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
                                                                                                                           
                                                                                                                 
    [TestFixture]
    public class AnomalyCardsATest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const float TargetHp = 100f;

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant, UpgradeAxis axes = UpgradeAxis.None)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, axes = axes, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        static void Place(Enemy enemy, Vector3 at, bool movable = false)
        {
            V3CombatSmokeTest.Teleport(enemy, at);
            if (movable) enemy.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            Physics.SyncTransforms();
        }

        static void LeaveHp(Combatant body, float hp) =>
            body.ReceiveDamage(new DamageInfo { amount = body.Hp - hp, source = DamageSource.Poison, ignoreShield = true });

                                                                                                                 
        static void WallAhead(GameManager game, float distance, float width = 6f)
        {
            WorldMap.Active.CreateWall(game.player.Position + Query.Flat(game.player.AimDirection).normalized * distance, width, 60f, 0f, game.player);
            Physics.SyncTransforms();
        }

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

        static IEnumerable<AttackActor> Actors(RunWeapon w, AttackMode mode) =>
            AttackPool.Instance.actors.Where(a => a && a.gameObject.activeSelf && a.weapon == w && a.mode == mode);

                                           

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedShotTwist_Release_FiresForwardAndBackwardEachAtTheTwinShare()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;
                                                                                               
            V3CombatSmokeTest.Teleport(game.player, origin + Vector3.forward * 7.5f);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            var rifle = Equip(game, WeaponForm.Rifle, 0, UpgradeAxis.Twist);
            float expected = game.runWeapons.HitDamage(rifle) * game.runWeapons.upgrades.chargedTwinShare;

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(rifle.charging, Is.True);
            rifle.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);
            Assert.That(Actors(rifle, AttackMode.Bolt).Count(), Is.EqualTo(2), "one release fires two rounds");

            await Until(() => targets.All(t => t.Hp < TargetHp), 2f, "both rounds pierce their whole line");
            await Until(() => !Actors(rifle, AttackMode.Bolt).Any(), 3f, "both rounds expire");
            foreach (var t in targets)
                Assert.That(t.Hp, Is.EqualTo(TargetHp - expected).Within(.01f), "each round deals the twin share of a full charge, once per body");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedShotTame_Charging_SlowsEnemiesOnTheAimLineNotYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var rifle = Equip(game, WeaponForm.Rifle, 0, UpgradeAxis.Tame);
            float slow = game.runWeapons.upgrades.chargeLineSlow;
            var before = targets.Select(t => t.ExternalMoveMultiplier).ToArray();

            game.runWeapons.HandleInput(true, true, false);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(rifle.charging, Is.True);
            Assert.That(game.runWeapons.MoveMultiplier, Is.EqualTo(1f), "the tamed charge no longer slows you");
            for (int i = 0; i < targets.Length; i++)
            {
                Assert.That(targets[i].ExternalMoveMultiplier, Is.EqualTo(before[i] * slow).Within(.001f), "every enemy on the line is slowed");
                Assert.That(game.runWeapons.IsChargeSlowed(targets[i]), Is.True);
            }

            game.player.DebugSetAim(Vector3.back);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            for (int i = 0; i < targets.Length; i++)
                Assert.That(targets[i].ExternalMoveMultiplier, Is.EqualTo(before[i]).Within(.001f), "off the line the slow lifts at once");

            game.player.DebugSetAim(Vector3.forward);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(targets[0].ExternalMoveMultiplier, Is.EqualTo(before[0] * slow).Within(.001f));
            rifle.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            for (int i = 0; i < targets.Length; i++)
                Assert.That(targets[i].ExternalMoveMultiplier, Is.EqualTo(before[i]).Within(.001f), "releasing the charge lifts the slow");
        }

                                           

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReverseShotTwist_FirstWallBounce_SplitsIntoTwoRoundsAtTheSplitShare()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            WallAhead(game, 10f);
                                                                                                                     
            game.player.DebugSetAim(Vector3.back);
            var rifle = Equip(game, WeaponForm.Rifle, 1, UpgradeAxis.Twist);
            var tuning = game.runWeapons.upgrades;
            float hit = game.runWeapons.HitDamage(rifle);

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(Actors(rifle, AttackMode.Bolt).Count(), Is.EqualTo(1));
            await Until(() => Actors(rifle, AttackMode.Bolt).Count(a => a.splitChild) == 2, 2f, "the round splits at the wall");

            var split = Actors(rifle, AttackMode.Bolt).ToArray();
            Assert.That(split, Has.Length.EqualTo(2), "one round became two");
            foreach (var a in split)
            {
                Assert.That(a.damage, Is.EqualTo(hit * tuning.reverseSplitShare).Within(.001f), "each keeps the split share");
                Assert.That(a.ricochets, Is.Zero, "split rounds do not bounce again");
                Assert.That(Vector3.Dot(a.direction, Vector3.back), Is.GreaterThan(.5f), "both fly back off the wall");
            }
            Assert.That(Vector3.Angle(split[0].direction, split[1].direction), Is.EqualTo(tuning.reverseSplitAngle * 2f).Within(1f), "either side of the bounce");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReverseShotTame_EnemyCloseBehind_DrawsTheRoundOtherwiseItFliesAlongTheAim()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
                                                                                                 
            game.player.DebugSetAim(Vector3.back);
            var rifle = Equip(game, WeaponForm.Rifle, 1, UpgradeAxis.Tame);

            game.runWeapons.HandleInput(true, true, false);
            var round = Actors(rifle, AttackMode.Bolt).Single();
            Assert.That(Vector3.Dot(round.direction, Vector3.forward), Is.GreaterThan(.99f), "the close enemy behind draws the round");
            await Until(() => targets[0].Hp < TargetHp, 2f, "the guard round hits the enemy behind");

            Place(targets[0], targets[0].Position + Vector3.forward * 8f);
            rifle.cooldown = rifle.reload = 0f; rifle.rounds = game.runWeapons.ClipSize(WeaponForm.Rifle, 1);
            game.runWeapons.HandleInput(true, true, false);
            var plain = Actors(rifle, AttackMode.Bolt).OrderByDescending(a => a.Sequence).First();
            Assert.That(Vector3.Dot(plain.direction, Vector3.back), Is.GreaterThan(.99f), "with nobody close behind it flies along the aim");
        }

                                             

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnifeTwist_KnifeComesHome_SpinsAndCutsEnemiesAroundTheThrowPoint()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 home = game.player.Position;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
                                                                                                            
            Place(targets[0], home + Vector3.back * 1.3f);
            float playerHp = game.player.Hp;
            var knife = Equip(game, WeaponForm.Knife, 0, UpgradeAxis.Twist);
            float spin = game.runWeapons.HitDamage(knife) * game.runWeapons.upgrades.knifeSpinShare;

            game.runWeapons.HandleInput(true, true, false);
            var actor = Actors(knife, AttackMode.Knife).Single();
            Assert.That(Query.Flat(actor.KnifeHomePoint - home).magnitude, Is.LessThan(.05f), "the throw point is known before the return (marker)");
            await Until(() => !knife.inFlight, 4f, "the knife comes home");

            Assert.That(targets[0].Hp, Is.EqualTo(TargetHp - spin).Within(.01f), "the spin cuts once at its share");
            Assert.That(game.player.Hp, Is.EqualTo(playerHp), "the spin never cuts you");

            float after = targets[0].Hp;
            var plain = Equip(game, WeaponForm.Knife, 0);
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !plain.inFlight, 4f, "the plain knife comes home");
            Assert.That(targets[0].Hp, Is.EqualTo(after), "without Twist there is no spin");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnifeTame_KillReturnThroughYou_SparesYouAndAddsTargetsToTheNextThrowOnce()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var knife = Equip(game, WeaponForm.Knife, 0, UpgradeAxis.Tame);
            LeaveHp(targets[0], 1f);
            float playerHp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !knife.inFlight, 4f, "the kill return comes back to you");

            Assert.That(targets[0].IsAlive, Is.False);
            Assert.That(game.player.Hp, Is.EqualTo(playerHp), "the tamed return spares you");
            Assert.That(knife.knifeCaught, Is.True, "standing on the return line catches the knife");
            Assert.That(game.runWeapons.AnomalySlotDetail(knife), Does.Contain("+" + tuning.knifeCatchTargets), "the hand slot shows the catch");

            knife.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            var next = Actors(knife, AttackMode.Knife).Single();
            Assert.That(next.targetsLeft, Is.EqualTo(4 + tuning.knifeCatchTargets), "the next throw bounces to more targets");
            Assert.That(knife.knifeCaught, Is.False, "the bonus is used up by one throw");
        }

                                   

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HookTwist_FlungEnemyCrashesIntoAnother_HooksItOncePerManualHook()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;
            targets[2].DiscardWithoutReward();
            Place(targets[0], origin + Vector3.forward * 3f, movable: true);
            Place(targets[1], origin + Vector3.forward * 7f, movable: true);
            var hook = Equip(game, WeaponForm.Knife, 1, UpgradeAxis.Twist);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => hook.hooked == targets[0], 3f, "the hook reels in the first enemy");
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => hook.hooked == targets[1], 3f, "the crash chains the hook onto the enemy it hit");

            Assert.That(targets[1].Hp, Is.LessThan(TargetHp), "the crash still deals its damage first");
            Assert.That(hook.hookChainUsed, Is.True);

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(hook.hooked, Is.Null, "the chained enemy can be flung again");
            game.runWeapons.ChainHook(hook, targets[0]);
            Assert.That(hook.hookBusy, Is.False, "no second chain from the same manual hook");

            hook.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(hook.hookChainUsed, Is.False, "a manual hook resets the chain");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HookTame_HookOut_LetsYouMoveAndAnEmptyHookOnAWallPullsYouToIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var player = game.player;

            var plain = Equip(game, WeaponForm.Knife, 1);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(game.runWeapons.HandHookBusy, Is.True);
            Assert.That(player.MovementLocked, Is.True, "the untamed hook holds you still");
            await Until(() => !plain.hookBusy, 3f, "the empty hook comes back");

            Vector3 start = player.Position;
            float hp = player.Hp;
            WallAhead(game, 6f);
            player.enabled = true;                      
            var tamed = Equip(game, WeaponForm.Knife, 1, UpgradeAxis.Tame);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(game.runWeapons.HandHookBusy, Is.True);
            Assert.That(player.MovementLocked, Is.False, "the tamed hook lets you move");

            await Until(() => Vector3.Dot(player.Position - start, Vector3.forward) > 2f, 2f, "the wall hit pulls you forward");
            await Seconds(.8f);
                                                                                       
            float moved = Vector3.Dot(player.Position - start, Vector3.forward);
            Assert.That(moved, Is.LessThan(5.5f - player.radius - game.runWeapons.upgrades.hookTameWallGap + .4f), "you stop short of the wall");
            Assert.That(player.Hp, Is.EqualTo(hp), "a push never deals wall damage");
            Assert.That(player.State, Is.EqualTo(CharacterState.Normal), "you keep control");
        }

                                               

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StrikeTwist_StrikeKillsAnEnemy_ItBlastsAgainAtItsShareWhereItStood()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;
            var tuning = game.runWeapons.upgrades;
                                                                                                                              
            Place(targets[0], origin + Vector3.forward * 8.8f);
            Place(targets[1], origin + Vector3.forward * 10.8f);
            Place(targets[2], origin + Vector3.forward * 16f);
            LeaveHp(targets[0], 1f);
            game.player.DebugSetAimPoint(origin + Vector3.forward * 6f);
            var strike = Equip(game, WeaponForm.RocketLauncher, 0, UpgradeAxis.Twist);
            float chain = game.runWeapons.HitDamage(strike) * tuning.strikeChainShare;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !targets[0].IsAlive, 3f, "the strike kills the weak enemy");
            Assert.That(targets[1].Hp, Is.EqualTo(TargetHp), "the outer enemy is outside the strike");
            await Until(() => targets[1].Hp < TargetHp, 1f, "the small blast goes off where the enemy died");
            await Seconds(.3f);
            Assert.That(targets[1].Hp, Is.EqualTo(TargetHp - chain).Within(.01f), "one small blast at its share");
            Assert.That(targets[2].Hp, Is.EqualTo(TargetHp), "the small blast is short");

                                                     
            Place(targets[2], origin + Vector3.forward * 8.8f);
            LeaveHp(targets[2], 1f);
            float outer = targets[1].Hp;
            var plain = Equip(game, WeaponForm.RocketLauncher, 0);
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !targets[2].IsAlive, 3f, "the plain strike kills the second weak enemy");
            await Seconds(.4f);
            Assert.That(targets[1].Hp, Is.EqualTo(outer), "no small blast without Twist");
            Assert.That(plain.Has(UpgradeAxis.Twist), Is.False);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StrikeTame_OwnBlast_PushesYouClearWithoutDamageOrKnockback()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var player = game.player;
            player.enabled = true;                      
            Vector3 start = player.Position;
            float hp = player.Hp;
                                                                                                       
            player.DebugSetAimPoint(start + Vector3.back);
            Equip(game, WeaponForm.RocketLauncher, 0, UpgradeAxis.Tame);

            game.runWeapons.HandleInput(true, true, false);
            bool knocked = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Vector3.Dot(player.Position - start, Vector3.forward) < game.runWeapons.upgrades.strikeJumpDistance * .75f)
            {
                knocked |= player.State == CharacterState.Knocked;
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for the blast to push you forward.");
                await Awaitable.NextFrameAsync();
            }
            await Seconds(.6f);
            knocked |= player.State == CharacterState.Knocked;

            Assert.That(player.Hp, Is.EqualTo(hp), "your own blast spares you");
            Assert.That(knocked, Is.False, "it is a push: you never lose control");
            Assert.That(Vector3.Dot(player.Position - start, Vector3.forward), Is.LessThan(game.runWeapons.upgrades.strikeJumpDistance + .5f), "about the jump distance");
        }

                                                        

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LaunchYourselfTwist_EnemiesHit_AreKnockedBackByTheFlightLeftPlusExtraCapped()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;
            var tuning = game.runWeapons.upgrades;
            targets[2].DiscardWithoutReward();
            Place(targets[1], origin + Vector3.forward * 15f);
            var knocks = new Dictionary<Combatant, float>();
            foreach (var t in targets.Take(2)) { var body = t; body.AttackReceived += info => knocks[body] = info.knockDistance; }
            Equip(game, WeaponForm.RocketLauncher, 1, UpgradeAxis.Twist);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => game.player.State != CharacterState.Dashing, 3f, "the flight ends");

            float scale = game.runWeapons.UpgradePushScale;
            Assert.That(knocks.ContainsKey(targets[0]) && knocks.ContainsKey(targets[1]), Is.True, "both enemies were hit");
            Assert.That(knocks[targets[0]], Is.EqualTo(tuning.rocketCueMax * scale).Within(.01f), "early in the flight the knockback is capped");
                                                                                                                              
            float left = targets[1].radius + game.player.radius + .3f;
            Assert.That(knocks[targets[1]], Is.EqualTo((left + tuning.rocketCueExtra) * scale).Within(.05f), "late in the flight: the flight left plus the extra");
            Assert.That(targets[0].State, Is.EqualTo(CharacterState.Knocked), "a real knockback");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LaunchYourselfTame_WallHit_CostsNoHpAndResetsTheCooldown()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var player = game.player;
            Vector3 start = player.Position;
            WallAhead(game, 6f);

            var plain = Equip(game, WeaponForm.RocketLauncher, 1);
            float hp = player.Hp;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => player.State != CharacterState.Dashing, 2f, "the untamed flight hits the wall");
            Assert.That(player.Hp, Is.LessThan(hp), "the backfire: a wall hit costs HP");
            Assert.That(plain.cooldown, Is.GreaterThan(0f));

            player.CancelDisplacement();
            V3CombatSmokeTest.Teleport(player, start);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            var tamed = Equip(game, WeaponForm.RocketLauncher, 1, UpgradeAxis.Tame);
            hp = player.Hp;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(tamed.cooldown, Is.GreaterThan(0f), "the flight starts its cooldown");
            await Until(() => player.State != CharacterState.Dashing, 2f, "the tamed flight hits the wall");
            Assert.That(player.Hp, Is.EqualTo(hp), "the tamed wall hit is free");
            Assert.That(tamed.cooldown, Is.Zero, "and refunds the flight at once");
        }

                                             

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterBlastTwist_SecondRound_PullsTheConeInThenBlastsWithoutFalloff()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            foreach (var t in targets) Place(t, t.Position, movable: true);
                                                                                                                              
            Place(targets[2], game.player.Position + Vector3.forward * (tuning.scatterVacuumRange + targets[2].radius + 1f), movable: true);
            foreach (var t in targets.Take(2)) t.enabled = true;                                 
            var blast = Equip(game, WeaponForm.ScatterCannon, 0, UpgradeAxis.Twist);
            blast.rounds = 1;                           
            float expected = game.runWeapons.HitDamage(blast) * tuning.scatterVacuumShare;

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(targets[0].State, Is.EqualTo(CharacterState.Knocked), "the near enemy is pulled");
            Assert.That(targets[1].State, Is.EqualTo(CharacterState.Knocked), "the enemy at 6 is pulled");
            Assert.That(targets[2].State, Is.EqualTo(CharacterState.Normal), "an enemy past the pull range stays put");
            Assert.That(targets.All(t => t.Hp == TargetHp), Is.True, "the pull itself deals no damage");
            Assert.That(blast.reload, Is.GreaterThan(0f), "the clip is spent as usual");

            await Until(() => targets[1].Hp < TargetHp, 1f, "the blast follows the pull");
            Assert.That(targets[0].Hp, Is.EqualTo(TargetHp - expected).Within(.01f), "a fixed share, no distance falloff");
            Assert.That(targets[1].Hp, Is.EqualTo(TargetHp - expected).Within(.01f), "the pulled enemy is inside the short cone");
            Assert.That(targets[2].Hp, Is.EqualTo(TargetHp), "the far enemy stays out of it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterBlastTame_PressInsideTheReloadWindow_ReloadsAndFiresAtOnceOncePerReload()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var blast = Equip(game, WeaponForm.ScatterCannon, 0, UpgradeAxis.Tame);
            float inside = (tuning.scatterQuickStart + tuning.scatterQuickEnd) * .5f;

            blast.rounds = 1;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(blast.reload, Is.GreaterThan(0f));
            Assert.That(blast.reloadTotal, Is.EqualTo(blast.reload));
                                                                                 
            blast.reload = blast.reloadTotal * .75f;
            Assert.That(game.runWeapons.Charge01, Is.EqualTo(.25f).Within(.001f), "the charge bar shows the reload done");
            Assert.That(game.runWeapons.ChargeWindow(out float windowStart, out float windowEnd), Is.True, "the reload bar marks the window");
            Assert.That(windowStart, Is.EqualTo(tuning.scatterQuickStart).Within(.001f));
            Assert.That(windowEnd, Is.EqualTo(tuning.scatterQuickEnd).Within(.001f));
            Assert.That(game.UiSnapshot.chargeWindowEnd, Is.GreaterThan(game.UiSnapshot.chargeWindowStart), "the HUD snapshot carries the window");
                                                                                                   
            blast.reload = blast.reloadTotal * (1f - tuning.scatterQuickStart * .5f);
            float hp = targets[0].Hp;
            game.runWeapons.HandleInput(true, true, false);
            blast.reload = blast.reloadTotal * (1f - inside);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(blast.reload, Is.GreaterThan(0f), "an early press wastes this reload's chance");
            Assert.That(targets[0].Hp, Is.EqualTo(hp));
            Assert.That(game.runWeapons.ChargeWindow(out _, out _), Is.False, "a spent chance takes the window off the bar");

            blast.reload = 0f; blast.cooldown = 0f; blast.rounds = 1;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(blast.quickReloadTried, Is.False, "a new reload brings a new chance");
            hp = targets[0].Hp;
            blast.reload = blast.reloadTotal * (1f - inside);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(blast.reload, Is.Zero, "inside the window the reload finishes at once");
            Assert.That(blast.rounds, Is.EqualTo(1), "both rounds loaded and the first one fired");
            Assert.That(targets[0].Hp, Is.LessThan(hp), "the press fires");
        }

                                             

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterFocusTame_Release_GuardsAgainstAttacksForTheChargedTimeButNotTicks()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var tuning = game.runWeapons.upgrades;
            player.enabled = true;                          
            player.ClearWaveState();                                      
            player.DebugSetAim(Vector3.back);                                                  
            var focus = Equip(game, WeaponForm.ScatterCannon, 1, UpgradeAxis.Tame);

            game.runWeapons.HandleInput(true, true, false);
            focus.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);
            float guarded = Time.time;
            Assert.That(player.Protected, Is.True);
            Assert.That(game.runWeapons.FocusGuardActive, Is.True, "the model shows the guard");
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, targets[0])), Is.False, "an enemy attack is blocked");
            float hp = player.Hp;
            Assert.That(player.ReceiveDamage(new DamageInfo { amount = 1f, source = DamageSource.Burn, dealer = targets[0] }), Is.True, "a burn tick still lands");
            Assert.That(player.Hp, Is.LessThan(hp));

            await Until(() => !player.Protected, 2f, "the guard ends");
            Assert.That(Time.time - guarded, Is.EqualTo(tuning.focusGuardMax).Within(.1f), "a full charge guards for the longest time");
        }

                                

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task CardText_TwistAndTameOfTheseWeapons_ReadTheLiveTuning()
        {
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var weapons = GameManager.Instance.runWeapons;
            var tuning = weapons.upgrades;
            string saved = JsonUtility.ToJson(tuning);
            try
            {
                tuning.reinfuseChance = 0f;
                tuning.chargedTwinShare = .65f; tuning.chargeLineSlow = .55f; tuning.reverseSplitShare = .45f; tuning.reverseGuardRange = 3.5f;
                tuning.knifeSpinShare = .75f; tuning.knifeCatchTargets = 3; tuning.strikeChainShare = .35f; tuning.strikeJumpDistance = 5.5f;
                tuning.rocketCueMax = 9f; tuning.scatterVacuumShare = .55f; tuning.scatterQuickStart = .65f; tuning.focusPierceRetention = .5f; tuning.focusGuardMax = .35f;
                var expected = new (WeaponForm form, int variant, UpgradeAxis axis, string number)[]
                {
                    (WeaponForm.Rifle, 0, UpgradeAxis.Twist, "65"), (WeaponForm.Rifle, 0, UpgradeAxis.Tame, "45"),
                    (WeaponForm.Rifle, 1, UpgradeAxis.Twist, "45"), (WeaponForm.Rifle, 1, UpgradeAxis.Tame, "3.5"),
                    (WeaponForm.Knife, 0, UpgradeAxis.Twist, "75"), (WeaponForm.Knife, 0, UpgradeAxis.Tame, "3"),
                    (WeaponForm.Knife, 1, UpgradeAxis.Twist, ""), (WeaponForm.Knife, 1, UpgradeAxis.Tame, ""),
                    (WeaponForm.RocketLauncher, 0, UpgradeAxis.Twist, "35"), (WeaponForm.RocketLauncher, 0, UpgradeAxis.Tame, "5.5"),
                    (WeaponForm.RocketLauncher, 1, UpgradeAxis.Twist, "9"), (WeaponForm.RocketLauncher, 1, UpgradeAxis.Tame, ""),
                    (WeaponForm.ScatterCannon, 0, UpgradeAxis.Twist, "55"), (WeaponForm.ScatterCannon, 0, UpgradeAxis.Tame, "65"),
                    (WeaponForm.ScatterCannon, 1, UpgradeAxis.Twist, "50"), (WeaponForm.ScatterCannon, 1, UpgradeAxis.Tame, "0.35"),
                };
                foreach (var (form, variant, axis, number) in expected)
                {
                                                                         
                    var open = UpgradeAxis.Infusion | (axis == UpgradeAxis.Twist ? UpgradeAxis.Tame : UpgradeAxis.Twist);
                    weapons.Hand = new[] { new RunWeapon { form = form, variant = variant, revealed = true, axes = open }, null };
                    weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
                    var card = weapons.RollUpgradeCard();
                    Assert.That(card, Is.Not.Null);
                    Assert.That(((WeaponUpgradeOffer)card.Payload).Axis, Is.EqualTo(axis));
                    string text = card.Description;
                    Assert.That(text, Is.Not.Empty, $"{form} {variant} {axis}");
                    Assert.That(text, Does.Not.Contain("{"), $"{form} {variant} {axis}: every token is filled");
                    Assert.That(text, Does.Contain(number), $"{form} {variant} {axis}: the card shows the tuned number");
                }
            }
            finally { JsonUtility.FromJsonOverwrite(saved, tuning); }
        }
    }
}
