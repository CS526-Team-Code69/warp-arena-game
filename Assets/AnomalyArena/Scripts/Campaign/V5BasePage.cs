using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The base's two pages in the fifth interface pass (work/art-plans/style-1112/ui-f/workshop.html and rocket.html), built only when
    /// V5HudKit.On: the English Alpha keeps the page it was graded with (V2CampaignUi.BasePage.cs). Both stand on the base panel's
    /// full-screen backing, laid out as the 1920 x 1080 mock-up times the story layer's scale and shrunk to fit a screen that is not 16:9,
    /// and share the frame drawn here: the banner with the page's name and its line in gold, the wallet, the button to the other page, the
    /// close button while Esc leaves (a visit from the afternoon or the night), the keys, and the ways out of the visit
    /// (V2Campaign.BaseExits: the main one orange at the right, its line under it). The workshop (V5WorkshopPage.cs) and the rocket page
    /// (V5RocketPage.cs, V5BaseShip.cs) fill the middle. Everything is read and bought through the campaign's interface for the two pages
    /// (V2Campaign.Redesign.cs, BuildStudy and StudyEntry.buy), and the page is drawn again after each purchase. Nothing of the columns
    /// reaches below B5Floor, so a subtitle over the page (its foot at BaseSubtitleBottom) never covers them; the keys and the ways out
    /// stand along the foot, under the subtitle's strip. A card's details show beside it while the pointer is on it. The keys and a pad
    /// move a focus over the page's buttons (the arrows, the move keys, the pad's cross or stick), the interact key or the pad's A presses
    /// the focused one, LB and RB change the research tab, and Esc or B closes the page while it can be closed. Every piece follows the
    /// art switch: the mock-up's pictures with the art on, flat fills with the thin edge and the whitebox icons without.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The pages in mock-up pixels (1920 x 1080). Nothing of the columns reaches below B5Floor: a subtitle over the page stands between
        // it and the foot.
        const float B5Css = V5HudKit.StoryCss, B5Width = 1920f, B5Height = 1080f, B5Margin = 36f, B5Top = 170f, B5Floor = 886f;
        // The banner (600 x 111, its painted ends B5BannerEnds wide): the page's name in 52-pixel letters, its line in 22, 24 apart.
        const float B5BannerTop = 26f, B5BannerW = 600f, B5BannerH = 111f, B5BannerEnds = 78f, B5BannerGap = 24f, B5TitlePx = 52f, B5LinePx = 22f;
        // The top right, every piece centred B5HeadMiddle down: the wallet B5WalletH tall, the button to the other page and the close
        // button B5HeadButtonH tall, B5HeadGap apart, the last one B5Margin from the right edge. The void's two currencies in a plate of
        // their own B5VoidWalletH tall, B5VoidWalletGap under the wallet and level with its right edge (above the columns' heads).
        const float B5HeadMiddle = 80f, B5WalletH = 78f, B5HeadButtonH = 64f, B5HeadGap = 18f, B5VoidWalletH = 40f, B5VoidWalletGap = 6f;
        // The foot: the keys' plate and the ways out B5FootH tall from B5FootTop, B5WayGap apart; the main way out at least B5GoMinW wide,
        // its line (B5NotePx letters) B5NoteGap under it.
        const float B5FootTop = 964f, B5FootH = 62f, B5WayGap = 22f, B5GoMinW = 210f, B5NoteGap = 10f, B5NotePx = 18f;
        // A card's details beside it: B5TipW wide.
        const float B5TipW = 430f;
        // Whether a card shows its details beside it while the pointer is on it.
        const bool B5Details = true;

        // Colours of the mock-ups the kits do not name: the page's abyss, a price the stock is short of, a chip's dark backing, the go
        // button's stripes, a locked row's grey, the check of a part done, the white rarity's bar.
        static readonly Color B5Abyss = V5TitleKit.Rgb(7, 10, 20), B5Short = V5TitleKit.Rgb(255, 107, 122), B5ChipBack = V5TitleKit.Rgb(6, 12, 24, .86f),
            B5HazardInk = V5TitleKit.Rgb(27, 14, 6, .55f), B5Lock = V5TitleKit.Rgb(107, 113, 131), B5Ok = V5TitleKit.Rgb(111, 211, 111),
            B5CommonBar = V5TitleKit.Rgb(217, 221, 230), B5DiamondRim = V5TitleKit.Rgb(200, 210, 225, .45f), B5DiamondOff = V5TitleKit.Rgb(26, 31, 43);

        /// <summary>One cell of the wallet: its medal, its name (the void's two have none) and its amount.</summary>
        sealed class B5Purse
        {
            public V5HudMedal medal;
            public Text name, amount;
            public int shown = -1;
        }

        /// <summary>One way out of the visit along the foot: its plate, its words, the line under it and what it does.</summary>
        sealed class B5Way
        {
            public V5Plate plate;
            public Text label, note;
            public Action act;
            public bool go;
        }

        /// <summary>A price drawn as up to three coins: each a round icon (cut round with the art on; a violet ring for the void's
        /// currencies) and its amount.</summary>
        sealed class B5Coins
        {
            public readonly Image[] holes = new Image[3], icons = new Image[3], rings = new Image[3];
            public readonly Text[] amounts = new Text[3];
            public float numberPx;
        }

        /// <summary>A part of the page the keys can focus: its id, its rect, what pressing it does (false: refused) and how it shows the
        /// focus.</summary>
        sealed class B5Focus
        {
            public string id;
            public RectTransform rect;
            public Func<bool> press;
            public Action<bool> mark;
        }

        bool b5Built, b5PadKeys, b5StickHeld;
        bool? b5Art, b5Chinese;
        Font b5Font;
        int b5TickFrame = -10, b5Version;
        float b5OpenedAt;
        V2Campaign.BaseScreen b5Screen;
        RectTransform b5Page, b5Wallet, b5VoidWallet, b5WorkshopRoot, b5RocketRoot, b5Tip, b5TipOver;
        RawImage b5Backdrop;
        Image b5Banner;
        Text b5Title, b5When, b5SwitchLabel, b5SwitchArrow, b5CloseLabel, b5TipTitle, b5TipBody;
        V5Plate b5Switch, b5Close;
        V5HudMedal b5SwitchMedal;
        V5HudKey b5CloseKey;
        V5HudPlate b5Hints;
        CanvasGroup b5TipFade;
        string b5FocusId;
        readonly V5HudKey[] b5HintKeys = new V5HudKey[2];
        readonly Text[] b5HintWords = new Text[2];
        readonly List<B5Way> b5Ways = new();
        readonly List<B5Focus> b5Focus = new();
        readonly B5Purse[] b5Purse = new B5Purse[6];
        // The price being drawn: which currency (0 alien energy, 1 alloy, 2 dark matter, 3 void energy, 4 void alloy), how much, how much is held.
        readonly List<(int which, int amount, int held)> b5Price = new();
        static readonly string[] B5PurseNames = { "Energy", "Alloy", "Dark", "Lamps", "VoidEnergy", "VoidAlloy" };
        static Texture2D b5BackdropTexture;

        /// <summary>Canvas units of so many mock-up pixels.</summary>
        static float Bp(float px) => px * B5Css;

        /// <summary>The title's plates (V5Plate) take their corner cut in pixels of the title's scale: this many of the page's.</summary>
        static float B5Cut(float px) => px * B5Css / V5TitleKit.Css;

        /// <summary>A rect placed in mock-up pixels from its parent's top-left corner.</summary>
        static void B5Rect(RectTransform rect, float left, float top, float width, float height) => V5TitleKit.Place(rect, Bp(left), Bp(top), Bp(width), Bp(height));

        static void B5Place(V5Plate plate, float left, float top, float width, float height) => plate.Place(Bp(left), Bp(top), Bp(width), Bp(height));

        /// <summary>How wide a label draws this text on one line at its size now, in mock-up pixels.</summary>
        static float B5W(Text label, string text) => V5StoryKit.WidthOf(label, text) / B5Css;

        // ───── Building (BuildBasePanel, when V5HudKit.On) ─────

        void BuildBase5()
        {
            // The old page stays built and hidden; the two pages are drawn instead.
            basePage.gameObject.SetActive(false);
            b5Backdrop = V5TitleKit.Raw(baseScreen, "Backdrop", B5BackdropTexture, Color.white);
            V5TitleKit.Stretch(b5Backdrop.rectTransform);
            b5Backdrop.uvRect = new Rect(.25f, .25f, .5f, .5f);
            b5Page = V5TitleKit.Rect(baseScreen, "Page5");
            V5TitleKit.Centre(b5Page, new Vector2(Bp(B5Width), Bp(B5Height)));
            b5Page.gameObject.AddComponent<V5BaseTicker>().Tick = TickBase5;
            b5WorkshopRoot = V5TitleKit.Rect(b5Page, "Workshop");
            V5TitleKit.Stretch(b5WorkshopRoot);
            b5RocketRoot = V5TitleKit.Rect(b5Page, "Rocket");
            V5TitleKit.Stretch(b5RocketRoot);
            BuildBase5Head();
            BuildWorkshop5(b5WorkshopRoot);
            BuildRocket5(b5RocketRoot);
            BuildBase5Foot();
            BuildBase5Tip();
            b5Built = true;
        }

        void BuildBase5Head()
        {
            b5Banner = V5TitleKit.Picture(b5Page, "Banner", null, Color.white);
            b5Banner.gameObject.AddComponent<V5StoryBannerFrame>();
            B5Rect(b5Banner.rectTransform, B5Margin, B5BannerTop, B5BannerW, B5BannerH);
            b5Title = V5StoryKit.Label(b5Banner.transform, "Title", B5TitlePx, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(b5Title);
            b5When = V5StoryKit.Label(b5Banner.transform, "When", B5LinePx, V5TitleKit.Gold, TextAnchor.MiddleLeft, true);

            b5Wallet = V5TitleKit.Rect(b5Page, "Wallet");
            V5TitleKit.Stretch(V5HudKit.Plate(b5Wallet, "Plate", V5HudKit.PlateLook.Plain, 12f, B5Css).Rect);
            b5VoidWallet = V5TitleKit.Rect(b5Page, "VoidWallet");
            V5TitleKit.Stretch(V5HudKit.Plate(b5VoidWallet, "Plate", V5HudKit.PlateLook.Plain, 10f, B5Css).Rect);
            for (int i = 0; i < b5Purse.Length; i++)
            {
                var purse = b5Purse[i] = new B5Purse();
                var holder = i >= 4 ? b5VoidWallet : b5Wallet;
                purse.medal = V5HudKit.Medal(holder, B5PurseNames[i] + "Medal", Bp(56f));
                purse.name = V5StoryKit.Label(holder, B5PurseNames[i] + "Name", 20f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
                purse.amount = V5StoryKit.Label(holder, B5PurseNames[i] + "Amount", 34f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            }
            b5Purse[0].medal.SetRing(ResourceColours.Energy);
            b5Purse[1].medal.SetRing(ResourceColours.Alloy);
            b5Purse[2].medal.SetGlow(V5StoryKit.VioletGlow);
            b5Purse[3].medal.SetGlow(V5StoryKit.Amber);
            for (int i = 4; i < 6; i++)
            {
                b5Purse[i].medal.SetRing(V5HudKit.Violet);
                b5Purse[i].medal.SetGlow(V5StoryKit.VioletGlow);
            }

            b5Switch = B5Button(b5Page, "OtherPage", V5Plate.Look.Plate, () => SwitchBase5());
            b5SwitchMedal = V5HudKit.Medal(b5Switch.transform, "Medal", Bp(46f));
            b5SwitchLabel = V5StoryKit.Label(b5Switch.transform, "Label", 22f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            b5SwitchArrow = V5StoryKit.Label(b5Switch.transform, "Arrow", 28f, V5TitleKit.Steel, TextAnchor.MiddleLeft, true);

            b5Close = B5Button(b5Page, "Close", V5Plate.Look.Plate, CloseBase5Button);
            b5CloseLabel = V5StoryKit.Label(b5Close.transform, "Label", 22f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            b5CloseKey = V5HudKit.Key(b5Close.transform, "Key", "Esc", false, B5Css, 30f, 16f);
        }

        void BuildBase5Foot()
        {
            b5Hints = V5HudKit.Plate(b5Page, "Keys", V5HudKit.PlateLook.Plain, 10f, B5Css);
            for (int i = 0; i < 2; i++)
            {
                b5HintKeys[i] = V5HudKit.Key(b5Hints.transform, "Key" + (i + 1), "", false, B5Css, 32f, 18f);
                b5HintWords[i] = V5StoryKit.Label(b5Hints.transform, "Word" + (i + 1), 20f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            }
        }

        /// <summary>A card's details beside it: a plate over everything (the base tour's dim included) that takes no click, with the card's
        /// name and what it does.</summary>
        void BuildBase5Tip()
        {
            b5Tip = V5TitleKit.Rect(root, "BaseDetails");
            b5Tip.anchorMin = b5Tip.anchorMax = b5Tip.pivot = Vector2.zero;
            b5TipFade = b5Tip.gameObject.AddComponent<CanvasGroup>();
            b5TipFade.blocksRaycasts = false;
            b5TipFade.interactable = false;
            V5TitleKit.Stretch(V5HudKit.Plate(b5Tip, "Plate", V5HudKit.PlateLook.Plain, 12f, B5Css).Rect);
            b5TipTitle = V5StoryKit.Label(b5Tip, "Title", 22f, V5TitleKit.Ink, TextAnchor.UpperLeft, true);
            b5TipBody = V5StoryKit.Label(b5Tip, "Body", 18f, V5TitleKit.Ink2, TextAnchor.UpperLeft);
            foreach (var label in new[] { b5TipTitle, b5TipBody })
            {
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Overflow;
                var rect = label.rectTransform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            }
            b5TipBody.lineSpacing = 1.15f;
            b5Tip.gameObject.SetActive(false);
        }

        /// <summary>A button of the page (a plate the keys can focus too): it lets go of the keyboard's selection once clicked, so Space and
        /// Enter, which the UI would read as a second click, do nothing here.</summary>
        static V5Plate B5Button(Transform parent, string name, V5Plate.Look look, Action click)
        {
            var plate = V5Plate.Make(parent, name, look, B5Cut(10f), () =>
            {
                B5Deselect();
                click?.Invoke();
            }, look == V5Plate.Look.Go);
            plate.Button.navigation = new Navigation { mode = Navigation.Mode.None };
            return plate;
        }

        /// <summary>The way out at index (counted from the right), made when first needed and reused.</summary>
        B5Way B5WayAt(int index)
        {
            while (b5Ways.Count <= index)
            {
                var way = new B5Way();
                int n = b5Ways.Count;
                way.plate = B5Button(b5Page, "Way" + (n + 1), V5Plate.Look.Plate, () => ClickBase5Way(way));
                way.plate.HoverPitch = 1f - n * .03f;
                way.label = V5StoryKit.Label(way.plate.transform, "Label", 22f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(way.label.rectTransform);
                way.note = V5StoryKit.Label(way.plate.transform, "Note", B5NotePx, V5TitleKit.Ink2, TextAnchor.MiddleCenter);
                var note = way.note.rectTransform;
                note.anchorMin = new Vector2(0f, 0f);
                note.anchorMax = new Vector2(1f, 0f);
                note.pivot = new Vector2(.5f, 1f);
                note.anchoredPosition = new Vector2(0f, -Bp(B5NoteGap));
                note.sizeDelta = new Vector2(0f, Bp(B5NotePx * 1.3f));
                b5Ways.Add(way);
            }
            return b5Ways[index];
        }

        // ───── Opening, switching, closing (ShowBase, BaseScreenRequested, HideBase) ─────

        /// <summary>The base opens (ShowBase): on the page the campaign asks for, with nothing focused.</summary>
        void OpenBase5(V2Campaign.BaseScreen screen)
        {
            b5Screen = screen;
            b5OpenedAt = Time.unscaledTime;
            b5FocusId = null;
            B5HideTip();
            OpenWorkshop5();
        }

        /// <summary>The campaign asks for one of the two pages (V2Campaign.BaseScreenRequested): the base door and the ship open the
        /// visit on their page through ShowBase; this switches between the two while the visit is open.</summary>
        void OnBaseScreenRequested(V2Campaign.BaseScreen screen)
        {
            if (!b5Built || !BaseOpen || b5Screen == screen) return;
            b5Screen = screen;
            b5FocusId = null;
            B5HideTip();
            V5UiSound.Play(V5UiSound.Ui.Panel);
            RefreshBase5(true);
        }

        /// <summary>The button at the top right: the other page of the same visit.</summary>
        void SwitchBase5()
        {
            if (!BaseOpen || !campaign) return;
            campaign.RequestBaseScreen(b5Screen == V2Campaign.BaseScreen.Workshop ? V2Campaign.BaseScreen.Rocket : V2Campaign.BaseScreen.Workshop);
        }

        /// <summary>The base closes (HideBase): the details and the focus go with it.</summary>
        void CloseBase5()
        {
            B5HideTip();
            b5FocusId = null;
        }

        /// <summary>The close button does what Esc does on the page (V2CampaignUi.Update): back out to where the visit came from.</summary>
        void CloseBase5Button()
        {
            if (!B5CanClose(campaign) || BaseTourRunning) return;
            V5UiSound.Play(V5UiSound.Ui.Back);
            campaign.LeaveBase();
        }

        /// <summary>Esc leaves the page now (a visit from the afternoon or the night): the close button and its key hint show only then.</summary>
        static bool B5CanClose(V2Campaign c) => c && c.Stage == CampaignStage.Base && !c.BaseAtDawn && c.NightTimeLeft(out _);

        void ClickBase5Way(B5Way way)
        {
            if (way.act == null || !BaseOpen) return;
            V5UiSound.Play(way.go ? V5UiSound.Ui.Start : V5UiSound.Ui.Press);
            way.act();
        }

        /// <summary>A button of either page: the purchase or the action, the refusal sound when it was refused, then the page drawn again
        /// (a purchase that goes through makes the sound of what it changed: CampaignAudio watches the base).</summary>
        void B5Act(Func<bool> action)
        {
            if (action == null || !BaseOpen) return;
            V5UiSound.Play(V5UiSound.Ui.Press, 1f, .7f);
            if (!action()) PlayRefused();
            if (BaseOpen) RefreshBase5(true);
        }

        /// <summary>A plate let go of the keyboard's selection once clicked: Space and Enter, which the UI would read as a second click,
        /// do nothing on this page.</summary>
        static void B5Deselect()
        {
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        }

        /// <summary>The ship's live view draws only while the rocket page (on the old page, the launch bay's tab) is open.</summary>
        bool ShipViewWanted => BaseOpen && (b5Built ? b5Screen == V2Campaign.BaseScreen.Rocket : tab == CampaignRoom.Ship);

        // ───── The frame ─────

        void TickBase5()
        {
            if (!b5Built) return;
            b5TickFrame = Time.frameCount;
            // The page keeps the mock-up's shape and shrinks to fit a screen narrower or shorter than 16:9.
            var size = root.rect.size;
            float scale = Mathf.Min(1f, Mathf.Min(size.x / Bp(B5Width), size.y / Bp(B5Height)));
            if (scale > 0f && !Mathf.Approximately(b5Page.localScale.x, scale)) b5Page.localScale = new Vector3(scale, scale, 1f);
            if (b5Art != Art.Enabled || b5Chinese != ArenaText.IsChinese || b5Font != V5TitleKit.Font) RefreshBase5(true);
            if (b5Screen == V2Campaign.BaseScreen.Rocket) TickRocket5();
            TickNudges5(); // the lamp and fortify buttons breathe while wanted (V2CampaignUi.Nudges.cs)
            PlaceB5Tip();
            TickBase5Keys();
        }

        /// <summary>The keys and a pad on the page, unless something else holds them (the base tour, a question, the settings, the codex)
        /// or the page has only just opened (the key that opened it does not act in it).</summary>
        void TickBase5Keys()
        {
            if (!BaseOpen || StudyOpen || ConfirmOpen || BaseTourRunning || V2Tutorial.HoldsInput || ArenaSettingsPanel.HoldsInput || ArenaCodex.HoldsInput) return;
            if (gm && gm.RunPhase != ArenaUiState.Base) return;
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            // The keys' hints name the pad's buttons while the pad is in use, the keyboard's once a key is pressed or the mouse moves.
            bool padNow = b5PadKeys;
            if (pad != null && (pad.dpad.ReadValue().sqrMagnitude > .25f || pad.leftStick.ReadValue().sqrMagnitude > .36f || pad.buttonSouth.wasPressedThisFrame
                    || pad.buttonEast.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame))
                padNow = true;
            else if (keys != null && keys.anyKey.wasPressedThisFrame || Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 4f)
                padNow = false;
            if (padNow != b5PadKeys)
            {
                b5PadKeys = padNow;
                RefreshBase5(true);
            }
            if (Time.unscaledTime - b5OpenedAt < .2f) return;
            var step = Vector2.zero;
            bool press = false, close = false;
            int tabStep = 0;
            if (keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame || ArenaKeys.Pressed(GameAction.MoveUp)) step.y += 1f;
                if (keys.downArrowKey.wasPressedThisFrame || ArenaKeys.Pressed(GameAction.MoveDown)) step.y -= 1f;
                if (keys.leftArrowKey.wasPressedThisFrame || ArenaKeys.Pressed(GameAction.MoveLeft)) step.x -= 1f;
                if (keys.rightArrowKey.wasPressedThisFrame || ArenaKeys.Pressed(GameAction.MoveRight)) step.x += 1f;
                press |= ArenaKeys.Pressed(GameAction.Interact);
            }
            if (pad != null)
            {
                if (pad.dpad.up.wasPressedThisFrame) step.y += 1f;
                if (pad.dpad.down.wasPressedThisFrame) step.y -= 1f;
                if (pad.dpad.left.wasPressedThisFrame) step.x -= 1f;
                if (pad.dpad.right.wasPressedThisFrame) step.x += 1f;
                // The stick moves one step per push: it has to come back toward the centre before the next.
                var stick = pad.leftStick.ReadValue();
                if (!b5StickHeld && stick.magnitude > .6f)
                {
                    b5StickHeld = true;
                    if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y)) step.x += Mathf.Sign(stick.x);
                    else step.y += Mathf.Sign(stick.y);
                }
                else if (stick.magnitude < .3f) b5StickHeld = false;
                press |= pad.buttonSouth.wasPressedThisFrame;
                close |= pad.buttonEast.wasPressedThisFrame;
                if (pad.leftShoulder.wasPressedThisFrame) tabStep--;
                if (pad.rightShoulder.wasPressedThisFrame) tabStep++;
            }
            if (close)
            {
                CloseBase5Button();
                return;
            }
            if (tabStep != 0 && b5Screen == V2Campaign.BaseScreen.Workshop) StepResearchTab5(tabStep);
            else if (step != Vector2.zero) MoveB5Focus(step);
            if (press) PressB5Focus();
        }

        // ───── Refreshing (RefreshBasePanel: on opening, after each purchase, and whenever what the page shows changed) ─────

        void RefreshBase5(bool rebuild)
        {
            if (!b5Built || !campaign || !campaign.Running) return;
            int version = B5Version();
            if (!rebuild && version == b5Version) return;
            b5Version = version;
            var c = campaign;
            bool art = Art.Enabled, workshop = b5Screen == V2Campaign.BaseScreen.Workshop, canClose = B5CanClose(c);
            b5Art = art;
            b5Chinese = ArenaText.IsChinese;
            b5Font = V5TitleKit.Font;
            if (baseScreen.TryGetComponent<Image>(out var back)) back.color = art ? B5Abyss : PageBack;
            b5Backdrop.enabled = art;
            SetShown(b5WorkshopRoot, workshop);
            SetShown(b5RocketRoot, !workshop);
            foreach (var focus in b5Focus) focus.mark(false);
            b5Focus.Clear();
            float walletLeft = RefreshBase5Head(c, art, workshop, canClose);
            if (workshop) RefreshWorkshop5(c, art, walletLeft);
            else RefreshRocket5(c, art);
            RefreshBase5Foot(c, art, canClose);
            B5MarkFocus();
            if (b5TipOver && !b5TipOver.gameObject.activeInHierarchy) B5HideTip();
        }

        /// <summary>A number that changes whenever what the page shows does: the page, the stock, what was bought, the day, the hands,
        /// the research tab, the art switch, the language and the keys' hints.</summary>
        int B5Version()
        {
            var c = campaign;
            unchecked
            {
                int h = (int)b5Screen * 7919 + c.Alloy * 31 + c.Energy * 131 + c.Dark * 521 + c.VoidEnergy * 37 + c.VoidAlloy * 41 + c.LampStock * 23 + c.Fuel * 17;
                h = h * 31 + c.FortifyLevel * 3 + c.PurchaseCount * 7 + c.Day * 11 + c.TeamMk * 13 + c.ArmorLevel * 19 + c.HomeStepsLeft;
                h = h * 4 + (B5CanClose(c) ? 1 : 0) + (c.BaseFromDusk ? 2 : 0);
                if (gm && gm.runWeapons)
                {
                    foreach (var w in gm.runWeapons.Hand) h = h * 37 + (w == null ? 0 : 1 + (int)w.form * 2 + (w.revealed ? 1 : 0));
                    foreach (var w in gm.runWeapons.Auto) h = h * 37 + (w == null ? 0 : 1 + (int)w.form);
                }
                h = h * 31 + (w5TabKey?.GetHashCode() ?? 0);
                h = h * 2 + (c.ProphetBeaten ? 1 : 0);
                h = h * 2 + (Art.Enabled ? 1 : 0);
                h = h * 2 + (ArenaText.IsChinese ? 1 : 0);
                h = h * 2 + (b5PadKeys ? 1 : 0);
                return h;
            }
        }

        /// <summary>The top row: the banner, then from the right the close button, the button to the other page and the wallet. Returns
        /// where the wallet begins (mock-up pixels from the left), for the workshop's fortification between it and the banner.</summary>
        float RefreshBase5Head(V2Campaign c, bool art, bool workshop, bool canClose)
        {
            // The banner: the page's name and its line (when, on the workshop; the steps still to go home, on the rocket page), centred
            // between the banner's painted ends.
            bool night = !c.BaseAfterTutorial && !c.BaseFromDusk;
            StoryText(b5Title, workshop ? ArenaText.Pair("整 备", "WORKSHOP") : ArenaText.Pair("火 箭", "ROCKET"));
            StoryText(b5When, workshop ? B5When(c) : B5HomeLine(c));
            b5When.color = workshop && night ? V5TitleKit.Moon : V5TitleKit.Gold;
            b5Title.fontSize = V5StoryKit.FontSize(B5TitlePx);
            b5When.fontSize = V5StoryKit.FontSize(B5LinePx);
            float inside = B5BannerW - B5BannerEnds * 2f;
            float titleW = Mathf.Min(B5W(b5Title, b5Title.text), inside * .6f);
            float room = Mathf.Max(40f, inside - titleW - B5BannerGap);
            B5PlaceLeft(b5When, 0f, Bp(room), Bp(B5BannerH));
            V5TitleKit.FitWidth(b5When, V5StoryKit.FontSize(B5LinePx), V5StoryKit.FontSize(15f));
            float lineW = Mathf.Min(room, B5W(b5When, b5When.text));
            float left = (B5BannerW - titleW - B5BannerGap - lineW) * .5f;
            B5PlaceLeft(b5Title, Bp(left), Bp(titleW), Bp(B5BannerH));
            V5TitleKit.FitWidth(b5Title, V5StoryKit.FontSize(B5TitlePx), V5StoryKit.FontSize(34f));
            B5PlaceLeft(b5When, Bp(left + titleW + B5BannerGap), Bp(room), Bp(B5BannerH));

            float right = B5Width - B5Margin, top = B5HeadMiddle - B5HeadButtonH * .5f;
            // The close button with its key, only while Esc leaves.
            SetShown(b5Close, canClose);
            if (canClose)
            {
                StoryText(b5CloseLabel, ArenaText.Pair("关闭", "Close"));
                b5CloseKey.SetText(b5PadKeys ? "B" : "Esc");
                b5CloseLabel.fontSize = V5StoryKit.FontSize(22f);
                float labelW = B5W(b5CloseLabel, b5CloseLabel.text), keyW = b5CloseKey.Rect.sizeDelta.x / B5Css;
                float width = 24f + labelW + 12f + keyW + 20f;
                B5Place(b5Close, right - width, top, width, B5HeadButtonH);
                B5PlaceLeft(b5CloseLabel, Bp(24f), Bp(labelW), Bp(B5HeadButtonH));
                B5PlaceKey(b5CloseKey, Bp(24f + labelW + 12f));
                B5AddFocus("h.close", b5Close);
                right -= width + B5HeadGap;
            }

            // The button to the other page: its medal, its name and "›".
            var set = c.art;
            b5SwitchMedal.SetIcons(workshop ? set ? set.shipBay : null : set ? set.armament : null,
                WhiteboxIcons.Room(workshop ? CampaignRoom.Ship : CampaignRoom.Armament));
            b5SwitchMedal.SetRing(workshop ? V5TitleKit.Steel : Color.clear);
            StoryText(b5SwitchLabel, workshop ? ArenaText.Pair("火箭", "Rocket") : ArenaText.Pair("整备", "Workshop"));
            StoryText(b5SwitchArrow, "›");
            b5SwitchArrow.color = workshop ? V5TitleKit.Steel : V5TitleKit.Ink2;
            b5SwitchLabel.fontSize = V5StoryKit.FontSize(22f);
            float medal = 46f, nameW = B5W(b5SwitchLabel, b5SwitchLabel.text), arrowW = B5W(b5SwitchArrow, "›");
            float switchW = 10f + medal + 14f + nameW + 6f + arrowW + 22f;
            B5Place(b5Switch, right - switchW, top, switchW, B5HeadButtonH);
            PlaceCentre(b5SwitchMedal.Rect, new Vector2(0f, .5f), new Vector2(Bp(10f + medal * .5f), 0f), Vector2.one * Bp(medal));
            B5PlaceLeft(b5SwitchLabel, Bp(10f + medal + 14f), Bp(nameW), Bp(B5HeadButtonH));
            B5PlaceLeft(b5SwitchArrow, Bp(10f + medal + 14f + nameW + 6f), Bp(arrowW), Bp(B5HeadButtonH));
            B5AddFocus("h.switch", b5Switch);
            right -= switchW + B5HeadGap;

            float walletW = RefreshBase5Wallet(c, art, workshop);
            B5Rect(b5Wallet, right - walletW, B5HeadMiddle - B5WalletH * .5f, walletW, B5WalletH);
            // The void's two currencies, once the Nash Prophet is beaten and only on the workshop (its void tab spends them).
            bool voids = workshop && c.ProphetBeaten;
            SetShown(b5VoidWallet, voids);
            if (voids)
            {
                float voidW = RefreshBase5VoidWallet(c);
                B5Rect(b5VoidWallet, right - voidW, B5HeadMiddle + B5WalletH * .5f + B5VoidWalletGap, voidW, B5VoidWalletH);
            }
            return right - walletW;
        }

        /// <summary>The banner's line on the workshop: the day and its part, gold by day and moonlight blue at night.</summary>
        static string B5When(V2Campaign c) => c.BaseAfterTutorial ? ArenaText.Pair("第 0 天", "DAY 0")
            : c.BaseFromDusk ? ArenaText.Pair($"第 {c.Day} 天 · 下午", $"DAY {c.Day} · AFTERNOON")
            : ArenaText.Pair($"第 {c.Day} 天 · 夜晚", $"DAY {c.Day} · NIGHT");

        /// <summary>The banner's line on the rocket page: how many of the four steps home are left (V2Campaign.HomeStepsLeft).</summary>
        static string B5HomeLine(V2Campaign c)
        {
            int left = c.HomeStepsLeft;
            return left <= 0 ? ArenaText.Pair("可以点火", "READY TO IGNITE")
                : ArenaText.Pair($"回家还差 {left} 步", left == 1 ? "1 STEP FROM HOME" : $"{left} STEPS FROM HOME");
        }

        /// <summary>The wallet: on the workshop the alloy and the alien energy, on the rocket page the alloy, the dark matter and the lamps;
        /// each a medal, its name and its amount in its own colour. A number that changes while the page shows swells once. Returns the
        /// wallet's width in mock-up pixels.</summary>
        float RefreshBase5Wallet(V2Campaign c, bool art, bool workshop)
        {
            var set = c.art;
            b5Purse[0].medal.SetIcons(set ? set.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            b5Purse[1].medal.SetIcons(set ? set.alloy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Alloy));
            b5Purse[2].medal.SetIcons(set ? set.dark : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Dark));
            b5Purse[3].medal.SetIcons(set ? set.lamp : null, V5StoryKit.LampIcon);
            b5Purse[4].medal.SetIcons(set ? set.energy : null, WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy));
            b5Purse[5].medal.SetIcons(set ? set.alloy : null, WhiteboxIcons.Look("voidalloy"));
            B5DarkIcon(b5Purse[2].medal.transform.Find("Hole/Icon"), art);
            int[] shown = workshop ? new[] { 1, 0 } : new[] { 1, 2, 3 };
            int[] values = { c.Energy, c.Alloy, c.Dark, c.LampStock, c.VoidEnergy, c.VoidAlloy };
            float medal = workshop ? 56f : 52f, namePx = workshop ? 20f : 19f, numberPx = workshop ? 34f : 32f;
            bool settled = Time.frameCount - b5TickFrame <= 1;
            float x = 14f;
            for (int i = 0; i < 4; i++)
            {
                var purse = b5Purse[i];
                bool on = Array.IndexOf(shown, i) >= 0;
                SetShown(purse.medal, on);
                SetShown(purse.name, on);
                SetShown(purse.amount, on);
                if (!on) purse.shown = -1;
            }
            for (int k = 0; k < shown.Length; k++)
            {
                int i = shown[k];
                var purse = b5Purse[i];
                bool lamps = i == 3;
                if (k > 0) x += 28f;
                PlaceCentre(purse.medal.Rect, new Vector2(0f, .5f), new Vector2(Bp(x + medal * .5f), 0f), Vector2.one * Bp(medal));
                purse.medal.SetSize(Bp(medal));
                x += medal + 12f;
                string name = i < 3 ? CurrencyLabel(i) : ArenaText.Pair("矿灯", "Lamps");
                StoryText(purse.name, name);
                purse.name.fontSize = V5StoryKit.FontSize(namePx);
                purse.name.color = workshop ? V5TitleKit.Ink2 : V5TitleKit.Ink;
                float nameW = B5W(purse.name, name);
                B5PlaceLeft(purse.name, Bp(x), Bp(nameW), Bp(B5WalletH));
                x += nameW + 10f;
                x += B5LayPurseAmount(purse, values[i], lamps ? "×" + values[i] : values[i].ToString(), numberPx, lamps ? V5TitleKit.Gold : ResourceColours.Of(i),
                    lamps ? "×0" : "000", x, B5WalletH, settled);
            }
            return x + 24f;
        }

        /// <summary>The void's two currencies (energy, alloy) in their own plate: each a violet-ringed medal and its number, no names.
        /// Returns the plate's width in mock-up pixels.</summary>
        float RefreshBase5VoidWallet(V2Campaign c)
        {
            bool settled = Time.frameCount - b5TickFrame <= 1;
            float x = 10f, medal = 30f;
            int[] values = { c.VoidEnergy, c.VoidAlloy };
            for (int k = 0; k < 2; k++)
            {
                var purse = b5Purse[4 + k];
                SetShown(purse.name, false);
                if (k > 0) x += 16f;
                PlaceCentre(purse.medal.Rect, new Vector2(0f, .5f), new Vector2(Bp(x + medal * .5f), 0f), Vector2.one * Bp(medal));
                purse.medal.SetSize(Bp(medal));
                x += medal + 8f;
                x += B5LayPurseAmount(purse, values[k], values[k].ToString(), 24f, V5HudKit.VioletHi, "00", x, B5VoidWalletH, settled);
            }
            return x + 14f;
        }

        /// <summary>A wallet cell's amount from left (mock-up pixels) in a row height tall, never narrower than sample (so the row does
        /// not shift as numbers grow and shrink); it swells once when it changes while the page shows. Returns its width.</summary>
        float B5LayPurseAmount(B5Purse purse, int value, string text, float px, Color colour, string sample, float left, float height, bool settled)
        {
            if (settled && purse.shown >= 0 && purse.shown != value) PulseOnce(purse.amount.transform);
            purse.shown = value;
            StoryText(purse.amount, text);
            purse.amount.fontSize = V5StoryKit.FontSize(px);
            purse.amount.color = colour;
            float w = Mathf.Max(B5W(purse.amount, text), DigitsWidth(purse.amount, sample) / B5Css);
            PlaceSpan(purse.amount, Bp(left), Bp(w), Bp(height));
            return w;
        }

        /// <summary>The foot: the keys that work on the page, and the ways out of the visit (V2Campaign.BaseExits) from the right, the main
        /// one at the right end in orange with its line under it.</summary>
        void RefreshBase5Foot(V2Campaign c, bool art, bool canClose)
        {
            bool pad = b5PadKeys;
            float x = 22f;
            for (int i = 0; i < 2; i++)
            {
                bool shown = i == 0 || canClose;
                SetShown(b5HintKeys[i], shown);
                SetShown(b5HintWords[i], shown);
                if (!shown) continue;
                if (i > 0) x += 34f;
                b5HintKeys[i].SetText(i == 0 ? pad ? "A" : ArenaKeys.InteractKey : pad ? "B" : "Esc");
                B5PlaceKey(b5HintKeys[i], Bp(x));
                x += b5HintKeys[i].Rect.sizeDelta.x / B5Css + 10f;
                string word = i == 0 ? ArenaText.Pair("购买", "Buy") : ArenaText.Pair("关闭", "Close");
                StoryText(b5HintWords[i], word);
                float w = B5W(b5HintWords[i], word);
                B5PlaceLeft(b5HintWords[i], Bp(x), Bp(w), Bp(B5FootH));
                x += w;
            }
            B5Rect(b5Hints.Rect, B5Margin, B5FootTop, x + 24f, B5FootH);

            var exits = c.BaseExits();
            float right = B5Width - B5Margin;
            int used = 0;
            for (int i = exits.Count - 1; i >= 0; i--)
            {
                var way = B5WayAt(used++);
                right = B5LayWay(way, exits[i], art, right) - B5WayGap;
                // The main way out is "f.go" (the base tour's last step points at it), the others "f.exit.N" by their place in BaseExits.
                B5AddFocus(exits[i].primary ? "f.go" : "f.exit." + i, way.plate);
            }
            for (int i = used; i < b5Ways.Count; i++) SetShown(b5Ways[i].plate, false);
        }

        /// <summary>One way out, its right edge at right (mock-up pixels); returns its left edge. The main one is the orange go button with
        /// "›" after its words and its line under it, never narrower than that line.</summary>
        float B5LayWay(B5Way way, V2Campaign.BaseExit exit, bool art, float right)
        {
            SetShown(way.plate, true);
            way.act = exit.go;
            way.go = exit.primary;
            way.plate.SetLook(way.go ? V5Plate.Look.Go : V5Plate.Look.Plate);
            string text = way.go ? exit.label + " ›" : exit.label;
            float px = way.go ? 26f : 22f, pad = way.go ? 30f : 28f;
            StoryText(way.label, text);
            way.label.fontSize = V5StoryKit.FontSize(px);
            way.label.color = way.go ? art ? V5TitleKit.Dark : Color.white : V5TitleKit.Ink;
            bool noted = !string.IsNullOrEmpty(exit.note);
            SetShown(way.note, noted);
            float noteW = 0f;
            if (noted)
            {
                StoryText(way.note, exit.note);
                way.note.fontSize = V5StoryKit.FontSize(B5NotePx);
                noteW = B5W(way.note, exit.note) + 12f;
            }
            float width = Mathf.Min(640f, Mathf.Max(way.go ? B5GoMinW : 0f, B5W(way.label, text) + pad * 2f, noteW));
            B5Place(way.plate, right - width, B5FootTop, width, B5FootH);
            V5TitleKit.FitWidth(way.label, V5StoryKit.FontSize(px), V5StoryKit.FontSize(px * .7f));
            if (noted) V5TitleKit.FitWidth(way.note, V5StoryKit.FontSize(B5NotePx), V5StoryKit.FontSize(13f));
            return right - width;
        }

        // ───── The keys' focus ─────

        /// <summary>A button the keys can focus: the pointer's arrival focuses it too, the focus shows as its chosen rim, and pressing it
        /// clicks it (a button that cannot be pressed now gives the refusal).</summary>
        void B5AddFocus(string id, V5Plate plate)
        {
            if (!plate || !plate.gameObject.activeInHierarchy) return;
            if (!plate.TryGetComponent<V5BaseHover>(out var hover)) hover = plate.gameObject.AddComponent<V5BaseHover>();
            hover.Hovered = on =>
            {
                if (on) SetB5Focus(id, false);
            };
            b5Focus.Add(new B5Focus
            {
                id = id, rect = plate.Rect,
                press = () =>
                {
                    if (plate && plate.isActiveAndEnabled && plate.Button.interactable)
                    {
                        plate.Button.onClick.Invoke();
                        return true;
                    }
                    PlayRefused();
                    return false;
                },
                mark = on =>
                {
                    if (plate) plate.Chosen = on;
                }
            });
        }

        /// <summary>A part the keys can focus that is not a plate (a research tab): what pressing it does and how it shows the focus.</summary>
        void B5AddFocus(string id, RectTransform rect, Func<bool> press, Action<bool> mark)
        {
            if (!rect || !rect.gameObject.activeInHierarchy) return;
            b5Focus.Add(new B5Focus { id = id, rect = rect, press = press, mark = mark });
        }

        void SetB5Focus(string id, bool byKey)
        {
            if (b5FocusId == id) return;
            b5FocusId = id;
            B5MarkFocus();
            if (!byKey) return;
            V5UiSound.Play(V5UiSound.Ui.Hover);
            var focus = b5Focus.Find(f => f.id == id);
            if (focus != null) RevealInWorkshop5(focus.rect);
        }

        void B5MarkFocus()
        {
            foreach (var focus in b5Focus) focus.mark(focus.id == b5FocusId);
        }

        /// <summary>The focus moves to the nearest part in the direction pressed (up is +y): the nearest along it, leaning against parts far
        /// to its side. With nothing focused yet, the first part of the page's middle takes the focus.</summary>
        void MoveB5Focus(Vector2 direction)
        {
            var current = b5Focus.Find(f => f.id == b5FocusId);
            if (current == null)
            {
                var first = b5Focus.Find(f => f.id.StartsWith("c.", StringComparison.Ordinal)) ?? (b5Focus.Count > 0 ? b5Focus[0] : null);
                if (first != null) SetB5Focus(first.id, true);
                return;
            }
            var dir = direction.normalized;
            var from = B5Centre(current.rect);
            B5Focus best = null;
            float bestScore = float.MaxValue;
            foreach (var focus in b5Focus)
            {
                if (focus == current || focus.id == current.id || !focus.rect || !focus.rect.gameObject.activeInHierarchy) continue;
                var d = B5Centre(focus.rect) - from;
                float along = Vector2.Dot(d, dir);
                if (along <= 1f) continue;
                float across = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                if (across > along * 2.5f) continue;
                float score = along + across * 2f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = focus;
                }
            }
            if (best != null) SetB5Focus(best.id, true);
        }

        void PressB5Focus()
        {
            var focus = b5Focus.Find(f => f.id == b5FocusId);
            focus?.press();
        }

        /// <summary>A rect's centre in the canvas's own units.</summary>
        Vector2 B5Centre(RectTransform rect) => root.InverseTransformPoint(rect.TransformPoint(rect.rect.center));

        // ───── A card's details ─────

        /// <summary>A card's details beside it: its name and what it does (StudyEntry.detail), shown while the pointer is on the card.</summary>
        void B5ShowTip(RectTransform over, string title, string body)
        {
            if (!B5Details || !b5Tip || !over || string.IsNullOrEmpty(body)) return;
            b5TipOver = over;
            StoryText(b5TipTitle, title ?? "");
            StoryText(b5TipBody, body);
            float width = Bp(B5TipW), pad = Bp(18f), inner = width - pad * 2f;
            float titleH = B5FitHeight(b5TipTitle, inner), bodyH = B5FitHeight(b5TipBody, inner);
            bool titled = titleH > 0f && !string.IsNullOrEmpty(b5TipTitle.text);
            SetShown(b5TipTitle, titled);
            b5TipTitle.rectTransform.anchoredPosition = new Vector2(pad, -pad);
            float bodyTop = pad + (titled ? titleH + Bp(8f) : 0f);
            b5TipBody.rectTransform.anchoredPosition = new Vector2(pad, -bodyTop);
            b5Tip.sizeDelta = new Vector2(width, bodyTop + bodyH + pad);
            b5Tip.gameObject.SetActive(true);
            b5Tip.SetAsLastSibling();
            PlaceB5Tip();
        }

        /// <summary>Hides the details (only those of over, when given).</summary>
        void B5HideTip(RectTransform over = null)
        {
            if (over && over != b5TipOver) return;
            b5TipOver = null;
            if (b5Tip && b5Tip.gameObject.activeSelf) b5Tip.gameObject.SetActive(false);
        }

        /// <summary>The details stand right of their card (left of it where the screen has no room), their tops level, inside the screen.</summary>
        void PlaceB5Tip()
        {
            if (!b5Tip || !b5Tip.gameObject.activeSelf) return;
            if (!BaseOpen || !b5TipOver || !b5TipOver.gameObject.activeInHierarchy || !(TourRect(b5TipOver) is Rect box))
            {
                B5HideTip();
                return;
            }
            var area = root.rect.size;
            var size = b5Tip.sizeDelta;
            float gap = Bp(14f), margin = Bp(12f);
            float x = box.xMax + gap;
            if (x + size.x > area.x - margin) x = box.xMin - gap - size.x;
            x = Mathf.Clamp(x, margin, Mathf.Max(margin, area.x - margin - size.x));
            float y = Mathf.Clamp(box.yMax - size.y, margin, Mathf.Max(margin, area.y - margin - size.y));
            b5Tip.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>Gives a wrapping label this width and returns how tall its text is there, the label made that tall.</summary>
        static float B5FitHeight(Text label, float width)
        {
            var rect = label.rectTransform;
            rect.sizeDelta = new Vector2(width, 10f);
            float height = string.IsNullOrEmpty(label.text) ? 0f : Mathf.Ceil(label.preferredHeight);
            rect.sizeDelta = new Vector2(width, height);
            return height;
        }

        /// <summary>A part that shows its details beside it while the pointer is on it (hit: the graphic that takes the pointer for that).</summary>
        void B5HoverDetails(Image hit, RectTransform over, Func<(string title, string body)> details)
        {
            hit.raycastTarget = true;
            if (!hit.TryGetComponent<V5BaseHover>(out var hover)) hover = hit.gameObject.AddComponent<V5BaseHover>();
            hover.Hovered = on =>
            {
                if (!on)
                {
                    B5HideTip(over);
                    return;
                }
                var (title, body) = details();
                B5ShowTip(over, title, body);
            };
        }

        // ───── Prices ─────

        static B5Coins B5BuildCoins(Transform parent, float numberPx)
        {
            var coins = new B5Coins { numberPx = numberPx };
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
            return coins;
        }

        /// <summary>A price into b5Price, alloy first, then the alien energy, the dark matter and the void's two; how many coins to draw (at
        /// most three).</summary>
        int B5PriceOf(CampaignCost? cost, int voidEnergy = 0, int voidAlloy = 0)
        {
            b5Price.Clear();
            var c = campaign;
            if (cost is { } p)
            {
                if (p.alloy > 0) b5Price.Add((1, p.alloy, c.Alloy));
                if (p.energy > 0) b5Price.Add((0, p.energy, c.Energy));
                if (p.dark > 0) b5Price.Add((2, p.dark, c.Dark));
            }
            if (voidEnergy > 0) b5Price.Add((3, voidEnergy, c.VoidEnergy));
            if (voidAlloy > 0) b5Price.Add((4, voidAlloy, c.VoidAlloy));
            return Mathf.Min(3, b5Price.Count);
        }

        /// <summary>A study entry's price into b5Price (none while it is owned or locked).</summary>
        int B5PriceOf(StudyEntry entry) => entry == null || entry.state is StudyState.Owned or StudyState.Locked ? B5PriceOf(null)
            : B5PriceOf(entry.price, entry.voidEnergyCost, entry.voidAlloyCost);

        /// <summary>How wide the price in b5Price draws (mock-up pixels): its icons size across.</summary>
        float B5CoinsWidth(B5Coins coins, int n, float size, bool art)
        {
            float width = 0f;
            for (int k = 0; k < n; k++)
            {
                var (which, amount, _) = b5Price[k];
                var label = coins.amounts[k];
                label.fontSize = V5StoryKit.FontSize(coins.numberPx);
                bool icon = B5CoinSprite(which, art);
                width += (k > 0 ? 14f : 0f) + (icon ? size + 8f : 0f) + B5W(label, icon ? amount.ToString() : $"{B5CoinName(which)} {amount}");
            }
            return width;
        }

        /// <summary>The price in b5Price as coins from left (mock-up pixels), their middle middle down from the parent's top: each icon
        /// size across and its amount in the currency's colour, red while the stock is short of it, grey when nothing is short but it
        /// cannot be bought for another reason. Returns where the row ends.</summary>
        float B5LayCoins(B5Coins coins, int n, float left, float middle, float size, bool art, bool blocked)
        {
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
                var (which, amount, held) = b5Price[k];
                var sprite = B5CoinSprite(which, art);
                // With the art on the currency's picture is cut round; whitebox shows its icon whole.
                var round = coins.holes[k].GetComponent<Mask>();
                if (round.enabled != art) round.enabled = art;
                coins.holes[k].enabled = art;
                coins.icons[k].sprite = sprite;
                coins.icons[k].enabled = sprite;
                coins.icons[k].transform.localScale = Vector3.one * (art && which == 2 ? DarkIconScale : 1f);
                if (k > 0) x += 14f;
                B5Rect(coins.holes[k].rectTransform, x, middle - size * .5f, size, size);
                bool ringed = which >= 3 && sprite;
                SetShown(coins.rings[k], ringed);
                if (ringed) B5Rect(coins.rings[k].rectTransform, x - size * .1f, middle - size * .6f, size * 1.2f, size * 1.2f);
                if (sprite) x += size + 8f;
                string text = sprite ? amount.ToString() : $"{B5CoinName(which)} {amount}";
                var label = coins.amounts[k];
                label.fontSize = V5StoryKit.FontSize(coins.numberPx);
                StoryText(label, text);
                label.color = held < amount ? B5Short : blocked ? V5TitleKit.Ink3 : B5CoinColour(which);
                float w = B5W(label, text);
                B5Rect(label.rectTransform, x, middle - coins.numberPx * .75f, w + 2f, coins.numberPx * 1.5f);
                x += w;
            }
            return x;
        }

        static void B5HideCoins(B5Coins coins)
        {
            for (int k = 0; k < 3; k++)
            {
                SetShown(coins.holes[k], false);
                SetShown(coins.rings[k], false);
                SetShown(coins.amounts[k], false);
            }
        }

        /// <summary>A currency's icon: the art's or the whitebox's; the void's two are the alien energy's and alloy's (the whitebox has a
        /// void alloy of its own).</summary>
        Sprite B5CoinSprite(int which, bool art)
        {
            if (which < 3) return CurrencySprite(which);
            if (!art) return which == 3 ? WhiteboxIcons.CurrencyIcon(WhiteboxIcons.Currency.Energy) : WhiteboxIcons.Look("voidalloy");
            var set = campaign ? campaign.art : null;
            if (!set) return null;
            return which == 3 ? set.energy : set.alloy;
        }

        static string B5CoinName(int which) => which < 3 ? CurrencyLabel(which)
            : which == 3 ? ArenaText.Pair("虚空能量", "Void energy") : ArenaText.Pair("虚空合金", "Void alloy");

        /// <summary>A currency's number colour: each of the three its own (ResourceColours), violet for the void's two.</summary>
        static Color B5CoinColour(int which) => which < 3 ? ResourceColours.Of(which) : V5HudKit.VioletHi;

        // ───── Small pieces ─────

        /// <summary>A rarity's colour on the dark plates: the white rarity a light grey, the void's own (near black) the void's violet.</summary>
        static Color B5RarityColour(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => B5CommonBar,
            CardRarity.Void => V5HudKit.Violet,
            _ => ArenaRarity.ColorFor(rarity)
        };

        /// <summary>An entry's whitebox icon: the whitebox's for its kind, a plain grey square for a kind it does not know where the entry
        /// has a picture.</summary>
        static Sprite B5FlatIcon(StudyEntry entry)
        {
            if (entry == null) return null;
            var sprite = WhiteboxIcons.ForStudy(entry.id);
            return sprite ? sprite : entry.icon ? WhiteboxIcons.Unknown() : null;
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

        /// <summary>A label from left in its row (canvas units), as wide as width and as tall as the row, its middle on the row's middle.</summary>
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

        /// <summary>A label of several lines at the largest size, from largest down to smallest, at which all its lines fit its box (no best
        /// fit: pitfall T-015); what still does not fit at the smallest is cut.</summary>
        static void B5FitLines(Text label, int largest, int smallest)
        {
            float height = label.rectTransform.rect.height;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
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

        /// <summary>A button that cannot be pressed now is grey: dimmed, its words grey, faded, and it takes no click.</summary>
        static void B5Live(V5Plate button, Text words, bool live, Color ink)
        {
            button.Button.interactable = live;
            button.Dim = !live;
            button.Alpha = live ? 1f : .55f;
            if (words) words.color = live ? ink : V5TitleKit.Ink3;
        }

        /// <summary>The words after the last "→" in a colour ("×1.26 → ×1.59": the number after it), the rest as it is.</summary>
        static string B5Gain(string text, Color colour)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int arrow = text.LastIndexOf('→');
            if (arrow < 0 || arrow >= text.Length - 1) return text;
            return text.Substring(0, arrow + 1) + $"<color=#{HexOf(colour)}>{text.Substring(arrow + 1)}</color>";
        }

        /// <summary>"now → after", the arrow dim and what the purchase gives in a colour; or what there is of it.</summary>
        static string B5NowAfter(string now, string after, Color colour)
        {
            bool hasNow = !string.IsNullOrEmpty(now), hasAfter = !string.IsNullOrEmpty(after);
            string arrow = $"<color=#{HexOf(V5TitleKit.Ink3)}>  →  </color>", gain = hasAfter ? $"<color=#{HexOf(colour)}>{after}</color>" : "";
            return hasNow && hasAfter ? now + arrow + gain : hasNow ? now : hasAfter ? arrow.TrimStart() + gain : "";
        }

        /// <summary>A clear image that takes the pointer (a research tab's hit area): drawn, so the raycaster sees it.</summary>
        static Image B5Hit(Transform parent, string name)
        {
            var hit = V5TitleKit.Picture(parent, name, null, Color.clear);
            hit.raycastTarget = true;
            hit.canvasRenderer.cullTransparentMesh = false;
            return hit;
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

        // ───── For automated checks ─────

        /// <summary>For automated checks: the base's page shown, or null while the base is closed (or on the English Alpha's old page).</summary>
        public V2Campaign.BaseScreen? BaseScreenShown => b5Built && BaseOpen ? b5Screen : null;

        static readonly Vector3[] b5FootCorners = new Vector3[4];

        /// <summary>The free room along the page's foot while the page shows, between the keys' plate and the ways out: its left and
        /// right edges and the foot's middle height, in the screen layer's units measured from the layer's middle (where the flash is
        /// anchored). False while the page is not shown.</summary>
        bool B5FootRoom(out float left, out float right, out float middle)
        {
            left = right = middle = 0f;
            if (!b5Built || !BaseOpen || !b5Hints || !b5Hints.gameObject.activeInHierarchy) return false;
            Vector2 centre = root.rect.center;
            b5Hints.Rect.GetWorldCorners(b5FootCorners);
            Vector3 low = root.InverseTransformPoint(b5FootCorners[0]), high = root.InverseTransformPoint(b5FootCorners[2]);
            left = high.x - centre.x;
            middle = (low.y + high.y) * .5f - centre.y;
            right = root.rect.xMax - centre.x;
            foreach (var way in b5Ways)
            {
                if (!way.plate || !way.plate.gameObject.activeInHierarchy) continue;
                ((RectTransform)way.plate.transform).GetWorldCorners(b5FootCorners);
                right = Mathf.Min(right, root.InverseTransformPoint(b5FootCorners[0]).x - centre.x);
            }
            return right > left;
        }

        /// <summary>For automated checks: the flash's plate in screen pixels while it shows.</summary>
        public Rect? DebugFlashRect() => FlashOnScreen();

        /// <summary>For automated checks: a part of the base's page in screen pixels (by its id, as DebugBaseParts lists them).</summary>
        public Rect? DebugBasePartRect(string id)
        {
            var focus = b5Focus.Find(f => f.id == id);
            if (focus == null || !focus.rect) return null;
            focus.rect.GetWorldCorners(b5FootCorners);
            return Rect.MinMaxRect(b5FootCorners[0].x, b5FootCorners[0].y, b5FootCorners[2].x, b5FootCorners[2].y);
        }

        /// <summary>For automated checks: the ids of the parts of the base's page the keys can focus now ("h.switch", "c.mk", "c.card.skill.Blink",
        /// "c.part.Engine", "f.go" ...).</summary>
        public List<string> DebugBaseParts() => b5Focus.ConvertAll(f => f.id);

        /// <summary>For automated checks: presses a part of the base's page by its id as a click on it would; false when it is not on the
        /// page or cannot be pressed now.</summary>
        public bool DebugPressBase(string id)
        {
            var focus = b5Focus.Find(f => f.id == id);
            return focus != null && focus.press();
        }
    }
}
