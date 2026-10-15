using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/World/Biome")]
    public sealed class Biome : ScriptableObject
    {
        public string displayName = "Interstellar Battlefield";
        public MapLayout layout;
        public Color ambient = new Color(0.19f, 0.24f, 0.35f);
        public Material floor;
        public Material wall;
        [Tooltip("Enemy mix offsets, in EnemyKind order. Only already-unlocked types can be tilted.")]
        public int[] mixTilt = new int[11];
    }
}
