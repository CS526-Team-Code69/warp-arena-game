using System;
using System.Collections.Generic;
using UnityEngine;

namespace AnomalyArena
{
    [Serializable]
    public sealed class WeaponUpgradeTuning
    {
        public float swapProtection = .5f, recallTimeout = 6f, stickyManualDelay = .3f;
        [Header("V4 backfires — candidate values")]
        [Tooltip("Flame Stream fuel tank: a hit taken while the trigger is held bursts the tank (radius, damage, Fire stacks on you, seconds before it can burst again).")]
        public float fuelTankRadius = 2.5f;
        public float fuelTankDamage = 4f, fuelTankSelfFire = 3f, fuelTankCooldown = 1.5f;
        [Min(0f), Tooltip("Stream Tame: the fuel-tank burst knocks enemies in its ring back this far (scaled by knockback stats; large enemies and elites by their own factor). Bosses are not knocked back.")]
        public float fuelTankTamePush = 4f;
        [Min(0f), Tooltip("Wildfire Twist: two of your fire patches whose centres are at most this far apart, with no wall between, are joined by a fire fence.")]
        public float wildfireFenceRange = 9f;
        [Min(0f), Tooltip("Wildfire Twist: half the fence width; a body whose edge comes this close to the line between the two centres is touched.")]
        public float wildfireFenceHalfWidth = .5f;
        [Min(0f), Tooltip("Wildfire Twist: an enemy touching a fire fence burns for this much per second (wave-1 value, × R(w); several fences count once). "
                          + "The fence deals no direct hit; the burn goes on for StatusRules.elementWindow after the enemy leaves it.")]
        public float wildfireFenceFirePerSecond = 4f;
        [Min(0f), Tooltip("Wildfire Twist backfire: Burning stacks per second you get while you touch your own fence (none once tamed). "
                          + "Above 2 your Burning keeps growing while you stay on it.")]
        public float wildfireFenceSelfFirePerSecond = 2f;
        [Min(1f), Tooltip("Wildfire Tame: your movement multiplier while your centre is inside one of your fire patches (overlaps do not stack).")]
        public float wildfireTameSpeed = 1.25f;
        [Tooltip("Shatter fires three shards; the side shards leave the aim line by this many degrees.")]
        public float shatterSpread = 12f;
        [Min(1f), Tooltip("Shatter: flight speed of each ice shard, cells per second. The shards are real projectiles that stop on the first body or wall.")]
        public float shardSpeed = 40f;
        public float iceRadius = .9f, iceLife = 5f, iceFrostPerSecond = 4f;
        [Min(0f), Tooltip("Shatter Twist: a shard hitting an already frozen enemy throws it this far along the shard's flight (body size does not shorten it).")]
        public float shatterThrowDistance = 6f;
        [Min(0f), Tooltip("Shatter Twist: when the thrown enemy stops it bursts; other enemies within this radius (plus their own) are hit.")]
        public float shatterBurstRadius = 2f;
        [Min(0f), Tooltip("Shatter Twist: burst damage to the other enemies, as a share of one shard.")]
        public float shatterSplashShare = .5f;
        [Min(1), Tooltip("Shatter Tame: ice patches one weapon may keep at once; a new one removes the oldest (owner 2026-10-27: kept, no longer on the card).")]
        public int icePatchTamedCap = 6;
        public float blizzardConeAngle = 50f, blizzardMaxRange = 7f, blizzardFrostPerTick = .8f, blizzardFrozenHold = .6f;
        [Min(0f), Tooltip("Blizzard Twist: on release, damage per second each frozen enemy in the cone has been frozen under this spray (scaled like the weapon).")]
        public float blizzardShatterPerSecond = 3f;
        [Min(0f), Tooltip("Blizzard Twist: highest release damage per enemy before scaling.")]
        public float blizzardShatterCap = 9f;
        [Min(0f), Tooltip("Blizzard Twist: seconds the trigger must be held for the release to shatter anything.")]
        public float blizzardShatterMinHold = .5f;
        public float chainReturnRadius = 3f;
        [Min(0), Tooltip("Chain Tame: unused jumps stored for the next discharge, at most.")]
        public int chainStoreMax = 3;
        [Min(0)] public int sporePods = 3;
        [Min(0f), Tooltip("Cloud Tame: a spore pod you step on is kicked this far away from you and bursts where it lands.")]
        public float sporeKickDistance = 5f;
        public float sporePodRadius = .7f, sporePodLife = 12f, sporeBurstRadius = 1.8f, sporeBurstLife = 3f;

        public float spinUpFreeTurn = 1080f, spinUpFullTurn = 90f;
        [Header("10-21 design alignment — candidate values")]
        [Range(.05f, 1f), Tooltip("Charged Shot Tame: movement multiplier of non-boss enemies on your aim line while you charge "
                                  + "(0.3 = they move 70% slower; owner 2026-10-27, renamed so this default replaces the scene's 0.5).")]
        public float chargeLineSlowFactor = .3f;
        [Min(0f), Tooltip("Charged Shot Tame: half width of that aim line in cells; an enemy's own radius is added.")]
        public float chargeLineHalfWidth = .5f;
        [Range(0f, 90f), Tooltip("Reverse Shot Twist: on its first wall bounce a round splits in two, this many degrees either side of the bounce.")]
        public float reverseSplitAngle = 15f;
        [Range(0f, 1f), Tooltip("Reverse Shot Twist: damage share each split round keeps of the round's damage.")]
        public float reverseSplitShare = .6f;
        [Min(0f), Tooltip("Reverse Shot Tame: an enemy in front of you within this many cells (plus its radius), with no wall between, "
                          + "draws every other round of a clip forward at it; the rest still leave from your back.")]
        public float reverseGuardRange = 3f;
        [Range(0f, 360f), Tooltip("Reverse Shot Tame: full width in degrees of the cone in front of you (around the aim) that is guarded.")]
        public float reverseGuardAngle = 120f;
        [Range(0f, 1f), Tooltip("Strike Twist: damage share of the small blast at each enemy the strike killed.")]
        public float strikeChainShare = .5f;
        [Min(0f), Tooltip("Strike Twist: radius of each small blast in cells.")]
        public float strikeChainRadius = 2f;
        [Min(0f), Tooltip("Strike Twist: outward knockback of each small blast in cells.")]
        public float strikeChainPush = 2f;
        [Min(0f), Tooltip("Strike Twist: seconds between the strike and its small blasts.")]
        public float strikeChainDelay = .15f;
        [Min(0), Tooltip("Strike Twist: most small blasts one strike can set off.")]
        public int strikeChainMax = 6;
        [Min(0f), Tooltip("Strike Tame: your own blast pushes you this many cells instead of hurting and knocking you back.")]
        public float strikeJumpDistance = 6f;
        [Min(0f), Tooltip("Launch Yourself Twist: knockback (cells) of an enemy you hit at the missile's launch speed. A slower missile pushes less "
                          + "in proportion, down to rocketEndSpeedShare of this at the end of the flight. Bosses are not knocked back.")]
        public float rocketCueMax = 10f;
        [Range(.1f, 1f), Tooltip("Launch Yourself: the missile slows evenly over its flight to this share of its launch speed (1 = constant speed). "
                                 + "The launch speed rises to match, so a full flight takes as long as at the old constant speed.")]
        public float rocketEndSpeedShare = .5f;
        [Range(0f, 1f), Tooltip("Scatter Blast: damage share at the far edge of the cone. Point-blank deals 100%, falling off linearly with distance.")]
        public float scatterFalloffMin = .4f;
        [Min(0f), Tooltip("Scatter Blast: reload seconds once the two-round clip is empty (scaled like other reloads).")]
        public float scatterReload = 1.2f;
        [Min(0f), Tooltip("Scatter Blast Twist: the second round of each clip first pulls in enemies within this many cells inside its cone (scatterVacuumAngle).")]
        public float scatterVacuumRange = 8f;
        [Range(0f, 180f), Tooltip("Scatter Blast Twist: full width in degrees of the pull and of the blast that follows it.")]
        public float scatterVacuumAngle = 60f;
        [Min(0f), Tooltip("Scatter Blast Twist: a pulled enemy stops this many cells from your body.")]
        public float scatterVacuumGap = 1f;
        [Min(0f), Tooltip("Scatter Blast Twist: seconds between the pull and the blast.")]
        public float scatterVacuumDelay = .25f;
        [Range(0f, 1f), Tooltip("Scatter Blast Twist: damage share of that blast; it has no distance falloff.")]
        public float scatterVacuumShare = .6f;
        [Min(.01f), Tooltip("Scatter Focus: seconds of holding for a full charge.")]
        public float focusChargeTime = 1f;
        [Min(1), Tooltip("Scatter Focus: bolts in the released burst; the weapon's damage is split evenly between them.")]
        public int focusPellets = 6;
        [Min(0f), Tooltip("Scatter Focus: seconds between bolts of the burst.")]
        public float focusBurstInterval = .05f;
        [Range(0f, 1f), Tooltip("Scatter Focus: damage share of an uncharged release; a full charge deals 100%.")]
        public float focusMinChargeDamage = .4f;
        [Min(1f), Tooltip("Scatter Focus: bolt speed in units per second.")]
        public float focusPelletSpeed = 60f;
        [Min(0f), Tooltip("Scatter Focus backfire: cells the recoil pushes you back on an uncharged release.")]
        public float focusRecoilMin = 2f;
        [Min(0f), Tooltip("Scatter Focus backfire: cells the recoil pushes you back on a full charge. Owner 2026-10-26: a push you keep control through, never a knockback, so a wall behind you does not hurt.")]
        public float focusRecoilMax = 6f;
        [Range(0f, 1f), Tooltip("Scatter Focus Twist: damage share a bolt keeps after piercing its first target.")]
        public float focusPierceRetention = .6f;
        [Min(0f), Tooltip("Scatter Focus Tame: seconds you are immune to enemy attacks after an uncharged release (burn and poison ticks still land).")]
        public float focusGuardMin = .1f;
        [Min(0f), Tooltip("Scatter Focus Tame: seconds of that immunity after a full charge.")]
        public float focusGuardMax = .3f;
        [Min(1), Tooltip("Needler Twist: the needle that makes this many hits on one enemy within needleBurstWindow bursts on it.")]
        public int needleBurstCount = 6;
        [Min(0f), Tooltip("Needler Twist: seconds the hit count lasts after the last needle on that enemy.")]
        public float needleBurstWindow = 2f;
        [Min(0f), Tooltip("Needler Twist: other enemies within this radius (plus their own) of the bursting enemy are hit; the enemy itself is not.")]
        public float needleBurstRadius = 2f;
        [Min(0f), Tooltip("Needler Twist: burst damage as a multiple of one needle.")]
        public float needleBurstShare = 2f;
        [Range(0f, 1f), Tooltip("Execute: HP share below which a non-boss enemy is executed. Owner 2026-10-27: it no longer grows with kills.")]
        public float executeThreshold = .4f;
        [Min(0f), Tooltip("Execute Twist: seconds a hit enemy stays marked; any damage that leaves it under the execute line executes it.")]
        public float executeMarkSeconds = 6f;
        [Min(1), Tooltip("Execute Twist: marks at once; a new one replaces the oldest.")]
        public int executeMarkMax = 3;
        [Min(0f), Tooltip("Lance Twist: seconds a slug's path stays on the ground; the next slug that crosses it bursts at the crossing.")]
        public float lanceTrailSeconds = 3f;
        [Range(0f, 2f), Tooltip("Lance Twist: crossing blast damage as a share of one slug's base hit (Mk and cards included, the Tame brace not).")]
        public float lanceCrossShare = .6f;
        [Min(0f), Tooltip("Lance Twist: crossing blast radius, cells. Like the slug it skips the Shield's damage cut (the Shield still takes it first) and spares you.")]
        public float lanceCrossRadius = 2.5f;
        [Min(0f), Tooltip("Lance backfire: cells the recoil pushes you back on open ground. Owner 2026-10-26: a push you keep control through, never a knockback, so a wall behind you does not hurt.")]
        public float lanceRecoil = 4f;
        [Min(0f), Tooltip("Lance Tame: a wall this close behind you (cells) braces the shot: no recoil and more damage.")]
        public float lanceBraceDistance = 4f;
        [Min(1f), Tooltip("Lance Tame: damage multiplier of a braced shot.")]
        public float lanceBraceDamage = 1.4f;
        [Min(0f), Tooltip("Barrage Twist: seconds after the first rocket sticks to an enemy before all rockets on it merge into one blast.")]
        public float swarmHiveFuse = 1f;
        [Min(0f), Tooltip("Barrage Twist: merged blast radius gained per rocket after the first (starts at the rocket radius 1.5).")]
        public float swarmHivePerRocketRadius = .3f;
        [Min(0f), Tooltip("Barrage Twist: largest merged blast radius.")]
        public float swarmHiveMaxRadius = 3.6f;
        [Header("2026-10-23 weapon base fixes")]
        [Min(0f), Tooltip("A shot spawning inside a wall the body touches starts this far in front of the wall instead (cells).")]
        public float spawnWallBackoff = .2f;
        [Range(0f, 1f), Tooltip("Wildfire backfire: share of a patch's base tick damage (not scaled by Mk or cards) its own wielder takes. " +
                                "The fraction carries over between ticks instead of rounding each tick up to 1, so 0.75 at 4 ticks a second is about 3 HP/s.")]
        public float wildfireSelfShare = .75f;
        [Min(0f), Tooltip("Bombard: distance of the first blast from your centre. Above blast radius 2 + body 0.5, so standing still is safe; walking forward is not.")]
        public float bombardStart = 2.75f;
        [Min(0f), Tooltip("Bombard: seconds every blast waits after the shot; its landing marker shows meanwhile.")]
        public float bombardFuse = .25f;
        [Min(0f), Tooltip("Bombard: extra delay per blast along the line (blast i goes off after fuse + step x i).")]
        public float bombardStep = .15f;
        [Min(1), Tooltip("Bombard: number of blasts, spread evenly from bombardStart to the weapon's range (rangeB).")]
        public int bombardBlasts = 6;
        [Range(0f, 1f), Tooltip("Bombard Tame: share of the full cooldown the first touch of your own blast removes, once per shot.")]
        public float bombardTameCooldownCut = .25f;
        [Range(0f, 180f), Tooltip("Chain: the first target is the nearest visible enemy within range inside this cone around the aim (full width, degrees).")]
        public float chainConeAngle = 30f;
        [Min(0f), Tooltip("Chain: seconds the yellow arc between two consecutive targets stays visible.")]
        public float chainArcSeconds = .1f;
        [Min(.01f), Tooltip("Chain: width of that arc, cells.")]
        public float chainArcWidth = .3f;
        [Min(0f), Tooltip("Throwing Knife: seconds a knife lost to a black hole needs to come back to its slot (shown as its cooldown).")]
        public float knifeHoleReturn = 3f;
        [Min(0f), Tooltip("Throwing Knife Twist: radius in cells (plus each enemy's radius) of the spin slash where the knife comes home.")]
        public float knifeSpinRadius = 1.5f;
        [Range(0f, 2f), Tooltip("Throwing Knife Twist: damage share of the spin slash.")]
        public float knifeSpinShare = .8f;
        [Min(0), Tooltip("Flying Blade Tame: extra bounce targets of the next throw after a return sweeps through you (a catch).")]
        public int knifeCatchTargets = 2;
        [Min(0f), Tooltip("Hook Tame: an empty hook that hits a wall pushes you toward it, stopping this many cells short of the wall.")]
        public float hookTameWallGap = 1f;
        [Min(0f), Tooltip("Meltdown: base blast damage. Enemies take it scaled by Mk and cards; you always take this flat value.")]
        public float meltdownDamage = 20f;
        [Range(0f, 1f), Tooltip("Meltdown: share of full heat (4) at which a warning sound plays, once each time the heat climbs past it.")]
        public float meltdownWarnShare = .75f;
        [Min(0f), Tooltip("Meltdown Twist: heat removed for every enemy a bullet kills (full heat is 4, 0.1 per shot). Killing an elite clears all heat.")]
        public float meltdownKillVent = .2f;
        [Range(0f, 3f), Tooltip("Meltdown Tame: attack speed bonus the meltdown gives you instead of hurting you (0.5 = +50% attack rate on every weapon, like an Attack Speed card).")]
        public float meltdownTameHaste = .5f;
        [Min(0f), Tooltip("Meltdown Tame: seconds that attack speed bonus lasts; the next meltdown starts it again. The 3 s lockout still applies.")]
        public float meltdownTameHasteSeconds = 5f;
        [Min(0f), Tooltip("Whirl: seconds before the drone looks again when no movable, visible enemy is in range.")]
        public float whirlRetry = .5f;
        [Min(1f), Tooltip("Capacitor: highest charge; the HUD bar shows charge / this.")]
        public float capacitorMax = 30f;
        [Min(0f), Tooltip("Capacitor: charge per cell walked while an enemy is within capacitorNearRadius.")]
        public float capacitorChargeNear = 1.2f;
        [Min(0f), Tooltip("Capacitor: charge per cell walked with no enemy that close.")]
        public float capacitorChargeFar = .2f;
        [Min(0f), Tooltip("Capacitor: an enemy whose body edge is this close (cells) makes walking charge fast.")]
        public float capacitorNearRadius = 3f;
        [Min(0f), Tooltip("Capacitor: a click below this charge does nothing and costs no cooldown.")]
        public float capacitorMinCharge = 3f;
        [Min(1f), Tooltip("Capacitor: damage multiplier of a discharge at full charge (otherwise damage is damageB x charge / max).")]
        public float capacitorFullBonus = 1.5f;
        [Min(0f), Tooltip("Capacitor backfire: base damage of the self-discharge after full charge is held too long. You take it flat; enemies take it scaled.")]
        public float capacitorSelfBlast = 7.5f;
        [Min(0f), Tooltip("Capacitor Twist: seconds of your recent path that also take the discharge.")]
        public float capacitorTrailSeconds = 2f;
        [Min(.01f), Tooltip("Capacitor Twist: seconds between two recorded path points while the Capacitor is in your hand.")]
        public float capacitorTrailSample = .1f;
        [Min(0f), Tooltip("Capacitor Twist: an enemy whose body edge is this close to the path is hit (once per discharge).")]
        public float capacitorTrailWidth = 1f;
        [Min(0f), Tooltip("Capacitor Twist: two path points further apart than this (a blink or a teleport) are not joined.")]
        public float capacitorTrailBreak = 2f;
        [Min(1f), Tooltip("Shatter: damage multiplier of a shard on a frozen target; with the global frozen x1.3 it is about x2.")]
        public float shatterFrozenBonus = 1.55f;
        [Min(.1f), Tooltip("Spin-up: shots per second when you start holding.")]
        public float spinUpStartRate = 4f;
        [Min(.1f), Tooltip("Spin-up: shots per second at full spin (after 3 s of holding).")]
        public float spinUpTopRate = 10f;
        [Min(0f), Tooltip("Spin-up Twist: seconds the minigun keeps firing where you left it when you switch away at full spin.")]
        public float turretSeconds = 3f;
        [Min(0f), Tooltip("Spin-up Twist: seconds of holding fire needed before a switch leaves the minigun behind.")]
        public float turretSpinSeconds = 3f;
        [Range(0f, 1f), Tooltip("Spin-up Tame: knockback distance you take while braced against a wall at full spin, as a share.")]
        public float spinAnchorKnockFactor = .5f;
        [Min(0f), Tooltip("Spin-up Tame: seconds of holding fire (full spin) before backing into a wall braces you (no retreat, less knockback, no black-hole pull, faster fire).")]
        public float spinAnchorSpinSeconds = 3f;
        [Min(0f), Tooltip("Scatter Blast: knockback distance of every hit, cells.")]
        public float scatterKnockback = 6f;
        [Min(0f), Tooltip("Execute: seconds the tracer line of every shot stays visible.")]
        public float executeTracerSeconds = .08f;
        [Min(.01f), Tooltip("Execute: tracer line width, cells.")]
        public float executeTracerWidth = .15f;
        [Range(0f, 1f), Tooltip("Chance that a weapon with an element and another axis still open shows a change-element card instead.")]
        public float reinfuseChance = .15f;
        [Min(0f), Tooltip("Hook: gap kept between a held enemy and a wall in front of you, cells.")]
        public float hookWallGap = .05f;

        [Min(1f), Tooltip("Charged Shot backfire: enemy attacks (punches, projectiles, charges) that hit you while you charge deal this many times "
                          + "their damage (1.5 = +50%). Burn and Poison ticks and wall hits are not changed.")]
        public float chargeDamageTaken = 1.5f;
        [Range(0f, 1f), Tooltip("Charged Shot Tame: the backfire turns into a guard: enemy attacks that hit you while you charge deal this many "
                                + "times their damage instead (0.5 = half).")]
        public float chargeTameDamageTaken = .5f;
        [Range(0f, 360f), Tooltip("Charged Shot Twist: full width in degrees of the muzzle blast cone a full-charge release sends along the aim.")]
        public float chargedBlastAngle = 60f;
        [Min(0f), Tooltip("Charged Shot Twist: reach of the muzzle blast in cells from your centre (an enemy's own radius is added).")]
        public float chargedBlastRange = 3f;
        [Min(0f), Tooltip("Charged Shot Twist: knockback in cells of every enemy in the muzzle blast (scaled by knockback stats and body size; "
                          + "bosses and Spires are not moved). The blast itself deals no damage.")]
        public float chargedBlastPush = 6f;
        [Min(0f), Tooltip("Charged Shot Twist: a full-charge round deals this share of its first hit more for every enemy it has already passed "
                          + "through (0.2 = +20% each, added up).")]
        public float chargedPierceGrowth = .2f;
        [Min(0f), Tooltip("Charged Shot Twist: the most that growth can add, as a share of the first hit (1 = at most +100%, double damage).")]
        public float chargedPierceGrowthCap = 1f;
        [Min(0f), Tooltip("Reverse Shot Tame: every round's recoil pushes you this many cells along the aim (a push: you keep control and a "
                          + "wall does not hurt); a burst stacks the pushes.")]
        public float reverseTameRecoil = .7f;
        [Range(0f, 1f), Tooltip("Flying Blade Tame: after a catch the throw's cooldown is this share of the usual one (0.5 = half).")]
        public float knifeCatchCooldownShare = .5f;
        [Min(1f), Tooltip("Flying Blade Tame: the throw after a catch flies out and back this many times as fast (1.3 = 30% faster).")]
        public float knifeCatchSpeed = 1.3f;
        [Min(0f), Tooltip("Chain Blade Tame: the moment you and the caught enemy meet, a shockwave knocks other enemies within this many cells "
                          + "of you (plus their radius) back; no damage.")]
        public float hookShockRadius = 2.5f;
        [Min(0f), Tooltip("Chain Blade Tame: knockback of that shockwave in cells (scaled by knockback stats and body size; bosses and Spires "
                          + "are not moved).")]
        public float hookShockPush = 4f;

        [Header("2026-10-27 Scatter Blast shells, Shatter ice, Blizzard snow")]
        [Min(0f), Tooltip("Scatter Blast backfire: cells from your centre, left and right of the aim, where the two hot shells land when a reload starts.")]
        public float scatterShellSide = 1.2f;
        [Min(0f), Tooltip("Scatter Blast Tame: cells from your centre where the tamed shells land (owner decision 10-27: farther than the tamed fire's "
            + "radius, so you take a step to stand on one; standing still never finishes the reload by itself).")]
        public float scatterShellTameSide = 1.8f;
        [Min(0f), Tooltip("Scatter Blast backfire: seconds a landed shell lies before it flares up into a small fire.")]
        public float scatterShellFuse = .4f;
        [Min(0f), Tooltip("Scatter Blast backfire: radius of each shell fire, cells. Enemies touching it burn; you burn only with your centre on it (you stepped on it).")]
        public float scatterShellRadius = 1f;
        [Min(.01f), Tooltip("Scatter Blast backfire: seconds each shell fire burns.")]
        public float scatterShellBurn = 1.5f;
        [Min(0f), Tooltip("Scatter Blast backfire: damage of each 0.25 s tick of a shell fire on an enemy, as a share of one full point-blank blast (Mk and cards included).")]
        public float scatterShellShare = .15f;
        [Min(0f), Tooltip("Scatter Blast backfire: flat HP each 0.25 s tick of your own shell fire costs you (never scaled; a fraction waits for the next tick).")]
        public float scatterShellSelfDamage = .5f;
        [Min(1f), Tooltip("Scatter Blast Tame: shell fire radius multiplier (the tamed fire never burns you).")]
        public float scatterShellTameRadius = 1.5f;
        [Min(1f), Tooltip("Scatter Blast Tame: your movement multiplier after your centre touches a burning shell of yours while it reloads (that also finishes the reload).")]
        public float scatterShellTameSpeed = 1.3f;
        [Min(0f), Tooltip("Scatter Blast Tame: seconds that speed bonus lasts.")]
        public float scatterShellTameSpeedSeconds = 1.5f;
        [Min(.05f), Tooltip("Shatter ice: cells a body keeps sliding after it stops walking at its own normal speed; on ice it also starts and turns gradually. Bosses and the Spire never slip.")]
        public float iceSlideDistance = 1.5f;
        [Min(1f), Tooltip("Shatter Tame: your movement multiplier while you stand on your own ice (you no longer slip on it; enemies still do).")]
        public float shatterTameSpeed = 1.3f;
        [Min(.05f), Tooltip("Blizzard backfire: seconds between two snow patches laid at your feet while you spray.")]
        public float snowInterval = .3f;
        [Min(0f), Tooltip("Blizzard backfire: radius of each snow patch, cells.")]
        public float snowRadius = 1f;
        [Min(0f), Tooltip("Blizzard backfire: seconds each snow patch stays.")]
        public float snowLife = 3f;
        [Min(0f), Tooltip("Blizzard backfire: Frost stacks per second you gain standing in your snow (your own Frost still fades 1 a second).")]
        public float snowFrostPerSecond = 2f;
        [Min(0f), Tooltip("Blizzard backfire: Frost stacks your snow builds on you at most. Kept below the freeze threshold, so it only slows.")]
        public float snowFrostCap = 5f;
        [Range(0f, 1f), Tooltip("Blizzard Tame: multiplier on the damage you take while you stand in your own snow (0.7 = 30% less).")]
        public float snowTameDamageTaken = .7f;
        [Range(.05f, 1f), Tooltip("Blizzard Tame: movement multiplier of enemies standing in your snow; it lifts when they leave it.")]
        public float snowTameSlow = .6f;
        [Min(0f), Tooltip("Blizzard Tame: extra Frost stacks per second enemies standing in your snow gain.")]
        public float snowTameFrostPerSecond = 2f;

        [Header("2026-10-27 Arc Cannon, Venom Launcher, Railgun")]
        [Min(.1f), Tooltip("Arc Jump Tame: your movement multiplier while the Arc Cannon is in your hand and holds stored jumps (1.1 = 10% faster).")]
        public float chainTameMoveSpeed = 1.1f;
        [Min(0f), Tooltip("Poison Cloud: seconds a landed cloud lasts; a Twist detonation resets it to this.")]
        public float cloudSeconds = 5f;
        [Min(0f), Tooltip("Poison Cloud Twist: radius of the detonation as a multiple of the cloud's own radius (1.5 = half as wide again).")]
        public float cloudBlastRadiusShare = 1.5f;
        [Min(0f), Tooltip("Poison Cloud Twist: detonation damage to each enemy, as this many of the cloud's own ticks (6 ticks of 0.5 s = 3 s of cloud).")]
        public float cloudBlastTicks = 6f;
        [Min(0f), Tooltip("Needler backfire: radius in cells of the poison mist that bursts where you stand as each reload starts.")]
        public float needlerMistRadius = 1.2f;
        [Min(0f), Tooltip("Needler backfire: seconds that mist lasts (Tame: times needlerMistTameLength).")]
        public float needlerMistSeconds = 2f;
        [Min(.05f), Tooltip("Needler backfire: seconds between two ticks of that mist.")]
        public float needlerMistTick = .5f;
        [Min(0f), Tooltip("Needler backfire: base damage per mist tick. Enemies take it scaled by Mk and cards and are poisoned; you take it flat, without poison (rule R6).")]
        public float needlerMistDamage = 1f;
        [Min(1f), Tooltip("Needler Tame: the mist lasts this many times as long (2 = 2 s becomes 4 s); it no longer hurts you.")]
        public float needlerMistTameLength = 2f;
        [Min(1f), Tooltip("Needler Tame: your reload runs this many times as fast while your body touches one of the Needler's mists.")]
        public float needlerMistTameReload = 2f;
        [Min(.01f), Tooltip("Lance: width in cells of the rail beam drawn along the slug's real path.")]
        public float lanceBeamWidth = .35f;
        [Min(0f), Tooltip("Lance: seconds the rail beam stays visible after the slug stops.")]
        public float lanceBeamSeconds = .15f;
        [Range(0f, 1f), Tooltip("Execute: share of the shot's full cooldown given back at once when that shot kills or executes an enemy.")]
        public float executeKillRefund = .5f;
        [Min(0f), Tooltip("Execute: damage bonus of the next shot after a shot that killed (0.3 = +30%). It does not stack; the next shot spends it, kill or not.")]
        public float executeNextShotBonus = .3f;
        [Min(0f), Tooltip("Execute backfire: HP a shot that kills no enemy shocks you for; flat, never scaled by Mk or cards (rule R6). A boss hit but not killed does not count.")]
        public float executeSelfShock = 5f;
        [Min(1f), Tooltip("Execute backfire: a shot that kills no enemy puts the gun on this many times its cooldown (attack speed still shortens it).")]
        public float executeMissCooldown = 2.5f;
        [Min(0f), Tooltip("Execute Tame: seconds the arc shield circles you after a shot that kills no enemy; another such shot restarts it, it does not stack.")]
        public float executeAuraSeconds = 3f;
        [Min(0f), Tooltip("Execute Tame: radius in cells of the arc shield; enemies within it (plus their own radius), not behind a wall, are shocked.")]
        public float executeAuraRadius = 2.5f;
        [Min(.05f), Tooltip("Execute Tame: seconds between two shocks of the arc shield.")]
        public float executeAuraInterval = .5f;
        [Min(0f), Tooltip("Execute Tame: each shock of the arc shield deals this share of the shot's damage. It blocks no damage.")]
        public float executeAuraShare = .15f;

        [Min(0f), Tooltip("Spin-up backfire: cells per second the spin pushes you back against your aim while you hold fire, growing with the spin from 0 at the first shot to this at full spin (3 s). A push you keep control through, never a knockback: a wall behind you stops it without hurting you.")]
        public float spinUpRetreatSpeed = 1f;
        [Min(0f), Tooltip("Spin-up Tame: a wall (map bound or Bulwark) this close behind your body (cells, against the aim) braces you once you have held fire spinAnchorSpinSeconds.")]
        public float spinBraceWallGap = .25f;
        [Min(0f), Tooltip("Spin-up Tame: extra fire rate while braced against a wall (0.3 = +30%: 10 to 13 shots a second at full spin). The deployed Twist minigun keeps the plain top rate.")]
        public float spinBraceRateBonus = .3f;
        [Min(0f), Tooltip("Meltdown Tame: cells the overheat blast knocks back the enemies it hits (scaled by knockback stats; large enemies and elites by their own factor). Bosses and the Spire are not knocked back.")]
        public float meltdownTamePush = 4f;
        [Min(0f), Tooltip("Meltdown Tame: movement speed bonus for the same meltdownTameHasteSeconds as the attack speed bonus (0.3 = +30%), whichever weapon is in your hand.")]
        public float meltdownTameMoveBonus = .3f;
        [Min(0f), Tooltip("Barrage: degrees per second a missile turns toward the body it is locked on.")]
        public float barrageTurnRate = 360f;
        [Range(0f, 90f), Tooltip("Barrage: the missiles leave in a fan this many degrees either side of the aim (one locked on an enemy: either side of the way to it) and curve in from there.")]
        public float barrageLaunchSpread = 40f;
        [Min(0f), Tooltip("Barrage: within this distance of the edge of its target's body (cells) a missile flies straight at it, so it never circles a target it cannot turn tightly enough for.")]
        public float barrageTerminalRange = 2f;
        [Min(0), Tooltip("Barrage backfire: one missile locks one visible enemy in range; of the missiles left without an enemy, at most this many lock onto you and come back to blast you (the weapon's base damage each); the rest fly straight on. None once tamed.")]
        public int barrageSelfLockMax = 2;
        [Min(0f), Tooltip("Barrage backfire: seconds a missile locked onto you flies out along the aim before it turns back for you.")]
        public float barrageSelfLockDelay = .25f;
        [Min(0f), Tooltip("Barrage Tame: seconds the missiles circle you when there is no enemy to lock; the first enemy that comes within range meanwhile is locked at once, and the missiles still circling at the end vanish without a blast.")]
        public float barrageEscortSeconds = 1.5f;
        [Min(0f), Tooltip("Barrage Tame: radius of that escort circle around you, cells.")]
        public float barrageEscortRadius = 1.5f;
        [Min(0f), Tooltip("Barrage Tame: degrees per second the escorting missiles circle you.")]
        public float barrageEscortSpin = 240f;

        [Header("g5-auto: auto weapons (owner 2026-10-27)")]
        [Min(0f), Tooltip("Closed Blade: seconds before the blade tips can knock the same pickup or corpse again (they are checked every frame).")]
        public float orbitKnockRepeat = .5f;
        [Min(0f), Tooltip("Closed Blade: cells a corpse the blade tips touch is knocked outward, away from you (pickups use OrbPool.repelDistance).")]
        public float orbitCorpseKnock = 3f;
        [Min(1), Tooltip("Gunner: rounds in one magazine; when it is empty the drone drops to the ground to cool down.")]
        public int gunnerClip = 20;
        [Min(0f), Tooltip("Gunner: seconds a dropped drone lies on the ground before it flies back by itself with a full magazine; stepping on it does that at once.")]
        public float gunnerAutoReturn = 8f;
        [Min(1f), Tooltip("Gunner: bullet speed, cells per second.")]
        public float gunnerBulletSpeed = 30f;
        [Min(.05f), Tooltip("Sentry Drone: body radius in cells; a dropped drone is picked up when your body touches it.")]
        public float droneRadius = .4f;
        [Tooltip("Sentry Drone: where it hovers relative to your centre, cells: x and z on the ground, y its height (0.8 = the height shots fly at, so they visibly leave the drone). Its shots and beam leave from there.")]
        public Vector3 droneHoverOffset = new(-.8f, Query.CastHeight, .6f);
        [Min(0f), Tooltip("Whirl: nearest distance (cells, centre to centre) of an enemy it may pull; the farthest is the weapon's range (WeaponRoster rangeB).")]
        public float whirlMinRange = 5f;
        [Min(0f), Tooltip("Whirl: an enemy counts as alone only when no other enemy's body is within this many cells of its centre.")]
        public float whirlLoneRadius = 3f;
        [Min(0f), Tooltip("Whirl: the enemy is slammed where you stood this many seconds before the beam locked on; your afterimage marks the spot.")]
        public float whirlAfterimageSeconds = 2f;
        [Min(1f), Tooltip("Whirl: speed of the throw along the ground, cells per second; it lasts at least whirlMinFlight.")]
        public float whirlThrowSpeed = 14f;
        [Min(.05f), Tooltip("Whirl: shortest throw, seconds.")]
        public float whirlMinFlight = .35f;
        [Min(0f), Tooltip("Whirl: height of the throw's arc at its middle, cells.")]
        public float whirlArcHeight = 2f;
        [Min(0f), Tooltip("Whirl: other enemies within this many cells (plus their own radius) of the landing take the slam too; you never do.")]
        public float whirlSlamRadius = 1.5f;
        [Range(.05f, 1f), Tooltip("Deflect: movement multiplier of enemies inside the ring with no wall between, bosses included (0.7 = 30% slower).")]
        public float deflectSlow = .7f;
        [Range(0f, 1f), Tooltip("Deflect: chance an enemy projectile coming into the ring is deflected and fizzles, and that one of your own bullets or lobs leaving the ring is knocked off course; rolled once per projectile.")]
        public float deflectChance = .3f;
        [Range(0f, 90f), Tooltip("Deflect: one of your own projectiles knocked off course turns by a random angle up to this many degrees either way.")]
        public float deflectOwnAngle = 25f;
        [Min(0f), Tooltip("Drain: seconds without an enemy attack before the Energy Shield forms, at Mk1; a higher Mk divides it by its Mk damage factor.")]
        public float drainShieldWait = 4f;
        [Min(0f), Tooltip("Drain: Energy you gain each time the Energy Shield blocks an attack.")]
        public float drainBlockEnergy = 5f;
        [Min(0f), Tooltip("Drain Infusion (audit decision P23, 2026-10-27): each block releases one hit of the element on the enemies within this many cells (no wall between), damageB x the weapon's scale.")]
        public float drainInfuseRadius = 2.5f;
        [Min(0f), Tooltip("Drain backfire: seconds after the shield breaks during which the next enemy attack costs Energy (once per break).")]
        public float drainOverloadSeconds = 2f;
        [Min(0f), Tooltip("Drain backfire: Energy that attack costs.")]
        public float drainOverloadEnergy = 20f;

        [Min(.01f), Tooltip("Capacitor, stowed (document 5.4): seconds a stowed Capacitor takes to lose a full charge; it drains evenly (10 s = 3 charge a second at the default max of 30). While stowed its full-charge timer also runs back one second a second, so it never discharges while stowed and a quick switch out and back does not reset the timer.")]
        public float capacitorStowedDrainSeconds = 10f;
    }

    public enum GroundHazardKind { IcePatch, SporePod, ScatterShell, Snow }

    public struct GroundHazard
    {
        public GroundHazardKind kind;
        public Vector3 position;
        public float radius, timeLeft, life;
        public RunWeapon weapon;
    }

    public struct WeaponTracer
    {
        public Vector3 from, to;
        public float width, until;

        public bool drawnByV5;
        public Color color;
    }

    public sealed partial class RunWeapons
    {
        [Header("Approved anomaly upgrades — tuning")]
        public WeaponUpgradeTuning upgrades = new();
        readonly HashSet<Combatant> upgradeHits = new();

        struct IceStone { public Combatant body; public int version; public float hit; public Vector3 direction; public RunWeapon weapon; }
        readonly List<IceStone> iceStones = new();
        readonly Dictionary<Combatant, float> wildfireFenceHitAt = new();
        readonly Dictionary<Combatant, float> blizzardFrozenSince = new();

        readonly object blizzardDose = new();
        readonly Dictionary<(Combatant body, int version), (int count, float last)> needleHits = new();
        readonly List<Combatant> elementTargets = new();
        readonly List<Combatant> elementPruned = new();
        readonly List<GroundHazard> hazards = new();
        public IReadOnlyList<GroundHazard> Hazards => hazards;
        readonly List<WeaponTracer> tracers = new();
        public IReadOnlyList<WeaponTracer> Tracers => tracers;
        struct TrackedCloud { public AttackActor actor; public int sequence; public RunWeapon weapon; public Vector3 position; public float radius; public bool landed; }
        readonly List<TrackedCloud> sporeClouds = new();
        Combatant watchedPlayer;

        void ReleaseChannel(RunWeapon w)
        {
            var field=w.channelledField;
            if(!field||BattlefieldFrozen(field.gameObject))return;
            field.Release();
        }

        void StopTrigger(RunWeapon w)
        {
            if(w==null)return;

            if(w.form==WeaponForm.CryoCannon&&w.variant==1)blizzardFrozenSince.Clear();
            w.heldTime=0;
            if(player)player.RemoveMovementModifier(w);
            ReleaseChannel(w);
        }

        void ScatterBlast(RunWeapon w,float range,float hit)
        {

            if(w.Has(UpgradeAxis.Twist)&&w.rounds==1)ScatterVacuum(w);
            else
            {
                WeaponSound(w);
                if(V5Vfx.On)V5Vfx.I.ScatterShot(player.Position,player.AimDirection,range,60f,w.element);
                else Fx.Cone(player.Position,player.AimDirection,range,60,w.element);
                foreach(var body in Combatant.All)
                {
                    if(body==player||!body.IsAlive||!InCone(body,range,60)||WallBlocks(player.Position,body.Position))continue;
                    Vector3 delta=Query.Flat(body.Position-player.Position);
                    float falloff=Mathf.Lerp(1f,upgrades.scatterFalloffMin,Mathf.Clamp01(delta.magnitude/Mathf.Max(.01f,range)));
                    Damage(w,body,hit*falloff,upgrades.scatterKnockback,delta.normalized);
                }
            }
            if(SpendRound(w,upgrades.scatterReload))DropShells(w);
        }

        void Focus(RunWeapon w,Vector3 aim,float damage)
        {
            float charge=Mathf.Clamp01(w.charge);
            int pellets=Mathf.Max(1,upgrades.focusPellets);
            w.burstLeft=pellets;w.burstTimer=0f;w.burstDirection=aim;
            V5WeaponAudio.FocusRelease(charge,player.Position);
            w.burstDamage=damage/pellets*Mathf.Lerp(upgrades.focusMinChargeDamage,1f,charge);

            player.Push(-aim,FocusRecoil(w,charge));
            if(w.Has(UpgradeAxis.Tame))GuardFor(Mathf.Lerp(upgrades.focusGuardMin,upgrades.focusGuardMax,charge));
            TickBurst(w,0f);
        }

        void TickBurst(RunWeapon w,float dt)
        {
            if(!player||!player.IsAlive||!roster){w.burstLeft=0;return;}
            var d=roster.Get(w.form);float range=(d!=null?d.rangeB:20f)*w.Reach;
            bool twist=w.Has(UpgradeAxis.Twist);
            w.burstTimer-=dt;
            while(w.burstLeft>0&&w.burstTimer<=0f)
            {
                w.burstLeft--;w.burstTimer+=upgrades.focusBurstInterval;
                var a=Bolt(w,w.burstDirection,w.burstDamage,0,range,upgrades.focusPelletSpeed,twist);

                if(a&&twist){a.piercesLeft=1;a.pierceDamageRetention=upgrades.focusPierceRetention;}
            }
        }

        void Barrage(RunWeapon w,Vector3 aim,float range,float damage,float selfDamage)
        {
            int n=w.Has(UpgradeAxis.Reach)?11:8;bool tame=w.Has(UpgradeAxis.Tame);
            var locks=BarrageTargets(range);
            if(tame&&locks.Count==0){var far=BarrageNearest(player.Position,float.PositiveInfinity);if(far)locks.Add(far);}
            Vector3 forward=Query.Flat(aim).normalized;int selfLocks=0;
            for(int i=0;i<n;i++)
            {
                Combatant target=i<locks.Count?locks[i]:tame&&locks.Count>0?locks[i%locks.Count]:null;
                bool self=!tame&&!target&&selfLocks<upgrades.barrageSelfLockMax;
                if(self){target=player;selfLocks++;}

                float slot=n>1?i/(n-1f)*2f-1f:0f;
                Vector3 toward=target&&target!=player?Query.Flat(target.Position-player.Position):forward;
                if(toward.sqrMagnitude<1e-4f)toward=forward;
                var a=Bolt(w,Quaternion.Euler(0f,slot*upgrades.barrageLaunchSpread,0f)*toward.normalized,damage,0,range,15);if(!a)continue;
                a.mode=AttackMode.Swarm;a.radius=1.5f;a.hurtOwner=self;a.ownerDamage=selfDamage;
                if(tame&&locks.Count==0)a.EscortOwner(upgrades.barrageEscortSeconds,i*360f/n);
                else a.HomeOn(target,self?upgrades.barrageSelfLockDelay:0f);

                if(self){if(a.visual)a.visual.sharedMaterial=GameManager.Instance.Mat(BarrageSelfLockColor);Fx.Ring(player.Position,player.radius+.45f,BarrageSelfLockColor,.4f);}
                else if(target&&i<locks.Count)Fx.Ring(target.Position,target.radius+.35f,new Color(1f,.7f,.3f,.85f),.3f);
            }
        }
        static readonly Color BarrageSelfLockColor=new(1f,.25f,.2f,.95f);
        readonly List<Combatant> barrageLocks=new();

        List<Combatant> BarrageTargets(float radius)
        {
            barrageLocks.Clear();
            Vector3 at=player.Position;
            foreach(var c in Combatant.All)
                if(c!=player&&c.IsAlive&&Query.Flat(c.Position-at).sqrMagnitude<=radius*radius&&!WallBlocks(at,c.Position))barrageLocks.Add(c);
            barrageLocks.Sort((x,y)=>Query.Flat(x.Position-at).sqrMagnitude.CompareTo(Query.Flat(y.Position-at).sqrMagnitude));
            return barrageLocks;
        }

        public Combatant BarrageNearest(Vector3 from,float radius)
        {
            Combatant best=null;float nearest=radius*radius;
            foreach(var c in Combatant.All)
            {
                if(c==player||!c.IsAlive)continue;
                float d=Query.Flat(c.Position-from).sqrMagnitude;
                if(d>nearest||WallBlocks(from,c.Position))continue;
                best=c;nearest=d;
            }
            return best;
        }

        public Combatant BarrageEscortTarget(RunWeapon w,float radius)
        {
            Combatant best=null;int fewest=int.MaxValue;
            foreach(var c in BarrageTargets(radius))
            {
                int locked=0;
                foreach(var a in AttackPool.Instance.actors)if(a&&a.gameObject.activeSelf&&a.weapon==w&&a.mode==AttackMode.Swarm&&a.HomeTarget==c)locked++;
                if(locked<fewest){best=c;fewest=locked;}
            }
            return best;
        }

        void NeedlerReloadMist(RunWeapon w)
        {
            var mist=player&&AttackPool.Instance?AttackPool.Instance.Acquire():null;if(!mist)return;
            bool tame=w.Has(UpgradeAxis.Tame);
            float life=Mathf.Max(0f,upgrades.needlerMistSeconds)*(tame?Mathf.Max(1f,upgrades.needlerMistTameLength):1f);

            mist.Configure(AttackMode.Field,player,w,player.Position,player.AimDirection,upgrades.needlerMistDamage*DamageScale(w),0,0,0,upgrades.needlerMistRadius,life);
            mist.tickInterval=Mathf.Max(.05f,upgrades.needlerMistTick);mist.hurtOwner=!tame;mist.ownerDamage=upgrades.needlerMistDamage;
            WeaponSound(w,WeaponAudioPhase.Impact);
        }

        public bool InNeedlerMist(RunWeapon w)
        {
            if(w==null||!player||!AttackPool.Instance)return false;
            foreach(var actor in AttackPool.Instance.actors)
                if(actor&&actor.gameObject.activeSelf&&actor.weapon==w&&actor.mode==AttackMode.Field&&!BattlefieldFrozen(actor.gameObject)
                   &&Query.Flat(player.Position-actor.point).magnitude<=actor.radius+player.radius)return true;
            return false;
        }

        void TickNeedlerMists(float dt)
        {
            if(!player)return;
            float chill=ElementStatus.For(player)?.PlayerChillMultiplier??1f;
            foreach(var w in Hand)
                if(w!=null&&w.form==WeaponForm.SporeLauncher&&w.variant==1&&w.Has(UpgradeAxis.Tame)&&w.reload>0f&&InNeedlerMist(w))
                    w.reload=Mathf.Max(0f,w.reload-dt*chill*(Mathf.Max(1f,upgrades.needlerMistTameReload)-1f));
        }

        public void NeedleLanded(AttackActor needle,Combatant body)
        {
            var w=needle.weapon;
            if(w==null||!player||!body||body==player||body.Team==Team.Player)return;
            var key=(body,body.SpawnVersion);
            needleHits.TryGetValue(key,out var entry);
            if(autoClock-entry.last>upgrades.needleBurstWindow)entry.count=0;
            entry.count++;entry.last=autoClock;
            if(entry.count<Mathf.Max(1,upgrades.needleBurstCount)){needleHits[key]=entry;return;}
            needleHits.Remove(key);
            if(needleHits.Count>256)needleHits.Clear();
            Vector3 at=body.Position;
            elementTargets.Clear();
            foreach(var c in Combatant.All)
                if(c!=player&&c!=body&&c.IsAlive&&c.Team!=Team.Player&&Query.Flat(c.Position-at).magnitude<=upgrades.needleBurstRadius+c.radius&&!WallBlocks(at,c.Position))elementTargets.Add(c);
            WeaponSound(w,WeaponAudioPhase.Explosion);
            foreach(var c in elementTargets)Damage(w,c,needle.damage*upgrades.needleBurstShare,0,Query.Flat(c.Position-at).normalized);
            Fx.Ring(at,upgrades.needleBurstRadius,ElementCombat.ColorFor(ElementKind.Poison));
        }

        void BlizzardShatter(RunWeapon w)
        {
            if(w.heldTime<upgrades.blizzardShatterMinHold||!roster||!player){blizzardFrozenSince.Clear();return;}
            var d=roster.Get(w.form);
            float reach=Mathf.Min(d.rangeB*w.Reach,upgrades.blizzardMaxRange*w.Reach);
            elementTargets.Clear();
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||!InCone(body,reach,upgrades.blizzardConeAngle)||WallBlocks(player.Position,body.Position))continue;
                var status=ElementStatus.For(body);
                if(status&&status.Frozen)elementTargets.Add(body);
            }
            if(elementTargets.Count>0)WeaponSound(w,WeaponAudioPhase.Explosion);
            foreach(var body in elementTargets)
            {
                float seconds=blizzardFrozenSince.TryGetValue(body,out float since)?Mathf.Max(0f,autoClock-since):0f;
                float hit=Mathf.Min(upgrades.blizzardShatterCap,upgrades.blizzardShatterPerSecond*seconds)*DamageScale(w);
                if(hit>0f)Damage(w,body,hit,0,Query.Flat(body.Position-player.Position).normalized);
                ElementStatus.For(body)?.BreakFreeze();
                Fx.Ring(body.Position,body.radius+.4f,ElementCombat.ColorFor(ElementKind.Frost));
            }
            blizzardFrozenSince.Clear();
        }

        void RecallBlast(RunWeapon w,Vector3 from,Vector3 to,float area,float hit)
        {
            WeaponSound(w,WeaponAudioPhase.Explosion);
            upgradeHits.Clear();
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive)continue;

                bool destination=Query.Flat(body.Position-to).magnitude<=area+body.radius&&!WallBlocks(to,body.Position);
                bool departure=w.Has(UpgradeAxis.Twist)&&Query.Flat(body.Position-from).magnitude<=area+body.radius&&!WallBlocks(from,body.Position);
                if((destination||departure)&&upgradeHits.Add(body))Damage(w,body,hit,5,Query.Flat(body.Position-(destination?to:from)).normalized);
            }
            Fx.Ring(to,area,ElementCombat.ColorFor(w.element));
            if(w.Has(UpgradeAxis.Twist))Fx.Ring(from,area,ElementCombat.ColorFor(w.element));
        }

        void Stream(RunWeapon w,float range,float hit)
        {
            WeaponSound(w);
            upgradeHits.Clear();
            Vector3 origin=player.Position,dir=player.AimDirection;
            bool wall=Physics.Raycast(Query.AtCastHeight(origin),dir,out var impact,range,GameManager.WallMask,QueryTriggerInteraction.Ignore);
            float first=wall?impact.distance:range;
            StreamSegment(w,origin,dir,first,hit);
            if(w.Has(UpgradeAxis.Twist))BurnEnemyShots(origin,dir,first);
        }

        void BurnEnemyShots(Vector3 origin,Vector3 dir,float range)
        {
            var bolts=GameManager.Instance&&GameManager.Instance.waves?GameManager.Instance.waves.preparedBolts:null;
            if(bolts==null)return;
            foreach(var bolt in bolts)
            {
                if(!bolt||!bolt.gameObject.activeSelf||!bolt.Hostile||BattlefieldFrozen(bolt.gameObject))continue;
                Vector3 delta=Query.Flat(bolt.transform.position-origin);
                if(delta.magnitude>range+EnemyBolt.HitRadius||delta.sqrMagnitude>.0001f&&Vector3.Angle(dir,delta)>20f)continue;
                if(Physics.Linecast(Query.AtCastHeight(origin),Query.AtCastHeight(bolt.transform.position),GameManager.WallMask,QueryTriggerInteraction.Ignore))continue;
                Fx.Pop(bolt.transform.position,new Color(1f,.6f,.25f),.6f);
                bolt.Burn();
            }
        }

        void StreamSegment(RunWeapon w,Vector3 origin,Vector3 dir,float range,float hit)
        {
            Fx.Cone(origin,dir,range,40,w.element);
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||upgradeHits.Contains(body))continue;
                Vector3 delta=Query.Flat(body.Position-origin);
                if(delta.magnitude>range+body.radius||Vector3.Angle(dir,delta)>20||Physics.Linecast(Query.AtCastHeight(origin),Query.AtCastHeight(body.Position),GameManager.WallMask))continue;
                upgradeHits.Add(body);Damage(w,body,hit,0,delta.normalized,continuous:true);
            }
        }

        void Shatter(RunWeapon w,Combatant first,float hit,Vector3 dir)
        {
            WeaponSound(w,WeaponAudioPhase.Fire,true);
            var status=ElementStatus.For(first);
            bool frozen=status&&status.Frozen;

            if(w.Has(UpgradeAxis.Twist)&&frozen&&first is not Enemy{Immovable:true}&&first.State!=CharacterState.Hooked&&!IsIceStone(first))
            {

                first.Throw(dir,upgrades.shatterThrowDistance,new ThrowImpact{dealer=player,damage=0f,sizeExponent=0f,radius=0f,knockback=0f,element=w.element});
                if(first.State==CharacterState.Thrown)
                {
                    iceStones.Add(new IceStone{body=first,version=first.SpawnVersion,hit=hit,direction=Query.Flat(dir).normalized,weapon=w});
                    return;
                }
            }
            Damage(w,first,hit*(frozen?upgrades.shatterFrozenBonus:1f),0,dir);
        }

        bool IsIceStone(Combatant body){foreach(var stone in iceStones)if(stone.body==body&&stone.version==body.SpawnVersion)return true;return false;}

        void TickIceStones()
        {
            for(int i=iceStones.Count-1;i>=0;i--)
            {
                var stone=iceStones[i];
                if(!stone.body||stone.body.SpawnVersion!=stone.version){iceStones.RemoveAt(i);continue;}
                if(BattlefieldFrozen(stone.body.gameObject))continue;
                if(!stone.body.IsAlive){iceStones.RemoveAt(i);continue;}
                if(stone.body.State==CharacterState.Thrown)continue;
                iceStones.RemoveAt(i);
                BurstIceStone(stone);
            }
        }

        void BurstIceStone(IceStone stone)
        {
            var w=stone.weapon;var body=stone.body;Vector3 at=body.Position;
            if(w==null||!player)return;
            elementTargets.Clear();
            foreach(var c in Combatant.All)
                if(c!=player&&c!=body&&c.IsAlive&&c.Team!=Team.Player&&Query.Flat(c.Position-at).magnitude<=upgrades.shatterBurstRadius+c.radius&&!WallBlocks(at,c.Position))elementTargets.Add(c);
            WeaponSound(w,WeaponAudioPhase.Explosion);

            Damage(w,body,stone.hit*upgrades.shatterFrozenBonus,0,stone.direction);
            foreach(var c in elementTargets)Damage(w,c,stone.hit*upgrades.shatterSplashShare,0,Query.Flat(c.Position-at).normalized);
            Fx.Ring(at,upgrades.shatterBurstRadius,ElementCombat.ColorFor(ElementKind.Frost));
        }

        public float ExecuteThreshold(RunWeapon w)=>upgrades.executeThreshold;

        void Execute(RunWeapon w,float range,float hit)
        {

            hit*=1f+Mathf.Max(0f,w.executeNextShot);w.executeNextShot=0f;
            var target=RayTarget(player.AimDirection,range);

            WeaponSound(w);
            Vector3 muzzle=Muzzle(player.AimDirection),end;
            if(target)end=Query.AtCastHeight(target.Position);
            else
            {
                end=Query.AtCastHeight(player.Position+player.AimDirection*range);
                if(Query.WallBetween(Query.AtCastHeight(player.Position),end,out var wall))end=Query.AtCastHeight(wall.point);
            }
            AddTracer(muzzle,end,upgrades.executeTracerWidth,upgrades.executeTracerSeconds,new Color(1f,.85f,.55f,.9f));
            bool killed=false;
            if(target)
            {

                Unmark(target as Enemy);
                Damage(w,target,hit,0,player.AimDirection);

                if(target.IsAlive&&target is Enemy{IsBoss:false} low&&target.Hp/target.maxHp<ExecuteThreshold(w))ExecuteKill(w,low);
                killed=!target.IsAlive;

                if(target.IsAlive&&w.Has(UpgradeAxis.Twist)&&target is Enemy{IsBoss:false} survivor)MarkForExecute(w,survivor);
            }
            if(killed)
            {
                w.cooldown=Mathf.Max(0f,w.cooldown-Mathf.Clamp01(upgrades.executeKillRefund)*CycleSeconds(w));
                w.executeNextShot=Mathf.Max(0f,upgrades.executeNextShotBonus);
                Fx.Ring(player.Position,.9f,new Color(1f,.85f,.45f,.9f));
            }
            else if(target is not Enemy{IsBoss:true})ExecuteBackflow(w,hit);
        }

        void ExecuteBackflow(RunWeapon w,float hit)
        {
            Color arc=ElementCombat.ColorFor(ElementKind.Lightning);
            if(w.Has(UpgradeAxis.Tame))
            {

                if(w.executeAuraLeft<=0f)w.executeAuraClock=0f;
                w.executeAuraLeft=Mathf.Max(0f,upgrades.executeAuraSeconds);w.executeAuraDamage=hit*Mathf.Max(0f,upgrades.executeAuraShare);
                Fx.Ring(player.Position,upgrades.executeAuraRadius,arc);
                return;
            }
            if(upgrades.executeSelfShock>0f)player.ReceiveDamage(DamageInfo.Attack(upgrades.executeSelfShock,player));                                                
            w.cooldown=Mathf.Max(w.cooldown,CycleSeconds(w)*Mathf.Max(1f,upgrades.executeMissCooldown));w.holeReturnSpan=w.cooldown;
            Fx.Ring(player.Position,.9f,arc);Fx.Burst(Query.AtCastHeight(player.Position),Vector3.zero,arc,6,4f,360f,.2f);
        }

        void TickExecuteAura(RunWeapon w,float dt)
        {
            if(w.executeAuraLeft<=0f)return;
            if(!player||!player.IsAlive){w.executeAuraLeft=0f;return;}
            w.executeAuraLeft=Mathf.Max(0f,w.executeAuraLeft-dt);
            if(w.executeAuraLeft<=0f)return;
            float radius=upgrades.executeAuraRadius;Vector3 at=player.Position;

            if(Mathf.Floor((w.executeAuraLeft+dt)*8f)!=Mathf.Floor(w.executeAuraLeft*8f))Fx.Ring(at,radius,ElementCombat.ColorFor(ElementKind.Lightning),.15f);
            w.executeAuraClock-=dt;
            if(w.executeAuraClock>0f)return;
            w.executeAuraClock+=Mathf.Max(.05f,upgrades.executeAuraInterval);

            auraTargets.Clear();
            foreach(var c in Combatant.All)
                if(c!=player&&c.IsAlive&&c.Team!=Team.Player&&Query.Flat(c.Position-at).magnitude<=radius+c.radius&&!WallBlocks(at,c.Position))auraTargets.Add(c);
            foreach(var c in auraTargets)Damage(w,c,w.executeAuraDamage,0,Query.Flat(c.Position-at).normalized,false,true);
            auraTargets.Clear();
        }
        readonly List<Combatant> auraTargets = new();

        void ExecuteKill(RunWeapon w,Enemy target)
        {
            Vector3 at=target.Position;
            target.Kill(player);
            if(target.IsAlive)return;
            Fx.Ring(at,1.2f,new Color(1f,.3f,.25f,.9f));
        }

        struct ExecuteMark { public Enemy enemy; public int spawnVersion; public float left; public RunWeapon weapon; }
        readonly List<ExecuteMark> executeMarks = new();
        readonly List<ExecuteMark> frozenExecuteMarks = new();

        void MarkForExecute(RunWeapon w,Enemy enemy)
        {
            Unmark(enemy);
            if(executeMarks.Count>=Mathf.Max(1,upgrades.executeMarkMax))executeMarks.RemoveAt(0);
            executeMarks.Add(new ExecuteMark{enemy=enemy,spawnVersion=enemy.SpawnVersion,left=upgrades.executeMarkSeconds,weapon=w});
        }

        void Unmark(Enemy enemy){if(!enemy)return;for(int i=executeMarks.Count-1;i>=0;i--)if(executeMarks[i].enemy==enemy)executeMarks.RemoveAt(i);}

        public bool IsExecuteMarked(Enemy enemy)
        {
            foreach(var m in executeMarks)if(m.enemy==enemy&&enemy&&m.spawnVersion==enemy.SpawnVersion&&m.left>0f)return true;
            return false;
        }

        public void CheckExecuteMark(Enemy enemy)
        {
            if(executeMarks.Count==0||!enemy||!enemy.IsAlive||enemy.IsBoss)return;
            for(int i=0;i<executeMarks.Count;i++)
            {
                var m=executeMarks[i];
                if(m.enemy!=enemy)continue;

                if(m.spawnVersion!=enemy.SpawnVersion||m.left<=0f||Array.IndexOf(Hand,m.weapon)<0){executeMarks.RemoveAt(i);return;}
                if(enemy.maxHp<=0f||enemy.Hp/enemy.maxHp>=ExecuteThreshold(m.weapon))return;
                executeMarks.RemoveAt(i);
                ExecuteKill(m.weapon,enemy);
                return;
            }
        }

        void TickExecuteMarks(float dt)
        {
            for(int i=executeMarks.Count-1;i>=0;i--)
            {
                var m=executeMarks[i];m.left-=dt;
                if(m.left<=0f||!m.enemy||!m.enemy.IsAlive||m.enemy.SpawnVersion!=m.spawnVersion||Array.IndexOf(Hand,m.weapon)<0)executeMarks.RemoveAt(i);
                else executeMarks[i]=m;
            }
        }

        void FreezeExecuteMarks(bool freeze)
        {
            if(freeze){frozenExecuteMarks.Clear();frozenExecuteMarks.AddRange(executeMarks);executeMarks.Clear();}
            else{executeMarks.Clear();executeMarks.AddRange(frozenExecuteMarks);frozenExecuteMarks.Clear();}
        }

        void ReturnCurrent(RunWeapon w,int jumps)
        {
            if(jumps<=0)return;

            if(w.Has(UpgradeAxis.Tame)){w.chainStoredJumps=Mathf.Clamp(w.chainStoredJumps+jumps,0,Mathf.Max(0,upgrades.chainStoreMax));return;}
            ElementStatus.For(player)?.Add(ElementKind.Lightning,jumps,player);
            Fx.Ring(player.Position,upgrades.chainReturnRadius,ElementCombat.ColorFor(ElementKind.Lightning));
        }

        void ShatterFan(RunWeapon w,Vector3 aim,float range,float hit)
        {
            WeaponSound(w);
            float speed=Mathf.Max(1f,upgrades.shardSpeed);

            var shot=new HashSet<Combatant>();
            foreach(float angle in new[]{-upgrades.shatterSpread,0f,upgrades.shatterSpread})
            {
                Vector3 dir=Quaternion.Euler(0f,angle,0f)*aim;
                var a=AttackPool.Instance?AttackPool.Instance.Acquire():null;
                if(a){a.Configure(AttackMode.Shard,player,w,SpawnPoint(dir,.7f),dir,hit,0,range,speed,0,range/speed+3);a.sharedStruck=shot;}
            }
        }

        public void ShardStruck(AttackActor shard,Combatant body)
        {
            if(shard.weapon==null)return;
            Vector3 at=Query.Flat(body.Position);
            Shatter(shard.weapon,body,shard.damage,shard.direction);
            ShardLanded(shard,at);
        }

        public void ShardLanded(AttackActor shard,Vector3 end)
        {
            if(shard.weapon==null)return;
            Fx.Ring(end,.4f,ElementCombat.ColorFor(ElementKind.Frost));
            if(shard.weapon.Has(UpgradeAxis.Tame))
            {
                int count=0,oldest=-1;
                for(int i=0;i<hazards.Count;i++)
                    if(hazards[i].kind==GroundHazardKind.IcePatch&&hazards[i].weapon==shard.weapon){count++;if(oldest<0)oldest=i;}
                if(count>=Mathf.Max(1,upgrades.icePatchTamedCap)&&oldest>=0)hazards.RemoveAt(oldest);
            }
            AddHazard(GroundHazardKind.IcePatch,end,upgrades.iceRadius,upgrades.iceLife,shard.weapon);
        }

        void BlizzardSpray(RunWeapon w,float range,float hit)
        {
            float reach=Mathf.Min(range,upgrades.blizzardMaxRange*w.Reach);
            Fx.Cone(player.Position,player.AimDirection,reach,upgrades.blizzardConeAngle,ElementKind.Frost);
            bool any=false;
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||!InCone(body,reach,upgrades.blizzardConeAngle))continue;
                if(Physics.Linecast(Query.AtCastHeight(player.Position),Query.AtCastHeight(body.Position),GameManager.WallMask))continue;
                var status=ElementStatus.For(body);

                Damage(w,body,hit,0,player.AimDirection,continuous:true);
                if(status){ElementCombat.ApplyContinuousStacks(blizzardDose,body,ElementKind.Frost,upgrades.blizzardFrostPerTick,player);status.ExtendFreeze(upgrades.blizzardFrozenHold);}

                if(w.Has(UpgradeAxis.Twist)&&status&&status.Frozen&&!blizzardFrozenSince.ContainsKey(body))blizzardFrozenSince[body]=autoClock;
                any=true;
            }
            if(any)WeaponSound(w);
            if(!w.Has(UpgradeAxis.Twist)||blizzardFrozenSince.Count==0)return;
            elementPruned.Clear();
            foreach(var pair in blizzardFrozenSince){var status=pair.Key?ElementStatus.For(pair.Key):null;if(!status||!status.Frozen||!pair.Key.IsAlive)elementPruned.Add(pair.Key);}
            foreach(var body in elementPruned)blizzardFrozenSince.Remove(body);
        }

        void WatchPlayerHits()
        {
            if(watchedPlayer==player)return;
            if(watchedPlayer)watchedPlayer.AttackReceived-=OnPlayerAttacked;
            watchedPlayer=player;
            if(player)player.AttackReceived+=OnPlayerAttacked;
        }

        void OnPlayerAttacked(DamageInfo info)
        {
            var w=Active;
            if(w==null||w.form!=WeaponForm.Flamethrower||w.variant!=0||!player||!player.IsAlive)return;

            if(!inputHeld||w.heldTime<=0f||Time.time<w.tankReadyAt)return;
            w.tankReadyAt=Time.time+upgrades.fuelTankCooldown;
            WeaponSound(w,WeaponAudioPhase.Explosion);

            Area(w,player.Position,upgrades.fuelTankRadius,upgrades.fuelTankDamage*DamageScale(w),w.Has(UpgradeAxis.Tame)?upgrades.fuelTankTamePush:0,false);
            if(!w.Has(UpgradeAxis.Tame))ElementStatus.For(player)?.Add(ElementKind.Fire,upgrades.fuelTankSelfFire,player);
            if(V5Vfx.On){V5Vfx.I.Explosion(player.Position,upgrades.fuelTankRadius);return;}
            var fx=FxPool.Active;var art=V4CombatVfx.Active;
            if(fx&&art&&art.strikeFlash)fx.Emit(player.Position+Vector3.up*.8f,new Color(1f,.75f,.45f),upgrades.fuelTankRadius*1.8f,.3f,texture:art.strikeFlash,pulse:true);
        }

        void AddHazard(GroundHazardKind kind,Vector3 at,float radius,float life,RunWeapon w)
        {
            hazards.Add(new GroundHazard{kind=kind,position=new Vector3(at.x,0f,at.z),radius=radius,timeLeft=life,life=life,weapon=w});
        }

        void TrackSporeCloud(AttackActor actor,RunWeapon w)=>sporeClouds.Add(new TrackedCloud{actor=actor,sequence=actor.Sequence,weapon=w});

        public bool IsMainCloud(AttackActor actor){foreach(var cloud in sporeClouds)if(cloud.actor==actor&&cloud.sequence==actor.Sequence)return true;return false;}

        void TickSporeClouds()
        {
            for(int i=sporeClouds.Count-1;i>=0;i--)
            {
                var cloud=sporeClouds[i];
                bool live=cloud.actor&&cloud.actor.gameObject.activeSelf&&cloud.actor.Sequence==cloud.sequence;
                if(live)
                {
                    if(!cloud.actor.LobInFlight){cloud.landed=true;cloud.position=Query.Flat(cloud.actor.transform.position);cloud.radius=cloud.actor.radius;}
                    sporeClouds[i]=cloud;continue;
                }
                sporeClouds.RemoveAt(i);
                if(!cloud.landed)continue;
                for(int k=0;k<upgrades.sporePods;k++)
                {
                    Vector2 offset=UnityEngine.Random.insideUnitCircle*cloud.radius*.6f;
                    AddHazard(GroundHazardKind.SporePod,cloud.position+new Vector3(offset.x,0f,offset.y),upgrades.sporePodRadius,upgrades.sporePodLife,cloud.weapon);
                }
            }
        }

        void SporePodBurst(RunWeapon w,Vector3 at)
        {
            if(w==null||w.form!=WeaponForm.SporeLauncher||w.variant!=0||!w.Has(UpgradeAxis.Twist)||!player)return;
            for(int i=0;i<sporeClouds.Count;i++)
            {
                var cloud=sporeClouds[i].actor;
                if(sporeClouds[i].weapon!=w||!cloud||!cloud.gameObject.activeSelf||cloud.Sequence!=sporeClouds[i].sequence||cloud.LobInFlight||BattlefieldFrozen(cloud.gameObject))continue;
                if(Query.Flat(at-cloud.point).magnitude<=cloud.radius)DetonateCloud(w,cloud);
            }
        }

        void DetonateCloud(RunWeapon w,AttackActor cloud)
        {
            Vector3 centre=Query.Flat(cloud.point);
            float radius=cloud.radius*Mathf.Max(0f,upgrades.cloudBlastRadiusShare),hit=cloud.damage*Mathf.Max(0f,upgrades.cloudBlastTicks);

            cloudBlastTargets.Clear();
            foreach(var c in Combatant.All)
                if(c!=player&&c.IsAlive&&c.Team!=Team.Player&&Query.Flat(c.Position-centre).magnitude<=radius+c.radius&&!WallBlocks(centre,c.Position))cloudBlastTargets.Add(c);
            WeaponSound(w,WeaponAudioPhase.Explosion);
            foreach(var c in cloudBlastTargets)
            {
                Damage(w,c,hit,0,Query.Flat(c.Position-centre).normalized);
                var status=c.IsAlive?ElementStatus.For(c):null;
                if(status&&status.Rules)status.Add(ElementKind.Poison,status.Rules.poisonOnEnemyCap,player);
            }
            cloudBlastTargets.Clear();
            cloud.remaining=Mathf.Max(0f,upgrades.cloudSeconds);
            Fx.Ring(centre,radius,ElementCombat.ColorFor(ElementKind.Poison),.4f);
        }

        readonly List<Combatant> cloudBlastTargets = new();

        public void SporeLobLanded(AttackActor lob)
        {
            if(lob&&lob.weapon!=null&&!IsMainCloud(lob))SporePodBurst(lob.weapon,lob.point);
        }

        void TickHazards(float dt)
        {
            TickIceStones();
            WildfireTameStride();
            for(int i=hazards.Count-1;i>=0;i--)
            {
                var h=hazards[i];h.timeLeft-=dt;

                if(h.timeLeft<=0f){if(h.kind==GroundHazardKind.ScatterShell)IgniteShell(h);hazards.RemoveAt(i);continue;}
                hazards[i]=h;

                if(h.kind is GroundHazardKind.ScatterShell or GroundHazardKind.Snow)continue;
                if(h.kind==GroundHazardKind.IcePatch)
                {

                    if(h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame))
                    {
                        foreach(var body in Combatant.All)
                            if(body!=player&&body.IsAlive&&body.Team!=Team.Player&&Query.Flat(body.Position-h.position).magnitude<=h.radius+body.radius&&!WallBlocks(h.position,body.Position))
                                ElementStatus.For(body)?.Add(ElementKind.Frost,upgrades.iceFrostPerSecond*dt,player);
                    }
                    continue;
                }
                bool tame=h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame);

                if(tame&&player&&player.IsAlive&&Query.Flat(player.Position-h.position).magnitude<=h.radius+player.radius)
                {if(KickSporePod(h))hazards.RemoveAt(i);continue;}
                foreach(var body in Combatant.All)
                {
                    if(!body||!body.IsAlive||body==player&&tame)continue;
                    if(Query.Flat(body.Position-h.position).magnitude>h.radius+body.radius)continue;
                    var def=roster.Get(WeaponForm.SporeLauncher);
                    float podDamage=def!=null?def.damageA:1f;
                    var burst=Field(h.weapon,AttackMode.Field,h.position,podDamage*DamageScale(h.weapon),0,upgrades.sporeBurstRadius,upgrades.sporeBurstLife,0,!tame);
                    if(burst){burst.tickInterval=.5f;burst.ownerDamage=podDamage;}
                    hazards.RemoveAt(i);SporePodBurst(h.weapon,h.position);break;
                }
            }
        }

        bool KickSporePod(GroundHazard h)
        {
            var pool=AttackPool.Instance;if(!pool||!WorldMap.Active||h.weapon==null)return false;
            Vector3 dir=Query.Flat(h.position-player.Position);
            dir=dir.sqrMagnitude>.01f?dir.normalized:Query.Flat(player.AimDirection).normalized;
            Vector3 landing=WorldMap.Active.ClampPoint(h.position+dir*upgrades.sporeKickDistance,.2f);
            var def=roster.Get(WeaponForm.SporeLauncher);float podDamage=def!=null?def.damageA:1f;
            var pod=pool.Acquire();if(!pod)return false;
            pod.Configure(AttackMode.Lob,player,h.weapon,h.position,dir,podDamage*DamageScale(h.weapon),0,0,Mathf.Max(1f,LobSpeed),upgrades.sporeBurstRadius,float.PositiveInfinity);

            pod.tickInterval=.5f;pod.hurtOwner=false;pod.ownerDamage=podDamage;
            pod.StartArc(landing,1.5f,upgrades.sporeBurstLife);
            WeaponSound(h.weapon,WeaponAudioPhase.Impact);
            return true;
        }

        void WildfireTameStride()
        {
            if(!player)return;
            foreach(var w in Hand)
            {
                if(w==null||w.form!=WeaponForm.Flamethrower||w.variant!=1)continue;
                bool inside=false;
                if(w.Has(UpgradeAxis.Tame)&&player.IsAlive&&AttackPool.Instance)
                    foreach(var actor in AttackPool.Instance.actors)
                        if(actor&&actor.gameObject.activeSelf&&actor.weapon==w&&actor.mode==AttackMode.Field&&!BattlefieldFrozen(actor.gameObject)
                           &&Query.Flat(player.Position-actor.point).magnitude<=actor.radius){inside=true;break;}
                if(inside)player.AddMovementModifier(w,upgrades.wildfireTameSpeed);else player.RemoveMovementModifier(w);
            }
        }

        public void WildfireFence(AttackActor field)
        {
            var w=field.weapon;if(w==null||!player||!AttackPool.Instance)return;
            float interval=Mathf.Max(.01f,field.tickInterval);
            foreach(var other in AttackPool.Instance.actors)
            {
                if(!other||other==field||!other.gameObject.activeSelf||other.weapon!=w||other.mode!=AttackMode.Field||other.Sequence<=field.Sequence||BattlefieldFrozen(other.gameObject))continue;
                Vector3 a=Query.Flat(field.point),b=Query.Flat(other.point);
                if(Query.Flat(b-a).magnitude>upgrades.wildfireFenceRange||WallBlocks(a,b))continue;
                AddTracer(Query.AtCastHeight(a),Query.AtCastHeight(b),upgrades.wildfireFenceHalfWidth*2f,field.tickInterval+.05f,new Color(1f,.5f,.15f,.85f));
                elementTargets.Clear();
                foreach(var body in Combatant.All)
                {
                    if(!body.IsAlive||body==player&&(w.Has(UpgradeAxis.Tame)||!field.hurtOwner))continue;
                    if(Query.Flat(body.Position-ClosestOnSegment(body.Position,a,b)).magnitude>upgrades.wildfireFenceHalfWidth+body.radius)continue;

                    if(wildfireFenceHitAt.TryGetValue(body,out float ready)&&ready>autoClock)continue;
                    elementTargets.Add(body);
                }
                foreach(var body in elementTargets)
                {
                    wildfireFenceHitAt[body]=autoClock+interval*.9f;
                    if(body==player)ElementCombat.ApplyStacks(body,ElementKind.Fire,upgrades.wildfireFenceSelfFirePerSecond*interval,player);
                    else ElementStatus.For(body)?.Ignite(upgrades.wildfireFenceFirePerSecond*ElementCombat.WaveScale,player);
                }
            }
        }

        static Vector3 ClosestOnSegment(Vector3 p,Vector3 a,Vector3 b)
        {
            p=Query.Flat(p);a=Query.Flat(a);b=Query.Flat(b);
            Vector3 ab=b-a;float length=ab.sqrMagnitude;
            return length<1e-6f?a:a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/length);
        }

        float CapacitorMax=>Mathf.Max(1f,upgrades.capacitorMax);

        void TickCapacitor(RunWeapon w,float distance,float dt)
        {
            if(player.State==CharacterState.Normal&&distance>0)w.charge=Mathf.Min(CapacitorMax,w.charge+distance*(EnemyWithin(upgrades.capacitorNearRadius)?upgrades.capacitorChargeNear:upgrades.capacitorChargeFar));
            if(w.Has(UpgradeAxis.Twist))RecordCapacitorTrail(w);
            if(w.charge>=CapacitorMax)w.chargeFullTime+=dt;
            if(w.chargeFullTime>=4)
            {
                if(w.Has(UpgradeAxis.Tame)&&roster){var d=roster.Get(w.form);CapacitorDischarge(w,d.rangeB*w.Reach,d.damageB*DamageScale(w));}
                else{WeaponSound(w,WeaponAudioPhase.Explosion);Area(w,player.Position,5,upgrades.capacitorSelfBlast*DamageScale(w),0,true,upgrades.capacitorSelfBlast);w.charge=0;w.chargeFullTime=0;}
            }
        }

        void DrainStowedCapacitor(RunWeapon w,float dt)
        {
            w.charge=Mathf.Max(0f,w.charge-dt*CapacitorMax/Mathf.Max(.01f,upgrades.capacitorStowedDrainSeconds));
            w.chargeFullTime=Mathf.Max(0f,w.chargeFullTime-dt);
        }

        void CapacitorDischarge(RunWeapon w,float range,float damage)
        {
            float hit=damage*Mathf.Clamp01(w.charge/CapacitorMax)*(w.charge>=CapacitorMax?upgrades.capacitorFullBonus:1f);
            Vector3 at=player.Position;
            WeaponSound(w);
            elementTargets.Clear();upgradeHits.Clear();
            foreach(var c in Combatant.All)
                if(c!=player&&c.IsAlive&&Query.Flat(c.Position-at).magnitude<=range+c.radius&&!WallBlocks(at,c.Position)&&upgradeHits.Add(c))elementTargets.Add(c);
            if(V5Vfx.On)V5Vfx.I.Nova(at,range,V5Vfx.ElementTint(w.element));else Fx.Ring(at,range,ElementCombat.ColorFor(w.element));
            if(w.Has(UpgradeAxis.Twist)&&w.trailPoints!=null)
            {
                Color bolt=ElementCombat.ColorFor(ElementKind.Lightning);
                Vector3 previous=default;bool joined=false;
                for(int k=0;k<=w.trailCount;k++)
                {

                    Vector3 point;
                    if(k<w.trailCount)
                    {
                        int slot=(w.trailHead-w.trailCount+k+w.trailPoints.Length)%w.trailPoints.Length;
                        if(Time.time-w.trailTimes[slot]>upgrades.capacitorTrailSeconds){joined=false;continue;}
                        point=w.trailPoints[slot];
                    }
                    else point=Query.Flat(at);
                    if(joined&&Query.Flat(point-previous).magnitude<=upgrades.capacitorTrailBreak)
                    {
                        if(V5Vfx.On)V5Vfx.I.Bolt(Query.AtCastHeight(previous),Query.AtCastHeight(point),bolt,1.1f,.2f);
                        AddTracer(Query.AtCastHeight(previous),Query.AtCastHeight(point),.35f,.2f,bolt,V5Vfx.On);
                        foreach(var c in Combatant.All)
                        {
                            if(c==player||!c.IsAlive||upgradeHits.Contains(c))continue;
                            Vector3 closest=ClosestOnSegment(c.Position,previous,point);
                            if(Query.Flat(c.Position-closest).magnitude>upgrades.capacitorTrailWidth+c.radius||WallBlocks(closest,c.Position))continue;
                            upgradeHits.Add(c);elementTargets.Add(c);
                        }
                    }
                    previous=point;joined=true;
                }
                w.trailCount=0;
            }
            foreach(var c in elementTargets)Damage(w,c,hit,0,Query.Flat(c.Position-at).normalized);
            w.charge=0;w.chargeFullTime=0;
        }

        void RecordCapacitorTrail(RunWeapon w)
        {
            int size=Mathf.Clamp(Mathf.CeilToInt(upgrades.capacitorTrailSeconds/Mathf.Max(.01f,upgrades.capacitorTrailSample)),1,256);
            if(w.trailPoints==null||w.trailPoints.Length!=size){w.trailPoints=new Vector3[size];w.trailTimes=new float[size];w.trailCount=w.trailHead=0;}

            if(w.trailCount>0&&Time.time-w.trailTimes[(w.trailHead-1+size)%size]<upgrades.capacitorTrailSample)return;
            w.trailPoints[w.trailHead]=Query.Flat(player.Position);w.trailTimes[w.trailHead]=Time.time;
            w.trailHead=(w.trailHead+1)%size;w.trailCount=Mathf.Min(size,w.trailCount+1);
        }

        void ClearElementWeapons()
        {
            iceStones.Clear();wildfireFenceHitAt.Clear();needleHits.Clear();meltdownHasteLeft=0f;
            FreezeElementWeaponsForTrial();
            foreach(var w in Hand)if(w!=null)w.chainStoredJumps=0;
        }

        void FreezeElementWeaponsForTrial()
        {
            blizzardFrozenSince.Clear();
            foreach(var w in Hand)
            {
                if(w==null)continue;
                w.trailCount=w.trailHead=0;
            }
            ClearGround();
        }

        bool EnemyWithin(float radius)
        {
            foreach(var body in Combatant.All)
                if(body!=player&&body.IsAlive&&Query.Flat(body.Position-player.Position).magnitude<=radius+body.radius)return true;
            return false;
        }

        void Meltdown(RunWeapon w)
        {
            WeaponSound(w,WeaponAudioPhase.Explosion);
            float blast=upgrades.meltdownDamage*DamageScale(w),self=upgrades.meltdownDamage;
            Area(w,player.Position,4,blast,w.Has(UpgradeAxis.Tame)?upgrades.meltdownTamePush:0,!w.Has(UpgradeAxis.Tame),self);
            if(w.Has(UpgradeAxis.Tame)&&upgrades.meltdownTameHasteSeconds>0f)
            {
                meltdownHasteLeft=upgrades.meltdownTameHasteSeconds;SyncMeltdownMove();
                V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair,player.Position);
                Fx.Ring(player.Position,1.2f,new Color(1f,.8f,.35f,.9f));
            }
            w.heat=0;w.reload=3;
        }

        float meltdownHasteLeft;

        public float MeltdownHasteLeft=>meltdownHasteLeft;

        public float MeltdownHaste=>meltdownHasteLeft>0f?1f+Mathf.Max(0f,upgrades.meltdownTameHaste):1f;

        void TickMeltdownHaste(float dt){meltdownHasteLeft=Mathf.Max(0f,meltdownHasteLeft-dt);SyncMeltdownMove();}
        static readonly object MeltdownMoveKey=new();

        void SyncMeltdownMove()
        {
            if(!player)return;
            if(meltdownHasteLeft>0f&&upgrades.meltdownTameMoveBonus>0f)player.AddMovementModifier(MeltdownMoveKey,1f+upgrades.meltdownTameMoveBonus);
            else player.RemoveMovementModifier(MeltdownMoveKey);
        }

        void MeltdownWarning(RunWeapon w,float before)
        {
            float warn=4f*upgrades.meltdownWarnShare;

            if(upgrades.meltdownWarnShare>0f&&before<warn&&w.heat>=warn)WeaponSound(w,WeaponAudioPhase.ChargeStart);
        }

        public void MeltdownKillVent(RunWeapon w,Combatant body)
        {
            if(w==null||body is not Enemy{IsBoss:false} enemy)return;
            if(enemy.Elite)
            {
                w.heat=0f;
                V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair,player.Position);
                Fx.Ring(player.Position,1.2f,new Color(.55f,.85f,1f,.9f));
            }
            else w.heat=Mathf.Max(0f,w.heat-upgrades.meltdownKillVent);
        }

        public float LanceRecoil(RunWeapon w)=>Mathf.Max(0f,upgrades.lanceRecoil);

        public float FocusRecoil(RunWeapon w,float charge)=>Mathf.Lerp(upgrades.focusRecoilMin,upgrades.focusRecoilMax,Mathf.Clamp01(charge));

        bool LanceBraced(Vector3 aim,out Vector3 wallPoint)
        {
            aim=Query.Flat(aim).normalized;wallPoint=player.Position;
            const float lead=.3f;
            if(!Physics.SphereCast(Query.AtCastHeight(player.Position+aim*lead),player.radius*.9f,-aim,out var wall,upgrades.lanceBraceDistance+lead,GameManager.WallMask,QueryTriggerInteraction.Ignore))return false;
            wallPoint=wall.distance>0f?Query.Flat(wall.point):player.Position-aim*player.radius;
            return true;
        }

        public void LanceTrail(RunWeapon w,Vector3 from,Vector3 to)
        {
            if(w==null)return;
            RemoveLanceTrailTracer(w);
            w.lanceTrailFrom=Query.Flat(from);w.lanceTrailTo=Query.Flat(to);w.lanceTrailLeft=upgrades.lanceTrailSeconds;
            if(w.lanceTrailLeft>0f)AddTracer(LanceTrailPoint(w.lanceTrailFrom),LanceTrailPoint(w.lanceTrailTo),.12f,w.lanceTrailLeft,LanceTrailColor(1f));
        }

        static Vector3 LanceTrailPoint(Vector3 at)=>new Vector3(at.x,.08f,at.z);
        static Color LanceTrailColor(float share)=>new Color(.55f,.85f,1f,.25f+.55f*Mathf.Clamp01(share));
        void RemoveLanceTrailTracer(RunWeapon w)
        {
            Vector3 from=LanceTrailPoint(w.lanceTrailFrom),to=LanceTrailPoint(w.lanceTrailTo);
            for(int i=tracers.Count-1;i>=0;i--)if(tracers[i].from==from&&tracers[i].to==to)tracers.RemoveAt(i);
        }

        void TickLanceTrail(RunWeapon w,float dt)
        {
            if(w.lanceTrailLeft<=0f)return;
            w.lanceTrailLeft=Mathf.Max(0f,w.lanceTrailLeft-dt);
            if(w.lanceTrailLeft<=0f){RemoveLanceTrailTracer(w);return;}
            Vector3 from=LanceTrailPoint(w.lanceTrailFrom),to=LanceTrailPoint(w.lanceTrailTo);
            Color color=LanceTrailColor(w.lanceTrailLeft/Mathf.Max(.01f,upgrades.lanceTrailSeconds));
            for(int i=0;i<tracers.Count;i++)
            {
                if(tracers[i].from!=from||tracers[i].to!=to)continue;
                var line=tracers[i];line.until=Time.unscaledTime+.1f;line.color=color;tracers[i]=line;return;
            }

            AddTracer(from,to,.12f,.1f,color);
        }

        public void LanceCross(AttackActor slug,Vector3 at)
        {
            var w=slug?slug.weapon:null;if(w==null||!AttackPool.Instance||!player)return;
            var a=AttackPool.Instance.Acquire();if(!a)return;
            a.Configure(AttackMode.Blast,player,w,Query.Flat(at),slug.direction,HitDamage(w)*upgrades.lanceCrossShare,0,0,0,upgrades.lanceCrossRadius*w.Reach,.3f);
            a.ignoreShieldReduction=true;a.hurtOwner=false;
        }

        struct LanceBeam { public AttackActor slug; public int sequence; public Vector3 from, to; }
        readonly List<LanceBeam> lanceBeams = new();
        static readonly Color LanceBeamColor = new(.8f,.95f,1f,.95f);
        void StartLanceBeam(AttackActor slug)
        {
            if(!slug)return;
            Vector3 at=slug.transform.position;
            lanceBeams.Add(new LanceBeam{slug=slug,sequence=slug.Sequence,from=at,to=at});
        }

        void TickLanceBeams()
        {
            for(int i=lanceBeams.Count-1;i>=0;i--)
            {
                var beam=lanceBeams[i];
                bool same=beam.slug&&beam.slug.Sequence==beam.sequence,flying=same&&beam.slug.gameObject.activeSelf&&!BattlefieldFrozen(beam.slug.gameObject);

                if(same)beam.to=beam.slug.transform.position;
                SetLanceBeamTracer(beam,flying?Time.unscaledTime+.1f:Time.unscaledTime+Mathf.Max(0f,upgrades.lanceBeamSeconds));
                if(flying)lanceBeams[i]=beam;else lanceBeams.RemoveAt(i);
            }
        }

        public void LanceSlugStopped(AttackActor slug)
        {
            for(int i=lanceBeams.Count-1;i>=0;i--)
            {
                var beam=lanceBeams[i];
                if(beam.slug!=slug||beam.sequence!=slug.Sequence)continue;
                beam.to=slug.transform.position;
                SetLanceBeamTracer(beam,Time.unscaledTime+Mathf.Max(0f,upgrades.lanceBeamSeconds));
                lanceBeams.RemoveAt(i);return;
            }
        }

        void SetLanceBeamTracer(LanceBeam beam,float until)
        {
            for(int i=0;i<tracers.Count;i++)
            {
                if(tracers[i].from!=beam.from)continue;
                var line=tracers[i];line.to=beam.to;line.until=until;tracers[i]=line;return;
            }
            AddTracer(beam.from,beam.to,upgrades.lanceBeamWidth,until-Time.unscaledTime,LanceBeamColor);
        }

        void TickRailgunEffects(float dt)
        {
            TickLanceBeams();
            foreach(var w in Hand)if(w!=null)TickExecuteAura(w,dt);
        }

        public void BombardSelfTouch(RunWeapon w)
        {
            if(w==null||w.bombardRefunded)return;
            w.bombardRefunded=true;
            w.cooldown=Mathf.Max(0f,w.cooldown-upgrades.bombardTameCooldownCut*CycleSeconds(w));
            if(player)Fx.Ring(player.Position,.8f,new Color(1f,.75f,.35f,.9f));
        }

        void TickBombard(RunWeapon w,float dt)
        {
            if(!player||!player.IsAlive||!roster||!AttackPool.Instance){w.bombardLeft=0;return;}
            var d=roster.Get(w.form);if(d==null){w.bombardLeft=0;return;}
            int count=Mathf.Max(1,upgrades.bombardBlasts);
            float range=d.rangeB*w.Reach,start=upgrades.bombardStart,radius=2*w.Reach,spacing=count>1?Mathf.Max(0f,range-start)/(count-1):0f;
            w.bombardTimer-=dt;
            while(w.bombardLeft>0&&w.bombardTimer<=0f)
            {
                int k=count-w.bombardLeft;w.bombardLeft--;w.bombardTimer+=upgrades.bombardStep;
                Vector3 aim=Query.Flat(player.AimDirection);if(aim.sqrMagnitude<1e-4f)continue;aim.Normalize();
                float distance=start+k*spacing;
                if(Physics.Raycast(Query.AtCastHeight(w.bombardOrigin),aim,distance,GameManager.WallMask,QueryTriggerInteraction.Ignore))continue;
                Vector3 at=w.bombardOrigin+aim*distance;
                var blast=AttackPool.Instance.Acquire();if(!blast)continue;
                float fuse=upgrades.bombardFuse;
                blast.Configure(AttackMode.Blast,player,w,at,aim,d.damageB*DamageScale(w),5*KnockScale,0,0,radius,.2f+fuse+.1f);
                blast.delay=fuse;blast.hurtOwner=!w.Has(UpgradeAxis.Tame);blast.ownerDamage=d.damageB;
                Fx.Ring(at,radius,new Color(1f,.55f,.25f,.8f),Mathf.Max(.05f,fuse));
            }
        }

        void DeployTurret(RunWeapon w)
        {
            if(w==null||w.form!=WeaponForm.Minigun||w.variant!=0||!w.Has(UpgradeAxis.Twist)||!inputHeld||w.heldTime<upgrades.turretSpinSeconds||!player)return;
            Vector3 aim=Query.Flat(player.AimDirection);if(aim.sqrMagnitude<1e-4f)return;
            w.turretLeft=upgrades.turretSeconds;w.turretPoint=Query.Flat(player.Position);w.turretDir=aim.normalized;w.turretClock=0f;
            Fx.Ring(w.turretPoint,.9f,new Color(1f,.8f,.4f,.9f));
        }

        void TickTurret(RunWeapon w,float dt)
        {
            if(w.turretLeft<=0f)return;
            if(w==Active||!player||!player.IsAlive||!roster){EndTurret(w);return;}
            var d=roster.Get(w.form);if(d==null){EndTurret(w);return;}
            float interval=Mathf.Max(.01f,CooldownScale(w)/Mathf.Max(.1f,upgrades.spinUpTopRate));
            float before=w.turretLeft;
            w.turretClock-=dt;
            while(w.turretClock<=0f&&w.turretLeft>0f)
            {
                TurretShot(w,d.damageA*DamageScale(w),d.rangeA*w.Reach);
                w.turretClock+=interval;
            }
            w.turretLeft-=dt;

            if(Mathf.Floor(before*2f)!=Mathf.Floor(Mathf.Max(0f,w.turretLeft)*2f))Fx.Ring(w.turretPoint,.7f,new Color(1f,.8f,.4f,.7f));
            if(w.turretLeft<=0f)EndTurret(w);
        }

        void EndTurret(RunWeapon w){w.turretLeft=0f;w.turretClock=0f;w.heldTime=0f;}

        void TurretShot(RunWeapon w,float hit,float reach)
        {
            var a=AttackPool.Instance?AttackPool.Instance.Acquire():null;if(!a)return;
            Vector3 dir=w.turretDir,from=Query.AtCastHeight(w.turretPoint);float distance=.7f;
            if(Physics.Linecast(from,from+dir*distance,out var wall,GameManager.WallMask,QueryTriggerInteraction.Ignore))distance=Mathf.Max(0f,wall.distance-upgrades.spawnWallBackoff);
            a.Configure(AttackMode.Bolt,player,w,w.turretPoint+dir*distance,dir,hit,0,reach,40,0,reach/40+3);
            V5WeaponAudio.Weapon(w,WeaponAudioPhase.Fire);
            var pool=FxPool.Active;
            if(V5Vfx.On)V5Vfx.I.Muzzle(from+dir*.6f,dir,new Color(1f,.82f,.5f),.8f);
            else if(pool&&pool.muzzleFlash)pool.EmitDirectional(pool.muzzleFlash,from+dir*.6f,dir,.6f,.07f,new Color(1f,.88f,.6f));
        }

        public bool Anchored=>SpinBraceReady(Active)&&SpinWallBehind();
        bool SpinBraceReady(RunWeapon w)=>w!=null&&w.form==WeaponForm.Minigun&&w.variant==0&&w.Has(UpgradeAxis.Tame)&&inputHeld&&w.heldTime>=upgrades.spinAnchorSpinSeconds&&player&&player.IsAlive;

        bool SpinWallBehind()
        {
            Vector3 aim=Query.Flat(player.AimDirection);if(aim.sqrMagnitude<1e-4f)return false;aim.Normalize();
            const float lead=.3f;
            return Physics.SphereCast(Query.AtCastHeight(player.Position+aim*lead),player.radius*.9f,-aim,out _,upgrades.spinBraceWallGap+lead,GameManager.WallMask,QueryTriggerInteraction.Ignore);
        }
        float anchorRingClock;

        void TickAnchor(float dt)
        {
            var w=Active;
            bool spinning=w!=null&&w.form==WeaponForm.Minigun&&w.variant==0&&inputHeld&&w.heldTime>0f&&CanUseHand,braced=spinning&&Anchored;
            if(spinning&&!braced)player.Push(-player.AimDirection,Mathf.Max(0f,upgrades.spinUpRetreatSpeed)*Mathf.Clamp01(w.heldTime/3f)*dt);
            if(!braced){anchorRingClock=0f;return;}
            anchorRingClock-=dt;
            if(anchorRingClock<=0f){anchorRingClock=.3f;Fx.Ring(player.Position,.9f,new Color(1f,.85f,.45f,.7f));}
        }

        void AddTracer(Vector3 from,Vector3 to,float width,float seconds,Color color,bool drawnByV5=false)
        {
            if(seconds<=0f)return;
            if(tracers.Count>=32)tracers.RemoveAt(0);
            tracers.Add(new WeaponTracer{from=from,to=to,width=width,until=Time.unscaledTime+seconds,color=color,drawnByV5=drawnByV5});
        }

        void PruneTracers(){for(int i=tracers.Count-1;i>=0;i--)if(tracers[i].until<Time.unscaledTime)tracers.RemoveAt(i);}
    }
}
