using UnityEngine;

namespace AnomalyArena
{
    /// <summary>What a continued save (ArenaSaves) asks of the map: the same map laid again, the void trial's outcome, and the first black
    /// hole's cutscene counted as seen once it had played in the run.</summary>
    public sealed partial class WorldMap
    {
        /// <summary>The map to lay at the next scene load, by its place in biomes (-1: none asked). A continued save sets it before the
        /// scene is loaded again; Awake takes it, ahead of NextMapFamily, and clears it.</summary>
        public static int NextMapIndex { get; set; } = -1;

        // The editor keeps statics from one play to the next (domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetMapIndexPick() => NextMapIndex = -1;

        /// <summary>A continued save: whether the void trial was won in this run (the holes themselves come back as they were saved), and
        /// whether the first black hole's cutscene had played, so it does not play again. Call it after the campaign's map is in place
        /// (ApplyCampaignLayout makes the cutscene new for the campaign's hole).</summary>
        internal void RestoreRunState(bool trialWon, bool holeIntroDone)
        {
            TrialWon = trialWon;
            if (holeIntroDone && holeIntroStage == HoleIntroStage.Idle) holeIntroStage = HoleIntroStage.Done;
        }
    }
}
