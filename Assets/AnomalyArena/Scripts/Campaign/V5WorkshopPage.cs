using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The workshop (整备), the base page the base door opens (work/art-plans/style-1112/ui-f/workshop.html), in the frame of
    /// V5BasePage.cs. In the top row, between the banner and the wallet, the base's fortification: its level, its HP now and after, its
    /// price ("full" at the top). Left, the weapons, paid in alloy: the weapon Mk with its ten steps (the bonus steps marked), armor, and
    /// a card for each weapon in hand with its Tame and Twist (each with its price and button, or the day it opens). Right, the energy
    /// research, paid in alien energy: the tabs body, skills and infusion (a tab not open yet says when it opens), the void once the Nash
    /// Prophet is beaten and the final overload from its day; under them the tab's items as cards (icon, name, now and after, price and
    /// button, the rarity's bar on the left; an item not revealed yet is left out), scrolled when there are more than fit, and at the foot
    /// the research level's card (what the next level opens). Everything comes from V2Campaign.BuildStudy and is bought with
    /// StudyEntry.buy; a button the stock does not pay for is grey.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The two columns (mock-up pixels): 904 wide from 36 and from 980; their heads 60 tall, the rule under them 8 lower; the cards
        // from 250. Left: the weapon Mk 268 tall, armor 118, the hands 214, 18 apart. Right: the tabs 52 tall, the cards from 314 to the
        // research level's card (84 tall at the floor, 12 above it), two to a row 24 apart and 12 down, 128 to 224 tall.
        const float W5LeftX = 36f, W5RightX = 980f, W5ColW = 904f, W5HeadH = 60f, W5BodyTop = 250f;
        const float W5MkH = 268f, W5ArmorTop = 536f, W5ArmorH = 118f, W5HandsTop = 672f, W5HandW = 440f, W5HandGap = 24f;
        const float W5TabsH = 52f, W5GridTop = 314f, W5TierH = 84f, W5GridGapX = 24f, W5GridGapY = 12f, W5CardMinH = 128f, W5CardMaxH = 224f, W5ScrollW = 14f;

        // A row's backing in a hand's card (locked, open), its rim, and the alloy's steps on the weapon Mk's track (top and foot of its fill).
        static readonly Color W5RowBack = V5TitleKit.Rgb(8, 10, 18, .45f), W5RowOpen = V5TitleKit.Rgb(14, 20, 34, .7f), W5RowEdge = V5TitleKit.Rgb(120, 128, 146, .3f),
            W5RowEdgeOpen = V5TitleKit.Rgb(227, 154, 85, .45f), W5LockName = V5TitleKit.Rgb(138, 143, 160), W5TrackOff = V5TitleKit.Rgb(160, 170, 190, .28f),
            W5CopperTop = V5TitleKit.Rgb(255, 211, 166), W5CopperFoot = V5TitleKit.Rgb(196, 111, 44), W5HeadRule = V5TitleKit.Rgb(200, 208, 224, .45f);

        sealed class W5Head
        {
            public V5HudMedal medal;
            public Text title, sub;
            public RawImage rule;
        }

        sealed class W5Tick
        {
            public Image diamond;
            public Text label, bonus;
        }

        sealed class W5Row
        {
            public RectTransform root;
            public Image back, lockIcon;
            public Text name, note;
            public B5Coins coins;
            public V5Plate button;
            public Text verb;
            public StudyEntry entry;
        }

        sealed class W5Hand
        {
            public V5HudPlate plate;
            public Image weapon;
            public Text title;
            public readonly W5Row[] rows = new W5Row[2];
        }

        sealed class W5Tab
        {
            public RectTransform root;
            public Image hit, underline;
            public Text label;
            public string key;
            public bool locked, chosen;
        }

        sealed class W5Card
        {
            public V5HudPlate plate;
            public Image bar;
            public V5HudMedal medal;
            public Text title, value, done;
            public B5Coins coins;
            public V5Plate button;
            public Text verb;
            public StudyEntry entry;
        }

        /// <summary>One research tab as the workshop shows it: the eng.* tabs as they are (their research level apart), every void.* tab
        /// merged into one, the final overload.</summary>
        sealed class W5Research
        {
            public string key, name, note, empty;
            public bool locked;
            public StudyEntry tier;
            public readonly List<StudyEntry> entries = new();
        }

        // The research tab shown: "eng.body", "eng.skill", "eng.infuse", "void" or "ctl.overload" (kept between visits).
        string w5TabKey;
        readonly W5Head[] w5Heads = new W5Head[2];
        // The fortification.
        V5Plate w5Fortify;
        V5HudMedal w5FortifyMedal;
        Text w5FortifyTitle, w5FortifyHp, w5FortifyDone;
        B5Coins w5FortifyCoins;
        // The weapon Mk.
        RectTransform w5Mk;
        V5HudPlate w5MkPlate;
        Text w5MkTitle, w5MkStep, w5MkEffect, w5MkDone;
        B5Coins w5MkCoins;
        V5Plate w5MkButton;
        Text w5MkVerb;
        Image w5TrackLine, w5TrackFill;
        readonly List<W5Tick> w5Ticks = new();
        // Armor.
        RectTransform w5Armor;
        V5HudPlate w5ArmorPlate;
        V5HudMedal w5ArmorMedal;
        Text w5ArmorTitle, w5ArmorEffect, w5ArmorDone;
        B5Coins w5ArmorCoins;
        V5Plate w5ArmorButton;
        Text w5ArmorVerb;
        StudyEntry w5MkEntry, w5ArmorEntry;
        // The hands.
        readonly W5Hand[] w5Hands = new W5Hand[2];
        // The research.
        RectTransform w5TabsRow, w5Viewport, w5Content, w5Tier;
        RawImage w5TabsRule;
        ScrollRect w5Scroll;
        Image w5ScrollTrack, w5ScrollThumb, w5TierGem;
        Text w5Empty, w5TierText, w5TierDone;
        V5HudPlate w5TierPlate;
        B5Coins w5TierCoins;
        V5Plate w5TierButton;
        Text w5TierVerb;
        StudyEntry w5TierEntry;
        readonly List<W5Tab> w5Tabs = new();
        readonly List<W5Card> w5Cards = new();
        List<W5Research> w5Research = new();

        // ───── Building (BuildBase5) ─────

        void BuildWorkshop5(RectTransform parent)
        {
            // The fortification in the top row.
            w5Fortify = B5Button(parent, "Fortify", V5Plate.Look.Plate, () => B5Act(() => campaign.TryFortify()));
            w5FortifyMedal = V5HudKit.Medal(w5Fortify.transform, "Medal", Bp(50f));
            w5FortifyMedal.SetRing(V5TitleKit.Steel);
            w5FortifyTitle = V5StoryKit.Label(w5Fortify.transform, "Title", 21f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            w5FortifyHp = V5StoryKit.Label(w5Fortify.transform, "Hp", 19f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            w5FortifyDone = V5StoryKit.Label(w5Fortify.transform, "Full", 21f, V5TitleKit.Teal, TextAnchor.MiddleLeft, true);
            w5FortifyCoins = B5BuildCoins(w5Fortify.transform, 26f);

            for (int side = 0; side < 2; side++)
            {
                var head = w5Heads[side] = new W5Head();
                string name = side == 0 ? "Weapons" : "Research";
                head.medal = V5HudKit.Medal(parent, name + "Medal", Bp(W5HeadH));
                head.medal.SetRing(side == 0 ? ResourceColours.Alloy : ResourceColours.Energy);
                head.title = V5StoryKit.Label(parent, name + "Title", 36f, Color.white, TextAnchor.MiddleLeft, true);
                V5TitleKit.Painted(head.title);
                head.sub = V5StoryKit.Label(parent, name + "Sub", 20f, V5TitleKit.Ink3, TextAnchor.MiddleLeft);
                head.rule = V5TitleKit.Raw(parent, name + "Rule", V5TitleKit.FadeRight, W5HeadRule);
            }

            BuildMk5(parent);
            BuildArmor5(parent);
            for (int h = 0; h < 2; h++) BuildHand5(parent, h);
            BuildResearch5(parent);
        }

        void BuildMk5(RectTransform parent)
        {
            w5Mk = V5TitleKit.Rect(parent, "WeaponMk");
            w5MkPlate = V5HudKit.Plate(w5Mk, "Plate", V5HudKit.PlateLook.Plain, 16f, B5Css);
            V5TitleKit.Stretch(w5MkPlate.Rect);
            B5HoverDetails(w5MkPlate.Image, w5MkPlate.Rect, () => (w5MkEntry?.title, w5MkEntry?.detail));
            w5MkTitle = V5StoryKit.Label(w5Mk, "Title", 38f, Color.white, TextAnchor.MiddleLeft, true);
            V5TitleKit.Painted(w5MkTitle);
            w5MkStep = V5StoryKit.Label(w5Mk, "Step", 32f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
            w5MkEffect = V5StoryKit.Label(w5Mk, "Effect", 22f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            w5MkDone = V5StoryKit.Label(w5Mk, "Full", 24f, V5TitleKit.Teal, TextAnchor.MiddleRight, true);
            w5MkCoins = B5BuildCoins(w5Mk, 30f);
            w5MkButton = B5Button(w5Mk, "Upgrade", V5Plate.Look.Go, () => B5Act(() => w5MkEntry?.buy != null && w5MkEntry.buy()));
            w5MkVerb = V5StoryKit.Label(w5MkButton.transform, "Verb", 24f, V5TitleKit.Dark, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(w5MkVerb.rectTransform);
            w5TrackLine = V5TitleKit.Picture(w5Mk, "Track", null, W5TrackOff);
            w5TrackFill = V5TitleKit.Picture(w5Mk, "TrackFill", null, ResourceColours.Alloy);
        }

        void BuildArmor5(RectTransform parent)
        {
            w5Armor = V5TitleKit.Rect(parent, "Armor");
            w5ArmorPlate = V5HudKit.Plate(w5Armor, "Plate", V5HudKit.PlateLook.Plain, 14f, B5Css);
            V5TitleKit.Stretch(w5ArmorPlate.Rect);
            B5HoverDetails(w5ArmorPlate.Image, w5ArmorPlate.Rect, () => (w5ArmorEntry?.title, w5ArmorEntry?.detail));
            w5ArmorMedal = V5HudKit.Medal(w5Armor, "Medal", Bp(76f));
            w5ArmorTitle = V5StoryKit.Label(w5Armor, "Title", 28f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            w5ArmorEffect = V5StoryKit.Label(w5Armor, "Effect", 21f, V5TitleKit.Ink2, TextAnchor.MiddleLeft);
            w5ArmorDone = V5StoryKit.Label(w5Armor, "Full", 22f, V5TitleKit.Teal, TextAnchor.MiddleRight, true);
            w5ArmorCoins = B5BuildCoins(w5Armor, 30f);
            w5ArmorButton = B5Button(w5Armor, "Upgrade", V5Plate.Look.Plate, () => B5Act(() => w5ArmorEntry?.buy != null && w5ArmorEntry.buy()));
            w5ArmorVerb = V5StoryKit.Label(w5ArmorButton.transform, "Verb", 21f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(w5ArmorVerb.rectTransform);
        }

        void BuildHand5(RectTransform parent, int index)
        {
            var hand = w5Hands[index] = new W5Hand();
            hand.plate = V5HudKit.Plate(parent, "Hand" + (index + 1), V5HudKit.PlateLook.Plain, 14f, B5Css);
            var t = hand.plate.transform;
            hand.weapon = V5TitleKit.Picture(t, "Weapon", null, Color.white);
            hand.weapon.preserveAspect = true;
            hand.title = V5StoryKit.Label(t, "Title", 25f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            for (int k = 0; k < 2; k++)
            {
                var row = hand.rows[k] = new W5Row();
                row.root = V5TitleKit.Rect(t, k == 0 ? "Tame" : "Twist");
                row.back = V5TitleKit.Picture(row.root, "Back", null, W5RowBack);
                V5TitleKit.Stretch(row.back.rectTransform);
                var current = row;
                B5HoverDetails(row.back, row.root, () => (current.entry?.title, current.entry != null && current.entry.state != StudyState.Locked ? current.entry.detail : null));
                row.lockIcon = V5TitleKit.Picture(row.root, "Lock", null, Color.white);
                row.lockIcon.preserveAspect = true;
                row.name = V5StoryKit.Label(row.root, "Name", 22f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                row.note = V5StoryKit.Label(row.root, "When", 20f, B5Lock, TextAnchor.MiddleRight);
                row.coins = B5BuildCoins(row.root, 24f);
                row.button = B5Button(row.root, "Buy", V5Plate.Look.Plate, () => B5Act(() => current.entry?.buy != null && current.entry.buy()));
                row.verb = V5StoryKit.Label(row.button.transform, "Verb", 19f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(row.verb.rectTransform);
            }
        }

        void BuildResearch5(RectTransform parent)
        {
            w5TabsRow = V5TitleKit.Rect(parent, "ResearchTabs");
            w5TabsRule = V5TitleKit.Raw(w5TabsRow, "Rule", null, V5TitleKit.Rgb(160, 170, 190, .22f));
            // The cards scroll (the wheel, or a drag) when there are more than fit; the keys' focus scrolls to its card.
            w5Viewport = V5TitleKit.Rect(parent, "ResearchCards");
            var catcher = V5TitleKit.Picture(w5Viewport, "Wheel", null, Color.clear);
            V5TitleKit.Stretch(catcher.rectTransform);
            catcher.raycastTarget = true;
            catcher.canvasRenderer.cullTransparentMesh = false;
            w5Viewport.gameObject.AddComponent<RectMask2D>();
            w5Content = V5TitleKit.Rect(w5Viewport, "Content");
            w5Content.anchorMin = new Vector2(0f, 1f);
            w5Content.anchorMax = new Vector2(1f, 1f);
            w5Content.pivot = new Vector2(0f, 1f);
            w5Content.anchoredPosition = Vector2.zero;
            w5Content.sizeDelta = Vector2.zero;
            w5Scroll = w5Viewport.gameObject.AddComponent<ScrollRect>();
            w5Scroll.viewport = w5Viewport;
            w5Scroll.content = w5Content;
            w5Scroll.horizontal = false;
            w5Scroll.vertical = true;
            w5Scroll.movementType = ScrollRect.MovementType.Clamped;
            w5Scroll.scrollSensitivity = 40f;
            w5Scroll.onValueChanged.AddListener(_ => PlaceScrollThumb5());
            w5ScrollTrack = V5TitleKit.Picture(parent, "ScrollTrack", null, V5HudKit.Track);
            w5ScrollThumb = V5TitleKit.Picture(parent, "ScrollThumb", null, V5TitleKit.Rgb(163, 172, 190, .7f));
            w5Empty = V5StoryKit.Label(w5Viewport, "Empty", 22f, V5TitleKit.Ink3, TextAnchor.MiddleCenter);
            w5Empty.horizontalOverflow = HorizontalWrapMode.Wrap;
            V5TitleKit.Stretch(w5Empty.rectTransform, Bp(40f));

            w5Tier = V5TitleKit.Rect(parent, "ResearchLevel");
            w5TierPlate = V5HudKit.Plate(w5Tier, "Plate", V5HudKit.PlateLook.Teal, 14f, B5Css);
            V5TitleKit.Stretch(w5TierPlate.Rect);
            B5HoverDetails(w5TierPlate.Image, w5TierPlate.Rect, () => (w5TierEntry?.title, w5TierEntry?.detail));
            w5TierGem = V5TitleKit.Picture(w5Tier, "Gem", null, Color.white);
            w5TierGem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            w5TierText = V5StoryKit.Label(w5Tier, "Text", 26f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
            w5TierDone = V5StoryKit.Label(w5Tier, "Full", 22f, V5TitleKit.Teal, TextAnchor.MiddleRight, true);
            w5TierCoins = B5BuildCoins(w5Tier, 28f);
            w5TierButton = B5Button(w5Tier, "Research", V5Plate.Look.Plate, () => B5Act(() => w5TierEntry?.buy != null && w5TierEntry.buy()));
            w5TierVerb = V5StoryKit.Label(w5TierButton.transform, "Verb", 21f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(w5TierVerb.rectTransform);
        }

        /// <summary>The research tab at index, made when first needed and reused: its words, its underline while shown, and its hit area.</summary>
        W5Tab W5TabAt(int index)
        {
            while (w5Tabs.Count <= index)
            {
                var tab = new W5Tab();
                tab.root = V5TitleKit.Rect(w5TabsRow, "Tab" + (w5Tabs.Count + 1));
                tab.hit = B5Hit(tab.root, "Hit");
                V5TitleKit.Stretch(tab.hit.rectTransform);
                var button = tab.hit.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() =>
                {
                    B5Deselect();
                    ShowResearchTab5(tab.key, true);
                });
                tab.label = V5StoryKit.Label(tab.root, "Name", 24f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
                tab.underline = V5TitleKit.Picture(tab.root, "Underline", null, V5TitleKit.Orange);
                w5Tabs.Add(tab);
            }
            return w5Tabs[index];
        }

        /// <summary>The card at index, made when first needed and reused (a card under the pointer stays the same card when the page is
        /// drawn again).</summary>
        W5Card W5CardAt(int index)
        {
            while (w5Cards.Count <= index)
            {
                var card = new W5Card();
                card.plate = V5HudKit.Plate(w5Content, "Card" + (w5Cards.Count + 1), V5HudKit.PlateLook.Plain, 12f, B5Css);
                var t = card.plate.transform;
                B5HoverDetails(card.plate.Image, card.plate.Rect, () => (card.entry?.title, card.entry?.detail));
                card.bar = V5TitleKit.Picture(t, "Rarity", null, Color.white);
                card.medal = V5HudKit.Medal(t, "Medal", Bp(70f));
                card.title = V5StoryKit.Label(t, "Title", 25f, V5TitleKit.Ink, TextAnchor.MiddleLeft, true);
                card.value = V5StoryKit.Label(t, "Value", 27f, V5TitleKit.Ink2, TextAnchor.MiddleLeft, true);
                card.done = V5StoryKit.Label(t, "Owned", 22f, V5TitleKit.Teal, TextAnchor.MiddleRight, true);
                card.coins = B5BuildCoins(t, 28f);
                card.button = B5Button(t, "Buy", V5Plate.Look.Plate, () => B5Act(() => card.entry?.buy != null && card.entry.buy()));
                card.verb = V5StoryKit.Label(card.button.transform, "Verb", 19f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
                V5TitleKit.Stretch(card.verb.rectTransform);
                w5Cards.Add(card);
            }
            return w5Cards[index];
        }

        // ───── Opening and the research tabs ─────

        /// <summary>The base opens: the research cards from their top.</summary>
        void OpenWorkshop5()
        {
            if (w5Content) w5Content.anchoredPosition = Vector2.zero;
        }

        /// <summary>Shows a research tab (a click on it, LB and RB, the base tour); a tab not open yet answers with the refusal.</summary>
        void ShowResearchTab5(string key, bool sound)
        {
            var research = w5Research.Find(r => r.key == key);
            if (research == null || research.locked)
            {
                if (sound) PlayRefused();
                return;
            }
            if (w5TabKey == key) return;
            w5TabKey = key;
            if (w5Content) w5Content.anchoredPosition = Vector2.zero;
            if (sound) V5UiSound.Play(V5UiSound.Ui.Tab);
            B5HideTip();
            if (BaseOpen) RefreshBase5(true);
        }

        /// <summary>LB and RB: the open research tab before or after the one shown.</summary>
        void StepResearchTab5(int step)
        {
            var open = w5Research.FindAll(r => !r.locked);
            if (open.Count < 2) return;
            int at = Mathf.Max(0, open.FindIndex(r => r.key == w5TabKey));
            ShowResearchTab5(open[(at + step % open.Count + open.Count) % open.Count].key, true);
        }

        /// <summary>For automated checks and the base tour: shows a research tab of the workshop by its key ("eng.skill"), as a click on
        /// it would (without its sound); nothing for a tab not open yet or while the workshop is not shown.</summary>
        public void ShowResearchTab(string key)
        {
            if (b5Built && BaseScreenShown == V2Campaign.BaseScreen.Workshop) ShowResearchTab5(key, false);
        }

        // ───── Refreshing (RefreshBase5) ─────

        void RefreshWorkshop5(V2Campaign c, bool art, float walletLeft)
        {
            var study = c.BuildStudy();
            var upgrades = study.Find(t => t.key == "arm.upgrade");
            w5MkEntry = upgrades?.entries.Find(e => e.id == "mk");
            w5ArmorEntry = upgrades?.entries.Find(e => e.id == "armor");
            RefreshWorkshopHeads5(c);
            LayMk5(c, art, w5MkEntry);
            LayArmor5(c, art, w5ArmorEntry);
            for (int h = 0; h < 2; h++) LayHand5(c, art, h, study.Find(t => t.key == "arm.tame"), study.Find(t => t.key == "arm.twist"));
            LayResearch5(c, art, study);
            LayFortify5(c, art, walletLeft);
        }

        void RefreshWorkshopHeads5(V2Campaign c)
        {
            var set = c.art;
            for (int side = 0; side < 2; side++)
            {
                var head = w5Heads[side];
                float x = side == 0 ? W5LeftX : W5RightX;
                head.medal.SetIcons(side == 0 ? set ? set.armament : null : set ? set.energyLab : null,
                    WhiteboxIcons.Room(side == 0 ? CampaignRoom.Armament : CampaignRoom.Energy));
                PlaceCentre(head.medal.Rect, new Vector2(0f, 1f), new Vector2(Bp(x + W5HeadH * .5f), -Bp(B5Top + W5HeadH * .5f)), Vector2.one * Bp(W5HeadH));
                StoryText(head.title, side == 0 ? ArenaText.Pair("武器", "Weapons") : ArenaText.Pair("能量研究", "Energy research"));
                StoryText(head.sub, side == 0 ? ArenaText.Pair("用合金升级", "Upgraded with alloy") : ArenaText.Pair("用异星能量研究", "Researched with alien energy"));
                head.title.fontSize = V5StoryKit.FontSize(36f);
                head.sub.fontSize = V5StoryKit.FontSize(20f);
                float titleW = Mathf.Min(B5W(head.title, head.title.text), W5ColW * .55f);
                B5Rect(head.title.rectTransform, x + 76f, B5Top, titleW + 4f, W5HeadH);
                V5TitleKit.FitWidth(head.title, V5StoryKit.FontSize(36f), V5StoryKit.FontSize(26f));
                float subLeft = x + 76f + titleW + 16f;
                B5Rect(head.sub.rectTransform, subLeft, B5Top + 6f, x + W5ColW - subLeft, W5HeadH);
                V5TitleKit.FitWidth(head.sub, V5StoryKit.FontSize(20f), V5StoryKit.FontSize(14f));
                B5Rect(head.rule.rectTransform, x + 76f, B5Top + W5HeadH + 8f, W5ColW - 76f, 2f);
            }
        }

        /// <summary>The base's fortification, centred in the room between the banner and the wallet: its medal, "fortification, level n",
        /// its HP now and after the next level, and that level's price, or "full" at the top. The whole plate is its button.</summary>
        void LayFortify5(V2Campaign c, bool art, float walletLeft)
        {
            float left = B5Margin + B5BannerW + 24f, room = walletLeft - 24f - left;
            bool shown = room >= 200f;
            SetShown(w5Fortify, shown);
            if (!shown) return;
            var set = c.art;
            w5FortifyMedal.SetIcons(set ? set.durability : null, WhiteboxIcons.Room(CampaignRoom.Control));
            var price = c.FortifyPrice;
            bool top = price == null, live = !top && c.CanAfford(price.Value);
            StoryText(w5FortifyTitle, ArenaText.Pair($"基地加固 {c.FortifyLevel} 级", $"Fortify · Lv {c.FortifyLevel}"));
            StoryText(w5FortifyHp, top ? ArenaText.Pair($"血量 {c.FortifyHpNow:0}", $"HP {c.FortifyHpNow:0}")
                : ArenaText.Pair("血量 ", "HP ") + B5NowAfter($"{c.FortifyHpNow:0}", $"{c.FortifyHpNext:0}", V5TitleKit.OrangeHi));
            w5FortifyTitle.fontSize = V5StoryKit.FontSize(21f);
            w5FortifyHp.fontSize = V5StoryKit.FontSize(19f);
            float textW = Mathf.Max(B5W(w5FortifyTitle, w5FortifyTitle.text), B5W(w5FortifyHp, w5FortifyHp.text));
            int n = top ? 0 : B5PriceOf(price);
            SetShown(w5FortifyDone, top);
            float tailW;
            if (top)
            {
                StoryText(w5FortifyDone, ArenaText.Pair("已满", "Full"));
                tailW = B5W(w5FortifyDone, w5FortifyDone.text);
            }
            else tailW = B5CoinsWidth(w5FortifyCoins, n, 28f, art);
            // The medal is left out where the gap is too narrow for it and the words.
            float medal = 14f + 50f + 14f + textW + 22f + tailW + 20f <= room ? 50f : 0f, lead = medal > 0f ? 14f + medal + 14f : 16f;
            SetShown(w5FortifyMedal, medal > 0f);
            float width = Mathf.Min(room, lead + textW + 22f + tailW + 20f);
            textW = width - (lead + 22f + tailW + 20f);
            float x = left + (room - width) * .5f, h = B5WalletH, topY = B5HeadMiddle - h * .5f;
            B5Place(w5Fortify, x, topY, width, h);
            if (medal > 0f) PlaceCentre(w5FortifyMedal.Rect, new Vector2(0f, .5f), new Vector2(Bp(14f + medal * .5f), 0f), Vector2.one * Bp(medal));
            float textLeft = lead;
            B5Rect(w5FortifyTitle.rectTransform, textLeft, 10f, textW, 30f);
            V5TitleKit.FitWidth(w5FortifyTitle, V5StoryKit.FontSize(21f), V5StoryKit.FontSize(14f));
            B5Rect(w5FortifyHp.rectTransform, textLeft, 42f, textW, 26f);
            V5TitleKit.FitWidth(w5FortifyHp, V5StoryKit.FontSize(19f), V5StoryKit.FontSize(13f));
            float tailLeft = width - 20f - tailW;
            if (top)
            {
                B5HideCoins(w5FortifyCoins);
                B5Rect(w5FortifyDone.rectTransform, tailLeft, 0f, tailW + 4f, h);
            }
            else B5LayCoins(w5FortifyCoins, n, tailLeft, h * .5f, 28f, art, false);
            // At the top it is no button any more; short of alloy it is grey.
            w5Fortify.Button.interactable = live;
            w5Fortify.Dim = !live && !top;
            w5Fortify.Alpha = live || top ? 1f : .55f;
            if (!top) B5AddFocus("h.fortify", w5Fortify);
        }

        /// <summary>The weapon Mk: its name, "Mk n → Mk n+1", what it does to every anomaly weapon's damage, its price and the orange
        /// button (grey while the stock is short), and the track of its steps: reached in copper, the next one ringed in orange, the bonus
        /// steps marked under their names.</summary>
        void LayMk5(V2Campaign c, bool art, StudyEntry mk)
        {
            SetShown(w5Mk, mk != null);
            if (mk == null) return;
            B5Rect(w5Mk, W5LeftX, W5BodyTop, W5ColW, W5MkH);
            bool top = mk.state == StudyState.Owned || mk.price == null, live = mk.state == StudyState.Available;
            StoryText(w5MkTitle, mk.title);
            B5Rect(w5MkTitle.rectTransform, 34f, 26f, 560f, 42f);
            V5TitleKit.FitWidth(w5MkTitle, V5StoryKit.FontSize(38f), V5StoryKit.FontSize(26f));
            StoryText(w5MkStep, B5NowAfter(mk.now, mk.after, V5TitleKit.OrangeHi));
            B5Rect(w5MkStep.rectTransform, 34f, 74f, 560f, 36f);
            V5TitleKit.FitWidth(w5MkStep, V5StoryKit.FontSize(32f), V5StoryKit.FontSize(22f));
            StoryText(w5MkEffect, B5Gain(mk.effect, V5TitleKit.OrangeHi));
            B5Rect(w5MkEffect.rectTransform, 34f, 116f, 640f, 30f);
            V5TitleKit.FitWidth(w5MkEffect, V5StoryKit.FontSize(22f), V5StoryKit.FontSize(15f));

            SetShown(w5MkButton, !top);
            SetShown(w5MkDone, top);
            if (top)
            {
                B5HideCoins(w5MkCoins);
                StoryText(w5MkDone, ArenaText.Pair("已满 ✓", "Full ✓"));
                B5Rect(w5MkDone.rectTransform, W5ColW - 34f - 300f, 36f, 300f, 62f);
            }
            else
            {
                w5MkButton.SetLook(live ? V5Plate.Look.Go : V5Plate.Look.Plate);
                StoryText(w5MkVerb, ArenaText.Pair("升级", "Upgrade"));
                w5MkVerb.fontSize = V5StoryKit.FontSize(24f);
                float bw = Mathf.Max(150f, B5W(w5MkVerb, w5MkVerb.text) + 48f);
                B5Place(w5MkButton, W5ColW - 34f - bw, 36f, bw, 62f);
                B5Live(w5MkButton, w5MkVerb, live, art ? V5TitleKit.Dark : Color.white);
                if (live) w5MkVerb.color = art ? V5TitleKit.Dark : Color.white;
                int n = B5PriceOf(mk);
                float cw = B5CoinsWidth(w5MkCoins, n, 36f, art);
                B5LayCoins(w5MkCoins, n, W5ColW - 34f - bw - 22f - cw, 67f, 36f, art, mk.state == StudyState.Unaffordable && c.CanAfford(mk.price ?? default));
                B5AddFocus("c.mk", w5MkButton);
            }
            LayMkTrack5(c, art, top);
        }

        void LayMkTrack5(V2Campaign c, bool art, bool top)
        {
            var r = c.rules;
            int count = r ? Mathf.Clamp(r.MaxAlloyMk, 1, 12) : 10, mk = Mathf.Clamp(c.TeamMk, 0, count);
            float inner = W5ColW - 68f, step = inner / count, middle = 172f;
            while (w5Ticks.Count < count)
            {
                var tick = new W5Tick
                {
                    diamond = V5TitleKit.Picture(w5Mk, "Tick" + (w5Ticks.Count + 1), null, Color.white),
                    label = V5StoryKit.Label(w5Mk, "TickName" + (w5Ticks.Count + 1), 22f, V5TitleKit.Ink3, TextAnchor.MiddleCenter, true),
                    bonus = V5StoryKit.Label(w5Mk, "TickBonus" + (w5Ticks.Count + 1), 21f, V5TitleKit.Paint, TextAnchor.MiddleCenter, true),
                };
                tick.diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                w5Ticks.Add(tick);
            }
            float first = 34f + step * .5f, last = 34f + step * (count - .5f);
            B5Rect(w5TrackLine.rectTransform, first, middle - 1.5f, last - first, 3f);
            bool filled = mk > 1;
            SetShown(w5TrackFill, filled);
            if (filled) B5Rect(w5TrackFill.rectTransform, first, middle - 1.5f, step * (mk - 1), 3f);
            var milestones = r ? r.weaponMkMilestones : null;
            string bonus = r ? $"+{r.mkMilestoneDamage * 100f:0}%" : "";
            for (int i = 0; i < w5Ticks.Count; i++)
            {
                var tick = w5Ticks[i];
                bool shown = i < count;
                SetShown(tick.diamond, shown);
                SetShown(tick.label, shown);
                if (!shown)
                {
                    SetShown(tick.bonus, false);
                    continue;
                }
                int level = i + 1;
                bool on = level <= mk, next = !top && level == mk + 1;
                float x = 34f + step * (i + .5f);
                var diamond = tick.diamond.rectTransform;
                diamond.anchorMin = diamond.anchorMax = new Vector2(0f, 1f);
                diamond.pivot = new Vector2(.5f, .5f);
                diamond.anchoredPosition = new Vector2(Bp(x), -Bp(middle));
                diamond.sizeDelta = Vector2.one * Bp(18f);
                V5TitleKit.Dress(tick.diamond, on && art ? V5StoryKit.Fill(W5CopperTop, W5CopperFoot) : WhiteboxSkin.Square,
                    on ? art ? Color.white : ResourceColours.Alloy : B5DiamondOff);
                B5Edge(tick.diamond, !on, next ? V5TitleKit.Orange : B5DiamondRim);
                StoryText(tick.label, "Mk" + level);
                tick.label.color = on ? ResourceColours.Alloy : next ? V5TitleKit.OrangeHi : V5TitleKit.Ink3;
                B5Rect(tick.label.rectTransform, x - step * .5f, 196f, step, 26f);
                V5TitleKit.FitWidth(tick.label, V5StoryKit.FontSize(22f), V5StoryKit.FontSize(14f));
                bool marked = milestones != null && Array.IndexOf(milestones, level) >= 0;
                SetShown(tick.bonus, marked);
                if (!marked) continue;
                StoryText(tick.bonus, bonus);
                B5Rect(tick.bonus.rectTransform, x - step * .5f, 224f, step, 26f);
                V5TitleKit.FitWidth(tick.bonus, V5StoryKit.FontSize(21f), V5StoryKit.FontSize(14f));
            }
        }

        /// <summary>Armor: its medal, "armor n → n+1", its max HP now and after, its price and button; "full" at the top.</summary>
        void LayArmor5(V2Campaign c, bool art, StudyEntry armor)
        {
            SetShown(w5Armor, armor != null);
            if (armor == null) return;
            B5Rect(w5Armor, W5LeftX, W5ArmorTop, W5ColW, W5ArmorH);
            bool top = armor.state == StudyState.Owned || armor.price == null, live = armor.state == StudyState.Available;
            w5ArmorMedal.SetIcons(armor.icon, WhiteboxIcons.Armor());
            PlaceCentre(w5ArmorMedal.Rect, new Vector2(0f, 1f), new Vector2(Bp(26f + 38f), -Bp(W5ArmorH * .5f)), Vector2.one * Bp(76f));
            float textLeft = 26f + 76f + 22f, right = W5ColW - 34f;
            SetShown(w5ArmorButton, !top);
            SetShown(w5ArmorDone, top);
            float tail;
            if (top)
            {
                B5HideCoins(w5ArmorCoins);
                StoryText(w5ArmorDone, ArenaText.Pair("已满 ✓", "Full ✓"));
                float w = B5W(w5ArmorDone, w5ArmorDone.text);
                B5Rect(w5ArmorDone.rectTransform, right - w - 4f, 0f, w + 4f, W5ArmorH);
                tail = w + 24f;
            }
            else
            {
                StoryText(w5ArmorVerb, ArenaText.Pair("升级", "Upgrade"));
                w5ArmorVerb.fontSize = V5StoryKit.FontSize(21f);
                float bw = Mathf.Max(120f, B5W(w5ArmorVerb, w5ArmorVerb.text) + 44f);
                B5Place(w5ArmorButton, right - bw, (W5ArmorH - 54f) * .5f, bw, 54f);
                B5Live(w5ArmorButton, w5ArmorVerb, live, V5TitleKit.Ink);
                int n = B5PriceOf(armor);
                float cw = B5CoinsWidth(w5ArmorCoins, n, 36f, art);
                B5LayCoins(w5ArmorCoins, n, right - bw - 22f - cw, W5ArmorH * .5f, 36f, art, armor.state == StudyState.Unaffordable && c.CanAfford(armor.price ?? default));
                B5AddFocus("c.armor", w5ArmorButton);
                tail = bw + 22f + cw + 24f;
            }
            float textW = right - tail - textLeft;
            StoryText(w5ArmorTitle, armor.title + " " + B5NowAfter(armor.now, armor.after, V5TitleKit.OrangeHi));
            B5Rect(w5ArmorTitle.rectTransform, textLeft, 22f, textW, 36f);
            V5TitleKit.FitWidth(w5ArmorTitle, V5StoryKit.FontSize(28f), V5StoryKit.FontSize(18f));
            StoryText(w5ArmorEffect, B5Gain(armor.effect, V5TitleKit.OrangeHi));
            B5Rect(w5ArmorEffect.rectTransform, textLeft, 64f, textW, 30f);
            V5TitleKit.FitWidth(w5ArmorEffect, V5StoryKit.FontSize(21f), V5StoryKit.FontSize(14f));
        }

        /// <summary>A weapon in hand: its picture, its name and which hand, and its Tame and Twist rows (the price and button, done, or
        /// the day they open). A hand without an anomaly weapon has no rows to buy: the pistol's show a dash, an empty hand shows nothing.</summary>
        void LayHand5(V2Campaign c, bool art, int index, V2Campaign.StudyTab tame, V2Campaign.StudyTab twist)
        {
            var hand = w5Hands[index];
            B5Rect(hand.plate.Rect, W5LeftX + index * (W5HandW + W5HandGap), W5HandsTop, W5HandW, B5Floor - W5HandsTop);
            var weapons = gm ? gm.runWeapons : null;
            var weapon = weapons && index < weapons.Hand.Length ? weapons.Hand[index] : null;
            string which = index == 0 ? ArenaText.Pair("主手", "Main hand") : ArenaText.Pair("副手", "Off hand");
            string dot = $" <color=#{HexOf(V5TitleKit.Ink3)}>·</color> ";
            var sprite = W5WeaponSprite(weapon, art);
            SetShown(hand.weapon, sprite);
            if (sprite)
            {
                hand.weapon.sprite = sprite;
                B5Rect(hand.weapon.rectTransform, 22f, 10f, 150f, 75f);
            }
            float titleLeft = sprite ? 186f : 26f;
            StoryText(hand.title, weapon == null ? ArenaText.Pair("空手", "Empty hand") + dot + which : weapon.FormName + dot + which);
            hand.title.color = weapon == null ? V5TitleKit.Ink3 : V5TitleKit.Ink;
            B5Rect(hand.title.rectTransform, titleLeft, 12f, W5HandW - titleLeft - 18f, 70f);
            // The longest English name ("Rocket Launcher · Main hand") fits at about 15 pixels.
            V5TitleKit.FitWidth(hand.title, V5StoryKit.FontSize(25f), V5StoryKit.FontSize(14f));
            for (int k = 0; k < 2; k++)
            {
                var row = hand.rows[k];
                var tab = k == 0 ? tame : twist;
                var feature = k == 0 ? V2Campaign.StudyFeature.Tame : V2Campaign.StudyFeature.Twist;
                row.entry = tab?.entries.Find(e => e.hand == index);
                SetShown(row.root, weapon != null);
                if (weapon == null) continue;
                B5Rect(row.root, 20f, 92f + k * 62f, W5HandW - 40f, 50f);
                LayHandRow5(c, art, row, V2Campaign.FeatureName(feature), weapon.IsAnomalous, $"c.hand{index}.{(k == 0 ? "tame" : "twist")}");
            }
        }

        /// <summary>One row of a hand's card: locked (the lock, its name grey, the day it opens), to buy (its price and button), done (its
        /// name and "done ✓"), or nothing to buy for this weapon (a dash).</summary>
        void LayHandRow5(V2Campaign c, bool art, W5Row row, string feature, bool anomalous, string id)
        {
            var entry = row.entry;
            bool locked = entry != null && entry.state == StudyState.Locked, owned = entry != null && entry.state == StudyState.Owned;
            bool buyable = entry != null && !locked && !owned && entry.buy != null;
            float w = W5HandW - 40f, h = 50f;
            // The backing: a dark inset chamfered strip, its rim warm while it can be bought.
            B5Flat(row.back, art, V5TitleKit.Chamfer, buyable ? W5RowOpen : W5RowBack, V5HudKit.ChamferDensity(8f, B5Css), WhiteboxSkin.PlateFill, WhiteboxSkin.Edge);
            if (art)
            {
                if (!row.back.TryGetComponent<UnityEngine.UI.Outline>(out var rim))
                {
                    rim = row.back.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    rim.useGraphicAlpha = false;
                    rim.effectDistance = new Vector2(1f, -1f);
                }
                rim.enabled = true;
                rim.effectColor = buyable ? W5RowEdgeOpen : W5RowEdge;
            }
            SetShown(row.lockIcon, locked);
            if (locked)
            {
                var set = campaign.art;
                row.lockIcon.sprite = art && set && set.locked ? set.locked : WhiteboxIcons.Mark(WhiteboxIcons.Glyph.Diamond);
                row.lockIcon.color = art && set && set.locked ? new Color(.85f, .87f, .92f, .8f) : B5Lock;
                B5Rect(row.lockIcon.rectTransform, 12f, 9f, 32f, 32f);
            }
            StoryText(row.name, feature);
            row.name.color = buyable || owned ? V5TitleKit.Ink : W5LockName;
            row.name.fontSize = V5StoryKit.FontSize(22f);
            float nameW = B5W(row.name, feature);
            B5Rect(row.name.rectTransform, 58f, 0f, nameW + 4f, h);
            SetShown(row.button, buyable);
            bool noted = !buyable;
            SetShown(row.note, noted);
            if (buyable)
            {
                bool live = entry.state == StudyState.Available;
                StoryText(row.verb, feature);
                row.verb.fontSize = V5StoryKit.FontSize(19f);
                float bw = Mathf.Max(92f, B5W(row.verb, feature) + 40f);
                B5Place(row.button, w - 6f - bw, (h - 40f) * .5f, bw, 40f);
                B5Live(row.button, row.verb, live, V5TitleKit.Ink);
                int n = B5PriceOf(entry);
                float cw = B5CoinsWidth(row.coins, n, 28f, art);
                B5LayCoins(row.coins, n, w - 6f - bw - 14f - cw, h * .5f, 28f, art, entry.state == StudyState.Unaffordable && c.CanAfford(entry.price ?? default));
                B5AddFocus(id, row.button);
                return;
            }
            B5HideCoins(row.coins);
            // Locked: the day it opens; done: what it has; no anomaly (the pistol): a dash.
            string note = locked ? entry.detail : owned ? entry.now + " ✓" : !anomalous || entry == null ? "—" : "";
            StoryText(row.note, note);
            row.note.color = owned ? V5TitleKit.Teal : B5Lock;
            float left = 58f + nameW + 16f;
            B5Rect(row.note.rectTransform, left, 0f, w - 18f - left, h);
            V5TitleKit.FitWidth(row.note, V5StoryKit.FontSize(20f), V5StoryKit.FontSize(13f));
        }

        Sprite W5WeaponSprite(RunWeapon weapon, bool art)
        {
            if (weapon == null) return null;
            int form = (int)weapon.form;
            if (!art) return WhiteboxIcons.Weapon(form);
            var set = gm && gm.hud && gm.hud.v2Hud ? gm.hud.v2Hud.art : null;
            return set && set.weaponSprites != null && form >= 0 && form < set.weaponSprites.Length ? set.weaponSprites[form] : null;
        }

        // ───── The energy research ─────

        /// <summary>The research tabs: body, skills and infusion as BuildStudy gives them (each one's research level apart, the items not
        /// revealed yet left out), every void tab merged into one, the final overload.</summary>
        static List<W5Research> W5ResearchOf(List<V2Campaign.StudyTab> study)
        {
            var list = new List<W5Research>();
            foreach (string key in new[] { "eng.body", "eng.skill", "eng.infuse" })
            {
                var tab = study.Find(t => t.key == key);
                if (tab == null) continue;
                var research = new W5Research { key = key, name = tab.name, locked = tab.locked, note = tab.lockedNote ?? "", empty = tab.emptyNote ?? "" };
                foreach (var entry in tab.entries)
                {
                    if (entry.id != null && entry.id.StartsWith("tier.", StringComparison.Ordinal)) research.tier = entry;
                    else if (entry.state != StudyState.Locked) research.entries.Add(entry);
                }
                list.Add(research);
            }
            W5Research voids = null;
            foreach (var tab in study)
            {
                if (tab.key == null || !tab.key.StartsWith("void.", StringComparison.Ordinal)) continue;
                voids ??= new W5Research { key = "void", name = tab.roomName, note = "", empty = tab.emptyNote ?? "" };
                foreach (var entry in tab.entries)
                    if (entry.state != StudyState.Locked) voids.entries.Add(entry);
            }
            if (voids != null) list.Add(voids);
            var overload = study.Find(t => t.key == "ctl.overload");
            if (overload != null)
            {
                var research = new W5Research { key = overload.key, name = overload.name, note = "", empty = overload.emptyNote ?? "" };
                research.entries.AddRange(overload.entries);
                list.Add(research);
            }
            return list;
        }

        void LayResearch5(V2Campaign c, bool art, List<V2Campaign.StudyTab> study)
        {
            w5Research = W5ResearchOf(study);
            // The tab shown: the one chosen before while it is open, else the skills, else the body.
            var shown = w5Research.Find(r => r.key == w5TabKey && !r.locked)
                        ?? w5Research.Find(r => r.key == "eng.skill" && !r.locked) ?? w5Research.Find(r => !r.locked);
            w5TabKey = shown?.key;
            LayResearchTabs5();

            // The cards: two to a row, as tall as the room allows within their least and most, scrolled when even the least does not fit.
            bool tiered = shown?.tier != null;
            float gridBottom = tiered ? B5Floor - W5TierH - W5GridGapY : B5Floor, gridH = gridBottom - W5GridTop;
            int n = shown?.entries.Count ?? 0, rows = (n + 1) / 2;
            float h = rows > 0 ? Mathf.Clamp((gridH - (rows - 1) * W5GridGapY) / rows, W5CardMinH, W5CardMaxH) : 0f;
            float contentH, cardW;
            bool scroll;
            int focusBefore = b5Focus.Count;
            // On the shortest cards "now → after" shares its row with the price and has only the room left of the coins. A long line
            // (the void room's) does not fit there even at its smallest letters and ran on under the price: such a tab is laid once
            // more with cards tall enough for the line to stand on its own row above the price.
            for (int pass = 0; ; pass++)
            {
                contentH = rows * h + Mathf.Max(0, rows - 1) * W5GridGapY;
                scroll = contentH > gridH + .5f;
                cardW = (W5ColW - W5GridGapX - (scroll ? W5ScrollW : 0f)) * .5f;
                B5Rect(w5Viewport, W5RightX, W5GridTop, W5ColW, gridH);
                w5Content.sizeDelta = new Vector2(0f, Bp(contentH));
                w5Scroll.vertical = scroll;
                if (!scroll) w5Content.anchoredPosition = Vector2.zero;
                bool spills = false;
                for (int i = 0; i < n; i++)
                {
                    var card = W5CardAt(i);
                    spills |= LayCard5(c, art, card, shown.entries[i], (i % 2) * (cardW + W5GridGapX), (i / 2) * (h + W5GridGapY), cardW, h, shown.key);
                }
                if (!spills || pass > 0 || h >= W5CardRoomyH) break;
                h = W5CardRoomyH;
                // The first laying's parts for the keys are entered again by the second.
                b5Focus.RemoveRange(focusBefore, b5Focus.Count - focusBefore);
            }
            for (int i = n; i < w5Cards.Count; i++)
            {
                w5Cards[i].entry = null;
                SetShown(w5Cards[i].plate, false);
            }
            string empty = shown == null ? "" : n > 0 ? "" : shown.locked ? shown.note : shown.empty;
            SetShown(w5Empty, !string.IsNullOrEmpty(empty));
            StoryText(w5Empty, empty);
            SetShown(w5ScrollTrack, scroll);
            SetShown(w5ScrollThumb, scroll);
            if (scroll)
            {
                B5Rect(w5ScrollTrack.rectTransform, W5RightX + W5ColW - 6f, W5GridTop, 6f, gridH);
                PlaceScrollThumb5();
            }
            LayTier5(c, art, tiered ? shown.tier : null);
        }

        /// <summary>The tabs in a row, their names at 24 pixels and 46 apart: the one shown in the light ink with the orange underline,
        /// the others dimmer, one not open yet grey with the day it opens after a dot.</summary>
        void LayResearchTabs5()
        {
            B5Rect(w5TabsRow, W5RightX, W5BodyTop, W5ColW, W5TabsH);
            B5Rect(w5TabsRule.rectTransform, 0f, W5TabsH - 1f, W5ColW, 1f);
            float x = 8f, gap = 46f;
            // Narrower gaps when the names would not fit in the column (long English names).
            float total = 0f;
            for (int i = 0; i < w5Research.Count; i++)
            {
                var tab = W5TabAt(i);
                tab.label.fontSize = V5StoryKit.FontSize(24f);
                total += B5W(tab.label, W5TabName(w5Research[i])) + (i > 0 ? gap : 0f);
            }
            if (total > W5ColW - 16f && w5Research.Count > 1) gap = Mathf.Max(18f, gap - (total - (W5ColW - 16f)) / (w5Research.Count - 1));
            for (int i = 0; i < w5Research.Count; i++)
            {
                var research = w5Research[i];
                var tab = W5TabAt(i);
                SetShown(tab.root, true);
                tab.key = research.key;
                tab.locked = research.locked;
                tab.chosen = research.key == w5TabKey;
                string name = W5TabName(research);
                StoryText(tab.label, name);
                tab.label.fontStyle = research.locked ? FontStyle.Normal : FontStyle.Bold;
                tab.label.fontSize = V5StoryKit.FontSize(24f);
                float w = Mathf.Min(B5W(tab.label, name), W5ColW - 8f - x);
                B5Rect(tab.root, x, 0f, w + 4f, W5TabsH);
                B5Rect(tab.label.rectTransform, 0f, 0f, w + 4f, W5TabsH - 6f);
                V5TitleKit.FitWidth(tab.label, V5StoryKit.FontSize(24f), V5StoryKit.FontSize(16f));
                SetShown(tab.underline, tab.chosen);
                if (tab.chosen) B5Rect(tab.underline.rectTransform, -6f, W5TabsH - 4f, w + 16f, 4f);
                W5MarkTab(tab, false);
                var current = tab;
                B5AddFocus("c.tab." + research.key, tab.root, () =>
                {
                    ShowResearchTab5(current.key, true);
                    return !current.locked;
                }, on => W5MarkTab(current, on));
                x += w + gap;
            }
            for (int i = w5Research.Count; i < w5Tabs.Count; i++) SetShown(w5Tabs[i].root, false);
        }

        static string W5TabName(W5Research research) =>
            research.locked && !string.IsNullOrEmpty(research.note) ? research.name + " · " + research.note : research.name;

        /// <summary>A tab's ink: the one shown light, a focused one warm, the others dimmer, one not open yet grey.</summary>
        static void W5MarkTab(W5Tab tab, bool focused)
        {
            tab.label.color = focused ? V5TitleKit.OrangeHi : tab.chosen ? V5TitleKit.Ink : tab.locked ? B5Lock : V5TitleKit.Ink2;
        }

        /// <summary>One research card: its plate (teal once owned) with the rarity's bar on the left, its medal ringed in the rarity's
        /// colour, its name, "now → after", and at the bottom right its price and button (grey while it cannot be bought), or what it has
        /// once owned.</summary>
        /// True when the card's "now → after" line shares its row with the price and does not fit in the room left of it.
        bool LayCard5(V2Campaign c, bool art, W5Card card, StudyEntry entry, float left, float top, float w, float h, string tabKey)
        {
            card.entry = entry;
            SetShown(card.plate, true);
            B5Rect(card.plate.Rect, left, top, w, h);
            bool owned = entry.state == StudyState.Owned, live = entry.state == StudyState.Available;
            card.plate.SetLook(owned ? V5HudKit.PlateLook.Teal : V5HudKit.PlateLook.Plain);
            var rarity = B5RarityColour(entry.rarity);
            card.bar.color = rarity;
            B5Rect(card.bar.rectTransform, 3f, 15f, 7f, h - 30f);
            float m = h >= 196f ? 88f : 70f, medalTop = h >= 196f ? 30f : 22f;
            card.medal.SetSize(Bp(m));
            card.medal.SetIcons(entry.icon, B5FlatIcon(entry));
            card.medal.SetRing(owned ? V5TitleKit.Teal : rarity);
            card.medal.SetGlow(entry.rarity == CardRarity.Void ? V5StoryKit.VioletGlow : Color.clear);
            PlaceCentre(card.medal.Rect, new Vector2(0f, 1f), new Vector2(Bp(28f + m * .5f), -Bp(medalTop + m * .5f)), Vector2.one * Bp(m));
            float x = 28f + m + 20f, titleTop = medalTop + 2f, valueTop = titleTop + 36f, right = w - 18f;
            StoryText(card.title, entry.title);
            B5Rect(card.title.rectTransform, x, titleTop, right - x, 32f);
            // Down to 11: the void room's enchant cards name the weapon, its anomaly and its Mk, and in English are wider than the box at 16
            // (the longest, the rocket launcher's second anomaly at Mk 10, needs 11).
            V5TitleKit.FitWidth(card.title, V5StoryKit.FontSize(25f), V5StoryKit.FontSize(11f));

            SetShown(card.button, !owned && entry.buy != null);
            SetShown(card.done, owned);
            float valueRight = right;
            if (owned)
            {
                B5HideCoins(card.coins);
                StoryText(card.done, W5OwnedWords(entry));
                B5Rect(card.done.rectTransform, x, h - 26f - 34f, w - 28f - x, 34f);
            }
            else if (entry.buy != null)
            {
                string verb = W5Verb(entry);
                StoryText(card.verb, verb);
                card.verb.fontSize = V5StoryKit.FontSize(19f);
                float bw = Mathf.Max(96f, B5W(card.verb, verb) + 36f), buttonTop = h - 16f - 46f;
                B5Place(card.button, w - 18f - bw, buttonTop, bw, 46f);
                B5Live(card.button, card.verb, live, V5TitleKit.Ink);
                int n = B5PriceOf(entry);
                float cw = B5CoinsWidth(card.coins, n, 32f, art), coinsLeft = w - 18f - bw - 16f - cw;
                B5LayCoins(card.coins, n, coinsLeft, buttonTop + 23f, 32f, art, entry.state == StudyState.Unaffordable && W5PriceHeld(c, entry));
                // On a card too short for two rows the price row would meet "now → after" (the button's top above the foot of the
                // line's box, 34 under its top, with W5LineGap of air): the line ends before the price.
                if (buttonTop < valueTop + 34f + W5LineGap) valueRight = coinsLeft - 12f;
                B5AddFocus("c.card." + entry.id, card.button);
            }
            else B5HideCoins(card.coins);
            var gain = tabKey.StartsWith("void", StringComparison.Ordinal) ? V5HudKit.VioletHi
                : tabKey == "ctl.overload" ? V5TitleKit.OrangeHi : ResourceColours.Energy;
            StoryText(card.value, B5NowAfter(entry.now, entry.after, gain));
            B5Rect(card.value.rectTransform, x, valueTop, Mathf.Max(40f, valueRight - x), 34f);
            V5TitleKit.FitWidth(card.value, V5StoryKit.FontSize(27f), V5StoryKit.FontSize(15f));
            return valueRight < right && card.value.preferredWidth > card.value.rectTransform.rect.width + 1f;
        }

        // The air kept between the foot of a card's "now → after" line and the top of its button, and the least height at which the
        // line stands on its own row, clear above the price and its button (the button's top lies 62 above the card's foot; LayCard5
        // gives the line the card's whole width from 162 up).
        const float W5LineGap = 6f;
        const float W5CardRoomyH = 164f;

        static readonly Vector3[] w5LineCorners = new Vector3[4];

        /// <summary>For automated checks: every research card shown with a price, by its entry's id: where the letters of its
        /// "now → after" line stand and where its price (coins and numbers) stands, in screen pixels.</summary>
        public List<(string id, Rect letters, Rect price)> DebugResearchCardLines()
        {
            var lines = new List<(string, Rect, Rect)>();
            foreach (var card in w5Cards)
            {
                if (card.entry == null || !card.plate || !card.plate.gameObject.activeInHierarchy || !card.value || card.coins == null) continue;
                bool priced = false;
                float left = float.MaxValue, bottom = float.MaxValue, rightEdge = float.MinValue, top = float.MinValue;
                for (int k = 0; k < card.coins.holes.Length; k++)
                {
                    foreach (var part in new RectTransform[] { card.coins.holes[k] ? card.coins.holes[k].rectTransform : null, card.coins.amounts[k] ? card.coins.amounts[k].rectTransform : null })
                    {
                        if (!part || !part.gameObject.activeInHierarchy) continue;
                        part.GetWorldCorners(w5LineCorners);
                        priced = true;
                        left = Mathf.Min(left, w5LineCorners[0].x); bottom = Mathf.Min(bottom, w5LineCorners[0].y);
                        rightEdge = Mathf.Max(rightEdge, w5LineCorners[2].x); top = Mathf.Max(top, w5LineCorners[2].y);
                    }
                }
                if (!priced) continue;
                var value = card.value.rectTransform;
                value.GetWorldCorners(w5LineCorners);
                float boxWidth = w5LineCorners[2].x - w5LineCorners[0].x, scale = value.rect.width > 0f ? boxWidth / value.rect.width : 1f;
                var letters = Rect.MinMaxRect(w5LineCorners[0].x, w5LineCorners[0].y, w5LineCorners[0].x + card.value.preferredWidth * scale, w5LineCorners[2].y);
                lines.Add((card.entry.id ?? "", letters, Rect.MinMaxRect(left, bottom, rightEdge, top)));
            }
            return lines;
        }

        /// <summary>For automated checks: every research card shown with its button, by its entry's id: where the letters of its
        /// "now → after" line stand and where the button stands, in screen pixels.</summary>
        public List<(string id, Rect letters, Rect button)> DebugResearchCardButtons()
        {
            var lines = new List<(string, Rect, Rect)>();
            foreach (var card in w5Cards)
            {
                if (card.entry == null || !card.plate || !card.plate.gameObject.activeInHierarchy || !card.value || !card.button || !card.button.gameObject.activeInHierarchy) continue;
                var value = card.value.rectTransform;
                value.GetWorldCorners(w5LineCorners);
                float boxWidth = w5LineCorners[2].x - w5LineCorners[0].x, scale = value.rect.width > 0f ? boxWidth / value.rect.width : 1f;
                var letters = Rect.MinMaxRect(w5LineCorners[0].x, w5LineCorners[0].y, w5LineCorners[0].x + card.value.preferredWidth * scale, w5LineCorners[2].y);
                card.button.Rect.GetWorldCorners(w5LineCorners);
                lines.Add((card.entry.id ?? "", letters, Rect.MinMaxRect(w5LineCorners[0].x, w5LineCorners[0].y, w5LineCorners[2].x, w5LineCorners[2].y)));
            }
            return lines;
        }

        /// <summary>For automated checks: every research card shown, by its entry's id: how wide its title's letters are at the size
        /// they are drawn in, and how wide the title's box is (the same units).</summary>
        public List<(string id, float letters, float box)> DebugResearchCardTitles()
        {
            var titles = new List<(string, float, float)>();
            foreach (var card in w5Cards)
            {
                if (card.entry == null || !card.plate || !card.plate.gameObject.activeInHierarchy || !card.title) continue;
                titles.Add((card.entry.id ?? "", card.title.preferredWidth, card.title.rectTransform.rect.width));
            }
            return titles;
        }

        /// <summary>The stock pays for an entry that cannot be bought (so its price is grey, not red: it waits for something else).</summary>
        bool W5PriceHeld(V2Campaign c, StudyEntry entry) =>
            c.CanAfford(entry.price ?? default) && c.VoidEnergy >= entry.voidEnergyCost && c.VoidAlloy >= entry.voidAlloyCost;

        /// <summary>A card's button: learn a skill not learned yet, raise one learned, research a body stat or a research level, infuse,
        /// buy a void item or the overload, upgrade the weapon Mk or armor, Tame or Twist.</summary>
        static string W5Verb(StudyEntry entry)
        {
            string id = entry.id ?? "";
            if (id.StartsWith("skill.", StringComparison.Ordinal))
                return entry.now == ArenaText.Pair("未学", "Not learned") ? ArenaText.Pair("学习", "Learn") : ArenaText.Pair("升级", "Upgrade");
            if (id.StartsWith("stat.", StringComparison.Ordinal) || id.StartsWith("tier.", StringComparison.Ordinal)) return ArenaText.Pair("研究", "Research");
            if (id.StartsWith("infuse.", StringComparison.Ordinal)) return ArenaTerms.Upgrade(UpgradeAxis.Infusion);
            if (id.StartsWith("tame.", StringComparison.Ordinal)) return ArenaTerms.Upgrade(UpgradeAxis.Tame);
            if (id.StartsWith("twist.", StringComparison.Ordinal)) return ArenaTerms.Upgrade(UpgradeAxis.Twist);
            if (id == "mk" || id == "armor") return ArenaText.Pair("升级", "Upgrade");
            return ArenaText.Pair("购买", "Buy");
        }

        /// <summary>What an owned card says: an infusion or a void item is owned; the rest are at their top.</summary>
        static string W5OwnedWords(StudyEntry entry)
        {
            string id = entry.id ?? "";
            return id.StartsWith("infuse.", StringComparison.Ordinal) || id.StartsWith("void.", StringComparison.Ordinal)
                ? ArenaText.Pair("已拥有 ✓", "Owned ✓") : ArenaText.Pair("已满 ✓", "Full ✓");
        }

        /// <summary>The research level's card at the column's foot: a gem in the colour of the rarest items the next level opens, "research
        /// level n: opens these colours", its price and button; "full" at the top.</summary>
        void LayTier5(V2Campaign c, bool art, StudyEntry tier)
        {
            w5TierEntry = tier;
            SetShown(w5Tier, tier != null);
            if (tier == null) return;
            B5Rect(w5Tier, W5RightX, B5Floor - W5TierH, W5ColW, W5TierH);
            bool top = tier.state == StudyState.Owned || tier.price == null, live = tier.state == StudyState.Available;
            var colour = B5RarityColour(tier.rarity);
            var gem = w5TierGem.rectTransform;
            gem.anchorMin = gem.anchorMax = new Vector2(0f, 1f);
            gem.pivot = new Vector2(.5f, .5f);
            gem.anchoredPosition = new Vector2(Bp(26f + 22f), -Bp(W5TierH * .5f));
            gem.sizeDelta = Vector2.one * Bp(36f);
            V5TitleKit.Dress(w5TierGem, art ? V5StoryKit.Fill(Color.Lerp(colour, Color.white, .55f), Color.Lerp(colour, Color.black, .35f)) : WhiteboxSkin.Square,
                art ? Color.white : colour);
            B5Edge(w5TierGem, !art, Color.Lerp(colour, Color.white, .5f));
            float right = W5ColW - 30f, tail;
            SetShown(w5TierButton, !top);
            SetShown(w5TierDone, top);
            if (top)
            {
                B5HideCoins(w5TierCoins);
                StoryText(w5TierDone, ArenaText.Pair("已满 ✓", "Full ✓"));
                float w = B5W(w5TierDone, w5TierDone.text);
                B5Rect(w5TierDone.rectTransform, right - w - 4f, 0f, w + 4f, W5TierH);
                tail = w + 24f;
                StoryText(w5TierText, ArenaText.Pair($"研究 {tier.now}", $"Research {tier.now}"));
            }
            else
            {
                StoryText(w5TierVerb, ArenaText.Pair("研究", "Research"));
                w5TierVerb.fontSize = V5StoryKit.FontSize(21f);
                float bw = Mathf.Max(120f, B5W(w5TierVerb, w5TierVerb.text) + 44f);
                B5Place(w5TierButton, right - bw, (W5TierH - 54f) * .5f, bw, 54f);
                B5Live(w5TierButton, w5TierVerb, live, V5TitleKit.Ink);
                int n = B5PriceOf(tier);
                float cw = B5CoinsWidth(w5TierCoins, n, 32f, art);
                B5LayCoins(w5TierCoins, n, right - bw - 22f - cw, W5TierH * .5f, 32f, art, tier.state == StudyState.Unaffordable && c.CanAfford(tier.price ?? default));
                B5AddFocus("c.tier", w5TierButton);
                tail = bw + 22f + cw + 24f;
                string effect = string.IsNullOrEmpty(tier.effect) ? "" : $"<color=#{HexOf(colour)}>{tier.effect}</color>";
                StoryText(w5TierText, ArenaText.Pair($"研究 {tier.after}：", $"Research {tier.after}: ") + effect);
            }
            float textLeft = 96f;
            B5Rect(w5TierText.rectTransform, textLeft, 0f, right - tail - textLeft, W5TierH);
            V5TitleKit.FitWidth(w5TierText, V5StoryKit.FontSize(26f), V5StoryKit.FontSize(16f));
        }

        /// <summary>The scroll bar's thumb, as long as the share of the cards in view and as far down as the list is scrolled.</summary>
        void PlaceScrollThumb5()
        {
            if (!w5ScrollThumb || !w5ScrollThumb.gameObject.activeSelf || !w5Viewport) return;
            float view = w5Viewport.rect.height, content = Mathf.Max(view, w5Content.rect.height);
            if (view <= 0f) return;
            float share = view / content, length = Mathf.Max(Bp(30f), view * share), travel = view - length;
            float at = content > view ? Mathf.Clamp01(w5Content.anchoredPosition.y / (content - view)) : 0f;
            var track = w5ScrollTrack.rectTransform;
            var thumb = w5ScrollThumb.rectTransform;
            thumb.anchorMin = thumb.anchorMax = thumb.pivot = new Vector2(0f, 1f);
            thumb.anchoredPosition = new Vector2(track.anchoredPosition.x, track.anchoredPosition.y - travel * at);
            thumb.sizeDelta = new Vector2(track.sizeDelta.x, length);
        }

        /// <summary>The keys' focus moved onto a card below or above the cards in view: the list scrolls so it shows whole.</summary>
        void RevealInWorkshop5(RectTransform rect)
        {
            if (!w5Content || !rect || !rect.IsChildOf(w5Content) || !w5Scroll.vertical) return;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            float top = -w5Content.InverseTransformPoint(corners[1]).y, bottom = -w5Content.InverseTransformPoint(corners[0]).y;
            float view = w5Viewport.rect.height, y = w5Content.anchoredPosition.y;
            // The card the button stands on shows whole: a margin over the button and under it.
            float margin = Bp(W5CardMaxH);
            if (top - margin < y) y = top - margin;
            else if (bottom > y + view) y = bottom - view + Bp(16f);
            w5Content.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, w5Content.rect.height - view)));
        }

        // ───── Where the base tour points ─────

        /// <summary>The research cards in view while the workshop shows the skills tab (the base tour's first step), else null.</summary>
        RectTransform W5SkillCards => BaseScreenShown == V2Campaign.BaseScreen.Workshop && w5TabKey == "eng.skill" && w5Viewport && w5Viewport.gameObject.activeInHierarchy ? w5Viewport : null;

        /// <summary>The weapon Mk's card while the workshop shows it and it can be raised.</summary>
        RectTransform W5MkCard => BaseScreenShown == V2Campaign.BaseScreen.Workshop && w5Mk && w5Mk.gameObject.activeInHierarchy && w5MkButton.gameObject.activeSelf ? w5Mk : null;

        /// <summary>A skill can be learned or raised now (the base tour's first step), as the workshop last drew its skills tab.</summary>
        bool W5SkillBuyable() => w5Research.Find(r => r.key == "eng.skill") is { locked: false } skills
                                 && skills.entries.Exists(e => e.id != null && e.id.StartsWith("skill.", StringComparison.Ordinal) && e.state == StudyState.Available);

        /// <summary>The weapon Mk can be raised now (the base tour's second step), as the workshop last drew it.</summary>
        bool W5MkBuyable() => w5MkEntry is { state: StudyState.Available };
    }
}
