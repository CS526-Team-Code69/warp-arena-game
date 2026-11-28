# WARP ARENA

WARP ARENA is a top-down survival game in which you have seven days to refuel a crashed ship: by day you defend your base with weapons that hide a backfire until you take them, and by night you mine by lamplight among shadows that multiply with every monster you killed.

**Play in your browser:** [Gold](https://cs526-team-code69.github.io/warp-arena-game/gold/) (the whole game, with art, sound and cutscenes) · [Chinese](https://cs526-team-code69.github.io/warp-arena-game/zh/) (the same game in Chinese) · [Beta](https://cs526-team-code69.github.io/warp-arena-game/beta/) (the Beta milestone build) · [Alpha](https://cs526-team-code69.github.io/warp-arena-game/) (Open Mode only). Desktop Chrome, Edge or Firefox, with mouse and keyboard.

| | |
|---|---|
| ![Title screen](Docs/images/gold-title.jpg) | ![Morning: monsters walk in on the base](Docs/images/gold-morning-raid.jpg) |
| ![Afternoon: mining the day's alloy ore](Docs/images/gold-afternoon-mining.jpg) | ![Night: mining dark matter in a lamp's light](Docs/images/gold-night-dark-matter.jpg) |
| ![The workshop page](Docs/images/gold-workshop.jpg) | ![The rocket page](Docs/images/gold-rocket.jpg) |
| ![A weapon crate: pick one of three](Docs/images/gold-weapon-crate.jpg) | ![The final battle against the Tyrant King](Docs/images/gold-tyrant.jpg) |

Screenshots from the Gold build in its illustrated look; Settings switches the game to whitebox.

## The game

**Story Mode** is a Day 0 tutorial, seven days and a final battle on one map, with the base at its centre.

- **Morning:** for 60 seconds monsters walk in from every side to strike the base; once the countdown ends, defeat the ones left to end the morning. A raider you hit comes for you. You aim and fire, switch between two hand weapons, and up to two auto weapons act on their own.
- **Afternoon:** the day's alloy ore surfaces on the way to a black hole, with a weapon crate on the trail. Lure ten monsters into the black hole and it opens a portal to the void trial against the Nash Prophet.
- **Night:** dark matter is mined in the light of mining lamps. The base gives two lamps at each dusk and more are made on the rocket page; lamps go down only after dark.
- **At the base:** the workshop, at the base door, sells the base's fortification, weapon Mk, armour, each weapon's own upgrades, and energy research for the body, the skills and element infusion. The rocket page, beside the ship, repairs the engine, the hull and the fuel tank, fills the fuel with dark matter, makes lamps and starts the ignition, which brings on the final battle against the Tyrant King.
- Your maximum health grows each dawn with the monsters' damage. The game saves at the start of each morning, afternoon and night, and a lost run can be retried from its last save.

**Open Mode** is the Alpha build's arena: 14 timed waves with level-up cards and the Armory, then the Tyrant King, on two maps, Dunes and Ruins.

**The twist has two parts.**

- Every weapon but the starting pistol is anomalous. A crate shows only each weapon's name and Mk; its attack and backfire are revealed once you take it.
- The night answers the day. The more monsters you kill in a region in the morning, the more shadows rise there at night. Weapons cannot hurt shadows: light burns them, but a large enough crowd walks into a lamp's light and puts it out.

## Controls

| Action | Input |
| --- | --- |
| Move / sprint | WASD / Shift |
| Aim / use the weapon in your hand | Mouse / left mouse button (hold to charge or to keep firing, depending on the weapon) |
| Switch between your two hand weapons | Right mouse button |
| Cast a skill, or pick option 1, 2 or 3 on a panel | 1, 2, 3 |
| Mine alloy ore or dark matter | Hold E beside it (at night, only in a lamp's light) |
| Open a weapon crate, the workshop at the base door, the rocket page beside the ship, or a portal | E, when its prompt shows |
| Set down a mining lamp | F, after dark |
| Void Dash toward the cursor | Space, once you have it |
| Character screen: health, weapons, skills and bonuses | I |
| Pause / resume | Esc |
| Restart on the result screen | R |
| Open Mode: reopen a saved card pick / put the build panel away / reroll the cards | T / Tab / R |

The move keys, Shift, E, the skill keys, F, I and T can be rebound in Settings, which also holds the language (English or Chinese), the look (illustrated or whitebox) and the volume.

## More

- [Game design document](Docs/GDD.md)
- [Analytics](Analytics/README.md): the web build sends one row per event, with random ids only, to the team's Google Sheet, which charts the Story Mode metrics.
- Art and sound: the 3D models and their animations were made with Meshy, and the sound effects, music and voices with ElevenLabs.

## Team

Felix Lin, Joe Zhang, Hongyi Wang, Yuyang Bo and Hector Medina— CS526-Team-Code69.
