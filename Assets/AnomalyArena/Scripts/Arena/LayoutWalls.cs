using UnityEngine;

namespace AnomalyArena
{

    public static class LayoutWalls
    {

        public static int Fill(Transform group, MapLayout layout)
        {
            if (!group || !layout || layout.walls == null || group.childCount == 0) return 0;
            int existing = group.childCount, added = 0;
            for (int i = layout.walls.Length; i < existing; i++) group.GetChild(i).gameObject.SetActive(false);
            for (int i = existing; i < layout.walls.Length; i++)
            {
                var rect = layout.walls[i];
                var template = ClosestInSize(group, existing, rect);
                var wall = Object.Instantiate(template.gameObject, group);
                wall.name = "Terrain_" + i.ToString("00");
                var t = wall.transform;
                t.localPosition = new Vector3(rect.center.x, template.localPosition.y, rect.center.y);
                t.localRotation = template.localRotation;
                t.localScale = new Vector3(rect.width, template.localScale.y, rect.height);
                wall.SetActive(true);
                added++;
            }
            return added;
        }

        internal static Transform ClosestInSize(Transform group, int count, Rect rect)
        {
            Transform best = group.GetChild(0);
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var child = group.GetChild(i);
                Vector3 scale = child.localScale;
                if (scale.x <= 0f || scale.z <= 0f) continue;
                float score = Mathf.Abs(Mathf.Log(Mathf.Max(.01f, rect.width) / scale.x)) + Mathf.Abs(Mathf.Log(Mathf.Max(.01f, rect.height) / scale.z));
                if (score < bestScore) { bestScore = score; best = child; }
            }
            return best;
        }
    }
}
