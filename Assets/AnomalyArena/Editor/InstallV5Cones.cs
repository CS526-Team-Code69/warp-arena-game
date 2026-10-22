using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    internal static class InstallV5Cones
    {
        static readonly string[] ScenePaths =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };

        const string Folder = "Assets/AnomalyArena/Art/V4/VFX/Textures/";

        [MenuItem("Anomaly Arena/V5/Install Cone Effects")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            string open = EditorSceneManager.GetActiveScene().path;
            var log = new StringBuilder("V5 cone effects:\n");
            var fire = Load("cone-fire-0", "cone-fire-1", "cone-fire-2", "cone-fire-3");
            var frost = Load("cone-frost-0", "cone-frost-1");
            var poison = Load("cone-poison-0", "cone-poison-1");
            var puffs = Load("status-fire-flame-0", "status-fire-flame-1", "status-fire-flame-2", "status-fire-flame-3", "status-fire-flame-4", "status-fire-flame-5");
            var plain = Load("cone-plain")[0];
            var telegraph = Load("cone-telegraph")[0];
            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var pool = UnityEngine.Object.FindAnyObjectByType<FxPool>(FindObjectsInactive.Include);
                if (!pool) { log.Append($"  {path}: no FxPool\n"); continue; }
                Undo.RecordObject(pool, "V5 cone effects");
                pool.coneFire = fire;
                pool.coneFrost = frost;
                pool.conePoison = poison;
                pool.flamePuffs = puffs;
                pool.conePlain = plain;
                pool.telegraph = telegraph;
                EditorUtility.SetDirty(pool);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.Append($"  {path}: fire {Count(fire)}, frost {Count(frost)}, poison {Count(poison)}, puffs {Count(puffs)}, plain {(plain ? "ok" : "missing")}, telegraph {(telegraph ? "ok" : "missing")}\n");
            }
            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static Texture[] Load(params string[] names)
        {
            var textures = new Texture[names.Length];
            for (int i = 0; i < names.Length; i++) textures[i] = AssetDatabase.LoadAssetAtPath<Texture>(Folder + names[i] + ".png");
            return textures;
        }

        static int Count(Texture[] set)
        {
            int n = 0;
            foreach (var t in set) if (t) n++;
            return n;
        }
    }
}
