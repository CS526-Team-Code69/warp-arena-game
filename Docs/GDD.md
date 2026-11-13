# Game Design Document

WARP ARENA  
CS526-Team-Code69  
Beta Milestone Build · 11/13/2026

## Team Members

| Name | Roles | E-mail & GitHub ID |
| :---: | :---: | :---: |
| Zhe Lin | Team Captain; lead designer; card draft system and weapon system, including attack and hit feel and feedback | [linzhe@usc.edu](mailto:linzhe@usc.edu) siniandragon |
| Hongyi Wang | Product Manager (Analytics); code optimization: state machines, object pooling and other complex optimization logic; finding bugs in the team's code; enemy chase steering | [hongyiw@usc.edu](mailto:hongyiw@usc.edu) LTDwang |
| Yuyang Bai | Engineer; Enemy tracking and combat: the logic for enemies following and attacking the player | [yuyangba@usc.edu](mailto:yuyangba@usc.edu) Ne1sonBa1 |
| Wenbo Zhang | Stenographer (notes from playtests and staff feedback); enemy states, arena terrain generation and enemy spawning for each wave | [wzhang26@usc.edu](mailto:wzhang26@usc.edu) joewenbozhang-blip |
| Hector Medina | Gameplay Engineer; QA; Testing and bug fixing. | [hrmedina@usc.edu](mailto:hrmedina@usc.edu) hmedina |

GitHub accounts: Zhe Lin commits as siniandragon, Hongyi Wang as LTDwang, Yuyang Bai as Ne1sonBa1, Wenbo Zhang as joewenbozhang-blip, Hector Medina commits as hmedina.

## Links

| Unity Project GitHub Repository | [https\://github.com/CS526-Team-Code69/warp-arena-game](https://github.com/CS526-Team-Code69/warp-arena-game) |
| :---- | :---- |
| Playable Alpha Progress Check Build | [https\://cs526-team-code69.github.io/warp-arena-game/](https://cs526-team-code69.github.io/warp-arena-game/) |
| Playable Alpha Build | [https\://cs526-team-code69.github.io/warp-arena-game/](https://cs526-team-code69.github.io/warp-arena-game/) |
| Alpha Gameplay Video | [https\://drive.google.com/file/d/17gGCc6qqtVs5IlKISt3jlKAzgyK4r1WU/view?usp=sharing](https://drive.google.com/file/d/17gGCc6qqtVs5IlKISt3jlKAzgyK4r1WU/view?usp=sharing)  |
| Alpha Survey | [https\://docs.google.com/forms/d/e/1FAIpQLSedXUD5N\_2HrlhcZl5iOWlA5yqF4oQQcDZ4mjKMZ3qLLu0VxA/viewform?usp=publish-editor](https://docs.google.com/forms/d/e/1FAIpQLSedXUD5N_2HrlhcZl5iOWlA5yqF4oQQcDZ4mjKMZ3qLLu0VxA/viewform?usp=publish-editor) |
| Playable Beta Progress Check Build | [https\://cs526-team-code69.github.io/warp-arena-game/beta](https://cs526-team-code69.github.io/warp-arena-game/beta) |
| Playable Beta Build | [https\://cs526-team-code69.github.io/warp-arena-game/beta](https://cs526-team-code69.github.io/warp-arena-game/beta) |
| Beta Gameplay Video |  |
| Beta Survey | [https\://docs.google.com/forms/d/1\_uJkzY7aBIubvAmls6jh-ddjKB8W\_XXB6kJiqECY0UY/edit?ts=6af4e02f](https://docs.google.com/forms/d/1_uJkzY7aBIubvAmls6jh-ddjKB8W_XXB6kJiqECY0UY/edit?ts=6af4e02f) |
| Playable Gold Progress Check Build | To be added for the Gold milestone. |
| Playable Gold Build | To be added for the Gold milestone. |
| Gold Gameplay Video | To be added for the Gold milestone. |

# Game Introduction

## Logline 

WARP ARENA is a top-down survival game in which you have seven days to refuel a crashed ship: by day you defend your base with weapons that hide a backfire until you take them, and by night you mine by lamplight among shadows that multiply with every monster you killed.

## Player Goals / Win Conditions

* Win (Story Mode): raise the Ship Launch Bay to level 3, repair the ship's engine and armor, and fill its tank with fuel made from 100 dark matter. The ignition then fails, the Tyrant King (the final boss) appears outside the base, and killing it wins the run. If the ship is not ready after Day 7, more days follow, marked OVERTIME.

* Lose (Story Mode): the player dies in a morning or in the final battle, or the base's HP reaches zero. A fall at night only loses what was mined or picked up that night, and a fall in the afternoon loses nothing.

* Optional (Story Mode): feed the black hole, break it in time, and beat the Nash Prophet behind its portal.

* Open Mode: survive waves 1 to 14, then kill the Tyrant King. The run is lost when HP reaches zero, the player enters a black hole, or the player dies or falls off the platform in the optional void trial.

* Shown to the player: the goal panel names the current goal, and the base bar shows the base's HP and the ship's state ("SHIP E× A× F0/10": engine, armor, fuel units).

## Game Description

WARP ARENA is a top-down survival game played in the browser. Story Mode is a Day 0 tutorial, seven days and a final boss on one map with the base at its centre. Each morning monsters walk in to strike the base; the player aims and fires, swapping two hand weapons while up to two auto weapons act on their own. Each afternoon a trail of alloy ore leads toward a black hole, with a weapon crate on the way. Each night dark matter is mined in the light of lamps the player sets down. At the base, alloy, dark matter and the alien energy dropped by kills buy weapon, skill, base and ship upgrades; a repaired, fuelled ship starts the final battle. Open Mode is the Alpha build's arena: 14 timed waves, then the final boss.

The twist has two parts. Every weapon but the starting pistol is anomalous: a crate shows only its name and Mk, and its attack and backfire are revealed once it is taken, such as a Reverse Shot whose bullets leave from your back. And the night answers the day: the more monsters were killed in a region in the morning, the more shadows rise there at night. Weapons cannot hurt shadows; light holds them back, but a large enough crowd walks into a lamp's light and puts it out.

## Controls

| Action | Input |
| :---- | :---- |
| Move / sprint | WASD / hold left Shift while moving |
| Aim / attack | Mouse / left mouse button (hold to charge or to keep firing, depending on the weapon) |
| Swap hand weapon | Right mouse button |
| Cast a skill / pick option 1, 2 or 3 on an open panel | 1, 2, 3 |
| Mine alloy ore or dark matter | Hold E beside it (at night, only in a lamp's light) |
| Open a weapon crate; enter the base (afternoon, night); top up the base shield with energy (morning); enter the void portal | E, when its prompt shows |
| Set down a mining lamp | F |
| Close the weapon card | Space, Enter or a click |
| Pause and resume | Esc |
| Start a new run on the result screen | R |
| Open Mode only: reopen a kept card pick / put the build panel away and back / reroll a card panel (once per wave) | T / Tab / R |

The move keys, Shift, E, the skill keys, F and T can be rebound in Settings; the mouse buttons, Esc, Tab, R, Space and Enter are fixed.

The game is built for keyboard and mouse on a desktop browser (Chrome, Edge or Firefox). It is not designed for mobile browsers, and we are not claiming the mobile bonus.

# Detailed Design

## Game Mechanics (How to Play)

Core mechanics are the ones the player uses again and again to reach the win condition; supporting mechanics make them more useful, more rewarding or harder (the Week 3 lecture's definitions). The list describes Story Mode.

* **Anomalous weapon combat (core):** aim with the mouse and fire with the left button; the right button swaps two hand weapons, and up to two auto weapons act on their own. The run starts with a plain pistol. Each of the 11 hand forms and 3 auto forms has two anomalies, rolled when the weapon is made and hidden until it is taken; a weapon card then shows how it fires, its damage, its element and its backfire.

* **Gathering (core):** hold E to mine alloy ore in the afternoon and dark matter at night; both are limited each day. Alloy pays for weapons, the base and the ship; dark matter only makes fuel; alien energy comes from morning kills and the night's nodes.

* **Base building (core):** at the base door the three currencies are spent in four rooms. Each level of the Armament Bay, the Energy Lab and the Ship Launch Bay unlocks new purchases, hidden until then: weapon Mk and armor, then new weapons and Tame (softens a backfire), then Twist (adds a behavior); body upgrades, then skills, then element infusions; the ship's engine, then its armor, then fuel. Central Control raises the base's hull and shield. One Mk level is shared by every anomalous weapon, and every purchase takes effect at once.

* **Skills, Energy and elements (core):** up to three skills on keys 1 to 3, paid with Energy, which refills each morning and a little with every kill. Each of the seven elements has one job: Fire and Poison deal damage over time, Frost and Lightning control, and Earth, Light and Void give the wielder shield, HP or Energy. Elements come from skills, weapon infusions and elite affixes.

* **Enemies and bosses (core):** each kind tests one thing. Grunts punch, Spitters shoot from range, Wardens shield their neighbours; the six specials buff allies (Chanter), charge and stun (Rammer), explode (Bloater), guard ground (Spire), launch the player (Stomper) and cloak for a killing strike (Shade). Kinds are added a day at a time, and from Day 3 an elite is a special with an element affix. The Nash Prophet (optional) and the Tyrant King (final) ignore Frost and Lightning and cannot be stunned, knocked back or swallowed.

* **Base defense:** the base has HP and a shield that refills each night. Morning monsters walk to it and strike it; those within a few cells of the player turn on the player. A side of the base that takes too much damage in one day breaks its room until it is repaired with alloy.

* **Night: lamps and shadows:** dark matter can be mined only in the light of the base's floodlights or of a mining lamp (a few are handed out each night; more are crafted at the Ship Launch Bay). Every region holds shadows, more where more was killed that morning. They hunt a little faster than a walk and stop at the edge of a light, but a crowd at one lamp walks in together and puts it out. Weapons and skills cannot hurt them, and their touch costs a share of max HP.

* **Knockback, walls and terrain:** knockback tiers run from none (rapid fire, the throwing knife) to launch (a full Charged Shot); a body slammed into a wall, a pillar or the map edge takes damage and rebounds. Quicksand slows walking and sprinting.

* **Sprint and stamina:** hold Shift while moving. A sprint can start at any time until the short bar is empty; an empty bar must refill fully first.

* **Weapon crates:** two on Day 0 and one on each later afternoon's ore trail. A crate offers three weapons the player does not own, shown as a name, a Mk and "? ? ?"; one is taken or the crate is left.

* **The black hole (optional):** it must first swallow ten monsters, lured into its pull by the player; it pulls only a little faster than a walk, so a sprint gets out. Then the first hit on it starts 45 seconds in which it must take damage equal to half the Nash Prophet's health, while it answers with void pillars and rings of shots. Passed, it becomes the portal to the Nash Prophet, whose defeat gives void energy and void alloy (a greater void skill, void enchants, Mk 11); failed, it can be tried again the next day.

* **Final Overload:** from Day 7, Central Control turns leftover alloy and energy into a starting shield or extra damage for the final battle.

Open Mode keeps the Alpha build's rules: 14 timed waves, then the Tyrant King; XP and level-up cards, the Armory after each wave, and two black holes that open the void trial once 40 monsters have been fed to one of them.

## Game Mechanic Research

**Zhe Lin**

| Game Title | Brotato |
| :---- | :---- |
| **Mechanic/System** | The wave-based roguelike loop: fight a short timed wave, then pick weapons and upgrades before the next, harder wave. |
| **Interest** | I like the roguelike feeling of fighting monsters, choosing weapons and upgrades, and getting through the waves one after another; every run grows into a different build. |
| **Interaction with other mechanics** | Materials dropped by killed enemies pay for weapons and items in the shop between waves, and level-ups during a wave give a choice of stat upgrades, so how well you fight decides what you can build, and the build decides how the next wave goes. |
| **Innovation** | Played straight, this loop felt a little dull to me, so in WARP ARENA every weapon has a special hidden effect with a backfire, and we added bosses and the void: black holes that can be fed to open an optional trial. |

**Hongyi Wang**

| Game Title | 20 Minutes Till Dawn |
| :---- | :---- |
| **Mechanic/System** | Upgrade and build synergy system. Players collect experience by defeating enemies and choose upgrades when leveling up. Different upgrades can be combined to create unique builds. |
| **Interest** | I like how simple upgrades can interact to create different playstyles. This encourages experimentation and makes each run feel different. |
| **Interaction with other mechanics** | Upgrades affect shooting, movement and survival. Stronger abilities help players defeat more enemies, gain experience faster and survive increasingly difficult waves. |
| **Innovation** | Apply this system to an objective-based survival game. Players could choose upgrades that improve combat, movement or repair efficiency. Some upgrades could involve trade-offs, forcing players to balance survival and completing objectives. |

**Yuyang Bai**

| Game Title | League of Legends |
| :---- | :---- |
| **Mechanic/System** | Jungle objectives (Dragon and Baron). |
| **Interest** | They create high-stakes moments where teams must decide whether to fight, trade objectives or take risks. |
| **Interaction with other mechanics** | They interact with vision control, team fights, champion abilities, respawn timers and map pressure. |
| **Innovation** | Shared objectives that encourage team coordination and strategic decision-making beyond individual combat. |

**Wenbo Zhang**

| Game Title | Dragon's Dogma 2 |
| :---- | :---- |
| **Mechanic/System** | The pawn system: AI companions shared between players. |
| **Interest** | Players build and train a personal AI companion while hiring other players' pawns. Each pawn arrives with its own knowledge, quest hints and combat habits, so every party feels different. |
| **Interaction with other mechanics** | It interacts with vocations and combat roles, climbing on large monsters, quest and map knowledge, the Rift Crystal economy, and Dragons plague, a disease that pawns can carry and spread to your world. |
| **Innovation** | Asynchronous multiplayer lets players affect each other's single-player games without playing together, by sharing trained AI companions that bring back gifts, knowledge and risks. |

**Hector Medina**

| Game Title | Hades and Hades 2 |
| :---- | :---- |
| **Mechanic/System** | Base and character upgrades with game progression. |
| **Interest** | The mechanics of Hades and Hades 2 offer the player a way to play the game for a long period of time and offers a way for players to try different builds for each run and achieve milestones for their base with each run after a boss is defeated. |
| **Interaction with other mechanics** | The base upgrade system tells the story of the player and progression made in the game. This tells the player what to work on next. This is what makes this base mechanic so interesting. With each run there are upgrades to gain and to test. |
| **Innovation** | Base mechanics are essential to tell the story of rogue likes. Upgrades to the player also let the player try different styles of run with each playthrough.  |

## Matrices

### Twist & Mechanics Matrix

Genre: top-down arena survival (roguelike builds within a run). Twist: anomalous weapons with hidden backfires, and a night that answers the day: the morning's kills become the night's shadows.

| Mechanics | Description | Interaction with Twist | Affected Genre Elements | Supports |
| :---- | :---- | :---- | :---- | :---- |
| **Core Mechanics** |  |  |  |  |
| Anomalous weapon combat | Aim and fire; 14 weapon forms, each with two anomalies hidden until the weapon is taken. | First half of the twist: a weapon is learned only by taking it, and its backfire becomes a move. | Combat, weapon choice | — |
| Gathering | The afternoon's ore trail; lamps and dark matter at night. | Second half of the twist: where it is safe to mine at night depends on where the morning's kills fell. | Resource loop (in place of XP drops) | Base building |
| Base building | Four rooms, three currencies; every upgrade of the run is bought here. | Tame softens a backfire and Twist adds a behavior; a faster build means more kills and more shadows. | Progression (in place of level-up cards) | Weapon combat; Gathering; Skills and elements |
| Skills, Energy and elements | Three skills paid with Energy; seven elements, each with one job. | An infusion changes what an anomalous weapon leaves on enemies; skills cannot hurt shadows. | Abilities, damage types | Weapon combat; Enemies and bosses |
| Enemies and bosses | New kinds each day; elites from Day 3; two bosses. | What was killed by day returns as its dark double at night; the Tyrant King tests the whole build, backfires included. | Difficulty curve, win condition | Weapon combat; Skills and elements |
| **Supporting Mechanics** |  |  |  |  |
| Base defense | Base HP and shield; each side tied to a room. | Fighting away from the base protects it but moves the night's shadows to where the kills fell. | Survival objective | Weapon combat; Base building |
| Night: lamps and shadows | Darkness, light circles, shadows in every region. | Is the second half of the twist. | Pacing, risk | Gathering |
| Knockback, walls and terrain | Knockback tiers, wall slams, quicksand. | Some backfires turn it on the player (Launch Yourself into a wall). | Arena, positioning | Weapon combat |
| Sprint and stamina | A short bar, usable until empty, then refilled in full. | The way out of a backfire, of the black hole's pull and of a shadow's chase. | Movement, survival | Weapon combat; Night |
| Weapon crates | Pick one of three weapons shown only by name and Mk. | Delivers the first half of the twist during exploration. | Loot | Weapon combat; Gathering |
| The black hole | Lure ten monsters in, then break it in time while it strikes back. | Lethal terrain that works for and against both sides; an optional test of the build. | Optional challenge | Weapon combat; Enemies and bosses |
| Final Overload | Leftover alloy and energy become a bonus for the final battle. | Turns the last day's gathering into combat power. | Endgame | Base building; Enemies and bosses |

### Mechanic Interaction Matrix

Each cell describes how the row mechanic acts on the column mechanic. "—" means no direct interaction.

| Subject / Object | Weapon combat | Gathering | Base building | Skills and elements | Enemies and bosses | Base defense | Night | Black hole |
| :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- | :---- |
| **Weapon combat** | — | Morning kills drop alien energy. | A weapon's backfire decides whether Tame is worth buying. | Hits carry the weapon's infusion; kills restore a little Energy. | Knockback slams enemies into walls; a Rammer that hits a wall is stunned. | Monsters near the player fight the player, not the base. | Morning kills raise shadows in their region. | Hits on the woken hole count toward its timed check. |
| **Gathering** | Crates on the ore trail give new weapons. | — | Alloy pays for weapons, the base and the ship; dark matter fills the tank. | The night's nodes carry alien energy, which pays for skills. | — | — | Dark matter is mined only in light. | The ore trail leads toward the hole. |
| **Base building** | Mk, Tame, Twist and armor change every fight. | Ship Launch Bay level 1: 30% more ore from the next day. | — | Ship Launch Bay level 3 cuts skill cooldowns and Energy costs by 30%. | Mk 4, 7 and 10 pierce part of the bosses' element immunity. | Hull and shield levels; Ship Launch Bay level 2: the base takes 25% less damage. | Ship Launch Bay level 2 widens lamp light; lamps are crafted there. | — |
| **Skills and elements** | Overcharge speeds up every weapon. | — | — | — | Frost and Lightning control enemies, but both bosses ignore them. | Bulwark raises a wall that holds enemies off the base. | Skills cannot hurt shadows; Blink opens the distance. | — |
| **Enemies and bosses** | Spitters force dodging; a Shade can kill in one strike. | Tougher enemies drop more of the day's energy. | Damage to a side of the base breaks its room. | Elite affixes put statuses on the player; a Void affix drains Energy. | — | The morning's monsters walk to the base and strike it. | The kinds killed by day are the shapes of the night's shadows. | The Nash Prophet waits behind the hole's portal. |
| **Base defense** | The base's shield takes part of each hit on a player near it. | — | A broken room sells nothing until it is repaired (15 alloy). | — | — | — | The base's floodlights are the night's safe circle. | — |
| **Night** | Shadows cannot be killed: the answers are light and the sprint. | A crowd of shadows puts a lamp out; a fall loses the night's haul. | Dark matter, the only fuel, comes only at night. | — | — | The base repairs part of its HP and refills its shield each night. | — | The hole is gone at night. |
| **Black hole** | Its pull drags the player; its pillars and rings force dodging. | After the Nash Prophet, void alloy grows where the hole stood. | Void energy and void alloy buy a greater void skill, void enchants and Mk 11\. | A void-enchanted weapon restores Energy on hit. | Swallows monsters lured into its pull. | — | — | — |

### Single Mechanic Challenge Matrix (Beta)

The five core mechanics and two supporting ones, all in Story Mode; difficulty on the course scale.

#### Anomalous weapon combat (core)

| Challenge | Location | Difficulty | Player Must Learn | Instructions | Required Skills |
| :---- | :---- | :---- | :---- | :---- | :---- |
| Fire the pistol three times | Day 0, crash site | Tutorial | Aim with the mouse, fire with the left button | Spotlight with a left-button prompt | None |
| Take a weapon from the first crate | Day 0, first crate | Tutorial | Its attack and backfire stay hidden until it is taken | Spotlight on the crate with an E prompt | Moving |
| Defeat four monsters with the new weapon | Day 0, way to the base | Easy | Use the anomaly in a real fight | Spotlight on the player | Aiming, sprint |
| Swap hands | Day 0, second crate | Tutorial | The right button swaps the two hand weapons | Spotlight with a right-button prompt; only a swap passes it | Aiming |
| Fight with the backfire, not against it | Days 2–4 | Medium | How to use each backfire (recoil into position, a delayed blast as a trap) | None | Aiming, sprint |
| Use walls as damage | Days 4–5 | Hard | Knock enemies into walls; lead Rammers into them | None | Swap, sprint, terrain |
| Defeat the Tyrant King | Final battle | Extreme | Break its shield, survive its second phase | Boss banner | All of the above |

#### Gathering (core)

| Challenge | Location | Difficulty | Player Must Learn | Instructions | Required Skills |
| :---- | :---- | :---- | :---- | :---- | :---- |
| Mine the glowing ore | Day 0, glowing corner | Tutorial | Hold E beside ore until the bar fills | Spotlight on the ore with "E Hold to mine" | Moving |
| Mine a second ore unprompted | Day 0, way to the base | Easy | Find ore without a spotlight | Goal marker only | Mining |
| Set a lamp and mine dark matter in its light | Day 0 night | Tutorial | F sets a lamp; dark matter is mined only in light | Two spotlight steps: F, then hold E | Mining |
| Follow the ore trail | Day 1 afternoon | Easy | Mining a node brings up the next, farther out; a crate lies on the trail | Goal panel and marker with the distance | Mining, navigation |
| Push the trail toward the black hole; mine two clusters at night | Days 3–4 | Medium | When to turn back; which regions are thick with shadows | None | Navigation, lamp placement |
| Fill the tank under heavy shadows | Nights 5–7 | Hard | Mine away from the regions cleared by day; bring the haul back to the base | None | All of the above |

#### Base building (core)

| Challenge | Location | Difficulty | Player Must Learn | Instructions | Required Skills |
| :---- | :---- | :---- | :---- | :---- | :---- |
| Build the first two rooms | Day 0, arrival at the base | Tutorial | E at the door opens the base; a purchase takes effect at once | The base system's voiced line | None |
| Spend Day 1's income (20 alloy, 25 energy) | Day 1 afternoon | Easy | Ship Launch Bay level 1 (more ore from tomorrow) against weapon Mk and armor | Prices as icons and numbers on each room's page | Reading costs |
| Balance three currencies and repairs | Days 2–3 | Medium | Repair a broken room (15 alloy) or upgrade; unlock the base shield | None | Planning |
| Commit to a build | Days 4–5 | Medium | High-Mk weapons or high-rarity skills; Ship Launch Bay levels 2 and 3 | None | Planning |
| Finish the ship and spend the rest | Days 6–7 | Hard | Fuel to 100; Final Overload turns leftovers into a shield or damage | None | Planning under pressure |

#### Skills, Energy and elements (core)

| Challenge | Location | Difficulty | Player Must Learn | Instructions | Required Skills |
| :---- | :---- | :---- | :---- | :---- | :---- |
| Cast the first skill | First fight after a skill is learned | Tutorial | Keys 1 to 3 cast at or toward the cursor and cost Energy | A ring on the player with the skill's key, until the skill is cast | Aiming |
| Stop a group with a control element | Days 2–3 | Easy | Frost slows and freezes; Lightning makes enemies take more damage | None | Timing, aiming |
| Choose an infusion against shields | Days 3–4 | Medium | A shield halves burn but poison ignores it | Item text in the study window | Planning |
| Handle elite affixes | Days 3–6 | Medium | An affixed elite puts its element on the player; Radiance clears statuses | Affix label beside the elite's HP bar | Skill timing |
| Fight a boss that ignores control | Nash Prophet, Tyrant King | Hard | Both ignore Frost and Lightning: bring damage over time or Mk penetration | Immunities listed on the boss bar | Build planning |

#### Enemies and bosses (core)

| Challenge | Location | Difficulty | Player Must Learn | Instructions | Required Skills |
| :---- | :---- | :---- | :---- | :---- | :---- |
| Four Grunts | Day 0 | Tutorial | Enemies chase and punch; a few shots kill them | The hero's line before they appear | Aiming |
| Grunts in numbers | Day 1 | Easy | They walk for the base; those near the player turn on the player | Goal text of the first morning | Moving, aiming |
| Spitter and Warden | Day 2 | Medium | Dodge ranged spikes; kill the Warden to drop its allies' shields | Yellow wind-up and a sound before the Spitter's shot | Dodging, target priority |
| Spire and Chanter; first elites | Day 3 | Medium | Leave the Spire's warning circle; kill the Chanter first | Name labels; the Spire's warning circle; a white outline on elites | Positioning |
| Bloater and Rammer | Day 4 | Hard | Kill the Bloater before it arrives; sidestep the Rammer's line or lead it into a wall | Charge line, yellow wind-up | Kiting, terrain |
| Stomper, then Shade | Days 5–6 | Hard | Stay out of the Stomper's slam cone; a Shade cloaks and can kill in one strike, and any hit reveals it | Yellow wind-up; the Shade's own colour and sound before it cloaks | Close-range dodging, area damage |
| The Nash Prophet | Void trial (optional) | Hard | Kill its summons to end its invulnerability; dodge its volleys and pillars | Confirm dialog before entry; boss banner | Positioning, dodging |
| Every kind, then the Tyrant King | Day 7, final battle | Extreme | Break its shield; read its moves by distance; survive its second phase | Boss banner | All of the above |

#### Night: lamps and shadows (supporting)

| Challenge | Location | Difficulty | Player Must Learn | Instructions | Required Skills |
| :---- | :---- | :---- | :---- | :---- | :---- |
| One lamp, one cluster, shadows that only watch | Day 0 night | Tutorial | Light is where mining works; shadows stay out of it | Two spotlight steps: F, then hold E | Mining |
| Two clusters after a quiet morning | Night 1 | Easy | Shadows are thicker where the morning's kills fell | The base system's voiced line | Lamp placement |
| Mine beside the regions cleared by day | Nights 3–4 | Medium | A crowd of shadows walks into a lamp's light and puts it out | The lamp's ring turns purple and flashes before they enter | Route planning, sprint |
| Bring the haul home before falling | Nights 5–7 | Hard | A fall loses the night's haul; a visit to the base keeps it | None | Risk judgment |

#### The black hole (supporting)

| Challenge | Location | Difficulty | Player Must Learn | Instructions | Required Skills |
| :---- | :---- | :---- | :---- | :---- | :---- |
| Watch three monsters spiral in | First sight of the hole | Tutorial | The hole swallows what comes near | Short cutscene; "FED 0/10" over the hole | None |
| Lure ten monsters into the pull | Any morning or afternoon | Medium | Stand in the pull so monsters follow, then sprint out | Side quest line with the count | Sprint, positioning |
| Break the hole in time | Once it is awake | Hard | Deal half the Nash Prophet's health in 45 seconds while dodging pillars and rings | Damage bar and clock over the hole; a circle counts down before each pillar | Build strength, dodging |

### Tutorialization implementation for Mechanics (Beta)

Day 0 of Story Mode is the tutorial: a walk from the crash site to the base, then a first night. Each control is taught by a spotlight step: the screen dims except for a circle around the thing to use, with a key cap and two or three words beside it. While the spotlight is up only that step's input works; doing it flashes the circle green, and the next step starts once the hero's spoken line has finished. No monster is on the map before the fight step.

After Day 0 the game guides without overlays: the goal panel names the goal with its distance, a marker points to it, and a key prompt shows only at the thing it concerns. Esc pauses on a table of every control, and the title's Tutorial button opens seven pages, each a small looping demo and one line.

| Mechanic | Where it is taught | How, and what must be done to go on | Practice that follows |
| :---- | :---- | :---- | :---- |
| Anomalous weapon combat (core) | Day 0, steps 2 and 6; second crate | Spotlight with a left-button prompt: three pistol shots. After the first crate, a fight step: four monsters with the new weapon. At the second crate a right-button spotlight passes only by swapping hands | Day 1 morning |
| Gathering (core) | Day 0, steps 3 and 7 | Spotlight on the glowing ore: hold E until the bar fills. After the fight, a spotlight on the dropped alien energy: walk over it | A second ore, unprompted; Day 1's ore trail |
| Base building (core) | Day 0, arrival at the base | No spotlight: the base system's voice states the main goal and asks for the Armament Bay and the Energy Lab, and Day 0 gives exactly their price. Levels a room has not reached read "? ? ?" | Day 1 afternoon |
| Skills, Energy and elements (core) | First fight after the first skill is learned | A ring on the player with the skill's key, until the skill is cast or 12 seconds pass. Elements have no forced step: the study window gives each infusion's effect | Days 2–6 |
| Enemies and bosses (core) | The day each kind first appears | No overlay: one or two new kinds a day, specials named above their HP bar, each with a yellow wind-up and a sound. A boss's banner names what to do ("Break its Shield") | The following days |
| Base defense | Day 1 morning | The first morning's goal says that monsters walk for the base and that those near the player attack the player first | Days 1–3 |
| Night: lamps and shadows | Day 0 night | Two spotlight steps: F sets a lamp by the dark matter, then hold E mines in its light. Four shadows watch but never touch; the base system then says that shadows fear the light and gather where the day's kills were | Night 1 |
| Knockback, walls and terrain | Open Mode's how-to-play pages | No forced step: Open Mode's page "USE THE ARENA" loops a demo ("Knock enemies into walls and black holes"); Story Mode leaves it to play | Days 4–5 (Rammers and walls) |
| Sprint and stamina | Day 0, steps 1 and 5 | Spotlight on the player: walk 4 m toward the marker (only the move keys work); later "Sprint once" passes only after a real sprint | The first fight |
| Weapon crates | Day 0, the two crates | Spotlight on the crate with an E prompt: one of three weapons must be taken; its weapon card pauses the game and shows the attack and the backfire | Each afternoon's crate |
| The black hole | First sight; side quest | A short cutscene (three monsters spiral in). The hole cannot be hurt until ten monsters have been lured into it ("FED n/10"), and the portal opens only when the timed check is passed | Later days |
| Final Overload | Day 7 | No forced step: a button on Central Control's page opens a window that says what each purchase gives | The final battle |

## Sketches and Diagrams

### Gameplay Loop

![Core loop of the current build][image1]

Core loop of the current build

### Mechanic/Level Sketches 

![Mechanic sketch: knocking enemies into a black hole and off a wall.][image2]

Mechanic sketch: knocking enemies into a black hole and off a wall.

Screenshots of implemented features:

![Wave 1 in Whitebox mode, the intended default][image3]

Wave 1 in Whitebox mode, the intended default

![The weapon card that pauses the game the moment a weapon is taken, revealing its anomaly and backfire.][image4]

The weapon card that pauses the game the moment a weapon is taken, revealing its anomaly and backfire.

![The Armory after a wave: a new weapon or a higher Mk of one already held.][image5]

The Armory after a wave: a new weapon or a higher Mk of one already held.

![Wind-up warnings in whitebox mode: the Spire's thin warning circle, the Rammer's charge line][image6]

Wind-up warnings in whitebox mode: the Spire's thin warning circle, the Rammer's charge line

![A black hole in whitebox mode: enemies chasing the player (top right) cross its gravity ring, the dashed outer circle, and are dragged toward the black core, which swallows them.][image7]

A black hole in whitebox mode: enemies chasing the player (top right) cross its gravity ring, the dashed outer circle, and are dragged toward the black core, which swallows them.

### Level Designs (Beta)

#### Story Mode

One map of 200 by 140 cells, played over Day 0, seven days and a final battle. The base stands at its centre, one black hole stands far out in a corner, and the terrain (ridges, broken walls, rock pillars, boulders, a few quicksand pools) is laid out afresh each run. Each day repeats a morning, an afternoon and a night; as of the Beta build this is Story Mode's loop, and the loop pictured above under Gameplay Loop is Open Mode's. What changes from day to day is who attacks and how much can be gathered:

| Day | New enemies | Skill tested | Alloy (afternoon) | Alien energy (kills and night nodes) | Dark matter (night) |
| :---- | :---- | :---- | :---- | :---- | :---- |
| 1 | Grunts only | Basic movement | 20 | 25 | 10 |
| 2 | Spitter, Warden | Breaking a shield, dodging ranged shots | 30 | 40 | 12 |
| 3 | Spire, Chanter; first elites | Picking off fixed targets, mid-range damage | 45 | 65 | 15 |
| 4 | Bloater, Rammer | Interrupting, kiting | 70 | 100 | 18 |
| 5 | Stomper | Close-range dodging, single-target damage | 105 | 150 | 28 |
| 6 | Shade in place of the Stomper | Area damage against cloaked enemies, avoiding one-hit kills | 160 | 220 | 36 |
| 7 | Every kind, with the Brute, Skitter and Splitter | Everything under high pressure | 240 | 300 | 45 |

Ship Launch Bay level 1 adds 30% to the alloy of every later day. Enemy health follows the damage a build of that day is expected to deal: a Grunt falls in about 0.4 seconds of it, a special in about 1.8, an elite in about 6\. The tank needs 100 of the 164 dark matter the seven nights hold.

##### Day 0: crash site to the base (tutorial)

![Day 0 in whitebox, the fight step: the spotlight on the player, its prompt at the right, the goal panel and the four monsters.][image8]

Day 0 in whitebox, the fight step: the spotlight on the player, its prompt at the right, the goal panel and the four monsters.

The player wakes at the crash site west of the base with a pistol and is led east, one spotlight step at a time: walk to a marker, fire the pistol, mine the glowing ore, open the first weapon crate, sprint, defeat four monsters, pick up the alien energy they drop. The way on passes a second ore, a second crate and purple-black crystals that cannot be mined. At the base the player builds the first two rooms; then comes the tutorial night: one lamp, one dark matter cluster near the base and four shadows that only watch.

##### Morning: defend the base

![Day 1 morning in whitebox: the base at the centre of the map, the goal panel, the base bar, and arrows at the screen edge counting the monsters walking in.][image9]

Day 1 morning in whitebox: the base at the centre of the map, the goal panel, the base bar, and arrows at the screen edge counting the monsters walking in.

Monsters are born on a ring around the base and walk in from every side; the morning ends when its timer has run out and the last of them is dead. Fighting near the base keeps the walk short but lets strikes land on its walls; fighting far out protects it but leaves the night's shadows where the kills fell.

##### Afternoon: the ore trail, a crate and the black hole

![The black hole in an afternoon, first stage: two monsters are dragged across its gravity ring; the side quest and the label over the hole count 2 of 10.][image10]

The black hole in an afternoon, first stage: two monsters are dragged across its gravity ring; the side quest and the label over the hole count 2 of 10\.

![Second stage: the woken black hole answers an attack with void pillars; standing inside a pillar's circle is death.][image11]

Second stage: the woken black hole answers an attack with void pillars; standing inside a pillar's circle is death.

No monsters come and there is no timer. Two ore nodes show at a time, and mining one brings up the next, farther along a line from the base to the black hole; the trail reaches farther each day, and the day's weapon crate lies about six tenths of the way along it. Purple shards on the ground and a low hum lead the last stretch to the hole, which has no marker of its own. The afternoon ends when the player enters the night at the base door.

##### Night: lamps, dark matter and shadows

![Night 3 in whitebox: a mining lamp's circle of light, shadows waiting at its edge, and the marker of a dark matter node 8 m away.][image12]

Night 3 in whitebox: a mining lamp's circle of light, shadows waiting at its edge, and the marker of a dark matter node 8 m away.

The map is dark outside the base's floodlights and the player's lamps. Each night's two dark matter clusters lie in regions that touch, away from the base's light. The night has no timer: it ends when the player waits for dawn at the base.

##### Final battle: the Tyrant King

![The final battle in whitebox: the Tyrant King (red) at the base, its bar with HP, shield and immunities, and the banner that names what to break.][image13]

The final battle in whitebox: the Tyrant King (red) at the base, its bar with HP, shield and immunities, and the banner that names what to break.

With the ship repaired and the tank full, the ignition fails; the Tyrant King appears outside the base and the player starts there. It opens with a shield, raises a second one on entering its second phase, and picks its moves by distance: a tail sweep, a charge that smashes walls, a thrown meteor whose fragments stand as new walls. Killing it launches the ship.

##### Hidden level: the void trial (optional)

![The void trial as it opens, in whitebox: the Nash Prophet's bar and the banner that names its rule.][image14]

The void trial as it opens, in whitebox: the Nash Prophet's bar and the banner that names its rule.

A circular platform in the void that keeps its size, with four fragments around it reached by portals; leaving the ground is death. The Nash Prophet stands at its centre and cannot be pushed. Its moves depend on the player's distance: up close a repulse pulse, rings of shots and summoned guards that make it invulnerable while they live; at range predictive volleys, void pillars and a pull toward the centre.

#### Open Mode

The arena of the Alpha build, pictured above under Mechanic/Level Sketches: a bounded map of 180 by 120 cells with the player starting at its centre, 14 timed waves with a build phase between them, then the Tyrant King. Each run has one large and one small black hole. The map select screen offers two maps and Random; each map has several wall layouts, one picked at random.

##### Dunes

Few walls, and quicksand pools that slow walking and sprinting. Open ground favours kiting and area weapons; more Bloaters and Spires spawn here.

##### Ruins

Many straight, L-shaped and bracket-shaped walls: surfaces for wall slams and lanes to funnel enemies. More Rammers spawn here.

### Concept Art

No concept art is included in this document: the game is designed in whitebox, its default look.

## Game UI \- (Beta)

All screenshots are in whitebox, the default of the English page. The interface is laid out on a 1600 by 900 reference canvas that scales with the screen size.

#### Menus

![The title screen: Settings, Language and the three entries.][image15]

The title screen: Settings, Language and the three entries.

![The Settings panel: volume, art mode and the keys of eleven actions.][image16]

The Settings panel: volume, art mode and the keys of eleven actions.

![The base page on Day 3: room tabs with their levels, the open room, its purchases with icon prices, and the ways out.][image17]

The base page on Day 3: room tabs with their levels, the open room, its purchases with icon prices, and the ways out.

![The weapon crate panel: a name, a Mk and "? ? ?" for each weapon.][image18]

The weapon crate panel: a name, a Mk and "? ? ?" for each weapon.

| Screen | Elements |
| :---- | :---- |
| Title | Settings (top left), Language (top right: English or Chinese), Start Game \- Story Mode, Start Game \- Open Mode, Tutorial |
| Settings (title and pause) | Volume slider; art mode, Illustrated or Whitebox (on the title only); the keys of eleven actions, each rebound by a click and a key press |
| Map select (the level select screen) | Opens after Start Game \- Open Mode: Dunes, Ruins, Random, Back to Title |
| Tutorial pages | Seven pages, each a small looping demo and one line |
| Pause | Controls table; Resume, Restart Run, How to Play, Quit to Title, Settings |
| Base page (full screen) | The three currencies as icons and numbers; four room tabs with their levels and a BROKEN mark; the open room's purchases with icon prices; the ways out: Back to the afternoon, Into the night, Wait for dawn, and Start the ignition sequence once the ship is ready |
| Study window (full screen) | Everything a room sells, one tile per item in rarity colours, with its value now and after the purchase ("Mk 1 ×1 → Mk 2 ×1.26") and its price; categories not yet unlocked read "???" |
| Weapon crate panel | Three weapons, each a picture, a name, a Mk and "? ? ?"; 1, 2, 3 or a click takes one |
| Weapon card | Pauses the game when a weapon is taken: its attack, damage, element and backfire |
| Confirm dialog | Before the void portal: what a fall inside costs, and yes or no |
| Cutscenes | "Day 0" card; memory; first sight of the black hole; failed ignition; ending |
| Open Mode panels | Starting weapon (one of three); level-up card pick; the Armory in the build phase |
| Result screen | Win or loss title; the day (or wave), level and kills reached; the ship's state on a loss |

#### HUD

![The HUD on Night 3, in whitebox. The numbers are the rows of the table below.][image19]

The HUD on Night 3, in whitebox. The numbers are the rows of the table below.

| \# | Element | Information |
| :---- | :---- | :---- |
| 1 | Level, HP, stamina, Energy (top left) | The player's level and three bars; the Energy bar appears with the first skill |
| 2 | Day and phase (top centre) | The day and its phase; in the morning also the seconds left and the monsters alive and still to come (Open Mode: the wave and its timer) |
| 3 | Base bar | The base's HP and shield as numbers and a bar, and the ship's state: engine, armor, fuel units |
| 4 | Currencies (top right) | Lamps left and their key (afternoon and night); alien energy, alloy and dark matter; void energy and void alloy once earned |
| 5 | Goal panel (left) | The goal; the prompt of the thing the player stands at ("E: enter the base"); one line of advice that fades; the side quest |
| 6 | Goal marker | Points to the goal with its distance, from the screen edge when the goal is off screen |
| 7 | Subtitles | The speaker (ME or BASE SYSTEM) and the voiced line, in large outlined text |
| 8 | Weapon slots (bottom) | The held weapon's name; two hand slots with an IN USE mark; two auto slots, shown from the first auto weapon |
| 9 | Skill slots | Three slots with their keys, Energy costs and cooldowns, shown from the first skill |
| — | Enemy arrows and labels | At the screen edge, an arrow and a count for the monsters off screen (Morning picture under Level Designs); over each enemy its HP bar, a special's or a boss's name and an elite's affix |
| — | Mining bar and gains | A bar at the bottom centre fills while mining (picture below); "+3 alien energy" pops where something is picked up; the screen edge pulses red at low health |
| — | Black hole and boss | Over the hole "FED n/10", then the damage share, the seconds left and a bar; the boss bar with name, HP, shield and immunities, and a banner that names what to do (pictures under Level Designs) |
| — | Night | Darkness outside the light circles of the base and the lamps; a lamp's ring turns purple before a crowd of shadows enters |

##### Tutorial prompts

![A Day 0 spotlight step: the screen dims around the ore, the key cap and three words stand beside it, and the mining bar fills at the bottom.][image20]

A Day 0 spotlight step: the screen dims around the ore, the key cap and three words stand beside it, and the mining bar fills at the bottom.

A spotlight step is a dimmed screen with one bright circle, a key cap and a short phrase ("E Hold to mine", "LMB Hold to fire"). After Day 0 the same ring and key cap appear for the first skill, without the dimming.

UI Flowchart

![UI screen flowchart: Story Mode in the middle, Open Mode on the right, the screens shared by both on the left.][image21]

UI screen flowchart: Story Mode in the middle, Open Mode on the right, the screens shared by both on the left.

# Analytics 

The four metrics follow Story Mode, per run and grouped by day. All four are collected by the web build and charted on the Dashboard tab of the team's Google Sheet. "How runs end" was Metric 2 in the Alpha version of this document and is Metric \#1 here. Open Mode's own rows go to the same sheet and are not among the four.

**Methodology Used:** 

1. Data Collection:

   * The web build sends one JSON row per event with the browser's sendBeacon to a Google Apps Script web app, which checks every field and appends the row to a Google Sheet. A row holds a random run id and page-session id (no names, accounts or IP addresses), the language, the build and the mode. Nothing is sent from the Unity editor, automated tests or a local page.

   * One tab per kind of row: runs, damage, v2\_days, v2\_purchases and v2\_events; Open Mode's picks keep their own tabs.

   * Data and dashboard: [Google Sheet "WARP ARENA analytics"](https://docs.google.com/spreadsheets/d/1elOvjBbfgnXD5yrqOw9NA02vD4WZa221_bRzFKo4E1M/edit)

2. Data Visualization:

   * The sheet's script builds the Dashboard tab: a card per metric with a live chart of players' rows and, under it, an example chart drawn from simulated runs and labelled as such. The mockups below are drawn by plot\_story\_metrics.py from 150 simulated runs (fixed seed, build simulated-demo). 

   * Code: [https\://github.com/CS526-Team-Code69/warp-arena-game/tree/main/Analytics](https://github.com/CS526-Team-Code69/warp-arena-game/tree/main/Analytics) (Apps Script, chart scripts and their tests) and Assets/AnomalyArena/Scripts/Analytics (collection) in the team repository.

## Metrics

**Metric \#1**: How runs end

**Description:** Where each Story Mode run ends: the day and phase, the run time and the cause (an enemy and its kind, burn or poison, the player's own weapon, a wall slam, the base destroyed, the Tyrant King, victory, or leaving the run). A run that never sends its end counts as unfinished.

![Mockup (simulated data, 150 runs). Both hypotheses hold here: Day 0 ends 6% of the started runs, and the day with the most defeats, Day 5, holds 18% of them.][image22]

Mockup (simulated data, 150 runs). Both hypotheses hold here: Day 0 ends 6% of the started runs, and the day with the most defeats, Day 5, holds 18% of them.

**Justification:**

Shows whether difficulty rises smoothly over the seven days, whether the player or the base is the weak point, and whether the tutorial loses players. It also checks the fix for the Alpha finding that 39 of 41 defeats came in the first five waves.

**Interpretation:**

One day holding most defeats: a spike there. Many "base destroyed": monsters reach the base too easily, or base defense is not understood. Many Day 0 endings: the tutorial is too long or unclear. Many own-weapon deaths: backfires are too harsh or unclear.

**Hypothesis:**

H1: no single day from 1 to 7 holds more than 30% of all defeats. H2: fewer than 10% of started runs end during Day 0\.

**Pipeline:**

run\_start and run\_end rows with mode story → tab runs → Dashboard, card 1: runs ended per day, by cause (stacked columns).

**Metric \#2**: Where the resources go

**Description:** Every purchase at the base: the day, room, item and price. It is read two ways: what each run buys first, and how each day's spending splits between the four rooms.

![Mockup (simulated data, 139 runs with purchases). Both hypotheses hold here. The charts label Central Control "Control Center" and the Ship Launch Bay "Ship".][image23]

Mockup (simulated data, 139 runs with purchases). Both hypotheses hold here. The charts label Central Control "Control Center" and the Ship Launch Bay "Ship".

**Justification:**

Base purchases took the place of card picks, so where the resources go is the player's build. The first purchase shows which room looks most worth having; the daily split shows whether all four rooms stay in use, and when players turn to the ship, which is the win condition.

**Interpretation:**

One room taking most first purchases: the others look weak, unclear or too expensive. A room with almost no share on most days: what it sells is not worth its price. The ship still small on Days 6 and 7: players do not see that fuel wins the run, or cannot afford it.

**Hypothesis:**

H1: no room takes more than 50% of the runs' first purchases. H2: on every day from 1 to 7, each of the four rooms receives at least 5% of that day's spending.

**Pipeline:**

v2\_purchase rows → tab v2\_purchases → Dashboard, card 2: spending per day by room (stacked columns), with the rooms of the runs' first purchases.

**Metric \#3**: Weapon choice

**Description:** Every weapon crate opened: the weapons it offered with their place on the panel (1 to 3), and which one was taken or that the crate was left. From these: each weapon's take rate when offered, each place's take rate, and the share of crates left.

![Mockup (simulated data, 820 crates in 140 runs). The simulation was given a pull toward the first place (38%, 27%, 22%) and toward some names, so both hypotheses fail in it: this is what a failed hypothesis looks like.][image24]

Mockup (simulated data, 820 crates in 140 runs). The simulation was given a pull toward the first place (38%, 27%, 22%) and toward some names, so both hypotheses fail in it: this is what a failed hypothesis looks like.

**Justification:**

A crate shows only each weapon's name and Mk (the first half of the twist), so a pick cannot follow the weapon's numbers. If picks are uneven, the cause is the name or the place on the panel, and some weapons and their anomalies are rarely met.

**Interpretation:**

The first place taken far more often: players take the first option without weighing the names. One weapon far above or below the overall rate: its name attracts or puts off, or players know it from Open Mode. A share of crates left that grows by day: an unknown weapon cannot compete with an upgraded one.

**Hypothesis:**

H1 (place): the take rates of the three places lie within 10 percentage points of each other. H2 (name): among the weapons offered at least 20 times, the most taken is taken no more than twice as often as the least taken.

**Pipeline:**

v2\_event rows of kind crate\_offer (one per weapon shown) and crate (taken or left) → tab v2\_events → Dashboard, card 3: take rate per weapon (bars), per place, and the share of crates left.

**Metric \#4**: What hurts the player

**Description:** Damage the player took per day, phase and source: each enemy kind (elites marked), the Tyrant King, the player's own weapon (backfire), wall slams and the night's shadows. It is summed as a share of max HP at each hit, so early and late days compare.

![Mockup (simulated data, 140 runs). Both hypotheses hold here: the player's own weapons cause 7% to 11% of a day's damage from Day 2 on, and no enemy kind more than 24% on Days 1 to 7.][image25]

Mockup (simulated data, 140 runs). Both hypotheses hold here: the player's own weapons cause 7% to 11% of a day's damage from Day 2 on, and no enemy kind more than 24% on Days 1 to 7\.

**Justification:**

Shows which enemy actually threatens each day (does the roster test what the Level Designs table says?), whether backfires bite without dominating (the first half of the twist), and how much of the danger comes from the night (the second half).

**Interpretation:**

One enemy kind dominating a day: a spike or a poorly read attack. Own-weapon share near zero: backfires can be ignored; very high: too punishing or unclear. Shadow share near zero: the night is safe and the day's kills cost nothing; very high: the night punishes more than the morning.

**Hypothesis:**

H1: the own-weapon share of the player's damage is between 5% and 20% on each of Days 2 to 7\. H2: on Days 1 to 7, no single enemy kind (elites counted with their kind) causes more than 40% of a day's damage.

**Pipeline:**

wave\_damage rows with mode story (one per source, sent at the end of each phase) → tab damage → Dashboard, card 4: each day's damage by source (columns stretched to 100%).

# Weekly Prototype Descriptions

**Week of Sep 28 – Oct 4**

We made the English edition of the paired prototype, Anomaly Weapon Arena (three weapons that reveal what they do on first use, three waves, gaps in the floor), tuned its weapon supply (Sep 30), and set the direction for a larger game, WARP ARENA: anomalous weapons with backfires, manual aiming, timed waves, two swappable hand weapons, a large bounded map, black holes that kill anything that enters, walls that rebound bodies, and a level-up choice of a stat, a weapon change or a skill.

![The paired prototype, Anomaly Weapon Arena.][image26]

The paired prototype, Anomaly Weapon Arena.

**Week of Oct 5 – Oct 11**

We designed WARP ARENA in detail: three starting weapons; health, stamina and sprint; knockback distance by attack type; up to three skills paid with Energy; gravity rings around black holes; a portal after 40 feeds; a post-wave Armory; timed waves, seven elements and a final boss. No build was pushed this week.

![Design notes: the WARP ARENA core loop.][image27]

Design notes: the WARP ARENA core loop.

**Week of Oct 12 – Oct 16**

* The first WARP ARENA build was deployed to GitHub Pages: timed waves and a final boss, two black holes with gravity and feeding, the void trial, the Armory, level-up cards, skills and elements. (Oct 14\)

* An area blast that broke when a kill spawned a new enemy was fixed (Oct 15).

* Weapon backfires, the enemy roster and hit feel by weapon type. Taking a weapon now pauses on a weapon card that reveals its anomaly and backfire; a sprint needs a minimum of stamina to start; picked cards play an effect that grows with rarity; the bottom HUD row no longer overlaps (Oct 16).

![Wave 1 next to the large black hole and its gravity ring.][image28]

Wave 1 next to the large black hole and its gravity ring.

**Week of Oct 17 – Oct 22**

* The run became 14 waves plus the final boss. Specials now arrive as ordinary enemies a few kinds at a time, and an elite is a special with an element affix. A sixth special, the Chanter, raises the damage of nearby allies. The building panel can be put away with Tab to walk the empty arena. A first-sight cutscene teaches black-hole feeding (Oct 21).

* A left click no longer skips it. Level-ups now open the card pick during the wave instead of after it: keep it for later, reopen it with T, one reroll per wave. XP orbs get a label and an edge arrow, and spawn points get a red cross. Enemy attacks were made faster and clearer: wind-ups are shorter, only the Spire draws a warning circle, the Stomper slams a small cone that throws the player far, and the Rammer pushes the player a little and stuns. An enemy lab scene lets the team tune one enemy type at a time. Enemies chase a visible player in a straight line instead of along grid lanes, with automated tests (Oct 22).

* Runs now report how they end to the team analytics sheet (Metric 2), and whitebox is the default art mode (Oct 22).

![The mid-wave card pick added this week.][image29]

The mid-wave card pick added this week.

![The first-sight cutscene added this week: three enemies spiral into the black hole while its feed counter][image30]

The first-sight cutscene added this week: three enemies spiral into the black hole while its feed counter

**Week of Oct 23 – Oct 30**

* Both bosses were rebuilt from the boss design. The Tyrant picks its moves by distance in two phases (tail sweep that breaks walls, ground pound, rampage charge, royal scepter, star throw, astral guard), and the Nash Prophet fights on a void platform with fragments and portals (repulse pulse, void summon, void pillars, predictive volley, equilibrium rings, black-hole pull) (Oct 25). Bosses lost the stagger meter (Oct 23). The final boss fight now counts as wave 15 for its damage (Oct 27). All 22 hand anomalies got new Twist and Tame cards, and weapons got base fixes: projectiles no longer spawn inside walls or enemies, and area effects respect walls (Oct 23).

* Progression and combat rules changed. A level-up restores HP to full, winning the void trial grants one level's rewards, and a reroll replaces all three cards once per wave (Oct 23). XP now follows wave difficulty, with level costs refit from bot runs, and spawn groups keep at least 10% for each listed kind (Oct 25). Skills have five levels and cannot be recast while their own effect is active. Element statuses stack per hit and last 3 s from the last hit, with new frost, lightning, poison and fire rules. Time flows during the build phase, and Esc pauses with a build panel open (Oct 26). Void Walk lasts 5 s and the player cannot be targeted meanwhile. While the supply crate comes down the player can walk and take an open portal, and elites wear a white outline on 3D models (Oct 26–27).

* Two teammate features reached the main branch. Animated wind-up warnings by Yuyang Bai, committed Oct 22, were merged on Oct 23: a warning fades in during the wind-up and flashes in its last 0.2 s. A player dash on the Space bar by Hector Medina (speed 27, 0.15 s, 0.4 s cooldown), committed Oct 26, was merged on Oct 29; it is on the main branch but not in the current web builds, and the team has still to decide how it fits the rule that Space does nothing outside the weapon card.

* Music plays only while the player can move (Oct 27). A higher Mk from the armory upgrades the held weapon in place and keeps its pairing, upgrades and infusion; card key words are coloured and card text is larger; every enemy shows its kind above its HP bar (Oct 28). The Chinese web build, at /zh/ on the game page, was slimmed from about 117 MB to 57 MB (Oct 27–28). The English edition was then rebuilt as a translation of the Chinese one, with Whitebox still the default and the title-screen art button (Oct 30).

**Week of Oct 31 – Nov 6**

* Help inside the game reached the main branch this week: how-to-play pages, each a small looping demo and one line, and a full controls table on the pause screen, by Wenbo Zhang; and a pulsing low-health warning by Hector Medina (pull request \#6). The weapon in use is marked, and attacks and casts flash their slots.

* The optional illustrated mode was redone, and the Bulwark skill rises as an earth wall. The Space-bar dash was taken out of the builds.

* Analytics: three more Open Mode metrics by Hongyi Wang: upgrade picks at level-up, supply drop choices and damage sources per wave.

* The game changed shape on Nov 6\. A day campaign, now Story Mode, turned the waves into seven days around a base: a morning raid, a night of lamps, dark matter and shadows, three currencies, and four rooms where every upgrade is bought. The camera became a corner-on 2.5D view, and the analytics send a row per day and per purchase. Since this build ordinary enemies show only their HP bar; specials and bosses keep their names.

![The day campaign added on Nov 6, in whitebox: a morning raid on the base (captured from the Nov 10 build).][image31]

The day campaign added on Nov 6, in whitebox: a morning raid on the base (captured from the Nov 10 build).

**Week of Nov 7 – Nov 13**

* The campaign starts with a plain pistol on its own larger map with terrain; the afternoon's ore is a trail toward the black hole; afternoons and nights lost their timers (Nov 7).

* One game in two languages (Nov 10): a Language option, English and whitebox by default, and three title entries: Story Mode, Open Mode (the 14-wave arena, behind a map select) and Tutorial. Story Mode was rebuilt: Day 0 as a forced spotlight tutorial; weapon crates that show only a name and a Mk; enemy kinds by day; void energy and void alloy; a full-screen base page and study window; Final Overload.

* After a playtest of that build the same day: prompts moved into the goal panel, a mining bar, currency icons, a lamp counter, larger subtitles, slots hidden until first owned, a sprint that works until the bar is empty, a spotlight step for the right button, and a key prompt for the first skill.

* Nights and the black hole (Nov 10 – 11): weapons fire at night but cannot hurt shadows; the base and the lamps light circles with a visible edge; shadows stand on the whole map, rise as dark doubles where monsters were killed and crowd a lamp out. The black hole must first be fed ten monsters, then broken in a timed damage check. The title has one Settings button (volume, art mode, key bindings).

* Analytics (Nov 10 – 11): Story Mode rows carry their mode, crates record what they offered, and the sheet's summaries became one Dashboard tab with a card for each of the four metrics.

![Day 0's spotlight tutorial added on Nov 10: only the taught input works until it is done.][image32]

Day 0's spotlight tutorial added on Nov 10: only the taught input works until it is done.

**Major issue from the Alpha analytics and feedback, and its solution**

* Issue: in the Alpha analytics 39 of 41 defeats came in waves 1 to 5, and no run passed wave 6\. The Alpha survey gave the reasons: too much to learn at once (7 of 22 answers), an unclear goal (6), and black holes as the most confusing part (10); 9 of the 43 ended runs were swallowed by a black hole.

* Solution, implemented in Story Mode: Day 0 teaches one control at a time in a spotlight step that cannot be passed without doing it, with no monster on the map before a first fight of four. The base system states the goal on arrival, and it stays on screen in the goal panel and on the base bar. Complexity arrives in steps: a plain pistol, then one anomalous weapon from a crate, rooms that unlock their purchases level by level, and enemy kinds added a day at a time, with Grunts alone on Day 1\. The black hole no longer stands in the opening fight: it is in a far corner, a cutscene and a counted side quest introduce it, and its pull is only a little faster than a walk. A fall in the afternoon or at night no longer ends the run.

* How we check it: Metric \#1 (How runs end) carries two hypotheses for it: fewer than 10% of started runs end on Day 0, and no single day holds more than 30% of all defeats.

# Weekly Progress

Individual work during the Alpha period (Sep 28 – Oct 22), plus the paired-prototype work it builds on. Commit links point to the team repository unless noted.

| Team Member | Week | Work | Commits |
| :---- | :---- | :---- | :---- |
| Zhe Lin | Sep 28 – Oct 4 | English edition of the paired prototype and weapon supply tuning; the WARP ARENA concept. | 4d8dd80, ea1af4c (prototype repository, no longer public)  |
| Wenbo Zhang | Before Sep 28  | Paired prototype base, pushed Sep 25: whitebox arena, missile targeting, punch, large-enemy drops, wave carry-over and the WebGL build. | [d472ecc](https://github.com/joewenbozhang-blip/anomaly-weapon-arena/commit/d472eccf565a925747394c45c6bf88c8fb1581d0), [df0a968](https://github.com/joewenbozhang-blip/anomaly-weapon-arena/commit/df0a968e2aa167676387d81b0b623a7867bfead1) |
| Zhe Lin | Oct 5 – Oct 11 | Detailed design of WARP ARENA: the system design document and its numbers sheet. | No commits this week |
| Zhe Lin | Oct 12 – Oct 16 | First WARP ARENA build and WebGL page; weapon backfires, enemy roster and hit feel; weapon card, card-pick effect and HUD layout. | [501f70f](https://github.com/CS526-Team-Code69/warp-arena-game/commit/501f70f10bdb3167b376aa092888925be1d305ec), [6187c90](https://github.com/CS526-Team-Code69/warp-arena-game/commit/6187c90151bb7fb5e3f3b52cf5bc98d0febf062e), [f8145c5](https://github.com/CS526-Team-Code69/warp-arena-game/commit/f8145c5b202c294efe5c7962b35a9374b37465b6), [c4ebdaa](https://github.com/CS526-Team-Code69/warp-arena-game/commit/c4ebdaa1113d290b2da1d3c8796cc545de1ee81e) |
| Hongyi Wang | Oct 12 – Oct 16 | Fixed the area blast that broke when a kill woke a new enemy (pull request \#1). | [b7635bf](https://github.com/CS526-Team-Code69/warp-arena-game/commit/b7635bfbf17bca0424288ff0a8fc0625439bc3c6) |
| Zhe Lin | Oct 17 – Oct 22 | Systems aligned with the design document (Chanter, cones, build-phase walk, black-hole cutscene); mid-wave card picks; XP hints and spawn crosses; weapon colours, Hook cooldown and enemy retuning; the enemy lab; the repository GDD and README; the Metric 2 analytics pipeline; whitebox as the default art mode. | [22dfec9](https://github.com/CS526-Team-Code69/warp-arena-game/commit/22dfec9a26282f2c70e31a25dd8b3c9632b8f214), [50d296b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/50d296b2d895f352d8dfc4ca0e3a310a1adf0d32), [1d88ac4](https://github.com/CS526-Team-Code69/warp-arena-game/commit/1d88ac45751ad7931788c7f7ec798bb66e8345c7), [d89b25a](https://github.com/CS526-Team-Code69/warp-arena-game/commit/d89b25aeac1eb8552c38370fb57b5b6b22d57e05), [6d0265b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/6d0265b66c8c1c3c8bf8b6a552cad5f855981f94), [2a5c185](https://github.com/CS526-Team-Code69/warp-arena-game/commit/2a5c1852998b147e5c9efdcc083d66beb1471557), [3c34098](https://github.com/CS526-Team-Code69/warp-arena-game/commit/3c34098ec04ac1b760f6a4828b467799b42d7cd8) |
| Hongyi Wang | Oct 17 – Oct 22 | Straight-line chase toward a visible player instead of grid lanes, with tests (pull request \#2); mechanics research. | [cfe11d5](https://github.com/CS526-Team-Code69/warp-arena-game/commit/cfe11d563d2637c0ea6407747665fc9edf8f8c99) |
| Yuyang Bai | Oct 17 – Oct 22 | Mechanics research (League of Legends); animated enemy wind-up warnings (feature-Yuyang branch, merged Oct 23). | [63923a0](https://github.com/CS526-Team-Code69/warp-arena-game/commit/63923a06e4817cd78222f633e3eae13981176857) |
| Wenbo Zhang | Oct 17 – Oct 22 | Mechanics research (Dragon's Dogma 2). Add the Full Controls List to the Title Screen and Pause Menu | N/A |
| Hector Medina | Oct 17 – Oct 22 | Low Health Warning | N/A |
| Zhe Lin | Oct 23 – Oct 30 | Weapon fixes and new Twist and Tame cards; void trial reward and reroll rules; keys, combat rules, waves and maps; both bosses rebuilt; XP refit; five skill levels; element rules; the 10-27 rule audit and the final fight as wave 15\. Merged pull request \#3. The music rule, armory upgrade, card text, enemy names and web slimming were developed in the private Chinese edition and released through the English edition. | [d49df5b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/d49df5bd219c2e5bddecb6617c14c058fffc7556), [ec4a4fa](https://github.com/CS526-Team-Code69/warp-arena-game/commit/ec4a4fa141de423a586a8c76e21c869bff18eabf), [281e501](https://github.com/CS526-Team-Code69/warp-arena-game/commit/281e5014b2538894b55d5b91847d5ca8f55a7f34), [078805a](https://github.com/CS526-Team-Code69/warp-arena-game/commit/078805a870f156398a00e82b685fb4d6ccb10749), [446cdf7](https://github.com/CS526-Team-Code69/warp-arena-game/commit/446cdf7f4a12f8147fe616d558d00f0d43ab372a), [108f1c9](https://github.com/CS526-Team-Code69/warp-arena-game/commit/108f1c92cf206fa6b57273fa6f127f40a21b6880), [d1b0419](https://github.com/CS526-Team-Code69/warp-arena-game/commit/d1b0419fc4e4b4a6c6c2e283850f4ffe2fa6be0b), [d290f0b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/d290f0ba653e8cbfa802d78c13c71dd69861616b) |
| Hector Medina | Oct 23 – Oct 30 | Player dash on the Space bar, speed 27 (pull request \#4, merged Oct 29). | [d9bd985](https://github.com/CS526-Team-Code69/warp-arena-game/commit/d9bd985bca3cc0bd8c2d68711ed62629ec51d056), [7fac018](https://github.com/CS526-Team-Code69/warp-arena-game/commit/7fac018050286886253b0de45d225e58e49eedcf), [7d43cb1](https://github.com/CS526-Team-Code69/warp-arena-game/commit/7d43cb18cb3fc52ad543b719c1ec9d2f3355dcf7) |
| Zhe Lin | Oct 31 – Nov 6 | Story Mode (day campaign: seven days around a base, nights, four rooms); 2.5D camera; new enemy models; analytics rows per day and per purchase. | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/afae14b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/afae14b)  |
| Hongyi Wang | Oct 31 – Nov 6 | Beta metrics: upgrade picks, supply drop choices and damage sources. | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/7f1a3a4](https://github.com/CS526-Team-Code69/warp-arena-game/commit/7f1a3a4)  |
| Yuyang Bai | Oct 31 – Nov 6 | Investigate and count where we need to add tutorials. | N/A |
| Wenbo Zhang | Oct 31 – Nov 6 | How-to-play pages (a looping demo and one line each) and a full controls table on the pause screen. | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/b74af8b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/b74af8b)  |
| Hector Medina | Oct 31 – Nov 6 | Pulsing low-health warning (pull request \#6). | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/ae2a64b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/ae2a64b)  |
| Zhe Lin | Nov 7 – Nov 13 | Larger map with terrain and ore trail; one game in two languages; Day 0 spotlight tutorial; nights with lamps and shadows; black-hole feeding; Settings. | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/7b21d37](https://github.com/CS526-Team-Code69/warp-arena-game/commit/7b21d37)  |
| Hongyi Wang | Nov 7 – Nov 13 | Object pool rework. | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/1b56b77](https://github.com/CS526-Team-Code69/warp-arena-game/commit/1b56b77)  |
| Yuyang Bai | Nov 7 – Nov 13 | Collection screen for weapons, skills and monsters (pull request \#7). | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/2fd3d5b](https://github.com/CS526-Team-Code69/warp-arena-game/commit/2fd3d5b)  |
| Wenbo Zhang | Nov 7 – Nov 13 | Analyzed the feedback from the survey and statistically determined the subsequent directions for improvement, designed some new mechanisms and gameplay elements | N/A |
| Hector Medina | Nov 7 – Nov 13 | Ship Launch Bay UI update and the fuel synthesis button. | [https\://github.com/CS526-Team-Code69/warp-arena-game/commit/55fbbec](https://github.com/CS526-Team-Code69/warp-arena-game/commit/55fbbec)  |

# Review Notes

## In-Class Pod Playtesting and Teaching Staff Feedback

### Playtest 1: Team playtest notes

* No control tutorial. Issue: players didn’t know how to move at the start and were caught and killed before they figured out the controls. Suggestion: add a control guide or short tutorial at the start, e.g. a safe area where players can get used to moving.

* Unclear meaning of “40”. Issue: a special interactive item on the map shows “40”, and players don’t understand what the number means. Suggestion: explain the item with an icon, a text label, or a tooltip that appears when the player gets close.

* Unclear game objective. Issue: players didn’t know what to do and couldn’t find any enemies. Suggestion: state the objective at the start (e.g. “Find and defeat X enemies”), and consider adding direction indicators or minimap markers.

### Feedback from October 16th, written by Zhe (Felix) Lin

* The player did not understand that the black hole counter represented the number of enemies sucked into the black hole. They initially thought it represented the number of kills.

* At the beginning, the player felt that progression was very slow. They thought they could only choose different weapons and did not realize that there was an upgrade system. They also noticed that the regular enemies did not change very much.

Feedback from October 30th in lecture playtest

1. At first, players didn't quite understand what unconventional weapons were like.  

2. Player has a bunch of enemies at the beginning 

3. Add more gameplay mechanics, not just survive the wave or kill the enemies.

4. Players need some time to familiarize themselves with the operation, so we need to add more tutorials.

5. Make sure player understand the goal of the game at the beginning

Playtest 3: Feedback from November 6th in lecture playtest

1. The weapon interface has too much text, making it less intuitive and confusing for players.  
2. Although we introduced the black hole in the player tutorial at the beginning, players still don't know much about the mechanism of the black hole.  
3. The overall objective of the game was not  clear enough, so players didn't really know what they were working toward. we need to add more milestone goals and rewards to give players a stronger sense of progression.  
4. The weaknesses of the weapons are not emphasized enough, making the gameplay somewhat repetitive.  
5. The black hole is too far away from the player. Players may be completely unaware of it

Improvement suggestions from playtests

1. Controls tutorial (team notes; Oct 30, \#4): Add a short tutorial at the beginning. There are simple animations in the tutorial to tell players how to operate.

2. Clear objective (team notes; Oct 30, \#5): Add an introduction at the beginning explaining the story or background. Let the players know the goal of the game.

3. Black hole counter and the “40” (team notes; Felix, Oct 16): label the counter as a feed count toward the portal (e.g. "Feeds 12 / 40 → Portal") with an icon or a tooltip when the player gets close, and use the same label in the first-sight cutscene.

4. Lighter start (Oct 30, \#2): make wave 1 smaller and slower.

5. Weapon preview (Oct 30, \#1): let players test a weapon before choosing it.

6. Visible upgrades (Felix, Oct 16): highlight Mk upgrades in the Armory and add a one-time prompt at the first level-up that explains stat, weapon and skill cards.

7. More variety (Felix, Oct 16; Oct 30, \#3): introduce the first special enemy earlier, and add optional wave goals that use existing mechanics

### **Alpha Survey Responses**

#### 22 responses：

![][image33]

1. Black holes were the most confusing part (10 of 22). Players weren't sure how they worked, how far the pull reached, or why swallowed enemies gave no XP. A few said the holes were too far from the fight to matter, or that the pull grabbed them from farther away than expected.

2. There was too much to learn at once (7). Controls, weapons, skills and black holes all show up at the start, with no time to practice.

3. The goal wasn't clear (6). Answers ranged from "survive" to "kill enemies" to "fill the black hole". Only 2 people mentioned surviving 14 waves and beating the boss.

4. Weapon backfires were hard to read (6). Reverse Shot made aiming feel broken, and players couldn't tell whether damage came from an enemy, their own weapon or a wall.

5. The HUD got in the way (5). Empty slots covered part of the arena early on, and the controls list on the title screen was small and hard to read.

6. Death feedback was unclear (4). Dead enemies stayed on screen for a few seconds, and the death screen didn't say what killed the player.

7. One player asked for a pause menu, which already exists on Esc but went unnoticed.

8. On the positive side, players liked the weapon twists most, especially the rocket launcher that launches you. 6 asked for more weapons.

### Alpha Analytics (Metric 2: How runs end)

1. Almost every failed run ended early: 39 of 41 defeats happened in waves 1 to 5\. The opening waves are too hard, which matches the survey and playtest comments.

2. No run made it past wave 6, so nobody reached the later waves or the boss. Our hypothesis that most runs end in waves 8 to 12 is not supported yet.

3. Enemies dealt the last hit in 26 of 43 ended runs, making them the main cause of death.

4. 9 runs ended with the player swallowed by a black hole, so players are not reading the gravity ring.

5. Weapon backfires ended only 3 runs. They rarely kill, even though survey players found them confusing.

#### Hypothesized Issues from Feedback (Gold)

To be completed for the Gold milestone.

# External References

Art: on Oct 1 the grader approved AI-generated art as an optional illustrated mode for our paired prototype, on the condition that Whitebox stays the default; on Oct 16 we told the grader that the same toggle carries over to WARP ARENA, the grader also approved.

AI tools used in making the game and this document:

| Name | Link | Usage |
| :---- | :---- | :---- |
| Meshy | [https\://www\.meshy.ai](https://www.meshy.ai) | 3D models, rigging and character animation for the optional illustrated mode: the player, the enemies and the bosses, and props of the story |
| ElevenLabs | [https\://elevenlabs.io](https://elevenlabs.io) | Sound effects, music, and the English voice lines of the story (text to speech) |
| GPT Image 2.5 | [https\://openai.com](https://openai.com) | 2D illustrations and concept art for the optional illustrated mode: the title art, the memory cutscene stills and room pictures of the story, enemy references and reference images for the Meshy models |
| Codex | [https\://openai.com/codex](https://openai.com/codex) | Running the image generation above, and the weapon, skill, stat and element icons of the illustrated mode |
| Claude | [https\://claude.com](https://claude.com) | Programming help throughout the project: gameplay systems, UI and VFX code, the analytics pipeline (collection code, Apps Script, chart scripts) and automated tests; bringing the art and audio assets into the game; and drafting and editing parts of this document |

Other external software, packages and assets (versions from the project's package manifest):

| Name | Version | Link | Usage |
| :---- | :---- | :---- | :---- |
| Unity | 6000.6.3f1 | [https\://unity.com](https://unity.com) | Game engine and WebGL build |
| Universal Render Pipeline (com.unity.render-pipelines.universal) | 17.6.0 | [https\://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest) | Rendering |
| Input System (com.unity.inputsystem) | 1.20.0 | [https\://docs.unity3d.com/Packages/com.unity.inputsystem@latest](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest) | Mouse and keyboard input |
| Localization (com.unity.localization) | 1.5.13 | [https\://docs.unity3d.com/Packages/com.unity.localization@latest](https://docs.unity3d.com/Packages/com.unity.localization@latest) | English and Chinese UI text |
| uGUI (com.unity.ugui) | 2.6.0 | [https\://docs.unity3d.com/Packages/com.unity.ugui@latest](https://docs.unity3d.com/Packages/com.unity.ugui@latest) | HUD and menus |
| Cinemachine (com.unity.cinemachine) | 6.6.0 | [https\://docs.unity3d.com/Packages/com.unity.cinemachine@latest](https://docs.unity3d.com/Packages/com.unity.cinemachine@latest) | Installed camera package |
| Test Helper (com.nowsprinting.test-helper, via OpenUPM) | 1.6.4 | [https\://github.com/nowsprinting/test-helper](https://github.com/nowsprinting/test-helper) | Helpers for the automated Play Mode tests |
| Noto Sans CJK SC (SIL Open Font License 1.1) | — | [https\://github.com/notofonts/noto-cjk](https://github.com/notofonts/noto-cjk) | UI font for English and Chinese text; the web builds ship a subset cut with fontTools |
| Blender | 5.2 | [https\://www\.blender.org](https://www.blender.org) | Rigs and clips for the nonhuman enemies' Meshy models; cutting the Chanter model to 20,000 triangles for the web build |
| fontTools | 4.62.1 | [https\://github.com/fonttools/fonttools](https://github.com/fonttools/fonttools) | Cutting the UI font down to a subset for the web build |
| Google Sheets and Google Apps Script | — | [https\://developers.google.com/apps-script](https://developers.google.com/apps-script) | Analytics collector, data tabs and the Dashboard |
| Matplotlib (Python) | 3.10 | [https\://matplotlib.org](https://matplotlib.org) | Charts of the analytics metrics in this document |

# Appendix

## Misc

[image1]: gdd/image01.png

[image2]: gdd/image02.png

[image3]: gdd/image03.png

[image4]: gdd/image04.png

[image5]: gdd/image05.png

[image6]: gdd/image06.png

[image7]: gdd/image07.png

[image8]: gdd/image08.png

[image9]: gdd/image09.png

[image10]: gdd/image10.png

[image11]: gdd/image11.png

[image12]: gdd/image12.png

[image13]: gdd/image13.png

[image14]: gdd/image14.png

[image15]: gdd/image15.png

[image16]: gdd/image16.png

[image17]: gdd/image17.png

[image18]: gdd/image18.png

[image19]: gdd/image19.png

[image20]: gdd/image20.png

[image21]: gdd/image21.png

[image22]: gdd/image22.png

[image23]: gdd/image23.png

[image24]: gdd/image24.png

[image25]: gdd/image25.png

[image26]: gdd/image26.png

[image27]: gdd/image27.png

[image28]: gdd/image28.png

[image29]: gdd/image29.png

[image30]: gdd/image30.png

[image31]: gdd/image31.png

[image32]: gdd/image32.png

[image33]: gdd/image33.png