using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Knockback tiers of document 5.3, from no push at all to the farthest throw.</summary>
    public enum KnockTier { None, Light, Medium, Heavy, Fly }

    /// <summary>
    /// Document 5.3 knockback numbers shared by every enemy: how far each tier pushes, and how body size resists a push (the bigger the
    /// body, the shorter the slide; elites slide less than their kind, document 10.6). Bosses and the Spire are never moved at all, the
    /// hook's fling ignores body size, and the player keeps its own multiplier (Combatant.knockbackScale).
    /// </summary>
    [Serializable]
    public sealed class KnockbackRules
    {
        /// <summary>The values used where no GameManager holds the scene's own.</summary>
        public static readonly KnockbackRules Defaults = new();

        /// <summary>The scene's rules (GameManager > Rules > Knockback), or the defaults.</summary>
        public static KnockbackRules Active
        {
            get
            {
                var game = GameManager.Instance;
                return game && game.rules?.knockback != null ? game.rules.knockback : Defaults;
            }
        }

        [Header("Tiers (document 5.3): cells a body of the reference size slides")]
        [Min(0f), Tooltip("Light: the Rammer's charge pushes the player this far, then stuns.")]
        public float light = 2f;

        [Min(0f), Tooltip("Medium: the Bloater's blast pushes the enemies it catches this far (it never pushes the player).")]
        public float medium = 5f;

        [Min(0f), Tooltip("Heavy: the Stomper's slam throws the player and the enemies in its cone this far.")]
        public float heavy = 8f;

        [Min(0f), Tooltip("Fly: the farthest tier. No ordinary enemy attack uses it.")]
        public float fly = 10f;

        [Header("Body size (document 5.3): the bigger the body, the shorter the slide")]
        [Min(.05f), Tooltip("Enemy body radius that slides the full distance. A Grunt is 0.5.")]
        public float referenceRadius = .5f;

        [Min(0f), Tooltip("How strongly size resists a push: slide × (reference radius ÷ body radius) to this power. 1: a body twice as wide slides half as far. 0: size does not matter.")]
        public float sizeExponent = 1f;

        [Min(0f), Tooltip("Shortest slide share of a large ordinary enemy (bosses and the Spire are never moved).")]
        public float minScale = .25f;

        [Min(0f), Tooltip("Longest slide share of a small body (the Shade, mini Splitters).")]
        public float maxScale = 1.5f;

        [Range(0f, 1f), Tooltip("An elite slides this share of what an ordinary enemy of its kind slides (document 10.6).")]
        public float eliteFactor = .5f;

        /// <summary>Cells an attack of this tier pushes a body of the reference size.</summary>
        public float Distance(KnockTier tier) => tier switch
        {
            KnockTier.Light => light,
            KnockTier.Medium => medium,
            KnockTier.Heavy => heavy,
            KnockTier.Fly => fly,
            _ => 0f
        };

        /// <summary>Knockback multiplier of an enemy body of this radius: (reference ÷ radius)^sizeExponent kept within
        /// [minScale, maxScale], times eliteFactor for an elite.</summary>
        public float ScaleFor(float radius, bool elite)
        {
            float size = Mathf.Pow(Mathf.Max(.05f, referenceRadius) / Mathf.Max(.05f, radius), Mathf.Max(0f, sizeExponent));
            float scale = Mathf.Clamp(size, Mathf.Min(minScale, maxScale), Mathf.Max(minScale, maxScale));
            return scale * (elite ? Mathf.Clamp01(eliteFactor) : 1f);
        }

        /// <summary>Document 5.3: the tier of an enemy kind's own attack. The Rammer's charge is light (and stuns), the Bloater's blast
        /// medium, the Stomper's slam heavy; the Shade's strike and the Spire's laser push nobody (its light wave only finds the player).
        /// Boss moves keep their own pushes
        /// (Boss Tuning).</summary>
        public static KnockTier AttackTier(EnemyKind kind) => kind switch
        {
            EnemyKind.Rammer => KnockTier.Light,
            EnemyKind.Bloater => KnockTier.Medium,
            EnemyKind.Stomper => KnockTier.Heavy,
            _ => KnockTier.None
        };
    }
}
