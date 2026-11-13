using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The player's volume, set in the settings panel and kept from one play to the next (ArenaPrefs). It is the listener's own volume,
    /// which scales everything that sounds by the same amount: the world's sounds, the music and the ambience (V3WorldAudio), the
    /// campaign's sounds and music, the weapons (V5WeaponAudio) and the interface, weapon and warning voices (V2Audio).
    /// V3WorldAudio.masterGain is not used for it: that is the scene's saved mix level, and it reaches only the sounds that read it,
    /// not V2Audio's voices nor the weapons'. The saved mix stays as it is; the player's volume is applied on top of it.
    /// </summary>
    public static class ArenaVolume
    {
        /// <summary>Its own key, so a volume kept while the default was full volume does not carry over.</summary>
        public const string SavedKey = "WarpArena.MasterVolume";
        /// <summary>The volume before the player chooses one: silent on the course's English builds (BuildEdition), which make no sound
        /// until the player turns it up in the settings; full on the whole game (the Chinese page, the editor).</summary>
        public static float Default => BuildEdition.IsCourseBuild ? 0f : EngineFull;
        // The engine's own full volume, which the editor's previews get back when a play ends.
        const float EngineFull = 1f;

        static float master;
        static bool loaded;

        /// <summary>The volume, 0 to 1.</summary>
        public static float Master
        {
            get
            {
                if (!loaded) Apply();
                return master;
            }
        }

        /// <summary>Sets the volume at once and keeps it for the next play. Save writes it to disk; the settings panel calls that once
        /// when a drag of the slider ends, not for every step of the drag.</summary>
        public static void Set(float volume)
        {
            master = Mathf.Clamp01(volume);
            loaded = true;
            AudioListener.volume = master;
            ArenaPrefs.SetFloat(SavedKey, master);
        }

        public static void Save() => ArenaPrefs.Save();

        /// <summary>Reads the saved volume again and applies it, as the next start does.</summary>
        public static void Reload() => Apply();

        static void Apply()
        {
            master = Mathf.Clamp01(ArenaPrefs.GetFloat(SavedKey, Default));
            loaded = true;
            AudioListener.volume = master;
        }

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay() => loaded = false;

        /// <summary>Before the first scene makes a sound. The listener's volume belongs to the engine, not to the play: leaving a play
        /// in the editor would keep a lowered volume for the editor's own previews, so it goes back to full then.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ApplySaved()
        {
            Apply();
            Application.quitting -= RestoreFull;
            Application.quitting += RestoreFull;
        }

        static void RestoreFull() => AudioListener.volume = EngineFull;
    }
}
