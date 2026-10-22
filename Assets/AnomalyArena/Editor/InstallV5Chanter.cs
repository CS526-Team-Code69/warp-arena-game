using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
                 
                                                                                                                      
                                                                                                                  
                                                                                  
                  
    public static class InstallV5Chanter
    {
        const string WardenPrefabPath = "Assets/AnomalyArena/V2/Prefabs/Warden.prefab";
        const string ChanterPrefabPath = "Assets/AnomalyArena/V2/Prefabs/Chanter.prefab";
        const string ChanterDefinitionPath = "Assets/AnomalyArena/V2/Data/Enemies/Chanter.asset";
        static readonly string[] ScenePaths =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };
        [Tooltip("Pooled Chanters per scene; the Warden pool is the reference size.")]
        const int PoolSize = 12;

                                                                                                                        
                                                                                                             
        static readonly (EnemyKind kind, int target)[] PoolTargets =
        {
            (EnemyKind.Splitter, 72), (EnemyKind.Rammer, 36), (EnemyKind.Bloater, 36), (EnemyKind.Spire, 36), (EnemyKind.Stomper, 36), (EnemyKind.Shade, 36)
        };

        [MenuItem("Anomaly Arena/V5/Install Chanter Pool")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before installing the Chanter pool.");
            var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ChanterDefinitionPath);
            if (!definition) throw new InvalidOperationException("Missing " + ChanterDefinitionPath + " (run Anomaly Arena/V5/Install Enemy Roster first).");
            var log = new StringBuilder("V5 Chanter pool:\n");
            var prefab = EnsurePrefab(definition, log);

            string open = EditorSceneManager.GetActiveScene().path;
            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var waves = UnityEngine.Object.FindFirstObjectByType<WaveManager>(FindObjectsInactive.Include);
                if (!waves) { log.Append($"  {path}: no WaveManager, skipped\n"); continue; }
                int added = InstallInScene(waves, prefab, definition);
                log.Append($"  {path}: +{added} Chanters, pool {Count(waves, definition)}, roster slot {(int)EnemyKind.Chanter}\n");
                foreach (var (kind, target) in PoolTargets)
                {
                    int grown = TopUpPool(waves, kind, target);
                    if (grown > 0) log.Append($"    {kind}: +{grown} (pool now {target})\n");
                }
                EditorUtility.SetDirty(waves);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            if (!string.IsNullOrEmpty(open) && Array.IndexOf(ScenePaths, open) < 0) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static GameObject EnsurePrefab(EnemyDefinition definition, StringBuilder log)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ChanterPrefabPath);
            if (existing) { log.Append("  prefab exists\n"); return existing; }
            var warden = AssetDatabase.LoadAssetAtPath<GameObject>(WardenPrefabPath);
            if (!warden) throw new InvalidOperationException("Missing " + WardenPrefabPath);
                                                                                                                         
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(warden);
            instance.name = "Chanter";
            var enemy = instance.GetComponent<Enemy>();
            enemy.definition = definition;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                if (renderer.name == "Body") Art.Tint(renderer, definition.colour);
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, ChanterPrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            log.Append("  prefab created as a Warden variant\n");
            return variant;
        }

        static int InstallInScene(WaveManager waves, GameObject prefab, EnemyDefinition definition)
        {
            var pool = new List<Enemy>(waves.preparedEnemies ?? Array.Empty<Enemy>());
            Transform parent = null;
            foreach (var enemy in pool)
                if (enemy && enemy.definition && enemy.definition.kind == EnemyKind.Warden) { parent = enemy.transform.parent; break; }
            int have = Count(waves, definition), added = 0;
            for (int i = have; i < PoolSize; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, waves.gameObject.scene);
                instance.name = $"Chanter {i + 1}";
                if (parent) instance.transform.SetParent(parent, false);
                instance.transform.position = new Vector3(-200f, -50f, -200f + i * 2f);
                instance.SetActive(false);
                var enemy = instance.GetComponent<Enemy>();
                enemy.pooled = true;
                enemy.definition = definition;
                pool.Add(enemy);
                added++;
            }
            waves.preparedEnemies = pool.ToArray();
            int slot = (int)EnemyKind.Chanter;
            var roster = new List<EnemyDefinition>(waves.roster ?? Array.Empty<EnemyDefinition>());
            while (roster.Count <= slot) roster.Add(null);
            roster[slot] = definition;
            waves.roster = roster.ToArray();
            return added;
        }

                                                                                                                          
        static int TopUpPool(WaveManager waves, EnemyKind kind, int target)
        {
            var pool = new List<Enemy>(waves.preparedEnemies ?? Array.Empty<Enemy>());
            Enemy sample = null; int have = 0;
            foreach (var enemy in pool)
                if (enemy && enemy.definition && enemy.definition.kind == kind) { have++; sample ??= enemy; }
            if (!sample || have >= target) return 0;
            var source = PrefabUtility.GetCorrespondingObjectFromSource(sample.gameObject);
            if (!source) return 0;
            int added = 0;
            for (int i = have; i < target; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, waves.gameObject.scene);
                instance.name = $"{kind} {i + 1}";
                instance.transform.SetParent(sample.transform.parent, false);
                instance.transform.position = new Vector3(-200f, -50f, -240f + i * 2f);
                instance.SetActive(false);
                var enemy = instance.GetComponent<Enemy>();
                enemy.pooled = true;
                enemy.definition = sample.definition;
                pool.Add(enemy);
                added++;
            }
            waves.preparedEnemies = pool.ToArray();
            return added;
        }

        static int Count(WaveManager waves, EnemyDefinition definition)
        {
            int count = 0;
            if (waves.preparedEnemies != null)
                foreach (var enemy in waves.preparedEnemies) if (enemy && enemy.definition == definition) count++;
            return count;
        }
    }
}
