using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class HitFeedbackTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [TearDown]
        public void RestoreClock() => Time.timeScale = 1f;

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task ReceiveDamage_RepeatedEnemyHits_DoNotSlowTheWholeWorld()
        {
            await Awaitable.NextFrameAsync();
            var gm = GameManager.Instance;
            gm.enabled = false;
            var enemy = gm.waves.preparedEnemies[0];
            enemy.Configure(enemy.definition, 1f, false);
            enemy.maxHp = 10000f;
            enemy.Heal(10000f);
            Time.timeScale = 1f;

            foreach (HitKind kind in System.Enum.GetValues(typeof(HitKind)))
            {
                enemy.ReceiveDamage(DamageInfo.Attack(1f, gm.player, kind: kind));
                Assert.That(Time.timeScale, Is.EqualTo(1f), $"Global clock after {kind}");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task ReceiveDamage_BurnAndPoison_DoNotSlowTheWholeWorld()
        {
            await Awaitable.NextFrameAsync();
            var gm = GameManager.Instance;
            gm.enabled = false;
            var enemy = gm.waves.preparedEnemies[0];
            enemy.Configure(enemy.definition, 1f, false);
            enemy.maxHp = 10000f;
            enemy.Heal(10000f);
            Time.timeScale = 1f;

            enemy.ReceiveDamage(new DamageInfo { amount = 1f, source = DamageSource.Burn, dealer = gm.player });
            enemy.ReceiveDamage(new DamageInfo { amount = 1f, source = DamageSource.Poison, dealer = gm.player });

            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task ReceiveDamage_PlayerHitByNonBossEnemy_FeedbackOnlyWithoutStunOrPush()
        {
            await Awaitable.NextFrameAsync();
            var gm = GameManager.Instance;
            gm.enabled = false;
            var player = gm.player;
            Assume.That(player && player.IsAlive, Is.True, "the scene keeps a live player");
            player.protectionTime = 0f;
            player.ClearWaveState();
            ElementStatus.For(player)?.SetShield(0f);
            var grunt = gm.waves.preparedEnemies.First(e => e && e.definition && !e.IsBoss);
            grunt.Configure(grunt.definition, 1f, false);
            grunt.enabled = false;
            float hpBefore = player.Hp;

            Time.timeScale = 1f;

            bool landed = player.ReceiveDamage(DamageInfo.Attack(1f, grunt, Vector3.right, 6f));

            Assert.That(landed, Is.True, "the hit lands");
            Assert.That(player.Hp, Is.LessThan(hpBefore), "the hit costs HP");
            Assert.That(player.HitFlash01, Is.GreaterThan(0f), "the hit still flashes the player");
            Assert.That(player.Stunned, Is.False, "no hit stun");
            Assert.That(player.ActionsBlocked, Is.False, "the player keeps control");
            Assert.That(player.State, Is.EqualTo(CharacterState.Normal), "a non-boss enemy never pushes the player");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "a hit on the player never slows the game");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task ReceiveDamage_PlayerHitByBoss_StillKnockedBackWithoutStun()
        {
            await Awaitable.NextFrameAsync();
            var gm = GameManager.Instance;
            gm.enabled = false;
            var player = gm.player;
            Assume.That(player && player.IsAlive, Is.True, "the scene keeps a live player");
            player.protectionTime = 0f;
            player.ClearWaveState();
            var boss = gm.waves.tyrant;
            Assume.That(boss && boss.definition && boss.IsBoss, Is.True, "the scene keeps the Tyrant");

            bool landed = player.ReceiveDamage(DamageInfo.Attack(1f, boss, Vector3.right, 6f));

            Assert.That(landed, Is.True, "the boss hit lands");
            Assert.That(player.State, Is.EqualTo(CharacterState.Knocked), "bosses still knock the player back");
            Assert.That(player.Stunned, Is.False, "a boss hit pushes but does not stun");
            player.ClearWaveState();
        }
    }
}
