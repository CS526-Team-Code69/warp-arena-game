using UnityEngine;

namespace AnomalyArena
{
    public enum HoleCheckState { Idle, Running, Passed, Failed }

    /// <summary>The Nash Prophet's answer that comes due: none, its void pillars or its equilibrium ring (they take turns).</summary>
    public enum HoleCounterMove { None, Pillars, Ring }

    /// <summary>
    /// The black hole's timed damage check (2026-11-10), as plain rules apart from the scene (V2Campaign.Hole.cs drives it). The first
    /// hit starts the clock with the day's requirement; every hit adds its damage, and the hit that reaches the requirement passes the
    /// check. The clock running out first fails it, and so does Fail (the player fell, or the hole was put away meanwhile). While it runs
    /// the Prophet answers: the first answer CounterDelay seconds in, then one every Interval seconds, which shrinks from IntervalStart
    /// to IntervalEnd as the damage share grows; pillars first, then the ring, by turns. Reset makes it new for the next day.
    /// </summary>
    public sealed class HoleCheck
    {
        public HoleCheckState State { get; private set; }
        public float Required { get; private set; }
        public float Seconds { get; private set; }
        public float Dealt { get; private set; }
        public float Elapsed { get; private set; }
        /// <summary>Answers cast so far in this check.</summary>
        public int MovesCast { get; private set; }
        public float CounterDelay { get; set; } = 3f;
        public float IntervalStart { get; set; } = 6f;
        public float IntervalEnd { get; set; } = 4f;
        float counterClock;

        /// <summary>The damage dealt as a share of the requirement, 0 to 1 (0 before the first hit).</summary>
        public float Progress => State == HoleCheckState.Passed ? 1f : Required > 0f ? Mathf.Clamp01(Dealt / Required) : 0f;

        /// <summary>Seconds left on the clock while it runs; the whole time before the first hit, none once it is over.</summary>
        public float SecondsLeft => State switch
        {
            HoleCheckState.Running => Mathf.Max(0f, Seconds - Elapsed),
            HoleCheckState.Idle => Seconds,
            _ => 0f
        };

        /// <summary>Seconds between the Prophet's answers at the damage share now.</summary>
        public float Interval => Mathf.Max(.1f, Mathf.Lerp(IntervalStart, IntervalEnd, Progress));

        public void Reset()
        {
            State = HoleCheckState.Idle;
            Required = Seconds = Dealt = Elapsed = 0f;
            MovesCast = 0;
            counterClock = 0f;
        }

        /// <summary>A hit of amount on the hole. The first starts the clock with this requirement and time limit (later hits keep the
        /// first ones). True when this hit reaches the requirement and passes the check; nothing counts once the check is over.</summary>
        public bool Hit(float amount, float required, float seconds)
        {
            if (amount <= 0f || State is HoleCheckState.Passed or HoleCheckState.Failed) return false;
            if (State == HoleCheckState.Idle)
            {
                State = HoleCheckState.Running;
                Required = Mathf.Max(1f, required);
                Seconds = Mathf.Max(.1f, seconds);
                Dealt = Elapsed = 0f;
                MovesCast = 0;
                counterClock = Mathf.Max(0f, CounterDelay);
            }
            Dealt += amount;
            if (Dealt < Required) return false;
            State = HoleCheckState.Passed;
            return true;
        }

        /// <summary>The clock moves on by dt while the check runs. Returns the answer that comes due now, if any; the time running out
        /// fails the check instead.</summary>
        public HoleCounterMove Tick(float dt)
        {
            if (State != HoleCheckState.Running || dt <= 0f) return HoleCounterMove.None;
            Elapsed += dt;
            if (Elapsed >= Seconds)
            {
                State = HoleCheckState.Failed;
                return HoleCounterMove.None;
            }
            counterClock -= dt;
            if (counterClock > 0f) return HoleCounterMove.None;
            counterClock += Interval;
            var move = MovesCast % 2 == 0 ? HoleCounterMove.Pillars : HoleCounterMove.Ring;
            MovesCast++;
            return move;
        }

        /// <summary>A running check fails at once (the player fell, or the hole was put away); before the first hit or once it is over
        /// nothing changes.</summary>
        public void Fail()
        {
            if (State == HoleCheckState.Running) State = HoleCheckState.Failed;
        }
    }
}
