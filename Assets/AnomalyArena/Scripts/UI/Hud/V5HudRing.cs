using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>The gauge ring round the portrait and the day dial (V5HudKit.Ring): the mock-up's machined ring with the art on, a thin
    /// circle in the whitebox edge's colour in whitebox.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class V5HudRing : V5HudPiece
    {
        protected override void Dress(bool art) =>
            V5TitleKit.Dress(GetComponent<Image>(), art ? V5TitleKit.Piece("gauge-ring") : V5HudKit.RingLine, art ? Color.white : WhiteboxSkin.Edge);
    }
}
