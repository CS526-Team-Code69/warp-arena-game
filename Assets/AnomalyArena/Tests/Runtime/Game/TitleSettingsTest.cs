using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using TestHelper.Attributes;
using TestHelper.RuntimeInternals;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>The title's settings, in the saved arena. The title shows neither the line of key hints nor the art switch under its
    /// entries; a Settings button stands in the top-left corner, the Language option's counterpart. It opens the settings panel over
    /// the title: the panel takes every click and holds the game's keys, and Esc or its Close button shuts it. In it the art mode
    /// switches as the title's old switch did, the slider sets the volume and the volume is kept, and a row of the key list takes the
    /// next key pressed (a taken key swaps, a fixed key is refused, Esc gives up, Reset brings the defaults back). The pause screen
    /// opens the same panel: the art mode cannot be switched there, and the pause table and the bottom row name a changed key.
    /// Keys are pressed through the panel's own entry points (Back, TakeKeyByName): device events never reach the game in the
    /// batch-mode test runner. The saved choices live in memory for the whole run (TestSavedSettings).</summary>
    [TestFixture]
    public class TitleSettingsTest
    {
        const string Scene = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        static readonly string[] TitleButtons =
            { V2Hud.TitleStoryButton, V2Hud.TitleOpenButton, V2Hud.TitleTutorialButton, V2Hud.TitleSettingsButton, V2Hud.TitleLanguageButton };
        // Every static field of Art (the switch, whether the first run took its default), saved before each test and put back after it.
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
        }

        [TearDown]
        public void TearDown()
        {
            if (ArenaSettingsPanel.IsOpen) ArenaSettingsPanel.Close();
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
            if (savedArtState != null)
                for (int i = 0; i < ArtState.Length; i++) ArtState[i].SetValue(null, savedArtState[i]);
            savedArtState = null;
            ArenaKeys.ResetToDefaults();
            ArenaVolume.Set(ArenaVolume.Default);
            TestStartLanguage.Reset();
            Time.timeScale = 1f;
        }

        // ───── The title ─────

        [TestCase(StartupLanguage.Chinese, "设置"), TestCase(StartupLanguage.English, "Settings"), BuildScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Title_EachLanguage_NoKeyHintsNoArtSwitch_ASettingsButtonTopLeftMirrorsLanguage(string language, string word)
        {
            TestStartLanguage.Use(language);
            await SceneManagerHelper.LoadSceneAsync(Scene);
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            var content = TitleContent();
            var settings = Part(content, V2Hud.TitleSettingsButton);
            Assert.That(settings && settings.gameObject.activeInHierarchy, Is.True, "the title shows the Settings button");
            var label = settings.GetComponentInChildren<Text>();
            await Until(() => label.text == word, 3f, "the Settings button is labelled in the start language");
            await Awaitable.NextFrameAsync();

            // Measured on the reference canvas, as the title is in FlowInputTest: a batch run's window is not 16:9.
            var hud = Object.FindAnyObjectByType<V2Hud>();
            ((RectTransform)hud.transform).sizeDelta = hud.GetComponentInParent<CanvasScaler>().referenceResolution;
            Canvas.ForceUpdateCanvases();
            var canvas = ((RectTransform)hud.transform).rect;
            Rect box = Box(settings, hud.transform), other = Box(Part(content, V2Hud.TitleLanguageButton), hud.transform);

            foreach (var gone in new[] { "Controls", "ToggleArt" })
                Assert.That(content.Find(gone) is { } part && part.gameObject.activeInHierarchy, Is.False, gone + " is not shown");
            Assert.That(content.GetComponentsInChildren<Text>().Select(text => text.text).Where(text => text.Contains("WASD")), Is.Empty,
                "no text on the title lists the keys");
            Assert.That(box.center.x, Is.LessThan(canvas.center.x - canvas.width * .25f), "the Settings button sits at the left");
            Assert.That(box.center.y, Is.GreaterThan(canvas.center.y + canvas.height * .25f), "and at the top");
            Assert.That(box.xMin - canvas.xMin, Is.EqualTo(canvas.xMax - other.xMax).Within(1f), "as far from its corner as Language is from the other");
            Assert.That(box.yMax, Is.EqualTo(other.yMax).Within(1f), "at Language's height");
            Assert.That(box.size.x, Is.EqualTo(other.size.x).Within(.5f), "as wide");
            Assert.That(box.size.y, Is.EqualTo(other.size.y).Within(.5f), "and as tall");
            Assert.That(label.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(label.text.Length), "its word shows in full");
            Assert.That(settings.TryGetComponent<ArenaUiButton>(out _), Is.False, "it sends no run command");
        }

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task SettingsButton_Click_OpensThePanelOverTheTitle_EscOrCloseShutsIt()
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            var content = TitleContent();
            var settings = Part(content, V2Hud.TitleSettingsButton);
            await Awaitable.NextFrameAsync();
            float titleTime = Time.timeScale;

            Click(settings);
            Assert.That(ArenaSettingsPanel.IsOpen && ArenaSettingsPanel.HoldsInput, Is.True, "a click opens the panel, which holds the game's keys");
            Assert.That(ArenaKeys.Suspended, Is.True, "and no action's key is read behind it");
            await Awaitable.NextFrameAsync();
            foreach (var name in TitleButtons)
            {
                var hit = TopHit(Part(content, name), out _);
                Assert.That(hit && hit.GetComponentInParent<ArenaSettingsPanel>(), Is.True, $"the panel covers {name}, so a click there stays on it");
            }
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "the title is still under it");
            Assert.That(Time.timeScale, Is.EqualTo(titleTime), "and its time is left alone");

            ArenaSettingsPanel.Back(); // Esc
            Assert.That(ArenaSettingsPanel.IsOpen, Is.False, "Esc closes the panel");
            Assert.That(ArenaSettingsPanel.HoldsInput, Is.True, "the closing frame is held too, so its Esc does nothing else");
            Assert.That(ArenaKeys.Suspended, Is.False, "the actions' keys are read again");
            await Awaitable.NextFrameAsync();
            Assert.That(ArenaSettingsPanel.HoldsInput, Is.False, "then the game has its keys back");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "the title is still there");
            Assert.That(Object.FindAnyObjectByType<V2Hud>().MapSelectOpen, Is.False, "as it was");

            Click(settings);
            await Awaitable.NextFrameAsync();
            Click(PanelPart("Close"));
            Assert.That(ArenaSettingsPanel.IsOpen, Is.False, "the Close button closes it too");
            await Awaitable.NextFrameAsync();
            foreach (var name in TitleButtons)
            {
                var hit = TopHit(Part(content, name), out _);
                Assert.That(hit && !hit.GetComponentInParent<ArenaSettingsPanel>(), Is.True, $"a click reaches {name} again");
            }
        }

        // ───── Art mode ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task ArtMode_InThePanel_SwitchesAsTheTitlesSwitchDid_AndMarksTheModeShowing()
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase == ArenaUiState.Title && !Art.IllustratedOnly, Is.True, "the title of a scene with both looks");
            Click(Part(TitleContent(), V2Hud.TitleSettingsButton));
            await Awaitable.NextFrameAsync();
            bool illustrated = Art.Enabled;
            RectTransform showing = PanelPart(illustrated ? "ArtIllustrated" : "ArtWhitebox"), other = PanelPart(illustrated ? "ArtWhitebox" : "ArtIllustrated");
            Assert.That(Mark(showing).enabled && !Mark(other).enabled, Is.True, "the mode showing is the one marked");
            Assert.That(showing.GetComponent<Button>().interactable && other.GetComponent<Button>().interactable, Is.True, "on the title both can be pressed");

            // The mode already showing: a click on it changes nothing, however long the menus have been open.
            float until = Time.realtimeSinceStartup + .7f;
            while (Time.realtimeSinceStartup < until)
            {
                Click(showing);
                await Awaitable.NextFrameAsync();
            }
            Assert.That(Art.Enabled, Is.EqualTo(illustrated), "a click on the mode showing switches nothing");

            await ClickUntil(other, () => Art.Enabled != illustrated, "a click on the other mode switches to it");
            Assert.That(game.UiSnapshot.illustrated, Is.EqualTo(!illustrated), "the game shows the other look, as after the title's old switch");
            await Awaitable.NextFrameAsync();
            Assert.That(Mark(other).enabled && !Mark(showing).enabled, Is.True, "and the mark moves with it");

            await ClickUntil(showing, () => Art.Enabled == illustrated, "a click on the first mode switches back");
            Assert.That(game.UiSnapshot.illustrated, Is.EqualTo(illustrated));
            Assert.That(ArenaSettingsPanel.IsOpen && game.RunPhase == ArenaUiState.Title, Is.True, "the panel stays open over the title meanwhile");
        }

        // ───── Volume ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Volume_TheSlider_SetsWhatIsHeardAtOnce_AndTheVolumeIsKept()
        {
            await ReadyScene();
            Click(Part(TitleContent(), V2Hud.TitleSettingsButton));
            await Awaitable.NextFrameAsync();
            var slider = PanelPart("Volume").GetComponent<Slider>();
            Assert.That(slider.value, Is.EqualTo(ArenaVolume.Default).Within(.001f), "the slider opens on the volume as it is: full, with nothing saved");

            slider.value = .35f;
            await Awaitable.NextFrameAsync();

            Assert.That(ArenaVolume.Master, Is.EqualTo(.35f).Within(.001f));
            Assert.That(AudioListener.volume, Is.EqualTo(.35f).Within(.001f), "everything that sounds is scaled at once");
            Assert.That(PanelPart("VolumeValue").GetComponent<Text>().text, Is.EqualTo("35%"), "and the panel says how much");

            ArenaSettingsPanel.Back();
            AudioListener.volume = 1f; // as a new play starts
            ArenaVolume.Reload();
            Assert.That(ArenaVolume.Master, Is.EqualTo(.35f).Within(.001f), "the volume is read back at the next start");
            Assert.That(AudioListener.volume, Is.EqualTo(.35f).Within(.001f), "and applied");
        }

        // ───── Keys ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(60000)]
        public async Task Keys_ARowTakesTheNextKey_ATakenKeySwaps_AFixedKeyIsRefused_EscGivesUp_ResetBringsTheDefaultsBack()
        {
            await ReadyScene();
            Click(Part(TitleContent(), V2Hud.TitleSettingsButton));
            await Awaitable.NextFrameAsync();
            RectTransform interact = PanelPart("KeyInteract/Key"), lamp = PanelPart("KeyLamp/Key");
            Text interactKey = interact.GetComponentInChildren<Text>(), lampKey = lamp.GetComponentInChildren<Text>(), message = PanelPart("Message").GetComponent<Text>();
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "E", "F" }), "the rows show the keys as they are");
            Assert.That(ArenaSettingsPanel.TakeKeyByName("G"), Is.EqualTo(KeyBindResult.Refused), "with no row waiting a key pressed changes nothing");
            Assert.That(ArenaKeys.InteractKey, Is.EqualTo("E"));

            // Esc gives up the waiting row and leaves the panel open.
            Click(interact);
            Assert.That(ArenaSettingsPanel.IsListening(out var waiting) && waiting == GameAction.Interact, Is.True, "a click on a row's key waits for the next key");
            ArenaSettingsPanel.Back();
            Assert.That(ArenaSettingsPanel.IsOpen && !ArenaSettingsPanel.IsListening(out _), Is.True, "Esc gives up; the panel stays");
            Assert.That(ArenaKeys.InteractKey, Is.EqualTo("E"), "and the key is as it was");

            // A fixed key is refused and the row keeps waiting; the next key is taken.
            Click(interact);
            Assert.That(ArenaSettingsPanel.TakeKeyByName("Tab"), Is.EqualTo(KeyBindResult.Refused), "Tab is one of the menus' fixed keys");
            Assert.That(ArenaSettingsPanel.IsListening(out _), Is.True, "the row still waits");
            await Awaitable.NextFrameAsync();
            Assert.That(message.text, Is.Not.Empty, "and the panel says why the key was not taken");
            Assert.That(ArenaSettingsPanel.TakeKeyByName("G"), Is.EqualTo(KeyBindResult.Bound));
            Assert.That(ArenaSettingsPanel.IsListening(out _), Is.False, "a key taken ends the waiting");
            await Awaitable.NextFrameAsync();
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "G", "F" }), "the row shows the new key");

            // A key another action holds: the two swap.
            Click(lamp);
            Assert.That(ArenaSettingsPanel.TakeKeyByName("G"), Is.EqualTo(KeyBindResult.Swapped));
            await Awaitable.NextFrameAsync();
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "F", "G" }), "both rows show the swap");
            Assert.That(message.text, Is.Not.Empty, "and the panel says the two swapped");

            Click(PanelPart("ResetKeys"));
            await Awaitable.NextFrameAsync();
            Assert.That(new[] { interactKey.text, lampKey.text }, Is.EqualTo(new[] { "E", "F" }), "Reset brings the defaults back");
            Assert.That(ArenaKeys.SkillKeys, Is.EqualTo("1 / 2 / 3"));
        }

        // ───── The pause screen ─────

        [Test, LoadScene(Scene), Category("Integration"), Timeout(120000)]
        public async Task Pause_ItsSettingsButton_OpensThePanel_TheArtModeCannotBeSwitchedThere_AndTheTableAndTheBottomRowNameAChangedKey()
        {
            var game = await ReadyScene();
            await StartRun(game);
            await CommandUntil(game, ArenaUiCommand.Pause, () => game.RunPhase == ArenaUiState.Paused);
            await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync();
            var hud = Object.FindAnyObjectByType<V2Hud>();
            var paused = hud.screens.First(screen => screen && screen.state == ArenaUiState.Paused).transform;
            var button = paused.Find("Content/" + V2Hud.PauseSettingsButton) as RectTransform;
            Assert.That(button && button.gameObject.activeInHierarchy, Is.True, "the pause screen has a Settings button");
            var label = button.GetComponentInChildren<Text>();
            await Until(() => label.text == ArenaText.Pair("设置", "SETTINGS"), 3f, "it is labelled");
            Canvas.ForceUpdateCanvases();
            var panel = Box(paused.Find("Content"), hud.transform);
            var box = Box(button, hud.transform);
            Assert.That(box.xMin >= panel.xMin && box.xMax <= panel.xMax && box.yMin >= panel.yMin && box.yMax <= panel.yMax, Is.True, "inside the pause panel");
            foreach (var name in V2Hud.PauseButtons)
                Assert.That(box.Overlaps(Box(paused.Find("Content/" + name), hud.transform)), Is.False, $"clear of {name}");
            var table = paused.GetComponentInChildren<V2ControlsTable>(true);
            Assume.That(table.keys.Select(key => key.text), Has.Member("E").And.Member("1 / 2 / 3"), "the pause table names the default keys");

            Click(button);
            Assert.That(ArenaSettingsPanel.IsOpen && ArenaSettingsPanel.HoldsInput, Is.True, "it opens the panel, which holds the game's keys");
            await Awaitable.NextFrameAsync();
            foreach (var name in V2Hud.PauseButtons)
            {
                var hit = TopHit((RectTransform)paused.Find("Content/" + name), out _);
                Assert.That(hit && hit.GetComponentInParent<ArenaSettingsPanel>(), Is.True, $"the panel covers {name}, so a click there stays on it");
            }

            // The title's art switch worked on the title only: here the two modes show which one is on and cannot be pressed.
            bool illustrated = Art.Enabled;
            RectTransform showing = PanelPart(illustrated ? "ArtIllustrated" : "ArtWhitebox"), other = PanelPart(illustrated ? "ArtWhitebox" : "ArtIllustrated");
            Assert.That(showing.GetComponent<Button>().interactable || other.GetComponent<Button>().interactable, Is.False, "the art mode cannot be pressed in a run");
            Assert.That(Mark(showing).enabled && !Mark(other).enabled, Is.True, "the mode showing is still marked");
            Assert.That(PanelPart("ArtNote").GetComponent<Text>().text, Is.Not.Empty, "and a line says where it is switched");
            float until = Time.realtimeSinceStartup + .7f;
            while (Time.realtimeSinceStartup < until)
            {
                Click(other);
                await Awaitable.NextFrameAsync();
            }
            Assert.That(Art.Enabled, Is.EqualTo(illustrated), "a click on the other mode switches nothing");

            // Two keys change: interacting to G, the first skill to Q.
            Click(PanelPart("KeyInteract/Key"));
            Assert.That(ArenaSettingsPanel.TakeKeyByName("G"), Is.EqualTo(KeyBindResult.Bound));
            Click(PanelPart("KeySkill1/Key"));
            Assert.That(ArenaSettingsPanel.TakeKeyByName("Q"), Is.EqualTo(KeyBindResult.Bound));

            ArenaSettingsPanel.Back(); // Esc
            Assert.That(ArenaSettingsPanel.IsOpen, Is.False);
            Assert.That(ArenaSettingsPanel.HoldsInput, Is.True, "the closing frame is held too, so its Esc does not also resume the run");
            await Awaitable.NextFrameAsync();
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Paused), "closing it goes back to the pause screen");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "still paused");
            await Until(() => table.keys.Any(key => key.text == "G"), 3f, "the pause table names the new interact key");
            Assert.That(table.keys.Select(key => key.text), Has.Member("Q / 2 / 3").And.None.EqualTo("E"), "and the new skill key, in place of the old ones");

            await CommandUntil(game, ArenaUiCommand.Resume, () => game.RunPhase == ArenaUiState.WaveActive);
            await Until(() => SlotKey(hud, 0) == ArenaText.Pair("技能1 [Q]", "SKILL 1 [Q]"), 3f, "the first skill slot names its new key");
            Assert.That(SlotKey(hud, 1), Is.EqualTo(ArenaText.Pair("技能2 [2]", "SKILL 2 [2]")), "the second keeps its own");
        }

        // ───── Helpers ─────

        static Transform TitleContent() => GameObject.Find("V2Canvas/TitleScreen/Content").transform;

        static RectTransform Part(Transform content, string name) => (RectTransform)content.Find(name);

        /// <summary>A part of the open settings panel, by its path under the panel.</summary>
        static RectTransform PanelPart(string path)
        {
            var part = GameObject.Find(ArenaSettingsPanel.RootName + "/Window/Panel/" + path);
            Assert.That(part, Is.Not.Null, $"the settings panel has its {path}");
            return (RectTransform)part.transform;
        }

        /// <summary>The line under an art mode's button that shows while that mode is on.</summary>
        static Image Mark(RectTransform artButton) => artButton.Find("Mark").GetComponent<Image>();

        static string SlotKey(V2Hud hud, int index) => hud.skillSlots[index].transform.Find("SlotKey").GetComponent<Text>().text;

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && GameManager.Instance.didStart && ArenaText.Ready && WorldMap.Active, 12f,
                "scene, localization and map initialize");
            await Awaitable.NextFrameAsync();
            return GameManager.Instance;
        }

        /// <summary>Title → supply crate → Rifle → close its card → the first wave is in combat (a first-sight cutscene is ended at once).</summary>
        static async Task StartRun(GameManager game)
        {
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            game.player.ProtectFor(600f);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) WorldMap.Active.SkipHoleIntro();
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

        /// <summary>Clicks until done holds: the menus take no command for a moment after a screen opens (GameManager's menu lock).</summary>
        static async Task ClickUntil(RectTransform button, Func<bool> done, string what, float timeout = 5f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!done())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("Timed out waiting for: " + what);
                Click(button);
                await Awaitable.NextFrameAsync();
            }
        }

        /// <summary>The part's rectangle in the given space, as FlowInputTest measures the title.</summary>
        static Rect Box(Transform part, Transform space)
        {
            var corners = new Vector3[4];
            ((RectTransform)part).GetWorldCorners(corners);
            Vector2 a = space.InverseTransformPoint(corners[0]), b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        static async Task CommandUntil(GameManager game, ArenaUiCommand command, Func<bool> completed, int index = -1)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (!completed())
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"Command {command} timed out in {game.RunPhase}.");
                game.ExecuteUiCommand(command, index);
                await Awaitable.NextFrameAsync();
            }
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
