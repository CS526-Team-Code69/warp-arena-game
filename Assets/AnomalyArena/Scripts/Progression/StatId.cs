namespace AnomalyArena
{

    public enum StatId
    {
        MaxHp, Regeneration, MoveSpeed, Stamina, Damage, AttackSpeed, Knockback,
        Impact, PickupRange, Protection, Luck, MaxEnergy, EnergySiphon, ElementPenetration, CritChance
    }

    public enum StatBonus { None, CritChance, Regeneration, Stamina, Impact, EnergySiphon, CritMultiplier }

    public enum CardRarity { Common = 0, Rare = 1, Exceptional = 2, Legendary = 3, Void = 4, Epic = 5 }
}
