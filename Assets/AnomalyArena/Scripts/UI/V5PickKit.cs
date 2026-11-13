using System;
using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// What the pick screens of the fifth interface pass are drawn with (work/art-plans/style-1112/ui-e/crate.html): the hull-plate banner
    /// with its painted heading, the line under it, the cards on the crate lid's frame (V5PickCard, V5PickFrame), the tier and rarity chips
    /// (V5PickChip), the orange go button and the row of key hints (V5PickHints). The story's weapon crate (V5PickCrate) and the open
    /// mode's starting weapon, armory, card and level-up picks (V5PickScreen) are drawn with the same pieces, so they read as one set.
    /// Sizes are the mock-up's pixels times the canvas's scale: V5HudKit.HudCss on the battle canvas, V5HudKit.StoryCss on the story layer.
    /// Nothing here is built on the English Alpha page (V5HudKit.On is false there).
    /// </summary>
    public static class V5PickKit
    {
        /// <summary>The crate lid's card frame (the crate-card picture, 419 x 535): where its dark inside begins, as shares of its size.</summary>
        public const float FrameLeft = 52f / 419f, FrameRight = 54f / 419f, FrameTop = 45f / 535f, FrameBottom = 47f / 535f;
        /// <summary>Width over height of the card frame.</summary>
        public const float FrameAspect = 419f / 535f;
        /// <summary>The banner's painted ends (the hazard stripes), as a share of its width; its words stay between them.</summary>
        public const float BannerEnds = 96f / 1150f;
        /// <summary>Width over height of the banner picture.</summary>
        public const float BannerAspect = 1150f / 213f;

        /// <summary>The mock-up's row behind "攻击方式 ???": a dark fill and a faint hairline.</summary>
        public static readonly Color RowFill = V5TitleKit.Rgb(5, 8, 16, .65f), RowEdge = V5TitleKit.Rgb(150, 160, 180, .18f);
        /// <summary>Whitebox's backdrop behind a pick screen: flat and dark (the art's veil is a gradient).</summary>
        public static readonly Color FlatVeil = new(0f, .01f, .03f, .88f);
        /// <summary>The weapon name a crate card would replace, in the mock-up's orange-hi, as rich text's hex.</summary>
        public static string SwapHex => "#" + ColorUtility.ToHtmlStringRGB(V5TitleKit.OrangeHi);

        static Sprite veil, cardGlow, artGlow;

        /// <summary>The mock-up's veil over the scene behind a pick screen: deep navy at the edges, a little lighter and warmer in the middle.</summary>
        public static Sprite Veil
        {
            get
            {
                if (veil) return veil;
                const int w = 96, h = 54;
                var pixels = new Color32[w * h];
                Color centre = V5TitleKit.Rgb(24, 30, 64, .82f), edge = V5TitleKit.Rgb(8, 9, 22, .95f), warm = V5TitleKit.Rgb(255, 200, 140, 1f);
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        // Rows count up from the bottom; the mock-up's ellipses are measured from the top.
                        float u = (x + .5f) / w - .5f, v = .5f - (y + .5f) / h;
                        float r = Mathf.Sqrt(u * u + v * v * .64f) / .62f;
                        var colour = Color.Lerp(centre, edge, Mathf.Clamp01(r / .72f));
                        float wu = u / .6f, wv = (v - .06f) / .5f;
                        float glow = Mathf.Clamp01(1f - Mathf.Sqrt(wu * wu + wv * wv) / .7f) * .10f;
                        colour = new Color(Mathf.Lerp(colour.r, warm.r, glow), Mathf.Lerp(colour.g, warm.g, glow), Mathf.Lerp(colour.b, warm.b, glow), colour.a);
                        pixels[y * w + x] = colour;
                    }
                var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "V5Pick veil", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                veil = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect);
                veil.name = "V5Pick veil";
                return veil;
            }
        }

        /// <summary>The menu's soft glow round a card (Resources/V5/UI/ui-card-glow, sliced): brightest 20 units inside its edge at the
        /// density CardGlowDensity. Null when the picture is missing.</summary>
        public static Sprite CardGlow
        {
            get
            {
                if (cardGlow) return cardGlow;
                var texture = Resources.Load<Texture2D>("V5/UI/ui-card-glow");
                if (!texture) return null;
                cardGlow = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(.5f, .5f), 100f, 0, SpriteMeshType.FullRect, Vector4.one * 112f);
                cardGlow.name = "V5Pick card glow";
                return cardGlow;
            }
        }

        /// <summary>The density the card glow is drawn at, and how far it reaches outside the card so its brightest line hugs the edge.</summary>
        public const float CardGlowDensity = 2f, CardGlowOutset = 22f;

        /// <summary>The mock-up's warm light behind a card's picture (an ellipse that fades out).</summary>
        public static Sprite ArtGlow => artGlow ? artGlow : artGlow = V5HudKit.Glow;

        /// <summary>A whole number of canvas units for a mock-up size in pixels on a canvas of this scale.</summary>
        public static int Px(float px, float css) => Mathf.Max(6, Mathf.RoundToInt(px * css));

        /// <summary>A one-line label (it never wraps) in the battle screens' font, its size in mock-up pixels.</summary>
        public static Text Label(Transform parent, string name, float px, float css, Color colour, TextAnchor anchor, bool bold = false)
        {
            var text = V5TitleKit.Label(parent, name, Px(px, css), colour, anchor, bold);
            text.font = V5BattleKit.TextFont;
            return text;
        }

        /// <summary>Left and top from the parent's top-left corner; written only when it changes (a moved rect makes the canvas rebuild).</summary>
        public static void Place(RectTransform rect, float left, float top, float width, float height)
        {
            if (!rect) return;
            var corner = new Vector2(0f, 1f);
            if (rect.anchorMin != corner) rect.anchorMin = corner;
            if (rect.anchorMax != corner) rect.anchorMax = corner;
            if (rect.pivot != corner) rect.pivot = corner;
            var at = new Vector2(left, -top);
            if (rect.anchoredPosition != at) rect.anchoredPosition = at;
            var size = new Vector2(width, height);
            if (rect.sizeDelta != size) rect.sizeDelta = size;
        }

        public static void Place(Component part, float left, float top, float width, float height)
        {
            if (part) Place(part.transform as RectTransform, left, top, width, height);
        }

        public static void Show(Component part, bool shown)
        {
            if (part && part.gameObject.activeSelf != shown) part.gameObject.SetActive(shown);
        }

        public static void SetText(Text label, string text)
        {
            text ??= "";
            if (label && label.text != text) label.text = text;
        }

        /// <summary>The banner: the hull-plate picture with the art on, a flat plate with the thin edge in whitebox (V5StoryBannerFrame).</summary>
        public static Image Banner(Transform parent, string name)
        {
            var image = V5TitleKit.Picture(parent, name, null, Color.white);
            image.gameObject.AddComponent<V5StoryBannerFrame>();
            return image;
        }

        /// <summary>A heading in the banner: the mock-up spaces Chinese headings widely; the font has no letter spacing, so a space goes
        /// between the letters. A heading with numbers in it (a count) is left as it is.</summary>
        public static string BannerWords(string words)
        {
            if (string.IsNullOrEmpty(words) || !ArenaText.IsChinese) return words ?? "";
            foreach (char c in words)
                if (c < 128 && c != ' ') return words;
            return V5TitleKit.Spaced(words.Replace(" ", ""));
        }

        /// <summary>The tier chip's words and colour: "MK I" on the pale chip, "MK II" and up on steel (as the dock's chips).</summary>
        public static string Tier(int mk) => "MK " + V5BattleKit.Roman(Mathf.Max(1, mk));

        public static Color TierColour(int mk) => mk <= 1 ? V5BattleKit.TierOne : V5TitleKit.Steel;

        /// <summary>A rarity's colour on a dark screen: void's own colour is black, so a void card glows violet (as the menu's cards did).</summary>
        public static Color RarityColour(CardRarity rarity) => rarity == CardRarity.Void ? V5MenuSkin.VoidGlow : ArenaRarity.ColorFor(rarity);

        /// <summary>How strongly a card glows in its rarity's colour, by ArenaRarity.Rank (white cards do not glow).</summary>
        public static float RarityGlow(CardRarity rarity) => ArenaRarity.Rank(rarity) switch { 0 => 0f, 1 => .45f, 2 => .55f, 3 => .65f, 4 => .75f, _ => .85f };

        /// <summary>The go button's hazard stripes at its left end, sized to a button of this height on a canvas of this scale.</summary>
        public static RawImage Hazard(Transform go, float css, float height)
        {
            var hazard = V5TitleKit.Raw(go, "V5Hazard", V5TitleKit.Stripes, V5TitleKit.Rgb(27, 14, 6, .55f));
            PlaceHazard(hazard, css, height);
            return hazard;
        }

        public static void PlaceHazard(RawImage hazard, float css, float height)
        {
            if (!hazard) return;
            float w = 40f * css, h = Mathf.Max(4f, height - 24f * css), repeat = 14f * 1.41421f * css;
            Place(hazard.rectTransform, 14f * css, 12f * css, w, h);
            hazard.uvRect = new Rect(0f, 0f, w / repeat, h / repeat);
        }

        /// <summary>The go button's words: dark on the orange plate, white on whitebox's flat button.</summary>
        public static Color GoInk(bool art) => art ? V5TitleKit.Dark : Color.white;

        /// <summary>Gives every pick screen's label the game's font once it arrives (the web build loads its Chinese letters late).</summary>
        public static void SyncFont(Text label) => V5BattleKit.Sync(label);

        /// <summary>Plays one of the interface's sounds (V5UiSound); nothing when it is missing.</summary>
        public static void Sound(V5UiSound.Ui sound, float pitch = 1f) => V5UiSound.Play(sound, pitch);

        /// <summary>Runs an action and logs, instead of throwing, whatever goes wrong in it: a fault while a pick screen is restyled must
        /// leave the screen working.</summary>
        public static bool Safely(Action action)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception fault)
            {
                Debug.LogException(fault);
                return false;
            }
        }
    }
}
