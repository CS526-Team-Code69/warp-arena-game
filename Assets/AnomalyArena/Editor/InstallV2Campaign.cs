using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
                 
                                                                                                                                         
                                                                                                                                           
                                                                                                                                    
                                                                                                                 
                                                                                                                                         
                                                                                                                                          
                                                                                                                                     
                                                                                               
                                                                                                                 
                                                                                                                                  
                                                                                                                             
                  
    public static class InstallV2Campaign
    {
        const string ArtRoot = "Assets/AnomalyArena/Art/Campaign/";
        const string DataRoot = "Assets/AnomalyArena/Data/Campaign/";
        const string RulesPath = DataRoot + "V2CampaignRules.asset";
        const string ArtPath = DataRoot + "V2CampaignArt.asset";
                                                                                                                                      
                                                                                                                                    
                                                                                                                                      
        static readonly string[] Scenes = { "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity", "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity" };
        const string LitShader = "Universal Render Pipeline/Lit";
        const string UnlitShader = "Universal Render Pipeline/Unlit";
                                                                                                                                      
                                                                                                                           
                                                                                                                          
                                                                                                                             
        const string BaseMesh = ArtRoot + "Models/Base/Base.fbx";

                                                                                                                          
                                                                                                                                        
                                                                                                                                        
                         
                                                                                                                                          
                                                                                                                                       
                                                                                              
                                                                                                                                          
                                                                                                                                       
                                                                                                                                      
                                                                                                                                        
                                                                        
                                                                                                                                           
        static readonly Color DarkNodeTint = new(.85f, .40f, .70f);
        static readonly Color DarkNodeGlow = new(1.15f, .07f, 1.30f);

        static readonly string[] Icons = { "energy", "alloy", "dark", "control", "ship-bay", "armament", "energy-lab", "shield", "durability", "engine", "armor", "fuel", "lamp", "skill", "locked", "repair", "night-lock" };
                                                                                                
                                                                                                                                
        static readonly Dictionary<string, string> Pictures = new()
        {
            ["ship-wreck"] = "ship-wreck/ship-wreck.png",
            ["ship-intact"] = "ship-intact/ship-intact.png",
            ["cg-base"] = "base-exterior/base-exterior.png",
        };
                                                                                                                                      
                                                                                                                                       
        static readonly string[] Rooms = { "room-control", "room-armament", "room-energy", "room-armament-2", "room-armament-3", "room-energy-2", "room-energy-3" };
        static readonly int[] Shots = { 2, 3, 4, 5, 6 };
        static string ShotName(int shot, bool front) => $"cg01-s{shot}-{(front ? "fg" : "bg")}";
                                                              
        static readonly string[] Sounds = { "SFX-memory-cg-transition", "SFX-wormhole-turbulence", "SFX-ship-crash", "SFX-base-deploy" };
                                                                                                                             
        const string RoomOfflineLine = "S04";
                                                                                                                      
        const float AudioQuality = .5f;
        static readonly string[] Models = { "Base", "BaseDamaged", "ShipWreck", "ShipIntact", "MiningLamp", "OreAlloy", "DarkMatterNode", "ShadowA", "ShadowB", "ShadowC" };
        static readonly Dictionary<string, string> Reuses = new() { ["DarkMatterNode"] = "OreAlloy", ["BaseDamaged"] = "Base" };

                                                                                                                    
        [System.Serializable] class VoiceManifest { public VoiceLine[] lines; }
        [System.Serializable] class VoiceLine { public string id; public int default_take; public VoiceTake[] takes; public VoiceVariant[] variants; }
        [System.Serializable] class VoiceVariant { public string room; public int default_take; public VoiceTake[] takes; }
        [System.Serializable] class VoiceTake { public int take; public string file; }

        static readonly StringBuilder log = new();
        static readonly List<string> problems = new();
        static void Log(string line) { log.AppendLine(line); Debug.Log("[V2Campaign] " + line); }
                                                                                                                                   
        static void Problem(string line) { problems.Add(line); Log(line); }

                                                                                      
        public static IReadOnlyList<string> Problems => problems;

        static string Project => Path.GetDirectoryName(Application.dataPath).Replace('\\', '/') + "/";
        static string Work => Project + "work/";
        static string IconSource => Work + "v2-campaign-1106/icons/";
        static string PictureSource => Work + "art-plans/v2-1104/gen/";
        static string ModelSource => Work + "art-plans/v2-1104/models/campaign/";
        static string PreparedPictures => Work + "v2-campaign-1106/pictures-1106/";
        static string VoiceSource => Work + "art-plans/v2-1104/voice/";
        static string SoundSource => Work + "art-plans/v2-1104/sfx-cg01/";
        static bool HasSources => Directory.Exists(IconSource) || Directory.Exists(PictureSource) || Directory.Exists(ModelSource)
                                  || Directory.Exists(PreparedPictures) || Directory.Exists(VoiceSource) || Directory.Exists(SoundSource);

        [MenuItem("Anomaly Arena/V2 Campaign/Install")]
        public static void InstallMenu() => Install(true);

                                                                                                                                    
        [MenuItem("Anomaly Arena/V2 Campaign/Install (use files in place)")]
        public static void InstallInPlaceMenu() => Install(true, filesInPlace: true);

                     
                                                                                                                                           
                                                                                                                                         
                                                                                                                                 
                                                                                                
                      
        public static void InstallBatch()
        {
            int code = 1;
            try
            {
                Install(true, filesInPlace: HasArg("-filesInPlace"));
                code = problems.Count == 0 ? 0 : 1;
            }
            catch (System.Exception e) { Debug.LogError("[V2Campaign] INSTALL FAILED: " + e); }
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

                                                                                                                                         
                                                                                                                                          
                                                                                                                                              
        public static string Install(bool addToScene, bool includeModels = true, bool filesInPlace = false)
        {
            log.Clear();
            problems.Clear();
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Problem("the editor is in Play mode; leave it first (the installer writes assets and saves the scene)");
                return Finish(null);
            }
            bool copy = !filesInPlace && HasSources;
            Log(copy ? $"files: copied from work/ into {ArtRoot}" : $"files: used in place in {ArtRoot} ({(filesInPlace ? "asked for" : "no prepared sources in work/")})");
            foreach (var folder in copy ? new[] { "Icons", "Pictures", "Pictures/CG", "Models", "Voice", "Audio", "Prefabs", "Materials" } : new[] { "Prefabs", "Materials" })
                Directory.CreateDirectory(Project + ArtRoot + folder);
            Directory.CreateDirectory(Project + DataRoot);
            var models = includeModels ? Models : new string[0];

                                                                               
            if (copy)
            {
                foreach (var icon in Icons) CopyIn(IconSource + icon + ".png", ArtRoot + "Icons/" + icon + ".png");
                foreach (var pair in Pictures) CopyIn(PictureSource + pair.Value, ArtRoot + "Pictures/" + pair.Key + ".png");
                foreach (var model in models)
                {
                    string from = ModelSource + model + "/";
                    if (!Directory.Exists(from)) { Log($"{model}: no prepared folder ({from})"); continue; }
                    Directory.CreateDirectory(Project + ArtRoot + "Models/" + model);
                    foreach (var file in Directory.GetFiles(from))
                    {
                        string ext = Path.GetExtension(file).ToLowerInvariant(), name = Path.GetFileNameWithoutExtension(file);
                                                                                                                          
                        bool used = ext == ".fbx" || ext == ".png" && (name.EndsWith("_BaseColor") || name.EndsWith("_Normal"));
                        string target = ArtRoot + "Models/" + model + "/" + Path.GetFileName(file);
                        if (used) CopyIn(file.Replace('\\', '/'), target);
                        else if (File.Exists(Project + target)) AssetDatabase.DeleteAsset(target);
                    }
                }
                                                                                                                                          
                if (Directory.Exists(PreparedPictures))
                {
                    foreach (var room in Rooms) CopyIn(PreparedPictures + room + ".png", ArtRoot + "Pictures/" + room + ".png");
                    foreach (int shot in Shots)
                        foreach (bool front in new[] { false, true })
                            CopyIn(PreparedPictures + ShotName(shot, front) + ".png", ArtRoot + "Pictures/CG/" + ShotName(shot, front) + ".png");
                }
                else Log($"no prepared pictures ({PreparedPictures}); rooms and memory CG keep what {ArtRoot}Pictures has");
                CopyVoices();
                if (Directory.Exists(SoundSource)) foreach (var sound in Sounds) CopyIn(SoundSource + sound + ".mp3", ArtRoot + "Audio/" + sound + ".mp3");
                else Log($"no CG sounds ({SoundSource}); memory sounds keep what {ArtRoot}Audio has");
            }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                                                                                                                                    
                                                                        
            foreach (var icon in Icons) SetupSprite(ArtRoot + "Icons/" + icon + ".png");
            foreach (var key in Pictures.Keys) SetupPicture(ArtRoot + "Pictures/" + key + ".png");
            foreach (var room in Rooms) SetupLayer(ArtRoot + "Pictures/" + room + ".png", false);
            foreach (int shot in Shots)
            {
                SetupLayer(CgPath(shot, false), false);
                SetupLayer(CgPath(shot, true), true);
            }
            foreach (var path in AudioIn("Voice")) SetupAudio(path);
            foreach (var path in AudioIn("Audio")) SetupAudio(path);

                                        
            var prefabs = new Dictionary<string, GameObject>();
            Material baseDamaged = null;
            int materials = 0;
            foreach (var model in models)
            {
                string dir = ArtRoot + "Models/" + model + "/";
                if (!AssetDatabase.IsValidFolder(dir.TrimEnd('/'))) { Problem($"{model}: no folder {dir}; whitebox stand-in"); continue; }
                string fbx = FindFile(dir, ".fbx");
                var material = BuildMaterial(model, dir);
                if (material) materials++;
                if (model == "BaseDamaged") { baseDamaged = material; if (fbx == null) continue; }
                                                                                                                                     
                if (fbx == null && Reuses.TryGetValue(model, out var partner)) fbx = FindFile(ArtRoot + "Models/" + partner + "/", ".fbx");
                if (fbx == null) { Problem($"{model}: no FBX in {dir}"); continue; }
                SetupModel(fbx, string.Equals(fbx, BaseMesh, System.StringComparison.OrdinalIgnoreCase));
                var prefab = BuildPrefab(model, fbx, material);
                if (prefab) prefabs[model] = prefab;
            }

                              
            var rules = AssetDatabase.LoadAssetAtPath<CampaignRules>(RulesPath);
            if (!rules) { rules = ScriptableObject.CreateInstance<CampaignRules>(); AssetDatabase.CreateAsset(rules, RulesPath); Log($"created {RulesPath} with the ledger defaults"); }
            else Log($"kept {RulesPath} (its numbers are tuned in the Inspector)");
            var art = AssetDatabase.LoadAssetAtPath<CampaignArt>(ArtPath);
            if (!art) { art = ScriptableObject.CreateInstance<CampaignArt>(); AssetDatabase.CreateAsset(art, ArtPath); }
            if (includeModels)
            {
                art.basePrefab = Get(prefabs, "Base");
                art.baseDamagedMaterial = baseDamaged;
                art.shipWreckPrefab = Get(prefabs, "ShipWreck");
                art.shipIntactPrefab = Get(prefabs, "ShipIntact");
                art.lampPrefab = Get(prefabs, "MiningLamp");
                art.oreNodePrefab = Get(prefabs, "OreAlloy");
                art.darkNodePrefab = Get(prefabs, "DarkMatterNode");
                var shadows = new List<GameObject>();
                foreach (var s in new[] { "ShadowA", "ShadowB", "ShadowC" }) if (Get(prefabs, s)) shadows.Add(prefabs[s]);
                art.shadowPrefabs = shadows.ToArray();
            }
            art.shardMaterial = ShardMaterial();
            if (art.shardMaterial) materials++;
            else Problem($"no shard material ({UnlitShader} not found)");
            art.energy = IconAt("energy"); art.alloy = IconAt("alloy"); art.dark = IconAt("dark");
            art.control = IconAt("control"); art.shipBay = IconAt("ship-bay"); art.armament = IconAt("armament"); art.energyLab = IconAt("energy-lab");
            art.shield = IconAt("shield"); art.durability = IconAt("durability"); art.engine = IconAt("engine"); art.armor = IconAt("armor");
            art.fuel = IconAt("fuel"); art.lamp = IconAt("lamp"); art.skill = IconAt("skill"); art.locked = IconAt("locked"); art.repair = IconAt("repair");
            art.nightLock = IconAt("night-lock");
            art.roomControl = Picture("room-control"); art.roomArmament = Picture("room-armament"); art.roomEnergy = Picture("room-energy");
                                                                                
            art.roomArmamentLevels = new[] { Picture("room-armament-2"), Picture("room-armament-3") };
            art.roomEnergyLevels = new[] { Picture("room-energy-2"), Picture("room-energy-3") };
            art.shipWreckPicture = Picture("ship-wreck"); art.shipIntactPicture = Picture("ship-intact");
                                                                                                    
            art.memoryShots = System.Array.ConvertAll(Shots, shot => AssetDatabase.LoadAssetAtPath<Texture2D>(CgPath(shot, false)));
            art.memoryForegrounds = System.Array.ConvertAll(Shots, shot => AssetDatabase.LoadAssetAtPath<Texture2D>(CgPath(shot, true)));
            FillVoices(art);
            art.memorySounds = System.Array.ConvertAll(Sounds, sound => AssetDatabase.LoadAssetAtPath<AudioClip>(ArtRoot + "Audio/" + sound + ".mp3"));
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            int icons = CountSprites(art), pictures = 0;
            Log($"art: {prefabs.Count} prefabs, {icons} icons, base damaged material {(baseDamaged ? "yes" : "no")}");
            foreach (var icon in Icons) if (!IconAt(icon)) Problem($"icon {ArtRoot}Icons/{icon}.png missing");
            foreach (var key in Pictures.Keys) if (Picture(key)) pictures++; else Problem($"picture {ArtRoot}Pictures/{key}.png missing");
            foreach (var room in new[] { "room-control", "room-armament", "room-energy" }) if (Picture(room)) pictures++; else Problem($"picture {ArtRoot}Pictures/{room}.png missing");
                                                                                                                                             
            int levels = Count(art.roomArmamentLevels) + Count(art.roomEnergyLevels), layers = Count(art.memoryShots) + Count(art.memoryForegrounds);
            int voices = 0, roomVoices = Count(art.roomOfflineVoices), sounds = Count(art.memorySounds);
            foreach (var v in art.voices) if (v.clip) voices++;
            string media = $"{levels} room level pictures, {layers} memory CG layers, {voices} voices, {roomVoices} room voices, {sounds} memory sounds";
            Log("media: " + media);

                                      
            string where = addToScene ? AttachToScene(rules, art) : null;
            string scene = !addToScene ? "scene not touched" : where != null ? $"V2Campaign on {where}, scene saved" : "scene not done";
            return Finish($"files {(copy ? "copied from work/" : "used in place")}, {prefabs.Count} prefabs, {materials} materials, {icons} icons, {pictures} pictures, {media}, {scene}");
        }

        static string CgPath(int shot, bool front) => ArtRoot + "Pictures/CG/" + ShotName(shot, front) + ".png";

        static int Count<T>(T[] items) where T : Object
        {
            int n = 0;
            if (items != null) foreach (var item in items) if (item) n++;
            return n;
        }

                                                                                          
        static List<string> AudioIn(string folder)
        {
            var paths = new List<string>();
            if (!AssetDatabase.IsValidFolder(ArtRoot + folder)) return paths;
            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ArtRoot + folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".mp3", System.StringComparison.OrdinalIgnoreCase) && !paths.Contains(path)) paths.Add(path);
            }
            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }

                                                                                                                                            
                                                
        static void CopyVoices()
        {
            string manifestPath = VoiceSource + "manifest.json";
            if (!File.Exists(manifestPath)) { Log($"no voice manifest ({manifestPath}); voices keep what {ArtRoot}Voice has"); return; }
            var manifest = JsonUtility.FromJson<VoiceManifest>(File.ReadAllText(manifestPath));
            int copied = 0;
            foreach (var line in manifest?.lines ?? new VoiceLine[0])
            {
                if (line.variants != null && line.variants.Length > 0)
                {
                    foreach (var variant in line.variants)
                        if (CopyTake(line.id + " " + variant.room, variant.takes, variant.default_take, $"{line.id}-{variant.room}")) copied++;
                }
                else if (CopyTake(line.id, line.takes, line.default_take, line.id)) copied++;
            }
            Log($"voice: {copied} default takes from {manifestPath}");
        }

        static bool CopyTake(string what, VoiceTake[] takes, int chosen, string name)
        {
            foreach (var take in takes ?? new VoiceTake[0])
                if (take.take == chosen && !string.IsNullOrEmpty(take.file))
                {
                    CopyIn(VoiceSource + take.file, ArtRoot + "Voice/" + name + ".mp3");
                    return true;
                }
            Log($"voice {what}: no take {chosen} in the manifest");
            return false;
        }

                                                                                                                                             
        static void FillVoices(CampaignArt art)
        {
            var rooms = System.Enum.GetNames(typeof(CampaignRoom));
            var voices = new List<CampaignVoice>();
            foreach (var path in AudioIn("Voice"))
            {
                string line = Path.GetFileNameWithoutExtension(path);
                if (line.StartsWith(RoomOfflineLine + "-")) continue;
                voices.Add(new CampaignVoice { line = line, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path) });
            }
            art.voices = voices.ToArray();
            art.roomOfflineVoices = System.Array.ConvertAll(rooms, room => AssetDatabase.LoadAssetAtPath<AudioClip>($"{ArtRoot}Voice/{RoomOfflineLine}-{room}.mp3"));
        }

                                                                                                                                          
                                                                            
        static void SetupLayer(string path, bool alpha)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            var source = alpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            if (importer.textureType == TextureImporterType.Default && !importer.mipmapEnabled && importer.maxTextureSize == 2048
                && importer.textureCompression == TextureImporterCompression.Compressed && importer.crunchedCompression
                && importer.npotScale == TextureImporterNPOTScale.None && importer.wrapMode == TextureWrapMode.Clamp
                && importer.alphaSource == source && importer.alphaIsTransparency == alpha) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaSource = source;
            importer.alphaIsTransparency = alpha;
            importer.SaveAndReimport();
        }

                                                                                                                  
        static void SetupAudio(string path)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return;
            var settings = importer.defaultSampleSettings;
            if (importer.forceToMono && settings.loadType == AudioClipLoadType.CompressedInMemory && settings.compressionFormat == AudioCompressionFormat.Vorbis
                && Mathf.Approximately(settings.quality, AudioQuality) && settings.preloadAudioData) return;
            importer.forceToMono = true;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = AudioQuality;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

                                                                                      
        static string Finish(string summary)
        {
            if (problems.Count == 0) { Log("INSTALL OK: " + summary); return log.ToString(); }
            string line = $"INSTALL FAILED: {problems.Count} problem(s): {string.Join("; ", problems)}" + (summary != null ? $" | done: {summary}" : "");
            log.AppendLine(line);
            Debug.LogError("[V2Campaign] " + line);
            return log.ToString();
        }

        static bool HasArg(string name) => System.Array.Exists(System.Environment.GetCommandLineArgs(), a => string.Equals(a, name, System.StringComparison.OrdinalIgnoreCase));

        static GameObject Get(Dictionary<string, GameObject> map, string key) => map.TryGetValue(key, out var go) ? go : null;

        static int CountSprites(CampaignArt art)
        {
            int n = 0;
            foreach (var s in new[] { art.energy, art.alloy, art.dark, art.control, art.shipBay, art.armament, art.energyLab, art.shield, art.durability, art.engine, art.armor, art.fuel, art.lamp, art.skill, art.locked, art.repair, art.nightLock })
                if (s) n++;
            return n;
        }

        static void CopyIn(string from, string assetPath)
        {
            if (!File.Exists(from)) { Log($"missing {from}"); return; }
            string to = Project + assetPath;
            if (File.Exists(to) && new FileInfo(to).Length == new FileInfo(from).Length && File.GetLastWriteTimeUtc(to) >= File.GetLastWriteTimeUtc(from)) return;
            File.Copy(from, to, true);
        }

        static string FindFile(string dir, string ext)
        {
            foreach (var guid in AssetDatabase.FindAssets("", new[] { dir.TrimEnd('/') }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(ext, System.StringComparison.OrdinalIgnoreCase)) return path;
            }
            return null;
        }

        static void SetupSprite(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.textureType == TextureImporterType.Sprite && importer.maxTextureSize == 256 && !importer.mipmapEnabled) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }

                                                                                                                                                    
        static void SetupPicture(string path)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;
            if (importer.maxTextureSize == 1024 && !importer.mipmapEnabled && importer.textureCompression == TextureImporterCompression.Compressed && importer.npotScale == TextureImporterNPOTScale.None) return;
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }

                                                                                                                                           
        static void SetupModel(string path, bool readable)
        {
            if (AssetImporter.GetAtPath(path) is not ModelImporter importer) return;
            bool changed = importer.materialImportMode != ModelImporterMaterialImportMode.None || importer.importAnimation || importer.animationType != ModelImporterAnimationType.None
                           || importer.importCameras || importer.importLights || importer.addCollider || (readable && !importer.isReadable);
            if (!changed) return;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            if (readable) importer.isReadable = true;
            importer.SaveAndReimport();
        }

        static Texture2D MapAt(string dir, string suffix, bool normal)
        {
            string path = null;
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir.TrimEnd('/') }))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(p).EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase)) { path = p; break; }
            }
            if (path == null) return null;
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                                                                                                                                         
                if (importer.textureType != type || importer.sRGBTexture == normal || importer.maxTextureSize != 1024)
                {
                    importer.textureType = type;
                    importer.sRGBTexture = !normal;
                    importer.textureCompression = TextureImporterCompression.Compressed;
                    importer.maxTextureSize = 1024;
                    importer.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material BuildMaterial(string model, string dir)
        {
            var baseMap = MapAt(dir, "BaseColor", false);
            var normalMap = MapAt(dir, "Normal", true);
            if (!baseMap) { Problem($"{model}: no base colour map in {dir}"); return null; }
            string path = ArtRoot + "Materials/" + model + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                var shader = Shader.Find(LitShader);
                if (!shader) { Problem($"{model}: {LitShader} not found"); return null; }
                material = new Material(shader) { name = model };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", baseMap);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", baseMap);
            if (normalMap) { material.SetTexture("_BumpMap", normalMap); material.EnableKeyword("_NORMALMAP"); }
            bool darkMatter = model == "DarkMatterNode";
                                                                                                                                                   
            var tint = darkMatter ? DarkNodeTint : Color.white;
            material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            material.SetFloat("_Metallic", model.StartsWith("Shadow") ? 0f : .25f);
            material.SetFloat("_Smoothness", model.StartsWith("Shadow") ? .15f : .35f);
                                                                                                                     
            if (model == "MiningLamp" || darkMatter)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetTexture("_EmissionMap", baseMap);
                                                                                                                                      
                                                                        
                material.SetColor("_EmissionColor", darkMatter ? DarkNodeGlow.gamma : new Color(.6f, .9f, .9f) * .6f);
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject BuildPrefab(string model, string fbxPath, Material material)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (!source) { Problem($"{model}: {fbxPath} did not import"); return null; }
            var root = new GameObject(model);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.transform.SetParent(root.transform, false);
            int triangles = 0;
            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                if (material) { var mats = r.sharedMaterials; for (int i = 0; i < mats.Length; i++) mats[i] = material; r.sharedMaterials = mats; }
                if (r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh) triangles += mf.sharedMesh.triangles.Length / 3;
            }
            string path = ArtRoot + "Prefabs/" + model + ".prefab";
            var saved = PrefabUtility.SaveAsPrefabAsset(root, path, out bool ok);
            Object.DestroyImmediate(root);
            Log($"{model}: prefab {(ok ? "saved" : "FAILED")} {path}, {triangles} triangles");
            if (!ok) Problem($"{model}: prefab not saved");
            return ok ? saved : null;
        }

        static Material ShardMaterial()
        {
            string path = ArtRoot + "Materials/Shard.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(UnlitShader);
            if (!shader) return null;
            if (!material) { material = new Material(shader) { name = "Shard" }; AssetDatabase.CreateAsset(material, path); }
                                                                                  
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 2f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Sprite IconAt(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "Icons/" + name + ".png");
        static Texture2D Picture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(ArtRoot + "Pictures/" + name + ".png");

                                                                                                                                                     
        static string AttachToScene(CampaignRules rules, CampaignArt art)
        {
            string path = null;
            foreach (var candidate in Scenes) if (File.Exists(Project + candidate)) { path = candidate; break; }
            if (path == null) { Problem($"no arena scene: none of {string.Join(", ", new HashSet<string>(Scenes))} exists"); return null; }
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            if (!scene.isLoaded)
            {
                if (Application.isBatchMode) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                else { Problem($"{path} is not open; open it and run the installer again to add the component"); return null; }
            }
            GameManager game = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                game = root.GetComponentInChildren<GameManager>(true);
                if (game) break;
            }
            if (!game) { Problem($"no GameManager in {path}"); return null; }
            var campaign = game.GetComponent<V2Campaign>();
            bool added = !campaign;
            if (added) campaign = Undo.AddComponent<V2Campaign>(game.gameObject);
            campaign.rules = rules;
            campaign.art = art;
            EditorUtility.SetDirty(campaign);
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            Log($"{(added ? "added" : "updated")} V2Campaign on {game.name} in {path}; scene {(saved ? "saved" : "NOT saved")}");
            if (!saved) { Problem($"{path} was not saved"); return null; }
            return $"{game.name} in {path}";
        }
    }
}
