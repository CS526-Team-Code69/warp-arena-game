using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    public static class InstallV4EnemyRoster
    {
        const string EnemyFolder = "Assets/AnomalyArena/V2/Data/Enemies/";
        const string WavesPath = "Assets/AnomalyArena/V2/Data/Waves15.asset";

        static readonly (EnemyKind kind, float speed)[] MeleeSpeeds =
        {
            (EnemyKind.Grunt, 5.4f), (EnemyKind.Skitter, 6.2f), (EnemyKind.Splitter, 5.0f), (EnemyKind.Brute, 4.4f)
        };

        static readonly EnemyKind[] EliteOnly = { EnemyKind.Rammer, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Stomper, EnemyKind.Shade };

        static int[] WeightsFor(int wave) => wave switch
        {
            <= 2 => new[] { 70, 10, 20, 0, 0, 0, 0, 0, 0, 0, 0 },
            <= 4 => new[] { 50, 10, 20, 15, 0, 0, 0, 10, 0, 0, 0 },
            5 => new[] { 45, 12, 20, 15, 0, 0, 5, 15, 0, 0, 0 },
            <= 9 => new[] { 40, 15, 20, 15, 8, 8, 5, 20, 0, 6, 4 },
            <= 11 => new[] { 35, 15, 22, 15, 8, 8, 6, 20, 5, 6, 5 },
            _ => new[] { 30, 18, 25, 15, 8, 8, 7, 20, 6, 8, 6 }
        };

        [MenuItem("Anomaly Arena/V4/Install Enemy Roster")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play before installing the V4 enemy roster.");
            var log = new StringBuilder("V4 enemy roster installed:\n");

            var grunt = Load(EnemyKind.Grunt);
            foreach (var (kind, speed) in MeleeSpeeds)
            {
                var data = Load(kind);
                if (data != grunt)
                {

                    data.hp = grunt.hp; data.damage = grunt.damage; data.windup = grunt.windup; data.interval = grunt.interval;
                    data.reach = grunt.reach; data.xp = grunt.xp; data.knockbackScale = grunt.knockbackScale;
                    data.canAffix = grunt.canAffix; data.startShieldShare = grunt.startShieldShare;
                }
                data.speed = speed;
                data.knockback = 0f;
                data.canElite = false;
                Save(data, log);
            }

            var spitter = Load(EnemyKind.Spitter);
            spitter.knockback = 0f; spitter.canElite = false; spitter.spikePoisonStacks = 1f;
            Save(spitter, log);

            var warden = Load(EnemyKind.Warden);
            warden.knockback = 0f; warden.canElite = false;
            Save(warden, log);

            foreach (var kind in EliteOnly)
            {
                var data = Load(kind);
                data.canElite = true;                                           
                data.canAffix = true;                                                                    
                Save(data, log);
            }

            var waves = AssetDatabase.LoadAssetAtPath<WaveSet>(WavesPath);
            if (!waves || waves.waves == null) throw new InvalidOperationException("Missing wave set: " + WavesPath);
            for (int i = 0; i < waves.waves.Length; i++)
            {
                waves.waves[i].weights = WeightsFor(i + 1);
                log.Append($"  wave {i + 1}: [{string.Join(",", waves.waves[i].weights)}]\n");
            }
            EditorUtility.SetDirty(waves);

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static EnemyDefinition Load(EnemyKind kind)
        {
            string path = EnemyFolder + kind + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (!data) throw new InvalidOperationException("Missing enemy definition: " + path);
            if (data.kind != kind) throw new InvalidOperationException($"{path} has kind {data.kind}, expected {kind}");
            return data;
        }

        static void Save(EnemyDefinition data, StringBuilder log)
        {
            EditorUtility.SetDirty(data);
            log.Append($"  {data.kind}: speed {data.speed}, damage {data.damage}, knockback {data.knockback}, canElite {data.canElite}, canAffix {data.canAffix}\n");
        }
    }
}
