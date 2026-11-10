using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AnomalyArena
{
    /// <summary>
    /// Music by stage for the title screen and the V2 day campaign (2026-11-06): each part of the run has its own track instead of the
    /// one combat track. title on the title screen; day for the day, its title cards, the loadout, the Day 0 tutorial, the lull and the
    /// build phase; dusk for the dusk; night for the night (and the base visited at night); boss for the Tyrant fight and its core, and
    /// (since 2026-11-06) for the void trial behind the black hole's hidden gate, by day or in the afternoon;
    /// ending, played once, for the ending and the result screen of a campaign run. A story scene keeps what is playing.
    /// The tracks are mp3 files in StreamingAssets/Music, loaded at run time, so on the web they are separate files and not part of the
    /// data file. Until dusk.mp3 and ending.mp3 exist, the dusk borrows night.mp3 and the ending title.mp3; once the files are there
    /// they are used at the next start, with no change here. A track with no file at all leaves V3WorldAudio's music as it was,
    /// without an error. A classic run keeps V3WorldAudio's combat music.
    /// Changes cross-fade over fadeSeconds of real time. A looping track plays from its file's head trim to its tail trim and
    /// cross-fades into its own start over loopFadeSeconds, so a file that fades in and out does not dip at the seam. The volume follows
    /// V3WorldAudio's musicGain and masterGain; the pause menu holds the music where it is.
    /// </summary>
    public sealed class CampaignMusic : MonoBehaviour
    {
        public enum Track { None, Title, Day, Dusk, Night, Boss, Ending }
        static readonly string[] Files = { null, "title", "day", "dusk", "night", "boss", "ending" };

        /// <summary>Seconds cut from the start and the end of a file when it loops (its own fade-in and fade-out).</summary>
        [Serializable] public struct Trim
        {
            public string file;
            [Min(0f)] public float head, tail;
        }

        public static CampaignMusic Instance { get; private set; }
        /// <summary>True while one of these tracks plays in place of V3WorldAudio's music, which stays silent meanwhile.</summary>
        public static bool Overrides => Instance && Instance.current != Track.None;
        /// <summary>The track playing now (None: V3WorldAudio's music has the channel).</summary>
        public Track Current => current;
        /// <summary>True once the track can play: its own file, or the one it borrows (dusk: night, ending: title), has been loaded.</summary>
        public bool Loaded(Track track) => ClipFor(track);

        [Min(.1f), Tooltip("Seconds of real time a change of track takes to cross-fade.")] public float fadeSeconds = 1.5f;
        [Min(.1f), Tooltip("Seconds of real time a looping track takes to cross-fade from its end into its own start.")] public float loopFadeSeconds = 2f;
        [Tooltip("Seconds cut from the start and the end of a file while it loops. night.mp3 fades in over about 3 s and out over about 2 s.")]
        public Trim[] trims = { new Trim { file = "night", head = 3f, tail = 2f } };
        [Tooltip("Folder under StreamingAssets that holds title.mp3, day.mp3, dusk.mp3, night.mp3, boss.mp3 and ending.mp3.")]
        public string folder = "Music";

        readonly AudioClip[] clips = new AudioClip[Files.Length];
        AudioSource playing, fading;
        Track current = Track.None;
        float blend = 1f, fadeFrom, fadeLength = 1.5f, position, loopEnd;
        bool paused, campaignOver, nightTime, duskTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Create()
        {
            if (Instance) return;
            var host = new GameObject("CampaignMusic");
            DontDestroyOnLoad(host);
            host.AddComponent<CampaignMusic>();
        }

        void Awake()
        {
            if (Instance && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            playing = NewSource();
            fading = NewSource();
            for (int i = 1; i < Files.Length; i++) StartCoroutine(Load((Track)i));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            CampaignEvents.DayStarted += OnDayStarted;
            CampaignEvents.PhaseChanged += OnPhaseChanged;
            CampaignEvents.Ended += OnEnded;
        }

        void OnDisable()
        {
            CampaignEvents.DayStarted -= OnDayStarted;
            CampaignEvents.PhaseChanged -= OnPhaseChanged;
            CampaignEvents.Ended -= OnEnded;
        }

        void OnDayStarted(int day, bool overtime) { nightTime = false; duskTime = false; }

        void OnPhaseChanged(string phase, int day)
        {
            duskTime = phase == "dusk";
            nightTime = phase == "night";
        }

        void OnEnded(string cause, int day) => campaignOver = true;

        AudioSource NewSource()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = false; // looping is done here, with a cross-fade at the seam
            source.volume = 0f;
            return source;
        }

        IEnumerator Load(Track track)
        {
            string path = Path.Combine(Application.streamingAssetsPath, folder, Files[(int)track] + ".mp3");
            // On the web the path is already a URL; in the Editor and a desktop build it is a file path.
            string uri = path.Contains("://") ? path : "file:///" + path.Replace('\\', '/').TrimStart('/');
            using (var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) yield break; // not there: the old music, or a borrowed track, stays
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (!clip) yield break;
                clip.name = Files[(int)track];
                clips[(int)track] = clip;
            }
        }

        /// <summary>The clip a track plays: its own file, else the borrowed one (dusk: night, ending: title), else none.</summary>
        AudioClip ClipFor(Track track)
        {
            if (track == Track.None) return null;
            var own = clips[(int)track];
            if (own) return own;
            if (track == Track.Dusk) return clips[(int)Track.Night];
            if (track == Track.Ending) return clips[(int)Track.Title];
            return null;
        }

        static bool Loops(Track track) => track != Track.Ending;

        Trim TrimOf(AudioClip clip)
        {
            foreach (var trim in trims ?? Array.Empty<Trim>())
                if (clip && trim.file == clip.name) return trim;
            return default;
        }

        /// <summary>The track this moment of the game asks for; None hands the music back to V3WorldAudio.</summary>
        Track Wanted()
        {
            var game = GameManager.Instance;
            if (!game) return Track.None;
            var phase = game.RunPhase;
            if (phase == ArenaUiState.Title) { campaignOver = false; nightTime = false; duskTime = false; return Track.Title; }
            var campaign = V2Campaign.Active;
            if (campaign && campaign.Running && game.CampaignRunning)
            {
                // The run is over: V2Campaign.Finish raises CampaignEvents.Ended and then turns the stage Off, while Running stays true
                // until the scene reloads. Its result screen plays the ending once, whether the run was won, the player died by day or the
                // base fell (2026-11-10: before this, Off handed the music back to the combat track, so a lost run's result screen was
                // silent). Checked before the void trial, which can still be on after a death in there.
                if (campaign.Stage == CampaignStage.Off) return campaignOver ? Track.Ending : Track.None;
                // The Nash Prophet's fight in the void trial is a boss fight whatever the time of day.
                if (WorldMap.Active && WorldMap.Active.TrialActive) return Track.Boss;
                switch (campaign.Stage)
                {
                    case CampaignStage.Story: return current; // a story scene keeps what is playing
                    case CampaignStage.Dusk: return Track.Dusk;
                    case CampaignStage.Night: return Track.Night;
                    case CampaignStage.FinalBoss:
                    case CampaignStage.Core: return Track.Boss;
                    case CampaignStage.Ending: return Track.Ending;
                    case CampaignStage.Base: return nightTime ? Track.Night : duskTime ? Track.Dusk : Track.Day;
                    default: return Track.Day; // the title cards, loadout, tutorial, day, lull and build phase
                }
            }
            if (campaignOver && (phase == ArenaUiState.Result || current == Track.Ending)) return Track.Ending;
            return Track.None; // a classic run: V3WorldAudio's combat music
        }

        void Update()
        {
            var game = GameManager.Instance;
            bool shouldPause = game && game.RunPhase == ArenaUiState.Paused;
            if (shouldPause != paused)
            {
                paused = shouldPause;
                if (paused) { playing.Pause(); fading.Pause(); }
                else { playing.UnPause(); fading.UnPause(); }
            }
            if (paused) return;

            var wanted = Wanted();
            if (wanted != Track.None && !ClipFor(wanted)) wanted = Track.None; // nothing loaded (yet): leave the old music be
            if (wanted != current)
            {
                // A track that borrows the clip already playing (dusk on night.mp3 into the night) goes on without a restart.
                if (current != Track.None && wanted != Track.None && ClipFor(wanted) == ClipFor(current) && Loops(wanted) == Loops(current))
                    current = wanted;
                else Switch(wanted, fadeSeconds);
            }
            else if (current != Track.None && Loops(current) && playing.isPlaying)
            {
                position += Time.unscaledDeltaTime;
                if (position >= loopEnd - loopFadeSeconds) Switch(current, loopFadeSeconds); // the seam: cross-fade into its own start
            }

            var audio = V3WorldAudio.Instance;
            float gain = audio ? audio.musicGain * audio.masterGain : .2f;
            if (audio && audio.musicSource && playing.outputAudioMixerGroup != audio.musicSource.outputAudioMixerGroup)
                playing.outputAudioMixerGroup = fading.outputAudioMixerGroup = audio.musicSource.outputAudioMixerGroup;
            blend = Mathf.MoveTowards(blend, 1f, Time.unscaledDeltaTime / Mathf.Max(.1f, fadeLength));
            playing.volume = current == Track.None ? 0f : gain * blend;
            fading.volume = fading.isPlaying ? fadeFrom * (1f - blend) : 0f;
            if (blend >= 1f && fading.isPlaying) fading.Stop();
        }

        /// <summary>The playing source becomes the fading one and the track starts on the other, after its head trim when it loops.</summary>
        void Switch(Track next, float seconds)
        {
            bool restart = next == current;
            (playing, fading) = (fading, playing);
            fadeFrom = fading.isPlaying ? fading.volume : 0f; // the old sound fades out from the level it had when the change began
            blend = 0f;
            fadeLength = seconds;
            current = next;
            playing.Stop();
            if (!restart) Debug.Log("[CampaignMusic] " + (next == Track.None ? "back to the combat music" : ClipFor(next).name + " for " + next));
            if (next == Track.None) return;
            var clip = ClipFor(next);
            var trim = Loops(next) ? TrimOf(clip) : default;
            float head = Mathf.Clamp(trim.head, 0f, clip.length * .5f);
            loopEnd = Mathf.Max(head + loopFadeSeconds + 1f, clip.length - Mathf.Clamp(trim.tail, 0f, clip.length * .5f));
            playing.clip = clip;
            playing.time = head;
            position = head;
            playing.volume = 0f;
            playing.Play();
        }
    }
}
