using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Whitebox / illustrated toggle shared by every presentation component, switched on the title (in whitebox the base,
    /// the characters, the monsters and the weapons are boxes, the terrain and the backdrop pictures stay as they are). Since 2026-11-10 one
    /// scene serves both languages, and the first run's art is whitebox in both (until that afternoon it followed the start language, illustrated in Chinese): whitebox
    /// is the default (the course staff approved the illustrated art only as an optional mode). Once
    /// the player switches, their choice is kept from run to run for the rest of the play: static fields survive scene reloads. The enemy
    /// lab shows only the illustrated art. The switch changes the scene, the monsters, the characters and the combat
    /// effects; the menus and icons are the same either way.
    /// The boxes of the characters and of the weapon in hand: V2ModelPresentation, V2WeaponPresentation.
    /// </summary>
    public static class Art
    {
        /// <summary>The enemy lab shows only the illustrated art. A developer's scene, not a language: it keeps its name check.</summary>
        public const string EnemyLabScene = "EnemyLab";

        /// <summary>The named scene shows only the illustrated art (the enemy lab).</summary>
        public static bool IsIllustratedOnlyScene(string sceneName) => sceneName == EnemyLabScene;

        /// <summary>The first run's art: whitebox, whatever language the game opened in (2026-11-10).</summary>
        public const bool DefaultIllustrated = false;

        public static bool Enabled;
        /// <summary>The running scene shows only the illustrated art (the enemy lab): Enabled stays on and the title's switch does nothing.</summary>
        public static bool IllustratedOnly { get; private set; }
        // Whether this play's first run has taken its default (the name it had before 2026-11-10; RunAnalyticsTest reads it), and the
        // runs' choice, held while the enemy lab runs.
        static bool v2ChoiceInitialized;
        static bool? toggleChoice;
        static MaterialPropertyBlock mpb;

        /// <summary>Every play starts the way a freshly loaded page does. The editor keeps statics from one play to the next (domain reload
        /// is off), so otherwise the choice made in the last play would decide the first run of the next one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            Enabled = false;
            IllustratedOnly = false;
            v2ChoiceInitialized = false;
#if UNITY_INCLUDE_TESTS
            DefaultForTests = null;
#endif
            toggleChoice = null;
        }

        /// <summary>Called as each run scene starts (GameManagerV2), with whether the scene shows only the illustrated art. The first run
        /// of a play takes the default (whitebox); later runs keep the player's choice, and get it back after the enemy lab.</summary>
        public static void EnsureV2Default(bool illustratedOnly = false)
        {
            if (illustratedOnly)
            {
                if (!IllustratedOnly) toggleChoice = Enabled;
                IllustratedOnly = true;
                Enabled = true;
                return;
            }
            if (IllustratedOnly)
            {
                IllustratedOnly = false;
                if (toggleChoice.HasValue) Enabled = toggleChoice.Value;
            }
            toggleChoice = null;
            if (!v2ChoiceInitialized)
            {
#if UNITY_INCLUDE_TESTS
                Enabled = DefaultForTests?.Invoke() ?? DefaultIllustrated;
#else
                Enabled = DefaultIllustrated;
#endif
                v2ChoiceInitialized = true;
            }
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Tests only: the first run's art in place of the default. TestStartLanguage sets it for the whole test run, so the tests
        /// keep the start they were written for (illustrated when started in Chinese, whitebox in English); a test of the default itself
        /// sets it to null. Null outside tests.</summary>
        public static System.Func<bool> DefaultForTests { get; set; }
#endif

        /// <summary>The title's whitebox / illustrated switch. The enemy lab has none, so it never leaves the illustrated art.</summary>
        public static void Toggle()
        {
            if (!IllustratedOnly) Enabled = !Enabled;
        }

        /// <summary>Tints one renderer through a property block, leaving its shared material untouched.</summary>
        public static void Tint(Renderer r, Color c)
        {
            if (!r) return;
            mpb ??= new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
        }
    }
}
