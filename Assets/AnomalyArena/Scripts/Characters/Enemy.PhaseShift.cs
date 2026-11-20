using UnityEngine;

namespace AnomalyArena
{
    /// <summary>
    /// A boss's change of phase. The Tyrant fights in two phases and the Nash Prophet in three, each later phase beginning below a share
    /// of its max HP (BossTuning). A hit that would take it past the line of its next phase stops on the line and the rest of the hit is
    /// lost: the boss enters that phase at once (the Tyrant's shield is refilled then) and shows the change before it fights on. While the
    /// change runs it drops the move it was on (a leap in the air lands first), starts no move and takes no damage of any kind, and its
    /// phase clip plays (the Tyrant's Roar, the Prophet's PhaseShift) while light gathers on it; part way through, a burst throws a close
    /// player back without hurting them, and a short hold follows before its next move. What is already in the air or on the ground (a
    /// meteor, a pound's wave, pillars, a summon's red crosses) runs on. The line holds only while the boss is fighting: the Tyrant on its
    /// own turn, the Prophet inside the void trial. Numbers in BossTuning's phase change block; V5BossPhaseFx draws the change and
    /// V5BattleHud the lines on the boss bar.
    /// </summary>
    public partial class Enemy
    {
        float shiftLeft, shiftLength, guardHeardAt;
        bool shiftBegun, shiftBurst;

        /// <summary>A phase change is running: the boss starts no move and takes no damage.</summary>
        public bool PhaseShifting => shiftLeft > 0f;

        /// <summary>The change is showing: running, and not waiting for a leap in the air to land.</summary>
        public bool PhaseShiftShown => PhaseShifting && !RushLeaping;

        /// <summary>How far the change has run, 0 as it begins and 1 as it ends; 0 while none runs.</summary>
        public float PhaseShift01 => PhaseShifting && shiftLength > 0f ? 1f - shiftLeft / shiftLength : 0f;

        /// <summary>The running change's burst has gone off.</summary>
        public bool PhaseShiftBurstDone => PhaseShifting && shiftBurst;

        /// <summary>Counts the changes begun, their bursts and the hits turned away during a change, for the art layer.</summary>
        public int PhaseShiftSequence { get; private set; }

        public int PhaseBurstSequence { get; private set; }
        public int PhaseGuardSequence { get; private set; }

        /// <summary>The phases it fights in: two for the Tyrant, three for the Nash Prophet, one for any other body.</summary>
        public int PhaseCount => IsTyrant ? TyrantPhaseCount : IsProphet ? 3 : 1;

        /// <summary>The share of its max HP below which the given phase begins (2 or 3), or 0 when it has no such phase.</summary>
        public float PhaseLine(int phase)
        {
            if (phase < 2 || phase > PhaseCount) return 0f;
            var b = Boss;
            return Mathf.Clamp01(IsTyrant ? b.tyrantPhase2At : phase >= 3 ? b.nashPhase3HpShare : b.nashPhase2HpShare);
        }

        /// <summary>The line holds while the boss is fighting: the Tyrant whenever its turn runs, the Prophet inside the void trial (a body
        /// only configured as one, or switched off, takes its hits as any body does).</summary>
        bool PhaseLineHolds => isActiveAndEnabled && (IsTyrant || IsProphet && WorldMap.Active && WorldMap.Active.TrialActive);

        /// <summary>A hit stops on the line of its next phase (none once it fights in its last); during a change no hit takes it lower.</summary>
        protected override float HpFloor => !IsAlive ? 0f : PhaseShifting ? Hp : PhaseLineHolds ? maxHp * PhaseLine(bossPhase + 1) : 0f;

        protected override void OnHpFloorReached() => BeginPhaseShift(bossPhase + 1);

        /// <summary>Enters the phase and starts showing the change; a phase is entered once, and never an earlier one again.</summary>
        void BeginPhaseShift(int phase)
        {
            phase = Mathf.Min(phase, PhaseCount);
            if (!IsAlive || phase <= bossPhase) return;
            bossPhase = phase;
            if (IsTyrant) RefillTyrantPhaseShield();
            var b = Boss;
            shiftLength = shiftLeft = Mathf.Max(.1f, IsTyrant ? b.tyrantShiftSeconds : b.nashShiftSeconds);
            shiftBegun = shiftBurst = false;
            PhaseShiftSequence++;
            // Heard at once: the lock rings like a shield going up, then the power rises to the burst.
            V5WeaponAudio.At(V5Sfx.RingShieldUp, Position, 1f, .8f);
            V5WeaponAudio.AtFit(V5Sfx.EnergyRise, Position, shiftLength * Mathf.Clamp01(b.shiftBurstShare), IsTyrant ? .9f : 1f);
            if (IsTyrant) V5WeaponAudio.At(V5Sfx.TyrantGrowl, Position, 1.1f, .72f);
        }

        /// <summary>Ends a running change at once, as if it had run its course (checks that only need the phase use it).</summary>
        public void EndPhaseShiftNow()
        {
            if (!PhaseShifting) return;
            shiftLeft = 0f;
            shiftBegun = shiftBurst = false;
        }

        void ClearPhaseShift()
        {
            shiftLeft = shiftLength = 0f;
            shiftBegun = shiftBurst = false;
        }

        /// <summary>ReceiveDamage hook: a hit during a change does nothing at all (no HP, no shield, no stacks); it is heard glancing off
        /// and seen on the art layer (PhaseGuardSequence).</summary>
        bool TurnedAwayByPhaseShift()
        {
            if (!PhaseShifting) return false;
            PhaseGuardSequence++;
            if (Time.time - guardHeardAt >= .12f)
            {
                guardHeardAt = Time.time;
                V5WeaponAudio.At(V5Sfx.RingDeflect, Position, .55f, 1.15f);
            }

            return true;
        }

        /// <summary>The change's turn, called by the boss's own turn before it picks a move; true while the change runs (the boss does
        /// nothing else then). It faces the player meanwhile.</summary>
        bool TickPhaseShift(float dt, PlayerController player)
        {
            if (!PhaseShifting) return false;
            if (!shiftBegun)
            {
                shiftBegun = true;
                CancelWindup();
                if (IsTyrant && tyrantStage != TyrantStage.Idle) EndTyrantMove();
                if (IsProphet)
                {
                    nashRingsLeft = nashVolleyLeft = 0;
                    nashPullLeft = 0f;
                }
            }

            Stop();
            Vector3 to = player ? Query.Flat(player.Position - Position) : Vector3.zero;
            if (to.sqrMagnitude > .01f) facing = to.normalized;
            var b = Boss;
            shiftLeft -= dt;
            if (!shiftBurst && shiftLength - shiftLeft >= shiftLength * Mathf.Clamp01(b.shiftBurstShare))
            {
                shiftBurst = true;
                PhaseBurst(player);
            }

            if (shiftLeft > 0f) return true;
            shiftLeft = 0f;
            cooldown = Mathf.Max(cooldown, b.shiftResumeHold);
            return true;
        }

        /// <summary>The change's burst: heard and felt, and a player within its reach is thrown back without being hurt. It is a push, so a
        /// wall behind the player stops them without a wall slam; never into an open hole of the void trial, as with the boss's hits; not
        /// while the player cannot be seen or touched. A push is not scaled by body size, so the distance is scaled here as a blow's is.</summary>
        void PhaseBurst(PlayerController player)
        {
            PhaseBurstSequence++;
            var b = Boss;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.BossSlam, Position);
            V5WeaponAudio.At(IsTyrant ? V5Sfx.TyrantGrowl : V5Sfx.CapacitorBoom, Position, 1.2f, IsTyrant ? .62f : .8f);
            if (GM) GM.Shake(.32f);
            if (!player || !player.IsAlive) return;
            var world = WorldMap.Active;
            if ((world && world.PlayerHidden) || (GM && GM.skills && GM.skills.Invincible)) return;
            Vector3 away = Query.Flat(player.Position - Position);
            if (away.magnitude - player.Radius > b.shiftPushRadius) return;
            Vector3 direction = away.sqrMagnitude > 1e-4f ? away.normalized : facing;
            float push = Mathf.Max(0f, b.shiftPushDistance), scale = Mathf.Max(.0001f, player.knockbackScale);
            if (world && world.TrialActive) push = world.LimitBossKnockback(player.Position, direction, push * scale) / scale;
            if (push > 0f) player.Knockback(direction, push * scale, KnockKind.Push, this);
        }
    }
}
