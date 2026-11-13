using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The interface's nine sounds (the fifth-pass title, work/art-plans/style-1112/ui-e/audio): the pointer arriving, a press, a window
    /// switching, a tab, a toggle, the volume slider's tick, starting a game, going back and a refused action. They are wav data kept as
    /// .bytes in Resources/V5/Title/Audio and made into clips at run time, as V5WeaponAudio does, because the web build re-encodes
    /// imported clips with 10 ms of silence at their start (pitfall T-024). Each play varies its pitch by up to 2.5 % either way, so a
    /// repeat never sounds the same; callers raise or lower it too (the menu steps down item by item, the slider's tick follows the
    /// volume). The levels are the mock-up's mix; the player's volume (ArenaVolume, the listener's own) scales them all, so the course's
    /// builds stay silent until it is turned up. V2Audio adds it, and on every page but the Alpha's plays the game's menu cues
    /// (ArenaUiSound) through it: hover, a choice as the tab sound, a confirmation as the press, going back.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5UiSound : MonoBehaviour
    {
        public enum Ui { Hover, Press, Panel, Tab, Toggle, Tick, Start, Back, Denied }

        const string Folder = "V5/Title/Audio/";
        static readonly string[] Files = { "ui-hover", "ui-press", "ui-panel", "ui-tab", "ui-toggle", "ui-tick", "ui-start", "ui-back", "ui-denied" };
        // Each sound's level against the others (the takes are trimmed to the same peak).
        static readonly float[] Mix = { .16f, .5f, .42f, .42f, .45f, .32f, .55f, .42f, .38f };
        // ±2.5 % pitch on every play; a quick sweep of the pointer over many plates stays one soft patter, not a burst.
        const float PitchSpread = .05f, HoverGap = .045f;
        const int VoiceCount = 6;
        static readonly AudioClip[] clips = new AudioClip[Files.Length];

        public static V5UiSound Instance { get; private set; }

        AudioSource[] voices;
        int next;
        float lastHover = -1f;
        // Its own random numbers: the game's draws stay as they are.
        readonly System.Random random = new();

        /// <summary>How many sounds have played (tests read it).</summary>
        public static int Played { get; private set; }

        /// <summary>The last sound played.</summary>
        public static Ui Last { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            Instance = null;
            Played = 0;
        }

        void Awake()
        {
            Instance = this;
            var holder = new GameObject("V5UiVoices");
            holder.transform.SetParent(transform, false);
            voices = new AudioSource[VoiceCount];
            for (int i = 0; i < voices.Length; i++)
            {
                var source = holder.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.priority = 40;
                voices[i] = source;
            }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Plays one of the sounds; false when there is no player or no clip.</summary>
        public static bool Play(Ui sound, float pitch = 1f, float gain = 1f) => Instance && Instance.PlayHere(sound, pitch, gain);

        /// <summary>The game's menu cues on these sounds: hover, a choice (the tab sound), a confirmation (the press), going back.</summary>
        public static bool PlayCue(ArenaUiSound cue) => Play(cue switch
        {
            ArenaUiSound.Hover => Ui.Hover,
            ArenaUiSound.Select => Ui.Tab,
            ArenaUiSound.Confirm => Ui.Press,
            _ => Ui.Back,
        });

        bool PlayHere(Ui sound, float pitch, float gain)
        {
            if (!isActiveAndEnabled || voices == null) return false;
            var clip = Clip(sound);
            if (!clip) return false;
            float now = Time.unscaledTime;
            if (sound == Ui.Hover)
            {
                if (now - lastHover < HoverGap) return true;
                lastHover = now;
            }
            var source = FreeVoice();
            source.clip = clip;
            source.pitch = Mathf.Clamp(pitch * (1f + ((float)random.NextDouble() - .5f) * PitchSpread), .25f, 3f);
            source.volume = Mathf.Clamp01(Mix[(int)sound] * gain);
            source.Play();
            Played++;
            Last = sound;
            return true;
        }

        AudioSource FreeVoice()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                int at = (next + i) % voices.Length;
                if (voices[at].isPlaying) continue;
                next = (at + 1) % voices.Length;
                return voices[at];
            }
            var oldest = voices[next];
            next = (next + 1) % voices.Length;
            return oldest;
        }

        static AudioClip Clip(Ui sound)
        {
            int i = (int)sound;
            if (clips[i]) return clips[i];
            return clips[i] = Build(Files[i]);
        }

        /// <summary>A clip made from a wav file under Resources/V5/Title/Audio (16-bit PCM, any rate, mono or stereo); null for anything else.</summary>
        public static AudioClip Build(string file)
        {
            var asset = Resources.Load<TextAsset>(Folder + file);
            if (!asset) return null;
            var bytes = asset.bytes;
            Resources.UnloadAsset(asset);
            if (bytes.Length < 44 || Tag(bytes, 0) != "RIFF" || Tag(bytes, 8) != "WAVE") return null;
            int channels = 0, rate = 0, bits = 0, dataAt = -1, dataSize = 0;
            for (int at = 12; at + 8 <= bytes.Length;)
            {
                string id = Tag(bytes, at);
                int size = BitConverter.ToInt32(bytes, at + 4);
                if (size < 0) break;
                if (id == "fmt " && size >= 16 && at + 24 <= bytes.Length)
                {
                    channels = BitConverter.ToInt16(bytes, at + 10);
                    rate = BitConverter.ToInt32(bytes, at + 12);
                    bits = BitConverter.ToInt16(bytes, at + 22);
                }
                else if (id == "data")
                {
                    dataAt = at + 8;
                    dataSize = Mathf.Min(size, bytes.Length - dataAt);
                    break;
                }
                at += 8 + size + (size & 1);
            }
            if (dataAt < 0 || bits != 16 || channels < 1 || rate <= 0) return null;
            int samples = dataSize / 2 / channels * channels;
            if (samples <= 0) return null;
            var data = new float[samples];
            for (int s = 0; s < samples; s++) data[s] = (short)(bytes[dataAt + s * 2] | bytes[dataAt + s * 2 + 1] << 8) / 32768f;
            var clip = AudioClip.Create(file, samples / channels, channels, rate, false);
            clip.SetData(data, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return clip;
        }

        static string Tag(byte[] bytes, int at) => System.Text.Encoding.ASCII.GetString(bytes, at, 4);
    }
}
