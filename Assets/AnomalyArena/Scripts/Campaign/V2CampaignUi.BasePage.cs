using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The base panel: the full-screen backing every base visit opens, and the page the English Alpha was graded with. On every page but
    /// that Alpha (V5HudKit.On) the fifth pass's two pages are drawn over it instead, the workshop and the rocket page (V5BasePage.cs,
    /// V5WorkshopPage.cs, V5RocketPage.cs): the visit opens on the page the campaign asks for (V2Campaign.BaseScreenNow) and the two
    /// switch on its request (BaseScreenRequested); the old page below stays built and hidden. The old page itself: top, where and when
    /// and the three currencies with their icons; left, one tab per room with its icon and its state; middle, the room's picture (its
    /// whitebox blocks with the art off), its name and the few lines that matter now; right, what can be done there, one button each, its
    /// price drawn with the currencies' icons; bottom, a strip kept free for the subtitle (V2CampaignUi.BaseSubtitleBottom), and the ways
    /// out (V2Campaign.BaseExits). It buys through the same interface as the new pages: the base's fortification at Central Control, the
    /// ship's three parts, the fuel and the lamps at the launch bay, the workshop's items in the study window.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The page's frame (canvas units, 1600 x 900 reference) and its columns. The subtitle's strip lies between the body's end
        // (BodyBottom from the page's top) and the ways out.
        const float PageWidth = 1520f, PageHeight = 860f, PageHeader = 74f, PageTop = 98f;
        const float TabsWidth = 320f, RoomTabHeight = 112f, RoomTabGap = 14f;
        const float MiddleLeft = 352f, MiddleWidth = 716f, PictureHeight = 316f, BodyBottom = 648f;
        const float ActionsWidth = 420f, ActionHeight = 84f, ActionGap = 14f, ActionHeightTight = 70f, ActionGapTight = 10f;
        const float LeaveHeight = 64f, LeaveWidth = 440f, LeaveGap = 20f;
        const float StripStepNamed = 200f, StripStepPlain = 132f;
        static readonly Color PageBack = new(.02f, .04f, .08f, 1f);

        CampaignRoom tab = CampaignRoom.Control;
        RectTransform basePage, tabColumn, actionColumn, leaveRow;
        Text baseHeading, roomTitle, roomBody;
        RawImage roomPicture;
        CurrencyStrip baseStrip;
        readonly List<GameObject> actionButtons = new();
        int panelVersion = -1;

        void BuildBasePanel()
        {
            baseScreen = Box("BasePanel", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, PageBack);
            StretchFull(baseScreen);
            baseScreen.GetComponent<Image>().raycastTarget = true; // the page is the whole screen: no click reaches the game under it
            basePage = MakeRect("Page", baseScreen, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(PageWidth, PageHeight));
            baseHeading = Label("Heading", basePage, "", 40, Color.white, TextAnchor.MiddleLeft);
            baseHeading.fontStyle = FontStyle.Bold;
            Anchor(baseHeading.rectTransform, new Vector2(0f, 1f), Vector2.zero, new Vector2(760f, 60f), new Vector2(0f, 1f));
            baseStrip = BuildCurrencyStrip(basePage, new Vector2(1f, 1f), new Vector2(0f, -6f), true);
            Box("Rule", basePage, new Vector2(.5f, 1f), new Vector2(0f, -PageHeader), new Vector2(PageWidth, 2f), new Vector2(.5f, 1f), PanelEdge);
            tabColumn = MakeRect("Tabs", basePage, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -PageTop), new Vector2(TabsWidth, 500f));
            tabColumn.pivot = new Vector2(0f, 1f);
            roomPicture = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            roomPicture.rectTransform.SetParent(basePage, false);
            Anchor(roomPicture.rectTransform, new Vector2(0f, 1f), new Vector2(MiddleLeft, -PageTop), new Vector2(MiddleWidth, PictureHeight), new Vector2(0f, 1f));
            roomPicture.raycastTarget = false;
            float titleTop = PageTop + PictureHeight + 14f;
            roomTitle = Label("RoomTitle", basePage, "", 36, Accent, TextAnchor.MiddleLeft);
            roomTitle.fontStyle = FontStyle.Bold;
            Anchor(roomTitle.rectTransform, new Vector2(0f, 1f), new Vector2(MiddleLeft, -titleTop), new Vector2(MiddleWidth, 48f), new Vector2(0f, 1f));
            float bodyTop = titleTop + 58f;
            roomBody = Label("RoomBody", basePage, "", 22, Soft, TextAnchor.UpperLeft);
            roomBody.lineSpacing = 1.25f;
            Anchor(roomBody.rectTransform, new Vector2(0f, 1f), new Vector2(MiddleLeft, -bodyTop), new Vector2(MiddleWidth, BodyBottom - bodyTop), new Vector2(0f, 1f));
            actionColumn = MakeRect("Actions", basePage, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -PageTop), new Vector2(ActionsWidth, BodyBottom - PageTop));
            actionColumn.pivot = new Vector2(1f, 1f);
            leaveRow = MakeRect("Leave", basePage, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, LeaveHeight));
            leaveRow.pivot = new Vector2(.5f, 0f);
            if (V5HudKit.On)
            {
                // The fifth pass's two pages over this one, which stays built and hidden (V5BasePage.cs); they switch on the campaign's request.
                BuildBase5();
                campaign.BaseScreenRequested += OnBaseScreenRequested;
            }
            baseScreen.gameObject.SetActive(false);
        }

        /// <summary>Opens the base panel (V2Campaign.OpenBase): on the fifth pass, on the page the campaign asks for (the workshop from
        /// the base door and on Day 0's arrival, the rocket page from the ship).</summary>
        public void ShowBase()
        {
            ApplyFont();
            panelVersion = -1;
            tab = CampaignRoom.Control;
            if (b5Built) OpenBase5(campaign.BaseScreenNow);
            baseScreen.gameObject.SetActive(true);
            RefreshBasePanel(true);
        }

        public void HideBase()
        {
            baseScreen.gameObject.SetActive(false);
            if (b5Built) CloseBase5();
        }

        /// <summary>Shows the base page again on the tab (the page) it had (after a weapon's card was closed).</summary>
        public void ResumeBase()
        {
            ApplyFont();
            panelVersion = -1;
            if (b5Built) b5OpenedAt = Time.unscaledTime;
            baseScreen.gameObject.SetActive(true);
            RefreshBasePanel(true);
        }

        /// <summary>Opens one room's tab of the open base page, as its tab button does (also for automated checks). On the fifth pass's
        /// pages the launch bay is the rocket page and every other room the workshop.</summary>
        public void ShowTab(CampaignRoom room)
        {
            if (!BaseOpen) return;
            if (b5Built)
            {
                campaign.RequestBaseScreen(room == CampaignRoom.Ship ? V2Campaign.BaseScreen.Rocket : V2Campaign.BaseScreen.Workshop);
                return;
            }
            tab = room;
            RefreshBasePanel(true);
        }

        /// <summary>A button's action; on failure the one refusal sound and its reason, unless it has none (a purchase that found nothing
        /// to give says so itself). A purchase that goes through makes the sound of what it changed (CampaignAudio watches the base).</summary>
        void Act(Func<bool> action, string failure)
        {
            if (!action())
            {
                PlayRefused(); // not enough material, or the room cannot do it now
                if (!string.IsNullOrEmpty(failure)) Flash(failure);
            }
            RefreshBasePanel(true);
        }

        /// <summary>A purchase refused: the one refusal sound, the fifth pass's iron-part refusal, or the campaign's own without it.</summary>
        static void PlayRefused()
        {
            if (!V5UiSound.Play(V5UiSound.Ui.Denied)) CampaignAudio.Play(CampaignSfx.UpgradeDenied);
        }

        void RefreshBasePanel(bool rebuild)
        {
            // The fifth pass draws its two pages instead (V5BasePage.cs); the old page is not drawn under them.
            if (b5Built)
            {
                RefreshBase5(rebuild);
                return;
            }
            var c = campaign;
            var r = c.rules;
            baseHeading.text = c.BaseAfterTutorial ? ArenaText.Pair("基地 · 第 0 天", "BASE · DAY 0")
                : c.BaseFromDusk ? ArenaText.Pair($"基地 · 第 {c.Day} 天下午", $"BASE · DAY {c.Day}, AFTERNOON")
                : ArenaText.Pair($"基地 · 第 {c.Day} 天夜晚", $"BASE · DAY {c.Day}, NIGHT");
            RefreshCurrencyStrip(baseStrip);
            int version = Hash(c);
            if (!rebuild && version == panelVersion) return;
            panelVersion = version;

            // Tabs: the room's icon, its name, its state.
            foreach (Transform child in tabColumn) Destroy(child.gameObject);
            for (int i = 0; i < 4; i++)
            {
                var room = (CampaignRoom)i;
                string state = room switch
                {
                    CampaignRoom.Control => ArenaText.Pair($"加固 {c.FortifyLevel} / {c.FortifyTop} 级", $"Fortified {c.FortifyLevel} / {c.FortifyTop}"),
                    CampaignRoom.Ship => ArenaText.Pair($"回家还差 {c.HomeStepsLeft} 步", $"{c.HomeStepsLeft} steps from home"),
                    _ => ArenaText.Pair("可以研习", "Ready to study")
                };
                AddRoomTab(i, room, V2Campaign.RoomName(room), state);
            }

            // Picture, name, the lines that matter.
            ShowRoomPicture(Picture(tab));
            ShowRoomBlocks(tab, !Art.Enabled && !roomPicture.texture);
            roomTitle.text = V2Campaign.RoomName(tab);
            roomTitle.color = Accent;
            roomBody.text = RoomText(tab);

            // Actions: a title each, and the price with the currencies' icons.
            foreach (var go in actionButtons) Destroy(go);
            actionButtons.Clear();
            var actions = new List<(string label, CampaignCost? cost, Func<bool> act, string fail, bool enabled)>();
            switch (tab)
            {
                case CampaignRoom.Control:
                    if (c.FortifyPrice is { } fp)
                        actions.Add((ArenaText.Pair($"基地加固到 {c.FortifyLevel + 1} 级（基地生命 {c.FortifyHpNext:0}）", $"Fortify the base to level {c.FortifyLevel + 1} (base HP {c.FortifyHpNext:0})"),
                            fp, c.TryFortify, Why(fp), true));
                    // The last day's overload is bought in the study window (V2CampaignUi.Study.cs), which says what it does.
                    if (c.OverloadOpen) actions.Add((ArenaText.Pair("决战过载", "Final overload"), null, () => { OpenStudy("ctl.overload"); return true; }, "", true));
                    break;
                case CampaignRoom.Ship:
                    foreach (var part in c.ShipParts())
                    {
                        if (part.done) continue;
                        var kind = part.kind;
                        string label = ArenaText.IsChinese ? part.verb + part.name : $"{part.verb} the {part.name.ToLowerInvariant()}";
                        actions.Add((label, part.price, () => c.TryBuyShipPart(kind), Why(part.price), true));
                    }
                    if (c.FuelTankFitted && !c.FuelFull)
                        actions.Add((ArenaText.Pair($"加注燃料（{c.Fuel}/{c.FuelTop}）", $"Refuel ({c.Fuel}/{c.FuelTop})"), null, c.TryAddFuel,
                            ArenaText.Pair("没有暗物质可加", "No dark matter to pour in"), c.CanAddFuel));
                    actions.Add((ArenaText.Pair($"制造矿灯（现有 {c.LampStock} 盏）", $"Craft a mining lamp (you have {c.LampStock})"), c.LampPrice, c.TryCraftLamp,
                        Why(c.LampPrice), c.LampsCraftable));
                    if (c.CanIgnite) actions.Add((ArenaText.Pair("启动点火程序", "Start the ignition sequence"), null, () => { c.AskIgnite(); return true; }, "", true));
                    break;
                default:
                    // What the weapons and the energy research sell is bought in the study window (V2CampaignUi.Study.cs), which shows
                    // every item with its rarity and numbers.
                    actions.Add((ArenaText.Pair("研习", "Study"), null, () => { OpenStudy(tab == CampaignRoom.Armament ? "arm.upgrade" : "eng.body"); return true; }, "", true));
                    break;
            }
            for (int i = 0; i < actions.Count; i++)
            {
                var (label, price, act, fail, enabled) = actions[i];
                actionButtons.Add(AddAction(i, actions.Count, label, price, act, fail, enabled));
            }

            // The ways out: the campaign's (V2Campaign.BaseExits), each with its line in brackets.
            foreach (Transform child in leaveRow) Destroy(child.gameObject);
            var leaves = new List<(string label, Action act)>();
            foreach (var exit in c.BaseExits())
                leaves.Add((string.IsNullOrEmpty(exit.note) ? exit.label : exit.label + ArenaText.Pair($"（{exit.note}）", $" ({exit.note})"), exit.go));
            if (leaves.Count == 0) leaves.Add((ArenaText.Pair("出发", "Head out"), c.LeaveBase));
            for (int i = 0; i < leaves.Count; i++)
            {
                var (label, act) = leaves[i];
                var b = MakeButton(leaveRow, label, new Vector2((i - (leaves.Count - 1) * .5f) * (LeaveWidth + LeaveGap), 0f), new Vector2(LeaveWidth, LeaveHeight), () => act(), true);
                var rect = (RectTransform)b.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f); rect.pivot = new Vector2(.5f, 0f);
                var text = b.GetComponentInChildren<Text>();
                text.resizeTextMinSize = 14; text.resizeTextMaxSize = 24;
            }
        }

        // ───── The page's parts ─────

        /// <summary>One room's tab: its icon, its name and its state under the name; the open one is lit.</summary>
        void AddRoomTab(int index, CampaignRoom room, string name, string state)
        {
            var b = MakeButton(tabColumn, "", new Vector2(0f, -index * (RoomTabHeight + RoomTabGap)), new Vector2(TabsWidth, RoomTabHeight), () => { tab = room; RefreshBasePanel(true); }, room == tab);
            var rect = (RectTransform)b.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f); rect.pivot = new Vector2(0f, 1f);
            var sprite = RoomIcon(room);
            float left = 18f;
            if (sprite)
            {
                var icon = Icon(rect, new Vector2(0f, .5f), new Vector2(14f, 0f), 72f);
                icon.sprite = sprite;
                left = 98f;
            }
            var title = b.GetComponentInChildren<Text>();
            title.text = name;
            title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.LowerLeft;
            title.resizeTextMinSize = 16; title.resizeTextMaxSize = 26;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            Stretch(title.rectTransform, left, 14f, 10f, RoomTabHeight * .5f);
            var line = Label("State", rect, state, 18, Soft, TextAnchor.UpperLeft);
            Stretch(line.rectTransform, left, RoomTabHeight * .5f + 4f, 10f, 10f);
        }

        /// <summary>One action: its title, and under it the price with the currencies' icons (none for a free one).</summary>
        GameObject AddAction(int index, int count, string title, CampaignCost? price, Func<bool> act, string fail, bool enabled)
        {
            bool tight = count > 6;
            float height = tight ? ActionHeightTight : ActionHeight, gap = tight ? ActionGapTight : ActionGap;
            var b = MakeButton(actionColumn, title, new Vector2(0f, -index * (height + gap)), new Vector2(ActionsWidth, height), () => Act(act, fail), false);
            var rect = (RectTransform)b.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f); rect.pivot = new Vector2(1f, 1f);
            b.interactable = enabled;
            bool priced = price is { } cost && !cost.IsFree;
            var label = b.GetComponentInChildren<Text>();
            label.resizeTextMinSize = 13; label.resizeTextMaxSize = 22;
            Stretch(label.rectTransform, 14f, 6f, 14f, priced ? height * .46f : 6f);
            if (priced) AddPrice(rect, price.Value, height * .25f, enabled);
            return b.gameObject;
        }

        /// <summary>A price along the bottom of a button: each currency it asks, as its icon and the amount (red while the stock is
        /// short of it), centred. A currency whose icon is missing shows its name instead.</summary>
        void AddPrice(RectTransform button, CampaignCost cost, float centreFromBottom, bool enabled)
        {
            var c = campaign;
            var row = MakeRect("Price", button, new Vector2(.5f, 0f), new Vector2(.5f, 0f), new Vector2(0f, centreFromBottom), new Vector2(10f, 30f));
            // In the HUD's order: energy, alloy, dark matter.
            (int which, int amount, int held)[] parts = { (0, cost.energy, c.Energy), (1, cost.alloy, c.Alloy), (2, cost.dark, c.Dark) };
            var items = new List<(RectTransform rect, float width)>();
            foreach (var (which, amount, held) in parts)
            {
                if (amount <= 0) continue;
                var item = MakeRect("Item" + which, row, new Vector2(.5f, .5f), new Vector2(.5f, .5f), Vector2.zero, new Vector2(10f, 30f));
                var sprite = CurrencySprite(which);
                float x = 0f;
                if (sprite)
                {
                    var icon = Icon(item, new Vector2(0f, .5f), Vector2.zero, 28f);
                    icon.sprite = sprite;
                    if (!enabled) icon.color = new Color(1f, 1f, 1f, .6f);
                    x = 34f;
                }
                var number = Label("Amount", item, sprite ? amount.ToString() : $"{CurrencyLabel(which)} {amount}", 21, held >= amount ? Color.white : Warn, TextAnchor.MiddleLeft);
                number.fontStyle = FontStyle.Bold;
                number.horizontalOverflow = HorizontalWrapMode.Overflow;
                float textWidth = Mathf.Ceil(number.preferredWidth) + 2f;
                Anchor(number.rectTransform, new Vector2(0f, .5f), new Vector2(x, 0f), new Vector2(textWidth, 30f), new Vector2(0f, .5f));
                item.pivot = new Vector2(0f, .5f);
                items.Add((item, x + textWidth));
            }
            const float between = 22f;
            float total = -between;
            foreach (var (_, width) in items) total += width + between;
            float at = -total * .5f;
            foreach (var (item, width) in items)
            {
                item.sizeDelta = new Vector2(width, 30f);
                item.anchoredPosition = new Vector2(at, 0f);
                at += width + between;
            }
        }

        /// <summary>The room's picture fills its box: a picture of another shape is cut at the edges, not stretched.</summary>
        void ShowRoomPicture(Texture picture)
        {
            roomPicture.texture = picture;
            roomPicture.color = picture ? Color.white : new Color(.12f, .16f, .22f, 1f);
            if (!picture || picture.height <= 0) { roomPicture.uvRect = new Rect(0f, 0f, 1f, 1f); return; }
            float shape = picture.width / (float)picture.height, box = MiddleWidth / PictureHeight;
            roomPicture.uvRect = shape < box
                ? new Rect(0f, (1f - shape / box) * .5f, 1f, shape / box)
                : new Rect((1f - box / shape) * .5f, 0f, box / shape, 1f);
        }

        // ───── The currencies with their icons ─────

        sealed class CurrencyStrip
        {
            public readonly Image[] icons = new Image[3];
            public readonly Text[] amounts = new Text[3], names = new Text[3];
        }

        /// <summary>The three currencies in a row (energy, alloy, dark matter), each as its icon and its amount, with its name after it
        /// where named; the row's top-right corner sits at position from the anchor.</summary>
        CurrencyStrip BuildCurrencyStrip(RectTransform parent, Vector2 anchor, Vector2 position, bool named)
        {
            var strip = new CurrencyStrip();
            float step = named ? StripStepNamed : StripStepPlain;
            // One icon size for all three, so they read as equals. The dark-matter picture has more empty margin than the other two,
            // so it is drawn larger by that margin to look the same size.
            float iconSize = named ? 44f : 36f, textLeft = iconSize + 10f;
            var row = MakeRect("Currencies", parent, anchor, anchor, position, new Vector2(step * 3f, named ? 64f : 52f));
            row.pivot = new Vector2(1f, 1f);
            for (int i = 0; i < 3; i++)
            {
                float x = i * step;
                var icon = Icon(row, new Vector2(0f, .5f), new Vector2(x, 0f), iconSize);
                if (i == 2) icon.rectTransform.sizeDelta = Vector2.one * (iconSize * DarkIconScale);
                icon.preserveAspect = true;
                strip.icons[i] = icon;
                strip.amounts[i] = Label("Amount" + i, row, "", named ? 30 : 24, Color.white, TextAnchor.MiddleLeft);
                strip.amounts[i].fontStyle = FontStyle.Bold;
                strip.amounts[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                // Named: the amount on top and the currency's name under it, both beside the icon, so a name is never read as the next icon's.
                Anchor(strip.amounts[i].rectTransform, new Vector2(0f, .5f), new Vector2(x + textLeft, named ? 9f : 0f), new Vector2(step - textLeft, 36f), new Vector2(0f, .5f));
                if (!named) continue;
                strip.names[i] = Label("Name" + i, row, "", 15, Faint, TextAnchor.MiddleLeft);
                strip.names[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                Anchor(strip.names[i].rectTransform, new Vector2(0f, .5f), new Vector2(x + textLeft, -17f), new Vector2(step - textLeft, 20f), new Vector2(0f, .5f));
            }
            return strip;
        }

        /// <summary>How much larger the dark-matter icon is drawn than the other two so that all three look the same size.</summary>
        const float DarkIconScale = 51.7f / 44f;

        void RefreshCurrencyStrip(CurrencyStrip strip)
        {
            if (strip == null) return;
            var c = campaign;
            for (int i = 0; i < 3; i++)
            {
                var sprite = CurrencySprite(i);
                strip.icons[i].enabled = sprite;
                if (sprite && strip.icons[i].sprite != sprite) strip.icons[i].sprite = sprite;
                int amount = i == 0 ? c.Energy : i == 1 ? c.Alloy : c.Dark;
                // Without its icon (no art installed) the amount says what it counts.
                strip.amounts[i].text = sprite || strip.names[i] ? amount.ToString() : $"{CurrencyLabel(i)} {amount}";
                if (strip.names[i]) strip.names[i].text = CurrencyLabel(i);
            }
        }

        static string CurrencyLabel(int which) => which switch
        {
            0 => ArenaText.Pair("异星能量", "Alien energy"),
            1 => ArenaText.Pair("合金", "Alloy"),
            _ => ArenaText.Pair("暗物质", "Dark matter")
        };

        // ───── What the page says ─────

        int Hash(V2Campaign c)
        {
            int h = (int)tab * 7919 + c.Alloy * 31 + c.Energy * 131 + c.Dark * 521 + c.FortifyLevel * 3 + c.PurchaseCount * 5 + c.Fuel * 17 + c.LampStock * 23 + c.HomeStepsLeft;
            h = h * 7 + Mathf.CeilToInt(c.BaseHp) + (c.NightTimeLeft(out float s) ? (float.IsInfinity(s) ? 1 : Mathf.CeilToInt(s) * 1009) : 0);
            // The art switch changes the tabs' icons and the room's picture, the language every text.
            h = h * 2 + (Art.Enabled ? 1 : 0);
            h = h * 2 + (ArenaText.IsChinese ? 1 : 0);
            return h;
        }

        string Why(CampaignCost cost)
        {
            var c = campaign;
            var missing = new List<string>();
            if (c.Alloy < cost.alloy) missing.Add(ArenaText.Pair($"合金还差 {cost.alloy - c.Alloy}", $"{cost.alloy - c.Alloy} alloy short"));
            if (c.Energy < cost.energy) missing.Add(ArenaText.Pair($"异星能量还差 {cost.energy - c.Energy}", $"{cost.energy - c.Energy} alien energy short"));
            if (c.Dark < cost.dark) missing.Add(ArenaText.Pair($"暗物质还差 {cost.dark - c.Dark}", $"{cost.dark - c.Dark} dark matter short"));
            return missing.Count > 0 ? string.Join(ArenaText.Pair("，", ", "), missing) : ArenaText.Pair("现在还做不了", "Can't do that yet");
        }

        Sprite RoomIcon(CampaignRoom room)
        {
            if (!Art.Enabled) return WhiteboxIcons.Room(room); // 2026-11-10: a house in the room's colour
            var art = campaign.art;
            if (!art) return null;
            return room switch { CampaignRoom.Control => art.control, CampaignRoom.Armament => art.armament, CampaignRoom.Energy => art.energyLab, _ => art.shipBay };
        }

        /// <summary>The tab's picture. The ship tab shows the live view of the ship (D1-C); the rooms show the background of their level
        /// (D6-B). Pictures follow the art switch like the models: the whitebox (the English edition's default) shows a plain backdrop.</summary>
        Texture Picture(CampaignRoom room)
        {
            if (room == CampaignRoom.Ship && campaign.ShipViewTexture) return campaign.ShipViewTexture;
            var art = campaign.art;
            if (!art || !Art.Enabled) return null;
            int level = campaign.Room(room).level;
            return room switch
            {
                CampaignRoom.Control => art.roomControl,
                CampaignRoom.Armament => LevelPicture(art.roomArmamentLevels, level, art.roomArmament),
                CampaignRoom.Energy => LevelPicture(art.roomEnergyLevels, level, art.roomEnergy),
                _ => campaign.HullFixed && art.shipIntactPicture ? art.shipIntactPicture : art.shipWreckPicture
            };
        }

        /// <summary>levels[0] is level 2, levels[1] level 3; level 1, an unbuilt room and a missing picture fall back to the level-1 one.</summary>
        static Texture LevelPicture(Texture2D[] levels, int level, Texture2D first)
        {
            int i = level - 2;
            return levels != null && i >= 0 && i < levels.Length && levels[i] ? levels[i] : first;
        }

        /// <summary>The few lines that matter in a room now: Central Control the base's life and fortification, the launch bay the ship's
        /// three parts and the fuel, the two workshop rooms what their study window sells.</summary>
        string RoomText(CampaignRoom room)
        {
            var c = campaign;
            var r = c.rules;
            string Done(bool on) => on ? "<color=#7dff9a>●</color> " : "<color=#58606c>○</color> ";
            string Small(string text, Color colour) => $"<size=18><color=#{ColorUtility.ToHtmlStringRGB(colour)}>{text}</color></size>";
            var lines = new List<string>();
            switch (room)
            {
                case CampaignRoom.Control:
                    lines.Add(ArenaText.Pair($"基地生命 {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)}　·　加固 {c.FortifyLevel} / {c.FortifyTop} 级",
                        $"Base HP {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)}   ·   fortified {c.FortifyLevel} / {c.FortifyTop}"));
                    if (r) lines.Add(Small(ArenaText.Pair($"基地每天会自动回复最大生命的 {r.nightSelfRepair * 100f:0}%。", $"Each day the base repairs {r.nightSelfRepair * 100f:0}% of its max HP on its own."), Faint));
                    break;
                case CampaignRoom.Armament:
                    lines.Add(ArenaText.Pair($"武器型号 Mk {c.TeamMk}　·　护甲 {c.ArmorLevel} 级", $"Weapon Mk {c.TeamMk}   ·   armor level {c.ArmorLevel}"));
                    lines.Add(Small(ArenaText.Pair("型号、护甲、驯化和变招在「研习」里购买。", "The weapon Mk, armor, Tame and Twist are bought under Study."), Faint));
                    break;
                case CampaignRoom.Energy:
                    lines.Add(Small(ArenaText.Pair("体质、技能和附魔在「研习」里研究。", "The body, skills and infusion are researched under Study."), Faint));
                    break;
                default:
                    foreach (var part in c.ShipParts()) lines.Add(Done(part.done) + part.name + (part.done ? "" : ArenaText.Pair($"　合金 {part.price.alloy}", $"   {part.price.alloy} alloy")));
                    lines.Add(Done(c.FuelFull) + ArenaText.Pair($"燃料 {c.Fuel} / {c.FuelTop}", $"Fuel {c.Fuel} / {c.FuelTop}"));
                    if (r) lines.Add(Small(ArenaText.Pair($"矿灯每盏 {r.lampCost} 合金。夜里放下矿灯，就能在它的光里采矿；用不完的会留到下一晚。",
                        $"Mining lamps cost {r.lampCost} alloy each. Set one down at night to mine in its light; unused lamps carry over to the next night."), Faint));
                    lines.Add(Small(ArenaText.Pair("修好三个部件、加满燃料，就能点火返航。", "Repair the three parts and fill the fuel, and the ship can head home."), Faint));
                    break;
            }
            return string.Join("\n", lines);
        }
    }
}
