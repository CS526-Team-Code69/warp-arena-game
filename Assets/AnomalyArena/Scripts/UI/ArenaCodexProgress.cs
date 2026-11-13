using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the collection (ArenaCodex) knows the player has met, kept from one run to the next in the saved settings (ArenaPrefs, so
    /// tests use their own). A monster counts as met when one is spawned, a skill when it is taken, a weapon when it is given or one of
    /// its anomalies is revealed. A weapon whose anomaly was revealed before the collection existed counts too (RunWeapons.Reveal's
    /// "anomaly.{form}.{variant}" keys).
    /// </summary>
    public static class ArenaCodexProgress
    {
        const string Prefix = "anomaly.codex.";

        // Keys known to be set, so a spawn costs no read of the saved settings after the first of its kind.
        static readonly HashSet<string> known = new();
#if UNITY_INCLUDE_TESTS
        static bool knownInMemory;
#endif

        public static void DiscoverWeapon(WeaponForm form) => Set("weapon", (int)form);
        public static void DiscoverSkill(SkillId id) => Set("skill", (int)id);
        public static void DiscoverEnemy(EnemyKind kind) => Set("enemy", (int)kind);

        public static bool WeaponDiscovered(WeaponForm form) => Has("weapon", (int)form) || AnomalyRevealed(form, 0) || AnomalyRevealed(form, 1);
        public static bool AnomalyRevealed(WeaponForm form, int variant) => PlayerPrefs.GetInt("anomaly." + (int)form + "." + variant, 0) != 0;
        public static bool SkillDiscovered(SkillId id) => Has("skill", (int)id);
        public static bool EnemyDiscovered(EnemyKind kind) => Has("enemy", (int)kind);

        /// <summary>Forgets what was read, so the next question reads the saved settings again (the tests switch them).</summary>
        public static void ForgetCache() => known.Clear();

        /// <summary>The name of a record in the saved settings: group is "weapon", "skill" or "enemy", id the form, skill or kind.</summary>
        public static string Key(string group, int id) => Prefix + group + "." + id;

        static bool Has(string group, int id)
        {
            string key = Key(group, id);
            Follow();
            if (known.Contains(key)) return true;
            if (ArenaPrefs.GetString(key) != "1") return false;
            known.Add(key);
            return true;
        }

        static void Set(string group, int id)
        {
            string key = Key(group, id);
            Follow();
            if (!known.Add(key) || ArenaPrefs.GetString(key) == "1") return;
            ArenaPrefs.SetString(key, "1");
            ArenaPrefs.Save();
        }

        // The tests' settings and the real ones never share what was read. A player build has no tests' settings.
        static void Follow()
        {
#if UNITY_INCLUDE_TESTS
            if (knownInMemory == ArenaPrefs.MemoryForTests) return;
            knownInMemory = ArenaPrefs.MemoryForTests;
            known.Clear();
#endif
        }

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay() => known.Clear();
    }
}
