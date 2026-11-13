using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>A bar on the story layer (the base's life in the day plate): a track, a fill from the left, a second fill along its upper
    /// part (the base's shield) and dark notches at every tenth (the mock-up's segmented bar). With the art on the track is the mock-up's
    /// bar picture and the fills are gradients; in whitebox the track is flat with a hairline and the fills are flat.</summary>
    public sealed class V5StoryBar : V5HudPiece
    {
        Image track, fill, extra;
        Color top = Color.white, bottom = Color.white, extraColour = Color.white;
        Outline edge;

        public RectTransform Rect => (RectTransform)transform;

        /// <summary>A bar filled from top to bottom colour, its second fill in extra's colour, notched into notches parts.</summary>
        public static V5StoryBar Create(Transform parent, string name, Color top, Color bottom, Color extra, int notches)
        {
            var rect = V5TitleKit.Rect(parent, name);
            var bar = rect.gameObject.AddComponent<V5StoryBar>();
            bar.Build(top, bottom, extra, notches);
            return bar;
        }

        void Build(Color topColour, Color bottomColour, Color extraTint, int notches)
        {
            top = topColour;
            bottom = bottomColour;
            extraColour = extraTint;
            track = V5TitleKit.Picture(transform, "Track", null, Color.white);
            V5TitleKit.Stretch(track.rectTransform);
            fill = V5TitleKit.Picture(transform, "Fill", null, Color.white);
            V5TitleKit.Stretch(fill.rectTransform, 1f);
            extra = V5TitleKit.Picture(transform, "Shield", null, Color.white);
            var band = extra.rectTransform;
            band.anchorMin = new Vector2(0f, .55f);
            band.anchorMax = Vector2.one;
            band.offsetMin = new Vector2(1f, 0f);
            band.offsetMax = new Vector2(-1f, -1f);
            extra.enabled = false;
            for (int i = 1; i < notches; i++)
            {
                var notch = V5TitleKit.Picture(transform, "Notch" + i, null, V5HudKit.Track).rectTransform;
                notch.anchorMin = new Vector2(i / (float)notches, 0f);
                notch.anchorMax = new Vector2(i / (float)notches, 1f);
                notch.pivot = new Vector2(1f, .5f);
                notch.anchoredPosition = Vector2.zero;
                notch.sizeDelta = new Vector2(V5StoryKit.Px(2f), 0f);
            }
            Redress();
        }

        /// <summary>How full the bar is and how full its second fill is (0 hides that one), each 0 to 1.</summary>
        public void Set(float value, float extraValue)
        {
            if (!fill) return;
            fill.fillAmount = Mathf.Clamp01(value);
            extra.fillAmount = Mathf.Clamp01(extraValue);
            bool shown = extraValue > 0f;
            if (extra.enabled != shown) extra.enabled = shown;
        }

        protected override void Dress(bool art)
        {
            if (!track) return;
            var picture = art ? V5TitleKit.Piece("bar-track") : null;
            V5TitleKit.Dress(track, picture, picture ? Color.white : V5HudKit.Track, V5HudKit.PieceDensity(V5StoryKit.Css));
            if (!picture && !edge)
            {
                edge = track.gameObject.AddComponent<Outline>();
                edge.useGraphicAlpha = false;
                edge.effectColor = V5HudKit.Hairline;
                edge.effectDistance = new Vector2(1f, -1f);
            }
            if (edge) edge.enabled = !picture;
            Paint(fill, art ? V5StoryKit.Fill(top, bottom) : WhiteboxSkin.Square, art ? Color.white : Color.Lerp(top, bottom, .5f));
            Paint(extra, art ? V5StoryKit.Fill(Color.Lerp(extraColour, Color.white, .3f), extraColour) : WhiteboxSkin.Square, art ? Color.white : extraColour);
        }

        static void Paint(Image image, Sprite sprite, Color colour)
        {
            if (image.sprite != sprite) image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            if (image.color != colour) image.color = colour;
        }
    }
}
