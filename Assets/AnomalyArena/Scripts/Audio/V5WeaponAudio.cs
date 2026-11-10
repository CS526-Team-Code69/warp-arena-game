using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Every clip of the 2026-10-30 sound rework, one per file in Resources/V5/Audio. The files come from
    /// work/art-plans/art-redo-1030/audio/make_sfx.py, which also prints the gains in the table below.</summary>
    public enum V5Sfx
    {
        // Hand weapons
        RifleShot, RifleHeavy, RifleBurst, ChargeTick, ChargeFull, ChargeCancel, ScatterBlast, ScatterPellet, ScatterVacuum, RailShot, RailExecute,
        MinigunShot, MinigunSpindown, MinigunOverheat, MinigunVent, SwarmSalvo, SwarmBurst, BombardLaunch, FlameStart, WildfireLob, WildfireLand,
        CryoShard, CryoStrike, CryoShatter, ArcChain, CapacitorBoom, NeedleShot, SporeLob, SporeLand, HookThrow, HookCatch, RocketSelf,
        ReloadClip, ReloadShell, ReloadNeedler, ReloadDone, DryClick,
        // Auto weapons
        OrbitSlice, DroneShot, DroneWhirl, WhirlSlam, RingDeflect, RingTick, RingShieldUp, RingShieldBreak,
        // Loops
        LoopChargeRifle, LoopChargeRail, LoopChargeFocus, LoopCapacitor, LoopMinigun, LoopFlame, LoopBlizzard, LoopGravity,
        // Enemies
        SpitGurgle, SpireCharge, StomperRaise, StomperSlam, ShadeHiss, ShadeStrike, RamCrash, TyrantGrowl, TyrantSweep, PoundBand, HeavyWhoosh,
        MeteorWhistle, EnergyRise, ScepterRaise, ScepterBlast, AstralDevour, NashWindup, NashRepulse, NashRing, NashVolley, NashPillarWarn, NashPillar, NashSummon,
        // The seven elements, in ElementKind order (ElementHit counts on it)
        ElementFire, ElementPoison, ElementFrost, ElementLightning, ElementEarth, ElementLight, ElementVoid
    }

    /// <summary>The sound layer: weapons that sound like themselves, charge and spray loops
    /// that follow the state of the gun, enemy moves with their own sounds and a short sound for each of the seven elements. V2Audio adds it at
    /// runtime, so no scene changes; it makes its own 2D AudioSources, and gameplay only ever reads from it. The loops are driven in LateUpdate
    /// from what the weapon's own state says, so a release or a cancel stops them in the frame it happens, whichever path it came by.
    /// Pitch variation uses its own sequence, never the gameplay random numbers.</summary>
    [DefaultExecutionOrder(1450)]
    public sealed class V5WeaponAudio : MonoBehaviour
    {
        enum Bus { Weapon, Enemy, Element }

        readonly struct Entry
        {
            public readonly V5Sfx id; public readonly string file; public readonly float gain, minInterval, jitter; public readonly int voices; public readonly Bus bus;
            public Entry(V5Sfx id, string file, float gain, float minInterval = .05f, int voices = 2, float jitter = 0f, Bus bus = Bus.Weapon)
            { this.id = id; this.file = file; this.gain = gain; this.minInterval = minInterval; this.voices = voices; this.jitter = jitter; this.bus = bus; }
        }

        // id, file, gain, shortest gap between two starts, voices at once (a fast stream cuts its own oldest tail, it never drops a shot), pitch variation.
        // The gains hold the levels of audio/levels.csv: the rifle shot about as loud as before, the music about where it was against it, elements well below.
        static readonly Entry[] Table =
        {
            new(V5Sfx.RifleShot, "rifle-shot", 0.314f, .05f, 4, .03f), new(V5Sfx.RifleHeavy, "rifle-heavy", 0.321f, .05f), new(V5Sfx.RifleBurst, "rifle-burst", 0.379f, .04f, 4, .035f),
            new(V5Sfx.ChargeTick, "charge-tick", 0.211f, .05f, 1), new(V5Sfx.ChargeFull, "charge-full", 0.212f, .1f, 1), new(V5Sfx.ChargeCancel, "charge-cancel", 0.153f, .05f, 1),
            new(V5Sfx.ScatterBlast, "scatter-blast", 0.319f, .06f, 2, .025f), new(V5Sfx.ScatterPellet, "scatter-pellet", 0.198f, .02f, 4), new(V5Sfx.ScatterVacuum, "scatter-vacuum", 0.186f, .1f, 1),
            new(V5Sfx.RailShot, "rail-shot", 0.315f, .1f, 2, .015f), new(V5Sfx.RailExecute, "rail-execute", 0.357f, .08f, 2, .02f),
            new(V5Sfx.MinigunShot, "minigun-shot", 0.147f, .025f, 4, .04f), new(V5Sfx.MinigunSpindown, "minigun-spindown", 0.168f, .15f, 1), new(V5Sfx.MinigunOverheat, "minigun-overheat", 0.233f, .4f, 1), new(V5Sfx.MinigunVent, "minigun-vent", 0.18f, .3f, 1),
            new(V5Sfx.SwarmSalvo, "swarm-salvo", 0.347f, .3f, 1, .02f), new(V5Sfx.SwarmBurst, "swarm-burst", 0.249f, .05f, 3, .05f), new(V5Sfx.BombardLaunch, "bombard-launch", 0.193f, .2f, 1, .02f),
            new(V5Sfx.FlameStart, "flame-start", 0.272f, .1f, 1, .02f), new(V5Sfx.WildfireLob, "wildfire-lob", 0.227f, .1f, 2, .03f), new(V5Sfx.WildfireLand, "wildfire-land", 0.207f, .08f, 2, .04f),
            new(V5Sfx.CryoShard, "cryo-shard", 0.204f, .06f, 2, .03f), new(V5Sfx.CryoStrike, "cryo-strike", 0.299f, .06f, 2, .05f), new(V5Sfx.CryoShatter, "cryo-shatter", 0.263f, .1f, 2, .02f),
            new(V5Sfx.ArcChain, "arc-chain", 0.276f, .05f, 2, .03f), new(V5Sfx.CapacitorBoom, "capacitor-boom", 0.309f, .1f, 1),
            new(V5Sfx.NeedleShot, "needle-shot", 0.178f, .04f, 3, .05f), new(V5Sfx.SporeLob, "spore-lob", 0.189f, .08f, 2, .03f), new(V5Sfx.SporeLand, "spore-land", 0.273f, .08f, 2, .03f),
            new(V5Sfx.HookThrow, "hook-throw", 0.231f, .08f, 1, .02f), new(V5Sfx.HookCatch, "hook-catch", 0.277f, .06f, 1, .02f), new(V5Sfx.RocketSelf, "rocket-self", 0.278f, .1f, 1, .01f),
            new(V5Sfx.ReloadClip, "reload-clip", 0.327f, .1f, 1, .01f), new(V5Sfx.ReloadShell, "reload-shell", 0.164f, .1f, 1, .01f), new(V5Sfx.ReloadNeedler, "reload-needler", 0.163f, .1f, 1, .02f),
            new(V5Sfx.ReloadDone, "reload-done", 0.131f, .05f, 1, .01f), new(V5Sfx.DryClick, "dry-click", 0.244f, .09f, 1, .02f),

            new(V5Sfx.OrbitSlice, "orbit-slice", 0.193f, .07f, 2, .06f), new(V5Sfx.DroneShot, "drone-shot", 0.207f, .04f, 3, .04f), new(V5Sfx.DroneWhirl, "drone-whirl", 0.163f, .2f, 1), new(V5Sfx.WhirlSlam, "whirl-slam", 0.281f, .1f, 1, .02f),
            new(V5Sfx.RingDeflect, "ring-deflect", 0.207f, .06f, 2, .04f), new(V5Sfx.RingTick, "ring-tick", 0.155f, .2f, 1, .03f), new(V5Sfx.RingShieldUp, "ring-shield-up", 0.12f, .3f, 1), new(V5Sfx.RingShieldBreak, "ring-shield-break", 0.352f, .1f, 1, .02f),

            new(V5Sfx.LoopChargeRifle, "loop-charge-rifle", 0.165f), new(V5Sfx.LoopChargeRail, "loop-charge-rail", 0.174f), new(V5Sfx.LoopChargeFocus, "loop-charge-focus", 0.282f), new(V5Sfx.LoopCapacitor, "loop-capacitor", 0.197f),
            new(V5Sfx.LoopMinigun, "loop-minigun", 0.13f), new(V5Sfx.LoopFlame, "loop-flame", 0.234f), new(V5Sfx.LoopBlizzard, "loop-blizzard", 0.226f), new(V5Sfx.LoopGravity, "loop-gravity", 0.116f, .05f, 2, 0f, Bus.Enemy),

            new(V5Sfx.SpitGurgle, "spit-gurgle", 0.155f, .15f, 2, .06f, Bus.Enemy), new(V5Sfx.SpireCharge, "spire-charge", 0.197f, .2f, 2, .02f, Bus.Enemy), new(V5Sfx.StomperRaise, "stomper-raise", 0.263f, .2f, 2, .03f, Bus.Enemy),
            new(V5Sfx.StomperSlam, "stomper-slam", 0.484f, .12f, 2, .03f, Bus.Enemy), new(V5Sfx.ShadeHiss, "shade-hiss", 0.411f, .2f, 2, .03f, Bus.Enemy), new(V5Sfx.ShadeStrike, "shade-strike", 0.327f, .1f, 2, .03f, Bus.Enemy),
            new(V5Sfx.RamCrash, "ram-crash", 0.309f, .12f, 2, .03f, Bus.Enemy), new(V5Sfx.TyrantGrowl, "tyrant-growl", 0.203f, .2f, 1, 0f, Bus.Enemy), new(V5Sfx.TyrantSweep, "tyrant-sweep", 0.349f, .2f, 1, 0f, Bus.Enemy),
            new(V5Sfx.PoundBand, "pound-band", 0.228f, .06f, 2, .02f, Bus.Enemy), new(V5Sfx.HeavyWhoosh, "heavy-whoosh", 0.346f, .15f, 1, 0f, Bus.Enemy), new(V5Sfx.MeteorWhistle, "meteor-whistle", 0.14f, .2f, 1, 0f, Bus.Enemy),
            new(V5Sfx.EnergyRise, "energy-rise", 0.275f, .2f, 1, 0f, Bus.Enemy), new(V5Sfx.ScepterRaise, "scepter-raise", 0.23f, .3f, 1, 0f, Bus.Enemy), new(V5Sfx.ScepterBlast, "scepter-blast", 0.406f, .3f, 1, 0f, Bus.Enemy),
            new(V5Sfx.AstralDevour, "astral-devour", 0.308f, .3f, 1, 0f, Bus.Enemy), new(V5Sfx.NashWindup, "nash-windup", 0.203f, .1f, 1, .01f, Bus.Enemy), new(V5Sfx.NashRepulse, "nash-repulse", 0.3f, .2f, 1, 0f, Bus.Enemy),
            new(V5Sfx.NashRing, "nash-ring", 0.347f, .1f, 2, 0f, Bus.Enemy), new(V5Sfx.NashVolley, "nash-volley", 0.18f, .05f, 3, 0f, Bus.Enemy), new(V5Sfx.NashPillarWarn, "nash-pillar-warn", 0.357f, .2f, 1, 0f, Bus.Enemy),
            new(V5Sfx.NashPillar, "nash-pillar", 0.313f, .12f, 2, .02f, Bus.Enemy), new(V5Sfx.NashSummon, "nash-summon", 0.309f, .2f, 2, .02f, Bus.Enemy),

            new(V5Sfx.ElementFire, "element-fire", 0.111f, 0f, 2, .04f, Bus.Element), new(V5Sfx.ElementPoison, "element-poison", 0.169f, 0f, 2, .05f, Bus.Element), new(V5Sfx.ElementFrost, "element-frost", 0.245f, 0f, 2, .04f, Bus.Element),
            new(V5Sfx.ElementLightning, "element-lightning", 0.11f, 0f, 2, .05f, Bus.Element), new(V5Sfx.ElementEarth, "element-earth", 0.159f, 0f, 2, .05f, Bus.Element), new(V5Sfx.ElementLight, "element-light", 0.096f, 0f, 2, .02f, Bus.Element),
            new(V5Sfx.ElementVoid, "element-void", 0.068f, 0f, 2, .04f, Bus.Element),
        };

        sealed class Loop { public AudioSource source; public V5Sfx sfx; public bool live, dying; public float level; }
        struct Voice { public AudioSource source; public int id; public float startedAt, endsAt; }

        public static V5WeaponAudio Instance { get; private set; }
        const string Folder = "V5/Audio/";
        const int VoiceCount = 18, WavHeader = 44;
        static readonly System.Collections.Generic.Dictionary<string, AudioClip> Built = new();
        const float ElementGap = .15f, ElementAnyGap = .05f, ElementSparseSeconds = .6f, HitStacks = .25f;

        [Header("Mix: one number per group of sounds")]
        [Range(0f, 1f)] public float weaponBus = 1f;
        [Range(0f, 1f)] public float enemyBus = 1f;
        [Range(0f, 1f)] public float elementBus = 1f;
        [Tooltip("Sounds started within a moment of each other add up: past this total a new one comes in lower, so a crowd cannot clip.")] public float headroom = .8f;
        [Tooltip("An enemy farther than this makes no sound; V3WorldAudio stops at the same distance.")] public float audibleRange = 26f;
        [Tooltip("Level of an enemy at that distance, next to one at your feet.")] [Range(0f, 1f)] public float farGain = .62f;

        Entry[] entries;
        AudioClip[] clips;
        float[] nextPlay, lastGain, lastPitch, elementNext, elementLast;
        int[] plays;
        Voice[] voices;
        Loop charge, capacitor, spin, spray, gravity;
        Loop[] loops;
        bool paused, chargeFull, capacitorFull, spinOn, wasFlame, wasSnow;
        float pausedAt, burst, burstAt, spinShare, focusCharge = 1f, elementAnyNext;
        int sequence;
        Enemy puller;
        Combatant watchedPlayer;
        readonly RunWeapon[] watchedHand = new RunWeapon[2];
        readonly bool[] handReloading = new bool[2];

        void Awake()
        {
            Instance = this;
            int count = Enum.GetValues(typeof(V5Sfx)).Length;
            entries = new Entry[count]; clips = new AudioClip[count]; nextPlay = new float[count]; lastGain = new float[count]; lastPitch = new float[count]; plays = new int[count];
            elementNext = new float[8]; elementLast = new float[8];
            foreach (var entry in Table) { entries[(int)entry.id] = entry; clips[(int)entry.id] = BuildClip(entry.file); }
            var holder = new GameObject("V5AudioVoices");
            holder.transform.SetParent(transform, false);
            voices = new Voice[VoiceCount];
            for (int i = 0; i < voices.Length; i++) { voices[i].source = NewSource(holder, 110); voices[i].id = -1; }
            charge = NewLoop(holder); capacitor = NewLoop(holder); spin = NewLoop(holder); spray = NewLoop(holder); gravity = NewLoop(holder);
            loops = new[] { charge, capacitor, spin, spray, gravity };
        }

        void Start()
        {
            SwapBanks();
            // A saved clip whose Preload is off loads on its first play; loading the banks now keeps that stall away from the first shot of a run.
            var v2 = V2Audio.Instance;
            if (v2 && v2.clips != null) foreach (var entry in v2.clips) if (entry != null && entry.clip && entry.clip.loadState == AudioDataLoadState.Unloaded && entry.clip.loadType == AudioClipLoadType.DecompressOnLoad) entry.clip.LoadAudioData();
            var v3 = V3WorldAudio.Instance;
            if (v3 && v3.bank != null) foreach (var sound in v3.bank) if (sound != null && sound.clip && sound.clip.loadState == AudioDataLoadState.Unloaded && sound.clip.loadType == AudioClipLoadType.DecompressOnLoad) sound.clip.LoadAudioData();
        }

        /// <summary>Why the clips are built here and not imported (measured 2026-10-30 in Chrome 156 on clips cut out of a web build; work/art-plans/art-redo-1030/audio):
        /// on the web Unity re-encodes every imported AudioClip to AAC whatever its import settings, and the first 10.2 ms of each comes out as digital silence with its end
        /// padded to a whole AAC frame. That eats the attack of every shot and opens a gap of about 31 ms at each turn of a loop. A clip made with AudioClip.Create reaches the
        /// browser as plain samples at once. The files are wav data kept as .bytes so that Unity leaves them alone; a clip is built once and lives through scene reloads.</summary>
        static AudioClip BuildClip(string file)
        {
            if (Built.TryGetValue(file, out var clip) && clip) return clip;
            var asset = Resources.Load<TextAsset>(Folder + file);
            if (!asset) return null;
            var bytes = asset.bytes;
            int count = (bytes.Length - WavHeader) / 2;
            if (count <= 0) return null;
            var samples = new float[count];
            for (int i = 0; i < count; i++) samples[i] = (short)(bytes[WavHeader + i * 2] | bytes[WavHeader + i * 2 + 1] << 8) / 32768f;
            clip = AudioClip.Create(file, count, 1, 44100, false);
            clip.SetData(samples, 0);
            clip.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Resources.UnloadAsset(asset);
            Built[file] = clip;
            return clip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ForgetBuiltClips() => Built.Clear();

        /// <summary>The saved banks' hits and shots: the scene keeps its clips and this run plays the same recordings built as above, so they lose nothing on the web either.
        /// Only what is struck or fired; wind and menus can spare 10 ms. A file that is missing leaves the saved clip in place.</summary>
        void SwapBanks()
        {
            var v2 = V2Audio.Instance;
            if (v2 && v2.clips != null)
                foreach (var entry in v2.clips)
                {
                    string file = entry == null ? null : entry.cue switch
                    {
                        ArenaSoundCue.Launch => "bank-weapon-launch", ArenaSoundCue.Knife => "bank-weapon-knife-hook", ArenaSoundCue.Impact => "bank-weapon-impact",
                        ArenaSoundCue.Explosion => "bank-weapon-explosion", ArenaSoundCue.BloaterWarning => "bank-bloater-warning", _ => null
                    };
                    var built = file == null ? null : BuildClip(file);
                    if (built) entry.clip = built;
                }
            var v3 = V3WorldAudio.Instance;
            if (v3 && v3.bank != null)
                foreach (var sound in v3.bank)
                {
                    string file = sound == null ? null : sound.cue switch
                    {
                        ArenaWorldCue.NpcSwing => "bank-npc-swing", ArenaWorldCue.NpcWindup => "bank-npc-windup", ArenaWorldCue.NpcHit => "bank-npc-hit", ArenaWorldCue.NpcSpit => "bank-npc-spit",
                        ArenaWorldCue.NpcChargeWarning => "bank-npc-charge-warning", ArenaWorldCue.NpcChargeRush => "bank-npc-charge-rush", ArenaWorldCue.NpcShield => "bank-npc-shield",
                        ArenaWorldCue.NpcPulse => "bank-npc-pulse", ArenaWorldCue.NpcSplit => "bank-npc-split", ArenaWorldCue.NpcBloaterBlast => "bank-npc-bloater-blast",
                        ArenaWorldCue.BossCleave => "bank-boss-cleave", ArenaWorldCue.BossSlam => "bank-boss-slam", ArenaWorldCue.BossCharge => "bank-boss-charge",
                        ArenaWorldCue.PlayerHit => "bank-player-hit", ArenaWorldCue.PlayerStunned => "bank-player-stunned", ArenaWorldCue.NpcLaserFire => "bank-npc-laser-fire",
                        ArenaWorldCue.WeaponJam => "bank-weapon-jam", ArenaWorldCue.WeaponRepair => "bank-weapon-repair", ArenaWorldCue.WeaponFocusCharge => "bank-weapon-focus-charge",
                        ArenaWorldCue.WeaponFocusRelease => "bank-weapon-focus-release", ArenaWorldCue.WeaponKnifeReturn => "bank-weapon-knife-return", ArenaWorldCue.SkillVoidWalk => "bank-skill-void-walk", _ => null
                    };
                    var built = file == null ? null : BuildClip(file);
                    if (built) sound.clip = built;
                }
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (watchedPlayer) watchedPlayer.AttackReceived -= OnPlayerAttacked;
        }

        void OnDisable() => StopAll();

        static AudioSource NewSource(GameObject on, int priority)
        {
            var source = on.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0f; source.dopplerLevel = 0f; source.priority = priority;
            return source;
        }

        static Loop NewLoop(GameObject on) => new() { source = NewSource(on, 100) };

        float BusGain(Bus bus) => bus == Bus.Enemy ? enemyBus : bus == Bus.Element ? elementBus : weaponBus;

        // ───── One-shot sounds ─────

        /// <summary>Plays one clip at once, heard the same everywhere (the player's own sounds).</summary>
        public bool Play(V5Sfx sfx, float gainScale = 1f, float pitchScale = 1f)
        {
            int id = (int)sfx;
            if (!isActiveAndEnabled || paused || clips == null || !clips[id]) return false;
            float now = Time.unscaledTime;
            if (now < nextPlay[id]) return false;
            var entry = entries[id];
            nextPlay[id] = now + entry.minInterval;
            int slot = PickVoice(id, Mathf.Max(1, entry.voices), now);
            float pitch = Mathf.Clamp(pitchScale * (1f + Mathf.Sin(++sequence * 2.39996f) * entry.jitter), .25f, 3f);
            float gain = Limited(Mathf.Clamp01(entry.gain * gainScale * BusGain(entry.bus)), now);
            var source = voices[slot].source;
            source.clip = clips[id]; source.pitch = pitch; source.volume = gain; source.Play();
            voices[slot].id = id; voices[slot].startedAt = now; voices[slot].endsAt = now + clips[id].length / pitch;
            plays[id]++; lastGain[id] = gain; lastPitch[id] = pitch;
            return true;
        }

        /// <summary>A sound from a point in the world: silent past the audible range, a little lower with distance. All voices are 2D, like the other banks.</summary>
        public static bool At(V5Sfx sfx, Vector3 at, float gainScale = 1f, float pitchScale = 1f)
        {
            var audio = Instance;
            if (!audio) return false;
            var gm = GameManager.Instance;
            if (gm && gm.player)
            {
                float distance = Query.Flat(gm.player.Position - at).magnitude;
                if (distance > audio.audibleRange) return false;
                gainScale *= Mathf.Lerp(1f, audio.farGain, distance / Mathf.Max(.01f, audio.audibleRange));
            }
            return audio.Play(sfx, gainScale, pitchScale);
        }

        /// <summary>The same, played fast or slow so that it lasts this many seconds: a wind-up that ends when the blow lands.</summary>
        public static bool AtFit(V5Sfx sfx, Vector3 at, float seconds, float gainScale = 1f)
        {
            var clip = Instance && Instance.clips != null ? Instance.clips[(int)sfx] : null;
            return clip && At(sfx, at, gainScale, Mathf.Clamp(clip.length / Mathf.Max(.05f, seconds), .5f, 2f));
        }

        int PickVoice(int id, int limit, float now)
        {
            int same = 0, oldestSame = -1, free = -1, oldest = 0;
            float oldestSameAt = float.PositiveInfinity, oldestAt = float.PositiveInfinity;
            for (int i = 0; i < voices.Length; i++)
            {
                if (now >= voices[i].endsAt) { if (free < 0) free = i; continue; }
                if (voices[i].id == id)
                {
                    same++;
                    if (voices[i].startedAt < oldestSameAt) { oldestSameAt = voices[i].startedAt; oldestSame = i; }
                }
                if (voices[i].startedAt < oldestAt) { oldestAt = voices[i].startedAt; oldest = i; }
            }
            if (same >= limit) return oldestSame;
            return free >= 0 ? free : oldest;
        }

        /// <summary>Keeps sounds that start together from adding up past the headroom. Tails do not count for long: what was started fades out of the sum in a moment.</summary>
        float Limited(float gain, float now)
        {
            burst *= Mathf.Exp(-(now - burstAt) / .06f); burstAt = now;
            gain = Mathf.Min(gain, Mathf.Max(.06f, headroom - burst));
            burst += gain;
            return gain;
        }

        // ───── The weapons ─────

        /// <summary>Every weapon sound comes through here. When this layer has none for the event, the saved V2Audio bank plays it as before.</summary>
        public static void Weapon(RunWeapon weapon, WeaponAudioPhase phase, bool strike = false)
        {
            if (weapon == null) return;
            if (Instance && Instance.PlayWeapon(weapon, phase, strike)) return;
            V2Audio.Instance?.PlayWeapon(weapon.form, weapon.variant, phase);
        }

        bool PlayWeapon(RunWeapon w, WeaponAudioPhase phase, bool strike)
        {
            if (paused) return true;
            switch (phase)
            {
                case WeaponAudioPhase.ChargeStart: return WeaponChargeStart(w);
                case WeaponAudioPhase.Cancel: Play(w.form == WeaponForm.ArcCoil ? V5Sfx.DryClick : V5Sfx.ChargeCancel); return true;
                case WeaponAudioPhase.Launch: return WeaponLaunch(w);
                case WeaponAudioPhase.Explosion: return WeaponExplosion(w);
                case WeaponAudioPhase.Impact: return WeaponImpact(w);
                default: return WeaponFire(w, strike);
            }
        }

        bool WeaponChargeStart(RunWeapon w)
        {
            switch (w.form)
            {
                case WeaponForm.Minigun: Play(V5Sfx.MinigunOverheat); return true; // Meltdown's warning
                case WeaponForm.ScatterCannon:
                    if (w.variant == 0) Play(V5Sfx.ScatterVacuum); // Focus has the saved burst WeaponFocusCharge on its press
                    return true;
                case WeaponForm.Rifle: Play(V5Sfx.ChargeTick); return true;
                case WeaponForm.Railgun: Play(V5Sfx.ChargeTick, 1f, 1.25f); return true;
                default: return false;
            }
        }

        bool WeaponLaunch(RunWeapon w)
        {
            switch (w.form)
            {
                case WeaponForm.Knife:
                    if (w.variant == 0) return false; // the saved Knife cue, its slow start now cut
                    if (w.hooked) Play(V5Sfx.HeavyWhoosh, .6f, 1.25f); // the caught enemy flung away
                    else Play(V5Sfx.HookThrow);
                    return true;
                case WeaponForm.RocketLauncher:
                    if (w.variant == 0) return false;
                    Play(V5Sfx.RocketSelf); return true;
                case WeaponForm.Flamethrower: Play(V5Sfx.WildfireLob); return true;
                case WeaponForm.SporeLauncher: Play(V5Sfx.SporeLob); return true;
                case WeaponForm.SwarmLauncher: Play(V5Sfx.BombardLaunch); return true;
                case WeaponForm.SentryDrone: Play(V5Sfx.DroneWhirl); return true;
                default: return false;
            }
        }

        bool WeaponExplosion(RunWeapon w)
        {
            switch (w.form)
            {
                case WeaponForm.ScatterCannon: Play(V5Sfx.WildfireLand, .8f, 1.1f); return true; // a hot shell flaring up
                case WeaponForm.CryoCannon: Play(V5Sfx.CryoShatter); return true;
                case WeaponForm.SporeLauncher: Play(V5Sfx.SporeLand); return true;
                case WeaponForm.SwarmLauncher:
                    if (w.variant == 1) return false; // Bombard's blasts are the saved explosion
                    Play(V5Sfx.SwarmBurst); return true;
                case WeaponForm.SentryDrone: Play(V5Sfx.WhirlSlam); return true;
                case WeaponForm.ArcCoil: Play(V5Sfx.CapacitorBoom); return true;
                default: return false;
            }
        }

        bool WeaponImpact(RunWeapon w)
        {
            switch (w.form)
            {
                case WeaponForm.OrbitBlades: return true; // the slice is its hit
                case WeaponForm.Flamethrower when w.variant == 0: return true; // the spray's loop carries it, a tick for each hit would break it up again
                case WeaponForm.CryoCannon when w.variant == 1: return true;
                case WeaponForm.Knife when w.variant == 1: Play(V5Sfx.HookCatch); return true;
                case WeaponForm.ShieldRing when w.variant == 0: Play(V5Sfx.RingTick); return true;
                default: return false;
            }
        }

        bool WeaponFire(RunWeapon w, bool strike)
        {
            var weapons = GameManager.Instance ? GameManager.Instance.runWeapons : null;
            switch (w.form)
            {
                case WeaponForm.Rifle:
                    if (w.variant == 1) { Play(V5Sfx.RifleBurst); return true; }
                    float c = Mathf.Clamp01(w.charge);
                    // The more it holds, the lower and heavier: the same shot with its weight mixed in.
                    Play(V5Sfx.RifleShot, Mathf.Lerp(.85f, .75f, c), Mathf.Lerp(1.1f, .88f, c));
                    if (c > .05f) Play(V5Sfx.RifleHeavy, Mathf.Lerp(.12f, 1f, c * c), Mathf.Lerp(1.22f, 1f, c));
                    return true;
                // The Pistol (since 2026-11-06) has no recording of its own: the Rifle's uncharged shot, lighter and a little higher.
                case WeaponForm.Pistol: Play(V5Sfx.RifleShot, .7f, 1.2f); return true;
                case WeaponForm.ScatterCannon:
                    if (w.variant == 0) { Play(V5Sfx.ScatterBlast); return true; }
                    // Focus: the bolts climb a little through the burst and weigh what the release held.
                    Play(V5Sfx.ScatterPellet, Mathf.Lerp(.6f, 1f, focusCharge), 1f + .035f * Mathf.Max(0, 6 - w.burstLeft));
                    return true;
                case WeaponForm.Flamethrower:
                    if (w.variant == 1) Play(V5Sfx.WildfireLand); // Stream is the loop
                    return true;
                case WeaponForm.CryoCannon:
                    if (w.variant == 0) Play(strike ? V5Sfx.CryoStrike : V5Sfx.CryoShard); // Blizzard is the loop
                    return true;
                case WeaponForm.ArcCoil:
                    if (w.variant == 0) { Play(V5Sfx.ArcChain); return true; }
                    float stored = weapons ? weapons.Charge01 : 1f;
                    Play(V5Sfx.ArcChain, Mathf.Lerp(.6f, 1f, stored), Mathf.Lerp(1.2f, .92f, stored));
                    Play(V5Sfx.CapacitorBoom, stored * stored, Mathf.Lerp(1.2f, 1f, stored));
                    return true;
                case WeaponForm.SporeLauncher: Play(w.variant == 0 ? V5Sfx.SporeLand : V5Sfx.NeedleShot); return true;
                case WeaponForm.Railgun: Play(w.variant == 0 ? V5Sfx.RailShot : V5Sfx.RailExecute); return true;
                case WeaponForm.Minigun: Play(V5Sfx.MinigunShot, 1f, Mathf.Lerp(.96f, 1.1f, spinShare)); return true;
                case WeaponForm.SwarmLauncher: Play(V5Sfx.SwarmSalvo); return true; // one clip for the volley; the rest of its frame falls under the gap
                case WeaponForm.OrbitBlades: Play(V5Sfx.OrbitSlice, 1f, w.variant == 0 ? 1f : .86f); return true;
                case WeaponForm.SentryDrone: Play(V5Sfx.DroneShot); return true;
                case WeaponForm.ShieldRing: Play(w.variant == 0 ? V5Sfx.RingDeflect : V5Sfx.RingShieldBreak); return true;
                default: return false;
            }
        }

        /// <summary>The saved Focus release, with the weight of what the charge held; the burst's bolts read it too.</summary>
        public static void FocusRelease(float charge, Vector3 at)
        {
            charge = Mathf.Clamp01(charge);
            if (Instance) Instance.focusCharge = charge;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponFocusRelease, at, Mathf.Lerp(1.22f, .92f, charge), Mathf.Lerp(.55f, 1f, charge));
        }

        // ───── The loops: read from the weapon's state every frame ─────

        void LateUpdate()
        {
            var gm = GameManager.Instance;
            bool pause = gm && gm.RunPhase == ArenaUiState.Paused;
            if (pause != paused) SetPaused(pause);
            if (!paused) Tick();
        }

        /// <summary>One pass over the state of the guns. LateUpdate runs it after input and the weapons' own tick, so it sees this frame's press or release.</summary>
        public void Tick()
        {
            var gm = GameManager.Instance;
            var weapons = gm ? gm.runWeapons : null;
            WatchPlayer(gm);
            bool live = gm && weapons && gm.IsCombatActive && gm.player && gm.player.IsAlive;
            var w = live ? weapons.Active : null;
            DriveCharge(w);
            DriveCapacitor(w, weapons);
            bool trigger = w != null && weapons.TriggerHeld && gm.CanUseCombatInput;
            DriveSpin(w, trigger);
            DriveSpray(w, trigger);
            if (puller && (!live || !puller.GravityPulling)) puller = null;
            Drive(gravity, V5Sfx.LoopGravity, puller, 1f, 1f, .25f);
            WatchReloads(live, weapons);
        }

        void DriveCharge(RunWeapon w)
        {
            bool on = w != null && w.charging;
            var sfx = V5Sfx.LoopChargeRifle;
            float low = .7f, high = 1.7f;
            if (on && w.form == WeaponForm.Railgun) { sfx = V5Sfx.LoopChargeRail; low = .8f; high = 2f; }
            else if (on && w.form == WeaponForm.ScatterCannon) { sfx = V5Sfx.LoopChargeFocus; low = .85f; high = 1.5f; }
            float c = on ? Mathf.Clamp01(w.charge) : 0f;
            Drive(charge, sfx, on, Mathf.Lerp(.45f, 1.25f, c), Mathf.Lerp(low, high, c), .03f);
            // The Railgun fires itself at full, so it never holds here long enough to ring.
            bool full = on && w.form != WeaponForm.Railgun && c >= .999f;
            if (full && !chargeFull) Play(V5Sfx.ChargeFull);
            chargeFull = full;
        }

        /// <summary>The Capacitor in hand hums with what it stores, rings once at full, and climbs through the seconds before it goes off by itself.</summary>
        void DriveCapacitor(RunWeapon w, RunWeapons weapons)
        {
            bool on = w != null && w.form == WeaponForm.ArcCoil && w.variant == 1 && w.charge > 0f;
            float stored = on ? weapons.Charge01 : 0f, wait = on ? Mathf.Clamp01(w.chargeFullTime / 4f) : 0f;
            Drive(capacitor, V5Sfx.LoopCapacitor, on, Mathf.Lerp(.25f, 1.2f, stored) + .3f * wait, Mathf.Lerp(.78f, 1.3f, stored) + .45f * wait, .08f);
            bool full = on && stored >= .999f;
            if (full && !capacitorFull) Play(V5Sfx.ChargeFull, 1f, .84f);
            capacitorFull = full;
        }

        /// <summary>The Gatling's barrels: pitch and level follow the spin (Spin-up) or the heat (Meltdown); the trigger coming up runs them down at once.</summary>
        void DriveSpin(RunWeapon w, bool trigger)
        {
            bool on = trigger && w.form == WeaponForm.Minigun && w.reload <= 0f;
            float s = !on ? 0f : w.variant == 0 ? Mathf.Clamp01(w.heldTime / 3f) : Mathf.Clamp01(w.heat / 4f);
            float pitch = !on ? 1f : w.variant == 0 ? Mathf.Lerp(.62f, 1.45f, s) : Mathf.Lerp(.95f, 1.4f, s);
            Drive(spin, V5Sfx.LoopMinigun, on, Mathf.Lerp(.5f, 1.15f, s), pitch, .04f);
            if (on) { spinShare = s; spinOn = true; }
            else if (spinOn)
            {
                spinOn = false;
                if (spinShare > .12f) Play(V5Sfx.MinigunSpindown, Mathf.Lerp(.5f, 1f, spinShare), Mathf.Lerp(.8f, 1.3f, spinShare));
                spinShare = 0f;
            }
        }

        /// <summary>The Stream and the Blizzard are one unbroken sound for as long as the trigger is down.</summary>
        void DriveSpray(RunWeapon w, bool trigger)
        {
            bool flame = trigger && w.form == WeaponForm.Flamethrower && w.variant == 0, snow = trigger && w.form == WeaponForm.CryoCannon && w.variant == 1;
            if (flame && !wasFlame) Play(V5Sfx.FlameStart);
            if (snow && !wasSnow) Play(V5Sfx.CryoShard, .5f, .8f);
            wasFlame = flame; wasSnow = snow;
            Drive(spray, snow ? V5Sfx.LoopBlizzard : V5Sfx.LoopFlame, flame || snow, 1f, 1f, .02f);
        }

        /// <summary>Starts, follows or ends a loop. The frame it ends it drops to a third and the next frame it stops: a few milliseconds' fade, so the hard cut does not click.</summary>
        void Drive(Loop loop, V5Sfx sfx, bool on, float gainScale, float pitch, float attack)
        {
            var clip = clips != null ? clips[(int)sfx] : null;
            if (on && clip)
            {
                var entry = entries[(int)sfx];
                float target = Mathf.Clamp01(entry.gain * gainScale * BusGain(entry.bus));
                if (!loop.live || loop.sfx != sfx)
                {
                    loop.sfx = sfx; loop.live = true; loop.dying = false; loop.level = target * .45f;
                    var source = loop.source;
                    source.clip = clip; source.loop = true; source.pitch = pitch; source.volume = loop.level; source.Play();
                    plays[(int)sfx]++;
                }
                else
                {
                    loop.level = Mathf.MoveTowards(loop.level, target, Time.unscaledDeltaTime * Mathf.Max(target, .01f) / Mathf.Max(.001f, attack));
                    loop.source.volume = loop.level; loop.source.pitch = pitch;
                }
            }
            else if (loop.live) { loop.live = false; loop.dying = true; loop.source.volume = loop.level * .3f; }
            else if (loop.dying) { loop.dying = false; loop.source.Stop(); }
        }

        /// <summary>A reload has no event of its own, so it is read from its timer: the moment it starts and the moment it is done.</summary>
        void WatchReloads(bool live, RunWeapons weapons)
        {
            if (!weapons || weapons.Hand == null) return;
            for (int i = 0; i < watchedHand.Length && i < weapons.Hand.Length; i++)
            {
                var w = weapons.Hand[i];
                bool reloading = w != null && w.reload > 0f;
                if (live && w == watchedHand[i] && reloading != handReloading[i])
                {
                    if (!reloading) Play(V5Sfx.ReloadDone, w == weapons.Active ? 1f : .55f);
                    else if (w.form == WeaponForm.Rifle) Play(V5Sfx.ReloadClip);
                    else if (w.form == WeaponForm.ScatterCannon) Play(V5Sfx.ReloadShell);
                    else if (w.form == WeaponForm.SporeLauncher) Play(V5Sfx.ReloadNeedler);
                    else if (w.form == WeaponForm.Minigun) Play(V5Sfx.MinigunVent);
                }
                watchedHand[i] = w; handReloading[i] = reloading;
            }
        }

        // ───── Enemies ─────

        /// <summary>The wind-up of an ordinary enemy's attack: its own where it had none, the saved one pitched to its body where that fits.</summary>
        public static void Windup(EnemyKind kind, Vector3 at)
        {
            var bank = V3WorldAudio.Instance;
            switch (kind)
            {
                case EnemyKind.Shade: At(V5Sfx.ShadeHiss, at); break;
                case EnemyKind.Spitter: At(V5Sfx.SpitGurgle, at); break;
                case EnemyKind.Spire: At(V5Sfx.SpireCharge, at); break;
                case EnemyKind.Stomper: At(V5Sfx.StomperRaise, at); break;
                case EnemyKind.Brute: bank?.Play(ArenaWorldCue.NpcWindup, at, .8f); break;
                case EnemyKind.Skitter: bank?.Play(ArenaWorldCue.NpcWindup, at, 1.25f, .8f); break;
                default: bank?.Play(ArenaWorldCue.NpcWindup, at); break;
            }
        }

        /// <summary>A melee blow: a heavy body swings low, a small one quick and light; the Shade's is a blade.</summary>
        public static void Swing(EnemyKind kind, Vector3 at)
        {
            var bank = V3WorldAudio.Instance;
            switch (kind)
            {
                case EnemyKind.Shade: At(V5Sfx.ShadeStrike, at); break;
                case EnemyKind.Brute: bank?.Play(ArenaWorldCue.NpcSwing, at, .78f); break;
                case EnemyKind.Skitter: bank?.Play(ArenaWorldCue.NpcSwing, at, 1.3f, .7f); break;
                case EnemyKind.Splitter: bank?.Play(ArenaWorldCue.NpcSwing, at, .9f); break;
                default: bank?.Play(ArenaWorldCue.NpcSwing, at); break;
            }
        }

        /// <summary>The Nash Prophet's gravity pull: a low swirl that lasts as long as the pull does.</summary>
        public void TrackPull(Enemy prophet) => puller = prophet;

        // ───── The seven elements ─────

        /// <summary>A hit that carried an element, under the weapons in the mix. Each element waits a moment after its last sound, all of them wait a little for each
        /// other, and an element heard again soon comes in lower, so a rapid-fire infused gun colours its hits instead of covering them.</summary>
        public static void ElementHit(ElementKind element, Vector3 at, float stacks = 1f)
        {
            var audio = Instance;
            if (!audio || element == ElementKind.None || stacks < HitStacks) return;
            int slot = (int)element;
            float now = Time.unscaledTime;
            if (now < audio.elementNext[slot] || now < audio.elementAnyNext) return;
            float density = Mathf.Lerp(.55f, 1f, Mathf.Clamp01((now - audio.elementLast[slot]) / ElementSparseSeconds));
            audio.elementLast[slot] = now; audio.elementNext[slot] = now + ElementGap; audio.elementAnyNext = now + ElementAnyGap;
            At(ElementSfx(element), at, density);
        }

        static V5Sfx ElementSfx(ElementKind element) => element switch
        {
            ElementKind.Fire => V5Sfx.ElementFire, ElementKind.Poison => V5Sfx.ElementPoison, ElementKind.Frost => V5Sfx.ElementFrost, ElementKind.Lightning => V5Sfx.ElementLightning,
            ElementKind.Earth => V5Sfx.ElementEarth, ElementKind.Light => V5Sfx.ElementLight, _ => V5Sfx.ElementVoid
        };

        /// <summary>An enemy's hit with Earth, Light or Void puts no stacks on you, so ElementStatus never hears of it: it is read here, shots included.</summary>
        void WatchPlayer(GameManager gm)
        {
            var player = gm ? gm.player : null;
            if (watchedPlayer == player) return;
            if (watchedPlayer) watchedPlayer.AttackReceived -= OnPlayerAttacked;
            watchedPlayer = player;
            if (player) player.AttackReceived += OnPlayerAttacked;
        }

        void OnPlayerAttacked(DamageInfo info)
        {
            if (info.element is ElementKind.Earth or ElementKind.Light or ElementKind.Void && watchedPlayer) ElementHit(info.element, watchedPlayer.Position);
        }

        // ───── Pause and stop ─────

        void SetPaused(bool pause)
        {
            paused = pause;
            float now = Time.unscaledTime;
            if (pause) pausedAt = now;
            for (int i = 0; i < voices.Length; i++)
            {
                if (pause) voices[i].source.Pause();
                else { voices[i].source.UnPause(); voices[i].endsAt += now - pausedAt; }
            }
            foreach (var loop in loops) { if (pause) loop.source.Pause(); else loop.source.UnPause(); }
        }

        public void StopAll()
        {
            if (voices != null) for (int i = 0; i < voices.Length; i++) { if (voices[i].source) voices[i].source.Stop(); voices[i].endsAt = 0f; }
            if (loops != null) foreach (var loop in loops) { if (loop.source) loop.source.Stop(); loop.live = loop.dying = false; }
            paused = chargeFull = capacitorFull = spinOn = wasFlame = wasSnow = false;
            spinShare = 0f; puller = null;
        }

        // ───── What the tests read ─────

        /// <summary>How many times this clip has started.</summary>
        public int Plays(V5Sfx sfx) => plays[(int)sfx];
        /// <summary>The volume and pitch this clip last started at.</summary>
        public float LastGain(V5Sfx sfx) => lastGain[(int)sfx];
        public float LastPitch(V5Sfx sfx) => lastPitch[(int)sfx];
        /// <summary>The clip this layer holds for the entry, or null when its file is missing.</summary>
        public AudioClip ClipOf(V5Sfx sfx) => clips[(int)sfx];
        /// <summary>This looping clip is running and not on its way out.</summary>
        public bool LoopLive(V5Sfx sfx)
        {
            foreach (var loop in loops) if (loop.live && loop.sfx == sfx) return true;
            return false;
        }
        /// <summary>The source playing this looping clip, null when it is not running.</summary>
        public AudioSource LoopSource(V5Sfx sfx)
        {
            foreach (var loop in loops) if ((loop.live || loop.dying) && loop.sfx == sfx) return loop.source;
            return null;
        }
    }
}
