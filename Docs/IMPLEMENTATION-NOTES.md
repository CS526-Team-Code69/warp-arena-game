# V2 implementation notes

How the V2 game (GDD v2.0 draft 5) is put together in Unity: scenes, saved objects, data assets, pools, localization and presentation. Design rules live in [GDD.md](GDD.md) and [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md); progression, element and skill code contracts in [PROGRESSION.md](PROGRESSION.md); current numbers in [V4-NUMBERS.md](V4-NUMBERS.md) and [COMBAT-BALANCE.md](COMBAT-BALANCE.md); test and verification status in [PROJECT-STATUS.md](PROJECT-STATUS.md).

All paths below are under `Assets/AnomalyArena/` unless written in full.

## Scenes

| Scene | Purpose |
| --- | --- |
| `Scenes/ArenaV2_EN.unity` | The scene in the build settings (English locale). |
| `Scenes/ArenaV2.unity` | The V2 game scene. |
| `Scenes/Arena.unity` | The earlier 1.0 prototype, kept unchanged. |

Saved hierarchy roots in the V2 scene include World, ArenaV2, Player, GameManager, WaveManager, Main Camera (with `CameraRig`), Directional Light, the prepared pools, V2Canvas and V2EventSystem.

## Editor setup

Scenes and data are built by editor scripts, not by hand-editing `.unity` or `.prefab` files. The game never calls these at runtime; at play time it only activates and moves saved objects.

| Script | What it does |
| --- | --- |
| `Editor/ArenaV2Setup.cs` (`Install()`) | Builds the V2 scene: map, biomes and wall groups, black holes, trial platform, player, pools, camera rig and light. Calls `ProgressionDataSetup.CreateAssets()` first. |
| `Editor/ProgressionDataSetup.cs` (`CreateAssets()`) | Creates missing progression, status, skill and element assets. Never resets existing ones. |
| `Editor/ArenaSetup.cs` | 1.0 scene builder and WebGL build (menu Anomaly Arena → 1, 2, 3). |
| `Editor/ArenaGeometryBuilder.cs` | Rebuilds the irregular 1.0 arena geometry (menu Anomaly Arena → 5). |
| `Editor/InstallV4CombatFeel.cs` | Applies the V4 numbers and combat-feel presentation (menu Anomaly Arena → V4 → Install Combat Feel). Safe to run again. |

The HUD canvas, illustrations, models and audio sources were added to the scene by one-time setup scripts that have since been removed; they now live in the saved scene.

Prefabs for the thirteen enemy kinds (eleven regular types, Hollow, Tyrant) and `PlayerV2` are in `V2/Prefabs/`.

## Runtime structure

`Scripts/Game/GameManagerV2.cs` owns the run flow and time. It ticks each system with the right clock: `WorldDeltaTime` (scaled by Time Slip) for enemies, world actors, wave clocks, orbs and enemy statuses; `PlayerDeltaTime` for player cooldowns, statuses and progression.

| Area | Main scripts | What is implemented |
| --- | --- | --- |
| Run flow | `Game/GameManagerV2.cs`, `Game/WaveManager.cs`, `UI/ArenaUiState.cs` | Title, three starting forms, wave intro, 15 timed waves, Armory, banked drafts, replacement, Ready, pause, void trial and reward, final boss, result. |
| Map | `Arena/WorldMap.cs`, `MapGrid.cs`, `MapLayout.cs`, `Biome.cs`, `BlackHole.cs` | 100 × 70 map, four closed boundary walls, two black holes (large size 7 at (−32, −20), small size 3 at (32, 20)), separate trial platform. |
| Camera | `Arena/CameraRig.cs` | Follows the player, clamps to the map, adds shake as an offset. |
| Enemies and bosses | `Characters/Enemy.cs`, `EnemyDefinition.cs`, `Arena/WaveSpawnMarker.cs`, `Arena/EnemyBolt.cs` | Eleven regular types, Hollow and Tyrant; safe spawn filtering with a grid fallback. |
| Progression | `Progression/RunProgression.cs`, `ProgressionRules.cs` | XP to level 10, Stat-only drafts after MAX, Energy and Void Energy, rarity. See [PROGRESSION.md](PROGRESSION.md). |
| Elements and skills | `Elements/ElementStatus.cs`, `ElementCombat.cs`, `Skills/SkillLoadout.cs` | Seven elements, Shield, 13 normal and 3 Greater Void skills. |
| Weapons | `Weapons/V2/RunWeapons.cs`, `RunWeaponsAutomation.cs`, `RunWeaponsUpgrades.cs`, `AttackActor.cs`, `WeaponRoster.cs` | Two hand slots and three auto slots; 14 hand forms × 2 anomalies; 3 auto forms × 2; Twist/Tame for every hand anomaly; weapon Mk tiers. |
| HUD | `UI/V2Hud.cs` and the other `UI/V2*.cs` (uGUI) | Menus, resource and cooldown bars, loadout, weapon and element images, boss resources with three immunity glyphs, ten banked-draft pips, greyed stamina when exhausted, enemy direction arrows. |

### Weapon behaviour notes

- Time Slip's callback takes `(scale, duration)`.
- Orbit weapons collide at two real opposite blade positions and draw six blades. Sentry Whirl holds an enemy for 2 s, hits other bodies with it and releases it.
- Sticky plasma follows its carrier, applies the ×1.5 movement modifier, detonates once after 2 s and then removes the modifier.
- Hook travels and pulls through a pooled attack actor. Fling carries element data and Tame's 1.5 s landing stun.
- Rail Lance has an automatic 0.5 s wind-up, ignores Shield and keeps ×0.75 damage per pierced target.
- Blizzard owns one cursor-following field. Wildfire keeps at most three patches; a fourth replaces the oldest.
- Crash Helmet skips wall damage only with Tame. Strike's Tame skips self-damage but keeps self-displacement.
- Swap is a travelling dagger, Recall a planted dagger, and Wildfire/Cloud are lobbed; rules in [DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) §2.
- Armory: three distinct forms; slot 1 is always a new weapon; slots 2 and 3 may show a stronger Mk of a held weapon ([V4-NUMBERS.md](V4-NUMBERS.md) §2). This replaced the earlier missing-hand / missing-auto guarantees and Mk II/III axis rolls.
- Draft weapon cards offer only Twist, Tame or Infusion (auto weapons: Infusion only).

### Bosses and the void trial

- Hollow's Inhale is a continuous 2 s pull.
- Tyrant summons use the late-wave mix; phase 2 attacks carry an immune element; phase 3 attacks and summons speed up.
- Tyrant rolls three distinct elements per run, announces them at wave 11 and applies those immunities. If every element the player owns is immune, an Element Penetration card is guaranteed until its cap.
- The player carries Void while any owned weapon or skill has it, or after gaining Void Energy.
- Entering the trial reserves frozen pool slots and saves the battlefield's attacks, held targets, channels and weapon state. Returning releases trial attacks and held targets and restores the saved state, so trial channels cannot overwrite a battlefield Blizzard or Hook.
- The trial reward returns to the same wave through `FinishGreaterReward()` ([DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) §5).

### Other rules in code

- Enemy healing does not remove Poison.
- Frost Nova's level-3 knockback bonus applies only to targets that cast actually froze.
- Devour spreads Contagion explicitly.
- Keyboard shortcuts and a 0.4 s lock after menus or a left-button release prevent accidental clicks. The UI clears stale icon data and shows each skill's real cooldown.

## Pools

Everything that appears during combat is created by the setup script and reused. A full pool refuses a request instead of instantiating.

| Pool | Capacity |
| --- | --- |
| Ordinary enemy slots (`PreparedEnemyPool`) | 220, split per type 48 / 24 / 48 / 20 / 16 / 20 / 12 / 16 / 2 / 12 / 2 |
| Spawn markers | 48 |
| Enemy bolts | 32 |
| Attack actors (`PreparedAttackPool`, `AttackPool`) | 256 |
| XP and HP orbs (`PreparedOrbPool`, `OrbPool`) | 256 |
| Effect quads (`PreparedFxQuads`, `FxPool`) | 256 |

Every prepared enemy has `EnemyDefinition`, `Rigidbody`, `Collider` and `ElementStatus`. The player has `RunWeapons`, `RunProgression`, `SkillLoadout` and `ElementStatus`, with the roster, rules and skill catalog bound. `FxPool` creates its `MaterialPropertyBlock` in `Awake`, not in a field initializer (Unity does not allow it there).

## Data assets

| Asset | Contents |
| --- | --- |
| `V2/Data/WeaponRoster.asset` | Weapon forms, anomalies, Mk table |
| `V2/Data/Waves15.asset` | The 15 waves |
| `V2/Data/Enemies/*.asset` | One definition per enemy type |
| `V2/Data/InterstellarBattlefield.asset`, `DustFlats.asset`, `RiftZone.asset` and their `*Map.asset` layouts | The three biomes: Wreck Field, Dust Flats, Rift Zone |
| `V2/Data/ExpandedWorld/` | Three further layouts per biome (`WreckField_1–3`, `DustFlats_1–3`, `RiftZone_1–3`) |
| `Data/ProgressionRules.asset`, `StatusRules.asset`, `SkillCatalog.asset`, `Skills/`, `Elements/` | Progression, elements and skills ([PROGRESSION.md](PROGRESSION.md)) |
| `Data/EncounterBalance.asset` | Wave time-to-kill budgets ([COMBAT-BALANCE.md](COMBAT-BALANCE.md)) |

The original three biomes have 10 / 4 / 6 solid interior walls, sharing the 4-piece boundary and the 100 × 70 floor. At runtime a biome selects one of the saved wall groups.

## Localization

UI text uses the Unity Localization package. Strings are in the `ArenaUI` string table (`Localization/Strings/`) with `en` and `zh-Hans` locales (`Localization/Locales/`), read through `Scripts/Localization/ArenaText.cs` and `ArenaTerms.cs`. `Localization/arena-ui.csv` and `arena-ui.json` list every key with its text per locale. `Editor/EnglishPublication.cs` (`Apply()`, run in batch mode with `-translationMap <file>`) selects the English locale in `ArenaV2_EN.unity` and replaces any remaining non-English scene strings from the map.

## Presentation

| Layer | Where |
| --- | --- |
| Theme and roster concepts | `Art/V2/Concepts/interstellar-battlefield.png`, `alien-roster.png` |
| Editable vector sources | `Art/V2/Sources/SVG/` (PNG derivatives in `Art/V2/Textures/`) |
| Weapon, skill, stat, element, axis, boss-phase and telegraph images | `Art/V2/Textures/` |
| Player model | `Art/V2/Models/explorer/` |
| Character, weapon, attack, orbit, black hole, terrain, boss, telegraph and threat presentation | `Scripts/Art/V2*.cs` |
| V4 impact and status visuals | `Scripts/Art/V4CombatVfx.cs` |
| Skill ground visuals | `Scripts/Art/SkillGroundPresentation.cs`, `SkillVisualPresentation.cs`, `Art/V2/Shaders/SkillGround.shader` |
| Audio | `Audio/V2/` (three base clips and `SFX/`), `Audio/V3/Full/`; playback in `Scripts/Audio/` |

## Known gaps

1. Storm Call level 3 has no defined effect; the skill stops at level 2 ([DESIGN-DECISIONS.md](DESIGN-DECISIONS.md) §4).
2. The Hollow reuses a tinted, scaled Shade sprite; distinct Hollow art is not done.
3. Navigation, control and Shield interactions, portal expiry, every attack variant, same-frame outcomes, input feel and trial restoration still need play checks.
4. Backlog items T0 (stress baseline), WebGL frame budget, garbage and pool saturation, draw calls, CI and atlas work are open ([TECH-BACKLOG.md](TECH-BACKLOG.md)).
5. The public GitHub Pages link still serves an earlier build; V2 has not been published there.
