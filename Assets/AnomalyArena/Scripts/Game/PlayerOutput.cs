using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// The player's damage output as the enemy lab measures it: a scale on everything the player deals to a monster (Combatant applies
    /// it before the monster's shield and the floor of one point), and a record of the damage that actually landed, for a rolling DPS
    /// and a time to kill. Only the enemy lab moves the scale; every play starts at 1, and the lab puts it back to 1 when it closes.
    /// </summary>
    public static class PlayerOutput
    {
        /// <summary>The scale on the player's damage to monsters; 1 leaves it as it is.</summary>
        public static float Scale = 1f;

        /// <summary>Damage that landed on monsters (HP and shield) since the last Reset.</summary>
        public static float Total { get; private set; }

        /// <summary>Seconds of game time since the last Reset.</summary>
        public static float Since => Time.time - resetAt;

        const int Capacity = 4096;
        static readonly float[] times = new float[Capacity], amounts = new float[Capacity];
        static int head, count;
        static float resetAt;

        /// <summary>The editor keeps statics from one play to the next, so every play starts unscaled and uncounted.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForPlay()
        {
            Scale = 1f;
            Reset();
        }

        /// <summary>Starts the count again: the total, the rolling window and the clock.</summary>
        public static void Reset()
        {
            Total = 0f;
            head = count = 0;
            resetAt = Time.time;
        }

        /// <summary>One hit or tick of the player's that landed on a monster (Combatant).</summary>
        public static void Record(float amount)
        {
            if (amount <= 0f) return;
            Total += amount;
            times[head] = Time.time;
            amounts[head] = amount;
            head = (head + 1) % Capacity;
            if (count < Capacity) count++;
        }

        /// <summary>Damage a second over the last window seconds of game time, or since the last Reset when that is shorter.</summary>
        public static float Dps(float window)
        {
            window = Mathf.Max(.1f, window);
            float now = Time.time, from = now - window, sum = 0f;
            for (int i = 0; i < count; i++)
            {
                int at = (head - 1 - i + Capacity) % Capacity;
                if (times[at] < from) break;
                sum += amounts[at];
            }
            float span = Mathf.Min(now - resetAt, window);
            return span > .05f ? sum / span : 0f;
        }
    }
}
