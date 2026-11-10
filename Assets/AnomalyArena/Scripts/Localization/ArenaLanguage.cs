using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace AnomalyArena
{
    /// <summary>
    /// The game's language in one scene for both languages (2026-11-10). Each scene start asks StartupLanguage which language to open
    /// in, with the player's saved choice and the web page's address read here (English without either); the title's Language option
    /// switches between Chinese and English at once and saves the choice for the next visit. The language does not depend on which
    /// scene runs. The first run's art follows the language the game opened in (Art.EnsureV2Default, called from GameManager).
    /// </summary>
    public static class ArenaLanguage
    {
        /// <summary>PlayerPrefs key of the player's own choice. In the editor PlayerPrefs are kept per company and product name, so every
        /// copy of this project on the machine shares it.</summary>
        public const string SavedChoiceKey = "WarpArena.Language";

        /// <summary>The locale code the last scene start chose (ResolveStartup); null before any scene started in this play.</summary>
        public static string StartupCode { get; private set; }

        /// <summary>The last scene start opened in Chinese. The first run's art follows it: illustrated in Chinese, whitebox in English.</summary>
        public static bool StartedInChinese => StartupCode == StartupLanguage.Chinese;

        /// <summary>The player's saved choice, empty when there is none.</summary>
        public static string SavedChoice => PlayerPrefs.GetString(SavedChoiceKey, "");

        // The editor keeps statics from one play to the next (domain reload is off), so every play starts as a freshly loaded page does.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            StartupCode = null;
#if UNITY_INCLUDE_TESTS
            StartupForTests = null;
#endif
        }

        /// <summary>The language a scene opens in, by StartupLanguage's rule, and remembered as StartupCode. Every scene start asks again
        /// (a restart reloads the scene), which gives the same answer unless the player chose on the title meanwhile, and that choice is
        /// saved.</summary>
        public static string ResolveStartup(string sceneName = null)
        {
#if UNITY_INCLUDE_TESTS
            if (StartupForTests != null) return StartupCode = StartupLanguage.Normalize(StartupForTests(sceneName)) ?? StartupLanguage.English;
#endif
            // The platform, not the UNITY_WEBGL symbol: the editor of this project is set to the WebGL target and defines it too.
            return StartupCode = StartupLanguage.Resolve(SavedChoice, Application.platform == RuntimePlatform.WebGLPlayer, Application.absoluteURL);
        }

        /// <summary>The title's Language option: Chinese to English or English to Chinese, at once, and saved. False while the text
        /// tables are still loading.</summary>
        public static bool ToggleChoice() =>
            ArenaText.Ready && Choose(ArenaText.IsChinese ? StartupLanguage.English : StartupLanguage.Chinese);

        /// <summary>Selects one of the two languages and saves it as the player's choice. False for an unknown code or a missing locale.</summary>
        public static bool Choose(string code)
        {
            code = StartupLanguage.Normalize(code);
            if (code == null) return false;
            var locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(code));
            if (!locale) return false;
            PlayerPrefs.SetString(SavedChoiceKey, code);
            PlayerPrefs.Save();
            if (locale != LocalizationSettings.SelectedLocale) LocalizationSettings.SelectedLocale = locale;
            return true;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Tests only: the start language by scene name, in place of the saved choice, the page and the system. TestStartLanguage
        /// fixes it for the whole test run, so no test depends on a choice saved in the editor; a test of the
        /// rule itself sets it to null. Null outside tests.</summary>
        public static Func<string, string> StartupForTests { get; set; }
#endif
    }
}
