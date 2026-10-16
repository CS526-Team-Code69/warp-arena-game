using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AnomalyArena.EditorTools
{

    public static class InstallV4LegacyCleanup
    {
        static readonly string[] Scenes =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };

        static readonly string[] RetiredObjects = { "WeaponSpawner" };

        [Serializable]
        class Entry
        {
            public string scene;
            public int missingScriptsRemoved, objectsRemoved;
        }

        [Serializable]
        class Receipt
        {
            public string savedAtUtc, rollbackFolder;
            public Entry[] scenes;
            public string[] buildScenes;
        }

        [MenuItem("Anomaly Arena/V4/Remove 1.0 Leftovers")]
        public static void Install()
        {
            if (EditorApplication.isPlaying)
                throw new InvalidOperationException("Stop Play before removing 1.0 leftovers.");
            var current = SceneManager.GetActiveScene();
            string returnTo = current.path;
            if (current.isDirty)
                throw new InvalidOperationException("The open scene has unsaved edits; save or discard them first.");
            var scenes = Scenes.Where(File.Exists).Distinct().ToArray();
            if (scenes.Length == 0) throw new InvalidOperationException("No V2 scene found.");
            string backup = "work/rollback/v4-legacy-cleanup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(backup);
            foreach (var path in scenes) File.Copy(path, backup + "/" + Path.GetFileName(path), false);
            File.Copy("ProjectSettings/EditorBuildSettings.asset", backup + "/EditorBuildSettings.asset", false);

            var entries = new List<Entry>();
            foreach (var path in scenes)
            {
                var scene = SceneManager.GetActiveScene().path == path
                    ? SceneManager.GetActiveScene()
                    : EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var entry = new Entry { scene = path };
                foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    entry.missingScriptsRemoved += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                foreach (var root in scene.GetRootGameObjects().Where(r => RetiredObjects.Contains(r.name)).ToArray())
                {

                    if (root.GetComponents<Component>().Length > 1 || root.transform.childCount > 0) continue;
                    UnityEngine.Object.DestroyImmediate(root);
                    entry.objectsRemoved++;
                }

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Save failed: " + path);
                entries.Add(entry);
            }

            EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => File.Exists(s.path)).ToArray();
            AssetDatabase.SaveAssets();
            var receipt = new Receipt
            {
                savedAtUtc = DateTime.UtcNow.ToString("o"), rollbackFolder = backup, scenes = entries.ToArray(),
                buildScenes = EditorBuildSettings.scenes.Select(s => s.path).ToArray()
            };
            Directory.CreateDirectory("work/authoring-receipts");
            File.WriteAllText("work/authoring-receipts/v4-legacy-cleanup.json", JsonUtility.ToJson(receipt, true));
            if (!string.IsNullOrEmpty(returnTo) && SceneManager.GetActiveScene().path != returnTo && File.Exists(returnTo))
                EditorSceneManager.OpenScene(returnTo, OpenSceneMode.Single);
            Debug.Log("[V4LegacyCleanup] " + JsonUtility.ToJson(receipt));
        }
    }
}
