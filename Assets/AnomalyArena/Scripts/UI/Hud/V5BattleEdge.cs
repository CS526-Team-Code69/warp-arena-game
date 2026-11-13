using UnityEngine;
using UnityEngine.UI;

namespace AnomalyArena
{
    /// <summary>
    /// A screen-edge marker of the fifth pass (work/art-plans/style-1112/ui-e/hud.html, .edge): a flat triangle that points off the screen
    /// toward what it marks, beside a small chamfered plate with its count or its name. V2EnemyDirections (the foes, an orange triangle and
    /// "×2") and V2WorldLabels (the black hole, the portal, the experience on the ground, each in its own colour) place the marker; it lays
    /// out its own parts around its middle. The plate follows the art switch (V5HudPlate); the triangle and the words look the same in both.
    /// </summary>
    public sealed class V5BattleEdge : MonoBehaviour
    {
        const float Css = V5BattleKit.Css;
        // The mock-up's marker (pixels): the triangle 22 x 26 when it points sideways, 6 from the plate; the plate's padding 6 / 10.
        const float ArrowLong = 26f, ArrowShort = 22f, ArrowGap = 6f, PadY = 6f, PadX = 10f;

        V5HudPlate plate;
        Text label;
        Image arrow;
        string shown;
        float shownSize;

        /// <summary>A marker centred on parent (hidden until Show).</summary>
        public static V5BattleEdge Create(Transform parent, string name)
        {
            var root = V5TitleKit.Rect(parent, name);
            V5TitleKit.Centre(root, Vector2.zero);
            var edge = root.gameObject.AddComponent<V5BattleEdge>();
            edge.Build();
            return edge;
        }

        void Build()
        {
            plate = V5HudKit.Plate(transform, "Plate", V5HudKit.PlateLook.Plain, 6f, Css);
            label = V5BattleKit.Label(transform, "Label", 19f, V5TitleKit.Ink, TextAnchor.MiddleCenter, true);
            arrow = V5HudKit.Arrow(transform, "Arrow", new Vector2(ArrowLong, ArrowShort) * Css, V5TitleKit.Orange);
            var shadow = arrow.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>Shows text on the plate and the triangle on the side of the plate that faces direction (screen space, any length),
        /// pointing that way.</summary>
        public void Show(string text, Color ink, Color arrowColour, Vector2 direction, float fontPx = 19f)
        {
            if (!plate) return;
            V5BattleKit.Sync(label);
            int size = V5BattleKit.Font(fontPx);
            if (label.fontSize != size) label.fontSize = size;
            if (label.text != text) label.text = text;
            label.color = ink;
            arrow.color = arrowColour;
            if (shown != text || !Mathf.Approximately(shownSize, size))
            {
                shown = text;
                shownSize = size;
                float width = V5BattleKit.Width(label) + 2f * PadX * Css, height = size + 2f * PadY * Css;
                V5TitleKit.Centre(plate.Rect, new Vector2(width, height));
                V5TitleKit.Centre(label.rectTransform, new Vector2(width, height));
            }
            var way = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector2.up;
            Vector2 half = plate.Rect.sizeDelta * .5f;
            // From the plate's middle along the way to its edge, then the gap and half the triangle's length.
            float toEdge = Mathf.Min(Mathf.Abs(way.x) > 1e-4f ? half.x / Mathf.Abs(way.x) : float.MaxValue, Mathf.Abs(way.y) > 1e-4f ? half.y / Mathf.Abs(way.y) : float.MaxValue);
            var rect = arrow.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = way * (toEdge + (ArrowGap + ArrowShort * .5f) * Css);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(way.y, way.x) * Mathf.Rad2Deg - 90f);
        }
    }
}
