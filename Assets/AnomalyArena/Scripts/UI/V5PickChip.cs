using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>A chip on a pick card (V5PickKit): the tier ("MK I" on the pale chip, "MK II" and up on steel) or the card's rarity in its
    /// colour, in dark bold letters. With the art on it is the mock-up's chip picture tinted; in whitebox a flat block of the same colour.
    /// It is as wide as its words need.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class V5PickChip : V5HudPiece
    {
        float css = V5HudKit.HudCss, heightPx = 23f, fontPx = 15f, padPx = 10f;
        Color fill = V5BattleKit.TierOne, ink = V5BattleKit.TierInk;
        Text label;
        Font shownFont;

        public RectTransform Rect => (RectTransform)transform;
        public float Width => Rect.sizeDelta.x;
        public float Height => Rect.sizeDelta.y;

        /// <summary>A chip heightPx mock-up pixels tall with fontPx-pixel words, on a canvas of this scale.</summary>
        public static V5PickChip Make(Transform parent, string name, float css, float heightPx = 23f, float fontPx = 15f)
        {
            var image = V5TitleKit.Picture(parent, name, null, Color.white);
            var chip = image.gameObject.AddComponent<V5PickChip>();
            chip.css = css;
            chip.heightPx = heightPx;
            chip.fontPx = fontPx;
            chip.Redress();
            return chip;
        }

        void Build()
        {
            if (label) return;
            label = V5PickKit.Label(transform, "Label", fontPx, css, ink, TextAnchor.MiddleCenter, true);
            V5TitleKit.Stretch(label.rectTransform);
        }

        public void Set(string text, Color fillColour, Color inkColour)
        {
            Build();
            text ??= "";
            if (label.text == text && fill == fillColour && ink == inkColour) return;
            label.text = text;
            fill = fillColour;
            ink = inkColour;
            Redress();
        }

        protected override void Dress(bool art)
        {
            Build();
            V5TitleKit.Dress(GetComponent<Image>(), art ? V5TitleKit.Piece("chip") : WhiteboxSkin.Square, fill, art ? V5HudKit.PieceDensity(css) : 0f);
            label.color = ink;
            label.font = shownFont = V5BattleKit.TextFont;
            label.fontSize = V5PickKit.Px(fontPx, css);
            Rect.sizeDelta = new Vector2(Mathf.Ceil(label.preferredWidth) + padPx * 2f * css, heightPx * css);
        }

        /// <summary>The game's font can arrive after the chip was dressed: the words take it and the chip fits them again.</summary>
        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (label && V5BattleKit.TextFont != shownFont) Redress();
        }
    }
}
