using UnityEngine;

namespace AnomalyArena
{
    // Day 0's forced spotlight tutorial and the weapon crates (implementation spec 2026-11-10, sections A and B). The spec's own numbers
    // are the step conditions (4 cells, 3 shots, 4 monsters, the 0.75 dim, the crate at six tenths of the ore trail); the rest are this
    // part's working values. None of these is in V2CampaignRules.asset yet, so the asset loads them at these defaults.
    public sealed partial class CampaignRules
    {
        [Header("Day 0: the forced spotlight tutorial")]
        [Range(0f, 1f), Tooltip("How dark the spotlight's dimmed screen is around its bright circle (0 none, 1 black).")]
        public float spotlightDim = .75f;
        [Min(.5f), Tooltip("Step 1: cells the player walks toward the goal's marker.")]
        public float tutorialMoveMeters = 4f;
        [Min(1), Tooltip("Step 2: pistol shots fired, in any direction.")]
        public int tutorialShots = 3;
        [Min(.05f), Tooltip("Step 5: seconds of sprinting that count as one sprint.")]
        public float tutorialSprintSeconds = .35f;
        [Min(1), Tooltip("Step 6: monsters of the first fight (the older tutorialGrunts is no longer read).")]
        public int tutorialFightGrunts = 4;
        [Min(4f), Tooltip("Step 6: cells ahead of the player, toward the base, where the first fight's monsters appear.")]
        public float tutorialFightDistance = 14f;
        [Min(1f), Tooltip("A step at a thing (the ore, the weapon crate, the dark matter) turns into its spotlight once the player is this close.")]
        public float tutorialTeachReach = 4.5f;
        [Min(0), Tooltip("Purple-black void crystals by the way home on Day 0, which cannot be mined.")]
        public int tutorialVoidCrystals = 3;

        [Header("Weapon crates")]
        [Min(.5f), Tooltip("Cells from a weapon crate within which E opens it.")]
        public float crateReach = 2.2f;
        [Min(1), Tooltip("The wave whose weapons Day 0's first crate offers (wave 1: the rifle, the knife and the rocket launcher).")]
        public int tutorialFirstCrateWave = 1;
        // 2, not 3, since the number ledger of 2026-11-10: wave 3's pool holds the Condenser's Shatter, so day 1 would open with a
        // weapon far above the reference one (数值说明-1110.md, section 11, the sixth question).
        [Min(1), Tooltip("The wave whose weapons Day 0's second crate offers (wave 2: every weapon of day 1 stays near the reference DPS).")]
        public int tutorialSecondCrateWave = 2;
        [Range(.1f, 1f), Tooltip("Where the afternoon's crate lies on the day's ore trail, as a share of the way from the base to the trail's farthest node.")]
        public float crateTrailShare = .6f;
        [Min(0f), Tooltip("Cells an afternoon crate keeps from every ore node, so E at it never mines and E at ore never opens it.")]
        public float crateNodeClearance = 3.2f;
    }
}
