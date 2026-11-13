using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// One slot of the dock in the fifth pass's look (work/art-plans/style-1112/ui-e/hud.html, .slot): a chamfered plate (warm, with the
    /// hazard tab on its top edge, for the hand in use; the empty plate for a slot with nothing in it), the key row (a keycap and which slot
    /// it is), the weapon's picture or the skill's icon in an octagon, the name, and the bottom row: the tier chip and the kind and element
    /// words, with the element's medal and, on a hand slot, a corner triangle in the element's colour. It restyles V2UiSlot's saved parts in
    /// place: the title is the name, the detail the bottom row's words, the weapon picture and the skill icon are still filled by V2Hud's
    /// setters, and the saved key line keeps the words V2SlotFeedback writes into it but is not drawn. V2UiSlot.Render hands the slot's
    /// values here once this is installed (V5BattleHud does that when V5HudKit.On).
    /// The weapon's level (its upgrade count) is not shown: the mock-up's bottom row is the tier and the kind; a weapon's tier is its Mk
    /// (RunWeapon.mk, written into the slot's title as "Mk n"), its kind the anomaly it revealed. A skill's level shows on its chip,
    /// under its keycap.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class V5BattleSlot : V5HudPiece
    {
        public enum Kind { Hand, Auto, Skill }

        /// <summary>A slot's size in mock-up pixels: the mock-up's hand (336 x 124) and auto (214 x 100) slots; a skill slot, which the
        /// mock-up does not have, as tall as a hand slot, so its octagon sits above its name, and narrow, so seven slots fit the dock.</summary>
        public static Vector2 SizeOf(Kind kind) => kind switch
        {
            Kind.Hand => new Vector2(336f, 124f),
            Kind.Auto => new Vector2(214f, 100f),
            _ => new Vector2(132f, 124f),
        };

        /// <summary>The plate's corner cut in mock-up pixels; V2SlotFeedback draws the slot's flashes and lit edge with the same cut.</summary>
        public const float CutPx = 12f;
        /// <summary>The cut in canvas units.</summary>
        public static int CutUnits => Mathf.RoundToInt(CutPx * V5BattleKit.Css);

        const float Css = V5BattleKit.Css;
        // The mock-up's slot (pixels): text 16 in from the left, the key row 14 down, the bottom row 12 up.
        const float Pad = 16f, KeyTop = 14f, KeyH = 24f, KeyFontPx = 13f, KeyGap = 8f, RowBottom = 12f, RowH = 20f, ChipPadX = 7f, ChipGap = 8f, WordGap = 10f;
        const float MedalRight = 12f, MedalBottom = 10f, UsingRight = 14f, CornerSize = 46f, TabLeft = 26f, TabW = 72f, TabH = 10f;
        const float OctagonSize = 50f, OctagonRight = 12f, OctagonTop = 10f, OctagonRim = 2.5f, OctagonIcon = .78f, SkillChipTop = 43f, SkillChipH = 18f;
        const string Separator = " · ";
        static readonly Color Unready = new(.48f, .55f, .63f);
        static readonly Regex LevelPart = new(@"^(?:等级|Lv)\s*(\d+)$");
        static readonly Regex MkPart = new(@"^Mk\s*(\d+)$");

        V2UiSlot slot;
        Kind kind;
        int index;
        Vector2 size;
        V5HudPlate plate;
        Image tab, corner, chip, octagonRim, octagonHole;
        RectTransform octagon;
        GameObject cooldownBar;
        V5HudKey key;
        Text keyWord, chipText;
        V5HudMedal medal;
        ArenaUiSlot model;
        V2ArtSet art;
        readonly List<(string text, int weight)> words = new();
        // What the slot drew last (Render draws again only what changed).
        bool drawn, drawnActive, drawnAvailable, drawnArt, drawnChinese, drawnTagReady;
        string drawnTitle, drawnDetail, drawnTagText;
        int drawnForm, drawnSkill, drawnElement, drawnKeys;
        CardRarity drawnRarity;
        V2ArtSet drawnSet;
        Font drawnFont, drawnTagFont;
        float drawnKeyWidth;
        // What TakeBack gives back: the saved key line, and where the skill icon stood before it moved into the octagon.
        Text savedKeyWords;
        Transform contentHome;
        int contentSibling;
        (Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size) contentPlace;

        public Kind SlotKind => kind;
        /// <summary>Mock-up pixels.</summary>
        public Vector2 Size => size;
        /// <summary>How many of the slot's first children are drawn under everything else (the plate): V2SlotFeedback puts its
        /// flashes right above them.</summary>
        public int LayersBelow => plate ? 1 : 0;
        /// <summary>Where a skill's cooldown turns: inside its octagon, over its icon (null on a weapon slot).</summary>
        public RectTransform ClockHolder => octagonHole ? octagonHole.rectTransform : null;
        /// <summary>Whether the in-use line (V2SlotFeedback's tag) has been moved to this slot's bottom-right corner: it is made on the
        /// slot's top edge, so V2SlotFeedback keeps it hidden until then.</summary>
        public bool InUsePlaced { get; private set; }

        /// <summary>Restyles a saved slot (once) and has V2UiSlot hand it its values.</summary>
        public static V5BattleSlot Install(V2UiSlot slot, Kind kind, int index)
        {
            // A saved slot without its name or its words is left in its saved look.
            if (!slot || !slot.title || !slot.detail) return null;
            if (slot.v5) return slot.v5;
            var v5 = slot.gameObject.AddComponent<V5BattleSlot>();
            v5.Build(slot, kind, index);
            slot.v5 = v5;
            return v5;
        }

        void Build(V2UiSlot saved, Kind slotKind, int slotIndex)
        {
            slot = saved;
            kind = slotKind;
            index = slotIndex;
            size = SizeOf(kind);
            var rect = (RectTransform)transform;
            rect.sizeDelta = size * Css;
            // The saved frame gives way to the plate; its whitebox skin is never made (V2UiSlot skips it), so only the plate dresses here.
            if (slot.surface) slot.surface.enabled = false;
            plate = V5HudKit.Plate(transform, "V5Plate", V5HudKit.PlateLook.Plain, CutPx, Css);
            V5TitleKit.Stretch(plate.Rect);
            plate.transform.SetAsFirstSibling();
            // The saved key line keeps the words V2SlotFeedback writes (the tests read them); the keycap row below says it now.
            if (transform.Find("SlotKey") is { } savedKey && savedKey.TryGetComponent<Text>(out var savedWords))
            {
                savedWords.enabled = false;
                savedKeyWords = savedWords;
            }
            // The element shows as the medal and the corner triangle.
            if (slot.icon) slot.icon.gameObject.SetActive(false);
            if (kind == Kind.Hand)
            {
                corner = V5TitleKit.Picture(transform, "V5Corner", V5BattleKit.Corner, Color.white);
                V5BattleKit.Anchor(corner.rectTransform, Vector2.one, Vector2.zero, Vector2.one * CornerSize * Css);
                corner.gameObject.SetActive(false);
                tab = V5TitleKit.Picture(transform, "V5Tab", null, Color.white);
                V5BattleKit.Place(tab.rectTransform, TabLeft, -TabH * .5f, TabW, TabH);
                tab.gameObject.SetActive(false);
            }
            key = V5HudKit.Key(transform, "V5Key", "", true, Css, KeyH, KeyFontPx);
            key.Rect.anchorMin = key.Rect.anchorMax = key.Rect.pivot = new Vector2(0f, 1f);
            key.Rect.anchoredPosition = new Vector2(Pad * Css, -KeyTop * Css);
            keyWord = V5BattleKit.Label(transform, "V5KeyWord", 14f, V5TitleKit.Ink3, TextAnchor.MiddleLeft, true);
            if (slot.weaponIcon)
            {
                slot.weaponIcon.preserveAspect = true;
                if (!slot.weaponIcon.TryGetComponent<Shadow>(out var shadow)) shadow = slot.weaponIcon.gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, .55f);
                shadow.effectDistance = new Vector2(0f, -3f * Css);
            }
            if (kind == Kind.Skill) BuildOctagon();
            // The name and the words over the picture, and over the plate's flashes.
            Dress(slot.title, kind == Kind.Hand ? 24f : 19f, true);
            slot.title.transform.SetAsLastSibling();
            chip = V5TitleKit.Picture(transform, "V5Tier", WhiteboxSkin.Square, V5BattleKit.TierOne);
            chipText = V5BattleKit.Label(chip.transform, "Label", 14f, V5BattleKit.TierInk, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(chipText.rectTransform);
            chip.gameObject.SetActive(false);
            Dress(slot.detail, 14f, false);
            slot.detail.color = V5TitleKit.Ink2;
            slot.detail.transform.SetAsLastSibling();
            medal = V5HudKit.Medal(transform, "V5Element", (kind == Kind.Hand ? 34f : 28f) * Css);
            V5BattleKit.Anchor(medal.Rect, new Vector2(1f, 0f), new Vector2(-MedalRight * Css, MedalBottom * Css), Vector2.one * medal.Size);
            medal.gameObject.SetActive(false);
            DressCooldown();
            LayOut();
            Redress();
        }

        static void Dress(Text text, float px, bool bold)
        {
            if (!text) return;
            text.resizeTextForBestFit = false;
            text.fontSize = V5BattleKit.Font(px);
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1f;
            text.supportRichText = true;
            text.raycastTarget = false;
        }

        void BuildOctagon()
        {
            octagon = V5TitleKit.Rect(transform, "V5Octagon");
            octagonRim = V5TitleKit.Picture(octagon, "Rim", V5BattleKit.Octagon, V5BattleKit.OctagonRim);
            V5TitleKit.Stretch(octagonRim.rectTransform);
            octagonHole = V5TitleKit.Picture(octagon, "Hole", V5BattleKit.Octagon, V5BattleKit.OctagonFill);
            V5TitleKit.Stretch(octagonHole.rectTransform, OctagonRim * Css);
            octagonHole.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            if (!slot.contentIcon) return;
            var icon = slot.contentIcon.rectTransform;
            contentHome = icon.parent;
            contentSibling = icon.GetSiblingIndex();
            contentPlace = (icon.anchorMin, icon.anchorMax, icon.pivot, icon.anchoredPosition, icon.sizeDelta);
            slot.contentIcon.transform.SetParent(octagonHole.transform, false);
            slot.contentIcon.preserveAspect = true;
            V5TitleKit.Stretch(slot.contentIcon.rectTransform, (OctagonSize - 2f * OctagonRim) * (1f - OctagonIcon) * .5f * Css);
        }

        /// <summary>A weapon's cooldown as a thin line along the plate's foot, inside its rim; a skill's turns over its octagon instead.</summary>
        void DressCooldown()
        {
            var bar = slot.cooldown ? slot.cooldown.transform.parent as RectTransform : null;
            if (!bar || bar == transform) return;
            cooldownBar = bar.gameObject;
            float width = size.x - 28f;
            V5BattleKit.Place(bar, 14f, size.y - 8f, width, 3f);
            V5TitleKit.Place(slot.cooldown.rectTransform, 0f, 0f, width * Css, 3f * Css);
            if (bar.TryGetComponent<Image>(out var back))
            {
                back.sprite = WhiteboxSkin.Square;
                back.type = Image.Type.Simple;
                back.color = new Color(0f, 0f, 0f, .45f);
            }
            slot.cooldown.sprite = WhiteboxSkin.Square;
            slot.cooldown.color = V5TitleKit.Steel;
            cooldownBar.SetActive(false);
        }

        void LayOut()
        {
            float w = size.x, h = size.y;
            bool hand = kind == Kind.Hand;
            if (slot.weaponIcon)
            {
                float pw = hand ? 190f : 110f, ph = hand ? 70f : 48f, top = hand ? 12f : 10f;
                // Pivot in the middle: an attack's pop (V2SlotFeedback) grows from the picture's middle.
                var r = slot.weaponIcon.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
                r.pivot = new Vector2(.5f, .5f);
                r.anchoredPosition = new Vector2((w - 12f - pw * .5f) * Css, -(top + ph * .5f) * Css);
                r.sizeDelta = new Vector2(pw, ph) * Css;
                r.localScale = Vector3.one;
            }
            if (octagon) V5BattleKit.Place(octagon, w - OctagonRight - OctagonSize, OctagonTop, OctagonSize, OctagonSize);
            float namePx = hand ? 24f : 19f, nameBottom = hand ? 42f : 38f, nameH = namePx + 6f;
            V5BattleKit.Place(slot.title.rectTransform, Pad, h - nameBottom - namePx * .5f - nameH * .5f, w - 2f * Pad, nameH);
            // The bottom row until the first values come (RenderBottomRow places it again with the chip before it).
            V5BattleKit.Place(slot.detail.rectTransform, Pad, h - RowBottom - RowH, w - 2f * Pad, RowH);
            V5TitleKit.Place(keyWord.rectTransform, Pad * Css + key.Rect.sizeDelta.x + KeyGap * Css, KeyTop * Css, 1f, KeyH * Css);
        }

        // ───── Values ─────

        /// <summary>Shows the slot's values (V2UiSlot.Render calls this in place of its own, ten times a second). The plate, the key row,
        /// the pictures and the name are drawn again only when what they show changed; the bottom row also when the words or the in-use
        /// line changed; otherwise only the cooldown moves, so an unchanged slot builds no words and fits no text.</summary>
        public void Render(ArenaUiSlot value, V2ArtSet set)
        {
            if (value == null || !plate) return;
            model = value;
            art = set;
            bool empty = value.formIndex < 0 && value.skillIndex < 0;
            bool inUse = kind == Kind.Hand && value.active && !empty;
            bool medalShown = !empty && kind != Kind.Skill && value.elementIndex >= 0;
            var font = V5BattleKit.TextFont;
            var tagLabel = slot.feedback ? slot.feedback.InUseLabel : null;
            bool tagReady = slot.feedback && slot.feedback.InUseTagRect;
            string tagText = tagLabel ? tagLabel.text : null;
            var tagFont = tagLabel ? tagLabel.font : null;
            // The keycap fits its name when the game's font arrives (V5HudKey), after a draw: its new width places the word after it again.
            bool head = !drawn || value.title != drawnTitle || value.formIndex != drawnForm || value.skillIndex != drawnSkill
                || value.elementIndex != drawnElement || value.rarity != drawnRarity || value.active != drawnActive
                || value.available != drawnAvailable || set != drawnSet || Art.Enabled != drawnArt || ArenaText.IsChinese != drawnChinese
                || ArenaKeys.Revision != drawnKeys || font != drawnFont || !Mathf.Approximately(KeyWidth, drawnKeyWidth);
            bool row = head || value.detail != drawnDetail || tagReady != drawnTagReady || tagText != drawnTagText || tagFont != drawnTagFont;
            if (head) RenderHead(value, set, empty, inUse, medalShown);
            if (row)
            {
                float usingWidth = RenderInUse(inUse, medalShown);
                float right = size.x - (medalShown ? MedalRight + medal.Size / Css + 8f : Pad) - (usingWidth > 0f ? usingWidth / Css + 8f : 0f);
                RenderBottomRow(value, empty, right);
                drawn = true;
                drawnTitle = value.title;
                drawnDetail = value.detail;
                drawnForm = value.formIndex;
                drawnSkill = value.skillIndex;
                drawnElement = value.elementIndex;
                drawnRarity = value.rarity;
                drawnActive = value.active;
                drawnAvailable = value.available;
                drawnSet = set;
                drawnArt = Art.Enabled;
                drawnChinese = ArenaText.IsChinese;
                drawnKeys = ArenaKeys.Revision;
                drawnFont = font;
                drawnKeyWidth = KeyWidth;
                drawnTagReady = tagReady;
                drawnTagText = tagText;
                drawnTagFont = tagFont;
            }
            RenderCooldown(value, empty);
        }

        float KeyWidth => key.gameObject.activeSelf ? key.Rect.sizeDelta.x : 0f;

        /// <summary>The plate's look, the hazard tab, the key row, the pictures, the element's medal and corner, and the name.</summary>
        void RenderHead(ArenaUiSlot value, V2ArtSet set, bool empty, bool inUse, bool medalShown)
        {
            plate.SetLook(empty ? V5HudKit.PlateLook.Empty : inUse ? V5HudKit.PlateLook.Warm : V5HudKit.PlateLook.Plain);
            if (tab && tab.gameObject.activeSelf != inUse) tab.gameObject.SetActive(inUse);
            RenderKey(value, empty);

            V2Hud.SetWeaponIcon(slot.weaponIcon, set, value.formIndex);
            V2Hud.SetContentIcon(slot.contentIcon, set, value.skillIndex, -1, -1);
            if (slot.contentIcon && !value.available) slot.contentIcon.color = Unready;

            int element = medalShown ? value.elementIndex : -1;
            if (medal.gameObject.activeSelf != medalShown) medal.gameObject.SetActive(medalShown);
            if (medalShown)
                medal.SetIcons(set && set.elementIcons != null && element < set.elementIcons.Length ? set.elementIcons[element] : null, WhiteboxIcons.Element(element));
            if (corner)
            {
                if (corner.gameObject.activeSelf != medalShown) corner.gameObject.SetActive(medalShown);
                if (medalShown) corner.color = V5BattleKit.Element(element);
            }

            // The name: a weapon's form (its kind and tier are in the bottom row), a skill's name; its colour is the rarity's.
            V5BattleKit.Sync(slot.title);
            string name = empty ? ArenaText.Pair("空", "Empty") : kind == Kind.Skill ? value.title : FormName(value);
            if (slot.title.text != name) slot.title.text = name;
            slot.title.color = empty ? V5TitleKit.Ink3 : ArenaRarity.InkFor(value.rarity);
            V5TitleKit.FitWidth(slot.title, V5BattleKit.Font(kind == Kind.Hand ? 24f : 19f), V5BattleKit.Font(12f));
        }

        /// <summary>A weapon's cooldown along the plate's foot (a skill's turns over its octagon, V2SlotFeedback): every draw.</summary>
        void RenderCooldown(ArenaUiSlot value, bool empty)
        {
            float cooldown = Mathf.Clamp01(value.cooldown01);
            if (slot.cooldown) slot.cooldown.fillAmount = cooldown;
            bool cooling = cooldownBar && kind != Kind.Skill && !empty && cooldown > .001f;
            if (cooldownBar && cooldownBar.activeSelf != cooling) cooldownBar.SetActive(cooling);
        }

        void RenderKey(ArenaUiSlot value, bool empty)
        {
            string cap, word;
            bool dark = true;
            switch (kind)
            {
                case Kind.Hand:
                    // The buttons follow the weapon: the left one uses the slot in hand, the right one swaps to the other, and an empty
                    // slot that is not in hand has nothing to swap to (V2SlotFeedback's saved key line says the same).
                    cap = value.active ? ArenaText.Pair("左键", "LMB") : !empty ? ArenaText.Pair("右键", "RMB") : "";
                    dark = !value.active;
                    word = ArenaText.Pair($"手持 {index + 1}", $"Hand {index + 1}");
                    break;
                case Kind.Auto:
                    cap = ArenaText.Pair("自动", "AUTO");
                    word = (index + 1).ToString();
                    break;
                default:
                    // The key the player has this skill on (ArenaKeys can move it).
                    cap = index <= 2 ? ArenaKeys.Name(GameAction.Skill1 + index) : (index + 1).ToString();
                    word = "";
                    break;
            }
            bool showCap = cap.Length > 0;
            if (key.gameObject.activeSelf != showCap) key.gameObject.SetActive(showCap);
            if (showCap)
            {
                key.SetDark(dark);
                key.SetText(cap);
            }
            V5BattleKit.Sync(keyWord);
            if (keyWord.text != word) keyWord.text = word;
            float left = Pad * Css + (showCap ? key.Rect.sizeDelta.x + KeyGap * Css : 0f);
            V5TitleKit.Place(keyWord.rectTransform, left, KeyTop * Css, Mathf.Max(1f, V5BattleKit.Width(keyWord) + 2f), KeyH * Css);
        }

        /// <summary>The hand in use says so at the bottom-right corner (left of the element's medal); V2SlotFeedback makes and shows the
        /// line (its tag), this places and colours it. Its width in canvas units, 0 while not in use.</summary>
        float RenderInUse(bool inUse, bool medalShown)
        {
            if (!inUse || !slot.feedback) return 0f;
            var tagRect = slot.feedback.InUseTagRect;
            var label = slot.feedback.InUseLabel;
            if (!tagRect || !label) return 0f;
            label.fontSize = V5BattleKit.Font(13f);
            label.fontStyle = FontStyle.Bold;
            label.color = V5TitleKit.OrangeHi;
            label.alignment = TextAnchor.MiddleRight;
            float width = Mathf.Max(1f, V5BattleKit.Width(label));
            float right = medalShown ? MedalRight + medal.Size / Css + 8f : UsingRight;
            tagRect.anchorMin = tagRect.anchorMax = tagRect.pivot = new Vector2(1f, 0f);
            tagRect.sizeDelta = new Vector2(width + 2f, RowH * Css);
            tagRect.anchoredPosition = new Vector2(-right * Css, RowBottom * Css);
            InUsePlaced = true;
            return width;
        }

        /// <summary>The tier chip, then the words: a weapon's kind (its anomaly, "?" before it is revealed), its element in the element's
        /// colour and what it is doing now (reloading, heat, charges); a skill's energy cost and what it is doing now. When they do not all
        /// fit before right (mock-up pixels), the element word goes first (the medal shows it), then the kind.</summary>
        void RenderBottomRow(ArenaUiSlot value, bool empty, float right)
        {
            string chipWords = "";
            words.Clear();
            if (empty)
                words.Add((ArenaText.Pair(kind == Kind.Skill ? "还没有技能" : "还没有武器", kind == Kind.Skill ? "No skill yet" : "No weapon yet"), 0));
            else if (kind == Kind.Skill) ReadSkill(value, out chipWords);
            else ReadWeapon(value, out chipWords);

            bool chipShown = chipWords.Length > 0;
            if (chip.gameObject.activeSelf != chipShown) chip.gameObject.SetActive(chipShown);
            float rowCentre = size.y - RowBottom - RowH * .5f, left = Pad;
            if (chipShown)
            {
                V5BattleKit.Sync(chipText);
                if (chipText.text != chipWords) chipText.text = chipWords;
                chip.color = chipWords is "MK I" or "LV 1" ? V5BattleKit.TierOne : V5TitleKit.Steel;
                float chipW = V5BattleKit.Width(chipText) / Css + ChipPadX * 2f;
                // A skill slot is narrow: its level chip sits under its keycap, left of its octagon, and its words have the whole row.
                if (kind == Kind.Skill) V5BattleKit.Place(chip.rectTransform, Pad, SkillChipTop, chipW, SkillChipH);
                else
                {
                    V5BattleKit.Place(chip.rectTransform, Pad, rowCentre - RowH * .5f, chipW, RowH);
                    left += chipW + ChipGap;
                }
            }
            var text = slot.detail;
            V5BattleKit.Sync(text);
            text.color = empty ? V5TitleKit.Ink3 : V5TitleKit.Ink2;
            float width = Mathf.Max(1f, right - left);
            V5BattleKit.Place(text.rectTransform, left, rowCentre - RowH * .5f, width, RowH);
            int largest = V5BattleKit.Font(14f), smallest = V5BattleKit.Font(11f);
            text.fontSize = largest;
            // The words that matter least go first while the line is too long; the last one left shrinks to fit.
            for (int drop = 0; ; drop++)
            {
                string line = Line(drop);
                if (text.text != line) text.text = line;
                if (V5BattleKit.Width(text) <= width * Css || CountLeft(drop) <= 1) break;
            }
            V5TitleKit.FitWidth(text, largest, smallest);
        }

        // The words left after dropping the `drop` least important ones (a higher weight goes first), joined.
        string Line(int drop)
        {
            var kept = new List<(string text, int weight)>(words);
            for (int i = 0; i < drop && kept.Count > 1; i++)
            {
                int worst = 0;
                for (int j = 1; j < kept.Count; j++) if (kept[j].weight > kept[worst].weight) worst = j;
                kept.RemoveAt(worst);
            }
            var line = new System.Text.StringBuilder();
            foreach (var (part, _) in kept)
            {
                if (line.Length > 0) line.Append("  ");
                line.Append(part);
            }
            return line.ToString();
        }

        int CountLeft(int drop) => Mathf.Max(1, words.Count - drop);

        void ReadWeapon(ArenaUiSlot value, out string chipWords)
        {
            ReadTitle(value.title, out string anomaly, out int mk, out bool hidden);
            chipWords = "MK " + V5BattleKit.Roman(Mathf.Max(1, mk));
            if (hidden) words.Add((ArenaText.Pair("？", "?"), 1));
            else if (anomaly.Length > 0) words.Add((anomaly, 1));
            if (value.elementIndex >= 0)
                words.Add(($"<color=#{V5BattleKit.Hex(V5BattleKit.Element(value.elementIndex))}>{ElementWord(value.elementIndex)}</color>", 2));
            // The detail is "rarity · level · element", then what the weapon is doing now (GameManager's WriteSlot).
            var parts = (value.detail ?? "").Split(Separator);
            for (int i = 3; i < parts.Length; i++) words.Add((Live(parts[i]), 0));
        }

        void ReadSkill(ArenaUiSlot value, out string chipWords)
        {
            chipWords = "";
            // The detail is "rarity · level · cost", the greater void skills "rarity · cost", then what the skill is doing now.
            var parts = (value.detail ?? "").Split(Separator);
            int i = parts.Length > 1 ? 1 : 0;
            if (i < parts.Length && LevelPart.Match(parts[i].Trim()) is { Success: true } level)
            {
                chipWords = "LV " + level.Groups[1].Value;
                i++;
            }
            // The cost stays whatever the room; what the skill is doing now (Blink's charges) goes first when the row is short.
            if (i < parts.Length) words.Add(($"<color=#{V5BattleKit.Hex(V5HudKit.VioletHi)}>{Live(parts[i])}</color>", 0));
            for (i++; i < parts.Length; i++) words.Add((Live(parts[i]), 1));
        }

        static string Live(string part) => V2UiSlot.SpaceBetweenNumbers.Replace(V2UiSlot.SpaceBesideNumber.Replace(part.Trim(), ""), "  ");

        static string ElementWord(int index) => ArenaTerms.Element(index switch { 5 => ElementKind.Light, 6 => ElementKind.Void, _ => (ElementKind)(index + 1) });

        /// <summary>A weapon slot's name: its form (RunWeapon.FormName), from the form number or else the title's first part.</summary>
        static string FormName(ArenaUiSlot value)
        {
            if (value.formIndex >= 0 && System.Enum.IsDefined(typeof(WeaponForm), value.formIndex)) return ArenaTerms.WeaponForm((WeaponForm)value.formIndex);
            string title = value.title ?? "";
            int cut = title.IndexOf(Separator, System.StringComparison.Ordinal);
            string first = cut < 0 ? title : title.Substring(0, cut);
            return first.EndsWith(" ?") ? first.Substring(0, first.Length - 2) : first;
        }

        /// <summary>A weapon's title (RunWeapon.Label): its form, then its anomaly once revealed ("?" after the form before), then "Mk n"
        /// from the second tier on.</summary>
        static void ReadTitle(string title, out string anomaly, out int mk, out bool hidden)
        {
            anomaly = "";
            mk = 1;
            hidden = false;
            if (string.IsNullOrEmpty(title)) return;
            var parts = title.Split(Separator);
            hidden = parts[0].EndsWith("?");
            for (int i = 1; i < parts.Length; i++)
            {
                var found = MkPart.Match(parts[i].Trim());
                if (found.Success) { mk = int.Parse(found.Groups[1].Value); continue; }
                if (anomaly.Length == 0) anomaly = parts[i].Trim();
            }
        }

        protected override void Dress(bool artOn)
        {
            if (!plate) return;
            if (tab) V5TitleKit.Dress(tab, artOn ? V5TitleKit.Piece("hazard-tab") : null, artOn ? Color.white : V5TitleKit.Orange);
            if (octagonRim) octagonRim.color = artOn ? V5BattleKit.OctagonRim : WhiteboxSkin.Edge;
            if (octagonHole) octagonHole.color = artOn ? V5BattleKit.OctagonFill : WhiteboxSkin.PlateFill;
            // The pictures follow at once rather than at the HUD's next refresh.
            if (model != null)
            {
                drawn = false;
                Render(model, art);
            }
        }

        /// <summary>Gives the slot back to its saved look when the battle HUD's build stopped half way (V5BattleHud.Abandon): V2UiSlot
        /// draws it again, its saved frame, key line and element icon show, the skill icon goes back where it stood, and the pieces made
        /// here go. The saved layout (V2HudLayout.Apply) places the slot and its parts after this.</summary>
        internal void TakeBack()
        {
            if (slot)
            {
                if (slot.v5 == this) slot.v5 = null;
                if (slot.surface) slot.surface.enabled = true;
                if (slot.icon) slot.icon.gameObject.SetActive(true);
                if (cooldownBar) cooldownBar.SetActive(true);
                if (slot.contentIcon && contentHome && slot.contentIcon.transform.parent != contentHome)
                {
                    var icon = slot.contentIcon.rectTransform;
                    icon.SetParent(contentHome, false);
                    icon.SetSiblingIndex(contentSibling);
                    (icon.anchorMin, icon.anchorMax, icon.pivot, icon.anchoredPosition, icon.sizeDelta) = contentPlace;
                }
            }
            if (savedKeyWords) savedKeyWords.enabled = true;
            // Every piece made here is named V5...
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i);
                if (child.name.StartsWith("V5", System.StringComparison.Ordinal)) Destroy(child.gameObject);
            }
            Destroy(this);
        }
    }
}
