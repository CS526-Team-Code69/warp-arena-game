using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The character screen (角色), the fifth interface pass's page for what the player has: opened with the character key (ArenaKeys,
    /// I unless moved in the settings) while the player is in play, in both modes, and closed with the same key, Esc, a pad's east button
    /// or its close button. It is built only when V5HudKit.On, so the English Alpha has no such page. While it is open the run is held as
    /// the pause holds it (GameManager.HoldForCharacterScreen: the Paused phase, its menu not shown), the game's actions read no key
    /// (ArenaKeys.Suspended) and its menu keys are not read either (HoldsInput), so no other key acts, and the frame it closes on is held
    /// too, so the Esc that closes it does not also pause.
    /// It draws a CharacterSheet read from the run as the screen opens (the run stands still while it shows), on a canvas of its own over
    /// the battle screens and the story layer, as a 1920 x 1080 page in the study window's family (V5StudyWindow.cs) shrunk to fit a
    /// screen that is not 16:9:
    /// Top: the banner with "角色" and where the run stands (the day and the time of day, or the wave and the level), and the close button
    /// with its keys.
    /// Left: the health and the energy as bars with their numbers beside the portrait in its gauge ring (the energy once it is unlocked),
    /// then the bonuses, one plain line each for every number that differs from the run's start.
    /// Middle: the weapons held, each a plate with its picture, name, slot, anomaly, how it attacks and its trait once the anomaly has
    /// shown (the plate grows with them), its Mk chip, its element and what was added to it.
    /// Right: the skills learned, each in its octagon with its level, cost, cooldown and key, and the void dash once it is won; then the
    /// upgrades: the study's purchases in the story, the upgrade cards taken in the open mode.
    /// A list longer than its column scrolls inside it (the mouse wheel or a drag). Nothing that is not owned or unlocked is shown, but
    /// every part keeps its caption, so the three columns always stand with their names: an empty part says so in one muted line under
    /// its caption. Every piece follows the art switch: the mock-up's pictures with the art on, flat fills with the thin edge and the
    /// whitebox icons without.
    /// For scripts: IsOpen, CanOpen, Open, Close, Sheet and Rows (the lines shown, in words), ReadNow (the same without opening).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5CharacterScreen : MonoBehaviour
    {
        /// <summary>The page's own canvas, a root object of the scene while the page exists.</summary>
        public const string RootName = "CharacterScreen";

        // Over the battle screens and the story layer; under the collection (28000), the settings (29000) and the tutorial's pages (30000),
        // which never show with it.
        const int SortingOrder = 27000;
        const float Css = V5HudKit.StoryCss;

        // The page in mock-up pixels, on the base page's grid: the banner and the close button along the top, three columns from Top down
        // to Floor, 36 from the sides and 28 apart.
        const float PageW = 1920f, PageH = 1080f, Margin = 36f, Top = 178f, Floor = 1040f;
        const float BannerTop = 26f, BannerW = 600f, BannerH = 111f, BannerEnds = 78f, BannerGap = 22f;
        const float CloseRight = 30f, CloseTop = 48f, CloseH = 58f;
        const float LeftX = 36f, LeftW = 540f, MidX = 604f, MidW = 652f, RightX = 1284f, RightW = 600f;
        // A part's caption, the room under it and before the next part, and the line an empty part shows under its caption.
        const float CaptionH = 22f, CaptionGap = 10f, SectionGap = 26f, EmptyH = 40f;
        // The vitals: the portrait in its ring beside the bars, a row of words over each bar.
        const float FacePicture = .58f, LabelRowH = 30f, LifeBarH = 18f, EnergyBarH = 12f, BarDrop = 6f, BlockGap = 22f;
        // The bonuses: rows 44 tall inside a plate.
        const float BonusRowH = 44f, BonusPad = 10f;
        // A weapon's plate: its picture at the top right, the words at the left, the chips along its foot; 8 kept over the first plate
        // for the hazard tab of the weapon in hand.
        const float WeaponH = 168f, WeaponGap = 14f, WeaponCut = 14f, ListPad = 8f, PictureW = 240f, PictureH = 96f, WordsLeft = 20f;
        const float ChipsTop = 124f, ChipH = 30f, ChipGap = 10f, ChipPad = 8f, MarkH = 24f, ElementMedal = 26f, TagIcon = 20f, TagPadL = 8f, TagPadR = 12f;
        // How a weapon attacks and its trait: two paragraphs under its picture across the plate, 15 pixels, wrapping as they need; the
        // chips follow them.
        const float ParagraphTop = 116f, ParagraphPx = 15f, ParagraphGap = 6f, ParagraphChipsGap = 12f;
        // A skill's row and an upgrade's row.
        const float SkillH = 84f, SkillGap = 10f, Octagon = 60f, OctagonRim = 2.5f, SkillWords = 92f;
        const float EntryH = 64f, EntryGap = 8f, EntryMedal = 44f, EntryWords = 70f;

        // The page's abyss with the art on, and the flat backing in whitebox (the study window's).
        static readonly Color Abyss = V5TitleKit.Rgb(7, 10, 20), FlatBack = new(.01f, .02f, .05f, 1f), VoidMark = V5TitleKit.Rgb(20, 14, 30);
        static Texture2D backdropTexture;
        static V5CharacterScreen instance;
        static int closedFrame = -1;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            instance = null;
            closedFrame = -1;
        }

        sealed class Caption
        {
            public Text text, empty;
            public Image rule;
        }

        sealed class Scroll
        {
            public RectTransform root, viewport, content;
            public ScrollRect rect;
        }

        sealed class BonusRow
        {
            public RectTransform root;
            public Text name, value;
            public Image rule;
        }

        sealed class TagView
        {
            public RectTransform root;
            public Image icon;
            public Text text;
        }

        sealed class WeaponCard
        {
            public RectTransform root, chips;
            public V5HudPlate plate;
            public Image tab, picture, mark;
            public Text place, name, anomaly, markText, elementWord, how, trait;
            public V5HudMedal element;
            public readonly List<TagView> tags = new();
        }

        sealed class SkillRow
        {
            public RectTransform root;
            public Image rim, hole, icon, chip;
            public Text name, chipText, detail;
            public V5HudKey key;
        }

        sealed class EntryRow
        {
            public RectTransform root;
            public V5HudMedal medal;
            public Text title, detail, value;
        }

        bool open, shownArt;
        int openedFrame = -1;
        Font shownFont;
        CharacterSheet sheet;
        List<CharacterRow> rows;
        GameObject canvasRoot;
        Canvas canvas;
        RectTransform page, face;
        Image back, banner, facePicture, portrait;
        RawImage backdrop;
        Text title, where, closeLabel, lifeWord, shieldWord, lifeValue, energyWord, energyValue;
        V5Plate close;
        V5HudKey closeKey, escKey;
        V5HudPlate vitals, bonusPlate;
        V5HudRing ring;
        V5StoryBar lifeBar, energyBar;
        Caption vitalsCaption, bonusCaption, weaponCaption, skillCaption, upgradeCaption;
        Scroll bonusScroll, weaponScroll, rightScroll;
        readonly List<BonusRow> bonusRows = new();
        readonly List<WeaponCard> weaponCards = new();
        readonly List<SkillRow> skillRows = new();
        readonly List<EntryRow> entryRows = new();

        // ───── For scripts and checks ─────

        /// <summary>The screen is showing.</summary>
        public static bool IsOpen => instance && instance.open;

        /// <summary>The game reads none of its menu keys while the screen is open, and on the frame it closes, so the Esc or the key that
        /// closes it does nothing else (GameManager.ReadShownKeys asks here, as it asks the settings panel).</summary>
        public static bool HoldsInput => IsOpen || Time.frameCount == closedFrame;

        /// <summary>The screen may open now, the moments its key's hint shows on the battle screen: the player in play (GameManager.
        /// CharacterScreenAllowed) with no other page or window over the run: the tutorial's pages, the settings, the collection, and in
        /// the story the base, the study window, a crate's panel, a question or the base's tour.</summary>
        public static bool CanOpen
        {
            get
            {
                if (!V5HudKit.On || IsOpen || Time.frameCount == closedFrame) return false;
                var gm = GameManager.Instance;
                if (!gm || !gm.CharacterScreenAllowed) return false;
                if (V2Tutorial.HoldsInput || ArenaSettingsPanel.HoldsInput || ArenaCodex.HoldsInput) return false;
                var ui = gm.CampaignRunning && V2Campaign.Active ? V2Campaign.Active.Ui : null;
                return !ui || !(ui.ConfirmOpen || ui.CrateOpen || ui.BaseOpen || ui.StudyOpen || ui.BaseTourRunning);
            }
        }

        /// <summary>Opens the screen as its key does; false when it may not open now (CanOpen).</summary>
        public static bool Open()
        {
            if (!CanOpen) return false;
            var screen = instance;
            if (!screen && GameManager.Instance) screen = Install(GameManager.Instance.hud && GameManager.Instance.hud.v2Hud ? GameManager.Instance.hud.v2Hud.gameObject : GameManager.Instance.gameObject);
            return screen && screen.Show();
        }

        /// <summary>Closes the screen as its key does, and the run goes on.</summary>
        public static void Close()
        {
            if (IsOpen) instance.Hide(true);
        }

        /// <summary>What the screen showed when it last opened (null before it ever did).</summary>
        public static CharacterSheet Sheet => instance ? instance.sheet : null;

        /// <summary>The lines the screen showed when it last opened, in words (empty before it ever did).</summary>
        public static IReadOnlyList<CharacterRow> Rows => instance && instance.rows != null ? instance.rows : (IReadOnlyList<CharacterRow>)Array.Empty<CharacterRow>();

        /// <summary>The lines the screen would show now, without opening it.</summary>
        public static List<CharacterRow> ReadNow() => CharacterSheet.Read(GameManager.Instance).Rows();

        /// <summary>The page's canvas object while it exists (for captures; inactive while the screen is closed).</summary>
        public static GameObject PageObject => instance ? instance.canvasRoot : null;

        /// <summary>Puts the screen's key reading on host (once; the battle HUD does this as it is built). The page itself is made the
        /// first time it opens.</summary>
        public static V5CharacterScreen Install(GameObject host)
        {
            if (instance) return instance;
            if (!host) return null;
            if (!host.TryGetComponent<V5CharacterScreen>(out var screen)) screen = host.AddComponent<V5CharacterScreen>();
            instance = screen;
            return screen;
        }

        void Awake()
        {
            if (!instance) instance = this;
        }

        void OnDestroy()
        {
            if (instance != this) return;
            // The keys are a static switch: a scene that goes while the screen is open must not leave every action dead.
            if (open) ArenaKeys.Suspended = false;
            open = false;
            if (canvasRoot) Destroy(canvasRoot);
            instance = null;
        }

        // ───── Opening and closing ─────

        void Update()
        {
            if (!V5HudKit.On || instance != this) return;
            if (open)
            {
                TickOpen();
                return;
            }
            if (ArenaKeys.Pressed(GameAction.Character) && CanOpen) Show();
        }

        bool Show()
        {
            var gm = GameManager.Instance;
            if (!gm) return false;
            Build();
            // Read while the run still shows its own phase (the banner names the fight it is in).
            var read = CharacterSheet.Read(gm);
            if (!gm.HoldForCharacterScreen()) return false;
            sheet = read;
            rows = read.Rows();
            open = true;
            openedFrame = Time.frameCount;
            ArenaKeys.Suspended = true;
            // Nothing stays selected behind the page: no key or pad button presses a button there.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            canvasRoot.SetActive(true);
            Lay();
            foreach (var scroll in new[] { bonusScroll, weaponScroll, rightScroll })
            {
                scroll.rect.StopMovement();
                scroll.content.anchoredPosition = Vector2.zero;
            }
            V5UiSound.Play(V5UiSound.Ui.Panel);
            return true;
        }

        /// <summary>Closes the page and hands the run back; with sound, the interface's going-back click.</summary>
        void Hide(bool sound)
        {
            if (!open) return;
            open = false;
            closedFrame = Time.frameCount;
            if (canvasRoot) canvasRoot.SetActive(false);
            ArenaKeys.Suspended = false;
            var gm = GameManager.Instance;
            if (gm) gm.ReleaseCharacterScreen();
            if (sound) V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
        }

        void TickOpen()
        {
            var gm = GameManager.Instance;
            // The run was taken over while the page showed (it ended, or something else changed its phase): the page goes and hands
            // nothing back.
            if (!gm || gm.State != GameState.Playing || gm.RunPhase != ArenaUiState.Paused || !gm.CharacterScreenHolds)
            {
                Hide(false);
                return;
            }
            FitPage();
            // The game's font can arrive after the page was drawn (the web build loads its Chinese letters later).
            if (V5TitleKit.Font != shownFont || Art.Enabled != shownArt) Lay();
            // The press that opened the page does not close it.
            if (Time.frameCount == openedFrame) return;
            var keyboard = Keyboard.current;
            bool leave = keyboard != null && (keyboard.escapeKey.wasPressedThisFrame || Pressed(keyboard, ArenaKeys.KeyOf(GameAction.Character)))
                || Gamepad.current is { } pad && pad.buttonEast.wasPressedThisFrame;
            if (leave) Hide(true);
        }

        // The game's actions read no key while the page is open (ArenaKeys.Suspended), so its own key is read here; Shift answers to either.
        static bool Pressed(Keyboard keyboard, Key key) =>
            keyboard[key].wasPressedThisFrame || key == Key.LeftShift && keyboard[Key.RightShift].wasPressedThisFrame;

        void ClickClose()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            Hide(true);
        }

        // ───── Building (once, the first time the screen opens) ─────

        void Build()
        {
            if (canvasRoot) return;
            canvasRoot = new GameObject(RootName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            var scaler = canvasRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = .5f;
            var root = (RectTransform)canvasRoot.transform;
            back = V5TitleKit.Picture(root, "Back", null, Abyss);
            V5TitleKit.Stretch(back.rectTransform);
            back.raycastTarget = true; // no click reaches what is under the page
            backdrop = V5TitleKit.Raw(root, "Backdrop", BackdropTexture, Color.white);
            V5TitleKit.Stretch(backdrop.rectTransform);
            backdrop.uvRect = new Rect(.25f, .25f, .5f, .5f);
            page = V5TitleKit.Rect(root, "Page");
            V5TitleKit.Centre(page, new Vector2(P(PageW), P(PageH)));
            BuildHead();
            BuildVitals();
            bonusCaption = MakeCaption(page, "BonusCaption");
            bonusPlate = V5HudKit.Plate(page, "Bonuses", V5HudKit.PlateLook.Plain, 14f, Css);
            bonusScroll = MakeScroll(bonusPlate.transform, "List");
            weaponCaption = MakeCaption(page, "WeaponCaption");
            weaponScroll = MakeScroll(page, "Weapons");
            rightScroll = MakeScroll(page, "SkillsAndUpgrades");
            skillCaption = MakeCaption(rightScroll.content, "SkillCaption");
            upgradeCaption = MakeCaption(rightScroll.content, "UpgradeCaption");
            canvasRoot.SetActive(false);
        }

        void BuildHead()
        {
            banner = V5TitleKit.Picture(page, "Banner", null, Color.white);
            banner.gameObject.AddComponent<V5StoryBannerFrame>();
            V5TitleKit.Place(banner.rectTransform, P(Margin), P(BannerTop), P(BannerW), P(BannerH));
            title = V5StoryKit.Label(banner.transform, "Title", 50f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(title);
            where = V5StoryKit.Label(banner.transform, "Where", 20f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);
            // The title's plates take their corner cut in the title's own scale.
            close = V5Plate.Make(page, "Close", V5Plate.Look.Plate, 10f * Css / V5TitleKit.Css, ClickClose);
            close.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            closeLabel = V5StoryKit.Label(close.transform, "Label", 19f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            closeKey = V5HudKit.Key(close.transform, "Key", "I", false, Css);
            escKey = V5HudKit.Key(close.transform, "Esc", "Esc", false, Css);
        }

        void BuildVitals()
        {
            vitalsCaption = MakeCaption(page, "VitalsCaption");
            vitals = V5HudKit.Plate(page, "Vitals", V5HudKit.PlateLook.Plain, 14f, Css);
            var plate = vitals.transform;
            // The portrait in the gauge ring, as on the battle screen; the ring's dark face goes behind the portrait.
            face = V5TitleKit.Rect(plate, "Face");
            facePicture = V5TitleKit.Picture(face, "Picture", V5BattleKit.FaceBack, Color.white);
            facePicture.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            portrait = V5TitleKit.Picture(facePicture.transform, "Portrait", null, Color.white);
            portrait.preserveAspect = true;
            ring = V5HudKit.Ring(face, "Ring", P(100f));
            ring.transform.SetAsFirstSibling();
            lifeWord = V5StoryKit.Label(plate, "LifeWord", 17f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            shieldWord = V5StoryKit.Label(plate, "ShieldWord", 15f, V5BattleKit.ShieldBand, TextAnchor.MiddleLeft, true);
            lifeValue = V5StoryKit.Label(plate, "LifeValue", 26f, V5TitleKit.Ink, TextAnchor.MiddleRight, true);
            lifeBar = V5StoryBar.Create(plate, "Life", V5BattleKit.LifeTop, V5BattleKit.LifeFoot, V5BattleKit.ShieldBand, 10);
            energyWord = V5StoryKit.Label(plate, "EnergyWord", 17f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            energyValue = V5StoryKit.Label(plate, "EnergyValue", 22f, V5HudKit.VioletHi, TextAnchor.MiddleRight, true);
            energyBar = V5StoryBar.Create(plate, "Energy", V5HudKit.VioletHi, V5HudKit.Violet, V5HudKit.VioletHi, 10);
        }

        static Caption MakeCaption(Transform parent, string name)
        {
            var caption = new Caption
            {
                text = V5StoryKit.Label(parent, name, 14f, V5TitleKit.Ink3, TextAnchor.MiddleLeft, true),
                rule = V5TitleKit.Picture(parent, name + "Rule", null, V5HudKit.Hairline),
                empty = V5StoryKit.Label(parent, name + "Empty", 17f, V5TitleKit.Ink3, TextAnchor.MiddleLeft),
            };
            caption.empty.gameObject.SetActive(false);
            return caption;
        }

        /// <summary>A list that scrolls up and down inside its own rectangle (the wheel or a drag); its rows go in its content.</summary>
        static Scroll MakeScroll(Transform parent, string name)
        {
            var view = new Scroll { root = V5TitleKit.Rect(parent, name) };
            view.rect = view.root.gameObject.AddComponent<ScrollRect>();
            view.viewport = V5TitleKit.Rect(view.root, "Viewport");
            V5TitleKit.Stretch(view.viewport);
            var hit = view.viewport.gameObject.AddComponent<Image>();
            hit.color = Color.clear; // takes the wheel over the gaps between rows
            view.viewport.gameObject.AddComponent<RectMask2D>();
            view.content = V5TitleKit.Rect(view.viewport, "Content");
            view.content.anchorMin = new Vector2(0f, 1f);
            view.content.anchorMax = new Vector2(1f, 1f);
            view.content.pivot = new Vector2(0f, 1f);
            view.content.anchoredPosition = Vector2.zero;
            view.content.sizeDelta = Vector2.zero;
            view.rect.viewport = view.viewport;
            view.rect.content = view.content;
            view.rect.horizontal = false;
            view.rect.vertical = true;
            view.rect.movementType = ScrollRect.MovementType.Clamped;
            view.rect.scrollSensitivity = 40f;
            return view;
        }

        BonusRow BonusRowAt(int index)
        {
            while (bonusRows.Count <= index)
            {
                var root = V5TitleKit.Rect(bonusScroll.content, "Bonus" + (bonusRows.Count + 1));
                bonusRows.Add(new BonusRow
                {
                    root = root,
                    name = V5StoryKit.Label(root, "Name", 18f, V5TitleKit.Ink2, TextAnchor.MiddleLeft),
                    value = V5StoryKit.Label(root, "Value", 22f, V5TitleKit.Gold, TextAnchor.MiddleRight, true),
                    rule = V5TitleKit.Picture(root, "Rule", null, V5HudKit.Hairline),
                });
            }
            return bonusRows[index];
        }

        WeaponCard WeaponCardAt(int index)
        {
            while (weaponCards.Count <= index)
            {
                var root = V5TitleKit.Rect(weaponScroll.content, "Weapon" + (weaponCards.Count + 1));
                var card = new WeaponCard { root = root };
                card.plate = V5HudKit.Plate(root, "Plate", V5HudKit.PlateLook.Plain, WeaponCut, Css);
                V5TitleKit.Stretch(card.plate.Rect);
                // The weapon in hand: the hazard tab on the plate's top edge, as on the dock.
                card.tab = V5TitleKit.Picture(root, "Tab", null, Color.white);
                V5TitleKit.Place(card.tab.rectTransform, P(26f), -P(5f), P(72f), P(10f));
                card.picture = V5TitleKit.Picture(root, "Picture", null, Color.white);
                card.picture.preserveAspect = true;
                var shadow = card.picture.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .55f);
                shadow.effectDistance = new Vector2(0f, -P(3f));
                V5TitleKit.Place(card.picture.rectTransform, P(MidW - 18f - PictureW), P(14f), P(PictureW), P(PictureH));
                float words = MidW - WordsLeft - PictureW - 36f;
                card.place = V5StoryKit.Label(root, "Place", 14f, V5TitleKit.Ink3, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(card.place.rectTransform, P(WordsLeft), P(14f), P(words), P(22f));
                card.name = V5StoryKit.Label(root, "Name", 28f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(card.name.rectTransform, P(WordsLeft), P(36f), P(words), P(40f));
                card.anomaly = V5StoryKit.Label(root, "Anomaly", 17f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
                V5TitleKit.Place(card.anomaly.rectTransform, P(WordsLeft), P(78f), P(words), P(26f));
                card.how = Paragraph(root, "How");
                card.trait = Paragraph(root, "Trait");
                // The chips' row, drawn smaller as a whole from its left end when it is wider than the plate.
                card.chips = V5TitleKit.Rect(root, "Chips");
                card.chips.anchorMin = card.chips.anchorMax = new Vector2(0f, 1f);
                card.chips.pivot = new Vector2(0f, .5f);
                card.chips.anchoredPosition = new Vector2(P(WordsLeft), -P(ChipsTop + ChipH * .5f));
                card.chips.sizeDelta = new Vector2(P(MidW - 2f * WordsLeft), P(ChipH));
                card.mark = V5TitleKit.Picture(card.chips, "Mk", WhiteboxSkin.Square, V5BattleKit.TierOne);
                card.markText = V5StoryKit.Label(card.mark.transform, "Label", 15f, V5BattleKit.TierInk, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(card.markText.rectTransform);
                card.element = V5HudKit.Medal(card.chips, "Element", P(ElementMedal));
                card.elementWord = V5StoryKit.Label(card.chips, "ElementWord", 16f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
                weaponCards.Add(card);
            }
            return weaponCards[index];
        }

        /// <summary>A paragraph of a weapon's plate: it wraps in its width and is as tall as its lines (LayParagraph).</summary>
        static Text Paragraph(Transform parent, string name)
        {
            var text = V5StoryKit.Label(parent, name, ParagraphPx, V5TitleKit.Ink2, TextAnchor.UpperLeft);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.SetActive(false);
            return text;
        }

        static TagView TagAt(WeaponCard card, int index)
        {
            while (card.tags.Count <= index)
            {
                var root = V5TitleKit.Rect(card.chips, "Tag" + (card.tags.Count + 1));
                V5TitleKit.Stretch(V5HudKit.Plate(root, "Plate", V5HudKit.PlateLook.Plain, 6f, Css).Rect);
                var icon = V5TitleKit.Picture(root, "Icon", null, Color.white);
                icon.preserveAspect = true;
                card.tags.Add(new TagView { root = root, icon = icon, text = V5StoryKit.Label(root, "Text", 15f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true) });
            }
            return card.tags[index];
        }

        SkillRow SkillRowAt(int index)
        {
            while (skillRows.Count <= index)
            {
                var root = V5TitleKit.Rect(rightScroll.content, "Skill" + (skillRows.Count + 1));
                V5TitleKit.Stretch(V5HudKit.Plate(root, "Plate", V5HudKit.PlateLook.Plain, 12f, Css).Rect);
                var row = new SkillRow { root = root };
                // The skill's octagon, as on the dock: a rim, a dark hole and the icon inside it.
                var octagon = V5TitleKit.Rect(root, "Octagon");
                V5TitleKit.Place(octagon, P(16f), P((SkillH - Octagon) * .5f), P(Octagon), P(Octagon));
                row.rim = V5TitleKit.Picture(octagon, "Rim", V5BattleKit.Octagon, V5BattleKit.OctagonRim);
                V5TitleKit.Stretch(row.rim.rectTransform);
                row.hole = V5TitleKit.Picture(octagon, "Hole", V5BattleKit.Octagon, V5BattleKit.OctagonFill);
                V5TitleKit.Stretch(row.hole.rectTransform, P(OctagonRim));
                row.hole.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                row.icon = V5TitleKit.Picture(row.hole.transform, "Icon", null, Color.white);
                row.icon.preserveAspect = true;
                V5TitleKit.Stretch(row.icon.rectTransform, P(Octagon * .11f));
                row.name = V5StoryKit.Label(root, "Name", 22f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                row.chip = V5TitleKit.Picture(root, "Level", WhiteboxSkin.Square, V5BattleKit.TierOne);
                row.chipText = V5StoryKit.Label(row.chip.transform, "Label", 14f, V5BattleKit.TierInk, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(row.chipText.rectTransform);
                row.detail = V5StoryKit.Label(root, "Detail", 16f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
                row.key = V5HudKit.Key(root, "Key", "", true, Css, 30f, 15f);
                skillRows.Add(row);
            }
            return skillRows[index];
        }

        EntryRow EntryRowAt(int index)
        {
            while (entryRows.Count <= index)
            {
                var root = V5TitleKit.Rect(rightScroll.content, "Upgrade" + (entryRows.Count + 1));
                V5TitleKit.Stretch(V5HudKit.Plate(root, "Plate", V5HudKit.PlateLook.Plain, 10f, Css).Rect);
                var row = new EntryRow { root = root, medal = V5HudKit.Medal(root, "Medal", P(EntryMedal)) };
                var medal = row.medal.Rect;
                medal.anchorMin = medal.anchorMax = new Vector2(0f, .5f);
                medal.pivot = new Vector2(.5f, .5f);
                medal.anchoredPosition = new Vector2(P(14f + EntryMedal * .5f), 0f);
                row.title = V5StoryKit.Label(root, "Title", 19f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                row.detail = V5StoryKit.Label(root, "Detail", 15f, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
                row.value = V5StoryKit.Label(root, "Value", 20f, V5TitleKit.Gold, TextAnchor.MiddleRight, true);
                entryRows.Add(row);
            }
            return entryRows[index];
        }

        // ───── Laying out (as it opens, and again when the font or the art switch changes) ─────

        void Lay()
        {
            if (sheet == null || !canvasRoot) return;
            shownFont = V5TitleKit.Font;
            shownArt = Art.Enabled;
            SyncFonts();
            bool art = shownArt;
            back.color = art ? Abyss : FlatBack;
            backdrop.enabled = art;
            FitPage();
            LayHead();
            float below = LayVitals(art);
            LayBonuses(below);
            LayWeapons(art);
            LayRight(art);
        }

        void SyncFonts()
        {
            var font = V5TitleKit.Font;
            foreach (var text in canvasRoot.GetComponentsInChildren<Text>(true))
                if (text.font != font) text.font = font;
        }

        /// <summary>The page keeps the mock-up's shape and shrinks to fit a screen narrower or shorter than 16:9. The canvas' size is worked
        /// out from the screen and the scale its scaler set as it came on: its own rectangle follows a frame later.</summary>
        void FitPage()
        {
            float unit = canvas && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float k = Mathf.Min(1f, Mathf.Min(Screen.width / unit / P(PageW), Screen.height / unit / P(PageH)));
            if (k > 0f && !Mathf.Approximately(page.localScale.x, k)) page.localScale = new Vector3(k, k, 1f);
        }

        void LayHead()
        {
            // "角色" and where the run stands, centred between the banner's painted ends; a long line shrinks to 14 pixels, then the
            // title gives way down to 36.
            SetText(title, ArenaText.IsChinese ? "角 色" : "CHARACTER");
            SetText(where, sheet.heading);
            float banner = P(BannerW), height = P(BannerH), gap = P(BannerGap), middle = banner - 2f * P(BannerEnds);
            float titleW = 0f, whereW = 0f;
            for (int size = F(50f); ; size--)
            {
                title.fontSize = size;
                titleW = V5StoryKit.WidthOf(title, title.text);
                float room = middle - titleW - gap;
                where.fontSize = F(20f);
                whereW = V5StoryKit.WidthOf(where, where.text);
                while (whereW > room && where.fontSize > F(14f))
                {
                    where.fontSize--;
                    whereW = V5StoryKit.WidthOf(where, where.text);
                }
                if (whereW <= room || size <= F(36f)) break;
            }
            whereW = Mathf.Min(whereW, Mathf.Max(0f, middle - titleW - gap));
            bool placed = whereW > 0f;
            Shown(where, placed);
            float left = (banner - titleW - (placed ? gap + whereW : 0f)) * .5f;
            PlaceLeft(title, left, titleW, height);
            PlaceLeft(where, left + titleW + gap, whereW, height);

            // The close button: its word and both its keys, the character key and Esc.
            SetText(closeLabel, ArenaText.Pair("关闭", "Close"));
            closeKey.SetText(ArenaKeys.CharacterKey);
            float labelW = V5StoryKit.WidthOf(closeLabel, closeLabel.text), keyW = closeKey.Rect.sizeDelta.x, escW = escKey.Rect.sizeDelta.x;
            float closeW = P(22f) + labelW + P(12f) + keyW + P(6f) + escW + P(22f), closeH = P(CloseH);
            close.Place(P(PageW - CloseRight) - closeW, P(CloseTop), closeW, closeH);
            PlaceLeft(closeLabel, P(22f), labelW, closeH);
            PlaceKey(closeKey, P(22f) + labelW + P(12f));
            PlaceKey(escKey, P(22f) + labelW + P(12f) + keyW + P(6f));
        }

        /// <summary>The vitals' plate: the portrait, then the life with its numbers over its bar (and the shield beside the word while
        /// there is one), and the energy the same way once it is unlocked. Where the plate ends, in mock-up pixels.</summary>
        float LayVitals(bool art)
        {
            LayCaption(vitalsCaption, "VITALS", "生命与充能", LeftX, Top, LeftW);
            bool energy = sheet.energyShown;
            float plateTop = Top + CaptionH + CaptionGap, plateH = energy ? 180f : 116f, faceSize = energy ? 128f : 86f;
            V5TitleKit.Place(vitals.Rect, P(LeftX), P(plateTop), P(LeftW), P(plateH));

            V5TitleKit.Place(face, P(18f), P((plateH - faceSize) * .5f), P(faceSize), P(faceSize));
            V5TitleKit.Centre((RectTransform)ring.transform, Vector2.one * P(faceSize));
            float circle = P(faceSize) * FacePicture;
            V5TitleKit.Centre(facePicture.rectTransform, Vector2.one * circle);
            facePicture.color = art ? Color.white : WhiteboxSkin.PlateFill;
            if (art)
            {
                // The helmet a little larger than its circle and lowered, as the mock-up crops it.
                portrait.sprite = V5TitleKit.Piece("portrait");
                V5TitleKit.Centre(portrait.rectTransform, Vector2.one * circle * 1.2f);
                portrait.rectTransform.anchoredPosition = new Vector2(0f, -.14f * circle);
            }
            else
            {
                portrait.sprite = WhiteboxIcons.Armor();
                V5TitleKit.Centre(portrait.rectTransform, Vector2.one * circle * .72f);
            }

            float left = 18f + faceSize + 24f, width = LeftW - left - 22f;
            float lifeBlock = LabelRowH + BarDrop + LifeBarH, energyBlock = LabelRowH + BarDrop + EnergyBarH;
            float y = (plateH - (energy ? lifeBlock + BlockGap + energyBlock : lifeBlock)) * .5f;
            SetText(lifeWord, ArenaText.Pair("生命", "HP"));
            float wordW = V5StoryKit.WidthOf(lifeWord, lifeWord.text);
            V5TitleKit.Place(lifeWord.rectTransform, P(left), P(y), wordW + 2f, P(LabelRowH));
            bool shielded = sheet.shield >= .5f;
            Shown(shieldWord, shielded);
            if (shielded)
            {
                SetText(shieldWord, ArenaText.Pair($"护盾 {Mathf.CeilToInt(sheet.shield)}", $"Shield {Mathf.CeilToInt(sheet.shield)}"));
                V5TitleKit.Place(shieldWord.rectTransform, P(left) + wordW + P(14f), P(y), V5StoryKit.WidthOf(shieldWord, shieldWord.text) + 2f, P(LabelRowH));
            }
            SetText(lifeValue, $"{Mathf.CeilToInt(sheet.hp)}<size={F(17f)}><color=#{Hex(V5TitleKit.Ink3)}> / {Mathf.CeilToInt(sheet.maxHp)}</color></size>");
            V5TitleKit.Place(lifeValue.rectTransform, P(left), P(y), P(width), P(LabelRowH));
            V5TitleKit.Place(lifeBar.Rect, P(left), P(y + LabelRowH + BarDrop), P(width), P(LifeBarH));
            // The shield's band is full at 60 % of max life, as on the battle screen (the level-5 Bulwark's shield).
            lifeBar.Set(sheet.maxHp > 0f ? sheet.hp / sheet.maxHp : 0f, sheet.maxHp > 0f ? sheet.shield / (sheet.maxHp * .6f) : 0f);

            Shown(energyWord, energy);
            Shown(energyValue, energy);
            Shown(energyBar, energy);
            if (energy)
            {
                y += lifeBlock + BlockGap;
                SetText(energyWord, ArenaText.Pair("充能", "Charge"));
                V5TitleKit.Place(energyWord.rectTransform, P(left), P(y), P(width), P(LabelRowH));
                SetText(energyValue, $"{Mathf.CeilToInt(sheet.energy)}<size={F(15f)}><color=#{Hex(V5TitleKit.Ink3)}> / {Mathf.CeilToInt(sheet.maxEnergy)}</color></size>");
                V5TitleKit.Place(energyValue.rectTransform, P(left), P(y), P(width), P(LabelRowH));
                V5TitleKit.Place(energyBar.Rect, P(left), P(y + LabelRowH + BarDrop), P(width), P(EnergyBarH));
                energyBar.Set(sheet.maxEnergy > 0f ? sheet.energy / sheet.maxEnergy : 0f, 0f);
            }
            return plateTop + plateH;
        }

        /// <summary>The bonuses under the vitals: a plate as tall as its lines, down to the floor at most, its lines scrolling inside it;
        /// without any, the caption and its empty line.</summary>
        void LayBonuses(float below)
        {
            int count = sheet.bonuses.Count;
            bool any = count > 0;
            float captionTop = below + SectionGap, plateTop = captionTop + CaptionH + CaptionGap;
            LayCaption(bonusCaption, "BONUSES", "加成", LeftX, captionTop, LeftW);
            LayEmpty(bonusCaption, !any, ArenaText.Pair("还没有获得加成", "No bonuses yet"), LeftX, plateTop, LeftW);
            Shown(bonusPlate, any);
            for (int i = count; i < bonusRows.Count; i++) Shown(bonusRows[i].root, false);
            if (!any) return;
            float plateH = Mathf.Min(Floor - plateTop, count * BonusRowH + 2f * BonusPad), rowW = LeftW - 2f * BonusPad;
            V5TitleKit.Place(bonusPlate.Rect, P(LeftX), P(plateTop), P(LeftW), P(plateH));
            V5TitleKit.Place(bonusScroll.root, P(BonusPad), P(BonusPad), P(rowW), P(plateH - 2f * BonusPad));
            bool chinese = ArenaText.IsChinese;
            for (int i = 0; i < count; i++)
            {
                var row = BonusRowAt(i);
                var bonus = sheet.bonuses[i];
                Shown(row.root, true);
                V5TitleKit.Place(row.root, 0f, P(i * BonusRowH), P(rowW), P(BonusRowH));
                SetText(row.value, bonus.Value(chinese));
                float valueW = V5StoryKit.WidthOf(row.value, row.value.text);
                V5TitleKit.Place(row.value.rectTransform, P(rowW - 14f) - valueW - 2f, 0f, valueW + 2f, P(BonusRowH));
                SetText(row.name, bonus.Name(chinese));
                V5TitleKit.Place(row.name.rectTransform, P(14f), 0f, Mathf.Max(P(60f), P(rowW - 28f) - valueW - P(14f)), P(BonusRowH));
                V5TitleKit.FitWidth(row.name, F(18f), F(13f));
                Shown(row.rule, i < count - 1);
                V5TitleKit.Place(row.rule.rectTransform, P(14f), P(BonusRowH) - 1f, P(rowW - 28f), 1f);
            }
            bonusScroll.content.sizeDelta = new Vector2(0f, P(count * BonusRowH));
        }

        void LayWeapons(bool art)
        {
            int count = sheet.weapons.Count;
            LayCaption(weaponCaption, "WEAPONS", "武器", MidX, Top, MidW);
            float listTop = Top + CaptionH + CaptionGap;
            LayEmpty(weaponCaption, count == 0, ArenaText.Pair("还没有武器", "No weapons yet"), MidX, listTop, MidW);
            V5TitleKit.Place(weaponScroll.root, P(MidX), P(listTop), P(MidW), P(Floor - listTop));
            float y = ListPad;
            for (int i = 0; i < count; i++) y += LayWeapon(WeaponCardAt(i), sheet.weapons[i], y, art) + WeaponGap;
            for (int i = count; i < weaponCards.Count; i++) Shown(weaponCards[i].root, false);
            weaponScroll.content.sizeDelta = new Vector2(0f, count > 0 ? P(y - WeaponGap + ListPad) : 0f);
        }

        /// <summary>One weapon's plate from top; its height in mock-up pixels: WeaponH, or taller by the paragraphs of how it attacks and its
        /// trait when it has them, with its chips moved down under them.</summary>
        float LayWeapon(WeaponCard card, CharacterWeapon weapon, float top, bool art)
        {
            Shown(card.root, true);
            float y = ParagraphTop, foot = 0f;
            if (LayParagraph(card.how, weapon.how.Length > 0 ? ArenaCodex.AttackLine(weapon.how) : "", y, out float howH)) foot = y += howH;
            if (foot > 0f) y += ParagraphGap;
            if (LayParagraph(card.trait, weapon.trait.Length > 0 ? ArenaCodex.TraitLine(weapon.trait) : "", y, out float traitH)) foot = y + traitH;
            float chipsTop = foot > 0f ? Mathf.Max(ChipsTop, foot + ParagraphChipsGap) : ChipsTop, height = chipsTop + WeaponH - ChipsTop;
            card.chips.anchoredPosition = new Vector2(P(WordsLeft), -P(chipsTop + ChipH * .5f));
            V5TitleKit.Place(card.root, 0f, P(top), P(MidW), P(height));
            // The weapon in hand is warm with the hazard tab, as its slot on the dock.
            card.plate.SetLook(weapon.inUse ? V5HudKit.PlateLook.Warm : V5HudKit.PlateLook.Plain);
            Shown(card.tab, weapon.inUse);
            if (weapon.inUse) V5TitleKit.Dress(card.tab, art ? V5TitleKit.Piece("hazard-tab") : null, art ? Color.white : V5TitleKit.Orange);
            var picture = art && weapon.art ? weapon.art : weapon.flat;
            card.picture.sprite = picture;
            card.picture.enabled = picture;
            SetText(card.place, weapon.inUse ? weapon.place + $"<color=#{Hex(V5TitleKit.OrangeHi)}>{ArenaText.Pair("  ·  使用中", "  ·  IN USE")}</color>" : weapon.place);
            SetText(card.name, weapon.name);
            card.name.color = ArenaRarity.InkFor(weapon.rarity);
            V5TitleKit.FitWidth(card.name, F(28f), F(18f));
            bool anomaly = weapon.anomaly.Length > 0;
            Shown(card.anomaly, anomaly);
            if (anomaly)
            {
                SetText(card.anomaly, weapon.anomaly);
                card.anomaly.color = weapon.anomalyHidden ? V5TitleKit.Ink3 : V5TitleKit.Ink2;
                V5TitleKit.FitWidth(card.anomaly, F(17f), F(13f));
            }
            LayChips(card, weapon, art);
            return height;
        }

        /// <summary>A paragraph across the plate from top, as tall as its words wrap to (its height in mock-up pixels); hidden, and false,
        /// when it has no words.</summary>
        bool LayParagraph(Text label, string words, float top, out float height)
        {
            height = 0f;
            bool shown = !string.IsNullOrEmpty(words);
            Shown(label, shown);
            if (!shown) return false;
            SetText(label, words);
            label.fontSize = F(ParagraphPx);
            float width = MidW - 2f * WordsLeft;
            // Placed at its width first: a label's preferred height is worked out at its own width.
            V5TitleKit.Place(label.rectTransform, P(WordsLeft), P(top), P(width), P(ParagraphPx * 2f));
            height = Mathf.Ceil(label.preferredHeight / Css) + 2f;
            V5TitleKit.Place(label.rectTransform, P(WordsLeft), P(top), P(width), P(height));
            return true;
        }

        /// <summary>A weapon's chips along its plate's foot: its Mk (pale grey at Mk I, steel above, black for the story's void Mk), its
        /// element's medal and word, then what was added to it, each on a small plate with its icon.</summary>
        void LayChips(WeaponCard card, CharacterWeapon weapon, bool art)
        {
            string mark = "MK " + V5BattleKit.Roman(weapon.mk);
            SetText(card.markText, mark);
            card.markText.color = weapon.voidMk ? V5HudKit.VioletHi : V5BattleKit.TierInk;
            card.mark.color = weapon.voidMk ? VoidMark : weapon.mk <= 1 ? V5BattleKit.TierOne : V5TitleKit.Steel;
            float markW = V5StoryKit.WidthOf(card.markText, mark) / Css + 2f * ChipPad;
            V5TitleKit.Place(card.mark.rectTransform, 0f, P((ChipH - MarkH) * .5f), P(markW), P(MarkH));
            float x = markW + ChipGap;

            bool element = weapon.element != ElementKind.None;
            Shown(card.element, element);
            Shown(card.elementWord, element);
            if (element)
            {
                card.element.SetIcons(weapon.elementArt ? weapon.elementArt : weapon.elementFlat, weapon.elementFlat);
                var medal = card.element.Rect;
                medal.anchorMin = medal.anchorMax = new Vector2(0f, .5f);
                medal.pivot = new Vector2(.5f, .5f);
                medal.anchoredPosition = new Vector2(P(x + ElementMedal * .5f), 0f);
                x += ElementMedal + 6f;
                string word = ArenaTerms.Element(weapon.element);
                SetText(card.elementWord, word);
                card.elementWord.color = V5BattleKit.Element((int)weapon.element - 1);
                float wordW = V5StoryKit.WidthOf(card.elementWord, word) / Css;
                V5TitleKit.Place(card.elementWord.rectTransform, P(x), 0f, P(wordW + 2f), P(ChipH));
                x += wordW + ChipGap;
            }

            for (int i = 0; i < weapon.tags.Count; i++)
            {
                var view = TagAt(card, i);
                var tag = weapon.tags[i];
                Shown(view.root, true);
                SetText(view.text, tag.text);
                view.text.color = tag.colour;
                var icon = art && tag.art ? tag.art : tag.flat;
                view.icon.sprite = icon;
                view.icon.enabled = icon;
                float textW = V5StoryKit.WidthOf(view.text, tag.text) / Css, iconW = icon ? TagIcon + 6f : 0f;
                float width = TagPadL + iconW + textW + TagPadR;
                V5TitleKit.Place(view.root, P(x), 0f, P(width), P(ChipH));
                V5TitleKit.Place(view.icon.rectTransform, P(TagPadL), P((ChipH - TagIcon) * .5f), P(TagIcon), P(TagIcon));
                V5TitleKit.Place(view.text.rectTransform, P(TagPadL + iconW), 0f, P(textW + 2f), P(ChipH));
                x += width + ChipGap;
            }
            for (int i = weapon.tags.Count; i < card.tags.Count; i++) Shown(card.tags[i].root, false);
            float room = MidW - 2f * WordsLeft, total = x - ChipGap, k = total > room ? room / total : 1f;
            card.chips.localScale = new Vector3(k, k, 1f);
        }

        /// <summary>The right column: the skills, then the upgrades, each under its caption (an empty one with its empty line),
        /// scrolling together.</summary>
        void LayRight(bool art)
        {
            V5TitleKit.Place(rightScroll.root, P(RightX), P(Top), P(RightW), P(Floor - Top));
            int skills = sheet.skills.Count, upgrades = sheet.upgrades.Count;
            bool dash = sheet.dash != null;
            float y = 0f;
            LayCaption(skillCaption, "SKILLS", "技能", 0f, y, RightW);
            y += CaptionH + CaptionGap;
            LayEmpty(skillCaption, skills == 0 && !dash, ArenaText.Pair("还没有学会技能", "No skills learned yet"), 0f, y, RightW);
            if (skills == 0 && !dash) y += EmptyH;
            for (int i = 0; i < skills; i++)
            {
                LaySkill(SkillRowAt(i), sheet.skills[i], y, art);
                y += SkillH + (i < skills - 1 ? SkillGap : 0f);
            }
            // The void dash once it is won, after the skills, in a row of their kind.
            if (dash)
            {
                if (skills > 0) y += SkillGap;
                LayDash(SkillRowAt(skills), sheet.dash, y, art);
                y += SkillH;
            }
            for (int i = skills + (dash ? 1 : 0); i < skillRows.Count; i++) Shown(skillRows[i].root, false);
            y += SectionGap;
            LayCaption(upgradeCaption, "UPGRADES", "升级", 0f, y, RightW);
            y += CaptionH + CaptionGap;
            LayEmpty(upgradeCaption, upgrades == 0, ArenaText.Pair("还没有获得升级", "No upgrades yet"), 0f, y, RightW);
            if (upgrades == 0) y += EmptyH;
            for (int i = 0; i < upgrades; i++)
            {
                LayEntry(EntryRowAt(i), sheet.upgrades[i], y);
                y += EntryH + (i < upgrades - 1 ? EntryGap : 0f);
            }
            for (int i = upgrades; i < entryRows.Count; i++) Shown(entryRows[i].root, false);
            rightScroll.content.sizeDelta = new Vector2(0f, P(y));
        }

        // Its level on a chip (the void's mark for a greater void skill), then its cost in the energy's violet and its cooldown.
        void LaySkill(SkillRow row, CharacterSkill skill, float top, bool art) =>
            LayAbility(row, top, art, art && skill.art ? skill.art : skill.flat, skill.key, skill.name, ArenaRarity.InkFor(skill.rarity),
                CharacterSheet.SkillLevel(skill), skill.greaterVoid ? V5HudKit.Violet : skill.level <= 1 ? V5BattleKit.TierOne : V5TitleKit.Steel,
                skill.greaterVoid ? Color.white : V5BattleKit.TierInk,
                ArenaText.Pair($"<color=#{Hex(V5HudKit.VioletHi)}>{skill.cost:0} 充能</color>  ·  冷却 {skill.cooldown:0.#} 秒",
                    $"<color=#{Hex(V5HudKit.VioletHi)}>{skill.cost:0} energy</color>  ·  {skill.cooldown:0.#} s cooldown"));

        // The void dash: the void's icon and mark, what one dash costs, and Space at the row's right end.
        void LayDash(SkillRow row, CharacterDash dash, float top, bool art) =>
            LayAbility(row, top, art, art && dash.art ? dash.art : dash.flat, ArenaText.Pair("空格", CharacterDash.Key), dash.name, ArenaRarity.InkFor(CardRarity.Void),
                ArenaText.Pair("虚空", "VOID"), V5HudKit.Violet, Color.white, dash.Detail);

        /// <summary>A row of the skills column: the icon in its octagon, the name, a chip and the detail beside it, and the key at the right
        /// end.</summary>
        void LayAbility(SkillRow row, float top, bool art, Sprite icon, string keyName, string name, Color nameInk, string chip, Color chipBack,
            Color chipInk, string detail)
        {
            Shown(row.root, true);
            V5TitleKit.Place(row.root, 0f, P(top), P(RightW), P(SkillH));
            row.rim.color = art ? V5BattleKit.OctagonRim : WhiteboxSkin.Edge;
            row.hole.color = art ? V5BattleKit.OctagonFill : WhiteboxSkin.PlateFill;
            row.icon.sprite = icon;
            row.icon.enabled = icon;
            // The key it is used with, at the row's right end.
            bool keyed = !string.IsNullOrEmpty(keyName);
            Shown(row.key, keyed);
            float keyW = 0f;
            if (keyed)
            {
                row.key.SetText(keyName);
                keyW = row.key.Rect.sizeDelta.x;
                var key = row.key.Rect;
                key.anchorMin = key.anchorMax = key.pivot = new Vector2(1f, .5f);
                key.anchoredPosition = new Vector2(-P(18f), 0f);
            }
            float words = P(RightW - SkillWords - 18f) - (keyed ? keyW + P(14f) : 0f);
            SetText(row.name, name);
            row.name.color = nameInk;
            V5TitleKit.Place(row.name.rectTransform, P(SkillWords), P(10f), words, P(32f));
            V5TitleKit.FitWidth(row.name, F(22f), F(15f));
            SetText(row.chipText, chip);
            row.chip.color = chipBack;
            row.chipText.color = chipInk;
            float chipW = V5StoryKit.WidthOf(row.chipText, chip) + P(2f * ChipPad);
            V5TitleKit.Place(row.chip.rectTransform, P(SkillWords), P(48f), chipW, P(22f));
            SetText(row.detail, detail);
            float detailLeft = P(SkillWords) + chipW + P(10f);
            V5TitleKit.Place(row.detail.rectTransform, detailLeft, P(46f), Mathf.Max(P(40f), P(SkillWords) + words - detailLeft), P(26f));
            V5TitleKit.FitWidth(row.detail, F(16f), F(12f));
        }

        void LayEntry(EntryRow row, CharacterEntry entry, float top)
        {
            Shown(row.root, true);
            V5TitleKit.Place(row.root, 0f, P(top), P(RightW), P(EntryH));
            // Its icon ringed in its rarity's colour; the void's own is near black, so a void line takes the void's violet and its glow.
            row.medal.SetIcons(entry.art ? entry.art : entry.flat, entry.flat);
            bool voids = entry.rarity == CardRarity.Void;
            row.medal.SetRing(voids ? V5HudKit.Violet : ArenaRarity.ColorFor(entry.rarity));
            row.medal.SetGlow(voids ? V5StoryKit.VioletGlow : Color.clear);
            string value = entry.value ?? "";
            Shown(row.value, value.Length > 0);
            float valueW = 0f;
            if (value.Length > 0)
            {
                SetText(row.value, value);
                valueW = V5StoryKit.WidthOf(row.value, value);
                V5TitleKit.Place(row.value.rectTransform, P(RightW - 18f) - valueW - 2f, 0f, valueW + 2f, P(EntryH));
            }
            float words = Mathf.Max(P(60f), P(RightW - EntryWords - 18f) - (valueW > 0f ? valueW + P(14f) : 0f));
            bool detail = !string.IsNullOrEmpty(entry.detail);
            SetText(row.title, entry.title);
            V5TitleKit.Place(row.title.rectTransform, P(EntryWords), detail ? P(8f) : P((EntryH - 28f) * .5f), words, P(28f));
            V5TitleKit.FitWidth(row.title, F(19f), F(14f));
            Shown(row.detail, detail);
            if (detail)
            {
                SetText(row.detail, entry.detail);
                V5TitleKit.Place(row.detail.rectTransform, P(EntryWords), P(36f), words, P(22f));
                V5TitleKit.FitWidth(row.detail, F(15f), F(11f));
            }
        }

        void LayCaption(Caption caption, string english, string chinese, float left, float top, float width)
        {
            Shown(caption.text, true);
            Shown(caption.rule, true);
            string words = ArenaText.IsChinese ? V5TitleKit.Spaced(english) + "   ·   " + chinese : V5TitleKit.Spaced(english);
            SetText(caption.text, words);
            float w = Mathf.Min(P(width - 40f), V5StoryKit.WidthOf(caption.text, words));
            V5TitleKit.Place(caption.text.rectTransform, P(left), P(top), w + 2f, P(CaptionH));
            float ruleLeft = P(left) + w + P(12f);
            V5TitleKit.Place(caption.rule.rectTransform, ruleLeft, P(top + CaptionH * .5f), Mathf.Max(0f, P(left + width) - ruleLeft), 1f);
        }

        /// <summary>The muted line under an empty part's caption, saying what it does not hold yet; hidden while the part has any.</summary>
        void LayEmpty(Caption caption, bool empty, string words, float left, float top, float width)
        {
            Shown(caption.empty, empty);
            if (!empty) return;
            SetText(caption.empty, words);
            V5TitleKit.Place(caption.empty.rectTransform, P(left + 2f), P(top), P(width - 4f), P(EmptyH));
            V5TitleKit.FitWidth(caption.empty, F(17f), F(13f));
        }

        // ───── Small pieces ─────

        /// <summary>Canvas units of so many mock-up pixels.</summary>
        static float P(float px) => px * Css;

        /// <summary>The font size of so many mock-up pixels.</summary>
        static int F(float px) => V5StoryKit.FontSize(px);

        static string Hex(Color colour) => ColorUtility.ToHtmlStringRGB(colour);

        static void SetText(Text label, string text)
        {
            text ??= "";
            if (label && label.text != text) label.text = text;
        }

        static void Shown(Component part, bool shown)
        {
            if (part && part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
        }

        /// <summary>A label from left in its row, as wide as width and as tall as the row, its middle on the row's middle.</summary>
        static void PlaceLeft(Text label, float left, float width, float height)
        {
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width + 2f, height);
        }

        static void PlaceKey(V5HudKey key, float left)
        {
            var rect = key.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, 0f);
        }

        /// <summary>The page's backing with the art on, as the base page's: deep navy at the foot to a lighter navy at the top, leaning to
        /// indigo on the right, shown through a uv of its middle as a smooth blend.</summary>
        static Texture2D BackdropTexture
        {
            get
            {
                if (backdropTexture) return backdropTexture;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "V5Character backdrop", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
                };
                texture.SetPixels32(new Color32[] { new(10, 14, 32, 255), new(20, 12, 40, 255), new(18, 28, 56, 255), new(30, 20, 60, 255) });
                texture.Apply(false, true);
                return backdropTexture = texture;
            }
        }
    }
}
