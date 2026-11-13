using System;
using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// Sprint stamina (0–1). Full stamina allows sprintDuration seconds of continuous sprinting; it refills from empty in recoverDuration seconds when not sprinting.
    /// While any stamina is left a sprint may start and stop freely. Once the stamina runs out the body is exhausted: no sprint starts
    /// again until the stamina is full (the rule since 2026-11-10; before, a sprint needed 30% to start).
    /// Independent of the scene and physics, so it can be tested on its own.
    /// </summary>
    [Serializable]
    public class Stamina
    {
        [Tooltip("Seconds of continuous sprinting on full stamina (the run's stats overwrite this)")] public float sprintDuration = 1f;
        [Tooltip("Seconds to refill from empty")] public float recoverDuration = 1.5f;
        [Tooltip("Movement speed multiplier while sprinting")] public float sprintMultiplier = 2f;

        /// <summary>Current stamina, 0–1.</summary>
        public float Value { get; private set; } = 1f;
        public bool Sprinting { get; private set; }
        /// <summary>The stamina ran out and has not been full again since: no sprint can start.</summary>
        public bool Exhausted { get; private set; }

        /// <summary>Advances by dt seconds; wantSprint is whether the player wants to sprint right now. Returns the current movement speed multiplier.</summary>
        public float Tick(float dt, bool wantSprint, float recoveryMultiplier = 1f)
        {
            Sprinting = wantSprint && !Exhausted && Value > 0f;
            if (!Sprinting)
            {
                Value = Mathf.Min(1f, Value + dt * Mathf.Max(0f, recoveryMultiplier) / Mathf.Max(0.01f, recoverDuration));
                if (Value >= 1f) Exhausted = false;
                return 1f;
            }
            Value = Mathf.Max(0f, Value - dt / Mathf.Max(0.01f, sprintDuration));
            if (Value <= 0f) { Sprinting = false; Exhausted = true; } // this step still ran; the next sprint waits for a full bar
            return sprintMultiplier;
        }

        public void Refill()
        {
            Value = 1f;
            Sprinting = false;
            Exhausted = false;
        }

        /// <summary>A continued save: the stamina as it was saved (0-1), not sprinting; an empty bar is exhausted, as after a sprint that ran out.</summary>
        public void Restore(float value)
        {
            Value = Mathf.Clamp01(value);
            Sprinting = false;
            Exhausted = Value <= 0f;
        }
    }
}
