using System;

namespace AnomalyArena
{
    public enum DraftCardKind { Stat, Weapon, Skill }

    /// <summary>A menu offer. The callback applies to an instance, never to a shared asset.</summary>
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

        /// <param name="stillFits">False once the build has changed under the card (its weapon was replaced, its skill changed level);
        /// a draft that was put off and is shown again draws only such a card anew.</param>
        public DraftCard(DraftCardKind kind, string title, string description, Func<int, bool> apply,
            bool needsReplacement = false, object payload = null, CardRarity rarity = CardRarity.Common, Func<bool> stillFits = null)
        {
            Kind = kind; Title = title; Description = description; this.apply = apply; this.stillFits = stillFits;
            NeedsReplacement = needsReplacement; Payload = payload;
            // A weapon card shows its direction's colour, whatever the weapon has taken so far or the element it infuses.
            Rarity = payload is WeaponUpgradeOffer upgrade ? ArenaRarity.ForWeaponCard(upgrade.Axis) : rarity;
        }
        public bool Apply(int replacementIndex = -1) => apply?.Invoke(replacementIndex) ?? false;
        public bool StillFits => stillFits?.Invoke() ?? true;
    }
}
