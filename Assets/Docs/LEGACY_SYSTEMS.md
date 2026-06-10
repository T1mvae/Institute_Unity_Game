# Legacy Systems

This file lists the pre-refactor scripts that operate on the old one-hex-one-region `Region` model.
They still compile (so old scenes don't break), but the **new gameplay path does not instantiate or
depend on them**. The new path is `WorldGameplayInstaller` → `WorldController` + the RegionData-driven
systems in `Institute.World.Gameplay` + the UI Toolkit overlays in `Institute.World.UI`.

## Source of truth

`Institute.World.RegionData` is the political region model. `Institute.World.HexTileData` is the tile
model. `WorldController.SelectedRegion` / `SelectedTile` are the selection. Decisions, events,
characters, UI panels and save/load all use these — never the legacy `Region`.

## Replaced by new systems (do not use going forward)

| Legacy script | Replaced by | Notes |
|---------------|-------------|-------|
| `Decisions/DecisionSelectionManager.cs` | `Institute.World.Gameplay.RegionDecisionSystem` | New system reuses the `DecisionDefinition` data class and the Region-free statics `GetEffectiveCost`/`AffectsRegion` from the legacy class. |
| `Events/EventManager.cs` | `Institute.World.Gameplay.RegionEventSystem` + `Institute.World.UI.WorldEventPopupController` | Reuses `GameEvent`/`EventOption`/`RawGameEvent` data classes. |
| `Characters/CharacterManager.cs` | `Institute.World.Gameplay.RegionCharacterSystem` | Reuses the `GameCharacter` data class (already regionId-keyed) and `WorldCharacterBridge`. |
| `UI/Gameplay/GameplaySceneInstaller.cs` | `Institute.World.WorldGameplayInstaller` | New installer; old one builds the uGUI one-hex map. |
| `UI/UIManager.cs`, `UI/Gameplay/*` (PauseMenuController, EventPopupController, DecisionPanelController, RegionPanelController, GameplayHUDController, GameLogUIController), `UI/CharacterPanelUI.cs` | `Institute.World.UI.WorldHUDController` + overlays | Old HUD is uGUI/Region; new HUD is UI Toolkit/RegionData. |
| `Map/HexMapGenerator.cs`, `RegionGridGenerator.cs`, `VoronoiRegionGenerator.cs`, `RegionUI.cs`, `MapViewController.cs`, `RegionManager.cs`, `Region.cs`, `RegionModifier.cs`, `Continent.cs` | `Institute.World.*` (WorldMapGenerator, RegionData/HexTileData, renderer) | Legacy map model + uGUI tiles. |

## Compatibility adapters (deliberate, narrow)

- **`LevelController` as resource source.** `Institute.World.Gameplay.GameResources` reads/writes
  resources via `ResourceManager` first, then `LevelController` if present. The new path creates a
  `ResourceManager` (not a `LevelController`), so no `Region` is touched. `LevelController` remains
  only so legacy scenes and the HUD's optional `LevelController` read still work.
- **Reused data/static helpers** (not Region logic): `DecisionDefinition`, `GameEvent`/`EventOption`,
  `GameCharacter`, `CharacterSaveData`, `DecisionCooldownSaveData`, and the static
  `DecisionSelectionManager.GetEffectiveCost` / `AffectsRegion`.

## Legacy scenes (not in Build Settings)

`Assets/Scenes/GameScreen.unity`, `Legacy_Gameplay_Backup.unity`, `Legacy_MainMenu_Backup.unity`.

## If you want to delete legacy code later

Safe order: (1) confirm no scene references the legacy MonoBehaviours (the build scenes use the new
installer after running `Tools / Institute Game / Rebuild Gameplay World Scene`); (2) delete the
legacy `UI/Gameplay/*` + `Map/*` (old) + `Decisions/DecisionSelectionManager.cs` +
`Events/EventManager.cs` + `Characters/CharacterManager.cs`; (3) keep the **data classes** that the new
systems reuse (`DecisionDefinition`, `GameEvent`/`EventOption`, `GameCharacter`, save data) or move them
into the new namespace. Until then they are isolated, not active.
