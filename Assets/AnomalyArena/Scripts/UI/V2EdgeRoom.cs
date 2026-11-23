using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The screen-edge markers (V2EnemyDirections for monsters, V2WorldLabels for places) and the story's goal plate at the top left. The
    /// markers keep the open mode's top inset in the story too, so those pointing up stand along the top edge where their direction
    /// leads; only a marker that would lie on the goal plate stands where its line from the screen's middle meets the plate's edge, just
    /// below it or just right of it. All in screen pixels.
    /// </summary>
    public static class V2EdgeRoom
    {
        /// <summary>Where a marker whose middle would be at (on the line from middle) stands: at itself, or, when its box (half: half its
        /// size) would come within gap of the goal plate, at the point where that line first meets the plate grown by half and gap.</summary>
        public static Vector2 ClearOfGoal(Vector2 middle, Vector2 at, Vector2 half, float gap)
        {
            if (V2CampaignUi.GoalPlateOnScreen() is not Rect plate) return at;
            return ClearOf(middle, at, Rect.MinMaxRect(plate.xMin - half.x - gap, plate.yMin - half.y - gap, plate.xMax + half.x + gap,
                plate.yMax + half.y + gap));
        }

        /// <summary>The first point of the line from middle to at that lies in keepOut, or at when the line does not reach it (or middle
        /// lies in it already).</summary>
        public static Vector2 ClearOf(Vector2 middle, Vector2 at, Rect keepOut)
        {
            if (keepOut.Contains(middle)) return at;
            Vector2 way = at - middle;
            float enter = 0f, leave = 1f;
            for (int axis = 0; axis < 2; axis++)
            {
                float from = middle[axis], step = way[axis], low = axis == 0 ? keepOut.xMin : keepOut.yMin, high = axis == 0 ? keepOut.xMax : keepOut.yMax;
                if (Mathf.Abs(step) < 1e-5f)
                {
                    if (from < low || from > high) return at;
                    continue;
                }
                float a = (low - from) / step, b = (high - from) / step;
                if (a > b) (a, b) = (b, a);
                enter = Mathf.Max(enter, a);
                leave = Mathf.Min(leave, b);
                if (enter > leave) return at;
            }
            return middle + way * enter;
        }
    }
}
