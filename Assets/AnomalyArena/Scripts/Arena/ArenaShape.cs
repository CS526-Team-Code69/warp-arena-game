using UnityEngine;

namespace AnomalyArena
{
    public static class ArenaShape
    {
        public static readonly Vector2[] Vertices =
        {
            new Vector2(-11f, -15f),        
            new Vector2(-3f, -13.5f),                                
            new Vector2(9f, -15f),          
            new Vector2(14f, -11f),         
            new Vector2(16f, -4f),          
            new Vector2(15f, 7f),           
            new Vector2(12f, 13.5f),        
            new Vector2(3f, 15f),           
            new Vector2(-8f, 14f),          
            new Vector2(-13.5f, 10.5f),      
            new Vector2(-15.5f, 2f),         
            new Vector2(-14f, -6f),                                 
            new Vector2(-16f, -11f),         
        };

        public struct Gap
        {
            public string name;

            public int edge;

            public float center;
            public float width;
        }

        public const float SmallGapWidth = 1.6f;

        public const float LargeGapWidth = 7f;

        public static readonly Gap[] Gaps =
        {
            new Gap { name = "LargeGap_North", edge = 7, center = -1f, width = LargeGapWidth },
            new Gap { name = "LargeGap_South", edge = 1, center = -1f, width = LargeGapWidth },
            new Gap { name = "LargeGap_East", edge = 4, center = -1f, width = LargeGapWidth },
            new Gap { name = "SmallGap_East", edge = 3, center = -1f, width = SmallGapWidth },
            new Gap { name = "SmallGap_West", edge = 10, center = -1f, width = SmallGapWidth },
        };

        public static int EdgeCount => Vertices.Length;
        public static Vector2 EdgeStart(int i) => Vertices[i];
        public static Vector2 EdgeEnd(int i) => Vertices[(i + 1) % Vertices.Length];
        public static float EdgeLength(int i) => (EdgeEnd(i) - EdgeStart(i)).magnitude;
        public static Vector2 EdgeDirection(int i) => (EdgeEnd(i) - EdgeStart(i)).normalized;

        public static Vector2 EdgeOutward(int i)
        {
            Vector2 d = EdgeDirection(i);
            return new Vector2(d.y, -d.x);
        }

        public static (float from, float to) GapRange(Gap g)
        {
            float len = EdgeLength(g.edge);
            float c = g.center < 0f ? len * 0.5f : g.center;
            return (c - g.width * 0.5f, c + g.width * 0.5f);
        }

        public static Vector3 ToWorld(Vector2 p, float y = 0f) => new Vector3(p.x, y, p.y);
        public static Vector2 ToPlane(Vector3 p) => new Vector2(p.x, p.z);

        public static bool Contains(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = Vertices.Length - 1; i < Vertices.Length; j = i++)
            {
                Vector2 a = Vertices[i], b = Vertices[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        public static float DistanceToEdge(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < Vertices.Length; i++) best = Mathf.Min(best, (p - ClosestOnEdge(i, p)).magnitude);
            return best;
        }

        public static bool ContainsWithMargin(Vector3 world, float margin)
        {
            Vector2 p = ToPlane(world);
            return Contains(p) && DistanceToEdge(p) >= margin;
        }

        public static Vector3 ClampInside(Vector3 world, float margin)
        {
            if (ContainsWithMargin(world, margin)) return world;
            Vector2 p = ToPlane(world);
            int bestEdge = 0;
            float best = float.MaxValue;
            for (int i = 0; i < Vertices.Length; i++)
            {
                float d = (p - ClosestOnEdge(i, p)).sqrMagnitude;
                if (d >= best) continue;
                best = d;
                bestEdge = i;
            }
            Vector2 q = ClosestOnEdge(bestEdge, p) - EdgeOutward(bestEdge) * margin;
            return new Vector3(q.x, world.y, q.y);
        }

        public static Vector3 RandomInside(float margin)
        {
            Vector2 min = Vertices[0], max = Vertices[0];
            foreach (var v in Vertices)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            for (int i = 0; i < 200; i++)
            {
                var w = new Vector3(Random.Range(min.x, max.x), 0f, Random.Range(min.y, max.y));
                if (ContainsWithMargin(w, margin)) return w;
            }
            return Vector3.zero;                                                                               
        }

        static Vector2 ClosestOnEdge(int i, Vector2 p)
        {
            Vector2 a = EdgeStart(i), b = EdgeEnd(i);
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }
    }
}
