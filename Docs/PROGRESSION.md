# Progression, elements and skills

How the V2 progression, element and skill code is wired, based on GDD v2.0 draft 5 ([GDD.md](GDD.md)). Values marked as candidates are tuning starts, not measured balance. Current stat-card, element and skill numbers are in [V4-NUMBERS.md](V4-NUMBERS.md); scene and pool layout in [IMPLEMENTATION-NOTES.md](IMPLEMENTATION-NOTES.md).

## Components and data

The player has three components: `RunProgression`, `ElementStatus` and `SkillLoadout`. Every enemy and boss has `ElementStatus`. They are assigned in the editor by the scene setup, never during a fight.

`ProgressionDataSetup.CreateAssets()` (`Assets/AnomalyArena/Editor/ProgressionDataSetup.cs`) creates only the data assets that are missing; it never resets existing ones.

| Asset | What to tune |
| --- | --- |
| `Assets/AnomalyArena/Data/ProgressionRules.asset` | Base player stats and caps, XP costs, draft cost after MAX, Energy regeneration, rarity and stat-card values |
| `Assets/AnomalyArena/Data/StatusRules.asset` | Stack application, Burn and Poison ticks, control thresholds, Shield, fractions, player protections |
| `Assets/AnomalyArena/Data/SkillCatalog.asset` | References to the 16 skill assets |
| `Assets/AnomalyArena/Data/Skills/<SkillId>.asset` | Skill cost, cooldown, and per-level duration, radius, range, strength and text |
| `Assets/AnomalyArena/Data/Elements/<ElementKind>.asset` | Name, colour and optional icon for each element |

Stat cards: since V4 there are seven card types plus Element Penetration (offered only on boss-immunity waves). The fourteen `StatId` values still exist so saved data loads; see [V4-NUMBERS.md](V4-NUMBERS.md) §3.

## Run manager contract

`GameManagerV2` drives these systems.

**New run.** Call `progression.ResetRun()`, `skills.ResetRun()`, and `ResetForSpawn()` on each actor's status board where it applies. The player's board keeps `CarriesVoid` in sync with the inventory or permanent Void Energy.

**Ticking.** `RunProgression.Tick(playerDt)`, `ElementStatus.TickAll(worldDt, playerDt)` and `SkillLoadout.TickAll(playerDt)` run only while their part of the game is active; menus and the battlefield left behind during the trial do not tick. `GameManagerV2` owns time: `WorldDeltaTime` is scaled by Time Slip, `PlayerDeltaTime` is not. Player cooldowns and buffs use player time; ground spells use world time.

**Void trial.** On entry, `SkillLoadout.FreezeBattlefieldForTrial(true)` stores battlefield ground spells and their remaining time; trial casts use a separate list. On return, `FreezeBattlefieldForTrial(false)` drops the trial spells and restores the stored ones. `OrbPool.FreezeForTrial(true/false)` keeps its orbs and pickup amounts, and `OrbPool.Tick` uses the world clock exactly once. `RunWeapons.FreezeBattlefieldForTrial(true/false)` is called alongside (see Weapons below).

**Events.**

- `RunProgression.HealingRequested` calls `player.Heal`.
- `StatChanged` updates the player's max HP, move speed, protection and stamina from `progression.Stats`. Max HP is updated before the heal.
- Every real enemy death calls `GainDeathEnergy`, whatever the cause. Recycling a stuck actor or dissolving actors at wave end is not a death.
- Every damage death calls `SkillLoadout.NotifyEnemyDied` before the status board is cleared.
- An ordinary black-hole death does not spread Contagion and gives no XP.

**XP.** `GainXp(amount)` carries excess XP through every threshold. Each threshold banks a draft and refills unlocked Energy at once, without opening a menu. `NextDraftIsStatOnly` records whether that bar fill happened after level 10; it is not inferred from the player's later level.

## Drafts

- Start a build phase with `BeginBuildPhase(wave, weapons.RollUpgradeCard, () => skills.RollCard(wave))`. This gives one reroll for the whole build phase.
- After the Armory, `DrawCards(wave)` returns three `DraftCard`s. A missing Weapon or Skill card becomes a Stat card for a different stat. After MAX all three are Stat cards.
- `ApplyCard(index, replacementIndex)` returns false while the choice still needs a valid replacement, and uses up a banked draft only on success.
- `Reroll()` rerolls the displayed draft once.
- `ResolvePendingRandom()` resolves pending drafts only among cards that need no further decision (GDD §2.3).
- The weapon callback covers hand and auto weapons. `DraftCard.Payload` tells the UI which `SkillId` or weapon upgrade a card is about without deciding gameplay. Show `Title`, `Description`, `Kind`, `Rarity` and `NeedsReplacement`.
- For a weapon card the payload is a `WeaponUpgradeOffer`; its `Weapon`, `Axis` and `Element` let the HUD show the weapon and the new element before the card is applied.

**Weapon upgrade cards.** A card picks a revealed weapon that still has an eligible axis, then an axis it does not have. Hand weapons can get Infusion, Twist or Tame; auto weapons only Infusion. Re-infusion replaces the current element with a different one, with a 15% chance or whenever no other axis is left. Built-in elements never change. Damage growth comes from armory Mk tiers, not cards ([V4-NUMBERS.md](V4-NUMBERS.md) §2).

**Greater Void reward.** After the trial, call `CompleteVoidTrial()` and show `SkillLoadout.GreaterVoidCards()`. The chosen card applies directly and does not use an ordinary banked draft. A full skill loadout uses the same replacement prompt. If it is the first skill, `Learn` unlocks Energy at full. The reward then returns to the same wave ([DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) §5).

## Damage and statuses

- Dead targets, protection and dash immunity are rejected before any Shield is spent.
- `ElementStatus.ResolveDamage(amount, source, ignoreShield)` returns the HP damage after mitigation and Shield. Poison bypasses Shield, Burn is halved by it, and ordinary direct damage is cut by 30% while Shield is up. `ignoreShield` skips both the cut and the Shield points.
- `DamageTicked(amount, DamageSource, dealer)` enters the damage path as a tick: no protection window, knockback, hit stop or new element stacks.
- `Heal` calls `ElementStatus.OnHeal()` once per positive heal. Only the player's Poison is reduced by healing (GDD §4.8); enemy healing does not remove Poison, which the W12 walkthrough depends on.
- `ClearStatuses()` keeps Shield; it clears the four target statuses, control timers and temporary immunity timers.
- Player weapon hits use `ElementCombat.ApplyHit(target, element, baseDamage, actualDamage, source, builtInElement)`. Enemy affixes use `ApplyAffix(attacker, target, element, actualDamage)`, because enemy stacks and Light healing follow different rules. Never apply both to one hit.
- `BlocksMovement` and `BlocksActions` sit on top of `CharacterState`. A Frozen character can still be Knocked or Thrown. Enemy paralysis twitches; player paralysis blocks attacks and skills but allows movement.
- Bosses set `ImmuneToControl` and turn `OnControlThreshold` into a stagger. Large bodies set `ControlThresholdFactor = 2`; elites multiply their type's factor by 2.
- Frost Nova level 3 sets `FrozenKnockbackMultiplier = 1.5` only on targets this cast actually froze, not on targets already Frozen by something else. `Combatant` multiplies incoming knockback by it; it resets when the freeze ends.

## Skills

- `TryCast(0/1/2)`: one key press, one cast. A refused cast spends no Energy.
- `SkillWorldAPI` connects casts to the world: placement, the prepared wall, decoy and temporary-hole pools, swallowing and feed counters, nearest hole, projectile reflection, teleport and Time Slip. `VisualRequested` asks the existing visual pools for an effect; it does not create scene objects.
- Callback signatures: Time Slip `(scale, duration)`; Pull `(actor, worldPoint, distanceThisTick)`. A cast whose required world hook is missing, or made from a menu, is refused before Energy is spent. A valid point at the player's own position is accepted; replacing an invalid point is `WorldMap`'s job.
- `SkillSlot.Cooldown` reads the cooldown for the current level, including Devour's 6 s → 4 s at level 3.
- Devour targets only a non-elite, non-boss enemy below its HP threshold. A held target can be finished; the Hook then has no living target. Before swallowing, Devour notifies Contagion, because GDD §6.3 says this combination spreads Poison. Ordinary hole swallowing does not. Any positive fractional Poison counts for the transfer; there is no one-stack minimum.
- Ground effects stop exactly at their remaining duration. Storm Call's last update fires its remaining bolt even if floating-point accumulation falls slightly short of the deadline. Radiance cannot heal a dead caster.
- XP and HP orbs sweep their movement through the pickup radius and hole footprint, so a long frame cannot carry an orb past the player or a hole.
- The weapon code reads `WeaponCooldownRate` (Overcharge, hand-weapon cooldowns only; auto weapons are not sped up), `AttackSpeedMultiplier` (level-3 Time Slip) and `KnockbackMultiplier` (level-3 Overcharge).
- Card text comes from `SkillDefinition.Description(level)` using the asset's current fields. Storm Call hit damage and stacks, Gravity Well's end hit, Bulwark's end push, Radiance's instant heal and Devour's payout and feed radius are editable fields, not numbers inside the cast code.

## Weapons

`RunWeapons.cs` handles hand and auto weapons; `RunWeaponsAutomation.cs` holds the orbit and Whirl code; `RunWeaponsUpgrades.cs` holds the Twist/Tame behaviour.

- **Hook** uses a pooled `AttackActor` in Hook mode. `HandHookBusy` is true only during an active, unfrozen hook flight; the movement lock reads it. `HeldEnemy` hides a held target while the trial freezes it. Held and Whirl targets survive being deactivated during the trial without being treated as dead or moved. `AttackActor.Release` must restore a partly pulled Hook carrier if the actor is interrupted.
- **Trial freeze.** `RunWeapons.FreezeBattlefieldForTrial(true)` saves timers, charge and clip state, held and Whirl references, channel references, auto-weapon collision cooldowns, the selected hand slot and the current attack-actor set. Trial input uses separate references. `false` releases trial attacks and newly held bodies, restores the saved state, and resets the movement baseline so the trip between arenas cannot instantly fill Capacitor. The attack set is saved because `WorldMap` restores its objects before it raises the return event.
- **Railgun Lance** finishes its 0.5 s wind-up automatically and sets `pierceDamageRetention` to 0.75 on the pooled projectile.
- **Blizzard** uses one `channelledField` with `followCursor` and its range limit. Release, switching and control interruption end the current unfrozen channel.
- **Wildfire** replaces its oldest patch when a fourth is placed (an implementation choice for the GDD's maximum of three).
- **Fling** carries its element and Tame's 1.5 s landing stun through `ThrowImpact`. **Crash Helmet** uses `BounceFromWall(..., takeDamage: false)`.
- Negative knockback flips the direction and uses a positive distance. Time Slip's attack-speed multiplier enters weapon timing; Overcharge's knockback multiplier scales outgoing pushes.
- Wave cleanup releases actors before clearing busy and channel references.

## Open rules

- **Storm Call level 3.** No distinct effect is defined yet, so the skill stops at level 2. Options and the jump-cap question are in [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) §4.
- **Skill shapes.** The GDD gives no exact width or point range for several skills. Candidates: trail half-width 0.75 units (1.5 at level 3) and point range 12 units.
- **Tests.** The GDD's historical M0 test results describe earlier builds and do not cover this integration. Pending a full fifteen-wave run.

## Reading the code

Start with `RunProgression.GainXp`: XP is added, a loop spends thresholds, a draft is banked and Energy refills. Then `ElementStatus.Add` and `Tick`: a hit adds a fractional stack, and the clock turns stacks into periodic effects. Then one `SkillLoadout.Execute` branch such as Frost Nova: find nearby enemies, add Frost to each, and let the shared status board decide who freezes. Changing an asset value changes the behaviour without touching components or the scene.
