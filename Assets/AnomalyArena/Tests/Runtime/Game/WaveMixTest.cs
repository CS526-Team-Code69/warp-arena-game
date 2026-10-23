using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class WaveMixTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";
        const float Tolerance = 1e-4f;
        static readonly string[] MapFamilies = { "DustFlats", "WreckField", "RiftZone" };
        UnityEngine.Random.State savedRandom;

        [SetUp]
        public void SetUp()
        {
            savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(5311);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Random.state = savedRandom;
            Time.timeScale = 1f;
        }

        [Test]
        public void ApplyShareFloor_KindsUnderTheFloor_EndExactlyOnItAndTheRestKeepTheirWeight()
        {
            var weights = new[] { 70f, 0f, 30f, 5f, 40f };
            var listed = new[] { true, true, true, true, false };

            WaveManager.ApplyShareFloor(weights, listed, .1f);

            float total = weights[0] + weights[1] + weights[2] + weights[3];
            Assert.That(weights[1] / total, Is.EqualTo(.1f).Within(Tolerance), "a kind the map tilted to zero is raised to the floor");
            Assert.That(weights[3] / total, Is.EqualTo(.1f).Within(Tolerance), "a small kind is raised to the floor");
            Assert.That(new[] { weights[0], weights[2] }, Is.EqualTo(new[] { 70f, 30f }), "kinds above the floor keep their weight");
            Assert.That(weights[4], Is.EqualTo(40f), "a kind outside the group is untouched");
        }

        [Test]
        public void ApplyShareFloor_RaisingGrowsTheTotal_AKindOnceAboveTheFloorIsRaisedToo()
        {

            var weights = new[] { 100f, 12f, 0f, 0f, 0f };
            var listed = Enumerable.Repeat(true, weights.Length).ToArray();

            WaveManager.ApplyShareFloor(weights, listed, .1f);

            float total = weights.Sum();
            Assert.That(weights.Skip(1).Select(w => w / total), Has.All.EqualTo(.1f).Within(Tolerance), "every small kind ends on the floor");
            Assert.That(weights[0], Is.EqualTo(100f), "the large kind keeps its weight");
        }

        [Test]
        public void ApplyShareFloor_TooManyKindsOrAllZero_ListedKindsSplitEvenly()
        {
            var crowded = Enumerable.Range(1, 12).Select(i => (float)i).ToArray();
            WaveManager.ApplyShareFloor(crowded, Enumerable.Repeat(true, crowded.Length).ToArray(), .1f);
            Assert.That(crowded, Has.All.EqualTo(crowded[0]).Within(Tolerance), "12 kinds cannot all hold 10%, so they share evenly");
            Assert.That(crowded[0], Is.GreaterThan(0f));

            var empty = new[] { 0f, 0f, 3f };
            WaveManager.ApplyShareFloor(empty, new[] { true, true, false }, .1f);
            Assert.That(empty, Is.EqualTo(new[] { 1f, 1f, 3f }), "listed kinds all tilted to zero still spawn, evenly");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task GroupWeights_EveryWaveOnEachMapFamily_EachListedKindKeepsTheFloorShareOfItsGroup()
        {
            var game = await ReadyScene();
            var waves = game.waves;
            var world = WorldMap.Active;
            Assume.That(world && world.biomes != null, Is.True, "the scene keeps its map list");
            Assert.That(waves.minKindShare, Is.EqualTo(.1f), "owner, 2026-10-23: at least 10% of the kind's own group");
            var failures = new List<string>();
            var original = world.biome;
            try
            {
                foreach (var family in MapFamilies)
                {
                    var biome = world.biomes.FirstOrDefault(b => b && b.name.StartsWith(family, StringComparison.Ordinal));
                    Assume.That(biome, Is.Not.Null, family + " is one of the scene's maps");
                    world.biome = biome;
                    for (int wave = 1; wave <= waves.WaveCount; wave++)
                    for (int group = 0; group < 3; group++)
                    {
                        var weights = waves.GroupWeights(wave, group);
                        float total = weights.Sum();
                        for (int i = 0; i < weights.Length; i++)
                        {
                            var data = waves.roster[i];
                            if (!data) continue;
                            string at = $"{biome.name} wave {wave} group {group} {data.kind}";
                            if (!InGroup(waves, data, wave, group) || !waves.waveSet.AllowsKind(wave, data.kind))
                            {
                                if (weights[i] != 0f) failures.Add($"{at}: not listed but weight {weights[i]}");
                                continue;
                            }
                            float share = total > 0f ? weights[i] / total : 0f;
                            if (weights[i] <= 0f || share < waves.minKindShare - Tolerance) failures.Add($"{at}: share {share:0.000}");

                            int tilted = Tilted(waves.waveSet, biome, wave, data.kind);
                            if (weights[i] != tilted && (weights[i] < tilted || Mathf.Abs(share - waves.minKindShare) > Tolerance))
                                failures.Add($"{at}: weight {weights[i]:0.00} from tilted {tilted} (share {share:0.000})");
                        }
                    }
                }
            }
            finally
            {
                world.biome = original;
            }

            Assert.That(failures, Is.Empty, "every listed kind holds at least 10% of its group on every map");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task GroupWeights_ShadeAtItsCap_IsLeftOutAndTheRestStillHoldTheFloor()
        {
            var game = await ReadyScene();
            var waves = game.waves;
            const int wave = 13;                                                   
            BeginWave(waves, wave - 1);
            var shade = waves.DefinitionOf(EnemyKind.Shade);
            var marker = waves.BorrowMarker();
            Assume.That(shade && marker, Is.True, "the scene keeps a Shade and a free warning");
            Assume.That(waves.waveSet.AllowsKind(wave, EnemyKind.Shade), Is.True, "wave 13 lists the Shade");
            marker.Begin(game.player.Position + Vector3.forward * 14f, shade, false, ElementKind.None);                                 

            var weights = new float[waves.roster.Length];
            var listed = new bool[weights.Length];
            typeof(WaveManager).GetMethod("FillGroupWeights", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(waves, new object[] { wave, 1, true, weights, listed });

            float total = weights.Sum();
            Assert.That(weights[Array.IndexOf(waves.roster, shade)], Is.Zero, "a Shade at its cap gets no weight");
            var rest = Enumerable.Range(0, weights.Length).Where(i => listed[i]).ToArray();
            Assert.That(rest, Is.Not.Empty, "the other functional kinds of wave 13 can spawn");
            Assert.That(rest.Select(i => weights[i] / total), Has.All.GreaterThanOrEqualTo(waves.minKindShare - Tolerance),
                "the floor is solved over the kinds that can spawn now");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task WaveAffixes_EveryWave_TwoDifferentElementsFromTheOpenTierFromWaveFive()
        {
            var game = await ReadyScene();
            var waves = game.waves;
            var failures = new List<string>();
            for (int index = 0; index < waves.WaveCount; index++)
            {
                int open = index < 4 ? 0 : index < 7 ? 3 : index < 10 ? 5 : 7;
                for (int draw = 0; draw < (open > 0 ? 20 : 1); draw++)
                {
                    BeginWave(waves, index);
                    var (a, b) = (waves.WaveAffixA, waves.WaveAffixB);
                    if (open == 0)
                    {
                        if (a != ElementKind.None || b != ElementKind.None) failures.Add($"wave {index + 1}: {a} / {b} before wave 5");
                        continue;
                    }
                    if (a == b) failures.Add($"wave {index + 1}: both {a}");
                    if ((int)a < 1 || (int)a > open || (int)b < 1 || (int)b > open)
                        failures.Add($"wave {index + 1}: {a} / {b} outside the first {open} elements");
                }
            }

            Assert.That(failures, Is.Empty, "doc 7.6: each wave brings exactly two affix kinds");
        }

        [TestCase(1, "RiftZone"), TestCase(3, "RiftZone"), TestCase(13, "WreckField"), LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task WaveSpawns_MapTiltsAKindDown_EveryKindTheWaveListsStillAppears(int wave, string family)
        {
            var game = await ReadyScene();
            var world = WorldMap.Active;
            var biome = world.biomes.FirstOrDefault(b => b && b.name.StartsWith(family, StringComparison.Ordinal));
            Assume.That(biome, Is.Not.Null, family + " is one of the scene's maps");
            world.biome = biome;                                                                    
            var listed = game.waves.roster.Where(d => d && !EnemyRoles.IsBoss(d.kind) && game.waves.waveSet.AllowsKind(wave, d.kind))
                .Select(d => d.kind).ToHashSet();
            var spawned = new List<Enemy>();
            void Record(Enemy enemy)
            {
                if (enemy && !enemy.Mini && !enemy.IsBoss) spawned.Add(enemy);
            }

            game.waves.EnemySpawned += Record;
            try
            {
                await BeginCombat(game, wave);
                float end = Time.realtimeSinceStartup + 15f, nextCull = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < end && !listed.IsSubsetOf(spawned.Select(e => e.definition.kind)))
                {
                    await Awaitable.NextFrameAsync();
                    if (world.HoleIntroPlaying) { world.SkipHoleIntro(); continue; }
                    if (Time.realtimeSinceStartup < nextCull) continue;

                    nextCull = Time.realtimeSinceStartup + 2f;
                    foreach (var enemy in game.waves.Alive.ToArray())
                    {
                        if (!enemy || !enemy.IsAlive) continue;
                        enemy.SuppressRewards = true;
                        enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                    }
                }
            }
            finally
            {
                game.waves.EnemySpawned -= Record;
            }

            var kinds = spawned.Select(e => e.definition.kind).ToHashSet();
            TestContext.Progress.WriteLine($"WAVE_MIX wave={wave} map={biome.name} kinds={string.Join(",", kinds)}");
            Assert.That(listed.Except(kinds), Is.Empty, $"every kind wave {wave} lists appears on {biome.name}");
            Assert.That(kinds.Except(listed), Is.Empty, "nothing outside the table spawns");
            Assert.That(spawned.Where(e => e.definition.kind == EnemyKind.Warden), Has.All.Matches<Enemy>(e => e.Affix == ElementKind.None),
                "a Warden never carries an affix");
            var pair = new[] { game.waves.WaveAffixA, game.waves.WaveAffixB };
            Assert.That(spawned.Where(e => !e.Elite && e.Affix != ElementKind.None), Has.All.Matches<Enemy>(e => pair.Contains(e.Affix)),
                "ordinary affixes come from the wave's pair");
        }

        static bool InGroup(WaveManager waves, EnemyDefinition data, int wave, int group)
        {
            if (EnemyRoles.IsBoss(data.kind)) return false;
            if (group == 0) return EnemyRoles.IsMelee(data.kind);
            if (group == 2) return EnemyRoles.IsSpecial(data.kind) && data.canElite && wave >= waves.firstEliteWave;
            return !EnemyRoles.IsMelee(data.kind) && (data.kind != EnemyKind.Spitter || wave >= waves.rangedFirstWave)
                && (data.kind != EnemyKind.Warden || wave >= waves.supportFirstWave);
        }

        static int Tilted(WaveSet set, Biome biome, int wave, EnemyKind kind)
        {
            var tilt = biome.mixTilt;
            int index = (int)kind;
            return Mathf.Max(0, set.Weight(wave, kind) + (tilt != null && index < tilt.Length ? Mathf.Clamp(tilt[index], -10, 10) : 0));
        }

        static void BeginWave(WaveManager waves, int index) =>
            typeof(WaveManager).GetMethod("BeginV2Wave", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(waves, new object[] { index });

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
