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

        [System.NonSerialized] public bool nameOnTwoLines;

        [System.NonSerialized] public int compactDetailSize;
        string renderedTitle, renderedDetail;

        const string Separator = " · ";

        static readonly Regex SpaceBesideNumber = new(@"(?<=[^\x00-\xFF]) (?=[+\d])|(?<=\d) (?=[^\x00-\xFF])");

        static readonly Regex SpaceBetweenNumbers = new(@"(?<=\d) (?=\d)");

        string TitleLines(string name)
        {
            int separator = nameOnTwoLines && name != null ? name.IndexOf(Separator, System.StringComparison.Ordinal) : -1;
            return separator < 0 ? name : name.Substring(0, separator) + "\n" + name.Substring(separator + Separator.Length);
        }

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
            surface.color = surface.sprite
                ? slot.active ? Color.white : new Color(.65f, .75f, .85f, 1f)
                : slot.active ? new Color(.10f, .31f, .39f, .96f) : new Color(.04f, .08f, .13f, .94f);
            title.color = ArenaRarity.ColorFor(slot.rarity);
            cooldown.fillAmount = Mathf.Clamp01(slot.cooldown01);
            V2Hud.SetElementIcon(icon, art, slot.elementIndex);
            V2Hud.SetWeaponIcon(weaponIcon, art, slot.formIndex);
            V2Hud.SetContentIcon(contentIcon, art, slot.skillIndex, -1, -1);
            if (contentIcon && !slot.available) contentIcon.color = new Color(.48f, .55f, .63f);
        }
    }
}
