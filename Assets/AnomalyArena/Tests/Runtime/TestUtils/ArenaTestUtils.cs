using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    internal static class ArenaTestUtils
    {
        public const string ArenaScene = "Assets/AnomalyArena/Scenes/Arena.unity";

        public const float SturdyHp = 999f;

        public static async Awaitable<GameManager> StartSandboxAsync()
        {
            await Awaitable.NextFrameAsync();
            var gm = GameManager.Instance;
            gm.waves.enabled = false;
            gm.BeginPlaying();
            foreach (var w in gm.weapons.ground.ToArray())
            {
                gm.weapons.Take(w);
                Object.Destroy(w.gameObject);
            }

            gm.player.DebugSetAim(Vector3.forward);
            return gm;
        }

        public static Enemy CreateEnemy(Vector3 position, string name, bool large = false)
        {
            var gm = GameManager.Instance;
            var enemy = Object.Instantiate(large ? gm.waves.largePrefab : gm.waves.smallPrefab, position,
                Quaternion.identity);
            enemy.name = name;
            enemy.moveSpeed = 0f;
            enemy.attackInterval = 9999f;
            enemy.maxHp = SturdyHp;
            enemy.Heal(SturdyHp);
            return enemy;
        }

        public static Weapon GiveWeapon(PlayerController player, WeaponType type, EffectId effect)
        {
            var spawner = GameManager.Instance.weapons;
            var def = spawner.GetDef(type);
            var weapon = spawner.Spawn(def, new Vector3(20f, 0f, 20f));
            weapon.name = $"{type}({effect})";
            weapon.Init(spawner.GetEffect(effect), def.uses);
            spawner.Take(weapon);
            player.Equip(weapon);
            return weapon;
        }

        public static async Awaitable WaitUntilAsync(Func<bool> condition)
        {
            while (!condition()) await Awaitable.NextFrameAsync();
        }

        public static async Awaitable WaitFixedUpdatesAsync(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.FixedUpdateAsync();
        }
    }
}