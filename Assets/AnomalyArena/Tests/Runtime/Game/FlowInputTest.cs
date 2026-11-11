using System;
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
    /// <summary>Flow and input, in the saved arena, with keys pressed on a real keyboard device. Space and
    /// Enter do nothing anywhere, not even on a selected button, except close the weapon card, the one text that names them (document 2.2,
    /// 10-27); E enters the Void Trial only while standing on the
    /// open portal and free to act; entering clears every status and empties the shield; back from a trial won mid-wave, the battlefield
    /// and the player hold still while the player absorbs the void's power, then the wave resumes with the extra pick, and Tab cannot walk
    /// away from the greater reward in between; a player sliding from a knockback cannot cast skills until the slide ends. Every test that
    /// presses a key for nothing also presses one that acts, so a keyboard that never reached the game cannot pass.</summary>
    [TestFixture]
    public class FlowInputTest
    {
        // Since 2026-11-10 one scene serves both languages; the English cases load it with an English start (TestStartLanguage).
        const string SceneCn = "Assets/AnomalyArena/Scenes/ArenaV2.unity";
        static readonly KeyCode[] SpaceAndEnter = { KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter };
        WaveSet originalWaves, runtimeWaves;
        TestKeyboard keyboard;
        bool? artWas;
        // The player's saved language before a test clicked the Language option: the editor shares PlayerPrefs between the copies of
        // this project, so the choice a test saves is taken back.
        bool languageSaved;
        string languageBefore;

        [TearDown]
        public void TearDown()
        {
            keyboard?.Dispose();
            keyboard = null;
            if (V2Tutorial.IsOpen) V2Tutorial.Close();
            if (artWas.HasValue) Art.Enabled = artWas.Value;
            artWas = null;
            if (languageSaved)
            {
                if (languageBefore == null) PlayerPrefs.DeleteKey(ArenaLanguage.SavedChoiceKey);
                else PlayerPrefs.SetString(ArenaLanguage.SavedChoiceKey, languageBefore);
                PlayerPrefs.Save();
            }
            languageSaved = false;
            languageBefore = null;
            TestStartLanguage.Reset();
            if (runtimeWaves)
            {
                if (GameManager.Instance && GameManager.Instance.waves) GameManager.Instance.waves.waveSet = originalWaves;
                Object.Destroy(runtimeWaves);
            }
            originalWaves = runtimeWaves = null;
            Time.timeScale = 1f;
        }

        // ───── Space and Enter do nothing ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task Title_SpaceOrEnter_DoesNotStartAndTheUiHasNoSubmit()
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            // The input module type lives in the Input System assembly, which the tests do not reference: read its submit by name.
            var modules = Object.FindObjectsByType<BaseInputModule>(FindObjectsInactive.Include)
                .Where(module => module.GetType().GetProperty("submit") != null).ToArray();
            Assume.That(modules, Is.Not.Empty, "the scene's event system reads an input action asset");
            keyboard = new TestKeyboard();
            var start = TitleStart(game);
            SelectButton(start);

            await keyboard.TapFor(.8f, SpaceAndEnter);

            Assert.That(modules.Select(module => module.GetType().GetProperty("submit").GetValue(module)), Has.All.Null,
                "no submit action, so Enter cannot press the selected button either");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "Space and Enter do not start the run, even on the selected Start button");
            // Document 2.2 (10-27): Space and Enter are read for the weapon card only, which names them (WeaponCard_SpaceOrEnter_ClosesIt).
            Assert.That(GameManager.ShownKeyCodes, Is.EquivalentTo(new[]
                { KeyCode.Escape, KeyCode.T, KeyCode.Tab, KeyCode.R, KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Space, KeyCode.Return, KeyCode.KeypadEnter }),
                "the menus read only the keys the game shows");
            // T no longer switches the title's art style: no screen shows that key.
            artWas = Art.Enabled;
            // One press at a time: an even number of toggles would look like no change.
            for (int press = 0; press < 3; press++)
            {
                await keyboard.Tap(KeyCode.T);
                Assert.That(Art.Enabled, Is.EqualTo(artWas.Value), $"T does nothing on the title (press {press + 1})");
            }
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            // What the title does answer to still works: its Start begins the run (since 2026-11-06 the V2 day campaign).
            await CommandUntil(game, start, () => start == ArenaUiCommand.StartCampaign ? V2Campaign.Active && V2Campaign.Active.Running : game.RunPhase == ArenaUiState.SupplyDrop);
        }

        // ───── The title's three entries (one game in two languages) ─────

        /// <summary>From the top the story mode, the open mode
        /// and the tutorial, in the language the game started in, every word whole, as wide as one another.</summary>
        [TestCase(StartupLanguage.Chinese, "开始游戏-剧情模式", "开始游戏-开放模式", "教学")]
        [TestCase(StartupLanguage.English, "Start Game - Story Mode", "Start Game - Open Mode", "Tutorial")]
        [BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task Title_ThreeEntriesTopDown_InTheStartLanguage_WholeAndAsWide(string language, string story, string open, string tutorial)
        {
            TestStartLanguage.Use(language);
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            var content = GameObject.Find("V2Canvas/TitleScreen/Content").transform;
            var entries = new[] { V2Hud.TitleStoryButton, V2Hud.TitleOpenButton, V2Hud.TitleTutorialButton }.Select(name => (RectTransform)content.Find(name)).ToArray();
            var labels = entries.Select(entry => entry.GetComponentInChildren<UnityEngine.UI.Text>()).ToArray();
            await Until(() => labels[0].text == story, 3f, "the entries' words in the start language");
            await Awaitable.NextFrameAsync();

            var hud = Object.FindAnyObjectByType<V2Hud>();
            ((RectTransform)hud.transform).sizeDelta = hud.GetComponentInParent<UnityEngine.UI.CanvasScaler>().referenceResolution;
            Canvas.ForceUpdateCanvases();
            var boxes = entries.Select(part => Box(part, hud.transform)).ToArray();

            Assert.That(ArenaText.IsChinese, Is.EqualTo(language == StartupLanguage.Chinese), "the game started in the language it was asked to");
            Assert.That(labels.Select(label => label.text), Is.EqualTo(new[] { story, open, tutorial }), "story, open, tutorial");
            Assert.That(labels.Where(label => label.cachedTextGenerator.characterCountVisible < label.text.Length).Select(label => label.text), Is.Empty,
                "every entry's words show in full");
            Assert.That(boxes.Zip(boxes.Skip(1), (upper, lower) => upper.yMin >= lower.yMax), Has.All.True, "each entry under the last");
            Assert.That(boxes.Take(3).Select(box => box.width), Has.All.EqualTo(boxes[0].width).Within(.5f), "the three entries as wide as one another");
        }

        /// <summary>The story mode is the V2 day campaign; the open mode goes through the map pick (OpenModeMapSelectTest), so no button sends
        /// the classic run's Start; the tutorial sends no run command at all.</summary>
        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task Title_StoryEntryOpensTheV2Campaign_TheOpenEntryNeedsAMap()
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            await Awaitable.NextFrameAsync();
            var content = GameObject.Find("V2Canvas/TitleScreen/Content").transform;

            var buttons = Object.FindObjectsByType<ArenaUiButton>(FindObjectsInactive.Exclude);
            Assert.That(buttons.Where(b => b.command == ArenaUiCommand.StartCampaign).Select(b => b.name), Is.EqualTo(new[] { V2Hud.TitleStoryButton }),
                "one story entry, the saved Start, and it opens the V2 campaign");
            Assert.That(buttons.Any(b => b.command == ArenaUiCommand.Start), Is.False, "no button starts the classic run without a map");
            Assert.That(content.Find(V2Hud.TitleOpenButton).TryGetComponent<ArenaUiButton>(out _), Is.False, "the open entry opens the map pick instead");
            Assert.That(content.Find(V2Hud.TitleTutorialButton).TryGetComponent<ArenaUiButton>(out _), Is.False, "the tutorial sends no run command");
            await CommandUntil(game, ArenaUiCommand.StartCampaign, () => V2Campaign.Active && V2Campaign.Active.Running);
            Assert.That(V2Campaign.Active.Day, Is.EqualTo(0), "the campaign opens on day 0");
            Assert.That(V2Campaign.Active.rules.wavesPerDay, Is.EqualTo(1), "one wave a day");
        }

        // ───── The Language option ─────

        /// <summary>Top right, it reads "Language" in both languages, a click switches between Chinese
        /// and English at once and saves the choice, and the title's words follow.</summary>
        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task LanguageOption_TopRight_ClickSwitchesAtOnceAndSavesTheChoice()
        {
            var game = await ReadyScene();
            Assume.That(ArenaText.IsChinese, Is.True, "the run's scenes start in Chinese (TestStartLanguage)");
            var content = GameObject.Find("V2Canvas/TitleScreen/Content").transform;
            var option = (RectTransform)content.Find(V2Hud.TitleLanguageButton);
            var label = option.GetComponentInChildren<UnityEngine.UI.Text>();
            var story = content.Find(V2Hud.TitleStoryButton).GetComponentInChildren<UnityEngine.UI.Text>();
            await Until(() => label.text == "Language", 3f, "the option reads Language");
            var hud = Object.FindAnyObjectByType<V2Hud>();
            var canvas = ((RectTransform)hud.transform).rect;
            var box = Box(option, hud.transform);
            languageBefore = PlayerPrefs.HasKey(ArenaLanguage.SavedChoiceKey) ? PlayerPrefs.GetString(ArenaLanguage.SavedChoiceKey) : null;
            languageSaved = true;

            await ClickUntil(option, () => !ArenaText.IsChinese, "a click switches to English");
            await Until(() => ArenaText.Ready && story.text == "Start Game - Story Mode", 5f, "the title's words follow at once");
            string savedEnglish = PlayerPrefs.GetString(ArenaLanguage.SavedChoiceKey, "");
            await Awaitable.NextFrameAsync(); // the title's canvas was off while the English text loaded; it takes clicks a frame after it is back
            await ClickUntil(option, () => ArenaText.IsChinese, "a second click switches back to Chinese");
            await Until(() => ArenaText.Ready && story.text == "开始游戏-剧情模式", 5f, "and the words follow back");

            Assert.That(box.center.x, Is.GreaterThan(canvas.center.x + canvas.width * .25f), "the option sits at the right");
            Assert.That(box.center.y, Is.GreaterThan(canvas.center.y + canvas.height * .25f), "and at the top");
            Assert.That(label.text, Is.EqualTo("Language"), "it reads Language in Chinese too");
            Assert.That(savedEnglish, Is.EqualTo(StartupLanguage.English), "the switch to English was saved");
            Assert.That(PlayerPrefs.GetString(ArenaLanguage.SavedChoiceKey, ""), Is.EqualTo(StartupLanguage.Chinese), "and the switch back too");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "still on the title");
        }

        /// <summary>The command the title's Start carries: since 2026-11-06 it opens the V2 campaign in a scene that has one.</summary>
        static ArenaUiCommand TitleStart(GameManager game) =>
            game.TryGetComponent<V2Campaign>(out var campaign) && campaign.isActiveAndEnabled && campaign.rules && campaign.startButtonOpensCampaign
                ? ArenaUiCommand.StartCampaign : ArenaUiCommand.Start;

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task Title_ShowsTheEntriesSettingsAndLanguage_NotTheKeyHintsTheArtSwitchOrTheOldLanguageButtons()
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            await Awaitable.NextFrameAsync();
            var content = GameObject.Find("V2Canvas/TitleScreen/Content");
            Assume.That(content, Is.Not.Null, "the saved title screen");
            // No tagline; since 2026-11-10 three entries and the Language option in place of the two saved language buttons, in
            // either language (one scene for both), and the Settings button in place of the art switch under the entries and of the
            // line of key hints along the bottom (the art mode and the keys are in the settings).
            foreach (var hidden in new[] { "InterstellarSubtitle", "ChineseTeam", "EnglishShowcase", "Controls", "ToggleArt", V2Hud.MapSelect })
            {
                var element = content.transform.Find(hidden);
                Assert.That(element == null || !element.gameObject.activeInHierarchy, Is.True, hidden + " is not shown");
            }
            foreach (var shown in new[] { V2Hud.TitleStoryButton, V2Hud.TitleOpenButton, V2Hud.TitleTutorialButton, V2Hud.TitleSettingsButton, V2Hud.TitleLanguageButton })
                Assert.That(content.transform.Find(shown).gameObject.activeInHierarchy, Is.True, shown + " is shown");
        }

        // ───── The title's tutorial entry (since 2026-11-06; "教学" since 2026-11-10) ─────

        [TestCase(StartupLanguage.Chinese, "教学"), TestCase(StartupLanguage.English, "Tutorial"), BuildScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task TitleTutorial_EachLanguage_OpensTheStoryPages_AndClosingThemLeavesTheTitle(string language, string word)
        {
            TestStartLanguage.Use(language);
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            await TitleTutorialOpensTheStoryPages(word);
        }

        /// <summary>The title's third entry opens the story mode's pages over the title, though no campaign runs there yet: the background
        /// first, then how to play. No text on any page, the footnote included, names a card or Tab (the story mode has no card picks and no
        /// build phase), and every page's line fits its box. The pages cover the title's buttons while open, and closing them leaves the
        /// title as it was. On the reference canvas the title's buttons (the entries and the two corner ones) do not overlap.</summary>
        static async Task TitleTutorialOpensTheStoryPages(string word)
        {
            var game = await ReadyScene();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title));
            Assume.That(V2Campaign.Active && V2Campaign.Active.Running, Is.False, "no campaign runs on the title");
            var content = GameObject.Find("V2Canvas/TitleScreen/Content").transform;
            var tutorial = content.Find(V2Hud.TitleTutorialButton) as RectTransform;
            Assert.That(tutorial && tutorial.gameObject.activeInHierarchy, Is.True, "the title shows the tutorial entry");
            var label = tutorial.GetComponentInChildren<UnityEngine.UI.Text>();
            await Until(() => label.text == word, 3f, "the tutorial entry is labelled in the start language");
            Assert.That(tutorial.TryGetComponent<ArenaUiButton>(out _), Is.False, "the tutorial entry sends no run command");
            await Awaitable.NextFrameAsync();
            float titleTime = Time.timeScale;

            Click(tutorial);
            Assert.That(V2Tutorial.IsOpen && V2Tutorial.HoldsInput, Is.True, "a click opens the pages, which hold the game's keys");
            Assert.That(Time.timeScale, Is.Zero, "the game holds still under them");
            await Awaitable.NextFrameAsync();
            foreach (var name in new[] { V2Hud.TitleStoryButton, V2Hud.TitleOpenButton, V2Hud.TitleTutorialButton, V2Hud.TitleSettingsButton, V2Hud.TitleLanguageButton })
            {
                var hit = TopHit((RectTransform)content.Find(name), out _);
                Assert.That(hit && hit.GetComponentInParent<V2Tutorial>(), Is.True, $"the pages cover {name}, so a click there stays on them");
            }
            var pages = Object.FindAnyObjectByType<V2Tutorial>().transform;
            var heading = pages.Find("Panel/Heading").GetComponent<UnityEngine.UI.Text>();
            var line = pages.Find("Panel/Line").GetComponent<UnityEngine.UI.Text>();
            var shownTexts = new System.Collections.Generic.List<string>();
            for (int page = 0; page < V2Tutorial.PageCount; page++)
            {
                Assert.That(V2Tutorial.Page, Is.EqualTo(page));
                // A page's demo is rebuilt when it opens and the old one goes at the end of that frame.
                await Awaitable.NextFrameAsync();
                Canvas.ForceUpdateCanvases();
                if (page == 0) Assert.That(heading.text, Is.EqualTo(ArenaText.Pair("坠落异星", "STRANDED")), "the story pages open on the background");
                Assert.That(line.text, Is.Not.Empty, $"page {page + 1} has its line");
                Assert.That(line.cachedTextGenerator.characterCountVisible, Is.GreaterThanOrEqualTo(line.text.Length), $"page {page + 1}: \"{line.text}\" is not cut off");
                // Every text the page shows: its heading and line, the demo's labels and keys, and on the last page the footnote.
                shownTexts.AddRange(pages.GetComponentsInChildren<UnityEngine.UI.Text>().Select(text => text.text));
                if (page < V2Tutorial.PageCount - 1) V2Tutorial.Next();
            }
            Assert.That(pages.Find("Panel/Footnote").gameObject.activeInHierarchy, Is.True, "the last page shows the footnote");
            string card = ArenaText.Pair("卡", "card");
            Assert.That(shownTexts.Where(text => text.IndexOf(card, StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty, "the story pages name no card");
            Assert.That(shownTexts.Where(text => text.Contains("Tab")), Is.Empty, "nor Tab");

            Click((RectTransform)pages.Find("Panel/Skip"));
            Assert.That(V2Tutorial.IsOpen, Is.False, "Skip closes the pages");
            Assert.That(Time.timeScale, Is.EqualTo(titleTime), "and gives the title its own time back");
            await Awaitable.NextFrameAsync();
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Title), "the title is still there");
            Assert.That(tutorial.gameObject.activeInHierarchy, Is.True, "with its tutorial button");

            // Measured on the reference canvas, as the bottom row is in V3SmokeTest: a batch run's window is not 16:9. The canvas takes its own
            // size back a few frames later, so everything is measured at once.
            var hud = Object.FindAnyObjectByType<V2Hud>();
            ((RectTransform)hud.transform).sizeDelta = hud.GetComponentInParent<UnityEngine.UI.CanvasScaler>().referenceResolution;
            Rect open = Box(content.Find(V2Hud.TitleOpenButton), hud.transform), help = Box(tutorial, hud.transform);
            Assert.That(help.yMax, Is.LessThanOrEqualTo(open.yMin), "the tutorial entry is under the open mode");
            Assert.That(help.width, Is.EqualTo(open.width).Within(.5f), "as wide as the starts");
            // The three entries and the two corner buttons (Settings top left, Language top right).
            var shown = content.Cast<Transform>().Where(part => part.gameObject.activeInHierarchy && part.GetComponent<UnityEngine.UI.Button>()).ToArray();
            for (int i = 0; i < shown.Length; i++)
                for (int j = i + 1; j < shown.Length; j++)
                    Assert.That(Box(shown[i], hud.transform).Overlaps(Box(shown[j], hud.transform)), Is.False, $"{shown[i].name} overlaps {shown[j].name}");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task CrateAndMidWavePick_SpaceOrEnter_DoNothingAndNoTextNamesThem()
        {
            var game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await Awaitable.NextFrameAsync();
            var crate = Object.FindAnyObjectByType<V3SupplyDropPresentation>();
            Assume.That(crate && crate.openHint && crate.openButton, Is.True, "the saved crate has its hint and button");
            Assert.That(EventSystem.current ? EventSystem.current.currentSelectedGameObject : null, Is.Not.SameAs(crate.openButton.gameObject),
                "the crate button is not selected for a keyboard confirm");
            keyboard = new TestKeyboard();
            Select(crate.openButton);

            await keyboard.TapFor(.8f, SpaceAndEnter);

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.SupplyDrop), "Space and Enter do not open the crate, even with its button selected");
            Assert.That(crate.openHint.text, Does.Contain("点击").IgnoreCase.And.Not.Contain("空格").IgnoreCase.And.Not.Contain("Enter").IgnoreCase, "the crate asks for a click");

            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            // The weapon card between them is the one screen that answers to Space and Enter (WeaponCard_SpaceOrEnter_ClosesIt).
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            await CommandUntil(game, ArenaUiCommand.DismissWeaponIntro, () => game.RunPhase != ArenaUiState.WeaponIntro);
            await ReachCombat(game);
            game.progression.GainXp(game.progression.XpRequired);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "a level gained mid-wave opens its pick");
            var screen = Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == ArenaUiState.Draft);
            Select(screen.startNext);
            await keyboard.TapFor(.8f, SpaceAndEnter);

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.LevelUp), "Space and Enter do not put the pick off, even on its button");
            Assert.That(screen.body.text, Does.Contain("Esc").And.Not.Contain("空格").IgnoreCase, "the pick names Esc, not Space");
            Assert.That(screen.startNext.GetComponentInChildren<UnityEngine.UI.Text>().text, Does.Contain("Esc"), "the pick-later button names its key");
            // The same keyboard does reach the game: Esc, the key the pick shows, puts it off.
            await keyboard.TapUntil(KeyCode.Escape, () => game.RunPhase == ArenaUiState.WaveActive, "Esc puts the pick off");
        }

        /// <summary>Document 2.2 (10-27, decisions extra-2.2, which overrides the 10-25 "only shown keys" rule for this one screen): the weapon
        /// card goes on with a click anywhere, Space or Enter, and its hint names them.</summary>
        [TestCase(KeyCode.Space), TestCase(KeyCode.Return), TestCase(KeyCode.KeypadEnter), LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task WeaponCard_SpaceOrEnter_ClosesIt(KeyCode key)
        {
            var game = await ReadyScene();
            await CommandUntil(game, ArenaUiCommand.Start, () => game.RunPhase == ArenaUiState.SupplyDrop);
            await Until(() => game.SupplyDropReady, 5f, "supply crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Loadout);
            await CommandUntil(game, ArenaUiCommand.ChooseLoadout, () => game.RunPhase == ArenaUiState.WeaponIntro, 1);
            Assert.That(game.UiSnapshot.weaponIntroHint, Does.Contain("点击").And.Contain("空格").And.Contain("回车"),
                "the weapon card names the keys that close it");
            keyboard = new TestKeyboard();

            await keyboard.TapUntil(key, () => game.RunPhase != ArenaUiState.WeaponIntro, key + " closes the weapon card");

            Assert.That(game.State, Is.EqualTo(GameState.Playing), "the run goes on as after a click");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task ChineseTables_KeyTexts_NameNoSpaceOrEnterAndShowEsc()
        {
            await ReadyScene();

            foreach (var key in new[] { "ui.nextwave", "ui.return.reward", "ui.ready.body", "ui.controls", "ui.hole.enter", "ui.hole.enter.build" })
                Assert.That(ArenaText.Text(key), Does.Not.Contain("空格").IgnoreCase.And.Not.Contain("回车").IgnoreCase, key);
            // Since 2026-11-10 the title's keys no longer explain the portal: the portal shows its own E once the player stands on it.
            Assert.That(ArenaText.Text("ui.controls"), Does.Contain("Esc 暂停").IgnoreCase.And.Not.Contain("传送门"), "the saved line of key hints (no longer shown on the title) lists the pause key and says nothing of the portal");
            Assert.That(ArenaText.Text("ui.hole.enter"), Does.Contain("护盾清 0").IgnoreCase, "the portal says entering empties the shield");
        }

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(30000)]
        public async Task EnglishTables_KeyTexts_NameNoSpaceOrEnterAndShowEsc()
        {
            TestStartLanguage.Use(StartupLanguage.English);
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            await ReadyScene();
            Assume.That(ArenaText.IsChinese, Is.False, "the game started in English");

            foreach (var key in new[] { "ui.nextwave", "ui.return.reward", "ui.ready.body", "ui.controls", "ui.hole.enter.build" })
                Assert.That(ArenaText.Text(key).ToUpperInvariant(), Does.Not.Contain("SPACE").And.Not.Contain("ENTER /").And.Not.Contain("/ ENTER"), key);
            Assert.That(ArenaText.Text("ui.controls"), Does.Contain("ESC PAUSE").And.Not.Contain("PORTAL"), "the saved line of key hints (no longer shown on the title) lists the pause key and says nothing of the portal");
        }

        // ───── One mid-wave pick, then the fight; early start asks only for unpicked cards ─────

        /// <summary>Document 2.2 (the 10-27 doc): a mid-wave pick ends with one card and the fight goes on; a pick still banked waits
        /// for T or the next level instead of popping up right after.</summary>
        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task MidWavePick_OneCard_BackToTheFight_TheOtherBankedPickWaitsForT()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var progression = game.progression;
            progression.GainXp(progression.XpRequired);
            progression.GainXp(progression.XpRequired);
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "two levels gained mid-wave open a pick");
            Assume.That(progression.BankedDrafts, Is.EqualTo(2), "two picks are banked");

            await CommandUntil(game, ArenaUiCommand.ChooseCard, () => game.RunPhase != ArenaUiState.LevelUp, 0);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveActive), "one card, then back to the fight");
            Assert.That(progression.BankedDrafts, Is.EqualTo(1), "the other pick stays banked");
            Assert.That(game.UiSnapshot.hint, Does.Contain("T"), "the HUD says T opens it");

            keyboard = new TestKeyboard();
            await keyboard.TapUntil(KeyCode.T, () => game.RunPhase == ArenaUiState.LevelUp, "T opens the banked pick");
        }

        /// <summary>Document 2.3: starting the next wave early asks first only while card picks are unpicked; at the armory with no pick
        /// banked the wave starts at once and the weapons not taken are gone.</summary>
        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task BuildPhase_StartNextWave_NoPickBanked_StartsAtOnceEvenAtTheArmory()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 6f);
            await StartRun(game);
            await Until(() => game.waves.TimeExpired, 15f, "the shortened wave times out");
            await ClearTheWave(game);
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 8f, "the build phase crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            if (game.progression.BankedDrafts > 0) game.progression.ResolvePendingRandom();
            Assume.That(game.progression.BankedDrafts, Is.Zero, "no card pick is banked");

            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.RunPhase != ArenaUiState.Armory);
            Assert.That(game.UiSnapshot.earlyStartConfirmation, Is.False, "nothing to confirm without unpicked cards");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.WaveIntro).Or.EqualTo(ArenaUiState.WaveActive), "the next wave starts at once");
        }

        // ───── The pause panel covers the run ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task Chinese_ThePausePanel_CoversTheBuildCrateAndTheDamageNumbers() => await ThePausePanelCoversTheBuildCrate();

        [Test, BuildScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task English_ThePausePanel_CoversTheBuildCrateAndTheDamageNumbers()
        {
            TestStartLanguage.Use(StartupLanguage.English);
            await SceneManagerHelper.LoadSceneAsync(SceneCn);
            await ThePausePanelCoversTheBuildCrate();
        }

        async Task ThePausePanelCoversTheBuildCrate()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 6f);
            await StartRun(game);
            await Until(() => game.waves.TimeExpired, 15f, "the shortened wave times out");
            await ClearTheWave(game);
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 8f, "the build phase crate lands");
            await ControlsTableTest.PauseCoversTheCrate(game);
        }

        // ───── E only on the portal ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(90000)]
        public async Task BuildPhase_OpenPortal_EnteredOnlyByStandingOnItWithThePanelAway()
        {
            var game = await ReadyScene();
            ShortenWaves(game, 6f);
            await StartRun(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            await Until(() => game.waves.TimeExpired, 15f, "the shortened wave times out");
            await ClearTheWave(game);
            await Until(() => game.RunPhase == ArenaUiState.SupplyDrop && game.SupplyDropReady, 8f, "the build phase crate lands");
            await CommandUntil(game, ArenaUiCommand.OpenSupply, () => game.RunPhase == ArenaUiState.Armory);
            Assume.That(portal.Phase, Is.EqualTo(HolePhase.Portal), "the portal stays open into the build phase");
            var armory = Object.FindAnyObjectByType<V2Hud>().screens.First(s => s && s.state == ArenaUiState.Armory);
            // The help colours its key words (ArenaEmphasis), so the words are read without the tags.
            string help = System.Text.RegularExpressions.Regex.Replace(armory.body.text, "<[^<>]+>", "");
            // Since 2026-11-10 the panel says nothing of the portal: the portal shows its own E once the player stands on it.
            Assert.That(help, Does.Not.Contain("站到门上按 E").IgnoreCase.And.Not.Contain("传送门已开启").And.Not.Contain("同样能进入").IgnoreCase,
                "the build panel does not explain the portal");
            keyboard = new TestKeyboard();

            // The panel is open: the old build-phase shortcut entered from anywhere, now nothing enters, not even standing on it.
            V3CombatSmokeTest.Teleport(game.player, Spot(world, portal.Centre, 6f));
            await keyboard.TapFor(.6f, KeyCode.E);
            await CommandFor(game, ArenaUiCommand.EnterPortal, .3f);
            Assert.That(world.TrialActive, Is.False, "the build panel no longer enters the trial from a distance");
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            await keyboard.TapFor(.6f, KeyCode.E);
            await CommandFor(game, ArenaUiCommand.EnterPortal, .3f);
            Assert.That(world.TrialActive, Is.False, "with the panel open the player is not standing anywhere");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Armory));

            await keyboard.TapUntil(KeyCode.Tab, () => game.BuildWalking, "Tab puts the build panel away");
            V3CombatSmokeTest.Teleport(game.player, Spot(world, portal.Centre, 2f));
            await keyboard.Tap(KeyCode.E);
            Assert.That(world.TrialActive, Is.False, "walking, but beside the portal");
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            await keyboard.TapUntil(KeyCode.E, () => world.TrialActive, "E on the portal enters the trial");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.VoidTrial));
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task OnThePortal_StunnedOrFrozen_EDoesNotEnterUntilThePlayerIsFreeToAct()
        {
            var game = await ReadyScene();
            await StartRun(game);
            QuietArena(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            var status = ElementStatus.For(game.player);
            StandOn(game, portal);
            Assume.That(world.CanEnterPortal(game.player, portal), Is.True, "free to act on the open portal");
            keyboard = new TestKeyboard();

            // A Rammer's charge: the push is over before the stun is.
            game.player.Stun(.6f);
            Assume.That(game.player.State, Is.EqualTo(CharacterState.Normal), "the player is in the normal state, only stunned");
            await keyboard.Tap(KeyCode.E);
            Assert.That(world.TrialActive, Is.False, "stunned, E does not enter");
            Assert.That(world.CanEnterPortal(game.player, portal), Is.False, "and the portal label does not offer E");
            await Until(() => !game.player.Stunned, 3f, "the stun wears off");

            status.Add(ElementKind.Frost, 999f);
            Assume.That(status.Frozen, Is.True, "enough Frost freezes the player");
            StandOn(game, portal);
            await keyboard.Tap(KeyCode.E);
            Assert.That(world.TrialActive, Is.False, "frozen, E does not enter");
            status.ClearStatuses();

            StandOn(game, portal);
            await keyboard.TapUntil(KeyCode.E, () => world.TrialActive, "free to act again, E on the portal enters the trial");
            Assert.That(game.player.Stunned || status.Frozen, Is.False, "nothing that stops the player is carried onto the platform");
        }

        // ───── Entering and leaving the trial ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task EnterTrial_StatusesAndShieldCarried_AreAllCleared()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var portal = OpenPortal(game);
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            var status = ElementStatus.For(game.player);
            status.Add(ElementKind.Fire, 3f);
            status.Add(ElementKind.Poison, 3f);
            status.Add(ElementKind.Frost, 1f);
            status.Add(ElementKind.Lightning, 1f);
            status.GainShield(5f);
            Assume.That(status.Fire > 0f && status.Poison > 0f && status.Frost > 0f && status.Lightning > 0f && status.Shield > 0f, Is.True,
                "the player carries statuses and a shield");

            Assert.That(WorldMap.Active.TryEnterPortal(game.player), Is.True, "the player enters the open portal");

            Assert.That(new[] { status.Fire, status.Poison, status.Frost, status.Lightning }, Has.All.Zero, "every status is cleared");
            Assert.That(status.Frozen || status.Paralysed, Is.False);
            Assert.That(status.Shield, Is.Zero, "document 12.5: the shield goes to 0");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task TrialWonMidWave_ReturnFreezesWorldAndPlayerWhileAbsorbing_ThenThePickPopsUp()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Assume.That(world.TryEnterPortal(game.player), Is.True, "the player enters the open portal");
            world.CompleteTrial();
            Assume.That(game.RunPhase, Is.EqualTo(ArenaUiState.Draft), "the greater void reward is offered first");
            await CommandUntil(game, ArenaUiCommand.StartNextWave, () => game.RunPhase != ArenaUiState.Draft);
            float frozenAt = Time.realtimeSinceStartup;
            Vector3 stood = game.player.Position;

            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.TrialReturn), "the run returns through the freeze");
            Assert.That(game.CanPlayerMove, Is.False, "the player is frozen too, not only the battlefield");
            Assert.That(Time.timeScale, Is.Zero, "nothing moves during the freeze");
            await CommandFor(game, ArenaUiCommand.Pause, .8f);
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.TrialReturn), "no input acts during the freeze, pausing included");
            Assert.That(FxPool.Active.items.Any(item => item && item.gameObject.activeSelf) || V5Vfx.On && V5Vfx.I.LiveParticles > 0, Is.True, "the absorb animation is playing");
            Assert.That(Query.Flat(game.player.Position - stood).magnitude, Is.LessThan(.01f), "the player stays where they came back");
            Assert.That(portal.Phase, Is.Not.EqualTo(HolePhase.Gone), "the portal is still in view during the freeze");

            await Until(() => game.RunPhase != ArenaUiState.TrialReturn, game.trialReturnFreeze + 3f, "the freeze ends by itself");
            Assert.That(Time.realtimeSinceStartup - frozenAt, Is.GreaterThanOrEqualTo(game.trialReturnFreeze - .15f), "it lasts the tuned freeze");
            Assert.That(world.holes.Where(h => h).All(h => h.Phase == HolePhase.Gone), Is.True, "after the freeze every black hole has turned into ground");
            await Until(() => game.RunPhase == ArenaUiState.LevelUp, 3f, "the extra level's pick pops up as the wave resumes");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task GreaterRewardAfterAMidWaveWin_Tab_DoesNotWalkTheLiveBattlefield()
        {
            var game = await ReadyScene();
            await StartRun(game);
            var world = WorldMap.Active;
            var portal = OpenPortal(game);
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Assume.That(world.TryEnterPortal(game.player), Is.True, "the player enters the open portal");
            world.CompleteTrial();
            Assume.That(game.RunPhase == ArenaUiState.Draft && game.UiSnapshot.isGreaterReward, Is.True, "the greater void reward is offered first");
            float clock = game.waves.TimeLeft;
            keyboard = new TestKeyboard();

            await keyboard.TapFor(.8f, KeyCode.Tab);
            await CommandFor(game, ArenaUiCommand.ToggleBuildWalk, .4f);

            Assert.That(game.BuildWalking, Is.False, "the greater reward is no build phase: Tab does not put it away");
            Assert.That(game.CanPlayerMove, Is.False, "the player cannot walk the live battlefield under the reward");
            Assert.That(Time.timeScale, Is.Zero, "the battlefield stays frozen under the reward");
            Assert.That(game.waves.TimeLeft, Is.EqualTo(clock), "the wave clock does not run");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.Draft));
            // The same keyboard does reach the game: 1, a key the reward shows, takes the first greater skill and heads for the frozen return.
            await keyboard.TapUntil(KeyCode.Alpha1, () => game.RunPhase != ArenaUiState.Draft, "1 takes the first greater void skill");
            Assert.That(game.RunPhase, Is.EqualTo(ArenaUiState.TrialReturn), "the run goes back through the freeze");
        }

        // ───── Knocked back: no skills until the slide ends ─────

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task KnockedBack_SkillCast_IsRefusedUntilTheSlideEnds()
        {
            var game = await ReadyScene();
            await StartRun(game);
            QuietArena(game);
            Assume.That(game.skills.Learn(SkillId.Overcharge), Is.True, "the player learns a skill");
            var lane = LaneStart(WorldMap.Active, Vector3.right, 10f);
            V3CombatSmokeTest.Teleport(game.player, lane);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();

            game.player.Knockback(Vector3.right, 4f, KnockKind.Hit);
            Assume.That(game.player.State, Is.EqualTo(CharacterState.Knocked), "the player slides from the knockback");
            Assert.That(game.skills.TryCast(0), Is.False, "no skill while knocked back");

            await Until(() => game.player.State == CharacterState.Normal, 3f, "the slide ends");
            Assert.That(game.skills.TryCast(0), Is.True, "skills work again once the slide is over");
        }

        [Test, LoadScene(SceneCn), Category("Integration"), Timeout(60000)]
        public async Task KnockedBack_Blink_StillCastsAndEndsTheSlide()
        {
            var game = await ReadyScene();
            await StartRun(game);
            QuietArena(game);
            Assume.That(game.skills.Learn(SkillId.Blink), Is.True, "the player learns Blink");
            var lane = LaneStart(WorldMap.Active, Vector3.right, 10f);
            V3CombatSmokeTest.Teleport(game.player, lane);
            Physics.SyncTransforms();
            await Awaitable.FixedUpdateAsync();
            game.player.DebugSetAimPoint(lane + Vector3.right * 7f);

            game.player.Knockback(Vector3.right, 4f, KnockKind.Hit);
            Assume.That(game.player.State, Is.EqualTo(CharacterState.Knocked), "the player slides from the knockback");
            Assert.That(game.skills.TryCast(0), Is.True, "Blink forcibly breaks a displacement, so it casts while knocked back");
            Assert.That(game.player.State, Is.EqualTo(CharacterState.Normal), "and the slide is over");
        }

        // ───── Helpers ─────

        static async Task<GameManager> ReadyScene()
        {
            await Awaitable.NextFrameAsync();
            await Until(() => GameManager.Instance && ArenaText.Ready && WorldMap.Active, 12f, "scene, localization and map initialize");
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
            await ReachCombat(game);
        }

        /// <summary>Protects the player and waits for the wave. One feed for each hole: an unfed hole coming into view later (after a
        /// teleport) would start the first-sight cutscene and stop the clock in the middle of a test.</summary>
        static async Task ReachCombat(GameManager game)
        {
            game.player.ProtectFor(600f);
            await UntilWaveActive(game);
            foreach (var hole in WorldMap.Active.holes) if (hole && hole.Phase == HolePhase.Active && hole.Feeds == 0) hole.Feed(1, game.waves.WaveIndex + 1);
            await Awaitable.NextFrameAsync();
            await UntilWaveActive(game);
        }

        /// <summary>Stops the wave spawning and takes away what is about, so nothing walks into the player during a check.</summary>
        static void QuietArena(GameManager game)
        {
            game.waves.enabled = false;
            foreach (var enemy in game.waves.preparedEnemies) if (enemy && enemy.gameObject.activeSelf) enemy.DiscardWithoutReward();
            foreach (var marker in game.waves.preparedMarkers) if (marker) marker.gameObject.SetActive(false);
        }

        /// <summary>Kills whatever is alive (and what it leaves behind) without rewards until the wave is over.</summary>
        static async Task ClearTheWave(GameManager game)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            while (game.RunPhase == ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never ended; {game.waves.Remaining} enemies remain.");
                foreach (var enemy in game.waves.Alive.ToArray())
                {
                    if (!enemy || !enemy.IsAlive) continue;
                    enemy.SuppressRewards = true;
                    enemy.ReceiveDamage(new DamageInfo { amount = 99999f, source = DamageSource.Poison, ignoreShield = true });
                }
                await Awaitable.NextFrameAsync();
            }
        }

        static async Task UntilWaveActive(GameManager game)
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            while (game.RunPhase != ArenaUiState.WaveActive)
            {
                if (Time.realtimeSinceStartup > deadline) Assert.Fail($"The wave never started; stuck in {game.RunPhase}.");
                if (game.RunPhase == ArenaUiState.Cutscene) WorldMap.Active.SkipHoleIntro();
                await Awaitable.NextFrameAsync();
            }
        }

        /// <summary>Feeds the first active fixed hole until its portal opens.</summary>
        static BlackHole OpenPortal(GameManager game)
        {
            var hole = WorldMap.Active.holes.First(h => h && h.Phase == HolePhase.Active);
            hole.Feed(hole.feedsToOpen, game.waves.WaveIndex + 1);
            hole.Tick(2f);
            Assume.That(hole.Phase, Is.EqualTo(HolePhase.Portal), "the fed hole opens a portal");
            return hole;
        }

        static void StandOn(GameManager game, BlackHole portal)
        {
            V3CombatSmokeTest.Teleport(game.player, portal.Centre);
            Physics.SyncTransforms();
        }

        void ShortenWaves(GameManager game, float duration)
        {
            originalWaves = game.waves.waveSet;
            runtimeWaves = Object.Instantiate(originalWaves);
            foreach (var wave in runtimeWaves.waves) wave.duration = duration;
            game.waves.waveSet = runtimeWaves;
        }

        /// <summary>Selects a shown button the way keyboard navigation would, so Enter would press it if the UI still had a submit action.</summary>
        static void Select(UnityEngine.UI.Button button)
        {
            Assume.That(EventSystem.current && button && button.isActiveAndEnabled && button.IsInteractable(), Is.True, "the button is shown and can be pressed");
            EventSystem.current.SetSelectedGameObject(button.gameObject);
        }

        static void SelectButton(ArenaUiCommand command) => Select(Object.FindObjectsByType<ArenaUiButton>(FindObjectsInactive.Exclude)
            .Where(saved => saved.command == command).Select(saved => saved.GetComponent<UnityEngine.UI.Button>())
            .FirstOrDefault(button => button && button.isActiveAndEnabled && button.IsInteractable()));

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
            var hits = new System.Collections.Generic.List<RaycastResult>();
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

        /// <summary>Clicks until done holds: the menus take no input for a moment after a screen opens.</summary>
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

        /// <summary>The part's rectangle in the given space, as V3SmokeTest measures the bottom row.</summary>
        static Rect Box(Transform part, Transform space)
        {
            var corners = new Vector3[4];
            ((RectTransform)part).GetWorldCorners(corners);
            Vector2 a = space.InverseTransformPoint(corners[0]), b = space.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        /// <summary>A free spot about distance cells from centre, off walls and holes.</summary>
        static Vector3 Spot(WorldMap world, Vector3 centre, float distance)
        {
            for (int step = 0; step < 16; step++)
            {
                Vector3 at = centre + Quaternion.Euler(0f, 22.5f * step, 0f) * Vector3.forward * distance;
                at.y = 0f;
                if (world.IsWalkable(at, .6f)) return at;
            }
            Assert.Fail($"No free spot {distance} cells from {centre}.");
            return centre;
        }

        /// <summary>Start of a straight lane of the given length along dir with no wall, hole or gravity ring on it.</summary>
        static Vector3 LaneStart(WorldMap world, Vector3 dir, float length)
        {
            for (int x = -36; x <= 36; x += 4)
            for (int z = -24; z <= 24; z += 4)
            {
                var start = new Vector3(x, 0f, z);
                bool clear = true;
                for (float along = 0f; along <= length && clear; along += .5f) clear = world.IsWalkable(start + dir * along, 1.2f);
                if (clear && !Physics.SphereCast(Query.AtCastHeight(start), 1f, dir, out _, length, GameManager.WallMask, QueryTriggerInteraction.Ignore))
                    return start;
            }
            Assert.Fail("The saved map has no clear lane for the knockback.");
            return Vector3.zero;
        }

        /// <summary>Keeps asking for a command, past the menu input lock, for the given time.</summary>
        static async Task CommandFor(GameManager game, ArenaUiCommand command, float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                game.ExecuteUiCommand(command);
                await Awaitable.NextFrameAsync();
            }
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
