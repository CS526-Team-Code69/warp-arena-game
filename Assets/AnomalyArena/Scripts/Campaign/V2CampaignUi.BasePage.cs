using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The base page (2026-11-10: the whole screen, larger and with more room between its parts; before, a smaller panel in the middle
    /// with every explanation on its first tab). Top: where and when, and the three currencies with their icons. Left: one tab per room
    /// with its icon and its state. Middle: the room's picture (its whitebox blocks with the art off), its name, and the few lines that
    /// matter now; what explains one room stands with that room (a warning while it is broken). Right: what can be done there, one
    /// button each, its price drawn with the currencies' icons. Bottom: a strip kept free
    /// for the subtitle (V2CampaignUi.BaseSubtitleBottom), and the ways out. A room's next level never says what it unlocks (V2 doc: a
    /// new feature is seen only once unlocked); what a room sells is in the study window.
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
            baseScreen.gameObject.SetActive(false);
        }

        public void ShowBase()
        {
            ApplyFont();
            panelVersion = -1;
            tab = CampaignRoom.Control;
            baseScreen.gameObject.SetActive(true);
            RefreshBasePanel(true);
        }

        public void HideBase() { baseScreen.gameObject.SetActive(false); }

        /// <summary>Shows the base page again on the tab it had (after a bought weapon's card was closed).</summary>
        public void ResumeBase()
        {
            ApplyFont();
            panelVersion = -1;
            baseScreen.gameObject.SetActive(true);
            RefreshBasePanel(true);
        }

        /// <summary>Opens one room's tab of the open base page, as its tab button does (also for automated checks).</summary>
        public void ShowTab(CampaignRoom room)
        {
            if (!BaseOpen) return;
            tab = room;
            RefreshBasePanel(true);
        }

        /// <summary>A button's action; on failure its reason, unless it has none (a purchase that found nothing to give says so itself).</summary>
        void Act(Func<bool> action, string failure)
        {
            if (!action())
            {
                // Not enough material, or the room cannot do it now (2026-11-10).
                CampaignAudio.Play(CampaignSfx.UpgradeDenied);
                if (!string.IsNullOrEmpty(failure)) Flash(failure);
            }
            RefreshBasePanel(true);
        }

        void RefreshBasePanel(bool rebuild)
        {
            var c = campaign;
            var r = c.rules;
            baseHeading.text = c.BaseAfterTutorial ? ArenaText.Pair("基地 · 第 0 天", "BASE · DAY 0")
                : c.BaseAtDawn ? ArenaText.Pair($"基地 · 第 {c.Day} 晚过后，天亮了", $"BASE · DAWN AFTER NIGHT {c.Day}")
                : c.BaseFromDusk ? ArenaText.Pair($"基地 · 第 {c.Day} 天下午", $"BASE · DAY {c.Day}, AFTERNOON")
                : ArenaText.Pair($"基地 · 第 {c.Day} 天夜里", $"BASE · NIGHT {c.Day}");
            RefreshCurrencyStrip(baseStrip);
            int version = Hash(c);
            if (!rebuild && version == panelVersion) return;
            panelVersion = version;

            // Tabs: the room's icon, its name, its state.
            foreach (Transform child in tabColumn) Destroy(child.gameObject);
            for (int i = 0; i < 4; i++)
            {
                var room = (CampaignRoom)i;
                var state = c.Room(room);
                string level = room == CampaignRoom.Control ? ArenaText.Pair($"耐久 {c.Durability} · 护盾 {c.ShieldLevel}", $"Hull {c.Durability} · Shield {c.ShieldLevel}")
                    : state.level <= 0 ? ArenaText.Pair("未建造", "Not built") : ArenaText.Pair($"{state.level} / 3 级", $"Level {state.level} / 3");
                if (state.damaged) level += ArenaText.Pair(" · <color=#ff7a5a>损坏</color>", " · <color=#ff7a5a>BROKEN</color>");
                AddRoomTab(i, room, V2Campaign.RoomName(room), level);
            }

            // Picture, name, the lines that matter.
            ShowRoomPicture(Picture(tab));
            ShowRoomBlocks(tab, !Art.Enabled && !roomPicture.texture);
            var current = c.Room(tab);
            roomTitle.text = V2Campaign.RoomName(tab) + (current.damaged ? ArenaText.Pair("（损坏）", " (BROKEN)") : "");
            roomTitle.color = current.damaged ? Warn : Accent;
            roomBody.text = RoomText(tab);

            // Actions: a title each, and the price with the currencies' icons.
            foreach (var go in actionButtons) Destroy(go);
            actionButtons.Clear();
            var actions = new List<(string label, CampaignCost? cost, Func<bool> act, string fail, bool enabled)>();
            if (current.damaged)
                actions.Add((tab == CampaignRoom.Control ? ArenaText.Pair("修复总控区", "Repair Central Control") : ArenaText.Pair("修复这个区", "Repair this room"),
                    new CampaignCost(c.RepairCost(tab)), () => c.TryRepairRoom(tab),
                    tab != CampaignRoom.Control && !c.ControlWorks ? ArenaText.Pair("先修复总控区", "Repair Central Control first") : ArenaText.Pair("合金不够", "Not enough alloy"), true));
            switch (tab)
            {
                case CampaignRoom.Control:
                    if (c.DurabilityCost is { } dc)
                        actions.Add((ArenaText.Pair($"耐久升到 {c.Durability + 1} 级（基地生命 {Mathf.RoundToInt(c.BaseMaxHpAt(c.Durability + 1))}）", $"Raise hull to level {c.Durability + 1} (base HP {Mathf.RoundToInt(c.BaseMaxHpAt(c.Durability + 1))})"),
                            dc, c.TryUpgradeDurability, Why(dc), c.ControlWorks));
                    if (c.ShieldCost is { } sc)
                        actions.Add((c.ShieldLevel == 0 ? ArenaText.Pair("解锁基地护盾", "Unlock the base shield") : ArenaText.Pair($"护盾升到 {c.ShieldLevel + 1} 级", $"Raise shield to level {c.ShieldLevel + 1}"),
                            sc, c.TryUpgradeShield, Why(sc), c.ControlWorks));
                    if (c.BaseHp < c.BaseMaxHp - .5f)
                        actions.Add((ArenaText.Pair("把外壳修满", "Fully repair the hull"), new CampaignCost(c.HullRepairCost), c.TryRepairHull,
                            ArenaText.Pair("合金不够，或者总控区已损坏", "Not enough alloy, or Central Control is broken"), c.ControlWorks));
                    // The last day's overload is bought in the study window (V2CampaignUi.Study.cs), which says what it does.
                    if (c.OverloadOpen) actions.Add((ArenaText.Pair("决战过载", "Final overload"), null, () => { OpenStudy("ctl.overload"); return true; }, "", true));
                    break;
                case CampaignRoom.Ship:
                    if (c.NextLevelCost(tab) is { } shipCost) actions.Add((UpgradeLabel(tab), shipCost, () => c.TryUpgradeRoom(tab), UpgradeFail(tab, shipCost), c.ControlWorks && !current.damaged));
                    if (!c.EngineFixed)
                        actions.Add((ArenaText.Pair("修复引擎", "Repair the engine"), r.engineRepair, c.TryRepairEngine,
                            current.level < 1 ? ArenaText.Pair("飞船发射区 1 级才能修引擎", "Needs Ship Launch Bay level 1") : Why(r.engineRepair), c.CanRepairEngine));
                    if (!c.ArmorFixed)
                        actions.Add((ArenaText.Pair("修复装甲", "Repair the armor"), r.armorRepair, c.TryRepairArmor,
                            current.level < 2 ? ArenaText.Pair("飞船发射区 2 级才能修装甲", "Needs Ship Launch Bay level 2") : Why(r.armorRepair), c.CanRepairArmor));
                    if (!c.FuelFull)
                        actions.Add((ArenaText.Pair($"合成反物质燃料（{c.Fuel}/{c.FuelUnits}）", $"Synthesize antimatter fuel ({c.Fuel}/{c.FuelUnits})"), new CampaignCost(0, 0, c.DarkPerFuel), c.TrySynthesiseFuel,
                            current.level < 3 ? ArenaText.Pair("飞船发射区 3 级才能合成燃料", "Needs Ship Launch Bay level 3") : ArenaText.Pair("暗物质不够", "Not enough dark matter"), c.CanSynthesiseFuel));
                    actions.Add((ArenaText.Pair($"制造矿灯（现有 {c.LampStock} 盏）", $"Craft a mining lamp (you have {c.LampStock})"), new CampaignCost(r.lampCost), c.TryCraftLamp,
                        current.damaged ? ArenaText.Pair("飞船发射区损坏", "The Ship Launch Bay is broken") : ArenaText.Pair("合金不够", "Not enough alloy"), c.LampsCraftable));
                    if (c.CanIgnite) actions.Add((ArenaText.Pair("启动点火程序", "Start the ignition sequence"), null, () => { c.Ignite(); return true; }, "", true));
                    break;
                default:
                    if (c.NextLevelCost(tab) is { } cost) actions.Add((UpgradeLabel(tab), cost, () => c.TryUpgradeRoom(tab), UpgradeFail(tab, cost), c.ControlWorks && !current.damaged));
                    // What the Armament Bay and the Energy Lab sell is bought in the study window (V2CampaignUi.Study.cs), which shows
                    // every item with its rarity and numbers; the button names none of them (they show once unlocked).
                    if (current.level >= 1)
                        actions.Add((ArenaText.Pair("研习", "Study"), null, () => { OpenStudy(tab == CampaignRoom.Armament ? "arm.upgrade" : "eng.body"); return true; }, "", true));
                    break;
            }
            for (int i = 0; i < actions.Count; i++)
            {
                var (label, price, act, fail, enabled) = actions[i];
                actionButtons.Add(AddAction(i, actions.Count, label, price, act, fail, enabled));
            }

            // The ways out.
            foreach (Transform child in leaveRow) Destroy(child.gameObject);
            var leaves = new List<(string label, Action act)>();
            if (c.NightTimeLeft(out float seconds))
            {
                // Since 2026-11-06 the afternoon and the night have no clock (open-ended evenings): these buttons are the only way on. An
                // afternoon visit goes back out, into the night, or skips the night; a night visit goes back out or waits for dawn.
                string left = c.EveningsOpenEnded ? "" : ArenaText.Pair($"（还剩 {Mathf.CeilToInt(seconds)} 秒）", $" ({Mathf.CeilToInt(seconds)} s left)");
                string dawn = c.Day >= r.days ? (c.CanIgnite ? ArenaText.Pair("（随后点火返航）", " (then the ignition starts)") : ArenaText.Pair($"（飞船还走不了，进入第 {c.Day + 1} 天）", $" (the ship can't leave yet; day {c.Day + 1} begins)"))
                    : ArenaText.Pair($"（进入第 {c.Day + 1} 天）", $" (day {c.Day + 1} begins)");
                if (c.BaseFromDusk)
                {
                    leaves.Add((ArenaText.Pair($"出门，继续下午{left}", $"Head back out into the afternoon{left}"), c.LeaveBase));
                    leaves.Add((ArenaText.Pair("入夜", "Into the night"), c.EnterNight));
                    leaves.Add((ArenaText.Pair("跳过夜晚，等待天亮", "Skip the night, wait for dawn") + dawn, c.WaitForDawn));
                }
                else
                {
                    leaves.Add((ArenaText.Pair($"出门，继续今晚{left}", $"Head back out into the night{left}"), c.LeaveBase));
                    leaves.Add((ArenaText.Pair("等待天亮", "Wait for dawn") + dawn, c.WaitForDawn));
                }
            }
            else if (c.Day == 0 && !c.BaseAtDawn) leaves.Add((ArenaText.Pair("出发（夜幕降临）", "Head out (night falls)"), c.LeaveBase));
            else if (c.Day >= r.days && c.CanIgnite) leaves.Add((ArenaText.Pair("启动点火程序", "Start the ignition sequence"), c.Ignite));
            else leaves.Add((c.Day >= r.days ? ArenaText.Pair($"燃料未满 · 开始第 {c.Day + 1} 天", $"Tank not full · start day {c.Day + 1}") : ArenaText.Pair($"出发：开始第 {c.Day + 1} 天", $"Head out: start day {c.Day + 1}"), c.LeaveBase));
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
            1 => ArenaText.Pair("异星合金", "Alien alloy"),
            _ => ArenaText.Pair("暗物质", "Dark matter")
        };

        // ───── What the page says ─────

        int Hash(V2Campaign c)
        {
            int h = (int)tab * 7919 + c.Alloy * 31 + c.Energy * 131 + c.Dark * 521 + c.Durability * 3 + c.ShieldLevel * 5 + c.Fuel * 17 + c.LampStock * 23 + (c.EngineFixed ? 1 : 0) + (c.ArmorFixed ? 2 : 0);
            for (int i = 0; i < 4; i++) h = h * 13 + c.Room((CampaignRoom)i).level * 2 + (c.Room((CampaignRoom)i).damaged ? 1 : 0);
            h = h * 7 + Mathf.CeilToInt(c.BaseHp) + (c.NightTimeLeft(out float s) ? (float.IsInfinity(s) ? 1 : Mathf.CeilToInt(s) * 1009) : 0);
            // The weapons held change the Armament Bay's buttons (the pistol, a free hand, the rocket's place).
            h = h * 3 + (c.HoldsAnomalyWeapon ? 1 : 0) + (c.NewWeaponReplacesPistol ? 2 : 0);
            if (gm && gm.runWeapons) foreach (var w in gm.runWeapons.Hand) h = h * 5 + (w == null ? 0 : 1 + (int)w.form);
            // The art switch changes the tabs' icons and the room's picture, the language every text.
            h = h * 2 + (Art.Enabled ? 1 : 0);
            h = h * 2 + (ArenaText.IsChinese ? 1 : 0);
            return h;
        }

        string UpgradeLabel(CampaignRoom room)
        {
            int next = campaign.Room(room).level + 1;
            return next == 1 ? ArenaText.Pair($"建造{V2Campaign.RoomName(room)}", $"Build the {V2Campaign.RoomName(room)}")
                : ArenaText.Pair($"升到 {next} 级", $"Upgrade to level {next}");
        }

        string UpgradeFail(CampaignRoom room, CampaignCost cost) => !campaign.ControlWorks ? ArenaText.Pair("总控区已损坏，先把它修好", "Central Control is broken. Repair it first.")
            : campaign.Room(room).damaged ? ArenaText.Pair("先修复这个区", "Repair this room first") : Why(cost);

        string Why(CampaignCost cost)
        {
            var c = campaign;
            var missing = new List<string>();
            if (c.Alloy < cost.alloy) missing.Add(ArenaText.Pair($"合金还差 {cost.alloy - c.Alloy}", $"{cost.alloy - c.Alloy} alloy short"));
            if (c.Energy < cost.energy) missing.Add(ArenaText.Pair($"能量还差 {cost.energy - c.Energy}", $"{cost.energy - c.Energy} energy short"));
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
                _ => campaign.ArmorFixed && art.shipIntactPicture ? art.shipIntactPicture : art.shipWreckPicture
            };
        }

        /// <summary>levels[0] is level 2, levels[1] level 3; level 1, an unbuilt room and a missing picture fall back to the level-1 one.</summary>
        static Texture LevelPicture(Texture2D[] levels, int level, Texture2D first)
        {
            int i = level - 2;
            return levels != null && i >= 0 && i < levels.Length && levels[i] ? levels[i] : first;
        }

        /// <summary>The few lines that matter in a room now. A level not reached reads "？？？": the V2 doc keeps it unknown until unlocked.</summary>
        string RoomText(CampaignRoom room)
        {
            var c = campaign;
            var r = c.rules;
            string Done(bool on) => on ? "<color=#7dff9a>●</color> " : "<color=#58606c>○</color> ";
            string Level(int level, bool reached, string what, bool done = true) => reached
                ? Done(done) + ArenaText.Pair($"{level} 级：{what}", $"Level {level}: {what}")
                : Done(false) + ArenaText.Pair($"{level} 级：？？？", $"Level {level}: ? ? ?");
            string Small(string text, Color colour) => $"<size=18><color=#{ColorUtility.ToHtmlStringRGB(colour)}>{text}</color></size>";
            int lv = c.Room(room).level;
            var lines = new List<string>();
            switch (room)
            {
                case CampaignRoom.Control:
                    lines.Add(ArenaText.Pair($"基地生命 {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)}　·　耐久 {c.Durability} / {c.MaxDurability} 级", $"Base HP {Mathf.CeilToInt(c.BaseHp)} / {Mathf.CeilToInt(c.BaseMaxHp)}   ·   hull level {c.Durability} / {c.MaxDurability}"));
                    lines.Add(c.ShieldLevel > 0
                        ? ArenaText.Pair($"护盾 {Mathf.CeilToInt(c.BaseShield)} / {Mathf.CeilToInt(c.BaseMaxShield)}　·　{c.ShieldLevel} / {c.MaxShieldLevel} 级　·　每晚回满", $"Shield {Mathf.CeilToInt(c.BaseShield)} / {Mathf.CeilToInt(c.BaseMaxShield)}   ·   level {c.ShieldLevel} / {c.MaxShieldLevel}   ·   refills each night")
                        : ArenaText.Pair("护盾：未解锁", "Shield: locked"));
                    // What only matters in a state is said in that state.
                    if (c.Room(room).damaged) lines.Add(Small(ArenaText.Pair("总控区损坏了。先在这里把它修好，其他三个区才能继续建造和升级。", "Central Control is broken. Repair it here first; until then, the other three rooms can't be built or upgraded."), Warn));
                    else if (c.BaseHp < c.BaseMaxHp - .5f) lines.Add(Small(ArenaText.Pair($"基地每晚会自动修复 {r.nightSelfRepair * 100f:0}% 的生命", $"The base repairs {r.nightSelfRepair * 100f:0}% of its HP on its own each night"), Faint));
                    break;
                case CampaignRoom.Armament:
                    lines.Add(Level(1, lv >= 1, ArenaText.Pair("装甲升级、武器型号", "armor and weapon Mk upgrades")));
                    lines.Add(Level(2, lv >= 2, ArenaText.Pair("新武器、驯化", "new weapons and taming")));
                    lines.Add(Level(3, lv >= 3, ArenaText.Pair("武器变招", "weapon twists")));
                    if (lv >= 1) lines.Add(Small(ArenaText.Pair($"装甲 {c.ArmorLevel} 级　·　武器 Mk {c.TeamMk}", $"Armor level {c.ArmorLevel}   ·   weapons Mk {c.TeamMk}"), Faint));
                    break;
                case CampaignRoom.Energy:
                    lines.Add(Level(1, lv >= 1, ArenaText.Pair("体质研究", "body research")));
                    lines.Add(Level(2, lv >= 2, ArenaText.Pair("技能研究", "skill research")));
                    lines.Add(Level(3, lv >= 3, ArenaText.Pair("附魔研究", "infusion research")));
                    break;
                default:
                    lines.Add(Level(1, lv >= 1, ArenaText.Pair("修复飞船引擎", "repair the ship's engine"), c.EngineFixed));
                    lines.Add(Level(2, lv >= 2, ArenaText.Pair("修复飞船装甲", "repair the ship's armor"), c.ArmorFixed));
                    lines.Add(Level(3, lv >= 3, ArenaText.Pair($"合成反物质燃料 {c.Fuel} / {c.FuelUnits}", $"synthesize antimatter fuel {c.Fuel} / {c.FuelUnits}"), c.FuelFull));
                    // Two sentences, one line each: the text breaks lines only at spaces, so one long Chinese line broke after the number.
                    lines.Add(Small(ArenaText.Pair($"矿灯在这里造，每盏 {r.lampCost} 合金，用不完的会留到下一晚。",
                        $"Mining lamps are made here, {r.lampCost} alloy each; unused ones keep for the next night."), Faint));
                    lines.Add(Small(ArenaText.Pair("修好引擎和装甲、加满燃料，就能点火返航。",
                        "Repair the engine and the armor and fill the tank, and the ship can head home."), Faint));
                    break;
            }
            return string.Join("\n", lines);
        }
    }
}
