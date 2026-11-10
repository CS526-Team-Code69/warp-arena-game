using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The walls of the V2 base and ship models (BaseFootprint, 2026-11-10): the outline of a model where shots fly, never outside
    /// it, cut down to a few corners, and cut into slabs along a long model so a hull does not bridge the gaps between its parts.</summary>
    [TestFixture]
    public class BaseFootprintTest
    {
        static readonly float[] ShotHeights = { .4f, .8f, 1.4f };

        [Test]
        public void Outline_OfABox_IsItsFootprint()
        {
            var (vertices, triangles) = Box(new Vector3(-5f, 0f, -5f), new Vector3(5f, 5f, 5f));
            var outlines = BaseFootprint.Outlines(vertices, triangles, ShotHeights, 1, 32);
            Assert.That(outlines, Has.Count.EqualTo(1));
            var outline = outlines[0];
            Assert.That(outline, Has.Length.EqualTo(4), "a box has four corners");
            foreach (var corner in outline)
            {
                Assert.That(Mathf.Abs(corner.x), Is.EqualTo(5f).Within(.001f), "corner " + corner);
                Assert.That(Mathf.Abs(corner.y), Is.EqualTo(5f).Within(.001f), "corner " + corner);
            }
            var bounds = BaseFootprint.Bounds(outline);
            Assert.That((bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax), Is.EqualTo((-5f, -5f, 5f, 5f)));
        }

        [Test]
        public void Outline_OfARoundBody_LeavesTheSquaresCornersOpen_AndIgnoresWhatLiesBelowOrAboveTheShots()
        {
            // An octagonal body of radius 4.5 from the ground to 5 m, a flat skirt on the ground out to 7 and a roof slab at 5.2 m out to 6.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            AddPrism(vertices, triangles, 8, 4.5f, 0f, 5f);
            AddFlat(vertices, triangles, 7f, .05f);
            AddFlat(vertices, triangles, 6f, 5.2f);
            var outlines = BaseFootprint.Outlines(vertices, triangles, ShotHeights, 1, 32);
            Assert.That(outlines, Has.Count.EqualTo(1));
            var outline = outlines[0];
            float farthest = outline.Max(p => p.magnitude);
            Assert.That(farthest, Is.EqualTo(4.5f).Within(.001f), "the outline is the body's, not the skirt's or the roof's");
            Assert.That(Support(outline, new Vector2(1f, 1f).normalized), Is.LessThan(5f * Mathf.Sqrt(2f) - 2f),
                "the square footprint's corner (7.07 out) is left open by about as much as the body leaves it");
        }

        [Test]
        public void Outlines_InSlabs_FollowANarrowPartBesideAWideOne()
        {
            // Like the ship: a narrow body 14 long and 2 wide, with wings 8 wide over its last 3.
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var (bar, barTriangles) = Box(new Vector3(-7f, 0f, -1f), new Vector3(7f, 3f, 1f));
            Append(vertices, triangles, bar, barTriangles);
            var (wings, wingTriangles) = Box(new Vector3(-7f, 0f, -4f), new Vector3(-4f, 3f, 4f));
            Append(vertices, triangles, wings, wingTriangles);

            var beside = new Vector2(1f, 1.5f); // open ground beside the narrow part
            var one = BaseFootprint.Outlines(vertices, triangles, ShotHeights, 1, 32);
            Assert.That(one, Has.Count.EqualTo(1));
            Assert.That(Inside(one[0], beside), Is.True, "one convex outline bridges the gap beside the narrow part (the reason for slabs)");

            var slabs = BaseFootprint.Outlines(vertices, triangles, ShotHeights, 7, 32);
            Assert.That(slabs, Has.Count.EqualTo(7));
            Assert.That(slabs.Any(o => Inside(o, beside)), Is.False, "no slab stands on the open ground beside the narrow part");
            foreach (var outline in slabs.Where(o => o.Min(p => p.x) > -3.5f))
                Assert.That(outline.Max(p => Mathf.Abs(p.y)), Is.LessThanOrEqualTo(1.001f), "a slab clear of the wings is as wide as the narrow part");
            Assert.That(slabs.Max(o => o.Max(p => Mathf.Abs(p.y))), Is.EqualTo(4f).Within(.001f), "the wings keep their walls");
            Assert.That(slabs.Min(o => o.Min(p => p.x)), Is.EqualTo(-7f).Within(.001f));
            Assert.That(slabs.Max(o => o.Max(p => p.x)), Is.EqualTo(7f).Within(.001f), "the slabs reach both ends");
        }

        [Test]
        public void Reduce_KeepsAtMostTheCornersAsked_AndOnlyShrinks()
        {
            var circle = new List<Vector2>();
            for (int i = 0; i < 100; i++) circle.Add(new Vector2(Mathf.Cos(i * Mathf.PI * 2f / 100f), Mathf.Sin(i * Mathf.PI * 2f / 100f)) * 5f);
            var hull = BaseFootprint.ConvexHull(circle);
            Assert.That(hull, Has.Count.EqualTo(100), "every point of a circle is a corner of its hull");
            var reduced = BaseFootprint.Reduce(hull, 16);
            Assert.That(reduced, Has.Count.EqualTo(16));
            Assert.That(reduced, Is.SubsetOf(hull), "corners are only dropped, never moved outward");
            Assert.That(Area(reduced), Is.GreaterThan(.9f * Mathf.PI * 25f), "the shape is kept");
        }

        [Test]
        public void Prism_StandsOnTheOutline_FromBottomToTop()
        {
            var outline = new[] { new Vector2(-2f, -1f), new Vector2(2f, -1f), new Vector2(2f, 1f), new Vector2(-2f, 1f) };
            var mesh = BaseFootprint.Prism(outline, 0f, 4f);
            try
            {
                Assert.That(mesh.vertexCount, Is.EqualTo(8));
                Assert.That(mesh.bounds.min, Is.EqualTo(new Vector3(-2f, 0f, -1f)));
                Assert.That(mesh.bounds.max, Is.EqualTo(new Vector3(2f, 4f, 1f)));
                Assert.That(mesh.triangles.Length, Is.EqualTo((2 * 2 + 4 * 2) * 3), "two caps of two triangles and four sides of two");
            }
            finally
            {
                Object.Destroy(mesh);
            }
        }

        static float Support(IEnumerable<Vector2> outline, Vector2 direction) => outline.Max(p => Vector2.Dot(p, direction));

        /// <summary>Whether the point lies inside a convex outline (either winding).</summary>
        static bool Inside(IReadOnlyList<Vector2> outline, Vector2 point)
        {
            bool left = false, right = false;
            for (int i = 0; i < outline.Count; i++)
            {
                Vector2 a = outline[i], b = outline[(i + 1) % outline.Count];
                float turn = (b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x);
                if (turn > 0f) left = true;
                else if (turn < 0f) right = true;
            }
            return !(left && right);
        }

        static void Append(List<Vector3> vertices, List<int> triangles, Vector3[] more, int[] moreTriangles)
        {
            int start = vertices.Count;
            vertices.AddRange(more);
            triangles.AddRange(moreTriangles.Select(i => start + i));
        }

        static float Area(IReadOnlyList<Vector2> polygon)
        {
            float twice = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                twice += a.x * b.y - b.x * a.y;
            }
            return Mathf.Abs(twice) * .5f;
        }

        static (Vector3[] vertices, int[] triangles) Box(Vector3 min, Vector3 max)
        {
            // Vertex i: bit 0 picks x, bit 1 y, bit 2 z (0 = min, 1 = max).
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++)
                vertices[i] = new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z);
            int[] triangles =
            {
                0, 1, 5, 0, 5, 4, 2, 6, 7, 2, 7, 3, // y min, y max
                0, 4, 6, 0, 6, 2, 1, 3, 7, 1, 7, 5, // x min, x max
                0, 2, 3, 0, 3, 1, 4, 5, 7, 4, 7, 6, // z min, z max
            };
            return (vertices, triangles);
        }

        static void AddPrism(List<Vector3> vertices, List<int> triangles, int sides, float radius, float bottom, float top)
        {
            int start = vertices.Count;
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, bottom, Mathf.Sin(a) * radius));
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, top, Mathf.Sin(a) * radius));
            }
            for (int i = 0; i < sides; i++)
            {
                int b0 = start + i * 2, t0 = b0 + 1, b1 = start + (i + 1) % sides * 2, t1 = b1 + 1;
                triangles.AddRange(new[] { b0, t0, t1, b0, t1, b1 });
            }
        }

        /// <summary>A flat square of this half size at one height (two triangles): no edge of it crosses another height.</summary>
        static void AddFlat(List<Vector3> vertices, List<int> triangles, float half, float height)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(-half, height, -half)); vertices.Add(new Vector3(half, height, -half));
            vertices.Add(new Vector3(half, height, half)); vertices.Add(new Vector3(-half, height, half));
            triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }
    }
}
