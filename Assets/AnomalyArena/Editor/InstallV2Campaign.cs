using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// Installs the V2 day campaign into the arena scene. Where work/ holds the prepared art (this edition only) it first copies it into
    /// Art/Campaign: icons sliced from the V2 icon sheets, the ship pictures, the decimated V2 props, the room backgrounds (levels 1 to 3)
    /// and memory CG layers from prepare_pictures_1106.py, each voice line's default take and the memory CG sounds. Without it (the
    /// English edition: sync_en.py brings Art/Campaign over with its .meta files, never work/) it uses the files
    /// already in Art/Campaign. Either way it sets their import settings, builds URP Lit materials and prefabs for the props, writes the
    /// CampaignRules and CampaignArt assets and puts a V2Campaign on the scene's GameManager. Every missing file is skipped (the campaign
    /// then draws its whitebox stand-in) and named again in the closing line. Safe to run again: it replaces what it made before and
    /// keeps the rules asset's numbers once it exists (they are tuned in the Inspector). The campaign's sound effects of 2026-11-10
    /// come from Audio/Campaign (InstallCampaignAudio); the voice lines recorded that day (E00, D01, K01, N01 to N03, B01) sit in
    /// Art/Campaign/Voice under their line ids like the others (copied there by work/v2-doc-1110/tools/copy_audio_picks.py, not
    /// listed in the voice manifest), so FillVoices takes them as it takes every file there.
    /// Menu: Anomaly Arena/V2 Campaign/Install, and Install (use files in place), which skips the copy here too.
    /// Batch: -executeMethod AnomalyArena.EditorTools.InstallV2Campaign.InstallBatch [-filesInPlace]. The log ends with one line,
    /// "[V2Campaign] INSTALL OK: ..." or "[V2Campaign] INSTALL FAILED: ...", and Unity exits with 0 or 1 (see InstallBatch).
    /// </summary>
    public static class InstallV2Campaign
    {
        const string ArtRoot = "Assets/AnomalyArena/Art/Campaign/";
        const string DataRoot = "Assets/AnomalyArena/Data/Campaign/";
        const string RulesPath = DataRoot + "V2CampaignRules.asset";
        const string ArtPath = DataRoot + "V2CampaignArt.asset";
        // The scene that gets the component (one scene serves both languages since 2026-11-10).
        static readonly string[] Scenes = { "Assets/AnomalyArena/Scenes/ArenaV2.unity" };
        const string LitShader = "Universal Render Pipeline/Lit";
        const string UnlitShader = "Universal Render Pipeline/Unlit";
        // Read/Write stays on for the base mesh alone (the damaged base is only a material on this same mesh, see Reuses): V2Campaign
        // splits it at run time into four submeshes, one per side, so a broken side alone takes the damaged material (D3-A).
        // A player build, the web one included, keeps a mesh's vertices for scripts
        // only when Read/Write is on; the editor always keeps them, so without this the split would fail in the build alone.
        const string BaseMesh = ArtRoot + "Models/Base/Base.fbx";

        // Dark matter node. It reuses the alloy ore's mesh (Reuses), so
        // its colour is what tells the two apart, and the V2 palette keeps purple for dark matter alone. Before, its colour map went in
        // untinted with a glow of (.8, .35, 1) x .9: the map's slate-blue body (sRGB about .11/.16/.21) read as a blue-grey rock beside
        // the brown ore.
        // DarkNodeTint (sRGB, multiplies the colour map): the body turns purple-black, about .09/.04/.13 (hue 274; the dark matter icon's
        //   body is .11/.07/.16), at a quarter of its old luminance and 7 % of the alloy ore's. Green is cut hardest because the map's
        //   body is green-blue; red stays high so the hue lands on violet rather than indigo.
        // DarkNodeGlow (linear HDR, multiplies the same colour map): the body glows a dim violet (hue about 269), so the node still reads
        //   purple in shadow and from afar, and the magenta cracks reach about .60/.02/.78 sRGB, twice their old glow. Red sits higher
        //   against blue than in the icon's violet (.43/.18/.64) to make up for the map's blue. The cracks' glow peaks near .6 linear
        //   (under .8 with full light on them), below the bloom threshold of the illustrated look (V5Look, 1.05; its soft knee passes a
        //   few per cent at most): the node glows without a bloom halo.
        // Worked out from DarkMatterNode_BaseColor.png with sRGB/linear arithmetic, before lights and post effects; the game view decides.
        static readonly Color DarkNodeTint = new(.85f, .40f, .70f);
        static readonly Color DarkNodeGlow = new(1.15f, .07f, 1.30f);

        static readonly string[] Icons = { "energy", "alloy", "dark", "control", "ship-bay", "armament", "energy-lab", "shield", "durability", "engine", "armor", "fuel", "lamp", "skill", "locked", "repair", "night-lock" };
        // Picture name in Art/Campaign/Pictures -> its source under work/art-plans/v2-1104/gen.
        // These keep the 1024 rule (SetupPicture). cg-base is no longer in the art asset since the memory CG has its own shots.
        static readonly Dictionary<string, string> Pictures = new()
        {
            ["ship-wreck"] = "ship-wreck/ship-wreck.png",
            ["ship-intact"] = "ship-intact/ship-intact.png",
            ["cg-base"] = "base-exterior/base-exterior.png",
        };
        // Room backgrounds, levels 1 to 3, and the memory CG layers come ready-sized from PreparedPictures (prepare_pictures_1106.py:
        // rooms 1080x720, CG 1280x720, both sides a multiple of 4 so DXT takes them as they are) and import at full size (SetupLayer).
        static readonly string[] Rooms = { "room-control", "room-armament", "room-energy", "room-armament-2", "room-armament-3", "room-energy-2", "room-energy-3" };
        static readonly int[] Shots = { 2, 3, 4, 5, 6 };
        static string ShotName(int shot, bool front) => $"cg01-s{shot}-{(front ? "fg" : "bg")}";
        // Memory CG sounds, in the order of art.memorySounds.
        static readonly string[] Sounds = { "SFX-memory-cg-transition", "SFX-wormhole-turbulence", "SFX-ship-crash", "SFX-base-deploy" };
        // The base system line voiced once per room (Voice/S04-<CampaignRoom>.mp3); every other line is Voice/<line id>.mp3.
        const string RoomOfflineLine = "S04";
        // Vorbis quality 0.5, mono, compressed in memory: the voice and CG sounds should add little to the web build.
        const float AudioQuality = .5f;
        static readonly string[] Models = { "Base", "BaseDamaged", "ShipWreck", "ShipIntact", "MiningLamp", "OreAlloy", "DarkMatterNode", "ShadowA", "ShadowB", "ShadowC" };
        static readonly Dictionary<string, string> Reuses = new() { ["DarkMatterNode"] = "OreAlloy", ["BaseDamaged"] = "Base" };

        // The parts of work/art-plans/v2-1104/voice/manifest.json the installer reads (JsonUtility skips the rest).
        [System.Serializable] class VoiceManifest { public VoiceLine[] lines; }
        [System.Serializable] class VoiceLine { public string id; public int default_take; public VoiceTake[] takes; public VoiceVariant[] variants; }
        [System.Serializable] class VoiceVariant { public string room; public int default_take; public VoiceTake[] takes; }
        [System.Serializable] class VoiceTake { public int take; public string file; }

        static readonly StringBuilder log = new();
        static readonly List<string> problems = new();
        static void Log(string line) { log.AppendLine(line); Debug.Log("[V2Campaign] " + line); }
        /// <summary>Something the install should have made and did not: logged now and listed again in the closing line.</summary>
        static void Problem(string line) { problems.Add(line); Log(line); }

        /// <summary>What the last run left out; empty when it was complete.</summary>
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

        /// <summary>The install the English edition runs (nothing copied from work/), to check that path in this project.</summary>
        [MenuItem("Anomaly Arena/V2 Campaign/Install (use files in place)")]
        public static void InstallInPlaceMenu() => Install(true, filesInPlace: true);

        /// <summary>
        /// Batch entry; -filesInPlace skips the copy even where work/ exists. It exits by itself: with -quit alone a returning method ends
        /// with 0 even after a failed install (logged errors do not change the code; only an exception escaping the method gives 1), and
        /// without -quit the batch editor would stay open. So: 0 when the closing line is INSTALL OK, 1 for INSTALL FAILED or an
        /// exception (caught to print the FAILED line). -quit on the command line does no harm.
        /// </summary>
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

        /// <summary>Returns the log. addToScene: put the V2Campaign on the GameManager of the arena scene (Scenes; opened in batch mode)
        /// and save it. includeModels false leaves the prop models out (while they are still being prepared); the art asset keeps what it
        /// had. filesInPlace: use what is in Art/Campaign without copying from work/, as happens anyway where work/ has no sources.</summary>
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

            // 1. Copy the prepared files into the project (this edition only).
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
                        // Only what the materials use: the colour and normal maps (metallic and roughness stay in work/).
                        bool used = ext == ".fbx" || ext == ".png" && (name.EndsWith("_BaseColor") || name.EndsWith("_Normal"));
                        string target = ArtRoot + "Models/" + model + "/" + Path.GetFileName(file);
                        if (used) CopyIn(file.Replace('\\', '/'), target);
                        else if (File.Exists(Project + target)) AssetDatabase.DeleteAsset(target);
                    }
                }
                // Rooms, memory CG, voice and CG sounds: a source that is not here is skipped with one line; what Art/Campaign has stays.
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

            // 2. Import settings, in place as well: a synced copy carries this edition's .meta files, so setting them again changes
            // nothing there, and it repairs any that did not come over.
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

            // 3. Materials and prefabs.
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
                // A retexture keeps its partner's mesh (same vertices and UVs, checked in Blender): only its textures were prepared.
                if (fbx == null && Reuses.TryGetValue(model, out var partner)) fbx = FindFile(ArtRoot + "Models/" + partner + "/", ".fbx");
                if (fbx == null) { Problem($"{model}: no FBX in {dir}"); continue; }
                SetupModel(fbx, string.Equals(fbx, BaseMesh, System.StringComparison.OrdinalIgnoreCase));
                var prefab = BuildPrefab(model, fbx, material);
                if (prefab) prefabs[model] = prefab;
            }

            // 4. Data assets.
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
            // Levels 2 and 3; an empty entry falls back to level 1 in the game.
            art.roomArmamentLevels = new[] { Picture("room-armament-2"), Picture("room-armament-3") };
            art.roomEnergyLevels = new[] { Picture("room-energy-2"), Picture("room-energy-3") };
            art.shipWreckPicture = Picture("ship-wreck"); art.shipIntactPicture = Picture("ship-intact");
            // CG01 shots 2 to 6, one background and one transparent foreground each, in shot order.
            art.memoryShots = System.Array.ConvertAll(Shots, shot => AssetDatabase.LoadAssetAtPath<Texture2D>(CgPath(shot, false)));
            art.memoryForegrounds = System.Array.ConvertAll(Shots, shot => AssetDatabase.LoadAssetAtPath<Texture2D>(CgPath(shot, true)));
            FillVoices(art);
            art.memorySounds = System.Array.ConvertAll(Sounds, sound => AssetDatabase.LoadAssetAtPath<AudioClip>(ArtRoot + "Audio/" + sound + ".mp3"));
            // The 2026-11-10 sound effects from Audio/Campaign (also on their own: Anomaly Arena/V2 Campaign/Install Sounds). Missing
            // files leave silent entries, counted in the closing line, not a problem.
            string effects = InstallCampaignAudio.Fill(art);
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            int icons = CountSprites(art), pictures = 0;
            Log($"art: {prefabs.Count} prefabs, {icons} icons, base damaged material {(baseDamaged ? "yes" : "no")}");
            foreach (var icon in Icons) if (!IconAt(icon)) Problem($"icon {ArtRoot}Icons/{icon}.png missing");
            foreach (var key in Pictures.Keys) if (Picture(key)) pictures++; else Problem($"picture {ArtRoot}Pictures/{key}.png missing");
            foreach (var room in new[] { "room-control", "room-armament", "room-energy" }) if (Picture(room)) pictures++; else Problem($"picture {ArtRoot}Pictures/{room}.png missing");
            // The newer media (room levels 2 and 3, memory CG, voice, CG sounds) may be missing: counted in the closing line, not a problem.
            int levels = Count(art.roomArmamentLevels) + Count(art.roomEnergyLevels), layers = Count(art.memoryShots) + Count(art.memoryForegrounds);
            int voices = 0, roomVoices = Count(art.roomOfflineVoices), sounds = Count(art.memorySounds);
            foreach (var v in art.voices) if (v.clip) voices++;
            string media = $"{levels} room level pictures, {layers} memory CG layers, {voices} voices, {roomVoices} room voices, {sounds} memory sounds, {effects}";
            Log("media: " + media);

            // 5. The scene component.
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

        /// <summary>The .mp3 assets in one Art/Campaign folder, sorted by path.</summary>
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

        /// <summary>Copies each line's default take from the voice manifest: Voice/&lt;line&gt;.mp3, and Voice/S04-&lt;room&gt;.mp3 for the
        /// line voiced once per room.</summary>
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

        /// <summary>art.voices from Art/Campaign/Voice (line id = file name, sorted), and the per-room line in CampaignRoom order.</summary>
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

        /// <summary>Rooms and memory CG layers: kept at their prepared size (2048 cap), crunched, no mipmaps, clamped. Without alpha they
        /// compress to DXT1, the transparent foregrounds to DXT5.</summary>
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

        /// <summary>Voice and CG sounds: mono, Vorbis at AudioQuality, compressed in memory, preloaded.</summary>
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

        /// <summary>The closing line the batch run and its caller look for.</summary>
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

        /// <summary>Room and CG pictures: at most 1024 px, compressed, no mipmaps (the asset plan's rule for CG layers in the web build).</summary>
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

        /// <param name="readable">turn Read/Write on (the base mesh, see BaseMesh); false leaves the model's own setting as it is.</param>
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
                // 1024 in the game: two 2048 maps per big model would add about 40 MB to the web build (the sources in work/ stay 2048).
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
            // The colour map as it is, except on the dark matter node (DarkNodeTint); set every run so the result does not depend on the last one.
            var tint = darkMatter ? DarkNodeTint : Color.white;
            material.SetColor("_BaseColor", tint);
            if (material.HasProperty("_Color")) material.SetColor("_Color", tint);
            material.SetFloat("_Metallic", model.StartsWith("Shadow") ? 0f : .25f);
            material.SetFloat("_Smoothness", model.StartsWith("Shadow") ? .15f : .35f);
            // Glow where the design glows: the lamp's glass and the dark matter's cracks, from their own colour map.
            if (model == "MiningLamp" || darkMatter)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                material.SetTexture("_EmissionMap", baseMap);
                // SetColor takes sRGB values and this linear-space project converts them on the way to the shader, so the dark node's
                // glow, chosen in linear terms, goes in through .gamma.
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
            // Additive: the icons are drawn on near-black, which then disappears.
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

        /// <summary>Puts the V2Campaign on the first scene in Scenes that exists; returns "GameManager in scene", or null after a problem.</summary>
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
