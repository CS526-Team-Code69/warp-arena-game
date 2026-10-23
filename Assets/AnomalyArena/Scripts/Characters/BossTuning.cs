using System;
using UnityEngine;

namespace AnomalyArena
{

    [Serializable]
    public sealed class BossTuning
    {
        [Header("Nash Prophet: platform")]
        [Tooltip("Trial platform radius in phases 1, 2 and 3.")] public Vector3 prophetPlatformRadii = new(12f, 10f, 8f);
        [Min(0f), Tooltip("Cells per second the platform shrinks toward the next phase's radius.")] public float platformShrinkSpeed = .3f;

        [Header("Nash Prophet: move choice")]
        [Min(0f), Tooltip("Phases 1-2: closer than this (centre to centre) it only pulses or inhales; farther it fires.")] public float prophetCloseRange = 6.3f;
        [Min(0f), Tooltip("Phase 3: the same split, closer in.")] public float prophetPhase3CloseRange = 4.5f;
        [Range(0f, 1f), Tooltip("Phase 1, far: share of predictive double shots (the rest are equilibrium rings).")] public float phase1DoubleShotShare = .6f;
        [Range(0f, 1f), Tooltip("Phase 2, far: share of predictive shots.")] public float phase2DoubleShotShare = .4f;
        [Range(0f, 1f), Tooltip("Phase 2, far: share of equilibrium rings (the rest are inhales).")] public float phase2RingShare = .3f;
        [Range(0f, 1f), Tooltip("Phase 2, close: share of repulse pulses (the rest are inhales).")] public float phase2ClosePulseShare = .6f;
        [Range(0f, 1f), Tooltip("Phase 3, far: share of predictive shots.")] public float phase3DoubleShotShare = .35f;
        [Range(0f, 1f), Tooltip("Phase 3, far: share of equilibrium rings (the rest are prophecy landings).")] public float phase3RingShare = .3f;

        [Header("Nash Prophet: predictive shot")]
        [Min(0f), Tooltip("Body-yellow wind-up before the shot (phase 3 stretches it to the 0.8 s floor).")] public float shotWindup = .6f;
        [Min(.1f), Tooltip("Shot speed, cells per second.")] public float shotSpeed = 8f;
        [Min(0f), Tooltip("Damage of each shot, × attack damage. Shots never push.")] public float shotDamage = 1f;
        [Min(0f), Tooltip("Phase 3: seconds after the first volley before it re-aims and fires again.")] public float shotSecondVolleyDelay = .4f;

        [Header("Nash Prophet: equilibrium ring")]
        [Min(0f), Tooltip("Body-yellow wind-up before the first ring.")] public float ringWindup = 1f;
        [Min(.1f), Tooltip("Ring shot speed, cells per second.")] public float ringSpeed = 6f;
        [Min(0f), Tooltip("Damage of each ring shot, × attack damage. Shots never push.")] public float ringDamage = .8f;
        [Min(1), Tooltip("Shots per ring in phases 1 and 2.")] public int ringShots = 12;
        [Min(1), Tooltip("Shots per ring in phase 3.")] public int phase3RingShots = 10;
        [Min(0f), Tooltip("Seconds between rings (two in phase 2, three in phase 3); each later ring is turned to close the earlier gaps.")] public float ringInterval = .6f;
        [Min(1), Tooltip("Rings per equilibrium ring move in phase 2; each later ring turns from the first by the shot spacing over this count (2: half a gap).")] public int phase2Rings = 2;
        [Min(1), Tooltip("Rings per move in phase 3; each later ring turns from the first by the spacing over this count (3: a third of a gap).")] public int phase3Rings = 3;

        [Header("Nash Prophet: repulse pulse and inhale")]
        [Min(0f), Tooltip("Wind-up of the pulse; a ground circle shows its reach.")] public float pulseWindup = .8f;
        [Min(0f), Tooltip("Pulse reach from the centre in phases 1-2 (the player's body counts).")] public float pulseReach = 5.5f;
        [Min(0f), Tooltip("Pulse reach in phase 3.")] public float phase3PulseReach = 4f;
        [Min(0f), Tooltip("Pulse damage, × attack damage.")] public float pulseDamage = 1.6f;
        [Min(0f), Tooltip("Cells the pulse pushes the player (capped short of the edge and of holes).")] public float pulsePush = 4f;
        [Min(0f), Tooltip("Wind-up of the inhale.")] public float inhaleWindup = .8f;
        [Min(0f), Tooltip("Seconds the inhale pulls.")] public float inhaleSeconds = 2f;
        [Min(0f), Tooltip("Cells per second the inhale pulls the player and its summons toward it.")] public float inhalePull = 4f;
        [Min(0f), Tooltip("Damage when the player touches it during an inhale (once per inhale), × attack damage. Summons are not hurt.")] public float inhaleContactDamage = 1.6f;
        [Min(0f), Tooltip("Cells that contact pushes the player.")] public float inhaleContactPush = 3f;
        [Min(0f), Tooltip("Wind-up of the pulse that always follows an inhale.")] public float inhaleFollowWindup = .6f;

        [Header("Nash Prophet: prophecy landing (phase 3)")]
        [Min(0f), Tooltip("Body-yellow wind-up before the two landing circles appear.")] public float prophecyWindup = .8f;
        [Min(.1f), Tooltip("Diameter of each landing circle and of the hole it becomes.")] public float prophecyDiameter = 3f;
        [Min(.1f), Tooltip("Seconds the circle fills from its edge to its centre before it becomes a hole; the first one is aimed where the player will be by then.")] public float prophecyFill = 1.2f;
        [Min(.1f), Tooltip("Seconds the hole stays open.")] public float prophecyHoleLife = 3f;
        [Min(0f), Tooltip("A landing centre never comes closer than this to the platform edge (the edge the platform is shrinking toward, if smaller).")] public float prophecyEdgeMargin = 2.5f;
        [Range(0f, 1f), Tooltip("The second landing circle lies on the line from the Prophet to the player, between this share of the way...")] public float prophecyBetweenMin = .3f;
        [Range(0f, 1f), Tooltip("...and this share (picked at random).")] public float prophecyBetweenMax = .8f;

        [Header("Nash Prophet: summons")]
        [Min(.1f), Tooltip("Seconds between summons (phase 3 runs the clock 1.3 times faster).")] public float prophetSummonInterval = 10f;
        [Min(0), Tooltip("Summons per call.")] public int prophetSummonCount = 4;
        [Min(0), Tooltip("Living summons never exceed this.")] public int prophetSummonCap = 8;
        [Min(0f), Tooltip("Seconds the red cross shows on each summon point before the summon appears.")] public float prophetSummonWarning = .6f;
        [Min(0f), Tooltip("Summons appear on a circle of this radius around the Prophet.")] public float prophetSummonRingRadius = 4f;
        [Min(.1f), Tooltip("Phase 3: the Prophet's summon clock runs this many times faster.")] public float prophetPhase3SummonRate = 1.3f;
        [Range(0f, 1f), Tooltip("Share of its max HP the Prophet loses for each summon that falls off the platform or into a hole.")] public float summonFallHpShare = .04f;

        [Header("Nash Prophet: knockback guard")]
        [Min(0f), Tooltip("A boss hit never pushes the player closer than this to the trial platform edge.")] public float edgeKnockMargin = 2.5f;
        [Min(0f), Tooltip("A boss hit never pushes the player closer than this to the edge of a void hole on the platform.")] public float holeKnockMargin = 1f;

        [Header("Tyrant: move choice")]
        [Min(0f), Tooltip("Up to this distance (centre to centre) it cleaves, slams or charges.")] public float tyrantCloseRange = 6.5f;
        [Min(0f), Tooltip("Up to this distance it slams or charges; beyond it charges or leaps.")] public float tyrantMidRange = 8.5f;
        [Min(0f), Tooltip("Beyond this distance, or while off screen, it only leaps.")] public float tyrantFarRange = 20f;
        [Range(0f, 1f), Tooltip("Close: share of cleaves.")] public float closeCleaveShare = .55f;
        [Range(0f, 1f), Tooltip("Close: share of slams (the rest are charges).")] public float closeSlamShare = .3f;
        [Range(0f, 1f), Tooltip("Middle band: share of slams (the rest are charges).")] public float midSlamShare = .5f;
        [Range(0f, 1f), Tooltip("Far and on screen: share of charges (the rest are leaps).")] public float farChargeShare = .7f;
        [Min(0f), Tooltip("Seconds after it appears before its first move (the banner time).")] public float tyrantOpeningGrace = 2.5f;

        [Header("Tyrant: cleave and back slash")]
        [Min(0f), Tooltip("Cleave wind-up; a sector shows on the ground.")] public float cleaveWindup = .8f;
        [Min(0f), Tooltip("Cleave reach from the centre (the player's body counts).")] public float cleaveRange = 6f;
        [Range(10f, 360f), Tooltip("Cleave width in degrees (150 matches the saved sector art).")] public float cleaveArc = 150f;
        [Min(0f), Tooltip("Cleave damage, × attack damage.")] public float cleaveDamage = 1f;
        [Min(0f), Tooltip("Cells the cleave pushes the player.")] public float cleavePush = 5f;
        [Min(0f), Tooltip("Phase 2 on: wind-up of the back slash that follows when the player got behind the first cut.")] public float backSlashWindup = .8f;
        [Min(0f), Tooltip("Back slash damage, × attack damage.")] public float backSlashDamage = .8f;

        [Header("Tyrant: slam")]
        [Min(0f), Tooltip("Slam wind-up; a ground circle shows its reach.")] public float slamWindup = 1f;
        [Min(0f), Tooltip("Slam reach from the centre (the player's body counts).")] public float slamRange = 8f;
        [Min(0f), Tooltip("Slam damage, × attack damage.")] public float slamDamage = .7f;
        [Min(0f), Tooltip("Cells the slam pushes the player.")] public float slamPush = 6f;

        [Header("Tyrant: charge")]
        [Min(0f), Tooltip("Charge wind-up; the line stops at the first wall or hole.")] public float chargeWindup = 1f;
        [Min(0f), Tooltip("Charge damage on the player, × attack damage.")] public float chargeDamage = 1.2f;
        [Min(0f), Tooltip("Cells a charge pushes the player.")] public float chargePush = 8f;
        [Min(1), Tooltip("Charges in a row in phase 2 (a wall or the player ends the run early).")] public int phase2Charges = 2;
        [Min(1), Tooltip("Charges in a row in phase 3.")] public int phase3Charges = 3;
        [Min(0f), Tooltip("Seconds it re-aims and redraws the line between two charges in a row.")] public float chargeReaim = .6f;
        [Range(0f, 1f), Tooltip("Share of its max HP it loses when a charge hits a wall, a bulwark or a hole (after its shield is cleared, before the stun).")] public float wallHitHpShare = .03f;
        [Min(0f), Tooltip("Seconds it is stunned after a charge hits a wall.")] public float wallHitStun = 2.5f;

        [Header("Tyrant: leap")]
        [Min(0f), Tooltip("Leap wind-up; the landing circle is drawn where the player stood when it took aim.")] public float leapWindup = .8f;
        [Min(.1f), Tooltip("Seconds in the air.")] public float leapFlight = .8f;
        [Min(0f), Tooltip("Peak height of the jump, cells (visual only).")] public float leapHeight = 2.5f;
        [Min(0f), Tooltip("Landing reach from the landing point (the player's body counts).")] public float leapRadius = 4f;
        [Min(0f), Tooltip("Landing damage, × attack damage.")] public float leapDamage = 1f;
        [Min(0f), Tooltip("Cells the landing pushes the player.")] public float leapPush = 6f;
        [Min(0f), Tooltip("Seconds it stands still after landing.")] public float leapRecovery = 1.2f;

        [Header("Tyrant: royal shield and summons")]
        [Min(0f), Tooltip("Seconds it stands yellow while raising the royal shield at a phase change (the shield share is EncounterBalance.tyrantPhaseShieldShare).")] public float royalShieldCast = 1.2f;
        [Min(1f), Tooltip("While the royal shield holds, the summon clock runs this many times faster.")] public float royalShieldSummonRate = 2f;
        [Min(.1f), Tooltip("Seconds between Tyrant summons.")] public float tyrantSummonInterval = 15f;
        [Min(0), Tooltip("Tyrant summons per call in phases 1-2.")] public int tyrantSummonCount = 6;
        [Min(0), Tooltip("Tyrant summons per call in phase 3.")] public int tyrantPhase3SummonCount = 9;
        [Min(.1f), Tooltip("Phase 3: the Tyrant's summon clock runs this many times faster.")] public float tyrantPhase3SummonRate = 1.25f;

        public static readonly BossTuning Defaults = new();
    }
}
