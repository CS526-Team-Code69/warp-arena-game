using System;
using UnityEngine;

namespace AnomalyArena
{

    [Serializable]
    public sealed class BossTuning
    {
        [Header("Nash Prophet: platform")]
        [Tooltip("x: the trial platform's radius, the same in all three phases (owner 2026-10-26: the platform no longer shrinks). y and z, its old phase-2 and phase-3 radii, are no longer used.")] public Vector3 prophetPlatformRadii = new(12f, 10f, 8f);
        [Min(0f), Tooltip("No longer used (owner 2026-10-26): the platform keeps its size in every phase.")] public float platformShrinkSpeed = .3f;

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

        [Header("Nash Prophet (2026-10-25): phases and move choice")]
        [Range(0f, 1f), Tooltip("Below this share of its max HP it enters phase 2: void pillars, the predictive volley and the gravity pull join, the rings come several at a time and the pulse pushes harder. The platform keeps its size.")] public float nashPhase2HpShare = .6f;
        [Range(0f, 1f), Tooltip("Below this share it enters phase 3: Shades instead of Wardens, bigger pillars, more rings and volley shots, a harder pulse and pull.")] public float nashPhase3HpShare = .3f;
        [Min(0f), Tooltip("Damage factor (Y): every hit deals the Hollow asset's damage × this × the move's share, whatever the wave. 1.12 is the wave-8 enemy factor, the wave its fixed HP is budgeted on.")] public float nashDamageFactor = 1.12f;
        [Min(0f), Tooltip("Player this close or closer (centre to centre): repulse pulse, void summon or equilibrium ring. Farther: equilibrium ring, predictive volley, void pillars or gravity pull (document 10-27).")] public float nashNearRange = 6.5f;
        [Min(0f), Tooltip("Seconds it waits after a move before winding up the next; after rings, a volley, a pull or a summon it counts from the moment they are over.")] public float nashMoveRest = 1.1f;
        [Min(0f), Tooltip("Close: chance weight of the repulse pulse.")] public float nashPulseWeight = 1f;
        [Min(0f), Tooltip("Close: chance weight of the void summon. It is only chosen while none of its summons lives and the summon rest is over.")] public float nashSummonWeight = 1.5f;
        [Min(0f), Tooltip("Close: chance weight of the equilibrium ring (0 keeps the ring a far move only).")] public float nashNearRingWeight = 1f;
        [Min(0f), Tooltip("Far, from phase 2: chance weight of the void pillars (document 10-27; before, a close move).")] public float nashPillarWeight = 1f;
        [Min(0f), Tooltip("Far: chance weight of the equilibrium ring.")] public float nashRingWeight = 1f;
        [Min(0f), Tooltip("Far, from phase 2: chance weight of the predictive volley.")] public float nashVolleyWeight = 1f;
        [Min(0f), Tooltip("Far, from phase 2: chance weight of the gravity pull.")] public float nashPullWeight = .8f;
        [Min(1f), Tooltip("While void pillars stand, the gravity pull's weight is multiplied by this, so it drags the player into them.")] public float nashPullPillarBonus = 2.5f;

        [Header("Nash Prophet (2026-10-25): repulse pulse and gravity pull")]
        [Min(0f), Tooltip("Wind-up of the repulse pulse; a ground circle shows its reach.")] public float nashPulseWindup = .8f;
        [Min(0f), Tooltip("Pulse reach from its centre in every phase (the player's body counts); the ground circle shows it. 7.5 reaches a cell past the close range (nashNearRange); the platform runs on past it to its edge, so the player can still step out of the circle. Every Bulwark wall any part of which lies inside it breaks when the pulse goes off (audit decision P29, 2026-10-27).")] public float nashPulseReach = 7.5f;
        [Min(0f), Tooltip("Pulse damage, × its base damage (Hollow asset damage × nashDamageFactor).")] public float nashPulseDamage = 1.5f;
        [Min(0f), Tooltip("Phase 1: cells the pulse pushes the player away from it (the slide runs about 5% longer). It throws off a player hit farther out than the platform radius less the slide: 4.8 on the 12-cell platform spares the whole close range and throws off only from about 7 cells out to the rim of its circle (8 cells: its reach plus the player's body).")] public float nashPulsePush = 4.8f;
        [Min(0f), Tooltip("Phase 2: cells the pulse pushes the player (owner 2026-10-27: 5.2). 5.2 on the 12-cell platform spares about the whole close range (6.5 cells out); only the outer part of its circle, about 1.5 cells wide, throws the player off. Above about 5.2 the outer end of the close range throws off too.")] public float nashPhase2PulsePush = 5.2f;
        [Min(0f), Tooltip("Phase 3: cells the pulse pushes the player (owner 2026-10-26). 7 on the 12-cell platform spares only a band a little over two player bodies wide (about 2.3 cells) outside its body, up to about 4.6 cells out; hit farther out, anywhere in its circle, the player goes off. Above about 7.3 that band gets narrower than two player bodies.")] public float nashPhase3PulsePush = 7f;
        [Min(0f), Tooltip("Wind-up of the gravity pull.")] public float nashPullWindup = .8f;
        [Min(0f), Tooltip("Seconds the gravity pull draws the player toward the middle. It deals no damage itself; the pillars do the killing.")] public float nashPullSeconds = 2.5f;
        [Min(0f), Tooltip("Cells per second the pull draws the player in, phases 1-2. Keep it under the player's walking speed (6) so walking out against it works.")] public float nashPullSpeed = 3.5f;
        [Min(0f), Tooltip("Cells per second the pull draws the player in, phase 3.")] public float nashPhase3PullSpeed = 5f;

        [Header("Nash Prophet (2026-10-25): void summon")]
        [Min(0f), Tooltip("Wind-up before the red crosses of a void summon appear.")] public float nashSummonWindup = 1f;
        [Min(0f), Tooltip("Seconds the red crosses show before the summons appear.")] public float nashSummonWarning = .8f;
        [Min(0), Tooltip("Wardens per summon in phases 1-2. While any of its summons lives it takes no damage.")] public int nashSummonCount = 3;
        [Min(0), Tooltip("Shades per summon in phase 3.")] public int nashPhase3SummonCount = 2;
        [Min(0f), Tooltip("Summons appear on a circle of this radius round it, the first behind it as seen from the player.")] public float nashSummonRingRadius = 3.5f;
        [Min(0f), Tooltip("Seconds after its last summon is gone before it may summon again.")] public float nashSummonCooldown = 10f;

        [Header("Nash Prophet (2026-10-25): void pillars, phase 2 on")]
        [Min(0f), Tooltip("Wind-up before the pillar circles appear.")] public float nashPillarWindup = .6f;
        [Min(0f), Tooltip("Seconds each circle fills from its rim before its pillar stands.")] public float nashPillarWarning = 1.4f;
        [Min(0f), Tooltip("Seconds a pillar stands. A player whose centre is inside a standing pillar dies at once.")] public float nashPillarLife = 5f;
        [Min(0), Tooltip("Pillars per cast in phase 2: the first on the player, the others stepping back toward the Prophet on alternate sides.")] public int nashPillarCount = 3;
        [Min(0), Tooltip("Pillars per cast in phase 3.")] public int nashPhase3PillarCount = 3;
        [Min(.1f), Tooltip("Pillar radius in phase 2.")] public float nashPillarRadius = 1.5f;
        [Min(.1f), Tooltip("Pillar radius in phase 3.")] public float nashPhase3PillarRadius = 2.2f;

        [Header("Nash Prophet (2026-10-25): equilibrium ring and predictive volley")]
        [Min(0f), Tooltip("Wind-up before the first ring.")] public float nashRingWindup = .8f;
        [Min(1), Tooltip("Shots per ring in phases 1-2.")] public int nashRingShots = 12;
        [Min(1), Tooltip("Shots per ring in phase 3 (more rings, so fewer shots each: the scene saves 32 enemy shots in all).")] public int nashPhase3RingShots = 10;
        [Min(1), Tooltip("Rings in a row in phase 2 (phase 1 fires one). Each later ring turns from the first by the shot spacing over the ring count, closing its gaps.")] public int nashPhase2Rings = 2;
        [Min(1), Tooltip("Rings in a row in phase 3.")] public int nashPhase3Rings = 3;
        [Min(0f), Tooltip("Seconds between two rings.")] public float nashRingInterval = .55f;
        [Min(.1f), Tooltip("Ring shot speed, cells per second.")] public float nashRingSpeed = 7f;
        [Min(0f), Tooltip("Damage of each ring shot, × its base damage. Shots never push.")] public float nashRingDamage = .8f;
        [Min(0f), Tooltip("Wind-up before a predictive volley.")] public float nashVolleyWindup = .6f;
        [Min(0), Tooltip("Shots of a predictive volley in phase 2.")] public int nashVolleyShots = 6;
        [Min(0), Tooltip("Shots of a predictive volley in phase 3.")] public int nashPhase3VolleyShots = 9;
        [Min(0f), Tooltip("Seconds between two shots of a volley.")] public float nashVolleyInterval = .09f;
        [Min(.1f), Tooltip("Volley shot speed, cells per second. Each is aimed where the player will be when it arrives.")] public float nashVolleySpeed = 13f;
        [Min(0f), Tooltip("Damage of each volley shot, × its base damage. Shots never push.")] public float nashVolleyDamage = .6f;
        [Min(0f), Tooltip("Degrees per second a volley shot turns toward where the player will be.")] public float nashVolleyTurnRate = 60f;
        [Min(0f), Tooltip("Seconds a volley shot keeps turning; after that it flies straight.")] public float nashVolleyTrackSeconds = .6f;
        [Min(0f), Tooltip("Its shots start this many cells outside its body.")] public float nashMuzzleGap = .3f;

        [Header("Nash Prophet (2026-10-25): trial terrain")]
        [Min(0), Tooltip("Broken fragments floating round the trial platform, with one black-hole portal on the platform for each.")] public int nashFragmentCount = 4;
        [Min(.5f), Tooltip("Fragment radius: 1.75 is 3.5 cells across, about three player bodies.")] public float nashFragmentRadius = 1.75f;
        [Min(0f), Tooltip("Cells from the platform's middle to each fragment's middle; the platform's edge is at 12 in every phase.")] public float nashFragmentDistance = 15.5f;
        [Tooltip("Direction of the first fragment, degrees clockwise from north (+z); the others are evenly spaced.")] public float nashFragmentAngle = 0f;
        [Min(0f), Tooltip("Cells from the platform's middle to each portal on it. At 6.5 a portal stands where the repulse pulse throws the player off in phases 2 and 3 (accepted by the owner, 2026-10-26: the portal is the way out of it).")] public float nashPortalRadius = 6.5f;
        [Tooltip("Direction of the first platform portal, degrees clockwise from north. Each portal leads to the fragment on the far side, so the player comes out in another direction.")] public float nashPortalAngle = 45f;
        [Min(.1f), Tooltip("The player stands on a portal while their centre is this close to its middle (its swirl is 0.65 in radius); only then, free to act, does E take it. Walking over a portal does nothing.")] public float nashPortalTrigger = .6f;
        [Min(0f), Tooltip("The player arrives this far from a fragment's middle, on its outer side, clear of the way-back portal in its middle.")] public float nashFragmentArrival = 1f;
        [Min(0f), Tooltip("Seconds after a jump before E takes the player through any portal again.")] public float nashPortalRearm = .4f;
        [Min(0f), Tooltip("Seconds the player stands still after every portal jump: a move key still held from the way in does not walk them off the small fragment or the platform before they see where they are, and no pull draws them. Shooting and skills still work. Keep it at least nashPortalRearm.")] public float nashPortalArrivalHold = .5f;

        [Header("Nash Prophet: knockback guard")]
        [Min(0f), Tooltip("No longer used (owner 2026-10-25): the trial platform edge does not stop a boss push any more, only an open hole does.")] public float edgeKnockMargin = 2.5f;
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
        [Range(0f, 1f), Tooltip("Share of its max HP it loses when a charge hits a wall, a bulwark or a hole (after its shield is cleared).")] public float wallHitHpShare = .03f;

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

        [Header("Tyrant (system doc 13.2, 2026-10-25): phases, enrage and move choice")]
        [Range(0f, 1f), Tooltip("Below this share of its max HP it enters its second and last phase, once: its shield is refilled (EncounterBalance.tyrantPhaseShieldShare), the Ground Pound, Royal Scepter and Astral Guard open, the Tail Sweep, Rampage Charge and Star Throw grow, and it is enraged.")]
        public float tyrantPhase2At = .5f;
        [Min(0f), Tooltip("Phase 1: seconds it walks toward you after a move before it picks the next one by your distance alone (on screen or not). Close is within the Tail Sweep's reach, middle within the Rampage Charge's longest run plus its leap, far is beyond.")]
        public float tyrantMoveGap = 1.8f;
        [Range(.1f, 1f), Tooltip("Phase 2 (enraged): that walk between two moves is this share as long. Together with the shorter wind-ups below, with the defaults a Tail Sweep comes about 1.75 times as often as in phase 1, a Star Throw about 2 times and a Rampage Charge from 10 cells about 1.5 times.")]
        public float tyrantEnragedGapShare = .4f;
        [Range(.1f, 1f), Tooltip("Phase 2 (enraged): the wind-ups of the Tail Sweep, the Rampage Charge and the Star Throw, and its pause after a charge's landing, are this share as long (their warnings show for less time too). The moves only phase 2 has keep their own wind-ups.")]
        public float tyrantEnragedWindupShare = .7f;
        [Min(1f), Tooltip("Phase 2 (enraged): its hits deal this many times their phase-1 damage.")]
        public float tyrantEnragedDamage = 1.25f;
        [Min(0f), Tooltip("Between moves it walks toward you until it is this close (centre to centre).")]
        public float tyrantWalkStop = 4.5f;
        [Range(0f, 1f), Tooltip("Phase 2, close: share of Ground Pounds (the rest are Tail Sweeps).")]
        public float poundShare = .4f;
        [Range(0f, 1f), Tooltip("Phase 2, middle distance: share of Royal Scepters while the scepter is ready (the rest are Rampage Charges).")]
        public float scepterShare = .4f;
        [Range(0f, 1f), Tooltip("Phase 2, far: share of Astral Guards while enough minions stand near it (the rest are Star Throws).")]
        public float astralShare = .5f;

        [Header("Tyrant: Tail Sweep (close, fan warning, breaks walls)")]
        [Min(0f), Tooltip("Wind-up; the fan shows on the ground. Shorter while enraged (tyrantEnragedWindupShare).")] public float sweepWindup = .9f;
        [Min(0f), Tooltip("Seconds the tail takes to cross the fan from its left edge to its right; it hits you as it passes.")] public float sweepSeconds = .45f;
        [Min(0f), Tooltip("Reach of the tail from its centre in phase 1 (your body counts); the close band ends here.")] public float sweepRange = 6.5f;
        [Min(0f), Tooltip("Reach of the tail in phase 2.")] public float sweepRangePhase2 = 9.5f;
        [Range(10f, 360f), Tooltip("Width of the fan in degrees (150 matches the saved sector art).")] public float sweepArc = 150f;
        [Min(0f), Tooltip("Half the tail's thickness, cells: it hits you and breaks walls this far beside its line.")] public float sweepTailHalfWidth = .6f;
        [Min(.5f), Tooltip("Map walls break in pieces at most this many cells across: the tail removes only the pieces it touches, for the rest of the fight. A bulwark cannot be broken by the tail (of the Tyrant's moves only the Rampage Charge breaks one) and shields you from it.")] public float sweepWallPiece = 2f;
        [Min(0f), Tooltip("Damage, × attack damage.")] public float sweepDamage = 1f;
        [Tooltip("Knockback tier on you (document 5.3: heavy); you are thrown along the swing and away from it.")] public KnockTier sweepKnock = KnockTier.Heavy;

        [Header("Tyrant: Ground Pound (close, phase 2, rectangle warning)")]
        [Min(0f), Tooltip("Wind-up; the rectangle shows on the ground in front of it.")] public float poundWindup = 1f;
        [Min(0f), Tooltip("Length of the rectangle from its front; the first wall ahead cuts it short.")] public float poundLength = 14f;
        [Min(0f), Tooltip("Width of the rectangle.")] public float poundWidth = 4f;
        [Min(1), Tooltip("The shockwave crosses the rectangle in this many steps, one band after another from its feet to the far end.")] public int poundSteps = 7;
        [Min(.01f), Tooltip("Seconds between two steps of the shockwave.")] public float poundStepSeconds = .12f;
        [Min(0f), Tooltip("Damage when the shockwave reaches you (once per pound), × attack damage.")] public float poundDamage = .9f;
        [Tooltip("Knockback tier on you; you are pushed along the shockwave.")] public KnockTier poundKnock = KnockTier.Medium;

        [Header("Tyrant: Rampage Charge (middle distance, line warning, breaks walls and bulwarks, knocks enemies aside)")]
        [Min(0f), Tooltip("Wind-up; the line, as wide as its body, shows the whole run and the leap up to the landing circle: nothing on the way stops it. Shorter while enraged (tyrantEnragedWindupShare).")] public float rushWindup = 1f;
        [Min(.1f), Tooltip("Run speed before the scene's global movement multiplier (WaveManager.movementSpeedMultiplier: 0.5 makes 18 run at 9 cells per second).")] public float rushSpeed = 18f;
        [Min(0f), Tooltip("Shortest run before the leap.")] public float rushMinRun = 3f;
        [Min(0f), Tooltip("Longest run before the leap in phase 1; it runs until it is one leap short of where you stood when it took aim.")] public float rushMaxRun = 9f;
        [Min(0f), Tooltip("Longest run before the leap in phase 2.")] public float rushMaxRunPhase2 = 14f;
        [Min(.5f), Tooltip("Nothing stops the run and it is never stunned: every wall its body runs into breaks, map walls in pieces at most this many cells across (only the pieces it touches go, for the rest of the fight), meteor fragments and bulwarks whole. It runs on over black holes, which never pull or swallow it.")] public float rushWallPiece = 2f;
        [Min(0f), Tooltip("Length of the leap at the end of the run: it lands this far on along the line (or on the last ground before you it can walk on, when you stand in a gravity ring).")] public float rushLeapDistance = 3f;
        [Min(.05f), Tooltip("Seconds in the air.")] public float rushLeapFlight = .55f;
        [Min(0f), Tooltip("Peak height of the leap, cells (visual only).")] public float rushLeapHeight = 2.5f;
        [Min(0f), Tooltip("Radius of the landing slam in phase 1 (bodies count); it hits everything inside, its own minions included.")] public float rushSlamRadius = 4f;
        [Min(0f), Tooltip("Radius of the landing slam in phase 2.")] public float rushSlamRadiusPhase2 = 6f;
        [Min(0f), Tooltip("Landing slam damage, × attack damage.")] public float rushSlamDamage = 1.3f;
        [Tooltip("Knockback tier of the landing slam (document 5.3: fly).")] public KnockTier rushSlamKnock = KnockTier.Fly;
        [Min(0f), Tooltip("Damage when the run itself runs you down (once per charge), × attack damage.")] public float rushRunDamage = .6f;
        [Tooltip("Knockback tier of the run: you are pushed aside out of its lane.")] public KnockTier rushRunKnock = KnockTier.Light;
        [Min(0f), Tooltip("The run knocks every other enemy its body runs into, its own minions too, out of its lane to the side each one stands on, instead of passing through it (owner 2026-10-26): each slides out until its body is clear of the lane by about this many cells. The shove deals no damage, a wall the enemy slides into does not hurt it either, and nothing slows the run; a Spire, which nothing moves, it still runs through.")] public float rushShoveClearance = .5f;
        [Min(0f), Tooltip("Seconds it stands still after the landing. Shorter while enraged (tyrantEnragedWindupShare).")] public float rushRecovery = 1f;

        [Header("Tyrant: Royal Scepter (middle distance, phase 2)")]
        [Min(0f), Tooltip("Seconds it raises the scepter before the red crosses show.")] public float scepterWindup = 1f;
        [Range(0f, 1f), Tooltip("One scepter calls this share of wave 14's crowd (its population target), with wave 14's kinds and mix; its minions alive or on the way never exceed a full wave-14 crowd.")] public float scepterCrowdShare = .5f;
        [Min(0f), Tooltip("The red crosses show on a ring of this radius around it (or on the usual spawn ring around you where that ground is taken).")] public float scepterRingRadius = 5f;
        [Min(0f), Tooltip("Seconds after a scepter before it can raise the next one.")] public float scepterCooldown = 18f;

        [Header("Tyrant: Star Throw (far, impact circle, leaves walls)")]
        [Min(0f), Tooltip("Seconds it kneads the meteor before it throws. Shorter while enraged (tyrantEnragedWindupShare).")] public float meteorWindup = .8f;
        [Min(.05f), Tooltip("Seconds the meteor flies; its impact circle shows where you stood at the throw and fills as the meteor nears.")] public float meteorFlight = 1.1f;
        [Min(0f), Tooltip("Peak height of the meteor's arc, cells (visual only).")] public float meteorHeight = 6f;
        [Min(0f), Tooltip("Impact radius in phase 1 (your body counts).")] public float meteorRadius = 3.5f;
        [Min(0f), Tooltip("Impact radius in phase 2.")] public float meteorRadiusPhase2 = 5.5f;
        [Min(0f), Tooltip("Impact damage, × attack damage.")] public float meteorDamage = 1.1f;
        [Tooltip("Knockback tier of the impact.")] public KnockTier meteorKnock = KnockTier.Medium;
        [Min(0), Tooltip("Fragments the meteor splits into in phase 1; each stands as a small wall.")] public int meteorFragments = 4;
        [Min(0), Tooltip("Fragments in phase 2.")] public int meteorFragmentsPhase2 = 6;
        [Range(0f, 1f), Tooltip("The fragments land evenly on a ring this share of the impact radius out from its centre (the ring turned at random).")] public float meteorFragmentSpread = .75f;
        [Min(.5f), Tooltip("Side of each fragment wall, cells.")] public float meteorFragmentSize = 1.6f;
        [Min(0f), Tooltip("Seconds a fragment wall stands (the Tail Sweep or the Rampage Charge breaks it sooner). A fragment never lands on a body, a wall or a hole.")] public float meteorFragmentLife = 12f;

        [Header("Tyrant: Astral Guard (far, phase 2)")]
        [Min(0f), Tooltip("Seconds it channels; a ring shows how far it reaches.")] public float astralChannel = 1.6f;
        [Min(0f), Tooltip("It devours minions within this distance of its centre (their body counts).")] public float astralRadius = 7f;
        [Min(1), Tooltip("It only channels when at least this many minions stand within reach.")] public int astralMinMinions = 2;
        [Min(1), Tooltip("Most minions one Astral Guard devours, the nearest first.")] public int astralMaxDevour = 4;
        [Range(0f, 1f), Tooltip("HP it restores per minion devoured, as a share of its max HP (0.05 = 5%).")] public float astralHealPerMinion = .05f;
        [Range(0f, 1f), Tooltip("Shield it gains per minion devoured, as a share of its max HP (0.025 = 2.5%).")] public float astralShieldPerMinion = .025f;
        [Range(0f, 1f), Tooltip("This never raises its shield past this share of its max HP.")] public float astralShieldCap = .2f;

        public static readonly BossTuning Defaults = new();
    }
}
