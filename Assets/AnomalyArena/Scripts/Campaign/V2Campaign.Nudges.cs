using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// What the afternoon reminds the player of before the night, so that the lamps and the base's fortification are found: the
    /// afternoon's goal adds a sentence for each while it is wanted (AfternoonObjective, V2Campaign.Night.cs), and on the base page the
    /// lamp button and the fortify button breathe while they are wanted and the stock pays (V2CampaignUi.Nudges.cs).
    /// Lamps are wanted from Day 1 while the ship's room can make them and the stock holds fewer than the day's lamps
    /// (rules.dayLampsToCraft). A fortification is wanted from Day 1 below the top level, until the base has been fortified once,
    /// while the base has lost more than FortifyHurtShare of its HP, or while the last morning cost it more than MorningLossShare of
    /// its max HP: the afternoon's repair hides a morning's loss, and the raids grow faster than the base, so a reminder that waited
    /// for the HP to stay down came a morning too late, and the next morning could not be won from its save.
    /// </summary>
    public sealed partial class V2Campaign
    {
        const float FortifyHurtShare = .25f, MorningLossShare = .2f;

        // The base's HP as the day's first wave began, and what the last morning took from it (saved with the run).
        float dawnBaseHp = -1f, lastMorningLoss;

        /// <summary>What the last morning took from the base's HP (0 before the first morning ends).</summary>
        public float LastMorningLoss => lastMorningLoss;

        /// <summary>StartNextWave: a new day's first wave begins.</summary>
        void NoteDawnBase() => dawnBaseHp = BaseHp;

        /// <summary>BeginDusk, before the afternoon's repair: what the morning took.</summary>
        void NoteMorningLoss()
        {
            if (dawnBaseHp >= 0f) lastMorningLoss = Mathf.Max(0f, dawnBaseHp - BaseHp);
            dawnBaseHp = -1f;
        }

        /// <summary>Checks: the last morning took this much from the base.</summary>
        public void DebugMorningLoss(float hp) => lastMorningLoss = Mathf.Max(0f, hp);

        /// <summary>The night's lamps are short: from Day 1, while the ship's room can make them and the stock holds fewer than the night's
        /// lamps (rules.NightLampsOn: the base's gift at dusk and those the afternoon's alloy pays for).</summary>
        public bool LampsWanted => Running && rules && Day >= 1 && LampsCraftable && lampStock < rules.NightLampsOn(Day);

        /// <summary>A fortification is worth pointing at: from Day 1 below the top level, until the base is fortified once, while it
        /// has lost more than FortifyHurtShare of its HP, or while the last morning cost it more than MorningLossShare of its max HP.</summary>
        public bool FortifyWanted => Running && rules && Day >= 1 && FortifyPrice != null
                                     && (FortifyLevel <= 1 || BaseHp < BaseMaxHp * (1f - FortifyHurtShare)
                                         || lastMorningLoss > BaseMaxHp * MorningLossShare);

        // What the afternoon's goal named when it was last written, and the numbers it gave: the lamps held and the base's level, which
        // sets the fortification's price and HP (TickDusk writes the goal again when any of them changes).
        bool goalNamesLamps, goalNamesFortify;
        int goalLampStock = -1, goalFortifyLevel = -1;

        /// <summary>The afternoon goal with a sentence added for each thing wanted, after a full stop so the goal plate shows them under the
        /// goal (V5StoryGoal). A goal that already ends a sentence (the crate's) is not given a second full stop. The goal as it was when
        /// nothing is wanted.</summary>
        string WithAfternoonNudges(string goal)
        {
            goalNamesLamps = LampsWanted;
            goalNamesFortify = FortifyWanted;
            goalLampStock = lampStock;
            goalFortifyLevel = FortifyLevel;
            var sentences = new System.Collections.Generic.List<string>();
            if (goalNamesLamps)
                sentences.Add(rules.dailyFreeLamps > 0
                    ? ArenaText.Pair($"夜里要靠矿灯才能采暗物质：基地每天送 {rules.dailyFreeLamps} 盏，不够就回基地，在火箭页造矿灯，每盏 {rules.lampCost} 合金（现有 {lampStock} 盏）。",
                        $"Mining dark matter at night takes lamps: the base gives you {rules.dailyFreeLamps} a day; make more on the rocket page, {rules.lampCost} alloy each (you have {lampStock}).")
                    : ArenaText.Pair($"夜里要靠矿灯才能采暗物质：回基地，在火箭页造矿灯，每盏 {rules.lampCost} 合金（现有 {lampStock} 盏）。",
                        $"Mining dark matter at night takes lamps: make them on the rocket page, {rules.lampCost} alloy each (you have {lampStock})."));
            if (goalNamesFortify && FortifyPrice is CampaignCost price)
                sentences.Add(ArenaText.Pair($"基地可以加固：在整备页顶部花 {price.alloy} 合金，基地生命上限 {FortifyHpNow:0} → {FortifyHpNext:0}。",
                    $"The base can be fortified at the workshop page's top: {price.alloy} alloy for max HP {FortifyHpNow:0} → {FortifyHpNext:0}."));
            if (sentences.Count == 0) return goal;
            string gap = ArenaText.Pair("", " ");
            bool ended = goal.EndsWith("。") || goal.EndsWith(".");
            return goal + (ended ? gap : ArenaText.Pair("。", ". ")) + string.Join(gap, sentences);
        }

        /// <summary>TickDusk: the lamps were made or the base fortified (or the other way round), so the goal is written again.</summary>
        bool AfternoonNudgesChanged => goalNamesLamps != LampsWanted || goalNamesFortify != FortifyWanted
                                       || goalNamesLamps && goalLampStock != lampStock || goalNamesFortify && goalFortifyLevel != FortifyLevel;
    }
}
