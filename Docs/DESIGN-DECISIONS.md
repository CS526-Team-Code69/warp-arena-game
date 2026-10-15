# Design decisions for V2

This file records the V2 rules that the [GDD](GDD.md) (v2.0 draft 5) left open or ambiguous, and what the team chose. It was written on 2026-10-12. Numbers marked **candidate** are tuning starts, not measured balance. None of the rules below has been through a full fifteen-wave playtest yet.

Later number changes (stat cards, weapon Mk, element formulas) are in [V4-NUMBERS.md](V4-NUMBERS.md). Where V4 replaced a rule described here, the current rule is stated.

## 1. Open items and how each is handled

| Item | What the GDD says | Decision |
| --- | --- | --- |
| Swap's travelling dagger | §5.4: throw, then swap on a hit | Implemented as a travelling dagger. Speed, reach and edge cases in §2. |
| Recall's planted dagger and second use | §5.4: stays where it lands, does not return by itself; recall deals 8 damage with Medium knockback in radius 2 | Implemented as a planted pooled actor with a two-step state. Arc, landing and failure rules in §2.3. |
| Wildfire and Cloud lobs | §5.4: lobbed; patch size, ticks, damage and duration are written | Implemented as travelling lobs; written patch rules kept. Only flight parameters were chosen (§2.4). |
| Sticky, Bouncy, Hook, Rail, Blizzard lifecycle | §5.3–5.4 give the lifecycle and most numbers | Implementation work, no new design. |
| Tyrant and Hollow: HP, phase thresholds, control, stagger, telegraphs, summons, rewards | §8.6 and §9.7 | Implement as written. A telegraph never drops below 0.8 s, even when a phase speeds up actions. |
| Stat, skill and axis icons; Hollow art; phase looks; bespoke telegraphs | §10 and §15 | Art production; no gameplay decision needed. |
| Remaining sound bank and music | §15 names events and tracks | Audio production. |
| Collision, trial restoration, readability, frame time, pool saturation, WebGL performance | GDD §14/§16, [TECH-BACKLOG.md](TECH-BACKLOG.md) T0–T6, T17 | Verification work, not a design choice. |
| Public V2 web build | §16 | Build and delivery task. The older published build is not V2. |
| New Twist/Tame for the new anomalies | §5.8 defined only the original six | Approved on 2026-10-12 (GDD Q4). See §3. |
| Storm Call level 3 | §6.3 | Still open. See §4. |
| Q2: scale enemy damage with waves | §9.1 scales HP only | Enemy damage stays unscaled for now. |
| Q3: starting-stat picks / per-run modifiers | §17.3 | Out of scope. Optional expansion, not needed for a complete run. |

**Anomaly count.** §5.4 lists 11 new hand forms with 2 anomalies each: 22 new anomalies (8 tricks, 14 Element/Killing). Q4 originally said "fourteen"; the 22 in the roster are the correct count. The three auto forms have no Twist or Tame.

## 2. Delivery rules for Swap, Recall and lobbed patches

### 2.1 Shared rules

From the GDD: each weapon has its own cooldown and holstered cooldowns keep running (§5.2). A killed target dies before it is displaced (§4.1). Walls stop projectiles unless the weapon's own rule allows a ricochet (§4.3). A ground placement inside a wall, a hole or outside the map uses the nearest valid point toward the player, or is refused (§6.2, §8.8). Wave end clears weapon projectiles and transient objects (§2.2). The void trial freezes battlefield objects and resumes the same wave (§8.6).

Chosen for implementation: flight is a state on pooled actors. A full pool refuses the request; no object is created during combat. Knockback, statuses and deaths go through the shared `Combatant` / `ElementStatus` path. A visual arc does not let a dagger pass through a wall.

| Parameter | Candidate value | Note |
| --- | --- | --- |
| Swap / Recall reach | 12 units; ×1.4 with Reach | From `WeaponRoster`; the GDD row gives no base reach. |
| Dagger flight speed | 20 units/s | Not a GDD value. |
| Swap contact radius | 0.16-unit sphere | Matches the small-projectile query; not an art size. |
| Recall / lob arc | Peak +2 units above the start/end height; at least 0.2 s, otherwise distance ÷ speed | Tunable in the Inspector (`RunWeapons.RecallArcHeight`). Walls still block. |
| Lob speed | 12 units/s | Patch damage and lifetime start on landing. |
| Ground check | Capsule footprint for character destinations; wall/hole/boundary check for planted actors | Check the whole footprint, not only the centre. |
| Failed placement | No plant, no swap; feedback shown; no hidden relocation | The cooldown is still spent. |

### 2.2 Swap

GDD: the thrown dagger swaps the player with the target it hits. The price is arriving where the target stood; it can leave a target in a gravity zone (§5.4).

1. The dagger stops at the first living enemy or hard surface. A flat flight over a black hole is lost (§8.4).
2. It deals the roster's **5 base damage** through the shared hit path. Damage resolves first; a target that dies is not swapped.
3. Only a living, movable target can swap. Spire, Hollow and Tyrant cannot; they still take the hit.
4. Both destinations are checked with each body's own radius. If either is invalid, neither moves; the hit still counts.
5. A Hooked target does not swap. Knocked or Thrown targets can; on a swap, both bodies' displacement is cancelled, and HP, Shield and statuses are kept.
6. The two swapping bodies ignore each other during the check; walls, boundary and holes still count. A gravity zone is valid ground and its pull resumes after the swap.

Expected result: with both destinations valid, a surviving Grunt and the player swap exactly once; with either invalid, both stay put and the damage is unchanged. Not yet playtested.

### 2.3 Recall

GDD: the dagger stays where it lands; the second use moves the player there, deals **8 damage** and **Medium (5-unit) knockback within radius 2**. The dagger is unavailable until recalled. The intended trick is planting across a hole (§5.4).

1. The flight deals no damage; only the recall blast does.
2. The first click clamps the target point to reach. The dagger flies on a small arc, lands on valid ground and becomes one planted actor. It does not home.
3. **Crossing a hole:** Recall daggers and lobbed pods/fireballs are airborne during the arc, so a hole does not swallow them in flight (`AttackActor.LobInFlight`). The landing point is still checked and a hole there is refused. This is an exception to the general §8.4 projectile rule. Flat Swap, knife and bullet flights still fall into holes.
4. A wall hit by the actual arc stops the flight. If no legal plant results, the actor returns to the pool.
5. Once planted, the recall has **no distance limit** from the player's new position. Only the throw is range-limited.
6. At recall time the player's full footprint is checked again (a temporary wall may have appeared). If blocked, the recall is refused and the dagger stays planted.
7. A successful recall cancels displacement, moves the player, resolves one blast through the shared damage and element rules, and frees the plant. It does not cleanse statuses or grant invulnerability.
8. Wave end and weapon replacement free the plant. Entering the trial suspends a battlefield plant; trial plants are separate and are discarded on return, before the old plant is restored.

**Owner hit.** §5.7 says explosions hit their owner, but Recall's arrival blast is centred on the player and its stated price is the missing dagger. Applying §5.7 literally would make every recall deal 8 self-damage. Decision: the Recall blast excludes its owner; other explosions keep their own rules.

### 2.4 Wildfire and Cloud

| Property | Rule (from GDD §5.4, unchanged) |
| --- | --- |
| Wildfire | Up to 10 away; radius 2.5; 4 s; 1 damage four times/s; Fire built in; hurts its user; cooldown 1.5 s; at most 3 patches. |
| Cloud | Up to 10 away; radius 3.5; 5 s; 1 damage twice/s; Poison built in; hurts its user; cooldown 3 s; clouds overlap. |
| Reach | Scales only the anomaly's spatial values, never tick rate (§5.8). |
| Landing | Ticks and lifetime start on landing, not on the click. |

Flight and landing use the Recall arc rules. Chosen where the GDD is silent: a fourth Wildfire replaces the oldest patch; a full pool refuses another Cloud; an intercepted lob makes a patch only if the impact point is legal ground.

Ground fields, pulls and blasts are not treated as flying projectiles just because their centre is over a hole (`AttackActor.AffectedByHoles`). This keeps the explicit §5.4 trick of centring Attract on a black hole. §8.8's general rule (refuse placement on a hole) still applies to Recall and damaging patches.

## 3. Twist and Tame for the 22 new anomalies

Approved on 2026-10-12 (GDD §5.8 and Q4): 44 additions, one Twist and one Tame per new hand anomaly. Each keeps the original backfire at Mk I; Twist changes one behaviour and Tame softens the named price. Hits still follow kill-before-knock, protection, Shield and element rules; a secondary effect cannot create itself again; shared pools still cap active effects.

The values are tunable in `WeaponUpgradeTuning` (`Scripts/Weapons/V2/RunWeaponsUpgrades.cs`). Since V4, draft weapon cards offer only Twist, Tame or Infusion (auto weapons: Infusion only); damage growth comes from armory Mk tiers ([V4-NUMBERS.md](V4-NUMBERS.md) §2).

| Form · anomaly | Twist | Tame |
| --- | --- | --- |
| Grav Gun · Attract | When the 1.5 s pull ends, surviving enemies in the radius get a **3-unit outward knockback**; no extra damage. | The field no longer pulls its owner. Enemy pull stays 5 units/s. |
| Grav Gun · Repel | The full ring becomes a **180° forward arc** with the same reach and knock. | No 2-unit recoil on the owner. Orb repulsion stays. |
| Scatter Cannon · Blast | Hit enemies are pushed **toward the aim line** instead of straight out, same strength. Targets already on the line move forward. | Recoil stays 3 units but is a non-damaging push, so no wall slam. |
| Scatter Cannon · Suction | Releasing pushes captured survivors **5 units forward** once, no damage. | Pull stops when an enemy reaches a **2-unit gap** from the muzzle. |
| Phase Dagger · Swap | Pierces the first enemy and swaps with the **second** valid movable enemy; each enemy is hit at most once. No second target, no swap. | **0.5 s protection** after a swap. No cleanse; does not stop falling into a hole. |
| Phase Dagger · Recall | A successful recall also fires an identical blast at the **departure point**. A body takes at most one blast per recall. | If unused **6 s after landing**, the plant returns with no teleport or blast. |
| Plasma Launcher · Sticky | A second click after **0.3 s attached** detonates the blob early; normal expiry is still 2 s. | No ×1.5 movement modifier on the carrier; blast and self-danger stay. |
| Plasma Launcher · Bouncy | After the third wall bounce it **sticks to that wall** until its 3 s fuse ends; enemy contact still detonates it. | The blast no longer damages its owner; owner knockback stays. |
| Flamethrower · Stream | The cone reflects off the first wall **once** with its remaining range; an enemy takes at most one tick per emission. | Overheating still locks for 1.5 s but adds no Fire to the player. |
| Flamethrower · Wildfire | The last tick of an expiring patch gives survivors a **3-unit outward knockback**; no extra damage or duration. | Patches do not damage or ignite the owner. Max 3 and 4 s life stay. |
| Cryo Cannon · Shatter | Shattering a Frozen target lets another Frozen enemy in the burst shatter too; each body once, **at most 3 per shot**. | Wall bursts still put 3 Frost on enemies, none on the owner. |
| Cryo Cannon · Blizzard | On release the cloud stays at its last point for **2 s**; a new channel waits for it to end (still one cloud). | The cloud does not damage or freeze its owner; the ×0.7 move speed while held stays. |
| Arc Coil · Chain | After the first hit, each jump prefers the enemy with the **most Lightning**, then the nearest. Jump count and radius unchanged. | Missing jumps do not deal 3 damage to the owner. |
| Arc Coil · Capacitor | Displacement while **Knocked or Thrown** also charges, 1 per unit; walking and the cap of 30 unchanged. | At full charge for 4 s it leaks **1 charge/s** instead of self-exploding. A click still releases what is left. |
| Spore Launcher · Cloud | The cloud drifts **1 unit/s along the throw direction**, blocked by walls and boundary; still 5 s. | Own clouds do not damage or poison the owner. |
| Spore Launcher · Needler | Needles get **one wall ricochet**; no pierce, no extra damage. | Reloading no longer slows to 60%. Reload time and clip unchanged. |
| Railgun · Lance | The slug passes **one interior wall**, never the map boundary; damage still decays ×0.75 per target. | Recoil stays 4 units but is a non-damaging push. |
| Railgun · Execute | A kill sends one **half-damage follow-up** to the nearest enemy within 4. It cannot execute, add the kill bonus or chain. | A shot that fails to kill adds no 1.5 s jam; the 1.2 s cooldown stays. |
| Minigun · Spin-up | Spin is kept for **1 s after release**, then resets. Release still removes the move penalty. | At full spin the owner keeps **70% move speed** instead of 40%. |
| Minigun · Meltdown | At full heat the radial blast becomes a **90° forward cone**, same range, 20 damage. Owner's 20 self-damage and 3 s lock stay. | Full heat no longer damages the owner; enemy blast and lock stay. |
| Swarm Launcher · Barrage | A rocket stopped by a wall becomes a **1 s mine** that triggers on an enemy within 1.5; it cannot make another mine. | Rockets that hit nothing fade out instead of returning to the owner. |
| Swarm Launcher · Carpet | The six blasts go off in **reverse order, 12 → 2 units**, same 0.15 s spacing and damage. | Carpet blasts no longer damage the owner; knockback stays. |

Not yet playtested.

## 4. Storm Call level 3 (open)

When this was written, §4.6 let Lightning jump only to Frost carriers, and §6.3 said a level-3 bolt jumps "as if its target carried Frost", which changed nothing. Three options were drafted:

| Option | Rule | Trade-off |
| --- | --- | --- |
| A — one ungated hop | Each level-3 bolt sends one 3-damage, 3-Lightning hop to the nearest enemy within 4, Frost or not; shared visited set, at most 5 secondaries. | Visible benefit without Frost; longer chains still need Frost. |
| B — fully ungated chain | Every secondary within 4 is eligible regardless of Frost, at most 5. | Simplest, but removes the reason to pair with Frost Nova. |
| C — wider Frost chain | Keep Frost-only secondaries; skill chain radius 4 → 6; cap 5. | Smallest change; strengthens the combo rather than adding a new effect. |

Since then §4.6 changed: basic Lightning now jumps to any target within 4 (at most 5 additional targets, 3 jump damage), and Frost adds range 6 from a Frost-carrying origin and ×1.5 stacks on a Frost-carrying recipient. Options A and B are therefore already the base rule, and C overlaps the Frost range bonus. A new level-3 effect still has to be defined. Until then Storm Call stops at level 2 and no level-3 draft is offered (`SkillLoadout`).

Also open: `ElementCombat.JumpLightning` allows the primary target plus `lightningJumpMax` (default 5) secondaries. The GDD says "at most 5 additional targets"; whether a level-3 cap should count the primary has to be stated when level 3 is designed.

## 5. Void trial reward returns to the same wave

**Problem.** After a trial win, `GameManagerV2` opened the ordinary Draft phase with `greaterDraft` set. Space, Start Next Wave and Confirm Early Start had no guard for this and could call `BeginNextV2Wave`, which resolves banked drafts and advances the wave (or starts the final boss). GDD §2.1 and §8.6 require a return to the same wave, and the Greater skill reward may be declined.

**Fix.** One `FinishGreaterReward()` path (`Scripts/Game/GameManagerV2.cs`) clears the reward and modal state, leaves banked drafts and the Armory untouched, gives 2 s protection and returns to WaveActive. Accepting, replacing and declining all use it. On the reward screen, Start/Space means "decline and return", never the next wave. The UI snapshot carries `isGreaterReward`, hides build and reroll controls, and labels the exit "Return to current wave". Pending a play check.

## 6. Rules for changing this file

- The GDD's `[D]`, `[P]` and `[A]` tags keep their meaning; this file does not change them.
- Anything not in the GDD is marked as a candidate or as a team decision with its date.
- Record an accepted change here before changing weapon Mk, cards, damage or skill text.
