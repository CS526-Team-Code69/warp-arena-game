using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    internal static class InstallV5SpawnCross
    {
        const string TexturePath = "Assets/AnomalyArena/Art/V4/VFX/Textures/spawn-cross.png";
        const string TelegraphPath = "Assets/AnomalyArena/V2/Materials/Telegraph.mat";
        const string MaterialPath = "Assets/AnomalyArena/V2/Materials/SpawnCross.mat";
        static readonly string[] ScenePaths =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity",
            "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };

        [MenuItem("Anomaly Arena/V5/Install Spawn Cross")]
        public static void Install()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play first.");
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            if (!texture) throw new System.IO.FileNotFoundException("Missing cross texture", TexturePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (!material)
            {
                var telegraph = AssetDatabase.LoadAssetAtPath<Material>(TelegraphPath);
                if (!telegraph) throw new System.IO.FileNotFoundException("Missing telegraph material", TelegraphPath);
                material = new Material(telegraph) { name = "SpawnCross" };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.mainTexture = texture;
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            var quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

            string open = EditorSceneManager.GetActiveScene().path;
            var log = new StringBuilder("V5 spawn cross:\n");
            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int changed = 0, total = 0;
                foreach (var marker in Object.FindObjectsByType<WaveSpawnMarker>(FindObjectsInactive.Include))
                {
                    if (!marker.visual) continue;
                    total++;
                    var filter = marker.visual.GetComponent<MeshFilter>();
                    if (!filter) continue;
                    var t = marker.visual.transform;
                    bool already = filter.sharedMesh == quad && marker.visual.sharedMaterial == material;
                    if (already) continue;
                    Undo.RecordObject(filter, "Spawn cross");
                    Undo.RecordObject(marker.visual, "Spawn cross");
                    Undo.RecordObject(t, "Spawn cross");
                    filter.sharedMesh = quad;
                    marker.visual.sharedMaterial = material;

                    t.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    t.localPosition = new Vector3(0f, .02f, 0f);
                    t.localScale = new Vector3(1.2f, 1.2f, 1f);
                    var collider = marker.visual.GetComponent<Collider>();
                    if (collider) Undo.DestroyObjectImmediate(collider);
                    EditorUtility.SetDirty(filter);
                    EditorUtility.SetDirty(marker.visual);
                    changed++;
                }
                if (changed > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                log.Append($"  {path}: {changed} of {total} markers switched to the cross\n");
            }
            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }
    }
}
