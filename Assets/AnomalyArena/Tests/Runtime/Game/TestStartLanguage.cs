using NUnit.Framework;

namespace AnomalyArena
{
    /// <summary>
    /// The game picks its start language from the player's saved choice or the web page, English otherwise, and its first run is in
    /// whitebox (ArenaLanguage, Art). The tests must not depend on a choice saved in an editor (the editor keeps PlayerPrefs per product,
    /// so every copy of the project shares them), and they were written for a start in Chinese with the illustrated art. For the whole
    /// run every scene starts in Chinese, and the first run's art is illustrated when started in Chinese and whitebox when started in
    /// English, as it was when the tests were written. A test that needs another start uses Use before it loads the scene and gets the
    /// run's start back with Reset in its TearDown; the tests of the real rules set ArenaLanguage.StartupForTests or
    /// Art.DefaultForTests to null themselves.
    /// </summary>
    [SetUpFixture]
    public class TestStartLanguage
    {
        /// <summary>The run's start: Chinese, whatever the scene.</summary>
        public static string RunStart(string sceneName) => StartupLanguage.Chinese;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Reset();
            Art.DefaultForTests = RunArt;
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            ArenaLanguage.StartupForTests = null;
            Art.DefaultForTests = null;
        }

        /// <summary>The run's first art: illustrated for a start in Chinese, whitebox for one in English.</summary>
        public static bool RunArt() => ArenaLanguage.StartupCode == StartupLanguage.Chinese;

        /// <summary>Every scene loaded from now on starts in this language (a locale code).</summary>
        public static void Use(string code) => ArenaLanguage.StartupForTests = _ => code;

        /// <summary>Back to the run's start.</summary>
        public static void Reset() => ArenaLanguage.StartupForTests = RunStart;
    }
}
