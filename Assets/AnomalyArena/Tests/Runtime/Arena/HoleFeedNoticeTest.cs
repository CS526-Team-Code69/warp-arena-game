using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The open mode's black hole: its count reads like the story's (已吞噬 3/40, FED 3/40), and the first monster it swallows in a
    /// run brings one banner on what feeding it opens; the monsters it swallows after that bring none.</summary>
    [TestFixture]
    public class HoleFeedNoticeTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;
            V2WorldLabels.ForgetFirstSights();
        }

        [Test, LoadScene(Scene), Timeout(20000)]
        public async Task OpenHoleCount_ReadsLikeTheStorysCount_AndTheEnglishAlphaKeepsItsBareCount()
        {
            await Until(() => ArenaText.Ready, 12f, "localization initializes");

            string expected = BuildEdition.IsAlpha ? "3 / 40" : ArenaText.Pair("已吞噬 3/40", "FED 3/40");

            Assert.That(V2WorldLabels.OpenHoleCount(3, 40), Is.EqualTo(expected));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task FirstSwallowedMonster_ShowsTheBannerOnce_AndTheNextOneDoesNot()
        {
            Assume.That(V5HudKit.On, Is.True, "the whole game's pages (the English Alpha shows no such banner)");
            var (game, targets) = await V3CombatSmokeTest.StartSandbox();
            var world = WorldMap.Active;
            if (world.HoleIntroPlaying) world.SkipHoleIntro();
            // A hole with a feed already counted never plays its first-sight cutscene; a feed counted here, not through the world's swallowing,
            // brings no banner.
            foreach (var each in world.holes)
                if (each && each.Phase == HolePhase.Active && each.Feeds == 0) each.Feed(1, game.waves.WaveIndex + 1);
            var hole = world.holes.FirstOrDefault(h => h && h.Phase == HolePhase.Active && h.Feeds < h.feedsToOpen - 2);
            Assume.That(hole, Is.Not.Null, "the arena has an active hole two feeds short of opening");
            foreach (var target in targets) target.DiscardWithoutReward();
            await Awaitable.NextFrameAsync();
            V2WorldLabels.ForgetFirstSights();
            Assume.That(V2WorldLabels.NoticesWaiting, Is.Zero, "no notice waits");
            int fed = hole.Feeds;

            var first = Swallow(game, hole);
            await Until(() => !first.IsAlive && hole.Feeds == fed + 1, 10f, "the hole swallows the first monster and counts it");
            string banner = V2WorldLabels.HoleFedNotice();
            Assert.That(banner, Does.Contain("\n"), "a banner: a title and a line");
            await Until(() => V2WorldLabels.LastShown == banner, 10f, "the banner on feeding the hole shows");

            var second = Swallow(game, hole);
            await Until(() => !second.IsAlive && hole.Feeds == fed + 2, 10f, "the hole swallows the second monster and counts it");
            await Awaitable.NextFrameAsync();
            Assert.That(V2WorldLabels.NoticesWaiting, Is.Zero, "the second swallowed monster queues no banner");
            Assert.That(V2WorldLabels.HoleFirstFed(), Is.False, "the banner has had its one time in this run");
        }

        /// <summary>A Grunt from the saved pool, worth no reward, put straight into the hole's core.</summary>
        static Enemy Swallow(GameManager game, BlackHole hole)
        {
            var enemy = game.waves.SpawnLab(EnemyKind.Grunt, hole.Centre + Vector3.forward * (hole.GravityRadius + 2f), false, ElementKind.None);
            Assume.That(enemy, Is.Not.Null, "the pool has a free Grunt");
            enemy.SuppressRewards = true;
            V3CombatSmokeTest.Teleport(enemy, hole.Centre);
            Physics.SyncTransforms();
            return enemy;
        }

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
