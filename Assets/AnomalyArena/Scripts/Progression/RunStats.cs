using System;
using UnityEngine;

namespace AnomalyArena
{

    [Serializable]
    public sealed class RunStats
    {
        public float maxHp = 50f;
        public float regeneration;
        public float moveSpeed = 6f;
        public float sprintDuration = 1f;
        public float sprintRecovery = 1.5f;
        public float damageBonus;
        public float attackSpeedBonus;
        public float knockbackBonus;
        public float impact;
        public float pickupRange = 2f;
        public float protection = 0.5f;
        public float luck;
        public float maxEnergy = 100f;
        public float energySiphon;
        public float elementPenetration;
        [Tooltip("Chance for a player hit on an enemy to deal CritMultiplier damage; Damage cards add to it.")]
        public float critChance;
        public float critCap = .6f;
        [Min(1f)] public float critMultiplier = 2f;
        [Tooltip("Protection cards add armor: damage taken × armorConstant / (armorConstant + armor), so each point adds the same share of effective HP.")]
        public float armor;
        [Min(1f)] public float armorConstant = 100f;
        public float armorCap = 300f;
        [Tooltip("Caps follow StatId order; negative means uncapped. MoveSpeed is in world units/s, bonuses are fractions.")]
        public float[] caps = { -1f, 5f, 8.4f, 1.75f, -1f, 0.5f, 1f, -1f, 10f, 1f, 30f, 250f, 2f, 1f };

        public float DamageMultiplier => 1f + damageBonus;
        public float CooldownMultiplier => 1f / (1f + Mathf.Max(0f, attackSpeedBonus));
        public float CritMultiplier => Mathf.Max(1f, critMultiplier);
        public float DamageTakenMultiplier => armorConstant / (armorConstant + Mathf.Max(0f, armor));
        public float KnockbackMultiplier => 1f + knockbackBonus;

        public float Cap(StatId stat) => stat == StatId.Protection ? armorCap : caps != null && (int)stat < caps.Length ? caps[(int)stat] : -1f;
        public bool AtCap(StatId stat) => Cap(stat) >= 0f && Value(stat) >= Cap(stat);
        float Limit(StatId stat, float value) => Cap(stat) < 0f ? value : Mathf.Min(Cap(stat), value);
        public RunStats Copy()
        {
            var copy = (RunStats)MemberwiseClone();
            if (caps != null) copy.caps = (float[])caps.Clone();
            return copy;
        }

        public float Add(StatId stat, float amount)
        {
            float before = Value(stat);
            switch (stat)
            {
                case StatId.MaxHp: maxHp += amount; break;
                case StatId.Regeneration: regeneration = Limit(stat, regeneration + amount); break;
                case StatId.MoveSpeed: moveSpeed = Limit(stat, moveSpeed + 6f * amount); break;
                case StatId.Stamina: sprintDuration = Limit(stat, sprintDuration + amount); break;
                case StatId.Damage: damageBonus = (1f + damageBonus) * (1f + amount) - 1f; break;
                case StatId.AttackSpeed: attackSpeedBonus = Limit(stat, attackSpeedBonus + amount); break;
                case StatId.Knockback: knockbackBonus = Limit(stat, knockbackBonus + amount); break;
                case StatId.Impact: impact += amount; break;
                case StatId.PickupRange: pickupRange = Limit(stat, pickupRange + amount); break;
                case StatId.Protection: armor = Limit(stat, armor + amount); break;
                case StatId.Luck: luck = Limit(stat, luck + amount); break;
                case StatId.MaxEnergy: maxEnergy = Limit(stat, maxEnergy + amount); break;
                case StatId.EnergySiphon: energySiphon = Limit(stat, energySiphon + amount); break;
                case StatId.ElementPenetration: elementPenetration = Limit(stat, elementPenetration + amount); break;
            }
            return Value(stat) - before;
        }

        public float AddBonus(StatBonus bonus, float amount)
        {
            switch (bonus)
            {
                case StatBonus.CritChance: { float before = critChance; critChance = Mathf.Min(critCap, critChance + amount); return critChance - before; }
                case StatBonus.Regeneration: return Add(StatId.Regeneration, amount);
                case StatBonus.Stamina: return Add(StatId.Stamina, amount);
                case StatBonus.Impact: return Add(StatId.Impact, amount);
                case StatBonus.EnergySiphon: return Add(StatId.EnergySiphon, amount);
                default: return 0f;
            }
        }

        public float Value(StatId stat) => stat switch
        {
            StatId.MaxHp => maxHp, StatId.Regeneration => regeneration, StatId.MoveSpeed => moveSpeed,
            StatId.Stamina => sprintDuration, StatId.Damage => damageBonus, StatId.AttackSpeed => attackSpeedBonus,
            StatId.Knockback => knockbackBonus, StatId.Impact => impact, StatId.PickupRange => pickupRange,
            StatId.Protection => armor, StatId.Luck => luck, StatId.MaxEnergy => maxEnergy,
            StatId.EnergySiphon => energySiphon, StatId.ElementPenetration => elementPenetration, _ => 0f
        };
    }
}
