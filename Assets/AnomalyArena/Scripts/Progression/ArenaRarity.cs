using UnityEngine;

namespace AnomalyArena
{

    public static class ArenaRarity
    {
        public static Color ColorFor(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => new Color(.38f, .92f, .47f),
            CardRarity.Epic => new Color(.30f, .65f, 1f),
            CardRarity.Legendary => new Color(1f, .62f, .18f),
            CardRarity.Void => new Color(.77f, .43f, 1f),
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
            return upgrades >= 5 ? CardRarity.Legendary : upgrades >= 3 ? CardRarity.Epic
                : upgrades >= 1 ? CardRarity.Rare : CardRarity.Common;
        }

        public static CardRarity ForSkill(SkillDefinition definition, int level)
        {
            if (definition && (definition.greaterVoid || definition.element == ElementKind.Void))
                return CardRarity.Void;
            return level >= 3 ? CardRarity.Epic : level >= 2 ? CardRarity.Rare : CardRarity.Common;
        }
    }
}
