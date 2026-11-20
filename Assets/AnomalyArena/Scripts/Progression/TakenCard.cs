using System;

namespace AnomalyArena
{
    /// <summary>
    /// One upgrade card taken in a run (RunProgression.TakenCards): what names it and nothing more, so the character screen
    /// (V5CharacterScreen) writes its title in the language showing, and a continued save keeps the list (RunSave.cardsTaken). A stat
    /// card keeps its stat, a weapon card the weapon's form, its direction and the element an infusion set, a skill card its skill; the
    /// rarity is the card's colour, which for a skill card is the level it gave.
    /// </summary>
    [Serializable]
    public struct TakenCard
    {
        public DraftCardKind kind;
        public CardRarity rarity;
        /// <summary>The stat (StatId), the weapon's form (WeaponForm) or the skill (SkillId), by kind.</summary>
        public int id;
        /// <summary>A weapon card's direction.</summary>
        public UpgradeAxis axis;
        /// <summary>The element a weapon card's infusion set.</summary>
        public ElementKind element;

        /// <summary>The record of a card as it was offered; a card whose payload names nothing it can keep comes back with id -1.</summary>
        public static TakenCard Of(DraftCard card)
        {
            var taken = new TakenCard { kind = card?.Kind ?? DraftCardKind.Stat, rarity = card?.Rarity ?? CardRarity.Common, id = -1 };
            switch (card?.Payload)
            {
                case StatCardOffer stat:
                    taken.id = (int)stat.Stat;
                    break;
                case WeaponUpgradeOffer upgrade:
                    taken.id = upgrade.Weapon != null ? (int)upgrade.Weapon.form : -1;
                    taken.axis = upgrade.Axis;
                    taken.element = upgrade.Element;
                    break;
                case SkillId skill:
                    taken.id = (int)skill;
                    break;
            }
            return taken;
        }

        /// <summary>Two records of the same card: the character screen lists them once, with how many were taken.</summary>
        public bool Same(TakenCard other) => kind == other.kind && rarity == other.rarity && id == other.id && axis == other.axis && element == other.element;
    }
}
