using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>
    /// Chinese line breaking for the game's texts. The game draws its words with the legacy UI Text, which has no rules of its own for
    /// Chinese (only TextMeshPro has leading and following characters): it breaks a line at the last plain space, so a Chinese sentence
    /// with a number in it ("之后 8 秒内") broke at that space and left the line half empty, and a run of Chinese longer than the line is cut
    /// wherever the line is full, so a line could begin with a comma, a closing bracket or the "%" of "10%".
    /// This effect lays such a text out again by these rules: a line is filled before it breaks; closing punctuation (，。、）」％ …) never
    /// begins a line and opening punctuation (（「“ …) never ends one; a number keeps its unit ("8 秒", "10%", "×1.26", "5–12 格") and a
    /// run of Latin letters or digits is never cut; a comma or full stop that would just miss the line hangs at its end instead of
    /// pulling a character down with it. The text's own string is left as it is (tests and code read it); only the drawn mesh changes.
    /// A text whose laid-out lines would be more than Unity's own is drawn as Unity lays it out, so nothing outgrows its box.
    /// Every text that wraps, does not use Best Fit and shows Chinese gets this effect the first time it is about to be drawn (Scan).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChineseLineBreak : BaseMeshEffect
    {
        /// <summary>Off: every text is drawn as Unity lays it out (a check that compares the two turns it off and on with SetEnabled).</summary>
        public static bool Enabled { get; private set; } = true;

        /// <summary>The string this text was last drawn from when this effect laid it out again; null while it is drawn as Unity lays it out.</summary>
        public string Drawn { get; private set; }

        Text text;
        TextGenerator generator;
        readonly UIVertex[] quad = new UIVertex[4];
        static readonly List<Component> modifiers = new();

        // What the last layout was made from, so a mesh drawn again for a colour or a fade does not lay the words out again.
        string keyText;
        float keyWidth;
        int keySize;
        FontStyle keyStyle;
        bool keyRich;
        Font keyFont;
        string keyResult;

        public override void ModifyMesh(VertexHelper vh)
        {
            Drawn = null;
            if (!Enabled || !IsActive()) return;
            if (!text) text = GetComponent<Text>();
            if (!text || !Wants(text) || !text.font || !text.font.dynamic) return;
            var native = text.cachedTextGenerator;
            int explicitLines = 1;
            foreach (char c in text.text) if (c == '\n') explicitLines++;
            // Unity broke no line of its own: every line fits as it stands.
            if (native.lineCount <= explicitLines) return;
            string wrapped = Layout(text, native);
            if (wrapped == null) return;
            if (!Fill(text, wrapped, vh)) return;
            Drawn = wrapped;
            // The effects before this one on the text (an outline, a shadow, a gradient) acted on Unity's mesh: they act on the new one.
            GetComponents(typeof(IMeshModifier), modifiers);
            foreach (var other in modifiers)
            {
                if (ReferenceEquals(other, this)) break;
                ((IMeshModifier)other).ModifyMesh(vh);
            }
            modifiers.Clear();
        }

        /// <summary>The text with its lines broken by the rules above, or null when Unity's own layout stays (a tag that changes the size,
        /// a character the font cannot measure, or more lines than Unity's).</summary>
        string Layout(Text label, TextGenerator native)
        {
            string value = label.text;
            float width = label.rectTransform.rect.width * label.pixelsPerUnit;
            int size = native.fontSizeUsedForBestFit;
            if (size <= 0 || width <= 1f) return null;
            if (ReferenceEquals(value, keyText) && Mathf.Approximately(width, keyWidth) && size == keySize && label.fontStyle == keyStyle
                && label.supportRichText == keyRich && label.font == keyFont)
                return keyResult;
            var font = label.font;
            string result = Wrap(value, label.supportRichText, (c, style) => font.GetCharacterInfo(c, out var glyph, size, style) ? glyph.advance : float.NaN,
                label.fontStyle, width, out int lines);
            if (result != null && lines > native.lineCount) result = null;
            keyText = value;
            keyWidth = width;
            keySize = size;
            keyStyle = label.fontStyle;
            keyRich = label.supportRichText;
            keyFont = font;
            keyResult = result;
            return result;
        }

        /// <summary>Fills the mesh as Text.OnPopulateMesh does, from the string with its lines broken here. The lines are kept as they are
        /// (no wrapping of Unity's on top of them): they were measured with the font's own advances to fit the width.</summary>
        bool Fill(Text label, string value, VertexHelper vh)
        {
            generator ??= new TextGenerator(value.Length);
            Vector2 extents = label.rectTransform.rect.size;
            var settings = label.GetGenerationSettings(extents);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            // The font's texture can have changed since the last fill (a cached result would point at glyphs no longer there).
            generator.Invalidate();
            generator.PopulateWithErrors(value, settings, label.gameObject);
            var verts = generator.verts;
            int count = verts.Count;
            if (count <= 0) return false;
            float unitsPerPixel = 1f / label.pixelsPerUnit;
            Vector2 rounding = new Vector2(verts[0].position.x, verts[0].position.y) * unitsPerPixel;
            rounding = label.PixelAdjustPoint(rounding) - rounding;
            vh.Clear();
            for (int i = 0; i < count; i++)
            {
                int corner = i & 3;
                quad[corner] = verts[i];
                quad[corner].position *= unitsPerPixel;
                quad[corner].position.x += rounding.x;
                quad[corner].position.y += rounding.y;
                if (corner == 3) vh.AddUIVertexQuad(quad);
            }
            return true;
        }

        // ───── The rules ─────

        // Never at the start of a line: closing punctuation, the "%" of a number, the long dash and the ellipsis, the separating dot.
        const string NoStart = "!%),.:;?]}¢°·’”‰′″℃∶、。〃〆〉》」』】〕〗〙〛〜〞〟﹚﹜！％），．：；？］｝～｠｣､…‥—–ー々ゝゞ・";
        // Never at the end of a line: opening brackets and quotation marks, a currency sign.
        const string NoEnd = "([{£¥$‘“〈《「『【〔〖〘〚〝（［｛｢￡￥＄«‹";
        // A comma or full stop that just misses the line may hang at its end.
        const string Hangs = "，。、；：！？,.;:!?";
        // Joined to the letters and digits round them in a run that is never cut: "10%", "×1.26", "5–12", "Mk2", "1/3".
        const string WordMarks = "%.,:+-–/'×#&_@";

        static bool IsChinese(char c) => c >= '\u2E80' && c <= '\u9FFF' || c >= '\uF900' && c <= '\uFAFF' || c >= '\uFE30' && c <= '\uFE4F'
                                         || c >= '\uFF00' && c <= '\uFFEF';

        static bool IsDigit(char c) => c >= '0' && c <= '9';

        static bool IsWord(char c) => !IsChinese(c) && (char.IsLetterOrDigit(c) || WordMarks.IndexOf(c) >= 0);

        /// <summary>The text shows Chinese (the rules are for Chinese lines; a text in English keeps Unity's layout).</summary>
        public static bool HasChinese(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            foreach (char c in value)
                if (c >= '\u3400' && c <= '\u9FFF' || c >= '\u3000' && c <= '\u303F' || c >= '\uFF00' && c <= '\uFFEF') return true;
            return false;
        }

        /// <summary>A line may break between two characters standing side by side.</summary>
        public static bool BreakBetween(char before, char after)
        {
            if (NoStart.IndexOf(after) >= 0 || NoEnd.IndexOf(before) >= 0) return false;
            if (IsDigit(before) && IsChinese(after)) return false; // a number and its unit: "5处"
            if (IsWord(before) && IsWord(after)) return false;
            return IsChinese(before) || IsChinese(after);
        }

        /// <summary>A line may break at a plain space standing between these two characters (the space goes).</summary>
        public static bool BreakAtSpace(char before, char after)
        {
            if (NoStart.IndexOf(after) >= 0 || NoEnd.IndexOf(before) >= 0) return false;
            return !(IsDigit(before) && IsChinese(after)); // a number and its unit: "8 秒", "现有 2 盏"
        }

        struct Glyph
        {
            public int at;     // its place in the string
            public char c;
            public float width;
        }

        static readonly List<Glyph> glyphs = new();
        static readonly List<int> breaks = new();     // places in the string where a new line begins
        static readonly HashSet<int> dropped = new(); // places of the spaces a break takes (each becomes the line break)
        static readonly HashSet<int> skipped = new(); // places of further spaces at the start of a new line (not drawn)
        static readonly System.Text.StringBuilder built = new();

        /// <summary>
        /// The text with a line break wherever a line is full, by the rules above: advance(c, style) is a character's width (NaN: it
        /// cannot be measured) and width the room of a line, in the same units. Rich text tags take no room (b and i change the style the
        /// characters are measured in); a tag that changes the size (size, quad, material) makes this give up. Null when it gives up;
        /// lines is the number of lines the result has.
        /// </summary>
        public static string Wrap(string value, bool richText, Func<char, FontStyle, float> advance, FontStyle style, float width, out int lines)
        {
            lines = 0;
            if (string.IsNullOrEmpty(value)) return value;
            glyphs.Clear();
            breaks.Clear();
            dropped.Clear();
            skipped.Clear();
            int bold = 0, italic = 0;
            bool baseBold = style is FontStyle.Bold or FontStyle.BoldAndItalic, baseItalic = style is FontStyle.Italic or FontStyle.BoldAndItalic;
            // Each line between explicit line breaks is laid out on its own.
            lines = 1;
            for (int i = 0; i <= value.Length; i++)
            {
                if (i == value.Length || value[i] == '\n')
                {
                    if (!LayParagraph(width)) return null;
                    glyphs.Clear();
                    if (i < value.Length) lines++;
                    continue;
                }
                char c = value[i];
                if (richText && c == '<' && Tag(value, i, out int end, out string name, out bool closing))
                {
                    if (name is "size" or "quad" or "material") return null;
                    if (name == "b") bold += closing ? -1 : 1;
                    else if (name == "i") italic += closing ? -1 : 1;
                    i = end;
                    continue;
                }
                if (c == '\r') continue;
                bool b = baseBold || bold > 0, it = baseItalic || italic > 0;
                var drawn = b && it ? FontStyle.BoldAndItalic : b ? FontStyle.Bold : it ? FontStyle.Italic : FontStyle.Normal;
                float w = advance(c, drawn);
                if (float.IsNaN(w)) return null;
                glyphs.Add(new Glyph { at = i, c = c, width = w });
            }
            lines += breaks.Count;
            if (breaks.Count == 0) return value;
            built.Clear();
            int next = 0;
            breaks.Sort();
            for (int i = 0; i < value.Length; i++)
            {
                while (next < breaks.Count && breaks[next] < i) next++;
                if (dropped.Contains(i))
                {
                    built.Append('\n');
                    continue;
                }
                if (skipped.Contains(i)) continue;
                if (next < breaks.Count && breaks[next] == i && !dropped.Contains(i - 1)) built.Append('\n');
                built.Append(value[i]);
            }
            return built.ToString();
        }

        /// <summary>Lays one paragraph's characters (glyphs) out into lines of width, adding where each new line begins to breaks (a space
        /// taken by a break goes to dropped). False when a character is wider than... never: a single character wider than the line keeps
        /// its line.</summary>
        static bool LayParagraph(float width)
        {
            int count = glyphs.Count, start = 0;
            const float Slack = .01f;
            bool broken = false; // the line follows a break made here
            while (start < count)
            {
                // Spaces at the start of a line that a break left are not drawn.
                while (start < count && glyphs[start].c == ' ')
                {
                    if (broken) skipped.Add(glyphs[start].at);
                    start++;
                }
                if (start >= count) break;
                float x = 0f;
                int k = start, cut = -1;       // cut: the last place in this line where it may break (the first glyph of the next line)
                bool cutIsSpace = false;
                int lineEnd = count;          // the first glyph of the next line
                bool spaceTaken = false;
                for (; k < count; k++)
                {
                    var g = glyphs[k];
                    if (g.c == ' ')
                    {
                        if (k > start && k + 1 < count && BreakAtSpace(glyphs[k - 1].c, Next(k)))
                        {
                            cut = k;
                            cutIsSpace = true;
                        }
                        x += g.width; // a space at the end of a line may stand past it
                        continue;
                    }
                    if (k > start && glyphs[k - 1].c != ' ' && BreakBetween(glyphs[k - 1].c, g.c))
                    {
                        cut = k;
                        cutIsSpace = false;
                    }
                    if (x + g.width <= width + Slack || k == start)
                    {
                        x += g.width;
                        continue;
                    }
                    // g does not fit. A comma or full stop that just misses hangs at the line's end when what follows may begin a line
                    // (not the point of a number such as 1.26, which belongs to its run).
                    bool inRun = k + 1 < count && IsWord(g.c) && IsWord(glyphs[k + 1].c);
                    if (Hangs.IndexOf(g.c) >= 0 && k > start && !inRun && (k + 1 >= count || NoStart.IndexOf(Next(k)) < 0))
                    {
                        lineEnd = k + 1;
                        break;
                    }
                    if (cut > start)
                    {
                        lineEnd = cut;
                        spaceTaken = cutIsSpace;
                    }
                    else
                        lineEnd = k; // one unbroken run longer than the line: it is cut where the line is full
                    break;
                }
                if (lineEnd >= count) break;
                broken = true;
                // The next line begins at lineEnd (a space there is taken by the break; further spaces are skipped as the line begins).
                if (spaceTaken || glyphs[lineEnd].c == ' ')
                {
                    dropped.Add(glyphs[lineEnd].at);
                    breaks.Add(glyphs[lineEnd].at);
                    start = lineEnd + 1;
                }
                else
                {
                    breaks.Add(glyphs[lineEnd].at);
                    start = lineEnd;
                }
            }
            return true;
        }

        /// <summary>The first character after glyph k that is not a space (a space when none is left).</summary>
        static char Next(int k)
        {
            for (int i = k + 1; i < glyphs.Count; i++)
                if (glyphs[i].c != ' ') return glyphs[i].c;
            return ' ';
        }

        /// <summary>A rich text tag of Unity's at i: its last character's place, its name and whether it closes.</summary>
        static bool Tag(string value, int i, out int end, out string name, out bool closing)
        {
            end = value.IndexOf('>', i + 1);
            name = null;
            closing = false;
            if (end < 0) return false;
            int from = i + 1;
            if (from < end && value[from] == '/')
            {
                closing = true;
                from++;
            }
            int stop = from;
            while (stop < end && char.IsLetter(value[stop])) stop++;
            name = value.Substring(from, stop - from);
            if (name is not ("b" or "i" or "size" or "color" or "material" or "quad")) return false;
            // An opening b or i ends at once; the others carry a value (size=, color=, material=) or attributes (quad).
            if (!closing && name is "b" or "i") return stop == end;
            if (closing) return stop == end && name is not "quad";
            return stop < end && (value[stop] == '=' || name == "quad" && value[stop] == ' ');
        }

        // ───── Which texts get it ─────

        /// <summary>A text this effect lays out: it wraps, does not use Best Fit (which sizes the words by Unity's own lines) and shows Chinese.</summary>
        static bool Wants(Text label) => label && label.horizontalOverflow == HorizontalWrapMode.Wrap && !label.resizeTextForBestFit && HasChinese(label.text);

        static IList<ICanvasElement> queue;
        static CanvasUpdateRegistry queueOwner;
        static bool queueMissing;
        static readonly List<Text> adding = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            Canvas.preWillRenderCanvases -= Scan;
            queue = null;
            queueOwner = null;
            queueMissing = false;
            Enabled = true;
            adding.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()
        {
            Canvas.preWillRenderCanvases -= Scan;
            Canvas.preWillRenderCanvases += Scan;
        }

        /// <summary>Just before the canvases are drawn (ahead of Unity's own canvas update): each text waiting to be drawn again that wants
        /// this effect and does not have it yet gets it, so it is laid out by the rules from its first frame on. The waiting texts are
        /// Unity's list of graphics to rebuild; should that list not be found, every text on the screen is looked at every tenth frame.</summary>
        static void Scan()
        {
            if (!Application.isPlaying) return;
            var waiting = RebuildQueue();
            if (waiting != null)
            {
                for (int i = 0; i < waiting.Count; i++)
                    if (waiting[i] is Text label && Wants(label)) adding.Add(label);
            }
            else if (Time.frameCount % 10 == 0)
                foreach (var label in Object.FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (Wants(label)) adding.Add(label);
            foreach (var label in adding)
                if (label && !label.TryGetComponent<ChineseLineBreak>(out _)) label.gameObject.AddComponent<ChineseLineBreak>();
            adding.Clear();
        }

        static IList<ICanvasElement> RebuildQueue()
        {
            if (queueMissing) return null;
            var owner = CanvasUpdateRegistry.instance;
            if (queue != null && ReferenceEquals(owner, queueOwner)) return queue;
            var field = typeof(CanvasUpdateRegistry).GetField("m_GraphicRebuildQueue", BindingFlags.Instance | BindingFlags.NonPublic);
            queue = field?.GetValue(owner) as IList<ICanvasElement>;
            queueOwner = owner;
            if (queue == null)
            {
                queueMissing = true;
                Debug.LogWarning("[ChineseLineBreak] Unity's list of graphics to rebuild was not found; texts are looked at every tenth frame");
            }
            return queue;
        }

        /// <summary>Turns the rules on or off for every text that has them, drawing each again (for checks that compare the two).</summary>
        public static void SetEnabled(bool on)
        {
            Enabled = on;
            foreach (var effect in Object.FindObjectsByType<ChineseLineBreak>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (effect.graphic) effect.graphic.SetVerticesDirty();
        }
    }
}
