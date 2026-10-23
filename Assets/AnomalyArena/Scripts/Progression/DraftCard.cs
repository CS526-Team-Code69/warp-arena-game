using System;

namespace AnomalyArena
{
    public enum DraftCardKind { Stat, Weapon, Skill }

    public sealed class DraftCard
    {
        public DraftCardKind Kind { get; }
        public string Title { get; }
        public string Description { get; }
        public CardRarity Rarity { get; }
        public bool NeedsReplacement { get; }
        public object Payload { get; }
        readonly Func<int, bool> apply;
        readonly Func<bool> stillFits;

        public DraftCard(DraftCardKind kind, string title, string description, Func<int, bool> apply,
            bool needsReplacement = false, object payload = null, CardRarity rarity = CardRarity.Common, Func<bool> stillFits = null)
        {
            Kind = kind; Title = title; Description = description; this.apply = apply; this.stillFits = stillFits;
            NeedsReplacement = needsReplacement; Payload = payload;
            Rarity = payload is WeaponUpgradeOffer upgrade
                ? ArenaRarity.ForWeapon(upgrade.Weapon, upgrade.Axis, upgrade.Element) : rarity;
        }
        public bool Apply(int replacementIndex = -1) => apply?.Invoke(replacementIndex) ?? false;
        public bool StillFits => stillFits?.Invoke() ?? true;
    }
}
