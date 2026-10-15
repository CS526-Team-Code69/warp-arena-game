using System;
using UnityEngine;

namespace AnomalyArena
{
    public sealed class MapGrid
    {
        readonly int width, height;
        readonly Vector2 origin;
        readonly bool[] blocked, gravity;
        readonly int[] safeField, fastField, queue;
        readonly Vector2Int[] offsets = { Vector2Int.left, Vector2Int.right, Vector2Int.down, Vector2Int.up };
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
            if (best == index) return Vector3.zero;
            return Query.Flat(Centre(best) - position).normalized;
        }
    }
}
