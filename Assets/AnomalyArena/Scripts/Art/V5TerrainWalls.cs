using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{

    public enum V5WallStyle { Ruins, Dunes, Meteor }

    public sealed class V5WallShape
    {
        public const float GridCell = .25f;
        public Mesh mesh;
        public float width, depth;
        public int cellsX, cellsZ;
        public float[] heights;
        public float tallest;

        public float HeightAt(float x, float z)
        {
            int ix = Mathf.FloorToInt((x + width * .5f) / GridCell), iz = Mathf.FloorToInt((z + depth * .5f) / GridCell);
            return ix < 0 || iz < 0 || ix >= cellsX || iz >= cellsZ ? 0f : heights[ix + iz * cellsX];
        }
    }

    public static class V5TerrainWalls
    {
        sealed class Look
        {
            public string material;
            public float rowSize, rowJitter, stoneLength, stoneJitter;
            public float bevel, drop, rise, tilt, dome, roughTop, mortar, edge;
            public float chipChance, chipSize;
            public float low, mid, high, plinth;
            public float breakScale, breakLow, breakMid, looseChance;
            public float courseHeight, courseJitter, courseValue, faceLength, faceJitter, faceBevel, faceRecess, faceValue, footAo, footHeight;
            public Color stoneA, stoneB, stain;
            public Color32 bed;
            public float stainChance, valueJitter, lowDark, midDark, shade, uvTile;
        }

        static readonly Look Ruins = new()
        {
            material = "V5/Terrain/V5WallRuins",
            rowSize = .5f, rowJitter = .1f, stoneLength = .95f, stoneJitter = .62f,
            bevel = .05f, drop = .035f, rise = .03f, tilt = .05f, dome = 0f, roughTop = 0f, mortar = .36f, edge = .8f,
            chipChance = .14f, chipSize = .16f,
            low = 1f, mid = 1.4f, high = 1.8f, plinth = .3f,
            breakScale = 4.3f, breakLow = .36f, breakMid = .47f, looseChance = .07f,
            courseHeight = .42f, courseJitter = .12f, courseValue = .05f, faceLength = 1f, faceJitter = .6f, faceBevel = .04f, faceRecess = .035f, faceValue = .6f, footAo = .55f, footHeight = 1.3f,
            stoneA = new Color(.92f, .82f, .7f), stoneB = new Color(.86f, .8f, .72f), stain = new Color(.62f, .55f, .44f), bed = new Color32(52, 46, 40, 255),
            stainChance = .16f, valueJitter = .13f, lowDark = .76f, midDark = .89f, shade = .62f, uvTile = 7f,
        };

        static readonly Look Dunes = new()
        {
            material = "V5/Terrain/V5WallDunes",
            rowSize = 1.25f, rowJitter = .34f, stoneLength = 1.9f, stoneJitter = .7f,
            bevel = .17f, drop = .13f, rise = .05f, tilt = .05f, dome = .1f, roughTop = .05f, mortar = .3f, edge = .72f,
            chipChance = .66f, chipSize = .36f,
            low = 1.2f, mid = 1.55f, high = 1.85f, plinth = .3f,
            breakScale = 3.4f, breakLow = .34f, breakMid = .47f, looseChance = .12f,
            courseHeight = .29f, courseJitter = .42f, courseValue = .13f, faceLength = 2.3f, faceJitter = .85f, faceBevel = .035f, faceRecess = .03f, faceValue = .62f, footAo = .52f, footHeight = 1.25f,
            stoneA = new Color(.86f, .62f, .3f), stoneB = new Color(.8f, .58f, .31f), stain = new Color(.55f, .36f, .2f), bed = new Color32(50, 30, 16, 255),
            stainChance = .2f, valueJitter = .11f, lowDark = .82f, midDark = .92f, shade = .6f, uvTile = 8f,
        };

        static readonly Look Meteor = new()
        {
            material = "V5/Terrain/V5WallMeteor",
            rowSize = 1.7f, rowJitter = .5f, stoneLength = 1.7f, stoneJitter = .8f,
            bevel = .2f, drop = .3f, rise = .08f, tilt = .1f, dome = .32f, roughTop = .16f, mortar = .3f, edge = .55f,
            chipChance = .9f, chipSize = .5f,
            low = 1.5f, mid = 1.6f, high = 1.75f, plinth = .3f,
            breakScale = 1.5f, breakLow = .3f, breakMid = .5f, looseChance = .1f,
            courseHeight = .5f, courseJitter = .7f, courseValue = .14f, faceLength = 1f, faceJitter = .9f, faceBevel = .06f, faceRecess = .05f, faceValue = 1f, footAo = .6f, footHeight = 1.2f,
            stoneA = new Color(.34f, .29f, .27f), stoneB = new Color(.28f, .24f, .23f), stain = new Color(.36f, .27f, .22f), bed = new Color32(26, 16, 12, 255),
            stainChance = .08f, valueJitter = .16f, lowDark = .9f, midDark = .95f, shade = .6f, uvTile = 5f,
        };

        struct Stone
        {
            public float x0, x1, z0, z1, top, hash;
            public int level;
        }

        static readonly Vector3 DefaultSun = new(.2868f, .8192f, -.4967f);
        static readonly List<Vector3> Points = new(8192), Normals = new(8192);
        static readonly List<Color32> Colors = new(8192);
        static readonly List<Vector2> Uvs = new(8192);
        static readonly List<int> Triangles = new(16384);
        static readonly List<Stone> Stones = new(256);
        static readonly List<float> Rows = new(32), Cuts = new(64), Courses = new(16), Pieces = new(32);
        static readonly List<(float s0, float s1, float top, int level)> Runs = new(32);
        static readonly Vector3[] OuterRing = new Vector3[8], InnerRing = new Vector3[8];
        static readonly byte[] OuterLight = new byte[8], InnerLight = new byte[8];
        static readonly Dictionary<V5WallStyle, Material> Materials = new();
        static Vector3 sun = DefaultSun;
        static bool sunRead;

        public static Vector3 ToSun
        {
            get
            {
                if (sunRead) return sun;
                sunRead = true;
                Light best = RenderSettings.sun;
                if (!best)
                    foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                        if (light.type == LightType.Directional && (!best || light.intensity > best.intensity)) best = light;
                if (best) sun = -best.transform.forward;
                return sun;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Forget()
        {
            sunRead = false;
            sun = DefaultSun;
            Materials.Clear();
        }

        public static bool StyleOfGroup(Transform group, out V5WallStyle style)
        {
            style = V5WallStyle.Ruins;
            if (!group) return false;
            if (group.name.Contains("WreckField")) return true;
            style = V5WallStyle.Dunes;
            return group.name.Contains("DustFlats");
        }

        public static Material MaterialOf(V5WallStyle style)
        {
            if (Materials.TryGetValue(style, out var known) && known) return known;
            var material = Resources.Load<Material>(LookOf(style).material);
            Materials[style] = material;
            return material;
        }

        static Look LookOf(V5WallStyle style) => style switch { V5WallStyle.Dunes => Dunes, V5WallStyle.Meteor => Meteor, _ => Ruins };

        public static V5WallShape Build(V5WallStyle style, Rect rect, int axis, bool allSides = false)
        {
            var look = LookOf(style);
            Points.Clear(); Normals.Clear(); Colors.Clear(); Uvs.Clear(); Triangles.Clear(); Stones.Clear();
            var shape = new V5WallShape
            {
                width = rect.width,
                depth = rect.height,
                cellsX = Mathf.Max(1, Mathf.CeilToInt(rect.width / V5WallShape.GridCell - .001f)),
                cellsZ = Mathf.Max(1, Mathf.CeilToInt(rect.height / V5WallShape.GridCell - .001f)),
            };
            shape.heights = new float[shape.cellsX * shape.cellsZ];
            LayOut(look, rect, axis);
            foreach (var stone in Stones) Stamp(shape, rect, stone);
            Vector2 centre = rect.center;
            Plinth(look, rect, centre);
            foreach (var stone in Stones) Top(look, stone, rect, centre, shape);
            Sides(look, rect, centre, allSides);
            Risers(look, centre, allSides);
            shape.mesh = Finish();
            return shape;
        }

        static void LayOut(Look look, Rect rect, int axis)
        {
            float a0 = axis == 0 ? rect.xMin : rect.yMin, a1 = axis == 0 ? rect.xMax : rect.yMax;
            float c0 = axis == 0 ? rect.yMin : rect.xMin, c1 = axis == 0 ? rect.yMax : rect.xMax;
            Lattice(c0, c1, look.rowSize, look.rowJitter, 0f, 101 + axis, Rows);
            for (int r = 0; r + 1 < Rows.Count; r++)
            {
                float across0 = Rows[r], across1 = Rows[r + 1];
                int row = Mathf.FloorToInt((across0 + across1) * .5f / look.rowSize);
                int seed = row * 131 + axis * 7 + 17;
                Lattice(a0, a1, look.stoneLength, look.stoneJitter, Hash(seed, 3) * look.stoneLength, seed, Cuts);
                for (int s = 0; s + 1 < Cuts.Count; s++)
                {
                    float along0 = Cuts[s], along1 = Cuts[s + 1];
                    float mid = (along0 + along1) * .5f;
                    int stoneIndex = Mathf.FloorToInt(mid / look.stoneLength);
                    float hash = Hash(seed + 5003, stoneIndex);
                    var stone = new Stone { hash = hash };
                    if (axis == 0) { stone.x0 = along0; stone.x1 = along1; stone.z0 = across0; stone.z1 = across1; }
                    else { stone.x0 = across0; stone.x1 = across1; stone.z0 = along0; stone.z1 = along1; }
                    stone.level = LevelAt(look, (stone.x0 + stone.x1) * .5f, (stone.z0 + stone.z1) * .5f, hash);
                    stone.top = stone.level == 0 ? look.low : stone.level == 1 ? look.mid : look.high;
                    Stones.Add(stone);
                }
            }
        }

        static int LevelAt(Look look, float x, float z, float hash)
        {
            float n = Noise(x / look.breakScale, z / look.breakScale) * .72f + Noise(x / look.breakScale * 3.1f + 19f, z / look.breakScale * 3.1f + 7f) * .28f;
            int level = n < look.breakLow ? 0 : n < look.breakMid ? 1 : 2;
            if (level > 0 && hash < look.looseChance) level--;
            return level;
        }

        static void Lattice(float lo, float hi, float size, float jitter, float phase, int seed, List<float> edges)
        {
            edges.Clear();
            edges.Add(lo);
            float least = size * .45f;
            int first = Mathf.FloorToInt((lo - phase) / size) - 1, last = Mathf.CeilToInt((hi - phase) / size) + 1;
            for (int i = first; i <= last; i++)
            {
                float at = phase + (i + (Hash(seed, i) - .5f) * jitter) * size;
                if (at - edges[edges.Count - 1] < least || hi - at < least) continue;
                edges.Add(at);
            }
            edges.Add(hi);
        }

        static void Stamp(V5WallShape shape, Rect rect, in Stone stone)
        {
            int ix0 = Mathf.FloorToInt((stone.x0 - rect.xMin) / V5WallShape.GridCell + .01f), ix1 = Mathf.CeilToInt((stone.x1 - rect.xMin) / V5WallShape.GridCell - .01f);
            int iz0 = Mathf.FloorToInt((stone.z0 - rect.yMin) / V5WallShape.GridCell + .01f), iz1 = Mathf.CeilToInt((stone.z1 - rect.yMin) / V5WallShape.GridCell - .01f);
            ix0 = Mathf.Clamp(ix0, 0, shape.cellsX); ix1 = Mathf.Clamp(ix1, 0, shape.cellsX);
            iz0 = Mathf.Clamp(iz0, 0, shape.cellsZ); iz1 = Mathf.Clamp(iz1, 0, shape.cellsZ);
            for (int z = iz0; z < iz1; z++)
            for (int x = ix0; x < ix1; x++)
                shape.heights[x + z * shape.cellsX] = stone.top;
            shape.tallest = Mathf.Max(shape.tallest, stone.top);
        }

        static void Plinth(Look look, Rect rect, Vector2 c)
        {
            var dark = look.bed;
            float y = look.plinth, x0 = rect.xMin - c.x, x1 = rect.xMax - c.x, z0 = rect.yMin - c.y, z1 = rect.yMax - c.y;
            Quad(new Vector3(x0, y, z0), new Vector3(x0, y, z1), new Vector3(x1, y, z1), new Vector3(x1, y, z0), dark, dark, dark, dark, Vector2.zero, 0);
        }

        static void Top(Look look, in Stone s, Rect rect, Vector2 c, V5WallShape shape)
        {
            float w = s.x1 - s.x0, d = s.z1 - s.z0, small = Mathf.Min(w, d);
            float bevel = Mathf.Min(look.bevel, small * .28f);
            int seed = Mathf.RoundToInt(s.hash * 1000000f);
            float outerY = s.top - look.drop;
            float innerY = s.top + (Hash(seed, 11) - .4f) * look.rise;
            int n = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                float cut = Hash(seed, 20 + corner) < look.chipChance ? Mathf.Min(look.chipSize * Mathf.Lerp(.45f, 1f, Hash(seed, 30 + corner)), small * .36f) : 0f;

                float x = corner < 2 ? s.x0 : s.x1, z = corner is 0 or 3 ? s.z0 : s.z1;
                float intoX = corner < 2 ? 1f : -1f, intoZ = corner is 0 or 3 ? 1f : -1f;
                if (cut <= 0f) { Ring(ref n, look, rect, c, shape, x, z, intoX, intoZ, bevel, outerY, innerY); continue; }

                bool alongXFirst = corner is 0 or 2;
                Ring(ref n, look, rect, c, shape, alongXFirst ? x + intoX * cut : x, alongXFirst ? z : z + intoZ * cut, intoX, intoZ, bevel, outerY, innerY);
                Ring(ref n, look, rect, c, shape, alongXFirst ? x : x + intoX * cut, alongXFirst ? z + intoZ * cut : z, intoX, intoZ, bevel, outerY, innerY);
            }

            Color tint = StoneTint(look, s.hash, seed, s.level, 1f);
            Vector2 uv = new(Hash(seed, 40), Hash(seed, 41));
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                Quad(OuterRing[i], OuterRing[next], InnerRing[next], InnerRing[i],
                    With(tint, OuterLight[i]), With(tint, OuterLight[next]), With(tint, InnerLight[next]), With(tint, InnerLight[i]), uv, look.uvTile);
            }

            if (look.dome > 0f)
            {

                Vector3 peak = Vector3.zero;
                for (int i = 0; i < n; i++) peak += InnerRing[i];
                peak = peak / n + new Vector3((Hash(seed, 44) - .5f) * small * .3f, look.dome * Mathf.Lerp(.6f, 1.3f, Hash(seed, 45)), (Hash(seed, 46) - .5f) * small * .3f);
                for (int i = 0; i < n; i++)
                {
                    int next = (i + 1) % n;
                    var facet = Vector3.Cross(InnerRing[i] - peak, InnerRing[next] - peak).normalized;
                    Triangle(peak, InnerRing[i], InnerRing[next], facet, With(tint, InnerLight[i]), With(tint, InnerLight[i]), With(tint, InnerLight[next]), uv, look.uvTile);
                }
                return;
            }

            var lean = new Vector3((Hash(seed, 42) - .5f) * look.tilt * 2f, 1f, (Hash(seed, 43) - .5f) * look.tilt * 2f).normalized;
            for (int i = 1; i + 1 < n; i++)
                Triangle(InnerRing[0], InnerRing[i], InnerRing[i + 1], lean, With(tint, InnerLight[0]), With(tint, InnerLight[i]), With(tint, InnerLight[i + 1]), uv, look.uvTile);
        }

        static void Ring(ref int n, Look look, Rect rect, Vector2 c, V5WallShape shape, float x, float z, float intoX, float intoZ, float bevel, float outerY, float innerY)
        {
            bool onEdge = Mathf.Abs(x - rect.xMin) < .001f || Mathf.Abs(x - rect.xMax) < .001f || Mathf.Abs(z - rect.yMin) < .001f || Mathf.Abs(z - rect.yMax) < .001f;
            float inX = x + intoX * bevel, inZ = z + intoZ * bevel;
            float lit = SelfShade(look, shape, inX - c.x, inZ - c.y, innerY);
            OuterRing[n] = new Vector3(x - c.x, outerY, z - c.y);
            InnerRing[n] = new Vector3(inX - c.x, innerY + (Hash(n + 90, Mathf.RoundToInt((inX + inZ * 7f) * 40f)) - .5f) * look.roughTop, inZ - c.y);
            OuterLight[n] = (byte)(255f * lit * (onEdge ? look.edge : look.mortar));
            InnerLight[n] = (byte)(255f * lit);
            n++;
        }

        static float SelfShade(Look look, V5WallShape shape, float x, float z, float y)
        {
            Vector3 toSun = ToSun;
            float flat = new Vector2(toSun.x, toSun.z).magnitude;
            if (flat < 1e-3f) return 1f;
            float dx = toSun.x / flat, dz = toSun.z / flat, climb = toSun.y / flat;
            for (float t = .2f; t < 1.5f; t += .25f)
                if (shape.HeightAt(x + dx * t, z + dz * t) > y + t * climb) return look.shade;
            return 1f;
        }

        static void Sides(Look look, Rect rect, Vector2 c, bool allSides)
        {
            OuterFace(look, Vector3.right, rect.yMin, c, 0);
            if (!allSides) return;
            OuterFace(look, Vector3.left, rect.yMax, c, 1);
            OuterFace(look, Vector3.forward, rect.xMax, c, 2);
            OuterFace(look, Vector3.back, rect.xMin, c, 3);
        }

        static void OuterFace(Look look, Vector3 along, float plane, Vector2 c, int side)
        {
            Runs.Clear();
            foreach (var s in Stones)
            {
                bool touches = side switch
                {
                    0 => Mathf.Abs(s.z0 - plane) < .001f,
                    1 => Mathf.Abs(s.z1 - plane) < .001f,
                    2 => Mathf.Abs(s.x1 - plane) < .001f,
                    _ => Mathf.Abs(s.x0 - plane) < .001f,
                };
                if (!touches) continue;
                float a = side < 2 ? s.x0 * along.x : s.z0 * along.z, b = side < 2 ? s.x1 * along.x : s.z1 * along.z;
                Runs.Add((Mathf.Min(a, b), Mathf.Max(a, b), s.top - look.drop, s.level));
            }
            Runs.Sort((p, q) => p.s0.CompareTo(q.s0));
            int start = 0;
            for (int i = 1; i <= Runs.Count; i++)
            {
                if (i < Runs.Count && Runs[i].level == Runs[start].level && Mathf.Abs(Runs[i].s0 - Runs[i - 1].s1) < .001f) continue;
                Face(look, along, plane, c, side, Runs[start].s0, Runs[i - 1].s1, 0f, Runs[start].top);
                start = i;
            }
        }

        static void Risers(Look look, Vector2 c, bool allSides)
        {
            for (int a = 0; a < Stones.Count; a++)
            {
                var high = Stones[a];
                for (int b = 0; b < Stones.Count; b++)
                {
                    var low = Stones[b];
                    if (low.top >= high.top) continue;
                    if (Mathf.Abs(low.z1 - high.z0) < .001f && Overlap(high.x0, high.x1, low.x0, low.x1, out float x0, out float x1))
                        Face(look, Vector3.right, high.z0, c, 0, x0, x1, low.top - look.drop, high.top - look.drop);
                    if (!allSides) continue;
                    if (Mathf.Abs(low.z0 - high.z1) < .001f && Overlap(high.x0, high.x1, low.x0, low.x1, out x0, out x1))
                        Face(look, Vector3.left, high.z1, c, 1, -x1, -x0, low.top - look.drop, high.top - look.drop);
                    if (Mathf.Abs(low.x0 - high.x1) < .001f && Overlap(high.z0, high.z1, low.z0, low.z1, out float z0, out float z1))
                        Face(look, Vector3.forward, high.x1, c, 2, z0, z1, low.top - look.drop, high.top - look.drop);
                    if (Mathf.Abs(low.x1 - high.x0) < .001f && Overlap(high.z0, high.z1, low.z0, low.z1, out z0, out z1))
                        Face(look, Vector3.back, high.x0, c, 3, -z1, -z0, low.top - look.drop, high.top - look.drop);
                }
            }
        }

        static bool Overlap(float a0, float a1, float b0, float b1, out float from, out float to)
        {
            from = Mathf.Max(a0, b0);
            to = Mathf.Min(a1, b1);
            return to - from > .01f;
        }

        static void Face(Look look, Vector3 along, float plane, Vector2 c, int side, float s0, float s1, float y0, float y1)
        {
            if (y1 - y0 < .04f || s1 - s0 < .02f) return;
            Vector3 outward = Vector3.Cross(Vector3.up, along);

            Vector3 origin = side < 2 ? new Vector3(-c.x, 0f, plane - c.y) : new Vector3(plane - c.x, 0f, -c.y);
            Lattice(y0, y1, look.courseHeight, look.courseJitter, 0f, 211 + side, Courses);
            for (int k = 0; k + 1 < Courses.Count; k++)
            {
                float low = Courses[k], high = Courses[k + 1];
                int course = Mathf.RoundToInt((low + high) * .5f / look.courseHeight * 2f);
                float band = 1f + (Hash(course, 77) - .5f) * 2f * look.courseValue;
                Lattice(s0, s1, look.faceLength, look.faceJitter, Hash(course, 5) * look.faceLength, 307 + course * 13 + side, Pieces);
                for (int p = 0; p + 1 < Pieces.Count; p++)
                {
                    float a = Pieces[p], b = Pieces[p + 1];
                    int seed = Mathf.RoundToInt((a + b) * 50f) * 37 + course * 101 + side;
                    float hash = Hash(seed, 1);
                    Color tint = StoneTint(look, hash, seed, 2, look.faceValue * band);
                    FaceStone(look, origin, along, outward, a, b, low, high, tint, seed);
                }
            }
        }

        static void FaceStone(Look look, Vector3 origin, Vector3 along, Vector3 outward, float a, float b, float low, float high, Color tint, int seed)
        {
            float bevel = Mathf.Min(look.faceBevel, Mathf.Min(b - a, high - low) * .3f);
            Vector3 up = Vector3.up;
            Vector3 At(float s, float y, float o) => origin + along * s + up * y + outward * o;
            byte Foot(float y, float scale) => (byte)(255f * scale * Mathf.Lerp(look.footAo, 1f, Mathf.Clamp01(y / look.footHeight)));
            OuterRing[0] = At(a, low, -look.faceRecess); OuterRing[1] = At(a, high, -look.faceRecess); OuterRing[2] = At(b, high, -look.faceRecess); OuterRing[3] = At(b, low, -look.faceRecess);
            InnerRing[0] = At(a + bevel, low + bevel, 0f); InnerRing[1] = At(a + bevel, high - bevel, 0f); InnerRing[2] = At(b - bevel, high - bevel, 0f); InnerRing[3] = At(b - bevel, low + bevel, 0f);
            byte outerLow = Foot(low, look.mortar), outerHigh = Foot(high, look.mortar), innerLow = Foot(low + bevel, 1f), innerHigh = Foot(high - bevel, 1f);
            OuterLight[0] = outerLow; OuterLight[1] = outerHigh; OuterLight[2] = outerHigh; OuterLight[3] = outerLow;
            InnerLight[0] = innerLow; InnerLight[1] = innerHigh; InnerLight[2] = innerHigh; InnerLight[3] = innerLow;
            Vector2 uv = new(Hash(seed, 40), Hash(seed, 41));
            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                QuadFlat(OuterRing[i], OuterRing[next], InnerRing[next], InnerRing[i],
                    With(tint, OuterLight[i]), With(tint, OuterLight[next]), With(tint, InnerLight[next]), With(tint, InnerLight[i]), uv, look.uvTile, along);
            }
            var lean = (outward + new Vector3((Hash(seed, 42) - .5f) * look.tilt, (Hash(seed, 43) - .5f) * look.tilt, 0f)).normalized;
            int first = Points.Count;
            for (int i = 0; i < 4; i++)
            {
                Points.Add(InnerRing[i]);
                Normals.Add(lean);
                Colors.Add(With(tint, InnerLight[i]));
                Uvs.Add(FaceUv(InnerRing[i], along, uv, look.uvTile));
            }
            Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
            Triangles.Add(first); Triangles.Add(first + 2); Triangles.Add(first + 3);
        }

        static Color StoneTint(Look look, float hash, int seed, int level, float value)
        {
            Color tint = Color.Lerp(look.stoneA, look.stoneB, Hash(seed, 50));
            if (Hash(seed, 51) < look.stainChance) tint = Color.Lerp(tint, look.stain, .35f + .5f * Hash(seed, 52));
            float shade = value * (1f + (hash - .5f) * 2f * look.valueJitter);
            if (level == 0) shade *= look.lowDark;
            else if (level == 1) shade *= look.midDark;
            return new Color(tint.r * shade, tint.g * shade, tint.b * shade, 1f);
        }

        static Color32 With(Color tint, byte light) =>
            new((byte)(Mathf.Clamp01(tint.r) * 255f), (byte)(Mathf.Clamp01(tint.g) * 255f), (byte)(Mathf.Clamp01(tint.b) * 255f), light);

        static Vector2 TopUv(Vector3 p, Vector2 offset, float tile) => tile > 0f ? new Vector2(p.x / tile + offset.x, p.z / tile + offset.y) : Vector2.zero;
        static Vector2 FaceUv(Vector3 p, Vector3 along, Vector2 offset, float tile) => new(Vector3.Dot(p, along) / tile + offset.x, p.y / tile + offset.y);

        static void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd, Vector2 uv, float tile)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.up;
            int first = Points.Count;
            Points.Add(a); Points.Add(b); Points.Add(c); Points.Add(d);
            Normals.Add(n); Normals.Add(n); Normals.Add(n); Normals.Add(n);
            Colors.Add(ca); Colors.Add(cb); Colors.Add(cc); Colors.Add(cd);
            Uvs.Add(TopUv(a, uv, tile)); Uvs.Add(TopUv(b, uv, tile)); Uvs.Add(TopUv(c, uv, tile)); Uvs.Add(TopUv(d, uv, tile));
            Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
            Triangles.Add(first); Triangles.Add(first + 2); Triangles.Add(first + 3);
        }

        static void QuadFlat(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color32 ca, Color32 cb, Color32 cc, Color32 cd, Vector2 uv, float tile, Vector3 along)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            n = n.sqrMagnitude > 1e-10f ? n.normalized : Vector3.Cross(Vector3.up, along);
            int first = Points.Count;
            Points.Add(a); Points.Add(b); Points.Add(c); Points.Add(d);
            Normals.Add(n); Normals.Add(n); Normals.Add(n); Normals.Add(n);
            Colors.Add(ca); Colors.Add(cb); Colors.Add(cc); Colors.Add(cd);
            Uvs.Add(FaceUv(a, along, uv, tile)); Uvs.Add(FaceUv(b, along, uv, tile)); Uvs.Add(FaceUv(c, along, uv, tile)); Uvs.Add(FaceUv(d, along, uv, tile));
            Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
            Triangles.Add(first); Triangles.Add(first + 2); Triangles.Add(first + 3);
        }

        static void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 n, Color32 ca, Color32 cb, Color32 cc, Vector2 uv, float tile)
        {
            int first = Points.Count;
            Points.Add(a); Points.Add(b); Points.Add(c);
            Normals.Add(n); Normals.Add(n); Normals.Add(n);
            Colors.Add(ca); Colors.Add(cb); Colors.Add(cc);
            Uvs.Add(TopUv(a, uv, tile)); Uvs.Add(TopUv(b, uv, tile)); Uvs.Add(TopUv(c, uv, tile));
            Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
        }

        static Mesh Finish()
        {
            var mesh = new Mesh { name = "V5 stone wall" };
            mesh.indexFormat = Points.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetVertices(Points);
            mesh.SetNormals(Normals);
            mesh.SetColors(Colors);
            mesh.SetUVs(0, Uvs);
            mesh.SetTriangles(Triangles, 0, true);
            mesh.UploadMeshData(true);
            return mesh;
        }

        internal static float Hash(int a, int b)
        {
            unchecked
            {
                uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u;
                h ^= h >> 15; h *= 0xC2B2AE3Du; h ^= h >> 13; h *= 0x27D4EB2Fu; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        static float Noise(float x, float z)
        {
            int ix = Mathf.FloorToInt(x), iz = Mathf.FloorToInt(z);
            float fx = x - ix, fz = z - iz;
            fx = fx * fx * (3f - 2f * fx);
            fz = fz * fz * (3f - 2f * fz);
            float a = Hash(ix, iz), b = Hash(ix + 1, iz), c = Hash(ix, iz + 1), d = Hash(ix + 1, iz + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fz);
        }
    }
}
