using System.Text.RegularExpressions;
using UnityEngine;

namespace AnomalyArena
{
    public class V2UiSlot : MonoBehaviour
    {
        public UnityEngine.UI.Text title, detail;
        public UnityEngine.UI.Image surface, icon, cooldown;
        public UnityEngine.UI.Image weaponIcon;
        public UnityEngine.UI.Image contentIcon;

        /// <summary>Set by V2HudLayout where the title box holds two lines: a weapon's form, then its anomaly and Mk ("步枪" / "蓄力射击 · Mk 2").</summary>
        [System.NonSerialized] public bool nameOnTwoLines;
        /// <summary>Set by V2HudLayout where the detail line is short of room: the size to keep it at. 0 shows the text as it comes.</summary>
        [System.NonSerialized] public int compactDetailSize;
        /// <summary>Added by V2Hud to the bottom row's slots.</summary>
        [System.NonSerialized] public V2SlotFeedback feedback;
        string renderedTitle, renderedDetail;
        WhiteboxSkin.Entry surfaceSkin;

        const string Separator = " · ";
        // "等级 1" to "等级1", "20 能量" to "20能量", "攻速 +30%" to "攻速+30%"; a space between two parts or two words stays.
        static readonly Regex SpaceBesideNumber = new(@"(?<=[^\x00-\xFF]) (?=[+\d])|(?<=\d) (?=[^\x00-\xFF])");
        // A space is two pixels at this size, so "等级1 35能量" read as level 135.
        static readonly Regex SpaceBetweenNumbers = new(@"(?<=\d) (?=\d)");

        string TitleLines(string name)
        {
            int separator = nameOnTwoLines && name != null ? name.IndexOf(Separator, System.StringComparison.Ordinal) : -1;
            return separator < 0 ? name : name.Substring(0, separator) + "\n" + name.Substring(separator + Separator.Length);
        }

        // The separator's dot is a full character wide, so "普通 · 等级 1 · 20 能量" lost its energy cost in an 86-wide slot. Without the dots
        // the usual lines fit. A weapon's rarity, level and element come first (parts 0 to 2) and what it is doing now after them. When that
        // is too long, rarity gives way first (the name's colour shows it), then the element (its icon shows it), then the level; if even
        // the live parts are too long for this size, as many as fit stay, down to the last one.
        string DetailLine(string text)
        {
            if (compactDetailSize <= 0 || string.IsNullOrEmpty(text)) return text;
            var parts = SpaceBesideNumber.Replace(text, "").Split(Separator);
            string line = SpaceBetweenNumbers.Replace(string.Join(" ", parts), "  ");
            if (parts.Length <= 3 || DetailFits(line, compactDetailSize)) return line;
            string now = string.Join(" ", parts, 3, parts.Length - 3);
            line = $"{parts[1]} {parts[2]} {now}";
            if (DetailFits(line, compactDetailSize)) return line;
            line = $"{parts[1]} {now}";
            if (DetailFits(line, compactDetailSize)) return line;
            if (DetailFits(now, compactDetailSize)) return now;
            // One above best fit's smallest size, so rounding to whole pixels at other canvas scales still fits.
            for (int first = 3; first < parts.Length - 1; first++)
            {
                now = string.Join(" ", parts, first, parts.Length - first);
                if (DetailFits(now, detail.resizeTextMinSize + 1)) return now;
            }
            return parts[parts.Length - 1];
        }

        bool DetailFits(string line, int size)
        {
            var settings = detail.GetGenerationSettings(detail.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = size;
            settings.scaleFactor = 1f;
            var generator = detail.cachedTextGeneratorForLayout;
            generator.Populate(line, settings);
            return generator.characterCountVisible >= line.Length;
        }

        public void Render(ArenaUiSlot slot, V2ArtSet art)
        {
            if (slot == null) return;
            if (renderedTitle != slot.title) { renderedTitle = slot.title; title.text = TitleLines(slot.title); }
            if (renderedDetail != slot.detail) { renderedDetail = slot.detail; detail.text = DetailLine(slot.detail); }
            // 2026-11-10 (second round): in whitebox the drawn frame gives way to a plain rectangle with a thin edge (WhiteboxSkin); with no
            // sprite the line below gives it the flat colours. Checked at each render, so the art switch shows at the next refresh.
            surfaceSkin ??= new WhiteboxSkin.Entry(surface, WhiteboxSkin.Look.Frame);
            surfaceSkin.Follow();
            surface.color = surface.sprite
                ? slot.active ? Color.white : new Color(.65f, .75f, .85f, 1f)
                : slot.active ? new Color(.10f, .31f, .39f, .96f) : new Color(.04f, .08f, .13f, .94f);
            // The name's colour shows the rarity; a void name is written in a colour that reads on the dark slot.
            title.color = ArenaRarity.InkFor(slot.rarity);
            cooldown.fillAmount = Mathf.Clamp01(slot.cooldown01);
            V2Hud.SetElementIcon(icon, art, slot.elementIndex);
            V2Hud.SetWeaponIcon(weaponIcon, art, slot.formIndex);
            V2Hud.SetContentIcon(contentIcon, art, slot.skillIndex, -1, -1);
            if (contentIcon && !slot.available) contentIcon.color = new Color(.48f, .55f, .63f);
            if (feedback) feedback.Bind(slot);
        }
    }
}
