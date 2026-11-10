using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The outline of a model where shots and bodies meet it (2026-11-10), for the walls of the V2 base and ship models (V2Campaign.Base.cs):
    /// where the model's triangles cross a few horizontal planes (the caller picks them round the shots' height), their convex hull, cut
    /// down to a few corners, and a prism over it for a convex MeshCollider. A long model whose outline is not convex (the ship: a narrow nose, wide wings at the
    /// back) is cut into slabs along x first, each with its own convex outline, so a hull does not bridge the gaps between its parts.
    /// Parts that lie wholly below or above those planes (a low skirt on the ground, a roof's overhang) do not widen an outline. Before,
    /// the base's wall was its whole 10 x 10 footprint, whose corners stood up to 2 cells clear of the rounded model, and the ship's a
    /// 14 x 6 box (monsters beside the base could not be hit).
    /// </summary>
    public static class BaseFootprint
    {
        /// <summary>Slabs overlap by this much (cells) so the outlines of two slabs leave no seam between them.</summary>
        const float SlabOverlap = .25f;

        /// <summary>The outlines of the model's meshes where they cross the given heights, as (x, z) points in space's local frame, one
        /// convex outline per slab along x (at least one), each with at most maxCorners corners. Empty when a mesh cannot be read (its
        /// Read/Write import setting is off) or too few points cross those heights; the caller then keeps a plain box.</summary>
        public static List<Vector2[]> Outlines(GameObject model, Transform space, IReadOnlyList<float> heights, int slabs, int maxCorners)
        {
            var cuts = new List<(Vector2 a, Vector2 b)>();
            if (!model || !space) return new List<Vector2[]>();
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = filter.sharedMesh;
                if (!mesh) continue;
                if (!mesh.isReadable) return new List<Vector2[]>();
                Matrix4x4 toSpace = space.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = toSpace.MultiplyPoint3x4(vertices[i]);
                AddCuts(vertices, mesh.triangles, heights, cuts);
            }
            return OutlinesOf(cuts, slabs, maxCorners);
        }

        /// <summary>The same outlines for a triangle list already in the wanted frame (y up).</summary>
        public static List<Vector2[]> Outlines(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, IReadOnlyList<float> heights, int slabs, int maxCorners)
        {
            var cuts = new List<(Vector2 a, Vector2 b)>();
            AddCuts(vertices, triangles, heights, cuts);
            return OutlinesOf(cuts, slabs, maxCorners);
        }

        /// <summary>Where each triangle crosses each height: a segment in (x, z) between the two of its edges that cross the plane.</summary>
        static void AddCuts(IReadOnlyList<Vector3> vertices, IReadOnlyList<int> triangles, IReadOnlyList<float> heights, List<(Vector2 a, Vector2 b)> into)
        {
            var ends = new List<Vector2>(3);
            for (int t = 0; t + 2 < triangles.Count; t += 3)
            {
                Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                for (int h = 0; h < heights.Count; h++)
                {
                    ends.Clear();
                    float height = heights[h];
                    Cross(a, b, height, ends);
                    Cross(b, c, height, ends);
                    Cross(c, a, height, ends);
                    if (ends.Count == 2) into.Add((ends[0], ends[1]));
                }
            }
        }

        /// <summary>Adds the (x, z) point where the edge from p to q crosses the plane y = height, if it does.</summary>
        static void Cross(Vector3 p, Vector3 q, float height, List<Vector2> into)
        {
            float dp = p.y - height, dq = q.y - height;
            if (dp >= 0f == dq >= 0f) return; // both ends on the same side: no crossing (and the two never divide by zero below)
            float t = dp / (dp - dq);
            into.Add(new Vector2(p.x + (q.x - p.x) * t, p.z + (q.z - p.z) * t));
        }

        /// <summary>One convex outline per slab along x: the hull of the cut segments clipped to the slab (slabs overlap by SlabOverlap).
        /// Clipping the segments, not just picking their ends, gives a slab its edges where one long face runs through several slabs.</summary>
        static List<Vector2[]> OutlinesOf(List<(Vector2 a, Vector2 b)> cuts, int slabs, int maxCorners)
        {
            var outlines = new List<Vector2[]>();
            if (cuts.Count < 2) return outlines;
            slabs = Mathf.Max(1, slabs);
            float min = float.MaxValue, max = float.MinValue;
            foreach (var (a, b) in cuts) { min = Mathf.Min(min, Mathf.Min(a.x, b.x)); max = Mathf.Max(max, Mathf.Max(a.x, b.x)); }
            float width = (max - min) / slabs;
            var points = new List<Vector2>(cuts.Count * 2);
            for (int s = 0; s < slabs; s++)
            {
                float from = slabs == 1 ? min : min + s * width - SlabOverlap, to = slabs == 1 ? max : min + (s + 1) * width + SlabOverlap;
                points.Clear();
                foreach (var (a, b) in cuts)
                    if (ClipX(a, b, from, to, out var p, out var q)) { points.Add(p); points.Add(q); }
                if (points.Count < 3) continue;
                var hull = ConvexHull(points);
                if (hull.Count >= 3) outlines.Add(Reduce(hull, maxCorners).ToArray());
            }
            return outlines;
        }

        /// <summary>The part of the segment from a to b with x between from and to; false when none of it is.</summary>
        static bool ClipX(Vector2 a, Vector2 b, float from, float to, out Vector2 p, out Vector2 q)
        {
            if (a.x > b.x) (a, b) = (b, a);
            p = a; q = b;
            if (b.x < from || a.x > to) return false;
            float run = b.x - a.x; // above 0 whenever a clip below is needed
            if (a.x < from) p = Vector2.Lerp(a, b, (from - a.x) / run);
            if (b.x > to) q = Vector2.Lerp(a, b, (to - a.x) / run);
            return true;
        }

        /// <summary>The convex hull of the points, counter-clockwise (x right, y up), without collinear points (Andrew's monotone chain).</summary>
        public static List<Vector2> ConvexHull(IReadOnlyList<Vector2> points)
        {
            var sorted = new List<Vector2>(points);
            sorted.Sort((u, v) => u.x != v.x ? u.x.CompareTo(v.x) : u.y.CompareTo(v.y));
            var hull = new List<Vector2>(sorted.Count + 1);
            foreach (var p in sorted)
            {
                while (hull.Count >= 2 && Turn(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            int lowerCount = hull.Count + 1;
            for (int i = sorted.Count - 2; i >= 0; i--)
            {
                var p = sorted[i];
                while (hull.Count >= lowerCount && Turn(hull[hull.Count - 2], hull[hull.Count - 1], p) <= 0f) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            if (hull.Count > 0) hull.RemoveAt(hull.Count - 1); // the first point again
            return hull;
        }

        /// <summary>Drops corners of a convex outline until at most maxCorners (at least 3) are left, each time the one whose removal loses
        /// the least area. Removing a corner of a convex outline cuts it off, so the outline only shrinks and stays convex: a wall made from
        /// it never stands outside the model.</summary>
        public static List<Vector2> Reduce(IReadOnlyList<Vector2> outline, int maxCorners)
        {
            var corners = new List<Vector2>(outline);
            maxCorners = Mathf.Max(3, maxCorners);
            while (corners.Count > maxCorners)
            {
                int n = corners.Count, drop = 0;
                float least = float.MaxValue;
                for (int i = 0; i < n; i++)
                {
                    float area = Mathf.Abs(Turn(corners[(i + n - 1) % n], corners[i], corners[(i + 1) % n]));
                    if (area < least) { least = area; drop = i; }
                }
                corners.RemoveAt(drop);
            }
            return corners;
        }

        /// <summary>The (x, z) rectangle round an outline.</summary>
        public static Rect Bounds(IReadOnlyList<Vector2> outline)
        {
            Vector2 min = outline[0], max = outline[0];
            for (int i = 1; i < outline.Count; i++) { min = Vector2.Min(min, outline[i]); max = Vector2.Max(max, outline[i]); }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>A closed prism over the outline, from y = bottom to y = top, in the outline's frame (x, z). For a convex MeshCollider,
        /// which builds its own hull from the vertices, so the triangles' winding does not matter.</summary>
        public static Mesh Prism(IReadOnlyList<Vector2> outline, float bottom, float top)
        {
            int n = outline.Count;
            var vertices = new Vector3[n * 2];
            for (int i = 0; i < n; i++)
            {
                vertices[i] = new Vector3(outline[i].x, bottom, outline[i].y);
                vertices[n + i] = new Vector3(outline[i].x, top, outline[i].y);
            }
            var triangles = new List<int>(Mathf.Max(0, n - 2) * 6 + n * 6);
            for (int i = 1; i + 1 < n; i++)
            {
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(i);
                triangles.Add(n); triangles.Add(n + i); triangles.Add(n + i + 1);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                triangles.Add(i); triangles.Add(n + j); triangles.Add(j);
                triangles.Add(i); triangles.Add(n + i); triangles.Add(n + j);
            }
            var mesh = new Mesh { name = "Shot wall" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Twice the signed area of the triangle o, a, b: positive when o, a, b turn counter-clockwise.</summary>
        static float Turn(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);
    }
}
