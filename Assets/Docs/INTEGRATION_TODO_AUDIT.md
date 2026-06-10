# Integration TODO Audit

Audit of how far the new `Institute.World` (RegionData/HexTileData/WorldController) model is wired
into the legacy gameplay systems, and what this pass changes.

## Where the old `Region` is still used (before this pass)

| File | Uses old `Region` for | Disposition |
|------|----------------------|-------------|
| `Map/Region.cs`, `RegionManager.cs`, `RegionUI.cs`, `RegionGridGenerator.cs`, `VoronoiRegionGenerator.cs`, `HexMapGenerator.cs`, `Continent.cs`, `RegionModifier.cs` | the legacy map model + uGUI tiles | **Legacy** (map). Not used by the new world path. |
| `Core/LevelController.cs` | `SelectedRegion`, `AllRegions` (List<Region>) | **Legacy/compat**. Kept for resources (`Money/Sanity/Artifacts`) which the new HUD reads. Its Region list is unused by the new path. |
| `Decisions/DecisionSelectionManager.cs` (15), `ActionButton.cs` | applies deltas to `Region`, cooldowns keyed by `Region` | **Superseded** by `Institute.World.Gameplay.RegionDecisionSystem`. |
| `Events/EventManager.cs` (28) | targets `Region`, uGUI `EventPanelUI` | **Superseded** by `RegionEventSystem` + UI-Toolkit `WorldEventPopupController`. |
| `Characters/CharacterManager.cs` (12) | generates/holds characters tied to `Region`/`LevelController.AllRegions` | **Superseded** by `RegionCharacterSystem` (reuses the `GameCharacter` data class, which is already `regionId`-keyed). |
| `UI/UIManager.cs` (14), `UI/CharacterPanelUI.cs`, `UI/Gameplay/*` | legacy uGUI HUD bound to `Region` | **Legacy**. The new HUD is `WorldHUDController` (UI Toolkit). |
| `SaveSystem/SaveLoadManager.cs` (6) | legacy `SaveGameData` with `RegionSaveData` (tiles-as-regions) | **Legacy**. The world is saved separately via `WorldSaveBridge` (`<slot>.world.json`); characters/decisions added in this pass. |

The data-only classes are **reused, not rewritten**: `DecisionDefinition` (DecisionPool.cs), `GameEvent`/`EventOption`/`EventScope`/`FeaturedPerson`/`RawGameEvent` (GameEvent.cs/EventManager.cs), `GameCharacter`/`CharacterRole`/`CharacterStatus`/`CharacterInteractionType` (CharacterData.cs), and `CharacterSaveData`/`DecisionCooldownSaveData` (SaveGameData.cs). The Region-free static helpers `DecisionSelectionManager.GetEffectiveCost` / `AffectsRegion` are reused too.

## How WorldController exposes selection (Part 6 mapping)

`WorldController` already raised: `WorldBuilt(WorldMapData)`, `RegionSelected(RegionData)`,
`TileSelected(HexTileData)`, `SelectionCleared()`, `TileHovered(HexTileData)`,
`MapModeChanged(MapMode)`. This pass **adds `RegionDataChanged(RegionData)`** and
`RaiseRegionDataChanged(region)` (also recolors the map when a stat map-mode is active). These map to
the Part-6 suggested names (OnSelectedRegionChanged → `RegionSelected`, OnSelectedTileChanged →
`TileSelected`, OnRegionDataChanged → `RegionDataChanged`, OnWorldMapChanged → `WorldBuilt`,
OnMapModeChanged → `MapModeChanged`). UI subscribes to these instead of polling each frame.

## Which systems were migrated this pass

- **Decisions** → `RegionDecisionSystem` (RegionData, resources, cooldowns, disabled reasons).
- **Events** → `RegionEventSystem` + `WorldEventPopupController` (UI Toolkit `EventPopup.uxml`).
- **Characters** → `RegionCharacterSystem` (region-attached `GameCharacter`s, interactions via `WorldCharacterBridge`).
- **Pause** → `WorldPauseController` (`PauseMenu.uxml`), replacing the HUD's `Time.timeScale` stopgap.
- **Settings** → `WorldSettingsController` (`Settings.uxml`), openable from pause.
- **Save** → world + characters + decision cooldowns persisted alongside `<slot>.world.json`.

## Which systems still need adapters / remain legacy

- `LevelController` is used as a **resource compatibility source** only (read-only `Money/Sanity/Artifacts`). If absent, `GameResources` falls back to `ResourceManager`. No Region access from the new path.
- The legacy `DecisionSelectionManager` / `EventManager` / `CharacterManager` MonoBehaviours are **not instantiated** by `WorldGameplayInstaller`. They remain only for the legacy `GameScreen`/`Gameplay` (old installer) scenes. See `LEGACY_SYSTEMS.md`.

## Risky dependencies / notes

- Old `SaveGameData` (one-hex-one-region) is still rejected by the world loader; this pass keeps that.
- The new systems assume `WorldController.Instance` exists in the scene (installer guarantees it).
- UI Toolkit runtime text needs a `PanelSettings` + theme — reused via `UIToolkitThemeUtility.EnsureDocument` (the proven menu pattern).

## Compile status at audit time

Project compiled clean (verified previously via Roslyn against Unity 6 DLLs, 0 errors). This pass adds
new files under `Institute.World.Gameplay`/`Institute.World.UI` and is re-verified the same way.
