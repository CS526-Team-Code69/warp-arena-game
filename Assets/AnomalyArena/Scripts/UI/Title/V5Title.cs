using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The title of the whole game: the Chinese page, the English Beta and anything off the web (the English Alpha keeps its own title,
    /// BuildEdition). The logo at the top left; under it a menu (继续游戏 while a save exists, 新游戏, 教学, 图鉴, 设置) and to its right a
    /// window that changes with the menu: the saves to continue, the new game's mode and slot, the tutorial's lessons, the collection and the
    /// settings. An orange triangle points from the chosen item to the window. Everything sits on one grid (the 1920 x 1080 mock-up,
    /// work/art-plans/style-1112/ui-e, times 0.75): the logo and the menu share the left edge, the menu and the window their top, and the
    /// window ends as far above the screen's foot as the logo starts below its top. With the art on, the crash-site picture fills the screen
    /// behind a shade on its left; in whitebox the saved title keeps its flat backdrop and its wordmark, and the plates are flat. No line of
    /// key hints: the settings and the tutorial say what the keys are when they are opened.
    /// V2Hud builds it in its Awake from the saved title's raw material: the saved Start stays hidden and still starts a new story run
    /// (V2Campaign routes it), and the open mode's map pick (V2Hud.MapSelect) is drawn in the window's frame. The saves come from ArenaSaves.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed partial class V5Title : MonoBehaviour
    {
        public const string RootName = "V5Title";
        /// <summary>The picture and its shades behind the title, the first part of the title's Content.</summary>
        public const string ArtName = "V5TitleArt";
        /// <summary>The menu's items from the top, under V5Title/Menu, and the window each one shows, under V5Title/Window/Pages. Continue
        /// shows only while a save exists in either mode.</summary>
        public static readonly string[] MenuNames = { "Continue", "NewGame", "Tutorial", "Collection", "Settings" };
        public const int ContinueTab = 0, NewGameTab = 1, TutorialTab = 2, CollectionTab = 3, SettingsTab = 4;

        /// <summary>This page has the title: every page but the English Alpha's.</summary>
        public static bool UsedOnThisPage => !BuildEdition.IsAlpha;

        // The grid on the 1440 x 810 canvas.
        public const float StageW = 1440f, StageH = 810f, GridLeft = 72f, GridTop = 69f;
        // The logo pictures carry a few pixels round their letters: the letters start on the grid.
        const float LogoLeft = 69f, LogoTop = 64.5f;
        const float MenuTop = 219f, MenuW = 352.5f, ItemH = 64.5f, ItemStep = 75f;
        const float PointerLeft = 427.5f, PointerW = 13.5f, PointerH = 21f;
        const float WindowLeft = 445.5f, WindowW = 411f, WindowH = 522f, PadX = 22.5f, PadY = 19.5f;
        /// <summary>The window's inner width.</summary>
        public const float InnerW = WindowW - 2f * PadX;
        const float HeadH = 45f, RuleTop = 75f, BodyTop = 88f, FootTop = 457.5f, FootH = 45f, FootGap = 12f;
        const float BodyH = FootTop - FootGap - BodyTop, BodyNoFootH = WindowH - PadY - BodyTop;
        const float Css = V5TitleKit.Css;
        const float MedalLeft = 18f * Css, MenuMedal = 54f * Css, LabelLeft = MedalLeft + MenuMedal + 18f * Css;
        // The medal picture carries its shadow round the ring: 152 pixels for a 128-pixel ring.
        const float MedalArt = 152f / 128f;
        const string PicturePath = "V5/Title/title-crash";
        const float PictureAspect = 1672f / 941f;
        // The window's parts slide in from the right one after another; the triangle glides to the chosen item (fx.js, title.js).
        const float EnterShift = 22f * Css, EnterSeconds = .28f, EnterStagger = .04f, GlideSeconds = .26f;

        static readonly string[] MenuZh = { "继续游戏", "新游戏", "教学", "图鉴", "设置" };
        static readonly string[] MenuEn = { "Continue", "New Game", "Tutorial", "Collection", "Settings" };
        static readonly string[] MenuCaps = { "CONTINUE", "NEW GAME", "TUTORIAL", "COLLECTION", "SETTINGS" };
        // The game's own icons (the campaign's art set).
        static readonly Func<CampaignArt, Sprite>[] MenuIcons = { a => a.engine, a => a.lamp, a => a.control, a => a.skill, a => a.repair };
        static readonly Color MenuInk = V5TitleKit.Rgb(216, 210, 198), RuleColour = V5TitleKit.Rgb(163, 172, 190, .35f), DarkDim = V5TitleKit.Rgb(27, 14, 6, .55f);
        static readonly Color[] MapFlat = { new(.62f, .52f, .36f), new(.47f, .49f, .53f), new(.2f, .24f, .31f) };

        static V5Title active;

        V2Hud hud;
        RectTransform content, stage, backdrop, menu, pointer, window, head, pages, foot;
        RawImage picture, rule;
        Image logo, windowPlate;
        Outline windowEdge;
        V5Block headBlock, footBlock;
        Text headCap, headTitle;
        readonly V5Plate[] items = new V5Plate[5];
        readonly Text[] itemLabels = new Text[5], itemCaps = new Text[5];
        readonly MedalView[] itemMedals = new MedalView[5];
        readonly int[] rows = { 0, 1, 2, 3, 4 };
        readonly RectTransform[] pageRoots = new RectTransform[5];
        int tab = -1;
        bool built, selectPending, mapOpen;
        GameObject pendingSelect, lastSelected;
        bool? shownChinese, shownArt;
        Font shownFont;
        int shownKeys = -1;
        Vector2 shownCanvas;
        Text buildLabel;

        /// <summary>The window showing, by its menu item (ContinueTab and the others).</summary>
        public int Tab => tab;

        /// <summary>No part of the title is moving (the window's parts have come in, the triangle has arrived).</summary>
        public bool Settled => tweens.Count == 0;

        /// <summary>The title being shown, if one is.</summary>
        public static V5Title Active => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            active = null;
#if UNITY_INCLUDE_TESTS
            SummariesForTests = null;
            SaveCallsForTests = null;
#endif
        }

        /// <summary>Builds the title under the saved title's Content (once).</summary>
        public static V5Title Install(V2Hud hud, RectTransform content)
        {
            if (!hud || !content || content.Find(RootName)) return null;
            var root = V5TitleKit.Rect(content, RootName);
            var title = root.gameObject.AddComponent<V5Title>();
            title.Build(hud, content, root);
            return title;
        }

        void Build(V2Hud owner, RectTransform titleContent, RectTransform root)
        {
            hud = owner;
            content = titleContent;
            stage = root;
            stage.anchorMin = stage.anchorMax = new Vector2(0f, .5f);
            stage.pivot = new Vector2(0f, .5f);
            stage.anchoredPosition = Vector2.zero;
            stage.sizeDelta = new Vector2(StageW, StageH);
            BuildArt();
            BuildLogo();
            BuildMenu();
            BuildWindow();
            BuildSaves();
            BuildLessons();
            BuildCodex();
            BuildSettings();
            DressMapPick();
            DressBuildLabel();
            // Over the picture, under the map pick (drawn in the window) and the build line.
            if (mapPick) stage.SetSiblingIndex(mapPick.GetSiblingIndex());
            // The title selects its own first item, quietly; the saved title's selection would land on the hidden Start.
            if (content.parent && content.parent.TryGetComponent<V3TitleBackdrop>(out var titleBackdrop)) titleBackdrop.initialSelection = null;
            built = true;
            LoadSaves();
            SetTab(continueShown ? ContinueTab : NewGameTab, false);
            selectPending = true;
        }

        void OnEnable()
        {
            active = this;
            ArenaSaves.Changed += OnSavesChanged;
            savesDirty = true;
            selectPending = true;
        }

        void OnDisable()
        {
            if (active == this) active = null;
            ArenaSaves.Changed -= OnSavesChanged;
            StopListening(null, false);
            FinishTweens();
        }

        void OnSavesChanged() => savesDirty = true;

        // ───── The picture, the logo ─────

        void BuildArt()
        {
            backdrop = V5TitleKit.Rect(content, ArtName);
            V5TitleKit.Stretch(backdrop);
            backdrop.SetAsFirstSibling();
            picture = V5TitleKit.Raw(backdrop, "Picture", Resources.Load<Texture2D>(PicturePath), Color.white);
            V5TitleKit.Stretch(picture.rectTransform);
            var shade = V5TitleKit.Raw(backdrop, "Shade", V5TitleKit.ShadeAcross, Color.white);
            V5TitleKit.Stretch(shade.rectTransform);
            var shadeFoot = V5TitleKit.Raw(backdrop, "ShadeFoot", V5TitleKit.ShadeUp, Color.white);
            V5TitleKit.Stretch(shadeFoot.rectTransform);
        }

        /// <summary>The saved logo moves onto the grid; with the art on it shows the language's logo picture, in whitebox its wordmark
        /// (V3TitleBackdrop) follows it.</summary>
        void BuildLogo()
        {
            var saved = content.Find("WarpArenaLogo");
            if (!saved || !saved.TryGetComponent(out logo)) return;
            saved.SetParent(stage, false);
            saved.SetAsFirstSibling();
            PlaceLogo();
        }

        void PlaceLogo()
        {
            if (!logo) return;
            string name = ArenaText.IsChinese ? "logo-zh" : "logo-en";
            var sprite = V5TitleKit.Piece(name);
            if (!sprite) return;
            if (logo.sprite != sprite) logo.sprite = sprite;
            logo.type = Image.Type.Simple;
            logo.preserveAspect = true;
            var size = V5TitleKit.PieceSize(name) * .5f * Css;
            V5TitleKit.Place(logo.rectTransform, LogoLeft, LogoTop, size.x, size.y);
        }

        // ───── The menu ─────

        void BuildMenu()
        {
            menu = V5TitleKit.Rect(stage, "Menu");
            V5TitleKit.Place(menu, GridLeft, MenuTop, MenuW, ItemStep * 4f + ItemH);
            for (int i = 0; i < items.Length; i++)
            {
                int index = i;
                var item = V5Plate.Make(menu, MenuNames[i], V5Plate.Look.Plate, 16f, () => OnMenuClick(index), true);
                item.Place(0f, i * ItemStep, MenuW, ItemH);
                item.HoverPitch = 1.12f - i * .05f;
                item.AddTab(26f);
                item.KeyboardSelected += _ => OnMenuKeyboard(index);
                itemMedals[i] = Medal(item.transform, "Medal", MenuMedal);
                V5TitleKit.Place(itemMedals[i].root, MedalLeft, (ItemH - MenuMedal) * .5f, MenuMedal, MenuMedal);
                itemLabels[i] = V5TitleKit.Label(item.transform, "Label", 23, MenuInk, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(itemLabels[i].rectTransform, LabelLeft, 0f, MenuW - LabelLeft - 18f, ItemH);
                itemCaps[i] = V5TitleKit.Label(item.transform, "Cap", 10, V5TitleKit.Ink3, TextAnchor.MiddleRight, true);
                V5TitleKit.Place(itemCaps[i].rectTransform, MenuW - 18f - 150f, 0f, 150f, ItemH);
                items[i] = item;
            }
            pointer = V5TitleKit.Picture(stage, "Pointer", V5TitleKit.Piece("pointer"), Color.white).rectTransform;
            V5TitleKit.Place(pointer, PointerLeft, MenuTop, PointerW, PointerH);
        }

        /// <summary>The items shown, from the top, a row apart: Continue only while a save exists.</summary>
        void LayMenu()
        {
            int row = 0;
            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i]) continue;
                bool shown = i != ContinueTab || continueShown;
                if (items[i].gameObject.activeSelf != shown) items[i].gameObject.SetActive(shown);
                rows[i] = shown ? row : -1;
                if (shown) items[i].Place(0f, row++ * ItemStep, MenuW, ItemH);
            }
        }

        float PointerTop(int index) => MenuTop + Mathf.Max(0, rows[index]) * ItemStep + (ItemH - PointerH) * .5f;

        void PlacePointer()
        {
            if (pointer && tab >= 0) pointer.anchoredPosition = new Vector2(PointerLeft, -PointerTop(tab));
        }

        void Glide()
        {
            float from = pointer.anchoredPosition.y, to = -PointerTop(tab);
            Animate(pointer, GlideSeconds, 0f, t => V5TitleKit.Bezier(t, .2f, .9f, .25f, 1f),
                p => pointer.anchoredPosition = new Vector2(PointerLeft, Mathf.Lerp(from, to, p)));
        }

        void RenderMenu()
        {
            bool chinese = ArenaText.IsChinese;
            var icons = CampaignIcons;
            for (int i = 0; i < items.Length; i++)
            {
                if (!items[i]) continue;
                bool chosen = i == tab;
                itemLabels[i].text = ArenaText.Pair(MenuZh[i], MenuEn[i]);
                itemLabels[i].color = chosen ? V5TitleKit.Chosen : MenuInk;
                V5TitleKit.FitWidth(itemLabels[i], 23, 16);
                // The spaced capitals are the Chinese title's decoration; in English they would repeat the word.
                itemCaps[i].text = ArenaText.Pair(V5TitleKit.Spaced(MenuCaps[i]), "");
                itemCaps[i].color = chosen ? V5TitleKit.OrangeHi : V5TitleKit.Ink3;
                ShowMedal(itemMedals[i], icons ? MenuIcons[i](icons) : null);
            }
        }

        void OnMenuClick(int index)
        {
            if (index == tab)
            {
                V5UiSound.Play(V5UiSound.Ui.Press, 1f, .7f);
                return;
            }
            SetTab(index, true);
        }

        void OnMenuKeyboard(int index)
        {
            if (index == tab) return;
            // Coming back from the window with the arrows lands on the window's own item, not on a neighbour of it.
            if (lastSelected && window && lastSelected.transform.IsChildOf(window))
            {
                pendingSelect = items[tab] ? items[tab].gameObject : null;
                return;
            }
            SetTab(index, true);
        }

        void SetTab(int next, bool animate)
        {
            if (next == ContinueTab && !continueShown) next = NewGameTab;
            if (next == tab) return;
            int from = tab;
            tab = next;
            StopListening(null, false);
            deleteAskedUntil = -1f;
            overwriteAsked = false;
            if (next == NewGameTab) newGameStep = 1;
            if (hud && hud.MapSelectOpen) hud.CloseMapSelectQuietly();
            for (int i = 0; i < items.Length; i++)
            {
                if (items[i]) items[i].Chosen = i == tab;
                if (pageRoots[i] && pageRoots[i].gameObject.activeSelf != (i == tab)) pageRoots[i].gameObject.SetActive(i == tab);
            }
            RenderMenu();
            RenderPage();
            if (animate && from >= 0)
            {
                V5UiSound.Play(V5UiSound.Ui.Panel, 1f + (rows[tab] - 2) * .025f);
                Glide();
                Enter();
            }
            else PlacePointer();
        }

        // ───── The window ─────

        void BuildWindow()
        {
            windowPlate = V5TitleKit.Picture(stage, "Window", V5TitleKit.Piece("plate"), Color.white, V5TitleKit.PlateDensity(16f));
            // Clicks between the window's parts stop at it.
            windowPlate.raycastTarget = true;
            window = windowPlate.rectTransform;
            V5TitleKit.Place(window, WindowLeft, MenuTop, WindowW, WindowH);
            head = V5TitleKit.Rect(window, "Head");
            headBlock = head.gameObject.AddComponent<V5Block>();
            headBlock.Place(PadX, PadY, InnerW, HeadH);
            headCap = V5TitleKit.Label(head, "Cap", 10, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
            V5TitleKit.Place(headCap.rectTransform, 0f, 1.5f, InnerW, 12f);
            headTitle = V5TitleKit.Label(head, "Title", 24, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Place(headTitle.rectTransform, 0f, 19.5f, InnerW, 26f);
            V5TitleKit.Painted(headTitle);
            rule = V5TitleKit.Raw(window, "Rule", V5TitleKit.FadeRight, RuleColour);
            V5TitleKit.Place(rule.rectTransform, PadX, RuleTop, InnerW, 1f);
            pages = V5TitleKit.Rect(window, "Pages");
            V5TitleKit.Place(pages, PadX, BodyTop, InnerW, BodyNoFootH);
            for (int i = 0; i < pageRoots.Length; i++)
            {
                pageRoots[i] = V5TitleKit.Rect(pages, MenuNames[i]);
                V5TitleKit.Place(pageRoots[i], 0f, 0f, InnerW, BodyNoFootH);
                pageRoots[i].gameObject.SetActive(false);
            }
            BuildFoot();
        }

        enum FootUse { None, Go, GoAndDelete, Reset, Confirm }

        V5Plate go, delete, reset, confirm, cancel;
        Text goLabel, goSub, deleteLabel, resetLabel, confirmLabel, cancelLabel;
        RawImage goHazard;
        FootUse footUse;
        const float DeleteW = 96f, FootSplit = 7.5f, GoTextLeft = 70f * Css;

        void BuildFoot()
        {
            foot = V5TitleKit.Rect(window, "Foot");
            footBlock = foot.gameObject.AddComponent<V5Block>();
            footBlock.Place(PadX, FootTop, InnerW, FootH);
            go = V5Plate.Make(foot, "Go", V5Plate.Look.Go, 10f, OnGo, true);
            goHazard = V5TitleKit.Raw(go.transform, "Hazard", V5TitleKit.Stripes, V5TitleKit.Rgb(27, 14, 6, .55f));
            V5TitleKit.Place(goHazard.rectTransform, 14f * Css, 12f * Css, 40f * Css, FootH - 24f * Css);
            goHazard.uvRect = new Rect(0f, 0f, 40f * Css / V5TitleKit.HazardRepeat, (FootH - 24f * Css) / V5TitleKit.HazardRepeat);
            goLabel = V5TitleKit.Label(go.transform, "Label", 17, V5TitleKit.Dark, TextAnchor.MiddleLeft, true);
            goSub = V5TitleKit.Label(go.transform, "Sub", 11, DarkDim, TextAnchor.MiddleLeft);
            delete = V5Plate.Make(foot, "Delete", V5Plate.Look.Plate, 10f, OnDelete);
            deleteLabel = V5TitleKit.Label(delete.transform, "Label", 11, V5TitleKit.Ink3, TextAnchor.MiddleCenter);
            V5TitleKit.Stretch(deleteLabel.rectTransform, 6f);
            reset = V5Plate.Make(foot, "Reset", V5Plate.Look.Plate, 10f, OnResetKeys);
            resetLabel = V5TitleKit.Label(reset.transform, "Label", 17, V5TitleKit.OrangeHi, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(resetLabel.rectTransform, 6f);
            confirm = V5Plate.Make(foot, "Confirm", V5Plate.Look.Go, 10f, OnConfirmOverwrite, true);
            confirmLabel = V5TitleKit.Label(confirm.transform, "Label", 17, V5TitleKit.Dark, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(confirmLabel.rectTransform, 6f);
            cancel = V5Plate.Make(foot, "Cancel", V5Plate.Look.Plate, 10f, OnCancelOverwrite);
            cancelLabel = V5TitleKit.Label(cancel.transform, "Label", 17, V5TitleKit.OrangeHi, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(cancelLabel.rectTransform, 6f);
        }

        void ShowFoot(FootUse use)
        {
            footUse = use;
            bool shown = use != FootUse.None && !mapOpen;
            if (foot.gameObject.activeSelf != shown) foot.gameObject.SetActive(shown);
            Show(go, use is FootUse.Go or FootUse.GoAndDelete);
            Show(delete, use == FootUse.GoAndDelete);
            Show(reset, use == FootUse.Reset);
            Show(confirm, use == FootUse.Confirm);
            Show(cancel, use == FootUse.Confirm);
            go.Place(0f, 0f, use == FootUse.GoAndDelete ? InnerW - DeleteW - FootSplit : InnerW, FootH);
            delete.Place(InnerW - DeleteW, 0f, DeleteW, FootH);
            reset.Place(0f, 0f, InnerW, FootH);
            float half = (InnerW - FootSplit) * .5f;
            confirm.Place(0f, 0f, half, FootH);
            cancel.Place(half + FootSplit, 0f, half, FootH);
            bool on = Art.Enabled;
            goHazard.gameObject.SetActive(on);
            confirmLabel.color = on ? V5TitleKit.Dark : Color.white;
            cancelLabel.color = V5TitleKit.OrangeHi;
            resetLabel.color = V5TitleKit.OrangeHi;
        }

        static void Show(Component part, bool shown)
        {
            if (part && part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
        }

        /// <summary>The go button's words, the line after them, and whether it does anything.</summary>
        void SetGo(string label, string sub, bool live)
        {
            go.Button.interactable = live;
            go.Dim = !live;
            bool on = Art.Enabled;
            goLabel.text = label;
            goSub.text = sub;
            goLabel.color = !on ? (live ? Color.white : V5TitleKit.Ink3) : live ? V5TitleKit.Dark : DarkDim;
            goSub.color = !on ? V5TitleKit.Ink2 : DarkDim;
            float width = go.Rect.sizeDelta.x;
            V5TitleKit.Place(goLabel.rectTransform, GoTextLeft, 0f, width - GoTextLeft - 15f, FootH);
            V5TitleKit.FitWidth(goLabel, 17, 12);
            float labelW = Mathf.Min(goLabel.preferredWidth, width - GoTextLeft - 15f);
            V5TitleKit.Place(goLabel.rectTransform, GoTextLeft, 0f, labelW + 1f, FootH);
            float subLeft = GoTextLeft + labelW + 10.5f;
            V5TitleKit.Place(goSub.rectTransform, subLeft, 0f, Mathf.Max(10f, width - subLeft - 15f), FootH);
            V5TitleKit.FitWidth(goSub, 11, 8);
        }

        void OnGo()
        {
            switch (tab)
            {
                case ContinueTab: TryContinue(); break;
                case NewGameTab: OnNewGameGo(); break;
                case TutorialTab: OpenTutorial(); break;
            }
        }

        // ───── Rendering ─────

        void RenderAll()
        {
            ApplyFont();
            RenderArt();
            PlaceLogo();
            RenderMenu();
            PlacePointer();
            RenderPage();
            RenderMapPick();
            RenderBuildLabel();
        }

        void RenderArt()
        {
            bool on = Art.Enabled, hasPicture = picture && picture.texture;
            if (backdrop.gameObject.activeSelf != (on && hasPicture)) backdrop.gameObject.SetActive(on && hasPicture);
            V5TitleKit.Dress(windowPlate, on ? V5TitleKit.Piece("plate") : null, on ? Color.white : WhiteboxSkin.PanelFill, V5TitleKit.PlateDensity(16f));
            if (!on && !windowEdge)
            {
                windowEdge = windowPlate.gameObject.AddComponent<Outline>();
                windowEdge.useGraphicAlpha = false;
                windowEdge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
                windowEdge.effectColor = WhiteboxSkin.Edge;
            }
            if (windowEdge) windowEdge.enabled = !on;
        }

        void RenderPage()
        {
            if (tab < 0) return;
            RenderHead();
            switch (tab)
            {
                case ContinueTab: RenderContinue(); break;
                case NewGameTab: RenderNewGame(); break;
                case TutorialTab: RenderLessons(); break;
                case CollectionTab: RenderCodex(false); break;
                default: RenderSettings(); break;
            }
        }

        void RenderHead()
        {
            string cap, title;
            switch (tab)
            {
                case ContinueTab:
                    cap = "CONTINUE";
                    title = ArenaText.Pair("继续游戏", "Continue");
                    break;
                case NewGameTab:
                    bool second = newGameStep == 2, story = newGameMode == 0;
                    cap = !second ? "NEW GAME" : story ? "NEW GAME · STORY" : "NEW GAME · OPEN";
                    title = !second ? ArenaText.Pair("新游戏", "New Game")
                        : story ? ArenaText.Pair("新游戏 · 剧情模式", "New Game · Story Mode") : ArenaText.Pair("新游戏 · 开放模式", "New Game · Open Mode");
                    break;
                case TutorialTab:
                    cap = "TUTORIAL";
                    title = ArenaText.Pair("教学", "Tutorial");
                    break;
                case CollectionTab:
                    cap = "COLLECTION";
                    title = ArenaText.Pair("图鉴", "Collection");
                    break;
                default:
                    cap = "SETTINGS";
                    title = ArenaText.Pair("设置", "Settings");
                    break;
            }
            headCap.text = ArenaText.Pair(V5TitleKit.Spaced(cap), "");
            headTitle.text = title;
            V5TitleKit.FitWidth(headTitle, 24, 16);
        }

        void ApplyFont()
        {
            var font = V5TitleKit.Font;
            // The whitebox wordmark keeps a font of its own (FontAtlasGuard: its large letters would grow the game font's texture).
            foreach (var text in stage.GetComponentsInChildren<Text>(true))
                if (text.font != font && text.name != "WhiteboxWordmark") text.font = font;
            if (mapPick)
                foreach (var text in mapPick.GetComponentsInChildren<Text>(true))
                    if (text.font != font) text.font = font;
            if (buildLabel && buildLabel.font != font) buildLabel.font = font;
        }

        // ───── Every frame ─────

        void LateUpdate()
        {
            if (!built) return;
            FitStage();
            if (!ArenaText.Ready) return;
            if (savesDirty)
            {
                savesDirty = false;
                LoadSaves();
                if (shownChinese.HasValue && !mapOpen && tab is ContinueTab or NewGameTab) RenderPage();
            }
            var font = V5TitleKit.Font;
            bool chinese = ArenaText.IsChinese, on = Art.Enabled;
            if (shownChinese != chinese || shownFont != font || shownArt != on)
            {
                shownChinese = chinese;
                shownFont = font;
                shownArt = on;
                FinishTweens();
                RenderAll();
            }
            if (ArenaKeys.Revision != shownKeys)
            {
                shownKeys = ArenaKeys.Revision;
                if (tab == TutorialTab) RenderLessons();
                if (tab == SettingsTab) RenderKeys();
            }
            ReadKeys();
            TickSaves();
            TickSettings();
            TickTweens(Time.unscaledDeltaTime);
            SelectNow();
            var events = EventSystem.current;
            lastSelected = events ? events.currentSelectedGameObject : null;
        }

        /// <summary>The 1440 x 810 grid scaled down whole on a screen that cannot hold it, kept at the left; the picture covers the screen.</summary>
        void FitStage()
        {
            var size = content.rect.size;
            if (size == shownCanvas || size.x <= 0f || size.y <= 0f) return;
            shownCanvas = size;
            float scale = Mathf.Min(1f, size.x / StageW, size.y / StageH);
            stage.localScale = new Vector3(scale, scale, 1f);
            if (mapPick)
            {
                mapPick.localScale = stage.localScale;
                mapPick.anchoredPosition = new Vector2((WindowLeft + PadX) * scale, (StageH * .5f - MenuTop - PadY) * scale);
            }
            if (buildLabel)
            {
                buildLabel.rectTransform.localScale = stage.localScale;
                buildLabel.rectTransform.anchoredPosition = new Vector2(-GridLeft * scale, GridTop * scale);
            }
            float aspect = size.x / size.y;
            picture.uvRect = aspect >= PictureAspect
                ? new Rect(0f, (1f - PictureAspect / aspect) * .5f, 1f, PictureAspect / aspect)
                : new Rect((1f - aspect / PictureAspect) * .5f, 0f, aspect / PictureAspect, 1f);
        }

        /// <summary>Esc on the title: a key row waiting gives up, an overwrite question is answered no, the new game's second step goes back to
        /// the first. The map pick closes on its own Esc (V2Hud.CloseMapSelect); the tutorial's pages and the pause screen's panels have theirs.</summary>
        void ReadKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || V2Tutorial.HoldsInput || ArenaSettingsPanel.IsOpen || ArenaCodex.HoldsInput) return;
            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                if (!hud || hud.MapSelectClosedFrame != Time.frameCount) GoBack();
                return;
            }
            if (listening >= 0) ReadBindingKey(keyboard);
        }

        /// <summary>What Esc does on the title (open to tests, as ArenaSettingsPanel.Back): true when it did something.</summary>
        public static bool Back() => active && active.GoBack();

        bool GoBack()
        {
            if (listening >= 0)
            {
                StopListening(ArenaSettingsPanel.NothingChanged, true);
                V5UiSound.Play(V5UiSound.Ui.Back);
                return true;
            }
            if (mapOpen) return false;
            if (overwriteAsked)
            {
                OnCancelOverwrite();
                return true;
            }
            if (tab == NewGameTab && newGameStep == 2)
            {
                OnBackToModes();
                return true;
            }
            return false;
        }

        void SelectNow()
        {
            var events = EventSystem.current;
            if (!events) return;
            if (pendingSelect)
            {
                var target = pendingSelect;
                pendingSelect = null;
                if (target.activeInHierarchy) Select(target);
            }
            if (!selectPending) return;
            selectPending = false;
            var selected = events.currentSelectedGameObject;
            if (selected && selected.activeInHierarchy && selected.transform.IsChildOf(content)) return;
            if (tab >= 0 && items[tab]) Select(items[tab].gameObject);
        }

        /// <summary>Selects a part of the title by code, which is not the keyboard's navigation (no sound, the window stays).</summary>
        static void Select(GameObject target)
        {
            var events = EventSystem.current;
            if (!events || !target) return;
            V5Plate.Quiet = true;
            try { events.SetSelectedGameObject(target); }
            finally { V5Plate.Quiet = false; }
        }

        // ───── What moves ─────

        sealed class Tween
        {
            public object key;
            public float delay, duration, age;
            public Func<float, float> ease;
            public Action<float> apply;
        }

        readonly List<Tween> tweens = new();

        /// <summary>Runs apply from 0 to 1 over duration after delay, eased; a newer one for the same key ends the older at its end.</summary>
        void Animate(object key, float duration, float delay, Func<float, float> ease, Action<float> apply)
        {
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(tweens[i].key, key)) continue;
                var old = tweens[i];
                tweens.RemoveAt(i);
                old.apply(1f);
            }
            var tween = new Tween { key = key, delay = delay, duration = Mathf.Max(.01f, duration), ease = ease ?? (t => t), apply = apply };
            tweens.Add(tween);
            apply(tween.ease(0f));
        }

        void TickTweens(float dt)
        {
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                if (i >= tweens.Count) continue;
                var tween = tweens[i];
                tween.age += dt;
                float t = Mathf.Clamp01((tween.age - tween.delay) / tween.duration);
                tween.apply(tween.ease(t));
                if (t >= 1f) tweens.Remove(tween);
            }
        }

        void FinishTweens()
        {
            var running = tweens.ToArray();
            tweens.Clear();
            foreach (var tween in running) tween.apply(1f);
        }

        static float EaseIn(float t) => V5TitleKit.Bezier(t, .2f, .8f, .2f, 1f);

        /// <summary>The window's head, the page's parts and the foot slide in from the right, one after another.</summary>
        void Enter()
        {
            var blocks = new List<V5Block> { headBlock };
            if (tab >= 0 && pageRoots[tab])
                foreach (Transform child in pageRoots[tab])
                    if (child.gameObject.activeSelf && child.TryGetComponent<V5Block>(out var part)) blocks.Add(part);
            if (foot.gameObject.activeSelf) blocks.Add(footBlock);
            for (int k = 0; k < blocks.Count; k++)
            {
                var block = blocks[k];
                Animate(block, EnterSeconds, k * EnterStagger, EaseIn, p =>
                {
                    block.Offset = new Vector2(EnterShift * (1f - p), 0f);
                    block.Alpha = p;
                });
            }
        }

        /// <summary>A short shake: a card not met yet, an empty slot that cannot be continued.</summary>
        void Shake(V5Block block)
        {
            float[] keys = { 0f, -5f, 4f, -2f, 0f };
            Animate(block, .26f, 0f, V5TitleKit.EaseOut, p =>
            {
                float at = p * 4f;
                int i = Mathf.Min(3, (int)at);
                block.Offset = new Vector2(Mathf.Lerp(keys[i], keys[i + 1], at - i) * Css, 0f);
            });
        }

        static V5Block Block(Transform parent, string name, float left, float top, float width, float height)
        {
            var block = V5TitleKit.Rect(parent, name).gameObject.AddComponent<V5Block>();
            block.Place(left, top, width, height);
            return block;
        }

        // ───── Medals: the game's icons in a machined ring ─────

        sealed class MedalView
        {
            public RectTransform root;
            public Image ring, hole, icon;
            public Outline edge;
            public float size;
        }

        static MedalView Medal(Transform parent, string name, float size)
        {
            var view = new MedalView { root = V5TitleKit.Rect(parent, name), size = size };
            view.ring = V5TitleKit.Picture(view.root, "Ring", V5TitleKit.Piece("medal"), Color.white);
            V5TitleKit.Centre(view.ring.rectTransform, Vector2.one * size * MedalArt);
            view.hole = V5TitleKit.Picture(view.root, "Hole", V5TitleKit.Piece("disc"), Color.white);
            V5TitleKit.Centre(view.hole.rectTransform, Vector2.one * size * .72f);
            view.hole.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            view.icon = V5TitleKit.Picture(view.hole.transform, "Icon", null, Color.white);
            V5TitleKit.Stretch(view.icon.rectTransform);
            return view;
        }

        /// <summary>With the art on, the icon in the ring; in whitebox a flat disc with a thin edge and no picture.</summary>
        static void ShowMedal(MedalView view, Sprite icon)
        {
            bool on = Art.Enabled;
            V5TitleKit.Dress(view.ring, V5TitleKit.Piece(on ? "medal" : "disc"), on ? Color.white : WhiteboxSkin.ButtonFill);
            view.ring.rectTransform.sizeDelta = Vector2.one * view.size * (on ? MedalArt : 1f);
            if (!on && !view.edge)
            {
                view.edge = view.ring.gameObject.AddComponent<Outline>();
                view.edge.useGraphicAlpha = false;
                view.edge.effectColor = WhiteboxSkin.Edge;
                view.edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (view.edge) view.edge.enabled = !on;
            bool shown = on && icon;
            if (view.hole.gameObject.activeSelf != shown) view.hole.gameObject.SetActive(shown);
            view.icon.sprite = icon;
        }

        static CampaignArt CampaignIcons => GameManager.Instance && GameManager.Instance.TryGetComponent<V2Campaign>(out var campaign) ? campaign.art : null;

        // ───── The open mode's map pick, in the window's frame ─────

        RectTransform mapPick;
        Text mapLine;
        RawImage mapRule;
        readonly Text[] mapLabels = new Text[4];
        readonly Image[] mapThumbs = new Image[3], mapPictures = new Image[3];
        Text mapGuess;

        /// <summary>V2Hud's map pick (the line, the Dunes, the Ruins, a random map, back) dressed as the window's page: the line as its head, the
        /// three maps as plates with their ground in a hexagon, back as the foot. It stays under the title's Content, where V2Hud finds it.</summary>
        void DressMapPick()
        {
            mapPick = content.Find(V2Hud.MapSelect) as RectTransform;
            if (!mapPick) return;
            mapPick.anchorMin = mapPick.anchorMax = new Vector2(0f, .5f);
            mapPick.pivot = new Vector2(0f, 1f);
            mapPick.sizeDelta = new Vector2(InnerW, WindowH - 2f * PadY);
            if (mapPick.Find("MapLine") is RectTransform lineRect && lineRect.TryGetComponent(out mapLine))
            {
                V5TitleKit.Place(lineRect, 0f, 19.5f, InnerW, 26f);
                mapLine.fontSize = 24;
                mapLine.fontStyle = FontStyle.Bold;
                mapLine.alignment = TextAnchor.MiddleLeft;
                mapLine.horizontalOverflow = HorizontalWrapMode.Overflow;
                mapLine.verticalOverflow = VerticalWrapMode.Overflow;
                V5TitleKit.Painted(mapLine);
            }
            mapRule = V5TitleKit.Raw(mapPick, "Rule", V5TitleKit.FadeRight, RuleColour);
            V5TitleKit.Place(mapRule.rectTransform, 0f, RuleTop - PadY, InnerW, 1f);
            float top = BodyTop - PadY, height = (BodyH - 2f * SlotGap) / 3f;
            for (int i = 0; i < 4 && i < V2Hud.MapButtons.Length; i++)
            {
                if (mapPick.Find(V2Hud.MapButtons[i]) is not RectTransform button) continue;
                bool back = i == 3;
                mapLabels[i] = button.GetComponentInChildren<Text>(true);
                foreach (Transform child in button)
                    if (child.name.Contains("Rule")) child.gameObject.SetActive(false);
                var plate = V5Plate.Dress(button.gameObject, V5Plate.Look.Plate, back ? 10f : 12f, !back);
                if (back) plate.Place(0f, FootTop - PadY, InnerW, FootH);
                else
                {
                    plate.Place(0f, top + i * (height + SlotGap), InnerW, height);
                    plate.HoverPitch = 1.1f - i * .05f;
                    if (button.TryGetComponent<Button>(out var click)) click.onClick.AddListener(() => V5UiSound.Play(V5UiSound.Ui.Start));
                    var hex = V5TitleKit.Picture(button, "Thumb", V5TitleKit.Piece("hex-mask"), Color.white);
                    V5TitleKit.Place(hex.rectTransform, 15f, (height - 60f) * .5f, 69f, 60f);
                    hex.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                    mapThumbs[i] = hex;
                    mapPictures[i] = V5TitleKit.Picture(hex.transform, "Picture", i < 2 ? V5TitleKit.Piece(i == 0 ? "map-dunes" : "map-ruins") : null, Color.white);
                    V5TitleKit.Stretch(mapPictures[i].rectTransform);
                    if (i == 2)
                    {
                        mapGuess = V5TitleKit.Label(hex.transform, "Mark", 26, V5TitleKit.OrangeHi, TextAnchor.MiddleCenter, true);
                        V5TitleKit.Stretch(mapGuess.rectTransform);
                    }
                }
                if (!mapLabels[i]) continue;
                var label = mapLabels[i];
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                label.fontStyle = FontStyle.Bold;
                label.raycastTarget = false;
                if (back)
                {
                    V5TitleKit.Stretch(label.rectTransform, 6f);
                    label.alignment = TextAnchor.MiddleCenter;
                }
                else
                {
                    V5TitleKit.Place(label.rectTransform, 99f, 0f, InnerW - 99f - 15f, height);
                    label.alignment = TextAnchor.MiddleLeft;
                }
                // The button's own word stays its first text (the random map's mark comes after it).
                label.transform.SetAsFirstSibling();
            }
        }

        void RenderMapPick()
        {
            if (!mapPick) return;
            bool on = Art.Enabled;
            if (mapLine)
            {
                mapLine.text = V2Hud.MapLineWords();
                V5TitleKit.FitWidth(mapLine, 24, 14);
            }
            for (int i = 0; i < mapLabels.Length; i++)
            {
                if (mapLabels[i])
                {
                    mapLabels[i].text = V2Hud.MapWord(i);
                    mapLabels[i].color = i == 3 ? V5TitleKit.OrangeHi : V5TitleKit.Ink;
                    V5TitleKit.FitWidth(mapLabels[i], i == 3 ? 17 : 20, 12);
                }
                if (i >= 3 || !mapThumbs[i]) continue;
                bool shown = on && mapPictures[i].sprite;
                if (mapPictures[i].gameObject.activeSelf != shown) mapPictures[i].gameObject.SetActive(shown);
                mapThumbs[i].color = shown ? Color.white : on ? V5TitleKit.Rgb(20, 28, 46) : MapFlat[i];
            }
            if (mapGuess) mapGuess.text = ArenaText.Pair("？", "?");
        }

        /// <summary>V2Hud opened or closed the open mode's map pick: the window lends it its frame, and its own parts step aside.</summary>
        public void ShowMapPick(bool open)
        {
            mapOpen = open;
            // Back from the map pick (its button, Esc, another window): the open mode's slot keeps what it holds.
            if (!open) pendingOpenSlot = -1;
            if (head) head.gameObject.SetActive(!open);
            if (rule) rule.gameObject.SetActive(!open);
            if (pages) pages.gameObject.SetActive(!open);
            if (foot && open) foot.gameObject.SetActive(false);
            if (!built) return;
            FinishTweens();
            if (open) RenderMapPick();
            else RenderPage();
        }

        /// <summary>Back from the map pick: the keyboard's selection goes to the slot the new game was for.</summary>
        public void FocusAfterMapPick()
        {
            int slot = newGameChosen[1];
            pendingSelect = tab == NewGameTab && newGameStep == 2 && slot >= 0 && newGameSlots[slot] != null ? newGameSlots[slot].plate.gameObject
                : tab >= 0 && items[tab] ? items[tab].gameObject : null;
        }

        // ───── The build line ─────

        /// <summary>The line naming the page's build (V2Hud.BuildLabelWords; the whole game shows none) at the bottom-right corner.</summary>
        void DressBuildLabel()
        {
            var found = content.Find("BuildLabel");
            if (!found || !found.TryGetComponent(out buildLabel)) return;
            var rect = buildLabel.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(360f, 16f);
            buildLabel.alignment = TextAnchor.MiddleRight;
            buildLabel.fontSize = 11;
            buildLabel.color = V5TitleKit.Ink3;
        }

        void RenderBuildLabel()
        {
            if (buildLabel) buildLabel.text = V2Hud.BuildLabelWords();
        }
    }
}
