# Refactor Changelog

Audit → architecture fix → Civ-like map rebuild → data-driven UI Toolkit gameplay layer.

## What was changed

### New world subsystem (`Assets/Scripts/World/`, namespace `Institute.World`)
- Correct data model: `HexCoord`, `HexTileData`, `RegionData`, `WorldMapData`, `MapGenerationSettings`,
  enums `TerrainType` / `BiomeType` / `RegionType` / `MapMode`.
- `WorldMapGenerator`: 6-step deterministic pipeline (grid → terrain → seeds → weighted-BFS growth →
  region stats → borders/neighbors). `MinHeap` supports the growth step.
- `WorldMapValidator`: enforces the corrected invariants (regions ≪ tiles, multi-tile regions, unclaimed
  land, no water-regions, ownership/neighbor integrity).
- Rendering: `MapRenderManager` (single mesh + collider), `RegionOverlayRenderer` (borders + selection),
  `MeshAccumulator`, `HexMetrics`, `MapColors`, `MapPalette`. World-space, not uGUI.
- Interaction: `MapSelectionController`, `WorldCameraController`, `MapInteractionGate`, and `WorldController`
  (orchestrator + public API + selection logic).
- Persistence: `WorldMapSave`/`WorldTileSave`/`WorldRegionSave`, `WorldMapSerializer` (with old-format
  detection), `WorldSaveBridge` (`<slot>.world.json`).
- Definitions: `TerrainDefinition`, `RegionTypeDefinition`, `MapDefinitions`, `MapPresets` loaders.
- Setup carrier: `WorldSetup`. Character link: `WorldCharacterBridge`.
- Gameplay: `WorldGameplayInstaller`, and `WorldHUDController` (UI Toolkit HUD, namespace `Institute.World.UI`).

### Data-driven content
- `Assets/Data/Map/terrain_definitions.json`, `region_type_definitions.json`, `map_presets.json`
- `Assets/Data/Difficulty/difficulty_presets.json`
- `Assets/Data/UI/theme.json` — added a `map` section (terrain-mode colors, unclaimed/sea/blocked,
  selection/border colors, stat gradient, hex size + border widths).

### UI Toolkit gameplay layer
- New UXML: `GameplayHUD.uxml`, `PauseMenu.uxml`, `EventPopup.uxml`, `Settings.uxml`.
- Extended USS: appended HUD classes to `gameplay.uss` and overlay/dialog classes to `popups.uss`.
- `WorldHUDController` builds the HUD from UXML/USS (or an equivalent programmatic tree), with **no
  hard-coded colors** — all visuals come from USS/theme tokens.

### New Game Setup
- Added a "World Map" section (Map Size preset, Region Count, Unclaimed Land %, Terrain Roughness) that
  writes `WorldSetup.PendingSettings`, decoupled from the tiny legacy `DifficultyConfig.mapWidth/Height`.

### Editor tools (`InstituteMapTools`, non-colliding menu paths)
- `Audit Project`, `Validate Map Data`, `Validate Save Data`, `Generate Test Hex World`,
  `Rebuild Gameplay World Scene`, `Rebuild Map Data Files`, `Rebuild UI Theme Files`.

### Docs
- `PREVIOUS_WORK_AUDIT.md`, `MAP_GENERATION.md`, this changelog, and an appended section in
  `UI_AND_SCENE_STRUCTURE.md`.

## What was replaced (demoted to legacy, still compiles)

- `Region` (hex-tile + political hybrid) → `HexTileData` + `RegionData`.
- `HexMapGenerator` (one `Region` per hex, uGUI tiles) → `WorldMapGenerator` + world-space renderer.
- `RegionGridGenerator`, `VoronoiRegionGenerator` → folded into the new growth step.
- `RegionUI` / `MapViewController` (uGUI per-tile views) → `MapRenderManager` + `MapColors`.
- The procedurally-built, hard-coded uGUI gameplay HUD (`GameplaySceneInstaller`) → UI Toolkit
  `WorldHUDController` driven by UXML/USS/JSON.

The legacy types are intentionally **not deleted** so the old scenes keep compiling; they are not part of
the new gameplay path. See `PREVIOUS_WORK_AUDIT.md` for the full legacy list.

## Verified

- Roslyn compile (Unity 6 managed DLLs) passes with **0 errors** in both configurations:
  - player (no `UNITY_EDITOR`): 94 runtime scripts
  - editor (`UNITY_EDITOR`): 97 scripts incl. editor tools
- Determinism: all RNG-consuming loops iterate in a stable sorted order; same seed → same world.

## What remains to improve (clean TODOs)

- **Scene wiring is editor-driven.** New `.cs` files have no Unity-assigned GUIDs until Unity imports
  them, so the build-flow `Gameplay.unity` is **not** auto-switched. Run
  `Tools / Institute Game / Rebuild Gameplay World Scene` once in Unity to make the normal
  MainMenu→NewGameSetup→Gameplay flow load the new world (or use `Generate Test Hex World` to try it
  standalone). Until then the old gameplay scene still loads the legacy map.
- ~~Legacy gameplay systems not re-pointed at `RegionData`~~ — **resolved in Pass 3** (see below):
  decisions/events/characters now run on RegionData via new systems; legacy managers are isolated.
- ~~Pause/Event/Settings overlays not wired~~ — **resolved in Pass 3**: wired to UI Toolkit controllers;
  the HUD pause button opens `WorldPauseController` (no more direct `Time.timeScale` toggle).
- **Borders** are per-edge line meshes; a shader-based outline could look crisper at extreme zoom.
- Old saves are **rejected safely**, not migrated. A migration that infers tiles from legacy region
  hex coords could be added if old saves must be preserved.

---

## Pass 3 — gameplay integration (RegionData) + overlay wiring

- New RegionData-driven systems (`Institute.World.Gameplay`): `RegionDecisionSystem`,
  `RegionEventSystem`, `RegionCharacterSystem`, `GameResources`, `GameSaveService`. They reuse the
  existing data classes (`DecisionDefinition`, `GameEvent`/`EventOption`, `GameCharacter`,
  `CharacterSaveData`) and the region-free statics on `DecisionSelectionManager`.
- New UI Toolkit overlays (`Institute.World.UI`): `WorldPauseController` (PauseMenu.uxml),
  `WorldEventPopupController` (EventPopup.uxml), `WorldSettingsController` (Settings.uxml), plus
  `OverlayUtil`.
- `WorldController`: added `RegionDataChanged` observable + `RaiseRegionDataChanged` (recolors map in
  stat modes).
- `WorldHUDController`: right panel now lists real decisions + character interactions; pause button opens
  `WorldPauseController` (removed the direct `Time.timeScale` toggle); resources read via `GameResources`;
  refreshes on `RegionDataChanged`.
- `WorldGameplayInstaller`: now stands up GameManager + ResourceManager (seeded from difficulty) +
  the three systems + the three overlays, wires `EventReady` → popup, and loads a saved game (or
  generates characters) on start.
- New Game Setup writes `WorldSetup.PendingSettings` (already added in pass 2) for map size/density.
- Editor: `Tools / Institute Game / Validate Gameplay Integration`; `Generate Test Hex World` and
  `Rebuild Gameplay World Scene` now use `WorldGameplayInstaller`.
- Legacy `DecisionSelectionManager` / `EventManager` / `CharacterManager` / `UIManager` are **isolated**
  (not instantiated by the new path). See `LEGACY_SYSTEMS.md`.
- Re-verified: Roslyn compile, 0 errors (player + editor configs).

---

## Pass 5 — Hard to Be a God strategy layer

- **States**: `StateData` + `RegionData.stateId` + `WorldMapData.statesById`; deterministic
  `WorldMapGenerator.GenerateStates` (BFS clustering into 3–6 kingdoms); serialized; Political map mode
  colors by state. `WorldStateUtil` keeps state aggregates in sync.
- **Economy**: `EconomySystem` ticks on `TimeManager.OnNewDay` — income (development × influence),
  rare artifacts from ruins, sanity drain/recovery, and the player-global **Exposure** meter. HUD gains
  resource-breakdown tooltips + an Exposure readout. Installer now adds GameDateTracker + TimeManager +
  EconomySystem.
- **Decisions**: `DecisionDefinition` extended (Region/State/Self target, exposureRisk, state deltas);
  `RegionDecisionSystem` applies state-level deltas (propagating to member regions) and Shadow
  Instruments; new thematic entries in `decisions.json`.
- **Events**: `EventScope.State` (kingdom-wide), character→state influence, and Anton mentor personal
  events tied to sanity strain.
- **Win/Loss**: evaluated daily in `EconomySystem` (sanity 0 / stability < 15% / exposure 100 = loss;
  80% influence in all states or final reform = win); HUD shows a VICTORY/DEFEAT banner.
- Re-verified: Roslyn compile, 0 errors (player + editor). See `HARD_TO_BE_A_GOD_SYSTEMS.md`.

---

## Pass 6 — standalone build UI fix + economy nerf + 1–100 rebalance

- **Build UI fix**: moved all UI assets from `Assets/UI/` to `Assets/Resources/UI/` (UXML, Styles, the
  theme `.tss`, and the PanelSettings asset), preserving `.meta`/GUIDs and repointing each overlay
  UXML's `<Style src>`. `OverlayUtil` now loads UXML/USS **Resources-first** (editor `AssetDatabase`
  only as a dev fallback) via `LoadUxml`/`LoadStyle`/`CleanResourcePath`. `WorldHUDController` and the
  MainMenu/NewGameSetup/Loading controllers load from Resources, so the styled UI now renders in the
  standalone player (no more giant grey bars). Editor path constants in `UIToolkitThemeUtility`,
  `InstituteMapTools`, `InstitutePresentationTools`, and `InstituteSceneStructureBuilder` updated to the
  new locations. (The old `Assets/UI/` subfolders are now empty/legacy.)
- **Economy nerf**: `EconomySystem.baseIncome` 10→5; `ComputeIncome` only grants bonus income from
  regions that have a **structure modifier** (Clinic / Guild / School / Network). Added a
  `DecisionDefinition.structureName` field + `RegionDecisionSystem` planting a permanent structure
  modifier, and new build decisions (Build Clinic / Establish Guild Hall / Found School / Plant Contact
  Network) so structure-gated income is reachable.
- **1–100 rebalance**: `decisions.json` deltas rescaled to the 5–20 band (e.g. Collect Taxes -5/-10,
  Propaganda +10/+5/-5, Silent Removal +20 infl / -15 stab / +25 exposure). `events.json` influence/
  stability/development changes multiplied ×4. Verified: Roslyn 0 errors (player + editor); both JSON
  files parse.
