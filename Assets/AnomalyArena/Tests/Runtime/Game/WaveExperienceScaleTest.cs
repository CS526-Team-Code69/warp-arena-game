using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    [Category("V3Smoke")]
    public class WaveExperienceScaleTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [Test, LoadScene(Scene), Timeout(40000)]
        public async Task WaveScale_MovesSpecialAndEliteXp_SwarmAndBossStayFixed()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var target = sandbox.targets[0];
            var waves = game.waves.waveSet.waves;
            var waveField = typeof(WaveManager).GetField("<WaveIndex>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var grunt = game.waves.roster.First(d => d && d.kind == EnemyKind.Grunt);
            var spitter = game.waves.roster.First(d => d && d.kind == EnemyKind.Spitter);
            var tyrant = game.waves.roster.First(d => d && d.kind == EnemyKind.Tyrant);
            const int Wave = 6;
            float saved = waves[Wave - 1].experienceScale;
            waveField.SetValue(game.waves, Wave - 1);
            try
            {
                float Xp(EnemyDefinition data, bool elite)
                {
                    target.Configure(data, game.waves.HpMultiplier, elite);
                    target.enabled = false;
                    return game.waves.ExperienceFor(target);
                }

                waves[Wave - 1].experienceScale = 1f;
                float special = Xp(spitter, false), elite = Xp(spitter, true);
                waves[Wave - 1].experienceScale = 2.5f;
                Assert.That(game.waves.WaveExperienceScale, Is.EqualTo(game.waves.CombatScale * 2.5f).Within(.0001f),
                    "the wave's scale is its difficulty times the wave table's extra factor");
                Assert.That(Xp(spitter, false), Is.EqualTo(special * 2.5f).Within(.0001f), "a special minion pays the wave's scale");
                Assert.That(Xp(spitter, true), Is.EqualTo(elite * 2.5f).Within(.0001f), "an elite pays the wave's scale");
                Assert.That(Xp(grunt, false), Is.EqualTo(game.waves.swarmExperienceReward), "the melee swarm keeps its fixed 0.5");
                Assert.That(Xp(tyrant, false), Is.Zero, "a boss still gives no XP");
            }
            finally
            {
                waves[Wave - 1].experienceScale = saved;
            }
        }

        [Test, LoadScene(Scene), Timeout(40000)]
        public async Task SavedWaves_PayTheSameXpPerShareOfHp_SoLaterWavesPayMore()
        {
            var sandbox = await V3CombatSmokeTest.StartSandbox();
            var game = sandbox.game;
            var target = sandbox.targets[0];
            var waves = game.waves.waveSet.waves;
            var waveField = typeof(WaveManager).GetField("<WaveIndex>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
            var spitter = game.waves.roster.First(d => d && d.kind == EnemyKind.Spitter);
            Assert.That(waves.Select(w => w.experienceScale), Has.All.GreaterThan(0f), "no wave turns special and elite XP off");

            (float xp, float hp) At(int wave)
            {
                waveField.SetValue(game.waves, wave - 1);
                target.Configure(spitter, game.waves.HpMultiplier, false);
                target.enabled = false;
                return (game.waves.ExperienceFor(target) / waves[wave - 1].experienceScale, target.maxHp);
            }

            var early = At(2);
            var late = At(waves.Length);
            Assert.That(late.xp, Is.GreaterThan(early.xp * 2f), "a Spitter on the last ordinary wave pays far more than on wave 2");

            Assert.That(late.xp / late.hp, Is.EqualTo(early.xp / early.hp).Within(10).Percent, "the same XP per point of Spitter HP");
        }

        [Test, LoadScene(Scene), Timeout(40000)]
        public async Task SavedLevelCurve_RisesEveryLevel_AndTheLastThreeLevelsTakeMostOfTheXp()
        {
            await Awaitable.NextFrameAsync();
            var game = GameManager.Instance;
            Assume.That(game && game.progression && game.progression.Rules, Is.True, "the arena scene has its progression rules");
            var costs = game.progression.Rules.levelCosts;

            for (int i = 1; i < costs.Length; i++) Assert.That(costs[i], Is.GreaterThan(costs[i - 1]), $"level {i + 2} costs more than level {i + 1}");
            Assert.That(costs.Skip(costs.Length - 3).Sum(), Is.GreaterThan(costs.Sum() / 2), "the last three levels take most of the XP");
        }
    }
}
