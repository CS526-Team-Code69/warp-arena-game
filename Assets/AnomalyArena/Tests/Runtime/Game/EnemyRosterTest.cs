using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{

    [TestFixture]
    [Category("V3Smoke")]
    public class EnemyRosterTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        UnityEngine.Random.State savedRandom;

        [SetUp]
        public void SetUp()
        {
            savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(4127);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Random.state = savedRandom;
            Time.timeScale = 1f;
        }

        static readonly EnemyKind[][] DocumentTable =
        {
            new EnemyKind[0],
            new[] { EnemyKind.Spitter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Stomper },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Spire, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Rammer },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Stomper },
            new[]
            {
                EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Rammer,
                EnemyKind.Chanter
            },
            new[]
            {
                EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater,
                EnemyKind.Shade, EnemyKind.Chanter
            },
            new[]
            {
                EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater,
                EnemyKind.Spire, EnemyKind.Shade, EnemyKind.Chanter
            },
        };

        [Test, LoadScene(Scene), Timeout(20000)]
        public async Task SavedWaveSet_FourteenOrdinaryWaves_WeightsListExactlyTheDocumentKinds()
        {
            var game = await ReadyScene();
            var waves = game.waves.waveSet;

            Assert.That(game.waves.WaveCount, Is.EqualTo(14), "14 ordinary waves; the final boss is wave 15");
            var failures = new List<string>();
            for (int wave = 1; wave <= 14; wave++)
            {
                var listed = Enum.GetValues(typeof(EnemyKind)).Cast<EnemyKind>()
                    .Where(k => !EnemyRoles.IsMelee(k) && waves.AllowsKind(wave, k)).OrderBy(k => k).ToArray();
                var expected = DocumentTable[wave - 1].OrderBy(k => k).ToArray();
                if (!listed.SequenceEqual(expected))
                    failures.Add(
                        $"wave {wave}: saved [{string.Join(",", listed)}] expected [{string.Join(",", expected)}]");
                foreach (var melee in new[] { EnemyKind.Grunt, EnemyKind.Brute, EnemyKind.Skitter, EnemyKind.Splitter })
                    if (!waves.AllowsKind(wave, melee)) failures.Add($"wave {wave}: no {melee}");
                if (waves.waves[wave - 1].weights.Length != Enum.GetValues(typeof(EnemyKind)).Length)
                    failures.Add($"wave {wave}: weights length {waves.waves[wave - 1].weights.Length}");
            }

            Assert.That(failures, Is.Empty, "§11.2 wave table");
        }

        [TestCase(1), TestCase(3), TestCase(5), TestCase(12), LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task WaveSpawns_ByWave_OnlyListedKindsSpawnAndElitesAreAffixedSpecials(int wave)
        {
            var game = await ReadyScene();
            var spawned = new List<Enemy>();
            game.waves.EnemySpawned += spawned.Add;
            await BeginCombat(game, wave);

            float end = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < end) await Awaitable.NextFrameAsync();
            game.waves.EnemySpawned -= spawned.Add;

            var ordinary = spawned.Where(e => !e.Elite && !e.IsBoss).ToArray();
            var elites = spawned.Where(e => e.Elite).ToArray();
            TestContext.Progress.WriteLine(
                $"V5_ROSTER wave={wave} ordinary={string.Join(",", ordinary.Select(e => e.definition.kind))} elites={string.Join(",", elites.Select(e => e.definition.kind + ":" + e.Affix))}");
            Assert.That(ordinary, Is.Not.Empty, "the wave spawned ordinary enemies");
            Assert.That(spawned, Has.All.Matches<Enemy>(e => game.waves.waveSet.AllowsKind(wave, e.definition.kind)),
                "only kinds the wave lists spawn");
            Assert.That(ordinary.Where(e => EnemyRoles.IsSpecial(e.definition.kind)),
                Has.All.Matches<Enemy>(e => e.Affix == ElementKind.None), "an ordinary special carries no affix");
            Assert.That(elites,
                Has.All.Matches<Enemy>(e => EnemyRoles.IsSpecial(e.definition.kind) && e.Affix != ElementKind.None),
                "every elite is a special with an element affix");
            if (wave < 2)
                Assert.That(ordinary, Has.All.Matches<Enemy>(e => EnemyRoles.IsMelee(e.definition.kind)),
                    "wave 1 is melee only");
            if (wave < 3)
                Assert.That(ordinary, Has.None.Matches<Enemy>(e => e.definition.kind == EnemyKind.Warden),
                    "support starts at wave 3");
            if (wave < game.waves.firstEliteWave)
                Assert.That(elites, Is.Empty, "no elites before the first elite wave");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task LateWaveWithForcedRefills_SpecialsSpawnAsOrdinary_EliteCapThreeWithDistinctAffixes()
        {
            var game = await ReadyScene();
            var spawned = new List<Enemy>();
            game.waves.EnemySpawned += spawned.Add;
            await BeginCombat(game, 12);
            int peak = 0;
            var failures = new List<string>();
            float end = Time.realtimeSinceStartup + 9f, nextCull = Time.realtimeSinceStartup + 2.5f;
            try
            {
                while (Time.realtimeSinceStartup < end)
                {
                    await Awaitable.NextFrameAsync();
                    var affixes = game.waves.Alive.Where(e => e && e.IsAlive && e.Elite).Select(e => e.Affix)
                        .Concat(game.waves.preparedMarkers.Where(m => m && m.gameObject.activeSelf && m.elite)
                            .Select(m => m.affix))
                        .ToArray();
                    peak = Mathf.Max(peak, affixes.Length);
                    var elements = affixes.Where(a => a != ElementKind.None).ToArray();
                    if (affixes.Length > 3 || elements.Distinct().Count() != elements.Length)
                        failures.Add($"t={end - Time.realtimeSinceStartup:0.00}: {string.Join(",", affixes)}");
                    if (Time.realtimeSinceStartup < nextCull) continue;

                    nextCull = Time.realtimeSinceStartup + 2.5f;
                    foreach (var enemy in game.waves.Alive.ToArray())
                    {
                        if (!enemy || !enemy.IsAlive) continue;
                        enemy.SuppressRewards = true;
                        enemy.ReceiveDamage(new DamageInfo
                            { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                    }
                }
            }
            finally
            {
                game.waves.EnemySpawned -= spawned.Add;
            }

            Assert.That(failures, Is.Empty,
                "alive plus pending elites stay at most three with pairwise different elements");
            Assert.That(peak, Is.EqualTo(game.waves.maxConcurrentElites),
                "the late wave wants more elites than the cap, so the cap is reached");
            Assert.That(spawned, Has.Some.Matches<Enemy>(e => !e.Elite && EnemyRoles.IsSpecial(e.definition.kind)),
                "specials also spawn as ordinary enemies");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MeleeVariants_KilledByHp_OnlyTheSplitterLeavesTwoMinisThatNeverSplit()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var waves = game.waves;
            var splitterData = waves.DefinitionOf(EnemyKind.Splitter);
            Assume.That(waves.preparedEnemies.Count(e => e && !e.gameObject.activeSelf && e.definition == splitterData && !waves.Alive.Contains(e)),
                Is.GreaterThanOrEqualTo(waves.splitterMiniCount),
                "the saved pool has free Splitter slots for the minis");
            var spawned = new List<Enemy>();
            waves.EnemySpawned += spawned.Add;
            try
            {
                var kinds = new[] { EnemyKind.Skitter, EnemyKind.Brute };
                for (int i = 0; i < kinds.Length; i++)
                {
                    var target = sandbox.targets[i];
                    target.Configure(waves.DefinitionOf(kinds[i]), 1f, false);
                    target.enabled = false;
                    Assert.That(target.IsLarge, Is.False, kinds[i] + " is an ordinary-sized melee variant");
                    target.ReceiveDamage(new DamageInfo
                        { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                    Assert.That(target.IsAlive, Is.False, kinds[i] + " died");
                }

                Assert.That(waves.SpawnPendingSplits(), Is.Zero, "Brute and Skitter do not split");

                var splitter = sandbox.targets[2];
                splitter.Configure(splitterData, 1f, false);
                splitter.enabled = false;
                Assert.That(splitter.Mini, Is.False, "a spawned Splitter is full size");
                splitter.ReceiveDamage(new DamageInfo
                    { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                Assert.That(waves.SpawnPendingSplits(), Is.EqualTo(waves.splitterMiniCount),
                    "a Splitter killed by HP leaves its minis");
            }
            finally
            {
                waves.EnemySpawned -= spawned.Add;
            }

            var balance = waves.waveSet.encounterBalance;
            float miniHp = balance.MaxHp(splitterData, waves.WaveIndex + 1, false, ElementKind.None, true);
            Assert.That(spawned, Has.Count.EqualTo(waves.splitterMiniCount), "only the Splitter spawned anything");
            Assert.That(spawned,
                Has.All.Matches<Enemy>(e =>
                    e.Mini && e.definition.kind == EnemyKind.Splitter && e.IsAlive && waves.Alive.Contains(e)),
                "minis are living Splitters tracked like any enemy");
            Assert.That(spawned,
                Has.All.Matches<Enemy>(e =>
                    e.maxHp == miniHp && Mathf.Abs(e.radius - splitterData.radius * waves.splitterMiniScale) < .001f),
                "minis are smaller with the mini HP share");
            Assert.That(miniHp, Is.LessThan(balance.MaxHp(splitterData, waves.WaveIndex + 1, false)).Or.EqualTo(1f),
                "a mini has less HP than a full Splitter");
            Assert.That(spawned,
                Has.All.Matches<Enemy>(e => Mathf.Abs(waves.ExperienceFor(e) - waves.miniExperienceReward) < .0001f),
                "fixed mini XP");

            spawned[0].ReceiveDamage(new DamageInfo
                { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
            Assert.That(waves.SpawnPendingSplits(), Is.Zero, "a mini never splits again");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Configure_EachRosterKind_OnlyBossesStomperAndRammerKnockBackThePlayer()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var target = sandbox.targets[0];
            foreach (var data in sandbox.game.waves.roster.Where(d => d))
            {
                target.Configure(data, 1f, EnemyRoles.IsSpecial(data.kind) && data.canElite);
                target.enabled = false;
                Assert.That(EnemyRoles.KnocksBackPlayer(data.kind),
                    Is.EqualTo(EnemyRoles.IsBoss(data.kind) || data.kind is EnemyKind.Stomper or EnemyKind.Rammer), data.kind + " push rule");

                float push = EnemyRoles.IsBoss(data.kind) ? data.knockback : KnockbackRules.Active.Distance(KnockbackRules.AttackTier(data.kind));
                if (EnemyRoles.KnocksBackPlayer(data.kind))
                    Assert.That(target.attackKnockback, Is.EqualTo(push), data.kind + " pushes by its tier");
                else Assert.That(target.attackKnockback, Is.Zero, data.kind + " never pushes the player");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Configure_HollowVoidAffixAndTyrantDraw_FollowDocumentRules()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var waves = sandbox.game.waves;
            var target = sandbox.targets[0];

            target.Configure(waves.DefinitionOf(EnemyKind.Hollow), 1f, false);
            target.enabled = false;
            var board = ElementStatus.For(target);
            Assert.That(board.ElementFactor(ElementKind.Frost), Is.Zero, "the Hollow ignores Frost");
            Assert.That(board.ElementFactor(ElementKind.Lightning), Is.Zero, "the Hollow ignores Lightning");
            Assert.That(board.ElementFactor(ElementKind.Fire), Is.EqualTo(1f),
                "other elements still work on the Hollow");

            var spitter = waves.DefinitionOf(EnemyKind.Spitter);
            target.Configure(spitter, 1f, false, ElementKind.Void);
            target.enabled = false;
            Assert.That(target.attackInterval,
                Is.EqualTo(spitter.interval * waves.voidAffixIntervalFactor).Within(.0001f),
                "a Void enemy attacks more often");
            target.Configure(spitter, 1f, false, ElementKind.Fire);
            Assert.That(target.attackInterval, Is.EqualTo(spitter.interval).Within(.0001f),
                "other affixes keep the interval");

            Assert.That(waves.TyrantImmunities, Is.EquivalentTo(new[] { ElementKind.Frost, ElementKind.Lightning }),
                "the Tyrant ignores Frost and Lightning");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task GruntPunch_PlayerInReach_DamagesWithoutKnockback()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var grunt = sandbox.targets[0];
            grunt.Configure(game.waves.roster.First(d => d.kind == EnemyKind.Grunt), 1f, false);
            V3CombatSmokeTest.Teleport(grunt,
                game.player.Position + Vector3.forward * (grunt.radius + game.player.Radius + .1f));
            grunt.enabled = true;
            float before = game.player.Hp;
            bool knocked = false;

            float deadline = Time.realtimeSinceStartup + 4f;
            while (game.player.Hp >= before)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Grunt never landed its punch.");
                await Awaitable.FixedUpdateAsync();
                knocked |= game.player.State == CharacterState.Knocked;
            }

            knocked |= game.player.State == CharacterState.Knocked;

            Assert.That(game.player.Hp, Is.LessThan(before), "the punch dealt damage");
            Assert.That(knocked, Is.False, "a basic melee hit never knocks the player back");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task ChanterAura_AllyInsideRadius_PunchDealsBonusDamage()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var player = game.player;
            player.protectionTime = 0f;
            sandbox.targets[2].DiscardWithoutReward();
            var grunt = sandbox.targets[0];
            var chanter = sandbox.targets[1];

            var saved = game.waves.DefinitionOf(EnemyKind.Chanter);
            var chanterData = saved ? saved : ScriptableObject.CreateInstance<EnemyDefinition>();
            if (!saved)
            {
                chanterData.kind = EnemyKind.Chanter;
                chanterData.damage = 0f;
                chanterData.hp = 20f;
                chanterData.interval = 3f;
            }

            try
            {
                grunt.Configure(game.waves.DefinitionOf(EnemyKind.Grunt), 1f, false);
                chanter.Configure(chanterData, 1f, false);
                chanter.enabled = false;
                Vector3 gruntAt = player.Position + Vector3.forward * (grunt.radius + player.Radius + .1f);
                V3CombatSmokeTest.Teleport(grunt, gruntAt);
                V3CombatSmokeTest.Teleport(chanter, gruntAt + Vector3.forward * (Enemy.ChantRadius + 3f));
                await Awaitable.FixedUpdateAsync();
                Assert.That(grunt.Chanted, Is.False, "outside the aura");

                grunt.enabled = true;
                float plain = await NextPunchDamage(player);
                grunt.enabled = false;
                player.Heal(player.maxHp);
                V3CombatSmokeTest.Teleport(chanter, gruntAt + Vector3.forward * 2f);
                await Awaitable.FixedUpdateAsync();
                Assert.That(grunt.Chanted, Is.True, "inside the aura");
                Assert.That(chanter.Chanted, Is.False, "a Chanter does not buff itself");

                grunt.enabled = true;
                float chanted = await NextPunchDamage(player);

                Assert.That(chanted, Is.EqualTo(plain * (1f + game.waves.chantDamageBonus)).Within(.01f),
                    "the aura adds the chant bonus to the punch");
            }
            finally
            {
                if (!saved) Object.Destroy(chanterData);
            }
        }

        static async Task<float> NextPunchDamage(PlayerController player)
        {
            ElementStatus.For(player)?.SetShield(0f);                           
            float before = player.Hp, deadline = Time.realtimeSinceStartup + 4f;
            while (player.Hp >= before)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The punch never landed.");
                await Awaitable.FixedUpdateAsync();
            }

            return before - player.Hp;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task SpitterSpike_HitsPlayer_DealsDamageAndAddsPoisonWithoutKnockback()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            sandbox.targets[0].DiscardWithoutReward();
            sandbox.targets[1].DiscardWithoutReward();
            var spitter = sandbox.targets[2];                                                                        
            var data = game.waves.roster.First(d => d.kind == EnemyKind.Spitter);
            spitter.Configure(data, 1f, false);
            spitter.enabled = true;
            var status = ElementStatus.For(game.player);
            Assume.That(status, Is.Not.Null, "player has a status board");
            float before = game.player.Hp;

            float deadline = Time.realtimeSinceStartup + 6f;
            while (game.player.Hp >= before)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Spitter spike never reached the player.");
                await Awaitable.FixedUpdateAsync();
            }

            Assert.That(game.player.State, Is.Not.EqualTo(CharacterState.Knocked),
                "the spike does not push the player");
            Assert.That(status.Poison, Is.GreaterThanOrEqualTo(data.spikePoisonStacks).And.GreaterThan(0f),
                "the spike adds Poison stacks");
        }

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "saved scene and localization initialize");
            var game = GameManager.Instance;
            Assume.That(game.player && game.waves && game.waves.waveSet, Is.True, "V2 scene references exist");
            return game;
        }

        static async Task BeginCombat(GameManager game, int wave)
        {
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 4f, "supply crate landing");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase == ArenaUiState.WaveIntro);
            while (game.waves.WaveIndex < wave - 1) game.waves.BeginNextWave();
            game.player.ProtectFor(120f);
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 7f, "wave introduction completes");
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline)
                    Assert.Fail($"Command {command} did not complete; phase={game.RunPhase}");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Timed out waiting for {operation}");
                await Awaitable.NextFrameAsync();
            }
        }
    }
}