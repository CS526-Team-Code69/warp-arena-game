using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class AutoWeaponsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ClosedBlade_PickupOnTheBladePath_IsKnockedOutward_OpenBladeLeavesIt()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var orbs = game.orbs;
            Assume.That(game.progression.Stats.pickupRange, Is.LessThan(2.2f), "an HP orb on the blade circle is not pulled in");
            Vector3 origin = game.player.Position;
            var orb = orbs.pool.First(o => o && !o.InUse);
            orb.Begin(origin + Vector3.forward * 2.5f, OrbKind.Hp, 1f, orbs.hpMaterial, orbs.hpTexture);
            float Distance() => Query.Flat(orb.Position - origin).magnitude;

            EquipAuto(game, WeaponForm.OrbitBlades, 1);
            await Seconds(1.2f);                                                        
            Assert.That(orb.InUse, Is.True);
            Assert.That(Distance(), Is.EqualTo(2.5f).Within(.2f), "the Open Blade leaves pickups alone");

            EquipAuto(game, WeaponForm.OrbitBlades, 0);
            await Until(() => Distance() > 3.5f, 2f, "a Closed Blade tip knocks the orb");
            await Seconds(.8f);
            Assert.That(Distance(), Is.GreaterThan(4.8f), "about OrbPool.repelDistance cells outward, away from you");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ClosedBlade_CorpseOnTheBladePath_IsKnockedOutward()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var corpse = targets[0];
            corpse.Kill();
            Assume.That(corpse.CorpseActive, Is.True, "a pooled enemy killed by HP loss leaves a corpse");
            await Seconds(1f);                                 
            Vector3 origin = game.player.Position;
            V3CombatSmokeTest.Teleport(corpse, origin + Vector3.forward * 2.5f);
            float Along() => Vector3.Dot(Query.Flat(corpse.Position - origin), Vector3.forward);

            EquipAuto(game, WeaponForm.OrbitBlades, 0);
            await Until(() => Along() > 3.5f, 2f, "a Closed Blade tip knocks the corpse");
            await Seconds(1f);

            Assert.That(corpse.CorpseActive, Is.True);
            Assert.That(Along(), Is.GreaterThan(4.8f), "about orbitCorpseKnock cells outward, away from you");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Gunner_EmptyMagazine_DropsToTheGround_StepOnItOrWaitForItToComeBack()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            int clip = tuning.gunnerClip;
            float back = tuning.gunnerAutoReturn;
            try
            {
                tuning.gunnerClip = 3;
                tuning.gunnerAutoReturn = .8f;
                Vector3 origin = game.player.Position;
                var gunner = EquipAuto(game, WeaponForm.SentryDrone, 0);
                Assert.That(weapons.AnomalySlotDetail(gunner), Does.Contain("3"), "the slot shows the rounds left");

                await Until(() => weapons.DroneDown(gunner), 2f, "three shots empty the magazine");
                Assert.That(gunner.droneShots, Is.EqualTo(3));
                Assert.That(RunWeapons.ValidTeleportPoint(gunner.dronePoint, tuning.droneRadius), Is.True, "it lies on clear ground");
                Assert.That(Query.Flat(gunner.dronePoint - origin).magnitude, Is.LessThan(2f), "where it hovered, by you");
                await Seconds(.3f);
                Assert.That(weapons.DroneDown(gunner), Is.True, "it cools down on the ground");
                Assert.That(gunner.droneShots, Is.EqualTo(3), "without firing");
                Assert.That(weapons.AnomalySlotDetail(gunner), Does.Contain("step on it"), "the slot says to step on it");

                V3CombatSmokeTest.Teleport(game.player, gunner.dronePoint);
                await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
                Assert.That(weapons.DroneDown(gunner), Is.False, "stepping on it sends it back up at once");
                Assert.That(gunner.droneShots, Is.LessThan(3), "with a full magazine");

                V3CombatSmokeTest.Teleport(game.player, origin);
                await Until(() => weapons.DroneDown(gunner), 2f, "it empties the magazine again");
                float downAt = Time.time;
                await Until(() => !weapons.DroneDown(gunner), 3f, "left alone it flies back by itself");
                Assert.That(Time.time - downAt, Is.EqualTo(tuning.gunnerAutoReturn).Within(.25f), "after gunnerAutoReturn seconds");
            }
            finally { tuning.gunnerClip = clip; tuning.gunnerAutoReturn = back; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Gunner_DroppedDrone_WaitsOnTheBattlefieldThroughTheTrial_AndAWaveEndBringsItBackFull()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            int clip = tuning.gunnerClip;
            try
            {
                tuning.gunnerClip = 3;
                var gunner = EquipAuto(game, WeaponForm.SentryDrone, 0);
                await Until(() => weapons.DroneDown(gunner), 2f, "three shots empty the magazine");
                Vector3 drop = gunner.dronePoint;
                float left = gunner.droneDownLeft;

                weapons.FreezeBattlefieldForTrial(true);
                Assert.That(weapons.DroneDown(gunner), Is.False, "the void trial gets a drone flying by you");
                Assert.That(gunner.droneShots, Is.Zero, "with a full magazine");
                weapons.FreezeBattlefieldForTrial(false);
                Assert.That(weapons.DroneDown(gunner), Is.True, "back on the battlefield the drone still lies where it dropped");
                Assert.That(gunner.dronePoint, Is.EqualTo(drop));
                Assert.That(gunner.droneDownLeft, Is.EqualTo(left).Within(.0001f), "with the same time left");

                weapons.ClearWaveEffects();                        
                Assert.That(weapons.DroneDown(gunner), Is.False, "the wave end brings it back to you");
                Assert.That(gunner.droneShots, Is.Zero, "with a full magazine");
            }
            finally { tuning.gunnerClip = clip; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Whirl_LoneFarEnemy_IsSlammedWhereYouStoodTwoSecondsAgo_HittingItsNeighboursButNotYou()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            Assume.That(game.progression.Stats.critChance, Is.Zero, "no crit changes a hit");
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            targets[2].DiscardWithoutReward();
            Vector3 origin = game.player.Position, before = origin + Vector3.forward * 2f;
            Place(targets[0], origin + Vector3.forward * 9f, movable: true);
            Place(targets[1], before + Vector3.forward * 1.2f);
            V3CombatSmokeTest.Teleport(game.player, before);
            weapons.ClearWaveEffects();                                             
            var whirl = EquipAuto(game, WeaponForm.SentryDrone, 1);
            whirl.cooldown = 99f;                                       

            await Seconds(tuning.whirlAfterimageSeconds + .4f);
            V3CombatSmokeTest.Teleport(game.player, origin);
            float hp = game.player.Hp, hit = weapons.HitDamage(whirl);
            whirl.cooldown = 0f;
            await Until(() => whirl.whirled, 1f, "the beam locks on the lone enemy");
            Assert.That(whirl.whirled, Is.SameAs(targets[0]), "the one 5 to 12 cells away with nobody near it");
            Assert.That(Query.Flat(whirl.whirlTo - before).magnitude, Is.LessThan(.3f), "it will land on your afterimage");
            await Until(() => !whirl.whirled, 3f, "the slam");
            await Awaitable.FixedUpdateAsync();

            Assert.That(Query.Flat(targets[0].Position - before).magnitude, Is.LessThan(.3f), "it comes down where you stood");
            Assert.That(targets[0].State, Is.EqualTo(CharacterState.Normal), "and is let go");
            Assert.That(targets[0].maxHp - targets[0].Hp, Is.EqualTo(hit).Within(.01f), "the thrown enemy takes one weapon hit");
            Assert.That(targets[1].maxHp - targets[1].Hp, Is.EqualTo(hit).Within(.01f), "and so does the enemy beside the landing");
            Assert.That(game.player.Hp, Is.EqualTo(hp), "you never do");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Deflect_Field_SlowsAndLightlyHitsTheEnemiesInside_NotBehindAWall()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var weapons = game.runWeapons;
            Place(targets[0], game.player.Position + Vector3.forward * 2f);
            var ring = EquipAuto(game, WeaponForm.ShieldRing, 0);

            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(targets[0].ExternalMoveMultiplier, Is.EqualTo(weapons.upgrades.deflectSlow).Within(.001f), "an enemy inside the ring moves slower");
            Assert.That(targets[1].ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "one outside it does not");
            float hp = targets[0].Hp;
            await Seconds(weapons.CycleSeconds(ring) + .15f);
            Assert.That(hp - targets[0].Hp, Is.EqualTo(weapons.HitDamage(ring)).Within(.01f), "one light hit a cycle (owner 2026-10-27, Q99)");
            Assert.That(targets[1].Hp, Is.EqualTo(targets[1].maxHp), "nothing outside the ring");

            WallAhead(game, 1f, 4f);
            await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
            Assert.That(targets[0].ExternalMoveMultiplier, Is.EqualTo(1f).Within(.001f), "a wall between you shields it");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Deflect_EnemyShotComingIn_FizzlesAtTheRingEdge()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var tuning = game.runWeapons.upgrades;
            float chance = tuning.deflectChance;
            try
            {
                var shooter = targets[2];
                targets[0].DiscardWithoutReward(); targets[1].DiscardWithoutReward();
                var ring = EquipAuto(game, WeaponForm.ShieldRing, 0);
                float radius = game.runWeapons.FieldRadius(ring);

                async Task<float> Shoot()
                {
                    var bolt = game.waves.preparedBolts.First(b => b && !b.gameObject.activeSelf);
                    bolt.Launch(shooter, game.player.Position + Vector3.forward * (radius + 4f), Vector3.back, 3f, 0f, 10f);
                    float last = float.PositiveInfinity;
                    await Until(() =>
                    {
                        if (bolt.gameObject.activeSelf) last = Query.Flat(bolt.transform.position - game.player.Position).magnitude;
                        return !bolt.gameObject.activeSelf;
                    }, 4f, "the shot ends");
                    return last;
                }

                tuning.deflectChance = 0f;
                float hp = game.player.Hp;
                Assert.That(await Shoot(), Is.LessThan(1.5f), "with no chance the shot flies on into you");
                Assert.That(game.player.Hp, Is.LessThan(hp));
                game.player.ClearWaveState();                           

                tuning.deflectChance = 1f;
                hp = game.player.Hp;
                Assert.That(await Shoot(), Is.GreaterThan(radius - 1f), "a deflected shot fizzles at the ring's edge");
                Assert.That(game.player.Hp, Is.EqualTo(hp), "and never reaches you");
            }
            finally { tuning.deflectChance = chance; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Deflect_OwnBulletLeavingTheRing_TurnsAtMostTheMaxAngle_TheRailgunSlugGoesStraight()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            float chance = tuning.deflectChance;
            var savedRandom = UnityEngine.Random.state;
            try
            {
                tuning.deflectChance = 1f;
                UnityEngine.Random.InitState(527);
                var ring = EquipAuto(game, WeaponForm.ShieldRing, 0);
                float radius = weapons.FieldRadius(ring);

                var gun = Equip(game, WeaponForm.Minigun, 1);
                int deflected = weapons.OwnShotsDeflected;
                weapons.HandleInput(true, true, false);
                var bullet = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == gun);
                weapons.HandleInput(false, false, true);
                Vector3 aim = Query.Flat(bullet.direction).normalized;
                await Until(() => Query.Flat(bullet.transform.position - game.player.Position).magnitude > radius + .5f, 2f, "the bullet leaves the ring");
                await Awaitable.NextFrameAsync();                                        

                Assert.That(weapons.OwnShotsDeflected, Is.EqualTo(deflected + 1), "it is knocked off course as it crosses the edge");
                Assert.That(Vector3.Angle(aim, Query.Flat(bullet.direction)), Is.LessThanOrEqualTo(tuning.deflectOwnAngle + .01f), "by up to the maximum");

                var rail = Equip(game, WeaponForm.Railgun, 0);
                weapons.HandleInput(true, true, false);
                AttackActor slug = null;
                await Until(() => (slug = AttackPool.Instance.actors.FirstOrDefault(a => a && a.gameObject.activeSelf && a.weapon == rail)) != null, 3f, "the Lance fires");
                aim = Query.Flat(slug.direction).normalized;
                deflected = weapons.OwnShotsDeflected;
                await Until(() => !slug.gameObject.activeSelf || Query.Flat(slug.transform.position - game.player.Position).magnitude > radius + .5f, 2f, "the slug leaves the ring");
                Assert.That(Vector3.Angle(aim, Query.Flat(slug.direction)), Is.LessThan(.01f), "the Railgun's slug is not a deflected shot (Q93)");
                Assert.That(weapons.OwnShotsDeflected, Is.EqualTo(deflected));
            }
            finally { tuning.deflectChance = chance; UnityEngine.Random.state = savedRandom; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Deflect_OwnLobLeavingTheRing_ItsLandingTurnsAboutWhereItCrosses()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            foreach (var target in targets) target.DiscardWithoutReward();
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            float chance = tuning.deflectChance, angle = tuning.deflectOwnAngle;
            try
            {
                tuning.deflectChance = 1f;
                tuning.deflectOwnAngle = 10f;                                                            
                var ring = EquipAuto(game, WeaponForm.ShieldRing, 0);
                float radius = weapons.FieldRadius(ring);
                var cloud = Equip(game, WeaponForm.SporeLauncher, 0);
                game.player.DebugSetAimPoint(game.player.Position + Vector3.forward * 8f);
                int deflected = weapons.OwnShotsDeflected;
                weapons.HandleInput(true, true, false);
                var pod = AttackPool.Instance.actors.First(a => a && a.gameObject.activeSelf && a.weapon == cloud);
                weapons.HandleInput(false, false, true);
                Vector3 aimed = Query.Flat(pod.point), bentAt = default;
                await Until(() =>
                {
                    if (weapons.OwnShotsDeflected > deflected) { bentAt = Query.Flat(pod.transform.position); return true; }
                    return pod.mode != AttackMode.Lob || !pod.gameObject.activeSelf;
                }, 3f, "the pod crosses the ring's edge");

                Assert.That(weapons.OwnShotsDeflected, Is.EqualTo(deflected + 1), "the pod is knocked off course");
                Assert.That(pod.mode, Is.EqualTo(AttackMode.Lob), "in the air, before it lands");
                Assert.That(Query.Flat(bentAt - game.player.Position).magnitude, Is.EqualTo(radius).Within(.6f), "at the ring's edge");

                float turned = Vector3.Angle(aimed - bentAt, Query.Flat(pod.point) - bentAt);
                Assert.That(turned, Is.LessThanOrEqualTo(tuning.deflectOwnAngle + 3f), "its unflown path and landing turn by up to the maximum");
                Assert.That(RunWeapons.ValidTeleportPoint(pod.point, .2f), Is.True, "onto legal ground (rule R7)");
            }
            finally { tuning.deflectChance = chance; tuning.deflectOwnAngle = angle; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Drain_ShieldTakesOneAttackWhole_TheNextAttackSoonAfterCostsEnergyOnce()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var tuning = game.runWeapons.upgrades;
            float wait = tuning.drainShieldWait;
            try
            {
                tuning.drainShieldWait = .4f;
                game.progression.UnlockEnergy();
                Assume.That(game.progression.TrySpendEnergy(50f), Is.True);
                float energy = game.progression.Energy;
                var drain = EquipAuto(game, WeaponForm.ShieldRing, 1);
                await Until(() => drain.drainShieldUp, 2f, "the Energy Shield forms");
                var player = game.player;
                var enemy = targets[0];
                float hp = player.Hp;

                Assert.That(player.ReceiveDamage(DamageInfo.Attack(4f, enemy, Vector3.forward, 3f)), Is.False, "the shield takes the attack whole");
                Assert.That(player.Hp, Is.EqualTo(hp), "no damage");
                Assert.That(drain.drainShieldUp, Is.False, "and breaks");
                Assert.That(game.progression.Energy, Is.EqualTo(energy + tuning.drainBlockEnergy).Within(.01f), "the block gives Energy (Q97)");
                Assert.That(game.runWeapons.AnomalySlotDetail(drain), Does.Contain("Overload"), "the slot shows the overload");

                energy = game.progression.Energy;
                Assert.That(player.ReceiveDamage(DamageInfo.Attack(4f, enemy)), Is.True, "the next attack lands");
                Assert.That(player.Hp, Is.LessThan(hp));
                Assert.That(game.progression.Energy, Is.EqualTo(energy - tuning.drainOverloadEnergy).Within(.01f), "and so soon after the break it costs Energy (Q96)");

                energy = game.progression.Energy;
                player.ClearWaveState();                           
                Assert.That(player.ReceiveDamage(DamageInfo.Attack(4f, enemy)), Is.True);
                Assert.That(game.progression.Energy, Is.EqualTo(energy).Within(.01f), "only once per break");
                await Until(() => drain.drainShieldUp, 2f, "quiet again, the shield forms again");
            }
            finally { tuning.drainShieldWait = wait; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task DrainInfused_EachBlock_ReleasesTheElementOnEnemiesWithinReach_UninfusedReleasesNothing()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            float wait = tuning.drainShieldWait;
            try
            {
                tuning.drainShieldWait = .4f;
                game.progression.UnlockEnergy();
                Assume.That(game.progression.TrySpendEnergy(50f), Is.True);
                var player = game.player;
                Place(targets[0], player.Position + Vector3.forward * 1.5f);
                Place(targets[1], player.Position + Vector3.forward * 6f);
                var near = targets[0]; var far = targets[1];
                var drain = EquipAuto(game, WeaponForm.ShieldRing, 1);
                Assume.That(weapons.HitDamage(drain), Is.GreaterThan(0f), "damageB carries the released hit");

                await Until(() => drain.drainShieldUp, 2f, "the Energy Shield forms");
                Assert.That(player.ReceiveDamage(DamageInfo.Attack(4f, far)), Is.False, "the shield takes the attack");
                await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
                Assert.That(near.Hp, Is.EqualTo(near.maxHp), "uninfused, a block releases nothing");

                drain.axes |= UpgradeAxis.Infusion; drain.element = ElementKind.Frost;
                await Until(() => drain.drainShieldUp, 2f, "the Energy Shield forms again");
                float hp = player.Hp, energy = game.progression.Energy;
                Assert.That(player.ReceiveDamage(DamageInfo.Attack(4f, far)), Is.False, "the shield takes the attack whole");
                await Until(() => near.Hp < near.maxHp, 1f, "the block releases the element");
                Assert.That(near.maxHp - near.Hp, Is.EqualTo(weapons.HitDamage(drain)).Within(.01f), "one hit of damageB, scaled like the weapon");
                Assert.That(ElementStatus.For(near).Frost, Is.GreaterThan(0f), "carrying the infused element");
                Assert.That(far.Hp, Is.EqualTo(far.maxHp), $"nothing beyond {tuning.drainInfuseRadius} cells");
                Assert.That(ElementStatus.For(far).Frost, Is.Zero);
                Assert.That(player.Hp, Is.EqualTo(hp), "you take nothing");
                Assert.That(game.progression.Energy, Is.EqualTo(energy + tuning.drainBlockEnergy).Within(.01f), "the block still gives Energy");
                float once = near.Hp;
                await Awaitable.NextFrameAsync(); await Awaitable.NextFrameAsync();
                Assert.That(near.Hp, Is.EqualTo(once), "one release per block");
            }
            finally { tuning.drainShieldWait = wait; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Drain_BurnAndPoisonTicks_NeitherBreakTheShieldNorRestartTheWait_HigherMkWaitsLess()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.runWeapons.Hand[0] = game.runWeapons.Hand[1] = null;
            var weapons = game.runWeapons;
            var tuning = weapons.upgrades;
            float wait = tuning.drainShieldWait;
            try
            {
                tuning.drainShieldWait = .6f;
                var drain = EquipAuto(game, WeaponForm.ShieldRing, 1);
                await Until(() => drain.drainShieldUp, 2f, "the Energy Shield forms");
                var player = game.player;
                float hp = player.Hp;

                player.ReceiveDamage(new DamageInfo { amount = 1f, source = DamageSource.Burn });
                Assert.That(player.Hp, Is.LessThan(hp), "a burn tick still hurts");
                Assert.That(drain.drainShieldUp, Is.True, "but neither breaks the shield (Q94)");

                player.ReceiveDamage(DamageInfo.Attack(2f, targets[0]));
                Assert.That(drain.drainShieldUp, Is.False, "an enemy attack does");
                float broke = Time.time, nextTick = Time.time + .15f;
                await Until(() =>
                {
                    if (Time.time >= nextTick) { nextTick += .2f; player.ReceiveDamage(new DamageInfo { amount = .5f, source = DamageSource.Poison, ignoreShield = true }); }
                    return drain.drainShieldUp;
                }, 3f, "the shield forms again while poison ticks");
                Assert.That(Time.time - broke, Is.LessThan(tuning.drainShieldWait + .3f), "nor do the ticks restart the wait");

                var mk3 = new RunWeapon { form = WeaponForm.ShieldRing, variant = 1, mk = 3 };
                Assert.That(weapons.DrainShieldWait(mk3), Is.EqualTo(tuning.drainShieldWait / weapons.MkDamage(mk3)).Within(.0001f), "a higher Mk divides the wait (Q99)");
                Assert.That(weapons.DrainShieldWait(mk3), Is.LessThan(weapons.DrainShieldWait(drain)));
            }
            finally { tuning.drainShieldWait = wait; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task RingCards_DrainAndDeflectBothTakeInfusion_AndTheCardsReadTheLiveTuning()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var weapons = GameManager.Instance.runWeapons;
            var tuning = weapons.upgrades;
            int clip = tuning.gunnerClip;
            try
            {
                weapons.Hand = new RunWeapon[2];

                var drained = new RunWeapon { form = WeaponForm.ShieldRing, variant = 1, revealed = true };
                weapons.Auto = new[] { drained, null };
                var drainCard = weapons.RollUpgradeCard();
                Assert.That(drainCard, Is.Not.Null, "the Drain is offered an Infusion");
                Assert.That(((WeaponUpgradeOffer)drainCard.Payload).Weapon, Is.SameAs(drained));
                Assert.That(((WeaponUpgradeOffer)drainCard.Payload).Axis, Is.EqualTo(UpgradeAxis.Infusion));
                var deflect = new RunWeapon { form = WeaponForm.ShieldRing, variant = 0, revealed = true };
                weapons.Auto = new[] { deflect, null };
                var card = weapons.RollUpgradeCard();
                Assert.That(card, Is.Not.Null);
                var offer = (WeaponUpgradeOffer)card.Payload;
                Assert.That(offer.Weapon, Is.SameAs(deflect));
                Assert.That(offer.Axis, Is.EqualTo(UpgradeAxis.Infusion), "the Deflect's light hit carries one");

                Assume.That(ArenaText.IsChinese, Is.True, "the Chinese scene starts in Chinese");
                Assert.That(ArenaTerms.WeaponForm(WeaponForm.ShieldRing), Is.EqualTo("Energy Ring"), "owner 2026-10-27 (Q100)");
                Assert.That(ArenaTerms.WeaponAnomaly(WeaponForm.ShieldRing, 0), Is.EqualTo("Deflect"));
                Assert.That(WeaponGuide.Backfire(WeaponForm.OrbitBlades, 0), Does.Contain("pickups").And.Contain("corpses"));
                Assert.That(WeaponGuide.How(WeaponForm.ShieldRing, 1), Does.Contain("灌注后").And.Contain($"{tuning.drainInfuseRadius:0.##} 格内"), "the Drain card says what an Infusion does (P23)");
                Assert.That(WeaponGuide.How(WeaponForm.ShieldRing, 0), Does.Contain("轻伤").And.Contain("Infusion"), "the Deflect card states its light hit (P22)");
                tuning.gunnerClip = 17;
                Assert.That(WeaponGuide.How(WeaponForm.SentryDrone, 0), Does.Contain("17"), "the Gunner card reads the live magazine");

                var drain = new RunWeapon { form = WeaponForm.ShieldRing, variant = 1, mk = 3, revealed = true };
                typeof(GameManager).GetMethod("ShowWeaponIntro", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(GameManager.Instance, new object[] { drain, null });
                string body = GameManager.Instance.UiSnapshot.weaponIntroBody;
                Assert.That(body, Does.Contain($"{weapons.DrainShieldWait(drain):0.#} s without an enemy hit").And.Not.Contain("per hit"));
                Assert.That(weapons.DrainShieldWait(drain), Is.LessThan(tuning.drainShieldWait), "Mk3 waits less than the Mk1 wait");
            }
            finally { tuning.gunnerClip = clip; }
        }

        static RunWeapon EquipAuto(GameManager game, WeaponForm form, int variant)
        {
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true };
            game.runWeapons.Auto[0] = weapon;
            return weapon;
        }

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant)
        {
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true, rounds = game.runWeapons.ClipSize(form, variant) };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        static void Place(Enemy enemy, Vector3 at, bool movable = false)
        {
            V3CombatSmokeTest.Teleport(enemy, at);
            if (movable) enemy.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
            Physics.SyncTransforms();
        }

        static void WallAhead(GameManager game, float distance, float width)
        {
            WorldMap.Active.CreateWall(game.player.Position + Query.Flat(game.player.AimDirection).normalized * distance, width, 60f, 0f, game.player);
            Physics.SyncTransforms();
        }

        static async Task Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) await Awaitable.NextFrameAsync();
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
    }
}
