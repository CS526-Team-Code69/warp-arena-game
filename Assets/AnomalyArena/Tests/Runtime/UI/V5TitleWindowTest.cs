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
    /// <summary>The whole game's title (V5Title): each menu item shows its own window and the orange triangle points at it; 继续游戏 is an
    /// item only while a save exists; everything sits on one grid and nothing overlaps; in whitebox the picture goes and the wordmark
    /// keeps the logo's place; the nine interface sounds load. The save windows read made-up summaries (V5Title's test hooks).</summary>
    [TestFixture]
    public class V5TitleWindowTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        static readonly string[] Shown = { "NewGame", "Tutorial", "Collection", "Settings" };
        static readonly int[] Tabs = { V5Title.NewGameTab, V5Title.TutorialTab, V5Title.CollectionTab, V5Title.SettingsTab };

        [SetUp]
        public void SetUp()
        {
            V5Title.SummariesForTests = EmptySlots;
            V5Title.SaveCallsForTests = (_, _, _) => { };
        }

        [TearDown]
        public void TearDown()
        {
            V5Title.SummariesForTests = null;
            V5Title.SaveCallsForTests = null;
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
        }

        [Test]
        public void UiSounds_TheNineChosenTakes_LoadAsClips()
        {
            foreach (var name in new[] { "ui-hover", "ui-press", "ui-panel", "ui-tab", "ui-toggle", "ui-tick", "ui-start", "ui-back", "ui-denied" })
            {
                var clip = V5UiSound.Build(name);
                Assert.That(clip, Is.Not.Null, $"{name} is wav data the game can read");
                Assert.That(clip.length, Is.GreaterThan(.05f), $"{name} has sound in it");
                Assert.That(clip.frequency, Is.EqualTo(48000), $"{name} keeps its own rate");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Menu_EachItem_ShowsItsWindow_TheTriangleGlidesToIt()
        {
            await ReadyScene();
            V5Title.ReloadForTests();
            await Frames(2);
            var pages = Part("Window/Pages");

            for (int k = Shown.Length - 1; k >= 0; k--)
            {
                await ShowWindow(Shown[k], Tabs[k]);
                var item = Part("Menu/" + Shown[k]);
                Rect itemBox = Box(item, Title()), pointer = Box(Part("Pointer"), Title());

                Assert.That(pages.Cast<Transform>().Where(page => page.gameObject.activeSelf).Select(page => page.name), Is.EqualTo(new[] { V5Title.MenuNames[Tabs[k]] }),
                    $"{Shown[k]} shows its own window and no other");
                Assert.That(pointer.center.y, Is.EqualTo(itemBox.center.y).Within(1f), "the triangle points at the item");
                Assert.That(pointer.xMin, Is.GreaterThanOrEqualTo(itemBox.xMax), "from the item's right");
                Assert.That(Shown.Where(name => Part("Menu/" + name).GetComponent<V5Plate>().Chosen).ToArray(), Is.EqualTo(new[] { Shown[k] }), "the item is the chosen one");
                Assert.That(Words("Window/Head/Title").text, Is.EqualTo(Words("Menu/" + Shown[k] + "/Label").text), "the window is named as its item");
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Menu_ContinueIsAnItem_OnlyWhileASaveExists_AndOpensFirst()
        {
            await ReadyScene();
            V5Title.ReloadForTests();
            await Frames(2);
            Assert.That(Part("Menu/Continue").gameObject.activeSelf, Is.False, "without a save the menu has no Continue");
            Assert.That(V5Title.Active.Tab, Is.EqualTo(V5Title.NewGameTab), "and opens on the new game");

            var open = EmptySlots(SaveMode.Open);
            open[1] = new SaveSummary { mode = SaveMode.Open, slot = 1, empty = false, wave = 4, waveCount = 15, savedAtUnixSeconds = 10 };
            V5Title.SummariesForTests = mode => mode == SaveMode.Open ? open : EmptySlots(mode);
            V5Title.ReloadForTests();
            await Settle();
            var items = new[] { "Continue" }.Concat(Shown).Select(name => Box(Part("Menu/" + name), Title())).ToArray();

            Assert.That(Part("Menu/Continue").gameObject.activeSelf, Is.True, "a save brings Continue");
            Assert.That(items.Zip(items.Skip(1), (upper, lower) => upper.yMin >= lower.yMax), Has.All.True, "at the top, the others under it");
            Assert.That(V5Title.Active.Tab, Is.EqualTo(V5Title.ContinueTab), "and the title opens on it");
            Assert.That(Part("Window/Pages/Continue/Modes/Open").GetComponent<V5Plate>().Chosen, Is.True, "on the mode of the most recent save");
            Assert.That(Part("Window/Pages/Continue/Slot2").GetComponent<V5Plate>().Chosen, Is.True, "with that save chosen");

            V5Title.SummariesForTests = EmptySlots;
            V5Title.ReloadForTests();
            await Settle();
            Assert.That(Part("Menu/Continue").gameObject.activeSelf, Is.False, "no save left, no Continue");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task Layout_OneGrid_NothingOverlaps_InAnyWindow()
        {
            await ReadyScene();
            var open = EmptySlots(SaveMode.Open);
            open[0] = new SaveSummary { mode = SaveMode.Open, slot = 0, empty = false, wave = 9, waveCount = 15, mapFamily = 1, savedAtUnixSeconds = 5,
                weapons = new[] { WeaponForm.Rifle, WeaponForm.ScatterCannon, WeaponForm.Minigun, WeaponForm.OrbitBlades } };
            V5Title.SummariesForTests = mode => mode == SaveMode.Open ? open : EmptySlots(mode);
            V5Title.ReloadForTests();
            await Settle();
            var hud = Object.FindAnyObjectByType<V2Hud>();
            var reference = hud.GetComponentInParent<CanvasScaler>().referenceResolution;
            var windows = new[] { ("Continue", V5Title.ContinueTab) }.Concat(Shown.Zip(Tabs, (name, tab) => (name, tab))).ToArray();

            foreach (var (item, tab) in windows)
            {
                await ShowWindow(item, tab);
                // Measured on the reference canvas at once, as the title is in FlowInputTest: a batch run's window is not 16:9 (pitfall T-028).
                ((RectTransform)hud.transform).sizeDelta = reference;
                Canvas.ForceUpdateCanvases();
                var space = hud.transform;
                var canvas = ((RectTransform)space).rect;
                var menu = Part("Menu").Cast<Transform>().Where(part => part.gameObject.activeSelf).Select(part => Box(part, space)).ToArray();
                Rect window = Box(Part("Window"), space), logo = Box(Part("WarpArenaLogo"), space), pointer = Box(Part("Pointer"), space);

                Assert.That(menu.Select(box => box.xMin), Has.All.EqualTo(menu[0].xMin).Within(.5f), $"{item}: the menu's items share a left edge");
                Assert.That(Mathf.Abs(logo.xMin - menu[0].xMin), Is.LessThan(5f), $"{item}: the logo starts on the menu's left edge");
                Assert.That(window.yMax, Is.EqualTo(menu[0].yMax).Within(.5f), $"{item}: the window and the menu share a top");
                Assert.That(Mathf.Abs((window.yMin - canvas.yMin) - (canvas.yMax - logo.yMax)), Is.LessThan(6f), $"{item}: the window ends as far above the foot as the logo starts below the top");
                Assert.That(window.yMin, Is.GreaterThanOrEqualTo(canvas.yMin), $"{item}: the window is on the canvas");
                var parts = menu.Select((box, i) => ($"item {i + 1}", box)).Append(("window", window)).Append(("logo", logo)).Append(("pointer", pointer)).ToList();
                var build = Title().parent.Find("BuildLabel");
                if (build && build.gameObject.activeInHierarchy && build.GetComponent<Text>().text.Length > 0) parts.Add(("build line", Box(build, space)));
                for (int i = 0; i < parts.Count; i++)
                    for (int j = i + 1; j < parts.Count; j++)
                        Assert.That(parts[i].Item2.Overlaps(parts[j].Item2), Is.False, $"{item}: the {parts[i].Item1} overlaps the {parts[j].Item1}");
                Assert.That(menu.Zip(menu.Skip(1), (upper, lower) => upper.yMin >= lower.yMax), Has.All.True, $"{item}: the items one under the other");

                // The window's own parts: its head, the page's blocks and its foot, apart from one another and inside it.
                var blocks = new List<(string name, Rect box)> { ("head", Box(Part("Window/Head"), space)) };
                foreach (Transform block in Part("Window/Pages/" + V5Title.MenuNames[tab]))
                    if (block.gameObject.activeSelf && block.GetComponent<V5Block>()) blocks.Add((block.name, Box(block, space)));
                if (Part("Window/Foot").gameObject.activeSelf) blocks.Add(("foot", Box(Part("Window/Foot"), space)));
                foreach (var (name, box) in blocks)
                    Assert.That(box.xMin >= window.xMin && box.xMax <= window.xMax && box.yMin >= window.yMin && box.yMax <= window.yMax, Is.True,
                        $"{item}: {name} is inside the window");
                for (int i = 0; i < blocks.Count; i++)
                    for (int j = i + 1; j < blocks.Count; j++)
                        Assert.That(blocks[i].box.Overlaps(blocks[j].box), Is.False, $"{item}: {blocks[i].name} overlaps {blocks[j].name}");
                await Frames(3); // the canvas takes its own size back
            }
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Whitebox_ThePictureGoes_TheWordmarkTakesTheLogosPlace_ThePlatesAreFlat()
        {
            bool was = Art.Enabled;
            try
            {
                await ReadyScene();
                Art.Enabled = true;
                await Frames(3);
                var art = Title().parent.Find(V5Title.ArtName);
                Assume.That(art.Find("Picture").GetComponent<RawImage>().texture, Is.Not.Null, "the title picture is in the project");
                Assert.That(art.gameObject.activeSelf, Is.True, "with the art on the crash-site picture fills the screen");
                Assert.That(Part("Window").GetComponent<Image>().sprite, Is.Not.Null, "and the window is a drawn plate");

                Art.Enabled = false;
                await Frames(4);
                Text wordmark = null;
                foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude))
                    if (text.name == "WhiteboxWordmark") wordmark = text;
                Assert.That(art.gameObject.activeSelf, Is.False, "in whitebox the picture goes (the saved title's flat backdrop shows)");
                Assert.That(wordmark, Is.Not.Null, "the whitebox title shows the game's name as text");
                Assert.That(wordmark.rectTransform.anchoredPosition, Is.EqualTo(Part("WarpArenaLogo").anchoredPosition), "in the logo's place");
                Assert.That(wordmark.font, Is.Not.SameAs(ArenaText.Font), "in a font of its own (FontAtlasGuard)");
                Assert.That(Part("Window").GetComponent<Image>().sprite, Is.Null, "the window is a flat panel");
                Assert.That(Part("Menu/NewGame").GetComponent<Image>().sprite, Is.Null, "and the menu's items flat plates");
            }
            finally { Art.Enabled = was; }
        }

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
