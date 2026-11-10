using UnityEngine;

namespace AnomalyArena
{
    public enum Team
    {
        Player,
        Enemy
    }

    /// <summary>States of the character state machine. Enter / exit logic for each state is in Combatant.SetState.</summary>
    public enum CharacterState
    {
        Normal, // free to act
        Knocked, // knocked back: no control, wall hits deal damage
        Thrown, // thrown by the hook: same as above, distance not affected by body size
        Hooked, // hooked: cannot move or attack, position controlled by the hook, cannot fall into a gap
        Dashing, // human-missile dash: immune to attack damage and knockback
        Falling, // fell into a gap: already counted as eliminated / lost, only the fall is left to play
        Dead,
    }

    public enum KnockKind
    {
        Hit, // normal knockback: distance scaled by body size, wall hits deal damage
        Throw, // hook throw: fixed distance, wall hits deal damage
        Push, // recoil push: wall hits deal no damage
    }

    public enum DeathCause
    {
        None,
        HpDepleted,
        FellIntoGap
    }

    /// <summary>Kind of hit effect: decides shard color, count and whether there is a shock ring.</summary>
    public enum HitKind
    {
        Blunt, // punch
        Bullet, // bullet
        Pierce, // charged shot, throwing knife, human missile passing through
        Slam, // wall impact, hit by a corpse
        Blast, // explosion
    }

    public struct DamageInfo
    {
        public float amount;

        /// <summary>Attack damage is subject to the 0.5 s protection and dash immunity; wall damage does not go through here.</summary>
        public bool isAttack;

        /// <summary>Knockback direction; when zero, hit shards fly in random directions.</summary>
        public Vector3 knockDir;

        public float knockDistance;
        public IDamageDealer dealer;
        public HitKind kind;
        public DamageSource source;
        public ElementKind element;
        public bool ignoreShield;
        /// <summary>Lance (document 6.4): the hit still goes into the target's Shield first, but the Shield does not soften it.</summary>
        public bool ignoreShieldReduction;
        public bool builtInElement;
        /// <summary>The hit only adds Lightning stacks: Chain Coil already jumps, so the element system must not jump again.</summary>
        public bool noLightningJump;
        /// <summary>The continuous weapon source this hit comes from (a spray, a ground field, a drain ring): it adds element stacks
        /// to one target at most once per StatusRules.continuousStackInterval. Null for an ordinary hit, which always counts.</summary>
        public object continuousSource;
        /// <summary>How the hit should feel; Auto derives it from kind.</summary>
        public HitFeel feel;
        /// <summary>The player weapon that dealt the hit (null for anything else): its Fire burns a share of the weapon's DPS.</summary>
        public RunWeapon weapon;

        public static DamageInfo Attack(float amount, IDamageDealer dealer, Vector3 knockDir = default,
            float knockDistance = 0f,
            HitKind kind = HitKind.Blunt) =>
            new DamageInfo
            {
                amount = amount, isAttack = true, dealer = dealer, knockDir = knockDir, knockDistance = knockDistance,
                kind = kind
            };
    }

    /// <summary>
    /// What a thrown character does to whatever it crashes into (hook throw). Filled in by the effect that throws it.
    /// Damage follows kinetic energy: it grows with the square of the speed left at the crash and with the size of the thrown body.
    /// </summary>
    public struct ThrowImpact
    {
        /// <summary>Body radius that counts as size 1 (a small enemy).</summary>
        public const float ReferenceRadius = 0.5f;

        public IDamageDealer dealer;

        /// <summary>Damage when a body of ReferenceRadius crashes at full launch speed.</summary>
        public float damage;

        /// <summary>How strongly body size raises damage and push: 1 = twice the radius, twice the effect.</summary>
        public float sizeExponent;

        /// <summary>How far beyond the thrown body's edge the crash reaches.</summary>
        public float radius;

        /// <summary>How far a body of ReferenceRadius crashing at full launch speed pushes what it hits.</summary>
        public float knockback;
        public ElementKind element;
        public float stunSeconds;

        /// <summary>Hook Twist: the hook that flung this body reels in the body it crashes into; null means no chain.</summary>
        public RunWeapon chainHook;

        public float SizeFactor(float bodyRadius) => Mathf.Pow(bodyRadius / ReferenceRadius, sizeExponent);

        /// <summary>Damage of a crash at speedRatio (speed left ÷ launch speed, 0–1) by a body of bodyRadius. Never below 1.</summary>
        public float DamageAt(float speedRatio, float bodyRadius) =>
            Mathf.Max(1f, Mathf.Round(damage * SizeFactor(bodyRadius) * speedRatio * speedRatio));

        /// <summary>Push distance of the same crash. It grows linearly with the speed left (momentum), not with its square.</summary>
        public float KnockbackAt(float speedRatio, float bodyRadius) => knockback * SizeFactor(bodyRadius) * speedRatio;
    }

    public interface IDamageDealer
    {
        Combatant Owner { get; }
    }

    public interface IDamageReceiver
    {
        bool IsAlive { get; }

        /// <returns>Whether the damage actually applied (false while protected / dashing)</returns>
        bool ReceiveDamage(DamageInfo info);
    }
}
