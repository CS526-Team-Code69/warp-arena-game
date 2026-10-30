using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace AnomalyArena
{

    public static class ArenaEmphasis
    {
        public static readonly Color BodyColor = new(.80f, .85f, .92f);
        const string NumberColor = "#FFD54A", KeyColor = "#7FD8FF", CostColor = "#FF6B5B";

        static readonly Regex Tags = new(@"(<[^<>]+>)");
        static readonly Regex Terms = new(
            @"(?<key>1\s*/\s*2\s*/\s*3|空格|回车|(?<![A-Za-z])(?:Tab|Esc|ESC|Shift|WASD|E|R|T)(?![A-Za-z]))" +
            @"|(?<cost>会丢失|丢失|失去|结束本局|无法|不能|ends the run|(?<![A-Za-z])(?:lost|lose|cannot)(?![A-Za-z]))" +
            @"|(?<fire>火焰|燃烧|灼烧|(?<![A-Za-z])(?:Fire|Burning|Burn)(?![A-Za-z]))" +
            @"|(?<poison>毒素|中毒|毒云|毒雾|(?<![A-Za-z])(?:Poisoned|Poison)(?![A-Za-z]))" +
            @"|(?<frost>冰冻|冰霜|冻结|冻住|(?<![A-Za-z])(?:Frost|Frozen|Freeze)(?![A-Za-z]))" +
            @"|(?<lightning>雷电|麻痹|感电|(?<![A-Za-z])(?:Lightning|Paralysed|Paralysis)(?![A-Za-z]))" +
            @"|(?<light>光明|(?<![A-Za-z])Light(?![A-Za-z]))" +
            @"|(?<void>虚空|(?<![A-Za-z])Void(?![A-Za-z]))" +
            @"|(?<number>[+\-−×]?\d+(?:\.\d+)?(?:\s?(?:%|％|秒|点|格|层|次|倍|级|个|发|跳|颗|(?<![A-Za-z])s(?![A-Za-z])))?)",
            RegexOptions.CultureInvariant);

        static readonly (string group, string colour)[] Elements =
        {
            ("fire", Hex(V2Hud.ElementColors[0])), ("poison", Hex(V2Hud.ElementColors[1])), ("frost", Hex(V2Hud.ElementColors[2])),
            ("lightning", Hex(V2Hud.ElementColors[3])), ("light", Hex(V2Hud.ElementColors[5])), ("void", Hex(V2Hud.ElementColors[6])),
        };

        public static string Apply(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var result = new StringBuilder(text.Length + 96);
            foreach (var part in Tags.Split(text))
                result.Append(part.Length > 1 && part[0] == '<' && part[part.Length - 1] == '>' ? part : Terms.Replace(part, Colour));
            return result.ToString();
        }

        static string Colour(Match match)
        {
            if (match.Groups["key"].Success) return Bold(match.Value, KeyColor);
            if (match.Groups["cost"].Success) return Bold(match.Value, CostColor);
            if (match.Groups["number"].Success) return Bold(match.Value, NumberColor);
            foreach (var (group, colour) in Elements)
                if (match.Groups[group].Success) return Bold(match.Value, colour);
            return match.Value;
        }

        static string Bold(string value, string colour) => $"<b><color={colour}>{value}</color></b>";
        static string Hex(Color colour) => "#" + ColorUtility.ToHtmlStringRGB(colour);

        public static void Style(UnityEngine.UI.Text label)
        {
            if (!label) return;
            label.supportRichText = true;
            label.color = BodyColor;
            var shadow = label.GetComponent<UnityEngine.UI.Shadow>();
            if (!shadow) shadow = label.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .55f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }
    }
}
