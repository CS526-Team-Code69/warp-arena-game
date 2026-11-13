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
        // TODO(lead): add the CritChance name as the 15th entry of ArenaTerms.StatZh/StatEn, then drop this special case.
        public string Title => Stat == StatId.CritChance ? ArenaText.Pair("暴击", "Critical") : ArenaTerms.Stat(Stat);
        public string Description => Main + (Bonus == StatBonus.None || BonusValue <= 0f ? "" : ArenaText.Pair("；", "; ") + BonusText);
        string Main => Stat switch
        {
            StatId.Damage => ArenaText.Pair($"直接伤害（包括技能伤害）按比例提高：×{1f + Value:0.##}（和其他伤害加成相乘）", $"Direct damage, skills included, scaled up: ×{1f + Value:0.##} (multiplies with other bonuses)"),
            StatId.AttackSpeed => ArenaText.Pair($"武器冷却缩短，攻击频率 +{Value * 100f:0}%（冷却时间 ÷{1f + Value:0.##}）", $"Shorter weapon cooldowns: attack rate +{Value * 100f:0}% (cooldown ÷{1f + Value:0.##})"),
            StatId.Protection => ArenaText.Pair($"护甲 +{Value:0}，受到的伤害按比例减少（×100/(100+护甲)）", $"Armor +{Value:0}: damage taken shrinks in proportion (×100/(100+armor))"),
            StatId.CritChance => ArenaText.Pair($"暴击率 +{Value * 100f:0}%", $"Crit chance +{Value * 100f:0}%"),
            StatId.ElementPenetration => ArenaText.Pair($"属性伤害按比例打进免疫它的目标：伤害穿透 +{Value * 100f:0}%（状态仍然叠不上）", $"Element damage partly gets through to targets immune to it: Damage Penetration +{Value * 100f:0}% (statuses still never land)"),
            StatId.MaxHp => ArenaText.Pair($"最大生命 +{Value:0.##}，拿到时同时回复等量的生命", $"Max HP +{Value:0.##}, and you heal the same amount when you take it"),
            StatId.MoveSpeed => ArenaText.Pair($"移动速度 +{Value * 100f:0}%", $"Move Speed +{Value * 100f:0}%"),
            StatId.MaxEnergy => ArenaText.Pair($"能量上限 +{Value:0.##}", $"Max Energy +{Value:0.##}"),
            StatId.Knockback => $"+{Value * 100f:0}% {Title}",
            _ => $"+{Value:0.##} {Title}"
        };
        string BonusText => Bonus switch
        {
            StatBonus.CritChance => ArenaText.Pair($"暴击率 +{BonusValue * 100f:0}%（暴击造成 2 倍伤害）", $"Crit chance +{BonusValue * 100f:0}% (crits deal ×2)"),
            StatBonus.CritMultiplier => ArenaText.Pair($"暴击倍率 +{BonusValue:0.##}", $"Crit multiplier +{BonusValue:0.##}"),
            StatBonus.Regeneration => ArenaText.Pair($"每 5 秒回复 {BonusValue:0.#} 点生命", $"Heal {BonusValue:0.#} HP every 5 s"),
            StatBonus.Stamina => ArenaText.Pair($"冲刺时间延长 {BonusValue:0.##} 秒", $"Sprint lasts {BonusValue:0.##} s longer"),
            StatBonus.Impact => ArenaText.Pair($"撞墙伤害 +{BonusValue:0}（随波次成长）", $"Wall slam damage +{BonusValue:0} (grows with waves)"),
            StatBonus.EnergySiphon => ArenaText.Pair($"每有一个敌人死亡，回复 {BonusValue:0.#} 点能量", $"Every enemy death restores {BonusValue:0.#} Energy"),
            _ => ""
        };
    }
}
