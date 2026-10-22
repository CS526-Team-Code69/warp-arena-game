using UnityEngine;

namespace AnomalyArena
{

    public enum EnemyKind
    {
        Grunt,
        Brute,
        Skitter,
        Spitter,
        Rammer,
        Bloater,
        Warden,
        Splitter,
        Spire,
        Stomper,
        Shade,
        Hollow,
        Tyrant,
        Chanter
    }

    public static class EnemyRoles
    {
        public static bool IsMelee(EnemyKind kind) =>
            kind is EnemyKind.Grunt or EnemyKind.Skitter or EnemyKind.Brute or EnemyKind.Splitter;

        public static bool IsSpecial(EnemyKind kind) => kind is EnemyKind.Rammer or EnemyKind.Bloater or EnemyKind.Spire
            or EnemyKind.Stomper or EnemyKind.Shade or EnemyKind.Chanter;

        public static bool IsSupport(EnemyKind kind) => kind is EnemyKind.Warden or EnemyKind.Chanter;

        public static bool IsBoss(EnemyKind kind) => kind is EnemyKind.Hollow or EnemyKind.Tyrant;

        public static bool KnocksBackPlayer(EnemyKind kind) => IsBoss(kind) || kind is EnemyKind.Stomper or EnemyKind.Rammer;
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

        [Tooltip(
            "Legacy reward weight. Rewards now come from WaveManager.ExperienceFor (fixed swarm XP, or XP per reference second of the TTK budget).")]
        public int xp = 1;

        public int XP => xp;
        public bool canAffix = true;
        public bool canElite = true;
        public float startShieldShare;

        [Min(0f), Tooltip("Poison stacks each projectile hit adds to the player (Spitter poison spikes).")]
        public float spikePoisonStacks = 1f;

        [Tooltip(
            "When killed by HP (not swallowed, not a trial summon) it leaves mini copies of itself that never split again (Splitter).")]
        public bool splitsOnDeath;
        [Header("Special behaviour (design document 10.2); the enemy lab shows these live")]
        [Min(0f), Tooltip("Spitter: backs away when the player is closer than this.")] public float rangedNear = 8f;
        [Min(0f), Tooltip("Spitter: closes in when the player is farther than this; the wind-up ring is this big.")] public float rangedFar = 10f;
        [Min(.1f), Tooltip("Spitter: speed of the spit, cells per second.")] public float projectileSpeed = 7f;
        [Min(0f), Tooltip("Rammer and Stomper: the wind-up starts when the player is within this distance.")] public float triggerDistance = 16f;
        [Min(0f), Tooltip("Rammer: length of the charge and of its telegraph line.")] public float chargeDistance = 12f;
        [Min(.1f), Tooltip("Rammer: charge speed before the scene's global movement multiplier (WaveManager.movementSpeedMultiplier, 0.5 now: 36 runs at 18 cells per second).")] public float chargeSpeed = 16f;
        [Min(0f), Tooltip("Rammer: seconds the player is stunned when the charge lands.")] public float chargeStun = 1f;
        [Min(0f), Tooltip("Spire pulse radius (its warning circle is this big), Bloater blast radius, and the length of the Stomper's slam cone.")] public float pulseRadius = 6f;
        [Range(10f, 180f), Tooltip("Stomper: width of the slam cone in front of it, in degrees, aimed at the player when the wind-up begins.")] public float coneAngle = 60f;
        [Min(0f), Tooltip("Spire: seconds between the pulse catching the player and the laser.")] public float laserDelay = .6f;
        [Min(0f), Tooltip("Spire: laser length; walls cut it short.")] public float laserLength = 12f;
        [Min(0f), Tooltip("Spire: half width of the laser line; the player's radius is added.")] public float laserHalfWidth = .6f;

        public Color colour = new Color(0.9f, 0.3f, 0.4f);
    }
}