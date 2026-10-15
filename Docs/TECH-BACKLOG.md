# Engineering backlog — performance and structure

Tasks that make the code ready for the v2.0 design in [GDD.md](GDD.md). Written so that any team member can pick one up. How V2 is currently built is in [IMPLEMENTATION-NOTES.md](IMPLEMENTATION-NOTES.md).

| | |
|---|---|
| Written | 2026-10-11 against the 1.0 code (about 5 500 lines of C# under `Assets/AnomalyArena/`), after the Charged Shot and Hook changes. Updated for GDD drafts 4 and 5: T18 and T19 added, T7, T10, T12 and T15 adjusted. |
| Target | WebGL (single thread, garbage collection is expensive), Unity 6000.6.3f1, URP |
| Evidence | **Nothing here has been profiled yet.** Every "why" comes from reading the code and from the scale v2.0 asks for. T0 exists to replace this ranking with measurements; if the numbers disagree with a task, the numbers win. |
| File references | Written against the 1.0 code. Files and methods are named; line numbers have moved since and are left out. The V2 code added some of the structures below; see "Since V2" under each task. |

## The scale that makes this matter

| | 1.0 today | v2.0 design (GDD §9.3, §9.4) |
|---|---|---|
| Enemies alive at once | up to 12 | 10 in wave 1, 38 in wave 15, plus elites. Then a final boss that calls 6 to 9 enemies every 15 s (GDD §9.7). |
| Enemies spawned in a run | 35 | Endless: every enemy killed is replaced, up to 6 per second. About 500 to 1 200 kills in a run, depending on the player. |
| Map | 32 × 30, one screen | 100 × 70 with interior walls, black holes, gravity zones |
| Hits per second | a few | many, plus auto weapons |
| Enemy types | 2 | 11 and a final boss; 8 of the 11 can carry one of 7 element affixes (GDD §9.1, §9.5, §9.7) |
| Things that tick on a character | nothing | Up to four stacking statuses, two damage-over-time clocks and a Shield (GDD §4.6, §4.7) |
| Screens | title, HUD, result | + loadout, armory, card drafts, replace overlays, pause |

## Working rules

- One task per branch and pull request. Say in the description what was measured before and after.
- Tests must stay green: menu **Anomaly Arena → 4. Run Play Mode Tests**, then `Logs/TestResults.txt` must start with `FINISHED Passed: … failed 0` (87 Play Mode tests and 8 Edit Mode tests as of 2026-10-15, see [PROJECT-STATUS.md](PROJECT-STATUS.md)).
- A performance task changes no game rule. If a rule has to change, stop and raise it.
- Do not hand-edit `.unity` or `.prefab` files; change them through editor scripts (`Editor/ArenaSetup.cs` shows the pattern). Do not create `.meta` files by hand.
- Do not commit `.idea/`, local Unity caches (`Library/`, `Temp/`, `Logs/`, `UserSettings/`), `ProjectSettings/Packages/com.unity.connect.share/Settings.json` or `ProjectSettings/UnityConnectSettings.asset`.
- Gameplay scripts (`Combatant.cs`, weapon effects, `WaveManager.cs`, `Hud.cs`) will be changing at the same time for the v2.0 features. Agree before making large edits there. New work is safest in new folders: `Scripts/Core/Pooling/`, `Scripts/World/`, `Scripts/Data/`.

## Suggested order

`T0` first. Then `T1 → T2 → T3` (they touch the same code). `T7` can start in parallel, since it is new code. `T8` before `T9`. `T10` and `T12` before `T18` and `T19`. The rest as needed.

| # | Task | Priority | Size |
|---|---|---|---|
| T0 | Stress test and baseline numbers | P0 | S |
| T1 | Object pooling | P0 | L |
| T2 | Queries without garbage | P0 | S–M |
| T3 | Effects: shared materials, fewer draw calls | P0 | M |
| T4 | HUD off IMGUI | P0 | L |
| T5 | Physics cost of crowds | P0 | M |
| T6 | Per-object per-frame work | P0 | S |
| T7 | Grid map and enemy navigation | P1 | L |
| T8 | Data-driven content and validation | P1 | M–L |
| T9 | Biome system | P1 | M |
| T10 | Stats and modifiers | P1 | M |
| T11 | Run state machine and one owner of time | P1 | M |
| T12 | Game events | P1 | S–M |
| T13 | Seeded randomness | P1 | S |
| T14 | Camera rig | P1 | S |
| T15 | Input actions | P1 | M |
| T18 | Statuses, elements and Shield | P1 | L |
| T19 | Weapon instances and upgrade axes | P1 | M |
| T16 | Continuous integration | P2 | M |
| T17 | Texture and atlas pass | P2 | S–M |

---

## P0 — performance

### T0. Stress test and baseline numbers

**Why.** Without numbers nobody can say whether a later task helped.

**What.** A menu command, for example **Anomaly Arena → 6. Run Stress Test**, that in Play mode spawns N enemies (20, 40, 60) walking at the player, fires each weapon on a timer for 20 s, and writes `Logs/Stress.txt` with, per N: average and 95th-percentile frame time, garbage allocated per frame, SetPass calls, live GameObject count. `ProfilerRecorder` gives all of these from code ("Main Thread", "GC Allocated In Frame", "SetPass Calls Count"). Run it in the editor and in a WebGL development build.

**Done when.** The command exists; a "Baseline" table with the numbers for 20 / 40 / 60 enemies, editor and WebGL, is added to this file.

### T1. Object pooling

**Why.**

- Every effect is a new primitive GameObject with a new Material, destroyed a fraction of a second later: `Fx.Prim` (`Scripts/Game/Fx.cs`), `Fx.Make` → `GameManager.FxMat` (`Fx.cs`, `Scripts/Game/GameManager.cs`, a `new Material` on every call), and `Fade.OnDestroy` destroys that material again.
- One hit creates 9 to 16 shard cubes (`Fx.Burst`, `Fx.cs`, called from `Combatant.PlayHitFeedback`, `Scripts/Characters/Combatant.cs`), plus a flash and sometimes a ring. The 1.0 notes record **71 effect objects for five hits on one enemy**.
- Every shot is `new GameObject` + `AddComponent` + a primitive + a `TrailRenderer` (`Scripts/Weapons/Runtime/Bullet.cs`, `Projectile.MakeVisual` and `AddTrail` in `Scripts/Weapons/WeaponEffect.cs`). Reverse Shot fires every 0.12 s.
- Enemies and spawn markers are instantiated and destroyed (`Scripts/Game/WaveManager.cs`); in v2.0 enemies never run out, and a strong build can force up to 6 spawns per second for a whole wave.
- The hook's crash (`Combatant.CheckThrowImpact`) calls `Query.Characters` and plays the hit feedback on every enemy in reach: one click can create a hundred effect objects.

**What.** A small pooling layer on `UnityEngine.Pool.ObjectPool<T>` (built in; do not write a custom pool):

- **Effects.** Pooled quads, cubes, spheres and cylinders. `Fade` returns the object to its pool instead of destroying it. Colour through a `MaterialPropertyBlock` on a shared material (see T3).
- **Projectiles.** `Bullet` first; later Spitter blobs and grenades. `Launch` must reset everything: `traveled`, the `struck` set, and `TrailRenderer.Clear()`.
- **Enemies**, one pool per prefab. Needs a `ResetForSpawn()` on `Combatant`: HP, state, protection and cooldown timers, flash and squash, rigidbody velocities, the `eliminated` flag.
- **XP and HP orbs** (new in v2.0): pooled from the first version.

**Watch out for.**

- `GameManager.ClearTransients` (`GameManager.cs`) destroys everything registered. It must release to the pool instead.
- `Projectile` registers in `Awake` and unregisters in `OnDestroy`; a pooled object needs `OnEnable` / `OnDisable` or explicit calls.
- `WaveManager` subscribes to `Eliminated` on every spawn (`WaveManager.cs`). A pooled enemy must not keep old subscribers.
- Tests create enemies with `Object.Instantiate` directly (`Tests/Runtime/TestUtils/ArenaTestUtils.cs`). That path must keep working.

**Done when.** In the 40-enemy stress run, garbage per frame in steady combat is at least 90 % below the baseline and the profiler shows no `Instantiate` or `Destroy` in steady state. All tests pass. A new Play Mode test proves a reused enemy comes back with full HP, in the Normal state, and reports its death once.

**Since V2.** The V2 scene allocates its pools at setup time: 220 enemy slots, 48 spawn markers, 32 enemy bolts, 256 attack actors (`AttackPool`), 256 orbs (`OrbPool`) and 256 effect quads (`FxPool`); a full pool refuses a request. The 1.0 paths above (`Fx`, `Bullet`) still allocate. The stress measurement is not done.

### T2. Queries without garbage

**Why.** `Query.Characters`, `Query.CharactersAlong` and `Query.InSector` each allocate a `List<Combatant>` per call and remove duplicates with `List.Contains` (`Scripts/Core/Query.cs`). Every projectile calls one of them every physics step (`Bullet.cs`, `ThrownKnife.cs`, `SelfLaunch.cs`). `Combatant.From` does a `GetComponent` per collider (`Combatant.cs`).

**What.**

- Overloads that fill a list supplied by the caller, or `ListPool<Combatant>.Get(out var list)` in a `using`.
- A static `Dictionary<Collider, Combatant>`, filled in `Combatant.Awake` and cleared in `OnDestroy`, for constant-time lookup.
- The shared overlap buffer holds 64 colliders. With 40 enemies plus the player inside one explosion that is close to the limit; size it from the alive cap or let it grow.

**Done when.** A test using Unity Test Framework's `Is.Not.AllocatingGCMemory()` passes for the new overloads. `QueryTest` still passes.

### T3. Effects: shared materials, fewer draw calls

**Why.** Because `FxMat` makes a unique material per effect, no two effects can be drawn together, and `Fade.Update` writes `mat.color` on each of them every frame.

**What.** One shared transparent material per texture; per-instance colour and alpha through `MaterialPropertyBlock` or vertex colours. Consider replacing the shard cubes with one pooled `ParticleSystem` burst per hit, or GPU instancing. Note that a property block takes a renderer out of the SRP Batcher; instancing or particles avoid that. Measure both.

**Done when.** SetPass calls in the 40-enemy stress run are at least 50 % below the baseline. Screenshots in whitebox and illustrated mode show the same look as before.

**Since V2.** `FxPool` colours its quads through one `MaterialPropertyBlock`. Not yet measured.

### T4. HUD off IMGUI

**Why.** `Hud.OnGUI` (`Scripts/Game/Hud.cs`) runs for every IMGUI event — layout, repaint and each input event — and every time it walks all ground weapons, all living enemies and all damage numbers (`WorldLabels`, `Hud.cs`) and builds interpolated strings. v2.0 adds seven screens, slot rows, a counter over every black hole and edge arrows (GDD §10).

**What.** Move the HUD and screens to UI Toolkit or uGUI — pick one and say why. Enemy HP bars become pooled world-space quads or one instanced mesh. Text is rebuilt only when its value changes.

**Constraint to check first.** 1.0 uses Unity's built-in font `LegacyRuntime.ttf` because the assignment spec asked for built-in fonts only. Whatever UI system is chosen must be able to use it. Confirm against the spec before starting.

**Done when.** No `OnGUI` remains in runtime code. With nothing changing on screen, the UI allocates nothing per frame. The loadout screen from GDD §10.2 is built on the new system as proof.

**Since V2.** The V2 HUD and screens are uGUI (`Scripts/UI/V2Hud.cs` and the other `V2*` UI scripts). The 1.0 `Hud.OnGUI` remains in the code. The per-frame allocation check is not done.

### T5. Physics cost of crowds

**Why.** Every character is a dynamic `Rigidbody` using `CollisionDetectionMode.ContinuousDynamic` (`Editor/ArenaSetup.cs`), the most expensive mode, although most of the time enemies walk at 3.6 to 7.5 units/s. The layer collision matrix has every pair enabled. The fixed timestep is 0.02 s.

**What.** Measure first (T0). Then try: `ContinuousSpeculative` or `Discrete` for enemies while they walk, continuous only while Knocked or Thrown; a collision matrix where effect and default-layer objects do not test against characters.

**Risk.** Tunnelling. A 10-unit knock starts at 60 units/s — 1.2 units per physics step — and v2.0 adds +100 % Knockback at the cap. Keep `SelfLaunchTest` green and add a test: an enemy knocked 10 units toward a wall 1 unit away ends on the near side.

**Done when.** Physics time at 40 enemies is lower than the baseline and the tunnelling test passes.

### T6. Per-object per-frame work

**Why.** `FaceCamera.LateUpdate` (`Scripts/Art/Art.cs`) copies the camera's rotation onto every billboard every frame, but the camera never rotates: it has a fixed pitch, and shake only moves it (`GameManager.cs`). Each ground weapon also has its own `Update` for bobbing.

**What.** Set the rotation once when the billboard is created, and again only if the camera rig changes; or do the facing in the shader. Keep `Update` methods only where something really changes each frame.

**Done when.** No per-frame rotation writes for billboards in the profiler; illustrated mode looks the same.

---

## P1 — structure the design needs

### T7. Grid map and enemy navigation

**Why.** Enemies walk straight at the player and only slide along the outer edge (`Enemy.KeepOffEdges`, `Scripts/Characters/Enemy.cs`, on top of `ArenaShape`). The v2.0 map has interior walls, black holes and gravity zones that enemies must go around (GDD §8, §9.2).

**What.**

- `MapGrid`: 100 × 70 cells of 1 unit, each with flags — Floor, Wall, Hole, GravityZone (and which hole), Spawnable — built from a `MapLayout` asset.
- A **flow field** toward the player: one breadth-first search outward from the player's cell over walkable cells; each enemy reads the direction stored in its own cell. Recompute when the player enters a new cell, or every 0.2 s.
- Two fields cover the rule in GDD §9.2: one treats gravity cells as blocked; the other allows them and is used by fast enemies only while the player stands in a zone.
- Runtime changes — a hole collapses into the portal and is later gone for good (GDD §8.5), the wall of the *Bulwark* skill appears and disappears — are a flag change and a recompute.

**Why a flow field rather than a NavMesh or a path per enemy.** One search serves every enemy, so the cost does not grow with the crowd; the design is already cell-based; changing the map at runtime is trivial. A search over 7 000 cells should be cheap, but measure it on WebGL.

**Done when.** The Play Mode tests named in GDD §16, M1 pass: an enemy 20 units from the player with a wall between reaches the player within 10 s and is never on a gravity cell on the way. Recompute time is logged.

**Since V2.** `Scripts/Arena/MapGrid.cs` keeps two breadth-first fields (a safe one and a fast one), and enemies steer with `WorldMap.GetChaseDirection`. The M1 tests and recompute timing are not done.

### T8. Data-driven content and validation

**Why.** Content is split between assets (`Effects/*.asset` — the good pattern), code (`WaveManager.waves`, enemy values in `ArenaSetup.SetEnemy`) and the scene (weapon definitions on the spawner). Setup scripts overwrite tuned values: `ArenaSetup.UpdateExisting` sets `reverse.roundsPerUse = 5` (`ArenaSetup.cs`) although the asset was tuned to 4, and the in-game description still says "5 rounds per use".

**What.** ScriptableObject definitions for everything in GDD §11, and Edit Mode validation tests in the style of `Tests/Editor/Art/ArtSetTest.cs`:

- every weapon form has exactly two anomalies;
- each wave's mix weights add up to 100;
- ids are unique;
- description text that quotes a number is generated from the field, so the two cannot disagree;
- setup scripts create missing assets but never write numbers into existing ones.

**Done when.** Adding an enemy type or changing a wave needs no code change, and the validation tests run in Edit Mode.

### T9. Biome system

**Why.** GDD §8.7: a biome is a look and a layout chosen per run. It is the cheapest way to make runs differ, and it forces the map to be data.

**What.** A `Biome` asset: art set (floor, wall, boundary, black-hole textures, or whitebox colours), ambient colour, a list of `MapLayout`s (walls and black-hole anchors), and a tilt for the enemy mix. Today `Art.DecorateArena` (`Art.cs`) swaps floor and walls for the single art set; generalise it to "apply this biome". A run picks a biome and a layout with the seeded generator from T13.

**Done when.** Two biomes with different layouts can be chosen from a debug menu. An Edit Mode test checks every layout against the rules in GDD §8.3 and §8.4: anchors at least 14 cells from the player's start, 10 from the boundary, 24 from each other; no passage narrower than 4 cells; no wall inside a gravity zone.

**Since V2.** `Biome` and `MapLayout` assets exist for Wreck Field, Dust Flats and Rift Zone, with three extra layouts each in `V2/Data/ExpandedWorld/`. The layout validation test is not written.

### T10. Stats and modifiers

**Why.** GDD §3.3 and §7.3: fourteen stats, one additive bracket, caps, and a second multiplying bracket per weapon. Today the values are public fields read directly (`moveSpeed`, `maxHp`, the fields of each effect).

**What.** A `Stat` type with a base value, an additive percentage, a cap and a change event. Weapon effects read final values through the holder (`user.Stats`) rather than their own fields. Unit tests for stacking and caps.

**Since V2.** `Scripts/Progression/RunStats.cs` holds the run stats with per-stat caps (crit capped at 60%, armor at 300). Since V4 the fourteen stats are offered through seven stat cards plus Element Penetration ([V4-NUMBERS.md](V4-NUMBERS.md) §3). Weapon effects in the 1.0 code still read their own fields.

### T11. Run state machine and one owner of time

**Why.** `GameState` has four values: Title, Playing, Won, Lost. Hit stop writes `Time.timeScale` directly and sets it back to 1 when it ends (`GameManager.cs`). That would silently cancel a pause, a frozen menu, or the Time Slip skill (GDD §12, W7).

**What.** The states of GDD §2.1, each with enter and exit. A `TimeControl` that collects requests — pause 0, menu 0, Time Slip 0.3, hit stop 0.05 — applies the lowest, and restores correctly when one ends. The build-phase clock (GDD §2.3) runs in real time and is not affected by it.

**Done when.** A test shows a hit stop during a pause does not unpause. Each row of the transition table in GDD §2.1 has a test.

**Since V2.** `GameManagerV2` owns the run phases and exposes `WorldDeltaTime` (scaled by Time Slip) and `PlayerDeltaTime`. There is no separate `TimeControl` yet, and the tests above are not written.

### T12. Game events

**Why.** Gameplay calls singletons directly: `GM.hud.DamageNumber` (`Combatant.cs`), `GM.hud.Reveal`, `GM.weapons.TryLargeDrop`. XP, black-hole counters, Energy Siphon, a Void-infused weapon, the *Devour* skill, the codex and the result screen all need to hear "an enemy died — how, where, by whom".

**What.** C# events on a run object: `EnemyDied`, `DamageDealt`, `LevelGained`, `WaveStarted`, `WaveEnded`, `HoleFed`, `WeaponRevealed`, and for T18 `StatusApplied` and `ShieldBroken`. `System.Action` delegates; raising methods prefixed `On`.

### T13. Seeded randomness

**Why.** `UnityEngine.Random` is called directly in 18 places: wave order, weapon rolls, effects. A run cannot be replayed and a test cannot fix an outcome.

**What.** One generator for gameplay and one for cosmetics, seeded per run; the seed is shown on the result screen; tests set it.

### T14. Camera rig

**Why.** `TickShake` sets `cam.transform.position = camBase + offset` every frame (`GameManager.cs`), which only works for a camera that never moves.

**What.** A `CameraRig`: follows the player, clamps to the map (GDD §8.1), adds shake as an offset on top, and offers the world-to-screen-edge helper the HUD arrows need.

**Since V2.** `Scripts/Arena/CameraRig.cs` follows the player, clamps to the map and applies shake as an offset.

### T15. Input actions

**Why.** `Keyboard.current` and `Mouse.current` are polled directly (`PlayerController.cs`, `GameManager.cs`), which is why the tests need `debugSprintHeld` and `debugLockAim`. v2.0 adds 1 / 2 / 3, E, Space, Esc and menu navigation, and gives the right mouse button a new job: it switches between the two hand slots (GDD §3.1). Today it picks up a weapon from the ground (`PlayerController.cs`).

**What.** An Input Actions asset with a Gameplay map and a Menu map (the project already has `Assets/SourceFiles/InputSystem`). Tests drive actions through the Input System's test fixture instead of debug fields.

### T18. Statuses, elements and Shield

**Why.** GDD §4.6–§4.9: seven elements; four of them sit on a character as stacks (Fire, Poison, Frost, Lightning); Shield stands in front of HP; three elements pay the wielder; enemies can carry an affix. Today a `Combatant` has HP and one `CharacterState`, `DamageInfo` carries damage, dealer, direction, knockback and `HitKind` (`Scripts/Core/Interfaces.cs`), and nothing ticks on a character. With 38 enemies alive, each able to carry several statuses, how this is stored and ticked matters for T1, T2 and T6.

**What.**

- A `StatusBoard`: a plain class owned by `Combatant`, not a component per status. Fixed fields for the four stack counts (floats: stacks need not be whole), the two tick clocks, the Frozen and Paralysed timers with their immunity timers, and Shield. No per-status objects and no lists, so applying a status allocates nothing.
- One place applies damage. The damage method of `Combatant` gains the Shield step (GDD §4.1, step 3) and a damage *source* — Hit, Burn, Poison, Wall — so that Poison can skip Shield and Burn can be halved by it. A direct hit is cut by 30 % while the target has any Shield, and a weapon can be flagged to ignore that cut (GDD §4.7, §5.4). A tick must not trigger hit stop, protection time or knockback.
- `DamageInfo` gains the element and the base damage that stacks are counted from (`stacks = base ÷ 5`, kept as a fraction; a weapon with a built-in element uses ÷ 2.5).
- Frozen and Paralysed are **not** new `CharacterState` values (GDD §3.4): they are flags that block acting. A Frozen character can still be Knocked, Thrown, Hooked or swallowed. `Enemy` and `PlayerController` ask one question, `CanAct`, instead of reading states.
- The jump of Lightning is a query for Frost carriers within 4 units: use the non-allocating query from T2 and a reusable visited set; stop at 5.
- The share paid to the wielder (Earth, Light, Void) keeps fractions: three float accumulators on the wielder.
- Every number comes from one `StatusRules` asset (GDD §11), under the rules of T8.
- Ticking is one loop over the characters that have any status, not an `Update` per character (see T6).
- Status icons and stack counts over enemies are per-enemy UI: T4 has to plan for them.
- Immunity and Element Penetration (GDD §4.6, §9.7): one factor per element on the receiver and one stat on the dealer. `applied = amount × (1 − immunity × (1 − penetration))`, for stacks and for the share paid to the wielder alike.
- The Shade's cloak (GDD §9.1) is a rendering state plus a reveal timer on the same board: any damage or status sets the timer.

**Depends on.** T10 (shares and thresholds are stats that upgrades may change), T12 (`StatusApplied`, `ShieldBroken`), T8 (the asset).

**Done when.** The Play Mode tests named in GDD §16, M3b pass. In the stress scene of T0, with 40 enemies all burning and poisoned, applying and ticking statuses allocates nothing per frame.

**Since V2.** Implemented as the `ElementStatus` component (a component per character, not a plain class) with numbers in `Data/StatusRules.asset`; see [PROGRESSION.md](PROGRESSION.md). Since the 2026-10-12 GDD clarification, Lightning jumps to any target within 4 and Frost only widens the jump (range 6) and boosts stacks (×1.5). The allocation check is not done.

### T19. Weapon instances and upgrade axes

**Why.** GDD §5.8: every weapon is upgraded along six axes (Infusion, Power, Tempo, Reach, Twist, Tame), each taken at most once, and carries at most one element. Today a `Weapon` reads its numbers straight from the shared effect asset (`Effects/*.asset`), so two weapons with the same anomaly cannot differ, and an upgrade would change the asset for every weapon that uses it.

**What.**

- A run-time `WeaponInstance`: the anomaly asset, the mark, a flag set of the axes taken, the element.
- Effects read final values through the instance: damage × Power; cooldown, charge time and reload × Tempo; the fields the anomaly lists as its Reach values × 1.4. Twist and Tame are booleans the effect checks.
- Nothing is ever written back into the asset (the same rule as T8).
- Card text is generated from the instance (before → after), so a card cannot disagree with what it does.

**Depends on.** T10.

**Done when.** Two weapons with the same anomaly and different axes behave differently in one scene. An Edit Mode test checks that every hand anomaly defines a Twist, a Tame and at least one Reach value.

**Since V2.** `Scripts/Weapons/V2/RunWeapon.cs` is the run-time instance: form, anomaly, Mk tier, axis flags and element. Since V4, draft cards add only Twist, Tame or Infusion (auto weapons: Infusion only), and damage growth comes from armory Mk tiers ([V4-NUMBERS.md](V4-NUMBERS.md) §2). The Edit Mode test is not written.

---

## P2 — pipeline

### T16. Continuous integration

Run Edit Mode and Play Mode tests on every push; on a version tag, build WebGL and publish to the `gh-pages` branch. GameCI's GitHub Actions are the usual route; they need a Unity licence stored in the repository's CI settings, and how personal licences activate in CI has changed over time — check the current GameCI documentation before planning around it.

### T17. Texture and atlas pass

Illustrated mode uses 31 separate PNGs (1.4 MB), each with its own material (`Art.cs`). Measure SetPass calls in illustrated mode at 40 enemies. If they are significant, pack the sprites into one atlas so billboards can share a material.

---

## Small fixes found while reading

| Where | What |
|---|---|
| `Effects/GunReverseShot.asset` | The description says "5 rounds per use"; the clip is 4. One of the two is wrong. |
| `Editor/ArenaSetup.cs` (`UpdateExisting`) | Menu 3 writes `roundsPerUse = 5` over the tuned value every time it runs. |
| `Scripts/Game/GameManager.cs` (`Register`) | `transients` is a `List` searched with `Contains` on every registration; a `HashSet` is enough. |
