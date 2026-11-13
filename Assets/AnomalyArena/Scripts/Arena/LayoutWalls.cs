using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Keeps a map's wall objects in step with its layout. The scene saves one wall object per layout rect, in order (collider on the Wall layer
    /// plus artwork). A saved wall whose rect moved in the layout since (the walls taken out of the black holes' wider pull ring) is moved and
    /// scaled onto it; rects the layout gained later, such as the L and ] walls of the Ruins (system doc 12.2), get a copy of the saved wall
    /// nearest in size, moved and scaled onto the rect. So collisions, bounces and the art match what paths and spawns read from the layout.
    /// Saved walls past the end of the layout are switched off.
    /// </summary>
    public static class LayoutWalls
    {
        /// <summary>Puts every saved wall of the group onto its rect and adds the missing wall objects of the layout; returns how many were
        /// added. Calling it again adds nothing.</summary>
        public static int Fill(Transform group, MapLayout layout)
        {
            if (!group || !layout || layout.walls == null || group.childCount == 0) return 0;
            int existing = group.childCount, added = 0;
            for (int i = layout.walls.Length; i < existing; i++) group.GetChild(i).gameObject.SetActive(false);
            for (int i = 0; i < Mathf.Min(existing, layout.walls.Length); i++)
            {
                var rect = layout.walls[i];
                var t = group.GetChild(i);
                t.localPosition = new Vector3(rect.center.x, t.localPosition.y, rect.center.y);
                t.localScale = new Vector3(rect.width, t.localScale.y, rect.height);
            }
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

        /// <summary>
        /// V2 day campaign (since 2026-11-06): a new wall group beside templates (the map's saved wall group) holding one wall object per
        /// rect of layout, each a copy of the first templateCount saved walls nearest it in size, moved and scaled onto its rect as Fill
        /// places the walls a layout gained. The group's name is the template group's with name added, so the stone style that follows
        /// from the group's name (V5TerrainWalls.StyleOfGroup) stays the same. Returns the group, or null without templates.
        /// </summary>
        public static Transform Build(Transform templates, int templateCount, MapLayout layout, string name)
        {
            if (!templates || !layout || templates.childCount == 0) return null;
            var group = new GameObject(templates.name + name).transform;
            group.SetParent(templates.parent, false);
            group.SetLocalPositionAndRotation(templates.localPosition, templates.localRotation);
            group.localScale = templates.localScale;
            int count = Mathf.Clamp(templateCount, 1, templates.childCount);
            var walls = layout.walls ?? new Rect[0];
            for (int i = 0; i < walls.Length; i++)
            {
                var rect = walls[i];
                var template = ClosestInSize(templates, count, rect);
                var wall = Object.Instantiate(template.gameObject, group);
                wall.name = "CampaignTerrain_" + i.ToString("00");
                var t = wall.transform;
                t.localPosition = new Vector3(rect.center.x, template.localPosition.y, rect.center.y);
                t.localRotation = template.localRotation;
                t.localScale = new Vector3(rect.width, template.localScale.y, rect.height);
                wall.SetActive(true);
            }
            return group;
        }

        /// <summary>The saved wall whose footprint is closest to the rect in proportion, so its artwork stretches the least. TyrantTerrain
        /// copies it for the Tyrant's meteor fragments too.</summary>
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
