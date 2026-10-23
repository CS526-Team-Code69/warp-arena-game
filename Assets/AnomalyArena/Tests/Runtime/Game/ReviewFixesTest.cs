using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    public class ReviewFixesTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const string LabScene = "Assets/AnomalyArena/Scenes/EnemyLab.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Whitebox_ExecuteShot_StillDrawsItsTracer()
        {
            var (game, _) = await V3CombatSmokeTest.StartSandbox();
            var vfx = V4CombatVfx.Active;
            Assume.That(vfx && vfx.items != null && vfx.items.Length > 0, Is.True, "the scene keeps the combat effect pool");
            var tuning = game.runWeapons.upgrades;
            bool art = Art.Enabled;
            float seconds = tuning.executeTracerSeconds;
            try
            {
                Art.Enabled = false;
                tuning.executeTracerSeconds = 1f;                                           
                Equip(game, WeaponForm.Railgun, 1);
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                Assert.That(vfx.items.Any(i => i && i.enabled), Is.False, "whitebox draws none of the illustrated effects");

                game.runWeapons.HandleInput(true, true, false);
                Assume.That(game.runWeapons.Tracers.Count, Is.GreaterThan(0), "the shot left a tracer");
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                Assert.That(vfx.items.Any(i => i && i.enabled), Is.True, "the Execute line is drawn in whitebox too");
            }
            finally
            {
                Art.Enabled = art;
                tuning.executeTracerSeconds = seconds;
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Hook_EnemyTouchingYou_IsCaughtInsteadOfTheOneBehind()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var near = targets[0];

            Place(near, game.player.Position + Vector3.forward * (game.player.radius + near.radius + .05f));
            var hook = Equip(game, WeaponForm.Knife, 1);

            game.runWeapons.HandleInput(true, true, false);
            Assume.That(hook.hookBusy, Is.True, "the hook goes out");
            await Until(() => near.State == CharacterState.Hooked || targets[1].State == CharacterState.Hooked || !hook.hookBusy && !hook.hooked, 2f,
                "the hook catches something or comes back");
            Assert.That(near.State, Is.EqualTo(CharacterState.Hooked), "the enemy the hook head starts inside is caught");
            Assert.That(targets[1].State, Is.Not.EqualTo(CharacterState.Hooked), "the hook does not fly through it to the next one");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task BlizzardTwist_SprayForcedToStop_ShattersNothing()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var tuning = game.runWeapons.upgrades;
            targets[1].DiscardWithoutReward(); targets[2].DiscardWithoutReward();
            var enemy = targets[0];
            Place(enemy, game.player.Position + Vector3.forward * 2f);
            var blizzard = Equip(game, WeaponForm.CryoCannon, 1, UpgradeAxis.Twist);
            var status = ElementStatus.For(enemy);
            status.ClearStatuses();
            status.Add(ElementKind.Frost, status.FrostThreshold + 1f, game.player);
            Assume.That(status.Frozen, Is.True);

            game.runWeapons.HandleInput(true, true, false);
            float start = Time.time;
            while (Time.time < start + 1f) { await Awaitable.NextFrameAsync(); game.runWeapons.HandleInput(false, true, false); }
            Assume.That(blizzard.heldTime, Is.GreaterThan(tuning.blizzardShatterMinHold), "held long enough that a release would shatter");
            float before = enemy.Hp;
            try
            {

                game.player.Stun(5f);
                await Awaitable.NextFrameAsync();
                await Awaitable.NextFrameAsync();
                Assert.That(blizzard.heldTime, Is.Zero, "the stun stopped the spray");
                Assert.That(enemy.Hp, Is.EqualTo(before), "a forced stop shatters nothing");
                Assert.That(status.Frozen, Is.True, "the enemy stays frozen");
            }
            finally { game.player.ClearWaveState(); }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ThrowingKnife_KillReturnThroughYou_CutsYouForTheBaseValue()
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var player = game.player;
            Assume.That(game.progression.Stats.DamageTakenMultiplier, Is.EqualTo(1f).Within(.001f), "starting stats take damage as dealt");
            Assume.That(ElementStatus.For(player).Shield, Is.Zero, "no Shield to soak the cut");
            var knife = Equip(game, WeaponForm.Knife, 0, mk: 3);
            var victim = targets[0];
            victim.ReceiveDamage(new DamageInfo { amount = victim.Hp - 1f, source = DamageSource.Poison, ignoreShield = true });
            float baseCut = game.runWeapons.roster.Get(WeaponForm.Knife).damageA;
            Assume.That(Mathf.Abs(game.runWeapons.HitDamage(knife) - baseCut), Is.GreaterThan(.5f), "Mk 3 scales the hit away from the base value");
            float hp = player.Hp;

            game.runWeapons.HandleInput(true, true, false);
            await Until(() => !victim.IsAlive, 2f, "the knife kills the enemy");
            await Until(() => !knife.inFlight, 3f, "the knife flies back to where it was thrown");
            Assert.That(hp - player.Hp, Is.EqualTo(baseCut).Within(.01f), "the kill return cuts you for the base value, not the scaled hit");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task CardTexts_EveryExpandedForm_FillsItsNumbersFromTheTuning()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var tuning = GameManager.Instance.runWeapons.upgrades;
            for (int form = (int)WeaponForm.GravGun; form <= (int)WeaponForm.SwarmLauncher; form++)
            for (int variant = 0; variant < 2; variant++)
            foreach (var axis in new[] { UpgradeAxis.Twist, UpgradeAxis.Tame })
            {
                string text = ArenaTerms.ExpandedUpgradeDescription((WeaponForm)form, variant, axis);
                Assert.That(text, Does.Not.Contain("{").And.Not.Contain("}"), $"{(WeaponForm)form} {variant} {axis}: every number filled in");
            }

            float range = tuning.wildfireFenceRange;
            int cap = tuning.icePatchTamedCap;
            try
            {
                tuning.wildfireFenceRange = 7.5f;
                tuning.icePatchTamedCap = 7;
                Assert.That(ArenaTerms.ExpandedUpgradeDescription(WeaponForm.Flamethrower, 1, UpgradeAxis.Twist), Does.Contain("7.5"), "a retuned fence range shows");
                Assert.That(ArenaTerms.ExpandedUpgradeDescription(WeaponForm.CryoCannon, 0, UpgradeAxis.Tame), Does.Contain("7"), "a retuned patch cap shows");
            }
            finally
            {
                tuning.wildfireFenceRange = range;
                tuning.icePatchTamedCap = cap;
            }
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task VoidTrial_PlayerHoles_NeverTakeTheProphetsSlots()
        {
            var (game, world, _) = await StartTrial();
            var bossSlots = Object.FindObjectsByType<BlackHole>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(h => h && h.name.Contains("(boss ")).ToArray();
            Assume.That(bossSlots, Is.Not.Empty, "the Prophet's slots are made at start");
            Vector3 centre = Flat(world.trialPlatform.position);
            for (int i = 0; i < 6; i++)
                world.CreateVoidHole(centre + Quaternion.Euler(0f, 60f * i, 0f) * Vector3.forward * 5f, 1.5f, 0f, 5f, game.player, true);
            Assert.That(bossSlots.Any(h => h.gameObject.activeSelf), Is.False, "six player holes stay in the player's own slots");
        }

        [Test, LoadScene(LabScene), Category("Integration"), Timeout(60000)]
        public async Task VoidTrial_Ending_ClearsTheBossShotsStillInFlight()
        {
            var (game, world, hollow) = await StartTrial();
            game.player.Invulnerable = true;
            try
            {
                hollow.ForceNextBossMove(Enemy.RingMove);
                SetPrivate(hollow, "cooldown", 0f);
                await Until(() => game.waves.preparedBolts.Any(s => s && s.gameObject.activeSelf && s.IsBossShot), 3f, "a ring in flight");
                world.CompleteTrial();
                Assert.That(game.waves.preparedBolts.Any(s => s && s.gameObject.activeSelf && s.IsBossShot), Is.False,
                    "the shots end with the trial instead of holding pool slots");
            }
            finally { game.player.Invulnerable = false; }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task TyrantPhaseChange_DuringACharge_StillCastsTheRoyalShieldAfterIt()
        {
            var (game, targets, tyrant) = await TyrantSandbox(10f);
            foreach (var t in targets) t.DiscardWithoutReward();
            tyrant.ForceNextBossMove(Enemy.ChargeMove);
            SetPrivate(tyrant, "cooldown", 0f);
            await Until(() => tyrant.Charging, 5f, "the charge");
            tyrant.ReceiveDamage(new DamageInfo { amount = tyrant.maxHp * .4f, source = DamageSource.Wall, ignoreShield = true });
            await Until(() => tyrant.BossPhase >= 2, 1f, "a phase change during the charge");
            await Until(() => tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.RoyalShieldMove, 8f, "the royal shield cast once the charge is over");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task TyrantLeap_AtAPlayerInAGravityRing_LandsWhereItCanWalk()
        {
            var (game, targets, tyrant) = await TyrantSandbox(14f);
            foreach (var t in targets) t.DiscardWithoutReward();
            var world = WorldMap.Active;
            var hole = world.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active);
            Assume.That(hole, Is.Not.Null, "the map has an active black hole");
            float ring = hole.KillRadius + hole.gravityCells * .5f;
            Vector3 spot = Vector3.zero;
            bool found = false;
            for (int a = 0; a < 360 && !found; a += 15)
            {
                spot = Flat(hole.Centre + Quaternion.Euler(0f, a, 0f) * Vector3.forward * ring);

                found = world.Contains(spot, tyrant.Radius) && !world.IsWall(spot, tyrant.Radius) && !world.IsHole(spot) && !world.IsWalkable(spot, tyrant.Radius);
            }

            Assume.That(found, Is.True, "a spot in the gravity ring");

            var body = game.player.GetComponent<Rigidbody>();
            var constraints = body.constraints;
            try
            {
                body.constraints = RigidbodyConstraints.FreezeAll;
                Place(game.player, spot);
                tyrant.ForceNextBossMove(Enemy.LeapMove);
                SetPrivate(tyrant, "cooldown", 0f);
                await Until(() => tyrant.WindingUp && tyrant.BossAttackIndex == Enemy.LeapMove, 4f, "the leap wind-up");
                Assert.That(world.IsWalkable(tyrant.LeapTarget, tyrant.Radius), Is.True, "it lands where it can walk: no gravity ring, no hole, no wall");
            }
            finally { body.constraints = constraints; }
        }

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

        static Vector3 Flat(Vector3 v) => Query.Flat(v);

        static void SetPrivate(Enemy enemy, string field, float value) =>
            typeof(Enemy).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(enemy, value);

        static void Place(Combatant body, Vector3 at)
        {
            body.ClearWaveState();
            V3CombatSmokeTest.Teleport(body, new Vector3(at.x, 0f, at.z));
            Physics.SyncTransforms();
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task<(GameManager game, Enemy[] targets, Enemy tyrant)> TyrantSandbox(float ahead)
        {
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            game.player.Invulnerable = true;
            game.player.enabled = true;                                           
            var tyrant = game.waves.SpawnLabTyrant(game.player.Position + Vector3.forward * ahead);
            Assume.That(tyrant && tyrant.IsBoss, Is.True, "the scene keeps the Tyrant");

            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            return (game, targets, tyrant);
        }

        static async Task<(GameManager game, WorldMap world, Enemy hollow)> StartTrial()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active && Object.FindAnyObjectByType<EnemyLab>(), 12f,
                "scene, localization, map and lab");
            var game = GameManager.Instance;
            float deadline = Time.realtimeSinceStartup + 20f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The lab never reached combat; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Loadout) game.ExecuteUiCommand(ArenaUiCommand.ChooseLoadout, 0);
                await Awaitable.NextFrameAsync();
            }

            var lab = Object.FindAnyObjectByType<EnemyLab>();
            var world = WorldMap.Active;
            lab.selected = EnemyKind.Hollow;
            Assume.That(lab.Spawn(), Is.SameAs(world.hollow), "the lab enters the void trial");
            Assume.That(world.TrialActive, Is.True);
            await Awaitable.FixedUpdateAsync();
            return (game, world, world.hollow);
        }
    }
}
