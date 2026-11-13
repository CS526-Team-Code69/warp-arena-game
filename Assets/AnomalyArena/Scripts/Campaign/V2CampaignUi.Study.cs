using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// The base's study window (2026-11-10): full screen over the base panel. Left, the tabs grouped by room (Armament Bay: upgrades, new
    /// weapons, Tame, Twist; Energy Lab: body, skills, infusion; the void once it has tiles; Central Control's overload on the last day).
    /// Middle, one tile per StudyEntry: a frame in its rarity's colour, its icon, its name, the number now and after, its price; a tile not
    /// unlocked yet shows only a question mark. Right, the chosen tile in full and its buy button. A purchase takes effect at once and the
    /// tiles are rebuilt. Mouse: click a tab or a tile, then Buy. Keys (shown at the bottom): arrows or WASD, Q / E for the tab, F to buy,
    /// Esc to close; a gamepad: d-pad or left stick, LB / RB, A, B. Space and Enter are never read here (only the weapon card reads them).
    /// The tiles come from V2Campaign.BuildStudy; this file only draws them and calls their buy.
    /// </summary>
    public sealed partial class V2CampaignUi
    {
        const float StudyTop = 96f, StudyBottom = 64f, StudySide = 24f, StudyTabsWidth = 250f, StudyDetailWidth = 440f, StudyGap = 20f;
        const float TileWidth = 188f, TileHeight = 156f, TileGap = 10f, TabHeight = 40f, TabHeaderHeight = 30f;
        static readonly Color StudyBack = new(.01f, .02f, .05f, 1f), TileInner = new(.06f, .09f, .14f, 1f), TileInnerSelected = new(.12f, .19f, .28f, 1f),
            LockedFrame = new(.3f, .32f, .36f, 1f);

        RectTransform studyScreen, studyTabColumn, studyViewport, studyContent;
        ScrollRect studyScroll;
        Text studyHeading, studyCurrencies, studyKeys, studyEmpty, detailTitle, detailRarity, detailNumbers, detailPrice, detailBody;
        CurrencyStrip studyStrip;
        Image detailIcon;
        Button detailBuy;
        Text detailBuyLabel;
        List<V2Campaign.StudyTab> studyData = new();
        readonly List<(Image frame, Image inner)> studyTiles = new();
        int studyTab, studySelected = -1, studyColumns = 4;
        string studyTabKey, studyEntryId;
        float studyRefreshAt, studyOpenedAt;
        bool padStickHeld, studyIllustrated;

        /// <summary>The study window is showing (it takes the keys while it does).</summary>
        public bool StudyOpen => studyScreen && studyScreen.gameObject.activeSelf;

        /// <summary>Opens the study window over the base panel, on the tab with this key (or the one it had last).</summary>
        public void OpenStudy(string tabKey = null)
        {
            if (!BaseOpen) return;
            if (!studyScreen) BuildStudyWindow();
            if (!string.IsNullOrEmpty(tabKey)) { studyTabKey = tabKey; studyEntryId = null; }
            ResumeStudy();
            V2Audio.Instance?.PlayUI(ArenaUiSound.Select);
        }

        /// <summary>Shows the window again on the tab and tile it had (after a weapon's card, AnnounceWeapon).</summary>
        public void ResumeStudy()
        {
            if (!studyScreen) return;
            ApplyFont();
            studyScreen.gameObject.SetActive(true);
            studyScreen.SetSiblingIndex(baseScreen.GetSiblingIndex() + 1); // over the base panel, under the purchase flash
            studyOpenedAt = Time.unscaledTime;
            RebuildStudy();
        }

        /// <summary>Hides the window, keeping its tab and tile for ResumeStudy.</summary>
        public void HideStudy() { if (studyScreen) studyScreen.gameObject.SetActive(false); }

        public void CloseStudy()
        {
            if (!StudyOpen) return;
            HideStudy();
            V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
        }

        void BuildStudyWindow()
        {
            studyScreen = Box("Study", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, StudyBack);
            StretchFull(studyScreen);
            studyScreen.GetComponent<Image>().raycastTarget = true; // no click reaches the base panel under it
            studyHeading = Label("Heading", studyScreen, "", 30, Color.white, TextAnchor.MiddleLeft);
            studyHeading.fontStyle = FontStyle.Bold;
            Anchor(studyHeading.rectTransform, new Vector2(0f, 1f), new Vector2(StudySide + 8f, -22f), new Vector2(720f, 48f), new Vector2(0f, 1f));
            var close = MakeButton(studyScreen, ArenaText.Pair("关闭（Esc）", "Close (Esc)"), Vector2.zero, new Vector2(180f, 48f), () => CloseStudy(), false);
            PinCorner((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-StudySide, -22f));
            // The three currencies with their icons (2026-11-10); the void currencies, once there are any, stay as words to their left.
            studyStrip = BuildCurrencyStrip(studyScreen, new Vector2(1f, 1f), new Vector2(-StudySide - 200f, -22f), false);
            studyCurrencies = Label("VoidCurrencies", studyScreen, "", 19, Soft, TextAnchor.MiddleRight);
            Anchor(studyCurrencies.rectTransform, new Vector2(1f, 1f), new Vector2(-StudySide - 200f - StripStepPlain * 3f - 16f, -22f), new Vector2(420f, 48f), new Vector2(1f, 1f));

            studyTabColumn = StudyRegion("Tabs", studyScreen, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(StudySide, StudyBottom), new Vector2(StudySide + StudyTabsWidth, -StudyTop));

            // The tiles scroll (mouse wheel or drag; the keys scroll to the chosen tile).
            var grid = StudyRegion("Grid", studyScreen, Vector2.zero, Vector2.one, new Vector2(StudySide + StudyTabsWidth + StudyGap, StudyBottom),
                new Vector2(-(StudySide + StudyDetailWidth + StudyGap), -StudyTop));
            studyScroll = grid.gameObject.AddComponent<ScrollRect>();
            studyViewport = StudyRegion("Viewport", grid, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var viewportImage = studyViewport.gameObject.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0f); // takes the wheel over empty space
            studyViewport.gameObject.AddComponent<RectMask2D>();
            studyContent = MakeRect("Content", studyViewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 100f));
            studyContent.pivot = new Vector2(0f, 1f);
            studyScroll.viewport = studyViewport; studyScroll.content = studyContent;
            studyScroll.horizontal = false; studyScroll.vertical = true;
            studyScroll.movementType = ScrollRect.MovementType.Clamped; studyScroll.scrollSensitivity = 40f;
            studyEmpty = Label("Empty", grid, "", 20, Faint, TextAnchor.MiddleCenter);
            Stretch(studyEmpty.rectTransform, 20f, 20f, 20f, 20f);

            var detail = StudyRegion("Detail", studyScreen, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-(StudySide + StudyDetailWidth), StudyBottom), new Vector2(-StudySide, -StudyTop));
            var back = detail.gameObject.AddComponent<Image>();
            back.color = Panel; back.raycastTarget = false;
            Outline(detail.gameObject);
            detailIcon = Icon(detail, new Vector2(0f, 1f), new Vector2(20f, -56f), 72f);
            detailTitle = Label("Title", detail, "", 22, Color.white, TextAnchor.UpperLeft);
            detailTitle.fontStyle = FontStyle.Bold;
            Anchor(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(104f, -18f), new Vector2(StudyDetailWidth - 124f, 56f), new Vector2(0f, 1f));
            detailRarity = Label("Rarity", detail, "", 15, Faint, TextAnchor.UpperLeft);
            Anchor(detailRarity.rectTransform, new Vector2(0f, 1f), new Vector2(104f, -76f), new Vector2(StudyDetailWidth - 124f, 24f), new Vector2(0f, 1f));
            detailNumbers = Label("StudyNumbers", detail, "", 22, Accent, TextAnchor.UpperLeft);
            detailNumbers.fontStyle = FontStyle.Bold;
            Anchor(detailNumbers.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -112f), new Vector2(StudyDetailWidth - 40f, 60f), new Vector2(0f, 1f));
            detailPrice = Label("Price", detail, "", 18, Soft, TextAnchor.UpperLeft);
            Anchor(detailPrice.rectTransform, new Vector2(0f, 1f), new Vector2(20f, -176f), new Vector2(StudyDetailWidth - 40f, 28f), new Vector2(0f, 1f));
            detailBody = Label("Body", detail, "", 16, Soft, TextAnchor.UpperLeft);
            detailBody.verticalOverflow = VerticalWrapMode.Truncate;
            Stretch(detailBody.rectTransform, 20f, 214f, 20f, 92f);
            detailBuy = MakeButton(detail, "", Vector2.zero, new Vector2(StudyDetailWidth - 40f, 58f), BuySelected, true);
            var buyRect = (RectTransform)detailBuy.transform;
            buyRect.anchorMin = buyRect.anchorMax = new Vector2(.5f, 0f); buyRect.pivot = new Vector2(.5f, 0f); buyRect.anchoredPosition = new Vector2(0f, 20f);
            detailBuyLabel = detailBuy.GetComponentInChildren<Text>();

            studyKeys = Label("Keys", studyScreen, "", 15, Faint, TextAnchor.MiddleCenter);
            var keys = studyKeys.rectTransform;
            keys.anchorMin = new Vector2(0f, 0f); keys.anchorMax = new Vector2(1f, 0f);
            keys.offsetMin = new Vector2(StudySide, 14f); keys.offsetMax = new Vector2(-StudySide, 50f);
            studyScreen.gameObject.SetActive(false);
        }

        /// <summary>A plain rect stretched between two anchors with these offsets (left-bottom, right-top).</summary>
        RectTransform StudyRegion(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }

        /// <summary>Pins a button by its top-left corner (or by the corner of `corner`) at `at` from that corner.</summary>
        static void PinCorner(RectTransform rect, Vector2 corner, Vector2 at)
        {
            rect.anchorMin = rect.anchorMax = corner; rect.pivot = corner; rect.anchoredPosition = at;
        }

        // ───── Content ─────

        /// <summary>The tiles again from the campaign (after opening and after each purchase), keeping the tab and the tile by their keys.</summary>
        void RebuildStudy()
        {
            studyData = campaign.BuildStudy();
            studyIllustrated = Art.Enabled;
            studyTab = Mathf.Max(0, studyData.FindIndex(t => t.key == studyTabKey));
            if (studyData.Count == 0) { studyTab = -1; }
            else studyTabKey = studyData[studyTab].key;
            BuildStudyTabs();
            BuildStudyTiles();
            RefreshStudyTexts();
        }

        V2Campaign.StudyTab CurrentStudyTab => studyTab >= 0 && studyTab < studyData.Count ? studyData[studyTab] : null;
        StudyEntry SelectedEntry => CurrentStudyTab is { } tab && studySelected >= 0 && studySelected < tab.entries.Count ? tab.entries[studySelected] : null;

        void BuildStudyTabs()
        {
            foreach (Transform child in studyTabColumn) Destroy(child.gameObject);
            float y = 0f;
            string room = null;
            for (int i = 0; i < studyData.Count; i++)
            {
                var tab = studyData[i];
                if (tab.roomName != room)
                {
                    room = tab.roomName;
                    var header = Label("Room", studyTabColumn, room, 17, Faint, TextAnchor.LowerLeft);
                    header.fontStyle = FontStyle.Bold;
                    Anchor(header.rectTransform, new Vector2(0f, 1f), new Vector2(6f, y - (y < 0f ? 8f : 0f)), new Vector2(StudyTabsWidth - 6f, TabHeaderHeight - 4f), new Vector2(0f, 1f));
                    y -= TabHeaderHeight + (y < 0f ? 8f : 0f);
                }
                int index = i;
                var b = MakeButton(studyTabColumn, tab.name, Vector2.zero, new Vector2(StudyTabsWidth, TabHeight), () => SelectStudyTab(index), index == studyTab);
                PinCorner((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, y));
                b.navigation = new Navigation { mode = Navigation.Mode.None };
                if (tab.locked) b.GetComponentInChildren<Text>().color = Faint;
                y -= TabHeight + 6f;
            }
        }

        void BuildStudyTiles()
        {
            foreach (Transform child in studyContent) Destroy(child.gameObject);
            studyTiles.Clear();
            var tab = CurrentStudyTab;
            float width = studyViewport.rect.width;
            studyColumns = Mathf.Max(1, Mathf.FloorToInt((width + TileGap) / (TileWidth + TileGap)));
            int count = tab == null || tab.locked ? 0 : tab.entries.Count;
            studyEmpty.text = tab == null ? ArenaText.Pair("现在没有可研习的内容。", "Nothing to study now.")
                : tab.locked ? tab.lockedNote : count == 0 ? tab.emptyNote ?? ArenaText.Pair("这里暂时没有内容。", "Nothing here yet.") : "";
            studyEmpty.gameObject.SetActive(!string.IsNullOrEmpty(studyEmpty.text));
            int rows = (count + studyColumns - 1) / studyColumns;
            studyContent.sizeDelta = new Vector2(0f, Mathf.Max(0f, rows * (TileHeight + TileGap) - TileGap));
            for (int i = 0; i < count; i++)
            {
                var entry = tab.entries[i];
                Vector2 at = new((i % studyColumns) * (TileWidth + TileGap), -(i / studyColumns) * (TileHeight + TileGap));
                studyTiles.Add(BuildTile(entry, i, at));
            }
            // Keep the tile chosen before (by its key), else the first one that can be bought, else the first.
            studySelected = count == 0 ? -1 : tab.entries.FindIndex(e => e.id == studyEntryId);
            if (count > 0 && studySelected < 0) studySelected = Mathf.Max(0, tab.entries.FindIndex(e => e.state == StudyState.Available));
            StyleTiles();
            ScrollToSelected();
        }

        (Image frame, Image inner) BuildTile(StudyEntry entry, int index, Vector2 at)
        {
            bool locked = entry.state == StudyState.Locked;
            bool owned = entry.state == StudyState.Owned;
            var tile = Box("Tile", studyContent, new Vector2(0f, 1f), at, new Vector2(TileWidth, TileHeight), new Vector2(0f, 1f), locked ? LockedFrame : ArenaRarity.ColorFor(entry.rarity));
            var frame = tile.GetComponent<Image>();
            frame.raycastTarget = true;
            var button = tile.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => SelectStudyTile(index, true));
            var innerRect = Box("Inner", tile, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, TileInner);
            Stretch(innerRect, 3f, 3f, 3f, 3f);
            var inner = innerRect.GetComponent<Image>();
            if (locked)
            {
                var mark = Label("Unknown", innerRect, "？", 52, Faint, TextAnchor.MiddleCenter);
                Stretch(mark.rectTransform, 0f, 0f, 0f, 0f);
                return (frame, inner);
            }
            float textLeft = 10f;
            var sprite = StudyIconOf(entry); // the whitebox's shape with the art off (V2CampaignUi.Whitebox.cs)
            if (sprite)
            {
                var icon = Icon(innerRect, new Vector2(0f, 1f), new Vector2(8f, -30f), 44f);
                icon.sprite = sprite;
                if (owned) icon.color = new Color(1f, 1f, 1f, .55f);
                textLeft = 58f;
            }
            var title = Label("Title", innerRect, entry.title, 15, owned ? Faint : Color.white, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;
            title.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(textLeft, -6f), new Vector2(TileWidth - 6f - textLeft - 6f, 46f), new Vector2(0f, 1f));
            var numbers = Label("StudyNumbers", innerRect, StudyNumbers(entry, "\n"), 14, owned ? Faint : Soft, TextAnchor.UpperLeft);
            numbers.verticalOverflow = VerticalWrapMode.Truncate;
            Anchor(numbers.rectTransform, new Vector2(0f, 1f), new Vector2(10f, -58f), new Vector2(TileWidth - 26f, 52f), new Vector2(0f, 1f));
            var price = Label("Price", innerRect, owned ? ArenaText.Pair("已拥有", "Owned") : entry.costText, 14,
                owned ? Good : entry.state == StudyState.Unaffordable ? Warn : Soft, TextAnchor.LowerLeft);
            Anchor(price.rectTransform, new Vector2(0f, 0f), new Vector2(10f, 6f), new Vector2(TileWidth - 26f, 24f), new Vector2(0f, 0f));
            return (frame, inner);
        }

        /// <summary>"now → after", or what there is of it.</summary>
        static string StudyNumbers(StudyEntry entry, string between)
        {
            bool now = !string.IsNullOrEmpty(entry.now), after = !string.IsNullOrEmpty(entry.after);
            return now && after ? $"{entry.now}{between}→ {entry.after}" : now ? entry.now : after ? "→ " + entry.after : "";
        }

        void StyleTiles()
        {
            var tab = CurrentStudyTab;
            for (int i = 0; i < studyTiles.Count; i++)
            {
                var (frame, inner) = studyTiles[i];
                if (!frame) continue;
                var entry = tab.entries[i];
                bool selected = i == studySelected;
                Color colour = entry.state == StudyState.Locked ? LockedFrame : ArenaRarity.ColorFor(entry.rarity);
                frame.color = selected ? Color.Lerp(colour, Color.white, .5f) : entry.state == StudyState.Owned ? colour * new Color(.6f, .6f, .6f, 1f) : colour;
                inner.color = selected ? TileInnerSelected : TileInner;
                frame.rectTransform.localScale = selected ? new Vector3(1.03f, 1.03f, 1f) : Vector3.one;
            }
            if (studySelected >= 0 && studySelected < studyTiles.Count && studyTiles[studySelected].frame) studyTiles[studySelected].frame.transform.SetAsLastSibling();
            ShowStudyDetail();
        }

        void ShowStudyDetail()
        {
            var tab = CurrentStudyTab;
            var entry = SelectedEntry;
            if (entry == null || entry.state == StudyState.Locked)
            {
                detailIcon.enabled = false;
                detailTitle.text = entry?.title ?? tab?.name ?? "";
                detailRarity.text = "";
                detailNumbers.text = "";
                detailPrice.text = "";
                detailBody.text = entry != null ? entry.detail : tab?.locked == true ? tab.lockedNote : tab?.emptyNote ?? "";
                detailBuy.gameObject.SetActive(false);
                return;
            }
            var sprite = StudyIconOf(entry);
            detailIcon.enabled = sprite;
            detailIcon.sprite = sprite;
            detailTitle.text = entry.title;
            detailTitle.color = Color.white;
            detailRarity.text = $"<color=#{ColorUtility.ToHtmlStringRGB(ArenaRarity.ColorFor(entry.rarity))}>■</color> {ArenaTerms.Rarity(entry.rarity)} · {entry.category}";
            detailNumbers.text = StudyNumbers(entry, "  ");
            detailPrice.text = entry.state == StudyState.Owned ? ArenaText.Pair("已拥有", "Owned") : ArenaText.Pair($"价格：{entry.costText}", $"Price: {entry.costText}");
            detailPrice.color = entry.state == StudyState.Unaffordable ? Warn : entry.state == StudyState.Owned ? Good : Soft;
            detailBody.text = entry.detail ?? "";
            detailBuy.gameObject.SetActive(entry.state != StudyState.Owned);
            detailBuy.interactable = entry.state == StudyState.Available;
            detailBuyLabel.text = ArenaText.Pair($"购买（F）· {entry.costText}", $"Buy (F) · {entry.costText}");
        }

        void RefreshStudyTexts()
        {
            var c = campaign;
            studyHeading.text = ArenaText.Pair("研习", "STUDY") + (CurrentStudyTab is { } tab ? $"<size=20><color=#{ColorUtility.ToHtmlStringRGB(Faint)}>　{tab.roomName} · {tab.name}</color></size>" : "");
            RefreshCurrencyStrip(studyStrip);
            // The void currencies join once the Nash Prophet is beaten (what the Void room's items cost).
            studyCurrencies.text = c.ProphetBeaten ? ArenaText.Pair($"虚空能量 {c.VoidEnergy}　虚空合金 {c.VoidAlloy}", $"Void energy {c.VoidEnergy}   Void alloy {c.VoidAlloy}") : "";
            studyKeys.text = ArenaText.Pair("方向键或 WASD 选择 · Q / E 换分类 · F 购买 · Esc 关闭　｜　手柄：十字键或左摇杆选择 · LB / RB 换分类 · A 购买 · B 关闭",
                "Arrows or WASD: choose · Q / E: tab · F: buy · Esc: close   |   Gamepad: D-pad or left stick · LB / RB: tab · A: buy · B: close");
        }

        // ───── Choosing and buying ─────

        void SelectStudyTab(int index)
        {
            if (studyData.Count == 0) return;
            index = (index % studyData.Count + studyData.Count) % studyData.Count;
            if (index == studyTab) return;
            studyTab = index;
            studyTabKey = studyData[index].key;
            studyEntryId = null;
            studyContent.anchoredPosition = Vector2.zero;
            V2Audio.Instance?.PlayUI(ArenaUiSound.Hover);
            BuildStudyTabs();
            BuildStudyTiles();
            RefreshStudyTexts();
        }

        void SelectStudyTile(int index, bool byClick)
        {
            var tab = CurrentStudyTab;
            if (tab == null || tab.entries.Count == 0) return;
            index = Mathf.Clamp(index, 0, tab.entries.Count - 1);
            if (index != studySelected || byClick) V2Audio.Instance?.PlayUI(ArenaUiSound.Hover);
            studySelected = index;
            studyEntryId = tab.entries[index].id;
            StyleTiles();
            if (!byClick) ScrollToSelected();
        }

        void MoveStudySelection(int dx, int dy)
        {
            var tab = CurrentStudyTab;
            if (tab == null || tab.locked || tab.entries.Count == 0) return;
            int from = Mathf.Max(0, studySelected);
            int to = from + dx + dy * studyColumns;
            if (dy != 0 && (to < 0 || to >= tab.entries.Count)) to = from; // no wrap past the top or the bottom row
            SelectStudyTile(to, false);
        }

        /// <summary>Scrolls the tiles so the chosen one shows whole.</summary>
        void ScrollToSelected()
        {
            if (studySelected < 0) { studyContent.anchoredPosition = Vector2.zero; return; }
            float top = (studySelected / studyColumns) * (TileHeight + TileGap), bottom = top + TileHeight;
            float view = studyViewport.rect.height, y = studyContent.anchoredPosition.y;
            if (top < y) y = top;
            else if (bottom > y + view) y = bottom - view;
            studyContent.anchoredPosition = new Vector2(0f, Mathf.Clamp(y, 0f, Mathf.Max(0f, studyContent.sizeDelta.y - view)));
        }

        void BuySelected()
        {
            var entry = SelectedEntry;
            if (entry == null || entry.buy == null || entry.state is StudyState.Locked or StudyState.Owned)
            {
                V2Audio.Instance?.PlayUI(ArenaUiSound.Cancel);
                return;
            }
            studyEntryId = entry.id;
            bool bought = entry.buy();
            if (!bought) CampaignAudio.Play(CampaignSfx.UpgradeDenied); // 2026-11-10: the purchase was refused
            if (!bought && entry.state == StudyState.Unaffordable) Flash(ArenaText.Pair("现在还买不了", "You can't buy this yet"));
            if (StudyOpen) RebuildStudy();
        }

        /// <summary>V2CampaignUi.Update calls this instead of its own key handling while the window is open.</summary>
        void TickStudy()
        {
            if (!BaseOpen || !campaign || !campaign.Running || campaign.Stage != CampaignStage.Base) { HideStudy(); return; }
            // A subtitle shows in the strip along the bottom, over this key line: the line steps aside while it is on (2026-11-10).
            bool keysShown = !SubtitleShowing;
            if (studyKeys.gameObject.activeSelf != keysShown) studyKeys.gameObject.SetActive(keysShown);
            if (Time.unscaledTime >= studyRefreshAt) { studyRefreshAt = Time.unscaledTime + .25f; RefreshStudyTexts(); }
            if (studyIllustrated != Art.Enabled) RebuildStudy(); // the tiles' icons follow the art switch (2026-11-10)
            // The press that opened the window does not act in it.
            if (Time.unscaledTime - studyOpenedAt < .15f) return;
            var keys = Keyboard.current;
            var pad = Gamepad.current;
            if (keys != null && keys.escapeKey.wasPressedThisFrame || pad != null && pad.buttonEast.wasPressedThisFrame) { CloseStudy(); return; }
            int dx = 0, dy = 0, tabStep = 0;
            bool buy = false;
            if (keys != null)
            {
                if (keys.leftArrowKey.wasPressedThisFrame || keys.aKey.wasPressedThisFrame) dx--;
                if (keys.rightArrowKey.wasPressedThisFrame || keys.dKey.wasPressedThisFrame) dx++;
                if (keys.upArrowKey.wasPressedThisFrame || keys.wKey.wasPressedThisFrame) dy--;
                if (keys.downArrowKey.wasPressedThisFrame || keys.sKey.wasPressedThisFrame) dy++;
                if (keys.qKey.wasPressedThisFrame) tabStep--;
                if (keys.eKey.wasPressedThisFrame) tabStep++;
                buy |= keys.fKey.wasPressedThisFrame;
            }
            if (pad != null)
            {
                if (pad.dpad.left.wasPressedThisFrame) dx--;
                if (pad.dpad.right.wasPressedThisFrame) dx++;
                if (pad.dpad.up.wasPressedThisFrame) dy--;
                if (pad.dpad.down.wasPressedThisFrame) dy++;
                // The stick moves one tile per push: it has to come back toward the centre before the next.
                Vector2 stick = pad.leftStick.ReadValue();
                if (!padStickHeld && stick.magnitude > .6f)
                {
                    padStickHeld = true;
                    if (Mathf.Abs(stick.x) > Mathf.Abs(stick.y)) dx += stick.x > 0f ? 1 : -1;
                    else dy += stick.y > 0f ? -1 : 1;
                }
                else if (stick.magnitude < .3f) padStickHeld = false;
                if (pad.leftShoulder.wasPressedThisFrame) tabStep--;
                if (pad.rightShoulder.wasPressedThisFrame) tabStep++;
                buy |= pad.buttonSouth.wasPressedThisFrame;
            }
            if (tabStep != 0) SelectStudyTab(studyTab + tabStep);
            else if (dx != 0 || dy != 0) MoveStudySelection(dx, dy);
            if (buy) BuySelected();
        }

        /// <summary>For automated checks: the tiles of the open window's tab.</summary>
        public IReadOnlyList<StudyEntry> StudyTiles => CurrentStudyTab?.entries ?? (IReadOnlyList<StudyEntry>)System.Array.Empty<StudyEntry>();
    }
}
