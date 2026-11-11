using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Where the settings panel's choices are kept from one play to the next (the volume, the keys): PlayerPrefs, under keys named like
    /// the saved language's (ArenaLanguage.SavedChoiceKey). In the editor PlayerPrefs are kept per company and product name, so every
    /// copy of this project on the machine shares them. The tests therefore keep these choices in memory for their whole run
    /// (UseMemoryForTests): they neither read what a player saved nor change it, and a test that saves something and reads it back
    /// still goes through the same calls.
    /// </summary>
    public static class ArenaPrefs
    {
        public static string GetString(string key, string fallback = "")
        {
#if UNITY_INCLUDE_TESTS
            if (memory != null) return memory.TryGetValue(key, out var kept) ? kept : fallback;
#endif
            return PlayerPrefs.GetString(key, fallback);
        }

        public static void SetString(string key, string value)
        {
#if UNITY_INCLUDE_TESTS
            if (memory != null) { memory[key] = value ?? ""; return; }
#endif
            PlayerPrefs.SetString(key, value ?? "");
        }

        /// <summary>A number is kept as text, written the same way whatever the system's language (a point, never a comma).</summary>
        public static float GetFloat(string key, float fallback)
        {
            string text = GetString(key, "");
            return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;
        }

        public static void SetFloat(string key, float value) => SetString(key, value.ToString("0.###", CultureInfo.InvariantCulture));

        public static void Delete(string key)
        {
#if UNITY_INCLUDE_TESTS
            if (memory != null) { memory.Remove(key); return; }
#endif
            PlayerPrefs.DeleteKey(key);
        }

        /// <summary>Writes what was set to disk (on the web, to the browser's storage). Setting alone only changes the copy in memory.</summary>
        public static void Save()
        {
#if UNITY_INCLUDE_TESTS
            if (memory != null) return;
#endif
            PlayerPrefs.Save();
        }

#if UNITY_INCLUDE_TESTS
        static Dictionary<string, string> memory;

        /// <summary>Tests only: on, the choices are kept in an empty memory store instead of PlayerPrefs; off, PlayerPrefs again.
        /// Whoever switches it then asks ArenaKeys and ArenaVolume to read their choices again (Reload).</summary>
        public static void UseMemoryForTests(bool on) => memory = on ? new Dictionary<string, string>() : null;

        /// <summary>Tests only: the choices are kept in memory now.</summary>
        public static bool MemoryForTests => memory != null;

        // The editor keeps statics from one play to the next (domain reload is off): a play after a test run saves to PlayerPrefs again.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay() => memory = null;
#endif
    }
}
