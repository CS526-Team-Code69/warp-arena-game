using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The base page in the fifth interface pass (work/art-plans/style-1112/ui-e/base.html), built only when V5HudKit.On: the English
    /// Alpha keeps the page it was graded with. It stands on the base panel's full-screen backing, laid out as the 1920 x 1080 mock-up
    /// times the story layer's scale, and is drawn from the very lists the old page is built from (RefreshBasePanel hands them over), so
    /// every button does what it did. The old page stays built under it, hidden, its texts up to date; its picture moves into the new
    /// view.
    /// Top: the banner with where and when, the wallet, and a close button while Esc leaves (a visit from the afternoon or the night).
    /// Left: the four zones, each a hexagonal frame round the zone's picture and a plate with its name, its level as diamonds and one line
    /// of state; the chosen one warm with an orange pointer. A click, W or S (the arrows too) changes zone.
    /// Middle: the zone's view (the room's picture; for the launch bay the live view of the ship with its status over it, V5BaseShip.cs:
    /// the base's hull, the engine, the armour and the fuel along its foot, the ignition at its top right), its name and level, the lines
    /// that matter now and, for the three rooms with levels, the levels as numbered steps (done in teal, the next one in orange, the fuel's
    /// count in gold; a level not reached keeps its "? ? ?").
    /// Right: what can be done there, one plate each with its medal, its title, its price in the currencies' icons and its button; the
    /// one to do now is warm with an orange button.
    /// Bottom: the keys, and the ways out; the one that moves the story on is the orange go button at the right.
    /// Every piece follows the art switch: the mock-up's pictures with the art on, flat fills with the thin edge and the whitebox icons
    /// without.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The page in mock-up pixels (1920 x 1080). Nothing of the three columns reaches below B5Floor, so a subtitle over the page (its
        // foot at BaseSubtitleBottom) never covers them; the keys and the ways out stand along the foot, under the subtitle's strip.
        const float B5Css = V5HudKit.StoryCss, B5Width = 1920f, B5Height = 1080f, B5Margin = 36f, B5Top = 178f, B5Floor = 870f;
        // The banner (600 x 111, its painted ends B5BannerEnds wide), its title 50 pixels and the time 20, 22 apart. The wallet 38 from the
        // top and 70 tall (50-pixel medals, 27-pixel numbers), 172 from the right beside the close button (58 tall, 48 from the top, 30
        // from the right), 36 from the right without it.
        const float B5BannerTop = 26f, B5BannerW = 600f, B5BannerH = 111f, B5BannerEnds = 78f, B5BannerGap = 22f;
        const float B5WalletTop = 38f, B5WalletBeside = 172f, B5WalletH = 70f, B5WalletMedal = 50f, B5WalletNumber = 27f;
        const float B5CloseRight = 30f, B5CloseTop = 48f, B5CloseH = 58f;
        // Zones: rows 114 tall and 16 apart; the hexagon 132 x 114 with the picture inside it at 13 % / 14 %, 74 % x 72 %; 12 to the
        // plate (270 x 96, corners cut at 12); the pointer's tip 24 beyond the plate.
        const float B5ZoneH = 114f, B5ZoneGap = 16f, B5HexW = 132f, B5HexH = 114f, B5HexGap = 12f, B5ZonePlateW = 270f, B5ZonePlateH = 96f, B5ZoneCut = 12f;
        // Middle: 484 from the left, 808 wide; the view 440 tall (480 in the mock-up: shortened so the steps end above the floor), corners
        // cut at 18; the name 30 under it; the steps 62 tall and 250 wide.
        const float B5MidLeft = 484f, B5MidW = 808f, B5ViewH = 440f, B5ViewCut = 18f, B5NameGap = 30f, B5NameH = 48f, B5StepH = 62f, B5StepW = 250f;
        // Actions: 1322 from the left, 566 wide, under their caption; rows at most 112 tall and 14 apart (10 when there are more than
        // five), the medal 72 across, the button 52 tall and at least 108 wide. Rows of what is done already follow while there are fewer
        // than B5DoneRowsBelow rows.
        const float B5ActLeft = 1322f, B5ActW = 566f, B5RowsTop = 206f, B5RowMax = 112f, B5RowGap = 14f, B5RowGapTight = 10f, B5ActMedal = 72f;
        const float B5ActButtonH = 52f, B5ActButtonW = 108f;
        const int B5DoneRowsBelow = 5;
        // Foot: 26 over the screen's foot; the go button 72 tall (its hazard stripes 16 in, 48 wide), the other ways out 60 tall and 14
        // apart, each no wider than its cap; the keys' plate 52 tall, centred on the go button's middle.
        const float B5FootBottom = 26f, B5GoH = 72f, B5WayH = 60f, B5WayGap = 14f, B5GoMaxW = 560f, B5WayMaxW = 460f, B5HintsH = 52f;

        // Colours of the mock-up the kits do not name: the page's abyss, a price the stock is short of, the chosen zone's glow, the view's
        // chips' backing, the diamonds (off, their rim, and the gold of one reached) and the go button's stripes.
        static readonly Color B5Abyss = V5TitleKit.Rgb(7, 10, 20), B5Short = V5TitleKit.Rgb(255, 107, 122), B5ZoneGlow = V5TitleKit.Rgb(242, 106, 43, .5f),
            B5ChipBack = V5TitleKit.Rgb(6, 12, 24, .82f), B5DiamondOff = V5TitleKit.Rgb(160, 170, 190, .25f), B5DiamondRim = V5TitleKit.Rgb(200, 210, 225, .4f),
            B5DiamondTop = V5TitleKit.Rgb(255, 224, 154), B5DiamondBottom = V5TitleKit.Rgb(213, 148, 32), B5HazardInk = V5TitleKit.Rgb(27, 14, 6, .55f),
            B5UnbuiltTint = new(.5f, .52f, .58f, 1f), B5BrokenTint = new(1f, .6f, .55f, 1f), B5TrackFlat = new(.3f, .55f, .85f, .5f);

        // A step's hexagon by its state (done, next, a count, not reached): the top and the foot of its fill, its digit's ink and the
        // colour of the word under its title.
        enum B5StepLook { Done, Next, Count, Later }
        static readonly Color[] B5StepTop = { V5TitleKit.Rgb(111, 240, 222), V5TitleKit.Rgb(255, 176, 122), V5TitleKit.Rgb(255, 224, 154), V5TitleKit.Rgb(163, 172, 190) };
        static readonly Color[] B5StepFoot = { V5TitleKit.Rgb(18, 142, 127), V5TitleKit.Rgb(201, 71, 26), V5TitleKit.Rgb(201, 138, 28), V5TitleKit.Rgb(75, 84, 104) };
        static readonly Color[] B5StepInk = { V5TitleKit.Rgb(4, 33, 29), V5TitleKit.Rgb(42, 14, 2), V5TitleKit.Rgb(42, 24, 4), V5TitleKit.Rgb(20, 24, 31) };
        static readonly Color[] B5StepWord = { V5TitleKit.Teal, V5TitleKit.OrangeHi, V5TitleKit.Gold, V5TitleKit.Ink3 };

        /// <summary>What an action does, read from the action itself (the medal and the button's word follow it).</summary>
        enum B5Deed { Other, Build, Upgrade, RepairRoom, Durability, Shield, Hull, Overload, Engine, Armor, Fuel, Lamp, Ignite, Study }

        sealed class B5Zone
        {
            public V5Plate plate;
            public V5HudPlate warm;
            public Image glow, frame, window, house, pointer;
            public RawImage picture;
            public Text name, level, line;
            public readonly List<Image> diamonds = new();
        }

        sealed class B5Step
        {
            public Image fill;
            public Text number, title, state;
        }

        sealed class B5Row
        {
            public RectTransform root;
            public CanvasGroup fade;
            public V5HudPlate plate;
            public Image tab;
            public V5HudMedal medal;
            public Text title, note, done, verb;
            public readonly Image[] holes = new Image[3], coins = new Image[3];
            public readonly Text[] amounts = new Text[3];
            public V5Plate button;
            public Func<bool> act;
            public string fail;
        }

        sealed class B5Way
        {
            public V5Plate plate;
            public Text label;
            public RawImage hazard;
            public Action act;
            public bool go;
        }

        /// <summary>One row of the actions: the game's action with its words split into a title and a note, or a row of what is done.</summary>
        sealed class B5Item
        {
            public string title, note, done, fail;
            public CampaignCost? cost;
            public Func<bool> act;
            public bool enabled;
            public B5Deed deed;
        }

        bool b5Built;
        bool? b5Art;
        int b5TickFrame = -10;
        RectTransform b5Page, b5Wallet, b5Window, b5Steps;
        RawImage b5Backdrop, b5Track;
        Image b5Banner, b5Cutout, b5WindowShape;
        Text b5Title, b5When, b5CloseLabel, b5Caption, b5Name, b5Broken, b5Lines, b5ActsCaption, b5ActsEmpty, b5ZonesWord, b5CloseWord;
        V5Plate b5Close;
        V5HudKey b5CloseKey, b5KeyW, b5KeyS, b5KeyEsc;
        V5HudPlate b5ViewPlate, b5Hints;
        readonly B5Zone[] b5Zones = new B5Zone[4];
        readonly B5Step[] b5StepViews = new B5Step[3];
        readonly List<Image> b5NameDiamonds = new();
        readonly List<B5Row> b5Rows = new();
        readonly List<B5Way> b5Ways = new();
        readonly V5HudMedal[] b5WalletMedals = new V5HudMedal[4];
        readonly Text[] b5WalletNumbers = new Text[4];
        readonly int[] b5WalletShown = { -1, -1, -1, -1 };
        static Texture2D b5BackdropTexture, b5TrackTexture;

        /// <summary>Canvas units of so many mock-up pixels.</summary>
        static float Bp(float px) => px * B5Css;

        /// <summary>The title's plates (V5Plate) take their corner cut in pixels of the title's scale: this many of the page's.</summary>
        static float B5Cut(float px) => px * B5Css / V5TitleKit.Css;

        // ───── Building ─────

        void BuildBase5()
        {
            // The old page stays built and keeps its texts, hidden; its picture moves into the new view (BuildBase5Middle).
            basePage.gameObject.SetActive(false);
            b5Backdrop = V5TitleKit.Raw(baseScreen, "Backdrop", B5BackdropTexture, Color.white);
            V5TitleKit.Stretch(b5Backdrop.rectTransform);
            b5Backdrop.uvRect = new Rect(.25f, .25f, .5f, .5f);
            b5Page = V5TitleKit.Rect(baseScreen, "Page5");
            V5TitleKit.Centre(b5Page, new Vector2(Bp(B5Width), Bp(B5Height)));
            b5Page.gameObject.AddComponent<V5BaseTicker>().Tick = TickBase5;
            BuildBase5Head();
            BuildBase5Zones();
            BuildBase5Middle();
            BuildBase5Actions();
            BuildBase5Foot();
            b5Built = true;
        }

        void BuildBase5Head()
        {
            b5Banner = V5TitleKit.Picture(b5Page, "Banner", null, Color.white);
            b5Banner.gameObject.AddComponent<V5StoryBannerFrame>();
            V5TitleKit.Place(b5Banner.rectTransform, Bp(B5Margin), Bp(B5BannerTop), Bp(B5BannerW), Bp(B5BannerH));
            b5Title = V5StoryKit.Label(b5Banner.transform, "Title", 50f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(b5Title);
            b5When = V5StoryKit.Label(b5Banner.transform, "When", 20f, V5TitleKit.Gold, TextAnchor.MiddleLeft, true);

            b5Wallet = V5TitleKit.Rect(b5Page, "Wallet");
            b5Wallet.anchorMin = b5Wallet.anchorMax = b5Wallet.pivot = new Vector2(1f, 1f);
            V5TitleKit.Stretch(V5HudKit.Plate(b5Wallet, "Plate", V5HudKit.PlateLook.Plain, 12f, B5Css).Rect);
            for (int i = 0; i < 4; i++)
            {
                b5WalletMedals[i] = V5HudKit.Medal(b5Wallet, WalletMedalNames[i], Bp(B5WalletMedal));
                b5WalletNumbers[i] = V5StoryKit.Label(b5Wallet, "Amount" + i, B5WalletNumber, i < 3 ? WalletColour(i) : V5TitleKit.Gold, TextAnchor.MiddleCenter, true);
            }
            b5WalletMedals[3].SetGlow(V5StoryKit.Amber);

            b5Close = V5Plate.Make(b5Page, "Close", V5Plate.Look.Plate, B5Cut(10f), CloseBase5);
            b5Close.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            b5CloseLabel = V5StoryKit.Label(b5Close.transform, "Label", 19f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            b5CloseKey = V5HudKit.Key(b5Close.transform, "Key", "Esc", false, B5Css);
        }

        void BuildBase5Zones()
        {
            var hexMask = V5TitleKit.Piece("hex-mask");
            for (int i = 0; i < 4; i++)
            {
                var room = (CampaignRoom)i;
                var zone = b5Zones[i] = new B5Zone();
                float top = B5Top + i * (B5ZoneH + B5ZoneGap);
                zone.plate = V5Plate.Make(b5Page, "Zone" + room, V5Plate.Look.Plate, B5Cut(B5ZoneCut), () => PickBase5Zone(room), true);
                zone.plate.Place(Bp(B5Margin + B5HexW + B5HexGap), Bp(top + (B5ZoneH - B5ZonePlateH) * .5f), Bp(B5ZonePlateW), Bp(B5ZonePlateH));
                zone.plate.HoverPitch = 1.08f - i * .03f;
                zone.plate.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                var plate = zone.plate.transform;
                // The chosen zone's warm plate lies over the plate's own surface.
                zone.warm = V5HudKit.Plate(plate, "Warm", V5HudKit.PlateLook.Warm, B5ZoneCut, B5Css);
                V5TitleKit.Stretch(zone.warm.Rect);
                // The hexagon stands left of the plate and belongs to it: a click on the picture picks the zone too.
                var hex = V5TitleKit.Rect(plate, "Hex");
                hex.anchorMin = hex.anchorMax = hex.pivot = new Vector2(0f, .5f);
                hex.anchoredPosition = new Vector2(-Bp(B5HexW + B5HexGap), 0f);
                hex.sizeDelta = new Vector2(Bp(B5HexW), Bp(B5HexH));
                zone.glow = V5TitleKit.Picture(hex, "Glow", V5HudKit.Glow, B5ZoneGlow);
                V5TitleKit.Centre(zone.glow.rectTransform, new Vector2(Bp(B5HexW), Bp(B5HexH)) * 1.4f);
                zone.frame = V5TitleKit.Picture(hex, "Frame", null, Color.white);
                V5TitleKit.Stretch(zone.frame.rectTransform);
                zone.frame.raycastTarget = true;
                zone.window = V5TitleKit.Picture(hex, "Window", hexMask, Color.white);
                B5Fraction(zone.window.rectTransform, .13f, .14f, .74f, .72f);
                zone.window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                zone.picture = V5TitleKit.Raw(zone.window.transform, "Picture", null, Color.white);
                V5TitleKit.Stretch(zone.picture.rectTransform);
                zone.house = V5TitleKit.Picture(zone.window.transform, "House", null, Color.white);
                V5TitleKit.Centre(zone.house.rectTransform, Vector2.one * Bp(58f));
                zone.house.preserveAspect = true;
                zone.name = V5StoryKit.Label(plate, "Name", 24f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(zone.name.rectTransform, Bp(22f), Bp(13f), Bp(B5ZonePlateW - 40f), Bp(30f));
                zone.level = V5StoryKit.Label(plate, "Level", 15f, V5TitleKit.Ink3, TextAnchor.MiddleLeft, true);
                zone.line = V5StoryKit.Label(plate, "Line", 14f, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
                V5TitleKit.Place(zone.line.rectTransform, Bp(22f), Bp(69f), Bp(B5ZonePlateW - 40f), Bp(20f));
                // The pointer: a triangle 18 wide and 26 tall pointing right, its tip 24 beyond the plate.
                zone.pointer = V5HudKit.Arrow(plate, "Pointer", new Vector2(Bp(26f), Bp(18f)), V5TitleKit.Orange, -90f);
                var pointer = zone.pointer.rectTransform;
                pointer.anchorMin = pointer.anchorMax = new Vector2(1f, .5f);
                pointer.pivot = new Vector2(.5f, .5f);
                pointer.anchoredPosition = new Vector2(Bp(24f - 9f), 0f);
            }
        }

        void BuildBase5Middle()
        {
            var mid = V5TitleKit.Rect(b5Page, "Middle");
            V5TitleKit.Place(mid, Bp(B5MidLeft), Bp(B5Top), Bp(B5MidW), Bp(B5Floor - B5Top));
            b5ViewPlate = V5HudKit.Plate(mid, "View", V5HudKit.PlateLook.Teal, B5ViewCut, B5Css);
            V5TitleKit.Place(b5ViewPlate.Rect, 0f, 0f, Bp(B5MidW), Bp(B5ViewH));
            var window = V5TitleKit.Picture(b5ViewPlate.transform, "Window", V5TitleKit.Chamfer, Color.white, V5HudKit.ChamferDensity(B5ViewCut - 1f, B5Css));
            V5TitleKit.Stretch(window.rectTransform, Bp(3f));
            window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            b5Window = window.rectTransform;
            b5WindowShape = window;
            // The old page's picture (the room's picture, the ship's live view, the whitebox blocks) moves into the view: RefreshBasePanel
            // keeps setting it, RefreshBase5View fits it to the view.
            roomPicture.rectTransform.SetParent(b5Window, false);
            V5TitleKit.Stretch(roomPicture.rectTransform);
            roomPicture.transform.SetAsFirstSibling();
            // The crashed ship cut out, for the launch bay when there is no live view of it with the art on.
            b5Cutout = V5TitleKit.Picture(b5Window, "Ship", V5TitleKit.Piece("ship-cutout"), Color.white);
            b5Cutout.preserveAspect = true;
            var ship = b5Cutout.rectTransform;
            ship.anchorMin = ship.anchorMax = ship.pivot = new Vector2(.5f, .59f);
            ship.anchoredPosition = Vector2.zero;
            var shipSize = V5TitleKit.PieceSize("ship-cutout");
            ship.sizeDelta = new Vector2(Bp(520f), Bp(520f * shipSize.y / Mathf.Max(1f, shipSize.x)));
            b5Caption = V5StoryKit.Label(b5Window, "Caption", 14f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);
            V5TitleKit.Place(b5Caption.rectTransform, Bp(25f), Bp(16f), Bp(B5MidW - 60f), Bp(20f));
            // The ship's status over the view, shown on the launch bay's page (V5BaseShip.cs).
            BuildBase5Ship(b5Window);

            b5Name = V5StoryKit.Label(mid, "Name", 42f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(b5Name);
            b5Broken = V5StoryKit.Label(mid, "Broken", 18f, V5TitleKit.OrangeHi, TextAnchor.MiddleLeft, true);
            b5Lines = V5StoryKit.Label(mid, "Lines", 18f, V5TitleKit.Ink2, TextAnchor.UpperLeft);
            b5Lines.horizontalOverflow = HorizontalWrapMode.Wrap;
            b5Lines.lineSpacing = 1.3f;
            b5Steps = V5TitleKit.Rect(mid, "Steps");
            // The line that runs through the three hexagons, behind them.
            b5Track = V5TitleKit.Raw(b5Steps, "Track", null, Color.white);
            V5TitleKit.Place(b5Track.rectTransform, Bp(40f), Bp(B5StepH * .5f - 2f), Bp(B5MidW - 80f), Bp(4f));
            var hexMask = V5TitleKit.Piece("hex-mask");
            for (int i = 0; i < 3; i++)
            {
                var step = b5StepViews[i] = new B5Step();
                var root = V5TitleKit.Rect(b5Steps, "Step" + (i + 1));
                V5TitleKit.Place(root, Bp(i * (B5MidW - B5StepW) * .5f), 0f, Bp(B5StepW), Bp(B5StepH));
                var hex = V5TitleKit.Picture(root, "Hex", hexMask, Color.white);
                V5TitleKit.Place(hex.rectTransform, 0f, 0f, Bp(70f), Bp(B5StepH));
                hex.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                step.fill = V5TitleKit.Picture(hex.transform, "Fill", null, Color.white);
                V5TitleKit.Stretch(step.fill.rectTransform);
                step.number = V5StoryKit.Label(root, "Number", 26f, Color.black, TextAnchor.MiddleCenter, true);
                V5TitleKit.Place(step.number.rectTransform, 0f, 0f, Bp(70f), Bp(B5StepH));
                step.number.text = (i + 1).ToString();
                step.title = V5StoryKit.Label(root, "Title", 17f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(step.title.rectTransform, Bp(82f), Bp(8f), Bp(B5StepW - 86f), Bp(24f));
                step.state = V5StoryKit.Label(root, "State", 15f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(step.state.rectTransform, Bp(82f), Bp(35f), Bp(B5StepW - 86f), Bp(20f));
            }
        }

        void BuildBase5Actions()
        {
            b5ActsCaption = V5StoryKit.Label(b5Page, "ActionsCaption", 14f, V5TitleKit.Ink3, TextAnchor.MiddleLeft, true);
            V5TitleKit.Place(b5ActsCaption.rectTransform, Bp(B5ActLeft + 4f), Bp(B5Top - 2f), Bp(B5ActW - 4f), Bp(20f));
            b5ActsEmpty = V5StoryKit.Label(b5Page, "ActionsEmpty", 16f, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
            V5TitleKit.Place(b5ActsEmpty.rectTransform, Bp(B5ActLeft + 4f), Bp(B5RowsTop), Bp(B5ActW - 4f), Bp(24f));
        }

        void BuildBase5Foot()
        {
            b5Hints = V5HudKit.Plate(b5Page, "Keys", V5HudKit.PlateLook.Plain, 10f, B5Css);
            b5KeyW = V5HudKit.Key(b5Hints.transform, "W", "W", false, B5Css);
            b5KeyS = V5HudKit.Key(b5Hints.transform, "S", "S", false, B5Css);
            b5ZonesWord = V5StoryKit.Label(b5Hints.transform, "Zones", 16f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            b5KeyEsc = V5HudKit.Key(b5Hints.transform, "Esc", "Esc", false, B5Css);
            b5CloseWord = V5StoryKit.Label(b5Hints.transform, "Close", 16f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
        }

        /// <summary>The action row at index, made when first needed (rows are kept and reused, so a plate under the pointer stays the same
        /// plate when the page is refreshed).</summary>
        B5Row B5RowAt(int index)
        {
            while (b5Rows.Count <= index)
            {
                var row = new B5Row();
                int n = b5Rows.Count;
                row.root = V5TitleKit.Rect(b5Page, "Action" + (n + 1));
                row.fade = row.root.gameObject.AddComponent<CanvasGroup>();
                row.plate = V5HudKit.Plate(row.root, "Plate", V5HudKit.PlateLook.Plain, 14f, B5Css);
                V5TitleKit.Stretch(row.plate.Rect);
                row.tab = V5TitleKit.Picture(row.root, "Tab", null, Color.white);
                V5TitleKit.Place(row.tab.rectTransform, Bp(26f), -Bp(5f), Bp(72f), Bp(10f));
                row.medal = V5HudKit.Medal(row.root, "Medal", Bp(B5ActMedal));
                row.title = V5StoryKit.Label(row.root, "Title", 22f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                var disc = V5TitleKit.Piece("disc");
                for (int k = 0; k < 3; k++)
                {
                    row.holes[k] = V5TitleKit.Picture(row.root, "Coin" + k, disc, Color.white);
                    row.holes[k].gameObject.AddComponent<Mask>().showMaskGraphic = false;
                    row.coins[k] = V5TitleKit.Picture(row.holes[k].transform, "Icon", null, Color.white);
                    V5TitleKit.Stretch(row.coins[k].rectTransform);
                    row.coins[k].preserveAspect = true;
                    row.amounts[k] = V5StoryKit.Label(row.root, "Amount" + k, 22f, V5TitleKit.Steel, TextAnchor.MiddleLeft, true);
                }
                row.note = V5StoryKit.Label(row.root, "Note", 14f, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
                row.done = V5StoryKit.Label(row.root, "Done", 18f, V5TitleKit.Teal, TextAnchor.MiddleRight, true);
                row.button = V5Plate.Make(row.root, "Button", V5Plate.Look.Plate, B5Cut(10f), () => ClickBase5Row(row), true);
                row.button.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                row.button.HoverPitch = 1.06f - n * .02f;
                row.verb = V5StoryKit.Label(row.button.transform, "Verb", 19f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(row.verb.rectTransform);
                b5Rows.Add(row);
            }
            return b5Rows[index];
        }

        /// <summary>The way out at index (counted from the right), made when first needed and reused.</summary>
        B5Way B5WayAt(int index)
        {
            while (b5Ways.Count <= index)
            {
                var way = new B5Way();
                way.plate = V5Plate.Make(b5Page, "Way" + (b5Ways.Count + 1), V5Plate.Look.Plate, B5Cut(10f), () => ClickBase5Way(way), true);
                way.plate.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                way.plate.HoverPitch = 1f - b5Ways.Count * .03f;
                way.hazard = V5TitleKit.Raw(way.plate.transform, "Hazard", V5TitleKit.Stripes, B5HazardInk);
                way.label = V5StoryKit.Label(way.plate.transform, "Label", 26f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                b5Ways.Add(way);
            }
            return b5Ways[index];
        }

        // ───── Acting ─────

        void PickBase5Zone(CampaignRoom room)
        {
            B5Deselect();
            if (!BaseOpen || room == tab) return;
            tab = room;
            V5UiSound.Play(V5UiSound.Ui.Tab);
            RefreshBasePanel(true);
        }

        /// <summary>An action's button: what the old page's button did (Act: the action, its refusal sound and reason, the refresh).</summary>
        void ClickBase5Row(B5Row row)
        {
            B5Deselect();
            if (row.act == null || !BaseOpen) return;
            V5UiSound.Play(V5UiSound.Ui.Press, 1f, .7f);
            Act(row.act, row.fail);
        }

        void ClickBase5Way(B5Way way)
        {
            B5Deselect();
            if (way.act == null || !BaseOpen) return;
            V5UiSound.Play(way.go ? V5UiSound.Ui.Start : V5UiSound.Ui.Press);
            way.act();
        }

        /// <summary>The close button does what Esc does on the page (V2CampaignUi.Update): back out to where the visit came from.</summary>
        void CloseBase5()
        {
            B5Deselect();
            if (!B5CanClose(campaign)) return;
            V5UiSound.Play(V5UiSound.Ui.Back);
            campaign.LeaveBase();
        }

        /// <summary>Esc leaves the page now (a visit from the afternoon or the night): the close button and its key hint show only then.</summary>
        static bool B5CanClose(V2Campaign c) => c && c.Stage == CampaignStage.Base && !c.BaseAtDawn && c.NightTimeLeft(out _);

        /// <summary>A plate let go of the keyboard's selection once clicked: Space and Enter, which the UI would read as a second click,
        /// do nothing on this page.</summary>
        static void B5Deselect()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        // ───── The frame ─────

        void TickBase5()
        {
            if (!b5Built) return;
            b5TickFrame = Time.frameCount;
            // The page keeps the mock-up's shape and shrinks to fit a screen narrower or shorter than 16:9.
            var size = root.rect.size;
            float scale = Mathf.Min(1f, Mathf.Min(size.x / Bp(B5Width), size.y / Bp(B5Height)));
            if (scale > 0f && !Mathf.Approximately(b5Page.localScale.x, scale)) b5Page.localScale = new Vector3(scale, scale, 1f);
            if (b5Art != Art.Enabled) RefreshBasePanel(true);
            if (!BaseOpen || StudyOpen || ConfirmOpen || V2Tutorial.HoldsInput || ArenaSettingsPanel.HoldsInput || ArenaCodex.HoldsInput) return;
            if (gm && gm.RunPhase != ArenaUiState.Base) return;
            var keys = Keyboard.current;
            if (keys == null) return;
            int step = 0;
            if (keys.wKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame) step--;
            if (keys.sKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame) step++;
            int next = Mathf.Clamp((int)tab + step, 0, 3);
            if (step != 0 && next != (int)tab) PickBase5Zone((CampaignRoom)next);
        }

        // ───── Refreshing (RefreshBasePanel, whenever what the page shows changed) ─────

        void RefreshBase5(List<(string label, CampaignCost? cost, Func<bool> act, string fail, bool enabled)> actions, List<(string label, Action act)> leaves)
        {
            if (!b5Built) return;
            var c = campaign;
            bool art = Art.Enabled;
            b5Art = art;
            bool canClose = B5CanClose(c);
            if (baseScreen.TryGetComponent<Image>(out var back)) back.color = art ? B5Abyss : PageBack;
            b5Backdrop.enabled = art;
            RefreshBase5Head(c, art, canClose);
            RefreshBase5Zones(c, art);
            RefreshBase5View(c, art);
            RefreshBase5Words(c, art);
            RefreshBase5Actions(c, art, actions);
            RefreshBase5Foot(art, leaves, canClose);
        }

        void RefreshBase5Head(V2Campaign c, bool art, bool canClose)
        {
            // The banner: "基地" and when, gold by day and moonlight blue on a visit at night, centred between its painted ends.
            bool night = !c.BaseAfterTutorial && !c.BaseAtDawn && !c.BaseFromDusk;
            StoryText(b5Title, ArenaText.IsChinese ? "基 地" : V5TitleKit.Spaced("BASE"));
            StoryText(b5When, c.BaseAfterTutorial ? ArenaText.Pair("第 0 天", "DAY 0")
                : c.BaseAtDawn ? ArenaText.Pair($"第 {c.Day} 晚过后 · 天亮", $"DAWN AFTER NIGHT {c.Day}")
                : c.BaseFromDusk ? ArenaText.Pair($"第 {c.Day} 天 · 下午", $"DAY {c.Day} · AFTERNOON")
                : ArenaText.Pair($"第 {c.Day} 天 · 夜里", $"NIGHT {c.Day}"));
            b5When.color = night ? V5TitleKit.Moon : V5TitleKit.Gold;
            float banner = Bp(B5BannerW), height = Bp(B5BannerH), gap = Bp(B5BannerGap);
            b5Title.fontSize = V5StoryKit.FontSize(50f);
            float titleW = V5StoryKit.WidthOf(b5Title, b5Title.text);
            float room = Mathf.Max(Bp(40f), banner - Bp(B5BannerEnds) * 2f - titleW - gap);
            B5PlaceLeft(b5When, 0f, room, height);
            V5TitleKit.FitWidth(b5When, V5StoryKit.FontSize(20f), V5StoryKit.FontSize(14f));
            float whenW = Mathf.Min(room, V5StoryKit.WidthOf(b5When, b5When.text));
            float left = (banner - titleW - gap - whenW) * .5f;
            B5PlaceLeft(b5Title, left, titleW, height);
            B5PlaceLeft(b5When, left + titleW + gap, room, height);

            // The wallet: the three currencies and, from the first night on, the lamps; a number that changes while the page shows swells once.
            var set = c.art;
            b5WalletMedals[0].SetIcons(set ? set.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            b5WalletMedals[1].SetIcons(set ? set.alloy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Alloy));
            b5WalletMedals[2].SetIcons(set ? set.dark : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Dark));
            b5WalletMedals[3].SetIcons(set ? set.lamp : null, V5StoryKit.LampIcon);
            B5DarkIcon(b5WalletMedals[2].transform.Find("Hole/Icon"), art);
            int[] values = { c.Energy, c.Alloy, c.Dark, c.LampStock };
            bool lamps = c.LampStock > 0 || c.Day > 0;
            bool settled = Time.frameCount - b5TickFrame <= 1;
            float walletH = Bp(B5WalletH), medal = Bp(B5WalletMedal), x = Bp(12f);
            for (int i = 0; i < 4; i++)
            {
                var number = b5WalletNumbers[i];
                bool shown = i < 3 || lamps;
                SetShown(b5WalletMedals[i], shown);
                SetShown(number, shown);
                if (!shown) continue;
                string text = i < 3 ? values[i].ToString() : "×" + values[i];
                if (settled && b5WalletShown[i] >= 0 && b5WalletShown[i] != values[i]) PulseOnce(number.transform);
                b5WalletShown[i] = values[i];
                StoryText(number, text);
                if (i > 0) x += Bp(6f);
                PlaceCentre(b5WalletMedals[i].Rect, new Vector2(0f, .5f), new Vector2(x + medal * .5f, 0f), Vector2.one * medal);
                x += medal + Bp(8f);
                // Never narrower than three digits (the lamps: one), so the row does not shift as numbers grow and shrink.
                float w = Mathf.Max(V5StoryKit.WidthOf(number, text), DigitsWidth(number, i < 3 ? "000" : "×0"));
                PlaceSpan(number, x, w, walletH);
                x += w + Bp(10f);
            }
            b5Wallet.sizeDelta = new Vector2(x + Bp(16f), walletH);
            b5Wallet.anchoredPosition = new Vector2(-Bp(canClose ? B5WalletBeside : B5Margin), -Bp(B5WalletTop));

            // The close button with its Esc key, only while Esc leaves.
            SetShown(b5Close, canClose);
            if (!canClose) return;
            StoryText(b5CloseLabel, ArenaText.Pair("关闭", "Close"));
            float labelW = V5StoryKit.WidthOf(b5CloseLabel, b5CloseLabel.text), keyW = b5CloseKey.Rect.sizeDelta.x;
            float closeW = Bp(22f) + labelW + Bp(12f) + keyW + Bp(22f), closeH = Bp(B5CloseH);
            b5Close.Place(Bp(B5Width - B5CloseRight) - closeW, Bp(B5CloseTop), closeW, closeH);
            B5PlaceLeft(b5CloseLabel, Bp(22f), labelW, closeH);
            B5PlaceKey(b5CloseKey, Bp(22f) + labelW + Bp(12f));
        }

        void RefreshBase5Zones(V2Campaign c, bool art)
        {
            var frame = art ? V5TitleKit.Piece("hex-frame") : null;
            var hexMask = V5TitleKit.Piece("hex-mask");
            float window = .74f * B5HexW / (.72f * B5HexH);
            for (int i = 0; i < 4; i++)
            {
                var room = (CampaignRoom)i;
                var zone = b5Zones[i];
                var state = c.Room(room);
                bool chosen = room == tab, unbuilt = room != CampaignRoom.Control && state.level <= 0;
                zone.plate.Chosen = chosen;
                SetShown(zone.warm, chosen);
                SetShown(zone.pointer, chosen);
                zone.glow.enabled = art && chosen;
                // The hexagon: the machined frame with the art on; a flat hexagon with the thin edge (orange while chosen) in whitebox.
                bool framed = frame;
                V5TitleKit.Dress(zone.frame, framed ? frame : hexMask, framed ? Color.white : WhiteboxSkin.PlateFill);
                B5Edge(zone.frame, !framed, chosen ? V5TitleKit.Orange : WhiteboxSkin.Edge);
                // Inside it the zone's picture with the art on (darker before it is built, reddened while broken), the room's house in
                // whitebox or where there is no picture.
                var picture = B5ZoneTexture(c, room, out var uv);
                bool pictured = art && picture;
                zone.picture.enabled = pictured;
                zone.house.enabled = !pictured;
                if (pictured)
                {
                    zone.picture.texture = picture;
                    float shape = picture.width * uv.width / Mathf.Max(1f, picture.height * uv.height);
                    zone.picture.uvRect = B5Cover(uv, shape, window);
                    zone.picture.color = state.damaged ? B5BrokenTint : unbuilt ? B5UnbuiltTint : Color.white;
                }
                else
                {
                    zone.house.sprite = WhiteboxIcons.Room(room);
                    zone.house.color = state.damaged ? RoomBroken : unbuilt ? RoomUnbuilt : Color.white;
                }
                StoryText(zone.name, V2Campaign.RoomName(room));
                zone.name.color = chosen ? V5TitleKit.Chosen : V5TitleKit.Ink;
                V5TitleKit.FitWidth(zone.name, V5StoryKit.FontSize(24f), V5StoryKit.FontSize(17f));
                B5LevelOf(c, room, out int count, out int filled);
                float after = B5Diamonds(zone.diamonds, zone.plate.transform, count, filled, Bp(13f), Bp(5f), Bp(22f), Bp(56.5f), art);
                StoryText(zone.level, "LV " + filled);
                V5TitleKit.Place(zone.level.rectTransform, after + Bp(11f), Bp(47f), Bp(70f), Bp(20f));
                StoryText(zone.line, B5ZoneLine(c, room, out var colour));
                zone.line.color = colour;
                V5TitleKit.FitWidth(zone.line, V5StoryKit.FontSize(14f), V5StoryKit.FontSize(11f));
            }
        }

        void RefreshBase5View(V2Campaign c, bool art)
        {
            bool ship = tab == CampaignRoom.Ship;
            b5ViewPlate.SetLook(ship ? V5HudKit.PlateLook.Teal : V5HudKit.PlateLook.Plain);
            // The view's inside follows the plate's shape: cut corners with the art on, a plain rectangle in whitebox.
            V5TitleKit.Dress(b5WindowShape, art ? V5TitleKit.Chamfer : null, Color.white, V5HudKit.ChamferDensity(B5ViewCut - 1f, B5Css));
            // The launch bay shows the live view of the ship (it shows each repair as it is bought); with the art on and no live view, the
            // crashed ship cut out on the teal plate.
            bool cutout = ship && art && !c.ShipViewTexture && b5Cutout.sprite;
            SetShown(b5Cutout, cutout);
            roomPicture.enabled = !cutout;
            var picture = roomPicture.texture;
            if (picture && picture.height > 0)
            {
                var size = b5Window.rect.size;
                roomPicture.uvRect = B5Cover(new Rect(0f, 0f, 1f, 1f), picture.width / (float)picture.height, size.x / Mathf.Max(1f, size.y));
            }
            SetShown(b5Caption, ship);
            if (ship) StoryText(b5Caption, ArenaText.IsChinese ? V5TitleKit.Spaced("SHIP STATUS") + "   ·   飞船状态" : V5TitleKit.Spaced("SHIP STATUS"));
            // Over the view the ship's status: the base's hull, the engine, the armour and the fuel along its foot, the ignition at its top
            // right (V5BaseShip.cs).
            RefreshBase5Ship(c, art, ship);
        }

        /// <summary>Under the view: the zone's name in the cream paint with its level as diamonds ("损坏" after them while it is broken), the
        /// lines that matter now, and the steps.</summary>
        void RefreshBase5Words(V2Campaign c, bool art)
        {
            var state = c.Room(tab);
            float top = B5ViewH + B5NameGap;
            StoryText(b5Name, V2Campaign.RoomName(tab));
            V5TitleKit.Place(b5Name.rectTransform, 0f, Bp(top), Bp(B5MidW * .7f), Bp(B5NameH));
            V5TitleKit.FitWidth(b5Name, V5StoryKit.FontSize(42f), V5StoryKit.FontSize(28f));
            float nameW = Mathf.Min(Bp(B5MidW * .7f), V5StoryKit.WidthOf(b5Name, b5Name.text));
            B5LevelOf(c, tab, out int count, out int filled);
            float after = B5Diamonds(b5NameDiamonds, b5Name.transform.parent, count, filled, Bp(15f), Bp(6f), nameW + Bp(18f), Bp(top + B5NameH * .5f), art);
            SetShown(b5Broken, state.damaged);
            if (state.damaged)
            {
                StoryText(b5Broken, ArenaText.Pair("损坏", "BROKEN"));
                V5TitleKit.Place(b5Broken.rectTransform, after + Bp(16f), Bp(top + (B5NameH - 24f) * .5f), Bp(160f), Bp(24f));
            }
            top += B5NameH + 10f;
            string lines = B5Lines(c, tab);
            bool any = lines.Length > 0;
            SetShown(b5Lines, any);
            if (any)
            {
                V5TitleKit.Place(b5Lines.rectTransform, 0f, Bp(top), Bp(B5MidW), Bp(30f));
                b5Lines.text = lines;
                float height = Mathf.Ceil(b5Lines.preferredHeight);
                b5Lines.rectTransform.sizeDelta = new Vector2(Bp(B5MidW), height + 2f);
                top += height / B5Css + 18f;
            }
            bool steps = tab != CampaignRoom.Control;
            SetShown(b5Steps, steps);
            if (!steps) return;
            V5TitleKit.Place(b5Steps, 0f, Bp(top), Bp(B5MidW), Bp(B5StepH));
            RefreshBase5Steps(c, art);
        }

        void RefreshBase5Steps(V2Campaign c, bool art)
        {
            var state = c.Room(tab);
            int lv = state.level;
            bool ship = tab == CampaignRoom.Ship, nextGiven = false;
            b5Track.texture = art ? B5TrackTexture : null;
            b5Track.color = art ? new Color(1f, 1f, 1f, .6f) : B5TrackFlat;
            b5Track.uvRect = new Rect(1f / 6f, 0f, 2f / 3f, 1f);
            for (int i = 0; i < 3; i++)
            {
                int level = i + 1;
                bool reached = lv >= level;
                bool done = ship ? (level == 1 ? c.EngineFixed : level == 2 ? c.ArmorFixed : c.FuelFull) : reached;
                B5StepLook look;
                string line;
                if (done)
                {
                    look = B5StepLook.Done;
                    line = !ship ? ArenaText.Pair("已解锁", "Unlocked") : level == 3 ? ArenaText.Pair("已加满", "Full") : ArenaText.Pair("已修好", "Repaired");
                }
                else if (reached && state.damaged)
                {
                    look = B5StepLook.Later;
                    line = ArenaText.Pair("先修好这个区", "Repair the room first");
                }
                else if (reached)
                {
                    // Only the launch bay's parts are reached and not done: a part to repair now, or the fuel's count.
                    look = level == 3 ? B5StepLook.Count : B5StepLook.Next;
                    line = level == 3 ? $"{c.Fuel} / {c.FuelUnits}" : ArenaText.Pair("可以修了", "Ready to repair");
                    nextGiven = true;
                }
                else if (level == lv + 1 && !nextGiven)
                {
                    look = B5StepLook.Next;
                    line = level == 1 ? ArenaText.Pair("建造后解锁", "Unlocks once built") : ArenaText.Pair($"升到 {level} 级解锁", $"Unlocks at level {level}");
                    nextGiven = true;
                }
                else
                {
                    look = B5StepLook.Later;
                    line = ArenaText.Pair($"{level} 级解锁", $"Unlocks at level {level}");
                }
                var step = b5StepViews[i];
                int k = (int)look;
                V5TitleKit.Dress(step.fill, art ? V5StoryKit.Fill(B5StepTop[k], B5StepFoot[k]) : WhiteboxSkin.Square,
                    art ? Color.white : Color.Lerp(B5StepTop[k], B5StepFoot[k], .5f));
                step.number.color = B5StepInk[k];
                // A level not reached keeps its "? ? ?": what it brings is seen only once it is unlocked.
                StoryText(step.title, reached || done ? B5StepWords(tab, level) : ArenaText.Pair("？？？", "? ? ?"));
                V5TitleKit.FitWidth(step.title, V5StoryKit.FontSize(17f), V5StoryKit.FontSize(12f));
                StoryText(step.state, line);
                step.state.color = B5StepWord[k];
                V5TitleKit.FitWidth(step.state, V5StoryKit.FontSize(15f), V5StoryKit.FontSize(11f));
            }
        }

        void RefreshBase5Actions(V2Campaign c, bool art, List<(string label, CampaignCost? cost, Func<bool> act, string fail, bool enabled)> actions)
        {
            StoryText(b5ActsCaption, ArenaText.IsChinese ? V5TitleKit.Spaced("ACTIONS") + "   ·   可做的事" : V5TitleKit.Spaced("ACTIONS"));
            var items = new List<B5Item>();
            bool repairFirst = c.Room(tab).damaged;
            for (int i = 0; i < actions.Count; i++)
            {
                var (label, cost, act, fail, enabled) = actions[i];
                B5Split(label, out string head, out string note);
                items.Add(new B5Item { title = head, note = note, cost = cost, act = act, fail = fail, enabled = enabled, deed = B5DeedOf(c, act, label, i == 0 && repairFirst) });
            }
            int go = B5GoItem(c, items);
            // The launch bay's repairs and its fuel leave the list once done: while there is room they stay at its foot as done.
            if (tab == CampaignRoom.Ship)
            {
                if (c.EngineFixed && items.Count < B5DoneRowsBelow)
                    items.Add(new B5Item { title = ArenaText.Pair("修复引擎", "Repair the engine"), done = ArenaText.Pair("✓ 已修好", "✓ Repaired"), deed = B5Deed.Engine });
                if (c.ArmorFixed && items.Count < B5DoneRowsBelow)
                    items.Add(new B5Item { title = ArenaText.Pair("修复装甲", "Repair the armor"), done = ArenaText.Pair("✓ 已修好", "✓ Repaired"), deed = B5Deed.Armor });
                if (c.FuelFull && items.Count < B5DoneRowsBelow)
                    items.Add(new B5Item { title = ArenaText.Pair("合成反物质燃料", "Synthesize antimatter fuel"), done = ArenaText.Pair("✓ 已加满", "✓ Full"), deed = B5Deed.Fuel });
            }
            int n = items.Count;
            SetShown(b5ActsEmpty, n == 0);
            if (n == 0) StoryText(b5ActsEmpty, ArenaText.Pair("这里现在没有可做的事", "Nothing to do here right now"));
            float gap = n > 5 ? B5RowGapTight : B5RowGap;
            float h = n > 0 ? Mathf.Min(B5RowMax, (B5Floor - B5RowsTop - (n - 1) * gap) / n) : 0f;
            for (int i = 0; i < n; i++) B5LayRow(c, art, B5RowAt(i), items[i], i == go, B5RowsTop + i * (h + gap), h);
            for (int i = n; i < b5Rows.Count; i++) SetShown(b5Rows[i].root, false);
        }

        /// <summary>One action's plate: warm with the hazard tab and an orange button when it is the one to do now, teal with "✓" when done,
        /// faded with its button off while it cannot be done; its medal, its title, then its price (each currency's icon and amount, the
        /// amount red while the stock is short of it) and its note.</summary>
        void B5LayRow(V2Campaign c, bool art, B5Row row, B5Item item, bool go, float top, float h)
        {
            SetShown(row.root, true);
            V5TitleKit.Place(row.root, Bp(B5ActLeft), Bp(top), Bp(B5ActW), Bp(h));
            bool done = item.done != null;
            row.plate.SetLook(done ? V5HudKit.PlateLook.Teal : go ? V5HudKit.PlateLook.Warm : V5HudKit.PlateLook.Plain);
            row.fade.alpha = done || item.enabled ? 1f : .55f;
            SetShown(row.tab, go);
            if (go) V5TitleKit.Dress(row.tab, art ? V5TitleKit.Piece("hazard-tab") : null, art ? Color.white : V5TitleKit.Orange);

            float m = Mathf.Min(B5ActMedal, h - 30f);
            B5MedalIcons(c, item.deed, out var artIcon, out var flatIcon);
            row.medal.SetSize(Bp(m));
            row.medal.SetIcons(artIcon, flatIcon);
            row.medal.SetRing(done ? V5TitleKit.Teal : go ? V5TitleKit.Orange : Color.clear);
            row.medal.SetGlow(item.deed == B5Deed.Lamp ? V5StoryKit.Amber : Color.clear);
            PlaceCentre(row.medal.Rect, new Vector2(0f, 1f), new Vector2(Bp(18f + m * .5f), -Bp(h * .5f)), Vector2.one * Bp(m));
            float textLeft = 18f + m + 18f, right;

            SetShown(row.button, !done);
            SetShown(row.done, done);
            if (done)
            {
                row.act = null;
                StoryText(row.done, item.done);
                float w = V5StoryKit.WidthOf(row.done, item.done) / B5Css;
                V5TitleKit.Place(row.done.rectTransform, Bp(B5ActW - 20f - w - 4f), Bp((h - 26f) * .5f), Bp(w + 4f), Bp(26f));
                right = 20f + w + 18f;
            }
            else
            {
                row.act = item.act;
                row.fail = item.fail;
                row.button.SetLook(go ? V5Plate.Look.Go : V5Plate.Look.Plate);
                row.button.Button.interactable = item.enabled;
                row.button.Dim = !item.enabled;
                string verb = B5Verb(c, item.deed);
                StoryText(row.verb, verb);
                row.verb.color = go ? (art ? V5TitleKit.Dark : Color.white) : V5TitleKit.Ink;
                float bw = Mathf.Max(B5ActButtonW, V5StoryKit.WidthOf(row.verb, verb) / B5Css + 44f), bh = Mathf.Min(B5ActButtonH, h - 28f);
                row.button.Place(Bp(B5ActW - 20f - bw), Bp((h - bh) * .5f), Bp(bw), Bp(bh));
                right = 20f + bw + 18f;
            }
            float textW = B5ActW - textLeft - right;

            var cost = item.cost ?? default;
            bool priced = item.cost.HasValue && !cost.IsFree, noted = !string.IsNullOrEmpty(item.note);
            float titleTop = priced || noted ? (h - 62f) * .5f : (h - 26f) * .5f;
            StoryText(row.title, item.title);
            V5TitleKit.Place(row.title.rectTransform, Bp(textLeft), Bp(titleTop), Bp(textW), Bp(26f));
            V5TitleKit.FitWidth(row.title, V5StoryKit.FontSize(22f), V5StoryKit.FontSize(15f));

            float x = Bp(textLeft), lineTop = Bp(titleTop + 36f), lineH = Bp(26f);
            int[] held = { c.Energy, c.Alloy, c.Dark };
            for (int k = 0; k < 3; k++)
            {
                int amount = !priced ? 0 : k == 0 ? cost.energy : k == 1 ? cost.alloy : cost.dark;
                bool shown = amount > 0;
                SetShown(row.holes[k], shown);
                SetShown(row.amounts[k], shown);
                if (!shown) continue;
                var sprite = CurrencySprite(k);
                // With the art on the currency's picture is cut round; whitebox shows its icon whole.
                var round = row.holes[k].GetComponent<Mask>();
                if (round.enabled != art) round.enabled = art;
                row.holes[k].enabled = art;
                row.coins[k].sprite = sprite;
                row.coins[k].enabled = sprite;
                B5DarkIcon(k == 2 ? row.coins[k].transform : null, art);
                V5TitleKit.Place(row.holes[k].rectTransform, x, lineTop, Bp(26f), lineH);
                if (sprite) x += Bp(26f + 6f);
                string text = sprite ? amount.ToString() : $"{CurrencyLabel(k)} {amount}";
                StoryText(row.amounts[k], text);
                row.amounts[k].color = held[k] >= amount ? WalletColour(k) : B5Short;
                float w = V5StoryKit.WidthOf(row.amounts[k], text);
                V5TitleKit.Place(row.amounts[k].rectTransform, x, lineTop, w + 2f, lineH);
                x += w + Bp(14f);
            }
            SetShown(row.note, noted);
            if (!noted) return;
            if (priced) x -= Bp(6f);
            StoryText(row.note, item.note);
            V5TitleKit.Place(row.note.rectTransform, x, lineTop, Mathf.Max(Bp(20f), Bp(textLeft + textW) - x), lineH);
            V5TitleKit.FitWidth(row.note, V5StoryKit.FontSize(14f), V5StoryKit.FontSize(11f));
        }

        void RefreshBase5Foot(bool art, List<(string label, Action act)> leaves, bool canClose)
        {
            // The keys: W and S change zone; Esc closes while it does.
            float x = Bp(20f), h = Bp(B5HintsH);
            B5PlaceKey(b5KeyW, x);
            x += b5KeyW.Rect.sizeDelta.x + Bp(8f);
            B5PlaceKey(b5KeyS, x);
            x += b5KeyS.Rect.sizeDelta.x + Bp(8f);
            StoryText(b5ZonesWord, ArenaText.Pair("换区域", "Change room"));
            float w = V5StoryKit.WidthOf(b5ZonesWord, b5ZonesWord.text);
            B5PlaceLeft(b5ZonesWord, x, w, h);
            x += w;
            SetShown(b5KeyEsc, canClose);
            SetShown(b5CloseWord, canClose);
            if (canClose)
            {
                x += Bp(26f);
                B5PlaceKey(b5KeyEsc, x);
                x += b5KeyEsc.Rect.sizeDelta.x + Bp(8f);
                StoryText(b5CloseWord, ArenaText.Pair("关闭", "Close"));
                w = V5StoryKit.WidthOf(b5CloseWord, b5CloseWord.text);
                B5PlaceLeft(b5CloseWord, x, w, h);
                x += w;
            }
            float middle = B5Height - B5FootBottom - B5GoH * .5f;
            V5TitleKit.Place(b5Hints.Rect, Bp(B5Margin), Bp(middle - B5HintsH * .5f), x + Bp(20f), h);

            // The ways out along the foot from the right: the one that moves the story on (the ignition, the night, the dawn, or the only
            // one) is the orange go button at the right end; the others keep the game's order to its left.
            var c = campaign;
            int go = -1;
            foreach (var forward in new Action[] { c.Ignite, c.EnterNight, c.WaitForDawn })
            {
                for (int i = 0; i < leaves.Count && go < 0; i++)
                    if (B5Same(leaves[i].act, forward)) go = i;
                if (go >= 0) break;
            }
            if (go < 0) go = leaves.Count - 1;
            float right = Bp(B5Width - B5Margin);
            int used = 0;
            if (go >= 0) right = B5LayWay(B5WayAt(used++), leaves[go], true, right, art) - Bp(B5WayGap);
            for (int i = leaves.Count - 1; i >= 0; i--)
            {
                if (i == go) continue;
                right = B5LayWay(B5WayAt(used++), leaves[i], false, right, art) - Bp(B5WayGap);
            }
            for (int i = used; i < b5Ways.Count; i++) SetShown(b5Ways[i].plate, false);
        }

        /// <summary>One way out, its right edge at right (canvas units from the page's left); returns its left edge. Its words are the game's,
        /// what stood in brackets after a "·".</summary>
        float B5LayWay(B5Way way, (string label, Action act) leave, bool go, float right, bool art)
        {
            SetShown(way.plate, true);
            way.act = leave.act;
            way.go = go;
            way.plate.SetLook(go ? V5Plate.Look.Go : V5Plate.Look.Plate);
            B5Split(leave.label, out string head, out string note);
            string text = string.IsNullOrEmpty(note) ? head : head + " · " + note;
            StoryText(way.label, text);
            float px = go ? 26f : 21f, h = go ? B5GoH : B5WayH, padLeft = go ? 84f : 22f, padRight = go ? 26f : 22f;
            float room = Bp((go ? B5GoMaxW : B5WayMaxW) - padLeft - padRight);
            B5PlaceLeft(way.label, Bp(padLeft), room, Bp(h));
            V5TitleKit.FitWidth(way.label, V5StoryKit.FontSize(px), V5StoryKit.FontSize(px * .65f));
            float w = Mathf.Min(room, V5StoryKit.WidthOf(way.label, text));
            way.label.color = go ? (art ? V5TitleKit.Dark : Color.white) : V5TitleKit.Ink;
            float width = Bp(padLeft + padRight) + w;
            way.plate.Place(right - width, Bp(B5Height - B5FootBottom - B5GoH * .5f - h * .5f), width, Bp(h));
            bool stripes = go && art;
            SetShown(way.hazard, stripes);
            if (stripes)
            {
                float repeat = 14f * 1.41421f * B5Css;
                V5TitleKit.Place(way.hazard.rectTransform, Bp(16f), Bp(14f), Bp(48f), Bp(h - 28f));
                way.hazard.uvRect = new Rect(0f, 0f, Bp(48f) / repeat, Bp(h - 28f) / repeat);
            }
            return right - width;
        }

        // ───── What the page says ─────

        /// <summary>A zone's level as diamonds: Central Control's hull level of its levels, a room's level of three.</summary>
        static void B5LevelOf(V2Campaign c, CampaignRoom room, out int count, out int filled)
        {
            bool control = room == CampaignRoom.Control;
            count = control ? Mathf.Clamp(c.MaxDurability, 1, 5) : 3;
            filled = control ? c.Durability : c.Room(room).level;
        }

        /// <summary>A zone plate's line of state: broken (orange: repair it), Central Control's hull and shield, the Armament Bay's armour and
        /// weapons, the Energy Lab ready to study (violet: the energy research), the launch bay's next repair (orange) or its fuel (gold).</summary>
        static string B5ZoneLine(V2Campaign c, CampaignRoom room, out Color colour)
        {
            var state = c.Room(room);
            colour = V5TitleKit.Ink3;
            if (state.damaged)
            {
                colour = V5TitleKit.OrangeHi;
                return ArenaText.Pair("损坏 · 需要修复", "Broken · needs repair");
            }
            switch (room)
            {
                case CampaignRoom.Control:
                    return c.ShieldLevel > 0 ? ArenaText.Pair($"耐久 {c.Durability} 级 · 护盾 {c.ShieldLevel} 级", $"Hull {c.Durability} · shield {c.ShieldLevel}")
                        : ArenaText.Pair($"耐久 {c.Durability} 级 · 护盾未解锁", $"Hull {c.Durability} · shield locked");
                case CampaignRoom.Armament:
                    return state.level <= 0 ? ArenaText.Pair("未建造", "Not built")
                        : ArenaText.Pair($"装甲 {c.ArmorLevel} 级 · 武器 Mk {c.TeamMk}", $"Armor {c.ArmorLevel} · weapons Mk {c.TeamMk}");
                case CampaignRoom.Energy:
                    if (state.level <= 0) return ArenaText.Pair("未建造", "Not built");
                    colour = V5HudKit.VioletHi;
                    return ArenaText.Pair("可以研习", "Ready to study");
                default:
                    if (state.level <= 0) return ArenaText.Pair("未建造", "Not built");
                    colour = V5TitleKit.OrangeHi;
                    if (c.CanIgnite) return ArenaText.Pair("可以点火返航", "Ready to launch");
                    if (!c.EngineFixed) return ArenaText.Pair("引擎可以修了", "Engine ready to repair");
                    if (!c.ArmorFixed && state.level >= 2) return ArenaText.Pair("装甲可以修了", "Armor ready to repair");
                    colour = V5TitleKit.Gold;
                    if (!c.FuelFull && state.level >= 3) return ArenaText.Pair($"燃料 {c.Fuel} / {c.FuelUnits}", $"Fuel {c.Fuel} / {c.FuelUnits}");
                    colour = V5TitleKit.Ink3;
                    return ArenaText.Pair($"{state.level} / 3 级", $"Level {state.level} / 3");
            }
        }

        /// <summary>The lines that matter in a zone now (its levels are the steps under them): Central Control's life, hull and shield and
        /// its warning while broken; the Armament Bay's armour and weapons; the launch bay's two notes.</summary>
        string B5Lines(V2Campaign c, CampaignRoom room)
        {
            var r = c.rules;
            var lines = new List<string>();
            string Tint(string text, Color colour) => $"<color=#{HexOf(colour)}>{text}</color>";
            switch (room)
            {
                case CampaignRoom.Control:
                    lines.Add(ArenaText.Pair($"基地生命 {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)}　·　耐久 {c.Durability} / {c.MaxDurability} 级",
                        $"Base HP {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)}   ·   hull level {c.Durability} / {c.MaxDurability}"));
                    lines.Add(c.ShieldLevel > 0
                        ? ArenaText.Pair($"护盾 {Mathf.CeilToInt(c.BaseShield)} / {Mathf.CeilToInt(c.BaseMaxShield)}　·　{c.ShieldLevel} / {c.MaxShieldLevel} 级　·　每晚回满",
                            $"Shield {Mathf.CeilToInt(c.BaseShield)} / {Mathf.CeilToInt(c.BaseMaxShield)}   ·   level {c.ShieldLevel} / {c.MaxShieldLevel}   ·   refills each night")
                        : ArenaText.Pair("护盾：未解锁", "Shield: locked"));
                    if (c.Room(room).damaged)
                        lines.Add(Tint(ArenaText.Pair("总控区损坏了。先在这里把它修好，其他三个区才能继续建造和升级。",
                            "Central Control is broken. Repair it here first; until then, the other three rooms can't be built or upgraded."), V5TitleKit.OrangeHi));
                    else if (r && c.BaseHp < c.BaseMaxHp - .5f)
                        lines.Add(Tint(ArenaText.Pair($"基地每晚会自动修复 {r.nightSelfRepair * 100f:0}% 的生命", $"The base repairs {r.nightSelfRepair * 100f:0}% of its HP on its own each night"),
                            V5TitleKit.Ink3));
                    break;
                case CampaignRoom.Armament:
                    if (c.Room(room).level >= 1) lines.Add(ArenaText.Pair($"装甲 {c.ArmorLevel} 级　·　武器 Mk {c.TeamMk}", $"Armor level {c.ArmorLevel}   ·   weapons Mk {c.TeamMk}"));
                    break;
                case CampaignRoom.Ship:
                    if (r) lines.Add(ArenaText.Pair($"矿灯在这里造，每盏 {r.lampCost} 合金，用不完的会留到下一晚。",
                        $"Mining lamps are made here, {r.lampCost} alloy each; unused ones keep for the next night."));
                    lines.Add(ArenaText.Pair("修好引擎和装甲、加满燃料，就能点火返航。", "Repair the engine and the armor and fill the tank, and the ship can head home."));
                    break;
            }
            return string.Join("\n", lines);
        }

        /// <summary>What a room's level brings, as the old page's lines name it.</summary>
        static string B5StepWords(CampaignRoom room, int level) => room switch
        {
            CampaignRoom.Armament => level == 1 ? ArenaText.Pair("装甲升级、武器型号", "Armor and weapon Mk upgrades")
                : level == 2 ? ArenaText.Pair("新武器、驯化", "New weapons and taming") : ArenaText.Pair("武器变招", "Weapon twists"),
            CampaignRoom.Energy => level == 1 ? ArenaText.Pair("体质研究", "Body research")
                : level == 2 ? ArenaText.Pair("技能研究", "Skill research") : ArenaText.Pair("附魔研究", "Infusion research"),
            _ => level == 1 ? ArenaText.Pair("修复飞船引擎", "Repair the ship's engine")
                : level == 2 ? ArenaText.Pair("修复飞船装甲", "Repair the ship's armor") : ArenaText.Pair("合成反物质燃料", "Synthesize antimatter fuel"),
        };

        /// <summary>What an action does. The campaign's own methods are known by the delegate; the few actions the page writes as lambdas
        /// by their place (a broken room's repair comes first) and their words, which the old page sets in the same frame.</summary>
        B5Deed B5DeedOf(V2Campaign c, Func<bool> act, string label, bool repairFirst)
        {
            if (B5Same(act, c.TryUpgradeDurability)) return B5Deed.Durability;
            if (B5Same(act, c.TryUpgradeShield)) return B5Deed.Shield;
            if (B5Same(act, c.TryRepairHull)) return B5Deed.Hull;
            if (B5Same(act, c.TryRepairEngine)) return B5Deed.Engine;
            if (B5Same(act, c.TryRepairArmor)) return B5Deed.Armor;
            if (B5Same(act, c.TrySynthesiseFuel)) return B5Deed.Fuel;
            if (B5Same(act, c.TryCraftLamp)) return B5Deed.Lamp;
            if (repairFirst) return B5Deed.RepairRoom;
            if (label == UpgradeLabel(tab)) return c.Room(tab).level <= 0 ? B5Deed.Build : B5Deed.Upgrade;
            if (label == ArenaText.Pair("研习", "Study")) return B5Deed.Study;
            if (label == ArenaText.Pair("决战过载", "Final overload")) return B5Deed.Overload;
            if (label == ArenaText.Pair("启动点火程序", "Start the ignition sequence")) return B5Deed.Ignite;
            return B5Deed.Other;
        }

        static bool B5Same(Func<bool> act, Func<bool> method) => act != null && act.Equals(method);

        static bool B5Same(Action act, Action method) => act != null && act.Equals(method);

        /// <summary>The row of the one action to do now (warm, an orange button): the ignition once it can start; otherwise the first action
        /// in the list that can be done and that the stock pays for. Opening the study window is never it. -1 for none.</summary>
        int B5GoItem(V2Campaign c, List<B5Item> items)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].deed == B5Deed.Ignite && items[i].enabled) return i;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (!item.enabled || item.deed is B5Deed.Study or B5Deed.Overload or B5Deed.Other) continue;
                if (item.deed == B5Deed.RepairRoom && tab != CampaignRoom.Control && !c.ControlWorks) continue; // Central Control first
                if (!item.cost.HasValue || item.cost.Value.IsFree) continue;
                var cost = item.cost.Value;
                if (c.Energy >= cost.energy && c.Alloy >= cost.alloy && c.Dark >= cost.dark) return i;
            }
            return -1;
        }

        string B5Verb(V2Campaign c, B5Deed deed) => deed switch
        {
            B5Deed.Build => ArenaText.Pair("建造", "Build"),
            B5Deed.Upgrade or B5Deed.Durability => ArenaText.Pair("升级", "Upgrade"),
            B5Deed.Shield => c.ShieldLevel == 0 ? ArenaText.Pair("解锁", "Unlock") : ArenaText.Pair("升级", "Upgrade"),
            B5Deed.RepairRoom or B5Deed.Hull or B5Deed.Engine or B5Deed.Armor => ArenaText.Pair("修复", "Repair"),
            B5Deed.Fuel => ArenaText.Pair("合成", "Synthesize"),
            B5Deed.Lamp => ArenaText.Pair("制造", "Craft"),
            B5Deed.Ignite => ArenaText.Pair("点火", "Ignite"),
            B5Deed.Study or B5Deed.Overload => ArenaText.Pair("打开", "Open"),
            _ => ArenaText.Pair("进行", "Go"),
        };

        /// <summary>An action's medal: the painted icon of what it does with the art on, the whitebox's icon for it in whitebox.</summary>
        void B5MedalIcons(V2Campaign c, B5Deed deed, out Sprite artIcon, out Sprite flatIcon)
        {
            var set = c.art;
            switch (deed)
            {
                case B5Deed.Durability:
                    artIcon = set ? set.durability : null;
                    flatIcon = WhiteboxIcons.Room(CampaignRoom.Control);
                    break;
                case B5Deed.Shield:
                    artIcon = set ? set.shield : null;
                    flatIcon = WhiteboxIcons.Mark(WhiteboxIcons.Glyph.Ring);
                    break;
                case B5Deed.Hull:
                case B5Deed.RepairRoom:
                    artIcon = set ? set.repair : null;
                    flatIcon = WhiteboxIcons.Room(deed == B5Deed.Hull ? CampaignRoom.Control : tab);
                    break;
                case B5Deed.Overload:
                    artIcon = set ? set.control : null;
                    flatIcon = WhiteboxIcons.Room(CampaignRoom.Control);
                    break;
                case B5Deed.Engine:
                case B5Deed.Ignite:
                    artIcon = set ? set.engine : null;
                    flatIcon = V5StoryKit.EngineIcon;
                    break;
                case B5Deed.Armor:
                    artIcon = set ? set.armor : null;
                    flatIcon = V5StoryKit.ArmorIcon;
                    break;
                case B5Deed.Fuel:
                    artIcon = set ? set.fuel : null;
                    flatIcon = V5StoryKit.FuelIcon;
                    break;
                case B5Deed.Lamp:
                    artIcon = set ? set.lamp : null;
                    flatIcon = V5StoryKit.LampIcon;
                    break;
                case B5Deed.Study:
                    artIcon = set ? (tab == CampaignRoom.Armament ? set.armament : set.skill) : null;
                    flatIcon = WhiteboxIcons.Room(tab);
                    break;
                default:
                    artIcon = B5RoomArt(c, tab);
                    flatIcon = WhiteboxIcons.Room(tab);
                    break;
            }
        }

        static Sprite B5RoomArt(V2Campaign c, CampaignRoom room)
        {
            var set = c.art;
            if (!set) return null;
            return room switch { CampaignRoom.Control => set.control, CampaignRoom.Armament => set.armament, CampaignRoom.Energy => set.energyLab, _ => set.shipBay };
        }

        /// <summary>A zone's picture in its hexagon: Central Control's room, the Armament Bay's and the Energy Lab's room at their level, the
        /// launch bay's icon (uv: the part of the texture that is the picture).</summary>
        static Texture B5ZoneTexture(V2Campaign c, CampaignRoom room, out Rect uv)
        {
            uv = new Rect(0f, 0f, 1f, 1f);
            var set = c.art;
            if (!set) return null;
            int level = c.Room(room).level;
            switch (room)
            {
                case CampaignRoom.Control: return set.roomControl;
                case CampaignRoom.Armament: return LevelPicture(set.roomArmamentLevels, level, set.roomArmament);
                case CampaignRoom.Energy: return LevelPicture(set.roomEnergyLevels, level, set.roomEnergy);
            }
            var icon = set.shipBay;
            if (!icon || !icon.texture || icon.packed && icon.packingMode == SpritePackingMode.Tight) return null;
            var texture = icon.texture;
            var area = icon.textureRect;
            uv = new Rect(area.x / texture.width, area.y / texture.height, area.width / texture.width, area.height / texture.height);
            return texture;
        }

        /// <summary>The part of uv that fills a box of the given shape (width over height) from a picture of shape: cut at the edges, never
        /// stretched.</summary>
        static Rect B5Cover(Rect uv, float shape, float box)
        {
            if (shape <= 0f || box <= 0f) return uv;
            if (shape < box)
            {
                float height = uv.height * shape / box;
                return new Rect(uv.x, uv.y + (uv.height - height) * .5f, uv.width, height);
            }
            float width = uv.width * box / shape;
            return new Rect(uv.x + (uv.width - width) * .5f, uv.y, width, uv.height);
        }

        /// <summary>A label's words before and in its brackets ("制造矿灯（现有 3 盏）": "制造矿灯" and "现有 3 盏"; English " (...)").</summary>
        static void B5Split(string label, out string head, out string note)
        {
            head = label ?? "";
            note = "";
            int open = head.IndexOf('（'), close = head.LastIndexOf('）');
            int skip = 1;
            if (open <= 0 || close <= open)
            {
                open = head.IndexOf(" (", StringComparison.Ordinal);
                close = head.LastIndexOf(')');
                skip = 2;
            }
            if (open <= 0 || close <= open) return;
            note = head.Substring(open + skip, close - open - skip).Trim();
            head = (head.Substring(0, open) + head.Substring(close + 1)).Trim();
        }

        // ───── Small pieces ─────

        /// <summary>Diamonds in a row (size across before they are turned, gap apart), the first left canvas units in and their centres
        /// centreFromTop down from the parent's top; filled ones gold. Returns where the row ends.</summary>
        static float B5Diamonds(List<Image> pool, Transform parent, int count, int filled, float size, float gap, float left, float centreFromTop, bool art)
        {
            while (pool.Count < count)
            {
                var made = V5TitleKit.Picture(parent, "Diamond" + (pool.Count + 1), null, Color.white);
                made.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var rim = made.gameObject.AddComponent<UnityEngine.UI.Outline>();
                rim.useGraphicAlpha = false;
                rim.effectColor = B5DiamondRim;
                rim.effectDistance = new Vector2(1f, -1f);
                pool.Add(made);
            }
            for (int k = 0; k < pool.Count; k++)
            {
                var diamond = pool[k];
                bool shown = k < count;
                SetShown(diamond, shown);
                if (!shown) continue;
                bool on = k < filled;
                V5TitleKit.Dress(diamond, on && art ? V5StoryKit.Fill(B5DiamondTop, B5DiamondBottom) : WhiteboxSkin.Square,
                    on ? (art ? Color.white : V5TitleKit.Gold) : B5DiamondOff);
                if (diamond.TryGetComponent<UnityEngine.UI.Outline>(out var rim)) rim.enabled = !on;
                var rect = diamond.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = new Vector2(left + size * .5f + k * (size + gap), -centreFromTop);
                rect.sizeDelta = Vector2.one * size;
            }
            return left + count * size + Mathf.Max(0, count - 1) * gap;
        }

        /// <summary>The dark matter's picture has more empty margin than the other two currencies': with the art on it is drawn larger by
        /// DarkIconScale inside its round, so the three read as the same size.</summary>
        static void B5DarkIcon(Transform icon, bool art)
        {
            if (icon) icon.localScale = Vector3.one * (art ? DarkIconScale : 1f);
        }

        /// <summary>An image in its picture with the art on, or a flat fill with the thin edge in whitebox (or without the picture).</summary>
        static void B5Flat(Image image, bool art, Sprite sprite, Color artColour, float density, Color flatColour, Color edge)
        {
            bool pictured = art && sprite;
            V5TitleKit.Dress(image, pictured ? sprite : null, pictured ? artColour : flatColour, pictured ? density : 0f);
            B5Edge(image, !pictured && edge.a > 0f, edge);
        }

        /// <summary>The whitebox's thin edge round a graphic, on or off.</summary>
        static void B5Edge(Graphic graphic, bool on, Color colour)
        {
            if (!graphic) return;
            graphic.TryGetComponent<UnityEngine.UI.Outline>(out var edge);
            if (on && !edge)
            {
                edge = graphic.gameObject.AddComponent<UnityEngine.UI.Outline>();
                edge.useGraphicAlpha = false;
                edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (!edge) return;
            if (edge.enabled != on) edge.enabled = on;
            if (edge.effectColor != colour) edge.effectColor = colour;
        }

        /// <summary>A label from left in its row, as wide as width and as tall as the row, its middle on the row's middle.</summary>
        static void B5PlaceLeft(Text label, float left, float width, float height)
        {
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, 0f);
            rect.sizeDelta = new Vector2(width + 2f, height);
        }

        static void B5PlaceKey(V5HudKey key, float left)
        {
            var rect = key.Rect;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, 0f);
        }

        /// <summary>A rect at fractions of its parent: left and top from the parent's top-left corner, width and height.</summary>
        static void B5Fraction(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = new Vector2(left, 1f - top - height);
            rect.anchorMax = new Vector2(left + width, 1f - top);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>The page's backing with the art on: deep navy at the foot to a lighter navy at the top, leaning to indigo on the right
        /// (ui.css: surfaces run from deep navy to deep indigo instead of flat black). Shown through a uv of its middle, a smooth blend.</summary>
        static Texture2D B5BackdropTexture
        {
            get
            {
                if (b5BackdropTexture) return b5BackdropTexture;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "V5Base backdrop", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
                };
                texture.SetPixels32(new Color32[] { new(10, 14, 32, 255), new(20, 12, 40, 255), new(18, 28, 56, 255), new(30, 20, 60, 255) });
                texture.Apply(false, true);
                return b5BackdropTexture = texture;
            }
        }

        /// <summary>The steps' line with the art on: teal, orange and gold, blended (the mock-up's track).</summary>
        static Texture2D B5TrackTexture
        {
            get
            {
                if (b5TrackTexture) return b5TrackTexture;
                var texture = new Texture2D(3, 1, TextureFormat.RGBA32, false)
                {
                    name = "V5Base track", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave
                };
                texture.SetPixels32(new Color32[] { V5TitleKit.Teal, V5TitleKit.Orange, V5TitleKit.Gold });
                texture.Apply(false, true);
                return b5TrackTexture = texture;
            }
        }
    }
}
