using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>One shared integration field per gravity policy; no path allocation for each enemy. Two body widths share the grid: a
    /// slim body's fields keep SlimReach clear of the walls and a wide body's (the Brute, the Stomper, the Rammer, the Bloater) WideReach,
    /// so the way a wide body is sent never runs closer to a wall than the body can stand.</summary>
    public sealed class MapGrid
    {
        /// <summary>How far a cell's centre must lie from a wall to be open ground for a slim body and for a wide one. A body with a
        /// radius above SlimReach (and a little) follows the wide fields.</summary>
        public const float SlimReach = .5f, WideReach = 1f;
        const float WideFromRadius = SlimReach + .05f;
        // The raiders' field starts on the open ground this far past a layer's reach from the base, so a cell's centre always falls in it.
        const float RaidRingWidth = 1.1f;

        /// <summary>The cells one body width may use and the fields counted over them.</summary>
        sealed class Layer
        {
            public readonly float reach;
            public readonly bool[] blocked, openSafe, openFast;
            public readonly int[] safeField, fastField;
            public int[] raidField;
            // The safe and the fast field start round their target, not at it: it stands on a cell this layer's body may not be
            // sent to (SeedAround), so from those cells the way goes straight at it (Direction).
            public bool safeAround, fastAround;
            // How far from the target the cells it starts from lie (the nearest and AroundSlack).
            public float safeAroundReach, fastAroundReach;

            public Layer(int count, float reach, bool[] blocked)
            {
                this.reach = reach;
                this.blocked = blocked;
                openSafe = new bool[count];
                openFast = new bool[count];
                safeField = new int[count];
                fastField = new int[count];
            }

            public bool Open(int cell, bool allowGravity) => allowGravity ? openFast[cell] : openSafe[cell];

            public bool Around(bool allowGravity) => allowGravity ? fastAround : safeAround;

            public float AroundReach(bool allowGravity) => allowGravity ? fastAroundReach : safeAroundReach;

            public void SetAround(bool allowGravity, bool around, float reach = 0f)
            {
                if (allowGravity) { fastAround = around; fastAroundReach = reach; }
                else { safeAround = around; safeAroundReach = reach; }
            }
        }

        readonly int width, height;
        readonly Vector2 origin;
        // The slim layer's blocked cells keep their own field (checks of broken walls read it by this name).
        readonly bool[] blocked, gravity;
        readonly int[] queue;
        readonly Layer slim, wide;
        readonly Vector2Int[] offsets = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
        const int LookAheadCells = 8;
        // A target on a blocked cell: the field starts from the open cells within AroundReachCells of it that see it, the nearest
        // and those no more than AroundSlack farther.
        const int AroundReachCells = 10;
        const float AroundSlack = .75f;
        public int Width => width;
        public int Height => height;

        public MapGrid(int width, int height)
        {
            this.width = width;
            this.height = height;
            origin = new Vector2(-width * 0.5f, -height * 0.5f);
            int count = width * height;
            gravity = new bool[count];
            queue = new int[count];
            blocked = new bool[count];
            slim = new Layer(count, SlimReach, blocked);
            wide = new Layer(count, WideReach, new bool[count]);
        }

        Layer For(float radius) => radius > WideFromRadius ? wide : slim;

        public int Index(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x - origin.x);
            int z = Mathf.FloorToInt(position.z - origin.y);
            return x < 0 || z < 0 || x >= width || z >= height ? -1 : z * width + x;
        }

        public Vector3 Centre(int index) => new Vector3(origin.x + index % width + 0.5f, 0f, origin.y + index / width + 0.5f);

        public void Rebuild(WorldMap map)
        {
            // V2 day campaign (since 2026-11-06): its larger map with many terrain walls stamps each wall once (StampBlocked) instead of
            // testing every wall at every cell; the cells come out the same. The classic maps keep the loop.
            if (map.CampaignMapApplied) StampBlocked(map);
            else
                for (int i = 0; i < gravity.Length; i++)
                {
                    Vector3 point = Centre(i);
                    bool hole = map.IsHole(point);
                    slim.blocked[i] = hole || !map.Contains(point, slim.reach) || map.IsWall(point, slim.reach);
                    wide.blocked[i] = hole || !map.Contains(point, wide.reach) || map.IsWall(point, wide.reach);
                    gravity[i] = map.GetPull(point).sqrMagnitude > 0.01f;
                }
            MarkOpen(slim);
            MarkOpen(wide);
        }

        /// <summary>Open cells of a layer: no blocked cell next to them (and for the safe fields no gravity either), so a straight line
        /// over open cells stays clear of the walls.</summary>
        void MarkOpen(Layer layer)
        {
            for (int i = 0; i < gravity.Length; i++)
            {
                int x = i % width, z = i / width;
                bool nearBlocked = false, nearGravity = false;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (!Inside(nx, nz)) { nearBlocked = true; continue; }
                    int cell = nz * width + nx;
                    nearBlocked |= layer.blocked[cell];
                    nearGravity |= gravity[cell];
                }
                layer.openFast[i] = !nearBlocked;
                layer.openSafe[i] = !nearBlocked && !nearGravity;
            }
        }

        /// <summary>The V2 campaign's Rebuild: the same blocked and gravity cells as the per-cell loop (WorldMap.Contains and IsWall with
        /// the layer's reach, whose rect test includes the edges and whose temporary walls test their collider's bounds at the wall's
        /// height, IsHole and GetPull), with each wall visiting only the cells round it.</summary>
        void StampBlocked(WorldMap map)
        {
            for (int i = 0; i < gravity.Length; i++)
            {
                Vector3 point = Centre(i);
                bool hole = map.IsHole(point);
                slim.blocked[i] = hole || !map.Contains(point, slim.reach);
                wide.blocked[i] = hole || !map.Contains(point, wide.reach);
                gravity[i] = map.GetPull(point).sqrMagnitude > 0.01f;
            }
            foreach (var rect in map.ExtraWallRects) { StampRect(slim, rect); StampRect(wide, rect); }
            if (map.layout && map.layout.walls != null) foreach (var rect in map.layout.walls) { StampRect(slim, rect); StampRect(wide, rect); }
            if (map.temporaryWalls == null) return;
            foreach (var wall in map.temporaryWalls)
            {
                if (!wall || !wall.gameObject.activeInHierarchy || !wall.TryGetComponent(out Collider body)) continue;
                Bounds bounds = body.bounds;
                float y = wall.transform.position.y;
                Range(bounds.min.x, bounds.max.x, width, origin.x, out int x0, out int x1);
                Range(bounds.min.z, bounds.max.z, height, origin.y, out int z0, out int z1);
                for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    int i = z * width + x;
                    Vector3 point = Centre(i);
                    if (bounds.Contains(new Vector3(point.x, y, point.z))) slim.blocked[i] = wide.blocked[i] = true;
                }
            }
        }

        void StampRect(Layer layer, Rect rect)
        {
            float margin = layer.reach;
            float xMin = rect.xMin - margin, xMax = rect.xMax + margin, zMin = rect.yMin - margin, zMax = rect.yMax + margin;
            Range(xMin, xMax, width, origin.x, out int x0, out int x1);
            Range(zMin, zMax, height, origin.y, out int z0, out int z1);
            for (int z = z0; z <= z1; z++)
            for (int x = x0; x <= x1; x++)
            {
                int i = z * width + x;
                Vector3 point = Centre(i);
                if (point.x >= xMin && point.x <= xMax && point.z >= zMin && point.z <= zMax) layer.blocked[i] = true;
            }
        }

        /// <summary>The cells whose centres may lie between lo and hi along one axis, a cell more each way for rounding; the exact test follows.</summary>
        static void Range(float lo, float hi, int cells, float start, out int first, out int last)
        {
            first = Mathf.Max(0, Mathf.FloorToInt(lo - start - .5f) - 1);
            last = Mathf.Min(cells - 1, Mathf.CeilToInt(hi - start - .5f) + 1);
        }

        public void Integrate(Vector3 target)
        {
            int goal = Index(target);
            Fill(slim, slim.safeField, goal, false, target);
            Fill(slim, slim.fastField, goal, true, target);
            Fill(wide, wide.safeField, goal, false, target);
            Fill(wide, wide.fastField, goal, true, target);
        }

        void Fill(Layer layer, int[] field, int goal, bool allowGravity, Vector3 chased)
        {
            var blocked = layer.blocked;
            Array.Fill(field, int.MaxValue);
            layer.SetAround(allowGravity, false);
            if (goal < 0) return;
            if (!allowGravity && gravity[goal])
            {
                int nearest = -1;
                float best = float.MaxValue;
                Vector3 target = Centre(goal);
                for (int i = 0; i < field.Length; i++)
                {
                    if (blocked[i] || gravity[i]) continue;
                    float distance = (Centre(i) - target).sqrMagnitude;
                    if (distance < best) { best = distance; nearest = i; }
                }
                goal = nearest;
                if (goal < 0) return;
            }
            int first = 0, last = 0;
            if (blocked[goal])
            {
                // The target stands where this layer's body may not be sent (against a wall, in a gap narrower than the margin, in
                // a nook). A field seeded on that cell alone may spread nowhere, and then no enemy on the map is given a way: the
                // field starts from the open cells nearest it instead.
                last = SeedAround(layer, field, goal, allowGravity, chased, out float reach);
                if (last == 0) return;
                layer.SetAround(allowGravity, true, reach);
            }
            else
            {
                field[goal] = 0;
                queue[last++] = goal;
            }
            // A target inside gravity is a valid seed: safe enemies stop at its rim rather than freeze everywhere.
            while (first < last)
            {
                int at = queue[first++];
                int x = at % width, z = at / width;
                foreach (var delta in offsets)
                {
                    int nx = x + delta.x, nz = z + delta.y;
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
                    int next = nz * width + nx;
                    if (blocked[next] || (!allowGravity && gravity[next]) || field[next] != int.MaxValue) continue;
                    field[next] = field[at] + 1;
                    queue[last++] = next;
                }
            }
        }

        /// <summary>
        /// Starts a field round a target on a blocked cell (Fill): the open cells within AroundReachCells of it with no wall between
        /// them and it, the nearest and those no more than AroundSlack farther (so both sides of a gap it stands in), at 0. Where no
        /// open cell sees it, the nearest open cells all the same. Returns how many cells were put in the queue.
        /// </summary>
        int SeedAround(Layer layer, int[] field, int goal, bool allowGravity, Vector3 target, out float reach)
        {
            int count = SeedAround(layer, field, goal, allowGravity, target, true, out reach);
            return count > 0 ? count : SeedAround(layer, field, goal, allowGravity, target, false, out reach);
        }

        int SeedAround(Layer layer, int[] field, int goal, bool allowGravity, Vector3 target, bool seen, out float reach)
        {
            reach = 0f;
            int gx = goal % width, gz = goal / width;
            Vector3 eye = Query.AtCastHeight(target);
            float nearest = float.MaxValue;
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass == 1) reach = Mathf.Sqrt(nearest) + AroundSlack;
                float within = pass == 0 ? float.MaxValue : reach * reach;
                int count = 0;
                for (int dz = -AroundReachCells; dz <= AroundReachCells; dz++)
                for (int dx = -AroundReachCells; dx <= AroundReachCells; dx++)
                {
                    int x = gx + dx, z = gz + dz;
                    if (!Inside(x, z)) continue;
                    int cell = z * width + x;
                    float distance = dx * dx + dz * dz;
                    if (layer.blocked[cell] || !allowGravity && gravity[cell] || distance > within) continue;
                    // The first pass looks only at cells nearer than the nearest so far, so few lines of sight are cast.
                    if (pass == 0 && distance >= nearest) continue;
                    if (seen && Query.WallBetween(Query.AtCastHeight(Centre(cell)), eye, out _)) continue;
                    if (pass == 0) { nearest = distance; continue; }
                    field[cell] = 0;
                    queue[count++] = cell;
                }
                if (pass == 0 && nearest == float.MaxValue) return 0;
                if (pass == 1) return count;
            }
            return 0;
        }

        /// <param name="radius">The walking body's radius: a wide body follows the wide fields, and the slim ones where the wide fields
        /// do not reach it (a gap it fits by a hair, ground closed in by the wider margin).</param>
        public Vector3 Direction(Vector3 position, Vector3 target, bool allowGravity, float radius = SlimReach)
        {
            int index = Index(position);
            if (index < 0) return Query.Flat(target - position).normalized;
            var layer = For(radius);
            if (Direction(layer, index, position, target, allowGravity, out var way)) return way;
            if (layer != slim && Direction(slim, index, position, target, allowGravity, out way)) return way;
            // No field reaches this body (the open ground it stands on does not join the ground round what it chases): it goes
            // straight at it and its walk slides it along the walls (WorldMap.WalkStep), rather than standing still.
            return Query.Flat(target - position).normalized;
        }

        /// <summary>The way along a layer's field. False when the field does not reach this cell or any next to it, so the layer has
        /// nothing to say here; true with no direction at the field's end (a safe enemy at a gravity ring's rim).</summary>
        bool Direction(Layer layer, int index, Vector3 position, Vector3 target, bool allowGravity, out Vector3 way)
        {
            way = Vector3.zero;
            if (HasClearPath(layer, position, target, allowGravity)) { way = Query.Flat(target - position).normalized; return true; }
            if (layer.Around(allowGravity) && Query.Flat(target - position).magnitude <= layer.AroundReach(allowGravity) + AroundSlack
                && (layer.blocked[index] || (allowGravity ? layer.fastField : layer.safeField)[index] == 0
                    || !Query.WallBetween(Query.AtCastHeight(position), Query.AtCastHeight(target), out _)))
            {
                // Within the reach of the cells the field starts round a blocked target, on one of those cells, on a cell closed to a
                // body's centre (the mouth of the gap it stands in) or with nothing between: straight at it, as near as the walk fits
                // (WorldMap.WalkStep), so a body that came at a slant does not stop on a seed's edge or turn back at the gap's mouth.
                way = Query.Flat(target - position).normalized;
                return true;
            }
            int at = index;
            Vector3 aim = Vector3.zero;
            for (int step = 0; step < LookAheadCells; step++)
            {
                int next = NextCell(layer, at, allowGravity);
                if (next == at || step > 0 && !HasClearPath(layer, position, Centre(next), allowGravity)) break;
                at = next;
                aim = Centre(at);
            }
            if (at == index) return (allowGravity ? layer.fastField : layer.safeField)[index] != int.MaxValue;
            way = Query.Flat(aim - position).normalized;
            return true;
        }

        public bool HasClearPath(Vector3 from, Vector3 to, bool allowGravity, float radius = SlimReach) => HasClearPath(For(radius), from, to, allowGravity);

        bool HasClearPath(Layer layer, Vector3 from, Vector3 to, bool allowGravity)
        {
            float fx = from.x - origin.x, fz = from.z - origin.y, tx = to.x - origin.x, tz = to.z - origin.y;
            int x = Mathf.FloorToInt(fx), z = Mathf.FloorToInt(fz), ex = Mathf.FloorToInt(tx), ez = Mathf.FloorToInt(tz);
            if (!Inside(x, z) || !Inside(ex, ez)) return false;
            float dx = tx - fx, dz = tz - fz;
            int stepX = dx > 0f ? 1 : -1, stepZ = dz > 0f ? 1 : -1;
            float deltaX = dx != 0f ? Mathf.Abs(1f / dx) : float.PositiveInfinity;
            float deltaZ = dz != 0f ? Mathf.Abs(1f / dz) : float.PositiveInfinity;
            float nextX = dx != 0f ? (stepX > 0 ? x + 1 - fx : fx - x) * deltaX : float.PositiveInfinity;
            float nextZ = dz != 0f ? (stepZ > 0 ? z + 1 - fz : fz - z) * deltaZ : float.PositiveInfinity;
            while (x != ex || z != ez)
            {
                bool moveX = x != ex && (z == ez || nextX < nextZ);
                bool moveZ = z != ez && (x == ex || nextZ < nextX);
                if (!moveX && !moveZ)
                {
                    if (!layer.Open(z * width + x + stepX, allowGravity) || !layer.Open((z + stepZ) * width + x, allowGravity)) return false;
                    moveX = moveZ = true;
                }
                if (moveX) { x += stepX; nextX += deltaX; }
                if (moveZ) { z += stepZ; nextZ += deltaZ; }
                int cell = z * width + x;
                if (x == ex && z == ez) return allowGravity || !gravity[cell];
                if (!layer.Open(cell, allowGravity)) return false;
            }
            return true;
        }

        bool raidReady;

        /// <summary>V2 day campaign raiders: one flow field for each body width toward the open ground ringing a rect (the base), so they
        /// walk round walls to it.</summary>
        public void IntegrateRaid(Rect target)
        {
            IntegrateRaid(slim, target);
            IntegrateRaid(wide, target);
            raidReady = true;
        }

        void IntegrateRaid(Layer layer, Rect target)
        {
            var field = layer.raidField ??= new int[width * height];
            var blocked = layer.blocked;
            Array.Fill(field, int.MaxValue);
            int first = 0, last = 0;
            float ring = layer.reach + RaidRingWidth;
            for (int i = 0; i < field.Length; i++)
            {
                if (blocked[i] || gravity[i]) continue;
                Vector3 c = Centre(i);
                float dx = Mathf.Max(target.xMin - c.x, Mathf.Max(0f, c.x - target.xMax)), dz = Mathf.Max(target.yMin - c.z, Mathf.Max(0f, c.z - target.yMax));
                if (Mathf.Max(dx, dz) > ring) continue;
                field[i] = 0; queue[last++] = i;
            }
            while (first < last)
            {
                int at = queue[first++];
                int x = at % width, z = at / width;
                foreach (var delta in offsets)
                {
                    int nx = x + delta.x, nz = z + delta.y;
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
                    int next = nz * width + nx;
                    if (blocked[next] || gravity[next] || field[next] != int.MaxValue) continue;
                    field[next] = field[at] + 1;
                    queue[last++] = next;
                }
            }
        }

        /// <param name="radius">The raider's radius; see Direction.</param>
        public Vector3 RaidDirection(Vector3 position, Vector3 spot, float radius = SlimReach)
        {
            int index = Index(position);
            Vector3 straight = Query.Flat(spot - position).normalized;
            if (index < 0 || !raidReady) return straight;
            var layer = For(radius);
            if (HasClearPath(layer, position, spot, false)) return straight;
            if (RaidWay(layer, index, position, out var way) || layer == slim || RaidWay(slim, index, position, out way)) return way.sqrMagnitude > 0f ? way : straight;
            return straight;
        }

        /// <summary>The way down a layer's raid field. False where the field does not reach; true with no direction on the ring round
        /// the base itself (from there the raider goes straight to its stand point).</summary>
        bool RaidWay(Layer layer, int index, Vector3 position, out Vector3 way)
        {
            way = Vector3.zero;
            int at = index;
            Vector3 aim = Vector3.zero;
            for (int step = 0; step < LookAheadCells; step++)
            {
                int next = NextRaidCell(layer, at);
                if (next == at || step > 0 && !HasClearPath(layer, position, Centre(next), false)) break;
                at = next;
                aim = Centre(at);
            }
            if (at == index) return layer.raidField[index] != int.MaxValue;
            way = Query.Flat(aim - position).normalized;
            return true;
        }

        int NextRaidCell(Layer layer, int index)
        {
            var field = layer.raidField;
            var blocked = layer.blocked;
            int x = index % width, z = index / width, best = index, score = field[index];
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nx >= width || nz < 0 || nz >= height || (dx == 0 && dz == 0)) continue;
                int next = nz * width + nx;
                if (blocked[next] || gravity[next]) continue;
                if (dx != 0 && dz != 0 && (blocked[z * width + nx] || blocked[nz * width + x])) continue;
                if (field[next] >= score) continue;
                score = field[next];
                best = next;
            }
            return best;
        }

        /// <summary>
        /// The nearest cell centre within reachCells of position that lies on a way to the goal for a body of this radius: a cell its layer
        /// leaves open, reached by the field asked for (the raid field, or the chase field with or without gravity) and, where the body's
        /// own cell is reached too, nearer the goal than it; fits says whether the body can stand there. A wide body takes a slim cell
        /// when no wide one qualifies. For a body whose own walking is refused where it stands (Enemy.SetDownOnOpenGround).
        /// </summary>
        public bool NearestOnPath(Vector3 position, float radius, bool raid, bool allowGravity, int reachCells, float minDistance, Func<Vector3, bool> fits, out Vector3 point)
        {
            point = position;
            int index = Index(position);
            if (index < 0 || raid && !raidReady) return false;
            var layer = For(radius);
            return NearestOnPath(layer, index, position, raid, allowGravity, reachCells, minDistance, fits, out point)
                   || layer != slim && NearestOnPath(slim, index, position, raid, allowGravity, reachCells, minDistance, fits, out point);
        }

        bool NearestOnPath(Layer layer, int index, Vector3 position, bool raid, bool allowGravity, int reachCells, float minDistance, Func<Vector3, bool> fits, out Vector3 point)
        {
            point = position;
            var field = raid ? layer.raidField : allowGravity ? layer.fastField : layer.safeField;
            if (field == null) return false;
            int x = index % width, z = index / width, here = field[index];
            float best = float.MaxValue;
            bool found = false;
            for (int dz = -reachCells; dz <= reachCells; dz++)
            for (int dx = -reachCells; dx <= reachCells; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
                int cell = nz * width + nx;
                if (layer.blocked[cell] || field[cell] == int.MaxValue || field[cell] >= here) continue;
                if (!allowGravity && gravity[cell]) continue;
                Vector3 centre = Centre(cell);
                float distance = Query.Flat(centre - position).sqrMagnitude;
                if (distance >= best || distance < minDistance * minDistance || !fits(centre)) continue;
                best = distance;
                point = centre;
                found = true;
            }
            return found;
        }

        bool Inside(int x, int z) => x >= 0 && z >= 0 && x < width && z < height;

        int NextCell(Layer layer, int index, bool allowGravity)
        {
            var field = allowGravity ? layer.fastField : layer.safeField;
            var blocked = layer.blocked;
            int x = index % width, z = index / width;
            int best = index;
            int score = field[index];
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, nz = z + dz;
                if (nx < 0 || nx >= width || nz < 0 || nz >= height || (dx == 0 && dz == 0)) continue;
                int next = nz * width + nx;
                if (blocked[next] || (!allowGravity && gravity[next])) continue;
                if (dx != 0 && dz != 0 && (blocked[z * width + nx] || blocked[nz * width + x])) continue;
                if (field[next] >= score) continue;
                score = field[next];
                best = next;
            }
            return best;
        }
    }
}
