using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Saved as ints in assets and wave weights: only append new kinds at the end.</summary>
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

    /// <summary>V5 roster roles (system doc §10). Basic: melee (Grunt and its variants Brute/Skitter/Splitter), ranged (Spitter), support (Warden).
    /// Specials (Rammer, Bloater, Spire, Stomper, Shade, Chanter) are ordinary spawns in the waves that list them; an elite is a special with an element affix.
    /// Only bosses knock the player back (§5.2).</summary>
    public static class EnemyRoles
    {
        public static bool IsMelee(EnemyKind kind) =>
            kind is EnemyKind.Grunt or EnemyKind.Skitter or EnemyKind.Brute or EnemyKind.Splitter;

        public static bool IsSpecial(EnemyKind kind) => kind is EnemyKind.Rammer or EnemyKind.Bloater or EnemyKind.Spire
            or EnemyKind.Stomper or EnemyKind.Shade or EnemyKind.Chanter;

        /// <summary>Never attacks; stays behind the nearest ally, away from the player.</summary>
        public static bool IsSupport(EnemyKind kind) => kind is EnemyKind.Warden or EnemyKind.Chanter;

        public static bool IsBoss(EnemyKind kind) => kind is EnemyKind.Hollow or EnemyKind.Tyrant;
        /// <summary>Bosses, the Stomper slam (far) and the Rammer charge (a little, then a stun) push the player; every other
        /// enemy hit is feedback only. The distance is the attack's knockback tier (KnockbackRules); bosses keep their own numbers.</summary>
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

        [Tooltip("Bosses only: push of a plain hit (their moves push by Boss Tuning). Every other enemy pushes by its attack's knockback tier (GameManager > Rules > Knockback).")]
        public float knockback = 5f;

        // Retired (document 5.3): knockback resistance follows body size now (GameManager > Rules > Knockback). Hidden but kept, because
        // the V5 roster installer still copies it.
        [HideInInspector] public float knockbackScale = 1f;

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
        [Min(0f), Tooltip("Rammer and Stomper: the wind-up starts when the player is within this distance. Bloater: starts rushing the player from this distance.")] public float triggerDistance = 16f;
        [Min(0f), Tooltip("Rammer: length of the charge and of its telegraph line. Bloater: how far one rush goes before it gives up for an interval.")] public float chargeDistance = 12f;
        [Min(.1f), Tooltip("Rammer charge and Bloater rush speed before the scene's global movement multiplier (WaveManager.movementSpeedMultiplier, 0.5 now: 36 runs at 18 cells per second).")] public float chargeSpeed = 16f;
        [Min(0f), Tooltip("Rammer: seconds the player is stunned when the charge lands.")] public float chargeStun = 1f;
        [Min(0f), Tooltip("Spire sentry range: its warning circle is this big and its light wave stops here (2026-11-05). Bloater blast radius, and the length of the Stomper's slam cone.")] public float pulseRadius = 6f;
        [Range(10f, 180f), Tooltip("Stomper: width of the slam cone in front of it, in degrees, aimed at the player when the wind-up begins.")] public float coneAngle = 60f;
        [Min(0f), Tooltip("Spire: seconds between its light wave catching the player and the laser.")] public float laserDelay = .6f;
        [Min(0f), Tooltip("Spire: laser length; walls cut it short.")] public float laserLength = 12f;
        [Min(0f), Tooltip("Spire: half width of the laser line; the player's radius is added.")] public float laserHalfWidth = .6f;
        [Min(.1f), Tooltip("Spire: speed of its light wave's front, cells per second; the wave starts at the Spire and stops at the sentry range (2026-11-05).")] public float waveSpeed = 12f;

        public Color colour = new Color(0.9f, 0.3f, 0.4f);
    }
}