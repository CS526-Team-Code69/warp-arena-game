using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/World/Map Layout")]
    public sealed class MapLayout : ScriptableObject
    {
        public int width = 100;
        public int height = 70;
        public Vector3 playerStart;
        public Rect[] walls;
        public Vector2[] holeAnchors;
    }
}
