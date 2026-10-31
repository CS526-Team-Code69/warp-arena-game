# WARP ARENA — developer guide

This folder holds everything the game uses: code, scenes, data, art, audio and tests. Unity **6000.6.3f1**, URP. Paths below are relative to `Assets/AnomalyArena/` unless they start with `Docs/`, `Packages/` or `Logs/`.

The design is in [Docs/GDD.md](../../Docs/GDD.md). Current numbers live in the data assets listed below.

## Scenes: which one to open

| Scene | Use |
|---|---|
| `Scenes/ArenaV2_EN.unity` | **Open this one.** The game. It is the only scene in the build settings. |
| `Scenes/ArenaV2.unity` | A second copy of the game scene. The V4 presentation menu (below) updates both. |
| `Scenes/EnemyLab.unity` | No waves: a panel on the right spawns any enemy, always with the art on. Press F6 to respawn after editing an enemy asset. |

Scene objects and component references are saved in the scenes. Normal work does not need any setup command: open `ArenaV2_EN.unity` and press Play.

## Folder layout

| Path | Contents |
|---|---|
| `Scripts/` | Runtime code, assembly `AnomalyArena` (root namespace `AnomalyArena`) |
| `Editor/` | Editor tools, assembly `AnomalyArena.Editor` (namespace `AnomalyArena.EditorTools`) |
| `Tests/Runtime/` | Play Mode tests, assembly `AnomalyArena.Tests` |
| `Tests/Editor/` | The test-result writer, assembly `AnomalyArena.Editor.Tests` |
| `Data/` | Rule assets: `ProgressionRules`, `StatusRules`, `EncounterBalance`, `SkillCatalog`, one asset per element in `Data/Elements/`, one per skill in `Data/Skills/` |
| `V2/Data/` | `WeaponRoster` (weapon forms and the Mk table), `Waves15` (wave table), biomes and map layouts (`DustFlats`, `RiftZone`, `InterstellarBattlefield` and their `…Map` assets, more layouts in `ExpandedWorld/`), enemy definitions in `Enemies/` |
| `V2/Prefabs/` | `PlayerV2` and one prefab per enemy type, including the bosses `Hollow` and `Tyrant` |
| `V2/Materials/` | Materials for the game scene |
| `Art/` | `Art/V2/` (player model, shaders, SVG sources, weapon, skill and element icons, `V2ArtSet.asset`); `Art/V3/` (enemy models and animations, ground, supply crate, title screen); `Art/V4/VFX/` (older combat effect sprites and their SVG sources); `Art/V5/Terrain/` (ground and wall shaders and textures) |
| `Resources/V5/` | Loaded at run time: `V5Look.asset` (effect tuning), effect shaders, `Fx/`, `Terrain/` and `UI/` textures, and `Audio/` (the weapon, skill and boss sounds as `.bytes`, see Audio below) |
| `Audio/` | `Audio/V2/`, the full sound bank in `Audio/V3/Full/`, and `Audio/V5/` |
| `Localization/` | Unity Localization settings, the `en` and `zh-Hans` locales, the `ArenaUI` string table and `ArenaAssets` asset table |
| `Fonts/` | The packaged UI font and its licence (see `Fonts/README.md`) |
| `Materials/` | Shared base materials, including `Base` and `FxTransparent` |

Folder, namespace and asset names still use the working title *Anomaly Arena*. Renaming them would break references; the product name is **WARP ARENA**.

## Main systems and where their data lives

| System | Code | Data to tune |
|---|---|---|
| Run flow, build phase, armory, UI commands | `Scripts/Game/GameManagerV2.cs`, `GameManagerFlow.cs`, `GameManager.cs` | Components on the GameManager object in the scene |
| Waves, spawning, enemy HP growth | `Scripts/Game/WaveManager.cs`, `WaveSet.cs`, `EncounterBalance.cs` | `V2/Data/Waves15.asset`, `Data/EncounterBalance.asset` |
| Player movement, sprint, character states | `Scripts/Characters/PlayerController.cs`, `Stamina.cs`, `Combatant.cs` | `V2/Prefabs/PlayerV2.prefab` |
| Enemies | `Scripts/Characters/Enemy.cs`, `Enemy.Tyrant.cs`, `Enemy.Prophet.cs`, `EnemyDefinition.cs` | `V2/Data/Enemies/*.asset`, `V2/Prefabs/*.prefab` |
| Hand and auto weapons, armory offers, Mk tiers | `Scripts/Weapons/V2/` (`RunWeapons`, `RunWeapon`, `RunWeaponsUpgrades`, `WeaponRoster`, `WeaponGuide`, `AttackActor`, `AttackPool`) | `V2/Data/WeaponRoster.asset` (Mk table and unlock waves), the upgrade tuning on the RunWeapons component |
| XP, levels, drafts, stat cards, rarity | `Scripts/Progression/` | `Data/ProgressionRules.asset` (stat cards, caps, level costs, rarity odds) |
| Elements and Shield | `Scripts/Elements/` (`ElementStatus`, `ElementCombat`, `StatusRules`) | `Data/StatusRules.asset`, `Data/Elements/*.asset` |
| Skills and Energy | `Scripts/Skills/` | `Data/SkillCatalog.asset`, `Data/Skills/*.asset` |
| World: map, walls, black holes, portal, biomes | `Scripts/Arena/` (`WorldMap`, `MapGrid`, `MapLayout`, `BlackHole`, `Biome`, `CameraRig`) | `V2/Data/` biome and map assets |
| HUD and screens | `Scripts/UI/` (`V2Hud`, `V2UiScreen`, `ArenaUiSnapshot`, `V2WorldLabels`, …) | UI objects in the scene |
| Menu look, in both art modes | `Scripts/UI/V5MenuSkin.cs`, `V5TextGradient.cs` | `Resources/V5/UI/` |
| Card and help text: coloured key words, text that fits its box | `Scripts/UI/ArenaEmphasis.cs`, `ArenaTextFit.cs` | The key-word list in `ArenaEmphasis` |
| Presentation: models, telegraphs | `Scripts/Art/` (`V2*Presentation`), `Scripts/Game/Fx.cs`, `FxPool.cs` | `Art/V2/V2ArtSet.asset`, the `FxPool` object in the scene |
| Combat effects of the art mode, bloom, automatic quality drop | `Scripts/Art/V5Vfx.cs` and its `V5Vfx.*.cs` parts, `V4CombatVfx.cs` | `Resources/V5/V5Look.asset` |
| Terrain of the art mode: ground, walls, quicksand pits, Bulwark rock wall | `Scripts/Art/V5TerrainGround.cs`, `V5TerrainWalls.cs`, `V5TerrainWallArt.cs`, `V5TerrainPits.cs`, `V5EarthWall.cs` | `Art/V5/Terrain/`, `Resources/V5/Terrain/` |
| Audio | `Scripts/Audio/` (`V3WorldAudio`, `V5WeaponAudio`) | `Audio/`, `Resources/V5/Audio/`, audio components in the scene |
| Run-ending analytics | `Scripts/Analytics/` | See [Analytics/README.md](../../Analytics/README.md) |
| Text and locales | `Scripts/Localization/` (`ArenaText`, `ArenaTerms`, `SceneLocalePreset`) | `Localization/` |

Rule assets are ScriptableObjects. Select one and edit it in the Inspector; the change is saved with the asset. `ProgressionRules` stats are copied per run, so play never writes back into the asset.

## Art modes

The English scene starts in whitebox, and the art button on the title screen (`ART: WHITEBOX` / `ART: INTERSTELLAR`) switches to the art. The Chinese edition's scene has no button and is always in the art. `Art.Enabled` holds the current mode.

| Content | Whitebox | Art |
|---|---|---|
| Menus, cards, icons, enemy bar width | Same in both | Same in both |
| Sounds | Same in both | Same in both |
| Scene, monsters and characters | Whitebox materials | Art materials and models |
| Combat effects and bloom | Older effects, no bloom | New effects with bloom |

New world visuals go behind `Art.Enabled` or `V5Vfx.On`; menus and icons do not. Anything created before the run starts must also undo itself, because the title can switch modes more than once in one scene.

## Audio

The hit, shot and loop sounds in `Resources/V5/Audio/` are kept as `.bytes` and handed directly to the browser at run time. Unity's AAC encoding for web builds cut about 10 ms from the start of each sound and broke every loop. Music, ambience, UI sounds and footsteps still go through the normal import.

## Running the tests

Tests use the Unity Test Framework and [TestHelper](https://github.com/nowsprinting/test-helper) 1.6.4, which comes from OpenUPM through the scoped registry in `Packages/manifest.json`.

1. Open **Window → General → Test Runner**.
2. **PlayMode** tab: run `AnomalyArena.Tests` (everything in `Tests/Runtime/`). The tests load their own setups; nothing needs to be open.

After every run the results are written to `Logs/TestResults.xml` and a summary to `Logs/TestResults.txt`.

Stop Play Mode and save open scenes before starting a run.

## Re-applying the V4 presentation

Menu **Anomaly Arena → V4 → Install Combat Feel** (`Editor/InstallV4CombatFeel.cs`). It is safe to run again. It:

- re-imports the sprites in `Art/V4/VFX/Textures/`;
- updates `ArenaV2.unity` and `ArenaV2_EN.unity`: hit-spark frames on the `FxPool`, the `V4 Combat VFX` object, black-hole layers and the armory choice buttons, then saves both scenes;
- writes the V4 values into data assets: the stat cards, caps, crit and armor settings in `Data/ProgressionRules.asset`, the Mk table in `V2/Data/WeaponRoster.asset` and the Shield shares in `Data/Skills/Bulwark.asset`.

The last step **overwrites** any tuning done by hand in those four values. Re-apply your changes afterwards, or change the defaults in code first. The menu refuses to run during Play Mode or when the open scene has unsaved changes.

## Other editor tools

| Tool | What it does | Note |
|---|---|---|
| **Anomaly Arena → Build WebGL (V2)** (`Editor/WebGLBuild.cs`) | Builds the game for the browser | Published builds are slimmed in a separate copy of the project: a font subset and a lighter Chanter model. This project keeps the full assets. |
| **Anomaly Arena → V4 → Install Interface**, **Remove 1.0 Leftovers** | Older scene installers | Normal work does not need them. |
| **Anomaly Arena → V5 → Install …** (`Editor/InstallV5*.cs`) | Installs saved hooks into the scenes: audio bank, art hooks, Chanter, cone effects, enemy roster, interface scale, spawn cross | Normal work does not need them. They write into scenes, so read the tool first. |
| **Anomaly Arena → V5 → Run XP Pacing Driver** (`Editor/RunXpPacingDriver.cs`) | Plays a scripted run to check the XP pacing | Re-run it after any XP or wave change. |
| `Editor/ProgressionDataSetup.cs` | Creates the progression, element and skill assets | No menu item. |
| `Editor/EnglishPublication.cs` | Batch-mode tool that sets the English locale in `ArenaV2_EN.unity` | Needs a `-translationMap` command-line argument. Not needed for normal work. |

## Conventions

- **Commit every `.meta` file with its asset**, and delete them together. A missing or regenerated `.meta` gives the asset a new GUID and breaks every scene and prefab that refers to it.
- **Coordinate scene edits.** `ArenaV2_EN.unity` and `ArenaV2.unity` are large YAML files that do not merge well. Tell the team before editing a scene, keep scene commits small, and do not run the V4 menu while someone else has scene changes in progress.
- Large art and audio files are stored with Git LFS. Run `git lfs pull` after cloning.
- Tune values in the data assets and the scene, not in code defaults. Code defaults matter only when an asset is created or a setup tool resets it.
- Rename assets inside Unity (or move the `.meta` with them) so the GUID is kept.
- New gameplay rules should get a Play Mode test in `Tests/Runtime/`, next to the system they cover.
