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
    public class StartingWeapons1027Test
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

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }

        static IEnumerable<AttackActor> Actors(RunWeapon w, AttackMode mode) =>
            AttackPool.Instance.actors.Where(a => a && a.gameObject.activeSelf && a.weapon == w && a.mode == mode);

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChargedShot_WhileCharging_EnemyAttacksHitHarderAndTamedSofter_TicksAndWallsUnchanged()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var tuning = game.runWeapons.upgrades;
            Assert.That((tuning.chargeDamageTaken, tuning.chargeTameDamageTaken), Is.EqualTo((1.5f, .5f)), "Q1 A: +50%; Q4 A: x0.5");
            Assume.That(game.progression.Stats.DamageTakenMultiplier, Is.EqualTo(1f).Within(.001f), "starting stats take damage as dealt");
            Assume.That(ElementStatus.For(player).Shield, Is.Zero, "no Shield to soak the hits");
            var enemy = targets[0];

            float Lost(DamageInfo info)
            {
                player.ClearWaveState();                                      
                player.Heal(player.maxHp);
                float before = player.Hp;
                player.ReceiveDamage(info);
                return before - player.Hp;
            }

            var rifle = Equip(game, WeaponForm.Rifle, 0);
            Assert.That(Lost(DamageInfo.Attack(4f, enemy)), Is.EqualTo(4f).Within(.01f), "not charging: an attack hits as dealt");
            game.runWeapons.HandleInput(true, true, false);
            Assume.That(rifle.charging, Is.True);
            Assert.That(Lost(DamageInfo.Attack(4f, enemy)), Is.EqualTo(6f).Within(.01f), "charging: an enemy attack hits 50% harder");
            Assert.That(Lost(new DamageInfo { amount = 2f, source = DamageSource.Burn, dealer = enemy }), Is.EqualTo(2f).Within(.01f), "a burn tick is unchanged");
            Assert.That(Lost(new DamageInfo { amount = 3f, source = DamageSource.Wall, kind = HitKind.Slam }), Is.EqualTo(3f).Within(.01f), "a wall hit is unchanged");
            game.runWeapons.HandleInput(false, false, true);
            Assert.That(Lost(DamageInfo.Attack(4f, enemy)), Is.EqualTo(4f).Within(.01f), "released: back to normal");

            var tamed = Equip(game, WeaponForm.Rifle, 0, UpgradeAxis.Tame);
            game.runWeapons.HandleInput(true, true, false);
            Assume.That(tamed.charging, Is.True);
            Assert.That(Lost(DamageInfo.Attack(4f, enemy)), Is.EqualTo(2f).Within(.01f), "tamed: the charge halves an enemy attack instead");
            Assert.That(game.runWeapons.MoveMultiplier, Is.EqualTo(1f), "and no longer slows you");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainBlade_Hook_YouAndTheEnemyMeetHalfwayAndOnlyThePullHoldsYouStill()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            var player = game.player;
            float pullSpeed = weapons.hookPullSpeed;
            try
            {
                weapons.hookPullSpeed = 2f;                                              
                targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
                Vector3 start = player.Position;
                var enemy = targets[0];
                Place(enemy, start + Vector3.forward * 7f, movable: true);
                Vector3 enemyStart = enemy.Position;
                var hook = Equip(game, WeaponForm.Knife, 1);

                weapons.HandleInput(true, true, false);
                Assert.That(player.MovementLocked, Is.False, "Q19: the hook in flight leaves you free to walk");
                await Until(() => weapons.HandHookReeling, 2f, "the hook catches the enemy");
                Assert.That(player.MovementLocked, Is.True, "the two-way pull carries you");
                await Until(() => hook.hooked == enemy, 5f, "you meet");
                Assert.That(player.MovementLocked, Is.False, "free again once you hold it");

                float half = (7f - (player.radius + enemy.radius + .3f)) * .5f;
                Assert.That(Vector3.Dot(player.Position - start, Vector3.forward), Is.EqualTo(half).Within(.35f), "Q16 A: you are dragged half the way");
                Assert.That(Vector3.Dot(enemyStart - enemy.Position, Vector3.forward), Is.EqualTo(half).Within(.35f), "the enemy is pulled the other half");
            }
            finally { weapons.hookPullSpeed = pullSpeed; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainBladeTame_ThePull_IsGuardedAndUnbrokenAndTheMeetingShockwaveThrowsOthersBack()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var weapons = game.runWeapons;
            var player = game.player;
            var tuning = weapons.upgrades;
            Assert.That((tuning.hookShockRadius, tuning.hookShockPush), Is.EqualTo((2.5f, 4f)), "Q23 A: 2.5 cells, 4 cells, no damage");
            float pullSpeed = weapons.hookPullSpeed;
            try
            {
                weapons.hookPullSpeed = 2f;
                Vector3 start = player.Position;
                var enemy = targets[0];
                Place(enemy, start + Vector3.forward * 7f, movable: true);

                var bystander = targets[1];
                float meet = (7f - (player.radius + enemy.radius + .3f)) * .5f;
                Place(bystander, start + Vector3.forward * meet + Vector3.right * (.7f + bystander.radius));
                targets[2].DiscardWithoutReward();
                var hook = Equip(game, WeaponForm.Knife, 1, UpgradeAxis.Tame);

                weapons.HandleInput(true, true, false);
                await Until(() => weapons.HandHookReeling, 2f, "the hook catches the enemy");
                await Awaitable.FixedUpdateAsync();
                await Awaitable.FixedUpdateAsync();
                Assume.That(weapons.HandHookReeling, Is.True, "still pulling");
                float hp = player.Hp;
                Assert.That(player.ReceiveDamage(DamageInfo.Attack(3f, bystander)), Is.False, "Q22 A: an attack during the pull is blocked");
                Assert.That(player.ReceiveDamage(new DamageInfo { amount = 1f, source = DamageSource.Burn, dealer = bystander }), Is.True, "a burn tick still lands");
                Assert.That(player.Hp, Is.EqualTo(hp - 1f).Within(.01f));
                player.Knockback(Vector3.back, 4f, KnockKind.Hit);                                                                               
                Assert.That(player.State, Is.EqualTo(CharacterState.Normal), "unstoppable: nothing knocks you off the pull");
                player.Stun(5f);                                   
                await Until(() => hook.hooked == enemy, 5f, "nothing breaks the tamed pull: you meet");

                Assert.That(bystander.State, Is.EqualTo(CharacterState.Knocked), "the meeting shockwave throws the enemy beside you back");
                Assert.That(bystander.Hp, Is.EqualTo(TargetHp), "the shockwave deals no damage");
                Assert.That(enemy.State, Is.EqualTo(CharacterState.Hooked), "the enemy you hold is not thrown");
            }
            finally { weapons.hookPullSpeed = pullSpeed; player.ClearWaveState(); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainBlade_HeldEnemy_StopsAHostileShotAndTakesItsDamage()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var shield = targets[0];
            Place(shield, shield.Position, movable: true);
            var hook = Equip(game, WeaponForm.Knife, 1);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => hook.hooked == shield, 3f, "the chain blade holds the enemy in front of you");
            Assert.That(player.HeldShield, Is.SameAs(shield));
            player.ClearWaveState();
            float playerHp = player.Hp, shieldHp = shield.Hp;
            Vector3 aim = Query.Flat(player.AimDirection).normalized;
            var bolt = game.waves.preparedBolts.First(b => b && !b.gameObject.activeSelf);
            bolt.Launch(targets[2], player.Position + aim * 6f, -aim);
            await Until(() => !bolt.gameObject.activeSelf, 3f, "the shot lands");

            Assert.That(shield.Hp, Is.LessThan(shieldHp), "Q15 A: the held enemy takes the shot");
            Assert.That(player.Hp, Is.EqualTo(playerHp), "and you take nothing");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChainBlade_Pull_StopsYouWhereABlackHolesPullBegins()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            var world = WorldMap.Active;
            Vector3 start = player.Position;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            Place(enemy, start + Vector3.forward * 7f, movable: true);

            world.CreateTemporaryHole(start + Vector3.forward * 2.4f + Vector3.right * 1.2f, 1f, 10f, null);
            Assume.That(world.IsHole(start + Vector3.forward * 2.4f), Is.False, "the line misses the core");
            Assume.That(world.GetPull(start + Vector3.forward * 2.4f, player).sqrMagnitude, Is.GreaterThan(.01f), "but crosses the pull");
            var hook = Equip(game, WeaponForm.Knife, 1);

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => hook.hooked == enemy, 3f, "the enemy is reeled in all the way");

            Assert.That(player.IsAlive, Is.True);
            Assert.That(world.GetPull(player.Position, player).sqrMagnitude, Is.LessThan(.01f), "Q18: the pull never drags you into a hole's reach");
            Assert.That(Vector3.Dot(player.Position - start, Vector3.forward), Is.LessThan(1.6f), "you stopped where its pull begins");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task LaunchYourselfTame_FlightOverHoles_IsNeitherSwallowedNorDraggedAndNeverEndsInsideOne()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var t in targets) t.DiscardWithoutReward();
            var player = game.player;
            var world = WorldMap.Active;
            Vector3 start = player.Position;
            var rocket = Equip(game, WeaponForm.RocketLauncher, 1, UpgradeAxis.Tame);
            float range = game.runWeapons.roster.Get(WeaponForm.RocketLauncher).rangeB * rocket.Reach;

            world.CreateVoidHole(start + Vector3.forward * 6f + Vector3.right * .4f, 2f, 1f, 10f, null, false);
            world.CreateVoidHole(start + Vector3.forward * range, 2f, 0f, 10f, null, false);
            Assume.That(world.IsHole(start + Vector3.forward * 6f) && world.IsHole(start + Vector3.forward * range), Is.True, "both holes lie on the flight");

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(player.State, Is.EqualTo(CharacterState.Dashing));
            Assert.That(game.runWeapons.MissileHoleProof, Is.True, "the tamed flight is out of every hole's reach");
            await Until(() => player.State != CharacterState.Dashing, 3f, "the flight ends");

            Assert.That(player.IsAlive, Is.True, "Q25 A: no hole swallowed the flight");
            Assert.That(world.IsHole(player.Position), Is.False, "Q26 B: it flew on until it was clear of the hole at its end");
            Assert.That(Vector3.Dot(player.Position - start, Vector3.forward), Is.GreaterThan(range), "past the nominal range");
            Assert.That(Mathf.Abs(Vector3.Dot(player.Position - start, Vector3.right)), Is.LessThan(.05f), "no hole pulled it sideways");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FlyingBlade_ReturnThroughAWall_CutsWhatLiesBeforeTheWallButNothingBeyondIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            targets[2].DiscardWithoutReward();
            var knife = Equip(game, WeaponForm.Knife, 0);
            float hit = game.runWeapons.HitDamage(knife), playerHp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            var actor = Actors(knife, AttackMode.Knife).Single();
            await Until(() => actor.IsReturning, 3f, "the knife turns back from the second enemy");

            WorldMap.Active.CreateWall(game.player.Position + Vector3.forward * 1.5f, 6f, 60f, 0f, game.player);
            Physics.SyncTransforms();
            await Until(() => !knife.inFlight, 3f, "the knife still comes home through the wall");

            Assert.That(targets[0].Hp, Is.EqualTo(TargetHp - 2f * hit).Within(.01f), "the first enemy, before the wall, is cut by the return as well");
            Assert.That(game.player.Hp, Is.EqualTo(playerHp), "Q11 A: the wall shields you from the return");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FlyingBladeTameAndTwist_ACaughtKnife_SpinsWhereYouCaughtIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            Vector3 home = game.player.Position;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();

            game.player.DebugSetAim(Vector3.back);
            Place(targets[0], home + Vector3.forward * (.6f + targets[0].radius));
            var knife = Equip(game, WeaponForm.Knife, 0, UpgradeAxis.Twist | UpgradeAxis.Tame);
            float spin = game.runWeapons.HitDamage(knife) * game.runWeapons.upgrades.knifeSpinShare, playerHp = game.player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !knife.inFlight, 4f, "the knife comes back to you");

            Assert.That(knife.knifeCaught, Is.True, "you caught it");
            Assert.That(targets[0].Hp, Is.EqualTo(TargetHp - spin).Within(.01f), "the spin went off where you caught it");
            Assert.That(game.player.Hp, Is.EqualTo(playerHp), "neither the tamed return nor the spin cuts you");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task KnifeTexts_Chinese_NameTheFlyingBladeAndTheChainBlade()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && ArenaText.IsChinese, 12f, "the Chinese scene and locale");
            var weapons = GameManager.Instance.runWeapons;
            var texts = new List<string>
            {
                WeaponGuide.How(WeaponForm.Knife, 0), WeaponGuide.Backfire(WeaponForm.Knife, 0),
                WeaponGuide.How(WeaponForm.Knife, 1), WeaponGuide.Backfire(WeaponForm.Knife, 1),
            };
            var hand = weapons.Hand;
            var auto = weapons.Auto;
            try
            {
                for (int variant = 0; variant < 2; variant++)
                foreach (var axis in new[] { UpgradeAxis.Twist, UpgradeAxis.Tame })
                {
                    var open = UpgradeAxis.Infusion | (axis == UpgradeAxis.Twist ? UpgradeAxis.Tame : UpgradeAxis.Twist);
                    weapons.Hand = new[] { new RunWeapon { form = WeaponForm.Knife, variant = variant, revealed = true, axes = open }, null };
                    weapons.Auto = new RunWeapon[RunWeapons.AutoSlotCount];
                    var card = weapons.RollUpgradeCard();
                    Assert.That(card.Title, Does.Contain(variant == 0 ? "Knife · Flying Blade" : "Knife · Chain Blade"), "the card title names the combination");
                    texts.Add(card.Title + " " + card.Description);
                }
            }
            finally { weapons.Hand = hand; weapons.Auto = auto; }

            foreach (var text in texts) Assert.That(text, Does.Not.Contain("Throwing Knife").And.Not.Contain("Fling Hook"), text);
            Assert.That(texts[0], Does.Contain("Flying Blade"), "the Flying Blade card");
            Assert.That(texts[2], Does.Contain("Chain Blade"), "the Chain Blade card");
        }
    }
}
