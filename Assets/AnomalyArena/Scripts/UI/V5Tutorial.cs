using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The tutorial's pages in the fifth pass's look (work/art-plans/style-1112/ui-e), on every page but the English Alpha's, which keeps the
    /// pages it was graded with (V5HudKit.On). The window is the title's (V5Title): a gunmetal plate whose head carries the spaced caps
    /// "TUTORIAL" and the page's title in the cream paint over a hairline that fades to the right. Under the head the page's demo plays on a
    /// dark screen framed by a plate, at the size the demos are drawn for; under the screen comes the page's line, and on the last page the
    /// footnote. The foot holds Skip, the page pips with "2 / 7", Previous and Next: the title's plates, each with its key on a keycap (Esc,
    /// and the left and right arrows, which turn the pages as the buttons do); Next is the orange go button and reads 知道了 on the last page.
    /// A page that opens slides its head, its demo and its line in from the right one after another, as the title's window does (from the
    /// left when going back), and its demo starts over. With the art on, the plates, keycaps and pips are the title's pictures; in whitebox
    /// they are flat with the thin edge (WhiteboxSkin), and the pages follow the art switch while they are open. The demos draw their keys,
    /// panels, slots and rows in the same family (the Demo parts below); what they show of the game keeps the game's own colours.
    /// The objects keep the names the Alpha's pages have (HowToPlay/Panel with its Heading, Line, Footnote, Skip and Next), so everything
    /// that finds them by name still does.
    /// </summary>
    public sealed partial class V2Tutorial
    {
        // Set when the pages are built in the fifth pass's look: the demos draw their parts in it too.
        static bool fifth;

        // Canvas units on the pages' 1440 x 810 canvas: the mock-up's pixels times Css. The screen is the size the demos are drawn for
        // (WindowW x WindowH) with a frame round it; the head, the rule and the body start where the title's window has them.
        const float Css = V5TitleKit.Css;
        const float PadX = 22.5f, PadY = 19.5f, StageFrame = 9f, StageW = WindowW + 2f * StageFrame, StageH = WindowH + 2f * StageFrame;
        const float InnerW = StageW, FifthW = InnerW + 2f * PadX;
        const float CapTop = PadY + 1.5f, TitleTop = PadY + 19.5f, RuleTop = 75f, StageTop = 88f;
        const float LineTop = StageTop + StageH + 12f, LineH = 54f, NoteTop = LineTop + LineH + 2f, NoteH = 18f;
        const float FootTop = NoteTop + NoteH + 12f, FootH = 45f, FifthH = FootTop + FootH + PadY;
        const float SkipW = 168f, PreviousW = 150f, NextW = 214f, FootSplit = 7.5f, GoTextLeft = 70f * Css, KeyGap = 8f;
        const float PipW = 20f * Css, PipH = 11f * Css, PipGap = 5f * Css, CounterGap = 9f;
        // The line takes the largest size at which it fits its box (two lines); the head's title and the footnote fit one line each. Never
        // best fit, which shrinks Chinese to one line (pitfall T-015).
        const int LineLargest = 17, LineSmallest = 13, TitleLargest = 24, TitleSmallest = 16, NoteLargest = 13, NoteSmallest = 10;
        const int ButtonLargest = 17, ButtonSmallest = 12;
        // The parts that come in slide this far, for this long, each this long after the one before (the title's window, V5Title.Enter).
        const float SlideShift = 22f * Css, SlideSeconds = .28f, SlideStagger = .04f;

        static readonly Color DimFill = V5TitleKit.Rgb(7, 10, 20, .8f), ScreenFill = V5TitleKit.Rgb(6, 9, 16), ScreenEdge = V5TitleKit.Rgb(160, 170, 190, .25f),
            RuleColour = V5TitleKit.Rgb(163, 172, 190, .35f), FifthGrid = V5TitleKit.Rgb(163, 172, 190, .06f), PipOff = V5TitleKit.Rgb(36, 42, 58),
            HazardInk = V5TitleKit.Rgb(27, 14, 6, .55f);

        Image framePlate, stagePlate;
        RawImage hazard;
        V5Plate skip, previous, next;
        Text capLabel, skipLabel, previousLabel;
        V5HudKey skipKey, previousKey, nextKey;
        // The parts that slide in when a page opens, each with its place in the order.
        (V5Block block, int step)[] sliding;
        bool? dressedArt;
        int slideFrom = 1, slideSign = 1;
        float slideAge = -1f;
        GameObject selectedBefore;

        /// <summary>The pages' canvas in the fifth pass's look.</summary>
        static V2Tutorial BuildFifth()
        {
            var root = new GameObject("HowToPlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440f, 810f);
            scaler.matchWidthOrHeight = .5f;
            var tutorial = root.AddComponent<V2Tutorial>();

            // A deep wash over the screen; a click anywhere off the window stops at it.
            var dim = V5TitleKit.Picture(root.transform, "Dim", null, DimFill);
            V5TitleKit.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            tutorial.framePlate = V5TitleKit.Picture(root.transform, "Panel", null, Color.white);
            tutorial.framePlate.raycastTarget = true;
            var body = tutorial.framePlate.rectTransform;
            V5TitleKit.Centre(body, new Vector2(FifthW, FifthH));

            // The head: the window's spaced caps and the page's title in the cream paint, over the hairline.
            tutorial.capLabel = V5TitleKit.Label(body, "Cap", 10, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
            tutorial.heading = V5TitleKit.Label(body, "Heading", TitleLargest, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(tutorial.heading);
            var rule = V5TitleKit.Raw(body, "Rule", V5TitleKit.FadeRight, RuleColour);
            V5TitleKit.Place(rule.rectTransform, PadX, RuleTop, InnerW, 1f);

            // The demo's screen in its frame. The screen clips the demo and is where the demos draw, centred on (0, 0).
            tutorial.stagePlate = V5TitleKit.Picture(body, "Stage", null, Color.white);
            var edge = V5TitleKit.Picture(tutorial.stagePlate.transform, "ScreenEdge", null, ScreenEdge);
            V5TitleKit.Centre(edge.rectTransform, new Vector2(WindowW + 2f, WindowH + 2f));
            var screen = V5TitleKit.Picture(tutorial.stagePlate.transform, "Window", null, ScreenFill);
            V5TitleKit.Centre(screen.rectTransform, new Vector2(WindowW, WindowH));
            screen.gameObject.AddComponent<RectMask2D>();
            tutorial.window = screen.rectTransform;

            tutorial.line = V5TitleKit.Label(body, "Line", LineLargest, V5TitleKit.Ink, TextAnchor.UpperLeft);
            tutorial.line.horizontalOverflow = HorizontalWrapMode.Wrap;
            // Nothing is ever cut: the size is picked so the line fits its box, and a line that would still need more room runs over it.
            tutorial.line.verticalOverflow = VerticalWrapMode.Overflow;
            tutorial.footnote = V5TitleKit.Label(body, "Footnote", NoteLargest, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            tutorial.footnote.text = Footnote();

            tutorial.sliding = new[]
            {
                (Slider(tutorial.capLabel, PadX, CapTop, InnerW, 12f), 0), (Slider(tutorial.heading, PadX, TitleTop, InnerW, 26f), 0),
                (Slider(tutorial.stagePlate, PadX, StageTop, StageW, StageH), 1), (Slider(tutorial.line, PadX, LineTop, InnerW, LineH), 2),
                (Slider(tutorial.footnote, PadX, NoteTop, InnerW, NoteH), 3),
            };

            // The foot: Skip at the left, Previous and Next at the right, the pips and the counter between them.
            float nextLeft = PadX + InnerW - NextW, previousLeft = nextLeft - FootSplit - PreviousW;
            tutorial.skip = V5Plate.Make(body, "Skip", V5Plate.Look.Plate, 10f, Skip);
            tutorial.skip.Place(PadX, FootTop, SkipW, FootH);
            tutorial.skipLabel = V5TitleKit.Label(tutorial.skip.transform, "Label", ButtonLargest, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
            tutorial.skipKey = V5HudKit.Key(tutorial.skip.transform, "Key", "Esc", true, Css, 24f, 13f);
            tutorial.previous = V5Plate.Make(body, "Previous", V5Plate.Look.Plate, 10f, Previous);
            tutorial.previous.Place(previousLeft, FootTop, PreviousW, FootH);
            tutorial.previousLabel = V5TitleKit.Label(tutorial.previous.transform, "Label", ButtonLargest, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            tutorial.previousKey = V5HudKit.Key(tutorial.previous.transform, "Key", "←", false, Css, 24f, 13f);
            tutorial.next = V5Plate.Make(body, "Next", V5Plate.Look.Go, 10f, Turn, true);
            tutorial.next.Place(nextLeft, FootTop, NextW, FootH);
            tutorial.hazard = V5TitleKit.Raw(tutorial.next.transform, "Hazard", V5TitleKit.Stripes, HazardInk);
            V5TitleKit.Place(tutorial.hazard.rectTransform, 14f * Css, 12f * Css, 40f * Css, FootH - 24f * Css);
            tutorial.hazard.uvRect = new Rect(0f, 0f, 40f * Css / V5TitleKit.HazardRepeat, (FootH - 24f * Css) / V5TitleKit.HazardRepeat);
            tutorial.nextLabel = V5TitleKit.Label(tutorial.next.transform, "Label", ButtonLargest, V5TitleKit.Dark, TextAnchor.MiddleLeft, true);
            tutorial.nextKey = V5HudKit.Key(tutorial.next.transform, "Key", "→", false, Css, 24f, 13f);
            // The arrows turn the pages and nothing submits a selected button (SourceKeysTest): the keyboard's navigation stays off them.
            foreach (var plate in new[] { tutorial.skip, tutorial.previous, tutorial.next })
                plate.Button.navigation = new Navigation { mode = Navigation.Mode.None };

            tutorial.dots = new Image[PageCount];
            for (int i = 0; i < PageCount; i++) tutorial.dots[i] = V5TitleKit.Picture(body, "Dot" + i, V5TitleKit.Piece("pip"), PipOff);
            tutorial.counter = V5TitleKit.Label(body, "Counter", 12, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);

            tutorial.DressFrame();
            root.SetActive(false);
            return tutorial;
        }

        /// <summary>Gives a part of the window its place to rest and to slide in from (V5Block).</summary>
        static V5Block Slider(Component part, float left, float top, float width, float height)
        {
            var block = part.gameObject.AddComponent<V5Block>();
            block.Place(left, top, width, height);
            return block;
        }

        /// <summary>The window's own pictures in the look the art switch asks for (the buttons and keycaps follow it themselves).</summary>
        void DressFrame()
        {
            bool art = Art.Enabled;
            dressedArt = art;
            V5TitleKit.Dress(framePlate, art ? V5TitleKit.Piece("plate") : null, art ? Color.white : WhiteboxSkin.PanelFill, V5TitleKit.PlateDensity(16f));
            FlatEdge(framePlate, !art);
            V5TitleKit.Dress(stagePlate, art ? V5TitleKit.Piece("plate") : null, art ? Color.white : WhiteboxSkin.PlateFill, V5TitleKit.PlateDensity(12f));
            FlatEdge(stagePlate, !art);
            // The go button's stripes and its dark ink belong to the orange plate; the whitebox's blue one takes white words.
            if (hazard.gameObject.activeSelf != art) hazard.gameObject.SetActive(art);
            nextLabel.color = art ? V5TitleKit.Dark : Color.white;
        }

        /// <summary>Open's part in the fifth pass's look, once the page's words are set: the words fitted to their boxes, the pips, the
        /// buttons' words, then the parts slide in.</summary>
        void ShowFifth(int index, bool last)
        {
            if (dressedArt != Art.Enabled) DressFrame();
            capLabel.text = V5TitleKit.Spaced("TUTORIAL");
            V5TitleKit.FitWidth(heading, TitleLargest, TitleSmallest);
            ArenaTextFit.Refit(line, LineLargest, LineSmallest);
            V5TitleKit.FitWidth(footnote, NoteLargest, NoteSmallest);
            for (int i = 0; i < dots.Length; i++) dots[i].color = i <= index ? V5TitleKit.OrangeHi : PipOff;
            skipLabel.text = last ? ArenaText.Pair("关闭", "Close") : ArenaText.Pair("跳过", "Skip");
            previousLabel.text = ArenaText.Pair("上一页", "Previous");
            nextLabel.text = last ? ArenaText.Pair("知道了", "Got It") : ArenaText.Pair("下一页", "Next");
            bool back = index > 0;
            previous.Button.interactable = back;
            // The first page has none before it: Previous keeps its place, faded.
            previous.Alpha = back ? 1f : .4f;
            LayFoot();
            Slide(slideFrom);
            slideFrom = 1;
        }

        /// <summary>The words and keys inside the buttons, and the pips with the counter centred between Skip and Previous.</summary>
        void LayFoot()
        {
            LayCentred(skip.Rect, skipLabel, skipKey, false);
            LayCentred(previous.Rect, previousLabel, previousKey, true);
            // The go button: its words after the stripes, its key at the right end, as the mock-up's go button carries its key.
            float keyW = nextKey.Rect.sizeDelta.x, right = 12f * Css;
            PlaceKey(nextKey, NextW - right - keyW);
            V5TitleKit.Place(nextLabel.rectTransform, GoTextLeft, 0f, NextW - GoTextLeft - right - keyW - KeyGap, FootH);
            V5TitleKit.FitWidth(nextLabel, ButtonLargest, ButtonSmallest);
            float counterW = Mathf.Ceil(counter.preferredWidth) + 2f, pipsW = PageCount * PipW + (PageCount - 1) * PipGap;
            float from = PadX + SkipW, to = PadX + InnerW - NextW - FootSplit - PreviousW;
            float left = (from + to - pipsW - CounterGap - counterW) * .5f;
            for (int i = 0; i < dots.Length; i++) V5TitleKit.Place(dots[i].rectTransform, left + i * (PipW + PipGap), FootTop + (FootH - PipH) * .5f, PipW, PipH);
            V5TitleKit.Place(counter.rectTransform, left + pipsW + CounterGap, FootTop, counterW, FootH);
        }

        /// <summary>A button's word and its keycap side by side, centred in the button; the key first or after the word.</summary>
        static void LayCentred(RectTransform plate, Text label, V5HudKey key, bool keyFirst)
        {
            float width = plate.sizeDelta.x, height = plate.sizeDelta.y;
            V5TitleKit.Place(label.rectTransform, 0f, 0f, width - key.Rect.sizeDelta.x - KeyGap - 16f, height);
            V5TitleKit.FitWidth(label, ButtonLargest, ButtonSmallest);
            float labelW = Mathf.Min(Mathf.Ceil(label.preferredWidth) + 1f, width - key.Rect.sizeDelta.x - KeyGap - 16f), keyW = key.Rect.sizeDelta.x;
            float left = Mathf.Max(8f, (width - labelW - KeyGap - keyW) * .5f);
            V5TitleKit.Place(label.rectTransform, keyFirst ? left + keyW + KeyGap : left, 0f, labelW, height);
            PlaceKey(key, keyFirst ? left : left + labelW + KeyGap);
        }

        /// <summary>A keycap left canvas units from its button's left edge, centred on the button's height.</summary>
        static void PlaceKey(V5HudKey key, float left)
        {
            var rect = key.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, 0f);
        }

        /// <summary>The page's parts come in: from the right (1), from the left (-1, back to the page before), or at once (0).</summary>
        void Slide(int from)
        {
            slideSign = from;
            slideAge = from == 0 ? -1f : 0f;
            DrawSlide();
        }

        void DrawSlide()
        {
            bool moving = false;
            foreach (var (block, step) in sliding)
            {
                float t = slideAge < 0f ? 1f : Mathf.Clamp01((slideAge - step * SlideStagger) / SlideSeconds);
                if (t < 1f) moving = true;
                float p = V5TitleKit.Bezier(t, .2f, .8f, .2f, 1f);
                block.Offset = new Vector2(slideSign * SlideShift * (1f - p), 0f);
                block.Alpha = p;
            }
            if (!moving) slideAge = -1f;
        }

        /// <summary>The fifth pass's part of a frame: the look follows the art switch, the arrows turn the pages, the parts slide in. False
        /// once the pages have closed.</summary>
        bool UpdateFifth()
        {
            if (dressedArt != Art.Enabled)
            {
                // Drawn again in the new look: the frame, and the page, whose demo starts over without sliding in.
                DressFrame();
                slideFrom = 0;
                Open(page);
            }
            if (Keyboard.current is { } keyboard)
            {
                if (keyboard.rightArrowKey.wasPressedThisFrame) Turn();
                else if (keyboard.leftArrowKey.wasPressedThisFrame) Previous();
            }
            if (!IsOpen) return false;
            if (slideAge >= 0f)
            {
                slideAge += Time.unscaledDeltaTime;
                DrawSlide();
            }
            return true;
        }

        /// <summary>Back to the page before, as the Previous button and the left arrow do; nothing on the first page.</summary>
        static void Previous()
        {
            if (!IsOpen || instance.page <= 0) return;
            V5UiSound.Play(V5UiSound.Ui.Panel, PagePitch(instance.page - 1));
            instance.slideFrom = -1;
            instance.Open(instance.page - 1);
        }

        /// <summary>The sound of Next and of the right arrow: the window's page sound, or a press on the last page, which closes them.</summary>
        static void TurnSound()
        {
            if (!IsOpen) return;
            if (instance.page >= PageCount - 1) V5UiSound.Play(V5UiSound.Ui.Press);
            else V5UiSound.Play(V5UiSound.Ui.Panel, PagePitch(instance.page + 1));
        }

        // The page sound climbs a little page by page, as the title's window sound does item by item.
        static float PagePitch(int page) => 1f + (page - 3) * .02f;

        /// <summary>The pages have closed: the parts rest where they belong, and the selection goes back to what had it.</summary>
        void ClosedFifth()
        {
            slideAge = -1f;
            DrawSlide();
            GiveSelectionBack();
        }

        /// <summary>The arrows turn the pages, so the button behind them (the title's, the pause screen's) gives up the selection while
        /// they are open: the keyboard's navigation would otherwise move it under the pages.</summary>
        void TakeSelection()
        {
            var events = EventSystem.current;
            selectedBefore = events ? events.currentSelectedGameObject : null;
            if (events && selectedBefore) events.SetSelectedGameObject(null);
        }

        /// <summary>Gives the selection back to what had it before the pages opened, quietly (no hover sound, no light), unless something
        /// outside the pages has taken it since.</summary>
        void GiveSelectionBack()
        {
            var events = EventSystem.current;
            var before = selectedBefore;
            selectedBefore = null;
            if (!events) return;
            var now = events.currentSelectedGameObject;
            if (now && !now.transform.IsChildOf(transform)) return;
            V5Plate.Quiet = true;
            try { events.SetSelectedGameObject(before && before.activeInHierarchy ? before : null); }
            finally { V5Plate.Quiet = false; }
        }

        /// <summary>The thin edge a flat plate has in whitebox (WhiteboxSkin), in the colour given; off with the art on.</summary>
        static void FlatEdge(Graphic graphic, bool shown, Color? colour = null)
        {
            if (!graphic) return;
            graphic.TryGetComponent<Outline>(out var edge);
            if (!edge)
            {
                if (!shown) return;
                edge = graphic.gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            var tint = colour ?? WhiteboxSkin.Edge;
            if (edge.effectColor != tint) edge.effectColor = tint;
            if (edge.enabled != shown) edge.enabled = shown;
        }

        // ───── The demos' parts in the fifth pass's look; on the Alpha's pages each part is drawn as it always was ─────

        abstract partial class Demo
        {
            // The corner cuts, in mock-up pixels, of the plates a demo shows: a panel, a weapon slot or a card, a row of a panel.
            const float SheetCut = 10f, SlotCut = 8f, RowCut = 6f;

            /// <summary>The mouse's buttons at rest: darker than its light cap, or lighter than the whitebox's flat one.</summary>
            protected static Color MouseIdle => Art.Enabled ? V5TitleKit.Rgb(150, 144, 132) : V5TitleKit.Rgb(54, 66, 86);

            /// <summary>The stamina bar's fill: the cream of the battle screen's stamina bar in the pass.</summary>
            protected static Color StaminaColour => fifth ? V5TitleKit.Rgb(240, 232, 212) : new Color(1f, .82f, .3f);

            /// <summary>The alien ground under the wreck: a dusky rust, since violet belongs to the void.</summary>
            protected static Color GroundColour => fifth ? V5TitleKit.Rgb(52, 36, 30, .92f) : new Color(.2f, .14f, .28f, .9f);

            /// <summary>A part of the ship mended: teal, the human systems' colour.</summary>
            protected static Color DoneMark => fifth ? V5TitleKit.Teal : Cyan;

            /// <summary>A key's name in the pause panel's key column: dark ink on the light keycap, the whitebox's ink on its flat cap.</summary>
            protected static Color KeyChipInk => fifth ? Art.Enabled ? V5HudKit.KeyInk : V5TitleKit.Ink : Cyan;

            /// <summary>A key as the pass draws keys: the title's light keycap, or its dark one for a name longer than two letters (Shift, Esc,
            /// Tab, as the title's lessons draw them), the name in dark ink on the light one; in whitebox a flat cap with the thin edge. Lit,
            /// it turns orange and sinks (Key.Light).</summary>
            Key FifthCap(string label, Vector2 at, float width)
            {
                var cap = NewImage(window, "Key " + label, null, Color.white);
                cap.rectTransform.sizeDelta = new Vector2(width, 40f);
                cap.rectTransform.anchoredPosition = at;
                var text = NewText(cap.rectTransform, "Label", 18, V5HudKit.KeyInk, FontStyle.Bold, TextAnchor.MiddleCenter);
                text.font = font;
                text.text = label;
                // A key the player chose may have a longer name than the cap was drawn for ("PgUp" on a letter's cap): it shrinks to fit.
                // Key names are Latin letters and arrows, never Chinese (pitfall T-015).
                text.resizeTextForBestFit = true;
                text.resizeTextMinSize = 9;
                text.resizeTextMaxSize = 18;
                Stretch(text.rectTransform);
                var key = new Key { cap = cap, text = text, dark = label.Length > 2 };
                key.Light(false);
                return key;
            }

            /// <summary>A small panel of the game inside a demo (the card pick, the pause screen, the base): a plate of the pass, faded and
            /// moved as one.</summary>
            (RectTransform rect, CanvasGroup fade) FifthSheet(Vector2 size, Vector2 at)
            {
                var frame = PassPlate(window, "Sheet", size, at, SheetCut);
                return (frame.rectTransform, frame.gameObject.AddComponent<CanvasGroup>());
            }

            /// <summary>The mouse: a light keycap with its two buttons, which light orange (Light).</summary>
            (Image left, Image right) FifthMouse(Vector2 at)
            {
                var body = NewImage(window, "Mouse", null, Color.white);
                body.rectTransform.sizeDelta = new Vector2(38f, 56f);
                body.rectTransform.anchoredPosition = at;
                bool art = Art.Enabled;
                V5TitleKit.Dress(body, art ? V5TitleKit.Piece("keycap") : null, art ? Color.white : WhiteboxSkin.ButtonFill, V5TitleKit.PieceDensity);
                FlatEdge(body, !art);
                var left = Bar(MouseIdle, new Vector2(15f, 20f), at + new Vector2(-9f, 14f));
                var right = Bar(MouseIdle, new Vector2(15f, 20f), at + new Vector2(9f, 14f));
                return (left, right);
            }

            /// <summary>A hand slot: a plate, the warm one with the orange rim and the orange and black tab over its top edge while it is the
            /// hand in use, as the battle screen's dock draws it (ChooseSlot). The caller sizes and places it.</summary>
            protected Image SlotPlate(string name)
            {
                if (!fifth) return NewImage(window, name, pill, KeyIdle, Image.Type.Sliced, 2f);
                var slot = PassPlate(window, name, Vector2.zero, Vector2.zero, SlotCut);
                var tab = NewImage(slot.rectTransform, "Tab", null, Color.white).rectTransform;
                tab.anchorMin = tab.anchorMax = tab.pivot = new Vector2(0f, 1f);
                tab.anchoredPosition = new Vector2(8f, 2.5f);
                tab.sizeDelta = new Vector2(36f, 5f);
                tab.gameObject.SetActive(false);
                return slot;
            }

            /// <summary>A row of a panel (the base's rooms): a plate, the selected one while it is chosen (ChooseRow).</summary>
            protected static Image RowPlate(RectTransform sheet, Vector2 size, Vector2 at) =>
                fifth ? PassPlate(sheet, "Row", size, at, RowCut) : Part(sheet, pill, KeyIdle, size, at);

            /// <summary>A card of the card pick: a plate.</summary>
            protected static Image CardPlate(RectTransform sheet, Vector2 size, Vector2 at) =>
                fifth ? PassPlate(sheet, "Card", size, at, SlotCut) : Part(sheet, pill, KeyIdle, size, at);

            /// <summary>A key in the pause panel's key column: the title's light keycap, flat in whitebox.</summary>
            protected static Image KeyChip(RectTransform sheet, Vector2 size, Vector2 at)
            {
                if (!fifth) return Part(sheet, pill, new Color(Cyan.r, Cyan.g, Cyan.b, .7f), size, at);
                var chip = NewImage(sheet, "Key", null, Color.white);
                chip.rectTransform.sizeDelta = size;
                chip.rectTransform.anchoredPosition = at;
                bool art = Art.Enabled;
                V5TitleKit.Dress(chip, art ? V5TitleKit.Piece("keycap") : null, art ? Color.white : WhiteboxSkin.ButtonFill, V5TitleKit.PieceDensity);
                FlatEdge(chip, !art);
                return chip;
            }

            /// <summary>A button of the pause panel: the first (resume) the orange go button, the others plain plates.</summary>
            protected static void PanelButton(RectTransform sheet, bool primary, Vector2 size, Vector2 at)
            {
                if (!fifth)
                {
                    var plate = primary ? buttonPrimary : button;
                    Part(sheet, plate, plate ? Color.white : new Color(.12f, .22f, .34f), size, at);
                    return;
                }
                var image = NewImage(sheet, "Button", null, Color.white);
                image.rectTransform.sizeDelta = size;
                image.rectTransform.anchoredPosition = at;
                DressPlate(image, primary ? "button-go" : "plate", SheetCut, primary ? WhiteboxSkin.PrimaryFill : WhiteboxSkin.ButtonFill, false);
            }

            /// <summary>A pause panel button's words: dark on the orange go button, cream on the others.</summary>
            protected static Color PanelButtonInk(bool primary) => fifth && primary && Art.Enabled ? V5TitleKit.Dark : Soft;

            protected static void ChooseSlot(Image slot, bool inUse)
            {
                if (!fifth)
                {
                    slot.color = inUse ? Cyan : KeyIdle;
                    return;
                }
                DressPlate(slot, inUse ? "plate-warm-selected" : "plate", SlotCut, inUse ? WhiteboxSkin.ButtonFill : WhiteboxSkin.PlateFill, inUse);
                if (slot.transform.Find("Tab") is not RectTransform tab) return;
                if (tab.gameObject.activeSelf != inUse) tab.gameObject.SetActive(inUse);
                if (!inUse || !tab.TryGetComponent<Image>(out var stripes)) return;
                bool art = Art.Enabled;
                V5TitleKit.Dress(stripes, art ? V5TitleKit.Piece("hazard-tab") : null, art ? Color.white : V5TitleKit.Orange);
            }

            protected static void ChooseRow(Image row, bool chosen)
            {
                if (!fifth) row.color = chosen ? Cyan : KeyIdle;
                else DressPlate(row, chosen ? "plate-selected" : "plate", RowCut, chosen ? WhiteboxSkin.ButtonFill : WhiteboxSkin.PlateFill, chosen);
            }

            static Image PassPlate(RectTransform parent, string name, Vector2 size, Vector2 at, float cut)
            {
                var image = NewImage(parent, name, null, Color.white);
                image.rectTransform.sizeDelta = size;
                image.rectTransform.anchoredPosition = at;
                DressPlate(image, "plate", cut, WhiteboxSkin.PlateFill, false);
                return image;
            }

            /// <summary>A plate's look: the title's picture of it with the art on, sliced at its corner cut; in whitebox a flat fill with the
            /// thin edge, orange while it is lit (as the battle screens' plates, V5HudPlate).</summary>
            static void DressPlate(Image image, string piece, float cut, Color flat, bool lit)
            {
                bool art = Art.Enabled;
                V5TitleKit.Dress(image, art ? V5TitleKit.Piece(piece) : null, art ? Color.white : flat, V5TitleKit.PlateDensity(cut));
                FlatEdge(image, !art, lit ? V5TitleKit.Orange : WhiteboxSkin.Edge);
            }
        }

        sealed partial class Key
        {
            /// <summary>The dark keycap, for a name longer than two letters.</summary>
            public bool dark;

            static readonly Color LightLit = new(1f, .72f, .52f), DarkLit = new(1f, .62f, .45f), FlatLit = new(.24f, .16f, .12f, 1f);

            /// <summary>Pressed in the pass's look: the light cap warms to orange, the dark one takes the orange of a key waiting to be set
            /// (V5Plate), the flat one an orange edge and orange letters; the cap sinks a little, as on the Alpha's pages.</summary>
            void LightFifth(bool on)
            {
                bool art = Art.Enabled;
                Color ink;
                if (art)
                {
                    V5TitleKit.Dress(cap, V5TitleKit.Piece(dark ? "keycap-dark" : "keycap"), on ? dark ? DarkLit : LightLit : Color.white, V5TitleKit.PieceDensity);
                    ink = !dark ? V5HudKit.KeyInk : on ? V5TitleKit.OrangeHi : V5TitleKit.Ink;
                }
                else
                {
                    V5TitleKit.Dress(cap, null, on ? FlatLit : WhiteboxSkin.ButtonFill);
                    ink = on ? V5TitleKit.OrangeHi : V5TitleKit.Ink;
                }
                text.color = ink;
                FlatEdge(cap, !art, on ? V5TitleKit.Orange : WhiteboxSkin.Edge);
                cap.rectTransform.localScale = Vector3.one * (on ? .9f : 1f);
            }
        }
    }
}
