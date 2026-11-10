using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AnomalyArena.EditorTools
{
    /// <summary>
    /// Fills CampaignArt.sounds (the V2 campaign's sound effects, CampaignSfx) from Audio/Campaign by file name: CampaignAudio.FileName
    /// with .wav, .ogg or .mp3 (SFX-night-fall.wav ...). Another take is the file replaced under the same name and this run again
    /// (work/v2-doc-1110/tools/copy_audio_picks.py copies the chosen takes there). A sound with no file keeps an entry without a clip and
    /// stays silent. A gain changed in the Inspector is kept; a new entry gets CampaignAudio.DefaultGain.
    /// Import settings, for the web build's size: the one-shots mono, the six loops stereo, all Vorbis at quality AudioQuality,
    /// compressed in memory and preloaded (the web build turns them into AAC either way; never PCM).
    /// Menu: Anomaly Arena/V2 Campaign/Install Sounds. Install (InstallV2Campaign) runs it too.
    /// </summary>
    public static class InstallCampaignAudio
    {
        public const string Folder = "Assets/AnomalyArena/Audio/Campaign";
        const string ArtPath = "Assets/AnomalyArena/Data/Campaign/V2CampaignArt.asset";
        static readonly string[] Extensions = { ".wav", ".ogg", ".mp3" };
        const float AudioQuality = .5f;

        [MenuItem("Anomaly Arena/V2 Campaign/Install Sounds")]
        public static void InstallMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[CampaignAudio] INSTALL FAILED: leave Play mode first"); return; }
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var art = AssetDatabase.LoadAssetAtPath<CampaignArt>(ArtPath);
            if (!art) { Debug.LogError($"[CampaignAudio] INSTALL FAILED: no {ArtPath}; run Anomaly Arena/V2 Campaign/Install first"); return; }
            string summary = Fill(art);
            AssetDatabase.SaveAssets();
            Debug.Log("[CampaignAudio] INSTALL OK: " + summary);
        }

        /// <summary>Sets the import settings of the files found and writes art.sounds (one entry per CampaignSfx, in its order); the
        /// caller saves the assets. Returns one line: how many sounds have a clip, and which have none.</summary>
        public static string Fill(CampaignArt art)
        {
            var kept = new Dictionary<CampaignSfx, float>();
            foreach (var old in art.sounds ?? new CampaignSound[0])
                if (old.gain > 0f) kept[old.sound] = old.gain;
            var sounds = new List<CampaignSound>();
            var missing = new List<string>();
            foreach (CampaignSfx sfx in System.Enum.GetValues(typeof(CampaignSfx)))
            {
                string name = CampaignAudio.FileName(sfx), path = Find(name);
                AudioClip clip = null;
                if (path != null)
                {
                    Setup(path, CampaignAudio.Loops(sfx));
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                }
                if (!clip) missing.Add(name);
                sounds.Add(new CampaignSound { sound = sfx, clip = clip, gain = kept.TryGetValue(sfx, out float gain) ? gain : CampaignAudio.DefaultGain(sfx) });
            }
            art.sounds = sounds.ToArray();
            EditorUtility.SetDirty(art);
            int found = sounds.Count - missing.Count;
            return $"{found} of {sounds.Count} campaign sounds from {Folder}" + (missing.Count > 0 ? $"; silent, no file: {string.Join(", ", missing)}" : "");
        }

        static string Find(string name)
        {
            foreach (var ext in Extensions)
            {
                string path = $"{Folder}/{name}{ext}";
                if (AssetImporter.GetAtPath(path) is AudioImporter) return path;
            }
            return null;
        }

        /// <summary>One-shots mono, loops stereo; Vorbis at AudioQuality, compressed in memory, preloaded. Reimports only on a change.</summary>
        static void Setup(string path, bool loop)
        {
            if (AssetImporter.GetAtPath(path) is not AudioImporter importer) return;
            var settings = importer.defaultSampleSettings;
            bool mono = !loop;
            if (importer.forceToMono == mono && settings.loadType == AudioClipLoadType.CompressedInMemory && settings.compressionFormat == AudioCompressionFormat.Vorbis
                && Mathf.Approximately(settings.quality, AudioQuality) && settings.preloadAudioData && !importer.loadInBackground) return;
            importer.forceToMono = mono;
            importer.loadInBackground = false;
            settings.loadType = AudioClipLoadType.CompressedInMemory;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = AudioQuality;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
    }
}
