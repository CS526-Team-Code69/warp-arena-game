using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Status Rules")]
    public sealed class StatusRules : ScriptableObject
    {
        [Header("GDD §4.6 candidate parameters")]
        public float damagePerStack = 5f;
        public float builtInElementStackFactor = 2f;
        public float fireInterval = 0.5f, fireCap = 20f, fireShieldFactor = 0.5f;
        public float poisonInterval = 1f, poisonCap = 30f, poisonDamagePerStack = 0.5f;
        public float frostThreshold = 6f, frostDuration = 1.5f, frostDecay = 1f, frostImmunity = 3f;
        public float lightningThreshold = 6f, lightningDuration = 4f, lightningDecay = 1f, lightningImmunity = 3f;
        public float twitchInterval = 1f, twitchLength = 0.3f;
        public float lightningJumpRadius = 4f, lightningJumpDamage = 3f;
        [Tooltip("Lightning-Frost conduction radius; base Lightning also conducts.")] public float frostLightningJumpRadius = 6f;
        [Tooltip("Additional Lightning stacks when the target has Frost.")] public float frostLightningStackMultiplier = 1.5f;
        public int lightningJumpMax = 5;
        public float earthShare = 0.10f, lightShare = 0.05f, voidShare = 0.10f, lightAgainstVoid = 1.5f;
        public float shieldDirectFactor = 0.7f;
        public float playerShieldCapShare = 0.5f, shieldDecayDelay = 3f, shieldDecayPerSecond = 1f;
        public float largeThresholdFactor = 2f, bossControlStagger = 15f;
        public float playerControlMax = 1f, playerControlImmunity = 4f;
        public float enemyFireStacks = 3f, enemyPoisonStacks = 1f, enemyFrostStacks = 3f, enemyLightningStacks = 3f;
        public float enemyVoidDrain = 10f;

        [Header("V4 element formulas on enemies (the player keeps the rules above)")]
        [Tooltip("Fire, exponential burn-out: each tick burns this fraction of the current stacks. 0.206 ≈ 1.5 s half-life at a 0.5 s interval.")]
        [Range(0.01f, 1f)] public float fireBurnFraction = 0.206f;
        [Tooltip("Fire: total damage each stack deals before it burns out (wave-1 value, multiplied by R(w)). Steady burn ≈ weapon DPS × this ÷ damagePerStack = 30%.")]
        [Min(0f)] public float fireDamagePerStack = 1.5f;
        [Tooltip("Poison, additive build-up: no decay; damage per stack per second (wave-1 value, multiplied by R(w)). After t seconds of hits ≈ weapon DPS × this × t ÷ damagePerStack.")]
        [Min(0f)] public float poisonOnEnemyDamagePerStack = 0.12f;
        [Tooltip("Poison stack cap on enemies; full stacks on wave 15 deal about 20×0.12×5.33 ≈ 12.8 damage/s.")]
        [Min(1f)] public float poisonOnEnemyCap = 20f;
        [Tooltip("Frost, division chill: speed × 1/(1 + this × stacks/freeze threshold); about ×0.4 just before freezing.")]
        [Min(0f)] public float frostChill = 1.5f;
        [Tooltip("Lightning paralysis: continuous speed multiplier, multiplied with the Frost chill; twitches only interrupt windups and no longer root.")]
        [Range(0.05f, 1f)] public float lightningSlow = 0.55f;
        [Tooltip("Lightning chain: the first jump keeps full damage and stacks; every later jump multiplies them by this value again.")]
        [Range(0f, 1f)] public float lightningChainFalloff = 0.7f;
        [Tooltip("Light lifesteal × (1 + this × missing HP fraction): heals more at low HP.")]
        [Min(0f)] public float lightMissingHpBonus = 2f;
    }
}
