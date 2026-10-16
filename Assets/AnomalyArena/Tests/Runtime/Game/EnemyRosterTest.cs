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

        [TestCase(1), TestCase(3), TestCase(5), TestCase(12), LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task WaveSpawns_ByWave_EliteOnlyKindsNeverSpawnAsOrdinaryEnemies(int wave)
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
            TestContext.Progress.WriteLine($"V4_ROSTER wave={wave} ordinary={string.Join(",", ordinary.Select(e => e.definition.kind))} elites={string.Join(",", elites.Select(e => e.definition.kind))}");
            Assert.That(ordinary, Is.Not.Empty, "the wave spawned ordinary enemies");
            Assert.That(ordinary, Has.None.Matches<Enemy>(e => EnemyRoles.IsEliteOnly(e.definition.kind)), "Rammer/Bloater/Spire/Stomper/Shade are elite-only");
            Assert.That(elites, Has.All.Matches<Enemy>(e => EnemyRoles.IsEliteOnly(e.definition.kind)), "every elite is a special enemy");
            if (wave < 3) Assert.That(ordinary, Has.All.Matches<Enemy>(e => EnemyRoles.IsMelee(e.definition.kind)), "waves 1-2 are melee only");
            if (wave < 5) Assert.That(ordinary, Has.None.Matches<Enemy>(e => e.definition.kind == EnemyKind.Warden), "support starts at wave 5");
            if (wave < game.waves.firstEliteWave) Assert.That(elites, Is.Empty, "no elites before the first elite wave");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Elites_LateWaveWithForcedRefills_AtMostThreeWithDistinctAffixes()
        {
            var game = await ReadyScene();
            await BeginCombat(game, 12);
            int peak = 0;
            var failures = new List<string>();
            float end = Time.realtimeSinceStartup + 9f, nextCull = Time.realtimeSinceStartup + 2.5f;
            while (Time.realtimeSinceStartup < end)
            {
                await Awaitable.NextFrameAsync();
                var affixes = game.waves.Alive.Where(e => e && e.IsAlive && e.Elite).Select(e => e.Affix)
                    .Concat(game.waves.preparedMarkers.Where(m => m && m.gameObject.activeSelf && m.elite).Select(m => m.affix))
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
                    enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                }
            }

            Assert.That(failures, Is.Empty, "alive plus pending elites stay at most three with pairwise different elements");
            Assert.That(peak, Is.EqualTo(game.waves.maxConcurrentElites), "the late wave wants more elites than the cap, so the cap is reached");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task MeleeVariants_Killed_DoNotSplitOrSpawnExtras()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var kinds = new[] { EnemyKind.Skitter, EnemyKind.Brute, EnemyKind.Splitter };
            var spawned = new List<Enemy>();
            game.waves.EnemySpawned += spawned.Add;
            try
            {
                for (int i = 0; i < kinds.Length; i++)
                {
                    var target = sandbox.targets[i];
                    target.Configure(game.waves.roster.First(d => d.kind == kinds[i]), 1f, false);
                    target.enabled = false;
                    Assert.That(target.IsLarge, Is.False, kinds[i] + " is an ordinary-sized melee variant");
                    target.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                    Assert.That(target.IsAlive, Is.False, kinds[i] + " died");
                }
                await Awaitable.FixedUpdateAsync();
                await Awaitable.NextFrameAsync();
            }
            finally { game.waves.EnemySpawned -= spawned.Add; }

            Assert.That(spawned, Is.Empty, "no melee variant splits or summons on death");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task Configure_EachRosterKind_OnlySpecialEnemiesAndBossesKnockBackThePlayer()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var target = sandbox.targets[0];
            foreach (var data in sandbox.game.waves.roster.Where(d => d))
            {
                target.Configure(data, 1f, EnemyRoles.IsEliteOnly(data.kind));
                target.enabled = false;
                if (EnemyRoles.KnocksBackPlayer(data.kind)) Assert.That(target.attackKnockback, Is.EqualTo(data.knockback), data.kind + " keeps its knockback");
                else Assert.That(target.attackKnockback, Is.Zero, data.kind + " is a basic enemy without knockback");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(40000)]
        public async Task GruntPunch_PlayerInReach_DamagesWithoutKnockback()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var grunt = sandbox.targets[0];
            grunt.Configure(game.waves.roster.First(d => d.kind == EnemyKind.Grunt), 1f, false);
            V3CombatSmokeTest.Teleport(grunt, game.player.Position + Vector3.forward * (grunt.radius + game.player.Radius + .1f));
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

            Assert.That(game.player.State, Is.Not.EqualTo(CharacterState.Knocked), "the spike does not push the player");
            Assert.That(status.Poison, Is.GreaterThanOrEqualTo(data.spikePoisonStacks).And.GreaterThan(0f), "the spike adds Poison stacks");
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
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WaveIntro, 1);
            while (game.waves.WaveIndex < wave - 1) game.waves.BeginNextWave();
            game.player.ProtectFor(120f);
            await Until(() => game.RunPhase == ArenaUiState.WaveActive, 7f, "wave introduction completes");
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 4f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} did not complete; phase={game.RunPhase}");
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
