# Institute Prototype Rebuild

This project is now organized around a cleaner grand-strategy prototype structure and an editor-driven scene rebuild workflow.


## Current Scene Architecture

The project now uses a multi-scene flow instead of hiding every screen inside one gameplay scene:

- `Assets/Scenes/Boot.unity` — initializes persistent services and theme/config loading.
- `Assets/Scenes/MainMenu.unity` — UI Toolkit main entry screen.
- `Assets/Scenes/NewGameSetup.unity` — difficulty, seed, and map setup.
- `Assets/Scenes/Loading.unity` — transition scene for world/gameplay loading.
- `Assets/Scenes/Gameplay.unity` — gameplay-only map, HUD, panels, popups, pause menu, and runtime systems.

Theme and layout data live in `Assets/Data/UI`, UI Toolkit UXML lives in `Assets/UI/UXML`, and USS styling lives in `Assets/UI/Styles`. See `Assets/Docs/UI_AND_SCENE_STRUCTURE.md` for the full rebuild workflow.

## Project Structure

- `Assets/Scripts/Core` — high-level gameplay state, time, resources, player log, and legacy `LevelController` compatibility.
- `Assets/Scripts/Map` — regions, continents, modifiers, map view modes, region UI, and procedural region generation.
- `Assets/Scripts/Characters` — generated world characters, relationships, interactions, passive regional effects, and save adapters.
- `Assets/Scripts/Decisions` — decision loading, execution, cooldowns, costs, and decision button behavior.
- `Assets/Scripts/Events` — event data models and event execution logic.
- `Assets/Scripts/UI` — HUD, panels, styling, tooltip behavior, menu controls, and UI adapters.
- `Assets/Scripts/SaveSystem` — versioned JSON save payloads and save/load manager.
- `Assets/Scripts/Data` — difficulty configuration and future data-only gameplay config classes.
- `Assets/Scripts/Editor` — editor automation, including the main scene rebuild tool.
- `Assets/Prefabs/UI`, `Assets/Prefabs/Map`, `Assets/Prefabs/Characters` — reorganized prefab locations.
- `Assets/StreamingAssets` — runtime JSON source for `decisions.json` and `events.json`.
- `Assets/Data/Events` and `Assets/Data/Decisions` — reserved for future editor-facing data organization.

## Rebuilding the Main Scene

Open Unity and run:

`Tools / Institute Game / Rebuild Main Scene`

This invokes `InstituteSceneRebuilder.RebuildMainScene`, which creates a coherent `Assets/Scenes/GameScreen.unity` layout with:

- Main camera and 16:9-scaled canvas.
- Dark Institute tablet visual theme.
- Top resource/date/menu bar.
- World overview and selected-region dossier panel.
- Decision panel with generated decision buttons.
- Event popup prefab with cost/consequence display.
- Map mode buttons for Default, Influence, Stability, and Development.
- Hex map generation using axial coordinates.
- Tooltip panel and UI-safe map click behavior.
- Game, region, resource, time, event, decision, and UI managers.
- Character manager and generated character interaction panel.
- Save/load buttons and map legend.

## New Game Flow

`MainMenuController` now opens a generated pre-game setup panel from the Start button. The panel supports:

- `Easy`, `Normal`, `Hard`, and `Custom` difficulty profiles.
- Custom starting resources, map size, random seed, event frequency, decision cost multiplier, and starting stat modifiers.
- `Continue` from the autosave slot when present.
- `Load Manual Save` when present.

Difficulty is stored in `GameSession.ActiveDifficulty` and is applied when `GameScreen` starts.

## Hex Map

`HexMapGenerator` is now the preferred map generator. It creates pointy-top hex tiles using axial `q/r` coordinates and automatically links six-direction neighbors. Each generated `Region` has:

- Stable ID.
- Display name.
- Hex coordinates.
- Region type.
- Influence, Stability, and Development.
- Neighbor IDs and runtime neighbor references.

The old `RegionGridGenerator` remains as a legacy fallback only.

## Characters

`CharacterManager` generates important world characters after a new hex map is created. Roles are influenced by region type and development, so ruins can produce scholars or Institute sympathizers, trade hubs can produce guild representatives, and unstable frontier regions can produce rebel organizers or village speakers.

Characters track relationship, loyalty, trust, fear, ambition, competence, corruption, influence power, status, revealed traits, hidden traits, faction, role, tags, home region, and current region. Runtime state is serializable through `CharacterSaveData`.

The generated character panel shows:

- Characters in the currently selected region.
- A selected character dossier with relationship and power stats.
- Interaction buttons for Negotiate, Bribe, Threaten, Support, Undermine, Recruit as Contact, and Investigate.
- A global searchable character list sorted by influence or relationship.

Characters also apply passive effects every few in-game days. Loyal lords and recruited contacts can raise Institute influence, scholars can improve development, priests can stabilize conservative regions, corrupt merchants can create money while reducing stability, and ignored rebels can destabilize their region.

## Save / Load

`SaveLoadManager` writes JSON to `Application.persistentDataPath/Saves`.

- Manual slot: `manual.json`.
- Autosave slot: `autosave.json`.
- Save format is versioned with `saveVersion = 1`.
- Bad, missing, or mismatched saves log warnings/errors and do not crash gameplay.

Saved state includes difficulty, seed, time, player resources, regions, hex coordinates, neighbors, active modifiers, selected region, decision cooldowns, character state, character interaction cooldowns, and active event state when available.

## Preserved Systems

- Region stats, modifiers, generated continents, and map modes remain driven by `LevelController` and `RegionGridGenerator`.
- Decisions still load from `Assets/StreamingAssets/decisions.json` through `DecisionPool`.
- Events still load from `Assets/StreamingAssets/events.json` through `EventManager`.
- Existing resources (`Money`, `Artifacts`, `Sanity`) remain visible through `UIManager` and compatible with `ResourceManager`.
- Legacy systems are preserved rather than deleted; new manager layers wrap them where useful.

## Manual Checks

After regenerating the scene in Unity:

1. Open `Assets/Scenes/GameScreen.unity`.
2. Enter Play Mode and verify regions generate.
3. Click regions and confirm the dossier updates.
4. Click empty map space and confirm the world overview appears.
5. Use each map mode button and confirm region colors change.
6. Use decisions with and without a selected region and confirm disabled reasons/cooldowns.
7. Select regions and confirm the character panel lists local characters.
8. Use each character interaction and confirm resources, character stats, and regional stats update.
9. Save, return to menu, continue/load, and confirm character state persists.
10. Wait for events and confirm options show costs and consequences.

If Unity reports missing prefab references, run the rebuild menu item again after scripts finish compiling.
