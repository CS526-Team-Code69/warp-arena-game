using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {

        public const int MaxLevel = 5;
        public SkillId id;
        public string displayName;
        public ElementKind element;
        [Tooltip("Draft band (system doc §11.2): the waves that may offer this skill as a NEW card. Upgrades of an owned skill ignore it.")]
        public SkillCategory category;
        public bool greaterVoid;
        public float cost = 30f;
        public float cooldown = 2f;
        [Tooltip("Devour's max-level (level 5) cooldown.")] public float advancedCooldown = 4f;
        [Tooltip("One value per level, levels 1 to 5 (seconds; what it times depends on the skill).")]
        public float[] duration = { 0f, 0f, 0f, 0f, 0f };
        [Tooltip("One value per level, levels 1 to 5 (area radius, trail half-width or wall width, depending on the skill).")]
        public float[] radius = { 0f, 0f, 0f, 0f, 0f };
        [Tooltip("One value per level, levels 1 to 5: how far from the player the skill may land or reach.")]
        public float[] range = { 12f, 12f, 12f, 12f, 12f };
        [Tooltip("One value per level, levels 1 to 5 (stacks, bolts, pull, shield share, HP line or speed, depending on the skill).")]
        public float[] strength = { 0f, 0f, 0f, 0f, 0f };
        [Tooltip("Storm Call direct damage per bolt.")] public float hitDamage = 6f;
        [Tooltip("Storm Call Lightning stacks per bolt. Levels 1-4 do not chain; level 5 chains through ElementCombat.ApplyStacks.")] public float hitStacks = 3f;
        [Tooltip("Gravity Well level 5 ending hit.")] public float endDamage = 10f;
        [Tooltip("Gravity Well level 5 ending push.")] public float endKnockback = 10f;
        [Tooltip("Frost Nova level 5: an enemy frozen by the cast stays frozen at least this many seconds in total (rules freeze 1.5 s + 1.5 s extra).")]
        [Min(0f)] public float freezeHold = 3f;
        [Tooltip("Bulwark: knockback distance per level (levels 1 to 5) for enemies that touch the wall (independent of Strength, which is the shield).")]
        public float[] contactKnockback = { 0f, 0f, 0f, 0f, 0f };
        [Tooltip("Radiance: status resistance per level (levels 1 to 5) while active, 0..1. Stacks enemies put on the player are multiplied by (1 - resistance).")]
        public float[] statusResistance = { 0f, 0f, 0f, 0f, 0f };
        [Tooltip("Radiance level 5 immediate heal.")] public float healOnCast = 20f;
        [Tooltip("Devour payout after its Energy cost.")] public float energyReward = 20f;
        [Tooltip("Devour can feed a hole within this distance.")] public float feedRange = 10f;
        [Tooltip("Candidate spacing between Flame Wake trail samples.")] public float trailSpacing = 0.5f;
        [Tooltip("Overcharge: cooldown timer rate of every weapon (hand and auto) while active.")]
        [Min(1f)] public float overchargeRate = 2f;
        [Tooltip("Overcharge level 5: weapon damage multiplier while active.")]
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
        [Tooltip("Singularity: the player's move speed multiplier while it gathers, until the blast (owner 2026-10-26: a heavy slow instead of "
            + "the old root; the player can still fire and cast). 0.35 = 35% speed.")]
        [Range(0f, 1f)] public float singularityPlayerMoveMultiplier = 0.35f;
        [Tooltip("Inspector notes per level, levels 1 to 5. The game shows the bilingual description written in code; these are only the fallback for a skill without one.")]
        public string[] descriptions = { "", "", "", "", "" };
        public Texture2D icon;
        public float Duration(int level) => At(duration, level);
        public float Radius(int level) => At(radius, level);
        public float Range(int level) => At(range, level);
        public float Strength(int level) => At(strength, level);
        public float ContactKnockback(int level) => At(contactKnockback, level);
        public float StatusResistance(int level) => Mathf.Clamp01(At(statusResistance, level));

        static float At(float[] values, int level) => values is { Length: > 0 } ? values[Mathf.Clamp(level - 1, 0, values.Length - 1)] : 0f;
        [Min(1f), Tooltip("Growth of direct skill damage per level: × this^(level-1), then × wave scale R(w) and the player's damage multiplier. "
            + "1.161895 (the square root of 1.35) makes level 5 hit 1.82 times as hard as level 1, as level 3 did when skills had three levels.")]
        public float levelDamageGrowth = 1.161895f;
        [Tooltip("Bulwark: Strength is a fraction of max HP given as Shield (0.2 = 20% max HP); off = flat points.")]
        public bool shieldScalesWithMaxHp = true;

        public float ScaledDamage(float baseDamage, int level, float damageMultiplier = 1f) =>
            baseDamage * ElementCombat.WaveScale * Mathf.Pow(Mathf.Max(1f, levelDamageGrowth), Mathf.Clamp(level, 1, MaxLevel) - 1) * Mathf.Max(0f, damageMultiplier);
        public float Cooldown(int level) => id == SkillId.Devour && level >= MaxLevel ? advancedCooldown : cooldown;
        public string Description(int level) => ArenaText.Pair(EnglishDescription(level),EnglishDescription(level));
        string ChineseDescription(int level) =>EnglishDescription(level);
        string EnglishDescription(int level) => id switch
        {
            SkillId.FlameWake => $"Leave a burning trail that sets touching enemies alight ({Duration(level):0.#} s, {Strength(level):0.#} Fire/s)."
                + (level >= 2 ? " Lasts longer" + (level >= MaxLevel ? "; at max level the trail is wider" : "") + "." : ""),
            SkillId.Contagion => $"Poison nearby enemies; meanwhile a poisoned enemy's death passes the poison to a neighbour ({Strength(level):0.#} Poison within {Radius(level):0.#}, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= MaxLevel ? "; at max level it jumps to two enemies, spreading outward" : "") + "." : ""),
            SkillId.FrostNova => $"Frost on the enemies around you; small enemies freeze at once ({Strength(level):0.#} Frost within {Radius(level):0.#})."
                + (level >= 2 ? " Larger area" + (level >= MaxLevel ? $"; at max level frozen enemies stay frozen at least {freezeHold:0.#} s" : "") + "." : ""),
            SkillId.StormCall => $"Random lightning near the cursor: damage plus Lightning ({Strength(level):0} bolts within {Radius(level):0.#} over {Duration(level):0.#} s; {ScaledDamage(hitDamage, level):0.#} damage and {hitStacks:0.#} Lightning each, damage grows with waves and Damage cards)."
                + (level >= 2 ? " More bolts, wider area" + (level >= MaxLevel ? "; at max level a hit also chains to nearby enemies" : "") + "." : ""),
            SkillId.Bulwark => $"A wall at the cursor knocks back enemies that run into it; only the Tyrant King's charge can break it. You also gain a Shield (wall {Radius(level):0.#} wide, {Duration(level):0.#} s, knockback {ContactKnockback(level):0.#}, "
                + (shieldScalesWithMaxHp ? $"Shield {Strength(level) * 100f:0}% of max HP)." : $"Shield {Strength(level):0.#}).")
                + (level >= 2 ? " Thicker Shield, farther knockback, longer wall" + (level >= MaxLevel ? "; at max level a very thick Shield" : "") + "." : ""),
            SkillId.Radiance => $"Clear your statuses; then your attacks heal you and status resistance rises ({Duration(level):0.#} s, {Strength(level) * 100f:0}% lifesteal, {StatusResistance(level) * 100f:0}% resistance)."
                + (level >= 2 ? " Lasts longer, more resistance" + (level >= MaxLevel ? $"; at max level also heals {healOnCast:0.#} HP at once" : "") + "." : ""),
            SkillId.Devour => $"Swallow a nearby low-HP enemy and restore Energy (below {Strength(level) * 100f:0}% HP within {Range(level):0.#}, elites too, never bosses; {energyReward:0.#} Energy, cooldown {Cooldown(level):0.#} s)."
                + (level >= 2 ? " Higher HP line" + (level >= MaxLevel ? "; at max level a shorter cooldown" : "") + "." : ""),
            SkillId.Blink => $"Teleport toward the cursor, cancelling any knockback (up to {Range(level):0.#})."
                + (level >= 2 ? " Farther" + (level >= MaxLevel ? "; at max level two charges, each with its own cooldown" : "") + "." : ""),
            SkillId.Shockwave => $"Shove every enemy around you away hard (push {Strength(level):0.#} within {Radius(level):0.#})."
                + (level >= 2 ? " Larger area" + (level >= MaxLevel ? "; at max level it also reflects enemy projectiles" : "") + "." : ""),
            SkillId.GravityWell => $"A gravity well at the target point pulls nearby enemies to its centre (pull {Strength(level):0.#}/s within {Radius(level):0.#}, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= MaxLevel ? $"; at max level it explodes at the end ({ScaledDamage(endDamage, level):0.#} damage, push {endKnockback:0.#}; damage grows with waves)" : "") + "." : ""),
            SkillId.TimeSlip => $"Everything but you slows down ({Strength(level) * 100f:0}% speed, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= MaxLevel ? "; at max level +50% Attack Speed meanwhile" : "") + "." : ""),
            SkillId.Decoy => $"A mirror image of you that enemies chase ({Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= MaxLevel ? "; at max level, when it dies it casts one of your other skills at random for free (at that skill's level)" : "") + "." : ""),
            SkillId.Overcharge => $"Every weapon attacks twice as often (hand and auto, cooldowns x{overchargeRate:0.#}, {Duration(level):0.#} s)."
                + (level >= 2 ? " Lasts longer" + (level >= MaxLevel ? $"; at max level weapons also deal +{(overchargeDamageMultiplier - 1f) * 100f:0}% damage" : "") + "." : ""),
            SkillId.PocketVoid => $"A small black hole at the cursor swallows everything but bosses, and you gain the XP of monsters it swallows ({Radius(level):0.#}×{Radius(level):0.#} footprint, {voidGravityWidth:0.#}-wide gravity ring, {Duration(level):0.#} s; it can swallow you too).",
            SkillId.VoidWalk => $"Enter the void: faster, untargetable and undamageable, breaking any knockback ({Duration(level):0.#} s, move speed x{voidWalkMoveMultiplier:0.#}).",
            SkillId.Singularity => $"Drag every enemy on screen to you and slow them; after {Duration(level):0.#} s it explodes, harder the more enemies it drew in. You are heavily slowed meanwhile but can fire and cast (pull {Strength(level):0.#}, enemies at {singularitySlowMultiplier * 100:0}%, you at {singularityPlayerMoveMultiplier * 100:0}%, blast radius {Radius(level):0.#}, {ScaledDamage(singularityBaseDamage, level):0.#} + {ScaledDamage(singularityDamagePerEnemy, level):0.#} per enemy; grows with waves and Damage cards).",
            _ => descriptions is { Length: > 0 } ? descriptions[Mathf.Clamp(level - 1, 0, descriptions.Length - 1)] : ""
        };

        public void SetDefaults(SkillId skill)
        {
            id = skill; greaterVoid = skill >= SkillId.PocketVoid; cooldown = 2f;
            switch (skill)
            {
                case SkillId.FlameWake:
                    displayName = "Flame Wake"; element = ElementKind.Fire; category = SkillCategory.BasicElement; cost = 25f;
                    duration = new[] { 4f, 4.5f, 5f, 5.5f, 6f }; radius = new[] { 0.75f, 0.75f, 0.75f, 0.75f, 1.5f }; strength = new[] { 3f, 3f, 3f, 3f, 3f };
                    descriptions = new[] { "Leave a burning trail for 4 s: 3 Fire/s.", "Trail for 4.5 s.", "Trail for 5 s.", "Trail for 5.5 s.", "A 6 s trail, twice as wide: 3 Fire/s." }; break;
                case SkillId.Contagion:
                    displayName = "Contagion"; element = ElementKind.Poison; category = SkillCategory.BasicElement; cost = 20f;
                    duration = new[] { 8f, 9f, 10f, 11f, 12f }; radius = new[] { 5f, 5f, 5f, 5f, 5f }; strength = new[] { 6f, 6f, 6f, 6f, 6f };
                    descriptions = new[] { "6 Poison to all within 5. For 8 s, Poison jumps on death within 5.", "Poison jumps for 9 s.", "Poison jumps for 10 s.", "Poison jumps for 11 s.", "6 Poison to all within 5. For 12 s, Poison jumps to the two nearest on death." }; break;
                case SkillId.FrostNova:
                    displayName = "Frost Nova"; element = ElementKind.Frost; category = SkillCategory.BasicElement; cost = 30f;
                    radius = new[] { 4f, 4.375f, 4.75f, 5.125f, 5.5f }; strength = new[] { 6f, 6f, 6f, 6f, 6f }; freezeHold = 3f;
                    descriptions = new[] { "6 Frost within 4: small enemies freeze.", "6 Frost within 4.375.", "6 Frost within 4.75.", "6 Frost within 5.125.", "6 Frost within 5.5; enemies it freezes stay frozen at least 3 s." }; break;
                case SkillId.StormCall:
                    displayName = "Storm Call"; element = ElementKind.Lightning; category = SkillCategory.BasicElement; cost = 30f;

                    duration = new[] { 1.5f, 1.5f, 1.5f, 1.5f, 1.5f }; radius = new[] { 5f, 5.25f, 5.5f, 5.75f, 6f }; strength = new[] { 3f, 4f, 5f, 5f, 6f };
                    descriptions = new[] { "3 bolts within 5 over 1.5 s: 6 damage and 3 Lightning each.", "4 bolts within 5.25 over 1.5 s.", "5 bolts within 5.5 over 1.5 s.", "5 bolts within 5.75 over 1.5 s.", "6 bolts within 6 over 1.5 s; bolts chain to nearby enemies." }; break;
                case SkillId.Bulwark:
                    displayName = "Bulwark"; element = ElementKind.Earth; category = SkillCategory.BasicElement; cost = 30f;
                    duration = new[] { 8f, 8f, 8f, 8f, 8f }; radius = new[] { 4f, 4.5f, 5f, 5.5f, 6f }; strength = new[] { 0.15f, 0.2125f, 0.275f, 0.3375f, 0.4f };
                    contactKnockback = new[] { 4f, 5f, 6f, 7f, 8f }; shieldScalesWithMaxHp = true;
                    descriptions = new[] { "Wall 4 wide for 8 s, broken only by the Tyrant King charge, contact push 4; Shield 15% max HP.", "Wall 4.5 wide, contact push 5; Shield 21.25% max HP.", "Wall 5 wide, contact push 6; Shield 27.5% max HP.", "Wall 5.5 wide, contact push 7; Shield 33.75% max HP.", "Wall 6 wide, contact push 8; Shield 40% max HP." }; break;
                case SkillId.Radiance:
                    displayName = "Radiance"; element = ElementKind.Light; category = SkillCategory.AdvancedElement; cost = 30f;
                    duration = new[] { 5f, 5.5f, 6f, 6.5f, 7f }; strength = new[] { 0.25f, 0.25f, 0.25f, 0.25f, 0.25f }; statusResistance = new[] { 0.3f, 0.4f, 0.5f, 0.6f, 0.7f };
                    descriptions = new[] { "Clear statuses; for 5 s hits heal 25%, status resistance 30%.", "For 5.5 s hits heal 25%, status resistance 40%.", "For 6 s hits heal 25%, status resistance 50%.", "For 6.5 s hits heal 25%, status resistance 60%.", "Heal 20 HP; for 7 s hits heal 25%, status resistance 70%." }; break;
                case SkillId.Devour:
                    displayName = "Devour"; element = ElementKind.Void; category = SkillCategory.AdvancedElement; cost = 10f; cooldown = 6f; advancedCooldown = 4f;
                    range = new[] { 6f, 6f, 6f, 6f, 6f }; strength = new[] { 0.3f, 0.35f, 0.4f, 0.45f, 0.5f };
                    descriptions = new[] { "Swallow an enemy (elites too, not bosses) below 30% HP within 6; gain 20 Energy.", "Target below 35% HP; gain 20 Energy.", "Target below 40% HP; gain 20 Energy.", "Target below 45% HP; gain 20 Energy.", "Target below 50% HP; cooldown 4 s; gain 20 Energy." }; break;
                case SkillId.Blink:
                    displayName = "Blink"; element = ElementKind.None; category = SkillCategory.BasicMechanic; cost = 20f;
                    range = new[] { 6f, 6.5f, 7f, 7.5f, 8f };
                    descriptions = new[] { "Teleport up to 6.", "Teleport up to 6.5.", "Teleport up to 7.", "Teleport up to 7.5.", "Teleport up to 8; two charges, each with its own cooldown." }; break;
                case SkillId.Shockwave:
                    displayName = "Shockwave"; element = ElementKind.None; category = SkillCategory.AdvancedMechanic; cost = 30f;
                    radius = new[] { 4f, 4.25f, 4.5f, 4.75f, 5f }; strength = new[] { 8f, 8f, 8f, 8f, 8f };
                    descriptions = new[] { "Heavy push within 4.", "Heavy push within 4.25.", "Heavy push within 4.5.", "Heavy push within 4.75.", "Heavy push within 5; reflect enemy projectiles." }; break;
                case SkillId.GravityWell:
                    displayName = "Gravity Well"; element = ElementKind.None; category = SkillCategory.AdvancedMechanic; cost = 35f;
                    radius = new[] { 3f, 3f, 3f, 3f, 3f }; duration = new[] { 3f, 3.375f, 3.75f, 4.125f, 4.5f }; strength = new[] { 4f, 4f, 4f, 4f, 4f };
                    descriptions = new[] { "Pull enemies within 3 at 4/s for 3 s.", "Pull for 3.375 s.", "Pull for 3.75 s.", "Pull for 4.125 s.", "Pull for 4.5 s; end with 10 damage and Launch push." }; break;
                case SkillId.TimeSlip:
                    displayName = "Time Stop"; element = ElementKind.None; category = SkillCategory.BasicMechanic; cost = 40f;
                    duration = new[] { 2.5f, 2.75f, 3f, 3.25f, 3.5f }; strength = new[] { 0.3f, 0.3f, 0.3f, 0.3f, 0.3f };
                    descriptions = new[] { "Everything except you runs at 30% for 2.5 s.", "30% speed for 2.75 s.", "30% speed for 3 s.", "30% speed for 3.25 s.", "30% speed for 3.5 s; your Attack Speed +50%." }; break;
                case SkillId.Decoy:
                    displayName = "Mirror Image"; element = ElementKind.None; category = SkillCategory.AdvancedMechanic; cost = 20f;
                    duration = new[] { 3f, 3.5f, 4f, 4.5f, 5f };
                    descriptions = new[] { "Enemies chase a mirror image for 3 s.", "Enemies chase it for 3.5 s.", "Enemies chase it for 4 s.", "Enemies chase it for 4.5 s.", "5 s mirror; when it ends, another random skill of yours is cast there for free." }; break;
                case SkillId.Overcharge:
                    displayName = "Overcharge"; element = ElementKind.None; category = SkillCategory.BasicMechanic; cost = 25f;
                    duration = new[] { 4f, 4.5f, 5f, 5.5f, 6f }; overchargeRate = 2f; overchargeDamageMultiplier = 1.25f;
                    descriptions = new[] { "All weapon cooldowns run twice as fast for 4 s.", "Twice as fast for 4.5 s.", "Twice as fast for 5 s.", "Twice as fast for 5.5 s.", "Twice as fast for 6 s; weapon damage +25%." }; break;
                case SkillId.PocketVoid:
                    displayName = "Pocket Void"; element = ElementKind.Void; cost = 50f;
                    duration = new[] { 4f, 4f, 4f, 4f, 4f }; radius = new[] { 3f, 3f, 3f, 3f, 3f }; voidGravityWidth = 1f;
                    descriptions = new[] { "3x3 hole for 4 s, 1-wide gravity. Swallowed enemies give XP; bosses are immune; it can swallow you too.", "3x3 hole for 4 s.", "3x3 hole for 4 s.", "3x3 hole for 4 s.", "3x3 hole for 4 s." }; break;
                case SkillId.VoidWalk:
                    displayName = "Void Walk"; element = ElementKind.Void; cost = 40f;
                    duration = new[] { 3f, 3f, 3f, 3f, 3f }; voidWalkMoveMultiplier = 1.6f;
                    descriptions = new[] { "Enter the void for 3 s: move x1.6, cannot be targeted or damaged.", "Void state for 3 s.", "Void state for 3 s.", "Void state for 3 s.", "Void state for 3 s." }; break;
                case SkillId.Singularity:
                    displayName = "Singularity"; element = ElementKind.Void; cost = 60f;
                    strength = new[] { 6f, 6f, 6f, 6f, 6f }; duration = new[] { 2f, 2f, 2f, 2f, 2f }; radius = new[] { 6f, 6f, 6f, 6f, 6f };
                    singularitySlowMultiplier = 0.5f; singularityBaseDamage = 10f; singularityDamagePerEnemy = 4f; singularityPlayerMoveMultiplier = 0.35f;
                    descriptions = new[] { "Draw on-screen enemies 6 toward you and slow them; you slow to 35% but can still fire and cast, and after 2 s it blasts based on distinct absorbed enemies.", "Singularity.", "Singularity.", "Singularity.", "Singularity." }; break;
            }
        }
    }
}
