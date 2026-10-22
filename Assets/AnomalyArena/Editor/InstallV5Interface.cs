using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    internal static class InstallV5Interface
    {
        static readonly string[] ScenePaths =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };

        [MenuItem("Anomaly Arena/V5/Install Interface Scale")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            string open = EditorSceneManager.GetActiveScene().path;
            var log = new StringBuilder("V5 interface scale:\n");
            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>(FindObjectsInactive.Include);
                if (!hud)
                {
                    log.Append($"  {path}: no V2Hud\n");
                    continue;
                }

                var layout = hud.GetComponent<V2HudLayout>();
                bool added = false;
                if (!layout)
                {
                    layout = Undo.AddComponent<V2HudLayout>(hud.gameObject);
                    added = true;
                }

                Undo.RecordObject(layout, "V5 interface scale");
                layout.hud = hud;
                EditorUtility.SetDirty(layout);
                layout.Apply();

                var labels = hud.GetComponentInChildren<V2WorldLabels>(true);
                if (labels && !labels.orbHintLabel && labels.holeLabels != null && labels.holeLabels.Length > 0 && labels.holeLabels[0])
                {
                    var source = labels.holeLabels[0];
                    var clone = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
                    clone.name = "OrbHintLabel";
                    clone.SetActive(false);
                    var text = clone.GetComponent<UnityEngine.UI.Text>();
                    text.text = "";
                    text.fontStyle = FontStyle.Bold;
                    Undo.RegisterCreatedObjectUndo(clone, "Orb hint label");
                    Undo.RecordObject(labels, "Orb hint label");
                    labels.orbHintLabel = text;
                    EditorUtility.SetDirty(labels);
                    log.Append("  orb hint label added\n");
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                log.Append(
                    $"  {path}: V2HudLayout {(added ? "added with defaults" : "kept (scene values)")}, reference {layout.referenceResolution.x:0}x{layout.referenceResolution.y:0}, applied\n");
            }

            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }
    }
}