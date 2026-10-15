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
            public int eliteCount;
            [Tooltip("Grunt, Brute, Skitter, Spitter, Rammer, Bloater, Warden, Splitter, Spire, Stomper, Shade.")] public int[] weights;
        }
        public Definition[] waves;
        [Tooltip("Uses wave TTK budgets when assigned; legacy hpMultiplier remains as a migration and XP reference.")]
        public EncounterBalance encounterBalance;
        [Header("Late-wave growth — candidate values, starts after wave 4")]
        public float middleHpPerWave = .15f, lateHpPerWave = .3f;
        public float middleDamagePerWave = .03f, lateDamagePerWave = .08f;
        public float middleSpeedPerWave = .01f, lateSpeedPerWave = .015f;
        public float finalBossHpFactor = 1.5f;
        public float ExtraHp(int wave) => Growth(wave,middleHpPerWave,lateHpPerWave);
        public float DamageFactor(int wave) => Growth(wave,middleDamagePerWave,lateDamagePerWave);
        public float SpeedFactor(int wave) => Mathf.Min(1.15f,Growth(wave,middleSpeedPerWave,lateSpeedPerWave));
        static float Growth(int wave,float middle,float late) => 1f+Mathf.Clamp(wave-4,0,6)*Mathf.Max(0,middle)+Mathf.Max(0,wave-10)*Mathf.Max(0,late);
    }
}
