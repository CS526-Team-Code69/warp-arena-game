using UnityEngine;

namespace AnomalyArena
{
    /// <summary>Shared rarity semantics; element icons retain their own independent colours.
    /// Display order: white Common, green Rare, blue Exceptional, purple Epic, orange Legendary, black Void.</summary>
    public static class ArenaRarity
    {
        /// <summary>0..5 in display order; the enum's serialized ints are not ordered.</summary>
        public static int Rank(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare => 1,
            CardRarity.Exceptional => 2,
            CardRarity.Epic => 3,
            CardRarity.Legendary => 4,
            CardRarity.Void => 5,
            _ => 0
        };

        /// <summary>The next ordinary tier. Legendary is the top tier; Void only comes from the void element, so both stay put.</summary>
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

        /// <summary>Design document 6.7: a weapon slot is coloured by the upgrades the weapon has already taken (0 white, 1 green, 2 blue,
        /// 3 orange; void element is black). The built-in element of the Flamethrower, Cryo Cannon, Arc Cannon and Venom Launcher is not an
        /// upgrade, and neither is the armory Mk tier, so a Mk 4 weapon with one upgrade is green, not orange. With added Infusion the
        /// preview uses the element that infusion would set. Draft weapon cards are coloured by ForWeaponCard instead.</summary>
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

        /// <summary>Document 9.6: a draft weapon card is coloured by its direction, not by the weapon it upgrades:
        /// Tame green, Twist blue, Infusion purple, whatever element the infusion sets (Void included). Once a weapon carries Void,
        /// its slot is black (ForWeapon).</summary>
        public static CardRarity ForWeaponCard(UpgradeAxis axis) => axis switch
        {
            UpgradeAxis.Tame => CardRarity.Rare,
            UpgradeAxis.Twist => CardRarity.Exceptional,
            UpgradeAxis.Infusion => CardRarity.Epic,
            _ => CardRarity.Common
        };

        /// <summary>Document 9.6, five levels since 2026-10-26 (document 8.6): the level a skill card's colour gives,
        /// white 1, green 2, blue 3, purple 4, orange 5. A void colour carries no level of its own.</summary>
        public static int SkillLevel(CardRarity rarity) => rarity == CardRarity.Void ? 1 : Mathf.Clamp(Rank(rarity), 0, SkillDefinition.MaxLevel - 1) + 1;

        /// <summary>Skills colour by level: 1 Common, 2 Rare, 3 Exceptional, 4 Epic, 5 Legendary (document 8.6), Devour included; only the
        /// greater void skills, which have no level, are Void.</summary>
        public static CardRarity ForSkill(SkillDefinition definition, int level)
        {
            if (definition && definition.greaterVoid)
                return CardRarity.Void;
            return level >= 5 ? CardRarity.Legendary : level >= 4 ? CardRarity.Epic : level >= 3 ? CardRarity.Exceptional
                : level >= 2 ? CardRarity.Rare : CardRarity.Common;
        }
    }
}
