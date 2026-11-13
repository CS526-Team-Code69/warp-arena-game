using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// What the title (V5Title) is drawn with. The plates, the medal, the keycaps, the pointer, the chips, pips and bars, the masks, the two
    /// lights and the logos are pictures drawn at twice the size of the 1920 x 1080 mock-up (work/art-plans/style-1112/ui-e/sprites). They
    /// are kept as .bytes under Resources/V5/Title/Sprites and decoded when first used, so they keep their exact pixels: an imported picture
    /// is rescaled to a power of two by default, which would move the nine-slice borders and soften the rims. The chamfer mask, the stripes,
    /// the shades over the picture and the speaker are drawn here. Sizes on the 1440 x 810 canvas are the mock-up's times Css.
    /// </summary>
    public static class V5TitleKit
    {
        /// <summary>Canvas units per pixel of the 1920 x 1080 mock-up.</summary>
        public const float Css = .75f;
        /// <summary>The pixels-per-unit multiplier that draws a picture made at twice the mock-up's size at the mock-up's size.</summary>
        public const float PieceDensity = 2f / Css;
        /// <summary>How far the mock-up's stripes repeat along a row: every 12 pixels across the stripe, 12 x √2 along the row.</summary>
        public const float StripeRepeat = 12f * 1.41421f * Css;
        /// <summary>The go button's hazard stripes repeat every 14 pixels across the stripe.</summary>
        public const float HazardRepeat = 14f * 1.41421f * Css;

        const string SpriteFolder = "V5/Title/Sprites/";
        // The corner cut the plates were drawn with: 16 mock-up pixels, 32 in the file.
        const float PlateCut = 32f;
        const int ChamferSize = 64, ChamferCut = 16, StripeSize = 32;

        // Each picture: its size in the file and its nine-slice border in the file's pixels (left, bottom, right, top), as the
        // mock-up's sprites.json gives them.
        static readonly Dictionary<string, (int w, int h, Vector4 border)> Table = new()
        {
            ["plate"] = (192, 192, Vector4.one * 40f),
            ["plate-hover"] = (192, 192, Vector4.one * 40f),
            ["plate-selected"] = (192, 192, Vector4.one * 40f),
            ["button-go"] = (192, 192, Vector4.one * 40f),
            ["medal"] = (152, 152, Vector4.zero),
            ["keycap"] = (100, 80, Vector4.one * 26f),
            ["keycap-dark"] = (100, 80, Vector4.one * 26f),
            ["pointer"] = (36, 56, Vector4.zero),
            ["chip"] = (80, 44, new Vector4(14f, 0f, 14f, 0f)),
            ["pip"] = (30, 22, Vector4.zero),
            ["hazard-tab"] = (144, 20, Vector4.zero),
            ["bar-track"] = (96, 32, Vector4.one * 6f),
            ["bar-fill"] = (32, 32, Vector4.one * 4f),
            ["hex-mask"] = (120, 104, Vector4.zero),
            ["disc"] = (128, 128, Vector4.zero),
            ["sweep"] = (128, 512, Vector4.zero),
            ["ripple"] = (512, 512, Vector4.zero),
            ["logo-zh"] = (1055, 302, Vector4.zero),
            ["logo-en"] = (1311, 272, Vector4.zero),
            ["map-dunes"] = (160, 160, Vector4.zero),
            ["map-ruins"] = (160, 160, Vector4.zero),
            // The battle screens' own (V5HudKit): two more plates of the mock-up's set, the gauge ring, the hull-plate banner, the
            // mining lamp that stands beside it and the helmet portrait.
            ["plate-warm-selected"] = (192, 192, Vector4.one * 40f),
            ["plate-teal"] = (192, 192, Vector4.one * 40f),
            ["gauge-ring"] = (253, 256, Vector4.zero),
            ["banner"] = (1150, 213, Vector4.zero),
            ["lamp-post"] = (335, 466, Vector4.zero),
            ["portrait"] = (192, 192, Vector4.zero),
            // The base page's and the crate window's: the hexagonal frame round a base zone's picture, the crate lid's card frame and the
            // crashed ship cut out for the ship panel.
            ["hex-frame"] = (260, 225, Vector4.zero),
            ["crate-card"] = (838, 1070, Vector4.zero),
            ["ship-cutout"] = (760, 464, Vector4.zero),
        };

        static readonly Dictionary<string, Sprite> loaded = new();
        static Sprite chamfer;
        static Texture2D stripes, shadeAcross, shadeUp, fadeRight, speaker;
        static Font builtin;

        // ───── Colours (the mock-up's ui.css): orange acts, crimson is life, teal the human systems, gold time and rewards, moonlight
        // blue the night ─────
        public static readonly Color Ink = Rgb(236, 231, 220), Ink2 = Rgb(180, 184, 198), Ink3 = Rgb(125, 131, 151), Paint = Rgb(233, 223, 201);
        public static readonly Color Orange = Rgb(242, 106, 43), OrangeHi = Rgb(255, 155, 94), Teal = Rgb(59, 211, 192), Crimson = Rgb(226, 59, 78),
            Gold = Rgb(246, 188, 76), Steel = Rgb(121, 174, 224), Moon = Rgb(140, 200, 255), Dark = Rgb(27, 14, 6), Chosen = Rgb(255, 244, 230);
        public static readonly Color Fire = Rgb(255, 122, 46), Frost = Rgb(130, 219, 255), Bolt = Rgb(255, 217, 94), Venom = Rgb(155, 225, 93);

        public static Color Rgb(int r, int g, int b, float a = 1f) => new(r / 255f, g / 255f, b / 255f, a);

        /// <summary>The game's font once the text tables are in, Unity's built-in one until then.</summary>
        public static Font Font => ArenaText.Font ? ArenaText.Font : builtin ? builtin : builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // ───── Pictures ─────

        /// <summary>One of the title's pictures by name (as in the mock-up's sprites folder), its border set for nine-slicing; null when
        /// the file is missing.</summary>
        public static Sprite Piece(string name)
        {
            if (loaded.TryGetValue(name, out var sprite) && sprite) return sprite;
            var asset = Resources.Load<TextAsset>(SpriteFolder + name);
            if (!asset) return null;
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "V5Title " + name };
            bool decoded = texture.LoadImage(asset.bytes, true);
            Resources.UnloadAsset(asset);
            if (!decoded)
            {
                Object.Destroy(texture);
                return null;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            var border = Vector4.zero;
            if (Table.TryGetValue(name, out var entry) && entry.w > 0) border = entry.border * (texture.width / (float)entry.w);
            sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            loaded[name] = sprite;
            return sprite;
        }

        /// <summary>A picture's size in its file, in pixels (twice the mock-up's).</summary>
        public static Vector2 PieceSize(string name) => Table.TryGetValue(name, out var entry) ? new Vector2(entry.w, entry.h) : Vector2.one;

        /// <summary>The pixels-per-unit multiplier that draws a plate with its corners cut at cutCss mock-up pixels.</summary>
        public static float PlateDensity(float cutCss) => PlateCut / (Mathf.Max(1f, cutCss) * Css);

        /// <summary>The multiplier that cuts the chamfer mask's corners at cutCss mock-up pixels.</summary>
        public static float ChamferDensity(float cutCss) => ChamferCut / (Mathf.Max(1f, cutCss) * Css);

        /// <summary>A white plate with the top-left and bottom-right corners cut, sliced at the cut: the shape the lights of a plate are
        /// clipped to, and an empty slot's stripes and fill.</summary>
        public static Sprite Chamfer
        {
            get
            {
                if (chamfer) return chamfer;
                const int n = ChamferSize;
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        // Inside distance from the two cut edges (rows count up from the bottom), half a pixel of smoothing.
                        float px = x + .5f, py = y + .5f;
                        float topLeft = (px + (n - py) - ChamferCut) * .7071f, bottomRight = ((n - px) + py - ChamferCut) * .7071f;
                        float alpha = Mathf.Clamp01(Mathf.Min(topLeft, bottomRight) + .5f);
                        pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                var texture = NewTexture("V5Title chamfer", n, n, pixels, TextureWrapMode.Clamp);
                chamfer = Sprite.Create(texture, new Rect(0f, 0f, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * ChamferCut);
                chamfer.name = "chamfer";
                return chamfer;
            }
        }

        /// <summary>White stripes at 45 degrees, half and half, rising to the right, that repeat without a seam (for a RawImage whose
        /// uvRect repeats them every StripeRepeat units).</summary>
        public static Texture2D Stripes
        {
            get
            {
                if (stripes) return stripes;
                const int n = StripeSize;
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cover = 0f;
                        for (int sy = 0; sy < 4; sy++)
                            for (int sx = 0; sx < 4; sx++)
                                if (Mathf.Repeat(x + (sx + .5f) / 4f - (y + (sy + .5f) / 4f), n) < n * .5f) cover += 1f / 16f;
                        pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(cover * 255f));
                    }
                return stripes = NewTexture("V5Title stripes", n, n, pixels, TextureWrapMode.Repeat);
            }
        }

        /// <summary>The mock-up's shade over the left of the picture, across the screen: deep at the left edge, clear from two thirds on.</summary>
        public static Texture2D ShadeAcross => shadeAcross ? shadeAcross : shadeAcross = Gradient("V5Title shade", 256, 1, new[]
        {
            (0f, Rgb(9, 8, 26, .90f)), (.32f, Rgb(12, 10, 34, .76f)), (.56f, Rgb(12, 10, 34, .30f)), (.66f, Rgb(12, 10, 34, 0f)), (1f, Rgb(12, 10, 34, 0f)),
        });

        /// <summary>The shade along the screen's foot, clear from 16 % of the height up.</summary>
        public static Texture2D ShadeUp => shadeUp ? shadeUp : shadeUp = Gradient("V5Title shade up", 1, 128, new[]
        {
            (0f, Rgb(9, 8, 26, .70f)), (.16f, Rgb(9, 8, 26, 0f)), (1f, Rgb(9, 8, 26, 0f)),
        });

        /// <summary>A hairline that fades to the right (the window's rule).</summary>
        public static Texture2D FadeRight => fadeRight ? fadeRight : fadeRight = Gradient("V5Title rule", 64, 1, new[]
        {
            (0f, new Color(1f, 1f, 1f, 1f)), (1f, new Color(1f, 1f, 1f, .23f)),
        });

        /// <summary>The mock-up's speaker beside the volume: an outlined speaker and two arcs, white on clear (tint it).</summary>
        public static Texture2D Speaker
        {
            get
            {
                if (speaker) return speaker;
                const int n = 64;
                const float scale = n / 24f, half = .9f;
                // The mock-up's 24-unit drawing (rows down): M4 9 h4 l5 -4 v14 l-5 -4 H4 z, then arcs of radius 5 and 8.5.
                var shape = new[] { new Vector2(4f, 9f), new Vector2(8f, 9f), new Vector2(13f, 5f), new Vector2(13f, 19f), new Vector2(8f, 15f), new Vector2(4f, 15f) };
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2((x + .5f) / scale, (n - y - .5f) / scale);
                        float d = float.MaxValue;
                        for (int i = 0; i < shape.Length; i++) d = Mathf.Min(d, Segment(p, shape[i], shape[(i + 1) % shape.Length]));
                        d = Mathf.Min(d, Arc(p, new Vector2(12.93f, 12f), 5f, 44.4f));
                        d = Mathf.Min(d, Arc(p, new Vector2(12.98f, 12f), 8.5f, 44.9f));
                        float alpha = Mathf.Clamp01((half - d) * scale + .5f);
                        pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                    }
                return speaker = NewTexture("V5Title speaker", n, n, pixels, TextureWrapMode.Clamp);
            }
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-5f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Distance to an arc round centre, opening to the right, halfDegrees either side of the horizontal.</summary>
        static float Arc(Vector2 p, Vector2 centre, float radius, float halfDegrees)
        {
            var d = p - centre;
            float angle = Mathf.Abs(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            if (angle <= halfDegrees) return Mathf.Abs(d.magnitude - radius);
            float a = halfDegrees * Mathf.Deg2Rad;
            var top = centre + new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * radius;
            var bottom = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            return Mathf.Min(Vector2.Distance(p, top), Vector2.Distance(p, bottom));
        }

        static Texture2D Gradient(string name, int w, int h, (float at, Color colour)[] stops)
        {
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float t = w > 1 ? x / (w - 1f) : y / (h - 1f);
                    var colour = stops[stops.Length - 1].colour;
                    for (int i = 0; i < stops.Length - 1; i++)
                        if (t <= stops[i + 1].at)
                        {
                            colour = Color.Lerp(stops[i].colour, stops[i + 1].colour, Mathf.InverseLerp(stops[i].at, stops[i + 1].at, t));
                            break;
                        }
                    pixels[y * w + x] = colour;
                }
            return NewTexture(name, w, h, pixels, TextureWrapMode.Clamp);
        }

        static Texture2D NewTexture(string name, int w, int h, Color32[] pixels, TextureWrapMode wrap)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        // ───── Building ─────

        public static RectTransform Rect(Transform parent, string name)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        /// <summary>An image that takes no clicks, sliced at the density given when the picture has a border.</summary>
        public static Image Picture(Transform parent, string name, Sprite sprite, Color colour, float density = 0f)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            image.rectTransform.SetParent(parent, false);
            Dress(image, sprite, colour, density);
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Sets an image's picture: sliced at the density given when the picture has a border, whole otherwise (a flat
        /// rectangle without a picture).</summary>
        public static void Dress(Image image, Sprite sprite, Color colour, float density = 0f)
        {
            if (!image) return;
            bool sliced = sprite && sprite.border != Vector4.zero && density > 0f;
            if (image.sprite != sprite) image.sprite = sprite;
            var type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            if (image.type != type) image.type = type;
            float multiplier = sliced ? density : 1f;
            if (!Mathf.Approximately(image.pixelsPerUnitMultiplier, multiplier)) image.pixelsPerUnitMultiplier = multiplier;
            if (image.color != colour) image.color = colour;
        }

        public static RawImage Raw(Transform parent, string name, Texture texture, Color colour)
        {
            var image = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage)).GetComponent<RawImage>();
            image.rectTransform.SetParent(parent, false);
            image.texture = texture;
            image.color = colour;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>A one-line label (it never wraps or loses its end; FitWidth sizes it to its box), in the title's font.</summary>
        public static Text Label(Transform parent, string name, int size, Color colour, TextAnchor anchor, bool bold = false)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text)).GetComponent<Text>();
            text.rectTransform.SetParent(parent, false);
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = colour;
            text.alignment = anchor;
            text.supportRichText = true;
            text.raycastTarget = false;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        /// <summary>A label of several lines: it wraps in its box and is fitted with ArenaTextFit (no best fit: pitfall T-015).</summary>
        public static void Wrap(Text text)
        {
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        /// <summary>The letters of the mock-up's paint: cream at the top, sand at the foot, a dark shadow under them.</summary>
        public static void Painted(Text text)
        {
            if (!text) return;
            text.color = Color.white;
            if (!text.TryGetComponent<V5TextGradient>(out var gradient)) gradient = text.gameObject.AddComponent<V5TextGradient>();
            gradient.top = Rgb(251, 244, 230);
            gradient.bottom = Rgb(217, 204, 176);
            if (!text.TryGetComponent<Shadow>(out var shadow)) shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .55f);
            shadow.effectDistance = new Vector2(0f, -1.5f);
        }

        /// <summary>Left and top from the parent's top-left corner, as the mock-up's boxes.</summary>
        public static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            if (!rect) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(left, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static void Stretch(RectTransform rect, float inset = 0f)
        {
            if (!rect) return;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = Vector2.one * inset;
            rect.offsetMax = Vector2.one * -inset;
        }

        public static void Centre(RectTransform rect, Vector2 size)
        {
            if (!rect) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
        }

        /// <summary>The mock-up's spaced capitals ("STORY · SAVES" to "S T O R Y   ·   S A V E S"): the font has no letter spacing.</summary>
        public static string Spaced(string caps)
        {
            var text = new System.Text.StringBuilder(caps.Length * 2);
            foreach (char c in caps)
            {
                if (text.Length > 0) text.Append(' ');
                text.Append(c);
            }
            return text.ToString();
        }

        /// <summary>A one-line label at the largest size, from largest down to smallest, at which it fits its box's width.</summary>
        public static int FitWidth(Text label, int largest, int smallest)
        {
            if (!label) return 0;
            float width = label.rectTransform.rect.width;
            int size = largest;
            for (; size > smallest; size--)
            {
                label.fontSize = size;
                if (label.preferredWidth <= width) break;
            }
            label.fontSize = size;
            return size;
        }

        /// <summary>The mock-up's CSS cubic-bezier(x1, y1, x2, y2) easing at time t (0 to 1).</summary>
        public static float Bezier(float t, float x1, float y1, float x2, float y2)
        {
            t = Mathf.Clamp01(t);
            float u = t;
            for (int i = 0; i < 8; i++)
            {
                float x = Curve(u, x1, x2) - t;
                if (Mathf.Abs(x) < 1e-4f) break;
                float slope = Slope(u, x1, x2);
                if (Mathf.Abs(slope) < 1e-5f) break;
                u = Mathf.Clamp01(u - x / slope);
            }
            return Curve(u, y1, y2);
        }

        static float Curve(float u, float a, float b) => ((1f - 3f * b + 3f * a) * u + (3f * b - 6f * a)) * u * u + 3f * a * u;

        static float Slope(float u, float a, float b) => 3f * (1f - 3f * b + 3f * a) * u * u + 2f * (3f * b - 6f * a) * u + 3f * a;

        /// <summary>CSS "ease-out".</summary>
        public static float EaseOut(float t) => Bezier(t, 0f, 0f, .58f, 1f);
    }
}
