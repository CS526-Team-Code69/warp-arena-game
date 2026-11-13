using System;

namespace AnomalyArena
{
    /// <summary>Which mode a save belongs to.</summary>
    public enum SaveMode { Story, Open }

    /// <summary>The part of a story day a save starts at: the morning (上午), the afternoon (下午) or the night (夜晚).</summary>
    public enum SavePhase { Morning, Afternoon, Night }

    /// <summary>
    /// What the title's save windows show for one slot. An empty slot carries only its mode and slot number. Story saves fill the day,
    /// the phase and the ship's repairs; open-mode saves fill the wave and the map family; both list the weapons held.
    /// </summary>
    [Serializable]
    public sealed class SaveSummary
    {
        public SaveMode mode;
        /// <summary>0, 1 or 2.</summary>
        public int slot;
        public bool empty = true;

        // story
        /// <summary>The day the saved phase belongs to (1 and up; past the last day while the run is in overtime).</summary>
        public int day;
        public SavePhase phase;
        public bool engineFixed, armorFixed;
        public int fuel, fuelMax;

        // open mode
        /// <summary>The wave about to start, counting from 1; the last one is the Tyrant.</summary>
        public int wave;
        /// <summary>How many waves a run has, the Tyrant's included.</summary>
        public int waveCount;
        /// <summary>The map family that was laid: 0 the dunes, 1 the ruins (-1 for a map of neither; story saves fill it too).</summary>
        public int mapFamily;

        // both
        /// <summary>The weapons held, in the order hand 1, hand 2, auto 1, auto 2, empty slots left out.</summary>
        public WeaponForm[] weapons = Array.Empty<WeaponForm>();
        /// <summary>When the save was written, in seconds since 1970 (UTC).</summary>
        public long savedAtUnixSeconds;
        /// <summary>The slot's run was won (the story's ending has played, or the open mode's Tyrant fell): the slot shows the finished run
        /// and cannot be continued (ArenaSaves.CanContinue). A new game there deletes it first.</summary>
        public bool cleared;
    }
}
