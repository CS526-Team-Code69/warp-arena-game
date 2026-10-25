using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        public SkillId id;
        public string displayName;
        public ElementKind element;
        [Tooltip("Draft band (system doc §11.2): the waves that may offer this skill as a NEW card. Upgrades of an owned skill ignore it.")]
        public SkillCategory category;
        public bool greaterVoid;
        public float cost = 30f;
        public float cooldown = 2f;
        [Tooltip("Devour's level-3 cooldown.")] public float advancedCooldown = 4f;
        public float[] duration = { 0f, 0f, 0f };
        public float[] radius = { 0f, 0f, 0f };
        public float[] range = { 12f, 12f, 12f };
        public float[] strength = { 0f, 0f, 0f };
        [Tooltip("Storm Call direct damage per bolt.")] public float hitDamage = 6f;
        [Tooltip("Storm Call Lightning stacks per bolt. Levels 1-2 do not chain; level 3 chains through ElementCombat.ApplyStacks.")] public float hitStacks = 3f;
        [Tooltip("Gravity Well level 3 ending hit.")] public float endDamage = 10f;
        [Tooltip("Gravity Well level 3 ending push.")] public float endKnockback = 10f;
        [Tooltip("Frost Nova level 3: an enemy frozen by the cast stays frozen at least this many seconds in total (rules freeze 1.5 s + 1.5 s extra).")]
        [Min(0f)] public float freezeHold = 3f;
        [Tooltip("Bulwark: knockback distance per level for enemies that touch the wall (independent of Strength, which is the shield).")]
        public float[] contactKnockback = { 0f, 0f, 0f };
        [Tooltip("Radiance: status resistance per level while active, 0..1. Stacks enemies put on the player are multiplied by (1 - resistance).")]
        public float[] statusResistance = { 0f, 0f, 0f };
        [Tooltip("Radiance level 3 immediate heal.")] public float healOnCast = 20f;
        [Tooltip("Devour payout after its Energy cost.")] public float energyReward = 20f;
        [Tooltip("Devour can feed a hole within this distance.")] public float feedRange = 10f;
        [Tooltip("Candidate spacing between Flame Wake trail samples.")] public float trailSpacing = 0.5f;
        [Tooltip("Overcharge: cooldown timer rate of every weapon (hand and auto) while active.")]
        [Min(1f)] public float overchargeRate = 2f;
        [Tooltip("Overcharge level 3: weapon damage multiplier while active.")]
        [Min(1f)] public float overchargeDamageMultiplier = 1.25f;
        [Header("Greater Void — latest user revision")]
        [Tooltip("Gravity ring width around a Pocket Void hole, in grid units. User-specified 1.")]
        [Min(0f)] public float voidGravityWidth = 1f;
        [Tooltip("Void Walk: player move speed multiplier during the void state.")]
        [Min(1f)] public float voidWalkMoveMultiplier = 1.6f;
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
        public float ContactKnockback(int level) => At(contactKnockback, level);
        public float StatusResistance(int level) => Mathf.Clamp01(At(statusResistance, level));
        static float At(float[] values, int level) => values is { Length: > 0 } ? values[Mathf.Clamp(level - 1, 0, values.Length - 1)] : 0f;
        [Min(1f), Tooltip("Exponential level growth: direct damage × this^(level-1), then × wave scale R(w) and the player's damage multiplier.")]
        public float levelDamageGrowth = 1.35f;
        [Tooltip("Bulwark: Strength is a fraction of max HP given as Shield (0.2 = 20% max HP); off = flat points.")]
        public bool shieldScalesWithMaxHp = true;

        public float ScaledDamage(float baseDamage, int level, float damageMultiplier = 1f) =>
            baseDamage * ElementCombat.WaveScale * Mathf.Pow(Mathf.Max(1f, levelDamageGrowth), Mathf.Clamp(level, 1, 3) - 1) * Mathf.Max(0f, damageMultiplier);
        public float Cooldown(int level) => id == SkillId.Devour && level >= 3 ? advancedCooldown : cooldown;
        public string Description(int level) => ArenaText.Pair(EnglishDescription(level),EnglishDescription(level));
        string ChineseDescription(int level) =>EnglishDescription(level);
        string EnglishDescription(int level) => id switch
        {
            SkillId.FlameWake => $"Leave a burning trail that sets touching enemies alight ({Duration(level):0.#} s, {Strength(level):0.#} Fire/s)."
                + (level >= 2 ? " Lasts longer" + (level >= 3 ? "; at max level the trail is wider" : "") + "." : ""),
            SkillId.Contagion => $"Poison nearby enemies; meanwhile a poisoned enemy's death passes the poison to a neighbour ({Strength(level):0.#} Poison within {Radius(level):0.#}, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= 3 ? "; at max level it jumps to two enemies, spreading outward" : "") + "." : ""),
            SkillId.FrostNova => $"Frost on the enemies around you; small enemies freeze at once ({Strength(level):0.#} Frost within {Radius(level):0.#})."
                + (level >= 2 ? " Larger area" + (level >= 3 ? $"; at max level frozen enemies stay frozen at least {freezeHold:0.#} s" : "") + "." : ""),
            SkillId.StormCall => $"Random lightning near the cursor: damage plus Lightning ({Strength(level):0} bolts within {Radius(level):0.#} over {Duration(level):0.#} s; {ScaledDamage(hitDamage, level):0.#} damage and {hitStacks:0.#} Lightning each, damage grows with waves and Damage cards)."
                + (level >= 2 ? " More bolts, wider area" + (level >= 3 ? "; at max level a hit also chains to nearby enemies" : "") + "." : ""),
            SkillId.Bulwark => $"An unbreakable wall at the cursor knocks back enemies that run into it, and you gain a Shield (wall {Radius(level):0.#} wide, {Duration(level):0.#} s, knockback {ContactKnockback(level):0.#}, "
                + (shieldScalesWithMaxHp ? $"Shield {Strength(level) * 100f:0}% of max HP)." : $"Shield {Strength(level):0.#}).")
                + (level >= 2 ? " Thicker Shield, farther knockback, longer wall" + (level >= 3 ? "; at max level a very thick Shield" : "") + "." : ""),
            SkillId.Radiance => $"Clear your statuses; then your attacks heal you and status resistance rises ({Duration(level):0.#} s, {Strength(level) * 100f:0}% lifesteal, {StatusResistance(level) * 100f:0}% resistance)."
                + (level >= 2 ? " Lasts longer, more resistance" + (level >= 3 ? $"; at max level also heals {healOnCast:0.#} HP at once" : "") + "." : ""),
            SkillId.Devour => $"Swallow a nearby low-HP enemy and restore Energy (below {Strength(level) * 100f:0}% HP within {Range(level):0.#}, elites too, never bosses; {energyReward:0.#} Energy, cooldown {Cooldown(level):0.#} s)."
                + (level >= 2 ? " Higher HP line" + (level >= 3 ? "; at max level a shorter cooldown" : "") + "." : ""),
            SkillId.Blink => $"Teleport toward the cursor, cancelling any knockback (up to {Range(level):0.#})."
                + (level >= 2 ? " Farther" + (level >= 3 ? "; at max level two charges, each with its own cooldown" : "") + "." : ""),
            SkillId.Shockwave => $"Shove every enemy around you away hard (push {Strength(level):0.#} within {Radius(level):0.#})."
                + (level >= 2 ? " Larger area" + (level >= 3 ? "; at max level it also reflects enemy projectiles" : "") + "." : ""),
            SkillId.GravityWell => $"A gravity well at the target point pulls nearby enemies to its centre (pull {Strength(level):0.#}/s within {Radius(level):0.#}, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= 3 ? $"; at max level it explodes at the end ({ScaledDamage(endDamage, level):0.#} damage, push {endKnockback:0.#}; damage grows with waves)" : "") + "." : ""),
            SkillId.TimeSlip => $"Everything but you slows down ({Strength(level) * 100f:0}% speed, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= 3 ? "; at max level +50% Attack Speed meanwhile" : "") + "." : ""),
            SkillId.Decoy => $"A mirror image of you that enemies chase ({Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= 3 ? "; at max level, when it dies it casts one of your other skills at random for free (at that skill's level)" : "") + "." : ""),
            SkillId.Overcharge => $"Every weapon attacks twice as often (hand and auto, cooldowns x{overchargeRate:0.#}, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= 3 ? $"; at max level weapons also deal +{(overchargeDamageMultiplier - 1f) * 100f:0}% damage" : "") + "." : ""),
            SkillId.PocketVoid => $"A small black hole at the cursor swallows everything but bosses, and you gain the XP of monsters it swallows ({Radius(level):0.#}×{Radius(level):0.#} footprint, {voidGravityWidth:0.#}-wide gravity ring, {Duration(level):0.#} s; it can swallow you too).",
            SkillId.VoidWalk => $"Enter the void: faster, untargetable and undamageable, breaking any knockback ({Duration(level):0.#} s, move speed x{voidWalkMoveMultiplier:0.#}).",
            SkillId.Singularity => $"Drag every enemy on screen to you and slow them; you hold still like a black hole, then explode, harder the more enemies you drew in (pull {Strength(level):0.#}, slow to {singularitySlowMultiplier * 100:0}%, held {Duration(level):0.#} s, blast radius {Radius(level):0.#}, {ScaledDamage(singularityBaseDamage, level):0.#} + {ScaledDamage(singularityDamagePerEnemy, level):0.#} per distinct enemy; grows with waves and Damage cards).",
            _ => descriptions[Mathf.Clamp(level - 1, 0, 2)]
        };

        public void SetDefaults(SkillId skill)
        {
            id = skill; greaterVoid = skill >= SkillId.PocketVoid; cooldown = 2f;
            switch (skill)
            {
                case SkillId.FlameWake:
                    displayName = "Flame Wake"; element = ElementKind.Fire; category = SkillCategory.BasicElement; cost = 25f;
                    duration = new[] { 4f, 6f, 6f }; radius = new[] { 0.75f, 0.75f, 1.5f }; strength = new[] { 3f, 3f, 3f };
                    descriptions = new[] { "Leave a burning trail for 4 s: 3 Fire/s.", "Leave a burning trail for 6 s: 3 Fire/s.", "A 6 s trail, twice as wide: 3 Fire/s." }; break;
                case SkillId.Contagion:
                    displayName = "Contagion"; element = ElementKind.Poison; category = SkillCategory.BasicElement; cost = 20f;
                    duration = new[] { 8f, 12f, 12f }; radius = new[] { 5f, 5f, 5f }; strength = new[] { 6f, 6f, 6f };
                    descriptions = new[] { "6 Poison to all within 5. For 8 s, Poison jumps on death within 5.", "6 Poison to all within 5. For 12 s, Poison jumps on death within 5.", "6 Poison to all within 5. For 12 s, Poison jumps to the two nearest on death." }; break;
                case SkillId.FrostNova:
                    displayName = "Frost Nova"; element = ElementKind.Frost; category = SkillCategory.BasicElement; cost = 30f;
                    radius = new[] { 4f, 5.5f, 5.5f }; strength = new[] { 6f, 6f, 6f }; freezeHold = 3f;
                    descriptions = new[] { "6 Frost within 4: small enemies freeze.", "6 Frost within 5.5.", "6 Frost within 5.5; enemies it freezes stay frozen at least 3 s." }; break;
                case SkillId.StormCall:
                    displayName = "Storm Call"; element = ElementKind.Lightning; category = SkillCategory.BasicElement; cost = 30f;
                    duration = new[] { 1.5f, 1.5f, 1.5f }; radius = new[] { 5f, 6f, 6f }; strength = new[] { 3f, 5f, 6f };
                    descriptions = new[] { "3 bolts within 5 over 1.5 s: 6 damage and 3 Lightning each.", "5 bolts within 6 over 1.5 s.", "6 bolts within 6 over 1.5 s; bolts chain to nearby enemies." }; break;
                case SkillId.Bulwark:
                    displayName = "Bulwark"; element = ElementKind.Earth; category = SkillCategory.BasicElement; cost = 30f;
                    duration = new[] { 8f, 8f, 8f }; radius = new[] { 4f, 5f, 6f }; strength = new[] { 0.15f, 0.25f, 0.4f };
                    contactKnockback = new[] { 4f, 6f, 8f }; shieldScalesWithMaxHp = true;
                    descriptions = new[] { "Unbreakable wall 4 wide for 8 s, contact push 4; Shield 15% max HP.", "Wall 5 wide, contact push 6; Shield 25% max HP.", "Wall 6 wide, contact push 8; Shield 40% max HP." }; break;
                case SkillId.Radiance:
                    displayName = "Radiance"; element = ElementKind.Light; category = SkillCategory.AdvancedElement; cost = 30f;
                    duration = new[] { 5f, 7f, 7f }; strength = new[] { 0.25f, 0.25f, 0.25f }; statusResistance = new[] { 0.3f, 0.5f, 0.7f };
                    descriptions = new[] { "Clear statuses; for 5 s hits heal 25%, status resistance 30%.", "For 7 s hits heal 25%, status resistance 50%.", "Heal 20 HP; for 7 s hits heal 25%, status resistance 70%." }; break;
                case SkillId.Devour:
                    displayName = "Devour"; element = ElementKind.Void; category = SkillCategory.AdvancedElement; cost = 10f; cooldown = 6f; advancedCooldown = 4f;
                    range = new[] { 6f, 6f, 6f }; strength = new[] { 0.3f, 0.4f, 0.5f };
                    descriptions = new[] { "Swallow an enemy (elites too, not bosses) below 30% HP within 6; gain 20 Energy.", "Target below 40% HP; gain 20 Energy.", "Target below 50% HP; cooldown 4 s; gain 20 Energy." }; break;
                case SkillId.Blink:
                    displayName = "Blink"; element = ElementKind.None; category = SkillCategory.BasicMechanic; cost = 20f;
                    range = new[] { 6f, 8f, 8f };
                    descriptions = new[] { "Teleport up to 6.", "Teleport up to 8.", "Teleport up to 8; two charges, each with its own cooldown." }; break;
                case SkillId.Shockwave:
                    displayName = "Shockwave"; element = ElementKind.None; category = SkillCategory.AdvancedMechanic; cost = 30f;
                    radius = new[] { 4f, 5f, 5f }; strength = new[] { 8f, 8f, 8f };
                    descriptions = new[] { "Heavy push within 4.", "Heavy push within 5.", "Heavy push within 5; reflect enemy projectiles." }; break;
                case SkillId.GravityWell:
                    displayName = "Gravity Well"; element = ElementKind.None; category = SkillCategory.AdvancedMechanic; cost = 35f;
                    radius = new[] { 3f, 3f, 3f }; duration = new[] { 3f, 4.5f, 4.5f }; strength = new[] { 4f, 4f, 4f };
                    descriptions = new[] { "Pull enemies within 3 at 4/s for 3 s.", "Pull for 4.5 s.", "Pull for 4.5 s; end with 10 damage and Launch push." }; break;
                case SkillId.TimeSlip:
                    displayName = "Time Stop"; element = ElementKind.None; category = SkillCategory.BasicMechanic; cost = 40f;
                    duration = new[] { 2.5f, 3.5f, 3.5f }; strength = new[] { 0.3f, 0.3f, 0.3f };
                    descriptions = new[] { "Everything except you runs at 30% for 2.5 s.", "30% speed for 3.5 s.", "30% speed for 3.5 s; your Attack Speed +50%." }; break;
                case SkillId.Decoy:
                    displayName = "Mirror Image"; element = ElementKind.None; category = SkillCategory.AdvancedMechanic; cost = 20f;
                    duration = new[] { 3f, 5f, 5f };
                    descriptions = new[] { "Enemies chase a mirror image for 3 s.", "Enemies chase it for 5 s.", "5 s mirror; when it ends, another random skill of yours is cast there for free." }; break;
                case SkillId.Overcharge:
                    displayName = "Overcharge"; element = ElementKind.None; category = SkillCategory.BasicMechanic; cost = 25f;
                    duration = new[] { 4f, 6f, 6f }; overchargeRate = 2f; overchargeDamageMultiplier = 1.25f;
                    descriptions = new[] { "All weapon cooldowns run twice as fast for 4 s.", "Twice as fast for 6 s.", "Twice as fast for 6 s; weapon damage +25%." }; break;
                case SkillId.PocketVoid:
                    displayName = "Pocket Void"; element = ElementKind.Void; cost = 50f;
                    duration = new[] { 4f, 4f, 4f }; radius = new[] { 3f, 3f, 3f }; voidGravityWidth = 1f;
                    descriptions = new[] { "3x3 hole for 4 s, 1-wide gravity. Swallowed enemies give XP; bosses are immune; it can swallow you too.", "3x3 hole for 4 s.", "3x3 hole for 4 s." }; break;
                case SkillId.VoidWalk:
                    displayName = "Void Walk"; element = ElementKind.Void; cost = 40f;
                    duration = new[] { 3f, 3f, 3f }; voidWalkMoveMultiplier = 1.6f;
                    descriptions = new[] { "Enter the void for 3 s: move x1.6, cannot be targeted or damaged.", "Void state for 3 s.", "Void state for 3 s." }; break;
                case SkillId.Singularity:
                    displayName = "Singularity"; element = ElementKind.Void; cost = 60f;
                    strength = new[] { 6f, 6f, 6f }; duration = new[] { 2f, 2f, 2f }; radius = new[] { 6f, 6f, 6f };
                    singularitySlowMultiplier = 0.5f; singularityBaseDamage = 10f; singularityDamagePerEnemy = 4f;
                    descriptions = new[] { "Draw on-screen enemies 6 toward you and slow them; lock yourself for 2 s, then blast based on distinct absorbed enemies.", "Singularity.", "Singularity." }; break;
            }
        }
    }
}
