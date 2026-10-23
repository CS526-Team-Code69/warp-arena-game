using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;

namespace AnomalyArena
{

    [TestFixture]
    [Category("V3Smoke")]
    public class HudLayoutStateTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2_EN.unity";

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task Apply_Repeated_SavedStateStaysTheSameSize()
        {
            await Awaitable.NextFrameAsync();
            var layout = Object.FindAnyObjectByType<V2HudLayout>();
            Assert.That(layout, Is.Not.Null, "the arena scene has a HUD layout");

            layout.Apply();
            string once = JsonUtility.ToJson(layout);
            layout.Apply();
            layout.Apply();
            string thrice = JsonUtility.ToJson(layout);

            Assert.That(thrice.Length, Is.EqualTo(once.Length), "applying again does not grow the saved state (it used to nest a copy of itself each time)");
            Assert.That(once.Length, Is.LessThan(20000), "the whole component stays a few kilobytes");
        }
    }
}
