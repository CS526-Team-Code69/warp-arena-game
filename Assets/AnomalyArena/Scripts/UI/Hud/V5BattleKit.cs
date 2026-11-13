using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// What the fifth pass's battle HUD (V5BattleHud, V5BattleSlot, V5BattleEdge) draws with beyond the shared kit (V5HudKit): the
    /// mock-up's bar colours (work/art-plans/style-1112/ui-e/hud.html), the element colours, and a few pictures made here (a bar's vertical
    /// gradient, the gaps that cut a bar into tenths, a skill's octagon, an element weapon's corner triangle, the foes' mark, the round
    /// backdrop behind the portrait). Sizes are the mock-up's pixels times Css on the battle canvas (V2Canvas, 1440 x 810).
    /// </summary>
    public static class V5BattleKit
    {
        /// <summary>Canvas units per mock-up pixel on the battle canvas.</summary>
        public const float Css = V5HudKit.HudCss;

        // The mock-up's bars: life crimson, stamina cream; experience in the light blue of the orbs on the ground (V2WorldLabels' edge
        // notice for them), so the bar and what fills it read as one thing; a hand weapon's charge in orange (act on it).
        public static readonly Color LifeTop = V5TitleKit.Rgb(255, 107, 120), LifeFoot = V5TitleKit.Rgb(184, 30, 51), LifeLost = V5TitleKit.Rgb(247, 201, 207, .85f),
            StaminaTop = V5TitleKit.Rgb(255, 246, 226), StaminaFoot = V5TitleKit.Rgb(201, 191, 168), Tired = V5TitleKit.Rgb(122, 140, 160),
            XpTop = V5TitleKit.Rgb(140, 214, 255), XpFoot = V5TitleKit.Rgb(47, 127, 192), ShieldBand = V5TitleKit.Rgb(233, 243, 255, .95f),
            ChargeTop = V5TitleKit.Rgb(255, 196, 156), ChargeFoot = V5TitleKit.Orange, BossTop = V5TitleKit.Rgb(255, 107, 120), BossFoot = V5TitleKit.Rgb(184, 30, 51);
        /// <summary>The tier chip: MK I on pale grey, MK II and up on steel, dark ink on both.</summary>
        public static readonly Color TierOne = V5TitleKit.Rgb(201, 206, 216), TierInk = V5TitleKit.Rgb(11, 26, 42);
        public static readonly Color OctagonRim = V5TitleKit.Rgb(122, 132, 152), OctagonFill = V5TitleKit.Rgb(13, 19, 34);
        /// <summary>A skill's cooldown over its octagon: a dark wash that turns away like a clock's hand.</summary>
        public static readonly Color CooldownWash = new(.02f, .03f, .08f, .62f);
        static readonly Color Earth = V5TitleKit.Rgb(204, 170, 130), Light = V5TitleKit.Rgb(255, 240, 174), VoidElement = V5TitleKit.Rgb(181, 123, 255);

        /// <summary>An element's colour by the art set's elementIcons index (0 fire, 1 poison, 2 frost, 3 lightning, 4 earth, 5 light,
        /// 6 void): ui.css's element colours, and the game's own for earth and light, which ui.css does not name.</summary>
        public static Color Element(int index) => index switch
        {
            0 => V5TitleKit.Fire,
            1 => V5TitleKit.Venom,
            2 => V5TitleKit.Frost,
            3 => V5TitleKit.Bolt,
            4 => Earth,
            5 => Light,
            6 => VoidElement,
            _ => V5TitleKit.Ink2
        };

        /// <summary>A whole number of canvas units for a size in mock-up pixels (font sizes are whole).</summary>
        public static int Font(float px) => Mathf.Max(6, Mathf.RoundToInt(px * Css));

        /// <summary>The font the battle HUD's words are drawn in: the game's once the text tables have brought it (ArenaText.Font); until
        /// then the font the saved HUD was saved with (Fallback, set by V5BattleHud), which has the Chinese letters too.</summary>
        public static Font TextFont => ArenaText.Font ? ArenaText.Font : Fallback ? Fallback : V5TitleKit.Font;
        /// <summary>The saved HUD's own font (V5BattleHud sets it from a saved label when it is installed).</summary>
        public static Font Fallback { get; set; }

        /// <summary>Gives a label the battle HUD's font if it has another (the game's font arrives after the HUD is built).</summary>
        public static void Sync(Text text)
        {
            if (!text) return;
            var font = TextFont;
            if (text.font != font) text.font = font;
        }

        /// <summary>A one-line label in the battle HUD's font, sized in mock-up pixels.</summary>
        public static Text Label(Transform parent, string name, float px, Color colour, TextAnchor anchor, bool bold = false)
        {
            var text = V5TitleKit.Label(parent, name, Font(px), colour, anchor, bold);
            text.font = TextFont;
            return text;
        }

        /// <summary>Left and top from the parent's top-left corner and the size, all in mock-up pixels.</summary>
        public static void Place(RectTransform rect, float left, float top, float width, float height) =>
            V5TitleKit.Place(rect, left * Css, top * Css, width * Css, height * Css);

        /// <summary>A rectangle in canvas units, its pivot at the anchor given (corners and edges of the parent).</summary>
        public static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            if (!rect) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>A label's width as drawn (rich text tags take no room).</summary>
        public static float Width(Text text) => text ? text.preferredWidth : 0f;

        /// <summary>The roman numeral of a weapon's tier (MK I to MK X and on).</summary>
        public static string Roman(int n)
        {
            if (n <= 0) return "I";
            var text = new System.Text.StringBuilder();
            foreach (var (value, mark) in Numerals)
                while (n >= value) { text.Append(mark); n -= value; }
            return text.ToString();
        }

        static readonly (int value, string mark)[] Numerals =
            { (1000, "M"), (900, "CM"), (500, "D"), (400, "CD"), (100, "C"), (90, "XC"), (50, "L"), (40, "XL"), (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };

        /// <summary>A colour as rich text's hex.</summary>
        public static string Hex(Color colour) => ColorUtility.ToHtmlStringRGBA(colour);

        // ───── Made pictures ─────

        static readonly Dictionary<(Color, Color), Sprite> fills = new();
        static Texture2D gaps;
        static Sprite octagon, corner, foeMark, faceBack;

        /// <summary>A bar's fill with the art on: the top colour at its top, the foot colour at its foot.</summary>
        public static Sprite Fill(Color top, Color foot)
        {
            if (fills.TryGetValue((top, foot), out var made) && made) return made;
            const int w = 4, h = 32;
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                Color32 c = Color.Lerp(foot, top, y / (h - 1f));
                for (int x = 0; x < w; x++) pixels[y * w + x] = c;
            }
            return fills[(top, foot)] = Create("V5Battle fill", w, h, pixels);
        }

        /// <summary>The gaps that cut a bar into tenths (ui.css .bar.seg): clear, with a dark line at the end of each repeat; a RawImage
        /// repeats it ten times along the bar.</summary>
        public static Texture2D Gaps
        {
            get
            {
                if (gaps) return gaps;
                const int w = 20;
                var pixels = new Color32[w];
                Color32 line = V5HudKit.Track, clear = new(0, 0, 0, 0);
                for (int x = 0; x < w; x++) pixels[x] = x >= w - 2 ? line : clear;
                gaps = new Texture2D(w, 1, TextureFormat.RGBA32, false) { name = "V5Battle gaps", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
                gaps.SetPixels32(pixels);
                gaps.Apply(false, true);
                return gaps;
            }
        }

        /// <summary>A white octagon (ui.css .oct): the shape that means a skill.</summary>
        public static Sprite Octagon
        {
            get
            {
                if (octagon) return octagon;
                const float n = 64f, a = .29f * n, b = .71f * n;
                return octagon = Polygon("V5Battle octagon", 64, new[]
                {
                    new Vector2(a, 0f), new Vector2(b, 0f), new Vector2(n, a), new Vector2(n, b), new Vector2(b, n), new Vector2(a, n), new Vector2(0f, b), new Vector2(0f, a),
                }, Color.white);
            }
        }

        /// <summary>The corner triangle of an element weapon's hand slot (ui.css .tri-corner): the slot's top-right corner, white at the
        /// corner and darker toward its long edge (tint it with the element's colour).</summary>
        public static Sprite Corner
        {
            get
            {
                if (corner) return corner;
                const int n = 64;
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float px = x + .5f, py = y + .5f;
                        // Inside: above the diagonal from the top-left corner to the bottom-right one (rows count up from the bottom).
                        float inside = (px + py - n) * .7071f;
                        float t = Mathf.Clamp01(((n - px) + (n - py)) / n);
                        float shade = Mathf.Lerp(1f, .5f, t);
                        pixels[y * n + x] = new Color(shade, shade, shade, Mathf.Clamp01(inside + .5f));
                    }
                return corner = Create("V5Battle corner", n, n, pixels);
            }
        }

        /// <summary>The foes' mark (the crimson triangle before 活敌 in the mock-up), small inside its square so a round medal does not
        /// clip it.</summary>
        public static Sprite FoeMark
        {
            get
            {
                if (foeMark) return foeMark;
                const float n = 64f;
                return foeMark = Polygon("V5Battle foe mark", 64, new[] { new Vector2(n * .5f, n * .8f), new Vector2(n * .2f, n * .26f), new Vector2(n * .8f, n * .26f) },
                    V5TitleKit.Crimson);
            }
        }

        /// <summary>The round backdrop behind the portrait (hud.html .face .pic): deep blue lit a little above the middle, darker at the rim.</summary>
        public static Sprite FaceBack
        {
            get
            {
                if (faceBack) return faceBack;
                const int n = 128;
                Color lit = V5TitleKit.Rgb(30, 53, 80), deep = V5TitleKit.Rgb(11, 18, 34);
                var pixels = new Color32[n * n];
                var centre = new Vector2(n * .5f, n * .5f);
                var light = new Vector2(n * .5f, n * .6f);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2(x + .5f, y + .5f);
                        float alpha = Mathf.Clamp01(n * .5f - Vector2.Distance(p, centre) + .5f);
                        var c = Color.Lerp(lit, deep, Mathf.Clamp01(Vector2.Distance(p, light) / (n * .6f)));
                        c.a = alpha;
                        pixels[y * n + x] = c;
                    }
                return faceBack = Create("V5Battle face", n, n, pixels);
            }
        }

        static Sprite Polygon(string name, int n, Vector2[] counterClockwise, Color colour)
        {
            var pixels = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float inside = float.MaxValue;
                    for (int i = 0; i < counterClockwise.Length; i++)
                        inside = Mathf.Min(inside, LeftOf(p, counterClockwise[i], counterClockwise[(i + 1) % counterClockwise.Length]));
                    var c = colour;
                    c.a *= Mathf.Clamp01(inside + .5f);
                    pixels[y * n + x] = c;
                }
            return Create(name, n, n, pixels);
        }

        // How far p is to the left of the line from a to b, in pixels.
        static float LeftOf(Vector2 p, Vector2 a, Vector2 b)
        {
            var along = (b - a).normalized;
            var ap = p - a;
            return along.x * ap.y - along.y * ap.x;
        }

        static Sprite Create(string name, int w, int h, Color32[] pixels)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }

    /// <summary>A bar of the fifth pass's battle HUD (ui.css .bar): the mock-up's dark track (its picture with the art on, a flat track
    /// with the thin edge in whitebox) under a fill in a vertical gradient (a flat colour in whitebox), optionally cut into tenths, with a
    /// lighter chunk for what was just lost and a band for a shield over the fill. The HUD sets the values; it dresses itself again when
    /// the art switch changes.</summary>
    public sealed class V5BattleBar : V5HudPiece
    {
        Image track, lost, fill, band;
        RawImage cuts;
        Outline edge;
        Color top = Color.white, foot = Color.white;
        float value = -1f, lostFrom = -1f, bandValue = -1f;

        public RectTransform Rect => (RectTransform)transform;
        public Image FillImage => fill;

        /// <summary>A bar under parent; place it with its Rect.</summary>
        public static V5BattleBar Create(Transform parent, string name, Color top, Color foot, bool tenths, bool withLost = false, bool withBand = false)
        {
            var root = V5TitleKit.Rect(parent, name);
            var bar = root.gameObject.AddComponent<V5BattleBar>();
            bar.Build(top, foot, tenths, withLost, withBand);
            return bar;
        }

        void Build(Color topColour, Color footColour, bool tenths, bool withLost, bool withBand)
        {
            top = topColour;
            foot = footColour;
            track = V5TitleKit.Picture(transform, "Track", null, V5HudKit.Track);
            V5TitleKit.Stretch(track.rectTransform);
            if (withLost)
            {
                lost = V5TitleKit.Picture(transform, "Lost", WhiteboxSkin.Square, V5BattleKit.LifeLost);
                lost.gameObject.SetActive(false);
            }
            fill = V5TitleKit.Picture(transform, "Fill", WhiteboxSkin.Square, Color.white);
            V5TitleKit.Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;
            if (withBand)
            {
                band = V5TitleKit.Picture(transform, "Shield", WhiteboxSkin.Square, V5BattleKit.ShieldBand);
                var rect = band.rectTransform;
                rect.anchorMin = new Vector2(0f, .62f);
                rect.anchorMax = Vector2.one;
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                band.type = Image.Type.Filled;
                band.fillMethod = Image.FillMethod.Horizontal;
                band.fillOrigin = (int)Image.OriginHorizontal.Left;
                band.gameObject.SetActive(false);
            }
            if (tenths)
            {
                cuts = V5TitleKit.Raw(transform, "Tenths", V5BattleKit.Gaps, Color.white);
                V5TitleKit.Stretch(cuts.rectTransform);
                cuts.uvRect = new Rect(0f, 0f, 10f, 1f);
            }
            Redress();
        }

        /// <summary>The fill's two colours (the stamina bar greys out while it is spent).</summary>
        public void SetColours(Color topColour, Color footColour)
        {
            if (top == topColour && foot == footColour) return;
            top = topColour;
            foot = footColour;
            Redress();
        }

        /// <summary>How full the bar is, 0 to 1.</summary>
        public void SetValue(float fraction)
        {
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Approximately(fraction, value) || !fill) return;
            value = fraction;
            fill.fillAmount = fraction;
            ShowLost();
        }

        /// <summary>The lighter chunk from the fill's end up to from (what was lost a moment ago); at or below the fill it goes.</summary>
        public void SetLost(float from)
        {
            from = Mathf.Clamp01(from);
            if (Mathf.Approximately(from, lostFrom)) return;
            lostFrom = from;
            ShowLost();
        }

        void ShowLost()
        {
            if (!lost) return;
            bool show = lostFrom > value + .002f;
            if (lost.gameObject.activeSelf != show) lost.gameObject.SetActive(show);
            if (!show) return;
            var rect = lost.rectTransform;
            rect.anchorMin = new Vector2(value, 0f);
            rect.anchorMax = new Vector2(lostFrom, 1f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>The shield band over the fill's upper part, 0 to 1 of its length (0 hides it).</summary>
        public void SetBand(float fraction)
        {
            if (!band) return;
            fraction = Mathf.Clamp01(fraction);
            if (Mathf.Approximately(fraction, bandValue)) return;
            bandValue = fraction;
            bool show = fraction > 0f;
            if (band.gameObject.activeSelf != show) band.gameObject.SetActive(show);
            band.fillAmount = fraction;
        }

        protected override void Dress(bool art)
        {
            if (!track) return;
            V5TitleKit.Dress(track, art ? V5TitleKit.Piece("bar-track") : null, art ? Color.white : V5HudKit.Track, V5HudKit.PieceDensity(V5BattleKit.Css));
            if (!art && !edge)
            {
                edge = track.gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectColor = V5HudKit.Hairline;
                edge.effectDistance = new Vector2(1f, -1f);
            }
            if (edge) edge.enabled = !art;
            // The fill keeps its filled type: it is set by hand here, not through V5TitleKit.Dress, which would make it a plain image.
            fill.sprite = art ? V5BattleKit.Fill(top, foot) : WhiteboxSkin.Square;
            fill.color = art ? Color.white : Color.Lerp(top, foot, .35f);
        }
    }
}
