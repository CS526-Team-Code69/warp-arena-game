using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Where the base tour's text plate stands when the page's own parts are known (V2RectSpotlight with a list of them). Beside the
    /// highlighted part on the side with more room, as BaseTour.PlatePlace has it, the plate can lie over what the player needs next to
    /// it: the armor row beside the skill cards, the fortification and the wallet beside the rocket button. Here the plate is tried all
    /// over the screen, at the usual width and, when its words need more for one line, as wide as they need, and it goes where it lies
    /// over the least of the page's words, buttons and icons; between places that cover equally little, the one nearer to the usual place
    /// wins. It never lies over the highlighted part. With nothing in the way the usual place stays.
    /// All in canvas units from the screen's bottom-left corner.
    /// </summary>
    public static class TourPlateRoom
    {
        // The plate is tried every Coarse units across the screen, then every Fine units round the best of those; a canvas unit of
        // distance from the usual place weighs DistanceWeight against a square unit of something covered; a button covered weighs
        // ButtonWeight times its area.
        const float Coarse = 24f, Fine = 3f, DistanceWeight = .2f, ButtonWeight = 3f;

        /// <summary>The plate's place. heightAt gives its height at a width (its words wrap there) and oneLine the width its words take on
        /// one line, its padding included; busy are the page's parts it should keep off and buttons those of them that are buttons (both
        /// may be empty).</summary>
        public static Rect Place(Vector2 area, Rect hole, Func<float, float> heightAt, float oneLine, IReadOnlyList<Rect> busy,
            IReadOnlyList<Rect> buttons, float minWidth, float maxWidth, float gap, float margin)
        {
            var usualSide = BaseTour.PlateSide(area, hole, minWidth, gap, margin);
            float usualWidth = BaseTour.PlateWidth(usualSide, area, hole, minWidth, maxWidth, gap, margin);
            var usual = BaseTour.PlatePlace(usualSide, area, hole, new Vector2(usualWidth, heightAt(usualWidth)), gap, margin);
            Rect keepOff = Grow(hole, gap * .5f);
            float best = usual.Overlaps(keepOff) ? float.MaxValue : Cost(usual, busy, buttons);
            if (best <= 0f) return usual;
            Rect chosen = usual;
            float screen = Mathf.Max(0f, area.x - 2f * margin);
            float wide = Mathf.Min(Mathf.Max(oneLine, minWidth), screen), usualW = Mathf.Min(maxWidth, screen);
            for (int pick = 0; pick < 2; pick++)
            {
                float w = pick == 0 ? usualW : wide;
                if (pick == 1 && w <= usualW + .5f) continue;
                float h = heightAt(w);
                float xMax = area.x - margin - w, yMax = area.y - margin - h;
                if (xMax < margin || yMax < margin) continue;
                // Across the screen, then round the best place found.
                Rect found = default;
                float foundCost = float.MaxValue;
                for (float x = margin; x <= xMax + Coarse - .01f; x += Coarse)
                    for (float y = margin; y <= yMax + Coarse - .01f; y += Coarse)
                        Try(Mathf.Min(x, xMax), Mathf.Min(y, yMax), w, h, ref found, ref foundCost, keepOff, busy, buttons, usual.center);
                if (foundCost == float.MaxValue) continue;
                Vector2 around = found.position;
                for (float x = around.x - Coarse; x <= around.x + Coarse; x += Fine)
                    for (float y = around.y - Coarse; y <= around.y + Coarse; y += Fine)
                        Try(Mathf.Clamp(x, margin, xMax), Mathf.Clamp(y, margin, yMax), w, h, ref found, ref foundCost, keepOff, busy, buttons, usual.center);
                if (foundCost < best)
                {
                    best = foundCost;
                    chosen = found;
                }
            }
            return chosen;
        }

        static void Try(float x, float y, float w, float h, ref Rect found, ref float foundCost, Rect keepOff, IReadOnlyList<Rect> busy,
            IReadOnlyList<Rect> buttons, Vector2 usual)
        {
            var place = new Rect(x, y, w, h);
            if (place.Overlaps(keepOff)) return;
            float cost = DistanceWeight * (place.center - usual).magnitude;
            if (cost >= foundCost) return;
            cost += Cost(place, busy, buttons);
            if (cost >= foundCost) return;
            foundCost = cost;
            found = place;
        }

        /// <summary>How much of the page's parts a plate there covers (square units; a button counts ButtonWeight times).</summary>
        public static float Cost(Rect place, IReadOnlyList<Rect> busy, IReadOnlyList<Rect> buttons)
        {
            float covered = 0f;
            if (busy != null) foreach (var part in busy) covered += Overlap(place, part);
            if (buttons != null) foreach (var part in buttons) covered += ButtonWeight * Overlap(place, part);
            return covered;
        }

        static float Overlap(Rect a, Rect b)
        {
            float w = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin), h = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
            return w > 0f && h > 0f ? w * h : 0f;
        }

        static Rect Grow(Rect rect, float by) => Rect.MinMaxRect(rect.xMin - by, rect.yMin - by, rect.xMax + by, rect.yMax + by);
    }
}
