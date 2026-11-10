using UnityEditor;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Author editable data assets in the editor; never reset an existing tuned asset.</summary>
    public static class ProgressionDataSetup
    {
        public const string DataFolder = "Assets/AnomalyArena/Data";
        public const string ProgressionPath = DataFolder + "/ProgressionRules.asset";
        public const string StatusPath = DataFolder + "/StatusRules.asset";
        public const string CatalogPath = DataFolder + "/SkillCatalog.asset";

        public static void CreateAssets()
        {
            EnsureFolder(DataFolder);
            GetOrCreate<ProgressionRules>(ProgressionPath);
            GetOrCreate<StatusRules>(StatusPath);
            EnsureFolder(DataFolder + "/Skills");
            var skills = new SkillDefinition[16];
            for (int i = 0; i < skills.Length; i++)
            {
                var id = (SkillId)i;
                string path = DataFolder + "/Skills/" + id + ".asset";
                skills[i] = AssetDatabase.LoadAssetAtPath<SkillDefinition>(path);
                if (skills[i]) continue;
                skills[i] = ScriptableObject.CreateInstance<SkillDefinition>();
                skills[i].SetDefaults(id); AssetDatabase.CreateAsset(skills[i], path);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<SkillCatalog>(CatalogPath);
            if (!catalog)
            {
                catalog = ScriptableObject.CreateInstance<SkillCatalog>();
                catalog.skills = skills; AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>Explicitly migrate only the three Greater Void definitions to the latest approved rules.</summary>
        public static void ApplyLatestVoidSkills()
        {
            CreateAssets();
            foreach (var id in new[] { SkillId.PocketVoid, SkillId.VoidWalk, SkillId.Singularity })
            {
                var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(DataFolder + "/Skills/" + id + ".asset");
                if (!definition) throw new System.InvalidOperationException("Missing Greater Void skill: " + id);
                definition.SetDefaults(id); EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Explicitly rewrite all 16 skill definitions from SetDefaults (system doc 10-21 alignment, including draft categories).
        /// Fields SetDefaults does not assign keep their current values. Run only when the saved assets should match the code defaults.
        /// </summary>
        public static void ApplyDocumentSkillDefaults()
        {
            CreateAssets();
            for (int i = 0; i <= (int)SkillId.Singularity; i++)
            {
                var id = (SkillId)i;
                var definition = AssetDatabase.LoadAssetAtPath<SkillDefinition>(DataFolder + "/Skills/" + id + ".asset");
                if (!definition) throw new System.InvalidOperationException("Missing skill: " + id);
                definition.SetDefaults(id); EditorUtility.SetDirty(definition);
            }
            AssetDatabase.SaveAssets();
        }

        static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset) return asset;
            asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset;
        }
        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            int slash = folder.LastIndexOf('/');
            string parent = folder[..slash];
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, folder[(slash + 1)..]);
        }
    }
}
