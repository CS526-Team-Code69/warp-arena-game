using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>The crate lid's card frame on the pick screens (V5PickKit): with the art on, the crate-card picture (its dark inside is part
    /// of it); in whitebox, a flat card with the thin edge, the edge in the card's colour, orange while it is the chosen card. A frame that
    /// cannot be taken is darker.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class V5PickFrame : V5HudPiece
    {
        static readonly Color DimArt = new(.58f, .6f, .66f, 1f), FlatHover = new(.1f, .15f, .23f, 1f);

        Color edgeColour = WhiteboxSkin.Edge;
        bool chosen, hovered, dim;
        Outline edge;

        public Image Image => GetComponent<Image>();

        /// <summary>The whitebox edge's colour when the card is not chosen (a card's rarity, or the plain edge).</summary>
        public void SetEdge(Color colour)
        {
            if (edgeColour == colour) return;
            edgeColour = colour;
            Redress();
        }

        public void SetState(bool isChosen, bool isHovered, bool isDim)
        {
            if (chosen == isChosen && hovered == isHovered && dim == isDim) return;
            chosen = isChosen;
            hovered = isHovered;
            dim = isDim;
            Redress();
        }

        protected override void Dress(bool art)
        {
            var image = Image;
            if (art) V5TitleKit.Dress(image, V5TitleKit.Piece("crate-card"), dim ? DimArt : Color.white);
            else V5TitleKit.Dress(image, null, dim ? WhiteboxSkin.PlateFill : hovered || chosen ? FlatHover : WhiteboxSkin.CardFill);
            if (!art && !edge)
            {
                edge = gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (!edge) return;
            edge.enabled = !art;
            var colour = chosen ? V5TitleKit.Orange : edgeColour;
            if (dim) colour.a *= .5f;
            edge.effectColor = colour;
        }
    }
}
