using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class GunReverseShotEffectTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task Use_TwoEnemiesInLineBehind_DamagesOnlyTheNearest()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();

            var near = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, -3f), "EnemySmall(Near)");
            var far = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, -6f), "EnemySmall(Far)");
            var gun = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Gun, EffectId.GunReverseShot);
            var sut = (GunReverseShotEffect)gun.Effect;
            await Awaitable.FixedUpdateAsync();

            sut.Use(gun, gm.player);
            await ArenaTestUtils.WaitUntilAsync(() => !Object.FindAnyObjectByType<Bullet>());

            Assert.That(near.Hp, Is.LessThan(near.maxHp), "nearest enemy");
            Assert.That(far.Hp, Is.EqualTo(far.maxHp), "enemy behind it");
        }
    }
}
