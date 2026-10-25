using System;
using UnityEngine;

namespace AnomalyArena
{

    [CreateAssetMenu(menuName = "Anomaly Arena/World/Map Layout")]
    public sealed class MapLayout : ScriptableObject
    {

        [Serializable]
        public struct SandPool
        {
            public Vector2 centre;
            [Min(.5f)] public float radius;

            public bool Contains(Vector3 point)
            {
                float dx = point.x - centre.x, dz = point.z - centre.y;
                return dx * dx + dz * dz <= radius * radius;
            }
        }

        public int width = 100;
        public int height = 70;
        public Vector3 playerStart;
        public Rect[] walls;
        public Vector2[] holeAnchors;
        [Tooltip("Quicksand pools (system doc 12.1, Dunes): centre and radius in cells. Walking in one is slowed to quicksandSpeed for the player and " +
                 "every enemy but the bosses. Pools never block movement, paths or shots; keep them off the walls and the black-hole anchors.")]
        public SandPool[] quicksand;
        [Range(.1f, 1f), Tooltip("Walking speed share inside quicksand: 0.6 = 60% of normal speed. A sprint is slowed by the same share.")]
        public float quicksandSpeed = .6f;
    }
}
