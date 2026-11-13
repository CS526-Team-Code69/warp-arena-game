using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// What the story mode's own layer (V2CampaignUi, a 1600 x 900 canvas) draws its fifth-pass pieces with, beside the battle screens'
    /// shared kit (V5HudKit): the mock-up's pixels on this canvas (StoryCss), the colours of ui.css the kits do not name, and the pictures
    /// only the story needs, made here: the subtitle's dark backing, the speaker's slanted tag, a bar's fill, and the whitebox icons of the
    /// ship's three parts and of the lamp. The small pieces that follow the art switch are V5StoryBar, V5StoryBannerFrame and V5StoryArtOnly.
    /// </summary>
    public static class V5StoryKit
    {
        /// <summary>Canvas units per mock-up pixel on the story layer.</summary>
        public const float Css = V5HudKit.StoryCss;

        // The speaker's tag in its file: 64 x 32 pixels, its top edge starting 16 pixels in from the left and its foot ending 16 short of
        // the right; drawn so the slant is 6 mock-up pixels wide at any height.
        public const int TagWidth = 64, TagHeight = 32, TagSlant = 16;
        public const float TagDensity = TagSlant / (6f * Css);

        /// <summary>Colours of ui.css the kits do not name: the subtitle's dark backing, the amber glow round a lamp, the base's teal bar
        /// (top and foot), the side quest's violet wash and the void's violet glow.</summary>
        public static readonly Color SubtitleBacking = V5TitleKit.Rgb(8, 8, 22, .82f), Amber = V5TitleKit.Rgb(246, 188, 76, .45f),
            BaseTop = V5TitleKit.Rgb(111, 240, 222), BaseBottom = V5TitleKit.Rgb(29, 158, 142), VioletWash = V5TitleKit.Rgb(145, 95, 255, .1f),
            VioletGlow = V5TitleKit.Rgb(145, 95, 255, .5f);

        static Sprite softOval, tag, engine, armor, fuel, lamp;
        static readonly Dictionary<long, Sprite> fills = new();
        static readonly TextGenerator measure = new();

        /// <summary>Canvas units of so many mock-up pixels.</summary>
        public static float Px(float mockPixels) => mockPixels * Css;

        /// <summary>The font size of so many mock-up pixels (never under 8).</summary>
        public static int FontSize(float mockPixels) => Mathf.Max(8, Mathf.RoundToInt(mockPixels * Css));

        /// <summary>A one-line label in the title's font, its size given in mock-up pixels.</summary>
        public static Text Label(Transform parent, string name, float mockPixels, Color colour, TextAnchor anchor, bool bold = false) =>
            V5TitleKit.Label(parent, name, FontSize(mockPixels), colour, anchor, bold);

        /// <summary>How wide a label would draw this text on one line, in canvas units (as its preferredWidth reads its own text).</summary>
        public static float WidthOf(Text label, string text)
        {
            if (!label || !label.font || string.IsNullOrEmpty(text)) return 0f;
            return measure.GetPreferredWidth(text, label.GetGenerationSettings(Vector2.zero)) / Mathf.Max(1e-4f, label.pixelsPerUnit);
        }

        // ───── Pictures ─────

        /// <summary>Opaque at its centre and fading evenly to nothing at its edge: stretched over a box it is the mock-up's
        /// radial-gradient(closest-side) backing behind a subtitle (tint it).</summary>
        public static Sprite SoftOval => softOval ? softOval : softOval = Draw("V5Story soft oval", 64, 64, Vector4.zero, 1, false,
            (x, y) => new Color(1f, 1f, 1f, Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), new Vector2(32f, 32f)) / 32f)));

        /// <summary>The speaker's slanted tag (the mock-up's clip-path: the top runs from 6 pixels in to the right edge, the foot from the
        /// left edge to 6 short of the right), sliced at the slant so the slant keeps its width at any size (tint it).</summary>
        public static Sprite Tag => tag ? tag : tag = Draw("V5Story tag", TagWidth, TagHeight, new Vector4(TagSlant, 0f, TagSlant, 0f), 4, false,
            (x, y) =>
            {
                float rise = y / TagHeight;
                bool inside = x >= TagSlant * rise && x <= TagWidth - TagSlant + TagSlant * rise;
                return new Color(1f, 1f, 1f, inside ? 1f : 0f);
            });

        /// <summary>A bar's fill from one colour at its top to another at its foot, drawn whole (for a filled image).</summary>
        public static Sprite Fill(Color top, Color bottom)
        {
            long key = (long)Pack(top) << 32 | Pack(bottom);
            if (fills.TryGetValue(key, out var made) && made) return made;
            return fills[key] = Draw("V5Story fill", 4, 32, Vector4.zero, 1, false, (x, y) => Color.Lerp(bottom, top, Mathf.Clamp01((y - .5f) / 31f)));
        }

        static uint Pack(Color colour)
        {
            Color32 c = colour;
            return (uint)c.r << 24 | (uint)c.g << 16 | (uint)c.b << 8 | c.a;
        }

        /// <summary>The ship's engine in whitebox: a turbine's ring round its hub, light grey.</summary>
        public static Sprite EngineIcon => engine ? engine : engine = Icon("V5Story engine", V5TitleKit.Rgb(201, 206, 216), p =>
        {
            float r = p.magnitude;
            return r <= .26f || r >= .5f && r <= .8f;
        });

        /// <summary>The ship's armour in whitebox: a shield-shaped plate, steel grey.</summary>
        public static Sprite ArmorIcon => armor ? armor : armor = Icon("V5Story armor", V5TitleKit.Rgb(163, 172, 190), p =>
            p.y <= .72f && p.y >= -.88f && Mathf.Abs(p.x) <= (p.y >= -.05f ? .68f : .68f * (p.y + .88f) / .83f));

        /// <summary>The ship's fuel in whitebox: a capsule of fuel in gold.</summary>
        public static Sprite FuelIcon => fuel ? fuel : fuel = Icon("V5Story fuel", V5TitleKit.Gold, p =>
            Vector2.Distance(p, new Vector2(0f, Mathf.Clamp(p.y, -.42f, .42f))) <= .36f);

        /// <summary>A mining lamp in whitebox: its yellow light (the whitebox lamp's side view is a thin post, unreadable this small).</summary>
        public static Sprite LampIcon => lamp ? lamp : lamp = Icon("V5Story lamp", V5TitleKit.Rgb(255, 230, 140), p => p.magnitude <= .62f);

        /// <summary>A flat shape in one colour on a 64-pixel square: inside answers for points from -1 to 1 across, y up.</summary>
        static Sprite Icon(string name, Color colour, Func<Vector2, bool> inside) =>
            Draw(name, 64, 64, Vector4.zero, 4, true, (x, y) => inside(new Vector2(x / 32f - 1f, y / 32f - 1f)) ? colour : new Color(colour.r, colour.g, colour.b, 0f));

        /// <summary>A picture drawn from a colour at each point (pixels from the bottom-left corner), samples x samples points a pixel;
        /// with mips it keeps smaller copies for the small sizes it is shown at.</summary>
        static Sprite Draw(string name, int width, int height, Vector4 border, int samples, bool mips, Func<float, float, Color> colourAt)
        {
            var pixels = new Color32[width * height];
            float share = 1f / (samples * samples);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var sum = Color.clear;
                    for (int sy = 0; sy < samples; sy++)
                        for (int sx = 0; sx < samples; sx++)
                            sum += colourAt(x + (sx + .5f) / samples, y + (sy + .5f) / samples);
                    pixels[y * width + x] = sum * share;
                }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mips)
            {
                name = name, wrapMode = TextureWrapMode.Clamp, filterMode = mips ? FilterMode.Trilinear : FilterMode.Bilinear, hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(mips, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, border);
            sprite.name = name;
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }
    }
}
