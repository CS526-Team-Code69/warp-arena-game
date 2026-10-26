using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class WeaponBaseFixesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant, UpgradeAxis axes = UpgradeAxis.None, int mk = 1)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, axes = axes, mk = mk, rounds = game.runWeapons.ClipSize(form, variant) };

            weapon.element = form switch { WeaponForm.Flamethrower => ElementKind.Fire, WeaponForm.CryoCannon => ElementKind.Frost, WeaponForm.ArcCoil => ElementKind.Lightning, WeaponForm.SporeLauncher => ElementKind.Poison, _ => ElementKind.None };
            weapon.builtInElement = weapon.element != ElementKind.None;
            if (weapon.builtInElement) weapon.axes |= UpgradeAxis.Infusion;
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        static RunWeapon EquipAuto(GameManager game, WeaponForm form, int variant)
        {
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true };
            game.runWeapons.Auto[0] = weapon;
            return weapon;
        }

        static float Scale(GameManager game, RunWeapon w)
        {
            var d = game.runWeapons.roster.Get(w.form);
            return game.runWeapons.HitDamage(w) / (w.variant == 0 ? d.damageA : d.damageB);
        }

        static float TakenFactor(GameManager game) => game.progression.Stats.DamageTakenMultiplier;

        static void WallAhead(GameManager game, float distance, float width = 4f)
        {
            WorldMap.Active.CreateWall(game.player.Position + Query.Flat(game.player.AimDirection).normalized * distance, width, 60f, 0f, game.player);
            Physics.SyncTransforms();
        }

        static void ClearWalls()
        {
            foreach (var wall in WorldMap.Active.temporaryWalls) if (wall && wall.gameObject.activeSelf) wall.gameObject.SetActive(false);
            Physics.SyncTransforms();
        }

        static void Place(Enemy enemy, Vector3 at, bool movable = false)
        {
            V3CombatSmokeTest.Teleport(enemy, at);
            if (movable) enemy.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            Physics.SyncTransforms();
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

        static bool ActiveActors(RunWeapon w, AttackMode mode) =>
            AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == w && a.mode == mode);

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SelfHit_OwnAttack_IgnoresProtectionGrantsNoneAndAddsNoStacks()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player; var status = ElementStatus.For(player);
            status.ClearStatuses();
            float hp = player.Hp;
            var self = DamageInfo.Attack(2f, player); self.element = ElementKind.Fire;

            Assert.That(player.ReceiveDamage(self), Is.True);
            Assert.That(player.Hp, Is.EqualTo(hp - 2f * TakenFactor(game)).Within(.01f), "a self-hit still hurts");
            Assert.That(player.Protected, Is.False, "a self-hit grants no hit protection");
            Assert.That(status.Fire, Is.Zero, "a self-hit adds no element stacks");

            player.ProtectFor(5f);
            hp = player.Hp;
            Assert.That(player.ReceiveDamage(self), Is.True, "protection does not block a self-hit");
            Assert.That(player.Hp, Is.LessThan(hp));
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, targets[0])), Is.False, "protection still blocks enemies");

            player.ClearWaveState();
            Assert.That(player.ReceiveDamage(DamageInfo.Attack(1f, targets[0])), Is.True);
            Assert.That(player.Protected, Is.True, "an enemy hit still grants protection");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Wildfire_PatchAtYourFeet_BurnsYouAtTheBaseShareOfItsTicks()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player; var status = ElementStatus.For(player);
            var wildfire = Equip(game, WeaponForm.Flamethrower, 1);
            status.ClearStatuses();
            player.DebugSetAimPoint(player.Position);
            Assume.That(TakenFactor(game), Is.EqualTo(1f).Within(.001f), "starting stats take damage as dealt");
            float hp = player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => player.Hp < hp, 2f, "the patch lands under the player and burns");
            float first = player.Hp;
            Assert.That(player.Protected, Is.False, "the own patch grants no protection");

            float share = game.runWeapons.roster.Get(WeaponForm.Flamethrower).damageB * game.runWeapons.upgrades.wildfireSelfShare;
            Assume.That(share, Is.LessThan(1f), "a share below the floor is the case the carry is for");
            await Seconds(2f);
            float lost = first - player.Hp;
            Assert.That(lost, Is.EqualTo(8f * share).Within(1.01f), "about the base share per tick, not the floor of 1 per tick");
            Assert.That(lost, Is.LessThan(8f), "the 1-damage floor no longer rounds every tick up");
            Assert.That(status.Fire, Is.Zero, "the own fire adds no burn stacks");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReverseShot_BackAgainstAWall_BouncesOffItIntoTheLane()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.Rifle, 1);

            game.player.DebugSetAim(Vector3.back);
            WallAhead(game, game.player.radius + .5f);
            game.player.DebugSetAim(Vector3.forward);

            game.runWeapons.HandleInput(true, true, false);

            await Until(() => targets[0].Hp < targets[0].maxHp, 3f, "the round bounces off the wall behind and hits the first target ahead");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SwarmRocket_StartingInsideAnEnemy_BurstsOnThatEnemy()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var rocket = AttackPool.Instance.Acquire();
            Assert.That(rocket, Is.Not.Null);
            rocket.Configure(AttackMode.Bolt, game.player, new RunWeapon { form = WeaponForm.SwarmLauncher, variant = 0, revealed = true },
                targets[0].Position, Vector3.forward, 10f, 0f, 15f, 15f, 0f, 3f);
            rocket.mode = AttackMode.Swarm; rocket.radius = 1.5f;

            await Until(() => !rocket.gameObject.activeSelf, 1f, "the rocket bursts");

            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp - 10f).Within(.01f), "a start overlap bursts where the rocket is, not at the map centre");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Bombard_StandingStill_FirstBlastWaitsAndMissesYouAndStopsAtAWall()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var bombard = Equip(game, WeaponForm.SwarmLauncher, 1);
            float hp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            var blasts = AttackPool.Instance.actors.Where(a => a && a.gameObject.activeSelf && a.weapon == bombard && a.mode == AttackMode.Blast).ToArray();
            Assert.That(blasts, Has.Length.EqualTo(tuning.bombardBlasts), "an open lane gets every blast");
            Assert.That(blasts.Min(a => a.delay), Is.GreaterThanOrEqualTo(tuning.bombardFuse - .001f), "even the first blast waits for its fuse");
            Assert.That(blasts.Min(a => Query.Flat(a.point - game.player.Position).magnitude), Is.EqualTo(tuning.bombardStart).Within(.01f));
            float far = blasts.Max(a => Query.Flat(a.point - game.player.Position).magnitude);
            Assert.That(far, Is.EqualTo(game.runWeapons.roster.Get(WeaponForm.SwarmLauncher).rangeB).Within(.01f), "the last blast sits at the range");
            await Until(() => !ActiveActors(bombard, AttackMode.Blast), 3f, "all blasts go off");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "standing still is safe");
            Assert.That(targets[0].Hp, Is.LessThan(targets[0].maxHp), "the line hits the first target");

            WallAhead(game, 5f);
            bombard.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            blasts = AttackPool.Instance.actors.Where(a => a && a.gameObject.activeSelf && a.weapon == bombard && a.mode == AttackMode.Blast).ToArray();
            Assert.That(blasts.Length, Is.InRange(1, tuning.bombardBlasts - 1), "blasts past the wall are not placed");
            Assert.That(blasts.Max(a => Query.Flat(a.point - game.player.Position).magnitude), Is.LessThan(4.5f), "none beyond the wall face");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainCoil_NobodyInTheCone_DischargesNothing()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var chain = Equip(game, WeaponForm.ArcCoil, 0);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();

            Place(targets[0], game.player.Position + Vector3.right * 2f);
            game.player.DebugSetAim(Vector3.back);

            game.runWeapons.HandleInput(true, true, false);

            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp), "no backlash on the enemy beside you");
            Assert.That(status.Lightning, Is.Zero, "no current returns to you");
            Assert.That(chain.cooldown, Is.GreaterThan(0f), "the dry click still costs the cooldown");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainCoil_ThreeInALane_HitsEachOnceDrawsArcsAndShocksNearbyPerUnusedJump()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var chain = Equip(game, WeaponForm.ArcCoil, 0);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();
            float hit = game.runWeapons.HitDamage(chain), scale = Scale(game, chain);

            game.runWeapons.HandleInput(true, true, false);

            Assert.That(targets[1].Hp, Is.EqualTo(targets[1].maxHp - hit).Within(.01f), "no element jump adds damage on top of the chain");
            Assert.That(targets[2].Hp, Is.EqualTo(targets[2].maxHp - hit).Within(.01f));
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp - hit - tuning.chainReturnDamagePerJump * 2f * scale).Within(.01f), "1 per unused jump");
            Assert.That(status.Lightning + (status.Paralysed ? 99f : 0f), Is.EqualTo(2f).Within(.01f), "one Lightning per unused jump on you");
            Assert.That(game.runWeapons.Tracers.Count, Is.EqualTo(3), "an arc to each target");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainCoil_FourUnusedJumps_ShockCountsThreeAtMost()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var chain = Equip(game, WeaponForm.ArcCoil, 0);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();
            targets[2].DiscardWithoutReward();

            V3CombatSmokeTest.Teleport(game.player, game.player.Position + Vector3.forward * 4f);
            Place(targets[0], game.player.Position + Vector3.forward * 5f);
            Place(targets[1], game.player.Position + Vector3.back * 2f);

            game.runWeapons.HandleInput(true, true, false);

            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp - game.runWeapons.HitDamage(chain)).Within(.01f), "the arc hits the target in the cone");
            Assert.That(targets[1].Hp, Is.EqualTo(targets[1].maxHp - tuning.chainReturnDamagePerJump * tuning.chainReturnMaxJumps * Scale(game, chain)).Within(.01f),
                "four unused jumps shock for three");
            Assert.That(status.Lightning, Is.EqualTo(4f).Within(.01f), "your own Lightning still counts every unused jump");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LightningHit_NoJumpFlag_AddsStacksWithoutJumping()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Place(targets[1], targets[0].Position + Vector3.right * 1.5f);
            var info = DamageInfo.Attack(5f, game.player); info.element = ElementKind.Lightning; info.noLightningJump = true;

            targets[0].ReceiveDamage(info);
            Assert.That(ElementStatus.For(targets[0]).Lightning, Is.GreaterThan(0f), "the hit still adds Lightning");
            Assert.That(targets[1].Hp, Is.EqualTo(targets[1].maxHp), "no element jump to the neighbour");

            info.noLightningJump = false;
            targets[0].ReceiveDamage(info);
            Assert.That(targets[1].Hp, Is.LessThan(targets[1].maxHp), "an ordinary Lightning hit still jumps");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnife_SwallowedByAHole_ComesBackToItsSlotAfterTheWait()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            float wait = tuning.knifeHoleReturn;
            try
            {
                tuning.knifeHoleReturn = .6f;
                var knife = Equip(game, WeaponForm.Knife, 0);
                int slot = game.runWeapons.ActiveIndex;
                game.runWeapons.HandleInput(true, true, false);
                var actor = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == knife);

                actor.SwallowedByHole();

                Assert.That(game.runWeapons.Hand[slot], Is.SameAs(knife), "the knife stays in its slot");
                Assert.That(knife.inFlight, Is.False);
                Assert.That(knife.cooldown, Is.EqualTo(.6f).Within(.02f), "it is back after the wait");
                Assert.That(game.runWeapons.GetCooldownDuration(knife), Is.EqualTo(.6f).Within(.001f), "the slot shows the wait as a cooldown");
                Assert.That(game.UiSnapshot.handSlots[slot].cooldown01, Is.GreaterThan(.9f));
                await Until(() => knife.cooldown <= 0f, 2f, "the knife comes back");
                game.runWeapons.HandleInput(true, true, false);
                Assert.That(knife.inFlight, Is.True, "it can be thrown again");
            }
            finally { tuning.knifeHoleReturn = wait; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Meltdown_HighMk_HurtsEnemiesScaledButYouOnlyTheFlatBase()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var gun = Equip(game, WeaponForm.Minigun, 1, mk: 5);
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            ElementStatus.For(game.player).ClearStatuses();
            float hp = game.player.Hp, scale = Scale(game, gun);
            gun.heat = 3.95f;

            game.runWeapons.HandleInput(true, true, false);

            Assert.That(game.player.Hp, Is.EqualTo(hp - tuning.meltdownDamage * TakenFactor(game)).Within(.01f), "you take the flat 20");
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp - tuning.meltdownDamage * scale).Within(.01f), "enemies take the scaled blast");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Strike_AtYourOwnFeet_HurtsYouTheBaseValue()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.RocketLauncher, 0, mk: 4);
            game.player.DebugSetAimPoint(game.player.Position);
            float hp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => game.player.Hp < hp, 2f, "the strike lands on the player");

            Assert.That(hp - game.player.Hp, Is.EqualTo(game.runWeapons.roster.Get(WeaponForm.RocketLauncher).damageA * TakenFactor(game)).Within(.01f),
                "the wielder takes damageA, not the Mk-scaled hit");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Whirl_SpireIsNearest_GrabsTheMovableEnemyInstead()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            targets[2].DiscardWithoutReward();
            targets[0].Configure(game.waves.DefinitionOf(EnemyKind.Spire), 1f, false);
            targets[0].enabled = false;
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            Place(targets[1], game.player.Position + Vector3.forward * 4f, movable: true);
            Assume.That(targets[0].Immovable, Is.True);
            var whirl = EquipAuto(game, WeaponForm.SentryDrone, 1);

            await Until(() => whirl.whirled, 2f, "the drone grabs someone");

            Assert.That(whirl.whirled, Is.SameAs(targets[1]), "the immovable Spire is skipped");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Whirl_NoMovableEnemyInRange_LooksAgainAfterTheRetry()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            targets[0].Configure(game.waves.DefinitionOf(EnemyKind.Spire), 1f, false);
            targets[0].enabled = false;
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            var whirl = EquipAuto(game, WeaponForm.SentryDrone, 1);

            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();

            Assert.That(whirl.whirled, Is.Null);
            Assert.That(whirl.cooldown, Is.GreaterThan(0f).And.LessThanOrEqualTo(game.runWeapons.upgrades.whirlRetry), "it waits before looking again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Whirl_WallInTheOrbit_EndsEarlyAndLeavesTheBodyOnValidGround()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            Place(targets[0], game.player.Position + Vector3.back * 2f, movable: true);
            WallAhead(game, 2f, 8f);
            var whirl = EquipAuto(game, WeaponForm.SentryDrone, 1);

            await Until(() => whirl.whirled, 2f, "the drone grabs the enemy");
            float grabbed = Time.time;
            await Until(() => !whirl.whirled, 3f, "the whirl ends");
            await Awaitable.FixedUpdateAsync();

            Assert.That(Time.time - grabbed, Is.LessThan(game.runWeapons.whirlDuration - .05f), "the wall ends the whirl early");
            Assert.That(RunWeapons.ValidTeleportPoint(targets[0].Position, targets[0].radius * .9f), Is.True, "the body is not left inside the wall");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Capacitor_ChargesOnlyInHandFastNearEnemiesAndDischargesByShare()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var weapons = game.runWeapons;
            var capacitor = Equip(game, WeaponForm.ArcCoil, 1);
            int hand = weapons.ActiveIndex;
            weapons.Hand[1 - hand] = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
            foreach (var t in targets) Place(t, t.Position + Vector3.forward * 8f);
            Vector3 home = game.player.Position;

            async Task Walk(int steps)
            {
                for (int i = 0; i < steps; i++)
                {
                    V3CombatSmokeTest.Teleport(game.player, home + (i % 2 == 0 ? Vector3.right : Vector3.zero));
                    await Awaitable.NextFrameAsync();
                }
                V3CombatSmokeTest.Teleport(game.player, home);
                await Awaitable.NextFrameAsync();
            }

            weapons.Switch();
            await Walk(4);
            Assert.That(capacitor.charge, Is.Zero, "a stowed Capacitor does not charge");
            weapons.Switch();

            await Walk(4);
            float far = capacitor.charge;
            Assert.That(far, Is.EqualTo(tuning.capacitorChargeFar * 4f).Within(.05f), "slow charge with no enemy close");

            capacitor.charge = 0f;
            Place(targets[0], home + Vector3.forward * 2f);
            await Walk(4);
            Assert.That(capacitor.charge, Is.EqualTo(tuning.capacitorChargeNear * 4f).Within(.05f), "fast charge with an enemy close");
            Assert.That(weapons.Charge01, Is.EqualTo(capacitor.charge / tuning.capacitorMax).Within(.001f), "the HUD bar shows the share of the maximum");

            float hit = weapons.HitDamage(capacitor);
            capacitor.charge = tuning.capacitorMinCharge - .5f;
            weapons.HandleInput(true, true, false);
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp), "a nearly empty click does nothing");
            Assert.That(capacitor.cooldown, Is.Zero, "and costs no cooldown");

            capacitor.charge = tuning.capacitorMax * .5f;
            float before = targets[0].Hp;
            weapons.HandleInput(true, true, false);
            Assert.That(before - targets[0].Hp, Is.EqualTo(hit * .5f).Within(.01f), "half the charge, half of damageB");

            capacitor.cooldown = 0f; capacitor.charge = tuning.capacitorMax;
            before = targets[0].Hp;
            weapons.HandleInput(true, true, false);
            Assert.That(before - targets[0].Hp, Is.EqualTo(hit * tuning.capacitorFullBonus).Within(.01f), "a full discharge hits harder");

            var status = ElementStatus.For(game.player);
            status.ClearStatuses();
            float hp = game.player.Hp;
            capacitor.cooldown = 0f; capacitor.charge = tuning.capacitorMax; capacitor.chargeFullTime = 3.98f;
            await Until(() => game.player.Hp < hp, 1f, "the full Capacitor discharges on you");
            Assert.That(hp - game.player.Hp, Is.EqualTo(tuning.capacitorSelfBlast * TakenFactor(game)).Within(.01f), "the self-discharge is the flat base");
            Assert.That(status.Lightning, Is.Zero, "your own discharge adds no Lightning");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Repulsor_EnemyInTheRing_TakesTheScaledHit()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            var ring = EquipAuto(game, WeaponForm.ShieldRing, 0);
            float hit = game.runWeapons.HitDamage(ring);
            Assume.That(hit, Is.GreaterThan(1f));

            await Until(() => targets[0].Hp < targets[0].maxHp, 2f, "the ring pulses");

            Assert.That(targets[0].maxHp - targets[0].Hp, Is.EqualTo(hit).Within(.01f), "damageA times the weapon's multiplier, not the floor of 1");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Shatter_ThreeShardsAtOneEnemy_HitItOnceAndFrozenBonusIsAboutDouble()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var shatter = Equip(game, WeaponForm.CryoCannon, 0);
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            float hit = game.runWeapons.HitDamage(shatter);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !ActiveActors(shatter, AttackMode.Shard), 2f, "the shards land");
            Assert.That(targets[0].maxHp - targets[0].Hp, Is.EqualTo(hit).Within(.01f), "one shard per enemy per shot");

            var status = ElementStatus.For(targets[0]);
            status.ClearStatuses();
            status.Add(ElementKind.Frost, status.FrostThreshold + 1f, game.player);
            Assume.That(status.Frozen, Is.True);
            float before = targets[0].Hp;
            shatter.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !ActiveActors(shatter, AttackMode.Shard), 2f, "the shards land");
            Assert.That(before - targets[0].Hp, Is.EqualTo(hit * game.runWeapons.upgrades.shatterFrozenBonus * status.Rules.frozenDamageTaken).Within(.01f),
                "own bonus times the global frozen bonus");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Blizzard_FrozenTarget_TakesOnlyTheGlobalFrozenBonus()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var blizzard = Equip(game, WeaponForm.CryoCannon, 1);
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            var status = ElementStatus.For(targets[0]);
            status.Add(ElementKind.Frost, status.FrostThreshold + 1f, game.player);
            Assume.That(status.Frozen, Is.True);

            game.runWeapons.HandleInput(true, true, false);

            Assert.That(targets[0].maxHp - targets[0].Hp, Is.EqualTo(game.runWeapons.HitDamage(blizzard) * status.Rules.frozenDamageTaken).Within(.01f));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task AreaHits_EnemyBehindAWall_IsShieldedExceptFromTheStrike()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            WallAhead(game, 1.8f);

            void Blast(WeaponForm form, int variant)
            {
                var actor = AttackPool.Instance.Acquire();
                actor.Configure(AttackMode.Blast, game.player, new RunWeapon { form = form, variant = variant, revealed = true },
                    game.player.Position, Vector3.forward, 10f, 0f, 0f, 0f, 4f);
                actor.Explode(); actor.Release();
            }

            Blast(WeaponForm.SwarmLauncher, 0);
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp), "a blast does not reach through the wall");
            Blast(WeaponForm.RocketLauncher, 0);
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp - 10f).Within(.01f), "the strike falls from the sky over the wall");
            game.player.CancelDisplacement();

            float hp = targets[0].Hp;
            Equip(game, WeaponForm.ScatterCannon, 0);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(targets[0].Hp, Is.EqualTo(hp), "Scatter Blast needs a line of sight");
            ClearWalls();
            Equip(game, WeaponForm.ScatterCannon, 0);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(targets[0].Hp, Is.LessThan(hp), "with the wall gone it hits");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task IcePatch_StandingOnIt_ChillsButNeverFreezesYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Equip(game, WeaponForm.CryoCannon, 0);
            Place(targets[0], game.player.Position + Vector3.forward * 1.3f);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.IcePatch
                && Query.Flat(h.position - game.player.Position).magnitude <= h.radius + game.player.radius), 1f, "a patch under the player");
            float most = 0f, until = Time.time + 2.5f;
            while (Time.time < until)
            {
                Assert.That(status.Frozen, Is.False, "the patch never freezes you");
                most = Mathf.Max(most, status.Frost);
                await Awaitable.NextFrameAsync();
            }
            Assert.That(most, Is.GreaterThan(3f).And.LessThanOrEqualTo(game.runWeapons.upgrades.iceFrostCap + .001f), "it chills up to the cap");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Execute_EveryShotDrawsATracer_AndHeldItMarksHpBarsAndTheSlot()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var rail = Equip(game, WeaponForm.Railgun, 1);
            game.player.DebugSetAim(Vector3.back);

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(game.runWeapons.Tracers.Count, Is.EqualTo(1), "a miss still shows its line");
            Assert.That(rail.cooldown, Is.GreaterThan(0f));
            var miss = game.runWeapons.Tracers[0];
            Assert.That(Query.Flat(miss.to - miss.from).magnitude, Is.GreaterThan(1f));

            game.player.DebugSetAim(Vector3.forward);
            rail.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            var shot = game.runWeapons.Tracers[game.runWeapons.Tracers.Count - 1];
            Assert.That(Query.Flat(shot.to - targets[0].Position).magnitude, Is.LessThan(.01f), "a hit ends on its target");

            var labels = UnityEngine.Object.FindFirstObjectByType<V2WorldLabels>();
            Assume.That(labels, Is.Not.Null);
            await Seconds(.3f);
            Assert.That(labels.ShowsExecuteLine(targets[1], out bool below), Is.True, "held Execute marks the threshold");
            Assert.That(below, Is.False);
            LeaveHp(targets[1], targets[1].maxHp * .3f);
            await Seconds(.3f);
            labels.ShowsExecuteLine(targets[1], out below);
            Assert.That(below, Is.True, "the bar turns red under the line");
            string line = $"{game.runWeapons.ExecuteThreshold(rail) * 100f:0}%";
            Assert.That(game.UiSnapshot.handSlots[game.runWeapons.ActiveIndex].detail, Does.Contain(line), "the slot shows the execute line");

            Equip(game, WeaponForm.Rifle, 0);
            await Seconds(.3f);
            Assert.That(labels.ShowsExecuteLine(targets[1], out _), Is.False, "another weapon hides it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HookHold_FacingAWall_KeepsTheEnemyOutOfIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var held = targets[0];
            Vector3 open = game.runWeapons.HookHoldPoint(held);
            Assert.That(Query.Flat(open - game.player.Position).magnitude, Is.EqualTo(game.player.radius + held.radius + .3f).Within(.01f), "open ground keeps the usual distance");

            WallAhead(game, 1.5f);

            foreach (float radius in new[] { held.radius, 1f })
            {
                held.radius = radius;
                Vector3 point = game.runWeapons.HookHoldPoint(held);
                Assert.That(Physics.CheckSphere(Query.AtCastHeight(point), held.radius * .95f, GameManager.WallMask, QueryTriggerInteraction.Ignore), Is.False,
                    $"the held enemy (radius {radius}) stays in front of the wall");
            }
            await Awaitable.NextFrameAsync();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Wildfire_AimedIntoAWall_ThrowsToTheNearestFreeSpot()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var wildfire = Equip(game, WeaponForm.Flamethrower, 1);
            WallAhead(game, 5f);
            game.player.DebugSetAimPoint(game.player.Position + Vector3.forward * 5f);

            game.runWeapons.HandleInput(true, true, false);

            var lob = AttackPool.Instance.actors.FirstOrDefault(a => a && a.gameObject.activeSelf && a.weapon == wildfire);
            Assert.That(lob, Is.Not.Null, "the fireball is still thrown");
            Assert.That(wildfire.cooldown, Is.GreaterThan(0f));
            float landing = Query.Flat(lob.point - game.player.Position).magnitude;
            Assert.That(landing, Is.InRange(3.5f, 4.5f), "it lands just in front of the wall");
            await Awaitable.NextFrameAsync();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Gunner_EnemiesBehindAWall_HoldFireThenShotsCostTwoEnergy()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;

            game.progression.UnlockEnergy();
            WallAhead(game, 1.5f, 6f);
            var gunner = EquipAuto(game, WeaponForm.SentryDrone, 0);

            await Seconds(.4f);
            Assert.That(ActiveActors(gunner, AttackMode.Bolt), Is.False, "no shot at enemies it cannot see");
            Assert.That(gunner.cooldown, Is.Zero, "it did not fire");

            ClearWalls();
            float energy = game.progression.Energy;
            await Until(() => ActiveActors(gunner, AttackMode.Bolt), 2f, "the drone fires once it can see");
            Assert.That(game.progression.Energy, Is.EqualTo(energy - game.runWeapons.upgrades.gunnerEnergyCost).Within(.01f), "each shot costs 2 Energy");
            Assert.That(game.runWeapons.upgrades.gunnerEnergyCost, Is.EqualTo(2f));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task UpgradeCards_ChangeElementOnlyStandsInForAnOpenAxis()
        {
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization");
            var weapons = GameManager.Instance.runWeapons;
            var tuning = weapons.upgrades;
            float chance = tuning.reinfuseChance;
            try
            {
                var rifle = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true, element = ElementKind.Fire,
                    axes = UpgradeAxis.Infusion | UpgradeAxis.Twist | UpgradeAxis.Tame };
                var blades = new RunWeapon { form = WeaponForm.OrbitBlades, variant = 0, revealed = true, element = ElementKind.Poison, axes = UpgradeAxis.Infusion };
                weapons.Hand = new[] { rifle, null };
                weapons.Auto = new[] { blades, null };
                tuning.reinfuseChance = 1f;
                for (int i = 0; i < 20; i++) Assert.That(weapons.RollUpgradeCard(), Is.Null, "weapons with no axis left get no card, not a change-element card");

                rifle.axes = UpgradeAxis.Infusion | UpgradeAxis.Twist;
                var card = weapons.RollUpgradeCard();
                Assert.That(((WeaponUpgradeOffer)card.Payload).Axis, Is.EqualTo(UpgradeAxis.Infusion), "with an axis open it may change the element");
                tuning.reinfuseChance = 0f;
                card = weapons.RollUpgradeCard();
                Assert.That(((WeaponUpgradeOffer)card.Payload).Axis, Is.EqualTo(UpgradeAxis.Tame), "otherwise it offers the open axis");
            }
            finally { tuning.reinfuseChance = chance; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Numbers_TunedWeapons_UseTheNewValues()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var roster = weapons.roster;
            Assert.That(roster.Get(WeaponForm.Flamethrower).damageA, Is.EqualTo(1.25f), "Stream");
            Assert.That(roster.Get(WeaponForm.Minigun).damageA, Is.EqualTo(2.5f), "Spin-up");
            Assert.That(roster.Get(WeaponForm.RocketLauncher).damageB, Is.EqualTo(7f), "Launch Yourself");
            Assert.That(roster.Get(WeaponForm.SwarmLauncher).rangeB, Is.EqualTo(12.75f), "Bombard range");
            Assert.That(roster.Get(WeaponForm.ShieldRing).damageA, Is.EqualTo(3f), "Repulsor");
            Assert.That(roster.Get(WeaponForm.ArcCoil).damageB, Is.EqualTo(15f), "Capacitor stays 15");

            var spin = Equip(game, WeaponForm.Minigun, 0);
            Assert.That(weapons.GetCooldownDuration(spin), Is.EqualTo(1f / weapons.upgrades.spinUpStartRate).Within(.001f));
            spin.heldTime = 3f;
            Assert.That(weapons.GetCooldownDuration(spin), Is.EqualTo(1f / weapons.upgrades.spinUpTopRate).Within(.001f));
            Assert.That(weapons.upgrades.spinUpTopRate, Is.EqualTo(10f));
            Assert.That(weapons.upgrades.chainReturnDamagePerJump, Is.EqualTo(1f));
        }
    }
}
