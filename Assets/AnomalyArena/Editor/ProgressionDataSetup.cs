using UnityEditor;
using UnityEngine;

namespace AnomalyArena
{
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
            EnsureFolder(DataFolder + "/Skills"); EnsureFolder(DataFolder + "/Elements");
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
            for (int i = 1; i <= 7; i++)
            {
                var element = (ElementKind)i;
                string path = DataFolder + "/Elements/" + element + ".asset";
                if (AssetDatabase.LoadAssetAtPath<ElementDefinition>(path)) continue;
                var definition = ScriptableObject.CreateInstance<ElementDefinition>();
                definition.element = element; definition.displayName = element.ToString();
                definition.colour = ElementCombat.ColorFor(element);
                definition.paysWielder = element is ElementKind.Earth or ElementKind.Light or ElementKind.Void;
                AssetDatabase.CreateAsset(definition, path);
            }
            AssetDatabase.SaveAssets();
        }

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
