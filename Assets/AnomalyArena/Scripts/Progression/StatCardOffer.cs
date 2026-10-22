namespace AnomalyArena
{
    public readonly struct StatCardOffer
    {
        public readonly StatId Stat;
        public readonly CardRarity Rarity;
        public readonly float Value;
        public readonly StatBonus Bonus;
        public readonly float BonusValue;
        public StatCardOffer(StatId stat, CardRarity rarity, float value, StatBonus bonus = StatBonus.None, float bonusValue = 0f)
        { Stat = stat; Rarity = rarity; Value = value; Bonus = bonus; BonusValue = bonusValue; }

        public string Title => Stat == StatId.CritChance ? ArenaText.Pair("Critical","Critical") : ArenaTerms.Stat(Stat);
        public string Description => Main + (Bonus == StatBonus.None || BonusValue <= 0f ? "" : " · " + BonusText);
        string Main => Stat switch
        {
            StatId.Damage => ArenaText.Pair($"All damage scaled up: ×{1f + Value:0.##} (multiplies)",$"All damage scaled up: ×{1f + Value:0.##} (multiplies)"),
            StatId.AttackSpeed => ArenaText.Pair($"Weapon cooldowns shorten: attack rate +{Value * 100f:0}% (cooldown ÷{1f + Value:0.##})",$"Weapon cooldowns shorten: attack rate +{Value * 100f:0}% (cooldown ÷{1f + Value:0.##})"),
            StatId.Protection => ArenaText.Pair($"Armor +{Value:0}; damage taken drops in proportion (×100/(100+armor))",$"Armor +{Value:0}; damage taken drops in proportion (×100/(100+armor))"),
            StatId.CritChance => ArenaText.Pair($"Crit chance +{Value * 100f:0}%",$"Crit chance +{Value * 100f:0}%"),
            StatId.ElementPenetration => ArenaText.Pair($"Your elements also work on element-immune targets, at {Value * 100f:0}% of the full effect",$"Your elements also work on element-immune targets, at {Value * 100f:0}% of the full effect"),
            StatId.MaxHp => ArenaText.Pair($"Max HP +{Value:0.##}, and heal the same amount when you take the card",$"Max HP +{Value:0.##}, and heal the same amount when you take the card"),
            StatId.MoveSpeed => ArenaText.Pair($"Move faster: Move Speed +{Value * 100f:0}%",$"Move faster: Move Speed +{Value * 100f:0}%"),
            StatId.MaxEnergy => ArenaText.Pair($"Max Energy +{Value:0.##}",$"Max Energy +{Value:0.##}"),
            StatId.Knockback => $"+{Value * 100f:0}% {Title}",
            _ => $"+{Value:0.##} {Title}"
        };
        string BonusText => Bonus switch
        {
            StatBonus.CritChance => ArenaText.Pair($"Crit chance +{BonusValue * 100f:0}% (crits ×2)",$"Crit chance +{BonusValue * 100f:0}% (crits ×2)"),
            StatBonus.CritMultiplier => ArenaText.Pair($"Crit multiplier +{BonusValue:0.##}",$"Crit multiplier +{BonusValue:0.##}"),
            StatBonus.Regeneration => ArenaText.Pair($"Timed healing: +{BonusValue:0.#} HP every 5 s",$"Timed healing: +{BonusValue:0.#} HP every 5 s"),
            StatBonus.Stamina => ArenaText.Pair($"Longer sprint: +{BonusValue:0.##} s",$"Longer sprint: +{BonusValue:0.##} s"),
            StatBonus.Impact => ArenaText.Pair($"Wall slam damage +{BonusValue:0} (grows with waves)",$"Wall slam damage +{BonusValue:0} (grows with waves)"),
            StatBonus.EnergySiphon => ArenaText.Pair($"Every enemy death restores {BonusValue:0.#} Energy",$"Every enemy death restores {BonusValue:0.#} Energy"),
            _ => ""
        };
    }
}
