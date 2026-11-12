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
    /// matter now; what explains one room stands with that room (the side of the base it is on beside its name, a warning while it is
    /// broken). Right: what can be done there, one button each, its price drawn with the currencies' icons. Bottom: a strip kept free
    /// for the subtitle (V2CampaignUi.BaseSubtitleBottom), and the ways out. A room's next level never says what it unlocks (V2 doc: a
    /// new feature is seen only once unlocked); what a room sells is in the study window.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        // The page's frame (canvas units, 1600 x 900 reference) and its columns. The subtitle's strip lies between the body's end
        // (BodyBottom from the page's top) and the ways out.
        const float PageWidth = 1520f, PageHeight = 860f, PageHeader = 74f, PageTop = 98f;
        const float TabsWidth = 320f, RoomTabHeight = 112f, RoomTabGap = 14f;
        const float MiddleLeft = 0f, MiddleWidth = 1040f, PictureHeight = 280f, BodyBottom = 648f;
        const float ActionsWidth = 420f, ActionHeight = 84f, ActionGap = 14f, ActionHeightTight = 70f, ActionGapTight = 10f;
        const float LeaveHeight = 64f, LeaveWidth = 440f, LeaveGap = 20f;
        const float StripStepNamed = 200f, StripStepPlain = 132f;
        static readonly Color PageBack = new(.02f, .04f, .08f, 1f);

        CampaignRoom tab = CampaignRoom.Ship;
        RectTransform basePage, tabColumn, actionColumn, leaveRow, roomSideBack;
        Text baseHeading, roomTitle, roomSide, roomBody;
        RawImage roomPicture;
        CurrencyStrip baseStrip;
        readonly List<GameObject> actionButtons = new();
        int panelVersion = -1;

       // new buttons in the repair station. - H.
        public enum RepairStationAction
        {
            DepositAlloy,
            DepositFuel,
            DepositCore,
            UpgradeLaserTurret,
            UpgradeJetpack
        }
        // Gameplay handlers will use this to record the player's selection. - H.
        public event Action<RepairStationAction> RepairStationActionRequested;
        // UI fix - H.
        void FitRepairStation()
        {
            if (!basePage || !baseScreen) return;

            Rect available = baseScreen.rect;

            if (available.width <= 0f || available.height <= 0f)
                return;

            const float margin = 24f;

            float scaleX =
                Mathf.Max(1f, available.width - margin * 2f) / PageWidth;

            float scaleY =
                Mathf.Max(1f, available.height - margin * 2f) / PageHeight;

            float scale = Mathf.Min(1f, Mathf.Min(scaleX, scaleY));

            basePage.anchorMin = new Vector2(.5f, .5f);
            basePage.anchorMax = new Vector2(.5f, .5f);
            basePage.pivot = new Vector2(.5f, .5f);
            basePage.anchoredPosition = Vector2.zero;
            basePage.localScale = Vector3.one * scale;
        }

        // Builds five buttons for the repair station. - H.
        void BuildRepairStationButtons()
        {
            // Remove the old buttons before rebuilding.
            foreach (var button in actionButtons)
            {
                if (!button) continue;

                button.SetActive(false);
                Destroy(button);
            }

            actionButtons.Clear();

            // One station, no room tabs.
            tabColumn.gameObject.SetActive(false);
            roomSideBack.gameObject.SetActive(false);

            baseHeading.text = ArenaText.Pair(
                "维修站",
                "REPAIR STATION");

            roomTitle.text = ArenaText.Pair(
                "火箭维修站",
                "ROCKET REPAIR STATION");

            roomBody.text = ArenaText.Pair(
                "白天：保卫飞船并采集合金。\n" +
                "夜晚：采集反物质，为飞船提供燃料。\n" +
                "击败首领，取回反应堆核心。\n" +
                "激光炮塔和喷气背包每次解锁或升级需要 50 合金。",

                "DAY: Defend the rocket and gather alloy.\n" +
                "NIGHT: Gather antimatter for rocket fuel.\n\n" +
                "Main Objective: Recover the reactor core and fuel your rocket.\n");
              
            AddRepairStationButton(
                0,
                ArenaText.Pair("合金储库", "ALLOY VAULT"),
                ArenaText.Pair(
                    "存入携带的全部合金",
                    "Deposit alloy"),
                RepairStationAction.DepositAlloy);

             // The current campaign stores fuel in synthesis batches.
            // Convert those batches into deposited fuel cells for display.
            int depositedFuel = Mathf.Clamp(
                campaign.Fuel * campaign.DarkPerFuel,
                0,
                100);

            AddRepairStationButton(
                1,
                ArenaText.Pair(
                    "反物质燃料存入",
                    "ANTIMATTER FUEL DEPOSIT"),
                ArenaText.Pair(
                    $"燃料：{depositedFuel}/100",
                    $"Fuel: {depositedFuel}/100"),
                RepairStationAction.DepositFuel);

            AddRepairStationButton(
                2,
                ArenaText.Pair(
                    "反应堆核心安装",
                    "REACTOR CORE MISSING"),
                ArenaText.Pair(
                    "安装从首领处取得的核心",
                    "Reactor Core required for launch"),
                RepairStationAction.DepositCore);

            AddRepairStationButton(
                3,
                ArenaText.Pair("激光炮塔", "LASER TURRET"),
                ArenaText.Pair(
                    "解锁或升级 · 50 合金",
                    "Unlock · 50 alien energy"),
                RepairStationAction.UpgradeLaserTurret);

            AddRepairStationButton(
                4,
                ArenaText.Pair("修复喷气背包", "JETPACK FIX"),
                ArenaText.Pair(
                    "解锁或升级 · 50 合金",
                    "Unlock · 50 alien energy"),
                RepairStationAction.UpgradeJetpack);
        }

        // Adds the buttons and the descriptions. - H 
        void AddRepairStationButton(
            int index,
            string title,
            string description,
            RepairStationAction action)
        {
            const float buttonHeight = 98f;
            const float buttonGap = 14f;

            var button = MakeButton(
                actionColumn,
                title + "\n<size=16>" + description + "</size>",
                new Vector2(
                    0f,
                    -index * (buttonHeight + buttonGap)),
                new Vector2(ActionsWidth, buttonHeight),
             () =>
            {
                if (action == RepairStationAction.DepositFuel)
                {
                    bool deposited = campaign.TrySynthesiseFuel();

                    if (!deposited)
                    {
                        Flash(campaign.FuelFull
                        ? ArenaText.Pair(
                            "燃料已满：100/100",
                            "FUEL READY · 100/100")
                        : ArenaText.Pair(
                            $"燃料：{campaign.Fuel}/100",
                            $"Fuel: {campaign.Fuel}/100"));
                    }
                }
                else
                {
                    RepairStationActionRequested?.Invoke(action);
                }

                RefreshBasePanel(true);
            },
              false);

            var rect = (RectTransform)button.transform;

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);

            var label = button.GetComponentInChildren<Text>();

            if (label)
            {
                label.supportRichText = true;
                label.alignment = TextAnchor.MiddleCenter;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 16;
                label.resizeTextMaxSize = 24;
                label.lineSpacing = 1.1f;

                Stretch(
                    label.rectTransform,
                    16f,
                    10f,
                    16f,
                    10f);
            }

            button.interactable = true;
            actionButtons.Add(button.gameObject);
        }

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
            // Which side of the base the room is on: a caption along the picture's bottom edge. It explains the room's own breaking, so
            // it stands with the room (made after the picture, so it lies over it and over the whitebox blocks drawn in it).
            roomSideBack = Box("RoomSide", basePage, new Vector2(0f, 1f), new Vector2(MiddleLeft, -(PageTop + PictureHeight)), new Vector2(MiddleWidth, 32f), new Vector2(0f, 0f), new Color(0f, .01f, .03f, .62f));
            roomSide = Label("Text", roomSideBack, "", 17, Soft, TextAnchor.MiddleLeft);
            Stretch(roomSide.rectTransform, 14f, 0f, 14f, 0f);
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
            tab = CampaignRoom.Ship;
            baseScreen.gameObject.SetActive(true);
            RefreshBasePanel(true);
        }

        // Retained for compatibility with existing callers.
        public void ShowTab(CampaignRoom room)
        {
            if (!BaseOpen) return;

            tab = CampaignRoom.Ship;
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
        
       // Only one tab called the repair station
       void RefreshBasePanel(bool rebuild)
        {
            FitRepairStation();

            var c = campaign;
            var r = c.rules;

            baseHeading.text = ArenaText.Pair(
                "维修站",
                "REPAIR STATION");

            tab = CampaignRoom.Ship;
            RefreshCurrencyStrip(baseStrip);

            int version = Hash(c);

            if (!rebuild && version == panelVersion)
                return;

            panelVersion = version;

            // Hide room navigation and use the rocket preview.
            tabColumn.gameObject.SetActive(false);

            ShowRoomPicture(Picture(CampaignRoom.Ship));
            ShowRoomBlocks(CampaignRoom.Ship, false);

            roomTitle.text = ArenaText.Pair(
                "火箭维修站",
                "ROCKET REPAIR STATION");

            roomTitle.color = Accent;
            roomSide.text = "";
            roomSideBack.gameObject.SetActive(false);

            // Existing fuel is stored in synthesis batches.
            // Display the equivalent fuel amount without changing that logic.
            int loadedFuel = c.Fuel * c.DarkPerFuel;
            int fuelCapacity = c.FuelUnits * c.DarkPerFuel;

            roomBody.text = ArenaText.Pair(
                $"外壳：{Mathf.CeilToInt(c.BaseHp)} / " +
                $"{Mathf.CeilToInt(c.BaseMaxHp)}\n" +
                $"引擎：{(c.EngineFixed ? "已修复" : "待修复")}   " +
                $"装甲：{(c.ArmorFixed ? "已修复" : "待修复")}\n" +
                $"燃料：{loadedFuel} / {fuelCapacity}\n" +
                "收集合金以维修飞船，在夜间收集燃料原料。",

                $"Hull: {Mathf.CeilToInt(c.BaseHp)} / " +
                $"{Mathf.CeilToInt(c.BaseMaxHp)}\n" +
                $"Engine: {(c.EngineFixed ? "repaired" : "needs repair")}   " +
                $"Armor: {(c.ArmorFixed ? "repaired" : "needs repair")}\n" +
                $"Fuel: {loadedFuel} / {fuelCapacity}\n" +
                "Gather alloy for repairs and collect fuel material at night.");

            BuildRepairStationButtons();
            actionButtons.Clear();
            
            string unavailable = ArenaText.Pair(
                "当前无法使用此服务",
                "This service is currently unavailable");

            // Hull repair.
            bool hullDamaged = c.BaseHp < c.BaseMaxHp - .5f;
                      
            // Preserve the existing day/night exit flow.
            foreach (Transform child in leaveRow)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            var leaves = new List<(string label, Action act)>();

            if (c.NightTimeLeft(out float seconds))
            {
                string left = c.EveningsOpenEnded
                    ? ""
                    : ArenaText.Pair(
                        $"（还剩 {Mathf.CeilToInt(seconds)} 秒）",
                        $" ({Mathf.CeilToInt(seconds)} s left)");

                string dawn = c.Day >= r.days
                    ? c.CanIgnite
                        ? ArenaText.Pair(
                            "：启动点火程序",
                            ": start the ignition")
                        : ArenaText.Pair(
                            $"：飞船还不能起飞，开始第 {c.Day + 1} 天",
                            $": the ship cannot leave yet, start day {c.Day + 1}")
                    : ArenaText.Pair(
                        $"：开始第 {c.Day + 1} 天",
                        $": start day {c.Day + 1}");

                if (c.BaseFromDusk)
                {
                    leaves.Add((
                        ArenaText.Pair(
                            $"回到下午{left}",
                            $"Back to the afternoon{left}"),
                        c.LeaveBase));

                    leaves.Add((
                        ArenaText.Pair("入夜", "Into the night"),
                        c.EnterNight));

                    leaves.Add((
                        ArenaText.Pair(
                            "跳过夜晚，等待天亮",
                            "Skip the night, wait for dawn") + dawn,
                        c.WaitForDawn));
                }
                else
                {
                    leaves.Add((
                        ArenaText.Pair(
                            $"回到夜里{left}",
                            $"Back into the night{left}"),
                        c.LeaveBase));

                    leaves.Add((
                        ArenaText.Pair(
                            "等待天亮",
                            "Wait for dawn") + dawn,
                        c.WaitForDawn));
                }
            }
            else if (c.Day == 0 && !c.BaseAtDawn)
            {
                leaves.Add((
                    ArenaText.Pair(
                        "出发（夜幕降临）",
                        "Head out (night falls)"),
                    c.LeaveBase));
            }
            else if (c.Day >= r.days && c.CanIgnite)
            {
                leaves.Add((
                    ArenaText.Pair(
                        "启动点火程序",
                        "Start the ignition sequence"),
                    c.Ignite));
            }
            else
            {
                leaves.Add((
                    c.Day >= r.days
                        ? ArenaText.Pair(
                            $"燃料未满 · 开始第 {c.Day + 1} 天",
                            $"Tank not full · start day {c.Day + 1}")
                        : ArenaText.Pair(
                            $"出发：开始第 {c.Day + 1} 天",
                            $"Head out: start day {c.Day + 1}"),
                    c.LeaveBase));
            }

            for (int i = 0; i < leaves.Count; i++)
            {
                var (label, act) = leaves[i];

                var button = MakeButton(
                    leaveRow,
                    label,
                    new Vector2(
                        (i - (leaves.Count - 1) * .5f) *
                        (LeaveWidth + LeaveGap),
                        0f),
                    new Vector2(LeaveWidth, LeaveHeight),
                    () => act(),
                    true);

                var rect = (RectTransform)button.transform;

                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
                rect.pivot = new Vector2(.5f, 0f);

                var text = button.GetComponentInChildren<Text>();
                text.resizeTextMinSize = 14;
                text.resizeTextMaxSize = 24;
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
   // Only the repair station.
       GameObject AddAction(
        int index,
        int count,
        string title,
        CampaignCost? price,
        Func<bool> act,
        string fail,
        bool enabled)
    {
        float gap = count > 6 ? 8f : ActionGapTight;
        float available = BodyBottom - PageTop;

        float height = Mathf.Min(
            ActionHeight,
            (available - Mathf.Max(0, count - 1) * gap) /
            Mathf.Max(1, count));

        var button = MakeButton(
            actionColumn,
            title,
            new Vector2(0f, -index * (height + gap)),
            new Vector2(ActionsWidth, height),
            () => Act(act, fail),
            false);

        var rect = (RectTransform)button.transform;

        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);

        button.interactable = enabled;

        bool priced = price is { } cost && !cost.IsFree;

        var label = button.GetComponentInChildren<Text>();
        label.resizeTextMinSize = 13;
        label.resizeTextMaxSize = 22;

        Stretch(
            label.rectTransform,
            14f,
            6f,
            14f,
            priced ? height * .46f : 6f);

        if (priced)
        {
            AddPrice(rect, price.Value, height * .25f, enabled);

            var priceRow = rect.Find("Price");

            if (priceRow)
            {
                priceRow.localScale =
                    Vector3.one * Mathf.Min(1f, height / 76f);
            }
        }

        return button.gameObject;
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

   // equal sized icons. - H.
    CurrencyStrip BuildCurrencyStrip(
    RectTransform parent,
    Vector2 anchor,
    Vector2 position,
    bool named)
    {
        const float iconSize = 44f;
        const float iconTextGap = 10f;

        var strip = new CurrencyStrip();

        float step = named ? StripStepNamed : StripStepPlain;
        float rowHeight = named ? 64f : 52f;
        float textOffset = iconSize + iconTextGap;

        var row = MakeRect(
            "Currencies",
            parent,
            anchor,
            anchor,
            position,
            new Vector2(step * 3f, rowHeight));

        row.pivot = new Vector2(1f, 1f);

        for (int i = 0; i < 3; i++)
        {
            float x = i * step;

            // Identical dimensions and alignment for every icon.
            var icon = Icon(
                row,
                new Vector2(0f, .5f),
                new Vector2(x, 0f),
                iconSize);

            strip.icons[i] = icon;

            var rect = icon.rectTransform;

            rect.anchorMin = new Vector2(0f, .5f);
            rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(x, 0f);
            rect.sizeDelta = i == 2
                ? new Vector2(51.6891f, 51.5f)
                : new Vector2(iconSize, iconSize);
                rect.localScale = Vector3.one;

            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.useSpriteMesh = false;
            icon.raycastTarget = false;

            // Resource amount.
            var amount = Label(
                "Amount" + i,
                row,
                "",
                named ? 30 : 24,
                Color.white,
                TextAnchor.MiddleLeft);

            strip.amounts[i] = amount;

            amount.fontStyle = FontStyle.Bold;
            amount.horizontalOverflow = HorizontalWrapMode.Overflow;

            Anchor(
                amount.rectTransform,
                new Vector2(0f, .5f),
                new Vector2(x + textOffset, named ? 9f : 0f),
                new Vector2(step - textOffset, 36f),
                new Vector2(0f, .5f));

            if (!named) continue;

            // Resource name.
            var name = Label(
                "Name" + i,
                row,
                "",
                15,
                Faint,
                TextAnchor.MiddleLeft);

            strip.names[i] = name;

            name.horizontalOverflow = HorizontalWrapMode.Overflow;

            Anchor(
                name.rectTransform,
                new Vector2(0f, .5f),
                new Vector2(x + textOffset, -17f),
                new Vector2(step - textOffset, 20f),
                new Vector2(0f, .5f));
        }

        return strip;
    }

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

        string UpgradeFail(CampaignRoom room, CampaignCost cost) => !campaign.ControlWorks ? ArenaText.Pair("总控区损坏：先手动修复总控", "Central Control is broken: repair it by hand first")
            : campaign.Room(room).damaged ? ArenaText.Pair("先修复这个区", "Repair this room first") : Why(cost);

        string Why(CampaignCost cost)
        {
            var c = campaign;
            var missing = new List<string>();
            if (c.Alloy < cost.alloy) missing.Add(ArenaText.Pair($"合金差 {cost.alloy - c.Alloy}", $"{cost.alloy - c.Alloy} alloy short"));
            if (c.Energy < cost.energy) missing.Add(ArenaText.Pair($"能量差 {cost.energy - c.Energy}", $"{cost.energy - c.Energy} energy short"));
            if (c.Dark < cost.dark) missing.Add(ArenaText.Pair($"暗物质差 {cost.dark - c.Dark}", $"{cost.dark - c.Dark} dark matter short"));
            return missing.Count > 0 ? string.Join(ArenaText.Pair("，", ", "), missing) : ArenaText.Pair("现在不能做", "Not possible now");
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

        /// <summary>Which side of the base the room is on: a side hit too hard breaks its room, so each room says its own side.</summary>
        static string SideNote(CampaignRoom room)
        {
            int side = Array.IndexOf(V2Campaign.SideRoom, room);
            string where = side switch
            {
                0 => ArenaText.Pair("基地北面", "North side of the base"),
                1 => ArenaText.Pair("基地东面", "East side of the base"),
                2 => ArenaText.Pair("基地南面（大门）", "South side of the base (the door)"),
                3 => ArenaText.Pair("基地西面", "West side of the base"),
                _ => ""
            };
            return where.Length == 0 ? "" : where + ArenaText.Pair(" · 这一面挨打太多，本区会损坏", " · too many hits there break this room");
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
                    if (c.Room(room).damaged) lines.Add(Small(ArenaText.Pair("总控区损坏：先在这里手动修复，其他三个区才能施工。", "Central Control is broken: repair it here by hand before the other rooms can be worked on."), Warn));
                    else if (c.BaseHp < c.BaseMaxHp - .5f) lines.Add(Small(ArenaText.Pair($"每晚自动修复 {r.nightSelfRepair * 100f:0}% 生命", $"Repairs {r.nightSelfRepair * 100f:0}% of its HP by itself each night"), Faint));
                    break;
                case CampaignRoom.Armament:
                    lines.Add(Level(1, lv >= 1, ArenaText.Pair("装甲升级、武器型号", "armor upgrades, the weapon Mk")));
                    lines.Add(Level(2, lv >= 2, ArenaText.Pair("新武器、驯化", "new weapons, taming")));
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
                    lines.Add(Small(ArenaText.Pair($"每晚配给矿灯 {r.freeLampsPerNight} 盏　·　引擎、装甲、燃料齐了就能点火返航", $"{r.freeLampsPerNight} lamps handed out each night   ·   engine, armor and a full tank let the ship go home"), Faint));
                    break;
            }
            return string.Join("\n", lines);
        }
    }
}
