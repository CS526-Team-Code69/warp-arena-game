using UnityEngine;

namespace AnomalyArena
{
    public class V2Audio : MonoBehaviour
    {
        public static V2Audio Instance { get; private set; }
        public AudioSource shotSource, voidSource, waveSource;
        public float shotMinInterval = .08f;
        public float voidMinInterval = .15f;
        [Header("Saved short SFX bank")]
        public ArenaAudioClip[] clips;
        public AudioSource[] weaponVoices, uiVoices;
        [Tooltip("Dedicated saved warning voice, protected from continuous weapon audio.")] public AudioSource[] warningVoices;
        [Min(1f), Tooltip("World sounds beyond this player distance are silent.")] public float worldAudibleRange = 24f;
        [Range(0f, 1f), Tooltip("Master gain for short weapon sounds; the original three channels retain their gains.")] public float weaponGain = .6f;
        [Range(0f, 1f), Tooltip("Master gain for short UI sounds.")] public float uiGain = .6f;
        [Range(0f, .15f), Tooltip("Small pitch variation per playback; independent from gameplay randomness.")] public float pitchVariation = .035f;
        [Tooltip("Shared minimum interval for hover and focus sounds.")] public float uiFocusMinInterval = .06f;
        [Tooltip("Fade-out at the end of a short playback window to avoid abrupt cuts.")] public float windowFadeOut = .025f;
        float nextShot, nextVoid;
        float nextFocus;
        float[] nextCue;
        ArenaSoundCue[] weaponAssignments, uiAssignments;
        ArenaSoundCue[] warningAssignments;
        float[] weaponRemaining, uiRemaining, weaponBaseVolumes, uiBaseVolumes;
        float[] warningRemaining, warningBaseVolumes;
        bool combatPaused;
        int playbackSequence;
        void Awake()
        {
            Instance = this;
            nextCue = new float[System.Enum.GetValues(typeof(ArenaSoundCue)).Length];
            weaponAssignments = new ArenaSoundCue[weaponVoices?.Length ?? 0];
            uiAssignments = new ArenaSoundCue[uiVoices?.Length ?? 0];
            warningAssignments = new ArenaSoundCue[warningVoices?.Length ?? 0];
            weaponRemaining = new float[weaponAssignments.Length]; uiRemaining = new float[uiAssignments.Length];
            weaponBaseVolumes = new float[weaponAssignments.Length]; uiBaseVolumes = new float[uiAssignments.Length];
            warningRemaining = new float[warningAssignments.Length]; warningBaseVolumes = new float[warningAssignments.Length];
            for (int i = 0; i < weaponAssignments.Length; i++) if (weaponVoices[i]) weaponBaseVolumes[i] = weaponVoices[i].volume;
            for (int i = 0; i < uiAssignments.Length; i++) if (uiVoices[i]) uiBaseVolumes[i] = uiVoices[i].volume;
            for (int i = 0; i < warningAssignments.Length; i++) if (warningVoices[i]) warningBaseVolumes[i] = warningVoices[i].volume;
        }
        void Update()
        {
            TickVoices(uiVoices, uiRemaining, uiBaseVolumes);
            if (!combatPaused) { TickVoices(weaponVoices, weaponRemaining, weaponBaseVolumes); TickVoices(warningVoices, warningRemaining, warningBaseVolumes); }
        }
        void TickVoices(AudioSource[] voices, float[] remaining, float[] volumes)
        {
            if (voices == null || remaining == null) return;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i] || remaining[i] <= 0f) continue;
                remaining[i] = Mathf.Max(0f, remaining[i] - Time.unscaledDeltaTime);
                voices[i].volume = volumes[i] * Mathf.Clamp01(remaining[i] / Mathf.Max(.001f, windowFadeOut));
                if (remaining[i] <= 0f) { voices[i].Stop(); voices[i].volume = volumes[i]; }
            }
        }
        void OnDisable() => StopAll();
        void OnDestroy() { if (Instance == this) Instance = null; }

        public void PlayUI(ArenaUiSound cue)
        {
            if (cue == ArenaUiSound.Hover)
            {
                if (Time.unscaledTime < nextFocus) return;
                nextFocus = Time.unscaledTime + Mathf.Max(0f, uiFocusMinInterval);
            }
            TryPlay((ArenaSoundCue)(int)cue, true, 1f, cue is ArenaUiSound.Confirm or ArenaUiSound.Cancel);
        }

        public void PlayWorld(ArenaSoundCue cue, Vector3 at)
        {
            if (combatPaused) return;
            var gm = GameManager.Instance;
            if (gm && gm.player && Query.Flat(gm.player.Position-at).sqrMagnitude > worldAudibleRange*worldAudibleRange) return;
            float pitch = cue == ArenaSoundCue.BloaterWarning && gm ? gm.WorldTimeScale : 1f;
            TryPlay(cue, false, pitch, false);
        }

        public void PlayWeapon(WeaponForm form, int variant, WeaponAudioPhase phase = WeaponAudioPhase.Fire)
        {
            if (combatPaused) return;
            var cue = phase switch
            {
                WeaponAudioPhase.ChargeStart => ArenaSoundCue.Charge,
                WeaponAudioPhase.Impact => ArenaSoundCue.Impact,
                WeaponAudioPhase.Explosion => ArenaSoundCue.Explosion,
                WeaponAudioPhase.Cancel => ArenaSoundCue.Pulse,
                WeaponAudioPhase.Launch => form switch
                {
                    WeaponForm.Knife or WeaponForm.PhaseDagger => ArenaSoundCue.Knife,
                    WeaponForm.CryoCannon => ArenaSoundCue.Ice, WeaponForm.SporeLauncher => ArenaSoundCue.Poison,
                    WeaponForm.Flamethrower => ArenaSoundCue.Spray, _ => ArenaSoundCue.Launch
                },
                _ => form switch
                {
                    WeaponForm.Knife or WeaponForm.PhaseDagger => ArenaSoundCue.Knife,
                    WeaponForm.RocketLauncher or WeaponForm.PlasmaLauncher or WeaponForm.SwarmLauncher => ArenaSoundCue.Launch,
                    WeaponForm.GravGun => ArenaSoundCue.Gravity,
                    WeaponForm.Flamethrower => ArenaSoundCue.Spray,
                    WeaponForm.CryoCannon => ArenaSoundCue.Ice,
                    WeaponForm.SporeLauncher => ArenaSoundCue.Poison,
                    WeaponForm.ArcCoil => ArenaSoundCue.Arc,
                    WeaponForm.OrbitBlades => ArenaSoundCue.Orbital,
                    WeaponForm.ShieldRing => ArenaSoundCue.Pulse,
                    _ => ArenaSoundCue.Fire
                }
            };
            float formPitch = form switch
            {
                WeaponForm.ScatterCannon => .8f, WeaponForm.Railgun => .85f, WeaponForm.Minigun => 1.13f,
                WeaponForm.CryoCannon => 1.2f, WeaponForm.SporeLauncher => .8f, _ => 1f
            };
            TryPlay(cue, false, formPitch * (variant == 1 ? 1.025f : .975f), false);
        }

        bool TryPlay(ArenaSoundCue cue, bool ui, float pitchScale, bool mayReplaceUi)
        {
            if (!isActiveAndEnabled || nextCue == null || clips == null || Time.unscaledTime < nextCue[(int)cue]) return false;
            ArenaAudioClip entry = null;
            foreach (var sound in clips) if (sound != null && sound.cue == cue) { entry = sound; break; }
            if (entry == null || !entry.clip) return false;
            bool warning = cue == ArenaSoundCue.BloaterWarning;
            var voices = ui ? uiVoices : warning ? warningVoices : weaponVoices;
            var assignments = ui ? uiAssignments : warning ? warningAssignments : weaponAssignments;
            if (voices == null || assignments == null) return false;
            int free = -1, concurrent = 0;
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voices[i]) continue;
                if (!voices[i].isPlaying) { if (free < 0) free = i; }
                else if (assignments[i] == cue) concurrent++;
            }
            if (concurrent >= Mathf.Max(1, entry.maxConcurrent)) return false;

            if (free < 0 && mayReplaceUi)
                for (int i = 0; i < voices.Length; i++)
                    if (voices[i] && assignments[i] is ArenaSoundCue.UiHover or ArenaSoundCue.UiSelect) { voices[i].Stop(); free = i; break; }
            if (free < 0) return false;
            var source = voices[free];
            var remaining = ui ? uiRemaining : warning ? warningRemaining : weaponRemaining;
            var volumes = ui ? uiBaseVolumes : warning ? warningBaseVolumes : weaponBaseVolumes;
            assignments[free] = cue;
            nextCue[(int)cue] = Time.unscaledTime + Mathf.Max(.01f, entry.minInterval);
            source.pitch = Mathf.Clamp(entry.pitch * pitchScale * (1f + Mathf.Sin(++playbackSequence * 2.39996f) * pitchVariation), .25f, 3f);
            source.volume = volumes[free];
            remaining[free] = (entry.playbackSeconds > 0f ? Mathf.Min(entry.playbackSeconds, entry.clip.length) : entry.clip.length) / source.pitch;
            source.clip = entry.clip;
            source.PlayOneShot(entry.clip, Mathf.Clamp01(entry.gain * (ui ? uiGain : weaponGain)));
            return true;
        }
        public void PlayShot()
        {
            if (!shotSource || Time.unscaledTime < nextShot) return;
            nextShot = Time.unscaledTime + shotMinInterval;
            shotSource.Play();
        }
        public void PlayVoidFeed()
        {
            if (!voidSource || Time.unscaledTime < nextVoid) return;
            nextVoid = Time.unscaledTime + voidMinInterval;
            voidSource.Play();
        }
        public void PlayWave() { if (waveSource) waveSource.Play(); }
        public void PauseAll()
        {
            combatPaused = true;
            if (shotSource) shotSource.Pause(); if (voidSource) voidSource.Pause(); if (waveSource) waveSource.Pause();
            if (weaponVoices != null) foreach (var voice in weaponVoices) if (voice) voice.Pause();
            if (warningVoices != null) foreach (var voice in warningVoices) if (voice) voice.Pause();
        }
        public void ResumeAll()
        {
            combatPaused = false;
            if (shotSource) shotSource.UnPause(); if (voidSource) voidSource.UnPause(); if (waveSource) waveSource.UnPause();
            if (weaponVoices != null) foreach (var voice in weaponVoices) if (voice) voice.UnPause();
            if (warningVoices != null) foreach (var voice in warningVoices) if (voice) voice.UnPause();
        }
        public void StopAll()
        {
            combatPaused = false;
            if (shotSource) shotSource.Stop(); if (voidSource) voidSource.Stop(); if (waveSource) waveSource.Stop();
            if (weaponVoices != null) foreach (var voice in weaponVoices) if (voice) voice.Stop();
            if (warningVoices != null) foreach (var voice in warningVoices) if (voice) voice.Stop();
            if (uiVoices != null) foreach (var voice in uiVoices) if (voice) voice.Stop();
            if (nextCue != null) System.Array.Clear(nextCue, 0, nextCue.Length);
            if (weaponRemaining != null) System.Array.Clear(weaponRemaining, 0, weaponRemaining.Length);
            if (uiRemaining != null) System.Array.Clear(uiRemaining, 0, uiRemaining.Length);
            if (warningRemaining != null) System.Array.Clear(warningRemaining, 0, warningRemaining.Length);
            nextFocus = nextShot = nextVoid = 0f;
        }
    }
}
