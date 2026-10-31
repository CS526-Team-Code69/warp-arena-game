# WARP ARENA

A top-down arena survival game about unpredictable weapons, elemental builds and dangerous void phenomena, built in Unity.

**Play in the browser:** https://cs526-team-code69.github.io/warp-arena-game/ (desktop Chrome, Edge or Firefox).

## The game

Survive fourteen waves in the arena, then defeat the Tyrant. Every weapon hides one of two anomalies, each with its own backfire; black holes and walls are weapons for both sides; and a build of weapons, elements, skills and stat cards grows through each run.

The game starts in whitebox mode; the art button on the title screen switches to the optional illustrated mode. The switch changes the scene, the monsters, the characters and the combat effects; menus, icons and sounds are the same in both.

A Chinese edition, always illustrated, is at https://cs526-team-code69.github.io/warp-arena-game/zh/.

## Open the project

Use **Unity 6000.6.3f1**. Clone with Git LFS, add the folder in Unity Hub, open `Assets/AnomalyArena/Scenes/ArenaV2_EN.unity` and press Play.

```sh
git lfs install
git clone https://github.com/CS526-Team-Code69/warp-arena-game.git
```

To try enemies one at a time, open `Assets/AnomalyArena/Scenes/EnemyLab.unity` instead: there are no waves, the panel on the right spawns any enemy, and the numbers live on the assets under `Assets/AnomalyArena/V2/Data/Enemies` (edit one, then press F6 to respawn).

## Controls

| Action | Input |
| --- | --- |
| Move | WASD |
| Sprint | Hold left Shift |
| Aim / attack | Mouse / left mouse button (hold to charge or to keep firing, depending on the weapon) |
| Swap held weapon | Right mouse button |
| Skills / select a card while a card panel is open | 1, 2, 3 |
| Reopen a card pick kept for later (in combat) | T |
| Put the build panel away to walk the empty arena, and bring it back | Tab |
| Enter the void portal | E while standing on it |
| Start, open the crate, start the next wave, switch art mode | Left mouse button on the button or crate |
| Close the weapon card | Space, Enter or a click |
| Pause / resume; keep a mid-wave pick for later | Esc |
| Reroll while a card panel is open (once per wave) / restart (result screen) | R |

## Documentation

- [Game design document](Docs/GDD.md)
- [Developer guide](Assets/AnomalyArena/README.md): where each system's code and data live, and how the two art modes differ.
- [Analytics: how runs end](Analytics/README.md): the WebGL build reports how each run ends to a Google Sheet (wave, cause, run time; random ids only). The collection code is in `Assets/AnomalyArena/Scripts/Analytics`.

## Team

Felix Lin, Joe Zhang, Hongyi Wang, Yuyang Bo and Hector Medina— CS526-Team-Code69.
