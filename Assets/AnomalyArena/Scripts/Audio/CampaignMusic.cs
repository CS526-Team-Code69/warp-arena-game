using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace AnomalyArena
{

    public sealed class CampaignMusic : MonoBehaviour
    {
        public enum Track { None, Title, Day, Dusk, Night, Boss, Ending }
        static readonly string[] Files = { null, "title", "day", "dusk", "night", "boss", "ending" };

        [Serializable] public struct Trim
        {
            public string file;
            [Min(0f)] public float head, tail;
        }

        public static CampaignMusic Instance { get; private set; }

        public static bool Overrides => Instance && Instance.current != Track.None;

        public Track Current => current;

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
            source.loop = false;                                                       
            source.volume = 0f;
            return source;
        }

        IEnumerator Load(Track track)
        {
            string path = Path.Combine(Application.streamingAssetsPath, folder, Files[(int)track] + ".mp3");

            string uri = path.Contains("://") ? path : "file:///" + path.Replace('\\', '/').TrimStart('/');
            using (var request = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.MPEG))
            {
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) yield break;                                                        
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (!clip) yield break;
                clip.name = Files[(int)track];
                clips[(int)track] = clip;
            }
        }

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

        Track Wanted()
        {
            var game = GameManager.Instance;
            if (!game) return Track.None;
            var phase = game.RunPhase;
            if (phase == ArenaUiState.Title) { campaignOver = false; nightTime = false; duskTime = false; return Track.Title; }
            var campaign = V2Campaign.Active;
            if (campaign && campaign.Running && game.CampaignRunning)
            {

                if (WorldMap.Active && WorldMap.Active.TrialActive) return Track.Boss;
                switch (campaign.Stage)
                {
                    case CampaignStage.Off: return Track.None;
                    case CampaignStage.Story: return current;                                       
                    case CampaignStage.Dusk: return Track.Dusk;
                    case CampaignStage.Night: return Track.Night;
                    case CampaignStage.FinalBoss:
                    case CampaignStage.Core: return Track.Boss;
                    case CampaignStage.Ending: return Track.Ending;
                    case CampaignStage.Base: return nightTime ? Track.Night : duskTime ? Track.Dusk : Track.Day;
                    default: return Track.Day;                                                                 
                }
            }
            if (campaignOver && (phase == ArenaUiState.Result || current == Track.Ending)) return Track.Ending;
            return Track.None;                                              
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
            if (wanted != Track.None && !ClipFor(wanted)) wanted = Track.None;                                                
            if (wanted != current)
            {

                if (current != Track.None && wanted != Track.None && ClipFor(wanted) == ClipFor(current) && Loops(wanted) == Loops(current))
                    current = wanted;
                else Switch(wanted, fadeSeconds);
            }
            else if (current != Track.None && Loops(current) && playing.isPlaying)
            {
                position += Time.unscaledDeltaTime;
                if (position >= loopEnd - loopFadeSeconds) Switch(current, loopFadeSeconds);                                           
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

        void Switch(Track next, float seconds)
        {
            bool restart = next == current;
            (playing, fading) = (fading, playing);
            fadeFrom = fading.isPlaying ? fading.volume : 0f;                                                                       
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
