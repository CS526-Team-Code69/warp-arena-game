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
    }
}
