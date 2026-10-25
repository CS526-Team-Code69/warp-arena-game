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
        public async Task WildfireTwist_TwoPatchesInRange_FenceKeepsAddingBurningToABodyOnTheLineWithoutAHit()
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
            float dose = tuning.wildfireFenceFirePerSecond * older.tickInterval;

            await Until(() => status.Fire > 0f, 1f, "the fence sets the enemy on the line burning");
            Assert.That(status.Fire, Is.EqualTo(dose).Within(.01f), "one dose of Burning per patch tick");
            Assert.That(victim.Hp, Is.EqualTo(victim.maxHp), "no direct hit: only the Burning hurts");
            Assert.That(game.runWeapons.Tracers.Count, Is.GreaterThan(0), "the fence is drawn");
            Assume.That(tuning.wildfireFenceFirePerSecond, Is.GreaterThan(1f / status.Rules.fireInterval), "the tuned fence adds stacks faster than one burns off per tick");
            await Seconds(1.1f);
            Assert.That(victim.Hp, Is.LessThan(victim.maxHp), "the Burning ticks");
            Assert.That(status.Fire, Is.GreaterThan(dose), "touching the fence keeps adding Burning faster than it burns off");
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
        public async Task ShatterTame_IcePatches_LeaveYouAloneFreezeEnemiesAndCapAtSix()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];

            Place(enemy, game.player.Position + Vector3.forward * 1.3f);
            var shatter = Equip(game, WeaponForm.CryoCannon, 0, UpgradeAxis.Tame);
            var mine = ElementStatus.For(game.player); var theirs = ElementStatus.For(enemy);
            mine.ClearStatuses(); theirs.ClearStatuses();

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => game.runWeapons.Hazards.Any(h => h.kind == GroundHazardKind.IcePatch
                && Query.Flat(h.position - game.player.Position).magnitude <= h.radius + game.player.radius), 1f, "a patch under you and the enemy");
            bool froze = false; float until = Time.time + 3f;
            while (Time.time < until && !froze)
            {
                Assert.That(mine.Frost, Is.Zero, "the tamed patch never chills you");
                froze = theirs.Frozen;
                await Awaitable.NextFrameAsync();
            }
            Assert.That(froze, Is.True, "4 Frost a second freezes the enemy standing on it");

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

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BlizzardTame_Spraying_SlowsEnemiesAroundYouInsteadOfYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            Equip(game, WeaponForm.CryoCannon, 1, UpgradeAxis.Tame);

            game.runWeapons.HandleInput(true, true, false);
            for (int i = 0; i < 3; i++) { await Awaitable.NextFrameAsync(); game.runWeapons.HandleInput(false, true, false); }

            Assert.That(game.player.ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "you keep your speed");
            Assert.That(targets[0].ExternalMoveMultiplier, Is.EqualTo(tuning.blizzardSelfSlow).Within(.001f), "the enemy beside you is slowed");
            Assert.That(targets[1].ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "one 6 cells away is not");
            Assert.That(game.runWeapons.BlizzardAuraActive, Is.True, "the chill ring shows");

            game.runWeapons.HandleInput(false, false, true);
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(targets[0].ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "letting go frees it at once");
            Assert.That(game.runWeapons.BlizzardAuraActive, Is.False);
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
            Assert.That(targets[0].Hp, Is.EqualTo(targets[0].maxHp), "a stowed Capacitor waits");
            weapons.Switch();

            await Until(() => targets[0].Hp < targets[0].maxHp, 1f, "the full Capacitor discharges by itself");
            Assert.That(targets[0].maxHp - targets[0].Hp, Is.EqualTo(hit * tuning.capacitorFullBonus).Within(.01f), "a normal full discharge");
            Assert.That(targets[1].Hp, Is.EqualTo(targets[1].maxHp), "only within its radius");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "it never hurts you");
            Assert.That(capacitor.charge, Is.Zero);
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task CloudTwist_StandingInTheLandedCloud_PullsItTowardYouOnlyWhileInside()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            Vector3 origin = player.Position;
            var cloud = Equip(game, WeaponForm.SporeLauncher, 0, UpgradeAxis.Twist);
            player.DebugSetAimPoint(origin + Vector3.forward * 3f);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => ActiveActors(cloud, AttackMode.Field), 2f, "the cloud lands");
            var actor = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud && a.mode == AttackMode.Field);
            float before = Query.Flat(actor.point - origin).magnitude;
            await Seconds(.4f);
            float after = Query.Flat(actor.point - origin).magnitude;
            Assert.That(before - after, Is.InRange(1f, game.runWeapons.upgrades.cloudFollowSpeed * .4f + .3f), "it comes toward you at its speed");

            V3CombatSmokeTest.Teleport(player, origin + Vector3.forward * 12f);
            await Awaitable.NextFrameAsync();
            Vector3 left = actor.point;
            await Seconds(.3f);
            Assert.That(Query.Flat(actor.point - left).magnitude, Is.LessThan(.01f), "out of it, it stays put");
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
        public async Task NeedlerTame_EveryEighthNeedle_GivesShieldInsteadOfCostingHp()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            game.player.DebugSetAim(Vector3.back);
            var needler = Equip(game, WeaponForm.SporeLauncher, 1, UpgradeAxis.Tame);
            var status = ElementStatus.For(game.player);
            status.SetShield(0f);
            float hp = game.player.Hp;

            for (int i = 0; i < tuning.needlerHpPerNeedles; i++)
            {
                needler.cooldown = needler.reload = 0f; needler.rounds = 24;
                game.runWeapons.HandleInput(true, true, false);
            }

            Assert.That(game.player.Hp, Is.EqualTo(hp), "firing is free");
            Assert.That(status.Shield, Is.EqualTo(tuning.needlerTameShield).Within(.001f), "eight needles, one Shield");
            await Awaitable.NextFrameAsync();
        }
    }
}
