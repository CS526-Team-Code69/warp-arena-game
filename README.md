# WARP ARENA

WARP ARENA is a top-down survival game in which you have seven days to refuel a crashed ship: by day you defend your base with weapons that hide a backfire until you take them, and by night you mine by lamplight among shadows that multiply with every monster you killed.

**Play in your browser:** [Beta](https://cs526-team-code69.github.io/warp-arena-game/beta/) (the whole game) · [Alpha](https://cs526-team-code69.github.io/warp-arena-game/) (Open Mode only). Desktop Chrome, Edge or Firefox, with mouse and keyboard.

| | |
|---|---|
| ![Title screen](Docs/images/title.jpg) | ![Day 1 morning: monsters walk in on the base](Docs/images/story-morning.jpg) |
| ![Night: a mining lamp's light, with shadows waiting at its edge](Docs/images/story-night.jpg) | ![The final battle against the Tyrant King](Docs/images/final-battle.jpg) |
| ![The base page](Docs/images/base-page.jpg) | ![A weapon crate: a name, a Mk and ? ? ? for each weapon](Docs/images/weapon-crate.jpg) |

All screenshots are in whitebox, the game's default look.

## The game

**Story Mode** is a Day 0 tutorial, seven days and a final battle on one map, with the base at its centre.

- **Morning:** monsters walk in from every side to strike the base. You aim and fire, swap two hand weapons, and up to two auto weapons act on their own.
- **Afternoon:** a trail of alloy ore leads toward a black hole, with a weapon crate on the way.
- **Night:** dark matter is mined in the light of the lamps you set down.
- **At the base:** alloy, dark matter and alien energy buy weapon, skill, base and ship upgrades in four rooms. A repaired, fuelled ship starts the final battle against the Tyrant King.

**Open Mode** is the Alpha build's arena: 14 timed waves with level-up cards and the Armory, then the Tyrant King.

**The twist has two parts.**

- Every weapon but the starting pistol is anomalous. A crate shows only each weapon's name and Mk; its attack and backfire are revealed once you take it.
- The night answers the day. The more monsters you kill in a region in the morning, the more shadows rise there at night. Weapons cannot hurt shadows: light holds them back, but a large enough crowd walks into a lamp's light and puts it out.

## Controls

| Action | Input |
| --- | --- |
| Move / sprint | WASD / hold Shift while moving |
| Aim / attack | Mouse / left mouse button |
| Swap hand weapon | Right mouse button |
| Cast a skill, or pick option 1, 2 or 3 on a panel | 1, 2, 3 |
| Mine alloy ore or dark matter | Hold E beside it (at night, only in a lamp's light) |
| Open a weapon crate, enter the base, enter the void portal | E, when its prompt shows |
| Set down a mining lamp | F |
| Close the weapon card | Space, Enter or a click |
| Pause | Esc |
| Start a new run on the result screen | R |
| Open Mode: reopen a kept card pick / put the build panel away / reroll the cards | T / Tab / R |

The move keys, Shift, E, the skill keys, F and T can be rebound in Settings.

## More

- [Game design document](Docs/GDD.md)
- [Analytics](Analytics/README.md): the web build sends one row per event, with random ids only, to the team's Google Sheet, which charts the Story Mode metrics.

## Team

Felix Lin, Joe Zhang, Hongyi Wang, Yuyang Bo and Hector Medina— CS526-Team-Code69.
