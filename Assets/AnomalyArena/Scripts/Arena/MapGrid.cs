using System;
using UnityEngine;

namespace AnomalyArena
{
    public sealed class MapGrid
    {
        readonly int width, height;
        readonly Vector2 origin;
        readonly bool[] blocked, gravity, openSafe, openFast;
        readonly int[] safeField, fastField, queue;
        readonly Vector2Int[] offsets = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
        const int LookAheadCells = 8;
        public int Width => width;
        public int Height => height;

        public MapGrid(int width, int height)
        {
            this.width = width;
            this.height = height;
            origin = new Vector2(-width * 0.5f, -height * 0.5f);
            int count = width * height;
            blocked = new bool[count];
            gravity = new bool[count];
            openSafe = new bool[count];
            openFast = new bool[count];
            safeField = new int[count];
            fastField = new int[count];
            queue = new int[count];
        }

        public int Index(Vector3 position)
        {
            int x = Mathf.FloorToInt(position.x - origin.x);
            int z = Mathf.FloorToInt(position.z - origin.y);
            return x < 0 || z < 0 || x >= width || z >= height ? -1 : z * width + x;
        }

        public Vector3 Centre(int index) => new Vector3(origin.x + index % width + 0.5f, 0f, origin.y + index / width + 0.5f);

        public void Rebuild(WorldMap map)
        {
            for (int i = 0; i < blocked.Length; i++)
            {
                Vector3 point = Centre(i);
                blocked[i] = !map.Contains(point, 0.5f) || map.IsWall(point, 0.5f) || map.IsHole(point);
                gravity[i] = map.GetPull(point).sqrMagnitude > 0.01f;
            }
            for (int i = 0; i < blocked.Length; i++)
            {
                int x = i % width, z = i / width;
                bool nearBlocked = false, nearGravity = false;
                for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if (!Inside(nx, nz)) { nearBlocked = true; continue; }
                    int cell = nz * width + nx;
                    nearBlocked |= blocked[cell];
                    nearGravity |= gravity[cell];
                }
                openFast[i] = !nearBlocked;
                openSafe[i] = !nearBlocked && !nearGravity;
            }
        }

        public void Integrate(Vector3 target)
        {
            Fill(safeField, Index(target), false);
            Fill(fastField, Index(target), true);
        }

        void Fill(int[] field, int goal, bool allowGravity)
        {
            Array.Fill(field, int.MaxValue);
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
            field[goal] = 0;
            queue[last++] = goal;

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

        public Vector3 Direction(Vector3 position, Vector3 target, bool allowGravity)
        {
            int index = Index(position);
            if (index < 0) return Query.Flat(target - position).normalized;
            if (HasClearPath(position, target, allowGravity)) return Query.Flat(target - position).normalized;
            int at = index;
            Vector3 aim = Vector3.zero;
            for (int step = 0; step < LookAheadCells; step++)
            {
                int next = NextCell(at, allowGravity);
                if (next == at || step > 0 && !HasClearPath(position, Centre(next), allowGravity)) break;
                at = next;
                aim = Centre(at);
            }
            return at == index ? Vector3.zero : Query.Flat(aim - position).normalized;
        }

        public bool HasClearPath(Vector3 from, Vector3 to, bool allowGravity)
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
                    if (!Open(z * width + x + stepX, allowGravity) || !Open((z + stepZ) * width + x, allowGravity)) return false;
                    moveX = moveZ = true;
                }
                if (moveX) { x += stepX; nextX += deltaX; }
                if (moveZ) { z += stepZ; nextZ += deltaZ; }
                int cell = z * width + x;
                if (x == ex && z == ez) return allowGravity || !gravity[cell];
                if (!Open(cell, allowGravity)) return false;
            }
            return true;
        }

        bool Inside(int x, int z) => x >= 0 && z >= 0 && x < width && z < height;
        bool Open(int cell, bool allowGravity) => allowGravity ? openFast[cell] : openSafe[cell];

        int NextCell(int index, bool allowGravity)
        {
            var field = allowGravity ? fastField : safeField;
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
