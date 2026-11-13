using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>A round medal on the battle screens (V5HudKit.Medal): with the art on, the machined ring and the painted icon inside it; in
    /// whitebox, a flat disc with the thin edge and the whitebox icon. It may carry a coloured ring inside the rim (the mock-up's ring-teal,
    /// ring-orange, ring-gold) and, with the art on, a glow behind it (glow-amber, glow-violet).</summary>
    public sealed class V5HudMedal : V5HudPiece
    {
        float size = 32f;
        Sprite artIcon, flatIcon;
        Color ring = Color.clear, glow = Color.clear;
        Image halo, frame, hole, icon, line;
        Outline edge;

        public RectTransform Rect => (RectTransform)transform;
        public float Size => size;

        internal void Setup(float value, Sprite art, Sprite flat)
        {
            size = value;
            artIcon = art;
            flatIcon = flat;
            Redress();
        }

        public void SetIcons(Sprite art, Sprite flat)
        {
            if (artIcon == art && flatIcon == flat) return;
            artIcon = art;
            flatIcon = flat;
            Redress();
        }

        /// <summary>The coloured ring inside the rim (clear for none).</summary>
        public void SetRing(Color colour)
        {
            if (ring == colour) return;
            ring = colour;
            Redress();
        }

        /// <summary>The glow behind the medal with the art on (clear for none).</summary>
        public void SetGlow(Color colour)
        {
            if (glow == colour) return;
            glow = colour;
            Redress();
        }

        public void SetSize(float value)
        {
            if (Mathf.Approximately(size, value)) return;
            size = value;
            Redress();
        }

        void Build()
        {
            if (frame) return;
            halo = V5TitleKit.Picture(transform, "Glow", V5HudKit.Glow, Color.clear);
            frame = V5TitleKit.Picture(transform, "Ring", null, Color.white);
            hole = V5TitleKit.Picture(transform, "Hole", V5TitleKit.Piece("disc"), Color.white);
            hole.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            icon = V5TitleKit.Picture(hole.transform, "Icon", null, Color.white);
            V5TitleKit.Stretch(icon.rectTransform);
            icon.preserveAspect = true;
            line = V5TitleKit.Picture(transform, "Line", V5HudKit.RingLine, Color.clear);
        }

        protected override void Dress(bool art)
        {
            Build();
            Rect.sizeDelta = Vector2.one * size;
            V5TitleKit.Centre(frame.rectTransform, Vector2.one * size * (art ? V5HudKit.MedalPicture : 1f));
            V5TitleKit.Dress(frame, V5TitleKit.Piece(art ? "medal" : "disc"), art ? Color.white : WhiteboxSkin.ButtonFill);
            if (!art && !edge)
            {
                edge = frame.gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectColor = WhiteboxSkin.Edge;
                edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (edge) edge.enabled = !art;
            var shown = art ? artIcon : flatIcon;
            bool hasIcon = shown != null;
            if (hole.gameObject.activeSelf != hasIcon) hole.gameObject.SetActive(hasIcon);
            V5TitleKit.Centre(hole.rectTransform, Vector2.one * size * .72f);
            icon.sprite = shown;
            bool ringed = ring.a > 0f;
            if (line.gameObject.activeSelf != ringed) line.gameObject.SetActive(ringed);
            V5TitleKit.Centre(line.rectTransform, Vector2.one * size * (58f / 64f));
            line.color = ring;
            bool glowing = art && glow.a > 0f;
            if (halo.gameObject.activeSelf != glowing) halo.gameObject.SetActive(glowing);
            V5TitleKit.Centre(halo.rectTransform, Vector2.one * size * 1.7f);
            halo.color = glow;
        }
    }
}
