using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>On the web the locale's font asset is a separate download of the whole CJK font. The title screen and HUD must not
    /// wait for it: text tables are enough, and the labels keep the packaged font the scene was saved with until it arrives.
    /// Since 2026-11-10 the scene no longer decides the start language (ArenaLanguage, StartupLanguageTest for the rule itself): in the
    /// scene, a choice the player saved decides it, and the tests' own start (TestStartLanguage) outranks everything.</summary>
    [TestFixture]
    public class LocalizationStartupTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        // The editor shares PlayerPrefs between the copies of this project, so a choice saved here is taken back.
        bool choiceTouched;
        string choiceBefore;

        [TearDown]
        public void TearDown()
        {
            if (choiceTouched)
            {
                if (choiceBefore == null) PlayerPrefs.DeleteKey(ArenaLanguage.SavedChoiceKey);
                else PlayerPrefs.SetString(ArenaLanguage.SavedChoiceKey, choiceBefore);
                PlayerPrefs.Save();
            }
            choiceTouched = false;
            choiceBefore = null;
            TestStartLanguage.Reset();
        }

        [TestCase(StartupLanguage.English, false), TestCase(StartupLanguage.Chinese, true), BuildScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task SceneStart_SavedChoice_StartsInTheSavedLanguageWhateverTheMachine(string saved, bool chinese)
        {
            SaveChoice(saved);
            ArenaLanguage.StartupForTests = null; // the real rule: the saved choice comes before the page and the system

            await SceneManagerHelper.LoadSceneAsync(Scene);
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");

            Assert.That(ArenaLanguage.StartupCode, Is.EqualTo(saved), "the scene start chose the saved language");
            Assert.That(ArenaText.IsChinese, Is.EqualTo(chinese), "and the text tables are in it");
        }

        [TestCase(StartupLanguage.English, StartupLanguage.Chinese), TestCase(StartupLanguage.Chinese, StartupLanguage.English)]
        [BuildScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task SceneStart_TheTestsStart_OutranksASavedChoice(string testStart, string saved)
        {
            SaveChoice(saved);
            TestStartLanguage.Use(testStart);

            await SceneManagerHelper.LoadSceneAsync(Scene);
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");

            Assert.That(ArenaText.IsChinese, Is.EqualTo(testStart == StartupLanguage.Chinese));
        }

        void SaveChoice(string code)
        {
            choiceBefore = PlayerPrefs.HasKey(ArenaLanguage.SavedChoiceKey) ? PlayerPrefs.GetString(ArenaLanguage.SavedChoiceKey) : null;
            choiceTouched = true;
            PlayerPrefs.SetString(ArenaLanguage.SavedChoiceKey, code);
        }

        [Test]
        public void LocalizationSettings_PreloadBehavior_NoPreloadingSoStartupSkipsTheFontAsset()
        {
            var settings = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
            Assume.That(settings, Is.Not.Null, "the Localization package is installed");
            var preload = settings.GetProperty("PreloadBehavior", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            Assert.That(preload?.ToString(), Is.EqualTo("NoPreloading"),
                "preloading the selected locale's asset table makes initialization wait for the whole font");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(30000)]
        public async Task TextLoaded_FontAssetStillLoading_HudStaysVisibleAndLabelsKeepTheirFont()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            // The font asset now lands after Ready; let it land first, or it would overwrite the simulated gap below.
            await Until(() => ArenaText.Font, 12f, "the locale's font asset loads");
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>();
            Assume.That(hud && hud.localizedCanvas, "the saved scene has the localized HUD canvas");
            var fontField = typeof(ArenaText).GetField("font", BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(fontField, Is.Not.Null, "ArenaText keeps the loaded locale font in a static field");

            object loadedFont = fontField.GetValue(null);
            try
            {
                // As if the font download had not finished yet.
                fontField.SetValue(null, null);
                for (int i = 0; i < 5; i++) await Awaitable.NextFrameAsync();

                Assert.That(ArenaText.Ready, Is.True, "text tables alone make the UI ready");
                Assert.That(ArenaText.Font, Is.Null);
                Assert.That(hud.localizedCanvas.enabled, Is.True, "the title screen and HUD show before the font asset arrives");
                var labels = (hud.fontLabels ?? Array.Empty<UnityEngine.UI.Text>()).Where(label => label).ToArray();
                Assert.That(labels, Is.Not.Empty);
                Assert.That(labels.Where(label => !label.font).Select(label => label.name), Is.Empty,
                    "labels keep the scene's font instead of being cleared");
            }
            finally { fontField.SetValue(null, loadedFont); }
        }

        static async Task Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition())
            {
                if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
