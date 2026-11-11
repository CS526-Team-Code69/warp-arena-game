using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The open mode's map pick (the course asks for a level select). The title's "Start Game - Open
    /// Mode" opens it in place of the title's entries: the Dunes, the Ruins, a random map, and back to the title (also Esc). A picked map
    /// starts the classic 15-wave run on a map of that family, reloading the scene when the map laid was another; a random pick keeps the map
    /// laid at random, as before. The story mode never comes here.</summary>
    [TestFixture]
    public class OpenModeMapSelectTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        static readonly string[] TitleEntries = { V2Hud.TitleStoryButton, V2Hud.TitleOpenButton, V2Hud.TitleTutorialButton, V2Hud.TitleSettingsButton, V2Hud.TitleLanguageButton };
        readonly List<Biome> madeMaps = new();

        [SetUp]
        public void SetUp()
        {
            V2Tutorial.ForgetShown();
            WorldMap.NextMapFamily = null;
            GameManager.ForgetOpenModeStartForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var map in madeMaps) Object.DestroyImmediate(map);
            madeMaps.Clear();
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
            V2Tutorial.ForgetShown();
            WorldMap.NextMapFamily = null;
            GameManager.ForgetOpenModeStartForTests();
            TestStartLanguage.Reset();
            Time.timeScale = 1f;
        }

        // ───── Which map a pick lays (no scene) ─────

        [TestCase(Biome.Dunes, new[] { 1, 3 })]
        [TestCase(Biome.Ruins, new[] { 0 })]
        public void PickForRun_Family_PicksOnlyItsMapsInRotation(string family, int[] expected)
        {
            var maps = Maps();

            var picks = Enumerable.Range(0, 60).Select(_ => Biome.PickForRun(maps, family)).Distinct().ToArray();

            Assert.That(picks, Is.SubsetOf(expected));
        }

        [TestCase((string)null)]
        [TestCase("")]
        [TestCase("Craters")]
        public void PickForRun_NoOrUnknownFamily_PicksAnyMapInRotationAsBefore(string family)
        {
            var maps = Maps();

            var picks = Enumerable.Range(0, 60).Select(_ => Biome.PickForRun(maps, family)).Distinct().ToArray();

            Assert.That(picks, Is.SubsetOf(new[] { 0, 1, 3 }));
        }

        /// <summary>The saved maps' names: Ruins and Dunes in rotation, the Rift Zone out of it, and one Ruins map taken out here.</summary>
        Biome[] Maps() => new[] { Map("Ruins · 1", true), Map("Dunes · 1", true), Map("Rift Zone · 1", false), Map("Dunes · 2", true), Map("Ruins · 2", false) };

        Biome Map(string name, bool inRotation)
        {
            var map = ScriptableObject.CreateInstance<Biome>();
            map.displayName = name;
            map.inRotation = inRotation;
            madeMaps.Add(map);
            return map;
        }

        // ───── The pick on the title ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task OpenMode_Click_ShowsTheMapPickInPlaceOfTheEntries()
        {
            var game = await ReadyScene();
            var content = TitleContent();

            await ClickUntil(Part(content, V2Hud.TitleOpenButton), () => Hud().MapSelectOpen, "the open mode opens the map pick");
            await Awaitable.NextFrameAsync();

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "no run starts before a map is picked");
            Assert.That(TitleEntries.Where(name => Part(content, name).gameObject.activeInHierarchy), Is.Empty, "the title's entries make room for it");
            Assert.That(V2Hud.MapButtons.Where(name => !MapButton(content, name).gameObject.activeInHierarchy), Is.Empty, "its four buttons show");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(MapButton(content, "MapDunes").gameObject),
                "the keyboard's selection starts on the first map, as the title's starts on its first entry");
        }

        [TestCase(StartupLanguage.Chinese, "沙丘", "遗迹", "随机", "返回标题")]
        [TestCase(StartupLanguage.English, "Dunes", "Ruins", "Random", "Back to Title")]
        [BuildScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task MapPick_EachLanguage_ItsWordsTopDown_WholeAndApart(string language, string dunes, string ruins, string random, string back)
        {
            TestStartLanguage.Use(language);
            await SceneManagerHelper.LoadSceneAsync(Scene);
            await ReadyScene();
            var content = TitleContent();
            var hud = Hud();
            hud.OpenMapSelect();
            var labels = V2Hud.MapButtons.Select(name => MapButton(content, name).GetComponentInChildren<UnityEngine.UI.Text>()).ToArray();
            await Until(() => labels[0].text == dunes, 3f, "the map pick's words in the language the game started in");
            await Awaitable.NextFrameAsync();

            // Measured on the reference canvas, as the title is in FlowInputTest: a batch run's window is not 16:9.
            ((RectTransform)hud.transform).sizeDelta = hud.GetComponentInParent<UnityEngine.UI.CanvasScaler>().referenceResolution;
            Canvas.ForceUpdateCanvases();
            var line = content.Find(V2Hud.MapSelect + "/MapLine");
            var stack = new[] { line }.Concat(V2Hud.MapButtons.Select(name => MapButton(content, name))).Select(part => Box(part, hud.transform)).ToArray();
            var canvas = ((RectTransform)hud.transform).rect;

            Assert.That(labels.Select(label => label.text), Is.EqualTo(new[] { dunes, ruins, random, back }), "the Dunes, the Ruins, a random map, back");
            Assert.That(labels.Where(label => label.cachedTextGenerator.characterCountVisible < label.text.Length).Select(label => label.text), Is.Empty,
                "every word shows in full");
            Assert.That(stack.Zip(stack.Skip(1), (upper, lower) => upper.yMin >= lower.yMax), Has.All.True,
                "the line, then each button under the last, none overlapping");
            // The line of key hints that lay under the pick is no longer shown; the pick still ends above the canvas' foot.
            Assert.That(stack.Where(box => box.yMin < canvas.yMin || box.yMax > canvas.yMax), Is.Empty, "and none off the canvas");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task MapPick_BackButton_ReturnsToTheEntries_TheSelectionOnTheOpenMode()
        {
            var game = await ReadyScene();
            var content = TitleContent();
            await ClickUntil(Part(content, V2Hud.TitleOpenButton), () => Hud().MapSelectOpen, "the open mode opens the map pick");
            await Awaitable.NextFrameAsync();

            Click(MapButton(content, "MapBack"));
            await Awaitable.NextFrameAsync();

            Assert.That(Hud().MapSelectOpen, Is.False, "the back button closes the map pick");
            Assert.That(TitleEntries.Where(name => !Part(content, name).gameObject.activeInHierarchy), Is.Empty, "the title's entries are back");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.SameAs(Part(content, V2Hud.TitleOpenButton).gameObject), "the selection is back on the open mode");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "on the title");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task MapPick_Esc_ReturnsToTheEntries()
        {
            var game = await ReadyScene();
            var content = TitleContent();
            await ClickUntil(Part(content, V2Hud.TitleOpenButton), () => Hud().MapSelectOpen, "the open mode opens the map pick");
            using var keyboard = new TestKeyboard();

            await keyboard.TapUntil(KeyCode.Escape, () => !Hud().MapSelectOpen, "Esc closes the map pick");

            Assert.That(Part(content, V2Hud.TitleOpenButton).gameObject.activeInHierarchy, Is.True, "the title's entries are back");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "on the title");
        }

        [TestCase(0, Biome.Dunes), TestCase(1, Biome.Ruins), LoadScene(Scene), Category("Integration"), Timeout(90000)]
        public async Task MapPick_AMap_StartsTheClassicRunOnAMapOfThatFamily_WithItsFirstPages(int choice, string family)
        {
            var game = await ReadyScene();
            bool laidInFamily = WorldMap.Active.biome.InFamily(family);
            var content = TitleContent();
            await ClickUntil(Part(content, V2Hud.TitleOpenButton), () => Hud().MapSelectOpen, "the open mode opens the map pick");
            await Awaitable.NextFrameAsync();
            var button = MapButton(content, V2Hud.MapButtons[choice]);

            // A map other than the laid one reloads the scene: the button goes with the old scene, whose run never starts.
            await ClickUntil(button, () => !button || game.RunPhase != ArenaUiState.Title, "the pick is taken");
            await Until(() => GameManager.Instance && GameManager.Instance.RunPhase == ArenaUiState.SupplyDrop, 20f, "the run starts on the picked map");

            Assert.That(ReferenceEquals(GameManager.Instance, game), Is.EqualTo(laidInFamily),
                "the scene reloads exactly when the map laid was of another family, and the scene it leaves starts nothing");
            Assert.That(WorldMap.Active.biome.InFamily(family), Is.True, $"the run is on a {family} map, not {WorldMap.Active.biome.displayName}");
            Assert.That(GameManager.Instance.CampaignRunning, Is.False, "it is the classic 15-wave run, not the story");
            await Until(() => V2Tutorial.IsOpen, 3f, "its first-time pages open on arrival, as after a start from the title");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task MapPick_Random_StartsOnTheMapLaidAtRandom_WithoutAReload()
        {
            var game = await ReadyScene();
            var laid = WorldMap.Active.biome;
            var content = TitleContent();
            await ClickUntil(Part(content, V2Hud.TitleOpenButton), () => Hud().MapSelectOpen, "the open mode opens the map pick");
            await Awaitable.NextFrameAsync();

            await ClickUntil(MapButton(content, "MapRandom"), () => game.RunPhase != ArenaUiState.Title, "the random pick is taken");

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.SupplyDrop), "the classic run starts with its supply crate");
            Assert.That(GameManager.Instance, Is.SameAs(game), "in the same scene");
            Assert.That(WorldMap.Active.biome, Is.SameAs(laid), "on the map laid at random, as before the pick existed");
            Assert.That(game.CampaignRunning, Is.False, "the classic run, not the story");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task StoryMode_Click_GoesStraightToTheCampaign_WithoutTheMapPick()
        {
            await ReadyScene();
            var content = TitleContent();

            await ClickUntil(Part(content, V2Hud.TitleStoryButton), () => V2Campaign.Active && V2Campaign.Active.Running, "the story mode starts the campaign");

            Assert.That(Hud().MapSelectOpen, Is.False, "the story mode never shows the map pick");
        }

        // ───── Helpers ─────

        static V2Hud Hud() => Object.FindAnyObjectByType<V2Hud>();

        static Transform TitleContent() => GameObject.Find("V2Canvas/TitleScreen/Content").transform;

        static RectTransform Part(Transform content, string name) => (RectTransform)content.Find(name);

        static RectTransform MapButton(Transform content, string name) => (RectTransform)content.Find(V2Hud.MapSelect + "/" + name);

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f,
                "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>Clicks until done holds: the menus take no input for a moment after the title opens (GameManager's menu lock).</summary>
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
    }
}
