using UnityEngine;

namespace AnomalyArena
{
    [CreateAssetMenu(menuName = "Anomaly Arena/Status Rules")]
    public sealed class StatusRules : ScriptableObject
    {
        [Header("GDD §4.6 candidate parameters")]
        [Tooltip("Element stacks one player weapon hit adds, however much damage it deals (owner 2026-10-26: stacks count hits, so fast weapons stack faster).")]
        [Min(0f)] public float stacksPerHit = 1f;
        [Tooltip("A weapon's own built-in element adds this many times stacksPerHit; an infused element adds stacksPerHit.")]
        public float builtInElementStackFactor = 2f;
        [Tooltip("Continuous weapon sources (Flame Stream, Blizzard spray, fire patches, poison clouds and mists, the Leech ring) count as a hit for stacks at most once per this many seconds on each target; so does the Blizzard spray's own Frost dose. Their damage is not limited.")]
        [Min(0f)] public float continuousStackInterval = .5f;
        [Tooltip("fireInterval: seconds between burn ticks (player and enemies). fireCap: most Burning stacks on the player. fireShieldFactor: share of a burn tick a Shield lets through before absorbing it.")]
        public float fireInterval = 0.5f, fireCap = 20f, fireShieldFactor = 0.5f;
        [Tooltip("poisonInterval: seconds between Poison ticks (player and enemies). poisonCap and poisonDamagePerStack: the player's Poison only.")]
        public float poisonInterval = 1f, poisonCap = 30f, poisonDamagePerStack = 0.5f;
        [Tooltip("frostThreshold: Frost stacks that freeze an ordinary body (ControlThresholdFactor raises it for large bodies and elites). frostDuration: seconds a freeze lasts. "
            + "frostDecay and frostImmunity: the player only, Frost lost per second and seconds after a freeze in which no Frost sticks.")]
        public float frostThreshold = 6f, frostDuration = 1.5f, frostDecay = 1f, frostImmunity = 3f;
        [Tooltip("lightningThreshold: Lightning stacks that paralyse an ordinary body. lightningDuration: seconds a paralysis lasts. "
            + "lightningDecay and lightningImmunity: the player only, Lightning lost per second and seconds after a paralysis in which no Lightning sticks.")]
        public float lightningThreshold = 6f, lightningDuration = 4f, lightningDecay = 1f, lightningImmunity = 3f;
        [Tooltip("During a paralysis the body twitches every twitchInterval seconds for twitchLength seconds (no moving, no acting). Enemies below the threshold roll their Lightning twitch chance on the same rhythm.")]
        public float twitchInterval = 1f, twitchLength = 0.3f;
        public float lightningJumpRadius = 4f, lightningJumpDamage = 3f;
        [Tooltip("Jump radius when the origin carries Frost; plain Lightning also jumps (lightningJumpRadius).")] public float frostLightningJumpRadius = 6f;
        [Tooltip("Lightning stack multiplier on a target that carries Frost.")] public float frostLightningStackMultiplier = 1.5f;
        public int lightningJumpMax = 5;
        public float earthShare = 0.10f, lightShare = 0.05f, voidShare = 0.10f, lightAgainstVoid = 1.5f;
        public float shieldDirectFactor = 0.7f;
        public float playerShieldCapShare = 0.5f, shieldDecayDelay = 3f, shieldDecayPerSecond = 1f;
        public float largeThresholdFactor = 2f;
        public float playerControlMax = 1f, playerControlImmunity = 4f;
        public float enemyFireStacks = 3f, enemyPoisonStacks = 1f, enemyFrostStacks = 3f, enemyLightningStacks = 3f;
        public float enemyVoidDrain = 10f;
        [Tooltip("Share of each hit's damage a Light-affixed enemy heals back (design document: 15%).")] public float lightAffixLifesteal = .15f;

        [Header("Element rules on enemies (owner 2026-10-26; the player keeps the stack rules above)")]
        [Tooltip("Seconds an element status lasts on an enemy after its last hit of that element; every hit of the same element starts it again. A burn ends when it runs out; stacks then drain away.")]
        [Min(0f)] public float elementWindow = 3f;
        [Tooltip("Once an enemy's window has run out, its remaining Poison, Frost or Lightning stacks drain to zero within this many seconds (0 = at once).")]
        [Min(0f)] public float elementDrainSeconds = 1f;
        [Tooltip("After an enemy's freeze or paralysis ends, it cannot be frozen, paralysed or made to twitch for this many seconds. Its stacks still build up meanwhile; full stacks take effect as soon as this ends.")]
        [Min(0f)] public float controlImmunity = 2f;
        [Tooltip("Fire on an enemy: a Fire weapon hit sets it burning for elementWindow seconds at this share of the hit's damage per second (0.25 = 25% a second, 75% over 3 s). The heaviest hit while it burns sets the burn; each Fire hit starts the window again.")]
        [Min(0f)] public float fireBurnShare = .25f;
        [Min(0f), Tooltip("Owner 2026-10-27: a weapon's built-in Fire (the Flamethrower) burns fireBurnShare of the weapon's DPS times "
            + "this. 1.6 brings the Flamethrower Stream at Mk1 with no cards to about 3 a second (25% of its 7.5 DPS is 1.875).")]
        public float builtInFireBurnFactor = 1.6f;
        [Min(0f), Tooltip("Owner 2026-10-27: the Flamethrower Wildfire's built-in Fire uses this factor instead of builtInFireBurnFactor. "
            + "12 brings it at Mk1 with no cards to about 3 a second too (25% of its 1.0 DPS is 0.25), like the Stream.")]
        public float wildfireFireBurnFactor = 12f;
        [Tooltip("Fire stacks given to an enemy by a source with no burn rate of its own (ElementStatus.Add; Flame Wake, fire fences and Fire affixes set their own rates): each stack is worth this much burn damage over the burn's window (wave-1 value, × R(w)); 1.5 is what one Burning stack dealt before 2026-10-26.")]
        [Min(0f)] public float fireStackDamage = 1.5f;
        [Tooltip("Poison on an enemy: each stack deals this much per second (wave-1 value, × R(w)). Stacks stay while hits keep coming and drain once the window runs out.")]
        [Min(0f)] public float poisonOnEnemyDamagePerStack = 0.3f;
        [Min(.01f), Tooltip("Enemy (owner 2026-10-27): seconds between Poison ticks on an enemy. Each tick deals the stacks × "
            + "poisonOnEnemyDamagePerStack × this × R(w). The player's Poison keeps poisonInterval.")]
        public float poisonOnEnemyInterval = .5f;
        [Tooltip("Most Poison stacks an enemy can carry.")]
        [Min(1f)] public float poisonOnEnemyCap = 10f;
        [Tooltip("Frost on an enemy: each stack slows its movement and its attack timers by this share (0.1 = 10%), the same on any body; a large body or an elite only needs more stacks to freeze.")]
        [Min(0f)] public float frostSlowPerStack = .1f;
        [Tooltip("Most an enemy's Frost stacks can slow it (0.5 = half speed, reached at 5 stacks).")]
        [Range(0f, 1f)] public float frostSlowMax = .5f;
        [Tooltip("Lightning on an enemy below the paralysis threshold: every twitchInterval it twitches with this chance per stack (0.1 = 10% a stack), the same on any body. Not while it is immune to control.")]
        [Min(0f)] public float lightningTwitchChancePerStack = .1f;
        [Tooltip("Highest twitch chance Lightning stacks give below the paralysis threshold (0.5 = 50%, five stacks' worth), so a large body or an elite, which needs more stacks, is not stopped every interval before it is paralysed.")]
        [Range(0f, 1f)] public float lightningTwitchChanceMax = .5f;
        [Tooltip("Lightning on an enemy: player attacks deal this much more per stack (0.05 = +5% a stack), the same on any body.")]
        [Min(0f)] public float lightningVulnerabilityPerStack = .05f;
        [Tooltip("Most extra damage an enemy takes from its Lightning stacks (0.3 = +30%, six stacks' worth on an ordinary body).")]
        [Min(0f)] public float lightningVulnerabilityMax = .3f;
        [Tooltip("Extra damage a paralysed enemy takes from player attacks. The paralysis cleared its stacks; it counts as fully charged (0.3 = +30%; 0 = nothing extra while paralysed).")]
        [Min(0f)] public float lightningParalysedVulnerability = .3f;
        [Tooltip("Enemy speed and timer rate between paralysis twitches, chained with the Frost slow; during a twitch the body can neither move nor act.")]
        [Range(0.05f, 1f)] public float lightningSlow = 0.55f;
        [Tooltip("Lightning jump: the first jump is full, every further jump multiplies damage and stacks by this again.")]
        [Range(0f, 1f)] public float lightningChainFalloff = 0.7f;
        [Tooltip("Player Frost division slow: move and weapon speed × 1/(1 + this × stacks/freeze threshold); about ×0.4 just before freezing. Enemies use frostSlowPerStack.")]
        [Min(0f)] public float frostChill = 1.5f;

        [Header("Control vulnerability (both teams)")]
        [Tooltip("Damage multiplier on attacks against a frozen target (1.3 = +30%). Damage over time is not affected.")]
        [Min(1f)] public float frozenDamageTaken = 1.3f;
    }
}
