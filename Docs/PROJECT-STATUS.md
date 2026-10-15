# Project status

Public name: **WARP ARENA**. Unity: **6000.6.3f1**. The repository contains the source, saved scenes, data, art, audio and tests; local caches and personal settings are excluded.

## What is in the game

- Fifteen waves with fixed, wave-based enemy HP budgets that never read the player's live damage. Shields and Tyrant phase shields count toward effective HP ([combat balance](COMBAT-BALANCE.md)).
- Element, skill and stat-card values follow the fodder effective-HP curve, each system with its own formula: Fire burns out exponentially, Poison builds up additively, Frost chills by division, Lightning slows (multiplied with Frost) and interrupts windups; Damage cards multiply and add crit chance, Attack Speed and armor divide ([V4 numbers](V4-NUMBERS.md)).
- Seven stat cards plus Element Penetration. Draft weapon cards change behaviour or element (Twist, Tame, Infusion); damage growth comes from armory Mk tiers.
- The armory offers three distinct weapon forms; the second and third slot may show a stronger Mk of a weapon you hold, which keeps its anomaly, changes and element.
- Hits land without pausing the world: the struck enemy holds its pose for a moment, with directional sparks, a crit star and a small, rate-limited camera kick. Element states and player buffs are drawn on the bodies; models are enlarged visually only.
- Black holes show a purple pull field, a pulsing warning ring on the pull border, inward chevrons and a halo around the core.
- Five rarity tiers; Flame Wake and skill areas follow the live skill state; Time Slip clears on cleanup; immovable enemies resist pull.

## Verification

- Automated tests on the current code: **87 of 87 Play Mode and 8 of 8 Edit Mode cases pass** (Unity 6000.6.3f1, batch mode). They cover the armory (three distinct offers, held weapons only as a higher Mk, in-place upgrades), the V4 formulas (chained damage, armor and cooldown division, Mk tiers, Frost and Lightning slows), wave flow, crate opening, XP pickup, skills and time scale under dense damage over time.
- First-wave attack-to-death measurements on a single target were 0.54 s (rifle A), 0.61 s (throwing knife), 0.53 s (rocket A) and 0.64 s (human rocket).
- Not yet done: playtesting the V4 numbers and feel, a complete fifteen-wave run, a new WebGL build, an exhaustive upgrade-combination sweep and a performance pass. Kill times and boss times in the balance documents come from paper and discrete-hit models.

## Next work

1. Playtest the V4 numbers: record ordinary-build fodder kill times and boss time on waves 6, 10 and 15, and compare them with the thresholds in [V4 numbers](V4-NUMBERS.md#7-most-likely-failure-and-the-minimal-check).
2. Record level-up times on the same runs to check the XP pacing in [combat balance](COMBAT-BALANCE.md).
3. Find the sector-effect arc mismatch reported in QA: the cone visuals and hit checks reviewed so far use the same half-angle, so the affected effect is still unidentified.
4. Run a complete fifteen-wave session and a performance pass on waves 6 to 8 with the new effect pool active.

## Collaboration

The repository belongs to CS526-Team-Code69; the Warp Arena Developers team has write access. Use short feature branches and pull requests, commit `.meta` files together with their assets, and coordinate edits to shared scenes. Superseded art that the game no longer uses is kept in `ArtSource/Archive/`, outside `Assets/`.
