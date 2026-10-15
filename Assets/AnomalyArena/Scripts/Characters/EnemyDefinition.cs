using UnityEngine;

namespace AnomalyArena
{
    public enum EnemyKind { Grunt, Brute, Skitter, Spitter, Rammer, Bloater, Warden, Splitter, Spire, Stomper, Shade, Hollow, Tyrant }

    [CreateAssetMenu(menuName = "Anomaly Arena/Enemies/Definition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public EnemyKind kind;
        public string displayName;
        public float hp = 10f;
        public float speed = 5.4f;
        public float radius = 0.5f;
        public float damage = 5f;
        public float windup = 0.5f;
        public float interval = 1f;
        public float reach = 0.8f;
        public float knockback = 5f;
        public float knockbackScale = 1f;
        public int xp = 1;
        public int XP => xp;
        public bool canAffix = true;
        public bool canElite = true;
        public float startShieldShare;
        public Color colour = new Color(0.9f, 0.3f, 0.4f);
    }
}
