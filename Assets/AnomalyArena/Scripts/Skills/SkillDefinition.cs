using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        public SkillId id;
        public string displayName;
        public ElementKind element;
        public bool greaterVoid;
        public float cost = 30f;
        public float cooldown = 2f;
        [Tooltip("Devour's level-3 cooldown from GDD §6.3.")] public float advancedCooldown = 4f;
        public float[] duration = { 0f, 0f, 0f };
        public float[] radius = { 0f, 0f, 0f };
        public float[] range = { 12f, 12f, 12f };
        public float[] strength = { 0f, 0f, 0f };
        [Tooltip("Storm Call direct damage per bolt.")] public float hitDamage = 6f;
        [Tooltip("Storm Call Lightning stacks per bolt.")] public float hitStacks = 3f;
        [Tooltip("Gravity Well level 3 ending hit.")] public float endDamage = 10f;
        [Tooltip("Gravity Well ending push, or Bulwark level 3 ending push.")] public float endKnockback = 10f;
        [Tooltip("Radiance level 3 immediate heal.")] public float healOnCast = 20f;
        [Tooltip("Devour payout after its Energy cost.")] public float energyReward = 20f;
        [Tooltip("Devour can feed a hole within this distance.")] public float feedRange = 10f;
        [Tooltip("Candidate spacing between Flame Wake trail samples.")] public float trailSpacing = 0.5f;
        [Header("Greater Void — latest user revision")]
        [Tooltip("Gravity ring width around a temporary void hole, in grid units. Pocket Void: user-specified 1.")]
        [Min(0f)] public float voidGravityWidth = 1f;
        [Tooltip("Candidate diameter of each Void Walk hole; the user specified two small holes.")]
        [Min(0.1f)] public float voidWalkHoleDiameter = 3f;
        [Tooltip("Candidate movement multiplier for enemies drawn by Singularity.")]
        [Range(0f, 1f)] public float singularitySlowMultiplier = 0.5f;
        [Tooltip("Candidate Singularity base blast damage, before the run Damage stat.")]
        [Min(0f)] public float singularityBaseDamage = 10f;
        [Tooltip("Candidate extra blast damage per distinct enemy absorbed into the blast radius.")]
        [Min(0f)] public float singularityDamagePerEnemy = 4f;
        public string[] descriptions = { "", "", "" };
        public Texture2D icon;
        public float Duration(int level) => duration[Mathf.Clamp(level - 1, 0, 2)];
        public float Radius(int level) => radius[Mathf.Clamp(level - 1, 0, 2)];
        public float Range(int level) => range[Mathf.Clamp(level - 1, 0, 2)];
        public float Strength(int level) => strength[Mathf.Clamp(level - 1, 0, 2)];
        [Min(1f), Tooltip("V4 exponential level growth: direct damage × this^(level-1), then × the wave scale R(w) and the player's damage multiplier.")]
        public float levelDamageGrowth = 1.35f;
        [Tooltip("V4: Bulwark strength is a share of max HP as Shield (0.2 = 20% of max HP); when off, it is flat points.")]
        public bool shieldScalesWithMaxHp = true;

        public float ScaledDamage(float baseDamage, int level, float damageMultiplier = 1f) =>
            baseDamage * ElementCombat.WaveScale * Mathf.Pow(Mathf.Max(1f, levelDamageGrowth), Mathf.Clamp(level, 1, 3) - 1) * Mathf.Max(0f, damageMultiplier);
        public float Cooldown(int level) => id == SkillId.Devour && level >= 3 ? advancedCooldown : cooldown;
        public string Description(int level) => ArenaText.Pair(EnglishDescription(level),EnglishDescription(level));
        string ChineseDescription(int level) =>EnglishDescription(level);
        string EnglishDescription(int level) => id switch
        {
            SkillId.FlameWake => $"Leave a burning trail for {Duration(level):0.#} s: {Strength(level):0.#} Fire/s"
                + (level >= 3 ? "; twice as wide." : "."),
            SkillId.Contagion => $"{Strength(level):0.#} Poison; for {Duration(level):0.#} s, jump on death to "
                + (level >= 3 ? "two enemies" : "one enemy") + $" within {Radius(level):0.#}.",
            SkillId.FrostNova => $"{Strength(level):0.#} Frost within {Radius(level):0.#}"
                + (level >= 3 ? "; frozen targets are knocked 50% further." : "."),
            SkillId.StormCall => $"{Strength(level):0} bolts over {Duration(level):0.#} s: {ScaledDamage(hitDamage, level):0.#} damage (grows with waves and Damage cards) and {hitStacks:0.#} Lightning."
                + (level >= 3 ? " Additional level-3 upgrade is not yet defined." : ""),
            SkillId.Bulwark => $"A wall {Radius(level):0.#} wide for {Duration(level):0.#} s; " + (shieldScalesWithMaxHp ? $"Shield equal to {Strength(level) * 100f:0}% of max HP." : $"{Strength(level):0.#} Shield.")
                + (level >= 3 ? $" Push {endKnockback:0.#} when it ends." : ""),
            SkillId.Radiance => $"Clear statuses; hits heal {Strength(level) * 100f:0}% for {Duration(level):0.#} s."
                + (level >= 3 ? $" Heal {healOnCast:0.#} HP now." : ""),
            SkillId.Devour => $"Swallow a non-elite below {Strength(level) * 100f:0}% HP within {Range(level):0.#}; gain {energyReward:0.#} Energy.",
            SkillId.Blink => $"Teleport up to {Range(level):0.#}; cancel knockback." + (level >= 3 ? " Leave a 2 s decoy." : ""),
            SkillId.Shockwave => $"Push {Strength(level):0.#} within {Radius(level):0.#}." + (level >= 3 ? " Reflect enemy projectiles." : ""),
            SkillId.GravityWell => $"Pull within {Radius(level):0.#} at {Strength(level):0.#}/s for {Duration(level):0.#} s."
                + (level >= 3 ? $" End: {ScaledDamage(endDamage, level):0.#} damage (grows with waves), push {endKnockback:0.#}." : ""),
            SkillId.TimeSlip => $"Everything except you runs at {Strength(level) * 100f:0}% for {Duration(level):0.#} s."
                + (level >= 3 ? " Your Attack Speed +50%." : ""),
            SkillId.Decoy => $"Enemies chase a dummy for {Duration(level):0.#} s." + (level >= 3 ? " Ends: 8 damage, Medium push." : ""),
            SkillId.Overcharge => $"Hand-weapon cooldowns run twice as fast for {Duration(level):0.#} s."
                + (level >= 3 ? " Knockback +50%." : ""),
            SkillId.PocketVoid => $"A circular hole in a {Radius(level):0.#}×{Radius(level):0.#} footprint, with a {voidGravityWidth:0.#}-wide gravity ring, for {Duration(level):0.#} s. No XP from swallowed enemies; it can swallow you too.",
            SkillId.VoidWalk => $"Blink up to {Range(level):0.#}; leave a small hole of diameter {voidWalkHoleDiameter:0.#} at both ends for {Duration(level):0.#} s. Only these two holes spare you.",
            SkillId.Singularity => $"Draw on-screen enemies {Strength(level):0.#} toward you at {singularitySlowMultiplier*100:0}% move speed. You cannot move or act for {Duration(level):0.#} s; then blast within {Radius(level):0.#} for {ScaledDamage(singularityBaseDamage, level):0.#}+{ScaledDamage(singularityDamagePerEnemy, level):0.#} per distinct absorbed enemy, growing with waves and Damage cards.",
            _ => descriptions[Mathf.Clamp(level - 1, 0, 2)]
        };

        public void SetDefaults(SkillId skill)
        {
            id = skill; greaterVoid = skill >= SkillId.PocketVoid; cooldown = 2f;
            switch (skill)
            {
                case SkillId.FlameWake:
                    displayName = "Flame Wake"; element = ElementKind.Fire; cost = 25f;
                    duration = new[] { 4f, 6f, 6f }; radius = new[] { 0.75f, 0.75f, 1.5f }; strength = new[] { 3f, 3f, 3f };
                    descriptions = new[] { "Leave a burning trail for 4 s: 3 Fire/s.", "Leave a burning trail for 6 s: 3 Fire/s.", "A 6 s trail, twice as wide: 3 Fire/s." }; break;
                case SkillId.Contagion:
                    displayName = "Contagion"; element = ElementKind.Poison; cost = 20f;
                    duration = new[] { 8f, 12f, 12f }; radius = new[] { 5f, 5f, 5f }; strength = new[] { 6f, 6f, 6f };
                    descriptions = new[] { "6 Poison. For 8 s, Poison jumps on death within 5.", "6 Poison. For 12 s, Poison jumps on death within 5.", "For 12 s, Poison jumps to the two nearest on death." }; break;
                case SkillId.FrostNova:
                    displayName = "Frost Nova"; element = ElementKind.Frost; cost = 30f;
                    radius = new[] { 4f, 5.5f, 5.5f }; strength = new[] { 6f, 6f, 6f };
                    descriptions = new[] { "6 Frost within 4: small enemies freeze.", "6 Frost within 5.5.", "6 Frost within 5.5; frozen enemies are knocked 50% further." }; break;
                case SkillId.StormCall:
                    displayName = "Storm Call"; element = ElementKind.Lightning; cost = 30f;
                    duration = new[] { 1.5f, 1.5f, 1.5f }; radius = new[] { 5f, 5f, 5f }; strength = new[] { 3f, 5f, 5f };
                    descriptions = new[] { "3 bolts over 1.5 s: 6 damage and 3 Lightning each.", "5 bolts over 1.5 s.", "Additional level-3 upgrade is not yet defined." }; break;
                case SkillId.Bulwark:
                    displayName = "Bulwark"; element = ElementKind.Earth; cost = 30f;
                    endKnockback = 5f;
                    duration = new[] { 5f, 8f, 8f }; radius = new[] { 4f, 4f, 4f }; strength = new[] { 10f, 15f, 15f };
                    descriptions = new[] { "A wall 4 wide for 5 s; gain 10 Shield.", "A wall for 8 s; gain 15 Shield.", "8 s wall; Medium push when it ends; 15 Shield." }; break;
                case SkillId.Radiance:
                    displayName = "Radiance"; element = ElementKind.Light; cost = 30f;
                    duration = new[] { 5f, 7f, 7f }; strength = new[] { 0.25f, 0.25f, 0.25f };
                    descriptions = new[] { "Clear statuses; hits heal 25% for 5 s.", "Clear statuses; hits heal 25% for 7 s.", "Clear statuses, heal 20 HP; hits heal 25% for 7 s." }; break;
                case SkillId.Devour:
                    displayName = "Devour"; element = ElementKind.Void; cost = 10f; cooldown = 6f;
                    range = new[] { 6f, 6f, 6f }; strength = new[] { 0.3f, 0.4f, 0.4f };
                    descriptions = new[] { "Swallow a non-elite below 30% HP within 6; gain 20 Energy.", "Target below 40% HP; gain 20 Energy.", "Target below 40% HP; cooldown 4 s; gain 20 Energy." }; break;
                case SkillId.Blink:
                    displayName = "Blink"; element = ElementKind.None; cost = 20f;
                    range = new[] { 6f, 8f, 8f };
                    descriptions = new[] { "Teleport up to 6; cancel knockback.", "Teleport up to 8; cancel knockback.", "Teleport up to 8; leave a decoy for 2 s." }; break;
                case SkillId.Shockwave:
                    displayName = "Shockwave"; element = ElementKind.None; cost = 30f;
                    radius = new[] { 4f, 5f, 5f }; strength = new[] { 8f, 8f, 8f };
                    descriptions = new[] { "Heavy push within 4.", "Heavy push within 5.", "Heavy push within 5; reflect enemy projectiles." }; break;
                case SkillId.GravityWell:
                    displayName = "Gravity Well"; element = ElementKind.None; cost = 35f;
                    radius = new[] { 3f, 3f, 3f }; duration = new[] { 3f, 4.5f, 4.5f }; strength = new[] { 4f, 4f, 4f };
                    descriptions = new[] { "Pull enemies within 3 at 4/s for 3 s.", "Pull for 4.5 s.", "Pull for 4.5 s; end with 10 damage and Launch push." }; break;
                case SkillId.TimeSlip:
                    displayName = "Time Slip"; element = ElementKind.None; cost = 40f;
                    duration = new[] { 2.5f, 3.5f, 3.5f }; strength = new[] { 0.3f, 0.3f, 0.3f };
                    descriptions = new[] { "Everything except you runs at 30% for 2.5 s.", "30% speed for 3.5 s.", "30% speed for 3.5 s; your Attack Speed +50%." }; break;
                case SkillId.Decoy:
                    displayName = "Decoy"; element = ElementKind.None; cost = 20f;
                    duration = new[] { 3f, 5f, 5f };
                    descriptions = new[] { "Enemies chase a dummy for 3 s.", "Enemies chase it for 5 s.", "5 s dummy; explodes for 8 damage and Medium push." }; break;
                case SkillId.Overcharge:
                    displayName = "Overcharge"; element = ElementKind.None; cost = 25f;
                    duration = new[] { 4f, 6f, 6f };
                    descriptions = new[] { "Hand-weapon cooldowns run twice as fast for 4 s.", "Twice as fast for 6 s.", "Twice as fast for 6 s; Knockback +50%." }; break;
                case SkillId.PocketVoid:
                    displayName = "Pocket Void"; element = ElementKind.Void; cost = 50f;
                    duration = new[] { 4f, 4f, 4f }; radius = new[] { 3f, 3f, 3f };
                    descriptions = new[] { "3×3 hole for 4 s, 1-wide gravity. It can swallow you too.", "3×3 hole for 4 s.", "3×3 hole for 4 s." }; break;
                case SkillId.VoidWalk:
                    displayName = "Void Walk"; element = ElementKind.Void; cost = 40f;
                    duration = new[] { 2f, 2f, 2f }; range = new[] { 8f, 8f, 8f };
                    voidWalkHoleDiameter = 3f; voidGravityWidth = 1f;
                    descriptions = new[] { "Blink; leave two small holes for 2 s.", "Blink; leave two small holes for 2 s.", "Blink; leave two small holes for 2 s." }; break;
                case SkillId.Singularity:
                    displayName = "Singularity"; element = ElementKind.Void; cost = 60f;
                    strength = new[] { 6f, 6f, 6f }; duration = new[] { 2f, 2f, 2f }; radius = new[] { 6f, 6f, 6f };
                    singularitySlowMultiplier = 0.5f; singularityBaseDamage = 10f; singularityDamagePerEnemy = 4f;
                    descriptions = new[] { "Draw on-screen enemies 6 toward you and slow them; lock yourself for 2 s, then blast based on distinct absorbed enemies.", "Singularity.", "Singularity." }; break;
            }
        }
    }
}
