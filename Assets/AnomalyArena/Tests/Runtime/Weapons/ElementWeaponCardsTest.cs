using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class ElementWeaponCardsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

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

        static void Freeze(Combatant body, GameManager game)
        {
            var status = ElementStatus.For(body);
            status.ClearStatuses();
            status.Add(ElementKind.Frost, status.FrostThreshold + 1f, game.player);
            Assume.That(status.Frozen, Is.True);
        }

        static EnemyBolt LaunchBolt(GameManager game, Enemy from, Vector3 at, Vector3 dir)
        {
            var bolt = game.waves.preparedBolts.First(b => b && !b.gameObject.activeSelf);
            bolt.Launch(from, at, dir);
            return bolt;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StreamTwist_EnemyShotInTheCone_IsBurnedButNotOneBehindYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;

            Equip(game, WeaponForm.Flamethrower, 0);
            var untouched = LaunchBolt(game, targets[0], origin + Vector3.forward * 2f, Vector3.back);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(untouched.gameObject.activeSelf, Is.True, "without the card the flame leaves shots alone");
            untouched.Burn();

            Equip(game, WeaponForm.Flamethrower, 0, UpgradeAxis.Twist);
            var ahead = LaunchBolt(game, targets[0], origin + Vector3.forward * 2f, Vector3.back);
            var behind = LaunchBolt(game, targets[0], origin + Vector3.back * 2f, Vector3.back);
            float hp = targets[0].Hp;
            game.runWeapons.HandleInput(true, true, false);

            Assert.That(ahead.gameObject.activeSelf, Is.False, "a shot inside the cone burns");
            Assert.That(behind.gameObject.activeSelf, Is.True, "a shot outside the cone flies on");
            Assert.That(hp - targets[0].Hp, Is.EqualTo(game.runWeapons.HitDamage(game.runWeapons.Active)).Within(.01f), "enemies still take the plain stream hit");
            behind.Burn();
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task StreamTame_FuelTankBurst_SparesYouAndKnocksTheEnemyBack()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var stream = Equip(game, WeaponForm.Flamethrower, 0, UpgradeAxis.Tame);
            var near = targets[0];
            Place(near, game.player.Position + Vector3.forward * 1.5f, movable: true);
            game.player.DebugSetAim(Vector3.back);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();
            Vector3 away = Query.Flat(near.Position - game.player.Position).normalized;

            game.runWeapons.HandleInput(true, true, false);
            stream.heldTime = .1f;
            game.player.ReceiveDamage(DamageInfo.Attack(1f, near));

            Assert.That(status.Fire, Is.Zero, "the tamed burst does not set you alight");
            Assert.That(near.Hp, Is.LessThan(near.maxHp), "it still burns the enemy in the ring");
            Assert.That(near.State, Is.EqualTo(CharacterState.Knocked), "and knocks it back for real");

            await Awaitable.FixedUpdateAsync(); await Awaitable.FixedUpdateAsync();
            float launch = Vector3.Dot(Query.Flat(near.GetComponent<Rigidbody>().linearVelocity), away);
            float expected = game.runWeapons.upgrades.fuelTankTamePush * game.runWeapons.UpgradePushScale * near.knockbackScale * game.rules.knockDamping;
            Assert.That(launch, Is.EqualTo(expected).Within(expected * .2f), "launched away from you for a 4-cell knockback");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WildfireTwist_TwoPatchesInRange_FenceKeepsABodyOnTheLineBurningWithoutAHit()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            targets[0].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var victim = targets[1];
            var status = ElementStatus.For(victim);
            status.ClearStatuses();
            Vector3 origin = game.player.Position;
            var wildfire = Equip(game, WeaponForm.Flamethrower, 1, UpgradeAxis.Twist);
            const float tick = 2f;

            AttackActor older = null;
            foreach (float z in new[] { 2f, 10f })
            {
                var patch = AttackPool.Instance.Acquire();
                patch.Configure(AttackMode.Field, game.player, wildfire, origin + Vector3.forward * z, Vector3.forward, tick, 0f, 0f, 0f, 1f, 4f);
                older ??= patch;
            }
            float burn = tuning.wildfireFenceFirePerSecond * ElementCombat.WaveScale;

            await Until(() => status.Fire > 0f, 1f, "the fence sets the enemy on the line burning");
            Assert.That(status.Fire, Is.EqualTo(burn).Within(.01f), "it burns at the fence's rate a second");
            Assert.That(victim.Hp, Is.EqualTo(victim.maxHp), "no direct hit: only the burn hurts");
            Assert.That(game.runWeapons.Tracers.Count, Is.GreaterThan(0), "the fence is drawn");
            await Seconds(1.1f);
            Assert.That(victim.Hp, Is.LessThan(victim.maxHp), "the burn ticks");
            Assert.That(status.Fire, Is.EqualTo(burn).Within(.01f), "touching the fence keeps it burning at that rate");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task WildfireTame_StandingInAnOwnPatchLaidBeforeTheCard_NoBurnAndFasterUntilYouLeave()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var wildfire = Equip(game, WeaponForm.Flamethrower, 1);
            var patch = AttackPool.Instance.Acquire();
            patch.Configure(AttackMode.Field, player, wildfire, player.Position, Vector3.forward, 1f, 0f, 0f, 0f, 2.5f, 4f);
            patch.hurtOwner = true; patch.ownerDamage = .5f;
            wildfire.axes |= UpgradeAxis.Tame;
            float hp = player.Hp;

            await Seconds(.6f);
            Assert.That(player.Hp, Is.EqualTo(hp), "a patch laid before the card no longer burns you");
            Assert.That(player.ExternalMoveMultiplier, Is.EqualTo(game.runWeapons.upgrades.wildfireTameSpeed).Within(.001f), "faster inside your fire");

            V3CombatSmokeTest.Teleport(player, player.Position + Vector3.forward * 6f);
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(player.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "back to normal outside it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ShatterTwist_FrozenEnemy_IsThrownAndBurstsWhenItStopsHittingItsNeighbour()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            targets[2].DiscardWithoutReward();
            var stone = targets[0]; var neighbour = targets[1];
            Place(stone, game.player.Position + Vector3.forward * 3f, movable: true);
            var shatter = Equip(game, WeaponForm.CryoCannon, 0, UpgradeAxis.Twist);
            float hit = game.runWeapons.HitDamage(shatter);
            Freeze(stone, game);
            Vector3 start = stone.Position;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => stone.State == CharacterState.Thrown, 1f, "the shard throws the frozen enemy");
            Assert.That(stone.Hp, Is.EqualTo(stone.maxHp), "the shard's damage waits for the stop");

            await Until(() => stone.Hp < stone.maxHp, 3f, "the stone stops and bursts");
            Assert.That(Query.Flat(stone.Position - start).magnitude, Is.GreaterThan(1f), "it slid away first");
            float taken = stone.maxHp - stone.Hp;

            Assert.That(taken, Is.GreaterThanOrEqualTo(hit * tuning.shatterFrozenBonus - .01f).And.LessThanOrEqualTo(hit * tuning.shatterFrozenBonus * 1.3f + 2.5f), "the stone takes the shard with its frozen bonus");
            Assert.That(neighbour.maxHp - neighbour.Hp, Is.GreaterThanOrEqualTo(hit * tuning.shatterSplashShare - .01f), "the burst hits the enemy beside it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ShatterTame_OwnIce_KeepsYourFootingAndSpeedsYouUpWhileEnemiesSlipFreezeAndThePatchesStayCapped()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            var player = game.player;

            Place(enemy, player.Position + Vector3.forward * 1.3f);
            var shatter = Equip(game, WeaponForm.CryoCannon, 0, UpgradeAxis.Tame);
            var mine = ElementStatus.For(player); var theirs = ElementStatus.For(enemy);
            mine.ClearStatuses(); theirs.ClearStatuses();

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.IcePatch
                && Query.Flat(h.position - player.Position).magnitude <= h.radius + player.radius), 1f, "a patch under you and the enemy");
            await Awaitable.NextFrameAsync();

            Assert.That(game.runWeapons.IceGrip(player), Is.Zero, "you keep your footing on your own ice");
            Assert.That(player.ExternalMoveMultiplier, Is.EqualTo(tuning.shatterTameSpeed).Within(.001f), "and move faster on it");
            Assert.That(game.runWeapons.IceGrip(enemy), Is.GreaterThan(0f), "the enemy on it slips");
            bool froze = false; float until = Time.time + 3f;
            while (Time.time < until && !froze)
            {
                Assert.That(mine.Frost, Is.Zero, "the tamed patch never chills you");
                froze = theirs.Frozen;
                await Awaitable.NextFrameAsync();
            }
            Assert.That(froze, Is.True, "4 Frost a second freezes the enemy standing on it");
            Vector3 stand = player.Position;
            V3CombatSmokeTest.Teleport(player, stand + Vector3.forward * 12f);                                         
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(player.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "off the ice your speed is back at once");
            V3CombatSmokeTest.Teleport(player, stand);
            Physics.SyncTransforms();

            for (int volley = 0; volley < 2; volley++)
            {
                await Until(() => !ActiveActors(shatter, AttackMode.Shard), 2f, "the shards land");
                shatter.cooldown = 0f;
                game.player.DebugSetAim(Vector3.forward);
                game.runWeapons.HandleInput(true, true, false);
            }
            await Until(() => !ActiveActors(shatter, AttackMode.Shard), 2f, "the last shards land");

            Assert.That(game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.IcePatch && h.weapon == shatter),
                Is.EqualTo(game.runWeapons.upgrades.icePatchTamedCap), "nine shards, six patches kept");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BlizzardTwist_ReleaseAfterAHold_ShattersTheFrozenEnemyForItsFrozenSeconds()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            Place(enemy, game.player.Position + Vector3.forward * 2f);
            var blizzard = Equip(game, WeaponForm.CryoCannon, 1, UpgradeAxis.Twist);
            var status = ElementStatus.For(enemy);
            float scale = game.runWeapons.HitDamage(blizzard) / game.runWeapons.roster.Get(WeaponForm.CryoCannon).damageB;
            Freeze(enemy, game);

            async Task<float> Hold(float seconds)
            {
                game.runWeapons.HandleInput(true, true, false);
                float start = Time.time;
                while (Time.time < start + seconds) { await Awaitable.NextFrameAsync(); game.runWeapons.HandleInput(false, true, false); }
                return Time.time - start;
            }

            await Hold(.2f);
            float before = enemy.Hp;
            game.runWeapons.HandleInput(false, false, true);
            Assert.That(enemy.Hp, Is.EqualTo(before), "a short tap shatters nothing");
            Assert.That(status.Frozen, Is.True);

            blizzard.cooldown = 0f;
            float held = await Hold(1.5f);
            before = enemy.Hp;
            game.runWeapons.HandleInput(false, false, true);
            float expected = Mathf.Min(tuning.blizzardShatterCap, tuning.blizzardShatterPerSecond * held) * scale * status.Rules.frozenDamageTaken;
            Assert.That(before - enemy.Hp, Is.EqualTo(expected).Within(tuning.blizzardShatterPerSecond * .15f * scale * status.Rules.frozenDamageTaken),
                "3 per frozen second, taken while still frozen");
            Assert.That(status.Frozen, Is.False, "the release thaws it");
            status.Add(ElementKind.Frost, status.FrostThreshold + 1f, game.player);
            Assert.That(status.Frozen, Is.False, "and it cannot freeze again for a while");
        }

        static async Task Spray(GameManager game, float seconds)
        {
            game.runWeapons.HandleInput(true, true, false);
            float start = Time.time;
            while (Time.time < start + seconds) { await Awaitable.NextFrameAsync(); game.runWeapons.HandleInput(false, true, false); }
        }

        static bool SnowAt(GameManager game, Vector3 at) =>
            game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.Snow && Query.Flat(h.position - at).magnitude < .3f);

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Blizzard_Spraying_LeavesASnowTrailThatChillsYouUpToTheCapAndNoLongerSlowsYourWalk()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            foreach (var t in targets) t.DiscardWithoutReward();                             
            Equip(game, WeaponForm.CryoCannon, 1);
            player.DebugSetAim(Vector3.back);                       
            var status = ElementStatus.For(player);
            status.ClearStatuses();
            Vector3 start = player.Position;

            await Spray(game, .5f);
            Assert.That(player.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "spraying no longer slows you by itself");
            Assert.That(SnowAt(game, start), Is.True, "snow under your feet");
            Assert.That(game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.Snow), Is.InRange(2, 3), $"a patch every {tuning.snowInterval} s");
            Assert.That(status.Frost, Is.GreaterThan(0f), "standing in it chills you");

            V3CombatSmokeTest.Teleport(player, start + Vector3.forward * 3f);
            Physics.SyncTransforms();
            game.runWeapons.HandleInput(false, true, false);
            await Until(() => SnowAt(game, start + Vector3.forward * 3f), 1f, "snow where you stand now");
            Assert.That(SnowAt(game, start), Is.True, "and the old snow stays");

            status.ClearStatuses();
            status.Add(ElementKind.Frost, tuning.snowFrostCap - .3f, player);
            float most = 0f, until = Time.time + 1.5f;
            while (Time.time < until)
            {
                await Awaitable.NextFrameAsync();
                game.runWeapons.HandleInput(false, true, false);
                Assert.That(status.Frozen, Is.False, "your snow never freezes you");
                most = Mathf.Max(most, status.Frost);
            }
            Assert.That(most, Is.GreaterThan(tuning.snowFrostCap - .3f).And.LessThanOrEqualTo(tuning.snowFrostCap + .001f), "it chills you up to the cap");
            game.runWeapons.HandleInput(false, false, true);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BlizzardTame_Snow_SparesYouCutsYourDamageAndSlowsAndChillsEnemiesStandingInIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            targets[2].DiscardWithoutReward();
            var near = targets[0];

            Place(near, player.Position + Vector3.forward * (tuning.snowRadius + near.radius * .5f));
            Equip(game, WeaponForm.CryoCannon, 1, UpgradeAxis.Tame);
            player.DebugSetAim(Vector3.back);                                
            var mine = ElementStatus.For(player); var theirs = ElementStatus.For(near);
            mine.ClearStatuses(); theirs.ClearStatuses();
            Assume.That(mine.Shield, Is.Zero, "no Shield to soak the test hit");

            await Spray(game, .4f);
            Assert.That(mine.Frost, Is.Zero, "the tamed snow never chills you");
            Assert.That(near.ExternalMoveMultiplier, Is.EqualTo(tuning.snowTameSlow).Within(.001f), "the enemy in the snow is slowed");
            Assert.That(theirs.Frost + (theirs.Frozen ? 99f : 0f), Is.GreaterThan(0f), "and gains Frost");
            Assert.That(targets[1].ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "one out of the snow is not");
            Assert.That(game.runWeapons.SnowGuardActive, Is.True, "you stand in your own snow");
            float hp = player.Hp;
            player.ReceiveDamage(new DamageInfo { amount = 10f, source = DamageSource.Wall });
            Assert.That(hp - player.Hp, Is.EqualTo(10f * game.progression.Stats.DamageTakenMultiplier * tuning.snowTameDamageTaken).Within(.01f), "and take less damage");

            game.runWeapons.HandleInput(false, false, true);
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(near.ExternalMoveMultiplier, Is.EqualTo(tuning.snowTameSlow).Within(.001f), "the snow outlasts the spray");
            Place(near, player.Position + Vector3.forward * 9f);
            V3CombatSmokeTest.Teleport(player, player.Position + Vector3.forward * 12f);
            Physics.SyncTransforms();
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(near.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "out of the snow it walks at its pace again");
            Assert.That(game.runWeapons.SnowGuardActive, Is.False, "and so do you take full damage");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainTwist_SecondJump_GoesToTheEnemyNearestTheAimPoint()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 origin = game.player.Position;

            Place(targets[0], origin + Vector3.forward * 3f);
            Place(targets[1], origin + new Vector3(1f, 0f, 1f));
            Place(targets[2], origin + Vector3.forward * 6.5f);
            game.player.DebugSetAimPoint(origin + Vector3.forward * 8f);

            Equip(game, WeaponForm.ArcCoil, 0);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(Query.Flat(game.runWeapons.Tracers[1].to - targets[1].Position).magnitude, Is.LessThan(.01f), "without the card it jumps to the nearest");

            ElementStatus.For(game.player).ClearStatuses();
            Equip(game, WeaponForm.ArcCoil, 0, UpgradeAxis.Twist);
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(Query.Flat(game.runWeapons.Tracers[0].to - targets[0].Position).magnitude, Is.LessThan(.01f), "the first target is unchanged");
            Assert.That(Query.Flat(game.runWeapons.Tracers[1].to - targets[2].Position).magnitude, Is.LessThan(.01f), "the Twist jumps toward the aim point");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainTame_UnusedJumps_AreStoredForTheNextDischargeInsteadOfReturning()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var chain = Equip(game, WeaponForm.ArcCoil, 0, UpgradeAxis.Tame);
            var status = ElementStatus.For(game.player);
            status.ClearStatuses();
            float hit = game.runWeapons.HitDamage(chain);

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(status.Lightning, Is.Zero, "nothing returns to you");
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp - hit).Within(.01f), "no shock around you, only the arc");
            Assert.That(chain.chainStoredJumps, Is.EqualTo(2), "five jumps, three targets: two stored");
            Assert.That(game.UiSnapshot.handSlots[game.runWeapons.ActiveIndex].detail, Does.Contain("+2"), "the slot shows them");

            Assert.That(game.runWeapons.MoveMultiplier, Is.EqualTo(tuning.chainTameMoveSpeed).Within(.0001f), "stored jumps in hand speed you up");
            int hand = game.runWeapons.ActiveIndex;
            game.runWeapons.Hand[1 - hand] = new RunWeapon { form = WeaponForm.Rifle, variant = 1, revealed = true, rounds = 3 };
            game.runWeapons.Switch();
            Assert.That(game.runWeapons.MoveMultiplier, Is.EqualTo(1f), "stowed, it does not");
            game.runWeapons.Switch();
            Assert.That(game.runWeapons.MoveMultiplier, Is.EqualTo(tuning.chainTameMoveSpeed).Within(.0001f), "back in hand it does again");

            int cap = tuning.chainStoreMax;
            try
            {
                tuning.chainStoreMax = 10;
                chain.cooldown = 0f;
                game.runWeapons.HandleInput(true, true, false);

                Assert.That(chain.chainStoredJumps, Is.EqualTo(4), "the stored jumps were spent on this discharge");
            }
            finally { tuning.chainStoreMax = cap; }
            chain.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            Assert.That(chain.chainStoredJumps, Is.EqualTo(cap), "at most +3 are kept");

            game.runWeapons.ClearWaveEffects();
            Assert.That(chain.chainStoredJumps, Is.Zero);
            Assert.That(game.runWeapons.MoveMultiplier, Is.EqualTo(1f), "nothing stored, no speed-up");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task CapacitorTwist_Discharge_AlsoStrikesEnemiesAlongYourRecentPathOnce()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            Vector3 origin = game.player.Position, side = Vector3.right * 1.2f;
            Assume.That(RunWeapons.ValidTeleportPoint(origin + side, game.player.radius) && RunWeapons.ValidTeleportPoint(origin + side + Vector3.forward * 9.5f, game.player.radius), Is.True);
            var capacitor = Equip(game, WeaponForm.ArcCoil, 1, UpgradeAxis.Twist);
            float hit = weapons.HitDamage(capacitor);

            float start = Time.time;
            while (Time.time - start < 1.2f)
            {
                float t = Mathf.Clamp01((Time.time - start) / 1.2f);
                V3CombatSmokeTest.Teleport(game.player, origin + side + Vector3.forward * Mathf.Lerp(9.5f, 0f, t));
                await Awaitable.NextFrameAsync();
            }
            V3CombatSmokeTest.Teleport(game.player, origin + side);
            await Awaitable.NextFrameAsync();
            Assume.That(Query.Flat(targets[1].Position - game.player.Position).magnitude, Is.GreaterThan(weapons.roster.Get(WeaponForm.ArcCoil).rangeB + targets[1].radius));

            capacitor.charge = weapons.upgrades.capacitorMax * .5f;
            weapons.HandleInput(true, true, false);

            foreach (var t in targets)
                Assert.That(t.maxHp - t.Hp, Is.EqualTo(hit * .5f).Within(.01f), "the ring and the path each hit once, at the same damage");
            Assert.That(capacitor.trailCount, Is.Zero, "the path starts again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task CapacitorTame_HeldFullForFourSeconds_DischargesAtFullStrengthAndSparesYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons; var tuning = weapons.upgrades;
            var capacitor = Equip(game, WeaponForm.ArcCoil, 1, UpgradeAxis.Tame);
            int hand = weapons.ActiveIndex;
            weapons.Hand[1 - hand] = new RunWeapon { form = WeaponForm.Rifle, variant = 0, revealed = true };
            float hit = weapons.HitDamage(capacitor), hp = game.player.Hp;

            float reach = weapons.roster.Get(WeaponForm.ArcCoil).rangeB * capacitor.Reach;
            Place(targets[1], game.player.Position + Vector3.forward * (reach + targets[1].radius + 1f));

            weapons.Switch();
            capacitor.charge = tuning.capacitorMax; capacitor.chargeFullTime = 3.98f;
            await Seconds(.3f);
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp), "a stowed Capacitor never discharges");
            weapons.Switch();

            capacitor.charge = tuning.capacitorMax;

            await Until(() => targets[0].Hp < targets[0].maxHp, 1f, "the full Capacitor discharges by itself");
            Assert.That(targets[0].maxHp - targets[0].Hp, Is.EqualTo(hit * tuning.capacitorFullBonus).Within(.01f), "a normal full discharge");
            Assert.That(targets[1].Hp, Is.EqualTo(targets[1].maxHp), "only within its radius");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "it never hurts you");
            Assert.That(capacitor.charge, Is.Zero);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task CloudTwist_SporePodBurstingInsideALandedCloud_DetonatesItAndRestartsItsTime()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player; var tuning = game.runWeapons.upgrades;
            Vector3 origin = player.Position, at = origin + Vector3.forward * 8f;
            var cloud = Equip(game, WeaponForm.SporeLauncher, 0, UpgradeAxis.Twist);
            cloud.element = ElementKind.Poison; cloud.builtInElement = true;                          

            var watched = targets[0];
            Place(watched, at + Vector3.forward * (watched.radius + 4.4f));
            targets[1].DiscardWithoutReward();
            var stepper = targets[2];
            Place(stepper, origin + Vector3.forward * 2f);
            player.DebugSetAimPoint(at);
            float playerHp = player.Hp;

            int Touched(Vector3 spot) => game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.SporePod && Query.Flat(h.position - spot).magnitude <= h.radius + stepper.radius);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => ActiveActors(cloud, AttackMode.Field), 2f, "the first cloud lands");
            var first = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field);
            cloud.cooldown = 0f;
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => AttackPool.Instance.actors.Count(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field) == 2, 2f, "the second cloud lands");
            var second = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field && a != first);
            await Seconds(.6f);
            UnityEngine.Random.InitState(11);
            first.Release();
            await Until(() => game.runWeapons.Hazards.Count(h => h.kind == GroundHazardKind.SporePod) == tuning.sporePods, 1f, "the first cloud drops its pods");
            Assume.That(watched.Hp, Is.EqualTo(watched.maxHp), "nothing has reached the watched enemy yet");
            float left = second.remaining;
            Assume.That(left, Is.LessThan(tuning.cloudSeconds - .4f));
            float blast = second.damage * tuning.cloudBlastTicks;

            var pod = game.runWeapons.Hazards.First(h => h.kind == GroundHazardKind.SporePod);
            int bursts = Touched(pod.position);
            Place(stepper, pod.position);
            await Until(() => watched.Hp < watched.maxHp, 2f, "the pod sets the cloud off");
            float lost = watched.maxHp - watched.Hp;

            Assert.That(lost, Is.GreaterThanOrEqualTo(blast * bursts - .01f).And.LessThan(blast * bursts + 2f), "each detonation deals the cloud's ticks at once");
            var status = ElementStatus.For(watched);
            Assert.That(status.Poison, Is.GreaterThanOrEqualTo(status.Rules.poisonOnEnemyCap - .01f), "and fills its Poison");
            Assert.That(second.remaining, Is.GreaterThan(tuning.cloudSeconds - .2f), "the cloud's time starts again");
            Assert.That(AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field
                && Mathf.Approximately(a.radius, tuning.sporeBurstRadius)), Is.True, "the pod's own mist still appears");

            if (game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.SporePod))
            {
                await Seconds(.3f);
                float before = second.remaining;
                var next = game.runWeapons.Hazards.First(h => h.kind == GroundHazardKind.SporePod);
                Place(stepper, next.position);
                await Until(() => second.remaining > before + .1f, 2f, "the next pod sets it off again");
            }
            Assert.That(player.Hp, Is.EqualTo(playerHp), "it never touches you");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task CloudTame_SteppingOnASporePod_KicksItAwayAndItsMistSparesYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var player = game.player; var tuning = game.runWeapons.upgrades;
            Vector3 origin = player.Position;
            var cloud = Equip(game, WeaponForm.SporeLauncher, 0, UpgradeAxis.Tame);
            player.DebugSetAimPoint(origin + Vector3.forward * 4f);
            game.runWeapons.HandleInput(true, true, false);
            await Until(() => ActiveActors(cloud, AttackMode.Field), 2f, "the cloud lands");
            await Awaitable.NextFrameAsync();

            UnityEngine.Random.InitState(7);
            AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field).Release();
            await Until(() => game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.SporePod), 1f, "the ended cloud drops its pods");
            var pod = game.runWeapons.Hazards.Where(h => h.kind == GroundHazardKind.SporePod).OrderBy(h => Mathf.Abs(h.position.x - origin.x)).First();
            var status = ElementStatus.For(player);
            status.ClearStatuses();
            float hp = player.Hp;

            V3CombatSmokeTest.Teleport(player, pod.position - Vector3.forward * .3f);
            await Until(() => !game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.SporePod && Query.Flat(h.position - pod.position).sqrMagnitude < .0001f), 1f, "you kick the pod");
            Assert.That(ActiveActors(cloud, AttackMode.Lob), Is.True, "it flies off");
            await Until(() => AttackPool.Instance.actors.Any(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field
                && Mathf.Approximately(a.radius, tuning.sporeBurstRadius)), 2f, "it bursts where it lands");
            var mist = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field && Mathf.Approximately(a.radius, tuning.sporeBurstRadius));
            Assert.That(Query.Flat(mist.point - pod.position).magnitude, Is.GreaterThan(2f), "away from where it lay");
            V3CombatSmokeTest.Teleport(player, mist.point);
            await Seconds(.6f);
            Assert.That(status.Poison, Is.Zero, "the mist does not poison you");
            Assert.That(player.Hp, Is.EqualTo(hp));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task NeedlerTwist_SixthNeedleOnOneEnemy_BurstsOnItsNeighboursOnly()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            var focus = targets[0]; var beside = targets[1]; var far = targets[2];
            Place(beside, focus.Position + Vector3.right * 1.3f);
            var needler = Equip(game, WeaponForm.SporeLauncher, 1, UpgradeAxis.Twist);
            float hit = game.runWeapons.HitDamage(needler);

            async Task Fire(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    needler.cooldown = needler.reload = 0f; needler.rounds = 24;
                    game.runWeapons.HandleInput(true, true, false);
                }
                await Until(() => !ActiveActors(needler, AttackMode.Bolt), 2f, "the needles land");
            }

            await Fire(tuning.needleBurstCount - 1);
            Assert.That(beside.Hp, Is.EqualTo(beside.maxHp), "no burst before the sixth needle");
            await Fire(1);
            Assert.That(beside.maxHp - beside.Hp, Is.EqualTo(hit * tuning.needleBurstShare).Within(.01f), "the neighbour takes twice a needle");
            Assert.That(focus.maxHp - focus.Hp, Is.EqualTo(hit * tuning.needleBurstCount).Within(.01f), "the target itself only takes its needles");
            Assert.That(far.Hp, Is.EqualTo(far.maxHp), "only within the burst radius");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task NeedlerTame_ReloadMist_SparesYouLastsLongerAndSpeedsTheReloadWhileYouTouchIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var tuning = game.runWeapons.upgrades;
            var player = game.player;
            Vector3 origin = player.Position;
            player.DebugSetAim(Vector3.back);
            var needler = Equip(game, WeaponForm.SporeLauncher, 1, UpgradeAxis.Tame);
            needler.element = ElementKind.Poison; needler.builtInElement = true;
            var status = ElementStatus.For(player);
            status.ClearStatuses(); status.SetShield(0f);
            float hp = player.Hp;

            needler.rounds = 1;
            game.runWeapons.HandleInput(true, true, false);
            var mist = AttackPool.Instance.actors.Single(a => a && a.gameObject.activeSelf && a.weapon == needler && a.mode == AttackMode.Field);
            Assert.That(mist.remaining, Is.EqualTo(tuning.needlerMistSeconds * tuning.needlerMistTameLength).Within(.05f), "the tamed mist lasts longer");
            Assert.That(game.runWeapons.InNeedlerMist(needler), Is.True, "it bursts where you stand");

            async Task<float> ReloadRate(float seconds)
            {
                float r0 = needler.reload, t0 = Time.time;
                await Seconds(seconds);
                return (r0 - needler.reload) / Mathf.Max(.01f, Time.time - t0);
            }
            float inside = await ReloadRate(.5f);
            Assert.That(inside, Is.EqualTo(tuning.needlerMistTameReload).Within(.3f), "touching it, the reload runs faster");

            V3CombatSmokeTest.Teleport(player, origin + Vector3.forward * (tuning.needlerMistRadius + player.radius + 1f));
            await Awaitable.NextFrameAsync();
            Assert.That(game.runWeapons.InNeedlerMist(needler), Is.False);
            float outside = await ReloadRate(.4f);
            Assert.That(outside, Is.EqualTo(1f).Within(.25f), "out of it, the normal rate at once");
            Assert.That(player.Hp, Is.EqualTo(hp), "the tamed mist never hurts you");
            Assert.That(status.Poison, Is.Zero);

            var plain = Equip(game, WeaponForm.SporeLauncher, 1);
            plain.element = ElementKind.Poison; plain.builtInElement = true;
            plain.rounds = 1;
            game.runWeapons.HandleInput(true, true, false);
            Assume.That(game.runWeapons.InNeedlerMist(plain), Is.True);
            plain.axes |= UpgradeAxis.Tame;
            hp = player.Hp;
            await Seconds(.8f);
            Assert.That(player.Hp, Is.EqualTo(hp), "a mist left before the card was taken spares you too");
        }
    }
}
