using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{
    [TestFixture]
    public class HookTest
    {
        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task HookedEnemy_PulledInFront_StaysHeldUntilSecondClick()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var enemy = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 4f), "EnemySmall");
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeHook);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => gm.player.HeldShield != null);

            await ArenaTestUtils.WaitFixedUpdatesAsync(10);

            Assert.That(enemy.State, Is.EqualTo(CharacterState.Hooked));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task HeldEnemy_SecondClick_IsFlung()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            var enemy = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 4f), "EnemySmall");
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeHook);
            await Awaitable.FixedUpdateAsync();
            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => gm.player.HeldShield != null);

            gm.player.TryUseWeapon();

            Assert.That(enemy.State, Is.EqualTo(CharacterState.Thrown));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task HoldingHookedEnemy_PlayerKeepsFullMoveSpeed()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();
            ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 4f), "EnemySmall");
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeHook);
            await Awaitable.FixedUpdateAsync();

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => gm.player.HeldShield != null);

            Assert.That(gm.player.SpeedMultiplier, Is.EqualTo(1f));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task FlungEnemy_CrashesIntoAnotherEnemy_DamagesBoth()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();

            var flung = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 3f), "EnemySmall(Flung)");
            var bystander = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 5f), "EnemySmall(Bystander)");
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeHook);
            await Awaitable.FixedUpdateAsync();
            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => gm.player.HeldShield != null);

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => bystander.Hp < bystander.maxHp);

            Assert.That(flung.Hp, Is.LessThan(flung.maxHp));
        }

        [Test]
        [LoadScene(ArenaTestUtils.ArenaScene)]
        [Category("Integration")]
        [Timeout(10000)]
        public async Task FlungEnemy_CrashesWithinReachOfThePlayer_LeavesPlayerUnharmed()
        {
            var gm = await ArenaTestUtils.StartSandboxAsync();

            ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 2f), "EnemySmall(Flung)");
            var bystander = ArenaTestUtils.CreateEnemy(new Vector3(0f, 0f, 3.1f), "EnemySmall(Bystander)");
            ArenaTestUtils.GiveWeapon(gm.player, WeaponType.Knife, EffectId.KnifeHook);
            await Awaitable.FixedUpdateAsync();
            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => gm.player.HeldShield != null);

            gm.player.TryUseWeapon();
            await ArenaTestUtils.WaitUntilAsync(() => bystander.Hp < bystander.maxHp);

            Assert.That(gm.player.Hp, Is.EqualTo(gm.player.maxHp));
        }
    }
}