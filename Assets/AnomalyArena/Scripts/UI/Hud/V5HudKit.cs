using System;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// What the fifth interface pass draws the battle screens with (work/art-plans/style-1112/ui-e/hud.html and ui.css). The pictures and
    /// colours are the title's (V5TitleKit). Each piece here has the mock-up's two looks: with the art on, the gunmetal plates, the machined
    /// medal rings, the gauge ring and the keycaps; in whitebox, flat fills with the thin edge (WhiteboxSkin) and the whitebox icons. A piece
    /// dresses itself again whenever the art switch changes while it is shown (V5HudPiece). Sizes are the mock-up's pixels times the
    /// canvas's scale: HudCss on the battle canvas (V2Canvas, 1440 x 810), StoryCss on the story's own layer (V2CampaignUi, 1600 x 900).
    /// The English Alpha page keeps the battle screens it was graded with: On is false there and none of this is built.
    /// </summary>
    public static class V5HudKit
    {
        /// <summary>The battle screens of the fifth pass are drawn on every page but the English Alpha.</summary>
        public static bool On => !BuildEdition.IsAlpha;

        /// <summary>Canvas units per mock-up pixel on the battle canvas and on the story's own layer.</summary>
        public const float HudCss = 1440f / 1920f, StoryCss = 1600f / 1920f;

        /// <summary>The plates: plain (navy to indigo), teal (human systems), warm (the weapon in hand: warm fill, orange rim), selected
        /// (plain fill, orange rim) and empty (a slot with nothing in it: dimmed, with faint stripes).</summary>
        public enum PlateLook { Plain, Teal, Warm, Selected, Empty }

        // Colours ui.css has and the title's kit does not.
        public static readonly Color Violet = V5TitleKit.Rgb(145, 95, 255), VioletHi = V5TitleKit.Rgb(205, 174, 255), LifeText = V5TitleKit.Rgb(255, 107, 122),
            KeyInk = V5TitleKit.Rgb(26, 29, 36), Track = V5TitleKit.Rgb(10, 13, 21), Hairline = V5TitleKit.Rgb(163, 172, 190, .2f),
            StripeTint = V5TitleKit.Rgb(160, 170, 190, .16f), EmptyArt = new(1f, 1f, 1f, .72f), EmptyFlat = new(.03f, .05f, .09f, .72f),
            EmptyEdge = new(.3f, .55f, .85f, .35f);

        // The plates' corner cut in their files: 16 mock-up pixels drawn at twice the size. The chamfer mask's: 16 of its own pixels.
        const float PlateCutInFile = 32f, ChamferCutInFile = 16f;

        /// <summary>The medal's picture is 152 pixels round a 128-pixel medal: its shadow and outer rim reach past the medal's size.</summary>
        public const float MedalPicture = 152f / 128f;

        static Sprite ringLine, glow, triangle;

        /// <summary>The multiplier that draws a plate with its corners cut at cutPx mock-up pixels on a canvas of this scale.</summary>
        public static float PlateDensity(float cutPx, float css) => PlateCutInFile / (Mathf.Max(1f, cutPx) * css);

        /// <summary>The multiplier that cuts the chamfer mask (V5TitleKit.Chamfer) at cutPx mock-up pixels on a canvas of this scale.</summary>
        public static float ChamferDensity(float cutPx, float css) => ChamferCutInFile / (Mathf.Max(1f, cutPx) * css);

        /// <summary>The multiplier that draws a picture made at twice the mock-up's size (keycaps, chips, bars) at the mock-up's size.</summary>
        public static float PieceDensity(float css) => 2f / css;

        /// <summary>How far the empty plate's stripes repeat along a row: every 12 mock-up pixels across the stripe.</summary>
        public static float StripeRepeat(float css) => 12f * 1.41421f * css;

        public static string PlatePiece(PlateLook look) => look switch
        {
            PlateLook.Teal => "plate-teal",
            PlateLook.Warm => "plate-warm-selected",
            PlateLook.Selected => "plate-selected",
            _ => "plate",
        };

        // ───── Pieces ─────

        /// <summary>A chamfered plate (an Image that takes no clicks) with its corners cut at cutPx mock-up pixels.</summary>
        public static V5HudPlate Plate(Transform parent, string name, PlateLook look, float cutPx, float css)
        {
            var image = V5TitleKit.Picture(parent, name, null, Color.white);
            var plate = image.gameObject.AddComponent<V5HudPlate>();
            plate.Setup(look, cutPx, css);
            return plate;
        }

        /// <summary>A round medal size canvas units across, with the painted icon (art on) and the whitebox icon (whitebox).</summary>
        public static V5HudMedal Medal(Transform parent, string name, float size, Sprite artIcon = null, Sprite flatIcon = null)
        {
            var root = V5TitleKit.Rect(parent, name);
            root.sizeDelta = Vector2.one * size;
            var medal = root.gameObject.AddComponent<V5HudMedal>();
            medal.Setup(size, artIcon, flatIcon);
            return medal;
        }

        /// <summary>A keycap heightPx mock-up pixels tall with its key's name in fontPx-pixel letters; it is as wide as the name needs.</summary>
        public static V5HudKey Key(Transform parent, string name, string text, bool dark, float css, float heightPx = 28f, float fontPx = 15f)
        {
            var image = V5TitleKit.Picture(parent, name, null, Color.white);
            var key = image.gameObject.AddComponent<V5HudKey>();
            key.Setup(text, dark, css, heightPx, fontPx);
            return key;
        }

        /// <summary>The gauge ring, size canvas units across, centred in its parent.</summary>
        public static V5HudRing Ring(Transform parent, string name, float size)
        {
            var image = V5TitleKit.Picture(parent, name, null, Color.white);
            V5TitleKit.Centre(image.rectTransform, Vector2.one * size);
            var ring = image.gameObject.AddComponent<V5HudRing>();
            ring.Redress();
            return ring;
        }

        /// <summary>A thin ring that fills clockwise from the top (the day dial's time of day), size canvas units across, centred in its
        /// parent: set its fillAmount from 0 to 1. The same in both looks.</summary>
        public static Image Arc(Transform parent, string name, float size, Color colour)
        {
            var image = V5TitleKit.Picture(parent, name, RingLine, colour);
            V5TitleKit.Centre(image.rectTransform, Vector2.one * size);
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.fillAmount = 0f;
            return image;
        }

        /// <summary>A flat triangle that points up, turned by degrees (90 points left, -90 right, 180 down): pointers and the screen edge's
        /// arrows. The same in both looks.</summary>
        public static Image Arrow(Transform parent, string name, Vector2 size, Color colour, float degrees = 0f)
        {
            var image = V5TitleKit.Picture(parent, name, Triangle, colour);
            image.rectTransform.sizeDelta = size;
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, degrees);
            return image;
        }

        // ───── Generated pictures ─────

        /// <summary>A thin white circle, its stroke about 3 % of its width: the whitebox ring, a medal's coloured ring, the day's arc.</summary>
        public static Sprite RingLine => ringLine ? ringLine : ringLine = Radial("V5Hud ring line", 128, r => 2.5f - Mathf.Abs(r - 61f));

        /// <summary>A soft white glow that fades out towards its edge (tint it).</summary>
        public static Sprite Glow => glow ? glow : glow = Radial("V5Hud glow", 64, r => Mathf.Pow(Mathf.Clamp01(1f - r / 32f), 2f));

        /// <summary>A white triangle that points up, its edges smoothed.</summary>
        public static Sprite Triangle
        {
            get
            {
                if (triangle) return triangle;
                const int n = 64;
                // Counter-clockwise (rows count up from the bottom), so the inside is on the left of each edge.
                Vector2 top = new(n * .5f, n), left = new(0f, 0f), right = new(n, 0f);
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        var p = new Vector2(x + .5f, y + .5f);
                        float inside = Mathf.Min(LeftOf(p, top, left), Mathf.Min(LeftOf(p, left, right), LeftOf(p, right, top)));
                        pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(inside + .5f) * 255f));
                    }
                return triangle = Create("V5Hud triangle", n, pixels);
            }
        }

        // How far p is to the left of the line from a to b, in pixels.
        static float LeftOf(Vector2 p, Vector2 a, Vector2 b)
        {
            var along = (b - a).normalized;
            var ap = p - a;
            return along.x * ap.y - along.y * ap.x;
        }

        static Sprite Radial(string name, int n, Func<float, float> alphaAt)
        {
            var pixels = new Color32[n * n];
            float centre = n * .5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(centre, centre));
                    pixels[y * n + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAt(r)) * 255f));
                }
            return Create(name, n, pixels);
        }

        static Sprite Create(string name, int n, Color32[] pixels)
        {
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, n, n), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            return sprite;
        }
    }

    /// <summary>A piece of the fifth pass's battle screens: it dresses itself in the look the art switch asks for, and again whenever the
    /// switch changes while it is shown.</summary>
    public abstract class V5HudPiece : MonoBehaviour
    {
        bool? dressedArt;

        protected abstract void Dress(bool art);

        /// <summary>Dresses the piece now, in the look the art switch asks for (after its settings changed).</summary>
        public void Redress()
        {
            dressedArt = Art.Enabled;
            Dress(Art.Enabled);
        }

        protected virtual void OnEnable() => Redress();

        protected virtual void LateUpdate()
        {
            if (dressedArt != Art.Enabled) Redress();
        }
    }
}
