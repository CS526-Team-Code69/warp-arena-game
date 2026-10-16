using UnityEngine;

namespace AnomalyArena
{
    public enum EnemyKind { Grunt, Brute, Skitter, Spitter, Rammer, Bloater, Warden, Splitter, Spire, Stomper, Shade, Hollow, Tyrant }

    public static class EnemyRoles
    {
        public static bool IsMelee(EnemyKind kind) => kind is EnemyKind.Grunt or EnemyKind.Skitter or EnemyKind.Brute or EnemyKind.Splitter;
        public static bool IsEliteOnly(EnemyKind kind) => kind is EnemyKind.Rammer or EnemyKind.Bloater or EnemyKind.Spire or EnemyKind.Stomper or EnemyKind.Shade;
        public static bool IsBoss(EnemyKind kind) => kind is EnemyKind.Hollow or EnemyKind.Tyrant;
        public static bool KnocksBackPlayer(EnemyKind kind) => IsEliteOnly(kind) || IsBoss(kind);
    }

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
        [Min(0f), Tooltip("Poison stacks each projectile hit adds to the player (Spitter poison spikes).")]
        public float spikePoisonStacks = 1f;
        public Color colour = new Color(0.9f, 0.3f, 0.4f);
    }
}