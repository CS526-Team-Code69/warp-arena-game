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
using System.Reflection;

namespace AnomalyArena
{
    /// <summary>The title's settings window (V5Title): the volume in ten steps, the art mode on the title, and the keys with ArenaKeys'
    /// rules and the settings panel's words (a row takes the next key, a taken key swaps, a fixed key is refused and the row keeps
    /// waiting, Esc gives up, Reset brings the defaults back); while a row waits the game's keys are held. Keys are pressed through the
    /// window's own entry points (TakeKeyByName, Back): device keys never reach the game in the batch test runner.</summary>
    [TestFixture]
    public class V5TitleSettingsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        const string Page = "Window/Pages/Settings/";
        // Every static field of Art, saved before each test and put back after it (as TitleSettingsTest).
        static readonly FieldInfo[] ArtState = typeof(Art).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(f => !f.IsLiteral && !f.IsInitOnly && f.FieldType != typeof(MaterialPropertyBlock)).ToArray();
        object[] savedArtState;

        [SetUp]
        public void SetUp()
        {
            Assume.That(ArenaPrefs.MemoryForTests, Is.True, "the test run keeps the saved choices in memory (TestSavedSettings)");
            savedArtState = ArtState.Select(f => f.GetValue(null)).ToArray();
            ArenaKeys.ResetToDefaults();
            ArenaVolume.Set(ArenaVolume.Default);
            V5Title.SummariesForTests = EmptySlots;
        }

        [TearDown]
        public void TearDown()
        {
            V5Title.Back();
            if (savedArtState != null)
                for (int i = 0; i < ArtState.Length; i++) ArtState[i].SetValue(null, savedArtState[i]);
            savedArtState = null;
            ArenaKeys.ResetToDefaults();
            ArenaVolume.Set(ArenaVolume.Default);
            V5Title.SummariesForTests = null;
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Keys_ARowTakesTheNextKey_ATakenKeySwaps_AFixedKeyIsRefused_EscGivesUp_ResetBringsTheDefaultsBack()
        {
            await ReadyScene();
            V5Title.ReloadForTests();
            await ShowWindow("Settings", V5Title.SettingsTab);
            RectTransform interact = Part(Page + "Keys/KeyInteract/Key"), lamp = Part(Page + "Keys/KeyLamp/Key");
            Text interactKey = Words(Page + "Keys/KeyInteract/Key/Label"), lampKey = Words(Page + "Keys/KeyLamp/Key/Label"), hint = Words(Page + "KeysHead/Hint");
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "E", "F" }), "the rows show the keys as they are");
            Assert.That(V5Title.TakeKeyByName("G"), Is.EqualTo(KeyBindResult.Refused), "with no row waiting a key pressed changes nothing");

            Click(interact);
            Assert.That(V5Title.IsListening(out var waiting) && waiting == GameAction.Interact, Is.True, "a click on a row's key waits for the next key");
            Assert.That(ArenaKeys.Suspended && ArenaSettingsPanel.HoldsInput, Is.True, "and the game's keys are held meanwhile");
            Assert.That(V5Title.Back(), Is.True, "Esc gives up");
            Assert.That(V5Title.IsListening(out _), Is.False);
            Assert.That(ArenaKeys.InteractKey, Is.EqualTo("E"), "the key is as it was");
            await Frames(1);
            Assert.That(ArenaKeys.Suspended || ArenaSettingsPanel.HoldsInput, Is.False, "and the game has its keys back");

            Click(interact);
            Assert.That(V5Title.TakeKeyByName("Tab"), Is.EqualTo(KeyBindResult.Refused), "Tab is one of the menus' fixed keys");
            Assert.That(V5Title.IsListening(out _), Is.True, "the row still waits");
            await Frames(1);
            Assert.That(hint.text, Is.Not.Empty.And.Not.EqualTo(ArenaSettingsPanel.KeysHint(true)), "and the window says why the key was not taken");
            Assert.That(V5Title.TakeKeyByName("G"), Is.EqualTo(KeyBindResult.Bound));
            Assert.That(V5Title.IsListening(out _), Is.False, "a key taken ends the waiting");
            await Frames(1);
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "G", "F" }), "the row shows the new key");

            Click(lamp);
            Assert.That(V5Title.TakeKeyByName("G"), Is.EqualTo(KeyBindResult.Swapped));
            await Frames(1);
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "F", "G" }), "both rows show the swap");
            Assert.That(hint.text, Is.EqualTo(ArenaSettingsPanel.BoundMessage(GameAction.Lamp, KeyBindResult.Swapped, GameAction.Interact)), "and the window says so");

            Click(Part("Window/Foot/Reset"));
            await Frames(1);
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "E", "F" }), "Reset brings the defaults back");
            Assert.That(ArenaKeys.SkillKeys, Is.EqualTo("1 / 2 / 3"));
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Volume_TenSteps_SetWhatIsHeardAtOnce_NoNumberShown()
        {
            await ReadyScene();
            V5Title.ReloadForTests();
            await ShowWindow("Settings", V5Title.SettingsTab);
            var slider = Part(Page + "Volume/Slider").GetComponent<Slider>();
            Assert.That(slider.wholeNumbers && Mathf.Approximately(slider.maxValue, 10f), Is.True, "ten steps");

            slider.value = 3f;
            await Frames(1);
            Assert.That(ArenaVolume.Master, Is.EqualTo(.3f).Within(.001f));
            Assert.That(AudioListener.volume, Is.EqualTo(.3f).Within(.001f), "everything that sounds is scaled at once");
            Assert.That(Part(Page + "Volume/Slider/Track").Cast<Transform>().Count(cell => cell.gameObject.activeSelf), Is.EqualTo(3), "three cells of the bar lit");
            Assert.That(Part(Page + "Volume").GetComponentsInChildren<Text>().Select(text => text.text).Where(text => text.Contains("%")), Is.Empty, "and no percentage");

            slider.value = 0f;
            await Frames(1);
            Assert.That(ArenaVolume.Master, Is.Zero, "the lowest step is silence");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task ArtMode_TheToggle_SwitchesTheArtOnTheTitle()
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase == ArenaUiState.Title && !Art.IllustratedOnly, Is.True, "the title of a scene with both looks");
            V5Title.ReloadForTests();
            await ShowWindow("Settings", V5Title.SettingsTab);
            bool illustrated = Art.Enabled;
            var other = Part(Page + "Art/Toggle/" + (illustrated ? "Whitebox" : "Illustrated"));
            var same = Part(Page + "Art/Toggle/" + (illustrated ? "Illustrated" : "Whitebox"));

            float until = Time.realtimeSinceStartup + .7f;
            while (Time.realtimeSinceStartup < until)
            {
                Click(same);
                await Awaitable.NextFrameAsync();
            }
            Assert.That(Art.Enabled, Is.EqualTo(illustrated), "a click on the mode showing switches nothing");

            await ClickUntil(other, () => Art.Enabled != illustrated, "a click on the other mode switches to it");
            Assert.That(game.UiSnapshot.illustrated, Is.EqualTo(!illustrated), "the game shows the other look");
            await ClickUntil(same, () => Art.Enabled == illustrated, "and back");
            Assert.That(V5Title.Active.Tab, Is.EqualTo(V5Title.SettingsTab), "the settings window stays meanwhile");
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
