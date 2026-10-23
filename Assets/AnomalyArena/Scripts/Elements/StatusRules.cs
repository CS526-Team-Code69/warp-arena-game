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
        [Tooltip("Jump radius when the origin carries Frost; plain Lightning also jumps (lightningJumpRadius).")] public float frostLightningJumpRadius = 6f;
        [Tooltip("Lightning stack multiplier on a target that carries Frost.")] public float frostLightningStackMultiplier = 1.5f;
        public int lightningJumpMax = 5;
        public float earthShare = 0.10f, lightShare = 0.05f, voidShare = 0.10f, lightAgainstVoid = 1.5f;
        public float shieldDirectFactor = 0.7f;
        public float playerShieldCapShare = 0.5f, shieldDecayDelay = 3f, shieldDecayPerSecond = 1f;
        public float largeThresholdFactor = 2f;
        public float playerControlMax = 1f, playerControlImmunity = 4f;
        public float enemyFireStacks = 3f, enemyPoisonStacks = 1f, enemyFrostStacks = 3f, enemyLightningStacks = 3f;
        public float enemyVoidDrain = 10f;
        [Tooltip("Share of each hit's damage a Light-affixed enemy heals back (design document: 15%).")] public float lightAffixLifesteal = .15f;

        [Header("Element formulas on enemies (the player keeps the integer rules above)")]
        [Tooltip("Fire on an enemy: every fireInterval one stack burns off and deals this much (wave-1 value, times R(w)).")]
        [Min(0f)] public float fireDamagePerStack = 1.5f;
        [Tooltip("Poison on an enemy: every poisonInterval it deals stacks × this (wave-1 value, times R(w)), then loses one stack.")]
        [Min(0f)] public float poisonOnEnemyDamagePerStack = 0.12f;
        [Tooltip("Cap on Poison stacks carried by an enemy.")]
        [Min(1f)] public float poisonOnEnemyCap = 20f;
        [Tooltip("Frost division slow: speed × 1/(1 + this × stacks/freeze threshold); about ×0.4 just before freezing.")]
        [Min(0f)] public float frostChill = 1.5f;
        [Tooltip("Enemy speed and timer rate between paralysis twitches, chained with the Frost slow; during a twitch the body can neither move nor act.")]
        [Range(0.05f, 1f)] public float lightningSlow = 0.55f;
        [Tooltip("Lightning jump: the first jump is full, every further jump multiplies damage and stacks by this again.")]
        [Range(0f, 1f)] public float lightningChainFalloff = 0.7f;
        [Tooltip("Light lifesteal × (1 + this × missing HP fraction): heals more at low HP.")]
        [Min(0f)] public float lightMissingHpBonus = 2f;

        [Header("Control vulnerability (both teams)")]
        [Tooltip("Damage multiplier on attacks against a frozen target (1.3 = +30%). Damage over time is not affected.")]
        [Min(1f)] public float frozenDamageTaken = 1.3f;
        [Tooltip("Player attacks on a paralysed enemy deal +this per paralysed enemy within paralysedBonusRadius, the target included.")]
        [Min(0f)] public float paralysedBonusPerNeighbour = 0.1f;
        [Tooltip("Radius (units) in which paralysed enemies count toward the paralysed damage bonus.")]
        [Min(0f)] public float paralysedBonusRadius = 4f;
        [Tooltip("Most paralysed enemies (target included) the bonus counts.")]
        [Min(1)] public int paralysedBonusMax = 5;
    }
}
