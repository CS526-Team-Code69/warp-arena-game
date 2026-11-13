using System;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// Scene-authored HUD layout knobs: overall scale, the top XP header, the resource panel, the wave texts and the bottom row of
    /// weapon and skill slots (each group on its own). Change the numbers in the Inspector and they apply at once in the editor
    /// (or use "Apply Layout"); the values live in the scene, so the V5 installer only seeds them once and never overwrites
    /// hand-tuned ones.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V2HudLayout : MonoBehaviour
    {
        /// <summary>One group of bottom-row slots (auto, hand or skill): where it sits, how big it is and how its text is sized.</summary>
        [Serializable]
        public sealed class SlotGroup
        {
            [Tooltip("格子的宽和高。")] public Vector2 size = new(150f, 104f);
            [Tooltip("离屏幕底边的距离。")] public float bottom = 20f;
            [Tooltip("自动格：离屏幕左边；技能格：离屏幕右边；手持格：两个格子的中线离屏幕中线的偏移（正数向右）。")] public float edge = 20f;
            [Tooltip("同一组里相邻格子之间的空隙。")] public float gap = 8f;
            [Tooltip("按键行字号（最小 – 最大）。")] public Vector2Int keyFont = new(11, 13);
            [Tooltip("名称字号（最小 – 最大）。")] public Vector2Int titleFont = new(13, 17);
            [Tooltip("细节行字号（最小 – 最大）。")] public Vector2Int detailFont = new(11, 13);
            [Tooltip("按键行文字颜色。")] public Color keyColor = Color.white;

            public SlotGroup(Vector2 size, float edge, Vector2Int keyFont, Vector2Int titleFont, Vector2Int detailFont, Color keyColor)
            {
                this.size = size; this.edge = edge; this.keyFont = keyFont; this.titleFont = titleFont; this.detailFont = detailFont; this.keyColor = keyColor;
            }
        }

        /// <summary>Canvas reference size at overallScale 1; the whole interface grows as overallScale rises.</summary>
        public static readonly Vector2 BaseReference = new(1440f, 810f);

        public V2Hud hud;

        [Header("整体")]
        [Tooltip("整个界面的放大倍率：1 = 现在的大小，1.2 = 全部放大 20%，0.9 = 缩小 10%。")]
        [Range(.6f, 1.8f)] public float overallScale = 1f;
        [Tooltip("由整体放大倍率算出的画布参考分辨率，不用手改。")]
        public Vector2 referenceResolution = new(1440f, 810f);
        [Tooltip("宽屏/窄屏适配：0 只按宽度缩放，1 只按高度缩放，0.5 各一半。")]
        [Range(0f, 1f)] public float matchWidthOrHeight = .5f;

        [Header("顶部经验条")]
        [Tooltip("经验条整条的高度。")] public float xpHeaderHeight = 48f;
        [Tooltip("经验条左右两边离屏幕边缘的留白总和。")] public float xpHeaderMargin = 40f;
        [Tooltip("\"等级 1 · 经验 0 / 15\" 的字号。")] public int xpFont = 18;
        [Tooltip("进度条的粗细。")] public float xpBarHeight = 8f;
        [Tooltip("待选卡组小方块的边长。")] public float xpPipSize = 14f;

        [Header("左上角资源条（生命/体力）")]
        [Tooltip("整块的放大倍率，1 = 原大小。")] [Range(.3f, 2f)] public float resourcePanelScale = 1.15f;
        [Tooltip("离屏幕左边的距离。")] public float resourcePanelLeft = 24f;
        [Tooltip("离屏幕顶边的距离；顶部经验条高约 48，小于它会重叠。")] public float resourcePanelTop = 64f;
        [Tooltip("是否显示生命/体力条后面的深色底板；关掉就只剩两条。")] public bool resourcePanelBackground;

        [Header("顶部中间的波次文字")]
        [Tooltip("\"第 01 / 14 波 · 18 秒\" 的字号。")] public int waveClockFont = 26;
        [Tooltip("\"第 1 批 · 活敌 10 · 待生成 0\" 的字号。")] public int batchFont = 18;
        [Tooltip("波次横幅（大字）的字号。")] public int bannerFont = 28;

        [Header("底栏：自动武器格（左下）")]
        public SlotGroup auto = new(new Vector2(170f, 112f), 20f, new Vector2Int(11, 13), new Vector2Int(13, 17), new Vector2Int(11, 13), new Color(.62f, .86f, .92f));
        [Header("底栏：手持武器格（中间）")]
        public SlotGroup hand = new(new Vector2(256f, 116f), -70f, new Vector2Int(11, 13), new Vector2Int(14, 19), new Vector2Int(12, 14), new Color(.93f, .97f, 1f));
        [Header("底栏：技能格（右下）")]
        public SlotGroup skill = new(new Vector2(138f, 112f), 20f, new Vector2Int(11, 13), new Vector2Int(13, 17), new Vector2Int(11, 13), new Color(.82f, .74f, 1f));

        [Header("格子内部的排版")]
        [Tooltip("文字离格子左右边框的距离（边框本身约 14）。")] public float padding = 22f;
        [Tooltip("按键行离格子顶部。")] public float keyTop = 14f;
        [Tooltip("名称行离格子顶部。")] public float titleTop = 40f;
        [Tooltip("细节行离格子底部（太小会压到边框）。")] public float detailBottom = 32f;
        public float detailHeight = 18f;
        [Tooltip("冷却条离格子底部。")] public float cooldownBottom = 9f;
        public float cooldownHeight = 4f;
        [Tooltip("右侧武器图标的宽和高。")] public Vector2 weaponIconSize = new(64f, 36f);
        [Tooltip("技能格右侧技能图标的边长。")] public float skillIconSize = 32f;
        [Tooltip("元素小图标的边长。")] public float elementIconSize = 26f;

        [Header("底栏上方（蓄力条和提示文字都跟着手持格整组的中心走）")]
        [Tooltip("蓄力条离手持格顶部的高度。")] public float chargeBarAboveRow = 14f;
        [Tooltip("蓄力条中心相对手持格整组中心的左右偏移，正数向右。")] public float chargeBarOffsetX;
        [Tooltip("蓄力条宽度；0 = 保持场景里的宽度。")] public float chargeBarWidth;
        [Tooltip("提示文字离手持格顶部的高度。")] public float hintAboveRow = 28f;
        [Tooltip("提示文字中心相对手持格整组中心的左右偏移，正数向右。")] public float hintOffsetX;
        public int hintFontSize = 18;

        [ContextMenu("Apply Layout")]
        public void Apply()
        {
            if (!hud) hud = GetComponent<V2Hud>() ?? GetComponentInChildren<V2Hud>(true);
            if (!hud) return;
            referenceResolution = BaseReference / Mathf.Max(.3f, overallScale);
            var scaler = hud.GetComponentInParent<CanvasScaler>();
            if (scaler)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = referenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = matchWidthOrHeight;
                Dirty(scaler);
            }
            // While the game runs with the fifth pass's battle HUD, it lays the play screen out itself; the knobs below place the saved HUD.
            if (Application.isPlaying && hud.TryGetComponent<V5BattleHud>(out var battle))
            {
                battle.LayOut();
                appliedState = CurrentState();
                return;
            }
            XpHeader();
            ResourcePanel();
            WaveTexts();
            LayOutBottomRow();
            if (Application.isPlaying) FitChineseNames();
            AboveRow();
            appliedState = CurrentState();
#if UNITY_EDITOR
            if (!Application.isPlaying && gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        // The values as they were when the layout was last applied. OnValidate also fires on scene load and on every domain
        // reload; re-applying then dirtied the scene each time, which is what raised the "save scene?" dialog before test runs.
        [SerializeField, HideInInspector] string appliedState;

        // Serialized without the two fields that are not layout values: the saved state itself and the HUD reference (an instance id).
        // They used to be cut out of the JSON text up to the next comma, but the saved state is JSON full of commas, so each save
        // nested the previous one with doubled escapes; by 2026-10-22 the lab scene held a 412 MB string and the editor crashed on it.
        string CurrentState()
        {
            string saved = appliedState;
            var savedHud = hud;
            appliedState = null;
            hud = null;
            try { return JsonUtility.ToJson(this); }
            finally { appliedState = saved; hud = savedHud; }
        }

        void XpHeader()
        {
            var header = FindRect("XpHeader");
            if (!header) return;
            float width = referenceResolution.x - xpHeaderMargin;
            header.sizeDelta = new Vector2(width, xpHeaderHeight);
            Dirty(header);
            float pipsWidth = 10f * (xpPipSize + 6f);
            if (hud.xpLabel)
            {
                float labelHeight = xpFont + 8f;
                Place(hud.xpLabel, 16f, 4f, width - 32f - pipsWidth, labelHeight);
                hud.xpLabel.resizeTextForBestFit = false;
                hud.xpLabel.fontSize = xpFont;
                Dirty(hud.xpLabel);
            }
            if (hud.xpFill)
            {
                var bar = hud.xpFill.transform.parent as RectTransform;
                float barWidth = width - 24f;
                if (bar && bar != header) Place(bar, 12f, xpHeaderHeight - xpBarHeight - 6f, barWidth, xpBarHeight);
                Place(hud.xpFill, 0f, 0f, barWidth, xpBarHeight);
            }
            if (hud.draftPips != null)
                for (int i = 0; i < hud.draftPips.Length; i++)
                {
                    if (!hud.draftPips[i]) continue;
                    float x = width - 16f - pipsWidth + i * (xpPipSize + 6f);
                    Place(hud.draftPips[i], x, 4f + (xpFont + 8f - xpPipSize) * .5f, xpPipSize, xpPipSize);
                }
        }

        void ResourcePanel()
        {
            var panel = FindRect("ResourcePanel");
            if (!panel) return;
            // Top-left corner, below the XP header; the dark backdrop is optional.
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.anchoredPosition = new Vector2(resourcePanelLeft, -resourcePanelTop);
            panel.localScale = Vector3.one * Mathf.Max(.3f, resourcePanelScale);
            Dirty(panel);
            var backdrop = panel.GetComponent<UnityEngine.UI.Image>();
            if (backdrop && backdrop.enabled != resourcePanelBackground) { backdrop.enabled = resourcePanelBackground; Dirty(backdrop); }
        }

        void WaveTexts()
        {
            Size(hud.waveLabel, waveClockFont);
            Size(hud.batchLabel, batchFont);
            Size(hud.bannerLabel, bannerFont, 2.6f);
            if (hud.batchLabel) { hud.batchLabel.supportRichText = true; Dirty(hud.batchLabel); }
        }

        static void Size(Text text, int font, float lines = 1.5f)
        {
            if (!text) return;
            text.resizeTextForBestFit = false;
            text.fontSize = Mathf.Max(6, font);
            var rect = text.rectTransform;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, Mathf.Max(rect.sizeDelta.y, font * lines));
            Dirty(text);
            Dirty(rect);
        }

        void LayOutBottomRow()
        {
            if (hud.autoSlots != null)
                for (int i = 0; i < hud.autoSlots.Length; i++)
                {
                    var slot = hud.autoSlots[i];
                    if (!slot) continue;
                    var rect = (RectTransform)slot.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
                    int column = Mathf.Min(i, 1), tier = Mathf.Max(0, i - 1);
                    rect.anchoredPosition = new Vector2(auto.edge + column * (auto.size.x + auto.gap), auto.bottom + tier * (auto.size.y + auto.gap));
                    LayOutSlot(slot, auto, false);
                }
            if (hud.skillSlots != null)
            {
                int skills = hud.skillSlots.Length;
                for (int i = 0; i < skills; i++)
                {
                    var slot = hud.skillSlots[i];
                    if (!slot) continue;
                    var rect = (RectTransform)slot.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
                    rect.anchoredPosition = new Vector2(-(skill.edge + (skills - 1 - i) * (skill.size.x + skill.gap)), skill.bottom);
                    LayOutSlot(slot, skill, true);
                }
            }
            if (hud.handSlots != null)
                for (int i = 0; i < hud.handSlots.Length; i++)
                {
                    var slot = hud.handSlots[i];
                    if (!slot) continue;
                    var rect = (RectTransform)slot.transform;
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f);
                    rect.pivot = new Vector2(.5f, 0f);
                    float half = hand.size.x * .5f + hand.gap * .5f;
                    rect.anchoredPosition = new Vector2(hand.edge + (i == 0 ? -half : half), hand.bottom);
                    LayOutSlot(slot, hand, false);
                }
        }

        void LayOutSlot(V2UiSlot slot, SlotGroup group, bool isSkill)
        {
            var rect = (RectTransform)slot.transform;
            rect.sizeDelta = group.size;
            Dirty(rect);
            float w = group.size.x, h = group.size.y;
            // Key row: key label on the left, element icon at the right. Title row: the weapon or skill glyph sits at the right,
            // so the title text stops before it.
            float glyphRight = w - padding;
            // Weapon slots keep room for the wide weapon glyph; skill slots only for the smaller skill glyph.
            float iconW = isSkill ? skillIconSize : weaponIconSize.x, iconH = isSkill ? skillIconSize : weaponIconSize.y;
            float elementX = glyphRight - elementIconSize;
            Place(slot.icon, elementX, keyTop - 2f, elementIconSize, elementIconSize);
            // Both glyph images share one rectangle per slot kind (only one of them is ever shown), kept below the key row.
            float iconTop = Mathf.Max(titleTop - 2f, keyTop + 24f);
            Place(slot.weaponIcon, glyphRight - iconW, iconTop, iconW, iconH);
            Place(slot.contentIcon, glyphRight - iconW, iconTop, iconW, iconH);
            var keyTransform = slot.transform.Find("SlotKey");
            var key = keyTransform ? keyTransform.GetComponent<Text>() : null;
            Place(key, padding, keyTop, elementX - 6f - padding, 22f);
            Fit(key, group.keyFont, TextAnchor.UpperLeft);
            if (key) { key.color = group.keyColor; Dirty(key); }
            float detailTop = h - detailBottom;
            Place(slot.title, padding, titleTop, glyphRight - iconW - 8f - padding, detailTop - titleTop - 2f);
            Fit(slot.title, group.titleFont, TextAnchor.UpperLeft);
            Place(slot.detail, padding, detailTop, w - padding * 2f, detailHeight);
            Fit(slot.detail, group.detailFont, TextAnchor.MiddleLeft);
            var bar = slot.cooldown ? slot.cooldown.transform.parent as RectTransform : null;
            if (bar && bar != rect)
            {
                Place(bar, 8f, h - cooldownBottom, w - 16f, cooldownHeight);
                Place(slot.cooldown, 0f, 0f, w - 16f, cooldownHeight);
            }
        }

        // Run time only, so the boxes saved in the scenes stay as they are.
        void Awake()
        {
            // The fifth pass's slots place their own parts (V5BattleSlot); when they were built first, this would undo that.
            if (hud && !hud.TryGetComponent<V5BattleHud>(out _)) FitChineseNames();
        }

        // Longest names at a group's smallest title size: a hand weapon's second line ("蓄力射击 · Mk 6", the dot is a full character wide)
        // and a four-character skill. Auto slots only hold forms with two-character anomalies, which fit their narrower box at size 14.
        const float WeaponNameEms = 7.7f, SkillNameEms = 4.2f;
        const float NameGap = 4f, SmallestSideGlyph = 24f, FrameEdge = 16f, KeyRowElement = 20f, KeyRowGlyphWidth = 30f, KeyRowGap = 2f, KeyRowHeight = 20f;
        // The font's first line takes its full height (about 1.44 × size) and later lines that × the spacing, so two lines of size 14 need 36.
        const float DetailDrop = 2f, ChineseLineSpacing = .8f, OneLineNameHeight = 25f;
        // How far below its group's smallest title size a name may go before it loses a line. The boxes are measured for the Chinese
        // names, which fit at the larger sizes; the longest English ones need the smaller ones ("Mirror Image" in a skill slot and
        // "Launch Yourself · Mk 10" in a hand slot fit at size 9). With 2, "Shockwave" showed as "Shockwa" and a weapon lost its Mk.
        const int TitleShrink = 6, SmallestDetail = 8;

        /// <summary>LayOutSlot gives the title only what the glyph leaves: 14 wide in an auto slot, so "空栏位" showed as "空", and 84 in a
        /// hand slot, so "步枪 · 蓄力射击" showed as "步枪 ·". Here the title gets two lines and the room for the longest Chinese name; the
        /// glyph takes what is left beside it, or moves up to the key row where nothing useful is left. The detail line runs to the frame and
        /// tightens its text (V2UiSlot.DetailLine). Until 2026-11-10 only the Chinese scene did this and the English scene kept the boxes its
        /// publication step fitted (EnglishPublication.FitEnglishText); one scene now serves both languages, so every scene lays its slots
        /// out this way, in either language. The longest English names need smaller sizes in these boxes than the
        /// Chinese ones (TitleShrink); HudSlotNameFitTest holds every name of both languages to its box.</summary>
        void FitChineseNames()
        {
            if (hud.handSlots != null) foreach (var slot in hud.handSlots) FitChineseSlot(slot, hand, WeaponNameEms, weaponIconSize, false);
            if (hud.autoSlots != null) foreach (var slot in hud.autoSlots) FitChineseSlot(slot, auto, WeaponNameEms, weaponIconSize, false);
            if (hud.skillSlots != null) foreach (var slot in hud.skillSlots) FitChineseSlot(slot, skill, SkillNameEms, Vector2.one * skillIconSize, true);
        }

        void FitChineseSlot(V2UiSlot slot, SlotGroup group, float nameEms, Vector2 glyph, bool oneLineName)
        {
            if (!slot) return;
            float w = group.size.x, top = keyTop + KeyRowHeight, normalDetailTop = group.size.y - detailBottom + DetailDrop, detailEnd = normalDetailTop + detailHeight;
            // A skill's name takes one line, which leaves its detail two: the energy cost and Blink's charges need them.
            float height = oneLineName ? OneLineNameHeight : normalDetailTop - top, detailTop = top + height;
            float room = w - padding * 2f - nameEms * group.titleFont.x - NameGap;
            float keyRight;
            if (room >= SmallestSideGlyph)
            {
                Vector2 size = glyph * Mathf.Min(1f, room / glyph.x, height / glyph.y);
                float glyphX = w - padding - size.x, glyphY = top + (height - size.y) * .5f;
                float element = Mathf.Min(elementIconSize, glyphY - (keyTop - 2f));
                Place(slot.icon, w - padding - element, keyTop - 2f, element, element);
                Place(slot.weaponIcon, glyphX, glyphY, size.x, size.y);
                Place(slot.contentIcon, glyphX, glyphY, size.x, size.y);
                Place(slot.title, padding, top, glyphX - NameGap - padding, height);
                keyRight = w - padding - element - 6f;
            }
            else
            {
                float glyphW = Mathf.Min(KeyRowGlyphWidth, KeyRowElement * glyph.x / glyph.y), glyphH = glyphW * glyph.y / glyph.x;
                float right = w - FrameEdge, elementX = right - KeyRowElement, glyphX = elementX - KeyRowGap - glyphW, glyphY = keyTop + (KeyRowElement - glyphH) * .5f;
                Place(slot.icon, elementX, keyTop, KeyRowElement, KeyRowElement);
                Place(slot.weaponIcon, glyphX, glyphY, glyphW, glyphH);
                Place(slot.contentIcon, glyphX, glyphY, glyphW, glyphH);
                Place(slot.title, padding, top, right - padding, height);
                keyRight = glyphX - KeyRowGap;
            }
            var keyTransform = slot.transform.Find("SlotKey");
            if (keyTransform) Place(keyTransform.GetComponent<Text>(), padding, keyTop, keyRight - padding, KeyRowHeight);
            Place(slot.detail, padding, detailTop, w - padding - FrameEdge, detailEnd - detailTop);
            // A line that does not fit is dropped whole, and font sizes round to whole pixels at some canvas scales, so these texts may go
            // below the group's smallest size rather than lose a line.
            Fit(slot.title, new Vector2Int(group.titleFont.x - TitleShrink, group.titleFont.y), TextAnchor.MiddleLeft);
            Fit(slot.detail, new Vector2Int(SmallestDetail, group.detailFont.y), oneLineName ? TextAnchor.LowerLeft : TextAnchor.MiddleLeft);
            if (slot.title) slot.title.lineSpacing = ChineseLineSpacing;
            if (slot.detail) slot.detail.lineSpacing = ChineseLineSpacing;
            slot.nameOnTwoLines = true;
            slot.compactDetailSize = group.detailFont.x;
        }

        void AboveRow()
        {
            float rowTop = hand.bottom + hand.size.y;
            if (hud.chargeFill && hud.chargeFill.transform.parent is RectTransform bar && bar.name == "ChargeProgressBackground")
            {
                bar.anchoredPosition = new Vector2(hand.edge + chargeBarOffsetX, rowTop + chargeBarAboveRow);
                if (chargeBarWidth > 0f)
                {
                    bar.sizeDelta = new Vector2(chargeBarWidth, bar.sizeDelta.y);
                    hud.chargeFill.rectTransform.sizeDelta = new Vector2(chargeBarWidth, hud.chargeFill.rectTransform.sizeDelta.y);
                    Dirty(hud.chargeFill.rectTransform);
                }
                Dirty(bar);
            }
            if (hud.hintLabel)
            {
                var hint = hud.hintLabel.rectTransform;
                float hintX = Mathf.Approximately(hint.anchorMin.x, .5f) ? hand.edge + hintOffsetX : hint.anchoredPosition.x;
                hint.anchoredPosition = new Vector2(hintX, rowTop + hintAboveRow);
                hud.hintLabel.fontSize = hintFontSize;
                Dirty(hint);
                Dirty(hud.hintLabel);
            }
            // Off-screen enemy and hole markers stay above the bottom row and its hint.
            float markerInset = rowTop + hintAboveRow + hintFontSize + 12f;
            var directions = hud.GetComponentInChildren<V2EnemyDirections>(true);
            if (directions) { directions.bottomInset = markerInset; Dirty(directions); }
            var labels = hud.GetComponentInChildren<V2WorldLabels>(true);
            if (labels) { labels.edgeBottomInset = markerInset; Dirty(labels); }
        }

        RectTransform FindRect(string name)
        {
            foreach (var rect in hud.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        static void Place(Component part, float left, float top, float width, float height)
        {
            if (!part || !(part.transform is RectTransform rect)) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
            Dirty(rect);
        }

        static void Fit(Text text, Vector2Int range, TextAnchor anchor)
        {
            if (!text) return;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(1, range.x);
            text.resizeTextMaxSize = Mathf.Max(text.resizeTextMinSize, range.y);
            text.fontSize = text.resizeTextMaxSize;
            Dirty(text);
        }

        static void Dirty(UnityEngine.Object target)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying && target) UnityEditor.EditorUtility.SetDirty(target);
#endif
        }

#if UNITY_EDITOR
        // OnValidate also runs while the editor restores objects after a script reload; the comparison waits until the editor is idle.
        void OnValidate()
        {
            if (Application.isPlaying) return;
            UnityEditor.EditorApplication.delayCall += () => { if (this && hud && CurrentState() != appliedState) Apply(); };
        }
#endif
    }
}
