using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class GunChargedShotEffectTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task Fire_TwoEnemiesInLine_DamagesBoth()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var near = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 3f), "EnemySmall(Near)");

            var far = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 12f), "EnemySmall(Far)");
            var sut = (GunChargedShotEffect)gm.weapons.GetEffect(EffectId.GunChargedShot);
            await Awaitable.FixedUpdateAsync();

            sut.Fire(gm.player, 0f);
            await ArenaTestUtils.WaitUntilAsync(() => !Object.FindAnyObjectByType<Bullet>());

            Assert.That(new[] { near, far }, Has.All.Matches<Enemy>(e => e.Hp < e.maxHp));
        }

        [TestCase(0f)]
        [TestCase(1f)]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task Fire_AtGivenCharge_ReducesHpByDamageAtThatCharge(float charge)
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();

            var enemy = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 2f), "EnemySmall");
            var sut = (GunChargedShotEffect)gm.weapons.GetEffect(EffectId.GunChargedShot);
            await Awaitable.FixedUpdateAsync();
            var expected = enemy.maxHp - sut.DamageAt(charge);

            sut.Fire(gm.player, charge);
            await ArenaTestUtils.WaitUntilAsync(() => !Object.FindAnyObjectByType<Bullet>());

            Assert.That(enemy.Hp, Is.EqualTo(expected));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task DamageAt_FullCharge_ReturnsMoreThanTap()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var sut = (GunChargedShotEffect)gm.weapons.GetEffect(EffectId.GunChargedShot);

            var actual = sut.DamageAt(1f);

            Assert.That(actual, Is.GreaterThan(sut.DamageAt(0f)));
        }
    }
}