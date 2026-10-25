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
        [Min(0f), Tooltip("Wildfire Twist: Burning stacks per second an enemy keeps getting while it touches a fire fence (several fences count once). "
                          + "The fence deals no direct hit; Burning on an enemy burns one stack every half second, so above 2 the stacks build up.")]
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
        [Min(1), Tooltip("Shatter Tame: ice patches one weapon may keep at once; a new one removes the oldest.")]
        public int icePatchTamedCap = 6;
        public float blizzardConeAngle = 50f, blizzardMaxRange = 7f, blizzardFrostPerTick = .8f, blizzardFrozenHold = .6f;
        [Min(0f), Tooltip("Blizzard Twist: on release, damage per second each frozen enemy in the cone has been frozen under this spray (scaled like the weapon).")]
        public float blizzardShatterPerSecond = 3f;
        [Min(0f), Tooltip("Blizzard Twist: highest release damage per enemy before scaling.")]
        public float blizzardShatterCap = 9f;
        [Min(0f), Tooltip("Blizzard Twist: seconds the trigger must be held for the release to shatter anything.")]
        public float blizzardShatterMinHold = .5f;
        [Min(0f), Tooltip("Blizzard Tame: while you spray, enemies within this radius (plus their own) move at blizzardSelfSlow.")]
        public float blizzardAuraRadius = 2.5f;
        public float chainReturnRadius = 3f;
        [Min(0), Tooltip("Chain Tame: unused jumps stored for the next discharge, at most.")]
        public int chainStoreMax = 3;
        [Min(0)] public int sporePods = 3;
        [Min(0f), Tooltip("Cloud Twist: cells per second your landed cloud moves toward you while you stand inside it.")]
        public float cloudFollowSpeed = 4f;
        [Min(0f), Tooltip("Cloud Tame: a spore pod you step on is kicked this far away from you and bursts where it lands.")]
        public float sporeKickDistance = 5f;
        public float sporePodRadius = .7f, sporePodLife = 12f, sporeBurstRadius = 1.8f, sporeBurstLife = 3f;
        public float spinUpMoveFloor = .8f, spinUpFreeTurn = 1080f, spinUpFullTurn = 90f;
        [Range(0f, 1f)] public float leechHealShare = .5f;
        [Header("10-21 design alignment — candidate values")]
        [Range(0f, 1f), Tooltip("Charged Shot Twist: damage share of each of the two rounds (forward and backward) a release fires.")]
        public float chargedTwinShare = .7f;
        [Range(.05f, 1f), Tooltip("Charged Shot Tame: movement multiplier of non-boss enemies on your aim line while you charge.")]
        public float chargeLineSlow = .5f;
        [Min(0f), Tooltip("Charged Shot Tame: half width of that aim line in cells; an enemy's own radius is added.")]
        public float chargeLineHalfWidth = .5f;
        [Range(0f, 90f), Tooltip("Reverse Shot Twist: on its first wall bounce a round splits in two, this many degrees either side of the bounce.")]
        public float reverseSplitAngle = 15f;
        [Range(0f, 1f), Tooltip("Reverse Shot Twist: damage share each split round keeps of the round's damage.")]
        public float reverseSplitShare = .6f;
        [Min(0f), Tooltip("Reverse Shot Tame: an enemy behind you within this many cells (plus its radius) draws the round instead of the aim.")]
        public float reverseGuardRange = 3f;
        [Range(0f, 360f), Tooltip("Reverse Shot Tame: full width in degrees of the cone behind you that is guarded.")]
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
        [Range(0f, 1f), Tooltip("Scatter Blast backfire: chance that a reload jams. Tame never jams.")]
        public float scatterJamChance = .25f;
        [Min(0f), Tooltip("Scatter Blast backfire: extra repair seconds added to a jammed reload (scaled like other reloads).")]
        public float scatterJamRepair = 2f;
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
        [Range(0f, 1f), Tooltip("Scatter Blast Tame: a press of fire after this share of the reload finishes it at once and fires.")]
        public float scatterQuickStart = .6f;
        [Range(0f, 1f), Tooltip("Scatter Blast Tame: the quick-reload window ends at this share of the reload.")]
        public float scatterQuickEnd = .8f;
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
        [Min(0f), Tooltip("Scatter Focus backfire: recoil knockback in units on an uncharged release.")]
        public float focusRecoilMin = 2f;
        [Min(0f), Tooltip("Scatter Focus backfire: recoil knockback in units on a full charge. It is a real knockback (a wall behind you hurts); Tame makes it a harmless push.")]
        public float focusRecoilMax = 6f;
        [Range(0f, 1f), Tooltip("Scatter Focus Twist: damage share a bolt keeps after piercing its first target.")]
        public float focusPierceRetention = .6f;
        [Min(0f), Tooltip("Scatter Focus Tame: seconds you are immune to enemy attacks after an uncharged release (burn and poison ticks still land).")]
        public float focusGuardMin = .1f;
        [Min(0f), Tooltip("Scatter Focus Tame: seconds of that immunity after a full charge.")]
        public float focusGuardMax = .3f;
        [Range(.05f, 1f), Tooltip("Blizzard backfire: your movement multiplier while you spray. Tame puts it on the enemies around you instead.")]
        public float blizzardSelfSlow = .6f;
        [Min(1), Tooltip("Needler backfire: needles fired per HP cost. Tame gives Shield at the same rhythm instead.")]
        public int needlerHpPerNeedles = 8;
        [Min(0f), Tooltip("Needler Tame: Shield you gain every needlerHpPerNeedles needles instead of the HP cost.")]
        public float needlerTameShield = 1f;
        [Min(1), Tooltip("Needler Twist: the needle that makes this many hits on one enemy within needleBurstWindow bursts on it.")]
        public int needleBurstCount = 6;
        [Min(0f), Tooltip("Needler Twist: seconds the hit count lasts after the last needle on that enemy.")]
        public float needleBurstWindow = 2f;
        [Min(0f), Tooltip("Needler Twist: other enemies within this radius (plus their own) of the bursting enemy are hit; the enemy itself is not.")]
        public float needleBurstRadius = 2f;
        [Min(0f), Tooltip("Needler Twist: burst damage as a multiple of one needle.")]
        public float needleBurstShare = 2f;
        [Min(0f), Tooltip("Needler backfire: HP you lose each time; it ignores Shield.")]
        public float needlerHpCost = 1f;
        [Range(0f, 1f), Tooltip("Execute: starting HP share below which a non-boss enemy is executed.")]
        public float executeThreshold = .4f;
        [Min(0f), Tooltip("Execute: threshold gained for every kill made with this weapon.")]
        public float executeThresholdPerKill = .02f;
        [Range(0f, 1f), Tooltip("Execute: highest threshold the kills can reach.")]
        public float executeThresholdCap = .7f;
        [Min(0f), Tooltip("Execute backfire: max HP you lose per execution. Tame removes it.")]
        public float executeMaxHpLoss = 1f;
        [Min(0f), Tooltip("Execute backfire: max HP never drops below this.")]
        public float executeMaxHpFloor = 10f;
        [Min(0f), Tooltip("Execute Twist: seconds a hit enemy stays marked; any damage that leaves it under the execute line executes it.")]
        public float executeMarkSeconds = 6f;
        [Min(1), Tooltip("Execute Twist: marks at once; a new one replaces the oldest.")]
        public int executeMarkMax = 3;
        [Min(0f), Tooltip("Execute Tame: max HP gained (and healed) per execution.")]
        public float executeTameHpGain = 1f;
        [Min(0f), Tooltip("Execute Tame: most max HP one Execute can add in a run.")]
        public float executeTameHpCap = 20f;
        [Min(0f), Tooltip("Lance Twist: seconds a slug's path stays on the ground; the next slug that crosses it bursts at the crossing.")]
        public float lanceTrailSeconds = 3f;
        [Range(0f, 2f), Tooltip("Lance Twist: crossing blast damage as a share of one slug's base hit (Mk and cards included, the Tame brace not).")]
        public float lanceCrossShare = .6f;
        [Min(0f), Tooltip("Lance Twist: crossing blast radius, cells. It ignores Shield and spares you.")]
        public float lanceCrossRadius = 2.5f;
        [Min(0f), Tooltip("Lance Tame: a wall this close behind you (cells) braces the shot: no recoil and more damage.")]
        public float lanceBraceDistance = 4f;
        [Min(1f), Tooltip("Lance Tame: damage multiplier of a braced shot.")]
        public float lanceBraceDamage = 1.4f;
        [Range(0f, 180f), Tooltip("Barrage backfire: widest rocket angle from the aim in degrees (180 = straight back).")]
        public float barrageMaxAngle = 180f;
        [Min(.01f), Tooltip("Barrage backfire: angle = ±max × r^exponent with r uniform in [0,1]. Higher keeps more rockets near the aim (3: about 55% within 30°).")]
        public float barrageAngleExponent = 3f;
        [Min(0f), Tooltip("Barrage Tame: a rocket flying more than 90 degrees off the aim turns to the nearest visible enemy behind you within this range (cells).")]
        public float barrageRearRange = 8f;
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
        [Min(0f), Tooltip("Chain backfire: damage per unused jump to each enemy around you (scaled like the weapon).")]
        public float chainReturnDamagePerJump = 1f;
        [Min(0), Tooltip("Chain backfire: unused jumps counted for that damage at most. Your own Lightning still gains 1 per unused jump.")]
        public int chainReturnMaxJumps = 3;
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
        [Min(0), Tooltip("Throwing Knife Tame: extra bounce targets of the next throw after a kill return sweeps through you.")]
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
        [Range(0f, 1f), Tooltip("Spin-up Tame: knockback distance you take while firing at full spin, as a share.")]
        public float spinAnchorKnockFactor = .5f;
        [Min(0f), Tooltip("Spin-up Tame: seconds of holding fire before you are anchored (half knockback, no black-hole pull).")]
        public float spinAnchorSpinSeconds = 3f;
        [Min(0f), Tooltip("Scatter Blast: knockback distance of every hit, cells.")]
        public float scatterKnockback = 6f;
        [Min(0f), Tooltip("Ice patch backfire: Frost stacks a patch can build on you at most. Kept below the freeze threshold, so it only slows.")]
        public float iceFrostCap = 5f;
        [Min(0f), Tooltip("Execute: seconds the tracer line of every shot stays visible.")]
        public float executeTracerSeconds = .08f;
        [Min(.01f), Tooltip("Execute: tracer line width, cells.")]
        public float executeTracerWidth = .15f;
        [Min(0f), Tooltip("Gunner backfire: Energy every drone shot costs.")]
        public float gunnerEnergyCost = 2f;
        [Range(0f, 1f), Tooltip("Chance that a weapon with an element and another axis still open shows a change-element card instead.")]
        public float reinfuseChance = .15f;
        [Min(0f), Tooltip("Hook: gap kept between a held enemy and a wall in front of you, cells.")]
        public float hookWallGap = .05f;
    }

    public enum GroundHazardKind { IcePatch, SporePod }

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
        readonly HashSet<Combatant> blizzardAura = new();
        readonly Dictionary<(Combatant body, int version), (int count, float last)> needleHits = new();
        readonly List<Combatant> elementTargets = new();
        readonly List<Combatant> elementPruned = new();

        public bool BlizzardAuraActive { get; private set; }
        public float BlizzardAuraRadius => upgrades.blizzardAuraRadius;
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
                Fx.Cone(player.Position,player.AimDirection,range,60,w.element);
                foreach(var body in Combatant.All)
                {
                    if(body==player||!body.IsAlive||!InCone(body,range,60)||WallBlocks(player.Position,body.Position))continue;
                    Vector3 delta=Query.Flat(body.Position-player.Position);
                    float falloff=Mathf.Lerp(1f,upgrades.scatterFalloffMin,Mathf.Clamp01(delta.magnitude/Mathf.Max(.01f,range)));
                    Damage(w,body,hit*falloff,upgrades.scatterKnockback,delta.normalized);
                }
            }
            bool reloading=SpendRound(w,upgrades.scatterReload);
            if(reloading&&!w.Has(UpgradeAxis.Tame)&&UnityEngine.Random.value<upgrades.scatterJamChance)
            {w.reload+=upgrades.scatterJamRepair*CooldownScale(w);w.jammed=true;V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponJam,player.Position);}
            if(reloading){w.reloadTotal=w.reload;w.quickReloadTried=false;}
        }

        void Focus(RunWeapon w,Vector3 aim,float damage)
        {
            float charge=Mathf.Clamp01(w.charge);
            int pellets=Mathf.Max(1,upgrades.focusPellets);
            w.burstLeft=pellets;w.burstTimer=0f;w.burstDirection=aim;
            V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponFocusRelease,player.Position);
            w.burstDamage=damage/pellets*Mathf.Lerp(upgrades.focusMinChargeDamage,1f,charge);
            float recoil=Mathf.Lerp(upgrades.focusRecoilMin,upgrades.focusRecoilMax,charge);

            if(w.Has(UpgradeAxis.Tame)){player.Push(-aim,recoil);GuardFor(Mathf.Lerp(upgrades.focusGuardMin,upgrades.focusGuardMax,charge));}
            else player.Knockback(-aim,recoil,KnockKind.Hit);
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

        float BarrageAngle()
        {
            float r=UnityEngine.Random.value;
            return (UnityEngine.Random.value<.5f?-1f:1f)*upgrades.barrageMaxAngle*Mathf.Pow(r,Mathf.Max(.01f,upgrades.barrageAngleExponent));
        }

        Vector3 BarrageDirection(RunWeapon w,Vector3 aim)
        {
            float angle=BarrageAngle();Vector3 dir=Quaternion.Euler(0,angle,0)*aim;
            if(!w.Has(UpgradeAxis.Tame)||Mathf.Abs(angle)<=90f)return dir;
            Vector3 forward=Query.Flat(aim).normalized;
            var rear=NearestVisible(upgrades.barrageRearRange,c=>c is Enemy&&Vector3.Angle(forward,Query.Flat(c.Position-player.Position))>90f);
            return rear?Query.Flat(rear.Position-player.Position).normalized:dir;
        }

        void NeedlerCost(RunWeapon w)
        {
            if(!player||++w.needlesFired<Mathf.Max(1,upgrades.needlerHpPerNeedles))return;
            w.needlesFired=0;
            if(w.Has(UpgradeAxis.Tame)){ElementStatus.For(player)?.GainShield(upgrades.needlerTameShield);return;}
            player.ReceiveDamage(new DamageInfo{amount=upgrades.needlerHpCost,source=DamageSource.Burn,ignoreShield=true,dealer=player});                                                
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

        void BlizzardChill(RunWeapon w)
        {
            if(!player)return;
            bool spraying=w==Active&&w.heldTime>0f&&player.IsAlive;
            if(spraying&&!w.Has(UpgradeAxis.Tame))player.AddMovementModifier(w,upgrades.blizzardSelfSlow);
            else player.RemoveMovementModifier(w);
            BlizzardAuraActive=spraying&&w.Has(UpgradeAxis.Tame);
            elementPruned.Clear();
            foreach(var body in blizzardAura)
                if(!BlizzardAuraActive||!body||!body.IsAlive||Query.Flat(body.Position-player.Position).magnitude>upgrades.blizzardAuraRadius+body.radius||WallBlocks(player.Position,body.Position))
                    elementPruned.Add(body);
            foreach(var body in elementPruned){if(body)body.RemoveMovementModifier(w);blizzardAura.Remove(body);}
            if(!BlizzardAuraActive)return;
            foreach(var body in Combatant.All)
            {
                if(body==player||!body.IsAlive||body.Team==Team.Player||blizzardAura.Contains(body))continue;
                if(Query.Flat(body.Position-player.Position).magnitude>upgrades.blizzardAuraRadius+body.radius||WallBlocks(player.Position,body.Position))continue;
                body.AddMovementModifier(w,upgrades.blizzardSelfSlow);blizzardAura.Add(body);
            }
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
                upgradeHits.Add(body);Damage(w,body,hit,0,delta.normalized);
            }
        }

        void Shatter(RunWeapon w,Combatant first,float hit,Vector3 dir)
        {
            WeaponSound(w);
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

        public float ExecuteThreshold(RunWeapon w)=>w==null?upgrades.executeThreshold:Mathf.Min(upgrades.executeThresholdCap,upgrades.executeThreshold+w.executeBonus);
        void RaiseExecuteThreshold(RunWeapon w)=>w.executeBonus=Mathf.Min(w.executeBonus+upgrades.executeThresholdPerKill,Mathf.Max(0f,upgrades.executeThresholdCap-upgrades.executeThreshold));

        void Execute(RunWeapon w,float range,float hit)
        {
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
            if(!target)return;

            Unmark(target as Enemy);
            Damage(w,target,hit,0,player.AimDirection);

            if(target.IsAlive&&target is Enemy{IsBoss:false} low&&target.Hp/target.maxHp<ExecuteThreshold(w))ExecuteKill(w,low);
            else if(!target.IsAlive)RaiseExecuteThreshold(w);

            if(target.IsAlive&&w.Has(UpgradeAxis.Twist)&&target is Enemy{IsBoss:false} survivor)MarkForExecute(w,survivor);
        }

        void ExecuteKill(RunWeapon w,Enemy target)
        {
            Vector3 at=target.Position;
            target.Kill(player);
            if(target.IsAlive)return;
            if(progression)
            {
                if(!w.Has(UpgradeAxis.Tame))progression.AdjustMaxHp(-upgrades.executeMaxHpLoss,upgrades.executeMaxHpFloor);
                else
                {
                    float gain=Mathf.Min(upgrades.executeTameHpGain,upgrades.executeTameHpCap-w.executeHpGained);
                    if(gain>0f){progression.AdjustMaxHp(gain,0f);w.executeHpGained+=gain;}
                }
            }
            RaiseExecuteThreshold(w);
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
            float shock=upgrades.chainReturnDamagePerJump*Mathf.Min(jumps,Mathf.Max(0,upgrades.chainReturnMaxJumps))*DamageScale(w);
            if(shock>0f)foreach(var body in Combatant.All)
                if(body!=player&&body.IsAlive&&Query.Flat(body.Position-player.Position).magnitude<=upgrades.chainReturnRadius+body.radius&&!WallBlocks(player.Position,body.Position))
                    Damage(w,body,shock,0,Query.Flat(body.Position-player.Position).normalized,true);
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

                Damage(w,body,hit,0,player.AimDirection);
                if(status){status.Add(ElementKind.Frost,upgrades.blizzardFrostPerTick,player);status.ExtendFreeze(upgrades.blizzardFrozenHold);}

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

        void TickHazards(float dt)
        {
            TickIceStones();
            WildfireTameStride();
            for(int i=hazards.Count-1;i>=0;i--)
            {
                var h=hazards[i];h.timeLeft-=dt;
                if(h.timeLeft<=0f){hazards.RemoveAt(i);continue;}
                hazards[i]=h;
                if(h.kind==GroundHazardKind.IcePatch)
                {

                    if(h.weapon!=null&&h.weapon.Has(UpgradeAxis.Tame))
                    {
                        foreach(var body in Combatant.All)
                            if(body!=player&&body.IsAlive&&body.Team!=Team.Player&&Query.Flat(body.Position-h.position).magnitude<=h.radius+body.radius&&!WallBlocks(h.position,body.Position))
                                ElementStatus.For(body)?.Add(ElementKind.Frost,upgrades.iceFrostPerSecond*dt,player);
                    }
                    else if(player&&player.IsAlive&&Query.Flat(player.Position-h.position).magnitude<=h.radius+player.radius&&!WallBlocks(h.position,player.Position))
                    {

                        var status=ElementStatus.For(player);
                        float room=status?Mathf.Min(upgrades.iceFrostCap,status.FrostThreshold-.01f)-status.Frost:0f;
                        if(room>0f)status.Add(ElementKind.Frost,Mathf.Min(room,upgrades.iceFrostPerSecond*dt),player);
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
                    hazards.RemoveAt(i);break;
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
                    float perSecond=body==player?upgrades.wildfireFenceSelfFirePerSecond:upgrades.wildfireFenceFirePerSecond;
                    ElementCombat.ApplyStacks(body,ElementKind.Fire,perSecond*interval,player);
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

        void CapacitorDischarge(RunWeapon w,float range,float damage)
        {
            float hit=damage*Mathf.Clamp01(w.charge/CapacitorMax)*(w.charge>=CapacitorMax?upgrades.capacitorFullBonus:1f);
            Vector3 at=player.Position;
            WeaponSound(w);
            elementTargets.Clear();upgradeHits.Clear();
            foreach(var c in Combatant.All)
                if(c!=player&&c.IsAlive&&Query.Flat(c.Position-at).magnitude<=range+c.radius&&!WallBlocks(at,c.Position)&&upgradeHits.Add(c))elementTargets.Add(c);
            Fx.Ring(at,range,ElementCombat.ColorFor(w.element));
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
                        AddTracer(Query.AtCastHeight(previous),Query.AtCastHeight(point),.35f,.2f,bolt);
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
                foreach(var body in blizzardAura)if(body)body.RemoveMovementModifier(w);
            }
            blizzardAura.Clear();BlizzardAuraActive=false;
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
            Area(w,player.Position,4,blast,0,!w.Has(UpgradeAxis.Tame),self);
            if(w.Has(UpgradeAxis.Tame)&&upgrades.meltdownTameHasteSeconds>0f)
            {
                meltdownHasteLeft=upgrades.meltdownTameHasteSeconds;
                V3WorldAudio.Instance?.Play(ArenaWorldCue.WeaponRepair,player.Position);
                Fx.Ring(player.Position,1.2f,new Color(1f,.8f,.35f,.9f));
            }
            w.heat=0;w.reload=3;
        }

        float meltdownHasteLeft;

        public float MeltdownHasteLeft=>meltdownHasteLeft;

        public float MeltdownHaste=>meltdownHasteLeft>0f?1f+Mathf.Max(0f,upgrades.meltdownTameHaste):1f;

        void TickMeltdownHaste(float dt)=>meltdownHasteLeft=Mathf.Max(0f,meltdownHasteLeft-dt);

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
            a.ignoreShield=true;a.hurtOwner=false;
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
            V2Audio.Instance?.PlayWeapon(w.form,w.variant,WeaponAudioPhase.Fire);
            var pool=FxPool.Active;
            if(pool&&pool.muzzleFlash)pool.EmitDirectional(pool.muzzleFlash,from+dir*.6f,dir,.6f,.07f,new Color(1f,.88f,.6f));
        }

        public bool Anchored
        {
            get
            {
                var w=Active;
                return w!=null&&w.form==WeaponForm.Minigun&&w.variant==0&&w.Has(UpgradeAxis.Tame)&&inputHeld&&w.heldTime>=upgrades.spinAnchorSpinSeconds&&player&&player.IsAlive;
            }
        }
        float anchorRingClock;
        void TickAnchor(float dt)
        {
            if(!Anchored){anchorRingClock=0f;return;}
            anchorRingClock-=dt;
            if(anchorRingClock<=0f){anchorRingClock=.3f;Fx.Ring(player.Position,.9f,new Color(1f,.85f,.45f,.7f));}
        }

        void AddTracer(Vector3 from,Vector3 to,float width,float seconds,Color color)
        {
            if(seconds<=0f)return;
            if(tracers.Count>=32)tracers.RemoveAt(0);
            tracers.Add(new WeaponTracer{from=from,to=to,width=width,until=Time.unscaledTime+seconds,color=color});
        }

        void PruneTracers(){for(int i=tracers.Count-1;i>=0;i--)if(tracers[i].until<Time.unscaledTime)tracers.RemoveAt(i);}
    }
}
