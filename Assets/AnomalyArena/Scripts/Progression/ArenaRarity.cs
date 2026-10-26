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
            int upgrades = 0;
            for (int i = 0; i < 6; i++)
            {
                var axis = (UpgradeAxis)(1 << i);
                if ((weapon.axes & axis) != 0 && !(axis == UpgradeAxis.Infusion && weapon.builtInElement)) upgrades++;
            }
            return upgrades >= 3 ? CardRarity.Legendary : upgrades >= 2 ? CardRarity.Exceptional
                : upgrades >= 1 ? CardRarity.Rare : CardRarity.Common;
        }

        public static CardRarity ForWeaponCard(UpgradeAxis axis) => axis switch
        {
            UpgradeAxis.Tame => CardRarity.Rare,
            UpgradeAxis.Twist => CardRarity.Exceptional,
            UpgradeAxis.Infusion => CardRarity.Epic,
            _ => CardRarity.Common
        };

        public static int SkillLevel(CardRarity rarity) => rarity == CardRarity.Void ? 1 : Mathf.Clamp(Rank(rarity), 0, SkillDefinition.MaxLevel - 1) + 1;

        public static CardRarity ForSkill(SkillDefinition definition, int level)
        {
            if (definition && (definition.greaterVoid || definition.element == ElementKind.Void))
                return CardRarity.Void;
            return level >= 5 ? CardRarity.Legendary : level >= 4 ? CardRarity.Epic : level >= 3 ? CardRarity.Exceptional
                : level >= 2 ? CardRarity.Rare : CardRarity.Common;
        }
    }
}
