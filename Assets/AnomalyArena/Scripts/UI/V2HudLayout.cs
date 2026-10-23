using System;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
                 
                                                                                                                                   
                                                                                                                                
                                                                                                                            
                        
                  
    [DisallowMultipleComponent]
    public sealed class V2HudLayout : MonoBehaviour
    {
                                                                                                                                           
        [Serializable]
        public sealed class SlotGroup
        {
            [Tooltip("Slot width and height.")] public Vector2 size = new(150f, 104f);
            [Tooltip("Distance from the bottom of the screen.")] public float bottom = 20f;
            [Tooltip("Auto slots: distance from the screen's left edge; skill slots: from the right edge; hand slots: offset of the pair's centre line from the screen's centre line (positive is right).")] public float edge = 20f;
            [Tooltip("Gap between neighbouring slots of the same group.")] public float gap = 8f;
            [Tooltip("Key row font size (min – max).")] public Vector2Int keyFont = new(11, 13);
            [Tooltip("Name font size (min – max).")] public Vector2Int titleFont = new(13, 17);
            [Tooltip("Detail row font size (min – max).")] public Vector2Int detailFont = new(11, 13);
            [Tooltip("Key row text colour.")] public Color keyColor = Color.white;

            public SlotGroup(Vector2 size, float edge, Vector2Int keyFont, Vector2Int titleFont, Vector2Int detailFont, Color keyColor)
            {
                this.size = size; this.edge = edge; this.keyFont = keyFont; this.titleFont = titleFont; this.detailFont = detailFont; this.keyColor = keyColor;
            }
        }

                                                                                                                        
        public static readonly Vector2 BaseReference = new(1440f, 810f);

        public V2Hud hud;

        [Header("Overall")]
        [Tooltip("Scale of the whole interface: 1 = current size, 1.2 = everything 20% larger, 0.9 = 10% smaller.")]
        [Range(.6f, 1.8f)] public float overallScale = 1f;
        [Tooltip("Canvas reference resolution derived from the overall scale; no need to edit.")]
        public Vector2 referenceResolution = new(1440f, 810f);
        [Tooltip("Wide / narrow screen match: 0 scales by width only, 1 by height only, 0.5 half each.")]
        [Range(0f, 1f)] public float matchWidthOrHeight = .5f;

        [Header("Top XP header")]
        [Tooltip("Height of the whole XP header.")] public float xpHeaderHeight = 48f;
        [Tooltip("Total margin between the XP header and the screen edges on both sides.")] public float xpHeaderMargin = 40f;
        [Tooltip("Font size of \"Level 1 · XP 0 / 15\".")] public int xpFont = 18;
        [Tooltip("Thickness of the progress bar.")] public float xpBarHeight = 8f;
        [Tooltip("Side length of the pending-card pips.")] public float xpPipSize = 14f;

        [Header("Top-left resource bars (health/stamina)")]
        [Tooltip("Scale of the whole block; 1 = original size.")] [Range(.3f, 2f)] public float resourcePanelScale = 1.15f;
        [Tooltip("Distance from the left of the screen.")] public float resourcePanelLeft = 24f;
        [Tooltip("Distance from the top of the screen; the XP header is about 48 tall, so smaller values overlap it.")] public float resourcePanelTop = 64f;
        [Tooltip("Whether the dark backdrop behind the health and stamina bars is shown; off leaves only the two bars.")] public bool resourcePanelBackground;

        [Header("Wave texts at the top centre")]
        [Tooltip("Font size of \"Wave 01 / 14 · 18 s\".")] public int waveClockFont = 26;
        [Tooltip("Font size of \"Batch 1 · 10 alive · 0 pending\".")] public int batchFont = 18;
        [Tooltip("Font size of the wave banner (large text).")] public int bannerFont = 28;

        [Header("Bottom row: auto weapon slots (bottom left)")]
        public SlotGroup auto = new(new Vector2(170f, 112f), 20f, new Vector2Int(11, 13), new Vector2Int(13, 17), new Vector2Int(11, 13), new Color(.62f, .86f, .92f));
        [Header("Bottom row: hand weapon slots (centre)")]
        public SlotGroup hand = new(new Vector2(256f, 116f), -70f, new Vector2Int(11, 13), new Vector2Int(14, 19), new Vector2Int(12, 14), new Color(.93f, .97f, 1f));
        [Header("Bottom row: skill slots (bottom right)")]
        public SlotGroup skill = new(new Vector2(138f, 112f), 20f, new Vector2Int(11, 13), new Vector2Int(13, 17), new Vector2Int(11, 13), new Color(.82f, .74f, 1f));

        [Header("Layout inside a slot")]
        [Tooltip("Distance between the text and the slot's side frame (the frame itself is about 14 wide).")] public float padding = 22f;
        [Tooltip("Key row distance from the slot's top edge.")] public float keyTop = 14f;
        [Tooltip("Name row distance from the slot's top edge.")] public float titleTop = 40f;
        [Tooltip("Detail row distance from the slot's bottom edge (too small overlaps the frame).")] public float detailBottom = 32f;
        public float detailHeight = 18f;
        [Tooltip("Cooldown bar distance from the slot's bottom edge.")] public float cooldownBottom = 9f;
        public float cooldownHeight = 4f;
        [Tooltip("Width and height of the weapon icon on the right.")] public Vector2 weaponIconSize = new(64f, 36f);
        [Tooltip("Side length of the skill icon at the right of a skill slot.")] public float skillIconSize = 32f;
        [Tooltip("Side length of the small element icon.")] public float elementIconSize = 26f;

        [Header("Above the bottom row (the charge bar and the hint follow the centre of the hand slot group)")]
        [Tooltip("Height of the charge bar above the hand slots' top edge.")] public float chargeBarAboveRow = 14f;
        [Tooltip("Horizontal offset of the charge bar's centre from the centre of the hand slot group; positive is right.")] public float chargeBarOffsetX;
        [Tooltip("Charge bar width; 0 = keep the width saved in the scene.")] public float chargeBarWidth;
        [Tooltip("Height of the hint above the hand slots' top edge.")] public float hintAboveRow = 28f;
        [Tooltip("Horizontal offset of the hint's centre from the centre of the hand slot group; positive is right.")] public float hintOffsetX;
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
            XpHeader();
            ResourcePanel();
            WaveTexts();
            LayOutBottomRow();
            AboveRow();
            appliedState = CurrentState();
#if UNITY_EDITOR
            if (!Application.isPlaying && gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

                                                                                                                            
                                                                                                                                
        [SerializeField, HideInInspector] string appliedState;

                                                                                                                                       
                                                                                                                                  
                                                                                                                                       
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
                                                                                                                                 
                                                 
            float glyphRight = w - padding;
                                                                                                              
            float iconW = isSkill ? skillIconSize : weaponIconSize.x, iconH = isSkill ? skillIconSize : weaponIconSize.y;
            float elementX = glyphRight - elementIconSize;
            Place(slot.icon, elementX, keyTop - 2f, elementIconSize, elementIconSize);
                                                                                                                            
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
