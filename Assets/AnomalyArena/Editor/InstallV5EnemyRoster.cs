using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    public static class InstallV5EnemyRoster
    {
        const string EnemyFolder = "Assets/AnomalyArena/V2/Data/Enemies/";
        const string WavesPath = "Assets/AnomalyArena/V2/Data/Waves15.asset";
        const string BalancePath = "Assets/AnomalyArena/Data/EncounterBalance.asset";
        const int OrdinaryWaves = 14;

        static readonly (EnemyKind kind, float damage, float windup, float speed, float radius, bool splits)[] Melee =
        {
            (EnemyKind.Grunt, 5f, .5f, 5.4f, .5f, false),
            (EnemyKind.Brute, 8f, .9f, 4.4f, 1f, false),
            (EnemyKind.Skitter, 3f, .3f, 6.2f, .3f, false),
            (EnemyKind.Splitter, 5f, .5f, 5.0f, .5f, true),
        };

        static readonly EnemyKind[] Specials = { EnemyKind.Rammer, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Stomper, EnemyKind.Shade, EnemyKind.Chanter };

        static readonly EnemyKind[][] Allowed =
        {
            new EnemyKind[0],
            new[] { EnemyKind.Spitter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Rammer },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Spire, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Stomper },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Stomper },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Rammer, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater, EnemyKind.Shade, EnemyKind.Chanter },
            new[] { EnemyKind.Spitter, EnemyKind.Warden, EnemyKind.Rammer, EnemyKind.Stomper, EnemyKind.Bloater, EnemyKind.Spire, EnemyKind.Shade, EnemyKind.Chanter },
        };

        static int[] WeightsFor(int wave)
        {
            var weights = new int[Enum.GetValues(typeof(EnemyKind)).Length];
            (int grunt, int brute, int skitter, int splitter) = wave switch
            {
                <= 2 => (70, 10, 20, 0),
                <= 4 => (50, 10, 20, 10),
                5 => (45, 12, 20, 15),
                <= 9 => (40, 15, 20, 20),
                <= 11 => (35, 15, 22, 20),
                _ => (30, 18, 25, 20)
            };
            weights[(int)EnemyKind.Grunt] = grunt; weights[(int)EnemyKind.Brute] = brute;
            weights[(int)EnemyKind.Skitter] = skitter; weights[(int)EnemyKind.Splitter] = splitter;
            foreach (var kind in Allowed[wave - 1])
                weights[(int)kind] = kind switch
                {
                    EnemyKind.Spitter => 15,
                    EnemyKind.Warden => wave >= 12 ? 7 : 6,
                    EnemyKind.Shade => 5,
                    _ => 6
                };
            return weights;
        }

        [MenuItem("Anomaly Arena/V5/Install Enemy Roster")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play before installing the V5 enemy roster.");
            var log = new StringBuilder("V5 enemy roster installed:\n");

            var grunt = Load(EnemyKind.Grunt);
            foreach (var (kind, damage, windup, speed, radius, splits) in Melee)
            {
                var data = Load(kind);
                if (data != grunt)
                {

                    data.hp = grunt.hp; data.interval = grunt.interval; data.reach = grunt.reach; data.xp = grunt.xp;
                    data.knockbackScale = grunt.knockbackScale; data.canAffix = grunt.canAffix; data.startShieldShare = grunt.startShieldShare;
                }
                data.damage = damage; data.windup = windup; data.speed = speed; data.radius = radius; data.splitsOnDeath = splits;
                data.knockback = 0f;
                data.canElite = false;
                Save(data, log);
            }

            var spitter = Load(EnemyKind.Spitter);
            spitter.knockback = 0f; spitter.canElite = false; spitter.spikePoisonStacks = 1f; spitter.splitsOnDeath = false;
            Save(spitter, log);

            var warden = Load(EnemyKind.Warden);
            warden.knockback = 0f; warden.canElite = false; warden.canAffix = false; warden.splitsOnDeath = false;                                      
            Save(warden, log);

            var chanter = LoadOrCreateChanter(warden, log);
            chanter.hp = 20f; chanter.speed = 4.5f; chanter.radius = .5f; chanter.damage = 0f; chanter.windup = .5f; chanter.interval = 3f;
            chanter.reach = warden.reach; chanter.knockback = 0f; chanter.knockbackScale = 1f; chanter.xp = 3; chanter.startShieldShare = 0f;
            chanter.colour = new Color(.62f, .25f, .95f);
            Save(chanter, log);

            foreach (var kind in Specials)
            {
                var data = kind == EnemyKind.Chanter ? chanter : Load(kind);
                data.canElite = true;                                   
                data.canAffix = true;
                data.splitsOnDeath = false;
                Save(data, log);
            }

            var waves = AssetDatabase.LoadAssetAtPath<WaveSet>(WavesPath);
            if (!waves || waves.waves == null || waves.waves.Length < OrdinaryWaves)
                throw new InvalidOperationException($"{WavesPath} needs at least {OrdinaryWaves} wave entries.");
            if (waves.waves.Length > OrdinaryWaves) waves.waves = waves.waves.Take(OrdinaryWaves).ToArray();                              
            for (int i = 0; i < waves.waves.Length; i++)
            {
                waves.waves[i].weights = WeightsFor(i + 1);
                log.Append($"  wave {i + 1}: [{string.Join(",", waves.waves[i].weights)}]\n");
            }
            EditorUtility.SetDirty(waves);

            var balance = waves.encounterBalance ? waves.encounterBalance : AssetDatabase.LoadAssetAtPath<EncounterBalance>(BalancePath);
            if (!balance) throw new InvalidOperationException("Missing encounter balance: " + BalancePath);
            balance.functionSeconds = WithChanter(balance.functionSeconds, 2.3f);
            balance.eliteSeconds = WithChanter(balance.eliteSeconds, 7.5f);
            balance.bossSeconds = 90f;
            balance.splitterMiniHpShare = .4f;
            EditorUtility.SetDirty(balance);
            log.Append($"  balance: boss {balance.bossSeconds}s, Chanter {balance.functionSeconds[(int)EnemyKind.Chanter]}/{balance.eliteSeconds[(int)EnemyKind.Chanter]}s, mini HP {balance.splitterMiniHpShare}\n");

            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static float[] WithChanter(float[] values, float chanter)
        {
            int length = Enum.GetValues(typeof(EnemyKind)).Length;
            var result = new float[Mathf.Max(length, values?.Length ?? 0)];
            if (values != null) Array.Copy(values, result, values.Length);
            result[(int)EnemyKind.Chanter] = chanter;
            return result;
        }

        static EnemyDefinition LoadOrCreateChanter(EnemyDefinition template, StringBuilder log)
        {
            string path = EnemyFolder + EnemyKind.Chanter + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(path);
            if (data) return data.kind == EnemyKind.Chanter ? data : throw new InvalidOperationException($"{path} has kind {data.kind}, expected Chanter");
            data = UnityEngine.Object.Instantiate(template);
            data.kind = EnemyKind.Chanter;
            data.displayName = "Chanter";
            AssetDatabase.CreateAsset(data, path);
            log.Append("  created " + path + "\n");
            return data;
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
            log.Append($"  {data.kind}: speed {data.speed}, damage {data.damage}, windup {data.windup}, radius {data.radius}, canElite {data.canElite}, canAffix {data.canAffix}, splits {data.splitsOnDeath}\n");
        }
    }
}
