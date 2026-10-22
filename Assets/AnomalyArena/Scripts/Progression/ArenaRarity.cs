using UnityEngine;

namespace AnomalyArena
{

    public static class ArenaRarity
    {

        public static int Rank(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => 1,
            CardRarity.Exceptional => 2,
            CardRarity.Epic => 3,
            CardRarity.Legendary => 4,
            CardRarity.Void => 5,
            _ => 0
        };

        public static CardRarity Next(CardRarity rarity) => rarity switch
        {
            CardRarity.Common => CardRarity.Rare,
            CardRarity.Rare => CardRarity.Exceptional,
            CardRarity.Exceptional => CardRarity.Epic,
            CardRarity.Epic => CardRarity.Legendary,
            _ => rarity
        };

        public static Color ColorFor(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => new Color(.38f, .92f, .47f),
            CardRarity.Exceptional => new Color(.30f, .65f, 1f),
            CardRarity.Epic => new Color(.70f, .42f, 1f),
            CardRarity.Legendary => new Color(1f, .62f, .18f),
            CardRarity.Void => new Color(.20f, .15f, .26f),
            _ => Color.white
        };

        public static CardRarity ForWeapon(RunWeapon weapon, UpgradeAxis added = UpgradeAxis.None,
            ElementKind infusion = ElementKind.None)
        {
            if (weapon == null) return CardRarity.Common;
            ElementKind element = (added & UpgradeAxis.Infusion) != 0 ? infusion : weapon.element;
            if (element == ElementKind.Void) return CardRarity.Void;
            UpgradeAxis axes = weapon.axes | added;
            int upgrades = 0;
            for (int i = 0; i < 6; i++) if ((axes & (UpgradeAxis)(1 << i)) != 0) upgrades++;
            upgrades += Mathf.Max(0, weapon.mk - 1);
            return upgrades >= 5 ? CardRarity.Legendary : upgrades >= 4 ? CardRarity.Epic
                : upgrades >= 2 ? CardRarity.Exceptional : upgrades >= 1 ? CardRarity.Rare : CardRarity.Common;
        }

        public static CardRarity ForSkill(SkillDefinition definition, int level)
        {
            if (definition && (definition.greaterVoid || definition.element == ElementKind.Void))
                return CardRarity.Void;
            return level >= 3 ? CardRarity.Exceptional : level >= 2 ? CardRarity.Rare : CardRarity.Common;
        }
    }
}
