using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>A keycap with its key's name on the battle screens (V5HudKit.Key): with the art on, the mock-up's light or dark cap; in
    /// whitebox, a flat button with the thin edge. It is as tall as asked and as wide as its name needs (never narrower than it is tall).</summary>
    [RequireComponent(typeof(Image))]
    public sealed class V5HudKey : V5HudPiece
    {
        bool dark;
        float css = V5HudKit.HudCss, heightPx = 28f, fontPx = 15f;
        Image cap;
        Text label;
        Outline edge;
        // The font the name was last given (V5TitleKit.Font then).
        Font shownFont;

        public RectTransform Rect => (RectTransform)transform;
        public Text Label
        {
            get
            {
                Build();
                return label;
            }
        }

        internal void Setup(string text, bool isDark, float scale, float height, float font)
        {
            dark = isDark;
            css = scale;
            heightPx = height;
            fontPx = font;
            Build();
            label.text = text;
            Redress();
        }

        public void SetText(string text)
        {
            Build();
            if (label.text == text) return;
            label.text = text;
            Fit();
        }

        public void SetDark(bool value)
        {
            if (dark == value) return;
            dark = value;
            Redress();
        }

        void Build()
        {
            if (label) return;
            cap = GetComponent<Image>();
            label = V5TitleKit.Label(transform, "Label", Mathf.Max(8, Mathf.RoundToInt(fontPx * css)), V5HudKit.KeyInk, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(label.rectTransform);
        }

        void Fit()
        {
            float height = heightPx * css, pad = 7f * css;
            label.fontSize = Mathf.Max(8, Mathf.RoundToInt(fontPx * css));
            Rect.sizeDelta = new Vector2(Mathf.Max(height, label.preferredWidth + pad * 2f), height);
        }

        protected override void Dress(bool art)
        {
            Build();
            V5TitleKit.Dress(cap, art ? V5TitleKit.Piece(dark ? "keycap-dark" : "keycap") : null, art ? Color.white : WhiteboxSkin.ButtonFill,
                V5HudKit.PieceDensity(css));
            label.font = shownFont = V5TitleKit.Font;
            label.color = art && !dark ? V5HudKit.KeyInk : V5TitleKit.Ink;
            if (!art && !edge)
            {
                edge = gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectColor = WhiteboxSkin.Edge;
                edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (edge) edge.enabled = !art;
            Fit();
        }

        /// <summary>The game's font can arrive after the cap was dressed (the web build loads its Chinese letters later), and the name
        /// would stay in the built-in font, which has none: when V5TitleKit.Font changes, the name takes it and the cap fits it again.
        /// Only a change is followed, so a font another screen gave the name on purpose (V2Tutorial) stays until then.</summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            var font = V5TitleKit.Font;
            if (!label || font == shownFont) return;
            shownFont = font;
            label.font = font;
            Fit();
        }
    }
}
