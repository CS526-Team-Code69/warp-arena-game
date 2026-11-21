using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The workshop's columns its entries belong to: Armament the weapon column (alloy), Energy the energy research (alien
    /// energy), Void the void tabs, which show once the hidden boss is beaten.</summary>
    public enum StudyRoom { Armament, Energy, Void }

    /// <summary>What a tile of the study window can be: not unlocked yet (drawn as a question mark), buyable, too dear right now, or owned / at its top.</summary>
    public enum StudyState { Locked, Available, Unaffordable, Owned }

    /// <summary>
    /// One item of the workshop (V2Campaign.BuildStudy): something the player can learn or raise, with its rarity colour and its
    /// numbers before and after. The parts of V2Campaign that own a kind of purchase fill these; a page only draws them and calls buy.
    /// All texts arrive localized (ArenaText.Pair).
    /// </summary>
    public sealed class StudyEntry
    {
        /// <summary>Stable key, for keeping the selection when the list is rebuilt after a purchase.</summary>
        public string id;
        public StudyRoom room;
        /// <summary>The tab inside the room this entry sits under, e.g. weapon upgrades, skills.</summary>
        public string category;
        public CardRarity rarity;
        public Sprite icon;
        public string title;
        /// <summary>The number as it is and as it will be after buying; both empty when the entry has no number to show.</summary>
        public string now, after;
        /// <summary>One short line for a card's face, what buying it changes where now and after do not say it: the weapon Mk's damage
        /// on every anomaly weapon, armor's max HP, the rarities a research level opens. Empty for the other items.</summary>
        public string effect = "";
        /// <summary>For an item about a weapon in hand (Tame, Twist, a hand weapon's infusion): its hand, 0 or 1 (RunWeapons.Hand); -1
        /// for every other item.</summary>
        public int hand = -1;
        /// <summary>The full description for the detail pane.</summary>
        public string detail;
        /// <summary>The price with its currency, ready to show.</summary>
        public string costText;
        /// <summary>The same price as numbers in the three currencies, so the window can draw it as each currency's icon and amount; null
        /// when there is nothing to pay (owned, at its top, not unlocked) or the price is in the void's currencies.</summary>
        public CampaignCost? price;
        /// <summary>The price in the void's two currencies (the Void room's tiles), 0 where there is none.</summary>
        public int voidEnergyCost, voidAlloyCost;
        public StudyState state;
        /// <summary>Buys it; the effect applies at once. False when it could not be bought.</summary>
        public Func<bool> buy;
    }

    public sealed partial class V2Campaign
    {
        /// <summary>The void entries of the study window. The void currencies' part implements it; until then the call compiles away.</summary>
        partial void CollectVoidStudy(List<StudyEntry> into);

        /// <summary>Called as each afternoon begins, after the day's ore is placed (BeginDusk): the afternoon's weapon crate and the line
        /// the hero says about going out to look. The weapon crates' part implements it.</summary>
        partial void AfternoonCrates();

        /// <summary>Called as each afternoon begins, after the day's ore is placed (BeginDusk): the void alloy that grows each day once the
        /// hidden boss is beaten. The void currencies' part implements it.</summary>
        partial void AfternoonVoidOre();

        /// <summary>Effects one part sets and another reads, so the two can be written apart. V2Campaign.Rooms.cs (ApplyShipEffects) sets
        /// them; the afternoon's ore and the night's lamps read them. afternoonOreScale multiplies the afternoon ore's alloy: the rules'
        /// alloyIncomeScale, times 1 + shipOreBonus from the day after the engine is repaired. lampRadiusScale widens the lamps' light
        /// once the hull is repaired. 1 means no effect.</summary>
        internal float afternoonOreScale = 1f, lampRadiusScale = 1f;
    }
}
