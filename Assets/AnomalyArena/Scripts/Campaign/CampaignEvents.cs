using System;

namespace AnomalyArena
{
    /// <summary>
    /// Record points of the V2 day campaign for the run analytics (RunAnalytics subscribes; nothing here sends anything). Raised by
    /// V2Campaign only while a campaign runs; the classic run raises none of them.
    /// </summary>
    public static class CampaignEvents
    {
        /// <summary>A new day's wave begins: the day number (1 to rules.days, higher in overtime) and whether it is an overtime day.</summary>
        public static event Action<int, bool> DayStarted;
        /// <summary>The day turns: "dusk", "night" (also the Day 0 tutorial night) or "dawn", with the day number.</summary>
        public static event Action<string, int> PhaseChanged;
        /// <summary>A hit on the base: the whole amount, the part the HP took after the shield, and the room of the side that was hit.</summary>
        public static event Action<float, float, CampaignRoom> BaseDamaged;
        /// <summary>The player died at night: what was lost (tonight's haul) and the day number. A fall in the afternoon (dusk) raises
        /// nothing since 2026-11-10: it loses nothing and is not a night death.</summary>
        public static event Action<CampaignCost, int> NightDeath;
        /// <summary>Currency spent at the base or the door (rooms, repairs, levels, ship parts, fuel, lamps, shield).</summary>
        public static event Action<CampaignCost> Spent;
        /// <summary>One purchase, raised once it is paid and given (the v2_purchase row): what was bought (player_level, weapon_damage,
        /// new_weapon, weapon_tame, weapon_twist, constitution, unlock_skill, weapon_infusion, room_upgrade, repair, shield, lamp, ship_part,
        /// fuel), which one (a weapon and its Mk, the upgrade given, a room and its level, a ship part), the room it was bought in, the price
        /// and the day number. Spent still fires for the same money.</summary>
        public static event Action<string, string, CampaignRoom, CampaignCost, int> Purchased;
        /// <summary>Dawn after a night: the day number, the wallet and everything earned so far in the run.</summary>
        public static event Action<int, CampaignCost, CampaignCost> Dawn;
        /// <summary>The campaign is over: "won", "base_destroyed" or "died_by_day", with the day number.</summary>
        public static event Action<string, int> Ended;
        /// <summary>A moment worth one v2_event row (the 2026-11-10 play): its kind, which one (detail), a number (value) and the day number.
        /// The kinds, and what detail and value hold:
        /// tutorial_step, a Day 0 step passed: the TutorialStep's name, and the seconds since the step began;
        /// crate, a weapon crate opened: "&lt;crate kind&gt;:&lt;weapon form&gt;" for the weapon taken, or the crate kind with ":left" (nothing
        /// taken), ":empty" (nothing new in it) or ":no_room"; the place of the weapon on the panel, 1 to 3, or 0 when none was taken;
        /// hole_check, the black hole's timed check: start, passed, failed_timeout, failed_death or failed_night (put away for the night),
        /// and the share of the required damage dealt, 0 to 1;
        /// prophet, the Nash Prophet beaten in the void trial: no detail, and the day number;
        /// void_purchase, something bought with the void currencies: skill:&lt;id&gt;, enchant:&lt;weapon form&gt;, mk:&lt;level&gt; or armor, and the price;
        /// void_crystal, E pressed at the void crystals on Day 0's way home: no detail, 0.</summary>
        public static event Action<string, string, float, int> Milestone;

        public const string MilestoneTutorialStep = "tutorial_step", MilestoneCrate = "crate", MilestoneHoleCheck = "hole_check",
            MilestoneProphet = "prophet", MilestoneVoidPurchase = "void_purchase", MilestoneVoidCrystal = "void_crystal";
        public const string HoleCheckStart = "start", HoleCheckPassed = "passed", HoleCheckFailedTimeout = "failed_timeout",
            HoleCheckFailedDeath = "failed_death", HoleCheckFailedNight = "failed_night";
        public const string CrateLeft = "left", CrateEmpty = "empty", CrateNoRoom = "no_room";

        internal static void RaiseDayStarted(int day, bool overtime) => DayStarted?.Invoke(day, overtime);
        internal static void RaisePhase(string phase, int day) => PhaseChanged?.Invoke(phase, day);
        internal static void RaiseBaseDamaged(float amount, float toHp, CampaignRoom room) => BaseDamaged?.Invoke(amount, toHp, room);
        internal static void RaiseNightDeath(CampaignCost lost, int day) => NightDeath?.Invoke(lost, day);
        internal static void RaiseSpent(CampaignCost cost) => Spent?.Invoke(cost);
        internal static void RaisePurchased(string item, string detail, CampaignRoom room, CampaignCost cost, int day) => Purchased?.Invoke(item, detail, room, cost, day);
        internal static void RaiseDawn(int day, CampaignCost wallet, CampaignCost earned) => Dawn?.Invoke(day, wallet, earned);
        internal static void RaiseEnded(string cause, int day) => Ended?.Invoke(cause, day);
        internal static void RaiseMilestone(string kind, string detail, float value, int day) => Milestone?.Invoke(kind, detail, value, day);
    }
}
