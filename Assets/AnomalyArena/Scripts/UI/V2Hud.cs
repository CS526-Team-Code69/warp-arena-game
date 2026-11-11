using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Retained uGUI view. All objects and references are saved in the scene by the editor installer.
    /// It reads real run data and dispatches commands; it never owns the wave or build clocks.
    /// </summary>
    public partial class V2Hud : MonoBehaviour
    {
        [Header("Run and independent interstellar art")]
        [Tooltip("A MonoBehaviour implementing IArenaUiSource, normally the GameManager.")]
        public MonoBehaviour modelSource;
        public V2ArtSet art;
        [Tooltip("Reference resolution is 1600 × 900; update numeric UI at 10 Hz.")]
        public float refreshInterval = .1f;

        [Header("Scene-authored HUD")]
        public GameObject gameplayRoot;
        public UnityEngine.UI.Text hpLabel, staminaLabel, energyLabel, xpLabel, waveLabel;
        public UnityEngine.UI.Text batchLabel;
        public UnityEngine.UI.Text statusLabel, immunityLabel, hintLabel, bannerLabel;
        public UnityEngine.UI.Text artLabel;
        public UnityEngine.UI.Image hpFill, shieldFill, staminaFill, energyFill, xpFill, chargeFill;
        public GameObject energyRoot, shieldRoot;
        public GameObject bossRoot;
        public UnityEngine.UI.Image bossHpFill, bossShieldFill;
        public UnityEngine.UI.Image[] statusIcons;
        public UnityEngine.UI.Text[] statusCounts;
        [Tooltip("Ten scene-authored markers; counts above ten remain visible in the XP text.")]
        public UnityEngine.UI.Image[] draftPips;
        [Tooltip("Three scene-authored immunity glyphs in the disclosed Tyrant element order.")]
        public UnityEngine.UI.Image[] bossImmunityIcons;
        public UnityEngine.UI.RawImage themeBackground;
        public UnityEngine.UI.Image damageFlash;
        public V2UiSlot[] handSlots, autoSlots, skillSlots;
        public V2UiScreen[] screens;
        [Tooltip("Plays on the picked card when a card is taken; optional.")] public V2CardPickFx cardPickFx;
        public GameObject earlyStartPrompt;
        public UnityEngine.UI.Text earlyStartLabel;
        [Tooltip("Fixed, saved labels reused for floating damage; no runtime UI object creation.")]
        public UnityEngine.UI.Text[] damageLabels;
        [Header("Official Localization")]
        public Canvas localizedCanvas;
        public UnityEngine.UI.Text[] fontLabels;

        // feature/low hp warning. It will be used to flash the screen when the player's health is low. The thresholds and opacity values can be adjusted in the inspector under the V2 canvas. - H.
        [Header("Low Health Warning")]
        [SerializeField, Range(0.01f, 1f)]
        float lowHealthThreshold = 0.50f;
        [SerializeField, Range(0.01f, 1f)]
        float criticalHealthThreshold = 0.50f;
        [SerializeField, Range(0f, 1f)]
        float lowHealthMinOpacity = 0.12f;
        [SerializeField, Range(0f, 1f)]
        float lowHealthMaxOpacity = 0.35f;
        [SerializeField, Min(0.01f)]
        float lowHealthFadeSpeed = 0.6f;
        [SerializeField, Min(0f)]
        float criticalPulseFrequency = 1f;
        float lowHealthOpacity;

        Font appliedFont;

        IArenaUiSource source;
        float nextRefresh, transientTime, flash;
        string transientText;
        Vector3[] damageWorld;
        float[] damageAge;
        Color[] damageColor;
        int damageCursor;
        ArenaUiSnapshot lastSnapshot;
        int seenPick = -1;

        // M168: the web build showed the arena and the player for a moment before the title. The localized canvas
        // (title included) waits for the text tables, so until then a black cover hides the world; it goes as the canvas comes on.
        GameObject bootCover;

        void Awake()
        {
            source = modelSource as IArenaUiSource;
            if (localizedCanvas) localizedCanvas.enabled = ArenaText.Ready;
            if (!ArenaText.Ready)
            {
                bootCover = new GameObject("BootCover", typeof(Canvas), typeof(UnityEngine.UI.Image));
                var cover = bootCover.GetComponent<Canvas>();
                cover.renderMode = RenderMode.ScreenSpaceOverlay; cover.sortingOrder = short.MaxValue;
                var black = bootCover.GetComponent<UnityEngine.UI.Image>();
                black.color = Color.black; black.raycastTarget = false;
            }
            int n = damageLabels?.Length ?? 0;
            damageWorld = new Vector3[n];
            damageAge = new float[n];
            damageColor = new Color[n];
            for (int i = 0; i < n; i++) { damageAge[i] = 1f; damageLabels[i].gameObject.SetActive(false); }
            if (themeBackground) themeBackground.texture = art ? art.themeBackground : null;
            // 2026-11-10: in whitebox the title art behind the title and the first weapon pick is covered by flat blocks.
            WhiteboxBackdrop.Attach(themeBackground);
            SimplifyTitle();
            AddTitleEntries();
            AddTitleLanguageOption();
            AddTitleSettingsButton();
            KeepScreensOverTheRun();
            gameObject.AddComponent<ArenaCodex>().Initialize(this);
            PlaceControlsTables();
            V2SlotFeedback.Add(handSlots, V2SlotFeedback.Kind.Hand);
            V2SlotFeedback.Add(autoSlots, V2SlotFeedback.Kind.Auto);
            V2SlotFeedback.Add(skillSlots, V2SlotFeedback.Kind.Skill);
            FlatBars();
            // The help texts of the choice screens and the weapon card show their key words (ArenaEmphasis), and the
            // weapon card's text is larger; it still shrinks to fit its box.
            if (screens != null)
                foreach (var screen in screens)
                {
                    if (!screen || screen.state is not (ArenaUiState.Loadout or ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.ReplaceWeapon
                            or ArenaUiState.ReplaceSkill or ArenaUiState.Ready or ArenaUiState.WeaponIntro)) continue;
                    ArenaEmphasis.Style(screen.body);
                    if (screen.state == ArenaUiState.WeaponIntro && screen.body)
                        weaponCardSmallest = Mathf.Max(8, screen.body.resizeTextForBestFit ? screen.body.resizeTextMinSize : screen.body.fontSize);
                }
        }

        const int WeaponCardTextSize = 24;
        int weaponCardSmallest = 10;

        // ───── The title (one game in two languages) ─────
        // From the top: the story mode (the saved Start, which V2Campaign routes to the V2 day campaign),
        // the open mode (the classic 15-wave run, after a map pick) and the tutorial (the story mode's pages); the Settings
        // button in the top-left corner (volume, art mode and keys: V2Hud.Settings.cs) and the Language option in the top-right one.
        // The made buttons are copies of saved ones, so they keep their look and V5MenuSkin skins them by name.

        // Until 2026-11-10 the Chinese scene hid these and the English scene showed them. One scene serves both languages now: the
        // tagline stays off (the 10-26 title) and the two saved language buttons give way to the Language option. The saved line of
        // key hints along the bottom ("Controls") and the saved art switch under the entries ("ToggleArt") are not shown either: the
        // keys can be changed in the settings, so a fixed line would not stay true, and the art mode is chosen there too. Both stay
        // in the scene: the entries are still laid out against the hints' place (StackMiddle), and the map pick's back button is a
        // copy of the art switch.
        static readonly string[] TitleHidden = { "InterstellarSubtitle", "ChineseTeam", "EnglishShowcase", "Controls", "ToggleArt" };

        /// <summary>The title's Content, or null in a scene without one.</summary>
        RectTransform TitleContent()
        {
            if (screens == null) return null;
            foreach (var screen in screens)
                if (screen && screen.state == ArenaUiState.Title) return screen.transform.Find("Content") as RectTransform;
            return null;
        }

        void SimplifyTitle()
        {
            var content = TitleContent();
            if (!content) return;
            foreach (var name in TitleHidden)
            {
                var element = content.Find(name);
                if (element) element.gameObject.SetActive(false);
            }
        }

        /// <summary>The saved Start, the story mode's entry.</summary>
        public const string TitleStoryButton = "StartRun";
        /// <summary>The open mode's entry, a copy of Start that opens the map pick (MapSelect) over the title; a picked map starts the run.</summary>
        public const string TitleOpenButton = "StartOpen";
        /// <summary>The tutorial's entry. It opens the story mode's pages (V2Tutorial.ShowStory) over the title, though no campaign runs there
        /// yet, and closing them leaves the title as it was. Its own name: "HowToPlay" is the pages' root and the pause screen's button, and a
        /// path lookup such as "HowToPlay/Panel/Line" must not meet a button that is shown on the title.</summary>
        public const string TitleTutorialButton = "TitleTutorial";
        /// <summary>The Language option in the top-right corner, a copy of a saved language button.</summary>
        public const string TitleLanguageButton = "TitleLanguage";
        /// <summary>The open mode's map pick: a group under the title's Content with a line and four buttons (MapButtons).</summary>
        public const string MapSelect = "MapSelect";
        /// <summary>The map pick's buttons from the top: the Dunes, the Ruins, a random map, back to the title. The first three send
        /// ArenaUiCommand.ChooseMap with their place as the index (GameManager.OpenModeMaps).</summary>
        public static readonly string[] MapButtons = { "MapDunes", "MapRuins", "MapRandom", "MapBack" };
        /// <summary>Paths under the title's Content that take Start's skin (V5MenuSkin), and those that take the plain button's.</summary>
        public static readonly string[] TitlePrimaryButtons =
            { TitleStoryButton, TitleOpenButton, TitleTutorialButton, MapSelect + "/MapDunes", MapSelect + "/MapRuins", MapSelect + "/MapRandom" };
        public static readonly string[] TitleSecondaryButtons = { TitleSettingsButton, TitleLanguageButton, MapSelect + "/MapBack" };

        // Heights in canvas units: the two starts as tall as the saved Start (76), the tutorial and the map pick's choices a little lower,
        // so the starts stay the first thing the eye finds. The art switch and the back button keep the saved art switch's height.
        const float TitleTutorialHeight = 64f, MapChoiceHeight = 64f, MapLineHeight = 34f;
        // The Language option: its size and its distance from the top-right corner. 14 down keeps it above the logo's drawn top (about 62).
        const float LanguageWidth = 180f, LanguageHeight = 42f, LanguageRight = 20f, LanguageTop = 14f;
        // The label sizes the fitting starts from and may go down to (ArenaTextFit): "Start Game - Open Mode" at the saved 36 is about as
        // wide as the button.
        const int EntrySmallest = 18, LanguageLargest = 24, LanguageSmallest = 14, MapLineSize = 26;

        UnityEngine.UI.Text titleStoryLabel, titleOpenLabel, titleTutorialLabel, titleLanguageLabel, mapLine;
        readonly UnityEngine.UI.Text[] mapLabels = new UnityEngine.UI.Text[4];
        RectTransform titleStory, titleOpen, titleTutorial, titleArt, titleLanguage, mapSelect;
        int entryLargest = 36;
        Font titleFittedFont;
        bool mapSelectOpen;

        /// <summary>The open mode's map pick is showing in place of the title's entries.</summary>
        public bool MapSelectOpen => mapSelectOpen;

        void AddTitleEntries()
        {
            var content = TitleContent();
            if (!content || content.Find(TitleOpenButton) || content.Find(TitleStoryButton) is not RectTransform start) return;
            titleStory = start;
            titleArt = content.Find("ToggleArt") as RectTransform;
            titleStoryLabel = start.GetComponentInChildren<UnityEngine.UI.Text>(true);
            if (titleStoryLabel) entryLargest = Mathf.Max(EntrySmallest, titleStoryLabel.fontSize);
            // The saved label read "ui.start" through a localizer; the entries' words are set by code now (RenderTitle).
            DropLocalizers(start);
            // The gap the saved title keeps between Start and the art switch, for the whole stack.
            float gap = titleArt ? Mathf.Clamp(Bottom(start) - Top(titleArt), 8f, 40f) : 22f;

            // The open mode: a copy of Start that opens the map pick; the pick starts the run. Its own name keeps V2Campaign's routing of
            // the saved Start (Content/StartRun) off it.
            var open = CopyAsPlainButton(start, TitleOpenButton, () =>
            {
                V2Audio.Instance?.PlayUI(ArenaUiSound.Select);
                OpenMapSelect();
            });
            open.transform.SetSiblingIndex(start.GetSiblingIndex() + 1);
            titleOpen = (RectTransform)open.transform;
            titleOpenLabel = titleOpen.GetComponentInChildren<UnityEngine.UI.Text>(true);

            var tutorial = CopyAsPlainButton(start, TitleTutorialButton, () =>
            {
                V2Audio.Instance?.PlayUI(ArenaUiSound.Select);
                V2Tutorial.ShowStory(appliedFont);
            });
            tutorial.transform.SetSiblingIndex(titleOpen.GetSiblingIndex() + 1);
            titleTutorial = (RectTransform)tutorial.transform;
            titleTutorialLabel = titleTutorial.GetComponentInChildren<UnityEngine.UI.Text>(true);

            var stack = new System.Collections.Generic.List<(RectTransform part, float height)>
                { (start, start.sizeDelta.y), (titleOpen, start.sizeDelta.y), (titleTutorial, TitleTutorialHeight) };
            // The art switch is not in the stack any more: the art mode is chosen in the settings (V2Hud.Settings.cs).
            LayStack(stack, gap, StackMiddle(content, start, stack, gap), start);

            AddMapSelect(content, start, gap);
        }

        /// <summary>The open mode's map pick, made hidden with the title: a line saying what to pick, then the Dunes, the Ruins and a random
        /// map (copies of Start), then back to the title (a copy of the art switch). Picking a map starts the classic 15-wave run on it
        /// (ArenaUiCommand.ChooseMap); the story mode never comes here.</summary>
        void AddMapSelect(RectTransform content, RectTransform start, float gap)
        {
            var group = new GameObject(MapSelect, typeof(RectTransform)).GetComponent<RectTransform>();
            group.SetParent(content, false);
            group.anchorMin = Vector2.zero;
            group.anchorMax = Vector2.one;
            group.offsetMin = group.offsetMax = Vector2.zero;

            mapLine = new GameObject("MapLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(UnityEngine.UI.Text)).GetComponent<UnityEngine.UI.Text>();
            mapLine.rectTransform.SetParent(group, false);
            mapLine.font = titleStoryLabel ? titleStoryLabel.font : null;
            mapLine.fontSize = MapLineSize;
            mapLine.alignment = TextAnchor.MiddleCenter;
            mapLine.color = new Color(.86f, .90f, .96f);
            mapLine.raycastTarget = false;
            mapLine.horizontalOverflow = HorizontalWrapMode.Overflow;
            // The line is taller than its row with the Chinese font: without this the whole line is cut away and nothing shows.
            mapLine.verticalOverflow = VerticalWrapMode.Overflow;
            mapLine.rectTransform.anchorMin = mapLine.rectTransform.anchorMax = start.anchorMin;
            mapLine.rectTransform.pivot = start.pivot;
            mapLine.rectTransform.sizeDelta = new Vector2(start.sizeDelta.x, MapLineHeight);

            var stack = new System.Collections.Generic.List<(RectTransform part, float height)> { (mapLine.rectTransform, MapLineHeight) };
            for (int i = 0; i < MapButtons.Length; i++)
            {
                int choice = i;
                bool back = i == MapButtons.Length - 1;
                var original = back && titleArt ? titleArt : start;
                var button = CopyAsPlainButton(original, MapButtons[i], back ? CloseMapSelect : () => Dispatch(ArenaUiCommand.ChooseMap, choice));
                button.transform.SetParent(group, false);
                button.gameObject.SetActive(true);
                mapLabels[i] = button.GetComponentInChildren<UnityEngine.UI.Text>(true);
                stack.Add(((RectTransform)button.transform, back ? original.sizeDelta.y : MapChoiceHeight));
            }
            LayStack(stack, gap * .75f, StackMiddle(content, start, stack, gap * .75f), start);
            mapSelect = group;
            group.gameObject.SetActive(false);
        }

        /// <summary>Opens the map pick in place of the title's entries; the keyboard's selection moves to its first map.</summary>
        public void OpenMapSelect()
        {
            if (!mapSelect || mapSelectOpen) return;
            mapSelectOpen = true;
            ShowTitleEntries();
            Select(mapSelect.Find(MapButtons[0]));
            RefreshView();
        }

        /// <summary>Back to the title's entries (the back button, Esc, or a gamepad's east button); the selection goes back to the open mode.</summary>
        public void CloseMapSelect()
        {
            if (!mapSelectOpen) return;
            mapSelectOpen = false;
            V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
            ShowTitleEntries();
            Select(titleOpen);
            RefreshView();
        }

        void ShowTitleEntries()
        {
            foreach (var part in new[] { titleStory, titleOpen, titleTutorial, titleSettings, titleLanguage })
                if (part && part.gameObject.activeSelf == mapSelectOpen) part.gameObject.SetActive(!mapSelectOpen);
            if (mapSelect && mapSelect.gameObject.activeSelf != mapSelectOpen) mapSelect.gameObject.SetActive(mapSelectOpen);
        }

        static void Select(Transform part)
        {
            var events = UnityEngine.EventSystems.EventSystem.current;
            if (events && part) events.SetSelectedGameObject(part.gameObject);
        }

        /// <summary>A gamepad's east button on the map pick goes back to the title, as its back button does; Esc goes through the game's
        /// key handling (GameManager.PressKey), where the tests reach it. The pick shows only on the title.</summary>
        void ReadMapSelectBack()
        {
            if (!mapSelectOpen || V2Tutorial.HoldsInput) return;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null && pad.buttonEast.wasPressedThisFrame) CloseMapSelect();
        }

        /// <summary>The Language option, top right: "Language" in both languages, so it can be found whatever language shows. A click switches
        /// between Chinese and English at once and saves the choice (ArenaLanguage.ToggleChoice).</summary>
        void AddTitleLanguageOption()
        {
            var content = TitleContent();
            if (!content || content.Find(TitleLanguageButton)) return;
            var original = content.Find("EnglishShowcase") ?? content.Find("ToggleArt");
            if (!original) return;
            var button = CopyAsPlainButton(original, TitleLanguageButton, () =>
            {
                if (ArenaLanguage.ToggleChoice()) V2Audio.Instance?.PlayUI(ArenaUiSound.Confirm);
            });
            // The saved language buttons select one Locale each; the option's own click does the switching.
            if (button.TryGetComponent<ArenaLocaleButton>(out var locale)) Destroy(locale);
            button.gameObject.SetActive(true);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(LanguageWidth, LanguageHeight);
            rect.anchoredPosition = new Vector2(-LanguageRight, -LanguageTop);
            // The saved rules above and under the label are as wide as the button they came from.
            foreach (Transform child in rect)
                if (child is RectTransform rule && child.name.Contains("Rule") && rule.anchorMin.x == rule.anchorMax.x)
                    rule.sizeDelta = new Vector2(LanguageWidth - 16f, rule.sizeDelta.y);
            titleLanguage = rect;
            titleLanguageLabel = rect.GetComponentInChildren<UnityEngine.UI.Text>(true);
        }

        /// <summary>The middle of the band between the logo's foot and the top of the saved key hints (no longer shown, they still mark
        /// where the entries' band ends). The logo is anchored to the top edge and the hints to the bottom edge, so the band's middle
        /// sits at the same height from the centre whatever the canvas height; on the 1440 x 810 reference the three entries leave
        /// about 58 units above and below, the map pick about 15. Without those two parts the stack keeps Start where it is.</summary>
        static float StackMiddle(Transform content, RectTransform start, System.Collections.Generic.List<(RectTransform part, float height)> stack, float gap)
        {
            float total = -gap;
            foreach (var (_, height) in stack) total += height + gap;
            bool edgeAnchored = content.Find("WarpArenaLogo") is RectTransform logo && content.Find("Controls") is RectTransform keys
                && logo.anchorMin.y == 1f && logo.anchorMax.y == 1f && keys.anchorMin.y == 0f && keys.anchorMax.y == 0f;
            if (start.anchorMin != start.anchorMax || start.anchorMin.y != .5f || !edgeAnchored) return Top(start) - total * .5f;
            var logoRect = (RectTransform)content.Find("WarpArenaLogo");
            var keysRect = (RectTransform)content.Find("Controls");
            return (Bottom(logoRect) + Top(keysRect)) * .5f;
        }

        /// <summary>Lays the parts from the top down, each the given height and as wide as Start, centred on Start's x, the gap apart, the
        /// whole stack centred on middle (all measured from Start's anchor).</summary>
        static void LayStack(System.Collections.Generic.List<(RectTransform part, float height)> stack, float gap, float middle, RectTransform start)
        {
            float total = -gap;
            foreach (var (_, height) in stack) total += height + gap;
            float top = middle + total * .5f;
            foreach (var (part, height) in stack)
            {
                part.anchorMin = start.anchorMin;
                part.anchorMax = start.anchorMax;
                part.sizeDelta = new Vector2(start.sizeDelta.x, height);
                part.anchoredPosition = new Vector2(start.anchoredPosition.x, top - (1f - part.pivot.y) * height);
                top -= height + gap;
            }
        }

        static void DropLocalizers(Transform part)
        {
            foreach (var localizer in part.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true))
            {
                localizer.enabled = false;
                Destroy(localizer);
            }
        }

        /// <summary>The title's words in the language showing, fitted to their buttons. The made labels take the font the saved Start label
        /// has (the loaded font reaches the saved labels only), and every label is fitted again when that font changes.</summary>
        void RenderTitle()
        {
            if (!titleStoryLabel) return;
            bool refit = titleStoryLabel.font != titleFittedFont;
            titleFittedFont = titleStoryLabel.font;
            TitleLabel(titleStoryLabel, ArenaText.Pair("开始游戏-剧情模式", "Start Game - Story Mode"), entryLargest, EntrySmallest, refit);
            TitleLabel(titleOpenLabel, ArenaText.Pair("开始游戏-开放模式", "Start Game - Open Mode"), entryLargest, EntrySmallest, refit);
            TitleLabel(titleTutorialLabel, ArenaText.Pair("教学", "Tutorial"), entryLargest, EntrySmallest, refit);
            // The same word in both languages on purpose.
            TitleLabel(titleLanguageLabel, ArenaText.Pair("Language", "Language"), LanguageLargest, LanguageSmallest, refit);
            TitleLabel(titleSettingsLabel, ArenaText.Pair("设置", "Settings"), LanguageLargest, LanguageSmallest, refit);
            if (mapLine)
            {
                if (mapLine.font != titleFittedFont && titleFittedFont) mapLine.font = titleFittedFont;
                SetText(mapLine, ArenaText.Pair("开放模式 · 选择地图", "Open Mode · Choose a Map"));
            }
            string[] mapWords =
            {
                ArenaText.Pair("沙丘", "Dunes"), ArenaText.Pair("遗迹", "Ruins"), ArenaText.Pair("随机", "Random"), ArenaText.Pair("返回标题", "Back to Title"),
            };
            for (int i = 0; i < mapLabels.Length; i++)
                TitleLabel(mapLabels[i], mapWords[i], i == mapLabels.Length - 1 ? LanguageLargest : entryLargest, EntrySmallest, refit);
        }

        void TitleLabel(UnityEngine.UI.Text label, string text, int largest, int smallest, bool refit)
        {
            if (!label) return;
            if (label != titleStoryLabel && titleFittedFont && label.font != titleFittedFont) label.font = titleFittedFont;
            if (label.text == text && !refit) return;
            label.text = text;
            ArenaTextFit.Refit(label, largest, smallest);
        }

        static float Top(RectTransform rect) => rect.anchoredPosition.y + (1f - rect.pivot.y) * rect.sizeDelta.y;
        static float Bottom(RectTransform rect) => rect.anchoredPosition.y - rect.pivot.y * rect.sizeDelta.y;

        // A screen covers the run. The saved canvas draws these after the screens, so the crate and its hint, the damage numbers and the
        // red edge showed over the pause panel. They go right under the screens, still over the HUD; the card pick's effect stays above the cards it plays on.
        static readonly string[] UnderTheScreens = { "DamageNumberPool", "PlayerDamageFlash", "SupplyDropOverlay" };

        void KeepScreensOverTheRun()
        {
            if (!localizedCanvas || screens == null) return;
            var canvas = localizedCanvas.transform;
            int first = canvas.childCount;
            foreach (var screen in screens)
                if (screen && screen.transform.parent == canvas) first = Mathf.Min(first, screen.transform.GetSiblingIndex());
            foreach (var name in UnderTheScreens)
                if (canvas.Find(name) is { } part && part.GetSiblingIndex() > first) part.SetSiblingIndex(first++);
        }

        // 2026-11-10 (second round): in whitebox the HUD's bars (life, shield, stamina, energy, XP, charge, the boss's) and their
        // backgrounds are plain rectangles: a square sprite in place of the rounded one, so they still fill; their colours stay.
        readonly WhiteboxSkin.Set flatBars = new();

        void FlatBars()
        {
            foreach (var fill in new[] { hpFill, shieldFill, staminaFill, energyFill, xpFill, chargeFill, bossHpFill, bossShieldFill })
            {
                if (!fill) continue;
                flatBars.Add(fill, WhiteboxSkin.Look.Bar);
                if (fill.transform.parent is { } parent && parent.name.EndsWith("Background") && parent.TryGetComponent(out UnityEngine.UI.Image back))
                    flatBars.Add(back, WhiteboxSkin.Look.Bar);
            }
        }

        void Update()
        {
            flatBars.Follow();
            if (localizedCanvas && localizedCanvas.enabled != ArenaText.Ready) localizedCanvas.enabled = ArenaText.Ready;
            if (!ArenaText.Ready) return;
            if (bootCover) Destroy(bootCover);
            ReadMapSelectBack();
            if (ArenaText.Font && appliedFont != ArenaText.Font)
            {
                appliedFont = ArenaText.Font;
                if (fontLabels != null) foreach (var label in fontLabels) if (label) label.font = appliedFont;
                nextRefresh = 0f;
            }
            float dt = Time.unscaledDeltaTime;
            transientTime = Mathf.Max(0f, transientTime - dt);
            flash = Mathf.Max(0f, flash - dt * 3f);
            if (damageFlash)
            {
                // Document 3.6: a hit reddens the screen's edges and leaves the centre clear (it used to tint the whole screen faintly).
                if (!edgeVignette) { edgeVignette = EdgeVignette(); damageFlash.sprite = edgeVignette; damageFlash.type = UnityEngine.UI.Image.Type.Simple; }
                TickHealthWarning(dt);
            }
            TickDamage(dt);
            // A card pick is shown at once instead of waiting for the next 10 Hz refresh.
            if (lastSnapshot != null && lastSnapshot.cardPickSequence != seenPick) nextRefresh = 0f;
            if (source == null || Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + Mathf.Max(.02f, refreshInterval);
            var snapshot = source.UiSnapshot;
            Render(snapshot);
            ShowTutorialOnArrival(snapshot);
        }
        // feature/low hp warning. It will be used to flash the screen when the player's health is low. The thresholds and opacity values can be adjusted in the inspector under the V2 canvas. - H.
        void TickHealthWarning(float dt)
        {
            if (!damageFlash) return;

            damageFlash.raycastTarget = false;

            var s = lastSnapshot;

            bool showFeedback =
                source != null &&
                gameplayRoot &&
                gameplayRoot.activeInHierarchy &&
                s != null &&
                s.maxHp > 0f &&
                s.hp > 0f;

            if (!showFeedback)
            {
                lowHealthOpacity = 0f;
                flash = 0f;
                damageFlash.color = new Color(1f, .1f, .04f, 0f);
                return;
            }

            float healthRatio = Mathf.Clamp01(s.hp / s.maxHp);
            float threshold = Mathf.Clamp(lowHealthThreshold, 0.01f, 1f);
            float targetOpacity = 0f;

            if (healthRatio <= threshold)
            {
                float severity = Mathf.Clamp01(1f - healthRatio / threshold);

                targetOpacity = Mathf.Lerp(
                    lowHealthMinOpacity,
                    lowHealthMaxOpacity,
                    severity);
            }

            lowHealthOpacity = Mathf.MoveTowards(
                lowHealthOpacity,
                targetOpacity,
                lowHealthFadeSpeed * dt);

            float pulse = 0.5f + 0.5f * Mathf.Sin(
                Time.unscaledTime *
                criticalPulseFrequency *
                Mathf.PI * 2f);

            float criticalThreshold = Mathf.Clamp(
                criticalHealthThreshold, 0f, threshold);

            float minimumBrightness =
                healthRatio <= criticalThreshold ? 0.35f : 0.65f;

            float pulsingOpacity = lowHealthOpacity *
                Mathf.Lerp(minimumBrightness, 1f, pulse);

            float finalOpacity = Mathf.Max(
                flash * .45f,
                pulsingOpacity);

            damageFlash.color =new Color(1f, .1f, .04f, finalOpacity);
        }

        public void Dispatch(ArenaUiCommand command, int index = -1)
        {
            // The classic run's first-time pages: the open mode's map pick starts it (ChooseMap); the tests and the lab send Start.
            if ((command is ArenaUiCommand.Start or ArenaUiCommand.ChooseMap) && source != null && source.UiSnapshot.state == ArenaUiState.Title) tutorialPending = true;
            source?.ExecuteUiCommand(command, index);
        }

        public void RefreshView(){if(source!=null&&ArenaText.Ready)Render(source.UiSnapshot);}

        public void ShowBanner(string message, float seconds = 2.2f)
        {
            transientText = message;
            transientTime = seconds;
        }

        public void FlashDamage() => flash = 1f;

        Sprite edgeVignette;
        /// <summary>White with alpha rising from the centre to the edges: clear within 45% of the way out, then a smooth rise to full at
        /// the edges' middle and beyond. The flash image stretches it over the screen and tints it red.</summary>
        public static Sprite EdgeVignette()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "HitEdgeVignette", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + .5f) / size * 2f - 1f, dy = (y + .5f) / size * 2f - 1f;
                    float t = Mathf.Clamp01((Mathf.Sqrt(dx * dx + dy * dy) - .45f) / .55f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * t * t * (3f - 2f * t)));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        public void DamageNumber(Vector3 world, float amount, Color color, bool heavy)
        {
            if (damageLabels == null || damageLabels.Length == 0) return;
            int i = damageCursor++ % damageLabels.Length;
            damageWorld[i] = world;
            damageAge[i] = 0f;
            damageColor[i] = color;
            var label = damageLabels[i];
            label.text = $"-{Mathf.RoundToInt(amount)}";
            label.fontSize = heavy ? 30 : 22;
            label.gameObject.SetActive(true);
        }

        void TickDamage(float dt)
        {
            var cam = GameManager.Instance ? GameManager.Instance.cam : null;
            if (!cam || damageLabels == null) return;
            for (int i = 0; i < damageLabels.Length; i++)
            {
                if (damageAge[i] >= .8f) continue;
                damageAge[i] += dt;
                var label = damageLabels[i];
                if (damageAge[i] >= .8f) { label.gameObject.SetActive(false); continue; }
                Vector3 sp = cam.WorldToScreenPoint(damageWorld[i] + cam.transform.up * (.6f + damageAge[i] * 2f));
                if (sp.z <= 0f) { label.gameObject.SetActive(false); continue; }
                label.rectTransform.position = sp;
                float pop = 1f + .5f * Mathf.Clamp01(1f - damageAge[i] / .12f);
                label.rectTransform.localScale = Vector3.one * pop;
                Color c = damageColor[i]; c.a *= 1f - damageAge[i] / .8f;
                label.color = c;
            }
        }

        void Render(ArenaUiSnapshot s)
        {
            if (s == null) return;
            lastSnapshot = s;
            // Checked before the screens redraw, so the picked card still shows what was taken.
            if (seenPick < 0) seenPick = s.cardPickSequence;
            else if (s.cardPickSequence != seenPick) { seenPick = s.cardPickSequence; PlayCardPick(s); }
            // Walking the arena during the build phase keeps the gameplay HUD (hint, hole labels, markers) in view.
            bool gameplay = s.state is ArenaUiState.SupplyDrop or ArenaUiState.WaveIntro or ArenaUiState.WaveActive or ArenaUiState.WaveEnd
                or ArenaUiState.VoidTrial or ArenaUiState.FinalBoss or ArenaUiState.TrialReturn or ArenaUiState.Cutscene or ArenaUiState.LevelUp or ArenaUiState.Night || s.buildWalking;
            gameplayRoot.SetActive(gameplay);
            if (waveLabel) waveLabel.gameObject.SetActive(s.state != ArenaUiState.SupplyDrop);
            if (themeBackground) themeBackground.gameObject.SetActive(s.state is ArenaUiState.Title or ArenaUiState.Loadout);
            SetText(hpLabel, ArenaText.Text("ui.hp", Mathf.CeilToInt(s.hp), Mathf.CeilToInt(s.maxHp)));
            SetFill(hpFill, s.hp, s.maxHp);
            shieldRoot.SetActive(s.shield > 0f);
            // The bar is full at 60% of max HP, the level-5 Bulwark's Shield.
            SetFill(shieldFill, s.shield, s.maxHp * .6f);
            SetText(staminaLabel, ArenaText.Text("ui.stamina"));
            SetFill(staminaFill, s.stamina, 1f);
            if (staminaFill) staminaFill.color = s.staminaExhausted ? new Color(.48f, .55f, .63f) : ElementColors[3];
            energyRoot.SetActive(s.energyUnlocked);
            SetText(energyLabel, ArenaText.Text(s.voidEnergy ? "ui.energy.void" : "ui.energy", Mathf.CeilToInt(s.energy), Mathf.CeilToInt(s.maxEnergy)));
            SetFill(energyFill, s.energy, s.maxEnergy);
            energyFill.color = s.voidEnergy ? ElementColors[6] : new Color(.22f, .68f, .93f);
            string level = s.level >= s.maxLevel ? ArenaText.Text("ui.level.max") : s.level.ToString();
            // V2 day campaign (hideXp): kills give no XP and every upgrade is bought at the base, so the line shows the
            // level alone and the XP bar and the banked-pick markers are hidden. The classic run keeps level, XP and picks.
            SetText(xpLabel, s.hideXp ? ArenaText.Pair($"等级 {level}", $"LEVEL {level}")
                : ArenaText.Text("ui.xp", level, Mathf.FloorToInt(s.xp), Mathf.CeilToInt(s.xpNeeded), s.bankedDrafts));
            // Document 9.3: once the Void Trial is won (its extra level), the level reads in void purple at any level.
            xpLabel.color = s.voidLevel ? ElementColors[6] : Color.white;
            SetFill(xpFill, s.xp, s.xpNeeded);
            var xpBar = XpBar;
            if (xpBar && xpBar.activeSelf == s.hideXp) xpBar.SetActive(!s.hideXp);
            if (draftPips != null)
                for (int i = 0; i < Mathf.Min(10, draftPips.Length); i++)
                {
                    if (!draftPips[i]) continue;
                    bool visible = !s.hideXp && i < s.bankedDrafts;
                    if (draftPips[i].gameObject.activeSelf != visible) draftPips[i].gameObject.SetActive(visible);
                }
            SetText(waveLabel, !string.IsNullOrEmpty(s.campaignLabel) && s.state is not (ArenaUiState.FinalBoss or ArenaUiState.VoidTrial) ? s.campaignLabel
                : s.state is ArenaUiState.FinalBoss or ArenaUiState.VoidTrial
                ? ArenaText.Text("ui.boss.hp", s.state == ArenaUiState.FinalBoss ? ArenaText.Pair("暴虐君王", "THE TYRANT KING") : ArenaText.Pair("纳什先知", "THE NASH PROPHET"), Mathf.CeilToInt(s.bossHp), Mathf.CeilToInt(s.bossMaxHp), Mathf.CeilToInt(s.bossShield))
                : s.waveTimeExpired ? ArenaText.Pair($"第 {s.wave:00} / {s.maxWaves:00} 波 · 清场中", $"WAVE {s.wave:00} / {s.maxWaves:00} · CLEAR SURVIVORS")
                : ArenaText.Text("ui.wave", s.wave.ToString("00"), s.maxWaves.ToString("00"), Mathf.CeilToInt(s.waveTime).ToString("00")));
            if (batchLabel)
            {
                // In the V2 campaign the count shows only while it has something to count (not on Day 0's walk or by the black hole in
                // the afternoon, where no wave runs).
                batchLabel.gameObject.SetActive(s.state == ArenaUiState.WaveActive && !(CampaignControls && s.waveLiving == 0 && s.wavePending == 0));
                batchLabel.supportRichText = true; // alive in red, pending in yellow
                SetText(batchLabel, s.waveTimeExpired
                    ? ArenaText.Pair($"已停止生成 · <color=#FF5F4F>剩余活敌 {s.waveLiving}</color>", $"SPAWNING STOPPED · <color=#FF5F4F>{s.waveLiving} ALIVE</color>")
                    : ArenaText.Pair($"<color=#FF5F4F>活敌 {s.waveLiving}</color> · <color=#FFD24A>待生成 {s.wavePending}</color>", $"<color=#FF5F4F>{s.waveLiving} ALIVE</color> · <color=#FFD24A>{s.wavePending} PENDING</color>"));
            }
            bool boss = s.state is ArenaUiState.VoidTrial or ArenaUiState.FinalBoss;
            waveLabel.color = !boss && s.waveTime <= 5f ? new Color(1f, .53f, .31f) : Color.white;
            if (bossRoot) bossRoot.SetActive(boss);
            SetFill(bossHpFill, s.bossHp, s.bossMaxHp);
            SetFill(bossShieldFill, s.bossShield, s.bossMaxHp);
            if (bossShieldFill) bossShieldFill.transform.parent.gameObject.SetActive(s.bossShield > 0f);
            RenderPlayerStatuses(s.playerStatuses);
            SetText(immunityLabel, s.bossImmunities);
            if (bossImmunityIcons != null)
                for (int i = 0; i < bossImmunityIcons.Length; i++)
                {
                    int index = s.wave >= 11 && s.bossImmunityIndices != null && i < s.bossImmunityIndices.Length ? s.bossImmunityIndices[i] : -1;
                    SetElementIcon(bossImmunityIcons[i], art, index);
                }
            SetText(hintLabel, s.hint);
            SetText(bannerLabel, transientTime > 0f ? transientText : s.banner);
            SetText(artLabel, ArenaText.Text(s.illustrated ? "ui.art.illustrated" : "ui.art.whitebox"));
            chargeFill.gameObject.SetActive(s.charge01 > 0f);
            SetFill(chargeFill, s.charge01, 1f);
            RenderSlots(handSlots, s.handSlots); RenderSlots(autoSlots, s.autoSlots, s.hideAutoSlots); RenderSlots(skillSlots, s.skillSlots, s.hideSkillSlots);
            foreach (var screen in screens)
            {
                // The mid-wave level-up pick reuses the Draft screen.
                bool active = (screen.state == s.state || screen.state == ArenaUiState.Draft && s.state == ArenaUiState.LevelUp) && !(s.buildWalking && screen.state is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.Ready);
                if (screen.gameObject.activeSelf != active) screen.gameObject.SetActive(active);
                if (active) RenderScreen(screen, s);
            }
            earlyStartPrompt.SetActive(s.earlyStartConfirmation);
            SetText(earlyStartLabel, s.hideXp
                ? ArenaText.Pair($"还有 {s.bankedDrafts} 次升级没选。随机选完剩下的，然后开始？", $"Upgrades still unchosen: {s.bankedDrafts}. Pick the rest at random and start?")
                : ArenaText.Text("ui.early.question", s.bankedDrafts));
        }

        /// <summary>The XP bar the V2 day campaign hides: the fill's background (its parent, which V2HudLayout places as the bar), or the
        /// fill alone when it sits straight in the header beside the level line.</summary>
        GameObject XpBar
        {
            get
            {
                if (!xpFill) return null;
                var parent = xpFill.transform.parent;
                bool ownBar = parent && (!xpLabel || parent != xpLabel.transform.parent) && (!gameplayRoot || parent != gameplayRoot.transform);
                return ownBar ? parent.gameObject : xpFill.gameObject;
            }
        }

        void PlayCardPick(ArenaUiSnapshot s)
        {
            V2Audio.Instance?.PlayCardPick(s.cardPickRarity);
            if (!cardPickFx || screens == null || s.cardPickIndex < 0) return;
            foreach (var screen in screens)
            {
                if (!screen || screen.state != s.cardPickState || screen.choices == null || s.cardPickIndex >= screen.choices.Length) continue;
                var choice = screen.choices[s.cardPickIndex];
                if (choice && choice.gameObject.activeInHierarchy) cardPickFx.Play((RectTransform)choice.transform, s.cardPickRarity);
                return;
            }
        }

        void RenderSlots(V2UiSlot[] views, ArenaUiSlot[] values, bool hidden = false)
        {
            if (views == null || values == null) return;
            for (int i = 0; i < views.Length; i++)
            {
                // Saved scenes may hold more slot views than the current rules use (three auto slots became two).
                // A hidden group shows none of its slots (the story mode before its first auto weapon or skill, since 2026-11-10).
                bool used = i < values.Length && !hidden;
                if (views[i] && views[i].gameObject.activeSelf != used) views[i].gameObject.SetActive(used);
                if (used && views[i]) views[i].Render(values[i], art);
            }
        }

        void RenderScreen(V2UiScreen screen, ArenaUiSnapshot s)
        {
            bool build = s.state is ArenaUiState.Armory or ArenaUiState.Draft or ArenaUiState.ReplaceWeapon
                or ArenaUiState.ReplaceSkill or ArenaUiState.Ready;
            if (screen.clock)
            {
                screen.clock.gameObject.SetActive(build && !s.isGreaterReward);
                SetText(screen.clock, build && !s.isGreaterReward ? s.selectionUnlimited ? ArenaText.Pair("选择不限时", "NO TIME LIMIT") : ArenaText.Text("ui.build.clock", Mathf.CeilToInt(s.buildTime).ToString("00")) : "");
                screen.clock.color = !s.selectionUnlimited && s.buildTime <= 5f ? ElementColors[0] : ElementColors[2];
            }
            if (screen.summary) SetText(screen.summary, s.buildSummary);
            if (screen.reroll) screen.reroll.gameObject.SetActive(!s.isGreaterReward);
            // V2 day campaign (since 2026-11-06): its void trial may be entered in the afternoon, when no wave waits to go back to.
            if (screen.startNext)
                SetText(screen.startNext.GetComponentInChildren<UnityEngine.UI.Text>(), s.isGreaterReward
                    ? s.hideXp ? ArenaText.Pair("放弃技能并返回", "RETURN / DECLINE SKILL") : ArenaText.Text("ui.return.reward")
                    : s.state == ArenaUiState.LevelUp ? ArenaText.Pair("稍后再选 [Esc]", "PICK LATER [ESC]") : ArenaText.Text("ui.nextwave"));
            ArenaUiCard[] cards = null;
            switch (s.state)
            {
                case ArenaUiState.Title:
                    // The game's name is English in every Locale; the entries' words follow the language (RenderTitle).
                    SetText(screen.heading, "WARP ARENA");
                    SetText(screen.body, "");
                    RenderTitle();
                    break;
                case ArenaUiState.Loadout:
                    cards = s.loadoutChoices;
                    // V2 day campaign: the game has no cards any more, so its text names none.
                    SetBody(screen.body, s.hideXp
                        ? ArenaText.Pair("选择一种熟悉的外形。拿到之前不知道是哪种攻击方式和特性；选中的那一刻揭晓：游戏暂停，显示这件武器的说明。\n手动瞄准，利用地形。", "Pick one familiar form. Its attack and trait are unknown until you take it; the moment you choose, the game pauses and shows what the weapon does.\nAim manually; use the terrain.")
                        : ArenaText.Pair("选择一种熟悉的外形。拿到之前不知道是哪种攻击方式和特性；选中的那一刻揭晓：游戏暂停，弹出武器卡。\n手动瞄准，利用地形。", "Pick one familiar form. Its attack and trait are unknown until you take it; the moment you choose, the game pauses and the weapon card appears.\nAim manually; use the terrain."));
                    break;
                case ArenaUiState.Armory:
                    cards = s.armoryOffers;
                    SetBody(screen.body, ArenaText.Pair("三选一：一件新武器，或把已持有的武器强化到更高型号（攻击方式、特性和已有强化都不变）。也可以跳过。新武器替换掉旧武器时，旧武器身上的强化会一起没了。", "Pick one of three: a new weapon, or a higher Mk for one you hold (its attack, trait and upgrades stay). You may skip. A new weapon that replaces an old one leaves the old one's upgrades behind.") + WalkHint());
                    break;
                case ArenaUiState.Draft:
                    cards = s.draftCards;
                    SetText(screen.heading, s.isGreaterReward ? ArenaText.Text("ui.greater.heading")
                        : s.hideXp ? ArenaText.Pair($"升级 {s.draftIndex} / {Mathf.Max(1, s.draftCount)}", $"UPGRADE {s.draftIndex} / {Mathf.Max(1, s.draftCount)}")
                        : ArenaText.Text("ui.draft.counter", s.draftIndex, Mathf.Max(1, s.draftCount)));
                    SetBody(screen.body, s.isGreaterReward ? ArenaText.Pair("从三个高阶虚空技能里选一个（可以不选），没有后续升级。按 " + ArenaKeys.SkillKeys + " 选择。", "Pick one of three Greater Void skills (or none); they have no further levels. " + ArenaKeys.SkillKeys + " select.")
                        : s.hideXp ? ArenaText.Pair("三选一。按 " + ArenaKeys.SkillKeys + " 选择；R 重抽三项（每波一次）。", "Pick one of three. " + ArenaKeys.SkillKeys + " select; R rerolls all three (once per wave).") + WalkHint()
                        : ArenaText.Pair("三张卡每种一张：属性卡、武器卡（给一把已经揭晓的武器一个还没拿过的强化方向）、技能卡（一个新技能，或者给已有技能升级；卡的颜色代表它给的等级）。按 " + ArenaKeys.SkillKeys + " 选择；R 重抽三张（每波一次）。", "Three cards, one of each: a stat card, a weapon card (an upgrade direction a revealed weapon has not taken yet) and a skill card (a new skill, or a higher level for one you own; its colour shows the level it gives). " + ArenaKeys.SkillKeys + " select; R rerolls all three (once per wave).") + WalkHint());
                    if (screen.reroll) screen.reroll.interactable = s.rerolls > 0;
                    SetText(screen.rerollLabel, ArenaText.Text("ui.reroll.counter", s.rerolls));
                    break;
                case ArenaUiState.LevelUp:
                    cards = s.draftCards;
                    SetText(screen.heading, ArenaText.Pair($"升级 · 选一张（还剩 {s.bankedDrafts} 次）", $"LEVEL UP · PICK ONE ({s.bankedDrafts} LEFT)"));
                    SetBody(screen.body, ArenaText.Pair($"按 {ArenaKeys.SkillKeys} 选择；R 重抽（每波一次）；Esc 稍后再选，战斗中按 {ArenaKeys.CardPickKey} 可再打开。", $"{ArenaKeys.SkillKeys} select; R rerolls (once per wave); Esc picks later, {ArenaKeys.CardPickKey} opens this again in combat."));
                    if (screen.reroll) screen.reroll.interactable = s.rerolls > 0;
                    SetText(screen.rerollLabel, ArenaText.Text("ui.reroll.counter", s.rerolls));
                    break;
                case ArenaUiState.ReplaceWeapon:
                    RenderReplacement(screen, s.replacementSlots, s.replacementCount);
                    SetBody(screen.body, ArenaText.Pair("选择要替换的武器。强化跟着武器走，武器被替换掉，它身上的强化也一起没了。", "Choose the weapon to replace. Upgrades stay with the weapon: replace it and they are gone with it."));
                    break;
                case ArenaUiState.ReplaceSkill:
                    RenderReplacement(screen, s.skillSlots, 3);
                    SetBody(screen.body, s.hideXp
                        ? ArenaText.Pair("已有 3 个技能时再选择新技能，就要选一个旧的替换；新技能从它给的等级开始。", "With 3 skills already, taking a new one means replacing an old one; the new skill starts at the level it comes with.")
                        : ArenaText.Pair("已有 3 个技能时再选择新技能，就要选一个旧的替换；新技能从这张卡给的等级开始。", "With 3 skills already, taking a new one means replacing an old one; the new skill starts at the level this card gives."));
                    break;
                case ArenaUiState.Ready:
                    SetBody(screen.body, ArenaText.Pair("整备选项已完成。点击「开始下一波」继续。", "Build choices complete. Click START NEXT WAVE to go on.") + WalkHint());
                    break;
                case ArenaUiState.WeaponIntro:
                    SetText(screen.heading, s.weaponIntroTitle);
                    ArenaTextFit.Show(screen.body, ArenaEmphasis.Apply(s.weaponIntroBody), WeaponCardTextSize, weaponCardSmallest);
                    SetText(screen.summary, s.weaponIntroHint);
                    if (screen.continueLabel) SetText(screen.continueLabel, s.weaponIntroContinue);
                    if (screen.icon) SetWeaponIcon(screen.icon, art, s.weaponIntroForm);
                    break;
                case ArenaUiState.Paused:
                    // V2 day campaign: there is no build phase, so no build clock either.
                    SetText(screen.body, s.hideXp ? ArenaText.Pair("战场已暂停。", "The battlefield is paused.")
                        : ArenaText.Pair("战场与整备倒计时已暂停。", "The battlefield and the build clock are paused."));
                    if (pauseControls) pauseControls.Fill(screen.body ? screen.body.font : null);
                    RenderPauseSettings(screen.body ? screen.body.font : null);
                    if (tutorialLabel)
                    {
                        tutorialLabel.text = ArenaText.Pair("教程", "HOW TO PLAY");
                        if (screen.body) tutorialLabel.font = screen.body.font;
                    }
                    break;
                case ArenaUiState.Result:
                    SetText(screen.heading, s.resultTitle);
                    SetText(screen.body, s.resultDetails);
                    break;
            }
            if (cards == null || screen.choices == null) return;
            for (int i = 0; i < screen.choices.Length; i++)
                screen.choices[i].Render(i < cards.Length ? cards[i] : null, art);
        }

        // ───── The tutorial and the pause screen's controls table (Wenbo Zhang's how-to-play branch) ─────

        /// <summary>The keys of a run, with what they do in the game now (the pages and this table follow the game): document 3.2's rows, with
        /// two wider than the document's (1 / 2 / 3 picks on every panel, E also works inside the trial) and T saying when it answers. Space and Enter are not here:
        /// they only close the weapon card, and the card says so itself. The input, the action, and what the action's brackets add. Built when asked, because
        /// ArenaText answers only once its tables are ready, and because the rows name the keys as they are now: the ones an action is on
        /// can be changed in the settings (ArenaKeys).</summary>
        public static (string input, string action, string note)[] ControlRows() => CampaignControls ? CampaignControlRows() : ClassicControlRows();

        /// <summary>The classic run's rows, whatever is running: the tutorial's classic pages show them.</summary>
        public static (string input, string action, string note)[] ClassicControlRows() => new[]
        {
            (ArenaKeys.MoveKeys, ArenaText.Pair("移动", "Move"), ""),
            (ArenaText.Pair("鼠标", "Mouse"), ArenaText.Pair("瞄准", "Aim"), ""),
            (ArenaText.Pair("左键", "Left mouse button"), ArenaText.Pair("使用当前手持武器", "Use the held weapon"),
                ArenaText.Pair("（按住蓄力或连射，取决于武器）", "(hold to charge or to keep firing, depending on the weapon)")),
            (ArenaText.Pair("右键", "Right mouse button"), ArenaText.Pair("切换两个手持栏位", "Swap the two hand slots"), ""),
            (ArenaKeys.SprintKey, ArenaText.Pair("冲刺", "Sprint"), ""),
            (ArenaKeys.SkillKeys, ArenaText.Pair("释放技能；面板打开时选对应的一项", "Cast a skill; pick that option while a panel is open"), ""),
            (ArenaKeys.InteractKey, ArenaText.Pair("互动（能用的东西会在你站上去时提示）", $"Interact (a thing that takes {ArenaKeys.InteractKey} says so once you stand on it)"), ""),
            ("Esc", ArenaText.Pair("暂停 / 继续；波次中的选卡面板上 = 稍后再选", "Pause / resume; on a mid-wave card panel, pick later"), ""),
            ("R", ArenaText.Pair("结算时重开；选卡面板打开时重抽", "Restart on the result screen; reroll while a card panel is open"), ""),
            (ArenaKeys.CardPickKey, ArenaText.Pair("波次中有存着的选卡时打开（屏幕上会提示）", "During a wave, open a card pick you kept for later (the screen tells you)"), ""),
            ("Tab", ArenaText.Pair("整备时收起面板，再按回到面板", "Put the build panel away in the build phase; press again to bring it back"), ""),
        };

        /// <summary>A V2 day campaign is running. It has no card picks and no build phase, so the pause table shows its
        /// own keys in place of T and Tab: mining, the base door and the lamps (V2Campaign: hold E, E, F).</summary>
        internal static bool CampaignControls => V2Campaign.Active && V2Campaign.Active.Running;

        /// <summary>The campaign's rows. Same count as the classic rows: the table builds its rows and the pause panel its height from
        /// that count before any run starts. The tutorial's story pages show them whatever is running: the title opens them before any
        /// campaign runs.</summary>
        internal static (string input, string action, string note)[] CampaignControlRows() => new[]
        {
            (ArenaKeys.MoveKeys, ArenaText.Pair("移动", "Move"), ""),
            (ArenaText.Pair("鼠标", "Mouse"), ArenaText.Pair("瞄准", "Aim"), ""),
            (ArenaText.Pair("左键", "Left mouse button"), ArenaText.Pair("使用当前手持武器", "Use the held weapon"),
                ArenaText.Pair("（按住蓄力或连射，取决于武器）", "(hold to charge or to keep firing, depending on the weapon)")),
            (ArenaText.Pair("右键", "Right mouse button"), ArenaText.Pair("切换两个手持栏位", "Swap the two hand slots"), ""),
            (ArenaKeys.SprintKey, ArenaText.Pair("冲刺", "Sprint"), ""),
            (ArenaKeys.SkillKeys, ArenaText.Pair("释放技能；面板打开时选对应的一项", "Cast a skill; pick that option while a panel is open"), ""),
            (ArenaText.Pair($"按住 {ArenaKeys.InteractKey}", $"Hold {ArenaKeys.InteractKey}"), ArenaText.Pair("采矿（夜里要站在矿灯的光里）", "Mine (at night, only in a lamp's light)"), ""),
            // Since 2026-11-06 the campaign's day is a morning, an afternoon and a night. The black hole's hidden gate is left out here: it
            // is a secret until it opens, and its own label says E.
            (ArenaKeys.InteractKey, ArenaText.Pair("在基地门口：下午和夜里进入基地，上午用能量补充基地护盾", "At the base door: enter the base in the afternoon or at night; in the morning, top up its shield with energy"), ""),
            (ArenaKeys.LampKey, ArenaText.Pair("放下一盏矿灯", "Set down a mining lamp"), ""),
            ("Esc", ArenaText.Pair("暂停 / 继续", "Pause / resume"), ""),
            ("R", ArenaText.Pair("结算时重开", "Restart on the result screen"), ""),
        };

        // The pause screen's table sits under the paused line; V5MenuSkin puts the buttons under PauseTableBottom.
        public const float PauseTableTop = 150f, PauseTableRow = 28f;
        public static float PauseTableBottom => PauseTableTop + ControlRows().Length * PauseTableRow;
        public static readonly string[] PauseButtons = { "ResumeRun", "RestartRun", "HowToPlay", "Codex", "QuitToTitle" };
        V2ControlsTable pauseControls;
        UnityEngine.UI.Text tutorialLabel;
        bool tutorialPending;

        /// <summary>The open mode's map pick reloaded the scene for its map, and the run starts there without the title (GameManager): its
        /// first-time pages come on arrival as after a click on the title.</summary>
        public void ExpectTutorialOnArrival() => tutorialPending = true;

        /// <summary>The title's button was pressed and the run has reached the arena. A run started by code (the tests) sets nothing here.</summary>
        void ShowTutorialOnArrival(ArenaUiSnapshot s)
        {
            if (!tutorialPending || s.state != ArenaUiState.SupplyDrop) return;
            tutorialPending = false;
            V2Tutorial.ShowFirstTime(appliedFont);
        }

        void PlaceControlsTables()
        {
            if (screens == null) return;
            foreach (var screen in screens)
            {
                if (!screen || screen.state != ArenaUiState.Paused) continue;
                var content = screen.transform.Find("Content");
                if (!content) continue;
                AddTutorialButton(content);
                AddPauseSettingsButton(content);
                pauseControls = V2ControlsTable.Create(content, PauseTableRow, 190f, 18);
                var rect = pauseControls.Rect;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(.5f, 1f);
                rect.offsetMin = new Vector2(64f, -PauseTableBottom);
                rect.offsetMax = new Vector2(-64f, -PauseTableTop);
                // Without the menu skin (the Editor's Play button keeps the loaded scene): paused line, table, then the buttons in a row.
                if (screen.body) PlaceTopLeft(screen.body.rectTransform, 44f, 100f, 1012f, 40f);
                float left = 44f, top = PauseTableBottom + 24f;
                foreach (var name in PauseButtons)
                {
                    if (content.Find(name) is RectTransform button) PlaceTopLeft(button, left, top, 186f, 65f);
                    left += 204f;
                }
                if (screen.summary) PlaceTopLeft(screen.summary.rectTransform, 44f, top + 80f, 1012f, 60f);
            }
        }

        /// <summary>A copy of the restart button that opens the tutorial over the pause screen.</summary>
        void AddTutorialButton(Transform content)
        {
            if (content.Find("HowToPlay") || content.Find("RestartRun") is not { } restart) return;
            tutorialLabel = CopyAsPlainButton(restart, "HowToPlay", () => V2Tutorial.Show(appliedFont)).GetComponentInChildren<UnityEngine.UI.Text>();
        }

        /// <summary>A copy of a saved command button, beside it, that runs click instead of a command: it keeps the look and drops the relay
        /// (ArenaUiButton) and the localizers, so its label is set by code.</summary>
        UnityEngine.UI.Button CopyAsPlainButton(Transform original, string name, System.Action click)
        {
            var copy = Instantiate(original.gameObject, original.parent);
            copy.name = name;
            if (copy.GetComponent<ArenaUiButton>() is { } relay) Destroy(relay);
            foreach (var localizer in copy.GetComponentsInChildren<UnityEngine.Localization.Components.LocalizeStringEvent>(true)) Destroy(localizer);
            var button = copy.GetComponent<UnityEngine.UI.Button>();
            button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
            button.onClick.AddListener(() => click());
            return button;
        }

        static void PlaceTopLeft(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        /// <summary>Build phase: Tab puts the panel away so the player can walk the empty arena (and step onto an open portal).</summary>
        static string WalkHint() => ArenaText.Pair("\nTab 收起面板，在空场上走动；再按 Tab 回到面板。", "\nTab puts this panel away so you can walk the empty arena; Tab again brings it back.");

        // Since 2026-11-10 the build panel no longer explains the portal: the portal itself shows its E, and what entering costs, once
        // the player stands on it (V2WorldLabels).

        void RenderReplacement(V2UiScreen screen, ArenaUiSlot[] slots, int count)
        {
            for (int i = 0; i < screen.choices.Length; i++)
            {
                var choice = screen.choices[i];
                bool valid = i < slots.Length && i < count;
                choice.gameObject.SetActive(valid);
                if (!valid) continue;
                SetText(choice.title, slots[i].title); choice.ShowBody(ArenaEmphasis.Apply(slots[i].detail));
                SetText(choice.kind, ArenaText.Text("ui.slot", i + 1)); SetText(choice.badge, ArenaTerms.Rarity(slots[i].rarity));
                choice.ApplyRarity(slots[i].rarity);
                choice.button.interactable = true;
                SetElementIcon(choice.icon, art, slots[i].elementIndex);
                SetWeaponIcon(choice.weaponIcon, art, slots[i].formIndex);
                SetContentIcon(choice.contentIcon, art, slots[i].skillIndex, -1, -1);
            }
        }

        void RenderPlayerStatuses(string fallback)
        {
            var gm = GameManager.Instance;
            var status = gm && gm.player ? ElementStatus.For(gm.player) : null;
            if (statusIcons == null || statusCounts == null || statusIcons.Length < 4 || statusCounts.Length < 4)
            { SetText(statusLabel, fallback); return; }
            for (int i = 0; i < 4; i++)
            {
                float count = status ? i switch { 0 => status.Fire, 1 => status.Poison, 2 => status.Frost, _ => status.Lightning } : 0f;
                bool locked = status && (i == 2 ? status.Frozen : i == 3 && status.Paralysed);
                bool show = count >= 1f || locked;
                SetElementIcon(statusIcons[i], art, show ? i : -1);
                if (statusCounts[i])
                {
                    statusCounts[i].gameObject.SetActive(show);
                    SetText(statusCounts[i], locked ? ArenaText.Text(i == 2 ? "ui.freeze.short" : "ui.paralysis.short") : Mathf.FloorToInt(count).ToString());
                }
            }
            SetText(statusLabel, status && status.Frozen ? ArenaText.Text("ui.frozen") : status && status.Paralysed ? ArenaText.Text("ui.paralysed") : "");
        }

        static void SetText(UnityEngine.UI.Text label, string value)
        {
            if (label && label.text != (value ?? "")) label.text = value ?? "";
        }

        static void SetBody(UnityEngine.UI.Text label, string value) => SetText(label, ArenaEmphasis.Apply(value));

        static void SetFill(UnityEngine.UI.Image image, float amount, float max)
        {
            if (image) image.fillAmount = max > 0f ? Mathf.Clamp01(amount / max) : 0f;
        }

        public static readonly Color[] ElementColors =
        {
            new(1f,.57f,.31f), new(.65f,.87f,.36f), new(.47f,.84f,.95f), new(1f,.84f,.36f),
            new(.80f,.65f,.51f), new(1f,.94f,.68f), new(.76f,.60f,1f)
        };

        // The three icon setters every slot, choice, crate and world label goes through. Since 2026-11-10
        // they give the whitebox's shapes (WhiteboxIcons) while the art is off, and the art set's pictures while it is on; each view
        // calls them at its refresh, so a switch shows at the next one.

        public static void SetElementIcon(UnityEngine.UI.Image image, V2ArtSet set, int index)
        {
            if (!image) return;
            Sprite sprite = !Art.Enabled ? WhiteboxIcons.Element(index)
                : set && set.elementIcons != null && index >= 0 && index < set.elementIcons.Length ? set.elementIcons[index] : null;
            Show(image, sprite);
        }

        public static void SetContentIcon(UnityEngine.UI.Image image, V2ArtSet set, int skill, int stat, int axis)
        {
            if (!image) return;
            int index = skill >= 0 ? skill : stat >= 0 ? stat : axis;
            Sprite sprite;
            if (!Art.Enabled) sprite = skill >= 0 ? WhiteboxIcons.Skill(skill) : stat >= 0 ? WhiteboxIcons.Stat(stat) : axis >= 0 ? WhiteboxIcons.Axis(axis) : null;
            else
            {
                Sprite[] sprites = !set ? null : skill >= 0 ? set.skillIcons : stat >= 0 ? set.statIcons : axis >= 0 ? set.axisIcons : null;
                sprite = sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
            }
            Show(image, sprite);
        }

        public static void SetWeaponIcon(UnityEngine.UI.Image image, V2ArtSet set, int index)
        {
            if (!image) return;
            Sprite sprite = !Art.Enabled ? WhiteboxIcons.Weapon(index)
                : set && set.weaponSprites != null && index >= 0 && index < set.weaponSprites.Length ? set.weaponSprites[index] : null;
            Show(image, sprite);
        }

        static void Show(UnityEngine.UI.Image image, Sprite sprite)
        {
            bool valid = sprite;
            image.gameObject.SetActive(valid);
            if (valid) { image.sprite = sprite; image.color = Color.white; }
        }
    }
}
