using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace AnomalyArena
{

    [Serializable]
    public class Stamina
    {
        [Tooltip("Seconds of continuous sprinting on full stamina (the run's stats overwrite this)")] public float sprintDuration = 1f;
        [Tooltip("Seconds to refill from empty")] public float recoverDuration = 1.5f;
        [Tooltip("Movement speed multiplier while sprinting")] public float sprintMultiplier = 2f;
        [FormerlySerializedAs("minToRestart")]
        [Tooltip("Stamina (0–1) needed to start a sprint; a running sprint continues below it until empty")] [Range(0f, 1f)] public float minToStart = 0.3f;

        public float Value { get; private set; } = 1f;
        public bool Sprinting { get; private set; }

        public bool Exhausted => !Sprinting && Value < minToStart;

        public float Tick(float dt, bool wantSprint, float recoveryMultiplier = 1f)
        {
            bool canRun = Sprinting ? Value > 0f : Value >= minToStart;
            Sprinting = wantSprint && canRun;
            if (!Sprinting)
            {
                Value = Mathf.Min(1f, Value + dt * Mathf.Max(0f, recoveryMultiplier) / Mathf.Max(0.01f, recoverDuration));
                return 1f;
            }
            Value = Mathf.Max(0f, Value - dt / Mathf.Max(0.01f, sprintDuration));
            if (Value <= 0f) Sprinting = false;                                                                
            return sprintMultiplier;
        }

        public void Refill()
        {
            Value = 1f;
            Sprinting = false;
        }
    }
}
