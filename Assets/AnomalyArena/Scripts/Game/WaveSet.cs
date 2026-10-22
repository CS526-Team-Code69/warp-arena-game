using System;
using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Waves/Set")]
    public sealed class WaveSet : ScriptableObject
    {
        [Serializable]
        public sealed class Definition
        {
            public float duration = 30f;
            public int aliveTarget = 10;
            public float hpMultiplier = 1f;
            public float buildTime = 20f;

            [Obsolete(
                "No longer read: elites come from the batch elite share and maxConcurrentElites. Kept so saved assets still load.")]
            [HideInInspector]
            public int eliteCount;

            [Tooltip(
                "One weight per EnemyKind (Grunt, Brute, Skitter, Spitter, Rammer, Bloater, Warden, Splitter, Spire, Stomper, Shade, Hollow, Tyrant, Chanter). " +
                "A kind may appear in this wave only when its weight is above zero (system doc §11.2); the weight sets its share inside its batch group.")]
            public int[] weights;
        }

        [Tooltip("14 ordinary waves; the final boss fight is wave 15.")]
        public Definition[] waves;

        [Tooltip(
            "When set, enemy HP comes from the per-wave TTK budget; the legacy hpMultiplier is kept for reference.")]
        public EncounterBalance encounterBalance;

        [Header("Late-wave growth — candidate values, starts after wave 4")]
        public float middleHpPerWave = .15f, lateHpPerWave = .3f;

        public float middleDamagePerWave = .03f, lateDamagePerWave = .08f;
        public float middleSpeedPerWave = .01f, lateSpeedPerWave = .015f;
        public float finalBossHpFactor = 1.5f;
        public float ExtraHp(int wave) => Growth(wave, middleHpPerWave, lateHpPerWave);
        public float DamageFactor(int wave) => Growth(wave, middleDamagePerWave, lateDamagePerWave);
        public float SpeedFactor(int wave) => Mathf.Min(1.15f, Growth(wave, middleSpeedPerWave, lateSpeedPerWave));

        static float Growth(int wave, float middle, float late) => 1f +
                                                                   Mathf.Clamp(wave - 4, 0, 6) * Mathf.Max(0, middle) +
                                                                   Mathf.Max(0, wave - 10) * Mathf.Max(0, late);

        public int Weight(int wave, EnemyKind kind)
        {
            if (waves == null || wave < 1 || wave > waves.Length) return 0;
            var weights = waves[wave - 1].weights;
            return weights != null && (int)kind < weights.Length ? Mathf.Max(0, weights[(int)kind]) : 0;
        }

        public bool AllowsKind(int wave, EnemyKind kind) => Weight(wave, kind) > 0;
    }
}