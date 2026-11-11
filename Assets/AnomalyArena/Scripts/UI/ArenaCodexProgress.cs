using UnityEngine;

namespace AnomalyArena
{
    public static class ArenaCodexProgress
    {
        const string Prefix = "anomaly.codex.";

        public static void DiscoverWeapon(WeaponForm form) => Set("weapon", (int)form);
        public static void DiscoverSkill(SkillId id) => Set("skill", (int)id);
        public static void DiscoverEnemy(EnemyKind kind) => Set("enemy", (int)kind);

        public static bool WeaponDiscovered(WeaponForm form)
        {
            if (PlayerPrefs.GetInt(Prefix + "weapon." + (int)form, 0) != 0) return true;
            return PlayerPrefs.GetInt("anomaly." + (int)form + ".0", 0) != 0
                || PlayerPrefs.GetInt("anomaly." + (int)form + ".1", 0) != 0;
        }

        public static bool SkillDiscovered(SkillId id) => Has("skill", (int)id);
        public static bool EnemyDiscovered(EnemyKind kind) => Has("enemy", (int)kind);

        static bool Has(string group, int id) => PlayerPrefs.GetInt(Prefix + group + "." + id, 0) != 0;
        static void Set(string group, int id)
        {
            string key = Prefix + group + "." + id;
            if (PlayerPrefs.GetInt(key, 0) != 0) return;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }
    }
}
