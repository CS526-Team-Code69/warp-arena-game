namespace AnomalyArena
{
    public enum StatId
    {
        MaxHp, Regeneration, MoveSpeed, Stamina, Damage, AttackSpeed, Knockback,
        Impact, PickupRange, Protection, Luck, MaxEnergy, EnergySiphon, ElementPenetration
    }

    public enum StatBonus { None, CritChance, Regeneration, Stamina, Impact, EnergySiphon }

    public enum CardRarity { Common = 0, Rare = 1, Epic = 2, Legendary = 3, Void = 4 }
}
