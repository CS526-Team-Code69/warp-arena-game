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
        public async Task ChargedShotTwist_FullChargeRelease_BlastsTheConeAndTheRoundGrowsPerEnemyPierced()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            Assert.That((tuning.chargedBlastAngle, tuning.chargedBlastRange, tuning.chargedBlastPush), Is.EqualTo((60f, 3f, 6f)), "Q2 A: 60°, 3 cells, 6 cells");
            Assert.That((tuning.chargedPierceGrowth, tuning.chargedPierceGrowthCap), Is.EqualTo((.2f, 1f)), "Q3 B: +20% each, at most +100%");
            var rifle = Equip(game, WeaponForm.Rifle, 0, UpgradeAxis.Twist);
            float hit = game.runWeapons.HitDamage(rifle);

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(rifle.charging, Is.True);
            rifle.charge = 1f;
            game.runWeapons.HandleInput(false, false, true);
            Assert.That(Actors(rifle, AttackMode.Bolt).Count(), Is.EqualTo(1), "one round, none out of your back any more");
                                                                                                                       
            Assert.That(targets[0].State, Is.EqualTo(CharacterState.Knocked), "the muzzle blast knocks the close enemy back");
            Assert.That(targets[0].Hp, Is.EqualTo(TargetHp), "the blast deals no damage");
            Assert.That(targets[1].State, Is.EqualTo(CharacterState.Normal), "the blast is short: 6 cells ahead is out of it");

            await Until(() => !Actors(rifle, AttackMode.Bolt).Any(), 3f, "the round flies its range");
            for (int i = 0; i < targets.Length; i++)
                Assert.That(targets[i].Hp, Is.EqualTo(TargetHp - hit * (1f + Mathf.Min(tuning.chargedPierceGrowthCap, tuning.chargedPierceGrowth * i))).Within(.01f),
                    $"enemy {i + 1} on the line takes the first hit plus {i} x the growth");

                                                        
            var targetHp = targets.Select(t => t.Hp).ToArray();
            foreach (var t in targets) t.SetState(CharacterState.Normal);
            rifle.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            rifle.charge = .5f;
            var d = game.runWeapons.roster.Get(WeaponForm.Rifle);
            float half = Mathf.Lerp(Mathf.Clamp(game.runWeapons.roster.RifleMinimumChargeDamage, 0f, d.damageA), d.damageA, .5f) * hit / d.damageA;
            game.runWeapons.HandleInput(false, false, true);
            Assert.That(targets[0].State, Is.EqualTo(CharacterState.Normal), "no blast below a full charge");
            await Until(() => !Actors(rifle, AttackMode.Bolt).Any(), 3f, "the partial round flies its range");
            for (int i = 0; i < targets.Length; i++)
                Assert.That(targets[i].Hp, Is.EqualTo(targetHp[i] - half).Within(.01f), "no growth below a full charge");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedShotTame_Charging_SlowsEnemiesOnTheAimLineNotYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var rifle = Equip(game, WeaponForm.Rifle, 0, UpgradeAxis.Tame);
            float slow = game.runWeapons.upgrades.chargeLineSlowFactor;
            Assert.That(slow, Is.EqualTo(.3f), "Q5 B: enemies on the line move 70% slower (the renamed field keeps its code default in the scene)");
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
        public async Task ReverseShotTame_EnemyCloseInFront_EveryOtherRoundGoesForItAndTheRecoilPushesYouForward()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            Assert.That((tuning.reverseGuardRange, tuning.reverseGuardAngle, tuning.reverseTameRecoil), Is.EqualTo((3f, 120f, .7f)), "Q9 A and Q7 A numbers");
                                                                                                
            var rifle = Equip(game, WeaponForm.Rifle, 1, UpgradeAxis.Tame);
            var directions = new List<Vector3>();
            for (int shot = 0; shot < 3; shot++)
            {
                rifle.cooldown = rifle.reload = 0f;
                game.runWeapons.HandleInput(true, true, false);
                directions.Add(Actors(rifle, AttackMode.Bolt).OrderByDescending(a => a.Sequence).First().direction);
            }
            Assert.That(Vector3.Dot(directions[0], Vector3.forward), Is.GreaterThan(.99f), "the 1st round goes for the enemy in front");
            Assert.That(Vector3.Dot(directions[1], Vector3.back), Is.GreaterThan(.99f), "the 2nd still leaves from your back");
            Assert.That(Vector3.Dot(directions[2], Vector3.forward), Is.GreaterThan(.99f), "the 3rd goes for it again");
            await Until(() => targets[0].Hp < TargetHp, 2f, "a forward round hits the enemy in front");

                                                                        
            Place(targets[0], targets[0].Position + Vector3.forward * 8f);
            rifle.cooldown = rifle.reload = 0f; rifle.rounds = game.runWeapons.ClipSize(WeaponForm.Rifle, 1);
            game.runWeapons.HandleInput(true, true, false);
            var plain = Actors(rifle, AttackMode.Bolt).OrderByDescending(a => a.Sequence).First();
            Assert.That(Vector3.Dot(plain.direction, Vector3.back), Is.GreaterThan(.99f), "with nobody close in front the round leaves from your back");

                                                                                                               
            foreach (var t in targets) t.DiscardWithoutReward();
            var player = game.player;
            player.CancelDisplacement();                                                                                  
            player.enabled = true;                      
            Vector3 start = player.Position;
            float hp = player.Hp;
            rifle.rounds = game.runWeapons.ClipSize(WeaponForm.Rifle, 1);
            for (int shot = 0; shot < 3; shot++) { rifle.cooldown = rifle.reload = 0f; game.runWeapons.HandleInput(true, true, false); }
            bool knocked = false;
            float until = Time.time + .8f;
            while (Time.time < until) { knocked |= player.State == CharacterState.Knocked; await Awaitable.NextFrameAsync(); }
            float moved = Vector3.Dot(player.Position - start, Vector3.forward);
            Assert.That(moved, Is.EqualTo(3f * tuning.reverseTameRecoil).Within(.5f), "three rounds push you about three times the recoil");
            Assert.That(knocked, Is.False, "a push, never a knockback");
            Assert.That(player.Hp, Is.EqualTo(hp));
        }

                                             

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnifeTwist_KnifeComesHome_SpinsAndCutsEnemiesAroundTheThrowPoint()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 home = game.player.Position;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
                                                                                                                                          
                                                                       
            game.player.DebugSetAim(Vector3.back);
            Place(targets[0], home + Vector3.forward * (.6f + targets[0].radius));
            float playerHp = game.player.Hp;
            var knife = Equip(game, WeaponForm.Knife, 0, UpgradeAxis.Twist);
            float spin = game.runWeapons.HitDamage(knife) * game.runWeapons.upgrades.knifeSpinShare;

            game.runWeapons.HandleInput(true, true, false);
            var actor = Actors(knife, AttackMode.Knife).Single();
            float cut = actor.ownerDamage * game.runWeapons.StartingFormDamageFactor(WeaponForm.Knife);                                                                                       
            Assert.That(Query.Flat(actor.KnifeHomePoint - home).magnitude, Is.LessThan(.05f), "the throw point is known before the return (marker)");
            await Until(() => !knife.inFlight, 4f, "the knife comes home");

            Assert.That(targets[0].Hp, Is.EqualTo(TargetHp - spin).Within(.01f), "the spin cuts once at its share");
                                                                                                                                         
            Assert.That(playerHp - game.player.Hp, Is.EqualTo(cut).Within(.01f), "only the return's own cut, nothing from the spin");

            float after = targets[0].Hp;
            var plain = Equip(game, WeaponForm.Knife, 0);
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !plain.inFlight, 4f, "the plain knife comes home");
            Assert.That(targets[0].Hp, Is.EqualTo(after), "without Twist there is no spin");
        }

                                                                                                                                              
                                                                                                                                           
        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnifeTame_ReturnThroughYou_IsCaughtWithHalfTheCooldownAndANextThrowThatBouncesMoreAndFliesFaster()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            Assert.That((tuning.knifeCatchCooldownShare, tuning.knifeCatchSpeed), Is.EqualTo((.5f, 1.3f)), "half the cooldown (document), Q14 A x1.3");
            foreach (var t in targets) t.DiscardWithoutReward();
            Vector3 home = game.player.Position;
            var knife = Equip(game, WeaponForm.Knife, 0, UpgradeAxis.Tame);
            float playerHp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            var actor = Actors(knife, AttackMode.Knife).Single();
            float outbound = actor.speed;
            Vector3 lastSeen = actor.transform.position;
            while (knife.inFlight)
            {
                if (actor.gameObject.activeSelf) lastSeen = actor.transform.position;
                await Awaitable.NextFrameAsync();
            }

            Assert.That(game.player.Hp, Is.EqualTo(playerHp), "the tamed return spares you");
            Assert.That(knife.knifeCaught, Is.True, "the return swept through you: a catch");
            Assert.That(Query.Flat(lastSeen - home).magnitude, Is.GreaterThan(.2f), "caught on the way in, before it reached the throw point");
            Assert.That(knife.cooldown, Is.LessThanOrEqualTo(game.runWeapons.ReturnCooldown(knife) * tuning.knifeCatchCooldownShare + .001f), "half the usual cooldown");
            Assert.That(game.runWeapons.AnomalySlotDetail(knife), Does.Contain("+" + tuning.knifeCatchTargets), "the hand slot shows the catch");

            knife.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            var next = Actors(knife, AttackMode.Knife).Single();
            Assert.That(next.targetsLeft, Is.EqualTo(4 + tuning.knifeCatchTargets), "the next throw bounces to more targets");
            Assert.That(next.speed, Is.EqualTo(outbound * tuning.knifeCatchSpeed).Within(.001f), "and flies out faster");
            Assert.That(next.knifeSpeedFactor, Is.EqualTo(tuning.knifeCatchSpeed).Within(.001f), "and back faster");
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
            Vector3 beforeChain = game.player.Position;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => hook.hooked == targets[1], 3f, "the crash chains the hook onto the enemy it hit");

            Assert.That(targets[1].Hp, Is.LessThan(TargetHp), "the crash still deals its damage first");
            Assert.That(hook.hookChainUsed, Is.True);
                                                                                                                        
            Assert.That(Vector3.Dot(game.player.Position - beforeChain, Vector3.forward), Is.GreaterThan(.5f), "the chained pull drags you as well");

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(hook.hooked, Is.Null, "the chained enemy can be flung again");
            game.runWeapons.ChainHook(hook, targets[0]);
            Assert.That(hook.hookBusy, Is.False, "no second chain from the same manual hook");

            hook.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(hook.hookChainUsed, Is.False, "a manual hook resets the chain");
        }

                                                                                                                                           
                                                                       
        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HookTame_EmptyHookOnAWall_PullsYouToItAndNoHookInFlightHoldsYouStill()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var player = game.player;

            var plain = Equip(game, WeaponForm.Knife, 1);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(game.runWeapons.HandHookBusy, Is.True);
            Assert.That(player.MovementLocked, Is.False, "the untamed hook in flight lets you move too");
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
        public async Task LaunchYourselfTwist_EnemiesHit_AreKnockedBackFartherTheFasterTheMissileStillFlies()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;
            var tuning = game.runWeapons.upgrades;
            targets[2].DiscardWithoutReward();
            Place(targets[1], origin + Vector3.forward * 15f);
            var knocks = new Dictionary<Combatant, float>();
            foreach (var t in targets.Take(2)) { var body = t; body.AttackReceived += info => knocks[body] = info.knockDistance; }
            var rocket = Equip(game, WeaponForm.RocketLauncher, 1, UpgradeAxis.Twist);
            float range = game.runWeapons.roster.Get(WeaponForm.RocketLauncher).rangeB * rocket.Reach;
                                                                                                                                                
            float Expected(Enemy target)
            {
                float flown = Vector3.Dot(target.Position - origin, Vector3.forward) - (target.radius + game.player.radius + .3f);
                return tuning.rocketCueMax * Mathf.Lerp(1f, tuning.rocketEndSpeedShare, flown / range) * game.runWeapons.UpgradePushScale;
            }
            float near = Expected(targets[0]), far = Expected(targets[1]);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => game.player.State != CharacterState.Dashing, 3f, "the flight ends");

            Assert.That(knocks.ContainsKey(targets[0]) && knocks.ContainsKey(targets[1]), Is.True, "both enemies were hit");
            Assert.That(knocks[targets[0]], Is.EqualTo(near).Within(.05f), "early in the flight the missile is fast and pushes far");
            Assert.That(knocks[targets[1]], Is.EqualTo(far).Within(.05f), "late in the flight the slower missile pushes less");
            Assert.That(knocks[targets[0]], Is.GreaterThan(knocks[targets[1]]), "the faster the missile still flies, the farther the push");
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
        public async Task ScatterBlastTame_StandingStill_TheShellsLandOutOfReach_TheReloadRunsItsCourse()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var blast = Equip(game, WeaponForm.ScatterCannon, 0, UpgradeAxis.Tame);
            blast.rounds = 1;
            game.runWeapons.HandleInput(true, true, false);
            float fired = Time.time;
            while (Time.time < fired + tuning.scatterShellFuse + .3f) await Awaitable.NextFrameAsync();

            Assert.That(tuning.scatterShellTameSide, Is.GreaterThan(tuning.scatterShellRadius * tuning.scatterShellTameRadius),
                "a tamed shell fire does not reach where you stand (owner decision 10-27)");
            Assert.That(blast.reload, Is.GreaterThan(0f), "standing still does not finish the reload");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterBlastTame_StepOnABurningShellWhileReloading_ReloadsAtOnceAndSpeedsYouUp()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            var blast = Equip(game, WeaponForm.ScatterCannon, 0, UpgradeAxis.Tame);
            Vector3 origin = player.Position, right = Vector3.Cross(Vector3.up, Vector3.forward);
            float hp = player.Hp;

            blast.rounds = 1;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(blast.reload, Is.GreaterThan(0f), "the clip ran dry");
            Assert.That(game.runWeapons.Charge01, Is.Zero, "the charge bar no longer shows the reload");
            Assert.That(game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.ScatterShell), Is.EqualTo(2), "the tamed shells still land");
                                                                                                                    
            V3CombatSmokeTest.Teleport(player, origin - right * tuning.scatterShellTameSide);
            Physics.SyncTransforms();
            float fired = Time.time;
            while (Time.time < fired + tuning.scatterShellFuse * .6f)
            {
                Assert.That(blast.reload, Is.GreaterThan(0f), "the fuse is still running");
                await Awaitable.NextFrameAsync();
            }
            await Until(() => blast.reload <= 0f, 1f, "the shell flares up under you");
            Assert.That(Time.time - fired, Is.LessThan(tuning.scatterReload * .7f), "long before the reload would have ended");
            Assert.That(blast.rounds, Is.EqualTo(2), "both rounds loaded at once");
            Assert.That(blast.cooldown, Is.Zero, "and it fires at once: the last shot's 1 s cooldown is cut too");
            Assert.That(player.ExternalMoveMultiplier, Is.EqualTo(tuning.scatterShellTameSpeed).Within(.001f), "and you move faster");
            var fire = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == blast && a.mode == AttackMode.Field);
            Assert.That(fire.radius, Is.EqualTo(tuning.scatterShellRadius * tuning.scatterShellTameRadius).Within(.001f), "the tamed fire is wider");

                                                                                                                             
            await Seconds(.5f);
            Assert.That(game.runWeapons.ShellStrideLeft, Is.LessThan(tuning.scatterShellTameSpeedSeconds - .3f), "once per reload");
            Assert.That(player.Hp, Is.EqualTo(hp), "the tamed shell never burns you");
            await Until(() => game.runWeapons.ShellStrideLeft <= 0f, tuning.scatterShellTameSpeedSeconds + 1f, "the speed bonus runs out");
            await Awaitable.NextFrameAsync();
            Assert.That(player.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "back to your own pace");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ScatterBlastTame_TakenWhileYouStandInABurningShell_StopsItBurningYouFromItsNextTick()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            var blast = Equip(game, WeaponForm.ScatterCannon, 0);
            Vector3 origin = player.Position, right = Vector3.Cross(Vector3.up, Vector3.forward);
            blast.rounds = 1;
            game.runWeapons.HandleInput(true, true, false);
            V3CombatSmokeTest.Teleport(player, origin - right * tuning.scatterShellSide);
            Physics.SyncTransforms();
            float hp = player.Hp;
            await Until(() => player.Hp < hp, 2f, "the untamed shell burns you");
            try
            {
                                                                                                                                         
                game.enabled = false;
                blast.axes |= UpgradeAxis.Tame;
                hp = player.Hp;
                await Seconds(.8f);
                Assert.That(player.Hp, Is.EqualTo(hp), "the tamed shell no longer burns you, though it was lit before the card");
            }
            finally { game.enabled = true; }
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
                tuning.chargedPierceGrowth = .25f; tuning.chargeLineSlowFactor = .35f; tuning.reverseSplitShare = .45f; tuning.reverseGuardRange = 3.5f;
                tuning.knifeSpinShare = .75f; tuning.knifeCatchTargets = 3; tuning.strikeChainShare = .35f; tuning.strikeJumpDistance = 5.5f; tuning.hookShockPush = 4.5f;
                tuning.rocketCueMax = 9f; tuning.scatterVacuumShare = .55f; tuning.scatterShellTameSpeed = 1.35f; tuning.focusPierceRetention = .5f; tuning.focusGuardMax = .35f;
                var expected = new (WeaponForm form, int variant, UpgradeAxis axis, string number)[]
                {
                    (WeaponForm.Rifle, 0, UpgradeAxis.Twist, "25"), (WeaponForm.Rifle, 0, UpgradeAxis.Tame, "65"),
                    (WeaponForm.Rifle, 1, UpgradeAxis.Twist, "45"), (WeaponForm.Rifle, 1, UpgradeAxis.Tame, "3.5"),
                    (WeaponForm.Knife, 0, UpgradeAxis.Twist, "75"), (WeaponForm.Knife, 0, UpgradeAxis.Tame, "3"),
                    (WeaponForm.Knife, 1, UpgradeAxis.Twist, ""), (WeaponForm.Knife, 1, UpgradeAxis.Tame, "4.5"),
                    (WeaponForm.RocketLauncher, 0, UpgradeAxis.Twist, "35"), (WeaponForm.RocketLauncher, 0, UpgradeAxis.Tame, "5.5"),
                    (WeaponForm.RocketLauncher, 1, UpgradeAxis.Twist, "9"), (WeaponForm.RocketLauncher, 1, UpgradeAxis.Tame, ""),
                    (WeaponForm.ScatterCannon, 0, UpgradeAxis.Twist, "55"), (WeaponForm.ScatterCannon, 0, UpgradeAxis.Tame, "1.35"),
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
