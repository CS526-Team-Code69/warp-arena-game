# V4 numbers: elements, stat cards, weapon Mk and skills

## Summary

Growth that used to feel like nothing now follows the fodder effective-HP (EHP) curve, and each system uses one formula family:

| System | Family | Shipped formula |
| --- | --- | --- |
| Fire | exponential (burn-out) | Every 0.5 s burn 20.6% of the current stacks (1.5 s half-life); each stack deals 1.5 × R(w) in total |
| Poison | additive (builds up, no decay) | Damage per second = stacks × 0.12 × R(w), cap 20 stacks |
| Frost | division (chill) | Speed × 1/(1 + 1.5 × stacks/freeze threshold); freeze for 1.5 s at the threshold |
| Lightning | multiplicative chain | Paralysis keeps enemies moving at ×0.55, multiplied with the Frost chill; each twitch interrupts a windup; chain jumps keep ×0.7 each |
| Light | additive | Lifesteal = damage × 5% × (1 + 2 × missing HP fraction) |
| Damage card | chain + additive crit | Damage × Π(1 + dᵢ); crit chance adds up, crits deal ×2, cap 60% |
| Attack Speed card | division | Cooldown = base ÷ (1 + Σsᵢ) |
| Protection card (armor) | division | Damage taken × 100/(100 + armor): every armor point adds the same share of effective HP |
| Weapon Mk | derived from EHP | M(k) = R(c)·h(c) / (h(1)·S(c)), c = centre wave of the tier window |
| Skill direct damage | exponential level growth | base × R(w) × 1.35^(level−1) × the player's damage multiplier |

R(w) is wave w's reference DPS divided by wave 1's: how much fodder EHP has grown since wave 1 (5.33 on wave 15).

**Evidence:** the formulas match the code and it compiles; the kill times and hit thresholds below come from a discrete paper model ([tools/v4_numbers.py](tools/v4_numbers.py)), not from playtests. The main open assumption is at the end.

## 1. Why upgrades used to feel empty

- Skill damage was flat: a Storm Call bolt dealt 6 while wave-15 fodder has 26 HP, so it took 5 bolts per kill. It now grows with R(w): 32 per bolt on wave 15, one bolt per kill.
- Element stacks came from raw damage and each burn tick was floored: a 4.5-damage hit gave 0.9 stacks, which floored to 0 damage. Stacks are now damage ÷ (5 × R(w)), and damage over time carries fractions in an accumulator instead of dropping them.
- +12% damage often missed the fodder threshold (5 HP against a 4.5 hit). Every Damage card now also gives 8% crit chance (×2), so a two-hit weapon gets that chance to kill in one hit on every swing.

## 2. Weapon Mk (pick one of three at the armory)

- Draft weapon cards only change behaviour and element: Twist, Tame and Infusion. Pure number growth moved to the armory Mk.
- The armory offers three distinct weapon forms. Slot 1 is always a new weapon; slots 2 and 3 each have a 50% chance to show a stronger Mk of a weapon you hold.
- New weapons arrive at the standard Mk for the wave, one tier every three waves (waves 1–3 Mk1, 4–6 Mk2, … 13–15 Mk5).
- An upgrade targets max(current + 1, standard + 1), up to Mk6, so a lagging weapon catches up to one tier above the wave in a single pick.

Derivation: in the reference build the held weapon's share of total DPS, h(w), falls linearly from 100% to 70% (auto weapons and skills supply the rest); the expected stat multiplier S(w) assumes, on average, two common Damage cards and one Attack Speed card by wave 15, spread evenly.

| Mk | From wave | Shipped | Recomputed with current card values |
| --- | ---: | ---: | ---: |
| Mk1 | 1 | ×1.00 | ×1.00 |
| Mk2 | 4 | ×1.40 | ×1.35 |
| Mk3 | 7 | ×1.65 | ×1.65 |
| Mk4 | 10 | ×1.90 | ×1.90 |
| Mk5 | 13 | ×2.10 | ×2.05 |
| Mk6 (upgrade only) | — | ×2.30 | ×2.25 |

The shipped table was computed with an earlier Damage card (+15%, 4% crit). With the final card (+12%, 8% crit) the recomputed table differs by at most 0.05 (about 3.7% at Mk2). The slightly higher shipped values stay so that early upgrades read clearly; a later tuning pass can switch to the recomputed column.

Paper check (recomputed table, standard Mk, reference cards, 75% attack uptime): fodder kill time stays between 0.66 and 0.75 s on every wave, inside the 0.5–0.8 s target. The shipped table is 0–4% faster.

## 3. Stat cards: fourteen types merged into seven, plus one conditional card

| Card | Common / Rare / Exceptional (Legendary ×1.2) | Also grants |
| --- | --- | --- |
| Max HP | +15 / +25 / +40 | +1 / +1.5 / +2.5 HP every 5 s |
| Move Speed | +8% / +12% / +16% | +0.2 / +0.3 / +0.45 s sprint |
| Damage (chain) | ×1.12 / ×1.18 / ×1.25 | +8% / +12% / +16% crit chance |
| Attack rate (division) | +15% / +22% / +32% | — |
| Knockback | +20% / +35% / +50% | +3 / +5 / +8 wall-slam damage (grows with the wave HP multiplier) |
| Protection (armor) | +25 / +40 / +60 | — |
| Max Energy | +25 / +40 / +60 | +0.4 / +0.7 / +1.0 Energy per kill |
| Element Penetration (boss immunity waves only) | +20% / +35% / +50% | — |

No longer offered on their own: Regeneration, Stamina, Impact, Pickup Range, Luck and Energy Siphon (most are folded in above). Enum values are unchanged, so saved data still loads.

What one common card does: Damage gives ×1.21 expected damage; Attack Speed gives cooldown ×0.87; 25 armor gives damage taken ×0.80 (effective HP +25%), and two cards give ×0.67 (effective HP +50%). Division keeps the effective-HP gain per armor point constant while the reduction percentage shrinks, so armor can never stack to immunity.

## 4. Elements

- **Fire:** steady burn ≈ weapon DPS × 1.5 ÷ 5 = 30% (built-in fire doubles stacks, about 60%); a stack lives 2.4 s on average. Mostly wasted on fodder that dies in 0.65 s; it pays off on elites, bosses and after you switch targets.
- **Poison:** after t seconds of hits it adds ≈ weapon DPS × 0.12 × t ÷ 5: +4.8% at 2 s, +19% at 8 s, +72% at 30 s (built-in poison ×2). At the 20-stack cap it deals 2.4 damage/s on wave 1 and 12.8 on wave 15. It is the boss killer: the longer the fight, the more it hurts.
- **Frost:** slows before it freezes: ×0.67 at 2/6 stacks, ×0.50 at 4/6, ×0.40 just before freezing; control-immune bosses take half the chill.
- **Lightning:** for the 4 s of paralysis, enemies are no longer rooted; they slow to ×0.55, multiplied with Frost (×0.31 with 3 Frost stacks), and a 0.3 s twitch every second interrupts any windup. Chain damage grows with the wave; jump n keeps 0.7^(n−1).
- **Light:** heals 5% at full HP, 10% at half HP, 13% at 20% HP.
- Element states on the player (from enemy affixes) keep the previous rules; enemy pressure on the player is unchanged in this pass.

## 5. Skills

| Skill | Wave 1 Lv1 / Lv3 | Wave 7 | Wave 15 |
| --- | --- | --- | --- |
| Storm Call per bolt | 6 / 10.9 | 14.4 / 26.2 | 32 / 58.3 |
| Gravity Well end | 10 / 18.2 | 24 / 43.7 | 53.3 / 97.2 |
| Singularity base | 10 / 18.2 | 24 / 43.7 | 53.3 / 97.2 |
| Singularity per absorbed enemy | 4 / 7.3 | 9.6 / 17.5 | 21.3 / 38.9 |

Values exclude Damage cards; Damage cards now boost skills too. Bulwark's Shield is now 20% / 30% / 30% of max HP. Element skills (Flame Wake, Frost Nova, Contagion) apply stacks, and their damage already grows through R(w).

## 6. Presentation and feel

- Impact without a global pause: only the struck enemy holds its pose for 0.045 s (heavy 0.085, crit 0.11), with a stronger flash, while the world keeps running. Directional sparks, a crit star and a ground ring on heavy hits; heavy hits and crits give a 0.1–0.16 camera kick, at most once every 0.12 s so crowds do not blur the screen.
- Visible states: flames, poison bubbles and mist, a frost shell or chill ring, and lightning arcs with a slow ring are drawn over enemies, and the model itself glows in the state's colour.
- Visible buffs: Overcharge, Radiance, Time Slip and Void Walk draw a ring at the player's feet; Shield shows a hexagon barrier.
- Models are enlarged visually only: player ×1.3, enemies ×1.35, bosses ×1.1. Colliders and hit radii are unchanged.
- Black holes: a stronger purple pull field, a pulsing warning ring on the pull border, chevrons spiralling inward, and a purple halo and bright rim around the core.

## 7. Most likely failure and the minimal check

Most likely wrong assumption: the reference build holds only two Damage cards and one Attack Speed card by wave 15. If players take more and also keep upgrading Mk, late fodder dies far faster than 0.5 s and elites and bosses fall too quickly under poison plus crits.

Minimal check: on waves 6, 10 and 15, record one ordinary run's average fodder kill time and the boss time. If fodder dies under 0.45 s or the boss under 40 s, switch to the recomputed Mk column and lower the Damage card to +10%; if fodder takes over 0.85 s, go the other way.

## 8. Reproduce and locate

- Model: `python Docs/tools/v4_numbers.py` prints every table above and writes `Docs/tools/v4_numbers.json`.
- Data: `Data/ProgressionRules.asset` (stat cards), `Data/StatusRules.asset` (elements), `V2/Data/WeaponRoster.asset` (Mk table), `Data/Skills/Bulwark.asset`.
- Authoring tool: `Editor/InstallV4CombatFeel.cs` (menu Anomaly Arena → V4 → Install Combat Feel; safe to run again).
