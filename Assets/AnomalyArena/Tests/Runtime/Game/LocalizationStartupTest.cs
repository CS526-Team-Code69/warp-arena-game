using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    public class LocalizationStartupTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

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

            await Until(() => ArenaText.Font, 12f, "the locale's font asset loads");
            var hud = UnityEngine.Object.FindAnyObjectByType<V2Hud>();
            Assume.That(hud && hud.localizedCanvas, "the saved scene has the localized HUD canvas");
            var fontField = typeof(ArenaText).GetField("font", BindingFlags.NonPublic | BindingFlags.Static);
            Assume.That(fontField, Is.Not.Null, "ArenaText keeps the loaded locale font in a static field");

            object loadedFont = fontField.GetValue(null);
            try
            {

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
