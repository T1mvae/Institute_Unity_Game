# Previous Work Audit

_Audited against the project state produced by the first three prompts. Unity 6000.0.42f1, UI Toolkit (`com.unity.modules.uielements`) present and supported._

## Summary verdict

The previous work delivered a reasonable **multi-scene shell** and a **data-driven theme loader**, but the **core map model is conceptually wrong** and the **gameplay UI is built procedurally in C# with hard-coded colors**. The single most important defect: there is no separation between a *hex tile* and a *region*. The class `Region` **is** the hex tile.

## What currently works (preserve)

- **Multi-scene flow exists.** `Boot → MainMenu → NewGameSetup → Loading → Gameplay` scenes exist and are all in `EditorBuildSettings`. `SceneFlowManager`, `GameSession`, `NewGameSettings`, `GameBootstrapper` form a usable transition layer.
- **Theme loader is sound.** `Assets/Data/UI/theme.json` + `UIThemeConfig`/`ThemeLoader` load colors/spacing/fonts from JSON with safe defaults if the file is missing. This is the right idea and is reused by the new work.
- **UI Toolkit beachhead exists.** `Assets/UI/UXML/{MainMenu,NewGameSetup,Loading,SettingsPanel}.uxml` + `Assets/UI/Styles/*.uss` exist for the menu/setup screens with runtime fallback controllers.
- **Content is already data-driven** for decisions/events: `Assets/StreamingAssets/{decisions,events}.json` loaded via `DecisionPool`/`EventManager`.
- **Difficulty + seed plumbing** exists (`DifficultyConfig`, `GameSession.ActiveDifficulty`).
- **Save/load is versioned** (`SaveGameData.saveVersion = 1`) and fails soft on bad/missing files.
- **Character system is reasonably complete** (`GameCharacter`, `CharacterManager`, `CharacterSaveData`) and already keys characters by `homeRegionId`/`currentRegionId` **strings** — which makes re-pointing them at the new `RegionData` IDs straightforward.

## What is broken / conceptually wrong (must be replaced)

1. **`Region` == hex tile (the headline bug).** `Assets/Scripts/Map/Region.cs` carries `HexQ`, `HexR` **and** `Influence`/`Stability`/`Development`. `HexMapGenerator.GenerateRegions()` creates **one `Region` per hex coordinate** (`mapWidth*mapHeight` regions). So "region count == hex count", every tile has political stats, and there is no concept of unclaimed/wilderness/sea tiles. This is exactly the model the new prompt forbids.
2. **Map is uGUI, not a world map.** Hexes are `Image`+`Button` UI elements parented to a Canvas `RectTransform` (`HexMapGenerator.SpawnRegionTiles`). It cannot scale to hundreds/thousands of tiles, has no real terrain, and is "forced into one screen."
3. **No terrain / biome / walkability / unclaimed model at all.** `RegionType` is a flat 8-value enum used as both terrain *and* political type.
4. **Gameplay UI is hard-coded in C#.** `GameplaySceneInstaller` (and the legacy `InstituteSceneRebuilder`) build the entire HUD procedurally with literal colors and `UITheme.*` constants. Despite the theme JSON existing, the gameplay screen does not consume UXML/USS. This is the "hardcoded visual style in random scripts" problem.
5. **Three competing map generators.** `HexMapGenerator`, `RegionGridGenerator` (1098 lines), and `VoronoiRegionGenerator` (1408 lines) all exist; `GameplaySceneInstaller` wires up all three onto one GameObject. Half-migrated and confusing.
6. **Save format encodes the wrong model.** `RegionSaveData` stores `hexQ/hexR/influence/stability/development` per "region" — i.e. per tile. There is no tile layer and no unclaimed list.

## What is badly designed (improve)

- `UIManager` is a catch-all adapter holding direct `Text` references for every stat.
- Gameplay scene relies on `GameplaySceneInstaller.Awake()` constructing the whole hierarchy at runtime — the scene asset is nearly empty, so the "scene" is really code.
- `LevelController` (554 lines) mixes camera, resources, selection, map modes, and game-over logic.

## Legacy files (kept compiling, demoted, not part of the new path)

| File | Disposition |
|------|-------------|
| `Assets/Scripts/Map/Region.cs` | Legacy political-tile hybrid. Superseded by `Institute.World.RegionData` + `HexTileData`. Left compiling for old systems. |
| `Assets/Scripts/Map/HexMapGenerator.cs` | Legacy "one hex = one region" generator. Superseded by `WorldMapGenerator`. |
| `Assets/Scripts/Map/RegionGridGenerator.cs` | Legacy generator. Not used by new path. |
| `Assets/Scripts/Map/VoronoiRegionGenerator.cs` | Legacy generator. Concepts folded into new growth step. |
| `Assets/Scripts/Map/RegionUI.cs`, `MapViewController.cs`, `RegionManager.cs` | Legacy uGUI tile/region view. Superseded by world-space renderer. |
| `Assets/Scenes/GameScreen.unity`, `Legacy_*_Backup.unity` | Legacy single-screen scenes. Not in build. |
| `Assets/Scripts/Editor/InstituteSceneRebuilder.cs` | Legacy single-scene builder (`Tools/Institute Game/Legacy/...`). |

## Compile / warnings status

- Project compiled under the previous prompts (built `.app` bundles exist). No duplicate top-level type names were found among current scripts.
- **The new world system is added under namespace `Institute.World`** with new type names (`RegionData`, `HexTileData`, `WorldMapData`, `WorldMapGenerator`, `TerrainType`, `BiomeType`, and `Institute.World.RegionType`). This guarantees **no collision** with the global `Region`/`RegionType`/`HexMapGenerator` types, so adding it cannot break existing compilation.

## Is UI Toolkit available and safe?

**Yes.** `com.unity.modules.uielements` is in `Packages/manifest.json`, the editor is Unity 6, and the project already ships working UXML/USS for the menu screens. UI Toolkit is used for the new gameplay HUD/panels. uGUI remains only in legacy scripts.

## Does save/load already support the needed data?

**No.** The save format stores tiles-as-regions only. The refactor adds a `WorldMapSaveData` block (separate tile list + region list + unclaimed list + seed + size) and bumps `saveVersion`. A migration/`detect-old-format` guard is added so pre-refactor saves are flagged and rejected safely instead of crashing.

## Decision

Per the prompt: **do not patch the one-hex-one-region model.** Build a new, self-contained, correct world-map subsystem (`Institute.World`) — data model, generator, renderer, selection, save, validation, editor tools — make it the canonical gameplay path, drive the UI from UXML/USS/JSON, and demote the old map/UI code to legacy. See `MAP_GENERATION.md`, `UI_AND_SCENE_STRUCTURE.md`, and `CHANGELOG_REFACTOR.md`.
