using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>One of the fifth pass's chamfered plates on the battle screens (V5HudKit.Plate): with the art on, the mock-up's picture of
    /// its look, sliced at its corner cut; in whitebox, a flat fill with the thin edge, orange for a warm or selected plate. An empty plate is
    /// dimmed and carries faint stripes in both looks (under whatever the plate holds).</summary>
    [RequireComponent(typeof(Image))]
    public sealed class V5HudPlate : V5HudPiece
    {
        V5HudKit.PlateLook look;
        float cutPx = 16f, css = V5HudKit.HudCss;
        Image image, stripeMask;
        RawImage stripes;
        Outline edge;
        Vector2 stripedSize;

        public Image Image => image ? image : image = GetComponent<Image>();
        public V5HudKit.PlateLook Look => look;
        public RectTransform Rect => (RectTransform)transform;

        internal void Setup(V5HudKit.PlateLook value, float cut, float scale)
        {
            look = value;
            cutPx = cut;
            css = scale;
            Redress();
        }

        public void SetLook(V5HudKit.PlateLook value)
        {
            if (look == value) return;
            look = value;
            Redress();
        }

        protected override void Dress(bool art)
        {
            bool empty = look == V5HudKit.PlateLook.Empty, hot = look is V5HudKit.PlateLook.Warm or V5HudKit.PlateLook.Selected;
            if (art) V5TitleKit.Dress(Image, V5TitleKit.Piece(V5HudKit.PlatePiece(look)), empty ? V5HudKit.EmptyArt : Color.white, V5HudKit.PlateDensity(cutPx, css));
            else V5TitleKit.Dress(Image, null, empty ? V5HudKit.EmptyFlat : hot ? WhiteboxSkin.ButtonFill : WhiteboxSkin.PlateFill);
            if (!art && !edge)
            {
                edge = gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (edge)
            {
                edge.enabled = !art;
                edge.effectColor = hot ? V5TitleKit.Orange : empty ? V5HudKit.EmptyEdge : WhiteboxSkin.Edge;
            }
            if (empty && !stripeMask) BuildStripes();
            if (!stripeMask) return;
            if (stripeMask.gameObject.activeSelf != empty) stripeMask.gameObject.SetActive(empty);
            if (!empty) return;
            V5TitleKit.Dress(stripeMask, V5TitleKit.Chamfer, Color.white, art ? V5HudKit.ChamferDensity(cutPx, css) : 0f);
            if (!art) stripeMask.sprite = null;
            FitStripes();
        }

        void BuildStripes()
        {
            stripeMask = V5TitleKit.Picture(transform, "Stripes", V5TitleKit.Chamfer, Color.white);
            stripeMask.transform.SetAsFirstSibling();
            V5TitleKit.Stretch(stripeMask.rectTransform);
            stripeMask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            stripes = V5TitleKit.Raw(stripeMask.transform, "Lines", V5TitleKit.Stripes, V5HudKit.StripeTint);
            V5TitleKit.Stretch(stripes.rectTransform);
        }

        void FitStripes()
        {
            if (!stripes) return;
            var size = Rect.rect.size;
            stripedSize = size;
            float repeat = V5HudKit.StripeRepeat(css);
            stripes.uvRect = new Rect(0f, 0f, size.x / repeat, size.y / repeat);
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();
            if (look == V5HudKit.PlateLook.Empty && stripes && Rect.rect.size != stripedSize) FitStripes();
        }
    }
}
