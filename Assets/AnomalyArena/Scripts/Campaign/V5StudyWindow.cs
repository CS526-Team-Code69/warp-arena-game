using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The base's study window in the fifth interface pass, in the base page's family (V5BasePage.cs, work/art-plans/style-1112/ui-e/
    /// base.html): built only when V5HudKit.On, so the English Alpha keeps the window it was graded with. The page is the 1920 x 1080
    /// mock-up times the story layer's scale with the base page's banner, wallet, close button, margins, floor and foot, centred on the
    /// window's full-screen backing and shrunk to fit a screen that is not 16:9. The old window stays built under it, hidden, its texts
    /// up to date; only its scrolling grid moves into the new page, where the tiles are drawn in the new look. Choosing a tab or a tile,
    /// the keys and buying are the old window's (V2CampaignUi.Study.cs): this part draws, and its plates call the same methods.
    /// Top: the banner ("研习" with the room and the tab), the wallet (the three currencies, and the void's two once the Nash Prophet is
    /// beaten) and the close button with its key.
    /// Left: the tabs under their rooms' names; the chosen one warm with an orange pointer.
    /// Middle: one plate per tile: its medal ringed in its rarity's colour, its name, its number now and after, its price as each
    /// currency's icon and amount (the amount red while the stock is short of it). The chosen tile is warm with the hazard tab on its top
    /// edge; one owned is teal with a check; one not unlocked yet is dim with a question mark.
    /// Right: the chosen tile in full: medal, name, rarity and tab, the numbers, the price, what it does.
    /// Foot: the keys as keycaps (the pad's buttons while the pad is in use) and the orange buy button with its key.
    /// Every piece follows the art switch: the mock-up's pictures with the art on, flat fills with the thin edge and the whitebox icons
    /// without.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // In mock-up pixels, on the base page's grid (B5Margin, B5Top, B5Floor): nothing of the three columns reaches below the floor, so
        // a subtitle over the window never covers them; the keys and the buy button stand along the foot, under the subtitle's strip.
        // Tabs: a column 300 wide; a room's name 30 tall and 12 more before the next room's; a tab 46 tall and 8 apart (down to 36 and 5
        // when there are many); the chosen tab's pointer 14 wide, its tip 24 beyond the tab.
        const float S5TabsW = 300f, S5RoomH = 30f, S5RoomGap = 12f, S5TabH = 46f, S5TabGap = 8f, S5TabMinH = 36f, S5TabMinGap = 5f;
        // Tiles: the grid 372 from the left and 968 wide, four tiles of 228 x 172 a row, 17 apart across and 16 down, 8 kept above the
        // first row and under the last so the chosen tile's tab stays inside the scrolled view; corners cut at 12, the medal 64 across.
        const float S5GridLeft = 372f, S5GridW = 968f, S5TileW = 228f, S5TileH = 172f, S5TileGapX = 17f, S5TileGapY = 16f, S5GridPad = 8f;
        const float S5TileCut = 12f, S5TileMedal = 64f;
        // The chosen tile in full: 1368 from the left and 516 wide (its right edge on the margin), corners cut at 18, the medal 92 across.
        // The buy button is at least 300 wide.
        const float S5DetailLeft = 1368f, S5DetailW = 516f, S5DetailCut = 18f, S5DetailMedal = 92f, S5GoMinW = 300f;

        /// <summary>A price drawn as icons and amounts: up to three coins, each a round icon (cut round with the art on), a violet ring for
        /// the void's currencies, and its amount; or the price in words when it cannot be read as numbers.</summary>
        sealed class S5Coins
        {
            public readonly Image[] holes = new Image[3], icons = new Image[3], rings = new Image[3];
            public readonly Text[] amounts = new Text[3];
            public Text words;
        }

        sealed class S5Tile
        {
            public int index;
            public V5Plate plate;
            public V5HudPlate teal, warm;
            public Image tab;
            public V5HudMedal medal;
            public Text mark, title, now, after, done;
            public readonly S5Coins coins = new();
        }

        sealed class S5Tab
        {
            public int index;
            public V5Plate plate;
            public V5HudPlate warm;
            public Image pointer;
            public Text name;
        }

        sealed class S5Room
        {
            public Text name;
            public Image rule;
        }

        bool s5Built, s5PadKeys;
        int s5TickFrame = -10;
        RectTransform s5Page, s5Wallet;
        RawImage s5Backdrop, s5GoStripes;
        Image s5Banner, s5WalletRule, s5DetailDiamond, s5DetailRule;
        Text s5Title, s5Where, s5CloseLabel, s5DetailMark, s5DetailTitle, s5DetailKind, s5DetailNumbers, s5PriceCaption, s5Done, s5DetailBody, s5GoLabel;
        V5Plate s5Close, s5Go;
        V5HudKey s5CloseKey, s5GoKey;
        V5HudPlate s5Detail, s5Hints;
        V5HudMedal s5DetailMedal;
        readonly S5Coins s5DetailCoins = new();
        readonly List<S5Tile> s5Tiles = new();
        readonly List<S5Tab> s5Tabs = new();
        readonly List<S5Room> s5Rooms = new();
        readonly List<V5HudKey> s5Keys = new();
        readonly List<Text> s5KeyWords = new();
        readonly V5HudMedal[] s5WalletMedals = new V5HudMedal[5];
        readonly Text[] s5WalletNumbers = new Text[5];
        readonly int[] s5WalletShown = { -1, -1, -1, -1, -1 };
        // The price being drawn: which currency (energy, alloy, dark matter, void energy, void alloy), how much, and how much is held.
        readonly List<(int which, int amount, int held)> s5Price = new();
        string s5ShownId, s5ShownNow;
        static readonly string[] S5WalletNames = { "Energy", "Alloy", "Dark", "VoidEnergy", "VoidAlloy" };
        static readonly string[] S5KeysChoose = { "W", "A", "S", "D" }, S5KeysTabs = { "Q", "E" }, S5KeysBuy = { "F" }, S5KeysClose = { "Esc" };
        static readonly string[] S5PadTabs = { "LB", "RB" }, S5PadBuy = { "A" }, S5PadClose = { "B" };

        /// <summary>A tile's size, the gaps between tiles and the room kept above the first row, in canvas units: the fifth pass's when
        /// V5HudKit.On, the old window's otherwise (it keeps no room).</summary>
        static float StudyTileW => V5HudKit.On ? Bp(S5TileW) : TileWidth;
        static float StudyTileH => V5HudKit.On ? Bp(S5TileH) : TileHeight;
        static float StudyGapX => V5HudKit.On ? Bp(S5TileGapX) : TileGap;
        static float StudyGapY => V5HudKit.On ? Bp(S5TileGapY) : TileGap;
        static float StudyPad => V5HudKit.On ? Bp(S5GridPad) : 0f;

        // ───── Building (BuildStudyWindow, once) ─────

        void BuildStudy5()
        {
            var grid = (RectTransform)studyScroll.transform;
            // The old window's pieces stay built and keep their texts, hidden; only its scrolling grid moves into the new page.
            foreach (Transform child in studyScreen)
                if (child != grid) child.gameObject.SetActive(false);
            s5Backdrop = V5TitleKit.Raw(studyScreen, "Backdrop", B5BackdropTexture, Color.white);
            V5TitleKit.Stretch(s5Backdrop.rectTransform);
            s5Backdrop.uvRect = new Rect(.25f, .25f, .5f, .5f);
            s5Page = V5TitleKit.Rect(studyScreen, "Page5");
            V5TitleKit.Centre(s5Page, new Vector2(Bp(B5Width), Bp(B5Height)));
            BuildStudy5Head();
            grid.SetParent(s5Page, false);
            V5TitleKit.Place(grid, Bp(S5GridLeft), Bp(B5Top), Bp(S5GridW), Bp(B5Floor - B5Top));
            studyEmpty.color = V5TitleKit.Ink3;
            studyEmpty.fontSize = V5StoryKit.FontSize(20f);
            BuildStudy5Detail();
            BuildStudy5Foot();
            s5Built = true;
        }

        void BuildStudy5Head()
        {
            s5Banner = V5TitleKit.Picture(s5Page, "Banner", null, Color.white);
            s5Banner.gameObject.AddComponent<V5StoryBannerFrame>();
            V5TitleKit.Place(s5Banner.rectTransform, Bp(B5Margin), Bp(B5BannerTop), Bp(B5BannerW), Bp(B5BannerH));
            s5Title = V5StoryKit.Label(s5Banner.transform, "Title", 50f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(s5Title);
            s5Where = V5StoryKit.Label(s5Banner.transform, "Where", 20f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);

            s5Wallet = V5TitleKit.Rect(s5Page, "Wallet");
            s5Wallet.anchorMin = s5Wallet.anchorMax = s5Wallet.pivot = new Vector2(1f, 1f);
            V5TitleKit.Stretch(V5HudKit.Plate(s5Wallet, "Plate", V5HudKit.PlateLook.Plain, 12f, B5Css).Rect);
            s5WalletRule = V5TitleKit.Picture(s5Wallet, "Rule", null, V5HudKit.Hairline);
            for (int i = 0; i < 5; i++)
            {
                s5WalletMedals[i] = V5HudKit.Medal(s5Wallet, S5WalletNames[i] + "Medal", Bp(B5WalletMedal));
                s5WalletNumbers[i] = V5StoryKit.Label(s5Wallet, S5WalletNames[i] + "Amount", B5WalletNumber, S5CoinColour(i), TextAnchor.MiddleCenter, true);
            }
            // The void's own currencies have no pictures of their own: the alien energy's and alloy's, ringed and lit in violet, as in the
            // story's wallet.
            for (int i = 3; i < 5; i++)
            {
                s5WalletMedals[i].SetRing(V5HudKit.Violet);
                s5WalletMedals[i].SetGlow(V5StoryKit.VioletGlow);
            }

            s5Close = V5Plate.Make(s5Page, "Close", V5Plate.Look.Plate, B5Cut(10f), ClickStudy5Close);
            s5Close.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            s5CloseLabel = V5StoryKit.Label(s5Close.transform, "Label", 19f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            s5CloseKey = V5HudKit.Key(s5Close.transform, "Key", "Esc", false, B5Css);
        }

        void BuildStudy5Detail()
        {
            s5Detail = V5HudKit.Plate(s5Page, "Detail5", V5HudKit.PlateLook.Plain, S5DetailCut, B5Css);
            V5TitleKit.Place(s5Detail.Rect, Bp(S5DetailLeft), Bp(B5Top), Bp(S5DetailW), Bp(B5Floor - B5Top));
            var panel = s5Detail.transform;
            s5DetailMedal = V5HudKit.Medal(panel, "Medal", Bp(S5DetailMedal));
            PlaceCentre(s5DetailMedal.Rect, new Vector2(0f, 1f), new Vector2(Bp(24f + S5DetailMedal * .5f), -Bp(24f + S5DetailMedal * .5f)), Vector2.one * Bp(S5DetailMedal));
            s5DetailMark = V5StoryKit.Label(s5DetailMedal.transform, "Unknown", 44f, V5TitleKit.Ink3, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(s5DetailMark.rectTransform);
            s5DetailTitle = V5StoryKit.Label(panel, "Title", 28f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Wrap(s5DetailTitle);
            V5TitleKit.Painted(s5DetailTitle);
            V5TitleKit.Place(s5DetailTitle.rectTransform, Bp(136f), Bp(18f), Bp(S5DetailW - 160f), Bp(74f));
            // The rarity as a small diamond in its colour before "rarity · tab".
            s5DetailDiamond = V5TitleKit.Picture(panel, "Rarity", WhiteboxSkin.Square, Color.white);
            s5DetailDiamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            PlaceCentre(s5DetailDiamond.rectTransform, new Vector2(0f, 1f), new Vector2(Bp(142f), -Bp(105f)), Vector2.one * Bp(10f));
            s5DetailKind = V5StoryKit.Label(panel, "Kind", 15f, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
            V5TitleKit.Place(s5DetailKind.rectTransform, Bp(156f), Bp(94f), Bp(S5DetailW - 180f), Bp(22f));
            s5DetailNumbers = V5StoryKit.Label(panel, "StudyNumbers", 26f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            s5DetailRule = V5TitleKit.Picture(panel, "Rule", null, V5HudKit.Hairline);
            s5PriceCaption = V5StoryKit.Label(panel, "PriceCaption", 13f, V5TitleKit.Ink3, TextAnchor.MiddleLeft, true);
            BuildStudy5Coins(s5DetailCoins, panel, 30f);
            s5Done = V5StoryKit.Label(panel, "Owned", 22f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);
            s5DetailBody = V5StoryKit.Label(panel, "Body", 17f, V5TitleKit.Ink2, TextAnchor.UpperLeft);
            V5TitleKit.Wrap(s5DetailBody);
            s5DetailBody.lineSpacing = 1.15f;
        }

        static void BuildStudy5Coins(S5Coins coins, Transform parent, float numberPx)
        {
            var disc = V5TitleKit.Piece("disc");
            for (int k = 0; k < 3; k++)
            {
                coins.holes[k] = V5TitleKit.Picture(parent, "Coin" + k, disc, Color.white);
                coins.holes[k].gameObject.AddComponent<Mask>().showMaskGraphic = false;
                coins.icons[k] = V5TitleKit.Picture(coins.holes[k].transform, "Icon", null, Color.white);
                V5TitleKit.Stretch(coins.icons[k].rectTransform);
                coins.icons[k].preserveAspect = true;
                coins.rings[k] = V5TitleKit.Picture(parent, "CoinRing" + k, V5HudKit.RingLine, V5HudKit.Violet);
                coins.amounts[k] = V5StoryKit.Label(parent, "Amount" + k, numberPx, V5TitleKit.Steel, TextAnchor.MiddleLeft, true);
            }
            coins.words = V5StoryKit.Label(parent, "PriceWords", numberPx * .7f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
        }

        void BuildStudy5Foot()
        {
            s5Hints = V5HudKit.Plate(s5Page, "Keys", V5HudKit.PlateLook.Plain, 10f, B5Css);
            for (int i = 0; i < 8; i++) s5Keys.Add(V5HudKit.Key(s5Hints.transform, "Key" + (i + 1), "", false, B5Css));
            for (int i = 0; i < 4; i++) s5KeyWords.Add(V5StoryKit.Label(s5Hints.transform, "Word" + (i + 1), 16f, V5TitleKit.Ink2, TextAnchor.MiddleLeft));
            s5Go = V5Plate.Make(s5Page, "Buy", V5Plate.Look.Go, B5Cut(10f), ClickStudy5Buy, true);
            s5Go.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            s5GoStripes = V5TitleKit.Raw(s5Go.transform, "Hazard", V5TitleKit.Stripes, B5HazardInk);
            s5GoLabel = V5StoryKit.Label(s5Go.transform, "Label", 26f, V5TitleKit.Dark, TextAnchor.MiddleLeft, true);
            s5GoKey = V5HudKit.Key(s5Go.transform, "Key", "F", false, B5Css);
            SetShown(s5Go, false);
        }

        /// <summary>The room's name over its tabs at index, made when first needed and reused.</summary>
        S5Room S5RoomAt(int index)
        {
            while (s5Rooms.Count <= index)
            {
                int n = s5Rooms.Count + 1;
                s5Rooms.Add(new S5Room
                {
                    name = V5StoryKit.Label(s5Page, "Room" + n, 16f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true),
                    rule = V5TitleKit.Picture(s5Page, "RoomRule" + n, null, V5HudKit.Hairline),
                });
            }
            return s5Rooms[index];
        }

        /// <summary>The tab at index, made when first needed and reused (a plate under the pointer stays the same plate when the tabs are
        /// laid out again).</summary>
        S5Tab S5TabAt(int index)
        {
            while (s5Tabs.Count <= index)
            {
                var tab = new S5Tab { index = s5Tabs.Count };
                tab.plate = V5Plate.Make(s5Page, "Tab" + (tab.index + 1), V5Plate.Look.Plate, B5Cut(10f), () => ClickStudy5Tab(tab), true);
                tab.plate.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                tab.plate.HoverPitch = 1.08f - Mathf.Min(tab.index, 10) * .02f;
                // The chosen tab's warm plate lies over the plate's own surface.
                tab.warm = V5HudKit.Plate(tab.plate.transform, "Warm", V5HudKit.PlateLook.Warm, 10f, B5Css);
                V5TitleKit.Stretch(tab.warm.Rect);
                tab.name = V5StoryKit.Label(tab.plate.transform, "Name", 20f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                // The pointer: a triangle 14 wide and 20 tall pointing right at the tiles, its tip 24 beyond the tab.
                tab.pointer = V5HudKit.Arrow(tab.plate.transform, "Pointer", new Vector2(Bp(20f), Bp(14f)), V5TitleKit.Orange, -90f);
                var pointer = tab.pointer.rectTransform;
                pointer.anchorMin = pointer.anchorMax = new Vector2(1f, .5f);
                pointer.pivot = new Vector2(.5f, .5f);
                pointer.anchoredPosition = new Vector2(Bp(24f - 7f), 0f);
                s5Tabs.Add(tab);
            }
            return s5Tabs[index];
        }

        /// <summary>The tile at index, made when first needed and reused.</summary>
        S5Tile S5TileAt(int index)
        {
            while (s5Tiles.Count <= index)
            {
                var tile = new S5Tile { index = s5Tiles.Count };
                tile.plate = V5Plate.Make(studyContent, "Tile", V5Plate.Look.Plate, B5Cut(S5TileCut), () => ClickStudy5Tile(tile), true);
                tile.plate.Button.navigation = new Navigation { mode = Navigation.Mode.None };
                var t = tile.plate.transform;
                // Owned: teal; chosen: warm, over the teal; both over the plate's own surface.
                tile.teal = V5HudKit.Plate(t, "Teal", V5HudKit.PlateLook.Teal, S5TileCut, B5Css);
                V5TitleKit.Stretch(tile.teal.Rect);
                tile.warm = V5HudKit.Plate(t, "Warm", V5HudKit.PlateLook.Warm, S5TileCut, B5Css);
                V5TitleKit.Stretch(tile.warm.Rect);
                tile.tab = V5TitleKit.Picture(t, "Tab", null, Color.white);
                V5TitleKit.Place(tile.tab.rectTransform, Bp(22f), -Bp(5f), Bp(64f), Bp(10f));
                tile.medal = V5HudKit.Medal(t, "Medal", Bp(S5TileMedal));
                PlaceCentre(tile.medal.Rect, new Vector2(0f, 1f), new Vector2(Bp(14f + S5TileMedal * .5f), -Bp(14f + S5TileMedal * .5f)), Vector2.one * Bp(S5TileMedal));
                tile.mark = V5StoryKit.Label(tile.medal.transform, "Unknown", 34f, V5TitleKit.Ink3, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(tile.mark.rectTransform);
                tile.title = V5StoryKit.Label(t, "Title", 18f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                V5TitleKit.Wrap(tile.title);
                V5TitleKit.Place(tile.title.rectTransform, Bp(90f), Bp(12f), Bp(S5TileW - 90f - 12f), Bp(68f));
                tile.now = V5StoryKit.Label(t, "StudyNumbers", 15f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
                V5TitleKit.Place(tile.now.rectTransform, Bp(14f), Bp(88f), Bp(S5TileW - 28f), Bp(20f));
                tile.after = V5StoryKit.Label(t, "After", 15f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(tile.after.rectTransform, Bp(14f), Bp(110f), Bp(S5TileW - 28f), Bp(20f));
                BuildStudy5Coins(tile.coins, t, 20f);
                tile.done = V5StoryKit.Label(t, "Owned", 17f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);
                V5TitleKit.Place(tile.done.rectTransform, Bp(14f), Bp(S5TileH - 12f - 26f), Bp(S5TileW - 28f), Bp(26f));
                s5Tiles.Add(tile);
            }
            return s5Tiles[index];
        }

        // ───── Acting: the old window's own methods, as its buttons called them ─────

        /// <summary>A plate lets go of the keyboard's selection once clicked (B5Deselect): Space and Enter are never read in this window.</summary>
        void ClickStudy5Tab(S5Tab tab)
        {
            B5Deselect();
            if (StudyOpen) SelectStudyTab(tab.index);
        }

        void ClickStudy5Tile(S5Tile tile)
        {
            B5Deselect();
            if (StudyOpen) SelectStudyTile(tile.index, true);
        }

        void ClickStudy5Buy()
        {
            B5Deselect();
            if (StudyOpen) BuySelected();
        }

        void ClickStudy5Close()
        {
            B5Deselect();
            CloseStudy();
        }

        // ───── The frame (TickStudy, while the window is open) ─────

        void TickStudy5()
        {
            if (!s5Built) return;
            s5TickFrame = Time.frameCount;
            S5FitPage();
            // The keys on screen name the pad's buttons while the pad is in use, the keyboard's once a key is pressed or the mouse moves.
            bool pad = s5PadKeys;
            var gamepad = Gamepad.current;
            if (gamepad != null && (gamepad.dpad.ReadValue().sqrMagnitude > .25f || gamepad.leftStick.ReadValue().sqrMagnitude > .36f
                    || gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame
                    || gamepad.leftShoulder.wasPressedThisFrame || gamepad.rightShoulder.wasPressedThisFrame))
                pad = true;
            else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame || Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4f)
                pad = false;
            if (pad == s5PadKeys) return;
            s5PadKeys = pad;
            RefreshStudy5Close();
            RefreshStudy5Foot();
            LayStudy5Go(SelectedEntry, Art.Enabled);
        }

        /// <summary>The page keeps the mock-up's shape and shrinks to fit a screen narrower or shorter than 16:9.</summary>
        void S5FitPage()
        {
            var size = root.rect.size;
            float scale = Mathf.Min(1f, Mathf.Min(size.x / Bp(B5Width), size.y / Bp(B5Height)));
            if (scale > 0f && !Mathf.Approximately(s5Page.localScale.x, scale)) s5Page.localScale = new Vector3(scale, scale, 1f);
        }

        // ───── Refreshing ─────

        /// <summary>RefreshStudyTexts (four times a second while open, and after every change): the backing, the banner, the wallet, the
        /// close button and the keys.</summary>
        void RefreshStudyTexts5()
        {
            if (!s5Built) return;
            var c = campaign;
            bool art = Art.Enabled;
            if (studyScreen.TryGetComponent<Image>(out var back)) back.color = art ? B5Abyss : StudyBack;
            s5Backdrop.enabled = art;
            S5FitPage();
            RefreshStudy5Banner();
            RefreshStudy5Wallet(c, art);
            RefreshStudy5Close();
            RefreshStudy5Foot();
        }

        void RefreshStudy5Banner()
        {
            // "研习" and where: the room and the tab, in the room's colour, centred between the banner's painted ends.
            var tab = CurrentStudyTab;
            StoryText(s5Title, ArenaText.IsChinese ? "研 习" : V5TitleKit.Spaced("STUDY"));
            StoryText(s5Where, tab != null ? $"{tab.roomName} · {tab.name}" : "");
            s5Where.color = S5RoomColour(tab?.key);
            float banner = Bp(B5BannerW), height = Bp(B5BannerH), gap = Bp(B5BannerGap);
            s5Title.fontSize = V5StoryKit.FontSize(50f);
            float titleW = V5StoryKit.WidthOf(s5Title, s5Title.text);
            float room = Mathf.Max(Bp(40f), banner - Bp(B5BannerEnds) * 2f - titleW - gap);
            B5PlaceLeft(s5Where, 0f, room, height);
            V5TitleKit.FitWidth(s5Where, V5StoryKit.FontSize(20f), V5StoryKit.FontSize(14f));
            float whereW = Mathf.Min(room, V5StoryKit.WidthOf(s5Where, s5Where.text));
            float left = (banner - titleW - gap - whereW) * .5f;
            B5PlaceLeft(s5Title, left, titleW, height);
            B5PlaceLeft(s5Where, left + titleW + gap, room, height);
        }

        void RefreshStudy5Wallet(V2Campaign c, bool art)
        {
            // The three currencies, then the void's two after a hairline once the Nash Prophet is beaten; a number that changes while the
            // window shows swells once.
            var set = c.art;
            s5WalletMedals[0].SetIcons(set ? set.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            s5WalletMedals[1].SetIcons(set ? set.alloy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Alloy));
            s5WalletMedals[2].SetIcons(set ? set.dark : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Dark));
            s5WalletMedals[3].SetIcons(set ? set.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            s5WalletMedals[4].SetIcons(set ? set.alloy : null, WhiteboxIcons.Look("voidalloy"));
            B5DarkIcon(s5WalletMedals[2].transform.Find("Hole/Icon"), art);
            bool voids = c.ProphetBeaten, settled = Time.frameCount - s5TickFrame <= 1;
            float walletH = Bp(B5WalletH), medal = Bp(B5WalletMedal), x = Bp(12f);
            for (int i = 0; i < 5; i++)
            {
                var number = s5WalletNumbers[i];
                bool shown = i < 3 || voids;
                SetShown(s5WalletMedals[i], shown);
                SetShown(number, shown);
                if (!shown) continue;
                int value = i switch { 0 => c.Energy, 1 => c.Alloy, 2 => c.Dark, 3 => c.VoidEnergy, _ => c.VoidAlloy };
                if (settled && s5WalletShown[i] >= 0 && s5WalletShown[i] != value) PulseOnce(number.transform);
                s5WalletShown[i] = value;
                string text = value.ToString();
                StoryText(number, text);
                if (i == 3)
                {
                    x += Bp(4f);
                    V5TitleKit.Place(s5WalletRule.rectTransform, x, (walletH - Bp(40f)) * .5f, 1.5f, Bp(40f));
                    x += Bp(10f);
                }
                else if (i > 0) x += Bp(6f);
                PlaceCentre(s5WalletMedals[i].Rect, new Vector2(0f, .5f), new Vector2(x + medal * .5f, 0f), Vector2.one * medal);
                x += medal + Bp(8f);
                // Never narrower than three digits, so the row does not shift as numbers grow and shrink.
                float w = Mathf.Max(V5StoryKit.WidthOf(number, text), DigitsWidth(number, "000"));
                PlaceSpan(number, x, w, walletH);
                x += w + Bp(10f);
            }
            SetShown(s5WalletRule, voids);
            s5Wallet.sizeDelta = new Vector2(x + Bp(16f), walletH);
            s5Wallet.anchoredPosition = new Vector2(-Bp(B5WalletBeside), -Bp(B5WalletTop));
        }

        void RefreshStudy5Close()
        {
            // Esc (the pad's B) always closes the window: the button and its key are always there.
            StoryText(s5CloseLabel, ArenaText.Pair("关闭", "Close"));
            s5CloseKey.SetText(s5PadKeys ? "B" : "Esc");
            float labelW = V5StoryKit.WidthOf(s5CloseLabel, s5CloseLabel.text), keyW = s5CloseKey.Rect.sizeDelta.x;
            float closeW = Bp(22f) + labelW + Bp(12f) + keyW + Bp(22f), closeH = Bp(B5CloseH);
            s5Close.Place(Bp(B5Width - B5CloseRight) - closeW, Bp(B5CloseTop), closeW, closeH);
            B5PlaceLeft(s5CloseLabel, Bp(22f), labelW, closeH);
            B5PlaceKey(s5CloseKey, Bp(22f) + labelW + Bp(12f));
        }

        void RefreshStudy5Foot()
        {
            // The keys as keycaps in one short row (the old window's long line of words is gone): choose, change tab, buy, close.
            bool pad = s5PadKeys;
            float x = Bp(20f), h = Bp(B5HintsH);
            int used = 0;
            for (int g = 0; g < 4; g++)
            {
                string[] caps = g switch
                {
                    0 => pad ? new[] { ArenaText.Pair("十字键", "D-pad") } : S5KeysChoose,
                    1 => pad ? S5PadTabs : S5KeysTabs,
                    2 => pad ? S5PadBuy : S5KeysBuy,
                    _ => pad ? S5PadClose : S5KeysClose,
                };
                string word = g switch
                {
                    0 => ArenaText.Pair("选择", "Choose"),
                    1 => ArenaText.Pair("换分类", "Change tab"),
                    2 => ArenaText.Pair("购买", "Buy"),
                    _ => ArenaText.Pair("关闭", "Close"),
                };
                if (g > 0) x += Bp(26f);
                foreach (string name in caps)
                {
                    var cap = s5Keys[used++];
                    SetShown(cap, true);
                    cap.SetText(name);
                    B5PlaceKey(cap, x);
                    x += cap.Rect.sizeDelta.x + Bp(8f);
                }
                var label = s5KeyWords[g];
                StoryText(label, word);
                float w = V5StoryKit.WidthOf(label, word);
                B5PlaceLeft(label, x, w, h);
                x += w;
            }
            for (int i = used; i < s5Keys.Count; i++) SetShown(s5Keys[i], false);
            float middle = B5Height - B5FootBottom - B5GoH * .5f;
            V5TitleKit.Place(s5Hints.Rect, Bp(B5Margin), Bp(middle - B5HintsH * .5f), x + Bp(20f), h);
        }

        // ───── The tabs (BuildStudyTabs) ─────

        void BuildStudyTabs5()
        {
            if (!s5Built) return;
            int n = studyData.Count, rooms = 0;
            string room = null;
            foreach (var data in studyData)
                if (data.roomName != room)
                {
                    room = data.roomName;
                    rooms++;
                }
            // The tabs keep their size while they all fit above the floor, and close up when there are many.
            float fixedH = rooms * S5RoomH + Mathf.Max(0, rooms - 1) * S5RoomGap, tabH = S5TabH, gap = S5TabGap;
            if (n > 0 && fixedH + n * tabH + (n - rooms) * gap > B5Floor - B5Top)
            {
                gap = S5TabMinGap;
                tabH = Mathf.Clamp((B5Floor - B5Top - fixedH - (n - rooms) * gap) / n, S5TabMinH, S5TabH);
            }
            float y = B5Top, columnLeft = Bp(B5Margin + 2f), columnRight = Bp(B5Margin + S5TabsW);
            int headers = 0;
            room = null;
            for (int i = 0; i < n; i++)
            {
                var data = studyData[i];
                if (data.roomName != room)
                {
                    if (room != null) y += S5RoomGap;
                    room = data.roomName;
                    // The room's name in its colour and a hairline to the column's edge.
                    var header = S5RoomAt(headers++);
                    SetShown(header.name, true);
                    SetShown(header.rule, true);
                    StoryText(header.name, room);
                    header.name.color = S5RoomColour(data.key);
                    float nameH = S5RoomH - 8f;
                    V5TitleKit.Place(header.name.rectTransform, columnLeft, Bp(y), Bp(S5TabsW - 2f), Bp(nameH));
                    float ruleLeft = columnLeft + Mathf.Min(Bp(S5TabsW - 40f), V5StoryKit.WidthOf(header.name, room)) + Bp(10f);
                    V5TitleKit.Place(header.rule.rectTransform, ruleLeft, Bp(y + nameH * .5f), Mathf.Max(0f, columnRight - ruleLeft), 1f);
                    y += S5RoomH;
                }
                else y += gap;
                var tab = S5TabAt(i);
                bool chosen = i == studyTab;
                SetShown(tab.plate, true);
                tab.plate.Place(Bp(B5Margin), Bp(y), Bp(S5TabsW), Bp(tabH));
                tab.plate.Chosen = chosen;
                tab.plate.Dim = data.locked;
                SetShown(tab.warm, chosen);
                SetShown(tab.pointer, chosen);
                StoryText(tab.name, data.name);
                tab.name.color = chosen ? V5TitleKit.Chosen : data.locked ? V5TitleKit.Ink3 : V5TitleKit.Ink;
                V5TitleKit.Place(tab.name.rectTransform, Bp(20f), 0f, Bp(S5TabsW - 40f), Bp(tabH));
                V5TitleKit.FitWidth(tab.name, V5StoryKit.FontSize(20f), V5StoryKit.FontSize(14f));
                y += tabH;
            }
            for (int i = headers; i < s5Rooms.Count; i++)
            {
                SetShown(s5Rooms[i].name, false);
                SetShown(s5Rooms[i].rule, false);
            }
            for (int i = n; i < s5Tabs.Count; i++) SetShown(s5Tabs[i].plate, false);
        }

        // ───── The tiles (BuildStudyTiles, StyleTiles) ─────

        /// <summary>One tile, drawn for its entry where the grid puts tile index (BuildStudyTiles has set the columns).</summary>
        void LayStudy5Tile(int index, StudyEntry entry)
        {
            var tile = S5TileAt(index);
            bool art = Art.Enabled, locked = entry.state == StudyState.Locked, owned = entry.state == StudyState.Owned;
            SetShown(tile.plate, true);
            tile.plate.Place((index % studyColumns) * (StudyTileW + StudyGapX), StudyPad + index / studyColumns * (StudyTileH + StudyGapY), StudyTileW, StudyTileH);
            tile.plate.HoverPitch = 1.08f - index % studyColumns * .03f;
            tile.plate.Dim = locked;
            tile.plate.Alpha = locked ? .6f : 1f;
            SetShown(tile.teal, owned);
            S5DressMedal(tile.medal, tile.mark, entry);
            StoryText(tile.title, entry.title);
            S5FitLines(tile.title, V5StoryKit.FontSize(18f), V5StoryKit.FontSize(13f));
            S5NumberLines(entry, out string first, out string second);
            SetShown(tile.now, first.Length > 0);
            SetShown(tile.after, second.Length > 0);
            StoryText(tile.now, first);
            StoryText(tile.after, second);
            V5TitleKit.FitWidth(tile.now, V5StoryKit.FontSize(15f), V5StoryKit.FontSize(11f));
            V5TitleKit.FitWidth(tile.after, V5StoryKit.FontSize(15f), V5StoryKit.FontSize(11f));
            SetShown(tile.done, owned);
            if (owned) StoryText(tile.done, ArenaText.Pair("✓ 已拥有", "✓ Owned"));
            if (locked || owned) HideStudy5Coins(tile.coins);
            else LayStudy5Coins(tile.coins, entry, Bp(14f), Bp(S5TileH - 12f - 26f), Bp(24f), Bp(26f), Bp(S5TileW - 28f), art);
        }

        void HideStudy5Tiles(int count)
        {
            for (int i = count; i < s5Tiles.Count; i++) SetShown(s5Tiles[i].plate, false);
        }

        void StyleTiles5()
        {
            var tab = CurrentStudyTab;
            bool art = Art.Enabled;
            int count = Mathf.Min(s5Tiles.Count, tab == null || tab.locked ? 0 : tab.entries.Count);
            for (int i = 0; i < count; i++)
            {
                var tile = s5Tiles[i];
                bool chosen = i == studySelected;
                tile.plate.Chosen = chosen;
                SetShown(tile.warm, chosen);
                SetShown(tile.tab, chosen);
                if (chosen) V5TitleKit.Dress(tile.tab, art ? V5TitleKit.Piece("hazard-tab") : null, art ? Color.white : V5TitleKit.Orange);
                tile.title.color = chosen ? V5TitleKit.Chosen : tab.entries[i].state == StudyState.Locked ? V5TitleKit.Ink3 : V5TitleKit.Ink;
            }
            // The chosen tile is drawn over its neighbours, so its tab and its rim show whole.
            if (studySelected >= 0 && studySelected < count) s5Tiles[studySelected].plate.transform.SetAsLastSibling();
            ShowStudyDetail();
        }

        // ───── The chosen tile in full (ShowStudyDetail) ─────

        void ShowStudyDetail5()
        {
            if (!s5Built) return;
            var tab = CurrentStudyTab;
            var entry = SelectedEntry;
            bool art = Art.Enabled;
            bool locked = entry == null || entry.state == StudyState.Locked, owned = !locked && entry.state == StudyState.Owned;
            SetShown(s5DetailMedal, entry != null);
            if (entry != null) S5DressMedal(s5DetailMedal, s5DetailMark, entry);
            StoryText(s5DetailTitle, entry?.title ?? tab?.name ?? "");
            S5FitLines(s5DetailTitle, V5StoryKit.FontSize(28f), V5StoryKit.FontSize(18f));
            SetShown(s5DetailDiamond, !locked);
            SetShown(s5DetailKind, !locked);
            if (!locked)
            {
                s5DetailDiamond.color = S5RarityColour(entry.rarity);
                StoryText(s5DetailKind, $"{ArenaTerms.Rarity(entry.rarity)} · {entry.category}");
                V5TitleKit.FitWidth(s5DetailKind, V5StoryKit.FontSize(15f), V5StoryKit.FontSize(11f));
            }

            // The numbers now and after, on one line; a number the purchase just raised swells once.
            float y = 134f;
            string numbers = locked ? "" : S5Numbers(entry);
            SetShown(s5DetailNumbers, numbers.Length > 0);
            if (numbers.Length > 0)
            {
                S5PlaceRow(s5DetailNumbers.rectTransform, Bp(24f), Bp(y), Bp(S5DetailW - 48f), Bp(40f));
                StoryText(s5DetailNumbers, numbers);
                V5TitleKit.FitWidth(s5DetailNumbers, V5StoryKit.FontSize(26f), V5StoryKit.FontSize(16f));
                if (entry.id == s5ShownId && entry.now != s5ShownNow && Time.frameCount - s5TickFrame <= 1) PulseOnce(s5DetailNumbers.transform);
                y += 50f;
            }
            s5ShownId = entry?.id;
            s5ShownNow = entry?.now;

            // Under a hairline: the price as icons and amounts, or the check of a tile owned.
            bool priced = !locked && !owned;
            SetShown(s5DetailRule, !locked);
            SetShown(s5PriceCaption, priced);
            SetShown(s5Done, owned);
            if (locked) HideStudy5Coins(s5DetailCoins);
            else
            {
                V5TitleKit.Place(s5DetailRule.rectTransform, Bp(24f), Bp(y + 4f), Bp(S5DetailW - 48f), 1f);
                y += 18f;
                if (priced)
                {
                    StoryText(s5PriceCaption, ArenaText.IsChinese ? V5TitleKit.Spaced("PRICE") + "   ·   价格" : V5TitleKit.Spaced("PRICE"));
                    V5TitleKit.Place(s5PriceCaption.rectTransform, Bp(24f), Bp(y), Bp(S5DetailW - 48f), Bp(18f));
                    y += 24f;
                    LayStudy5Coins(s5DetailCoins, entry, Bp(24f), Bp(y), Bp(34f), Bp(40f), Bp(S5DetailW - 48f), art);
                    y += 40f;
                }
                else
                {
                    HideStudy5Coins(s5DetailCoins);
                    StoryText(s5Done, ArenaText.Pair("✓ 已拥有", "✓ Owned"));
                    V5TitleKit.Place(s5Done.rectTransform, Bp(24f), Bp(y), Bp(S5DetailW - 48f), Bp(32f));
                    y += 32f;
                }
                y += 18f;
            }

            // What it does, down to the panel's foot (a tab not unlocked yet or with nothing in it: its note).
            StoryText(s5DetailBody, entry != null ? entry.detail ?? "" : tab?.locked == true ? tab.lockedNote : tab?.emptyNote ?? "");
            float bottom = B5Floor - B5Top - 24f;
            V5TitleKit.Place(s5DetailBody.rectTransform, Bp(24f), Bp(y), Bp(S5DetailW - 48f), Bp(Mathf.Max(40f, bottom - y)));
            S5FitLines(s5DetailBody, V5StoryKit.FontSize(17f), V5StoryKit.FontSize(13f));
            LayStudy5Go(entry, art);
        }

        /// <summary>The orange buy button at the foot's right with its key, for a tile that can be bought or could be once the stock allows;
        /// dimmed and not clickable while it cannot (F still answers with the refusal and its reason, as before).</summary>
        void LayStudy5Go(StudyEntry entry, bool art)
        {
            bool shown = entry != null && entry.buy != null && entry.state is StudyState.Available or StudyState.Unaffordable;
            SetShown(s5Go, shown);
            if (!shown) return;
            bool live = entry.state == StudyState.Available;
            s5Go.Dim = !live;
            s5Go.Button.interactable = live;
            StoryText(s5GoLabel, ArenaText.Pair("购买", "Buy"));
            s5GoLabel.color = art ? V5TitleKit.Dark : Color.white;
            s5GoKey.SetText(s5PadKeys ? "A" : "F");
            float h = Bp(B5GoH), padLeft = Bp(84f), padRight = Bp(26f);
            float labelW = V5StoryKit.WidthOf(s5GoLabel, s5GoLabel.text), keyW = s5GoKey.Rect.sizeDelta.x;
            float width = Mathf.Max(Bp(S5GoMinW), padLeft + labelW + Bp(16f) + keyW + padRight);
            s5Go.Place(Bp(B5Width - B5Margin) - width, Bp(B5Height - B5FootBottom - B5GoH), width, h);
            B5PlaceLeft(s5GoLabel, padLeft, labelW, h);
            B5PlaceKey(s5GoKey, width - padRight - keyW);
            SetShown(s5GoStripes, art);
            if (!art) return;
            float repeat = 14f * 1.41421f * B5Css;
            V5TitleKit.Place(s5GoStripes.rectTransform, Bp(16f), Bp(14f), Bp(48f), h - Bp(28f));
            s5GoStripes.uvRect = new Rect(0f, 0f, Bp(48f) / repeat, (h - Bp(28f)) / repeat);
        }

        // ───── Prices ─────

        /// <summary>A price as icons and amounts in a row from left (canvas units, in the coins' parent) in a row height tall from top: each
        /// currency's icon size across and its amount, red while the stock is short of it, grey when nothing is short but the tile cannot be
        /// bought for another reason (a broken room, full slots); a price that cannot be read as numbers (free) in words.</summary>
        void LayStudy5Coins(S5Coins coins, StudyEntry entry, float left, float top, float size, float height, float room, bool art)
        {
            int n = S5PriceOf(entry);
            bool anyShort = false;
            for (int k = 0; k < n; k++) anyShort |= s5Price[k].held < s5Price[k].amount;
            bool blocked = entry.state == StudyState.Unaffordable && !anyShort;
            float x = left;
            for (int k = 0; k < 3; k++)
            {
                bool shown = k < n;
                SetShown(coins.holes[k], shown);
                SetShown(coins.amounts[k], shown);
                if (!shown)
                {
                    SetShown(coins.rings[k], false);
                    continue;
                }
                var (which, amount, held) = s5Price[k];
                var sprite = S5CoinSprite(which, art);
                // With the art on the currency's picture is cut round; whitebox shows its icon whole.
                var round = coins.holes[k].GetComponent<Mask>();
                if (round.enabled != art) round.enabled = art;
                coins.holes[k].enabled = art;
                coins.icons[k].sprite = sprite;
                coins.icons[k].enabled = sprite;
                // The dark matter's picture has a wider empty margin: drawn larger so the three read as the same size (DarkIconScale).
                coins.icons[k].transform.localScale = Vector3.one * (art && which == 2 ? DarkIconScale : 1f);
                float y = top + (height - size) * .5f;
                V5TitleKit.Place(coins.holes[k].rectTransform, x, y, size, size);
                bool ringed = which >= 3 && sprite;
                SetShown(coins.rings[k], ringed);
                if (ringed) V5TitleKit.Place(coins.rings[k].rectTransform, x - size * .1f, y - size * .1f, size * 1.2f, size * 1.2f);
                if (sprite) x += size + Bp(6f);
                string text = sprite ? amount.ToString() : $"{S5CoinName(which)} {amount}";
                StoryText(coins.amounts[k], text);
                coins.amounts[k].color = held < amount ? B5Short : blocked ? V5TitleKit.Ink3 : S5CoinColour(which);
                float w = V5StoryKit.WidthOf(coins.amounts[k], text);
                V5TitleKit.Place(coins.amounts[k].rectTransform, x, top, w + 2f, height);
                x += w + Bp(14f);
            }
            bool words = n == 0 && !string.IsNullOrEmpty(entry.costText);
            SetShown(coins.words, words);
            if (!words) return;
            StoryText(coins.words, entry.costText);
            coins.words.color = V5TitleKit.Ink2;
            V5TitleKit.Place(coins.words.rectTransform, left, top, room, height);
        }

        static void HideStudy5Coins(S5Coins coins)
        {
            for (int k = 0; k < 3; k++)
            {
                SetShown(coins.holes[k], false);
                SetShown(coins.rings[k], false);
                SetShown(coins.amounts[k], false);
            }
            SetShown(coins.words, false);
        }

        /// <summary>The entry's price into s5Price, in the wallet's order (energy, alloy, dark matter, then the void's); how many to draw (at
        /// most three). Nothing for a tile owned or not unlocked.</summary>
        int S5PriceOf(StudyEntry entry)
        {
            s5Price.Clear();
            if (entry == null || entry.state is StudyState.Owned or StudyState.Locked) return 0;
            var c = campaign;
            if (entry.price is { } p)
            {
                if (p.energy > 0) s5Price.Add((0, p.energy, c.Energy));
                if (p.alloy > 0) s5Price.Add((1, p.alloy, c.Alloy));
                if (p.dark > 0) s5Price.Add((2, p.dark, c.Dark));
            }
            if (entry.voidEnergyCost > 0) s5Price.Add((3, entry.voidEnergyCost, c.VoidEnergy));
            if (entry.voidAlloyCost > 0) s5Price.Add((4, entry.voidAlloyCost, c.VoidAlloy));
            return Mathf.Min(3, s5Price.Count);
        }

        /// <summary>A currency's icon: the art's or the whitebox's; the void's two are the alien energy's and alloy's (the whitebox has a
        /// void alloy of its own).</summary>
        Sprite S5CoinSprite(int which, bool art)
        {
            if (which < 3) return CurrencySprite(which);
            if (!art) return which == 3 ? WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy) : WhiteboxIcons.Look("voidalloy");
            var set = campaign ? campaign.art : null;
            if (!set) return null;
            return which == 3 ? set.energy : set.alloy;
        }

        static string S5CoinName(int which) => which < 3 ? CurrencyLabel(which)
            : which == 3 ? ArenaText.Pair("虚空能量", "Void energy") : ArenaText.Pair("虚空合金", "Void alloy");

        /// <summary>A currency's number colour: steel for the alloy, violet for the energy, the dark matter and the void's two.</summary>
        static Color S5CoinColour(int which) => which == 1 ? V5TitleKit.Steel : V5HudKit.VioletHi;

        // ───── Small pieces ─────

        /// <summary>A tile's medal: its icon (the painted one with the art on, the whitebox's without) ringed in its rarity's colour, teal once
        /// owned; a tile not unlocked yet shows an empty medal with a question mark.</summary>
        static void S5DressMedal(V5HudMedal medal, Text mark, StudyEntry entry)
        {
            bool locked = entry == null || entry.state == StudyState.Locked, owned = !locked && entry.state == StudyState.Owned;
            medal.SetIcons(locked ? null : entry.icon, locked ? null : S5FlatIcon(entry));
            medal.SetRing(locked ? Color.clear : owned ? V5TitleKit.Teal : S5RarityColour(entry.rarity));
            medal.SetGlow(!locked && entry.rarity == CardRarity.Void ? V5StoryKit.VioletGlow : Color.clear);
            SetShown(mark, locked);
            if (locked) StoryText(mark, "？");
        }

        /// <summary>A rarity's colour on the dark plates: the void's own is near black, so a void tile takes the void's violet.</summary>
        static Color S5RarityColour(CardRarity rarity) => rarity == CardRarity.Void ? V5HudKit.Violet : ArenaRarity.ColorFor(rarity);

        /// <summary>A tile's whitebox icon, as StudyIconOf picks it with the art off: the whitebox's for the tile's kind, a plain grey square
        /// for a kind it does not know where the tile has a picture.</summary>
        static Sprite S5FlatIcon(StudyEntry entry)
        {
            if (entry == null) return null;
            var sprite = WhiteboxIcons.ForStudy(entry.id);
            return sprite ? sprite : entry.icon ? WhiteboxIcons.Unknown() : null;
        }

        /// <summary>A room's colour by its tab's key: violet for the energy research and the void, teal for the human rooms.</summary>
        static Color S5RoomColour(string key) =>
            key != null && (key.StartsWith("eng.", StringComparison.Ordinal) || key.StartsWith("void.", StringComparison.Ordinal)) ? V5HudKit.VioletHi : V5TitleKit.Teal;

        static string S5Arrow => $"<color=#{HexOf(V5TitleKit.Ink3)}>→</color> ";

        static string S5Gain(string after) => $"<color=#{HexOf(V5TitleKit.Gold)}>{after}</color>";

        /// <summary>A tile's two lines: the number now, then "→" and the number after it (in gold: what the purchase gives).</summary>
        static void S5NumberLines(StudyEntry entry, out string first, out string second)
        {
            first = second = "";
            if (entry == null || entry.state == StudyState.Locked) return;
            bool now = !string.IsNullOrEmpty(entry.now), after = !string.IsNullOrEmpty(entry.after);
            string gain = after ? S5Arrow + S5Gain(entry.after) : "";
            if (now)
            {
                first = entry.now;
                second = gain;
            }
            else first = gain;
        }

        /// <summary>The detail's line: "now  →  after", or what there is of it.</summary>
        static string S5Numbers(StudyEntry entry)
        {
            bool now = !string.IsNullOrEmpty(entry.now), after = !string.IsNullOrEmpty(entry.after);
            return now && after ? $"{entry.now}  {S5Arrow} {S5Gain(entry.after)}" : now ? entry.now : after ? S5Arrow + S5Gain(entry.after) : "";
        }

        /// <summary>A label of several lines at the largest size, from largest down to smallest, at which all its lines fit its box (no best
        /// fit: pitfall T-015); what still does not fit at the smallest is cut.</summary>
        static void S5FitLines(Text label, int largest, int smallest)
        {
            float height = label.rectTransform.rect.height;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            int size = largest;
            for (; size > smallest; size--)
            {
                label.fontSize = size;
                if (label.preferredHeight <= height + .5f) break;
            }
            label.fontSize = size;
            label.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>A rect left and top from its parent's top-left corner, its pivot on its left middle (a pulse swells it from there).</summary>
        static void S5PlaceRow(RectTransform rect, float left, float top, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(left, -(top + height * .5f));
            rect.sizeDelta = new Vector2(width, height);
        }
    }
}
