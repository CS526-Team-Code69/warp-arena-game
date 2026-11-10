using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The V2 day campaign's own ground (since 2026-11-06): a map larger than the classic arena, with terrain, laid out afresh from a seed
    /// at the start of every campaign run. Its pieces are the walls' rectangles every map already has (MapLayout.walls), so paths, spawns,
    /// shots, knockback and both art styles read them like any map wall: ridges (a long wall, sometimes with a kink), broken walls (a row
    /// of short pieces with gaps a body walks through), rock forests (a cluster of pillars), L-shaped outcrops and lone boulders, plus a
    /// few quicksand pools between them. The ground the campaign needs stays open (Plan.keep: the base and the ship, the crash site and
    /// the Day 0 route, the black hole and a band from the base to it). The result is checked on a one-cell grid laid like MapGrid's:
    /// every open cell must be reachable on foot from the base door, so no wall closes off any ground; a wall that does is taken away,
    /// until the check passes (with no wall left at worst). Only the V2 campaign calls this; the classic maps are the saved layouts.
    /// </summary>
    public static class CampaignTerrain
    {
        // Shape of the terrain, in cells. These are level geometry, not balance numbers.
        const float Rim = 4f;              // walls keep this far from the map's edge, so a corridor runs all round inside it
        const float FeatureGap = 5f;       // between two features: room for the largest walking body (Brute, Stomper: 2 cells across)
        const float MinGap = 3f;           // between the pieces of one broken wall or rock forest: a 2-cell body still fits through
        const float KeepMargin = 1f;       // walls keep at least this far out of the ground that stays open
        const float PoolGap = 2f;          // quicksand keeps this far from walls (and twice this from other pools)
        const float Body = .6f;            // the walking body the connectivity check fits through gaps (MapGrid blocks 0.5 round a wall)
        const float AreaPerFeature = 800f; // one feature for this many cells of map
        const int MinFeatures = 8, MaxFeatures = 90, MaxRects = 260;
        const float AreaPerPool = 7000f;
        const int MinPools = 2, MaxPools = 8;

        /// <summary>Ground that must stay open: a circle (a == b) or a band along the segment from a to b, radius wide on each side.</summary>
        public readonly struct Keep
        {
            public readonly Vector2 a, b;
            public readonly float radius;
            Keep(Vector2 a, Vector2 b, float radius) { this.a = a; this.b = b; this.radius = radius; }
            public static Keep Circle(Vector2 centre, float radius) => new(centre, centre, radius);
            public static Keep Band(Vector2 from, Vector2 to, float halfWidth) => new(from, to, halfWidth);
            /// <summary>How far the rect stays outside this ground; negative where it reaches in.</summary>
            public float Clearance(Rect rect) => SegmentToRect(rect, a, b) - radius;
            public float Clearance(Vector2 point) => PointToSegment(point, a, b) - radius;
        }

        /// <summary>What the campaign asks of its map. Coordinates are (x, z) in cells, the map centred on (0, 0).</summary>
        public sealed class Plan
        {
            public Vector2Int size = new(200, 140);
            public int seed;
            /// <summary>Ground no wall or pool may take.</summary>
            public readonly List<Keep> keep = new();
            /// <summary>Walls the campaign adds itself (the base, the ship pad): they block the check and are never taken away.</summary>
            public readonly List<Rect> fixedWalls = new();
            /// <summary>Where walking starts (the base door): every open cell must be reachable from here.</summary>
            public Vector2 start;
            /// <summary>The black hole and the radius of its pull ring: blocked in the check.</summary>
            public Vector2 hole;
            public float holePull;
            /// <summary>False when the map has no wall objects to copy: the layout then gets no terrain walls.</summary>
            public bool walls = true;
            public bool sand = true;
        }

        /// <summary>What Generate laid out: the terrain's wall rects, its quicksand pools, and how it got there.</summary>
        public sealed class Result
        {
            public readonly List<Rect> walls = new();
            public readonly List<MapLayout.SandPool> pools = new();
            /// <summary>Features placed, and wall rects taken away again to keep every open cell reachable.</summary>
            public int features, removed;
            /// <summary>Every open cell can be walked to from the plan's start (always true once Generate returns).</summary>
            public bool connected;
            public int width, height;
            public string Report(int seed) => $"map {width} x {height}, seed {seed}: {walls.Count} wall rects in {features} features " +
                                              $"({removed} taken away to keep every open cell reachable), {pools.Count} quicksand pools";
        }

        /// <summary>Builds the campaign's layout: a runtime copy of template (the classic map's own settings, such as the quicksand speed)
        /// with the plan's size, the terrain walls, the pools and the black hole as its one anchor. report says what was built.</summary>
        public static MapLayout Build(MapLayout template, Plan plan, out string report)
        {
            var result = Generate(plan);
            var layout = template ? Object.Instantiate(template) : ScriptableObject.CreateInstance<MapLayout>();
            layout.name = (template ? template.name : "Map") + " (campaign)";
            layout.width = result.width;
            layout.height = result.height;
            layout.walls = result.walls.ToArray();
            layout.holeAnchors = new[] { plan.hole };
            layout.quicksand = result.pools.ToArray();
            layout.playerStart = new Vector3(plan.start.x, 0f, plan.start.y);
            report = result.Report(plan.seed);
            return layout;
        }

        /// <summary>The terrain of a plan, without touching any object (the same seed always gives the same terrain): features at random
        /// spots that fit, then walls taken away until every open cell is reachable, then the pools.</summary>
        public static Result Generate(Plan plan)
        {
            var rng = new System.Random(plan.seed);
            var result = new Result { width = Mathf.Max(20, plan.size.x), height = Mathf.Max(20, plan.size.y) };
            int width = result.width, height = result.height;
            float hw = width * .5f, hh = height * .5f;
            var walls = result.walls;
            if (plan.walls)
            {
                int target = Mathf.Clamp(Mathf.RoundToInt(width * height / AreaPerFeature), MinFeatures, MaxFeatures);
                var parts = new List<Rect>();
                for (int attempt = 0; attempt < target * 60 && result.features < target && walls.Count < MaxRects; attempt++)
                {
                    parts.Clear();
                    var centre = new Vector2(Range(rng, -hw + Rim, hw - Rim), Range(rng, -hh + Rim, hh - Rim));
                    MakeFeature(rng, centre, parts);
                    if (parts.Count == 0 || walls.Count + parts.Count > MaxRects || !Fits(parts, walls, plan, hw, hh)) continue;
                    walls.AddRange(parts);
                    result.features++;
                }
            }
            result.removed = Connect(walls, plan, width, height, out result.connected);
            if (plan.sand) PlacePools(rng, walls, plan, hw, hh, result.pools);
            return result;
        }

        /// <summary>Whether every open cell of the layout (as Generate checks it) can be walked to from the plan's start; for checks.</summary>
        public static bool AllReachable(MapLayout layout, Plan plan) => layout && AllReachable(layout.walls, layout.width, layout.height, plan);

        public static bool AllReachable(IList<Rect> layoutWalls, int width, int height, Plan plan)
        {
            var walls = new List<Rect>(layoutWalls ?? new Rect[0]);
            int count = width * height;
            var blocked = new bool[count];
            var seen = new bool[count];
            Mark(blocked, walls, plan, width, height);
            int start = NearestOpen(blocked, plan.start, width, height);
            if (start < 0) return false;
            Flood(blocked, seen, new int[count], start, width, height);
            return FirstStray(blocked, seen) < 0;
        }

        // ───── Features: u runs along a feature, v across it; turn lays u along z instead of x ─────

        static void MakeFeature(System.Random rng, Vector2 c, List<Rect> parts)
        {
            bool turn = rng.NextDouble() < .5;
            double roll = rng.NextDouble();
            if (roll < .26) Ridge(rng, c, turn, parts);
            else if (roll < .52) BrokenWall(rng, c, turn, parts);
            else if (roll < .72) RockForest(rng, c, parts);
            else if (roll < .88) Outcrop(rng, c, turn, parts);
            else Boulder(rng, c, parts);
        }

        static Rect Piece(Vector2 c, float u0, float u1, float v0, float v1, bool turn) => turn
            ? Rect.MinMaxRect(c.x + Mathf.Min(v0, v1), c.y + Mathf.Min(u0, u1), c.x + Mathf.Max(v0, v1), c.y + Mathf.Max(u0, u1))
            : Rect.MinMaxRect(c.x + Mathf.Min(u0, u1), c.y + Mathf.Min(v0, v1), c.x + Mathf.Max(u0, u1), c.y + Mathf.Max(v0, v1));

        /// <summary>A long wall; often a shorter one goes on from its end, shifted sideways but still touching it (a kink).</summary>
        static void Ridge(System.Random rng, Vector2 c, bool turn, List<Rect> parts)
        {
            float length = Range(rng, 12f, 22f), thick = Range(rng, 2f, 3f);
            parts.Add(Piece(c, -length * .5f, length * .5f, -thick * .5f, thick * .5f, turn));
            if (rng.NextDouble() >= .55) return;
            float more = Range(rng, 6f, 12f), jog = (rng.NextDouble() < .5 ? -1f : 1f) * Range(rng, thick * .4f, thick * .8f);
            parts.Add(Piece(c, length * .5f, length * .5f + more, jog - thick * .5f, jog + thick * .5f, turn));
        }

        /// <summary>A row of three to five short pieces with walkable gaps; the row wanders a little sideways.</summary>
        static void BrokenWall(System.Random rng, Vector2 c, bool turn, List<Rect> parts)
        {
            int count = rng.Next(3, 6);
            float thick = Range(rng, 1.5f, 2f), total = 0f;
            var lengths = new float[count];
            var gaps = new float[count];
            for (int i = 0; i < count; i++)
            {
                lengths[i] = Range(rng, 3f, 6f);
                gaps[i] = i < count - 1 ? Range(rng, MinGap, 4.5f) : 0f;
                total += lengths[i] + gaps[i];
            }
            float u = -total * .5f, drift = 0f;
            for (int i = 0; i < count; i++)
            {
                parts.Add(Piece(c, u, u + lengths[i], drift - thick * .5f, drift + thick * .5f, turn));
                u += lengths[i] + gaps[i];
                drift += Range(rng, -.6f, .6f);
            }
        }

        /// <summary>Five to eight pillars scattered in a disc, each at least MinGap from the others; fewer than three is no forest.</summary>
        static void RockForest(System.Random rng, Vector2 c, List<Rect> parts)
        {
            int want = rng.Next(5, 9);
            float reach = Range(rng, 5f, 8f);
            for (int attempt = 0; attempt < want * 8 && parts.Count < want; attempt++)
            {
                float size = Range(rng, 1.5f, 2.8f), depth = size * Range(rng, .8f, 1.25f);
                float angle = Range(rng, 0f, Mathf.PI * 2f), r = reach * Mathf.Sqrt((float)rng.NextDouble());
                var at = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
                var rect = new Rect(at.x - size * .5f, at.y - depth * .5f, size, depth);
                bool clear = true;
                foreach (var other in parts)
                    if (RectGap(rect, other) < MinGap) { clear = false; break; }
                if (clear) parts.Add(rect);
            }
            if (parts.Count < 3) parts.Clear();
        }

        /// <summary>An L: a long arm and a short arm meeting at a corner, open on its two inner sides, so nothing is closed in.</summary>
        static void Outcrop(System.Random rng, Vector2 c, bool turn, List<Rect> parts)
        {
            float a = Range(rng, 7f, 11f), b = Range(rng, 5f, 8f), thick = Range(rng, 2f, 3f);
            float su = rng.NextDouble() < .5 ? -1f : 1f, sv = rng.NextDouble() < .5 ? -1f : 1f;
            parts.Add(Piece(c, 0f, su * a, 0f, sv * thick, turn));
            parts.Add(Piece(c, 0f, su * thick, sv * thick, sv * (thick + b), turn));
        }

        static void Boulder(System.Random rng, Vector2 c, List<Rect> parts)
        {
            float w = Range(rng, 4f, 7f), h = Range(rng, 3f, 5f);
            if (rng.NextDouble() < .5) (w, h) = (h, w);
            parts.Add(new Rect(c.x - w * .5f, c.y - h * .5f, w, h));
        }

        /// <summary>A feature fits where every piece lies inside the rim, out of the open ground, and FeatureGap from every other wall.</summary>
        static bool Fits(List<Rect> parts, List<Rect> walls, Plan plan, float hw, float hh)
        {
            foreach (var rect in parts)
            {
                if (rect.xMin < -hw + Rim || rect.xMax > hw - Rim || rect.yMin < -hh + Rim || rect.yMax > hh - Rim) return false;
                if ((rect.center - plan.hole).magnitude < plan.holePull + KeepMargin + rect.size.magnitude * .5f) return false;
                foreach (var keep in plan.keep)
                    if (keep.Clearance(rect) < KeepMargin) return false;
                foreach (var wall in plan.fixedWalls)
                    if (RectGap(rect, wall) < FeatureGap) return false;
                foreach (var wall in walls)
                    if (RectGap(rect, wall) < FeatureGap) return false;
            }
            return true;
        }

        /// <summary>A few round quicksand pools on open ground, PoolGap from walls and out of the ground that stays open.</summary>
        static void PlacePools(System.Random rng, List<Rect> walls, Plan plan, float hw, float hh, List<MapLayout.SandPool> pools)
        {
            int want = Mathf.Clamp(Mathf.RoundToInt(hw * hh * 4f / AreaPerPool), MinPools, MaxPools);
            for (int attempt = 0; attempt < want * 40 && pools.Count < want; attempt++)
            {
                float radius = Range(rng, 3.5f, 6f);
                var at = new Vector2(Range(rng, -hw + Rim + radius, hw - Rim - radius), Range(rng, -hh + Rim + radius, hh - Rim - radius));
                bool clear = (at - plan.hole).magnitude >= plan.holePull + radius + PoolGap * 2f;
                foreach (var keep in plan.keep)
                    if (clear && keep.Clearance(at) < radius + KeepMargin) clear = false;
                foreach (var wall in plan.fixedWalls)
                    if (clear && PointToRect(at, wall) < radius + PoolGap) clear = false;
                foreach (var wall in walls)
                    if (clear && PointToRect(at, wall) < radius + PoolGap) clear = false;
                foreach (var pool in pools)
                    if (clear && (at - pool.centre).magnitude < radius + pool.radius + PoolGap * 2f) clear = false;
                if (clear) pools.Add(new MapLayout.SandPool { centre = at, radius = radius });
            }
        }

        // ───── Connectivity: a one-cell grid laid like MapGrid's (cell centres at -size / 2 + i + 0.5) ─────

        /// <summary>Takes walls away until every open cell can be walked to from the plan's start: the wall nearest a cell that cannot be
        /// reached is part of what closes it off. Returns how many went; connected says whether the check passed (it always does once no
        /// terrain wall is left, as the base, the ship pad and the black hole's ring cannot close off ground on their own).</summary>
        static int Connect(List<Rect> walls, Plan plan, int width, int height, out bool connected)
        {
            int count = width * height, removed = 0;
            var blocked = new bool[count];
            var seen = new bool[count];
            var queue = new int[count];
            connected = false;
            while (true)
            {
                Mark(blocked, walls, plan, width, height);
                int start = NearestOpen(blocked, plan.start, width, height);
                if (start < 0) return removed;
                System.Array.Clear(seen, 0, count);
                Flood(blocked, seen, queue, start, width, height);
                int stray = FirstStray(blocked, seen);
                if (stray < 0) { connected = true; return removed; }
                if (walls.Count == 0) return removed;
                var at = CellCentre(stray, width, height);
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < walls.Count; i++)
                {
                    float d = PointToRect(at, walls[i]);
                    if (d < best) { best = d; nearest = i; }
                }
                walls.RemoveAt(nearest);
                removed++;
            }
        }

        static Vector2 CellCentre(int index, int width, int height) => new(-width * .5f + index % width + .5f, -height * .5f + index / width + .5f);

        /// <summary>Blocked: a body would stand off the map, against a wall (the campaign's own included) or in the black hole's pull.</summary>
        static void Mark(bool[] blocked, List<Rect> walls, Plan plan, int width, int height)
        {
            float hw = width * .5f, hh = height * .5f, pull = plan.holePull * plan.holePull;
            for (int i = 0; i < blocked.Length; i++)
            {
                var p = CellCentre(i, width, height);
                blocked[i] = Mathf.Abs(p.x) > hw - Body || Mathf.Abs(p.y) > hh - Body || (p - plan.hole).sqrMagnitude < pull;
            }
            foreach (var rect in walls) Stamp(blocked, rect, width, height);
            foreach (var rect in plan.fixedWalls) Stamp(blocked, rect, width, height);
        }

        static void Stamp(bool[] blocked, Rect rect, int width, int height)
        {
            float hw = width * .5f, hh = height * .5f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(rect.xMin - Body + hw - .5f)), x1 = Mathf.Min(width - 1, Mathf.CeilToInt(rect.xMax + Body + hw - .5f));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(rect.yMin - Body + hh - .5f)), z1 = Mathf.Min(height - 1, Mathf.CeilToInt(rect.yMax + Body + hh - .5f));
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                float px = -hw + x + .5f, pz = -hh + z + .5f;
                if (px >= rect.xMin - Body && px <= rect.xMax + Body && pz >= rect.yMin - Body && pz <= rect.yMax + Body) blocked[z * width + x] = true;
            }
        }

        static int NearestOpen(bool[] blocked, Vector2 point, int width, int height)
        {
            int cx = Mathf.FloorToInt(point.x + width * .5f), cz = Mathf.FloorToInt(point.y + height * .5f);
            for (int r = 0; r <= 8; r++)
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                int x = cx + dx, z = cz + dz;
                if (x < 0 || z < 0 || x >= width || z >= height) continue;
                if (!blocked[z * width + x]) return z * width + x;
            }
            return -1;
        }

        static void Flood(bool[] blocked, bool[] seen, int[] queue, int start, int width, int height)
        {
            int first = 0, last = 0;
            seen[start] = true;
            queue[last++] = start;
            while (first < last)
            {
                int at = queue[first++], x = at % width, z = at / width;
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + (k == 0 ? -1 : k == 1 ? 1 : 0), nz = z + (k == 2 ? -1 : k == 3 ? 1 : 0);
                    if (nx < 0 || nz < 0 || nx >= width || nz >= height) continue;
                    int next = nz * width + nx;
                    if (blocked[next] || seen[next]) continue;
                    seen[next] = true;
                    queue[last++] = next;
                }
            }
        }

        /// <summary>An open cell the flood did not reach, or -1 when it reached them all.</summary>
        static int FirstStray(bool[] blocked, bool[] seen)
        {
            for (int i = 0; i < blocked.Length; i++)
                if (!blocked[i] && !seen[i]) return i;
            return -1;
        }

        // ───── Geometry ─────

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);

        /// <summary>The gap between two rects: 0 where they touch or overlap.</summary>
        static float RectGap(Rect a, Rect b)
        {
            float dx = Mathf.Max(0f, Mathf.Max(a.xMin - b.xMax, b.xMin - a.xMax));
            float dz = Mathf.Max(0f, Mathf.Max(a.yMin - b.yMax, b.yMin - a.yMax));
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float PointToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(Mathf.Max(r.xMin - p.x, 0f), p.x - r.xMax), dz = Mathf.Max(Mathf.Max(r.yMin - p.y, 0f), p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        static float PointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        /// <summary>Distance from the segment (a point when a == b) to the rect, 0 where they meet.</summary>
        static float SegmentToRect(Rect r, Vector2 a, Vector2 b)
        {
            if (SegmentHits(a, b, r)) return 0f;
            float near = Mathf.Min(PointToRect(a, r), PointToRect(b, r));
            near = Mathf.Min(near, PointToSegment(new Vector2(r.xMin, r.yMin), a, b));
            near = Mathf.Min(near, PointToSegment(new Vector2(r.xMax, r.yMin), a, b));
            near = Mathf.Min(near, PointToSegment(new Vector2(r.xMin, r.yMax), a, b));
            return Mathf.Min(near, PointToSegment(new Vector2(r.xMax, r.yMax), a, b));
        }

        /// <summary>Liang-Barsky: whether the segment from a to b crosses the rect (a point: whether it lies inside).</summary>
        static bool SegmentHits(Vector2 a, Vector2 b, Rect r)
        {
            Vector2 d = b - a;
            float enter = 0f, leave = 1f;
            return Clip(-d.x, a.x - r.xMin, ref enter, ref leave) && Clip(d.x, r.xMax - a.x, ref enter, ref leave) &&
                   Clip(-d.y, a.y - r.yMin, ref enter, ref leave) && Clip(d.y, r.yMax - a.y, ref enter, ref leave);
        }

        static bool Clip(float p, float q, ref float enter, ref float leave)
        {
            if (Mathf.Abs(p) < 1e-6f) return q >= 0f;
            float t = q / p;
            if (p < 0f)
            {
                if (t > leave) return false;
                if (t > enter) enter = t;
            }
            else
            {
                if (t < enter) return false;
                if (t < leave) leave = t;
            }
            return true;
        }
    }
}
