using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The meshes of the whitebox polyhedra, made in code once and shared: every shape is a unit mesh filling the box from -0.5 to 0.5
    /// (a part's transform scales it to the part's size), except the ring, whose band width depends on its size and which is therefore
    /// made at its size and shared by parts of the same size. Every face has its own vertices and normal, so the shapes are flat-lit and
    /// read as polyhedra. The shapes follow the side views in make_sheet.py (shape()).
    /// </summary>
    public static partial class Whitebox
    {
        const float ObeliskShaftTop = .28f; // half width of an obelisk's shaft where the cap starts (make_sheet.py: sz * .28), at 80 % of its height
        const int DiscSides = 24, RingSegments = 32;
        const float RingBandShare = .06f; // a ring's band is this share of its mean diameter wide, and never thinner than the ring is thick

        static readonly Dictionary<int, Mesh> unitMeshes = new();
        static readonly Dictionary<Vector3Int, Mesh> ringMeshes = new();

        /// <summary>The mesh of a part: shared by every part of the same shape (and, for prisms, the same axis); rings by size.</summary>
        internal static Mesh MeshFor(in Part part)
        {
            if (part.prim == Prim.Ring)
            {
                var key = Vector3Int.RoundToInt(part.size * 1000f);
                if (ringMeshes.TryGetValue(key, out var ring) && ring) return ring;
                return ringMeshes[key] = RingMesh(part.size);
            }
            int axis = part.prim is Prim.Prism6 or Prim.Prism8 or Prim.Disc ? AxisOf(part.size) : 1;
            int id = (int)part.prim * 3 + axis;
            if (unitMeshes.TryGetValue(id, out var mesh) && mesh) return mesh;
            return unitMeshes[id] = UnitMesh(part.prim, axis);
        }

        /// <summary>The local scale of a part's transform: its size for a unit mesh, 1 for a ring (made at its size).</summary>
        internal static Vector3 PartScale(in Part part) => part.prim == Prim.Ring ? Vector3.one : part.size;

        /// <summary>The axis a prism runs along: the size that differs from the other two (y when x and z match: the Spitter's body;
        /// z when x and y match: the rocket launcher's tube); otherwise the longest.</summary>
        static int AxisOf(Vector3 size)
        {
            const float same = 1e-4f;
            if (Mathf.Abs(size.x - size.z) < same) return 1;
            if (Mathf.Abs(size.x - size.y) < same) return 2;
            if (Mathf.Abs(size.y - size.z) < same) return 0;
            return size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;
        }

        static Mesh UnitMesh(Prim prim, int axis)
        {
            var s = new ShapeBuilder();
            switch (prim)
            {
                case Prim.Wedge: Wedge(s); break;
                case Prim.Pyramid: Pyramid(s); break;
                case Prim.PyramidZ: PyramidZ(s); break;
                case Prim.Octa: Octa(s); break;
                case Prim.Prism6: Prism(s, 6, axis); break;
                case Prim.Prism8: Prism(s, 8, axis); break;
                case Prim.Disc: Prism(s, DiscSides, axis); break;
                case Prim.Icosphere: Icosphere(s); break;
                case Prim.Obelisk: Obelisk(s); break;
                case Prim.Flare: Flare(s); break;
                default: Box(s); break;
            }
            return s.ToMesh("Whitebox " + prim + (axis != 1 ? " along " + "xyz"[axis] : ""));
        }

        static Vector3 Corner(float x, float y, float z) => new(x - .5f, y - .5f, z - .5f);

        static void Box(ShapeBuilder s)
        {
            s.Quad(Corner(0, 0, 0), Corner(1, 0, 0), Corner(1, 0, 1), Corner(0, 0, 1));
            s.Quad(Corner(0, 1, 0), Corner(0, 1, 1), Corner(1, 1, 1), Corner(1, 1, 0));
            s.Quad(Corner(0, 0, 0), Corner(0, 1, 0), Corner(1, 1, 0), Corner(1, 0, 0));
            s.Quad(Corner(0, 0, 1), Corner(1, 0, 1), Corner(1, 1, 1), Corner(0, 1, 1));
            s.Quad(Corner(0, 0, 0), Corner(0, 0, 1), Corner(0, 1, 1), Corner(0, 1, 0));
            s.Quad(Corner(1, 0, 0), Corner(1, 1, 0), Corner(1, 1, 1), Corner(1, 0, 1));
        }

        /// <summary>High at the back, sloping down to the front edge on the ground (the side view's right triangle, run across x).</summary>
        static void Wedge(ShapeBuilder s)
        {
            s.centre = new Vector3(0f, -1f / 6f, -1f / 6f);
            Vector3 bl = Corner(0, 0, 0), br = Corner(1, 0, 0), fl = Corner(0, 0, 1), fr = Corner(1, 0, 1), tl = Corner(0, 1, 0), tr = Corner(1, 1, 0);
            s.Quad(bl, br, fr, fl);
            s.Quad(bl, tl, tr, br);
            s.Quad(fl, fr, tr, tl);
            s.Tri(bl, fl, tl);
            s.Tri(br, tr, fr);
        }

        static void Pyramid(ShapeBuilder s)
        {
            s.centre = new Vector3(0f, -.25f, 0f);
            Vector3 a = Corner(0, 0, 0), b = Corner(1, 0, 0), c = Corner(1, 0, 1), d = Corner(0, 0, 1), top = new(0f, .5f, 0f);
            s.Quad(a, b, c, d);
            s.Tri(a, b, top); s.Tri(b, c, top); s.Tri(c, d, top); s.Tri(d, a, top);
        }

        /// <summary>A pyramid lying with its base at the back and its apex forward (the rocket's nose cone).</summary>
        static void PyramidZ(ShapeBuilder s)
        {
            s.centre = new Vector3(0f, 0f, -.25f);
            Vector3 a = Corner(0, 0, 0), b = Corner(1, 0, 0), c = Corner(1, 1, 0), d = Corner(0, 1, 0), tip = new(0f, 0f, .5f);
            s.Quad(a, b, c, d);
            s.Tri(a, b, tip); s.Tri(b, c, tip); s.Tri(c, d, tip); s.Tri(d, a, tip);
        }

        static void Octa(ShapeBuilder s)
        {
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                s.Tri(new Vector3(.5f * x, 0f, 0f), new Vector3(0f, .5f * y, 0f), new Vector3(0f, 0f, .5f * z));
        }

        /// <summary>A prism of the given number of sides along an axis, stretched so its outline fills the unit square exactly.</summary>
        static void Prism(ShapeBuilder s, int sides, int axis)
        {
            var outline = new Vector2[sides];
            Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            for (int i = 0; i < sides; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2f / sides; // a flat side, not a corner, faces each axis when the count allows
                outline[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                min = Vector2.Min(min, outline[i]); max = Vector2.Max(max, outline[i]);
            }
            for (int i = 0; i < sides; i++)
                outline[i] = new Vector2((outline[i].x - min.x) / (max.x - min.x) - .5f, (outline[i].y - min.y) / (max.y - min.y) - .5f);
            Vector3 At(Vector2 p, float h) => axis switch
            {
                0 => new Vector3(h, p.x, p.y),
                2 => new Vector3(p.x, p.y, h),
                _ => new Vector3(p.x, h, p.y)
            };
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++) { bottom[i] = At(outline[i], -.5f); top[i] = At(outline[i], .5f); }
            s.Polygon(bottom);
            s.Polygon(top);
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                s.Quad(bottom[i], bottom[j], top[j], top[i]);
            }
        }

        static void Icosphere(ShapeBuilder s)
        {
            float t = (1f + Mathf.Sqrt(5f)) * .5f;
            var v = new[]
            {
                new Vector3(-1f, t, 0f), new Vector3(1f, t, 0f), new Vector3(-1f, -t, 0f), new Vector3(1f, -t, 0f),
                new Vector3(0f, -1f, t), new Vector3(0f, 1f, t), new Vector3(0f, -1f, -t), new Vector3(0f, 1f, -t),
                new Vector3(t, 0f, -1f), new Vector3(t, 0f, 1f), new Vector3(-t, 0f, -1f), new Vector3(-t, 0f, 1f)
            };
            int[] faces =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int i = 0; i < faces.Length; i += 3)
            {
                Vector3 a = v[faces[i]].normalized * .5f, b = v[faces[i + 1]].normalized * .5f, c = v[faces[i + 2]].normalized * .5f;
                Vector3 ab = (a + b).normalized * .5f, bc = (b + c).normalized * .5f, ca = (c + a).normalized * .5f;
                s.Tri(a, ab, ca); s.Tri(b, bc, ab); s.Tri(c, ca, bc); s.Tri(ab, bc, ca);
            }
        }

        /// <summary>A square shaft narrowing to ObeliskShaftTop at 80 % of the height, then a pyramid cap.</summary>
        static void Obelisk(ShapeBuilder s)
        {
            s.centre = new Vector3(0f, -.1f, 0f);
            float k = ObeliskShaftTop, h = .3f;
            Vector3[] low = { Corner(0, 0, 0), Corner(1, 0, 0), Corner(1, 0, 1), Corner(0, 0, 1) };
            Vector3[] high = { new(-k, h, -k), new(k, h, -k), new(k, h, k), new(-k, h, k) };
            var apex = new Vector3(0f, .5f, 0f);
            s.Quad(low[0], low[1], low[2], low[3]);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                s.Quad(low[i], low[j], high[j], high[i]);
                s.Tri(high[i], high[j], apex);
            }
        }

        /// <summary>A horn: the back face half as wide and high as the front (the scatter cannon's mouth).</summary>
        static void Flare(ShapeBuilder s)
        {
            s.centre = new Vector3(0f, 0f, .07f);
            Vector3[] back = { new(-.25f, -.25f, -.5f), new(.25f, -.25f, -.5f), new(.25f, .25f, -.5f), new(-.25f, .25f, -.5f) };
            Vector3[] front = { new(-.5f, -.5f, .5f), new(.5f, -.5f, .5f), new(.5f, .5f, .5f), new(-.5f, .5f, .5f) };
            s.Quad(back[0], back[1], back[2], back[3]);
            s.Quad(front[0], front[1], front[2], front[3]);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                s.Quad(back[i], back[j], front[j], front[i]);
            }
        }

        /// <summary>A closed band round the axis of the thinnest size, filling the size's box: flat (the hole's rim, the drone's ring)
        /// or standing (the gate). Made at its size so the band keeps its width.</summary>
        static Mesh RingMesh(Vector3 size)
        {
            int thin = size.x <= size.y && size.x <= size.z ? 0 : size.y <= size.z ? 1 : 2;
            int u = thin == 0 ? 1 : 0, w = thin == 2 ? 1 : 2;
            float a = size[u] * .5f, b = size[w] * .5f, half = size[thin] * .5f;
            float band = Mathf.Min(Mathf.Max(size[thin], RingBandShare * (size[u] + size[w]) * .5f), Mathf.Min(a, b) * .45f);
            Vector3 At(float pu, float pw, float h)
            {
                var p = Vector3.zero;
                p[u] = pu; p[w] = pw; p[thin] = h;
                return p;
            }
            var s = new ShapeBuilder();
            var axis = At(0f, 0f, 1f);
            for (int i = 0; i < RingSegments; i++)
            {
                float a0 = i * Mathf.PI * 2f / RingSegments, a1 = (i + 1) * Mathf.PI * 2f / RingSegments, mid = (a0 + a1) * .5f;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0), c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);
                Vector3 o0 = At(a * c0, b * s0, -half), o1 = At(a * c1, b * s1, -half), o0t = At(a * c0, b * s0, half), o1t = At(a * c1, b * s1, half);
                Vector3 i0 = At((a - band) * c0, (b - band) * s0, -half), i1 = At((a - band) * c1, (b - band) * s1, -half);
                Vector3 i0t = At((a - band) * c0, (b - band) * s0, half), i1t = At((a - band) * c1, (b - band) * s1, half);
                var radial = At(Mathf.Cos(mid), Mathf.Sin(mid), 0f);
                s.QuadHint(o0, o1, o1t, o0t, radial);
                s.QuadHint(i0, i0t, i1t, i1, -radial);
                s.QuadHint(o0t, o1t, i1t, i0t, axis);
                s.QuadHint(o0, i0, i1, o1, -axis);
            }
            return s.ToMesh("Whitebox Ring " + size.x.ToString("0.##") + "x" + size.y.ToString("0.##") + "x" + size.z.ToString("0.##"));
        }

        /// <summary>Collects flat-shaded faces. Each face is turned to face away from the shape's centre (or along the hint given), so the
        /// corners can be listed in either order; front faces then wind clockwise as Unity draws them.</summary>
        sealed class ShapeBuilder
        {
            readonly List<Vector3> vertices = new(), normals = new();
            readonly List<int> triangles = new();
            /// <summary>A point inside a convex shape, from which every face points away.</summary>
            public Vector3 centre;

            public void Tri(Vector3 a, Vector3 b, Vector3 c) => TriHint(a, b, c, (a + b + c) / 3f - centre);

            public void TriHint(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
            {
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(normal, outward) < 0f) { (b, c) = (c, b); normal = -normal; }
                normal.Normalize();
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                triangles.Add(first); triangles.Add(first + 1); triangles.Add(first + 2);
            }

            /// <summary>A flat four-cornered face, corners in order round it.</summary>
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d) => QuadHint(a, b, c, d, (a + b + c + d) * .25f - centre);

            public void QuadHint(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
            {
                TriHint(a, b, c, outward);
                TriHint(a, c, d, outward);
            }

            /// <summary>A flat convex face, corners in order round it.</summary>
            public void Polygon(IReadOnlyList<Vector3> loop)
            {
                var middle = Vector3.zero;
                for (int i = 0; i < loop.Count; i++) middle += loop[i];
                middle /= loop.Count;
                var outward = middle - centre;
                for (int i = 0; i < loop.Count; i++) TriHint(middle, loop[i], loop[(i + 1) % loop.Count], outward);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
