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
    /// <summary>Three spawn groups (system doc 11.2): basics (melee kinds, Spitter, Warden), ordinary specials and elites,
    /// filled per wave from the doc's kind table. Specials join gradually from the first wave that lists one, and every special a wave lists gets
    /// a slot in the batch until it has appeared; a refill fills only the free places, so those extra slots never lift the field over the wave's
    /// population.</summary>
    [TestFixture]
    public class SpawnGroupsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const int FirstSpecialWave = 4; // the Chanter
        static readonly EnemyKind[] Melee = { EnemyKind.Grunt, EnemyKind.Brute, EnemyKind.Skitter, EnemyKind.Splitter };

        // Doc 11.2 table: the specials of each ordinary wave. Every wave also has the four melee kinds, the Spitter from wave 2 and the Warden from 3.
        static readonly EnemyKind[][] Specials =
        {
            new EnemyKind[0],
            new EnemyKind[0],
            new EnemyKind[0],
            new[] { EnemyKind.Chanter },
            new[] { EnemyKind.Stomper },
            new[] { EnemyKind.Spire, EnemyKind.Chanter },
            new[] { EnemyKind.Bloater, EnemyKind.Rammer },
            new[] { EnemyKind.Rammer, EnemyKind.Stomper },
            new[] { EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Chanter },
            new[] { EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater },
            new[] { EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Stomper },
            new[] { EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Rammer, EnemyKind.Chanter },
            new[] { EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater, EnemyKind.Shade, EnemyKind.Chanter },
            new[] { EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Shade, EnemyKind.Chanter },
        };

        UnityEngine.Random.State savedRandom;

        [SetUp]
        public void SetUp()
        {
            savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(6107);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Random.state = savedRandom;

        [Test]
        public void IsBasic_MeleeKindsSpitterAndWarden_AreTheBasicsAndNothingElse()
        {
            var basics = Enum.GetValues(typeof(EnemyKind)).Cast<EnemyKind>().Where(WaveManager.IsBasic).OrderBy(k => k);

            Assert.That(basics, Is.EqualTo(Melee.Concat(new[] { EnemyKind.Spitter, EnemyKind.Warden }).OrderBy(k => k)),
                "doc 10.2: the Spitter and the Warden are basics beside the melee kinds");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task GroupWeights_EveryWave_BasicGroupHoldsTheBasicsAndTheOtherTwoOnlyTheListedSpecials()
        {
            var waves = await ReadyWaves();
            Assume.That(waves.WaveCount, Is.EqualTo(Specials.Length), "14 ordinary waves");
            var failures = new List<string>();

            for (int wave = 1; wave <= waves.WaveCount; wave++)
            {
                var basics = new List<EnemyKind>(Melee);
                if (wave >= 2) basics.Add(EnemyKind.Spitter);
                if (wave >= 3) basics.Add(EnemyKind.Warden);
                Compare(failures, wave, "basic", Kinds(waves, wave, 0), basics);
                Compare(failures, wave, "special", Kinds(waves, wave, 1), Specials[wave - 1]);
                Compare(failures, wave, "elite", Kinds(waves, wave, 2), wave >= waves.firstEliteWave ? Specials[wave - 1] : new EnemyKind[0]);
            }

            Assert.That(failures, Is.Empty, "doc 11.2: basics, specials and elites each spawn exactly the kinds the wave lists");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task ActiveRoleShares_ByWave_SpecialsRampInFromTheChanterWaveAndElitesStartAtTheFirstEliteWave()
        {
            var waves = await ReadyWaves();
            var failures = new List<string>();
            float previous = 0f;

            for (int index = 0; index < waves.WaveCount; index++)
            {
                BeginWave(waves, index);
                int wave = index + 1;
                var shares = waves.ActiveRoleShares;
                if (Mathf.Abs(shares.x + shares.y + shares.z - 1f) > 1e-4f) failures.Add($"wave {wave}: shares add up to {shares.x + shares.y + shares.z}");
                if (wave < FirstSpecialWave && shares.y != 0f) failures.Add($"wave {wave}: special share {shares.y} before any special is listed");
                if (wave >= FirstSpecialWave && (shares.y <= 0f || shares.y < previous)) failures.Add($"wave {wave}: special share {shares.y} after {previous}");
                if (wave >= FirstSpecialWave + waves.specialRampWaves - 1 && Mathf.Abs(shares.y - waves.specialShare) > 1e-4f)
                    failures.Add($"wave {wave}: special share {shares.y}, not the full {waves.specialShare}");
                if (wave < waves.firstEliteWave && shares.z != 0f) failures.Add($"wave {wave}: elite share {shares.z} before the first elite wave");
                previous = shares.y;
            }

            Assert.That(failures, Is.Empty, "doc 11.2: early waves are all basics, specials join gradually, elites come from mid-game");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task BeginBatch_WaveListingTwoSpecials_EachUnseenSpecialGetsASlotUntilItHasAppeared()
        {
            var waves = await ReadyWaves();
            const int wave = 6; // the Spire and the Chanter
            BeginWave(waves, wave - 1);
            int population = waves.PopulationTargetForWave(wave - 1);
            var shares = waves.ActiveRoleShares;
            int basics = Mathf.RoundToInt(population * shares.x);
            int plain = population - basics - Mathf.Clamp(Mathf.RoundToInt(population * shares.z), 0, population - basics);
            if (plain == 0 && shares.y > 0f && basics > 1) plain = 1;
            Assume.That(plain, Is.LessThan(2), "wave 6's special share alone gives fewer than two special slots");

            Invoke(waves, "BeginBatch");
            Assert.That(Slots(waves, "specialRemaining"), Is.EqualTo(2), "one special slot for each special the wave lists that has not appeared");
            Assert.That(Slots(waves, "basicRemaining") + Slots(waves, "specialRemaining") + Slots(waves, "eliteRemaining"), Is.EqualTo(population),
                "the reserved slots come out of the basics, not on top of the batch");

            Seen(waves).UnionWith(new[] { EnemyKind.Spire, EnemyKind.Chanter });
            Invoke(waves, "BeginBatch");
            Assert.That(Slots(waves, "specialRemaining"), Is.EqualTo(plain), "once both have appeared the batch keeps the plain special share");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task RefillPopulation_SpecialsAboveTheirShareAfterFirstAppearances_NeverQueuesPastThePopulation()
        {
            var waves = await ReadyWaves();
            const int wave = 6; // the Spire and the Chanter: two reserved special slots against a share of one
            BeginWave(waves, wave - 1);
            int population = waves.PopulationTargetForWave(wave - 1);
            Invoke(waves, "BeginBatch");
            Assume.That(Slots(waves, "specialRemaining"), Is.EqualTo(2), "each unseen special reserved a slot");
            Land(waves, EnemyKind.Grunt, false, Slots(waves, "basicRemaining"));
            Land(waves, EnemyKind.Spire, false, 1);
            Land(waves, EnemyKind.Chanter, false, 1);
            Land(waves, EnemyKind.Chanter, true, Slots(waves, "eliteRemaining"));
            Seen(waves).UnionWith(new[] { EnemyKind.Spire, EnemyKind.Chanter });
            Issued(waves);
            Assume.That(OnField(waves), Is.EqualTo(population), "the batch filled the population");

            int threshold = Mathf.Max(1, Mathf.CeilToInt(population * waves.refillMissingShare)), queued = 0;
            for (int down = 1; down <= threshold && queued == 0; down++)
            {
                Lift(waves, EnemyKind.Grunt, false);
                Invoke(waves, "RefillPopulation");
                queued = Slots(waves, "batchSpawnsRemaining");
                Assert.That(OnField(waves) + queued, Is.LessThanOrEqualTo(population), $"{down} basics down: the refill stays inside the population");
            }

            Assert.That(queued, Is.EqualTo(threshold), "once the refill share of the places is free, the refill fills exactly those places");
            Assert.That(Slots(waves, "basicRemaining"), Is.EqualTo(queued), "the specials keep their extra body, so the refill is all basics");
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task RefillPopulation_UnseenSpecialWhileAnotherTookItsSlot_ReservesAgainOnlyOnceTheSpecialsAreBackAtTheirShare()
        {
            var waves = await ReadyWaves();
            const int wave = 6;
            BeginWave(waves, wave - 1);
            int population = waves.PopulationTargetForWave(wave - 1);
            int threshold = Mathf.Max(1, Mathf.CeilToInt(population * waves.refillMissingShare));
            Invoke(waves, "BeginBatch");
            Assume.That(Slots(waves, "specialRemaining"), Is.EqualTo(2), "each unseen special reserved a slot");
            // The Spire found no open ground, so both special slots became Chanters: the Spire is still unseen.
            Land(waves, EnemyKind.Grunt, false, Slots(waves, "basicRemaining"));
            Land(waves, EnemyKind.Chanter, false, 2);
            Land(waves, EnemyKind.Chanter, true, Slots(waves, "eliteRemaining"));
            Seen(waves).Add(EnemyKind.Chanter);
            Issued(waves);

            for (int down = 0; down < threshold; down++) Lift(waves, EnemyKind.Grunt, false);
            Invoke(waves, "RefillPopulation");
            Assert.That(Slots(waves, "batchSpawnsRemaining"), Is.EqualTo(threshold), "the free places are refilled");
            Assert.That(Slots(waves, "specialRemaining"), Is.Zero, "the extra Chanter still holds the Spire's reserved place: no special on top");

            Land(waves, EnemyKind.Grunt, false, Slots(waves, "batchSpawnsRemaining"));
            Issued(waves);
            Lift(waves, EnemyKind.Chanter, false); // the specials are back at their share
            for (int down = 1; down < threshold; down++) Lift(waves, EnemyKind.Grunt, false);
            Invoke(waves, "RefillPopulation");
            Assert.That(Slots(waves, "batchSpawnsRemaining"), Is.EqualTo(threshold), "the free places are refilled");
            Assert.That(Slots(waves, "specialRemaining"), Is.EqualTo(1), "the unseen Spire gets its slot again");
            Assert.That(OnField(waves) + Slots(waves, "batchSpawnsRemaining"), Is.EqualTo(population), "and the field stays at its population");
        }

        // ───── Helpers ─────

        /// <summary>Lands batch slots as warnings on the field. RefillPopulation counts a warning as a body, and nothing ticks them here
        /// (the run has not started), so they stay warnings.</summary>
        static void Land(WaveManager waves, EnemyKind kind, bool elite, int count)
        {
            var data = waves.DefinitionOf(kind);
            for (int i = 0; i < count; i++)
            {
                var marker = waves.BorrowMarker();
                Assert.That(marker, Is.Not.Null, "a free saved warning marker");
                marker.Begin(Vector3.zero, data, elite, elite ? ElementKind.Fire : ElementKind.None);
            }
        }

        /// <summary>One body of a kind leaves the field, as if killed.</summary>
        static void Lift(WaveManager waves, EnemyKind kind, bool elite) =>
            waves.preparedMarkers.First(m => m && m.gameObject.activeSelf && m.definition.kind == kind && m.elite == elite).gameObject.SetActive(false);

        static int OnField(WaveManager waves) => waves.LivingCount + waves.preparedMarkers.Count(m => m && m.gameObject.activeSelf);

        /// <summary>The queued batch has been placed: nothing is left to issue, as when TickV2 calls RefillPopulation.</summary>
        static void Issued(WaveManager waves)
        {
            foreach (var field in new[] { "batchSpawnsRemaining", "basicRemaining", "specialRemaining", "eliteRemaining" })
                typeof(WaveManager).GetField(field, Private).SetValue(waves, 0);
        }

        static HashSet<EnemyKind> Seen(WaveManager waves) => (HashSet<EnemyKind>)typeof(WaveManager).GetField("seenThisWave", Private).GetValue(waves);

        static EnemyKind[] Kinds(WaveManager waves, int wave, int group)
        {
            var weights = waves.GroupWeights(wave, group);
            return Enumerable.Range(0, weights.Length).Where(i => weights[i] > 0f && waves.roster[i]).Select(i => waves.roster[i].kind).OrderBy(k => k).ToArray();
        }

        static void Compare(List<string> failures, int wave, string group, EnemyKind[] actual, IEnumerable<EnemyKind> expected)
        {
            var sorted = expected.OrderBy(k => k).ToArray();
            if (!actual.SequenceEqual(sorted)) failures.Add($"wave {wave} {group}: [{string.Join(",", actual)}], expected [{string.Join(",", sorted)}]");
        }

        /// <summary>Starts a 0-based ordinary wave the way the run does (kind memory, affixes, banner) without running its clock.</summary>
        static void BeginWave(WaveManager waves, int index) => Invoke(waves, "BeginV2Wave", index);

        static void Invoke(WaveManager waves, string method, params object[] arguments) =>
            typeof(WaveManager).GetMethod(method, Private).Invoke(waves, arguments);

        static int Slots(WaveManager waves, string field) => (int)typeof(WaveManager).GetField(field, Private).GetValue(waves);

        static async Task<WaveManager> ReadyWaves()
        {
            await Awaitable.NextFrameAsync();
            float deadline = Time.realtimeSinceStartup + 12f;
            while (!GameManager.Instance || !ArenaText.Ready)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for the saved scene and localization.");
                await Awaitable.NextFrameAsync();
            }
            var waves = GameManager.Instance.waves;
            Assume.That(waves && waves.waveSet && waves.roster != null, Is.True, "V2 scene references exist");
            return waves;
        }
    }
}
