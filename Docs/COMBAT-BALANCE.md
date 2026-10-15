# Combat balance

## Targets and assumptions

Use an ordinary same-stage build, including movement and attack downtime: fodder 0.5-0.8 seconds, elites 5-10 seconds, bosses 45-75 seconds. Strong builds may kill faster. Enemy HP depends on wave and role, never the player's current damage.

Reference effective DPS by wave is `7.5, 8.5, 10, 12, 14, 16, 18, 20, 23, 26, 28, 31, 34, 37, 40`. This is a modeling assumption rather than measured player telemetry. Main-weapon availability is estimated at 75 percent, with automatic-weapon coverage and skill damage included separately.

| Wave | Reference DPS | Fodder HP | Continuous-output TTK |
| --- | ---: | ---: | ---: |
| 1 | 7.5 | 5 | 0.667 s |
| 6 | 16 | 10 | 0.625 s |
| 10 | 26 | 17 | 0.654 s |
| 15 | 40 | 26 | 0.650 s |

`Fodder HP = round(reference DPS * 0.65)`, with a minimum of one. This replaces the earlier fixed 3 HP requirement. Fodder XP stays at 0.1 per kill.

## Shields and bosses

Direct damage has a 0.7 shield factor:

`Effective HP = HP + total shield budget / 0.7`

For a total shield share `s` relative to HP:

`HP = round(reference DPS * target seconds / (1 + s / 0.7))`

Elites use role-dependent centers of 6-8 seconds. Bosses use 60 seconds. At wave 15, Tyrant has 1680 HP and three shield budgets of 168 (initial plus two phase grants): `1680 + 504 / 0.7 = 2400`, or 60 seconds at 40 DPS. The unshielded Hollow has 2400 HP at that reference stage.

The discrete-hit model predicts 60.37 seconds for wave-15 Hollow and 58.29 for Tyrant. At 20 percent lower output efficiency, Hollow reaches 75.42 seconds, slightly beyond the requested upper bound. These estimates need ordinary-build playtesting.

## Weapons and XP

Human-rocket contact remains a uniform 3 damage without falloff. Its saved base cooldown is 1.0 seconds, yielding 0.5 after the initial cycle factor. Two hits kill the initial 5 HP fodder. Similar single-target TTK does not imply equal crowd-clearing output.

Controlled first-wave attack-to-death measurements: rifle A 0.541 s, throwing knife 0.611 s, rocket A 0.531 s, human rocket 0.639 s (single target at a fixed distance, not a full wave).

XP compensation keeps fodder at 0.1 and adjusts other rewards in proportion to the change in weighted effective HP. With fodder share `f`, other weighted base XP `V`, old reward multiplier `q`, and old/new weighted EHP `Ho/Hn`:

`old mean XP = f * 0.1 + V * q`

`new target mean XP = old mean XP * Hn / Ho`

`new multiplier = (new target mean XP - f * 0.1) / V`, clamped to 0.05-6.

The pure-fodder first wave cannot compensate slower kills through non-fodder rewards, so real level-up timing still needs playtesting.

## Source of truth

Player-side growth (stat cards, weapon Mk, element and skill scaling) follows these budgets; see [V4 numbers](V4-NUMBERS.md).

`Assets/AnomalyArena/Data/EncounterBalance.asset` and `Enemy.Configure` own enemy HP. `WaveManager.CalculateExperienceRewardScale` owns XP correction. The saved assets hold the current values.
