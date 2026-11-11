using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AnomalyArena
{
    /// <summary>A stone wall (V5TerrainWalls) is a closed solid: from any direction above the ground the view stops on a face that looks at the viewer and never reaches the
    /// hollow inside. The wall's material draws fronts only and the game's view direction is a setting, so a wall that holds only the faces one direction shows is an
    /// open box from the next direction.</summary>
    [TestFixture]
    public class V5TerrainSolidTest
    {
        // The edge of a stone's top lies at most this far below the stone's height (the largest drop among the three kinds of stone), so below
        // "lowest stone - EdgeDrop" every side of a wall is whole and every spot is inside the stone.
        const float EdgeDrop = .3f;
        // A side's stones stand on the rectangle's edge or a few centimetres behind it (their recessed joints); this far in counts as on the side.
        const float Skin = .1f;
        const float BinSize = .5f;

        // A boulder, a pillar, two long walls (one each way), an outcrop and a piece cut to an odd size.
        static readonly Rect[] Rects =
        {
            new Rect(-4.8f, 11f, 1.6f, 1.6f), new Rect(-65f, -33f, 4f, 2f), new Rect(-30f, 40f, 14f, 2f), new Rect(50f, -26f, 2f, 12f),
            new Rect(22f, -8f, 6f, 4f), new Rect(3.2f, 7.7f, 1.667f, 2f),
        };

        static readonly (string name, Vector3 outward)[] Sides =
        {
            ("south", Vector3.back), ("north", Vector3.forward), ("east", Vector3.right), ("west", Vector3.left),
        };

        [Test]
        public void Build_AnyRectAndStone_EverySideCarriesStoneThatFacesOutward()
        {
            foreach (V5WallStyle style in System.Enum.GetValues(typeof(V5WallStyle)))
            foreach (var rect in Rects)
            foreach (int axis in new[] { 0, 1 })
            {
                var shape = V5TerrainWalls.Build(style, rect, axis, true);
                try
                {
                    Vector3[] points = shape.mesh.vertices;
                    int[] triangles = shape.mesh.triangles;
                    float whole = Mathf.Min(shape.heights) - EdgeDrop;
                    foreach (var (name, outward) in Sides)
                    {
                        bool alongX = Mathf.Abs(outward.z) > .5f;
                        float plane = alongX ? rect.height * .5f : rect.width * .5f, length = alongX ? rect.width : rect.height;
                        // What the side shows to a viewer straight in front of it: the area of its outward-looking triangles, flattened onto the side.
                        float shown = 0f;
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            Vector3 a = points[triangles[t]], b = points[triangles[t + 1]], c = points[triangles[t + 2]];
                            if (plane - Vector3.Dot(a, outward) > Skin || plane - Vector3.Dot(b, outward) > Skin || plane - Vector3.Dot(c, outward) > Skin) continue;
                            // A triangle's front is the side its corners run clockwise on, which is the side Cross(b - a, c - a) points to.
                            shown += Mathf.Max(0f, Vector3.Dot(Vector3.Cross(b - a, c - a), outward) * .5f);
                        }
                        Assert.That(shown, Is.GreaterThanOrEqualTo(length * whole * .98f),
                            $"{style} {rect} axis {axis}: the {name} side shows {shown:0.00} m2 of stone, it is {length:0.00} m long and whole up to {whole:0.00} m");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(shape.mesh);
                }
            }
        }

        [Test]
        public void Build_AnyRectAndStone_NoSpotInsideShowsFromAnyDirectionAboveTheGround()
        {
            // Where the view looks: every 45 degrees round the wall at a low, a middle (the game's 50) and a steep pitch, and straight down.
            var views = new List<(float yaw, float pitch)> { (0f, 90f) };
            foreach (float pitch in new[] { 15f, 50f, 80f })
                for (int turn = 0; turn < 8; turn++) views.Add((turn * 45f, pitch));

            foreach (V5WallStyle style in System.Enum.GetValues(typeof(V5WallStyle)))
            foreach (var rect in Rects)
            foreach (int axis in new[] { 0, 1 })
            {
                var shape = V5TerrainWalls.Build(style, rect, axis, true);
                try
                {
                    Vector3[] points = shape.mesh.vertices;
                    int[] triangles = shape.mesh.triangles;
                    var spots = SpotsInside(rect, Mathf.Min(shape.heights) - EdgeDrop);
                    int seen = 0;
                    string first = null;
                    foreach (var (yaw, pitch) in views)
                    {
                        var view = new View(points, triangles, yaw, pitch);
                        foreach (var spot in spots)
                        {
                            if (view.Hides(spot)) continue;
                            seen++;
                            first ??= $"({spot.x:0.00}, {spot.y:0.00}, {spot.z:0.00}) from the centre, looking with yaw {yaw:0} and pitch {pitch:0}";
                        }
                    }
                    Assert.That(seen, Is.EqualTo(0),
                        $"{style} {rect} axis {axis}: {seen} of {spots.Count * views.Count} looks reach a spot inside the wall with no stone in front of it; the first is {first}");
                }
                finally
                {
                    Object.DestroyImmediate(shape.mesh);
                }
            }
        }

        /// <summary>Spots inside the wall, in the mesh's coordinates: a grid kept 0.2 m from the sides, between the bed (0.3 m) and the height up to which the wall is whole.</summary>
        static List<Vector3> SpotsInside(Rect rect, float whole)
        {
            var spots = new List<Vector3>();
            const float inset = .2f, lowest = .4f;
            float highest = whole - .05f;
            Assert.That(highest, Is.GreaterThanOrEqualTo(lowest), "the wall is tall enough to have an inside above its bed");
            int nx = Mathf.Max(2, Mathf.CeilToInt((rect.width - inset * 2f) / .3f) + 1), nz = Mathf.Max(2, Mathf.CeilToInt((rect.height - inset * 2f) / .3f) + 1);
            for (int ix = 0; ix < nx; ix++)
            for (int iz = 0; iz < nz; iz++)
            for (int iy = 0; iy < 3; iy++)
                spots.Add(new Vector3(
                    Mathf.Lerp(-rect.width * .5f + inset, rect.width * .5f - inset, ix / (nx - 1f)),
                    Mathf.Lerp(lowest, highest, iy * .5f),
                    Mathf.Lerp(-rect.height * .5f + inset, rect.height * .5f - inset, iz / (nz - 1f))));
            return spots;
        }

        /// <summary>The mesh as one view direction shows it with parallel rays (the game's camera is orthographic): the triangles whose fronts look at the viewer, flattened
        /// onto the screen and sorted into squares so a spot is tried only against the triangles near it.</summary>
        sealed class View
        {
            readonly Vector3 into, right, up;
            readonly List<int>[] bins;
            readonly Vector3[] flat;
            readonly int[] triangles;
            readonly float minX, minY;
            readonly int binsX, binsY;

            public View(Vector3[] points, int[] triangles, float yaw, float pitch)
            {
                this.triangles = triangles;
                var turn = Quaternion.Euler(pitch, yaw, 0f);
                into = turn * Vector3.forward; right = turn * Vector3.right; up = turn * Vector3.up;
                flat = new Vector3[points.Length];
                float maxX = float.MinValue, maxY = float.MinValue;
                minX = float.MaxValue; minY = float.MaxValue;
                for (int i = 0; i < points.Length; i++)
                {
                    flat[i] = Flat(points[i]);
                    minX = Mathf.Min(minX, flat[i].x); maxX = Mathf.Max(maxX, flat[i].x);
                    minY = Mathf.Min(minY, flat[i].y); maxY = Mathf.Max(maxY, flat[i].y);
                }
                binsX = Mathf.FloorToInt((maxX - minX) / BinSize) + 1; binsY = Mathf.FloorToInt((maxY - minY) / BinSize) + 1;
                bins = new List<int>[binsX * binsY];
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    Vector3 a = flat[triangles[t]], b = flat[triangles[t + 1]], c = flat[triangles[t + 2]];
                    // On the screen (x to the right, y up) a front runs clockwise; a back or an edge-on triangle is not drawn.
                    if ((b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) >= -1e-9f) continue;
                    int x0 = BinX(Mathf.Min(a.x, Mathf.Min(b.x, c.x))), x1 = BinX(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
                    int y0 = BinY(Mathf.Min(a.y, Mathf.Min(b.y, c.y))), y1 = BinY(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
                    for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                        (bins[x + y * binsX] ??= new List<int>()).Add(t);
                }
            }

            // x and y are the place on the screen, z the distance along the view.
            Vector3 Flat(Vector3 p) => new(Vector3.Dot(p, right), Vector3.Dot(p, up), Vector3.Dot(p, into));
            int BinX(float x) => Mathf.Clamp(Mathf.FloorToInt((x - minX) / BinSize), 0, binsX - 1);
            int BinY(float y) => Mathf.Clamp(Mathf.FloorToInt((y - minY) / BinSize), 0, binsY - 1);

            /// <summary>True when a drawn triangle lies between the viewer and the spot.</summary>
            public bool Hides(Vector3 spot)
            {
                Vector3 q = Flat(spot);
                if (q.x < minX || q.y < minY || q.x > minX + binsX * BinSize || q.y > minY + binsY * BinSize) return false;
                var near = bins[BinX(q.x) + BinY(q.y) * binsX];
                if (near == null) return false;
                foreach (int t in near)
                {
                    Vector3 a = flat[triangles[t]], b = flat[triangles[t + 1]], c = flat[triangles[t + 2]];
                    float whole = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
                    float wa = ((b.y - c.y) * (q.x - c.x) + (c.x - b.x) * (q.y - c.y)) / whole;
                    float wb = ((c.y - a.y) * (q.x - c.x) + (a.x - c.x) * (q.y - c.y)) / whole;
                    float wc = 1f - wa - wb;
                    // A spot exactly behind the line two triangles share counts as behind both.
                    if (wa < -1e-4f || wb < -1e-4f || wc < -1e-4f) continue;
                    if (wa * a.z + wb * b.z + wc * c.z < q.z - .001f) return true;
                }
                return false;
            }
        }
    }
}
