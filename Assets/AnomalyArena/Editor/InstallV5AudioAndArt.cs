using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{

    public static class InstallV5AudioAndArt
    {
        static readonly string[] ScenePaths =
        {
            "Assets/AnomalyArena/Scenes/ArenaV2.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity"
        };
        const string AudioFolder = "Assets/AnomalyArena/Audio/V5/DocAlign/";
        const string VfxFolder = "Assets/AnomalyArena/Art/V4/VFX/Textures/";
        const string IconFolder = "Assets/AnomalyArena/Art/V2/Textures/";
        const string ArtSetPath = "Assets/AnomalyArena/Art/V2/V2ArtSet.asset";
        const string ChanterSourceFbx = "Assets/AnomalyArena/Art/V3/Enemies/Chanter/Chanter-source.fbx";
        const string ChanterTextureFolder = "Assets/AnomalyArena/Art/V3/Enemies/Chanter/Textures/";
        const string ChanterModelFolder = "Assets/AnomalyArena/Art/V3/EnemyModels/Generated/Enemy_Chanter/";
        const string WardenModelFolder = "Assets/AnomalyArena/Art/V3/EnemyModels/Generated/Enemy_Warden/";
        const string ChanterPrefabPath = "Assets/AnomalyArena/V2/Prefabs/Chanter.prefab";
        const float ChanterPresentationHeight = 2.1f;

        static readonly (ArenaWorldCue cue, string file, float gain, float interval, int concurrent)[] Takes =
        {
            (ArenaWorldCue.NpcChant, "npc-chant", .5f, .4f, 2), (ArenaWorldCue.NpcLaserLock, "npc-laser-lock", .55f, .2f, 2),
            (ArenaWorldCue.NpcLaserFire, "npc-laser-fire", .6f, .15f, 2), (ArenaWorldCue.NpcRadiation, "npc-radiation", .45f, .5f, 2),
            (ArenaWorldCue.PlayerStunned, "player-stunned", .7f, .3f, 1), (ArenaWorldCue.WeaponJam, "weapon-jam", .65f, .2f, 1),
            (ArenaWorldCue.WeaponRepair, "weapon-repair", .6f, .2f, 1), (ArenaWorldCue.WeaponFocusCharge, "weapon-focus-charge", .5f, .3f, 1),
            (ArenaWorldCue.WeaponFocusRelease, "weapon-focus-release", .65f, .1f, 1), (ArenaWorldCue.WeaponKnifeReturn, "weapon-knife-return", .55f, .15f, 2),
            (ArenaWorldCue.SkillVoidWalk, "skill-void-walk", .7f, .5f, 1), (ArenaWorldCue.SkillMirrorEcho, "skill-mirror-echo", .6f, .3f, 1),
            (ArenaWorldCue.PickupEnergy, "pickup-energy", .55f, .08f, 3), (ArenaWorldCue.TimeHeld, "time-held", .75f, .5f, 1)
        };

        [MenuItem("Anomaly Arena/V5/Install Audio Bank")]
        public static void InstallAudio()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            var log = new StringBuilder("V5 audio bank:\n");
            var clips = new Dictionary<ArenaWorldCue, AudioClip>();
            foreach (var take in Takes)
            {

                string path = AudioFolder + take.file + ".wav";
                if (!File.Exists(path)) path = AudioFolder + take.file + ".mp3";
                if (AssetImporter.GetAtPath(path) is AudioImporter importer)
                {

                    var settings = importer.defaultSampleSettings;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
                    bool changed = !importer.forceToMono || importer.defaultSampleSettings.loadType != settings.loadType || importer.defaultSampleSettings.compressionFormat != settings.compressionFormat;
                    importer.forceToMono = true; importer.defaultSampleSettings = settings;
                    if (changed) importer.SaveAndReimport();
                }
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip) clips[take.cue] = clip; else log.Append($"  missing take: {path}\n");
            }
            string open = EditorSceneManager.GetActiveScene().path;
            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var bank = UnityEngine.Object.FindFirstObjectByType<V3WorldAudio>(FindObjectsInactive.Include);
                if (!bank) { log.Append($"  {path}: no V3WorldAudio\n"); continue; }
                var sounds = new List<V3WorldAudio.Sound>(bank.bank ?? Array.Empty<V3WorldAudio.Sound>());
                int added = 0, updated = 0;
                foreach (var take in Takes)
                {
                    if (!clips.TryGetValue(take.cue, out var clip)) continue;
                    var existing = sounds.Find(s => s != null && s.cue == take.cue);
                    if (existing == null) { sounds.Add(new V3WorldAudio.Sound { cue = take.cue, clip = clip, gain = take.gain, minimumInterval = take.interval, maxConcurrent = take.concurrent }); added++; }
                    else if (existing.clip != clip) { existing.clip = clip; updated++; }
                }
                bank.bank = sounds.ToArray();
                EditorUtility.SetDirty(bank);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                log.Append($"  {path}: +{added} cues, {updated} replaced, bank {sounds.Count}\n");
            }
            Reopen(open);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        [MenuItem("Anomaly Arena/V5/Install Art Hooks")]
        public static void InstallArt()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            var log = new StringBuilder("V5 art hooks:\n");
            var radiation = AssetDatabase.LoadAssetAtPath<Texture>(VfxFolder + "radiation-ground.png");
            var chant = AssetDatabase.LoadAssetAtPath<Texture>(VfxFolder + "chant-aura.png");
            var orb = AssetDatabase.LoadAssetAtPath<Texture2D>(VfxFolder + "orb-energy.png");
            var crit = LoadSprite(IconFolder + "stat-crit.png");
            var artSet = AssetDatabase.LoadAssetAtPath<V2ArtSet>(ArtSetPath);
            if (artSet)
            {
                int slot = (int)StatId.CritChance;
                if (artSet.statIcons == null || artSet.statIcons.Length <= slot) Array.Resize(ref artSet.statIcons, slot + 1);
                artSet.statIcons[slot] = crit;
                EditorUtility.SetDirty(artSet);
                log.Append($"  art set: statIcons[{slot}] = {(crit ? crit.name : "missing")}\n");
            }
            string open = EditorSceneManager.GetActiveScene().path;
            foreach (var path in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int vfxCount = 0;
                foreach (var vfx in UnityEngine.Object.FindObjectsByType<V4CombatVfx>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                { vfx.radiationGround = radiation; vfx.chantAura = chant; EditorUtility.SetDirty(vfx); vfxCount++; }
                var orbs = UnityEngine.Object.FindFirstObjectByType<OrbPool>(FindObjectsInactive.Include);
                if (orbs) { orbs.energyTexture = orb; EditorUtility.SetDirty(orbs); }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
                log.Append($"  {path}: {vfxCount} VFX pools, orb texture {(orbs ? "set" : "no pool")}\n");
            }
            Reopen(open);
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        [MenuItem("Anomaly Arena/V5/Install Chanter Model")]
        public static void InstallChanterModel()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play first.");
            var log = new StringBuilder("V5 Chanter model:\n");
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(ChanterSourceFbx);
            if (!source) throw new InvalidOperationException("Missing " + ChanterSourceFbx);
            if (AssetImporter.GetAtPath(ChanterSourceFbx) is ModelImporter importer && (importer.animationType != ModelImporterAnimationType.None || importer.materialImportMode != ModelImporterMaterialImportMode.None))
            {

                importer.animationType = ModelImporterAnimationType.None;
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.SaveAndReimport();
                source = AssetDatabase.LoadAssetAtPath<GameObject>(ChanterSourceFbx);
            }
            if (!AssetDatabase.IsValidFolder(ChanterModelFolder.TrimEnd('/'))) AssetDatabase.CreateFolder(Path.GetDirectoryName(ChanterModelFolder.TrimEnd('/')).Replace('\\', '/'), "Enemy_Chanter");

            string materialPath = ChanterModelFolder + "PBR_0.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!material)
            {
                var template = AssetDatabase.LoadAssetAtPath<Material>(WardenModelFolder + "PBR_0.mat");
                material = template ? new Material(template) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            SetTexture(material, "_BaseMap", ChanterTextureFolder + "texture_0_base_color.png");
            SetTexture(material, "_BumpMap", ChanterTextureFolder + "texture_0_normal.png", normal: true);
            SetTexture(material, "_MetallicGlossMap", ChanterModelFolder + "MetallicSmoothness_0.png");
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 1f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 1f);
            material.EnableKeyword("_NORMALMAP"); material.EnableKeyword("_METALLICSPECGLOSSMAP");
            EditorUtility.SetDirty(material);

            string modelPrefabPath = ChanterModelFolder + "Model.prefab";
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPrefabPath);
            if (!modelPrefab)
            {
                var root = new GameObject("Model");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                instance.transform.SetParent(root.transform, false);
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = material;
                    renderer.sharedMaterials = materials;
                }
                var animator = root.AddComponent<Animator>();
                string controllerPath = ChanterModelFolder + "MovementCombat.controller";
                if (!AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath)) AssetDatabase.CopyAsset(WardenModelFolder + "MovementCombat.controller", controllerPath);
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
                animator.applyRootMotion = false;
                modelPrefab = PrefabUtility.SaveAsPrefabAsset(root, modelPrefabPath);
                UnityEngine.Object.DestroyImmediate(root);
                log.Append("  model prefab created\n");
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(ChanterPrefabPath);
            try
            {
                var presentation = prefabRoot.GetComponent<V2ModelPresentation>();
                if (!presentation) throw new InvalidOperationException("Chanter prefab has no V2ModelPresentation");
                Transform parent = presentation.modelRoot ? presentation.modelRoot.parent : prefabRoot.transform;
                if (presentation.modelRoot && presentation.modelRoot.name != "ChanterModel") presentation.modelRoot.gameObject.SetActive(false);
                var existing = parent.Find("ChanterModel");
                GameObject model = existing ? existing.gameObject : null;
                if (!model)
                {
                    model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, prefabRoot.scene);
                    model.name = "ChanterModel";
                    model.transform.SetParent(parent, false);
                }
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                var bounds = new Bounds(model.transform.position, Vector3.zero);
                bool any = false;
                foreach (var r in renderers) { if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds); }
                float height = any ? Mathf.Max(.01f, bounds.size.y) : 1f;
                float scale = ChanterPresentationHeight / height;
                model.transform.localScale = Vector3.one * scale;
                model.transform.localPosition = new Vector3(0f, -bounds.min.y * scale, 0f);
                model.transform.localRotation = Quaternion.identity;
                presentation.modelRoot = model.transform;
                presentation.animator = model.GetComponent<Animator>();
                presentation.modelRenderers = renderers;
                presentation.authoredScale = model.transform.localScale;
                presentation.authoredPosition = model.transform.localPosition;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, ChanterPrefabPath);
                log.Append($"  prefab: model height {height:0.##} → scale {scale:0.###}, {renderers.Length} renderers; Warden clips stay as placeholders until a local rig exists\n");
            }
            finally { PrefabUtility.UnloadPrefabContents(prefabRoot); }
            AssetDatabase.SaveAssets();
            Debug.Log(log.ToString());
        }

        static void SetTexture(Material material, string property, string path, bool normal = false)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var wanted = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                bool srgb = property == "_BaseMap";
                if (importer.textureType != wanted || importer.sRGBTexture != srgb) { importer.textureType = wanted; importer.sRGBTexture = srgb; importer.SaveAndReimport(); }
            }
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture && material.HasProperty(property)) material.SetTexture(property, texture);
        }

        static Sprite LoadSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer && importer.textureType != TextureImporterType.Sprite)
            { importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static void Reopen(string open)
        {
            if (!string.IsNullOrEmpty(open) && Array.IndexOf(ScenePaths, open) < 0) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
            else if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open, OpenSceneMode.Single);
        }
    }
}
