using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AnomalyArena
{
    /// <summary>
    /// The V2 campaign's sound effects made on 2026-11-10 (work/art-plans/v2-1104/audio-1110/清单.md): one value per file, named after it
    /// (CampaignAudio.FileName: NightFall is SFX-night-fall). CampaignArt.sounds stores them by number, so new values go at the end.
    /// </summary>
    public enum CampaignSfx
    {
        NightFall, DayBreak, DarknessLoop, MineHit, OrePickupE, CollectAlienAlloy, CollectDarkMatter, ResourceDeposit,
        CraftLight, LightPlace, LightOn, LightFlicker, LightDestroyed, ShadowApproachLoop, ShadowAttack, ShadowFleeLight,
        ZoneOverrunWarning, BaseHit, ShieldHit, ShieldBreak, ShieldRecharge, RoomBreak, RoomRepair, ShipEngineRepair,
        ShipArmorRepair, FuelFillLoop, FuelFull, AreaUpgrade, UpgradeDenied, CorePickup, CollectAlienEnergy, CollectStarMatter,
        ShadowAppear, TyrantArrive, BaseUiOpen, BaseUiClose, DayTitleIngame, TutorialCrashAmbience, NightNoCombat,
        NightAmbienceLoop, BaseDamagedLoop, AreaBuild, MemoryCgTransition, WormholeTurbulence, ShipCrash, BaseDeploy,
        IgnitionFail, IgnitionSuccess, WarpJump, CoreInsert, DayTitleStinger
    }

    /// <summary>
    /// Plays the V2 campaign's sound effects (CampaignSfx) from CampaignArt.sounds, filled by the editor menu Anomaly Arena/V2 Campaign/
    /// Install Sounds from Audio/Campaign by file name, so another take is a file swapped and the menu run again. V2Campaign puts it beside
    /// itself; it acts only while a campaign runs, so the classic run never hears it, and a sound without a clip stays silent.
    /// One-shots come two ways: the campaign's scripted moments call Play or PlayAt (the memory CG, the ignition, the ending, the base's
    /// stock, the base panel's refusals), and the rest are found here by comparing the campaign's public state with the frame before
    /// (Watch: a room breaks or is repaired or gains a level, the shield breaks or refills, a lamp is set, eaten or lost, the night's
    /// first shadows, a shadow's touch, currency mined or picked up, mining strikes, an overrun region entered, a click that cannot fire)
    /// and from CampaignEvents (nightfall, dawn, a new day, a hit on the base, a lamp crafted, fuel). The six loops follow the state
    /// (LoopLevel) and fade in and out. A sound placed in the world is quieter the farther it is from the player (Proximity); every level
    /// is its own gain times effectsGain or loopsGain times V3WorldAudio's master gain, and the pause menu holds every sound where it is.
    /// Each loop cross-fades from its end into its own start over loopSeam (two sources): the web build turns every imported clip into
    /// AAC, which pads both ends with silence, so a plain AudioSource loop would gap once a round there.
    /// </summary>
    [DefaultExecutionOrder(200)] // after V2Campaign (150): Watch sees the state this frame left
    public sealed class CampaignAudio : MonoBehaviour
    {
        public static CampaignAudio Instance { get; private set; }

        [Range(0f, 1f), Tooltip("Level of every one-shot sound, times its own gain in CampaignArt.sounds and V3WorldAudio's master gain.")]
        public float effectsGain = .85f;
        [Range(0f, 1f), Tooltip("Level of every looping sound, times its own gain in CampaignArt.sounds and V3WorldAudio's master gain.")]
        public float loopsGain = .7f;
        [Min(0f), Tooltip("A one-shot placed in the world plays at full level within this many cells of the player.")]
        public float fullWithin = 10f;
        [Min(1f), Tooltip("A one-shot placed in the world is silent this many cells from the player and farther.")]
        public float audibleRange = 32f;
        [Range(1, 16), Tooltip("One-shot sounds that can play at once.")]
        public int voiceCount = 8;
        [Min(0f), Tooltip("Seconds over which a one-shot fades out at its very end, so a take whose last moment is still loud does not click.")]
        public float tailFade = .04f;
        [Min(.05f), Tooltip("Seconds a loop takes to fade in.")] public float loopFadeIn = .6f;
        [Min(.05f), Tooltip("Seconds a loop takes to fade out.")] public float loopFadeOut = 1.2f;
        [Min(.02f), Tooltip("Seconds a loop cross-fades from its end into its own start (at most a quarter of the clip).")]
        public float loopSeam = .25f;
        public LoopRanges ranges = LoopRanges.Default;
        [Min(.1f), Tooltip("Seconds between two mining strikes while E is held on a node.")] public float mineStrikeSeconds = .4f;
        [Min(.1f), Tooltip("Seconds the fuel fill loop plays after each unit of fuel.")] public float fuelFillSeconds = 1.6f;
        [Tooltip("Pitch of the fuel fill loop at an empty tank (x) and a full one (y): it rises as the tank fills.")]
        public Vector2 fuelPitch = new(.85f, 1.25f);
        [Min(0f), Tooltip("Seconds after a lamp is set before its light comes on.")] public float lightOnDelay = .25f;
        [Min(0f), Tooltip("Seconds after nightfall before the night's shadows are heard appearing.")] public float shadowAppearDelay = 1.5f;
        [Min(0f), Tooltip("Seconds after dawn before the day's title sound, so it does not land on the daybreak.")] public float dayTitleDelay = 1.2f;

        /// <summary>Distances of the loops that depend on where the player stands.</summary>
        [Serializable]
        public struct LoopRanges
        {
            [Tooltip("Shadow approach loop: full level within this many cells of the nearest shadow.")] public float shadowNear;
            [Tooltip("Shadow approach loop: silent this many cells from the nearest shadow and farther.")] public float shadowFar;
            [Tooltip("Damaged base loop: full level within this many cells of the base's centre.")] public float baseNear;
            [Tooltip("Damaged base loop: silent this many cells from the base's centre and farther.")] public float baseFar;
            [Tooltip("Crash site ambience on Day 0: full level within this many cells of the crash site.")] public float crashNear;
            [Tooltip("Crash site ambience on Day 0: silent this many cells from the crash site and farther.")] public float crashFar;
            [Range(0f, 1f), Tooltip("Night ambience in the light with no shadow in the player's region; it rises to full as the region fills up to overrun.")]
            public float calmNight;

            public static LoopRanges Default => new()
            {
                shadowNear = 3f, shadowFar = 18f, baseNear = 8f, baseFar = 30f, crashNear = 10f, crashFar = 45f, calmNight = .35f
            };
        }

        /// <summary>What the loops follow, read from the campaign each frame; LoopLevel turns it into levels without a scene.</summary>
        public struct LoopInputs
        {
            public CampaignStage stage;
            public int day;
            /// <summary>At night the player stands outside every lamp's light and the base's.</summary>
            public bool inDarkness;
            /// <summary>Cells from the player to the nearest shadow (infinity: no shadow).</summary>
            public float nearestShadow;
            /// <summary>Shadows in the player's region, and how many make a region overrun (CampaignRules.overrunShadows).</summary>
            public int shadowsHere, overrunAt;
            /// <summary>A room of the base is broken.</summary>
            public bool roomBroken;
            /// <summary>Cells from the player to the base's centre.</summary>
            public float toBase;
            /// <summary>Cells from the player to the Day 0 crash site.</summary>
            public float toCrash;
            /// <summary>A unit of fuel was synthesised moments ago.</summary>
            public bool fueling;

            /// <summary>Every distance infinite: no loop that depends on a distance is heard.</summary>
            public static LoopInputs Far => new() { nearestShadow = float.PositiveInfinity, toBase = float.PositiveInfinity, toCrash = float.PositiveInfinity, overrunAt = 1 };
        }

        // ───── Rules (static, for the tests) ─────

        static readonly int Count = Enum.GetValues(typeof(CampaignSfx)).Length;
        static readonly CampaignSfx[] LoopSounds =
        {
            CampaignSfx.DarknessLoop, CampaignSfx.ShadowApproachLoop, CampaignSfx.FuelFillLoop, CampaignSfx.NightAmbienceLoop,
            CampaignSfx.BaseDamagedLoop, CampaignSfx.TutorialCrashAmbience
        };

        /// <summary>The file a sound is read from, without its extension: "SFX-" and the value's words in lower case joined by "-"
        /// (OrePickupE is SFX-ore-pickup-e).</summary>
        public static string FileName(CampaignSfx sfx)
        {
            var text = new System.Text.StringBuilder("SFX");
            foreach (char c in sfx.ToString())
            {
                if (char.IsUpper(c)) text.Append('-').Append(char.ToLowerInvariant(c));
                else text.Append(c);
            }
            return text.ToString();
        }

        /// <summary>The six sounds that loop while a state lasts; every other one plays once.</summary>
        public static bool Loops(CampaignSfx sfx) => Array.IndexOf(LoopSounds, sfx) >= 0;

        /// <summary>The gain the installer gives a sound the first time (it keeps a gain changed in the Inspector afterwards). Chosen from
        /// the peaks in the takes' checks (checks.json), not by ear: the quiet takes at full, the frequent loud ones lower.</summary>
        public static float DefaultGain(CampaignSfx sfx) => sfx switch
        {
            CampaignSfx.ShieldRecharge or CampaignSfx.DayBreak or CampaignSfx.BaseUiOpen or CampaignSfx.ShadowFleeLight or CampaignSfx.DarknessLoop => 1f,
            CampaignSfx.MemoryCgTransition or CampaignSfx.WormholeTurbulence or CampaignSfx.ShipCrash or CampaignSfx.BaseDeploy or CampaignSfx.IgnitionFail
                or CampaignSfx.IgnitionSuccess or CampaignSfx.WarpJump or CampaignSfx.CoreInsert or CampaignSfx.TyrantArrive or CampaignSfx.DayTitleStinger => .9f,
            CampaignSfx.MineHit or CampaignSfx.ShadowApproachLoop or CampaignSfx.BaseDamagedLoop => .5f,
            CampaignSfx.NightAmbienceLoop or CampaignSfx.TutorialCrashAmbience or CampaignSfx.FuelFillLoop => .6f,
            _ => .7f
        };

        /// <summary>Seconds before the same sound may start again.</summary>
        static float MinInterval(CampaignSfx sfx) => sfx switch
        {
            CampaignSfx.BaseHit or CampaignSfx.ShieldHit or CampaignSfx.ShadowAttack => .3f,
            CampaignSfx.LightFlicker => 1.1f,
            CampaignSfx.NightNoCombat or CampaignSfx.UpgradeDenied => .5f,
            CampaignSfx.ShieldRecharge => 1.5f,
            CampaignSfx.ZoneOverrunWarning => 4f,
            _ => .08f
        };

        static int MaxAtOnce(CampaignSfx sfx) => sfx is CampaignSfx.MineHit or CampaignSfx.BaseHit or CampaignSfx.ShieldHit or CampaignSfx.CollectAlienEnergy ? 2 : 1;

        /// <summary>1 within near cells, 0 from far on, on a square curve between, so a sound swells as its source comes near.</summary>
        public static float Proximity(float distance, float near, float far)
        {
            if (float.IsNaN(distance) || float.IsInfinity(distance)) return 0f;
            if (far <= near) return distance <= near ? 1f : 0f;
            float t = Mathf.Clamp01((far - distance) / (far - near));
            return t * t;
        }

        /// <summary>The level (0 to 1) a loop wants now, before its fade, its gain and the master gain.
        /// Darkness: at night outside every light. Night ambience: at night in the light, from ranges.calmNight with no shadow in the
        /// player's region up to full at overrun (the danger is heard). Shadow approach: at night, by the nearest shadow's distance.
        /// Fuel fill: for a moment after each unit of fuel. Damaged base: while a room is broken, by the distance to the base (not in a
        /// story scene, the title card or the ending). Crash site: on Day 0 from the title card to the end of the walk, by the distance
        /// to the crash site.</summary>
        public static float LoopLevel(CampaignSfx loop, in LoopInputs s, in LoopRanges r)
        {
            bool night = s.stage == CampaignStage.Night;
            switch (loop)
            {
                case CampaignSfx.DarknessLoop:
                    return night && s.inDarkness ? 1f : 0f;
                case CampaignSfx.NightAmbienceLoop:
                    return night && !s.inDarkness ? Mathf.Lerp(r.calmNight, 1f, Mathf.Clamp01(s.shadowsHere / (float)Mathf.Max(1, s.overrunAt))) : 0f;
                case CampaignSfx.ShadowApproachLoop:
                    return night ? Proximity(s.nearestShadow, r.shadowNear, r.shadowFar) : 0f;
                case CampaignSfx.FuelFillLoop:
                    return s.fueling ? 1f : 0f;
                case CampaignSfx.BaseDamagedLoop:
                    bool shown = s.stage is not (CampaignStage.Off or CampaignStage.TitleCard or CampaignStage.Loadout or CampaignStage.Story or CampaignStage.Ending);
                    return s.roomBroken && shown ? Proximity(s.toBase, r.baseNear, r.baseFar) : 0f;
                case CampaignSfx.TutorialCrashAmbience:
                    bool walk = s.stage is CampaignStage.TitleCard or CampaignStage.Loadout or CampaignStage.Tutorial;
                    return s.day == 0 && walk ? Proximity(s.toCrash, r.crashNear, r.crashFar) : 0f;
                default:
                    return 0f;
            }
        }

        /// <summary>Equal-power cross-fade at a loop's seam: the new round's weight t of the way through (its square plus the old
        /// round's square is always 1, so the level holds).</summary>
        public static float SeamIn(float t) => Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI * .5f);
        public static float SeamOut(float t) => Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI * .5f);

        // ───── Calls from the campaign ─────

        /// <summary>Plays a one-shot without a place. True when the sound has a clip (also when it was held back as a repeat): a caller
        /// that keeps an older stand-in plays it only on false.</summary>
        public static bool Play(CampaignSfx sfx) => Instance && Instance.Emit(sfx, null);

        /// <summary>Plays a one-shot at a point of the world: quieter the farther it is from the player (fullWithin, audibleRange).</summary>
        public static bool PlayAt(CampaignSfx sfx, Vector3 at) => Instance && Instance.Emit(sfx, at);

        /// <summary>The sound has a clip in this build.</summary>
        public static bool Has(CampaignSfx sfx) => Instance && Instance.EntryOf(sfx).clip;

        // ───── State ─────

        struct Entry { public AudioClip clip; public float gain; }

        sealed class LoopChannel
        {
            public CampaignSfx sfx;
            public AudioSource heard, fading;
            public bool on;
            public float level, played, seam = -1f;
        }

        V2Campaign campaign;
        GameManager gm;
        GameObject host;
        Entry[] entries;
        CampaignArt entriesFrom;
        AudioSource[] pool;
        float[] poolLeft, poolVolume;
        CampaignSfx[] poolSfx;
        readonly float[] nextAllowed = new float[Count];
        LoopChannel[] loops;
        readonly List<(float at, CampaignSfx sfx, Vector3? where)> later = new();
        bool paused;
        float fuelUntil, mineClock;

        // What the campaign showed last frame: each change between two frames is one sound.
        bool seenValid, seenEngine, seenArmor, seenFull, seenOverrun;
        CampaignStage seenStage;
        readonly bool[] seenBroken = new bool[4];
        readonly int[] seenLevel = new int[4];
        int seenDurability, seenLamps, seenShadows, seenAlloy, seenEnergy, seenDark;
        float seenShield, seenHp, seenLampHp;
        Vector3 seenWeakestLamp;

        void Awake()
        {
            Instance = this;
            campaign = GetComponent<V2Campaign>();
            gm = GetComponent<GameManager>();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void OnEnable()
        {
            CampaignEvents.PhaseChanged += OnPhase;
            CampaignEvents.DayStarted += OnDayStarted;
            CampaignEvents.BaseDamaged += OnBaseDamaged;
            CampaignEvents.Purchased += OnPurchased;
        }

        void OnDisable()
        {
            CampaignEvents.PhaseChanged -= OnPhase;
            CampaignEvents.DayStarted -= OnDayStarted;
            CampaignEvents.BaseDamaged -= OnBaseDamaged;
            CampaignEvents.Purchased -= OnPurchased;
            StopAll();
        }

        bool Live => campaign && campaign.Running && campaign == V2Campaign.Active;
        static float Master => V3WorldAudio.Instance ? V3WorldAudio.Instance.masterGain : 1f;

        void Update()
        {
            if (!gm) gm = GameManager.Instance;
            bool shouldPause = gm && gm.RunPhase == ArenaUiState.Paused;
            if (shouldPause != paused) SetPaused(shouldPause);
            if (paused) return;
            float dt = Time.unscaledDeltaTime;
            TickVoices(dt);
            bool live = Live && gm;
            if (live)
            {
                Watch(dt);
                RunLater();
            }
            else
            {
                seenValid = false;
                later.Clear();
                fuelUntil = 0f;
            }
            TickLoops(dt, live ? ReadLoopInputs() : LoopInputs.Far, live);
        }

        // ───── One-shots ─────

        Entry EntryOf(CampaignSfx sfx)
        {
            var art = campaign ? campaign.art : null;
            if (entries == null || entriesFrom != art) BuildEntries(art);
            int i = (int)sfx;
            return i >= 0 && i < entries.Length ? entries[i] : default;
        }

        void BuildEntries(CampaignArt art)
        {
            entriesFrom = art;
            entries = new Entry[Count];
            if (!art || art.sounds == null) return;
            foreach (var sound in art.sounds)
            {
                int i = (int)sound.sound;
                if (sound.clip && i >= 0 && i < entries.Length) entries[i] = new Entry { clip = sound.clip, gain = sound.gain };
            }
        }

        bool Emit(CampaignSfx sfx, Vector3? at)
        {
            if (!Live) return false;
            var entry = EntryOf(sfx);
            if (!entry.clip) return false;
            if (Loops(sfx) || paused) return true;
            int id = (int)sfx;
            float now = Time.unscaledTime;
            if (now < nextAllowed[id]) return true;
            float level = entry.gain * effectsGain * Master;
            var player = gm ? gm.player : null;
            if (at.HasValue && player) level *= Proximity(Query.Flat(player.Position - at.Value).magnitude, fullWithin, audibleRange);
            if (level <= .001f) return true;
            int voice = PickVoice(sfx);
            if (voice < 0) return true;
            nextAllowed[id] = now + MinInterval(sfx);
            var source = pool[voice];
            source.Stop();
            source.clip = entry.clip;
            source.pitch = 1f;
            source.volume = poolVolume[voice] = Mathf.Clamp01(level);
            source.Play();
            poolLeft[voice] = entry.clip.length;
            poolSfx[voice] = sfx;
            return true;
        }

        /// <summary>A free voice; with none free, the one nearest its end. -1 when this sound already plays as often as it may.</summary>
        int PickVoice(CampaignSfx sfx)
        {
            if (pool == null)
            {
                int n = Mathf.Clamp(voiceCount, 1, 16);
                pool = new AudioSource[n]; poolLeft = new float[n]; poolVolume = new float[n]; poolSfx = new CampaignSfx[n];
                for (int i = 0; i < n; i++) pool[i] = NewSource(false);
            }
            int free = -1, nearestEnd = -1, same = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                if (poolLeft[i] <= 0f) { if (free < 0) free = i; continue; }
                if (poolSfx[i] == sfx) same++;
                if (nearestEnd < 0 || poolLeft[i] < poolLeft[nearestEnd]) nearestEnd = i;
            }
            if (same >= MaxAtOnce(sfx)) return -1;
            return free >= 0 ? free : nearestEnd;
        }

        void TickVoices(float dt)
        {
            if (pool == null) return;
            for (int i = 0; i < pool.Length; i++)
            {
                if (poolLeft[i] <= 0f || !pool[i]) continue;
                poolLeft[i] -= dt * Mathf.Abs(pool[i].pitch);
                if (poolLeft[i] <= 0f) { poolLeft[i] = 0f; pool[i].Stop(); continue; }
                if (tailFade > 0f && poolLeft[i] < tailFade) pool[i].volume = poolVolume[i] * poolLeft[i] / tailFade;
            }
        }

        void Later(CampaignSfx sfx, float seconds, Vector3? where) => later.Add((Time.unscaledTime + seconds, sfx, where));

        void RunLater()
        {
            float now = Time.unscaledTime;
            for (int i = later.Count - 1; i >= 0; i--)
            {
                if (later[i].at > now) continue;
                var (_, sfx, where) = later[i];
                later.RemoveAt(i);
                Emit(sfx, where);
            }
        }

        AudioSource NewSource(bool loop)
        {
            if (!host)
            {
                host = new GameObject("CampaignSounds");
                host.transform.SetParent(transform, false);
            }
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false; // the loops cross-fade at their seam here (TickLoops)
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            source.volume = 0f;
            // Through the same mixer group as the world's one-shots and its ambience, when the scene routes those through one.
            var world = V3WorldAudio.Instance;
            if (world)
            {
                var like = loop ? world.ambienceSource : world.worldVoices != null && world.worldVoices.Length > 0 ? world.worldVoices[0] : null;
                if (like) source.outputAudioMixerGroup = like.outputAudioMixerGroup;
            }
            return source;
        }

        void SetPaused(bool on)
        {
            paused = on;
            if (pool != null)
                foreach (var source in pool)
                    if (source) { if (on) source.Pause(); else source.UnPause(); }
            if (loops != null)
                foreach (var loop in loops)
                    foreach (var source in new[] { loop.heard, loop.fading })
                        if (source) { if (on) source.Pause(); else source.UnPause(); }
        }

        void StopAll()
        {
            if (pool != null)
                for (int i = 0; i < pool.Length; i++) { if (pool[i]) pool[i].Stop(); poolLeft[i] = 0f; }
            if (loops != null) foreach (var loop in loops) { Silence(loop); loop.level = 0f; }
            later.Clear();
            paused = false;
            seenValid = false;
        }

        // ───── Events ─────

        void OnPhase(string phase, int day)
        {
            if (phase == "night") Emit(CampaignSfx.NightFall, null);
            else if (phase == "dawn") Emit(CampaignSfx.DayBreak, null);
        }

        void OnDayStarted(int day, bool overtime) { if (Live) Later(CampaignSfx.DayTitleIngame, dayTitleDelay, null); }

        void OnBaseDamaged(float amount, float toHp, CampaignRoom room)
        {
            if (!Live) return;
            Vector3 at = SidePoint(room);
            if (toHp > 0f) Emit(CampaignSfx.BaseHit, at);
            // All of it on the shield: its hit, unless that emptied it (Watch then plays the break).
            else if (amount > 0f && campaign.BaseShield > .01f) Emit(CampaignSfx.ShieldHit, at);
        }

        void OnPurchased(string item, string detail, CampaignRoom room, CampaignCost cost, int day)
        {
            if (!Live) return;
            if (item == "lamp") Emit(CampaignSfx.CraftLight, null);
            else if (item == "fuel") fuelUntil = Time.unscaledTime + fuelFillSeconds;
        }

        static Vector3 SidePoint(CampaignRoom room)
        {
            int side = Array.IndexOf(V2Campaign.SideRoom, room);
            return side < 0 ? V2Campaign.BaseCentre : V2Campaign.BaseCentre + V2Campaign.SideNormal[side] * V2Campaign.BaseHalf;
        }

        // ───── Watching the campaign's state ─────

        void Watch(float dt)
        {
            var c = campaign;
            var p = gm.player;
            Vector3 pp = p ? Query.Flat(p.Position) : V2Campaign.BaseCentre;
            var stage = c.Stage;
            float lampHp = c.LampHpTotal(out var weakestLamp);
            bool overrun = stage == CampaignStage.Night && p && c.Overrun(c.RegionOf(pp));
            if (seenValid)
            {
                // The base panel opens and closes.
                if (stage != seenStage)
                {
                    if (stage == CampaignStage.Base) Emit(CampaignSfx.BaseUiOpen, null);
                    else if (seenStage == CampaignStage.Base) Emit(CampaignSfx.BaseUiClose, null);
                }
                // Rooms: broken, repaired, built (level 0 to 1) or upgraded; the hull's level counts as Central Control's.
                for (int i = 0; i < 4; i++)
                {
                    var room = (CampaignRoom)i;
                    var state = c.Room(room);
                    if (state.damaged && !seenBroken[i]) Emit(CampaignSfx.RoomBreak, SidePoint(room));
                    else if (!state.damaged && seenBroken[i]) Emit(CampaignSfx.RoomRepair, null);
                    if (state.level > seenLevel[i]) Emit(seenLevel[i] == 0 ? CampaignSfx.AreaBuild : CampaignSfx.AreaUpgrade, null);
                }
                if (c.Durability > seenDurability) Emit(CampaignSfx.AreaUpgrade, null);
                // The ship.
                if (c.EngineFixed && !seenEngine) Emit(CampaignSfx.ShipEngineRepair, null);
                if (c.ArmorFixed && !seenArmor) Emit(CampaignSfx.ShipArmorRepair, null);
                if (c.FuelFull && !seenFull) Emit(CampaignSfx.FuelFull, null);
                // The shield: emptied, or refilled (the afternoon's refill, a new level, a top-up).
                if (seenShield > .01f && c.BaseShield <= .01f) Emit(CampaignSfx.ShieldBreak, V2Campaign.BaseCentre);
                else if (c.BaseShield > seenShield + Mathf.Max(.5f, c.BaseMaxShield * .01f)) Emit(CampaignSfx.ShieldRecharge, null);
                // Lamps: one set down (its light comes on a moment later), one eaten by shadows (its hp falls), one lost at night.
                if (c.LampCount > seenLamps) { Emit(CampaignSfx.LightPlace, pp); Later(CampaignSfx.LightOn, lightOnDelay, pp); }
                else if (c.LampCount < seenLamps && stage == CampaignStage.Night && seenStage == CampaignStage.Night) Emit(CampaignSfx.LightDestroyed, seenWeakestLamp);
                else if (c.LampCount == seenLamps && lampHp < seenLampHp - 1e-4f) Emit(CampaignSfx.LightFlicker, weakestLamp);
                // Shadows: the night's appear once; at night only a shadow's touch takes the player's HP.
                if (c.ShadowCount > seenShadows && stage == CampaignStage.Night) Later(CampaignSfx.ShadowAppear, shadowAppearDelay, null);
                if (p && stage == CampaignStage.Night && p.Hp < seenHp - .01f) Emit(CampaignSfx.ShadowAttack, pp);
                if (overrun && !seenOverrun) Emit(CampaignSfx.ZoneOverrunWarning, null);
                // Currency: alloy only from ore since 2026-11-06 (no alloy shard drops), dark matter only mined, energy only picked up. The
                // arrival's grant (a story scene that opens the base in the same frame) makes no sound. Void alloy plays from
                // V2Campaign.Void.cs (MinedVoidAlloy).
                if (stage is not (CampaignStage.Story or CampaignStage.Base))
                {
                    if (c.EarnedAlloy > seenAlloy) Emit(CampaignSfx.OrePickupE, pp);
                    if (c.EarnedDark > seenDark) Emit(CampaignSfx.CollectDarkMatter, pp);
                    if (c.EarnedEnergy > seenEnergy) Emit(CampaignSfx.CollectAlienEnergy, pp);
                }
                // A click to fire where no weapon fires (the night, and the afternoon without a fight), so it does not seem broken.
                var mouse = Mouse.current;
                var ui = c.Ui;
                if (gm.RunPhase == ArenaUiState.Night && gm.CanPlayerMove && p && p.IsAlive && mouse != null && mouse.leftButton.wasPressedThisFrame
                    && !(ui && ui.ConfirmOpen) && !c.CratePanelOpen)
                    Emit(CampaignSfx.NightNoCombat, null);
            }
            // Mining: a strike at once and then every mineStrikeSeconds while E is held on a node.
            if (c.MiningAt(out var node))
            {
                mineClock -= dt;
                if (mineClock <= 0f) { Emit(CampaignSfx.MineHit, node); mineClock = mineStrikeSeconds; }
            }
            else mineClock = 0f;

            seenValid = true;
            seenStage = stage;
            for (int i = 0; i < 4; i++) { var state = c.Room((CampaignRoom)i); seenBroken[i] = state.damaged; seenLevel[i] = state.level; }
            seenDurability = c.Durability;
            seenEngine = c.EngineFixed; seenArmor = c.ArmorFixed; seenFull = c.FuelFull;
            seenShield = c.BaseShield;
            seenLamps = c.LampCount; seenLampHp = lampHp; seenWeakestLamp = weakestLamp;
            seenShadows = c.ShadowCount;
            seenHp = p ? p.Hp : 0f;
            seenOverrun = overrun;
            seenAlloy = c.EarnedAlloy; seenEnergy = c.EarnedEnergy; seenDark = c.EarnedDark;
        }

        // ───── Loops ─────

        LoopInputs ReadLoopInputs()
        {
            var c = campaign;
            var s = LoopInputs.Far;
            s.stage = c.Stage;
            s.day = c.Day;
            s.overrunAt = c.rules ? c.rules.overrunShadows : 1;
            s.fueling = Time.unscaledTime < fuelUntil;
            for (int i = 0; i < 4; i++) s.roomBroken |= c.Room((CampaignRoom)i).damaged;
            var p = gm.player;
            if (p)
            {
                Vector3 pp = Query.Flat(p.Position);
                s.toBase = Query.Flat(pp - V2Campaign.BaseCentre).magnitude;
                s.toCrash = Query.Flat(pp - c.CrashSitePoint).magnitude;
                if (p.IsAlive)
                {
                    s.inDarkness = c.PlayerInDarkness;
                    s.nearestShadow = c.NearestShadowDistance(pp);
                    s.shadowsHere = c.ShadowsIn(c.RegionOf(pp));
                }
            }
            return s;
        }

        float FuelPitch() => campaign && campaign.FuelUnits > 0 ? Mathf.Lerp(fuelPitch.x, fuelPitch.y, Mathf.Clamp01(campaign.Fuel / (float)campaign.FuelUnits)) : 1f;

        void TickLoops(float dt, in LoopInputs inputs, bool live)
        {
            if (loops == null)
            {
                loops = new LoopChannel[LoopSounds.Length];
                for (int i = 0; i < loops.Length; i++) loops[i] = new LoopChannel { sfx = LoopSounds[i] };
            }
            foreach (var ch in loops)
            {
                var entry = EntryOf(ch.sfx);
                float target = live && entry.clip ? LoopLevel(ch.sfx, inputs, ranges) : 0f;
                ch.level = Mathf.MoveTowards(ch.level, target, dt / Mathf.Max(.05f, target > ch.level ? loopFadeIn : loopFadeOut));
                if (ch.level <= .0005f || !entry.clip) { Silence(ch); continue; }
                if (!ch.heard) { ch.heard = NewSource(true); ch.fading = NewSource(true); }
                float pitch = ch.sfx == CampaignSfx.FuelFillLoop ? FuelPitch() : 1f;
                float length = entry.clip.length, seamLength = Mathf.Max(.01f, Mathf.Min(loopSeam, length * .25f));
                if (!ch.on || ch.heard.clip != entry.clip) { Begin(ch.heard, entry.clip, pitch); ch.on = true; ch.played = 0f; ch.seam = -1f; }
                ch.heard.pitch = ch.fading.pitch = pitch;
                ch.played += dt * pitch;
                // The seam: the round ending fades out on one source while the next starts from the top on the other.
                if (ch.seam < 0f && ch.played >= length - seamLength)
                {
                    (ch.heard, ch.fading) = (ch.fading, ch.heard);
                    Begin(ch.heard, entry.clip, pitch);
                    ch.played = 0f; ch.seam = 0f;
                }
                float gain = ch.level * entry.gain * loopsGain * Master, weight = 1f;
                if (ch.seam >= 0f)
                {
                    ch.seam += dt * pitch;
                    float t = ch.seam / seamLength;
                    weight = SeamIn(t);
                    ch.fading.volume = gain * SeamOut(t);
                    if (t >= 1f) { ch.fading.Stop(); ch.seam = -1f; weight = 1f; }
                }
                ch.heard.volume = gain * weight;
            }
        }

        static void Begin(AudioSource source, AudioClip clip, float pitch)
        {
            source.Stop();
            source.clip = clip;
            source.time = 0f;
            source.pitch = pitch;
            source.volume = 0f;
            source.Play();
        }

        static void Silence(LoopChannel ch)
        {
            if (!ch.on) return;
            if (ch.heard) ch.heard.Stop();
            if (ch.fading) ch.fading.Stop();
            ch.on = false; ch.played = 0f; ch.seam = -1f;
        }
    }
}
