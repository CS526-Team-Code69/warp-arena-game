# WARP ARENA — developer guide

This folder holds everything the game uses: code, scenes, data, art, audio and tests. Unity **6000.6.3f1**, URP. Paths below are relative to `Assets/AnomalyArena/` unless they start with `Docs/`, `Packages/` or `Logs/`.

The design is in [Docs/GDD.md](../../Docs/GDD.md); current numbers are in [Docs/V4-NUMBERS.md](../../Docs/V4-NUMBERS.md) and [Docs/COMBAT-BALANCE.md](../../Docs/COMBAT-BALANCE.md); test results and open work are in [Docs/PROJECT-STATUS.md](../../Docs/PROJECT-STATUS.md).

## Scenes: which one to open

| Scene | Use |
|---|---|
| `Scenes/ArenaV2_EN.unity` | **Open this one.** The game. It is the only scene in the build settings. |
| `Scenes/ArenaV2.unity` | A second copy of the V2 game scene. The V4 presentation menu (below) updates both V2 scenes. |
| `Scenes/Arena.unity` | The 1.0 prototype: a single irregular arena, three waves, six anomalies on ground weapons. Kept for reference. Its arena meshes are in `Scenes/ArenaGeometry.asset`. |

Scene objects and component references are saved in the scenes. Normal work does not need any setup command: open `ArenaV2_EN.unity` and press Play.

## Folder layout

| Path | Contents |
|---|---|
| `Scripts/` | Runtime code, assembly `AnomalyArena` (root namespace `AnomalyArena`) |
| `Editor/` | Editor tools, assembly `AnomalyArena.Editor` (namespace `AnomalyArena.EditorTools`) |
| `Tests/Runtime/` | Play Mode tests, assembly `AnomalyArena.Tests` |
| `Tests/Editor/` | Edit Mode tests and the test-result writer, assembly `AnomalyArena.Editor.Tests` |
| `Data/` | Rule assets for the V2 game: `ProgressionRules`, `StatusRules`, `EncounterBalance`, `SkillCatalog`, one asset per element in `Data/Elements/`, one per skill in `Data/Skills/` |
| `V2/Data/` | `WeaponRoster` (weapon forms and the Mk table), `Waves15` (wave table), biomes and map layouts (`DustFlats`, `RiftZone`, `InterstellarBattlefield` and their `…Map` assets, more layouts in `ExpandedWorld/`), enemy definitions in `Enemies/` |
| `V2/Prefabs/` | `PlayerV2` and one prefab per enemy type, including the bosses `Hollow` and `Tyrant` |
| `V2/Materials/` | Materials for the V2 scene |
| `Prefabs/` | 1.0 prototype prefabs: Player, EnemySmall, EnemyLarge, Gun, Knife, Missile |
| `Effects/` | Value assets for the six original anomalies: `GunChargedShot`, `GunReverseShot`, `KnifeThrow`, `KnifeHook`, `MissileStrike`, `MissileSelfLaunch` |
| `Art/` | `ArtSet.asset` (1.0 illustrated mode) and `Art/Textures/`; `Art/V2/` (player model, shaders, SVG sources, `V2ArtSet.asset`); `Art/V3/` (enemy models and animations, ground, supply crate, title screen); `Art/V4/VFX/` (combat effect sprites and their SVG sources) |
| `Audio/` | Sound effects in `Audio/V2/` and the full sound bank in `Audio/V3/Full/` |
| `Localization/` | Unity Localization settings, the `en` and `zh-Hans` locales, the `ArenaUI` string table and `ArenaAssets` asset table |
| `Fonts/` | The packaged UI font and its licence (see `Fonts/README.md`) |
| `Materials/` | Shared base materials, including `Base` and `FxTransparent` |

Folder, namespace and asset names still use the working title *Anomaly Arena*. Renaming them would break references; the product name is **WARP ARENA**.

## Main systems and where their data lives

| System | Code | Data to tune |
|---|---|---|
| Run flow, build phase, armory, UI commands | `Scripts/Game/GameManagerV2.cs`, `Scripts/Game/GameManager.cs` | Components on the GameManager object in the scene |
| Waves, spawning, enemy HP growth | `Scripts/Game/WaveManager.cs`, `WaveSet.cs`, `EncounterBalance.cs` | `V2/Data/Waves15.asset`, `Data/EncounterBalance.asset` |
| Player movement, sprint, character states | `Scripts/Characters/PlayerController.cs`, `Stamina.cs`, `Combatant.cs` | `V2/Prefabs/PlayerV2.prefab` |
| Enemies | `Scripts/Characters/Enemy.cs`, `EnemyDefinition.cs` | `V2/Data/Enemies/*.asset`, `V2/Prefabs/*.prefab` |
| Hand and auto weapons, armory offers, Mk tiers | `Scripts/Weapons/V2/` (`RunWeapons`, `RunWeapon`, `RunWeaponsUpgrades`, `WeaponRoster`, `AttackActor`, `AttackPool`) | `V2/Data/WeaponRoster.asset` (Mk table and unlock waves), `Effects/*.asset` for the six original anomalies |
| XP, levels, drafts, stat cards, rarity | `Scripts/Progression/` | `Data/ProgressionRules.asset` (stat cards, caps, level costs, rarity odds) |
| Elements and Shield | `Scripts/Elements/` (`ElementStatus`, `ElementCombat`, `StatusRules`) | `Data/StatusRules.asset`, `Data/Elements/*.asset` |
| Skills and Energy | `Scripts/Skills/` | `Data/SkillCatalog.asset`, `Data/Skills/*.asset` |
| World: map, walls, black holes, portal, biomes | `Scripts/Arena/` (`WorldMap`, `MapGrid`, `MapLayout`, `BlackHole`, `Biome`, `CameraRig`) | `V2/Data/` biome and map assets |
| HUD and screens | `Scripts/UI/` (`V2Hud`, `V2UiScreen`, `ArenaUiSnapshot`, …) | UI objects in the scene |
| Presentation: models, telegraphs, hit effects | `Scripts/Art/` (`V2*Presentation`, `V4CombatVfx`), `Scripts/Game/Fx.cs`, `FxPool.cs` | `Art/V2/V2ArtSet.asset`, the `FxPool` and `V4 Combat VFX` objects in the scene |
| Audio | `Scripts/Audio/` | `Audio/`, audio components in the scene |
| Text and locales | `Scripts/Localization/` (`ArenaText`, `ArenaTerms`, `SceneLocalePreset`) | `Localization/` |

Rule assets are ScriptableObjects. Select one and edit it in the Inspector; the change is saved with the asset. `ProgressionRules` stats are copied per run, so play never writes back into the asset.

## Running the tests

Tests use the Unity Test Framework and [TestHelper](https://github.com/nowsprinting/test-helper) 1.6.4, which comes from OpenUPM through the scoped registry in `Packages/manifest.json`.

1. Open **Window → General → Test Runner**.
2. **PlayMode** tab: run `AnomalyArena.Tests` (everything in `Tests/Runtime/`). The tests load their own setups; nothing needs to be open.
3. **EditMode** tab: run `AnomalyArena.Editor.Tests` (`Tests/Editor/`).

Menu **Anomaly Arena → 4. Run Play Mode Tests** runs the Play Mode assembly without the window. After every run, from the window or the menu, the results are written to `Logs/TestResults.xml` and a summary to `Logs/TestResults.txt`, whose first line reads `FINISHED Passed: passed N, failed 0, …` when everything passes.

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
| **Anomaly Arena → 1. Build Whitebox Scene** (`Editor/ArenaSetup.cs`) | Rebuilds the 1.0 prototype `Arena.unity`, its prefabs and materials | Overwrites `Arena.unity` and `Prefabs/`. Tuned `Effects/` assets are kept. Does not touch the V2 scenes. |
| **Anomaly Arena → 2. Build WebGL** | Builds `Arena.unity` to `Builds/WebGL/` | Builds the 1.0 prototype only, not the current game. |
| **Anomaly Arena → 3. Update Existing Scene** | Patches `Arena.unity` and the 1.0 prefabs | 1.0 prototype only. |
| **Anomaly Arena → 5. Rebuild Arena Geometry** (`Editor/ArenaGeometryBuilder.cs`) | Rebuilds the prototype arena meshes from `Scripts/Arena/ArenaShape.cs` | 1.0 prototype only. |
| `Editor/ArenaV2Setup.cs` | Creates the V2 scene and data from scratch | No menu item. Refuses to run if `ArenaV2.unity` exists. |
| `Editor/ProgressionDataSetup.cs` | Creates the progression, element and skill assets | No menu item; called by `ArenaV2Setup`. |
| `Editor/EnglishPublication.cs` | Batch-mode tool that sets the English locale in `ArenaV2_EN.unity` | Needs a `-translationMap` command-line argument. Not needed for normal work. |

## Conventions

- **Commit every `.meta` file with its asset**, and delete them together. A missing or regenerated `.meta` gives the asset a new GUID and breaks every scene and prefab that refers to it.
- **Coordinate scene edits.** `ArenaV2_EN.unity` and `ArenaV2.unity` are large YAML files that do not merge well. Tell the team before editing a scene, keep scene commits small, and do not run the V4 menu while someone else has scene changes in progress.
- Large art and audio files are stored with Git LFS. Run `git lfs pull` after cloning.
- Tune values in the data assets and the scene, not in code defaults. Code defaults matter only when an asset is created or a setup tool resets it.
- Rename assets inside Unity (or move the `.meta` with them) so the GUID is kept.
- New gameplay rules should get a Play Mode test in `Tests/Runtime/`, next to the system they cover.
