# WARP ARENA

A top-down arena survival game about unpredictable weapons, elemental builds and dangerous void phenomena, built in Unity by a four-person team.

## The game

Fight through fifteen waves, open the supply crates that drop between them, and combine anomalous weapons with skills and stat cards. Fodder is there to be cleared quickly; specialists, elites and bosses give damage over time and elemental effects room to matter.

- Seventeen weapon forms, each with two anomaly variants, upgraded through armory Mk tiers.
- Sixteen active skills, including Flame Wake, Gravity Well and the greater void skills.
- Fire, Poison, Frost, Lightning, Earth, Light and Void, each with its own formula (see [V4 numbers](Docs/V4-NUMBERS.md)).
- Seven stat cards plus Element Penetration, in Common, Rare, Exceptional, Legendary and Void rarity.
- Player-relative spawning, offscreen enemy indicators, XP attraction and supply crates opened by the player.
- Saved scenes, editable prefabs, Inspector parameters, animation, audio and pooled effects.

## Open the project

Use **Unity 6000.6.3f1** with URP and the packages pinned in `Packages/`.

```sh
git lfs install
git clone https://github.com/CS526-Team-Code69/warp-arena-game.git
cd warp-arena-game
git lfs pull
```

Add the folder in Unity Hub and let the import finish. Open **`Assets/AnomalyArena/Scenes/ArenaV2_EN.unity`** and press Play. Scene objects and component references are saved in the scene; no generation step is needed.

`ArenaV2.unity` is a second English entry scene for editing. `Arena.unity` keeps the original prototype, which the low-level weapon tests still use.

## Controls

| Action | Input |
| --- | --- |
| Move / sprint | WASD / hold Shift |
| Aim / attack | Mouse / left mouse button |
| Swap held weapon | Right mouse button |
| Active skills / offered choices | 1, 2, 3 |
| Open a landed crate | Click its prompt, Enter, or Space |
| Pause / resume | Escape |
| Start / advance when offered | Space or the screen button |
| Restart at results / reroll at draft | R |

## Code and tuning

Paths are relative to `Assets/AnomalyArena/`.

| System | Entry point |
| --- | --- |
| Movement | `Scripts/Characters/PlayerController.cs` |
| Enemies and waves | `Scripts/Characters/Enemy.cs`, `Scripts/Game/WaveManager.cs` |
| Weapons and armory | `Scripts/Weapons/V2/`, `V2/Data/WeaponRoster.asset` |
| Skills and elements | `Scripts/Skills/`, `Scripts/Elements/`, `Data/StatusRules.asset`, `Data/Skills/` |
| Enemy HP and TTK | `Scripts/Game/EncounterBalance.cs`, `Data/EncounterBalance.asset` |
| Growth and drafts | `Scripts/Progression/`, `Data/ProgressionRules.asset` |
| Presentation | `Scripts/Art/`, `Scripts/Audio/`, `Scripts/UI/` |
| Automated tests | `Tests/Runtime/` (Play Mode), `Tests/Editor/` (Edit Mode) |

Folder and namespace names (`AnomalyArena`) keep the working title so asset references stay intact. The product name is **WARP ARENA**.

The combat-feel layer (black-hole layers, status and buff effects, hit sparks, three armory cards) is saved in the scenes. To re-apply it after changing its textures or adding a scene, use **Anomaly Arena → V4 → Install Combat Feel**; it is safe to run more than once.

## Documentation

| Document | Contents |
| --- | --- |
| [Game design](Docs/GDD.md) | Full design: rules, systems, content and decision log |
| [V4 numbers](Docs/V4-NUMBERS.md) | Current element, stat-card, weapon Mk and skill formulas, with the model in `Docs/tools/` |
| [Combat balance](Docs/COMBAT-BALANCE.md) | Wave HP budgets and time-to-kill targets |
| [Progression](Docs/PROGRESSION.md) | XP, levels, drafts and rarity |
| [Design decisions](Docs/DESIGN-DECISIONS.md) | Decisions and the reasons behind them |
| [Implementation notes](Docs/IMPLEMENTATION-NOTES.md) | How the V2 scenes, data and presentation are built |
| [Technical backlog](Docs/TECH-BACKLOG.md) | Open engineering tasks and acceptance criteria |
| [Project status](Docs/PROJECT-STATUS.md) | What works, what is verified, what is next |

Time-to-kill targets for an ordinary build are **0.5–0.8 s for fodder, 5–10 s for elites and 45–75 s for bosses**. The reference DPS behind them is a tuning assumption, not recorded player data.

## Assets

3D characters and props were generated with Meshy, then rigged and animated for the game. Effect sprites are authored as SVG (sources in `Art/V4/VFX/Sources/SVG/`), and sound effects were produced with ElevenLabs. Earlier art that the current game no longer uses is kept in `ArtSource/Archive/`, outside `Assets/`, so Unity does not import it. Large art and audio files use Git LFS; keep each `.meta` file with its asset.

The `gh-pages` branch of the previous repository holds an older browser prototype; there is no current WebGL build.

## Team

Felix Lin, Joe Zhang, Hongyi Wang and Yuyang Bo— CS526-Team-Code69.
