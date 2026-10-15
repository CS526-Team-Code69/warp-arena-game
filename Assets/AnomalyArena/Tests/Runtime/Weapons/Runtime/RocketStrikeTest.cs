using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class RocketStrikeTest
    {
        static readonly Vector3 MarkedPoint = new Vector3(0f, 0f, 8f);

        static bool StrikeHasLanded() => Object.FindAnyObjectByType<RocketStrike>() == null;

        static float BlastDamage(Weapon weapon) => ((MissileStrikeEffect)weapon.Effect).damage;

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task Strike_EnemyOnTheMarkedPoint_LosesBlastDamageWhenTheRocketLands()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var enemy = ArenaTestUtils.CreateEnemy(MarkedPoint, "EnemySmall");
            gm.player.DebugSetAimPoint(MarkedPoint);
            var weapon = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Missile, EffectId.MissileStrike);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(StrikeHasLanded);

            Assert.That(enemy.Hp, Is.EqualTo(ArenaTestUtils.SturdyHp - BlastDamage(weapon)));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task Strike_HalfTheDelayHasPassed_EnemyOnTheMarkedPointKeepsItsHp()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var enemy = ArenaTestUtils.CreateEnemy(MarkedPoint, "EnemySmall");
            gm.player.DebugSetAimPoint(MarkedPoint);
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Missile, EffectId.MissileStrike);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitFixedUpdatesAsync(25);                          

            Assert.That(enemy.Hp, Is.EqualTo(ArenaTestUtils.SturdyHp));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task Strike_EnemyOutsideTheBlastRadius_KeepsItsHp()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();

            var enemy = ArenaTestUtils.CreateEnemy(MarkedPoint + new Vector3(5f, 0f, 0f), "EnemySmall");
            gm.player.DebugSetAimPoint(MarkedPoint);
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Missile, EffectId.MissileStrike);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(StrikeHasLanded);

            Assert.That(enemy.Hp, Is.EqualTo(ArenaTestUtils.SturdyHp));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task Strike_AimMovedAfterTheCall_StillLandsOnTheFirstPoint()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var enemy = ArenaTestUtils.CreateEnemy(MarkedPoint, "EnemySmall");
            gm.player.DebugSetAimPoint(MarkedPoint);
            var weapon = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Missile, EffectId.MissileStrike);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            gm.player.DebugSetAimPoint(new Vector3(8f, 0f, 0f));
            await ArenaTestUtils.WaitUntilAsync(StrikeHasLanded);

            Assert.That(enemy.Hp, Is.EqualTo(ArenaTestUtils.SturdyHp - BlastDamage(weapon)));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(15000)]
        public async Task Strike_MarkedPointNextToTheUser_UserLosesBlastDamage()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            gm.player.DebugSetAimPoint(new Vector3(0f, 0f, 1f));
            var weapon = ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Missile, EffectId.MissileStrike);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(StrikeHasLanded);

            Assert.That(gm.player.Hp, Is.EqualTo(gm.player.maxHp - BlastDamage(weapon)));
        }
    }
}
