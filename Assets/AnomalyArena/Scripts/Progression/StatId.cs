namespace AnomalyArena
{
    /// <summary>Serialized ids: append only. Regeneration, Stamina, Knockback, Impact, PickupRange, Luck and EnergySiphon are retired as cards.</summary>
    public enum StatId
    {
        MaxHp, Regeneration, MoveSpeed, Stamina, Damage, AttackSpeed, Knockback,
        Impact, PickupRange, Protection, Luck, MaxEnergy, EnergySiphon, ElementPenetration, CritChance
    }

    /// <summary>Second effect carried by a merged stat card; the primary stays a StatId. Append only.</summary>
    public enum StatBonus { None, CritChance, Regeneration, Stamina, Impact, EnergySiphon, CritMultiplier }

    /// <summary>Serialized ints are kept: 2 is the blue Exceptional tier, the purple Epic tier is appended as 5.
    /// Never compare the raw ints; use ArenaRarity.Rank for display order.</summary>
    public enum CardRarity { Common = 0, Rare = 1, Exceptional = 2, Legendary = 3, Void = 4, Epic = 5 }
}
