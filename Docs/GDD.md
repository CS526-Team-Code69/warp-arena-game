# Anomaly Weapon Arena — Game Design Document v2.0 (draft 5)

| | |
|---|---|
| Status | **Draft 5 for review**, 2026-10-11. Replaces draft 4 of the same day; what changed is listed as D52–D60 in §17.1. Revisions of 2026-10-12 and the V4 number pass of 2026-10-14 are folded into the sections they change. |
| Based on | 1.0.0, the first web prototype, plus the weapon changes made after it (§5.3) |
| Current numbers | Where this document and [`V4-NUMBERS.md`](V4-NUMBERS.md) differ, V4-NUMBERS and the data assets are current. Sections changed by V4 say so: stats (§3.3), elements (§4.6), Armory (§5.6), weapon upgrades (§5.8), drafts and stat cards (§7.2–§7.4). |
| Reference games | *Brotato* (bounded map, timed waves, build phase between waves), *Vampire Survivors* (auto weapons around the player) — with manual aiming and physics-driven combat |
| Companion document | [`TECH-BACKLOG.md`](TECH-BACKLOG.md): engineering and optimisation tasks that prepare the code for this design |

## How to read this document

Every rule carries one of these tags:

| Tag | Meaning |
|---|---|
| **[D##]** | **Decided** by the team. The number points to the decision log (§17.1). Implement as written. |
| **[1.0]** | Behaviour that already exists in the 1.0 code and is kept. Values with this tag were read from the code or the tuned assets on 2026-10-11. |
| **[P]** | **Proposed** default, chosen so the rules fit together. Change freely; the ones that most need a yes/no are listed in §17.2. |
| **[A]** | **Assumption** about how players will behave. It cannot be settled on paper; §14 says how to test it. |

- **Units.** 1 cell = 1 world unit (the same "tile" the 1.0 weapon texts use). Speeds are in units per second.
- **Numbers** without [D] or [1.0] are starting values for tuning, not balance claims.

## Contents

1. Overview · 2. Run flow · 3. Player · 4. Combat rules · 5. Weapons · 6. Skills and Energy · 7. XP, levels and cards · 8. World · 9. Enemies and waves · 10. UI · 11. Data schema · 12. Cross-system walkthroughs · 13. Tuning table · 14. Assumptions to test · 15. Art and audio needs · 16. Milestones · 17. Decision log and open items · 18. Impact on the 1.0 code

---

## 1. Overview

### 1.1 Pitch

A top-down survival arena on an alien battlefield. Your weapons look familiar but do the wrong thing, and the map is the best weapon you have: knock monsters into black holes, bounce them off walls, and feed a black hole until it opens a door to something worse.

### 1.2 Setting **[D11]**

- In the future, other species invade Earth.
- The **Interstellar Battlefield** lies outside the solar system. It is Earth's first battlefield and first line of defence against alien life.
- You are an explorer, the last survivor of your squad. Survive wave after wave of interstellar monsters, then return to Earth.
- A strange **interstellar energy** saturates the battlefield and distorts weapons: a weapon here looks like one thing and works like something else entirely.

How the systems sit in this fiction **[P]** (flavour only, no rules attached):

| System | In the fiction |
|---|---|
| Wave timer | Time until the next supply pod from Earth lands. After wave 15 it is the evacuation ship — and the Tyrant, who comes to stop it (§9.7). |
| Armory (§5.6) | The supply pod. Its contents were distorted on the way, so you cannot tell what a weapon does until you use it. |
| Level-up, skills, Energy | The explorer attunes to the interstellar energy and learns to channel it. |
| Black holes, the void, Void Energy | A second, deeper force under the battlefield. Feed it and it opens. Beat what lives in it and it feeds you. |

Story text is limited to three places: one card on the loadout screen, one line at the start of waves 6 and 11, and the ending card. **[P]**

### 1.3 Target experience, and the rules that are meant to produce it

| The player should feel | What the player does to get there | Rules behind it |
|---|---|---|
| Surprise, then mastery: *"it does WHAT? … oh, I can use that"* | Fires an unknown weapon in a live fight, gets burned by its backfire, then repositions so the backfire becomes a trick | Hidden anomaly, reveal on first use, a backfire on every anomaly, no use limit so the trick can be practised (§5.1) |
| *"The map did the killing"* | Gets enemies between themself and a black hole or a wall, then picks the attack with the right knockback | Knockback by attack type (§4.2), wall slam and rebound (§4.3), black holes and gravity (§8.4), enemies that avoid holes on their own (§9.2) |
| *"My build came together"* | Chooses which weapon to keep, which upgrade to take with a level, which element to put on which weapon, which three skills to carry | Armory (§5.6), three-card draft (§7.2), Mk tiers and draft upgrades (§5.8), elements (§4.6), skill slots and Energy (§6) |
| *"Do I risk it?"* | Decides how to place living enemies and damage-killed bodies near a hole, and when to enter the trial | Living enemies swallowed directly give no XP; a damage-killed corpse may subsequently feed one fixed hole without a second reward (§4.1, §8.4). Per-hole counter, a door that stays open for three waves after 40 feeds, and the trial ends the run if lost. [Revision 2026-10-12] |

### 1.4 Pillars

1. **Anomalous weapons.** Familiar form, hidden function, a backfire on every one. **[D01]**
2. **The terrain is a weapon — for both sides.** Anything that can kill an enemy can kill you. **[D06] [D07]**
3. **A build that grows.** Levels buy cards; weapons and skills both get stronger. **[D09] [D15]**
4. **A hidden door.** Feeding the void is optional, costly and dangerous, and it changes the rest of the run. **[D08] [D19]**

### 1.5 Design tensions and how each is handled

These pairs of goals pull against each other. None can be "solved"; each row says which trade was chosen.

| # | Goal A | Goal B | Handling |
|---|---|---|---|
| T1 | Weapon effects are a surprise | The player can plan a build | The unknown lasts only until the first use. A form has exactly two anomalies and the Armory shows the form, so a blind pick is a known 50/50. Upgrades are only offered for revealed weapons. |
| T2 | Black holes stay useful as terrain | Feeding a hole closes it | Two holes with separate counters **[D57]**. The one that is fed becomes a door for three waves and is then gone, used or not. The other stays as terrain — unless it is fed to 40 as well. A player who wants terrain to the end feeds one of them at most. |
| T3 | Manual aim is where the skill is | Auto weapons play by themselves | Separate slots **[D22]**, capped at 3. Auto weapons are built for control and support, and each has its own backfire. Target: under 30 % of the damage dealt in a run **[A]**. |
| T4 | Energy is scarce: it refills only when the XP bar fills **[D16]**, and there are only 9 levels **[D36]** | Skills should get pressed, not hoarded | A refill *sets* Energy to max, so anything unspent at that moment is wasted. The XP bar shows how close the refill is: spend before it fills. The scarcity late in a run is deliberate: it is what makes leech and the void trial worth going for (§6.5). |
| T5 | Cards are picked at the end of the wave, so fights are not interrupted **[D27]** | A level-up should be felt at once | The level-up itself is immediate: Energy refill, banner, a pip on the XP bar. The card comes in the build phase. |
| T6 | Enemies never run out: the more you kill, the more you grow **[D41]** | Levels stop at 10 **[D36]** | Each level costs 65 % more XP than the one before, so a player who kills half as fast again reaches level 10 only two waves sooner (§7.5). Past level 10 every further fill of the bar is a Stat card **[D40]**: growth continues, but in numbers only. |
| T7 | The build phase has a clock **[D38]** | Choices should be considered | The clock grows with the run (20 s to 55 s, including the build phase after wave 15), running out never loses a card — one is picked at random — and the player can always start the next wave early. |
| T8 | Seven elements, twenty-eight anomalies, eleven enemy types, thirteen skills **[D45] [D46] [D58]** | A player can learn the game inside one run | Each element does one thing and fits in one sentence; there are exactly four rules between elements (§4.6). Each enemy has one job (§9.1). Every weapon grows the same way: Mk tiers from the Armory and three kinds of draft upgrade (§5.8). Affixes start in wave 5, and a wave announces the two it uses (§9.5). |

### 1.6 Not in v2.0

- No currency, shop or item system. **[D21]**
- No progress carried between runs, except a codex of anomalies already seen. **[P]**
- No shield weapon form. **[D30]** (The Shield of §4.7 is a resource, not a weapon.)
- No procedural map generation beyond choosing black-hole positions from hand-placed anchors. **[P]**
- No saving or resuming a run; no gamepad; no multiplayer. **[P]**

### 1.7 What changes from 1.0

| 1.0 | v2.0 |
|---|---|
| Irregular arena of about 32 × 30, fully on screen, fixed camera | Map of 100 × 70 with a boundary; the camera follows the player **[D05]** |
| Gaps in the outer wall; falling out kills | The boundary never kills **[D05]**; black holes inside the map kill **[D06]** |
| Weapons lie on the ground, have limited uses, disappear when empty | Weapons come from the Armory, are kept, have no use limit **[D01] [D21]** |
| One weapon in hand | 2 hand slots **[D04]**, switched with the right mouse button **[D44]**, + 3 auto slots **[D22]** |
| 3 waves, each cleared by killing everything | 15 timed waves of endless enemies; 10 min 45 s of fighting, plus a timed build phase after each wave, and then a final boss **[D03] [D23] [D38] [D41] [D56]** |
| No growth inside a run | XP and 10 levels, three-card draft, weapon upgrades, skills, Energy **[D09] [D13] [D15] [D36]** |
| No elements, statuses or shields | Seven elements, Shield, affixed enemies **[D45] [D50]** |
| 2 enemy types, 6 anomalies | 11 enemy types with one job each, 28 anomalies in three families, Mk tiers and three kinds of draft upgrade, 13 skills **[D46] [D51] [D58]** |
| Missile · Homing: a rocket that picks its own target | Rocket Launcher · Strike: a rocket that lands where you pointed, 1 s later — **already implemented** **[D52]** |
| Throwing Knife 10 damage; sprint 1 s, recovery 3 s | Throwing Knife 5 damage **[D59]**; sprint 0.5 s, recovery 1.5 s **[D60]** — **already implemented** |
| Gun · Charge Swing | Rifle · Charged Shot — **already implemented** **[D34]** |
| Hook pulls an enemy in as a shield and slows you down; a second click throws it | Still pulled in front and flung by a second click **[D43]**, but at full move speed **[D33]**; the fling is now an attack: a crash damages everything around it — **already implemented** **[D35]** |
| Manual aim, left button to attack, Shift to sprint | Unchanged **[D02] [1.0]** |

---

## 2. Run flow

**Intent.** A run alternates between a fight that is never interrupted and a short build phase on a clock, where every decision is made with the battlefield frozen.

### 2.1 States

```
 Title ─START─▶ Loadout ─pick 1 of 3─▶ WaveIntro ─3 s─▶ WaveActive ─timer 0 + remaining enemies cleared─▶ WaveEnd ─▶ BuildPhase
                                           ▲               │   ▲                           │
                                           │        E on   │   │ boss dies                 │
                                           │        portal ▼   │                           │
                                           │             VoidTrial                         │
                                           └──────────────────── next wave, 2 to 15 ◀──────┤
                                                                                           │ after wave 15
                                                                                           ▼
                                                    Result (victory) ◀── Tyrant dies ── FinalBoss

 WaveActive / VoidTrial / FinalBoss ─ player dies ─▶ Result (defeat)
```

| From | Trigger | To | What happens |
|---|---|---|---|
| Title | START or Space | Loadout | Whitebox / illustrated toggle stays on the title screen **[1.0]** |
| Loadout | A starting weapon is picked **[D20]** | WaveIntro, wave 1 | Player at the map centre, full HP, second hand slot empty |
| WaveIntro | 3 s countdown ends | WaveActive | The player can move during the countdown. Black holes and gravity are live. No enemies. |
| WaveActive | Wave timer reaches 0 AND all remaining living enemies are cleared | WaveEnd | At timer 0, stop spawning but continue combat until the survivors are cleared (§2.2). Applies after wave 15 as well. [Revision 2026-10-12] |
| BuildPhase, after wave 15 | The same three triggers as any build phase | FinalBoss | The evacuation ship is in sight, and so is the Tyrant. An open portal closes (§8.5). No timer runs (§9.7). |
| FinalBoss | The Tyrant's HP ≤ 0 | Result, victory | The ship leaves with the player on it |
| FinalBoss | Player HP ≤ 0, or the player's centre is inside a black hole | Result, defeat | |
| WaveActive | Player HP ≤ 0, or the player's centre is inside a black hole | Result, defeat | |
| WaveActive | E pressed while standing on the portal cell | VoidTrial | The battlefield freezes: wave timer, enemies, projectiles, orbs **[P]** |
| VoidTrial | Boss HP ≤ 0 | WaveActive, same wave continues | Rewards (§8.6); 2 s of protection on return |
| VoidTrial | Player HP ≤ 0 or falls off the platform | Result, defeat **[D26]** | |
| WaveEnd | Cleanup done (0.8 s real time) | BuildPhase | The battlefield is frozen; the build clock starts |
| BuildPhase, after waves 1 to 14 | Everything is picked, **or** the player presses Start next wave, **or** the build clock runs out **[D38]** | WaveIntro, next wave | Anything still unpicked is resolved automatically (§2.3). HP restored to max **[1.0]** |
| WaveIntro / WaveActive / VoidTrial / FinalBoss | Esc | Paused overlay | Resume / Restart / Quit to title **[P]** |
| Result | R | Loadout | New run |

### 2.2 What happens when a wave timer reaches 0

**Revision, 2026-10-12:** while time remains, clearing the current batch immediately schedules the next batch around the player. At timer 0, stop new spawns and cancel unhatched markers, but keep combat active until all remaining living enemies have been cleared. The player is still vulnerable during this cleanup combat. Existing duration numbers are unchanged.

A batch is a refill inside the same numbered wave, not a new wave or level: it does not advance WaveIndex, enemy scaling, Armory, build phases, full-HP restores or portal wave countdowns. Ordinary kill rewards and XP growth remain in their existing paths. Dead corpses do not count as living survivors and are cleaned with the other transients after combat finishes.

Once timer 0 and no living enemy remains, perform the following cleanup in order. **[P]** for existing cleanup values.

1. The spawner stops. Spawn markers that have not hatched are removed.
2. The player becomes *safe*: knock velocity is cleared, and the player cannot be damaged, pulled or swallowed until the build phase opens. Every status on the player is cleared **[D50]**; Shield is kept and does not decay until the next wave starts.
3. There are no remaining living enemies to dissolve. Recycle any retained corpses without another XP, kill, Energy or black-hole reward.
4. All projectiles and skill objects are removed. A thrown knife is back in its slot. A hooked enemy dissolves with the rest.
5. Every XP and HP orb on the map flies to the player and is collected. This can bank more level-ups, and the Energy refill applies.
6. 0.8 s later (real time) the build phase opens.

Kept across waves: black-hole counters **[D24]**, an open portal, for up to three waves **[D57]**, weapons, upgrades, skills, Energy, stats.

### 2.3 Build phase **[D38]**

After each wave the player gets a limited time to choose cards and change weapons. Later in the run there is more to decide, so there is more time.

**The clock.** `build time after wave w = 20 s + 5 s × ⌊(w − 1) ÷ 2⌋`: 20 s after waves 1 and 2, then 5 s more every two waves, up to 50 s after waves 13 and 14, and 55 s after wave 15, before the final boss (the full list is in §9.4). It runs in real time while the battlefield is frozen, and it is always on screen.

**What happens in it**, in this order **[P]**:

1. **Armory** (§5.6).
2. **One draft per banked level-up** (§7.2), in the order the levels were gained.
3. A summary of the build with a **Start next wave** button.

The Armory comes first because an upgrade bought for a weapon that is then replaced would be lost **[D31]**.

**Leaving early.** *Start next wave* (button, or Space) is available at every step. If something is still unpicked, the game asks once — "2 cards not picked: pick at random?" — and then does exactly what a timeout does.

**When the clock runs out [D38].**

- The Armory offer counts as skipped.
- Every draft that has not been picked is **picked at random**. The random pick only chooses among cards that need no further decision; a Stat card always qualifies, a new skill that would need a replacement does not **[P]**.
- An open replace prompt is closed without replacing anything.
- A short list of what was picked for the player is shown during the next countdown.

### 2.4 Length **[D23]**

15 waves: waves 1–5 last 30 s, 6–10 last 40 s, 11–15 last 50 s. That is 600 s of fighting plus 15 countdowns of 3 s = **10 min 45 s**. Then the final boss **[D56]**: about one and a half to three and a half minutes, by the model of §9.7 **[A]**. The 15 build phases add at most 545 s if every one is used to the last second. A run therefore lasts **between about 13 and 24 minutes**, and the player decides where in that range by how fast they pick. (D23 asked for a run of a little over ten minutes: the waves still are; the boss comes on top.) The three blocks of five waves are called Act I, II and III in the rest of this document.

### 2.5 Edge cases

| Situation | Rule |
|---|---|
| The player dies on the same frame the timer reaches 0 | Death is checked first: defeat. **[P]** (1.0 resolves "player and last enemy die together" as a win; with timers a clear rule is needed.) |
| A level-up happens during wave 15 | Energy refills as usual; the draft is shown in the build phase that follows wave 15, before the final boss. |
| The portal is open when the wave ends | It stays, and the number of waves it has left goes down by one **[D57]** (§8.5). |
| The build clock runs out while the player is reading a card | §2.3: a card is picked at random. Nothing earned is lost. |
| The player presses Start next wave by accident | The confirmation only appears when something is unpicked; with nothing pending the wave starts at once. |
| The player is being knocked toward a black hole when the timer reaches 0 | Step 2 clears the knock; the player is not swallowed. |
| The player stands in a gravity zone when WaveIntro starts | Gravity is live during the countdown; the player has 3 s without enemies to walk out. |
| R is pressed during a fight | Nothing. Restart is only on the result screen and in the pause menu, so a 10-minute run cannot be lost to a slip of the finger next to E. **[P]** |
| The browser tab is closed mid-run | The run is lost. Nothing is saved. |

---

## 3. Player

**Intent.** The explorer is fragile and mobile. Three resources ask three different questions: HP — how many mistakes are left; Stamina — can I get out now; Energy — is this the moment to spend.

### 3.1 Controls

| Input | Action | Tag |
|---|---|---|
| W A S D | Move | [1.0] |
| Mouse | Aim | [D02] |
| Left button | Use the active hand weapon. Hold to charge or to keep firing, depending on the weapon. With an enemy held on the hook, a click flings it. | [1.0] [D43] |
| Left Shift | Sprint while moving | [1.0] |
| Right button | Switch between hand slot 1 and 2. With an enemy held on the hook, it is flung first, then the weapon switches (§5.2). | [D04] [D44] |
| 1 / 2 / 3 | Cast the skill in that slot once, at or toward the cursor. One press = one cast. | [D15] |
| E | Enter the void portal while standing on it | [D37] |
| Space | Start the next wave during a build phase | [D38] [P: key] |
| Esc | Pause | [P] |
| T | Whitebox / illustrated toggle, title screen only | [1.0] |

### 3.2 Resources

Every resource has at least one source and one sink; none piles up without a use.

| Resource | Start | Sources | Sinks | On screen |
|---|---|---|---|---|
| **HP** | 50 / 50 [1.0] | Restored to max between waves [1.0]; Regeneration (a bonus on Max HP cards); HP orbs dropped by elites; a Light-infused weapon and *Radiance* [D45]; trial reward | Enemy attacks; wall slams (5); your own backfires; Burn and Poison from affixed enemies [D50]. A black hole kills outright. | Green bar |
| **Shield** [D45] | 0 | An Earth-infused weapon; *Bulwark* | Cuts direct damage by 30 % and takes it before HP does **[D55]**. Falls by 1 per second once 3 s have passed without gaining any. At most half of Max HP (§4.7). | Grey bar over the HP bar |
| **Stamina** [D13] | Full. Full = 0.5 s of sprint at ×2 speed **[D60]** | Refills from empty in 1.5 s when not sprinting **[D60]**; after running dry it must reach 25 % before sprinting again [1.0] | Sprinting | Yellow bar |
| **Interstellar Energy** [D13] | Locked. Appears, full, when the first skill is taken. Max 100 [P] | **Set to max every time the XP bar fills [D16]**: at each level-up, and at each Stat card after level 10 [P]. Energy Siphon (a bonus on Max Energy cards). A Void-infused weapon, the *Devour* skill, auto weapons that leech [D45]. After the void trial it regenerates [D19]. | Skill casts; a hit from a Void-affixed enemy (10 each) [D50] | Blue bar; purple once it is Void Energy |
| **XP** | 0 | Orbs from enemies killed by damage. Enemies never run out [D41]. | Levels 2 to 10 [D36]; after that, a Stat card every 549 XP [D40] | Bar across the top |
| **Banked drafts** | 0 | Each fill of the XP bar during a wave | One per draft in the build phase [D27] | Pips on the XP bar |
| **Hole counter**, one per black hole | 0 / 40 | Each enemy that hole swallows [D32] | Turns the hole into the portal at 40 [D37]; the portal is open for three waves [D57] | Number above the hole |
| **Build time** | 20 s to 55 s per build phase, including after wave 15 [D38] | — | Runs down in real time; Start next wave gives the rest up | Clock on every build screen |
| **Reroll** | 1 per build phase [P] | — | Rerolls one draft | Button on the card screen |

### 3.3 Stats

Stats change only through Stat cards (§7.3). Starting values are the 1.0 values.

**V4 (2026-10-14).** Fourteen separate stat cards were merged into seven cards plus Element Penetration. Several stats below are now raised only as a second effect on another card, and three are no longer raised at all. Caps are the values in `Data/ProgressionRules.asset`.

| Stat | Start | What it does | Raised by | Cap |
|---|---|---|---|---|
| Max HP | 50 [1.0] | A Max HP card also heals by the same amount | Max HP card | — |
| Regeneration | 0 | HP restored every 5 s | Bonus on Max HP cards | 5 |
| Move Speed | 6 [1.0] | Units per second. Helps sprint escape against the current fixed-hole pull; ordinary walking cannot move radially out of a fixed-hole gravity zone under the 2026-10-12 revision (§8.4). | Move Speed card | +40 % |
| Stamina | 0.5 s sprint, 1.5 s recovery **[D60]** | Longer sprint. The bar is short on purpose: a sprint is one decision, not a way of moving. | Bonus on Move Speed cards | 1.25 s sprint |
| Damage | × 1 | All weapon and skill damage. Each card multiplies it: × (1 + d) | Damage card | — |
| Crit chance | 0 % | Chance for a hit on an enemy to deal × 2 | Bonus on Damage cards | 60 % |
| Attack Speed | +0 % | Hand and auto weapon cooldowns and reloads: base ÷ (1 + sum of the bonuses). Not skills. | Attack Speed card | +200 % (cooldown ÷ 3) |
| Knockback | +0 % | How far your attacks push. Pure terrain stat: feeds holes and causes slams. | Knockback card | +100 % |
| Impact | 0 | Added to the wall damage enemies take from your knockbacks; grows with the wave's enemy HP multiplier | Bonus on Knockback cards | — |
| Armor | 0 | Damage taken × 100 ÷ (100 + armor): every point adds the same share of effective HP | Protection card | 300 |
| Protection time | 0.5 s [1.0] | Time after a hit during which attacks do nothing to you | No longer raised by cards | — |
| Pickup Range | 2 | Orbs inside it fly to you | No longer raised by cards | — |
| Luck | 0 | Better odds of Rare and Exceptional stat cards (§7.4) | No longer raised by cards | — |
| Max Energy \* | 100 | | Max Energy card | 250 |
| Energy Siphon \* | 0 | Energy gained each time an enemy dies | Bonus on Max Energy cards | 2 per death |
| Element Penetration \*\* | 0 % | The share of an element immunity that your elements ignore (§4.6, §9.7) **[D56]** | Element Penetration card | 100 % |

\* Only offered once Energy is unlocked. \*\* Only offered from wave 11 (§7.3).

**Damage formula [V4].** `damage = weapon base × charge factor × Mk multiplier (§5.6) × Π(1 + dᵢ)`, where each dᵢ is one Damage card; the hit then crits for × 2 at the player's crit chance. This replaces the draft-5 formula, in which all Damage % cards added up inside one bracket and a weapon's own *Power* upgrade multiplied in a second. Enemies have no armour stat; while the target has a Shield (§4.7) the result is cut by 30 % and taken off the Shield first. Light against a Void carrier multiplies the result by 1.5 (§4.6). Enemy damage to the player is a flat amount, multiplied by the player's armor factor 100 ÷ (100 + armor). **[P]**

### 3.4 Character states **[1.0]**

The state machine is shared by the player and enemies and is kept as it is.

| State | Control | Notes |
|---|---|---|
| Normal | Full | |
| Knocked | None | Entered by a knockback. Ends when the slide slows below 0.3 units/s: about 0.6 s after a 1.5-unit knock, 0.8 s after 5, 0.9 s after 10. |
| Thrown | None | Same as Knocked, but distance ignores body size (hook throw). |
| Hooked | None | Held by the hook. Immune to knockback. Cannot be pulled or swallowed. |
| Dashing | None | Launch Yourself. Immune to attack damage and knockback. |
| Swallowed | None | Entered a black hole; already counted as dead. (1.0 name: Falling.) |
| Dead | None | |

New in v2.0 **[P]**: gravity (§8.4) adds a drift to Normal, Knocked and Thrown. It does nothing to Hooked or Dashing — but a dash whose path crosses a black hole still ends there, as in 1.0.

Frozen and Paralysed (§4.6) are effects layered on top of these states, not states of their own: a Frozen character in the Normal state cannot act, and can still be Knocked. **[P]**

---

## 4. Combat rules

**Intent.** Damage and displacement are two separate tools. Hitting hard gives XP; pushing far gives terrain kills. The rules below keep them separate.

### 4.1 What happens when an attack lands

Checked in this order. **[1.0]** unless tagged.

1. Target already dead → nothing.
2. Target is in its protection window (the player, 0.5 s after the last hit) or Dashing → nothing at all: no damage, no knockback.
3. If the target has any Shield, the damage (§3.3) is cut by 30 % **[D55]**. It comes off Shield first, then HP (§4.7).
4. **HP ≤ 0 → settle the damage death and its rewards once.** Its saved enemy instance can remain briefly as a dead body, with no AI or attacks, so displacement or gravity can carry the corpse into a fixed hole. A later corpse feed does not give a second XP orb, kill credit or Energy payout. [Revision 2026-10-12]
5. Otherwise the knockback is applied (§4.2).

**Latest override, 2026-10-12:** a hit that kills does not itself add a feed; its actual corpse entering a fixed hole may add one afterwards. The former "a killing hit never feeds" rule has been replaced by the corpse-feeding rule. Each enemy spawn lifetime can add at most one feed across all holes. An enemy already swallowed alive cannot feed again as a corpse. Temporary skill holes still produce no feeds.

An attack that carries an element settles it after these steps: stacks go on a target that is still alive, or the wielder is paid (§4.6). **[D45]**

### 4.2 Knockback **[D14]**

Both the player and monsters are knocked back by most attacks. How far depends on the attack.

**Model [1.0].** An attack carries a knockback *distance*. The target slides exactly that far along the hit direction: fast at first (initial speed = 6 × distance), slowing exponentially; 95 % of the distance is covered in the first 0.5 s.

**Classes.** New attacks pick a class; existing attacks are listed with the value now in the code.

| Class | Distance | Attacks |
|---|---|---|
| None | 0 | Reverse Shot bullet [1.0]; Throwing Knife hit [1.0] |
| Light | 3 | Fists [1.0] |
| Medium | 5 | Grunt and Brute punch on the player [1.0]; Charged Shot, tap [1.0]; Strike blast [the 1.0 value]; the corpse dragged by the knife [1.0] |
| Heavy | 8 | Hook fling [1.0] |
| Launch | 10 | Charged Shot, full charge [1.0] |

The Charged Shot scales smoothly from 5 to 10 with the charge. The crash of a flung enemy pushes by its own formula (§5.3): up to 5 for a small body, 10 for a large one, less the slower it is.

**Modifiers.** `distance = class distance × target's size factor × (1 + attacker's Knockback %)`

| Target | Size factor |
|---|---|
| Player, Grunt, Spitter, Bloater, Warden, Splitter | 1.0 [1.0 for player and Grunt] |
| Brute, Stomper | 0.5 [1.0 for the Brute] |
| Skitter, Shade | 1.5 [P] |
| Rammer | 0.75 [P] |
| Spire | 0 — it cannot be moved (§9.1) [P] |
| Elite | × 0.5 on top of its type [P] |
| Void boss | 0 — knockback becomes stagger instead (§8.6) [P] |

A hook throw ignores the size factor [1.0]. The Knockback stat only affects how far *you* push; nothing reduces how far the player is pushed [P].

**Rules.**

- A new knockback overwrites the one in progress. [1.0]
- While Knocked or Thrown a character cannot move, attack, or resist gravity. [1.0 + P]
- "Most attacks" **[D14]** leaves room for exceptions: rapid bullets and the knife's pierce stay at 0. Giving a 0.12 s-interval bullet even a small knockback would keep its target in the Knocked state permanently. [P]

### 4.3 Hard surfaces: slam and rebound **[D07] [D28]**

Hard surfaces are interior walls, pillars, **the map boundary**, and walls made by skills (*Bulwark*, §6.3). One rule covers all of them.

- **Slam [1.0].** A Knocked or Thrown character that reaches a hard surface while still moving toward it faster than 3 units/s takes wall damage, once per impact. Because speed = 6 × the distance still to go, this means: *at least half a unit of the knock was left*.
- **Wall damage.** 5 to the player [1.0]. To an enemy: `5 × that wave's enemy HP multiplier + your Impact stat` [P], so a slam keeps removing the same share of an enemy's HP in wave 15 as in wave 1. Wall damage ignores protection [1.0].
- **Rebound [D07].** After the slam, what is left of the knock is reflected off the surface at half strength [P]. A rebound can carry an enemy into a black hole, or into a second wall. At most 3 slams count per knock [P].
- Pushes that are not knockbacks — gun recoil, the bounce after Launch Yourself hits a wall — never deal wall damage [1.0].
- **Projectiles [D07].** A projectile is destroyed by a hard surface unless it has *Ricochet N*: it bounces N times, then is destroyed. Ricochet is a property of a weapon or an upgrade, not a global rule [P].

### 4.4 Hit feedback **[1.0]**

Unchanged: white flash, squash, shards by hit type (Blunt / Bullet / Pierce / Slam / Blast), damage number, hit stop, screen shake. New sources map onto the existing types. A Burn or Poison tick shows a small coloured number only: no hit stop, no shake [P].

### 4.5 Edge cases

| Situation | Rule |
|---|---|
| An enemy punch lands while the player stands in a gravity zone | The knock direction is away from the attacker. Enemies arrive from outside the zone, so the push is toward the hole. Standing in a zone as bait is deliberately dangerous. |
| The player is knocked into a wall | Same slam and rebound as an enemy: 5 damage, bounce back. |
| A character is slammed while protected | Wall damage still applies [1.0]. |
| An enemy is knocked into another enemy | They collide as bodies; no damage [1.0]. The exception is an enemy flung by the hook (§5.3). |
| The player is knocked during a charge | The charge is cancelled, no shot [1.0]. |

### 4.6 Elements **[D45] [D48] [D49]**

Seven elements: Fire, Poison, Frost, Lightning, Earth, Light, Void. An element is an **affix**: a tag that can sit on a weapon (through an Infusion card, §5.8), on a skill (built in, §6.3), on an enemy (§4.8). The player **carries Void** while any of their weapons is infused with it, and for good after the void trial (§8.6). That matters for one rule only: Light × Void.

**Intent.** Each element does one thing that no other does, so choosing an element is choosing a job, not a colour. Four of them leave something **on the target**; three give something **to whoever wields them**.

| Element | Family | What it does | Rule | Tag |
|---|---|---|---|---|
| **Fire** | On the target | Burn: fast damage over time that burns itself out | Ticks twice a second. On an enemy each tick burns 20.6 % of the current stacks, and each stack deals 1.5 × R(w) in total before it is gone [V4]; on the player each tick deals damage equal to the stacks, then removes 1. Halved while the target has a Shield. | [D45] [D49] |
| **Poison** | On the target | Poison: slow damage over time that never fades | Ticks once a second. Stacks never go down. On an enemy, damage per second = stacks × 0.12 × R(w) [V4]. Ignores Shield: it comes straight off HP. | [D45] |
| **Frost** | On the target | Builds up to a full stop | Stacks add up and slow an enemy before the threshold [V4]. At the threshold the target is **Frozen**: for a short time it can neither move nor attack. | [D45] [D48] |
| **Lightning** | On the target | Builds up to a long disruption and travels between targets | Stacks add up. At the threshold the target is **Paralysed**: for a longer time an enemy is slowed and twitches at intervals, and each twitch interrupts a wind-up [V4]. Basic Lightning also jumps to other targets; Frost accelerates stack accumulation and widens the jump range. Updated 2026-10-12. | [D45] [D48] |
| **Earth** | To the wielder | Shield | Each Earth hit gives the wielder Shield points (§4.7). | [D45] |
| **Light** | To the wielder | Lifesteal; bane of Void | Each Light hit heals the wielder, more when the wielder is hurt [V4]. Light deals more damage to anything that carries Void. | [D45] |
| **Void** | To the wielder | Energy; prey of Light | Each Void hit restores the wielder's Energy. Whoever carries Void takes more damage from Light-affixed attackers. | [D45] |

**Why these seven do not overlap.** Each fills one column and no other:

| | Damage now | Damage later | Stop it | Disrupt a group | Survive a hit | Recover | Cast more |
|---|---|---|---|---|---|---|---|
| Fire | ● | | | | | | |
| Poison | | ● | | | | | |
| Frost | | | ● | | | | |
| Lightning | | | | ● | | | |
| Earth | | | | | ● | | |
| Light | | | | | | ● | |
| Void | | | | | | | ● |

**How stacks are applied [P].** A hit from an element-carrying weapon or skill applies `stacks = the hit's base damage ÷ 5` to the player, and `stacks = the hit's base damage ÷ (5 × R(w))` to an enemy **[V4]**. R(w) is wave w's reference damage per second divided by wave 1's: how much fodder effective HP has grown since wave 1 (5.33 on wave 15; see [`V4-NUMBERS.md`](V4-NUMBERS.md)). Stacks need not be whole: on wave 1 a 3-damage bullet adds 0.6 of a stack, a 5-damage bullet 1, a full Charged Shot (15) 3, a hook crash of 12 2.4. Damage over time on an enemy keeps fractional damage and pays it out in whole points. (Draft 5 divided by 5 on every wave and used only the whole part of the stacks; V4 changed it because late-wave hits applied stacks that ticked for nothing.) A weapon with a built-in element (§5.4) applies it twice as fast: `damage ÷ 2.5`. Damage-over-time ticks apply nothing. This makes fast and slow weapons equally good carriers: what counts is damage dealt, not hits landed. Earth, Light and Void do not use stacks: they pay the wielder a share of the damage dealt.

**The numbers [P].** Rows marked V4 were changed on 2026-10-14; element states on the player keep the draft-5 rules.

| | Value | What follows from it |
|---|---|---|
| Fire | On an enemy [V4]: every 0.5 s, 20.6 % of the stacks burn off (a half-life of 1.5 s); each stack deals 1.5 × R(w) in total. On the player: every 0.5 s, damage = stacks, then stacks − 1 **[D49]**. At most 20 stacks. | On an enemy the steady burn adds about 30 % of the weapon's damage per second (about 60 % for a built-in Fire weapon); a stack lives 2.4 s on average. It is mostly wasted on fodder that dies in 0.65 s and pays off on elites, bosses and after a target switch. On the player, N stacks burn for N ÷ 2 seconds and deal N × (N + 1) ÷ 2 in all: 3 stacks → 6. |
| Poison | On an enemy [V4]: tick every 1 s, damage per second = stacks × 0.12 × R(w); at most 20 stacks. On the player: tick every 1 s, damage = stacks ÷ 2, rounded up; at most 30 stacks. Never decays **[D45]**. | After t seconds of hits it adds about weapon damage per second × 0.12 × t ÷ 5: +4.8 % at 2 s, +19 % at 8 s, +72 % at 30 s (twice that for a built-in Poison weapon). At the cap it deals 2.4 per second on wave 1 and 12.8 on wave 15. Slow against small enemies; relentless against big, shielded or long-lived ones: the boss killer. |
| Frost | 6 stacks → Frozen for 1.5 s **[D48]**. Before that, an enemy is chilled [V4]: speed × 1 ÷ (1 + 1.5 × stacks ÷ threshold); a control-immune boss takes half the chill. Stacks fall by 1 per second. After a freeze: stacks reset and the target cannot gain Frost for 3 s. | × 0.67 at 2 of 6 stacks, × 0.50 at 4, × 0.40 just before the freeze. No permanent freeze. |
| Lightning | 6 stacks → Paralysed for 4 s: a 0.3 s twitch every second **[D48]**. A twitch cancels a wind-up, a charge or a spit. On an enemy [V4] paralysis no longer roots it: for the whole 4 s it moves at × 0.55, multiplied with any Frost chill (draft 5 stopped movement during each twitch). Stacks fall by 1 per second. Afterwards: reset, 3 s without gaining Lightning. | × 0.31 together with 3 Frost stacks. Longer but weaker than a freeze: the enemy still acts between twitches. |
| Lightning's jump | Receiving Lightning can spread it to other targets within 4 units, then onward; each target once, at most 5 additional targets. Jump damage 3 × R(w); the first jump keeps full damage and stacks, and jump n keeps 0.7^(n − 1) of them [V4]. Frost is not required. A Frost-carrying jump origin has range 6; a Frost-carrying recipient gains 1.5× Lightning stacks. Range 6 and multiplier 1.5 are adjustable candidates. | Basic Lightning spreads; Frost strengthens accumulation and range. |
| Earth | The wielder gains Shield equal to 10 % of the damage dealt; fractions are kept and paid out in whole points. | Cap and decay are in §4.7. |
| Light | The wielder is healed for 5 % of the damage dealt × (1 + 2 × the wielder's missing HP fraction) [V4], fractions kept the same way. +50 % damage to Void carriers. | Heals 5 % at full HP, 10 % at half HP, 13 % at 20 % HP. |
| Void | The wielder gains Energy equal to 10 % of the damage dealt, fractions kept the same way. A Void carrier takes +50 % damage from Light-affixed attackers. | |

Thresholds grow with body size: a Brute or a Stomper needs twice the stacks (12), an elite twice its type's. The void boss cannot be Frozen or Paralysed; each time it would be, its stagger meter gets 15 instead (§8.6).

**What the three wielder elements pay [P].** They scale with damage dealt, so all three are weak early and strong late. For the typical player of §7.5, if the infused weapon deals half of the wave's damage (Light at full HP; it heals more when the wielder is hurt):

| Wave | Damage through the weapon | Earth: Shield gained | Light: HP healed | Void: Energy gained | For scale |
|---|---|---|---|---|---|
| 3 | 130 | 13 | 7 | 13 | One level-up refill is 100 Energy |
| 8 | 600 | 60 | 30 | 60 | The Regeneration stat at its cap heals 40 HP in a 40 s wave |
| 13 | 1 740 | 174 | 87 | 174 | Void Energy regenerates 200 in a 50 s wave |

The shares are set so that a Void-infused weapon stays just under what the void trial gives **[D37]**, and Light stays within twice the cap of the Regeneration stat. All three are levers in §13.

**Immunity and Penetration [D56].** A target that is immune to an element takes nothing from it: no stacks, and no payout to the wielder for the damage dealt to it. The direct damage of the hit is unchanged. Only the final boss is immune to elements (§9.7). The Element Penetration stat (§7.3) lets the player's elements work at that share of their strength against an immune target: at 40 % Penetration, 5 stacks become 2, and a 10 % payout becomes 4 %.

**Interactions between elements.** There are exactly four, all taken from the decisions; no other pair interacts.

| Pair | Rule |
|---|---|
| Fire × Shield | Burn ticks are halved while the target has any Shield |
| Poison × Shield | Poison ignores Shield |
| Lightning × Frost | Basic Lightning already spreads; Frost adds faster accumulation and wider jumps (candidate stacks ×1.5, jump range 4→6). |
| Light × Void | Light deals +50 % to a Void carrier — in both directions, player and enemy |

### 4.7 Shield **[D45] [D55] [P: one rule for both sides]**

Shield is a layer of points in front of HP. *Armour* **[D55]** and *shield value* **[D45]** are this one layer. The player gets it from Earth. Enemies get it from the Earth affix or from a Warden; a Stomper starts with it; the final boss renews it (§9.1, §9.7).

- **Direct damage** — hits, crashes, blasts, wall slams — **is cut by 30 % while the target has any Shield [D55]**, for the player and for enemies alike. What is left comes off Shield first, and the rest off HP.
- **Burn** ticks are halved (rounded up) while the target has any Shield, and come off Shield first. **[D45]**
- **Poison** ignores Shield completely. **[D45]**
- Shield stops neither knockback nor the application of stacks.
- **The player's Shield**: at most half of max HP (25 at the start). It falls by 1 per second once 3 s have passed without gaining any. Shown as a grey bar over the HP bar.
- **An enemy's Shield**: shown as a pale segment on its HP bar. Earth-affixed: starts at half its max HP. Under a Warden: 30 % of max HP, renewed every 4 s while it stays in range. A Stomper: half its max HP from the start, never renewed.

Shield is what makes the kind of damage a real choice. Direct damage loses 30 % to it, Burn loses half, Poison loses nothing, and a Railgun Lance ignores it (§5.4). Fire melts unshielded crowds and stalls on Shields; Poison does the opposite.

### 4.8 Affixed enemies, and what protects the player **[D50]**

From wave 5 some enemies spawn with one element affix; elites always have one (§9.5). An affixed enemy has a coloured outline and an icon. It follows the same element rules as the player:

| Affix on an enemy | What it means |
|---|---|
| Fire | Its hits put 3 Fire on the player: 6 damage over 1.5 s, half of that if the player has Shield |
| Poison | Its hits put 1 Poison on the player |
| Frost | Its hits put 3 Frost on the player |
| Lightning | Its hits put 3 Lightning on the player |
| Earth | It has a Shield of half its max HP |
| Light | Its hits heal it for the damage dealt; +50 % damage to a player who carries Void |
| Void | Its hits drain 10 Energy from the player; it takes +50 % from the player's Light |

**Protections for the player [D50].**

- The player is Frozen or Paralysed for **at most 1 s**, and is then immune to that status for 4 s. Frozen: cannot move or attack. Paralysed: cannot attack or cast and the current charge is cancelled; movement stays.
- **Every status on the player is cleared when the wave ends.**
- **Any healing removes one Poison stack** from the player: an HP orb, a tick of Regeneration, a point of lifesteal.
- Gravity still acts on a Frozen player. A freeze inside a gravity zone is the most dangerous second in the game — and it is exactly one second.

### 4.9 Edge cases for elements

| Situation | Rule |
|---|---|
| A burning enemy gains Shield | From that tick on, Burn is halved; when the Shield is gone, it is full again |
| Fire and Poison on the same enemy | Both tick, each on its own clock |
| An enemy dies with stacks on it | They are gone, unless a skill says otherwise (Contagion, §6.3) |
| A Frozen enemy is hit by a knockback | It slides as usual: being Frozen stops what it does, not what is done to it |
| A Rammer is Frozen, or twitches, in mid-charge | The charge ends there |
| What counts as carrying Frost | At least 1 Frost stack. A Frozen enemy counts until its freeze ends. |
| Lightning hits an enemy with no Frost carrier nearby | Basic jumps still occur within 4; Frost is not a prerequisite. |
| Lightning jumps onto an enemy that cannot gain Lightning right now | It takes the 3 damage and no stacks; the jump continues past it |
| A Light weapon hits a Void-affixed enemy that has a Shield | +50 % first, then the 30 % cut, then Shield, then HP |
| The player carries Void — a Void-infused weapon in any slot, or Void Energy after the trial — and meets Light-affixed enemies | They deal +50 % to the player. Replacing that weapon or its infusion ends it; after the trial it is permanent (§8.6). |
| The player holds a Light weapon and a Void weapon | Allowed. The player counts as a Void carrier; their own Light weapon does nothing to them. |
| Statuses in the void trial | Same rules. The boss is immune to Freeze and Paralysis, not to Burn or Poison. |
| A swallowed enemy had Poison on it | Nothing happens; swallowed is swallowed |
| An element hit kills the target | No stacks are applied, and Earth, Light and Void still pay the wielder for the damage dealt |
| A hit is larger than the Shield that is left | The whole hit is cut by 30 % first; Shield takes what it can and HP the rest |
| The target has a single point of Shield | The cut still applies to that hit; the Shield is gone after it |
| A hit of an element the target is immune to | No stacks, no payout; the direct damage is unchanged (§4.6) |

---

## 5. Weapons

**Intent.** A weapon is a small puzzle the player solves once and then exploits for the rest of the run. Two hand weapons carry the skill; auto weapons add texture without taking the aim away.

### 5.1 Rules

| # | Rule | Tag |
|---|---|---|
| R1 | A weapon is a **form** (what it looks like) plus an **anomaly** (what it does). | [D11] |
| R2 | Every form has exactly two anomalies. One is rolled, 50/50, when the weapon is created, and never changes. | [1.0] for the three base forms; [P] as a rule for all forms |
| R3 | The anomaly is unknown until the weapon is first used. Before: "Rifle ?". After: "Rifle · Charged Shot" and its description. | [D01] |
| R4 | Every anomaly has a backfire and a way to exploit it with terrain. | [D01] |
| R5 | No use limit. Cooldown — or a clip and a reload for held-fire weapons — is the only limiter. | [D01] |
| R6 | Every weapon grows in two ways: its Mk tier, raised at the Armory, multiplies its damage; draft cards add Infusion, Twist or Tame, each taken at most once (§5.8). (V4; draft 5 also offered Power, Tempo and Reach.) | [D15] weapons can be upgraded; [D46] [D47]; [P] the axes |
| R7 | The player never holds two weapons with the same anomaly. | [P] |

### 5.2 Slots

- **2 hand slots [D04].** One is active; the **right mouse button** switches **[D44]**. Each slot keeps its own cooldown, and cooldowns keep running while holstered. An empty hand slot is **Fists**: 3 damage, Light knockback, hits only the nearest enemy in a small arc, 0.4 s cooldown [1.0].
- **3 auto slots [D22].** Auto weapons act on their own while a wave is active.

Switching [P]: instant, allowed only in the Normal state.

| While… | Switching does this |
|---|---|
| Charging a shot | Cancels the charge; nothing is fired |
| The hook's blade is flying out, pulling or coming back | Blocked until it is done (well under a second) |
| An enemy is held on the hook | The enemy is flung where you are aiming first, then the weapon switches **[D43]** (1.0 behaviour, kept) |
| The knife is in flight | Allowed; the knife keeps flying and returns to its own slot |
| Dashing | Blocked |

### 5.3 The three starting forms **[D12]**

Rocket Launcher, Rifle, Knife. (In the code they are `Missile`, `Gun`, `Knife`; only the display names change.) The six anomalies exist today. Values in the table were read from the tuned assets on 2026-10-11.

| Form · Anomaly | What it does | Backfire | Terrain exploit | Limiter in v2.0 [P] |
|---|---|---|---|---|
| **Rifle · Charged Shot** [D34] | Hold up to 1 s, release: one shot that **pierces** every enemy on its path until a wall or 40 units. Damage 6 → 15 and knockback 5 → 10 as the charge grows. | You move at half speed while charging | Line enemies up with a black hole or a wall behind them | Cooldown 0.35 s |
| **Rifle · Reverse Shot** | Hold to fire. Bullets leave from your **back**: 5 damage, no knockback, range 40. Each shot's recoil pushes you 0.8 toward where you are aiming. | The recoil can walk you into a black hole | Stand with your back to a wall: with Ricochet 1 [P] the bullets come back past you. Use the recoil as a dash. | Clip of 4, reload 0.6 s |
| **Knife · Throwing Knife** | Flies 12, then ricochets through up to 4 enemies within 7 of each other: 5 damage **[D59]**, no knockback. Then it returns. It cannot be thrown again until it is back. | The first enemy it kills is dragged back toward you; the corpse hits anyone in its way, you included (Medium) | Reaches enemies around corners and behind a crowd | Cooldown 0.3 s after it returns |
| **Knife · Hook** [D43] [D35] | The blade extends up to 10, hooks the first enemy and pulls it in front of you, where it is **held**. Click again to **fling it 8 units where you are aiming** **[D43]**. If the flung enemy **crashes into another enemy, both of them and everything around take damage**: more the sooner it hits, more the bigger the flung body (formula below). While held it is cover: a punch aimed at you hits it first [1.0]. You move at full speed while holding **[D33]**. | You cannot move while the blade is going out or pulling. While you hold an enemy you can use nothing else: a click flings it, and so does switching weapons. The flung enemy is usually alive and comes straight back. | Walk the held enemy to where you want it, then fling it into a crowd (crash), a black hole (a feed) or a wall (slam and rebound). The closer you stand to the crowd, the harder the crash. | Cooldown 0.2 s after the fling |
| **Rocket Launcher · Strike** [D52] | Nothing leaves the launcher. It marks the point under the cursor, up to 20 away; **1 s later a rocket comes down on that point**: blast radius 3, 10 damage, Medium knockback away from the centre, to everyone in it. The point is fixed at the click. | The delay: what you aimed at has moved on, and it was running at you. The blast hurts you like anyone else. | Mark where they will be, not where they are: a corridor mouth, the rim of a gravity zone. The push goes outward from the mark, so mark the side away from the hole. | Cooldown 2.5 s |
| **Rocket Launcher · Launch Yourself** | You fly 15 units at 25 units/s. Enemies you touch die. A wall stops you, hurts you (5) and bounces you back 3. | A black hole on the path swallows you | Cross the map; cut a lane through a crowd | Cooldown 4 s. Elites and bosses take 40 damage instead of dying. |

In 1.0 these are limited by uses: 5 for the gun, 3 for the knife, 1 for the missile in the current scene. With uses gone **[D01]**, the last column is what keeps each one in check; all of it needs playtesting.

#### The hook's crash, as a formula **[D35] [P: the numbers]**

A flung enemy slides like any knockback (§4.2): it leaves at 48 units/s (6 × the fling distance of 8) and slows down exponentially. The crash is scored by the **kinetic energy** it still has when it touches another enemy — mass × speed².

```
speed ratio   r = speed at the crash ÷ launch speed
                = 1 − (distance flown ÷ 8)          the same thing, by distance
                = e^(−6 × seconds flown)            the same thing, by time
size          m = (radius of the flung body ÷ 0.5) ^ impactSizeExponent

crash damage    = impactDamage    × m × r²          rounded, never below 1
crash knockback = impactKnockback × m × r           pushed away from the crash point
crash reach     = radius of the flung body + impactRadius
```

| Parameter (in `Effects/KnifeHook.asset`) | Start value | Meaning |
|---|---|---|
| `impactDamage` | 12 | Damage when a small enemy crashes at full launch speed |
| `impactSizeExponent` | 1 | 1 = a body twice as wide hits twice as hard; 2 would make it four times |
| `impactRadius` | 1.5 | How far past the flung body's edge the crash reaches |
| `impactKnockback` | 5 | Push when a small enemy crashes at full launch speed |
| `throwDistance` | 8 [1.0] | How far the fling carries. Raising it also makes crashes at the same distance harder, because less speed has been lost. |

What the start values give (small body = Grunt, radius 0.5; large body = Brute, radius 1):

| The flung enemy has flown | Time in the air | Speed left | Damage, small body | Damage, large body | Push, small / large |
|---|---|---|---|---|---|
| 0 units (crash at once) | 0 s | 100 % | 12 | 24 | 5.0 / 10.0 |
| 1 unit | 0.02 s | 88 % | 9 | 18 | 4.4 / 8.8 |
| 2 units | 0.05 s | 75 % | 7 | 14 | 3.8 / 7.5 |
| 3 units | 0.08 s | 63 % | 5 | 9 | 3.1 / 6.2 |
| 4 units | 0.12 s | 50 % | 3 | 6 | 2.5 / 5.0 |
| 5 units | 0.16 s | 38 % | 2 | 3 | 1.9 / 3.8 |
| 6 units | 0.23 s | 25 % | 1 | 2 | 1.2 / 2.5 |
| 7 units | 0.35 s | 13 % | 1 | 1 | 0.6 / 1.2 |

Rules around it:

- **The fling is manual [D43].** A hooked enemy stays held until the player clicks. So the player chooses how far it flies before it hits, and with that the damage: walking up to a group before flinging is the whole skill of this weapon.
- **Who is hit.** The flung enemy itself, the enemy it touched, and every enemy whose body is within the crash reach. The player is neither hurt nor pushed, and touching the player does not set a crash off. **[P]**
- **Once per fling.** The flung enemy stops where it crashes. Below 3 units/s (the last half unit of the slide) nothing happens — the same threshold a wall slam uses.
- **A wall comes first.** A flung enemy that reaches a wall takes wall damage as before and causes no crash.
- **Damage and upgrades.** In v2.0 the crash damage goes through the normal formula of §3.3 (Damage cards, the weapon's Mk multiplier); the push is scaled by the Knockback stat. A crash from an infused hook applies its element like any other hit (12 damage → 2.4 stacks).
- **Checked on 2026-10-11.** The Strike is covered by 8 tests of its own (it lands after the delay and not before, misses what is outside its radius, stays on the first point when the aim moves, hurts its user, and is pulled in to its range). The crash formula is covered by automated tests (`ThrowImpactTest`: 12, 3, 24, never below 1, push 2.5; `HookTest`: held until the second click, flung by the click, full speed while holding, a crash hurts both enemies, the player is unharmed). The measurements in the game were taken with the earlier build that flung automatically; the formula is the same: a small enemy crashing after 1 unit dealt 9 to itself and to the enemy it hit; after about 4 units, 3; a large enemy after about 3.4 units, 7; the player, standing 2 units from the first crash, lost nothing. The manual fling has not been measured in the game separately.

Read the table against enemy HP: a wave-1 Grunt has 10. A Grunt flung into its neighbours from point-blank nearly kills all of them; the same fling into a group 4 units away only scratches them. That is the intended shape — **the hook rewards fighting close** — and `impactDamage` is the one number that moves the whole table up or down.

**Upgrades.** All six anomalies grow by Mk tier and take Infusion, Twist and Tame cards (§5.8); their *Twist* and *Tame* are listed there.

### 5.4 More hand weapons **[D12] [D46] [D58] [P: this roster]**

**Intent [D58].** The six starting anomalies are tricks: they move things around, and the kill comes from the terrain. Draft 4 added fourteen more tricks, and the roster had too many tools and not enough weapons that simply kill. This draft keeps eight of those tricks and adds fourteen anomalies whose job is damage — damage that is modest when the weapon is picked up and large once a build has been grown around it.

Three families:

| Family | What it is for | Forms | From |
|---|---|---|---|
| **Trick** | Moving things: pushing, pulling, swapping | Rifle, Knife, Rocket Launcher (§5.3); Grav Gun, Scatter Cannon, Phase Dagger, Plasma Launcher | The start; the four new ones from wave 6 |
| **Element** | Carrying one element, built in | Flamethrower, Cryo Cannon, Arc Coil, Spore Launcher | Wave 3 |
| **Killing** | Late weapons that end things | Minigun, Swarm Launcher; Railgun | Wave 6; wave 11 |

**Cut in this draft [D58]:** Pylon Gun, Mass Driver, Tether Lance. They dealt the least damage of the roster, and each needed a system of its own (placed objects, size changes, links).

Every weapon still follows §5.1: two anomalies per form, one of them rolled; unknown until first used; a backfire on each.

#### The damage budget **[D58] [P: the numbers]**

Three rules shape every number below:

1. **A weapon that hits hard hits few.** High damage per hit goes with one target, or a line that loses strength.
2. **A weapon that hits many starts low.** Wide weapons are the weakest pick at Mk I.
3. **Wide weapons grow the most.** *Power* and *Tempo* double any weapon; *Reach* adds another 40 % only to weapons that hit several targets; Damage cards and elements multiply every hit, so they are worth more the more hits there are. A wide weapon is the weak choice in the Armory and the strong one four upgrades later.

Direct damage only: no cards, no element. "One target" is damage per second on a single enemy; "a crowd of 8" assumes eight enemies within the weapon's reach. "Fully upgraded" is *Power* and *Tempo*, plus *Reach* where the weapon hits several targets.

| Weapon · anomaly | Family | Damage per hit | DPS vs one target | DPS vs a crowd of 8 | Fully upgraded: one / crowd |
|---|---|---|---|---|---|
| Rifle · Charged Shot | Trick | 15, full charge | 11 | 33 | 22 / 93 |
| Rifle · Reverse Shot | Trick | 5 | 19 | 19 | 37 / 37 |
| Knife · Throwing Knife | Trick | 5 | 3 | 17 | 7 / 47 |
| Knife · Hook | Trick | 12, point-blank crash | 0 | 32 | 0 / 90 |
| Rocket Launcher · Strike | Trick | 10 | 4 | 24 | 8 / 67 |
| Flamethrower · Stream | Element | 2 per tick, 4 ticks/s | 5 | 27 | 11 / 75 |
| Flamethrower · Wildfire | Element | 1 per tick, per patch | 4 | 16 | 8 / 45 |
| Cryo Cannon · Shatter | Element | 18; 36 on a Frozen target | 27 | 39 | 54 / 78 |
| Cryo Cannon · Blizzard | Element | 1.5 per tick | 6 | 36 | 12 / 101 |
| Arc Coil · Chain | Element | 6 | 7 | 33 | 13 / 93 |
| Arc Coil · Capacitor | Element | 15, full charge | 2 | 20 | 5 / 56 |
| Spore Launcher · Cloud | Element | 1 per tick | 2 | 12 | 4 / 34 |
| Spore Launcher · Needler | Element | 2 | 10 | 10 | 19 / 19 |
| Railgun · Lance | Killing | 36, then 27, 20… | 13 | 31 | 27 / 86 |
| Railgun · Execute | Killing | 24, growing to 48 | 20 | 20 | 40 / 40 |
| Minigun · Spin-up | Killing | 3 | 36 | 36 | 72 / 72 |
| Minigun · Meltdown | Killing | 3, and a blast of 20 | 20 | 34 | 40 / 69 |
| Swarm Launcher · Barrage | Killing | 4 × 8 rockets | 4 | 36 | 9 / 100 |
| Swarm Launcher · Carpet | Killing | 6 × 6 blasts | 2 | 43 | 5 / 121 |

**V4 note.** Power, Tempo and Reach are no longer offered in drafts; damage now grows through Mk tiers (§5.6), up to × 2.3 at Mk6. The "fully upgraded" column, rule 3 above and the mentions of Power, Tempo and Reach in the tables below are the draft-5 model, kept for comparison.

Read down the last two columns: the weapons that hit several targets average 28 against a crowd at Mk I and 77 fully upgraded; the single-target ones go from 22 to 44. For scale, a Grunt has 10 HP in wave 1 and 27 in wave 15; the typical player of §7.5 has to deal about 90 damage per second to keep up with wave 15. The numbers leave out what the elements add, which for the Element family is most of the point.

#### Element weapons **[D58]**

A weapon of this family arrives with its element built in:

- its *Infusion* axis counts as taken, so no Infusion card is offered for it;
- the element cannot be replaced;
- it applies the element **twice as fast** as an infused weapon: `stacks = damage ÷ 2.5` (§4.6).

One form for each of the four elements that sit on the target. Earth, Light and Void pay the wielder and fit any weapon, so they come by Infusion only.

| Form · Anomaly | Element | What it does | Backfire | How it grows |
|---|---|---|---|---|
| **Flamethrower · Stream** | Fire | Hold: a cone of flame, range 6 and 40° wide. Every enemy in it takes 2 damage four times a second. | It overheats after 3 s: 1.5 s locked, and 3 Fire on you | Fire only climbs when it arrives faster than it burns off (2 per second). At Mk I it barely does; with *Power* it does, and Burn damage grows with the square of the stacks (§4.6). |
| **Flamethrower · Wildfire** | Fire | Lobs a fireball up to 10 away that leaves a burning patch of radius 2.5 for 4 s: 1 damage four times a second to everything inside. Cooldown 1.5 s; at most 3 patches. | The patches burn you too | Three patches on one corridor mouth triple it; *Reach* widens every patch |
| **Cryo Cannon · Shatter** | Frost | One shard: 18 damage to the first enemy — enough Frost to freeze a small enemy on the spot. A Frozen target takes double, and shatters: 8 damage to enemies within 2.5. Cooldown 1 s. | A shard that hits a wall bursts: 3 Frost to everything within 3, you included | The hard-hitting one: first shot freezes, second shot shatters. *Tempo* shortens the rhythm. |
| **Cryo Cannon · Blizzard** | Frost | Hold: a cloud of radius 3.5 at the cursor, up to 10 away, that follows the cursor at 3 units/s. 1.5 damage four times a second to everything inside. | You move 30 % slower while holding it, and the cloud freezes you as well | At Mk I it freezes a Grunt after about 4 s inside; with *Power*, after 2.5 s. Then everything in it stands still for whatever else you have. |
| **Arc Coil · Chain** | Lightning | A bolt to the nearest enemy within 8 in front of you, which jumps to up to 4 more within 4 of each other: 6 damage each. Cooldown 0.9 s. | Jumps that find no enemy come back to you: 3 damage each | *Reach* adds two jumps. After a freeze the element's own rule spreads the stacks further (§4.6). |
| **Arc Coil · Capacitor** | Lightning | It charges as you walk: 1 charge per unit, up to 30. Click: everything within 5 takes half the charge as damage, up to 15. | Carry a full charge for more than 4 s and it goes off on you at half strength | A weapon you load by moving: Move Speed is its Tempo |
| **Spore Launcher · Cloud** | Poison | Lobs a pod up to 10 away: a cloud of radius 3.5 for 5 s, 1 damage twice a second to everything inside. Cooldown 3 s; clouds overlap. | It poisons you too; any healing takes a stack off you (§4.8) | The weakest hit in the game, and the Poison it leaves never fades. Every second an enemy spends in a cloud is damage for the rest of its life. |
| **Spore Launcher · Needler** | Poison | Hold: 8 needles a second at one target within 12, 2 damage each. Clip of 24, reload 2 s. | You move at 60 % speed while it reloads | 6 Poison stacks a second on one body: the answer to Brutes, Shields and the final boss — unless it is immune |

#### Killing weapons **[D58]**

Offered late: Minigun and Swarm Launcher from wave 6, Railgun from wave 11. They carry no element until infused.

| Form · Anomaly | What it does | Backfire | How it grows |
|---|---|---|---|
| **Railgun · Lance** | After a 0.5 s wind-up, a slug pierces a thin line for 30 units: 36 damage to the first enemy, a quarter less to each one after it. **It is not reduced by Shield** (§4.7). Cooldown 2.2 s. | The recoil knocks you back 4 units: a real knockback, so a wall behind you hurts | The answer to armour: Stompers, a Warden's group, the Tyrant |
| **Railgun · Execute** | One target: 24 damage, and a non-elite left below 40 % HP dies. **Each kill adds 1 damage to this weapon for the rest of the run, up to +24.** Cooldown 1.2 s. | A shot that does not kill jams it for another 1.5 s | It grows with the run itself: a weapon picked up in wave 11 and fed kills is twice as strong by the last fight |
| **Minigun · Spin-up** | Hold: the rate of fire climbs from 4 to 12 bullets a second over 3 s. 3 damage each, range 14, no knockback. | Your move speed drops to 40 % as it spins up; letting go loses the spin | Stand your ground and it out-damages everything on one target. Any element card turns twelve hits a second into stacks. |
| **Minigun · Meltdown** | 10 bullets a second from the first shot, 3 damage each. Heat fills in 4 s of firing and drains when you stop. | At full heat it explodes: 20 damage within 4 to everything, you included, then 3 s of cooldown | The explosion is a backfire, or a bomb you carry into a crowd |
| **Swarm Launcher · Barrage** | 8 small rockets fan out over 50°, up to 9 units. Each bursts on the first enemy it meets: 4 damage within 1.5. Cooldown 1.8 s. | A rocket that meets nothing turns round and comes back to you | Eight separate hits: *Reach* makes it eleven, and an Infusion puts stacks on every one |
| **Swarm Launcher · Carpet** | Six blasts walk away from you in a line, from 2 to 12 units out, one every 0.15 s: 6 damage within 2 each. Cooldown 2.5 s. | The first blast lands 2 units in front of you: walk forward and you are in it | A lane-clearer: almost nothing on a single enemy, the most damage in the game on a column |

#### Trick weapons kept from draft 4 **[D46]**

Four questions describe what a trick weapon does; no two rows answer all four alike.

| # | Form · Anomaly | Shape | Timing | Displacement | Aim |
|---|---|---|---|---|---|
| 1 | Rifle · Charged Shot | Line | Charged | Pushes the target | You |
| 2 | Rifle · Reverse Shot | One target | Instant, sustained | Pushes **you** | You, backwards |
| 3 | Knife · Throwing Knife | Chain | Instant, returns | None | The weapon picks the next target |
| 4 | Knife · Hook | One target | Held, then released | Carries the target, then throws it | You |
| 5 | Rocket Launcher · Strike | Area | Delayed | Pushes targets outward | You, by a point on the ground |
| 6 | Rocket Launcher · Launch Yourself | Your own body | Instant | Moves you | You |
| 7 | Grav Gun · Attract | Area | Lingers 1.5 s | **Pulls** targets to a point | You |
| 8 | Grav Gun · Repel | Area around you | Instant | Pushes targets, and you | None: centred on you |
| 9 | Plasma Launcher · Sticky | One target, then area | Delayed | Pushes targets | You, then the carrier |
| 10 | Plasma Launcher · Bouncy | Area | Delayed | Pushes targets | You, off walls |
| 11 | Scatter Cannon · Blast | Cone | Instant | Pushes targets, and you | You |
| 12 | Scatter Cannon · Suction | Cone | While held | Pulls targets to you | You |
| 13 | Phase Dagger · Swap | One target | Instant | You and the target trade places | You |
| 14 | Phase Dagger · Recall | A point, then area | Two steps: plant, recall | Moves you to the dagger | You, in advance |

What the eight kept anomalies do. Concepts; each needs its own spec before it is built.

| Form · Anomaly | Effect | Backfire | Terrain exploit |
|---|---|---|---|
| Grav Gun · Attract | For 1.5 s, everything within 4 of the point is dragged to it at 5 units/s | You are dragged too, at half strength | Put the point on a black hole |
| Grav Gun · Repel | Heavy knockback in a ring of radius 4 around you | Flings XP orbs away as well, and shoves you 2 back | Fire it next to a hole or a wall |
| Plasma Launcher · Sticky | A slow blob sticks to the first enemy; after 2 s it bursts: radius 3, 12 damage, Medium | The carrier runs at you half as fast again | Stick it to an enemy in a crowd — or hook the carrier and fling it |
| Plasma Launcher · Bouncy | A grenade with Ricochet 3; it bursts on an enemy or after 3 s | It can bounce back to your feet | Bank it round a corner |
| Scatter Cannon · Blast | A short cone, range 5, 60° wide: 8 damage, Heavy knockback | The recoil throws you 3 back | Empty a crowd into a hole; use the recoil to leave a corner |
| Scatter Cannon · Suction | While held, a cone of range 8 drags enemies to the muzzle at 4 units/s; no damage | Everything it catches ends up in your face | Gather them into a line, switch weapon, one Charged Shot — or one Carpet |
| Phase Dagger · Swap | Throw the dagger; on a hit you and the target trade places | You arrive where it stood | Swap with an enemy at the edge of a crowd to get out; leave an enemy where you were standing — in a gravity zone |
| Phase Dagger · Recall | The dagger stays where it lands. Use again to appear there: 8 damage and Medium knockback within 2 | Until you recall it you have no dagger; it does not come back by itself | Plant it across a hole, cross when cornered |

**Build order [P].** The Element forms first — Flamethrower and Spore Launcher need only a cone, a patch and the status system (TECH-BACKLOG T18). Then Minigun and Swarm Launcher, which reuse the bullet and the blast. Then Grav Gun and Scatter Cannon, which fill the largest gaps among the tricks (pulling and cones). Cryo Cannon, Arc Coil, Railgun, Phase Dagger and Plasma Launcher after that.

### 5.5 Auto weapons **[D12] [D22] [P: this list]**

Rules [P]: an auto weapon triggers on its own timer during WaveActive and the void trial; it is revealed the first time it triggers; it is affected by Damage, Attack Speed and Knockback; it grows by Mk tier at the Armory, and its only draft upgrade is Infusion (§5.8, V4); like every weapon it has two anomalies and a backfire.

| Form · Anomaly | What it does | Backfire |
|---|---|---|
| Orbit Blades · Saw | Two blades circle you at radius 2.5. Touch: 4 damage, Light knockback outward | Also bats XP orbs away; can push the 20th enemy into a hole when you did not want it |
| Orbit Blades · Magnet | Same orbit. Touch: 4 damage, and the enemy is pulled 1.5 toward you | Drags enemies into your face — or into position for a Charged Shot |
| Sentry Drone · Gunner | Every 1.2 s, shoots the nearest enemy within 10: 5 damage, no knockback | Each shot nudges **you** 0.5 the other way |
| Sentry Drone · Whirl | Every 5 s, grabs the nearest enemy within 6 and whirls it around you for 2 s, hitting others (5 damage, Medium), then lets go | You do not choose where it lets go; the whirled enemy blocks your shots |
| Shield Ring · Repulsor | Every 4 s, Medium knockback to everything within 3 | Pushes orbs away and scatters the line you were building |
| Shield Ring · Leech | Enemies within 3 lose 1 HP per second; every 5 HP drained gives 1 Energy | Your stamina recovers 25 % slower |

*Shield Ring · Leech* is one concrete form of the "equipment with an energy-leech attribute" in **[D16]**.

### 5.6 Armory **[D21]**

After every wave — the last one too, before the final boss — the Armory offers **3 weapons of three different forms**. Take one, or skip. Weapons no longer lie on the ground. (V4; draft 5 offered 2 unrevealed weapons.)

| Detail | Rule | Tag |
|---|---|---|
| What is shown | The form, whether it is a hand or auto weapon, and its Mk. Not the anomaly of a new weapon. An upgrade offer shows the held weapon and its current → new Mk. | [D21] |
| Pool | After waves 1–2: the three starting forms. After 3–5: + the four Element forms and the auto forms. After 6–10: + Grav Gun, Scatter Cannon, Phase Dagger, Plasma Launcher, Minigun, Swarm Launcher. After 11–15: + Railgun (§5.4). | [P] |
| Guarantee | If a slot of one kind is empty, the first offer fits it; if both kinds have an empty slot, the first two offers cover both | [P] |
| Mk | A new weapon arrives at the standard Mk for the wave: one tier every three waves (waves 1–3 Mk1, 4–6 Mk2, … 13–15 Mk5). Damage × 1.00 / 1.40 / 1.65 / 1.90 / 2.10 for Mk1–Mk5, × 2.30 for Mk6, which only an upgrade reaches. Draft 5 instead gave Act II and III offers one or two random upgrade axes. | V4 |
| Upgrade offer | Slot 1 is always a new form. Slots 2 and 3 each have a 50 % chance to show a stronger Mk of a weapon the player holds instead. Taking it raises that weapon to max(current Mk + 1, standard Mk + 1), up to Mk6, so a lagging weapon catches up to one tier above the wave in one pick. The weapon keeps its anomaly, element and upgrades and needs no slot. | V4 |
| Anomaly | Rolled among the anomalies of that form the player does not already hold (R7). A form the player already holds is not offered as a new weapon. | [P] |
| Free slot | The weapon goes there | |
| No free slot | The player chooses which weapon of that kind to replace. The old weapon and its upgrades are gone. Cancel returns to the offers. | [D31] |
| Skip | Nothing is gained | [D21] |

Why Mk tiers exist: by Act II all five slots are usually full, and a Mk I weapon can never beat a weapon that has been upgraded for six waves. Without them the Armory stops mattering halfway through the run. Since V4 the Mk tier is also where a weapon's damage growth comes from.

**Starting weapon [D20].** On the loadout screen the player picks Rocket Launcher, Rifle or Knife. It is Mk I and unrevealed.

**Codex [P].** Once an anomaly has been revealed in any run, the loadout and Armory screens list both possible anomalies under a form the player has fully discovered. Stored locally.

### 5.7 Edge cases

| Situation | Rule |
|---|---|
| An unrevealed weapon is in a slot when a draft is rolled | No Weapon card is offered for it (§7.2) |
| The first use of an unknown weapon backfires fatally | That is the game. The loadout screen says so once. |
| Both hand slots hold weapons on cooldown | The player waits or uses skills; Fists are only for an empty slot |
| A weapon with a projectile out is replaced in the Armory | Cannot happen: projectiles are removed at wave end (§2.2) |
| Own bullets after a ricochet | Never hit the player. Explosions always do. [P] |
| A skill is cast while an enemy is held on the hook | Allowed; the enemy stays held [P] |
| The held enemy dies (a Burn tick, someone else's blast) | The hook lets go and ends; nothing is flung [1.0] |
| The wave ends while an enemy is held | It dissolves with the others (§2.2) |

### 5.8 Weapon upgrades **[D15] [D46] [D47] [P: the axes]**

**Intent [D46].** An upgrade should change one thing about a weapon, and each kind of upgrade should change a different thing. Then any set of upgrades makes sense together, and two players holding the same weapon end up with different ones.

**V4 (2026-10-14).** Pure number growth moved out of the draft. A weapon's damage grows with its **Mk tier**, bought at the Armory (§5.6). Draft Weapon cards only change behaviour or element: **Infusion, Twist and Tame**. Each can be taken once per weapon; Infusion can be taken again, to change the element. Draft 5 also offered Power, Tempo and Reach.

| Axis | The one thing it changes | General rule | For |
|---|---|---|---|
| **Infusion** [D47] | What the weapon leaves behind | Puts one element on the weapon (§4.6). A weapon holds one element at a time; a new Infusion replaces the old one. | Hand and auto |
| **Twist** | How it behaves | One new behaviour, different for every anomaly | Hand |
| **Tame** | What it costs you | Removes or softens the anomaly's backfire | Hand |
| **Mk** (Armory, not a draft card) | How hard | Damage × 1.00 / 1.40 / 1.65 / 1.90 / 2.10 / 2.30 for Mk1–Mk6 (§5.6) | Hand and auto |

No longer offered in drafts (draft-5 values, kept for reference): **Power**, damage +40 %; **Tempo**, cooldown, charge time and reload −30 %; **Reach**, range, radius or number of targets +40 %.

Why the axes do not overlap: Infusion changes the *payload*, Twist the *rule*, Tame the *price*, and Mk the *size of the number*. None can stand in for another. (With six on-or-off axes and seven elements, draft 5 counted 256 versions of every weapon.)

Twist and Tame for the six existing anomalies:

| Anomaly | Twist | Tame |
|---|---|---|
| Charged Shot | *Ricochet Round*: a full charge bounces off hard surfaces twice | *Recoilless*: full speed while charging |
| Reverse Shot | *Ricochet Rounds*: bullets bounce three times | *Braced*: no recoil |
| Throwing Knife | *Wall Bank*: the knife also ricochets off walls, twice | *Clean Kill*: the corpse it drags no longer hits you |
| Hook | *Grapple*: if the hook hits a wall, it pulls **you** there, without damage | *Dazed*: a flung enemy that survives is stunned for 1.5 s where it lands |
| Strike | *Implosion*: the blast pulls toward its centre instead of pushing out | *Friend-or-Foe*: your own strike does not hurt you; it still pushes you |
| Launch Yourself | *Shock Landing*: Heavy knockback within 3 where a dash ends in the open | *Crash Helmet*: walls do not hurt you |

**Additional upgrades approved by the team on 2026-10-12.** The 22 new hand-weapon anomalies of §5.4 now each have one approved Twist and one approved Tame: 44 additions. Their individual mechanisms and adjustable candidate values are defined in [`DESIGN-DECISIONS.md`](DESIGN-DECISIONS.md) §3. This resolves Q4; it does not establish runtime correctness or balance. Auto weapons take only Infusion from drafts. Do not enable an upgrade before its actual behaviour and card description are connected.

What Reach meant for each of them in draft 5 (no longer offered): Charged Shot, range 40 → 56; Reverse Shot, range 40 → 56; Throwing Knife, 4 → 6 targets; Hook, range 10 → 14 and crash reach 1.5 → 2.1; Strike, blast radius 3 → 4.2; Launch Yourself, distance 15 → 21.

Rules:

- A Weapon card (§7.2) offers Infusion, Twist or Tame, whichever the weapon has not taken. An Infusion card names its element; if the weapon already has one, the card says which element it would replace.
- A weapon's Mk is shown with its name. The draft-5 Armory marks, which arrived with one or two random axes already taken, are replaced by Mk tiers (§5.6).
- Upgrades belong to the weapon and are lost with it **[D31]**. An Armory Mk upgrade keeps them.
- Auto weapons take only Infusion from drafts; they grow by Mk like hand weapons.
- A weapon with a built-in element (§5.4) arrives with its Infusion axis taken. Its element cannot be replaced, and it applies it twice as fast **[D58]**.
- A new anomaly only needs its Twist and its Tame written. Infusion and Mk work from its numbers.

---

## 6. Skills and Energy

**Intent.** Skills are the player's few big moments per wave. Energy makes each one a decision; the level-up refill makes sure they are spent.

### 6.1 Energy **[D13] [D16]**

- **Locked** until the player takes their first skill. The bar then appears, full.
- **Max 100** [P].
- **Every time the XP bar fills, Energy is set to max**, at that moment, in the middle of the fight **[D16] [D27]**. That is each of the 9 level-ups, and each Stat card earned after level 10 **[P]**.
- Other sources exist only if the build includes them **[D16]**: Energy Siphon (a bonus on Max Energy cards), a weapon infused with Void (§5.8), the *Devour* skill, auto weapons that leech (*Shield Ring · Leech*).
- A hit from a Void-affixed enemy takes 10 Energy away (§4.8) **[D50]**.
- Energy above max is lost. Energy does not change between waves.
- After the void trial, Interstellar Energy becomes **Void Energy [D19]**: the bar turns purple and regenerates 4 per second [P]. The level-up refill still applies.

### 6.2 Skills **[D15]**

| Rule | Tag |
|---|---|
| One key press casts the skill once, immediately, at or toward the cursor. No channelling, no toggles, no passive skills. | [D15] |
| At most 3 skills, on keys 1, 2, 3. | [D15] |
| A cast costs Energy. Not enough Energy → no cast, a short "no energy" cue, nothing is spent. | [D16] |
| Skills have levels 1 to 3. A Skill card for an owned skill raises its level. | [D15] upgradable; [P] three levels |
| With 3 skills owned, taking a new one means choosing which to replace (§6.4). | [D15] |
| Each skill also has a short cooldown (2 s unless stated) so a held or doubled key press cannot empty the bar. Energy stays the real limit. | [P] |
| Skills can be cast while Knocked. A slower rule would make *Blink* useless at the moment it matters most. They cannot be cast while Dashing or in menus. | [P] |
| A skill aimed at an invalid point — inside a wall, a black hole, outside the boundary — uses the nearest valid point along the line from the player. If there is none within 0.5, the cast is refused and nothing is spent. | [P] |

### 6.3 Skill pool **[D46] [D51] [P: this list]**

**Intent [D46] [D51].** Seven skills each express one element; six are pure mechanics with no element. No two skills do the same job: each has its own *verb*. And each element skill delivers its element in a *form* that no weapon upgrade can give — a trail, a contagion, a wall.

**Element skills**

| Skill | Element | Cost | Verb | Level 1 | Level 2 | Level 3 |
|---|---|---|---|---|---|---|
| Flame Wake | Fire | 25 | Deny ground | For 4 s you leave a burning trail behind you; an enemy touching it gains 3 Fire per second | 6 s | The trail is twice as wide |
| Contagion | Poison | 20 | Spread | Put 6 Poison on one enemy. For 8 s, whenever a poisoned enemy dies, its Poison jumps to the nearest enemy within 5 | 12 s | It jumps to the two nearest |
| Frost Nova | Frost | 30 | Stop | 6 Frost to every enemy within 4 — enough to freeze small enemies at once | Radius 5.5 | Enemies it freezes are knocked 50 % further while Frozen |
| Storm Call | Lightning | 30 | Disrupt at range | Over 1.5 s, 3 bolts strike random enemies within 5 of the cursor: 6 damage and 3 Lightning each | 5 bolts | Additional level-3 upgrade remains undecided. Basic Lightning already travels between targets; Frost strengthens accumulation and range under the 2026-10-12 rule (§4.6). No level-3 draft is offered until its distinct upgrade is defined. |
| Bulwark | Earth | 30 | Build | A wall 4 wide at the cursor for 5 s — a hard surface: enemies slam into it — and you gain 10 Shield | 8 s and 15 Shield | It shoves whatever touches it when it ends (Medium) |
| Radiance | Light | 30 | Recover | Clear every status on you. For 5 s your hits heal you for 25 % of their damage | 7 s | Also 20 HP at once |
| Devour | Void | 10 | Finish | One non-elite enemy within 6 that is below 30 % HP is swallowed: no XP, and you gain 20 Energy. If a black hole is within 10, its counter gets +1. Cooldown 6 s. | Below 40 % HP | Cooldown 4 s |

**Mechanic skills**

| Skill | Cost | Verb | Level 1 | Level 2 | Level 3 |
|---|---|---|---|---|---|
| Blink | 20 | Relocate | Teleport up to 6 toward the cursor; cancels a knockback in progress | Up to 8 | Leaves a decoy for 2 s where you were |
| Shockwave | 30 | Push | Heavy knockback to every enemy within 4 | Radius 5 | Also reflects enemy projectiles |
| Gravity Well | 35 | Pull | For 3 s, enemies within 3 of the point are pulled to it at 4 units/s | 4.5 s | Ends with a Launch knockback outward and 10 damage |
| Time Slip | 40 | Slow time | Everything except you runs at 30 % speed for 2.5 s | 3.5 s | Your Attack Speed +50 % during it |
| Decoy | 20 | Lure | A dummy that enemies chase for 3 s | 5 s | Explodes at the end (8 damage, Medium) |
| Overcharge | 25 | Hasten | Your hand weapons' cooldowns run twice as fast for 4 s | 6 s | Knockback +50 % during it |

**Thirteen verbs, none shared:** deny ground, spread, stop, disrupt at range, build, recover, finish; relocate, push, pull, slow time, lure, hasten. The average cost is about 26.

*Devour*, together with a Void-infused weapon (§5.8), is the "skills and gear that give Energy back" of **[D16]**.

**What the rules produce together.** None of these needs a rule of its own; they follow from each element doing exactly one thing:

- Frost Nova, then Storm Call: the bolts jump through everything the nova touched.
- Gravity Well on a Flame Wake trail: the pull holds them in the fire.
- Bulwark behind a crowd, then a hook fling: a slam and a rebound off a wall that was not there a second ago.
- Contagion, then Devour on the poisoned enemy: it dies, its Poison jumps, and you are paid in Energy.
- Radiance while poisoned: cleansed at once, and every heal after it removes another stack.

**Greater Void skills [D19].** Offered only after the void trial, three at once, pick one. They never appear in normal drafts.

| Greater Void skill | Cost | Effect |
|---|---|---|
| Pocket Void | 50 | At the cursor, a black hole in a 3 × 3 footprint with a 1-wide gravity ring for 4 s. Swallowed enemies give no XP and count toward nothing. It can swallow the caster. The latest circular-hole direction applies to its footprint. [Revision 2026-10-12] |
| Void Walk | 40 | Blink; leave a small black hole at both the departure and arrival point, each for 2 s. [Revision 2026-10-12] Candidate Inspector values: blink distance 8, hole diameter 3, gravity ring 1. Only these two holes spare their own caster while they exist, to prevent an unavoidable death at the blink destination; other holes remain dangerous. [P: implementation safeguard] |
| Singularity | 60 | Draw every on-screen enemy 6 grid units toward the player and slow it. The player cannot move or operate for 2 s; then explode, with damage based on the number of distinct enemies absorbed nearby. [Revision 2026-10-12] Candidate Inspector values: slow multiplier 0.5, blast radius 6, damage = 10 + 4 × distinct enemies pulled into that radius. Pull is distributed over the 2 s; immovable actors are not teleported. [P: values and exact counting] |

### 6.4 Replacing a skill

1. In a draft, the player picks a *new skill* card while already holding three.
2. An overlay shows the three slots: key, skill name, level.
3. Clicking a slot (or pressing 1 / 2 / 3) puts the new skill there at level 1. The old skill and its levels are gone.
4. *Back* returns to the three cards. No card has been spent.

### 6.5 Does the Energy budget work?

A check by arithmetic, using §7.5.

- With 10 levels **[D36]** a run has 8 to 11 refills: about 1 000 Energy for a typical player before any leech. The average cost in the pool is about 26, so that is **roughly 38 casts in a whole run**.
- They are front-loaded. A typical player levels 4 times in Act I, 3 times in Act II and twice in Act III, and earns one Stat card in the last wave. So the last five waves — 250 s of the hardest fighting — bring **three refills: about 11 casts**.
- Void Energy at 4 per second **[P]** adds 1 000 Energy over those same 250 s: **about 38 more casts on top of those 11**. This is what makes the void trial the run's one big mid-game upgrade **[D37]**, and what *"shapes the late build"* **[D19]** means in numbers.
- The other way out is leech **[D16]**: Energy Siphon at +0.6 per death is worth about 50 Energy in an Act III wave of 85 kills; a Void-infused weapon gives about 170 in the same wave if it deals half of the damage (§4.6); *Devour* nets 10 Energy per cast on a 6 s cooldown, about 80 in a 50 s wave at most; *Shield Ring · Leech* adds to it. Each is weaker than the trial, and without the risk.

So the design as decided makes skills plentiful early, scarce late, and gives the player two ways to fix that. Whether the scarce stretch reads as "I need to solve this" or as "my skills stopped working" is an assumption **[A]** (§14). If it reads badly, the smallest fix is a partial refill at the start of each wave (§13).

### 6.6 Edge cases

| Situation | Rule |
|---|---|
| Level-up before any skill is owned | No Energy effect; the level is still banked |
| The first skill is taken in a build phase | The bar appears full for the next wave |
| A card raises Max Energy | Energy is not refilled by the card itself |
| The trial is beaten by a player with no skill | The Greater Void skill pick is their first skill; the bar appears, already purple |
| A key is held down | One cast per press; no auto-repeat |
| Time Slip is active and the player stands in a gravity zone | Gravity on the player is not slowed |
| A skill is replaced while on cooldown | The new skill starts ready |
| The player is Paralysed | No casting until it ends: at most 1 s (§4.8) |
| *Devour* has no valid target | The cast is refused; nothing is spent |
| *Radiance* is cast at full HP | Statuses are still cleared; healing above max HP is lost |
| *Bulwark* is placed on top of enemies | They are pushed out to the nearer side; no damage [P] |

---

## 7. XP, levels and cards

**Intent.** Killing is how you grow, and enemies never run out: kill faster, grow faster **[D41]**. The first levels come quickly so every run gets a build going; the last ones have to be earned **[D36]**. Growth is spent between fights, one card at a time, always choosing between a number, a weapon and a skill.

### 7.1 XP and levels

- Enemies killed **by damage** drop XP orbs where they die, by type (§9.1): from 1 for a Grunt to 5 for a Spire. An elite drops 5 times its type's value; an affixed enemy 1 more (§9.5). [P]
- Enemies swallowed by a black hole drop **nothing** **[D32]**. Enemies that dissolve at wave end drop nothing.
- Orbs within Pickup Range fly to the player. Orbs inside a gravity zone drift toward the hole at 3 units/s and are lost if they reach it **[D06] [D25]**. Whatever is still on the ground at wave end is collected (§2.2) [P].

**The level curve [D36].** The top level is **10**. Each level costs about 65 % more than the one before:

`XP from level L to L + 1 = 10 × 1.65^(L − 1)`, rounded. [P: 10 and 1.65]

| Level reached | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|
| XP for this level | 10 | 16 | 27 | 45 | 74 | 122 | 202 | 333 | 549 |
| XP in total | 10 | 26 | 53 | 98 | 172 | 294 | 496 | 829 | 1 378 |

The last two levels cost 64 % of the total. Two numbers hold the whole curve: the first level's cost (10) sets how soon the first card comes; the growth factor (1.65) sets how hard the end is.

**After level 10 [D40].** The bar keeps filling. Every **549 XP** — the price of the last level — gives one more draft, but of **three Stat cards** instead of one of each kind [P: three to choose from; 549, not growing]. The level stays 10. Growth is no longer capped, only narrowed to numbers.

**When the bar fills**, at once, in the middle of the fight: Energy to max **[D16]**, one draft banked **[D27]**, banner and sound. One pickup can fill the bar more than once; each fill banks its own draft.

**The badge [D39].** The level number sits next to the XP bar. At level 10 it reads "10 · MAX". If the player has also entered a black hole and beaten the hidden boss, the 10 is drawn in **rainbow colours** — on the HUD and on the result screen. It is the run's one visible mark of "did everything".

### 7.2 The draft **[D09] [D27]**

For each banked level the build phase shows **three cards, one of each kind**. The player takes one.

| Slot | Card | When that kind is not available |
|---|---|---|
| A | **Stat**: raises one stat (§7.3) | — |
| B | **Weapon**: Infusion, Twist or Tame (§5.8) for one revealed weapon that has not taken it yet; an auto weapon is only offered Infusion. An Infusion card names its element **[D47]**. | A second Stat card, for a different stat |
| C | **Skill**: a new skill, or +1 level for an owned skill | A second Stat card, for a different stat |

How slot B is filled [P]: a revealed weapon is picked at random, then one of the axes it has not taken; if that axis is Infusion, the element is random. A weapon that already has an element is offered a different element 15 % of the time instead of a new axis, and always once its other axes are all taken; the card says which element it replaces **[D47]**.

How slot C is filled [P]:

| Skills owned | Roll |
|---|---|
| 0 | Always a new skill |
| 1 or 2 | 70 % new skill, 30 % level-up of an owned skill that is below level 3 |
| 3 | 60 % level-up of an owned skill below level 3, 40 % new skill (which means a replacement, §6.4). If all three are level 3: always a new skill. |

- The three cards in one draft are always different. The same card can come back in a later draft.
- A draft cannot be skipped; a Stat card is never a bad pick. [P]
- **Reroll**: once per build phase, replaces all three cards of the draft on screen. [P]
- **If the build clock runs out**, the draft is picked at random among the cards that need no further choice (§2.3) **[D38]**.
- **After level 10** the draft is three different Stat cards (§7.1) **[D40]**.

### 7.3 Stat cards **[P]**

With 10 levels a run has only 9 normal drafts, so one Stat card has to matter. Draft 5 raised the values by about half over draft 2.

**V4 (2026-10-14).** Fourteen card types were merged into seven stronger cards plus Element Penetration; most of the small stats now come as a second effect on one of the seven. These are the values in `Data/ProgressionRules.asset`; the derivation is in [`V4-NUMBERS.md`](V4-NUMBERS.md) §3.

| Card | Common | Rare | Exceptional | Also grants (Common / Rare / Exceptional) |
|---|---|---|---|---|
| Max HP | +15 | +25 | +40 | +1 / +1.5 / +2.5 HP every 5 s (Regeneration) |
| Move Speed | +8 % | +12 % | +16 % | +0.2 / +0.3 / +0.45 s sprint (Stamina) |
| Damage | × 1.12 | × 1.18 | × 1.25 | +8 % / +12 % / +16 % crit chance; crits deal × 2, cap 60 % |
| Attack Speed | +15 % | +22 % | +32 % | — (cooldown = base ÷ (1 + sum of the bonuses)) |
| Knockback | +20 % | +35 % | +50 % | +3 / +5 / +8 wall-slam damage (Impact), grows with the wave's enemy HP multiplier |
| Protection (armor) | +25 | +40 | +60 | — (damage taken × 100 ÷ (100 + armor)) |
| Max Energy \* | +25 | +40 | +60 | +0.4 / +0.7 / +1.0 Energy per kill (Energy Siphon) |
| Element Penetration \*\* | +20 % | +35 % | +50 % | — |

A Legendary card gives the Exceptional value × 1.2. Regeneration, Stamina, Impact, Pickup Range, Luck and Energy Siphon are no longer offered as cards of their own.

What one Common card does: Damage gives × 1.21 expected damage (including the crit chance); Attack Speed gives cooldown × 0.87; 25 armor gives damage taken × 0.80 (effective HP +25 %), and two cards give × 0.67 (+50 %). Division keeps the effective-HP gain per armor point constant while the percentage shrinks, so armor can never stack to immunity.

\* Only once Energy is unlocked. \*\* Only from wave 11, when the final boss's immunities are known, and only to a player who owns a weapon or a skill of an immune element (§9.7) **[D56]**. A stat at its cap (§3.3) is no longer offered.

### 7.4 Rarity **[P]**

Only Stat cards have a rarity. `Rare % = 25 + Luck` (cap 55), `Exceptional or Legendary % = 5 + Luck ÷ 2` (cap 20), Common is the rest. One in five of the high-tier cards is Legendary, a 1 % base rate. If 8 Stat cards in a row were neither Exceptional nor Legendary, the next one is. (Draft 5 called the high tier Epic and had no Legendary. Since V4 no card raises Luck, so the odds stay at their base values.)

### 7.5 Does the level budget work?

Because enemies never run out **[D41]**, XP is no longer limited by how many enemies a wave holds but by **how fast the player kills**. The model below therefore starts from a kill rate. It is an assumption, written down so it can be replaced by measurements:

`kills per second in wave w = k × (0.4 + 0.12 × (w − 1))`, with k = 0.6 for a slow player, 1.0 for a typical one, 1.5 for a fast one. For the typical player that is 12 kills in wave 1 and 104 in wave 15. **[A]**

Computed with a script from that rate, the mix of §9.4 and the curve of §7.1:

| Player | Kills in the run | Level 5 reached in | Level 8 in | Level 10 in | Stat cards after level 10 | Fills of the bar (= Energy refills) |
|---|---|---|---|---|---|---|
| Slow (k = 0.6) | 480 | Wave 6 | Wave 11 | Never: ends at 9 | 0 | 8 |
| Typical (k = 1.0) | 800 | Wave 5 | Wave 9 | Wave 13 | 1 | 10 |
| Fast (k = 1.5) | 1 200 | Wave 4 | Wave 8 | Wave 12 | 2 | 11 |

The level after each wave, for all three, is in the wave table (§9.4).

- **Early is fast, late is slow.** The typical player levels 4 times in the first five waves, 3 times in the next five, twice in the last five. Every player has a draft after wave 2 at the latest.
- **Killing faster helps, within limits.** A player who kills two and a half times as fast as the slow one reaches level 10 in wave 12 instead of never — and then keeps earning Stat cards. The positive loop **[D41]** is real, and the curve keeps it from running away.
- **Drafts are few.** 8 to 11 in a run, at most 2 in one build phase; the last of them are shown in the build phase after wave 15, before the final boss. A full build would need 2 × 6 + 3 × 4 = 24 weapon upgrades and 9 skill picks. Nobody gets close; every draft is a real choice between kinds.
- **What feeding costs.** 40 feeds **[D37]** cost about 52 to 56 XP in Act I, 74 to 84 in Act II, 84 to 98 in Act III. In the middle of a run that is the price of one level (level 6 costs 74). So opening the door means **giving up about one mid-game level for the chance at the trial's reward** — which is the trade **[D32]** and **[D37]** describe.

### 7.6 Edge cases

| Situation | Rule |
|---|---|
| No weapon is revealed yet | Slot B is a second Stat card |
| Every revealed weapon has taken every axis | Slot B offers one of them a different element (§7.2) |
| The bar fills during wave 15 | The draft is shown in the build phase before the final boss (§9.7) |
| Two levels banked | The drafts are shown one after another; the reroll is still one for the whole phase |
| A Weapon card is taken for a weapon in a holstered slot | Allowed; the card names the weapon |
| The bar fills from orbs collected at wave end | The draft is banked and shown in the build phase that follows |
| The player dies with drafts banked | They are lost with the run |
| Level 10 is reached with XP left over | The remainder counts toward the first Stat card |
| Every stat is at its cap after level 10 | The draft offers Max HP cards; that stat has no cap |
| The rainbow badge: level 10 first, or the boss first? | Either order. It turns rainbow the moment both are true. |

---

## 8. World

**Intent.** The map gives the player places worth fighting near. A black hole is the best weapon on the field and the most dangerous spot on it. Being far from one is safe and slow; being close is fast and risky.

### 8.1 Map and grid

- The map is a rectangle of **100 × 70 cells**, 1 cell = 1 unit [P]. Cell (0, 0) is the south-west corner. The player starts at the centre.
- **Camera [P].** It follows the player with the 1.0 angle and distance, and stops at the map edge so that no more than 4 units outside the boundary are ever visible. With the 1.0 camera settings (height 46, pitch 74°, field of view 40°, 16:9) the visible ground is about **62 wide × 37 deep**. *This corrects draft 1, which said 30 × 17.*
- So the map is 1.6 screens wide and 1.9 screens deep: never all visible, crossed corner to corner in about 20 s of walking.
- Black holes and walls off screen are shown by markers on the screen edge (§10.1).

### 8.2 Boundary **[D05] [D28]**

- The edge of the map blocks movement. Reaching it never kills.
- It is a hard surface: slam, wall damage and rebound apply exactly as for a wall (§4.3).
- Projectiles end there unless they have Ricochet.

### 8.3 Walls **[D07]**

- Wall pieces inside the map: straight runs of 6 to 10 cells, L shapes, 2 × 2 pillars. About 10 per map. [P]
- They block movement, projectiles and the Throwing Knife's search for its next target [1.0].
- Layout rules [P]: no passage narrower than 4 cells anywhere (a Brute is 2 wide and has to path through); every black hole anchor has a wall 8 to 12 cells away, so there is always a "backboard" for a rebound shot; no wall inside a gravity zone.

### 8.4 Black holes **[D06] [D17] [D24] [D25] [D32] [D57]**

**Shape.** A black hole is a square of **n × n cells**, aligned to the grid **[D17]**. Each run has **two: a large one of 7 × 7 and a small one of 3 × 3** **[D57] [P: the two sizes]**. Odd sizes, so that the hole has a centre cell for the portal. The large one is easy to feed and hard to keep away from; the small one is the opposite.

**Placement [P].** The map has 6 hand-placed anchors. A run picks 2 at random, one for each size. Anchor rules: at least 14 cells from the player's start, 10 from the boundary, 24 from each other.

**Kill rule [D06].** Anything whose centre is inside the square is swallowed: the player (the run ends), enemies, XP and HP orbs, projectiles. A projectile is therefore lost if its path crosses a hole — holes block shots, though not sight.

| Exceptions | |
|---|---|
| A Hooked enemy | Cannot be swallowed while held [1.0]; it is swallowed if thrown in |
| The Throwing Knife | If it crosses a hole it is back in its slot 1.5 s later [P]; hand weapons are never lost |
| A Dashing player | Swallowed if the dash crosses the hole [1.0] |

**Gravity zone [D17] [D25].** Around each hole is a zone in which everything drifts toward the hole's centre at **3 units per second** — half the player's base speed of 6, so walking straight out still makes headway at 3 **[D17]**.

- **Latest fixed-map-hole revision, 2026-10-12:** extend the gravity border by one grid unit, from 2 to 3. The player must sprint to escape radially; while not Sprinting, clamp their outward radial velocity to at most zero, so Move Speed upgrades alone do not bypass that rule. Current pull-speed candidate: 6 units/s, changed from the earlier candidate 8. At base speed 6 and sprint multiplier 2, the ideal full-width time is 3 / (12 - 6) = 0.5 s, matching the initial sprint duration. This is a paper calculation, not a guarantee under slows or other movement effects. Sprint and Launch Yourself displacement retain their escape routes; teleport keeps its legality checks. Temporary skill holes retain their separately specified 1-unit gravity ring and pull speed 3.
- It acts on **every unit** **[D25]**: the player, enemies, orbs. It does not bend projectiles [P].
- Characters that are Knocked or Thrown cannot resist it (§3.4). A knock that ends inside the zone is usually a kill: over the 0.8 s of a Medium knock the drift alone adds about 2.3 units toward the hole.
- `gravityCells` is now 3 for fixed map holes; pull speed 6 is an adjustable, untested candidate. The older width-2/pull-3 escape-time calculation below is retained only as historical rationale and is superseded by the new sprint-only radial escape rule. It is not a measurement of the current game.

| Historical unit | Historical walk speed | Historical speed against pull 3 | Historical time to walk out of a 2-cell zone |
|---|---|---|---|
| Player | 6 [1.0] | 3 | 0.7 s (0.2 s sprinting) |
| Shade | 8 [P] | 5 | 0.4 s |
| Skitter | 7.5 [P] | 4.5 | 0.4 s |
| Grunt | 5.4 [1.0] | 2.4 | 0.8 s |
| Warden, Splitter | 4.5 [P] | 1.5 | 1.3 s |
| Spitter, Rammer | 4 [P] | 1 | 2 s |
| Brute | 3.6 [1.0] | 0.6 | 3.3 s |
| Bloater | 3.5 [P] | 0.5 | 4 s |
| Stomper | 3.2 [P] | 0.2 | 10 s: in practice it does not get out |
| Spire | 0 | — | Never in one: it does not spawn in a zone and cannot be moved |

**Feeding [D24] [D32].**

- Each hole has its own counter, shown above it, and keeps it for the whole run.
- Every living enemy the fixed hole swallows adds 1, whoever or whatever put it there. An elite counts as 1; direct live swallowing gives no XP. A damage-killed corpse entering that hole also adds 1, but its death rewards have already been settled and are never paid again. One enemy spawn lifetime contributes at most one feed in total, including live swallowing and later corpse movement. Temporary skill holes never count. [Revision 2026-10-12]
- At **40** the hole collapses into the portal (§8.5). **[D37]** [P: the same for both sizes]
- The number is set so that the door opens in the **middle of a run** for a player who feeds one hole on purpose — the target is waves 6 to 9 **[A]** — and costs about one level's worth of XP (§7.5).

**Lifecycle [D57].**

```
 Active ──counter reaches 40──▶ Collapsing (1.5 s) ──▶ Portal (open for 3 waves)
                                                          │              │
                                                          │ E            │ the 3 waves pass
                                                          ▼              ▼
                                                      VoidTrial        Gone
                                                          │ won
                                                          ▼
                                                        Gone
```

| State | Kills | Pulls | Counts feeds | Notes |
|---|---|---|---|---|
| Active | Yes | Yes | Yes | |
| Collapsing | Shrinking area still kills | No | No | Enemies entering now die with no credit |
| Portal | No | No | No | One cell, the centre cell. Open for three waves (§8.5). |
| Gone | No | No | No | Ordinary floor for the rest of the run: after the trial is won, or after the portal has run out |

**The other hole.**

- While a portal is open, the other hole still kills and pulls but does not count feeds: one door at a time. [P]
- **If the portal runs out unused, that hole is gone and the other one stays [D57].** It keeps the count it had and counts again, so the run has one more chance at the trial. [P]
- If the trial is won, the other hole stays as terrain for the rest of the run and no longer counts. There is one void trial per run **[D24]**.
- So a run can end with two holes on the map, with one, or with none — none only if both doors were opened and both were left to run out.

### 8.5 The void portal **[D18] [D37] [D57]**

- One cell, where the hole's centre was. It does not pull and does not kill. **[D18]**
- **It stays open for three waves [D57].** The wave in which it opens does not count; it closes when the third wave after that one ends. The number of waves left is shown over the portal and next to the arrow that points to it. A portal that would outlast wave 15 closes when the final boss arrives (§9.7).
- Entering is a choice: stand on the cell and **press E** **[D37]**. A prompt says that losing the trial ends the run. Walking, being knocked or dashing across the portal does nothing; the player has to be in the Normal state [P].
- An arrow on the screen edge points to it whenever it is off screen.
- When the three waves are over, the portal closes and the hole is gone for good. The 40 feeds that opened it are spent. **[D57]**

**What it is for [D37].** The portal is the run's optional mid-game upgrade. Opening it costs about a level (§7.5); going in risks the whole run **[D26]**; coming out gives regenerating Energy and a Greater Void skill **[D19]** — the answer to the Energy drought of the late waves (§6.5). The three waves are time to get ready: one or two more drafts, an Armory offer or two, full HP at the start of each wave. After that the offer is withdrawn, and the hole with it — so letting it pass costs a piece of terrain as well.

### 8.6 The void trial **[D08] [D19] [D26]**

The trial is the hidden level. This section is a concept at rule level; it needs its own spec before it is built. **[P]** throughout, except where tagged.

**Stakes [D26].** If the player dies in the trial, the run is over.

**Place.** A round platform, radius 12, floating in the void. Where the battlefield's boundary is a wall that never kills, **the platform's edge is the void: falling off is death.** The outer 2 units are a gravity zone that pulls outward at 3.

**The battlefield** is frozen while the player is away: wave timer, enemies, projectiles, orbs. On return the player stands where the portal was, with 2 s of protection.

**The boss — working name "The Hollow".** It is built to test exactly what opening the portal required: pushing things over an edge.

| Rule | Detail |
|---|---|
| HP | `300 × the enemy HP multiplier of the wave in which the portal was entered` — an early entry is neither trivial nor hopeless |
| Knockback | It cannot be moved. Each hit adds its knockback distance to a **stagger** meter; at 40 the boss is stunned for 3 s and takes +50 % damage, then the meter resets. Knockback builds stay useful. |
| Its own spawn | It summons Grunts. **Each one the player knocks off the platform costs the boss 5 % of its max HP.** Feeding the void is the main way to win. |
| Phase 1, HP 100–60 % | *Slam*: a circle on the ground for 0.8 s, then Heavy knockback outward. *Summon*: 4 Grunts every 10 s. |
| Phase 2, HP 60–30 % | The platform shrinks from 12 to 9 over 10 s. *Inhale*: for 2 s everything is pulled toward the boss at 4; touching it costs 10 HP and a Heavy knockback. |
| Phase 3, HP < 30 % | The platform shrinks to 7. Attacks come 30 % faster. Skitters replace Grunts. |
| Fairness | Every attack has a ground telegraph of at least 0.8 s. The boss never uses two knockback attacks within 1.5 s. After a Slam it stands still for 1.5 s. |
| Elements | Burn and Poison work on it. It cannot be Frozen or Paralysed: each time it would be, its stagger meter gets 15 instead (§4.6). Its summons follow the normal rules. |
| What does not work here | Launch Yourself deals 40 instead of killing (§5.3). The summoned enemies give no XP, so no levels and no refills are gained inside. |

**Rewards [D19].**

1. Interstellar Energy becomes **Void Energy**: the bar turns purple and regenerates (§6.1).
2. A pick of **1 of 3 Greater Void skills** (§6.3). With three skills already owned, it is a replacement (§6.4); it can be declined.
3. HP restored to max. [P]
4. The hole is gone. The other hole stays as terrain and stops counting.

**The price [D45].** From then on the player carries Void (§4.6): Light-affixed enemies, which appear from wave 11, deal +50 % damage to them.

### 8.7 Biomes **[P]**

A biome is a *look and a layout*, chosen per run. It changes no rule. One biome is enough for the first playable; the system is worth building because it is the cheapest way to make runs feel different. Engineering notes are in `TECH-BACKLOG.md`.

A biome defines: floor, wall, boundary and black-hole art; ambient colour; its set of wall layouts and black-hole anchors; weights that tilt the enemy mix (§9.4) by up to ±10 points per type.

| Biome | Look | Layout character | Enemy tilt |
|---|---|---|---|
| Dust Flats | The 1.0 sand and brick | Open; few, long walls | None |
| Wreck Field | Hull plates and debris | Many short walls and pillars: slams and ricochets | More Rammers |
| Rift Zone | Dark glass, purple light | Black-hole anchors closer together | More Skitters |

### 8.8 Edge cases

| Situation | Rule |
|---|---|
| Two holes reach 40 in the same physics step | The first one processed collapses. The other stops at 39 and waits: it counts again if that portal runs out unused (§8.4). |
| Several enemies enter on the step the counter reaches 40 | The counter stops at 40; the others die with no credit |
| The 40th enemy goes in at a bad moment — an auto weapon pushed it, or the player has 5 HP | The player has the rest of this wave and three more to get ready (§8.5). |
| The player never enters | After three waves the portal closes and that hole is gone. The cost was the XP of 40 enemies and a piece of terrain. The other hole can still be fed. |
| The portal opens in the last seconds of wave 15 | The player may still enter: the wave timer is frozen during the trial. On return the wave resumes and ends. A portal still open after that closes when the final boss arrives. |
| A skill or weapon would place something inside a hole | §6.2: nearest valid point, or the cast is refused |
| An orb lies in a gravity zone at wave end | It is collected with the rest (§2.2) |
| The player enters the portal with statuses on them | They are cleared on entering. Statuses on the enemies left behind are frozen with them. [P] |
| The second hole reaches 40 after the first portal ran out | It opens its own portal, for three waves, under the same rules |
| Both holes are gone | The map has no black hole for the rest of the run. Knockback builds are left with walls; the choice was the player's. |

---

## 9. Enemies and waves

**Intent.** Each enemy type asks one question. The mix decides which questions are asked together.

### 9.1 Roster **[D46] [D53] [D54] [P]**

**Intent [D46].** Every enemy type has one job that no other type has. If two types could be swapped without the player changing what they do, one of them should not exist.

The two 1.0 enemies keep their values. The names are working names for the alien castes.

| Enemy | Its job | HP | Speed | What it does | Size factor | XP |
|---|---|---|---|---|---|---|
| **Grunt** (1.0 small) | Close pressure | 10 [1.0] | 5.4 [1.0] | Walks up, winds up 0.5 s, punches: 5 damage, Medium [1.0] | 1.0 | 1 |
| **Brute** (1.0 large) | Mass | 30 [1.0] | 3.6 [1.0] | Winds up 0.8 s, punches: 15 damage, Medium [1.0] | 0.5 [1.0] | 4 |
| **Skitter** | Speed, in numbers | 5 | 7.5 | Comes in packs of 4 to 6; bite: 3 damage, no knockback | 1.5 | 1 |
| **Spitter** | Range | 12 | 4 | Keeps 8 to 10 away; spits a slow blob (7 units/s): 6 damage, Light | 1.0 | 2 |
| **Rammer** | A straight line | 25 | 4 | Shows a line for 0.8 s, then charges 12 units at 16 units/s: 12 damage, Heavy | 0.75 | 3 |
| **Bloater** | Its own death | 14 | 3.5 | No attack. It explodes when it dies or reaches you: radius 3, 12 damage, Medium, to **everything**, other enemies included. A wall slam or a hook crash sets it off too. | 1.0 | 2 |
| **Warden** | Protecting others | 20 | 4.5 | No attack. It stays behind its group. Every ally within 5 has a Shield of 30 % of its max HP, renewed every 4 s (§4.7). | 1.0 | 3 |
| **Splitter** | What comes after | 18 | 4.5 | Punches like a Grunt. Killed by damage, it breaks into 3 Skitters. Swallowed by a black hole, it does not. | 1.0 | 1, plus its Skitters |
| **Spire** | Ground | 40 | 0 | It does not move and cannot be moved: no knockback, no hook, no feeding. Every 3 s a ring warns for 0.8 s, then 8 damage and Medium knockback to everything within 6. | — | 5 |
| **Stomper** **[D53]** | Moving **you** | 30 | 3.2 | Walks up. Within 4 of the player it rears up for 1.0 s while a ring shows its reach, then pounds the ground: 6 damage and **Heavy knockback** to everything within 5, other enemies included. It **starts with a Shield of half its max HP** (§4.7). | 0.5 | 4 |
| **Shade** **[D54]** | Being noticed in time | 6 | 8 | It announces itself: its marker has its own colour and sound. It stands visible for 2 s where it appears, then **cloaks** and only a shimmer shows. It shows itself for 0.6 s before it strikes: **30 damage** — three fifths of the starting HP — and Light knockback. Any damage or status reveals it for 2 s. | 1.5 | 3 |

HP is multiplied by the wave's HP multiplier (§9.4). Damage is not scaled in this draft.

**What each one asks, what answers it, and what the terrain has to do with it:**

| Enemy | The question it asks | Good answers | Terrain |
|---|---|---|---|
| Grunt | Can you step out of a telegraphed punch? | Movement | The standard thing to feed to a hole |
| Brute | It barely moves when hit. Now what? | Sustained damage, Poison, the hook, a wall | Hard to push; flung with the hook, it crashes twice as hard |
| Skitter | They are faster than you walk. | Areas, cones, Frost | Knocked 50 % further: one push can feed several |
| Spitter | Can you fight while dodging from range? | Close the distance, break line of sight | Walls and black holes stop its blobs |
| Rammer | Where will you be standing? | Sidestep with a wall or a hole behind you | A wall stuns it; a hole swallows it |
| Bloater | Do you really want to kill it *here*? | Push it away first; kill it when it is among the others | A flung Bloater is a grenade |
| Warden | Which one do you kill first? | Reach it, pull it out, or use Poison | Hook it out of the group. Its shields cut direct damage by 30 % and halve Fire. |
| Splitter | Kill it, or get rid of it? | Kill it for XP and take the Skitters, or feed it and take neither | The one enemy a hole handles better than a gun |
| Spire | Can you fight somewhere else — or break it? | Raw damage, Poison, or leave | The one enemy terrain cannot touch |
| Stomper | Where will its wave throw you? | Stay out of the ring, or stand where the push sends you somewhere safe. Poison goes through its Shield; a Railgun Lance ignores it. | Its wave throws enemies too: let it pound next to a black hole |
| Shade | Did you see it arrive? | Kill it in its first 2 s: it has 6 HP. After that, anything that covers an area or leaves a status shows it. | A Burn or a Poison keeps it visible; a wall does not hide its shimmer |

**Eleven jobs, none shared:** pressure, mass, speed, range, line, death, protection, aftermath, ground, displacement, attention.

Element affixes (§4.8) are a second, independent layer. Eight of the eleven types can carry any of the seven affixes — Spires, Wardens and Shades never do (§9.5) — which gives 56 affixed variants on top of the 11 plain ones, from 18 definitions.

### 9.2 Behaviour rules

- **Telegraphs [1.0].** Every attack shows before it lands: the Grunt's yellow wind-up with locked facing, the Rammer's line, the Spitter's slow blob, the Bloater's swelling, the Spire's ring, the Stomper's ring, the Shade's uncloaking. Walking away from a locked wind-up dodges it.
- **Pathing [P].** Enemies path toward the player around walls, black holes and gravity zones. (1.0 walks in a straight line and slides along edges; that does not survive interior walls. See `TECH-BACKLOG.md`.)
- **They never walk into a black hole or its gravity zone on their own [D25]** — with one exception.
- **Exception: the player is inside a gravity zone [P].** Enemies whose walk speed is at least 1.5 × the pull (Grunts, Skitters, Splitters, Shades) follow the player in. Slower ones wait at the edge. Without this rule the zone would be a place where melee enemies can never reach the player. With it, standing in the zone baits fast enemies to the brink — and their punches push the player inward (§4.5).
- **A Rammer's charge does not steer [P].** Once it starts it ignores holes, zones and walls. A charge into a wall: twice the wall damage and 1.5 s stunned. A charge into a hole: swallowed, and it counts as a feed.
- **Inside a zone, not by choice.** An enemy that ends a knock inside a zone walks out by the shortest way. The table in §8.4 shows who makes it.
- **A Bloater walks straight at the player [P].** It swells for 0.4 s, visibly, before it blows up — whether it reached the player or was killed. Swallowed by a black hole, it does not explode.
- **A Warden stays back [P].** It keeps 4 to 6 units behind the nearest group of allies and backs away from a player closer than 6. It never attacks.
- **A Splitter's Skitters [P]** appear where it died and cannot bite for 0.5 s. They count toward the number the wave keeps alive.
- **A Spire [P]** appears on a marker like any enemy, at least 8 units from any black hole or wall. It does not count toward the number the wave keeps alive; at most 2 stand at once.
- **A Stomper [D53]** walks at the player and pounds when it is within 4. The ring is on the ground for the whole 1.0 s. The wave pushes outward from the Stomper and does not hurt it. It pounds again no sooner than 4 s later.
- **A Shade [D54].** Its marker has its own colour and sound and lasts 1.5 s instead of 1 s. It always appears 12 to 16 units from the player. For 2 s it stands still and visible; then it cloaks and comes. Cloaked, it is drawn as a shimmer that is easier to see the closer it is. It shows itself for the 0.6 s of its wind-up and for 1 s after the strike, then cloaks again. At most one is alive in waves 6 to 8, two from wave 9. [P: the numbers]

### 9.3 Spawning **[D41] [P: numbers]**

**Revision, 2026-10-12:** spawning is organized into batches within one timed wave. While the wave has time left, clearing the current living batch immediately starts preparing another batch. At time 0, spawning stops; the wave ends only after all remaining living enemies are cleared. This replaces the earlier one-enemy-at-a-time top-up and timer-only ending rules.

- **Batch refill.** Prepare a new batch only after the current batch has no living enemy and no pending hatch. This uses the existing bounded saved enemy/marker pools; do not treat pool saturation as a reason to create hidden runtime enemies.
- Existing per-wave targets and mixes remain the starting batch-size configuration: `10 + 2 × (wave − 1)`, unless later tuned. Batch count does not increment the numbered wave or its scaling.
- A red X retains its existing 1 s spawn warning. "Immediately refill" schedules the next batch when cleared; it does not remove the spawn warning or change any existing duration number.
- Position: 8 to 16 units from the player — on screen, so the X is seen — at least 6 from the player [1.0], never in a wall, a black hole or a gravity zone, never where the player stands [1.0].
- The current requirement is spawning around the player's current position. Remaining off-screen enemies have screen-edge direction markers so cleanup does not become an unmarked search. Exact marker grouping and spawn-ring tuning are implementation candidates, not new wave progression.
- An enemy more than 40 units from the player for 5 s is removed and replaced nearer.
- Elites and Spires come at fixed times, on top of the target (§9.4, §9.5).

### 9.4 Wave table **[D03] [D23] [D38] [P: numbers]**

"Kills" and "Level after" come from the kill-rate model of §7.5; they are expectations to test, not rules.

| Wave | Fight | Enemies kept alive | Enemy HP | Elites | Build time after | Kills (typical) | Level after: slow / typical / fast |
|---|---|---|---|---|---|---|---|
| 1 | 30 s | 10 | × 1.00 | 0 | 20 s | 12 | 1 / 2 / 2 |
| 2 | 30 s | 12 | × 1.12 | 0 | 20 s | 16 | 2 / 3 / 4 |
| 3 | 30 s | 14 | × 1.24 | 0 | 25 s | 19 | 3 / 4 / 4 |
| 4 | 30 s | 16 | × 1.36 | 0 | 25 s | 23 | 4 / 4 / 5 |
| 5 | 30 s | 18 | × 1.48 | 0 | 30 s | 26 | 4 / 5 / 6 |
| 6 | 40 s | 20 | × 1.60 | 1 | 30 s | 40 | 5 / 6 / 7 |
| 7 | 40 s | 22 | × 1.72 | 1 | 35 s | 45 | 6 / 7 / 7 |
| 8 | 40 s | 24 | × 1.84 | 1 | 35 s | 50 | 6 / 7 / 8 |
| 9 | 40 s | 26 | × 1.96 | 1 | 40 s | 54 | 7 / 8 / 8 |
| 10 | 40 s | 28 | × 2.08 | 1 | 40 s | 59 | 7 / 8 / 9 |
| 11 | 50 s | 30 | × 2.20 | 2 | 45 s | 80 | 8 / 9 / 9 |
| 12 | 50 s | 32 | × 2.32 | 2 | 45 s | 86 | 8 / 9 / 10 |
| 13 | 50 s | 34 | × 2.44 | 2 | 50 s | 92 | 9 / 10 / 10 +1 |
| 14 | 50 s | 36 | × 2.56 | 2 | 50 s | 98 | 9 / 10 / 10 +2 |
| 15 | 50 s | 38 | × 2.68 | 3 | 55 s | 104 | 9 / 10 +1 / 10 +2 |

"10 +1" means level 10 and one Stat card earned after it. About 480, 800 and 1 200 kills in a run for the three players. Enemy HP grows 12 % of the base per wave. Build time: 545 s in total if fully used, counting the build phase after wave 15. Then comes the final boss (§9.7).

**Mix**, in percent of spawns:

| Waves | Grunt | Skitter | Bloater | Spitter | Warden | Shade | Rammer | Splitter | Stomper | Brute |
|---|---|---|---|---|---|---|---|---|---|---|
| 1–2 | 90 | — | — | — | — | — | — | — | — | 10 |
| 3–5 | 55 | 25 | 10 | — | — | — | — | — | — | 10 |
| 6–8 | 32 | 20 | 10 | 15 | 5 | 3 | — | — | — | 15 |
| 9–11 | 22 | 18 | 10 | 15 | 5 | 4 | 10 | 5 | 3 | 8 |
| 12–15 | 12 | 18 | 10 | 13 | 5 | 5 | 10 | 10 | 7 | 10 |

Each band adds two or three types: one that presses the player in a new way (Skitter, Spitter, Rammer), one that changes what a kill means (Bloater, Warden, Splitter), and from wave 6 one that punishes a lapse of attention (the Shade, then the Stomper).

**Spires [P]:** two per wave from wave 11, at one quarter and at half of the wave's time. They are outside the mix and outside the number kept alive.

For comparison, 1.0 has 5, 10 and 20 enemies in its three waves and at most 12 alive.

### 9.5 Elites and affixed enemies **[D50] [P: numbers]**

**Elites.** From wave 6, elites appear at fixed points in the wave: halfway; in Act III at one third and two thirds; three in wave 15. An elite is one of the wave's types — never a Spire or a Shade — with 3 × HP, +50 % damage, half the knockback, a bright outline and an arrow on the screen edge. It always carries one element affix. It drops 5 × the XP and one HP orb (+10 HP).

**Affixed enemies (§4.8).** An ordinary enemy can spawn with one element affix.

| Waves | Share of spawns that carry an affix | Affixes in the pool |
|---|---|---|
| 1–4 | None | — |
| 5–7 | 10 % | Fire, Poison, Frost |
| 8–10 | 15 % | + Lightning, Earth |
| 11–15 | 25 % | + Light, Void |

- A wave uses at most two different affixes, chosen when it starts and shown in the wave banner, so the player can read what is coming.
- Spires, Wardens and Shades never carry an affix: the first two already change how elements work, and a Shade is dangerous enough as it is.
- An affixed enemy gives 1 more XP.

### 9.6 Edge cases

| Situation | Rule |
|---|---|
| The player runs for the whole timer and kills nothing | At time 0 spawning stops, but the remaining living enemies must still be cleared before settlement. |
| Every remaining enemy is waiting at the edge of a zone | Zone behavior stays (§9.2); timer 0 alone does not settle. Off-screen markers point toward survivors. |
| A batch is cleared before timer 0 | Immediately prepare another batch in the same numbered wave, retaining spawn warnings and existing kill rewards; no build or wave-scaling advance. |
| An elite is swallowed | One feed, no XP, no HP orb |
| An enemy is stuck behind geometry | The 40-unit / 5 s rule recycles it |
| A Bloater is killed next to the player | The blast hits the player too: 12 damage, Medium. Push it away first. |
| A Bloater's blast kills other enemies | They drop XP as usual: the kill is the player's [P] |
| A Bloater is swallowed | No blast; one feed |
| A Warden's allies leave its range | The Shield they have stays until it is used up; it is no longer renewed |
| Two Wardens cover the same enemy | One Shield, not two; they share the renewal timer [P] |
| A Splitter is flung by the hook and dies in the crash | It splits where it dies |
| A Spire stands where the player wants to fight | It has 40 HP × the wave's multiplier and cannot be moved: break it, or fight somewhere else. Its ring hits enemies too. |
| An affixed enemy is swallowed | One feed; no XP, as for any enemy |
| A cloaked Shade is caught by an area attack aimed at something else | It takes the damage and is visible for 2 s |
| A Shade's strike lands on a player who has Shield | 30 is cut to 21, Shield first (§4.7) |
| Two Shades strike half a second apart | The second lands after the protection window: 60 in all. This is why at most two are alive, and why each announces itself. |
| A Stomper pounds while the player is Frozen | The player is thrown all the same: being Frozen does not stop a knockback (§4.9) |
| A Stomper's wave throws a Bloater | The Bloater flies, and blows up if it slams into a wall (§9.1) |

### 9.7 The final boss **[D56] [P: everything not tagged]**

**When [D56].** Wave 15 is played to the end like any other wave. Then comes one more build phase — 55 s, with the Armory and any drafts still banked — and then **the Tyrant** (working name). There is no timer: the run is won when it dies and lost when the player does.

**Intent.** The last fight tests the *build*, the way the void trial tests pushing. So the Tyrant is one target with a lot of HP and a Shield (does the build have anything that hits one thing hard, or goes through armour?); it calls crowds (does it have anything wide?); and it is immune to three elements (did the build lean on one thing?).

| Rule | Detail |
|---|---|
| HP | 4 000. For the typical player of §7.5 that is a fight of a little over two minutes; about a minute and a half for the fast one, three and a half for the slow one **[A]**. |
| Shield **[D55]** | 400 at the start of each of its three phases. While it lasts: direct damage −30 %, Burn halved, Poison unaffected (§4.7). |
| Immunities **[D56]** | **Three of the seven elements, rolled for each run.** They are named in the banner of wave 11 and stay on the HUD from then on. An element it is immune to does nothing to it: no stacks, and no payout to the wielder. The hit's direct damage is unchanged (§4.6). The enemies it calls have no immunity. |
| Control | It cannot be Frozen or Paralysed. Each time it would be — and is not immune to that element — its stagger meter gets 15. |
| Knockback | It cannot be moved or swallowed. Each hit adds its knockback distance to a **stagger** meter; at 60 it is stunned for 2.5 s and takes +50 % damage, then the meter resets. |
| *Cleave* | An arc of 150° and radius 5, shown for 0.8 s: 15 damage, Heavy knockback. |
| *Quake* | A circle of radius 8, shown for 1.0 s: 10 damage and Launch knockback outward. |
| *Charge* | A line, shown for 1.0 s, then 20 units at 18 units/s: 20 damage, Heavy. **If it runs into a wall it is stunned for 2.5 s and loses the Shield it has.** The terrain is still a weapon. |
| *Call* | Every 15 s, 6 enemies of the Act III mix at wave-15 HP, without Spires. They give no XP, and they are useful: for payouts, for crashes, for feeding a hole that is still there. |
| Phase 2, HP below 66 % | The Shield is back at 400. Its attacks now carry one of the elements it is immune to, as an affixed enemy's would (§4.8). |
| Phase 3, HP below 33 % | The Shield is back at 400. Everything it does is 25 % faster; *Call* brings 9. |
| Fairness | Every attack is shown on the ground for at least 0.8 s. Never two knockback attacks within 1.5 s. After a *Charge* it stands still for 1.5 s. |
| What the player brings | Full HP, as at the start of any wave. Energy is whatever the bar holds: nothing refills it during the fight except leech, *Devour* on the called enemies, and Void Energy. |

**Element Penetration [D56].** A Stat card (§7.3): +20 % / +35 % / +50 %, up to 100 %. With Penetration *p*, an element the target is immune to works at *p* of its strength: stacks × *p*, payouts × *p*. The card is offered from wave 11 on — when the immunities are known — and only to a player who owns a weapon or a skill of an immune element.

**What a build can do about an immunity:** buy Penetration; replace the element (an Infusion card, another weapon from the Armory, a different skill); or keep it for the crowds the Tyrant calls and bring something else for the Tyrant itself. An immunity never takes a weapon away: its direct damage always lands.

**Edge cases**

| Situation | Rule |
|---|---|
| A portal is still open when wave 15 ends | It closes when the Tyrant arrives (§8.5) |
| A black hole is still on the map | It stays. The Tyrant cannot be swallowed: a *Charge* that reaches a hole stops at its edge and counts as running into a wall. The player can still fall in. |
| The Tyrant is immune to Void and the player has Void Energy | Regeneration is not a hit: it keeps working |
| The Tyrant takes Light as its phase-2 element and the player carries Void | Its hits deal +50 % to the player (§4.6) |
| The player owns only immune elements | The card is always offered in that case; taking it is still a choice, and direct damage still works |
| The player dies on the frame the Tyrant dies | Death is checked first: defeat (§2.5) |

---

## 10. UI

**Intent.** Everything the player needs to decide is on screen at the moment of deciding: how close the refill is, what each hole stands at, what a card will change.

### 10.1 HUD

| Area | Shows | States |
|---|---|---|
| Top left | HP bar and number, with Shield as a grey bar over it; Stamina bar; Energy bar; an icon and a stack count for each status on the player | Shield: hidden at 0. Energy: hidden while locked; blue; purple with a slow fill animation once it is Void Energy. Stamina: grey while exhausted [1.0]. |
| Top, full width | XP bar, level badge, one pip per banked draft | Flashes when it fills. Badge: the level number; "10 · MAX" at level 10; the 10 in **rainbow colours** once the void boss is also beaten **[D39]**. After level 10 the bar shows progress to the next Stat card. |
| Top centre | Wave *n* / 15 and the timer. From wave 11: the icons of the three elements the final boss is immune to **[D56]**. In the final fight: the Tyrant's HP and Shield instead of the timer. | Last 5 s pulse. Frozen and greyed during the trial. |
| Bottom centre | Two hand slots: form, anomaly name or "?", cooldown or clip, Mk and level, and the icon of its element once infused; the active one is highlighted. The right mouse button switches. Charge bar while charging [1.0]. | Empty slot shows "Fists" |
| Bottom left | Three auto slots, small | Empty / unrevealed "?" / named |
| Bottom right | Three skill slots: key, icon in its element's colour, cost, level | Greyed when Energy is short; a sweep while on cooldown; empty |
| In the world | Above each black hole: its counter, "7 / 40". On the portal: "E — enter the void" while the player stands on it. Enemy HP bars [1.0], with Shield as a pale segment and, above the bar, an icon and a stack count per status. An affixed enemy has a coloured outline and its element's icon. Damage numbers [1.0]; Burn and Poison ticks in their element's colour. | While a portal is open the other hole's counter is hidden. Over the portal: the waves it has left **[D57]**. |
| Screen edge | Arrows to off-screen black holes (dim), the portal (bright, with its waves left), elites, a Shade's marker (in its own colour) | |
| Hint line | While an enemy is held on the hook: left click flings it where you aim [in the 1.0 HUD; text updated] | |
| Centre | Wave banner [1.0], which from wave 5 names the wave's affixes (§9.5); weapon reveal [1.0]; "LEVEL UP — Energy restored"; "The void opens" | One at a time, newest wins |

### 10.2 Screens

| Screen | Opens when | Shows | Input | Leaves to |
|---|---|---|---|---|
| Loadout | A run starts | Three forms, a line of story, a line warning that effects are unknown | Click a form, or 1 / 2 / 3 | WaveIntro |
| *Every build screen* | | **The build clock** (seconds left, turning red under 5) and a **Start next wave** button **[D38]** | Space, or click the button | WaveIntro; anything unpicked is resolved as in §2.3 |
| Armory | Build phase begins | Three offers: form, hand or auto, Mk; an upgrade offer shows current → new Mk. A Skip button. The current loadout. | Click an offer or Skip | Replace overlay, or the drafts |
| Replace weapon | An offer is taken with no free slot of its kind | The weapons of that kind with their levels | Click one to replace it; Back | Drafts; or back to the Armory |
| Draft | Once per banked level | Three cards. Each names its kind, what changes, and for Weapon and Skill cards the before → after values. An Infusion card shows its element and, if the weapon has one, the element it replaces. Reroll button with its count. "Draft 2 of 3" when several are banked. | Click a card, or 1 / 2 / 3; R to reroll | Next draft; Replace skill; or Ready |
| Replace skill | A new-skill card is taken with three skills owned | The three slots | Click a slot or 1 / 2 / 3; Back | Next draft; or back to the cards |
| Pause | Esc | Resume, Restart, Quit; the current build | | |
| Result | Victory or defeat | Outcome and cause, wave reached, level, the build, feeds per hole | R | Loadout |

Rules for every menu [P]:

- The battlefield is frozen. In a build phase the build clock keeps running; in the pause menu nothing runs.
- **A menu ignores input for 0.4 s after it opens and until the left button has been released.** The player is very likely holding fire when a wave ends.
- Keyboard and mouse both work; nothing needs a drag or a double click.

### 10.3 Readability

- A black hole's art shows its exact kill square; the gravity zone has a visible edge. What you see is what kills you.
- Unknown is always "?"; revealed names never change colour or position.
- The whitebox / illustrated toggle stays [1.0]; every new piece of content needs a whitebox look first.
- **One colour and one icon per element**, the same everywhere: weapon and skill slots, stacks, outlines, damage numbers. A status is never shown by colour alone: the icon says what it is, and the number next to it says how many stacks. [P]

---

## 11. Data schema

Field lists for the content that designers tune. Each row of content is one asset; nothing below should be hard-coded. Types are indicative.

**WeaponForm**

| Field | Type | Notes |
|---|---|---|
| id | text | `rifle`, `knife`, `rocket_launcher`, … |
| displayName | text | |
| kind | Hand / Auto | |
| anomalyA, anomalyB | → Anomaly | Exactly two (R2) |
| firstAct | 1–3 | Earliest act in which the Armory offers it |
| icon, whiteboxShape | | |

**Anomaly**

| Field | Type | Notes |
|---|---|---|
| id, displayName, description | text | Description is shown on reveal |
| cooldown | seconds | |
| clipSize, reloadTime | int, seconds | 0 = no clip |
| holdBehaviour | Tap / HoldToRepeat / HoldToCharge | |
| knockbackClass | None / Light / Medium / Heavy / Launch | Or two classes for a charged weapon |
| ricochet | int | Bounces off hard surfaces |
| twist, tame | → Upgrade | Hand weapons only. Infusion and the Mk tier need no data of their own (§5.8). |
| reachValues | field names | Which of its own values the *Reach* upgrade scales by 40 % |
| *own values* | | Each anomaly keeps its specific numbers, as the six assets do today |

**Upgrade** (a Twist or a Tame): id, displayName, description, the values or behaviour it changes (before → after, for the card text).

**WeaponInstance** (run-time state, not an asset): anomaly, Mk, axes taken (Infusion / Twist / Tame from drafts; the code still knows Power / Tempo / Reach), element (None or one of the seven).

**Element** (seven assets): id, displayName, colour, icon, family (OnTarget / ToWielder).

**StatusRules** (one asset): damagePerStack 5; fire: tickInterval 0.5, maxStacks 20, shieldFactor 0.5; poison: tickInterval 1, damagePerStack 0.5 (rounded up), maxStacks 30; frost: threshold 6, duration 1.5, decayPerSecond 1, immunity 3; lightning: threshold 6, duration 4, twitchEvery 1, twitchLength 0.3, decayPerSecond 1, immunity 3, jumpRadius 4, jumpMax 5, jumpDamage 3; earthShieldShare 0.10, lightHealShare 0.05, voidEnergyShare 0.10, lightAgainstVoid 1.5; shieldDirectFactor 0.7, builtInElementStackFactor 2; largeBodyThresholdFactor 2; for the player: controlMax 1, controlImmunity 4, shieldCapShareOfMaxHp 0.5, shieldDecayDelay 3, shieldDecayPerSecond 1. V4 fields for enemies: fireBurnFraction 0.206, fireDamagePerStack 1.5, poisonOnEnemyDamagePerStack 0.12, poisonOnEnemyCap 20, frostChill 1.5, lightningSlow 0.55, lightningChainFalloff 0.7; lightMissingHpBonus 2; Lightning with Frost: frostLightningJumpRadius 6, frostLightningStackMultiplier 1.5.

**AffixRules**: per wave band, the share of spawns and the pool of elements; maxDifferentPerWave 2; xpBonus 1; per element, what an affixed enemy's hit applies (§4.8).

**Skill**: id, displayName, element (None or one of the seven), isGreaterVoidSkill, cooldown, targeting (Self / Point / Direction), and per level 1–3: cost, description, values.

**StatCard**: stat, valueCommon, valueRare, valueEpic (Exceptional), a bonus stat with its three values, retired (kept for old data, never offered), cap, needsEnergyUnlocked. Legendary = Exceptional × 1.2.

**Enemy**: id, displayName, baseHp, moveSpeed, radius, sizeFactor, xp, attack (damage, windup, interval, range, knockbackClass), behaviour (Melee / Pack / Ranged / Charge / Bomb / Support / Static / Pounder / Assassin), onDeath (None / Explode / Split), immovable, canCarryAffix, canBeElite, startShieldShare, cloak (visibleAfterSpawn, revealTime, windupVisible), maxAlive, entersGravityZone (derived: speed ≥ 1.5 × pull).

**Wave** (one row per wave): duration, aliveTarget, hpMultiplier, mix (weight per enemy id), eliteTimes and spireTimes (lists of fractions of the wave), affixBand, buildTime, armoryPoolAct, armoryMark.

**BlackHoleRules** (one asset): sizes (7 and 3), gravityCells, pullSpeed, feedsToOpen (40), collapseTime, portalWaves (3).

**FinalBoss** (one asset): hp 4000, shieldPerPhase 400, phaseThresholds (0.66, 0.33), immunityCount 3, staggerLimit 60, stunTime 2.5, the attacks with their telegraph times, callInterval 15, callSize (6, 6, 9).

**MapLayout**: width, height, playerStart, walls (list of cell rectangles), holeAnchors (list of cells).

**Biome**: id, displayName, art set, ambient colour, layouts (→ MapLayout list), mixTilt (per enemy id, −10 to +10).

**Rules** (one asset, extends today's `GameManager.Rules`): wallDamage 5 [1.0], wallHitSpeed 3 [1.0], knockDamping 6 [1.0], wallRestitution 0.5, maxSlamsPerKnock 3, protectionTime 0.5 [1.0], maxLevel 10, xpFirstLevel 10, xpGrowth 1.65, xpPerStatCardAfterMax 549, energyMax 100, voidEnergyRegen 4, spawnTopUpPerSecond 6, buildTimeBase 20, buildTimeStep 5 (every 2 waves), menuInputLock 0.4.

The hook's crash values (§5.3) live with its anomaly: impactDamage 12, impactSizeExponent 1, impactRadius 1.5, impactKnockback 5.

---

## 12. Cross-system walkthroughs

Each walkthrough follows one event through every system it touches, to find rules that collide. Where a rule was missing, it has been added above and is referenced. The walkthroughs use the draft-5 numbers; Fire, Poison and Lightning on enemies, stat cards and weapon growth follow V4 since 2026-10-14 (§3.3, §4.6, §5.6, §7.3).

**W1 — An elite dies next to a black hole and its orb is worth two levels.**
1. The killing hit leaves it dead in place (§4.1); the orb drops there, inside the gravity zone.
2. The orb drifts toward the hole at 3. The player's Pickup Range is 2: either the player steps into the zone to get it, or it is lost.
3. Collected: XP crosses two thresholds → 2 drafts banked, Energy set to max once (§7.1).
4. Nothing else changes until the build phase. *No collision.*

**W2 — A full Charged Shot through five Grunts in wave 3 with a hole behind them.**
1. Wave 3 Grunts have 12 HP (× 1.24, rounded). The shot does 15.
2. All five die in place (§4.1): 5 XP, **no feed**.
3. A tap does 6: all five survive with 6 HP and are knocked 5 units; those that reach the zone or the hole are fed, with no XP.
4. *This is the intended choice, and it only exists because of the kill-in-place rule.* By wave 9 a Grunt has 20 HP and a full shot no longer kills: the full charge becomes the feeding shot. The weapon's role flips during the run; whether that reads as depth or as confusion is **[A]**.

**W3 — The wave ends with two drafts banked; the player takes a weapon, then the clock runs out.**
1. Timer 0 → §2.2: the player is safe, enemies dissolve, orbs are collected. Build clock: 25 s (after wave 3).
2. Armory: the player takes a hand weapon with both slots full → replace prompt → picks a slot. 14 s gone.
3. Draft 1 of 2: the player reads the cards. The clock reaches 0.
4. §2.3: draft 1 and draft 2 are each picked at random among cards that need no further choice. Draft 2 rolled a new skill with three skills owned, so its random pick is between the Stat card and the Weapon card.
5. The next countdown lists what was picked. *No collision; nothing earned was lost.*

**W4 — The 40th feed comes from Orbit Blades while the player has 8 HP.**
1. The hole collapses into the portal; the other hole's counter stops and is hidden. The portal shows *3 waves*.
2. The player does not enter yet. Nothing is lost for three waves **[D57]**.
3. Two waves later, with full HP and one more level, the player walks back and presses E. *The auto weapon's unintended feed set a deadline, not a disaster. In wave 1 or 2 the same three waves might not be enough to get ready* **[A]**.

**W5 — The player enters the trial in wave 7 with no skill yet.**
1. Boss HP = 300 × 1.72 = 516. Stagger works; knocking Grunts off works.
2. Winning gives a Greater Void skill as the first skill → Energy unlocks directly as Void Energy (§6.6).
3. The rest of the run has a regenerating bar. *Strong, and earned at real risk [D26].* Watch for "skip skills, rush the door" becoming the only sensible line **[A]**.
4. The price arrives in wave 11: the player now carries Void, so Light-affixed enemies deal +50 % to them (§8.6).

**W6 — Three skills owned, all level 3; the draft rolls.**
Slot C is a new skill (§7.2), which means a replacement, which the player can decline by taking card A or B. *No dead card.*

**W7 — Time Slip is cast, then the wave ends 1 s later.**
Skill effects are removed at wave end with other skill objects (§2.2 step 4); time scale returns to normal before the build phase freezes it. *Requires one owner for the time scale — see `TECH-BACKLOG.md`.*

**W8 — A fast player reaches level 10 in wave 12 and has already beaten the boss.**
1. The bar fills: level 10, Energy to max, one normal draft banked. The badge turns rainbow at once **[D39]**.
2. The bar starts again at 0 / 549 toward a Stat card **[D40]**.
3. In wave 14 it fills: Energy to max, a draft of three Stat cards banked.
4. *No collision.* The player's growth after the cap is numbers only, which keeps a snowballing build from also collecting every skill and upgrade.

**W9 — A Brute is hooked in a crowd and flung point-blank.**
1. The Brute is pulled to 1.6 from the player and held. The player walks it up to the group and clicks **[D43]**: it touches a Grunt almost at once, speed ratio about 0.9.
2. Crash damage = 12 × 2 × 0.81 = 19 to the Brute, the Grunt and every enemy within 2.5 of the Brute's centre; each is pushed about 9 away.
3. Wave-3 Grunts (12 HP) die where they stand and drop XP (§4.1); the Brute (37 HP) survives and stops there.
4. The player, 1.6 away, is untouched. *The strongest use of the hook is also the one that leaves a live Brute at arm's length — its backfire.*

**W10 — A Fire-infused rifle meets a group standing under a Warden.**
1. Wave 7: Grunts have 17 HP (× 1.72); under the Warden each has a Shield of 5 (30 %).
2. A full Charged Shot (15 damage, 3 Fire) goes through four of them. Each has a Shield, so the hit is cut by 30 % to 10: 5 comes off Shield and 5 off HP, leaving each with 12 HP and 3 Fire.
3. Until the Warden renews it (every 4 s) the Shield is gone, so the Burn ticks at full strength: 3 + 2 + 1 = 6 over 1.5 s. Each is left with 6 HP.
4. Had the Shield still been up, each tick would be halved (2 + 1 + 1 = 4) and would come off Shield first. With Poison instead: 3 stacks tick for 2 per second straight off HP, whatever the Warden does; the 12 HP are gone in 6 s. With a Railgun Lance: 36, not cut, and each Grunt in the line is dead.
5. *No collision. The Warden does not make Fire useless; it makes the order matter: break the Shield with the hit, then burn — or kill the Warden first, or bring Poison.*

**W11 — Frost Nova, then Storm Call, in a wave-9 crowd.**
1. Frost Nova (30 Energy): 6 Frost on every enemy within 4. Grunts, Skitters and Spitters are Frozen for 1.5 s; a Brute needs 12 and merely carries the 6.
2. Storm Call (30 Energy) inside those 1.5 s: each bolt deals 6 and puts 3 Lightning on its target, then jumps to every Frost carrier within 4, up to 5 of them: 3 damage and 3 Lightning each.
3. Three bolts land in the same frozen group: an enemy reached by two of them has 6 Lightning and is Paralysed for 4 s as its freeze ends.
4. The result: 60 Energy buys 1.5 s of full stop and then 4 s of interruptions on most of the crowd. The damage is 6 to 9 each, which is little: a wave-9 Grunt has 20 HP. *The pair is control, not a kill; the kill has to come from a weapon or from terrain — a frozen crowd still slides when it is knocked (§4.9).*

**W12 — After the trial, a Light-affixed elite Brute in wave 12.**
1. The player carries Void. The elite Brute's punch is 15 × 1.5 (elite) × 1.5 (Light against Void) = 34 — or 24 if the player has any Shield.
2. It heals itself for the 34 it dealt (§4.8). Its HP is 30 × 2.32 × 3 = 209.
3. What the player can do: not get hit — the wind-up is 0.8 s; push or fling it into a black hole (a swallowed elite is one feed, §9.6); use Poison, which keeps ticking whatever it heals. The player's own Light weapon gets no bonus here: the Brute carries Light, not Void.
4. *This is the price of D19 in numbers: two such punches kill a player with the starting 50 HP. It is announced — the wave banner names Light — and it can be avoided. Whether it reads as a fair price or as a punishment for winning the trial is* **[A]** (A15 in §14).

**W13 — A Shade appears in wave 7 while the player is charging a shot at a group.**
1. Its marker, with its own colour and sound, shows 14 units away for 1.5 s. Then the Shade stands there, visible, for 2 s. It has 6 × 1.72 = 10 HP.
2. The player turns and fires. A tap (6 damage) leaves it at 4; a full charge (15) or two bullets kill it. *Noticing it costs one shot.*
3. If the player does not: it cloaks, covers the 14 units in under 2 s at speed 8, shows itself for 0.6 s and strikes: 30 of the player's 50 HP, or 21 if the player has any Shield.
4. Any area effect during its approach gives it away: a Strike blast, a Blizzard, a Flame Wake trail it crosses. *No collision. The Shade asks for attention, not for damage; a build with no area damage and no status has only the first 2 s.*

**W14 — The Tyrant is immune to Fire, Frost and Void; the player holds a Flamethrower, a Light-infused Rifle, Frost Nova and Devour.**
1. Wave 11's banner shows the three icons. Five waves and about three drafts are left, plus the build phase before the fight.
2. Against the Tyrant: the Flamethrower still does its direct damage and applies no Fire; Frost Nova does nothing to it; Devour could never target it. The Light Rifle works in full. All four still work on the enemies it calls.
3. The drafts now offer Element Penetration. Two Rare cards make 70 %: the Flamethrower then applies 70 % of its Fire. Or the player keeps the Flamethrower for the called crowds and takes a Railgun from the Armory for the Tyrant itself.
4. In the fight the Tyrant's Shield (400 at the start of each phase) cuts the Rifle's hits by 30 % and would halve any Burn. Making it charge into a wall strips the Shield and stuns it for 2.5 s.
5. *No collision. An immunity takes an element away, never a weapon: the worst case is a weapon doing only its direct damage. Whether three immunities out of seven read as a puzzle or as a punishment for the build the game let the player make is* **[A]** (A18 in §14).

---

## 13. Tuning table

The parameters most likely to be changed first, what moving them does, and what to watch.

| Parameter | Start | Raise it and… | Lower it and… | Linked to | Watch |
|---|---|---|---|---|---|
| `gravityCells` | 2 | Holes feel bigger; feeding easier; the player dies to pulls more | Holes are sharp edges; pulls are rarely felt | Pull speed; knock distances | Deaths by swallow per run; how often players leave a zone on purpose |
| `pullSpeed` | 3 | Brutes and Spitters cannot leave; a slowed player dies | The zone stops mattering | Move speeds (§8.4 table) | Whether anything ever walks out |
| `feedsToOpen` | 40 [D37] | The door opens late or never | The door opens before the build can use it | XP cost (§7.5); kill rate | Wave in which the portal first opens (target 6–9); share of runs that open it |
| Kill-in-place (§4.1) | On | — | Off: every kill near a hole also feeds; the XP-or-feed choice disappears | W2 | Do players tap on purpose? |
| XP curve: first level 10, growth 1.65 | | Higher first level: the first card comes later. Higher growth: level 10 moves out of reach. | Lower growth: everyone is level 10 by Act II and the cap stops meaning anything | Kill rate; Energy refills (§6.5); Stat cards after max | Wave in which levels 5, 8 and 10 are reached, per player |
| XP per Stat card after max | 549 | Fewer late Stat cards | Fast players snowball in numbers | | Stat cards earned after level 10 |
| Enemies kept alive | 10 + 2 per wave | More XP for strong builds; more pressure; more load on WebGL | Fights thin out | Top-up rate; frame time | Kills per wave; enemies alive; frame time |
| Top-up rate | 6 per second | Strong builds are never short of targets | Caps how fast anyone can grow | | Kills per second of the best builds |
| Build time | 20 s, +5 s per two waves [D38] | Slower runs, calmer choices | Random picks become common | Number of drafts per phase (at most 2) | Share of drafts picked at random; seconds left when the player starts the wave |
| Hook `impactDamage` | 12 | The hook kills clusters outright | The crash is decoration | Enemy HP per wave | Kills and damage from crashes per use |
| Energy max / skill costs | 100 / 10–40 | More casts per refill | Each cast is precious; hoarding risk | How often the bar fills | Energy left unspent each time the bar fills (T4) |
| Energy at wave start | No refill [D16] | — | A partial refill (for example to 50 %) softens the late drought of §6.5 | Void Energy's value | Casts per wave in Act III, with and without the trial |
| Void Energy regen | 4 /s | The trial decides the run | The trial is not worth the risk | Siphon values | Win rate with and without the trial |
| Wall restitution | 0.5 | Pinball; multi-slams | Walls stop things, as in 1.0 | Max slams per knock | Share of feeds that came off a rebound |
| Enemy HP per wave | +12 % | Kills slow down; feeding gains value | Damage builds trivialise late waves | Wall damage scaling (§4.3) | Time to kill a Grunt and a Brute per act |
| Charged Shot knockback | 5 → 10 | | A long-range, piercing Launch is a lot; try 3 → 8 | | Share of feeds from this weapon |
| Between-wave heal | Full [1.0] | — | 30–50 %: HP carries pressure across waves; Regeneration and Max HP gain value | | HP at the start of each wave |
| Damage per stack | 5 | Elements stop mattering on fast weapons | Everything triggers a status; Fire totals explode, because they grow with the square of the stacks | Enemy HP per wave; Frost and Lightning thresholds | Time to freeze a Grunt and a Brute |
| Frost / Lightning thresholds | 6 / 6 | Control is rare | Near-permanent lock-down in spite of the 3 s immunity | Damage per stack; immunity time | Share of time enemies spend Frozen or Paralysed (target: under 25 %) |
| Wielder shares: Earth / Light / Void | 10 % / 5 % / 10 % | Shield is always full; Light out-heals everything; Void beats the trial | Not worth a card | Regeneration cap; Void Energy regen; the table in §4.6 | Shield uptime; HP healed and Energy gained per wave, by source |
| Light × Void | +50 % | The trial's price becomes a death sentence in Act III | Nobody notices the rule | Elite damage (+50 %) | Deaths to Light-affixed enemies after the trial |
| Affix share | 10 / 15 / 25 % | Every fight is a status fight | Affixes are trivia | The player's protections (§4.8) | Seconds per wave the player spends Frozen or Paralysed |
| Warden's Shield | 30 %, every 4 s | Fire builds stall | Wardens are free XP | Fire halved by Shield | How often the Warden is killed first |
| *Radiance* lifesteal | 25 % for 5 s | One cast is a full heal late in a run | Not worth 30 Energy | Damage per second; Max HP | HP healed per cast, by act |
| Shield's cut of direct damage | 30 % [D55] | Earth and Wardens dominate; small hits feel useless | Shield is only extra HP | Railgun Lance; Poison; Burn halving | Share of the player's damage lost to Shields, per wave |
| Stamina | 0.5 s sprint, 1.5 s recovery [D60] | Sprint becomes a way of moving again | The sprint is too short to cross a gravity zone (0.22 s is needed) | Gravity zone width; Shade speed 8 | How often the bar is empty when the player is hit |
| Throwing Knife damage | 5 [D59] | It one-shots early Grunts again | It is a utility weapon only | Enemy HP per wave; the corpse drag | Kills per throw in Act I |
| Shade | 30 damage, 6 HP, 2 s visible, at most 2 alive | Deaths out of nowhere | It is free XP | Protection time; Shield's cut | Share of Shades killed before they cloak; deaths to Shades |
| Stomper | Shield 50 %, pound every 4 s, Heavy | The player is thrown around constantly | A slow Brute | Wall damage; hole positions | Wall slams and swallows of the player caused by a Stomper |
| The portal's window | 3 waves [D57] | It is as good as permanent | Nobody can get ready in time | Build time; wave length | Share of opened portals that run out unused |
| The Tyrant's HP and Shield | 4 000, 400 per phase | The fight is a slog | The fight is over before phase 3 | Damage per second at wave 15 (§9.7) | Length of the fight; phase in which players die |
| The Tyrant's immunities | 3 of 7, shown from wave 11 | Most builds lose their element | Immunity is trivia | Element Penetration values; re-infusion chance | Share of players who own an immune element; what they do about it |
| Built-in elements stack twice as fast | × 2 | Element weapons need no upgrades | No reason to take one over an infused weapon | Damage per stack; Fire cap | Stacks per second on one target, by weapon |

---

## 14. Assumptions to test

Rules can be checked by reading and by tests. These cannot; they need people playing.

| # | Assumption | How to test | It holds if | It fails if |
|---|---|---|---|---|
| A1 | Real kill rates fall between the slow and fast players of §7.5 | Log kills per wave | The median player reaches level 10 in waves 12–15 | Level 10 by wave 9, or level 7 at the end: retune the curve |
| A2 | 40 feeds make the door a mid-run event | Log feeds per hole over time | Players who feed on purpose open it in waves 6–9 | Nobody opens it, or it opens in Act I |
| A3 | A 2-cell gravity zone is felt and escaped | Watch first-time players; log swallow deaths | They say they felt a pull; most deaths come from knocks, not from walking in | Most deaths are players walking in unaware |
| A4 | Players spend Energy before the bar fills (T4) | Log Energy at the moment of each fill | Median under 30 | Median near max: skills are hoarded |
| A5 | Drafts at wave end do not make level-ups feel empty (T5) | Ask after a session | Players mention the refill | Players ask why nothing happened |
| A6 | Auto weapons stay under 30 % of damage dealt (T3) | Log damage by source | Under 30 % | Players stop aiming |
| A7 | The Charged Shot's flip from killing to feeding reads as depth (W2) | Watch whether players change how they use it mid-run | They start tapping or full-charging on purpose | They do not notice, or call it inconsistent |
| A8 | Three waves are the right window for a door that ends the run if the fight is lost | Share of opened portals entered, and how many waves were left | Between a third and two thirds enter; most use one or two of the three waves | Almost none enter, or everyone enters at once, or most portals run out unused |
| A9 | The Energy drought of the late waves reads as a problem to solve (§6.5) | Ask; log casts per wave in Act III | Players name leech or the trial as their plan | Players say skills "stopped working" |
| A10 | 20 s is enough for an Armory choice and a draft | Log random picks and time left | Under 10 % of drafts are picked at random | Random picks are common in Act I |
| A11 | Endless enemies feel like a reward, not a treadmill (T6) | Ask; watch whether strong builds get bored or get greedy | Players push for more kills | Players stand still and wait for the timer |
| A12 | The hook's "closer is stronger" is discovered (§5.3) | Watch whether players walk a held enemy up to a crowd before flinging | They move in before they fling | They fling from where they stand and call it weak |
| A13 | Seven elements can be learned inside one run (T8) | After one run, ask players what each element does | They can say what four or more do | They describe them as colours, or only as different damage types |
| A14 | Shield makes the pick between Fire and Poison a real one (§4.7) | Log which element players infuse, and whether that changes once Wardens appear (wave 6) | Both are picked; Poison rises after wave 6 | One of the two is never picked |
| A15 | Carrying Void after the trial reads as a fair price (W12) | Ask; log deaths to Light-affixed enemies | Players can name the risk before they enter | Players feel punished for winning |
| A16 | One job per enemy can be read in a mixed wave | Watch whether target order changes when a Warden or a Bloater appears | They go for the Warden; they push Bloaters away | They treat everything as a Grunt |
| A17 | A Shade is noticed in time (W13) | Log how many Shades die before they cloak; ask what killed the player | More than half die in their first 2 s; players can name the marker | Players say they died "to nothing" |
| A18 | Three immunities read as a puzzle, not as a punishment (W14) | Ask after the final fight; log what players did between wave 11 and the fight | They changed something: a card, a weapon, a plan | They felt their build was switched off |
| A19 | Wide weapons are worth keeping while they are weak (§5.4) | Log which weapons are replaced in the Armory, and when | Wide weapons taken in Act I are still held in Act III | They are dropped at the first offer of something that hits harder |
| A20 | A short stamina bar creates pressure rather than frustration (D60) | Ask; log sprints per wave and hits taken with an empty bar | Players time their sprints | Players stop sprinting, or call movement sluggish |

---

## 15. Art and audio needs

New for v2.0. Every item needs a whitebox look first (a primitive and a colour), then art as **transparent PNG, no text, one item per image**, following the 1.0 art notes.

| Group | Items |
|---|---|
| World | Black hole in three sizes showing its exact square; gravity-zone edge; collapse animation; portal; boundary; wall and pillar tiles per biome; floor per biome |
| Pickups | XP orb, HP orb |
| Weapons | Display art for Rifle and Rocket Launcher is the 1.0 gun and launcher. New hand weapons: Grav Gun, Plasma Launcher, Scatter Cannon, Phase Dagger; Flamethrower and its patches, Cryo Cannon and its cloud, Arc Coil, Spore Launcher and its cloud; Railgun, Minigun, Swarm Launcher. The Strike's ground mark and its falling rocket. Auto weapons: Orbit Blades, Sentry Drone, Shield Ring. Charged shot (large bullet); aim line |
| Enemies | Skitter, Spitter and its blob, Rammer and its charge line, Bloater and its swelling, Warden and the link to those it shields, Splitter, Spire and its ring, Stomper and its ring, Shade (visible, as a shimmer, and its marker); the Tyrant in three phases with its telegraphs; elite outline; wind-up versions |
| Elements | One colour and one icon for each of the seven; status icons with a stack count; outlines for affixed enemies; how a body looks while burning, poisoned, Frozen and Paralysed; the Shield segment on HP bars; Flame Wake trail, Frost Nova ring, Storm Call bolt, Bulwark wall |
| Trial | Platform, void backdrop, The Hollow in three phases, its telegraphs |
| UI | Card frames for the three kinds and three rarities; slot frames; icons for 13 skills, 3 Greater Void skills, 8 stat cards (V4) and 3 draft upgrades plus the Mk tier; Energy and Void Energy bars; Shield bar |
| Audio | Level-up, Energy refill, no-energy, feed, collapse, portal hum, build clock's last 5 s, card pick, hook crash (heavier the more damage), a status applied, a freeze, a Shield breaking, each skill; one track per act and one for the trial |

---

## 16. Milestones

Each milestone ends with a playable web build and the automated tests passing: menu **Anomaly Arena → 4. Run Play Mode Tests**, then the first line of `Logs/TestResults.txt` must read `FINISHED Passed: … failed 0`.

Each acceptance line is written so it can become one Play Mode test.

**M0 — Weapon changes in the 1.0 code. Done 2026-10-11.**

| Given | When | Then | Checked |
|---|---|---|---|
| Two enemies in a line | A Charged Shot is fired | Both lose HP | Test passes |
| One enemy | A shot is fired at charge 0 and at charge 1 | HP lost equals 6 and 15 | Test passes |
| Two enemies in a line behind the player | Reverse Shot fires | Only the nearer loses HP | Test passes |
| An enemy 4 units in front of the player | The hook is used once | It is pulled in front of the player and stays held | Test passes |
| An enemy held on the hook | The left button is clicked again | It is flung | Test passes |
| An enemy held on the hook | — | The player's move speed multiplier is 1 | Test passes |
| Two enemies in a line, 3 and 5 units away | The hook is used, then clicked again | The flung one crashes into the other and loses HP | Test passes |
| The same, 2 and 3.1 units away | The hook is used, then clicked again | The crash happens within reach of the player; the player's HP is unchanged | Test passes |
| The crash formula | Full speed; half speed; twice the radius; almost no speed | 12; 3; 24; 1 | 5 tests pass |
| An enemy standing on a point 8 units from the player | Strike is used on that point | Halfway through the delay it has lost nothing; when the rocket lands, 1 s after the call, it has lost 10 HP | 2 tests pass |
| An enemy 5 units to the side of the marked point | Strike is used | Its HP is unchanged | Test passes |
| The aim is moved after the call | — | The rocket still lands on the first point | Test passes |
| The marked point is 1 unit from the player | Strike is used | The player loses 10 HP | Test passes |
| The cursor is further away than the range | The strike point is worked out | It is at the range, and on the ground | 3 tests pass |

52 tests (45 Play Mode, 7 Edit Mode) pass. Two values were also changed that day as tuning, without new tests: Throwing Knife damage 10 → 5 **[D59]**, and stamina — sprint 1 s → 0.5 s, recovery 3 s → 1.5 s **[D60]**; the 45 Play Mode tests were run again after them. Not yet in the published web build.

**M1 — World and waves.** Grid map 100 × 70, follow camera, boundary, walls, slam and rebound, two black holes with kill, gravity and counters, enemy pathing, 15 timed waves with endless enemies and the spawn ring. Existing weapons, no growth.

| Given | When | Then |
|---|---|---|
| The player at the boundary | Walks into it for 1 s | Position does not pass it; HP unchanged |
| An enemy knocked 8 units toward a wall 3 units away | The knock plays out | It takes wall damage once and ends on the near side, having bounced |
| An enemy standing 1 unit inside a gravity zone, not moving | 1 s passes | It is 3 units closer to the hole, or swallowed |
| A Grunt knocked into a black hole | — | It is gone, no orb exists, that hole's counter is 1 and the others are 0 |
| The player's centre enters a black hole | — | The run ends in defeat |
| An enemy 20 units from the player with a wall between | 10 s pass | It has reached the player without entering any gravity zone |
| Wave 1 running | 30 s of game time pass | The wave ends; no enemy remains |
| Wave 1 running, 10 enemies alive | 5 of them are killed at once | Within 2 s there are 10 again |
| Wave 1 running | 40 enemies are killed in a row | New enemies are still arriving |

**M2 — Loadout and growth.** Loadout pick; no use limits; two hand slots and switching; XP orbs, 10 levels, banked drafts with Stat and Weapon cards; Stat drafts after level 10; the timed build phase with the Armory (hand weapons); the upgrade axes Power, Tempo, Reach, Twist and Tame for the six anomalies (Infusion comes with M3b).

| Given | When | Then |
|---|---|---|
| A new run | A form is picked | The player holds one unrevealed weapon of that form; slot 2 is Fists |
| Level 1, 9 XP | A 1-XP orb is collected | Level 2; one draft banked; no menu opens |
| Level 9, 548 XP toward the next level | A 1-XP orb is collected | Level 10; the badge reads "10 · MAX" |
| Level 10 | 549 more XP are collected | The level is still 10; one draft of three Stat cards is banked |
| One draft banked and a revealed weapon with an untaken axis | The wave ends | The Armory shows 3 offers (2 in draft 5), then exactly 3 different cards: a Stat card, a Weapon card, and a second Stat card for a different stat while Skills are not implemented (§7.2); the third becomes a Skill card in M3 |
| The build phase after wave 1, nothing picked | 20 s of real time pass | The next wave's countdown starts; the draft has been resolved with one of its cards; no Armory weapon was taken |
| The build phase after wave 5 | — | The clock starts at 30 s |
| A build phase with everything picked | Space is pressed | The next wave's countdown starts at once |
| A Stat card "+12 % Damage" is taken | A weapon with base damage 10 hits | The enemy loses 11 |
| Both hand slots full | An Armory hand weapon is taken | A replace prompt appears; after it, the replaced weapon and its upgrades are gone |
| Two hand weapons | The right mouse button is pressed | The other slot is the active one |
| An enemy held on the hook, another weapon in slot 2 | The right mouse button is pressed | The enemy is flung, then slot 2 is the active one |
| A weapon fired 50 times | — | It is still in its slot |

**M3 — Skills and Energy.** Energy bar; the six mechanic skills; Skill cards; replacement; Max Energy and Siphon.

| Given | When | Then |
|---|---|---|
| No skill owned | A level is gained | No Energy bar exists |
| The first skill is taken | The next wave starts | Energy is 100 / 100 |
| Energy 10, Shockwave costs 30 | Key 1 is pressed | Nothing is cast; Energy is still 10 |
| Energy 40 | A level is gained mid-wave | Energy is 100 at once |
| Three skills owned | A new-skill card is taken and slot 2 chosen | Slot 2 holds the new skill at level 1 |

**M3b — Elements.** Stacks and the four statuses; Shield for both sides; Infusion cards; the seven element skills; affixed enemies and the player's protections. The Fire, Poison and Lightning-jump rows below use the draft-5 enemy rules; since V4 enemies follow the formulas of §4.6.

| Given | When | Then |
|---|---|---|
| An enemy with 3 Fire and no Shield | 1.5 s pass | It has lost 6 HP and has no Fire |
| The same enemy with a Shield of 10 | 1.5 s pass | Its Shield has lost 4; its HP is unchanged |
| An enemy with a Shield of 10 and 20 HP | A hit of 20 lands | The hit is cut to 14: its Shield is 0 and its HP is 16 |
| An enemy with 4 Poison and a Shield | 3 s pass | Its HP is 6 lower; its Shield is unchanged; it still has 4 Poison |
| A Grunt with 5 Frost | A hit applies 1 more | For 1.5 s it neither moves nor attacks; afterwards it has 0 Frost and gains none for 3 s |
| Three Grunts within 4 of each other, two of them carrying Frost | The third receives 3 Lightning | All three have 3 Lightning; the two with Frost have lost 3 HP |
| A weapon infused with Earth; the player's Shield is 0 | It deals 50 damage | The player's Shield is 5 |
| A weapon infused with Void; Energy is 0 | It deals 50 damage | Energy is 5 |
| A weapon infused with Light; the player is at 20 / 50 HP | It deals 100 damage | HP is 25 |
| A Light-infused weapon with base damage 10 | It hits a Void-affixed enemy | The enemy loses 15 |
| A weapon infused with Fire | An Infusion card for Frost is taken for it | Its element is Frost; its hits apply no Fire |
| The player has 3 Frost | A Frost-affixed enemy hits them | For 1 s the player cannot move or attack; for the next 4 s they cannot be Frozen |
| The player has 3 Poison | The wave ends | The player has no status |
| The player has 2 Poison | An HP orb is collected | The player has 1 Poison |

**M4 — The void.** Collapse, portal, trial, rewards.

| Given | When | Then |
|---|---|---|
| A hole at 39 | One more enemy is swallowed | It collapses; 1.5 s later a one-cell portal is there; the other counters stop |
| A portal opened in wave 6 | Wave 8 ends | It is still there, and shows 1 wave left |
| The same portal | Wave 9 ends | It is gone, and so is its hole; the other hole counts feeds again |
| The player walks across the portal cell | — | Nothing happens |
| The player on the portal cell | E is pressed | The trial starts; the wave timer does not move |
| The trial is won | — | The player is back, HP full, the Energy bar regenerates, a pick of 3 Greater Void skills is shown |
| The trial has been won | A Light-affixed enemy whose hit deals 10 lands it on the player | The player loses 15 |
| The trial is won by a level-10 player | — | The level badge is drawn in rainbow colours |
| The player's HP reaches 0 in the trial | — | The run ends in defeat |

**M5 — Content and balance.** Skitter, Spitter, Rammer, Bloater, Warden, Splitter, Spire, Stomper, Shade, elites; auto weapons and their slots; the new hand weapons in the build order of §5.4; biomes; art; codex; tuning against §14.

| Given | When | Then |
|---|---|---|
| A Shade's marker has appeared | 1.5 s pass | The Shade is there, visible and standing still |
| A Shade that appeared 2 s ago | — | It is cloaked: only its shimmer is drawn |
| A cloaked Shade | It takes any damage | It is visible for 2 s |
| A Shade's strike lands on a player with 50 HP and no Shield | — | The player has 20 HP |
| A Stomper pounds with the player 3 units away | — | The player loses 6 HP and is knocked 8 units away from it |
| A new Stomper in wave 1 | — | Its HP is 30 and its Shield is 15 |

**M6 — The final boss.** The build phase after wave 15, the Tyrant, immunities, Element Penetration.

| Given | When | Then |
|---|---|---|
| Wave 15's timer reaches 0 | — | A build phase of 55 s opens; after it the Tyrant appears and no wave timer runs |
| The Tyrant is immune to Fire and has no Shield | A Fire-infused hit of 10 lands | It loses 10 HP and has no Fire |
| The same, and the player has 50 % Element Penetration | A hit worth 4 Fire lands | It has 2 Fire |
| The Tyrant has a Shield | A hit of 20 lands | The hit is cut to 14 |
| The Tyrant charges into a wall | — | It is stunned for 2.5 s and its Shield is 0 |
| The Tyrant's HP reaches 0 | — | The run ends in victory |
| The player's HP reaches 0 in the final fight | — | The run ends in defeat |

---

## 17. Decision log and open items

### 17.1 Decisions

| # | Date | Decision |
|---|---|---|
| D01 | 10-02 | Weapons reveal on first use and backfire. No use limits. |
| D02 | 10-02 | Manual aiming stays. |
| D03 | 10-02 | Waves are timed. |
| D04 | 10-02 | Two hand weapons, switchable. |
| D05 | 10-02 | A large bounded map, Brotato style. The boundary stops the player and never kills. |
| D06 | 10-02 | Black holes: anything that enters dies, the player included. |
| D07 | 10-02 | Walls bounce shots and bounce enemies. |
| D08 | 10-02 | Feeding the void opens a hidden boss. |
| D09 | 10-02 | Level-up gives a pick of 1 from 3: a stat, a weapon change, a skill. |
| D10 | 10-02 | The weapon roster is reworked and extended. |
| D11 | 10-11 | Setting: interstellar war; the Interstellar Battlefield beyond the solar system; a lone surviving explorer; survive the waves, return to Earth; interstellar energy distorts weapons. |
| D12 | 10-11 | Three starting weapons: rocket launcher, rifle, knife. Later: high-tech weapons and auto weapons around the player. |
| D13 | 10-11 | The player has HP and stamina and can sprint. Interstellar Energy, the blue bar, unlocks with the first skill. |
| D14 | 10-11 | Most attacks knock back the player and monsters alike; distance depends on the attack. |
| D15 | 10-11 | Skills are one press, one cast. At most three, on 1 / 2 / 3. Weapons and skills can be upgraded. A full bar means choosing one to replace. |
| D16 | 10-11 | Skills cost Energy. At first only a level-up refills it, to full. Other sources: leech attributes on gear, or skills that leech. |
| D17 | 10-11 | A black hole is n × n. Around it, gravity pulls the player in; it can be escaped by moving away. Example: speed 6, pull 3. |
| D18 | 10-11 | After x enemies are fed, the hole closes into a one-cell portal that does not pull. Entering is the player's choice. |
| D19 | 10-11 | Beating the void boss turns the blue bar into Void Energy, which regenerates, and offers powerful skills that shape the late build. |
| D20 | 10-11 | The run starts by choosing one of the three starting weapons. |
| D21 | 10-11 | New weapons come from the Armory after each wave: 2 unrevealed weapons, take one or skip. No ground pickups, no currency, no shop. *Amended by V4 (10-14): three offers, which may include a stronger Mk of a held weapon (§5.6).* |
| D22 | 10-11 | Auto weapons have their own slots (3) and do not take hand slots. |
| D23 | 10-11 | 15 waves; a run is a little over 10 minutes. |
| D24 | 10-11 | Several black holes, each with its own counter, kept for the whole run. One boss fight per run. *Amended by D57: two holes.* |
| D25 | 10-11 | Gravity acts on every unit. Enemies avoid the zone when walking on their own. |
| D26 | 10-11 | Losing the boss fight ends the run. |
| D27 | 10-11 | Level-up cards are picked at the end of the wave. |
| D28 | 10-11 | Hitting the map boundary at speed deals wall damage: one rule for all hard surfaces. |
| D29 | 10-11 | The right mouse button is not used for now. *Superseded by D44.* |
| D30 | 10-11 | No shield weapon. |
| D31 | 10-11 | A replaced weapon's upgrades are lost. |
| D32 | 10-11 | An enemy swallowed by a black hole drops no XP and adds 1 to that hole's counter. |
| D33 | 10-11 | Knife · Hook: no slowdown while holding an enemy. **Implemented.** *Suspended by D35 for a few hours, back in force with D43.* |
| D34 | 10-11 | The gun's charge attack is a charged shot that pierces; damage grows with charge time. **Implemented.** |
| D35 | 10-11 | Knife · Hook: a hooked enemy is flung out at once (no holding, no second click). If it crashes into enemies it deals area damage that depends on its kinetic energy — the sooner it hits, the more — and on its size. Values are a formula in a tunable asset. **Implemented.** *Amended by D43: the fling is manual again; the crash stays.* |
| D36 | 10-11 | Levels: a non-linear model, top level 10. Early levels come fast; later ones need much more XP. |
| D37 | 10-11 | The portal opens after 40 feeds, is permanent, and is entered by standing on it and pressing E. It is meant as one mid-run upgrade opportunity the player can choose to take. *Amended by D57: the portal stays open for three waves, not for the whole run.* |
| D38 | 10-11 | After a wave the player has a limited time to pick cards and change weapons: 20 s early on, 5 s more every two waves. The player may start the next wave early. If time runs out, unpicked cards are picked at random. |
| D39 | 10-11 | A player who is level 10 and has beaten the hidden boss gets a rainbow-coloured 10. |
| D40 | 10-11 | After level 10, each time the XP bar fills the player gets a Stat card. |
| D41 | 10-11 | A wave's enemies are endless: the more the player kills, the more they grow. The wave still ends on time. |
| D42 | 10-11 | The design and engineering documents in the repository are in English. Translations are kept outside the repository. |
| D43 | 10-11 | Knife · Hook does not fling by itself: the hooked enemy is held and the player flings it. The crash of D35 stays. **Implemented.** |
| D44 | 10-11 | The right mouse button switches weapons; Q does not. Supersedes D29. |
| D45 | 10-11 | Seven elements: Fire, Poison, Frost, Lightning, Earth, Void, Light. Burn ticks twice a second, loses 1 Fire per tick, and is halved against a target with a Shield. Poison ticks once a second, never decays, ignores Shield and comes straight off HP. Lightning and Frost build up; Frost freezes an enemy in place; Lightning paralyses and travels between enemies that carry Frost. Earth gives the player Shield. Light gives the player lifesteal and deals more to enemies that carry Void. Void restores Energy, and whoever carries it takes more damage from Light-affixed enemies. *Revised 10-12: basic Lightning jumps without Frost. V4 (10-14) changed the Fire, Poison, Frost, Lightning and Light formulas on enemies (§4.6).* |
| D46 | 10-11 | Orthogonal design: every enemy type has a different function; more weapons, orthogonal to the six that exist; weapon upgrades, orthogonal to each other; skills, orthogonal to each other. |
| D47 | 10-11 | A weapon gets its element from an Infusion among the weapon upgrade cards. One element per weapon; a new Infusion replaces the old one. |
| D48 | 10-11 | Frozen is a short full stop: no moving, no attacking. Paralysed lasts longer and interrupts at intervals. *V4 (10-14): a paralysed enemy is slowed to × 0.55 instead of being stopped (§4.6).* |
| D49 | 10-11 | A Burn tick deals damage equal to the current Fire stacks. *V4 (10-14): still the rule on the player; on enemies Fire burns out exponentially (§4.6).* |
| D50 | 10-11 | Affixed enemies put statuses on the player, with protections: loss of control lasts at most 1 s and is followed by a short immunity; all statuses are cleared when the wave ends; any healing removes one Poison stack. |
| D51 | 10-11 | Skills are built on the elements; a skill can also be a pure mechanic. |
| D52 | 10-11 | The rocket launcher's random homing rocket becomes a strike on a chosen position: 1 s later a rocket arrives there and explodes. **Implemented.** |
| D53 | 10-11 | A new enemy, the Stomper: it pounds the ground for a shockwave, and it starts with armour. |
| D54 | 10-11 | A new enemy, the Shade: very high damage — one hit nearly cripples the player — fast, cloaked soon after it appears, low HP. The player is meant to notice it and kill it as it appears. |
| D55 | 10-11 | Armour reduces damage: a normal hit on it does 30 % less, for the player and for monsters alike. Armour and the Shield value are one layer. |
| D56 | 10-11 | A final boss before leaving in wave 15: hard, and immune to some elements. So the drafts offer Element Penetration, which lets the player ignore part of an immunity. The boss comes after wave 15 has been played out. |
| D57 | 10-11 | Black holes: only two on the map, one larger, one smaller. Once a portal is open the player has three waves to fight the void boss; after that the portal closes and that hole disappears. The other hole remains. Amends D24 and D37. |
| D58 | 10-11 | Weapons were too much utility and not satisfying enough: cut the three most utility-heavy forms; add weapons with a built-in element and strong late-game killing weapons. Damage need not be very high: high damage hits few enemies at once; wide, satisfying weapons start with low damage. The player should feel the power after growing a build. |
| D59 | 10-11 | The Throwing Knife's damage is cut by at least half. Set to 5. **Implemented.** |
| D60 | 10-11 | The stamina bar was too long for any pressure: halve it, and double the recovery speed. Sprint 0.5 s, recovery 1.5 s. **Implemented.** |

### 17.2 Proposed defaults that most need a yes or no

Ordered by how much depends on them. Items 17 to 29 were added with drafts 4 and 5 and are not ranked.

| # | Item | This draft says | The alternative |
|---|---|---|---|
| 1 | **Gravity zone width** (§8.4) | 2 cells beyond each edge: 0.67 s to react | The literal (n + 1) × (n + 1): half a cell, 0.17 s |
| 2 | **The Energy drought late in a run** (§6.5) | Accepted as designed: about 10 refills in a run, 3 of them in the last five waves; a Void infusion, *Devour* or the trial is the way out | A partial refill at the start of each wave |
| 3 | The hook's crash numbers (§5.3) | `impactDamage` 12, reach 1.5, size counts linearly, the flung enemy is hurt too, the player never is | Any of them; they are fields in `KnifeHook.asset` |
| 4 | The level curve (§7.1) | First level 10 XP, each level 65 % dearer; a typical player is level 10 in wave 13 | Flatter or steeper |
| 5 | Stat cards after level 10 (§7.1) | A draft of three Stat cards every 549 XP; it also refills Energy | One random Stat card with no choice; a rising price; no refill |
| 6 | Endless spawning (§9.3) | Keep 10 + 2 per wave alive; top up at most 6 per second | Other numbers |
| 7 | Stat card values (§7.3) | Raised by about half, because a run now has only 9 normal drafts. Since V4: seven merged cards plus Element Penetration. | Keep the smaller values |
| 8 | Cell size, hole sizes, map size (§8.1, §8.4) | 1 cell = 1 unit; two holes, of 7 and 3; map 100 × 70 with the 1.0 camera, which shows 62 × 37 | Other sizes; zoom the camera in |
| 9 | Superseded by the 2026-10-12 revision: damage-killed corpses can enter fixed holes and count once (§4.1, §8.4). | The earlier kill-never-feed rule is historical. | Each enemy spawn lifetime feeds at most once; corpse feeding pays no second XP, kill or Energy reward. |
| 10 | Charged Shot knockback (§5.3) | 5 → 10, the swing's values, because only the damage change was specified | Lower, e.g. 3 → 8; or none |
| 11 | The battlefield during the trial; The Hollow (§8.6) | Frozen; the boss concept as written | Cleared, or it keeps running; another boss |
| 12 | Build phase order and Mk tiers (§2.3, §5.6) | Armory first, then drafts; Mk tiers by wave (V4) | Drafts first; no tiers |
| 13 | Wave end (§2.2) | Enemies dissolve with no XP; all orbs are collected; HP to full | Orbs left behind are lost; partial heal |
| 14 | Skills (§6.2) | 2 s cooldown; castable while knocked | Energy only; not while knocked |
| 15 | Wall damage to enemies grows with the wave (§4.3) | Yes | Flat 5 |
| 16 | Content lists | Hand weapons (§5.4), auto weapons (§5.5), upgrade axes (§5.8), skills (§6.3), enemies (§9.1), biomes (§8.7) | Any of them |
| 17 | **Who counts as carrying Void** (§4.6) | The player, while any of their weapons is infused with Void, and for good after the trial: Light-affixed enemies deal +50 % to them | Only enemies carry it; the player never pays this price |
| 18 | Element numbers (§4.6) | 1 stack per 5 damage (÷ R(w) on enemies since V4); thresholds of 6; Frozen 1.5 s; Paralysed 4 s; Earth 10 %, Light 5 %, Void 10 % of the damage dealt | Any of them; they sit in one asset |
| 19 | Affixed enemies (§9.5) | From wave 5; 10 / 15 / 25 % of spawns; at most two affixes per wave, announced | Earlier or later; more; unannounced |
| 20 | Re-infusion in slot B (§7.2) | A weapon that has an element is offered another one 15 % of the time | Only when nothing else is left; or a free choice of element |
| 21 | Skills while an enemy is held on the hook (§5.7) | Allowed | Blocked |
| 22 | *Devour*'s cooldown (§6.3) | 6 s, so that it cannot out-earn the trial's reward | The general 2 s |
| 23 | The Strike's point (§5.3) | Fixed where the cursor was at the click; at most 20 away | It follows the cursor until the rocket lands; no range limit |
| 24 | The portal's three waves (§8.5) | The wave it opens in does not count; if it runs out, the other hole counts again | Count the opening wave; no second chance |
| 25 | The final boss (§9.7) | The Tyrant: 4 000 HP, a Shield of 400 per phase, immune to 3 of the 7 elements, rolled per run and shown from wave 11 | Fixed immunities; shown from the start; fewer or more of them |
| 26 | Element Penetration (§7.3) | A Stat card of +20 / 35 / 50 %, offered from wave 11 to players who own an immune element | Offered all run; also useful against something before the boss |
| 27 | The Shade and the Stomper (§9.1) | Shade: 30 damage, 6 HP, 2 s visible, at most two alive, never elite or affixed. Stomper: 30 HP, a Shield of half that, Heavy knockback within 5. | Other numbers |
| 28 | The weapon roster and its budget (§5.4) | Element forms from wave 3, Minigun and Swarm Launcher from wave 6, Railgun from wave 11; numbers as in the budget table | Any of them |
| 29 | Stacks need not be whole (§4.6) | A 3-damage bullet adds 0.6 of a stack; built-in elements stack twice as fast | At least 1 stack per hit, which lets rapid weapons stack far faster than their damage |

### 17.3 Still open

| # | Question |
|---|---|
| Q1 | *Answered by D56: the run ends with a final boss, not with the timer.* |
| Q2 | Does enemy damage scale with the wave? This draft scales HP only. |
| Q3 | Should runs differ by more than biome and black-hole positions — a pick of starting stat, a modifier per run? |
| Q4 | Answered on 2026-10-12: the team approved all 44 additional Twist/Tame definitions for the 22 new hand-weapon anomalies; see [`DESIGN-DECISIONS.md`](DESIGN-DECISIONS.md) §3. The earlier "fourteen" count was inconsistent with the roster. Implementation and runtime verification remain separate. |

---

## 18. Impact on the 1.0 code

Planning notes only; details and task breakdown are in [`TECH-BACKLOG.md`](TECH-BACKLOG.md).

| 1.0 | v2.0 |
|---|---|
| `ArenaShape`: one polygon with gaps in its edge | A grid map: boundary, wall cells, black holes, gravity zones |
| `FallZone`: trigger outside a gap | Black-hole component: kill square, gravity, counter, lifecycle |
| `Enemy.KeepOffEdges`: straight line, slide along edges | Pathing on the grid that treats walls, holes and zones as blocked |
| `WeaponSpawner`: ground weapons, supply per wave | Loadout screen and Armory |
| `Weapon.UsesLeft` | Removed; clip and reload stay for held-fire weapons |
| `WaveManager`: fixed enemy counts, clear to advance | Timer, endless top-up to a target, mix table, elites, timed build phase |
| `GameManager`: Title / Playing / Won / Lost; hit stop writes the global time scale | Run state machine of §2.1; one owner for the time scale (pause, menus, hit stop, Time Slip) |
| Fixed camera; shake is an offset from a stored position | Follow camera with bounds; shake as an offset on top |
| `Hud` (IMGUI) | The HUD and screens of §10 |
| `PlayerController`: one weapon in hand | Two hand slots; the right mouse button switches **[D44]** |
| `IWeaponHolder.AimDirection` | Also `AimPoint`, the cursor's point on the ground — added with the Strike **[D52]** |
| `WaveManager`: the run is won when the last wave is cleared | A build phase and a final-boss state after wave 15 (§9.7) |
| `Combatant`: HP and a state | Also Shield, element stacks, tick clocks, Frozen / Paralysed, and payouts to the wielder (`TECH-BACKLOG.md`, T18) |
| Hit feedback, stamina, state machine, art toggle, the six effects, the test setup | Kept |
