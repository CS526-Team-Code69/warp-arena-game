using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>The rooms the base's study window groups its entries by. Void shows once the hidden boss is beaten.</summary>
    public enum StudyRoom { Armament, Energy, Void }

    /// <summary>What a tile of the study window can be: not unlocked yet (drawn as a question mark), buyable, too dear right now, or owned / at its top.</summary>
    public enum StudyState { Locked, Available, Unaffordable, Owned }

    /// <summary>
    /// One tile of the base's study window (2026-11-10): something a room lets the player learn or raise, with its rarity colour and
    /// its numbers before and after. The parts of V2Campaign that own a kind of purchase fill these; the window only draws them and
    /// calls buy. All texts arrive localized (ArenaText.Pair).
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
        /// <summary>The full description for the detail pane.</summary>
        public string detail;
        /// <summary>The price with its currency, ready to show.</summary>
        public string costText;
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

        /// <summary>Room effects one part sets and another reads (2026-11-10), so the two can be written apart. The rooms' part sets them
        /// when a ship level is bought; the ore and the night's lamps read them. 1 means no effect.</summary>
        internal float afternoonOreScale = 1f, lampRadiusScale = 1f;
    }
}
