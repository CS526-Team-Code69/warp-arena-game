using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The story's tutorial on the base: the skills page, the base page and the title's two lessons on them name the workshop at the
    /// base door and the ship's own page, and nothing of the old base (its rooms, the study window, the Energy Lab, the shield). The base
    /// page's moving picture shows the two pages, the workshop and the rocket, and no room's name.</summary>
    [TestFixture]
    public class TutorialBaseWordsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const int SkillsPage = 3, BasePage = 5;
        static readonly string[] OldWords = { "建造", "研习", "能量研究区", "各区", "护盾", "build rooms", "study", "Energy Lab", "rooms", "shield" };

        [TearDown]
        public void TearDown()
        {
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
            TestStartLanguage.Reset();
            Time.timeScale = 1f;
        }

        [Test, LoadScene(Scene), Timeout(30000)]
        public async Task Chinese_TheBasePagesAndLessons_NameTheWorkshopAndTheShip_AndNoOldRoom() => await BaseWords();

        [Test, BuildScene(Scene), Timeout(30000)]
        public async Task English_TheBasePagesAndLessons_NameTheWorkshopAndTheShip_AndNoOldRoom()
        {
            TestStartLanguage.Use(StartupLanguage.English);
            await SceneManagerHelper.LoadSceneAsync(Scene);
            await BaseWords();
        }

        static async Task BaseWords()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready, 12f, "scene and localization initialize");
            string workshop = ArenaText.Pair("整备", "workshop"), ship = ArenaText.Pair("飞船旁", "beside the ship"), lamps = ArenaText.Pair("矿灯", "lamps");

            var lines = new[]
            {
                ("the skills page", V2Tutorial.StoryPageLine(SkillsPage)),
                ("the base page", V2Tutorial.StoryPageLine(BasePage)),
                ("the skills lesson", V5Title.LessonLine(SkillsPage - 1)),
                ("the base lesson", V5Title.LessonLine(BasePage - 1)),
            };
            foreach (var (where, line) in lines)
            {
                Assert.That(line, Does.Contain(workshop).IgnoreCase, $"{where} names the workshop: \"{line}\"");
                foreach (var old in OldWords) Assert.That(line, Does.Not.Contain(old).IgnoreCase, $"{where} says nothing of the old base ({old}): \"{line}\"");
            }
            foreach (var (where, line) in lines.Where((_, i) => i % 2 == 1))
            {
                Assert.That(line, Does.Contain(ship).IgnoreCase, $"{where} names the ship's side: \"{line}\"");
                Assert.That(line, Does.Contain(lamps).IgnoreCase, $"{where} says the lamps are made there: \"{line}\"");
            }

            // The base page's moving picture: every text it draws, the captions shown later in its loop included.
            V2Tutorial.ShowStory(ArenaText.Font, BasePage);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            Assert.That(V2Tutorial.Page, Is.EqualTo(BasePage), "the pages open at the base page");
            var texts = Object.FindAnyObjectByType<V2Tutorial>().GetComponentsInChildren<Text>(true).Select(text => text.text).ToArray();
            Assert.That(texts, Has.Some.EqualTo(ArenaText.Pair("整备", "WORKSHOP")), "the picture shows the workshop");
            Assert.That(texts, Has.Some.EqualTo(ArenaText.Pair("火箭", "ROCKET")), "and the rocket page");
            foreach (var room in new[] { CampaignRoom.Control, CampaignRoom.Armament, CampaignRoom.Energy, CampaignRoom.Ship })
                Assert.That(texts, Has.None.EqualTo(V2Campaign.RoomName(room)), $"no row names the {room} room");
            foreach (var old in OldWords)
                Assert.That(texts.Where(text => text.IndexOf(old, StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty, $"no text says {old}");
            V2Tutorial.Close();
        }

        static async Task Until(Func<bool> done, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }
    }
}
