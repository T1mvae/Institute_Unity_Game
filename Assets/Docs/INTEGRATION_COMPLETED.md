# Integration Completed

This pass connected the new `Institute.World` (RegionData/HexTileData/WorldController) model to the
gameplay systems and wired the authored UI Toolkit overlays into the new HUD. Verified to compile via
Roslyn against Unity 6 DLLs (0 errors) in both player and editor configurations.

## Decision migration

- New `Institute.World.Gameplay.RegionDecisionSystem` operates on `RegionData` + `WorldController.SelectedRegion`.
- Reuses the `DecisionDefinition` data class (loaded from `StreamingAssets/decisions.json`) and the
  region-free statics `DecisionSelectionManager.GetEffectiveCost` / `AffectsRegion`.
- `CanApplyDecision(def, region, out reason)`, `GetDisabledReason(...)`, `ApplyDecision(...)`.
- Costs/rewards via `GameResources` (ResourceManager → LevelController). Stat deltas applied to
  `RegionData.influence/stability/development`. Per-(decision, region) cooldowns; saved/restored.
- HUD right panel lists real decisions with cost/effect/disabled-reason; clicking applies and refreshes
  the dossier + recolors the map (stat modes) via `WorldController.RaiseRegionDataChanged`.
- Unclaimed tile selected → only non-regional decisions are offered (no nulls/crashes).

## Event migration

- New `Institute.World.Gameplay.RegionEventSystem` schedules Personal/Local/Global events (difficulty-paced),
  reusing `GameEvent`/`EventOption`. Local → random `RegionData`; Personal → a `GameCharacter` + its region;
  Global → all regions.
- Effects modify `RegionData` stats (+ per-tick modifiers collapsed to immediate deltas), resources, and
  character relationship/loyalty/trust/etc. Safe skips when no valid region/character exists.
- Displayed through `Institute.World.UI.WorldEventPopupController` (authored `EventPopup.uxml`): title,
  body, affected region/character, choices with cost/effect, disabled choices stay visible. Modal: pauses
  time while open. Logs appeared/selected/applied.

## Character migration

- New `Institute.World.Gameplay.RegionCharacterSystem` generates region-attached `GameCharacter`s
  (deterministic by map seed) and attaches them via `WorldCharacterBridge` (regionId, never a tile).
- Selected-region panel shows local characters; interaction buttons (Negotiate/Bribe/Support/Recruit/
  Investigate) modify character stats and the region's stats (`WorldCharacterBridge.ApplyEffect`).
- Save/load uses `CharacterSaveData` (regionId-based) and re-attaches characters to regions on load.

## UI wiring

- **Pause** → `WorldPauseController` (`PauseMenu.uxml`): Resume / Save / Load / Settings / Return to Main
  Menu / Quit. Pauses via `Time.timeScale` + `GameManager`. The HUD pause button opens it (the old direct
  `Time.timeScale` toggle is removed).
- **Event popup** → `WorldEventPopupController` (`EventPopup.uxml`), fed by `RegionEventSystem.EventReady`.
- **Settings** → `WorldSettingsController` (`Settings.uxml`): UI scale (adjusts shared PanelSettings
  reference resolution), master volume (`AudioListener.volume`), tile-grid toggle (stored placeholder),
  close. Persists to PlayerPrefs. Opens from pause without losing pause state.
- HUD (`WorldHUDController`) coordinates panels and subscribes to `WorldController` selection +
  `RegionDataChanged`; it does not own gameplay logic.

## Save/load

- `GameSaveService.SaveAll(slot)` writes the world (`<slot>.world.json`) + a companion
  `<slot>.gameplay.json` (characters, decision cooldowns, selected region).
- `GameSaveService.LoadAll(slot)` rebuilds the world, restores characters→regions and cooldowns, and
  restores/clears the selection. Old one-hex-one-region saves are still **rejected safely** (no crash).

## WorldController observables (Part 6)

Existing `WorldBuilt`, `RegionSelected`, `TileSelected`, `SelectionCleared`, `TileHovered`,
`MapModeChanged` + **new `RegionDataChanged`** (+ `RaiseRegionDataChanged`, which recolors the map in
stat modes). UI reacts to these instead of polling.

## Remaining limitations / TODOs

- **Scene wiring is editor-driven** (new scripts have no Unity GUIDs until import): run
  `Tools / Institute Game / Rebuild Gameplay World Scene` once so the normal flow's `Gameplay.unity`
  uses `WorldGameplayInstaller`. Until then the old scene loads the legacy map.
- Event per-tick modifiers are applied as a single immediate delta (no over-time ticker yet).
- Settings UI scale uses reference-resolution scaling (no separate per-canvas option); fullscreen not
  exposed (not in the authored UXML).
- Borders are per-edge meshes (shader outline is a future TODO; see `RegionOverlayRenderer`).
- The legacy `CharacterManager`/`DecisionPool`/`EventManager`/`UIManager` remain in the project but are
  **not used** by the new path (see `LEGACY_SYSTEMS.md`).
