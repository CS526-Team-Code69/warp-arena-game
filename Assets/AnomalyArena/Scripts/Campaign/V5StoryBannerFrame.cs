using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>The night banner's plate on the story layer: the mock-up's hull-plate picture with the art on; in whitebox (or without the
    /// picture) a flat plate with the thin edge.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class V5StoryBannerFrame : V5HudPiece
    {
        Outline edge;

        protected override void Dress(bool art)
        {
            var picture = art ? V5TitleKit.Piece("banner") : null;
            V5TitleKit.Dress(GetComponent<Image>(), picture, picture ? Color.white : WhiteboxSkin.PlateFill);
            if (!picture && !edge)
            {
                edge = gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectColor = WhiteboxSkin.Edge;
                edge.effectDistance = new Vector2(WhiteboxSkin.EdgeWidth, -WhiteboxSkin.EdgeWidth);
            }
            if (edge) edge.enabled = !picture;
        }
    }
}
