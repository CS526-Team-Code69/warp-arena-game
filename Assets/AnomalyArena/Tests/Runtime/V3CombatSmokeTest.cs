using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    [Category("V3Smoke")]
    public class V3CombatSmokeTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const float TargetHp = 100f;
        UnityEngine.Random.State savedRandom;

        [SetUp]
        public void SetUp() => savedRandom = UnityEngine.Random.state;

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Random.state = savedRandom;
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Knife_TwoLiveTargets_HitsEachOnceAndReturnsToSavedPool()
        {
            var sandbox = await StartSandbox();
            var game = sandbox.game;
            var first = sandbox.targets[0];
            var second = sandbox.targets[1];
            sandbox.targets[2].DiscardWithoutReward();
            var knife = Equip(game, WeaponForm.Knife, 0);
            float expected = game.runWeapons.roster.Get(WeaponForm.Knife).damageA
                * game.runWeapons.startingDamageFactor * game.runWeapons.crowdDamageMultiplier;

            game.runWeapons.HandleInput(true, true, false);
            var actor = AttackPool.Instance.actors.FirstOrDefault(a => a.gameObject.activeSelf && a.weapon == knife);
            Assert.That(actor, Is.Not.Null, "saved attack pool supplies the thrown knife");
            Assert.That(knife.inFlight, Is.True, "throw owns the hand slot while outbound");
            await Until(() => second.Hp < TargetHp && actor.IsReturning, 4f, "knife hits two targets and starts returning");
            await Until(() => !knife.inFlight, 4f, "knife return completes");

            Assert.That(first.Hp, Is.EqualTo(TargetHp - expected).Within(.001f), "first target receives exactly one hit");
            Assert.That(second.Hp, Is.EqualTo(TargetHp - expected).Within(.001f), "second target receives exactly one hit");
            Assert.That(actor.gameObject.activeSelf, Is.False, "return releases the saved actor");
            Assert.That(game.player.Hp, Is.EqualTo(25f), "empty return does not damage owner");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task HumanRocket_ThreeTargetsInFlight_DealsTheSameHitToEachWithoutRepeatedContacts()
        {
            var sandbox = await StartSandbox();
            var game = sandbox.game;
            Equip(game, WeaponForm.RocketLauncher, 1);

            float hit = game.runWeapons.roster.Get(WeaponForm.RocketLauncher).damageB
                * game.runWeapons.startingDamageFactor * game.runWeapons.crowdDamageMultiplier;

            game.runWeapons.HandleInput(true, true, false);
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Dashing), "accepted rocket launch begins flight");
            await Until(() => game.player.State != CharacterState.Dashing, 4f, "human rocket flight ends");

            Assert.That(sandbox.targets.Select(e => e.Hp).ToArray(),
                Is.EqualTo(new[] { TargetHp - hit, TargetHp - hit, TargetHp - hit }).Within(.001f), "all three contacts deal the same hit exactly once");
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal), "flight restores normal control");
            Assert.That(game.player.Hp, Is.EqualTo(25f), "clear flight does not cause wall/self damage");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ReceiveDamage_SubOneAttack_SpendsOneShieldThenOneHpWithoutShieldBypass()
        {
            var sandbox = await StartSandbox();
            var target = sandbox.targets[0];
            var board = ElementStatus.For(target);
            board.SetShield(5f);
            var attack = DamageInfo.Attack(.1f, sandbox.game.player);

            target.ReceiveDamage(attack);

            Assert.That(board.Shield, Is.EqualTo(4f), "sub-one accepted hit spends one shield");
            Assert.That(target.Hp, Is.EqualTo(TargetHp), "fully absorbed damage does not leak into HP");
            board.SetShield(0f);
            target.ReceiveDamage(attack);
            Assert.That(target.Hp, Is.EqualTo(TargetHp - 1f), "unshielded sub-one accepted hit deals one HP");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task FrostNovaAndPocketVoid_SavedSkillAndWorldHooks_FreezeAndSwallowWithDirectXp()
        {
            var sandbox = await StartSandbox();
            var game = sandbox.game;
            var near = sandbox.targets[0];
            var swallowed = sandbox.targets[1];
            game.player.DebugSetAimPoint(swallowed.Position);
            Assert.That(game.skills.Learn(SkillId.FrostNova), Is.True, "learn Frost Nova from saved catalog");
            Assert.That(game.skills.Learn(SkillId.PocketVoid), Is.True, "learn Pocket Void from saved catalog");
            float before = game.progression.Energy;

            Assert.That(game.skills.TryCast(0), Is.True, "Frost Nova cast accepted");
            Assert.That(ElementStatus.For(near).Frozen, Is.True, "nearby small target is frozen through actual status board");
            Assert.That(game.progression.Energy, Is.EqualTo(before - 30f), "Frost Nova spends thirty energy");
            Assert.That(game.skills.TryCast(1), Is.True, "Pocket Void cast accepted");
            var hole = WorldMap.Active.temporaryHole;
            Assert.That(hole.gameObject.activeSelf && hole.Temporary, Is.True, "saved temporary hole is activated");
            Assert.That(hole.size, Is.EqualTo(3f), "pocket void diameter");
            Assert.That(hole.gravityCells, Is.EqualTo(1f), "pocket void gravity border");
            Assert.That(hole.Contains(swallowed.Position, game.player), Is.True, "Pocket Void also accepts its own caster into the kill core");
            Assert.That(game.progression.Energy, Is.EqualTo(before - 80f), "both costs are paid exactly once");
            float expectedXp = game.waves.ExperienceFor(swallowed);
            await Until(() => !swallowed.IsAlive, 3f, "saved world loop swallows target inside temporary hole");

            Assert.That(swallowed.DeathCause, Is.EqualTo(DeathCause.FellIntoGap), "void uses the fall death path");
            Assert.That(game.orbs.pool, Has.None.Matches<ExperienceOrb>(o => o && o.InUse), "swallowed enemy drops no XP orb");

            Assert.That(game.progression.Xp, Is.EqualTo(expectedXp), "swallow pays the enemy's experience directly");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Field_RepeatedAreaTicks_DamagesTargetAndExpiresWithoutGlobalSlowdown()
        {
            var sandbox = await StartSandbox();
            var game = sandbox.game;
            var target = sandbox.targets[0];
            var weapon = new RunWeapon { form = WeaponForm.Flamethrower, variant = 1, revealed = true };
            var actor = AttackPool.Instance.Acquire();
            Assert.That(actor, Is.Not.Null, "saved field actor is available");
            actor.Configure(AttackMode.Field, game.player, weapon, target.Position, Vector3.forward,
                .1f, 0f, 0f, 0f, 1f, .8f);
            float minimumTimeScale = await MinimumTimeScaleUntilReleased(actor, 4f);

            Assert.That(target.Hp, Is.LessThanOrEqualTo(TargetHp - 2f), "repeated field applies at least two damage ticks");
            Assert.That(minimumTimeScale, Is.EqualTo(1f), "field enemy hits never change global time scale");
            Assert.That(actor.gameObject.activeSelf, Is.False, "field lifetime completes and returns actor to pool");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Explode_KillWakesUnusedEnemySlot_HitsEachTargetOnceWithoutListError()
        {
            var sandbox = await StartSandbox();
            var game = sandbox.game;
            var target = sandbox.targets[0];
            sandbox.targets[1].DiscardWithoutReward();
            sandbox.targets[2].DiscardWithoutReward();
            var skitters = game.waves.preparedEnemies.Where(e => e && e.definition.kind == EnemyKind.Skitter).ToArray();
            foreach (var used in skitters.Where(e => Combatant.All.Contains(e)))
            {
                used.enabled = false;
                used.gameObject.SetActive(true);
            }
            var fresh = skitters.Where(e => !Combatant.All.Contains(e)).ToArray();
            Assume.That(fresh, Is.Not.Empty, "the saved pool keeps Skitter slots that have never been awake");
            var victim = game.waves.preparedEnemies.First(e => e && !e.gameObject.activeSelf && e.definition.kind == EnemyKind.Splitter);
            victim.Configure(victim.definition, 1f, false);
            victim.maxHp = 1f;
            victim.ResetForSpawn();
            ElementStatus.For(victim)?.SetShield(0f);
            victim.enabled = false;
            Teleport(victim, target.Position + Vector3.right);
            game.waves.TrackBoss(victim);
            Physics.SyncTransforms();

            Action<Enemy> wake = dead => { if (dead == victim) fresh[0].gameObject.SetActive(true); };
            game.waves.EnemyEliminated += wake;
            var actor = AttackPool.Instance.Acquire();
            Assert.That(actor, Is.Not.Null, "saved blast actor is available");
            actor.Configure(AttackMode.Blast, game.player, new RunWeapon { form = WeaponForm.RocketLauncher, variant = 1, revealed = true },
                target.Position, Vector3.forward, 10f, 0f, 0f, 0f, 2.5f);

            try { Assert.DoesNotThrow(actor.Explode, "a death that wakes a new enemy must not break the blast loop"); }
            finally { game.waves.EnemyEliminated -= wake; }
            actor.Release();

            Assert.That(victim.IsAlive, Is.False, "blast kills the victim");
            Assert.That(Combatant.All.Contains(fresh[0]), Is.True, "the kill woke a never-used Skitter slot");
            Assert.That(target.Hp, Is.EqualTo(TargetHp - 10f).Within(.001f), "the other target in range is hit exactly once");
        }

        static RunWeapon Equip(GameManager game, WeaponForm form, int variant)
        {
            game.runWeapons.ClearWaveEffects();
            var weapon = new RunWeapon { form = form, variant = variant, revealed = true };
            game.runWeapons.Hand[game.runWeapons.ActiveIndex] = weapon;
            return weapon;
        }

        internal static async Task<(GameManager game, Enemy[] targets)> StartSandbox()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            var game = GameManager.Instance;
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 4f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase == ArenaUiState.WaveIntro);
            game.player.ProtectFor(20f);
            await Until(() => game.IsCombatActive && game.waves.LivingCount >= 3, 12f, "first three authored enemies spawn");
            game.waves.enabled = false;
            var targets = game.waves.Alive.Where(e => e && e.IsAlive).Take(3).ToArray();
            foreach (var enemy in game.waves.preparedEnemies)
                if (enemy && !targets.Contains(enemy)) enemy.DiscardWithoutReward();
            foreach (var marker in game.waves.preparedMarkers) if (marker) marker.gameObject.SetActive(false);
            var origin = ClearLaneOrigin();
            game.player.enabled = false;
            game.player.ClearWaveState();
            game.player.Heal(game.player.maxHp);
            Teleport(game.player, origin);
            game.player.DebugSetAim(Vector3.forward);
            for (int i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                target.Configure(target.definition, 1f, false);
                target.maxHp = TargetHp;
                target.ResetForSpawn();
                target.enabled = false;
                target.moveSpeed = 0f;
                target.GetComponent<Rigidbody>().constraints = RigidbodyConstraints.FreezeAll;
                Teleport(target, origin + Vector3.forward * (3f + i * 3f));
            }
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            return (game, targets);
        }

        static Vector3 ClearLaneOrigin()
        {
            var map = WorldMap.Active;
            for (int x = -36; x <= 36; x += 4)
            for (int z = -22; z <= 14; z += 4)
            {
                var start = new Vector3(x, 0f, z);
                bool clear = true;
                for (int step = 0; step <= 34; step++)
                {

                    var point = start + Vector3.forward * (step * .5f);
                    if (!map.IsWalkable(point, 1.2f) || Quicksand.IsQuicksand(point)) { clear = false; break; }
                }
                if (clear && !Physics.SphereCast(Query.AtCastHeight(start), 1f, Vector3.forward,
                    out _, 17f, GameManager.WallMask, QueryTriggerInteraction.Ignore)) return start;
            }
            Assert.Fail("The saved map has no 17-unit clear lane for the projectile smoke scenario.");
            return Vector3.zero;
        }

        internal static void Teleport(Combatant body, Vector3 position)
        {
            body.GetComponent<Rigidbody>().position = position;
            body.transform.position = position;
        }

        static async Task<float> MinimumTimeScaleUntilReleased(AttackActor actor, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            float minimum = Time.timeScale;
            while (actor.gameObject.activeSelf)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Repeated field failed to expire.");
                minimum = Mathf.Min(minimum, Time.timeScale);
                await Awaitable.NextFrameAsync();
            }
            return minimum;
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}.");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
