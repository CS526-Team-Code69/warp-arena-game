using System;
using UnityEngine;

namespace AnomalyArena
{

    public enum KnockTier { None, Light, Medium, Heavy, Fly }

    [Serializable]
    public sealed class KnockbackRules
    {

        public static readonly KnockbackRules Defaults = new();

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

        public float Distance(KnockTier tier) => tier switch
        {
            KnockTier.Light => light,
            KnockTier.Medium => medium,
            KnockTier.Heavy => heavy,
            KnockTier.Fly => fly,
            _ => 0f
        };

        public float ScaleFor(float radius, bool elite)
        {
            float size = Mathf.Pow(Mathf.Max(.05f, referenceRadius) / Mathf.Max(.05f, radius), Mathf.Max(0f, sizeExponent));
            float scale = Mathf.Clamp(size, Mathf.Min(minScale, maxScale), Mathf.Max(minScale, maxScale));
            return scale * (elite ? Mathf.Clamp01(eliteFactor) : 1f);
        }

        public static KnockTier AttackTier(EnemyKind kind) => kind switch
        {
            EnemyKind.Rammer => KnockTier.Light,
            EnemyKind.Bloater => KnockTier.Medium,
            EnemyKind.Stomper => KnockTier.Heavy,
            _ => KnockTier.None
        };
    }
}
