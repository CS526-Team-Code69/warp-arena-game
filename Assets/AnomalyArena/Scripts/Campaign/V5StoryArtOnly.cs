using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>An image of the story layer shown only with the art on (the mining lamps beside the night banner and their amber glow):
    /// whitebox draws neither the picture nor the light.</summary>
    [RequireComponent(typeof(Image))]
    public sealed class V5StoryArtOnly : V5HudPiece
    {
        protected override void Dress(bool art)
        {
            var image = GetComponent<Image>();
            bool shown = art && image.sprite;
            if (image.enabled != shown) image.enabled = shown;
        }
    }
}
