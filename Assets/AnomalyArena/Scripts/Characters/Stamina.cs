using System;
using UnityEngine;

namespace AnomalyArena
{
    [Serializable]
    public class Stamina
    {
        [Tooltip("Seconds of continuous sprinting on full stamina")] public float sprintDuration = 0.5f;
        [Tooltip("Seconds to refill from empty")] public float recoverDuration = 1.5f;
        [Tooltip("Movement speed multiplier while sprinting")] public float sprintMultiplier = 2f;
        [Tooltip("After running out, minimum stamina (0–1) needed to sprint again")] [Range(0f, 1f)] public float minToRestart = 0.25f;

        public float Value { get; private set; } = 1f;
        public bool Sprinting { get; private set; }

        public bool Exhausted { get; private set; }

        public float Tick(float dt, bool wantSprint, float recoveryMultiplier = 1f)
        {
            Sprinting = wantSprint && !Exhausted && Value > 0f;
            if (Sprinting)
            {
                Value = Mathf.Max(0f, Value - dt / Mathf.Max(0.01f, sprintDuration));
                if (Value <= 0f) Exhausted = true;
            }
            else
            {
                Value = Mathf.Min(1f, Value + dt * Mathf.Max(0f, recoveryMultiplier) / Mathf.Max(0.01f, recoverDuration));
                if (Exhausted && Value >= minToRestart) Exhausted = false;
            }
            return Sprinting ? sprintMultiplier : 1f;
        }

        public void Refill()
        {
            Value = 1f;
            Exhausted = false;
            Sprinting = false;
        }
    }
}
