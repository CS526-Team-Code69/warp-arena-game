using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The end of a run in the fifth pass's look (V5HudKit.On: every page but the Alpha's), on a canvas of its own over the old result
    /// screen, which stays underneath for its key (R goes back to the title, GameManagerFlow) and for the checks that read it. A 1920 x 1080
    /// page shrunk to fit the screen, in the character screen's family: the page's abyss; the hull-plate banner with the result
    /// ("探索者陨落", "基地被摧毁" or "即将重返地球") and where the run ended (the story's day, the open mode's wave and level); under it a
    /// window plate with what ended the run in plain words and what to know about it, a rule, the run's numbers, and one button back to
    /// the title. It shows while the run is in its result phase and goes with it, and it follows the art switch as the other pages do.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5ResultScreen : MonoBehaviour
    {
        /// <summary>The page's own canvas, a root object of the scene while the page exists.</summary>
        public const string RootName = "ResultScreen";

        // Over the battle screens, the story layer and the old result screen; under the character screen (27000), the collection
        // (28000), the settings (29000) and the tutorial's pages (30000).
        const int SortingOrder = 26000;
        const float Css = V5HudKit.StoryCss;
        // The page in mock-up pixels: the banner over the window, the two centred together; inside the window the cause, its note, a
        // rule, the numbers and the button at the foot.
        const float PageW = 1920f, PageH = 1080f;
        const float BannerW = 780f, BannerH = 111f, BannerEnds = 78f, BannerGap = 22f;
        const float WindowW = 1040f, WindowCut = 16f, StackGap = 20f, Side = 72f, Top = 52f;
        const float CausePx = 32f, NotePx = 21f, StatsPx = 22f, LineGap = 12f, RuleGap = 26f, RuleH = 2f;
        const float GoW = 420f, GoH = 74f, GoGap = 34f, GoFoot = 44f, GoPx = 24f, KeyGap = 14f;

        // The page's abyss with the art on, and the flat backing in whitebox (the character screen's).
        static readonly Color Abyss = V5TitleKit.Rgb(7, 10, 20), FlatBack = new(.01f, .02f, .05f, 1f);
        static Texture2D backdropTexture;
        static V5ResultScreen instance;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay() => instance = null;

        GameObject canvasRoot;
        Canvas canvas;
        RectTransform page;
        Image back, banner, rule;
        RawImage backdrop;
        V5HudPlate window;
        Text title, where, cause, note, stats, goLabel;
        V5Plate go;
        V5HudKey goKey;
        bool shown, shownArt;
        Font shownFont;
        string shownWords;

        /// <summary>The page shows now: the run is in its result phase and the fifth pass's look is on.</summary>
        public static bool IsShown => instance && instance.shown;
        /// <summary>The page's canvas object while it exists (the checks read its words).</summary>
        public static GameObject PageObject => instance ? instance.canvasRoot : null;

        public static V5ResultScreen Install(GameObject host)
        {
            if (instance) return instance;
            if (!host) return null;
            if (!host.TryGetComponent<V5ResultScreen>(out var screen)) screen = host.AddComponent<V5ResultScreen>();
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
            if (canvasRoot) Destroy(canvasRoot);
            instance = null;
        }

        void Update()
        {
            if (instance != this) return;
            var gm = GameManager.Instance;
            var s = gm ? gm.UiSnapshot : null;
            if (!V5HudKit.On || s == null || gm.RunPhase != ArenaUiState.Result)
            {
                if (shown) Hide();
                return;
            }

            if (!shown) Show();
            FitPage();
            string words = s.resultTitle + "\n" + s.resultWhere + "\n" + s.resultCause + "\n" + s.resultStats;
            if (words != shownWords || V5TitleKit.Font != shownFont || Art.Enabled != shownArt) Lay(s, words);
        }

        void Show()
        {
            Build();
            shown = true;
            shownWords = null;
            // Nothing stays selected behind the page: no key or pad button presses a button there.
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
            canvasRoot.SetActive(true);
        }

        void Hide()
        {
            shown = false;
            if (canvasRoot) canvasRoot.SetActive(false);
        }

        static void ClickBack()
        {
            var gm = GameManager.Instance;
            if (gm && gm.RunPhase == ArenaUiState.Result) gm.ExecuteUiCommand(ArenaUiCommand.Restart);
        }

        // ───── Building ─────

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
            back.raycastTarget = true; // no click reaches the old screen under the page
            backdrop = V5TitleKit.Raw(root, "Backdrop", BackdropTexture, Color.white);
            V5TitleKit.Stretch(backdrop.rectTransform);
            backdrop.uvRect = new Rect(.25f, .25f, .5f, .5f);
            page = V5TitleKit.Rect(root, "Page");
            V5TitleKit.Centre(page, new Vector2(P(PageW), P(PageH)));

            banner = V5TitleKit.Picture(page, "Banner", null, Color.white);
            banner.gameObject.AddComponent<V5StoryBannerFrame>();
            title = V5StoryKit.Label(banner.transform, "Title", 50f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(title);
            where = V5StoryKit.Label(banner.transform, "Where", 20f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);

            window = V5HudKit.Plate(page, "Window", V5HudKit.PlateLook.Plain, WindowCut, Css);
            cause = Paragraph(window.transform, "Cause", CausePx, V5TitleKit.Ink, true);
            note = Paragraph(window.transform, "Note", NotePx, V5TitleKit.Ink2, false);
            rule = V5TitleKit.Picture(window.transform, "Rule", null, V5HudKit.Hairline);
            stats = Paragraph(window.transform, "Stats", StatsPx, V5TitleKit.Ink2, false);
            // The title's plates take their corner cut in the title's own scale.
            go = V5Plate.Make(window.transform, "BackToTitle", V5Plate.Look.Go, 10f * Css / V5TitleKit.Css, ClickBack);
            go.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            goLabel = V5StoryKit.Label(go.transform, "Label", GoPx, V5TitleKit.Dark, TextAnchor.MiddleLeft, true);
            goKey = V5HudKit.Key(go.transform, "Key", "R", true, Css);
            canvasRoot.SetActive(false);
        }

        static Text Paragraph(Transform parent, string name, float px, Color colour, bool bold)
        {
            var label = V5StoryKit.Label(parent, name, px, colour, TextAnchor.UpperCenter, bold);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.lineSpacing = 1.15f;
            return label;
        }

        // ───── Laying out ─────

        void Lay(ArenaUiSnapshot s, string words)
        {
            shownWords = words;
            shownFont = V5TitleKit.Font;
            shownArt = Art.Enabled;
            foreach (var label in new[] { title, where, cause, note, stats, goLabel })
                if (label && shownFont) label.font = shownFont;
            back.color = shownArt ? Abyss : FlatBack;
            backdrop.enabled = shownArt;
            goLabel.color = shownArt ? V5TitleKit.Dark : Color.white;

            // What ended the run on its first line, what to know about it after; a run that was won has no cause.
            string causeWords = s.resultCause ?? "", noteWords = "";
            int split = causeWords.IndexOf('\n');
            if (split >= 0)
            {
                noteWords = causeWords.Substring(split + 1).Trim();
                causeWords = causeWords.Substring(0, split).Trim();
            }

            SetText(title, s.resultTitle);
            SetText(where, s.resultWhere);
            SetText(cause, causeWords);
            SetText(note, noteWords);
            SetText(stats, s.resultStats);
            SetText(goLabel, ArenaText.Pair("回到标题", "Back to Title"));
            goKey.SetText("R");

            // The window grows with its lines: the cause and its note, the rule, the numbers, the button.
            float inner = P(WindowW - 2f * Side), y = P(Top);
            bool hasCause = causeWords.Length > 0, hasNote = noteWords.Length > 0;
            Shown(cause, hasCause);
            Shown(note, hasNote);
            Shown(rule, hasCause);
            if (hasCause)
            {
                y += PlaceParagraph(cause, y, inner);
                if (hasNote) y += P(LineGap) + PlaceParagraph(note, y + P(LineGap), inner);
                y += P(RuleGap);
                V5TitleKit.Place(rule.rectTransform, P(Side), y, inner, P(RuleH));
                y += P(RuleH) + P(RuleGap);
            }

            y += PlaceParagraph(stats, y, inner);
            y += P(GoGap);
            go.Place(P((WindowW - GoW) * .5f), y, P(GoW), P(GoH));
            LayGo();
            float windowH = y + P(GoH) + P(GoFoot);

            // The banner and the window, centred on the page together.
            float top = (P(PageH) - (P(BannerH) + P(StackGap) + windowH)) * .5f;
            V5TitleKit.Place(banner.rectTransform, P((PageW - BannerW) * .5f), top, P(BannerW), P(BannerH));
            V5TitleKit.Place(window.Rect, P((PageW - WindowW) * .5f), top + P(BannerH) + P(StackGap), P(WindowW), windowH);
            LayBanner();
        }

        /// <summary>A wrapping paragraph from top in the window, as wide as width; its height.</summary>
        static float PlaceParagraph(Text label, float top, float width)
        {
            V5TitleKit.Place(label.rectTransform, P(Side), top, width, P(40f));
            float height = Mathf.Max(label.preferredHeight, label.fontSize * 1.2f);
            V5TitleKit.Place(label.rectTransform, P(Side), top, width, height);
            return height;
        }

        /// <summary>The result and where the run ended, centred between the banner's painted ends; a long line shrinks to 14 pixels, then
        /// the result gives way down to 36 (as the character screen's banner).</summary>
        void LayBanner()
        {
            float bannerW = P(BannerW), height = P(BannerH), gap = P(BannerGap), middle = bannerW - 2f * P(BannerEnds);
            float titleW, whereW;
            for (int size = F(50f);; size--)
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
            bool placed = whereW > 0f && where.text.Length > 0;
            Shown(where, placed);
            float left = (bannerW - titleW - (placed ? gap + whereW : 0f)) * .5f;
            PlaceLeft(title, left, titleW, height);
            PlaceLeft(where, left + titleW + gap, whereW, height);
        }

        /// <summary>The button's word and its key, centred on it.</summary>
        void LayGo()
        {
            float labelW = V5StoryKit.WidthOf(goLabel, goLabel.text), keyW = goKey.Rect.sizeDelta.x, gap = P(KeyGap);
            float left = (P(GoW) - labelW - gap - keyW) * .5f;
            PlaceLeft(goLabel, left, labelW, P(GoH));
            var rect = goKey.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left + labelW + gap, 0f);
        }

        void FitPage()
        {
            float unit = canvas && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            float k = Mathf.Min(1f, Mathf.Min(Screen.width / unit / P(PageW), Screen.height / unit / P(PageH)));
            if (k > 0f && !Mathf.Approximately(page.localScale.x, k)) page.localScale = new Vector3(k, k, 1f);
        }

        // ───── Small helpers ─────

        /// <summary>Canvas units for so many mock-up pixels.</summary>
        static float P(float px) => px * Css;

        /// <summary>The font size of so many mock-up pixels.</summary>
        static int F(float px) => V5StoryKit.FontSize(px);

        static void SetText(Text label, string text)
        {
            text ??= "";
            if (label && label.text != text) label.text = text;
        }

        static void Shown(Component part, bool show)
        {
            if (part && part.gameObject.activeSelf != show) part.gameObject.SetActive(show);
        }

        /// <summary>A label from left in its row, as wide as width and as tall as the row, its middle on the row's middle.</summary>
        static void PlaceLeft(Text label, float left, float width, float height)
        {
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width + 2f, height);
        }

        /// <summary>The page's backing with the art on: the character screen's four navy corners, stretched soft.</summary>
        static Texture2D BackdropTexture
        {
            get
            {
                if (backdropTexture) return backdropTexture;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "V5Result backdrop", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
                };
                texture.SetPixels32(new Color32[] { new(10, 14, 32, 255), new(20, 12, 40, 255), new(18, 28, 56, 255), new(30, 20, 60, 255) });
                texture.Apply(false, true);
                return backdropTexture = texture;
            }
        }
    }
}
