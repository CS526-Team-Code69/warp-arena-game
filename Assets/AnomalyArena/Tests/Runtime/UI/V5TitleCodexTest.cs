using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The title's collection window (V5Title): weapons, skills and monsters from ArenaCodex's lists; one not met yet is ？？？,
    /// shows nothing of itself and cannot be opened (it shakes); one met opens its details; each shelf counts what was found. The records
    /// it touches in the test run's saved settings are put back.</summary>
    [TestFixture]
    public class V5TitleCodexTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const string Page = "Window/Pages/Collection/";
        readonly List<string> touched = new();
        readonly List<string> restore = new(); // marks this test took away, put back in TearDown

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaPrefs.MemoryForTests, Is.True, "the test run keeps the saved choices in memory (TestSavedSettings)");
            touched.Clear();
            restore.Clear();
            V5Title.SummariesForTests = EmptySlots;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var key in touched) ArenaPrefs.Delete(key);
            foreach (var key in restore) ArenaPrefs.SetString(key, "1");
            ArenaCodexProgress.ForgetCache();
            V5Title.SummariesForTests = null;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Collection_NotMetYet_IsQuestionMarks_AndCannotBeOpened()
        {
            await ReadyScene();
            var listed = ArenaCodex.ListedWeapons().ToList();
            var met = WeaponForm.Rifle;
            touched.Add(ArenaCodexProgress.Key("weapon", (int)met));
            ArenaCodexProgress.DiscoverWeapon(met);
            // Earlier tests of the same run may have met every weapon: the first one no anomaly has revealed is made unmet here, and its
            // mark is put back afterwards.
            WeaponForm? unmet = null;
            foreach (var form in listed)
            {
                if (form == met || ArenaCodexProgress.AnomalyRevealed(form, 0) || ArenaCodexProgress.AnomalyRevealed(form, 1)) continue;
                string key = ArenaCodexProgress.Key("weapon", (int)form);
                if (ArenaPrefs.GetString(key) == "1") restore.Add(key);
                ArenaPrefs.Delete(key);
                ArenaCodexProgress.ForgetCache();
                if (ArenaCodexProgress.WeaponDiscovered(form)) continue;
                unmet = form;
                break;
            }
            Assume.That(unmet.HasValue, Is.True, "a weapon not met yet in this run");
            ArenaCodexProgress.ForgetCache();
            V5Title.ReloadForTests();
            await ShowWindow("Collection", V5Title.CollectionTab);
            string metCard = Page + "Grid/Card" + (listed.IndexOf(met) + 1).ToString("00"), unmetCard = Page + "Grid/Card" + (listed.IndexOf(unmet.Value) + 1).ToString("00");

            Assert.That(ActiveCards(), Is.EqualTo(listed.Count), "a card for every weapon listed");
            Assert.That(Words(metCard + "/Name").text, Is.EqualTo(ArenaTerms.WeaponForm(met)), "a weapon met shows its name");
            Assert.That(Part(metCard + "/Unknown").gameObject.activeSelf, Is.False, "and no question marks");
            Assert.That(Part(unmetCard + "/Unknown").gameObject.activeSelf, Is.True, "one not met yet shows question marks");
            Assert.That(Words(unmetCard + "/Unknown").text, Is.EqualTo(ArenaText.Pair("？？？", "???")));
            Assert.That(Part(unmetCard + "/Name").gameObject.activeSelf || Part(unmetCard + "/Picture").gameObject.activeSelf, Is.False, "and nothing of itself");
            Assert.That(Words(Page + "Shelves/Found").text, Does.Contain("/").And.Contain(listed.Count.ToString()), "the shelf counts what was found of all");

            Click(Part(metCard));
            await Frames(2);
            Assert.That(Words(Page + "Detail/Name").text, Is.EqualTo(ArenaTerms.WeaponForm(met)), "a card met opens its details");
            Assert.That(Words(Page + "Detail/Text").text, Is.Not.Empty, "with how it fights, or that it has not shown it yet");

            Click(Part(unmetCard));
            await Frames(2);
            Assert.That(Words(Page + "Detail/Name").text, Is.EqualTo(ArenaTerms.WeaponForm(met)), "a card not met cannot be opened");
            Assert.That(Part(unmetCard).GetComponent<V5Plate>().Chosen, Is.False, "nor chosen");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Collection_Shelves_SkillsInSixColumns_MonstersFromTheRoster()
        {
            await ReadyScene();
            V5Title.ReloadForTests();
            await ShowWindow("Collection", V5Title.CollectionTab);
            float weaponWidth = Part(Page + "Grid/Card01").rect.width;

            await ClickUntil(Part(Page + "Shelves/Skills"), () => Part(Page + "Shelves/Skills").GetComponent<V5Plate>().Chosen, "the skills' shelf");
            await Settle();
            Assert.That(ActiveCards(), Is.EqualTo(ArenaCodex.ListedSkills().Count()), "a card for every skill listed");
            Assert.That(Part(Page + "Grid/Card01").rect.width, Is.LessThan(weaponWidth), "six to a row, against five weapons");

            await ClickUntil(Part(Page + "Shelves/Monsters"), () => Part(Page + "Shelves/Monsters").GetComponent<V5Plate>().Chosen, "the monsters' shelf");
            await Settle();
            Assert.That(ActiveCards(), Is.EqualTo(ArenaCodex.ListedMonsters().Count()), "a card for every monster in the roster");
            Assert.That(Words(Page + "Shelves/Found").text, Does.Contain(ArenaCodex.ListedMonsters().Count().ToString()), "counted");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Collection_ABossWithALongEnglishName_ItsTagFollowsTheName()
        {
            await ReadyScene();
            var boss = EnemyKind.Tyrant;
            string key = ArenaCodexProgress.Key("enemy", (int)boss);
            if (ArenaPrefs.GetString(key) != "1") touched.Add(key);
            ArenaCodexProgress.DiscoverEnemy(boss);
            var locale = new GameObject("test locale").AddComponent<ArenaLocaleButton>();
            try
            {
                locale.localeCode = "en";
                locale.SelectLocale();
                await Until(() => ArenaText.Ready && !ArenaText.IsChinese, 12f, "the English locale");
                V5Title.ReloadForTests();
                await ShowWindow("Collection", V5Title.CollectionTab);
                await ClickUntil(Part(Page + "Shelves/Monsters"), () => Part(Page + "Shelves/Monsters").GetComponent<V5Plate>().Chosen, "the monsters' shelf");
                await Settle();
                int card = ArenaCodex.ListedMonsters().ToList().IndexOf(boss) + 1;
                Assume.That(card, Is.GreaterThan(0), "the Tyrant is in the roster");
                string name = ArenaText.Text("enemy." + boss);
                await ClickUntil(Part(Page + $"Grid/Card{card:00}"), () => Words(Page + "Detail/Name").text == name, "the Tyrant's details");

                var title = Words(Page + "Detail/Name");
                var tag = Part(Page + "Detail/Tag1");
                Assert.That(tag.gameObject.activeSelf, Is.True, "its Boss tag shows");
                Assert.That(tag.anchoredPosition.x, Is.GreaterThanOrEqualTo(title.rectTransform.anchoredPosition.x + title.preferredWidth),
                    "the tag starts after the name's last letter");
            }
            finally
            {
                locale.localeCode = "zh-Hans";
                locale.SelectLocale();
                await Until(() => ArenaText.Ready && ArenaText.IsChinese, 12f, "back to Chinese");
                Object.Destroy(locale.gameObject);
            }
        }

        static int ActiveCards() => Part(Page + "Grid").Cast<Transform>().Count(card => card.gameObject.activeSelf);

        // ───── Helpers ─────

        static Transform Title() => GameObject.Find("V2Canvas/TitleScreen/Content/" + V5Title.RootName).transform;

        static RectTransform Part(string path)
        {
            var part = Title().Find(path);
            Assert.That(part, Is.Not.Null, $"the title has {path}");
            return (RectTransform)part;
        }

        static Text Words(string path) => Part(path).GetComponent<Text>();

        static SaveSummary[] EmptySlots(SaveMode mode) => Enumerable.Range(0, ArenaSaves.SlotCount).Select(i => new SaveSummary { mode = mode, slot = i }).ToArray();

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f,
                "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>Waits for the title's window to stop moving (its parts slide in), then a frame.</summary>
        static async Task Settle()
        {
            await Until(() => V5Title.Active && V5Title.Active.Settled, 3f, "the title's window settles");
            await Awaitable.NextFrameAsync();
        }

        /// <summary>Clicks a menu item until its window shows.</summary>
        static async Task ShowWindow(string item, int tab)
        {
            await ClickUntil(Part("Menu/" + item), () => V5Title.Active.Tab == tab, $"{item} shows its window");
            await Settle();
        }

        /// <summary>What the pointer lands on over the middle of this rect: the top EventSystem raycast, as a player's click.</summary>
        static GameObject TopHit(RectTransform rect, out PointerEventData pointer)
        {
            Canvas.ForceUpdateCanvases();
            var canvas = rect.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left,
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            return hits.Count > 0 ? hits[0].gameObject : null;
        }

        /// <summary>A real pointer click: the button has to be the one on top where it is drawn.</summary>
        static void Click(RectTransform button)
        {
            var hit = TopHit(button, out var pointer);
            Assert.That(hit, Is.Not.Null, $"{button.name} is under the pointer");
            var receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit);
            Assert.That(receiver, Is.EqualTo(button.gameObject), $"a click on {button.name} reaches it, not {hit.name}");
            ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>Clicks until done holds (the run's commands wait out the menus' input lock after the title opens).</summary>
        static async Task ClickUntil(RectTransform button, Func<bool> done, string what, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + what);
                if (button && button.gameObject.activeInHierarchy) Click(button);
                await Awaitable.NextFrameAsync();
            }
        }

        /// <summary>The part's rectangle in the given space.</summary>
        static Rect Box(Transform part, Transform space)
        {
            var corners = new Vector3[4];
            ((RectTransform)part).GetWorldCorners(corners);
            Vector2 a = space.InverseTransformPoint(corners[0]), b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static async Task Until(Func<bool> completed, float timeout, string operation)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + operation);
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task Frames(int count)
        {
            for (int i = 0; i < count; i++) await Awaitable.NextFrameAsync();
        }
    }
}
