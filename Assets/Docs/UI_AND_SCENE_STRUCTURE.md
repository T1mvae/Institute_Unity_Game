# UI and Scene Structure

## Implementation Plan

1. Split the old single-screen flow into Boot, Main Menu, New Game Setup, Loading, and Gameplay scenes.
2. Preserve gameplay systems that already work: hex regions, decisions, events, resources, save/load, and characters.
3. Move menu/setup screens to UI Toolkit controllers using UXML/USS where editor assignments are available, with runtime fallback UI for resilience.
4. Keep Gameplay on uGUI temporarily as a compatibility layer because the map, region buttons, decision buttons, event popup, and legacy managers already depend on Canvas/UI components.
5. Move visual tokens into JSON and shared USS files so colors, spacing, and typography are not scattered through scene-specific scripts.
6. Add editor tools that can rebuild scenes and validate the theme without manual hierarchy construction.

## Scene Flow

Build Settings now use this order:

1. `Assets/Scenes/Boot.unity`
2. `Assets/Scenes/MainMenu.unity`
3. `Assets/Scenes/NewGameSetup.unity`
4. `Assets/Scenes/Loading.unity`
5. `Assets/Scenes/Gameplay.unity`

Runtime flow:

- `Boot` contains `GameBootstrapper`, which creates persistent configuration, scene-flow, and save services.
- `Boot` transitions to `MainMenu`.
- `MainMenu` uses `MainMenuUIController` and exposes New Game, Continue, Load Game, Settings, About, and Exit.
- `NewGameSetup` uses `NewGameSetupUIController` and stores difficulty, seed, and map settings through `GameSession`.
- `Loading` is a simple transitional scene that loads the pending target scene.
- `Gameplay` uses `GameplaySceneInstaller` to create gameplay-only systems, map viewport, HUD, panels, popup layer, and pause menu.

## Scene Management

`SceneFlowManager` is the single transition service. Use it instead of direct `SceneManager.LoadScene` calls:

- `LoadBoot()`
- `GoToMainMenu()`
- `GoToNewGameSetup()`
- `StartNewGame(NewGameSettings settings)`
- `ContinueGame()`
- `LoadGame(string slot)`
- `ReturnToMainMenu()`
- `QuitGame()`

`GameSession` stores the selected difficulty, new-game map settings, load slot, and pending loading target.

## Theme and Layout Data

Theme JSON:

- `Assets/Data/UI/theme.json`

Layout JSON:

- `Assets/Data/UI/layout_main_menu.json`
- `Assets/Data/UI/layout_gameplay.json`

USS/CSS-like styling:

- `Assets/UI/Styles/base.uss`
- `Assets/UI/Styles/main_menu.uss`
- `Assets/UI/Styles/new_game_setup.uss`
- `Assets/UI/Styles/gameplay.uss`
- `Assets/UI/Styles/popups.uss`

UXML screen layouts:

- `Assets/UI/UXML/MainMenu.uxml`
- `Assets/UI/UXML/NewGameSetup.uxml`
- `Assets/UI/UXML/Loading.uxml`
- `Assets/UI/UXML/SettingsPanel.uxml`

`ThemeLoader` reads `theme.json` and creates safe defaults if the file is missing. `UITheme` is now a compatibility facade for legacy uGUI components and resolves colors/fonts from the same theme data.

## Editor Tools

Use these menu items:

- `Tools / Institute Game / Rebuild Scene Structure`
- `Tools / Institute Game / Rebuild Main Menu`
- `Tools / Institute Game / Rebuild New Game Setup`
- `Tools / Institute Game / Rebuild Gameplay UI`
- `Tools / Institute Game / Validate UI Theme`

The old single-scene builder is intentionally moved to:

- `Tools / Institute Game / Legacy / Rebuild Single Gameplay Scene`

Use the legacy tool only if you need to inspect the previous all-in-one layout.

## Gameplay UI Connection

`GameplaySceneInstaller` creates:

- Gameplay systems: `GameManager`, `ResourceManager`, `TimeManager`, `RegionManager`, `DecisionPool`, `DecisionSelectionManager`, `SaveLoadManager`, `CharacterManager`, `GameDateTracker`, `TimerUI`, and `EventManager`.
- Map controller: `LevelController`, `HexMapGenerator`, and the hex region layer.
- Top HUD: resources, date, and pause.
- Left panel: selected region/world overview.
- Right panel: decisions and embeddable character interactions.
- Bottom panel: operation log.
- Popup layer: event popups and future confirmation dialogs.
- Pause overlay: resume, save, autosave, UI scale placeholder, and return to main menu.

`UIManager` remains as a compatibility adapter for existing gameplay scripts. New panels should be implemented as focused controllers rather than expanding `UIManager`.

## Legacy Files

Preserved legacy scenes:

- `Assets/Scenes/GameScreen.unity`
- `Assets/Scenes/Legacy_Gameplay_Backup.unity`
- `Assets/Scenes/Legacy_MainMenu_Backup.unity`

Legacy runtime UI scripts still present for compatibility:

- `Assets/Scripts/UI/MainMenuController.cs`
- `Assets/Scripts/UI/SaveLoadMenuUI.cs`
- `Assets/Scripts/UI/UIManager.cs`

The new Build Settings do not include the legacy scenes.

## Changing Visual Design

To adjust the prototype style:

1. Edit `Assets/Data/UI/theme.json` for colors, spacing, font sizes, opacity, and animation durations.
2. Edit USS files in `Assets/UI/Styles` for layout and state styling.
3. Run `Tools / Institute Game / Validate UI Theme`.
4. Run `Tools / Institute Game / Rebuild Scene Structure` if you want editor-assigned UXML/USS references refreshed.

## Manual Checks

After Unity recompiles:

1. Open `Assets/Scenes/Boot.unity` and enter Play Mode.
2. Verify `Boot -> MainMenu` transition.
3. Verify `MainMenu -> NewGameSetup -> Loading -> Gameplay`.
4. Verify Continue/Load are disabled when saves are missing and load the proper slot when saves exist.
5. In Gameplay, select a hex region and confirm the left dossier updates.
6. Use map modes, decisions, event popups, character interactions, pause, save, autosave, and return to menu.

---

# Addendum: Corrected map-centric gameplay UI (post-refactor)

The gameplay screen was rebuilt around the new `Institute.World` system. The menu/setup/loading
screens are unchanged (they already used UI Toolkit). The gameplay HUD moved off the hard-coded,
procedurally-built uGUI layout onto **UI Toolkit driven by UXML/USS/JSON**.

## Decision: UI Toolkit (not uGUI)

UI Toolkit is available (Unity 6, `com.unity.modules.uielements`) and already proven by the menu
screens, which set up a runtime `UIDocument` + `PanelSettings` via `UIToolkitThemeUtility.EnsureDocument`.
The new gameplay HUD uses the same pattern, so **no uGUI Canvas is used for the new HUD**. The hex map
itself is rendered in **world space** (a mesh), not as UI.

## New gameplay files

- Structure (UXML): `Assets/UI/UXML/GameplayHUD.uxml`, `PauseMenu.uxml`, `EventPopup.uxml`, `Settings.uxml`
- Styling (USS): appended HUD classes to `Assets/UI/Styles/gameplay.uss`; overlay/dialog classes to
  `Assets/UI/Styles/popups.uss` (plus existing `base.uss`).
- Tokens (JSON): `Assets/Data/UI/theme.json` (now includes a `map` section for terrain/map-mode colors).
- Controller: `Assets/Scripts/World/UI/WorldHUDController.cs` (namespace `Institute.World.UI`).
- Installer: `Assets/Scripts/World/WorldGameplayInstaller.cs`.

## HUD layout (GameplayHUD.uxml)

- **Top bar** — Money, Artifacts, Sanity, Global Stability, Date, Pause.
- **Left panel** — selected **region** dossier (type, tiles, capital, Influence/Stability/Development,
  population/wealth, neighbors, tags, characters) **or** selected **tile** info (terrain, biome,
  elevation/moisture, danger, features, "Unclaimed — No organized region") **or** world overview.
- **Right panel** — contextual directives (region: Project Influence / Stabilize / Invest; wilderness:
  Scout / Establish Outpost). _Illustrative actions; not yet wired to the legacy decision system._
- **Bottom log**, **map mode bar** (Terrain/Political/Influence/Stability/Development/Danger), and a
  **hover tooltip** (coordinate, terrain, region, features).

`WorldHUDController` binds to `WorldController` events (`RegionSelected`, `TileSelected`,
`SelectionCleared`, `TileHovered`, `MapModeChanged`) and sets `MapInteractionGate.PointerOverUI` on
panel hover so map clicks don't fire through the UI.

## Scenes

Existing flow is unchanged: `Boot → MainMenu → NewGameSetup → Loading → Gameplay` (all in Build
Settings). The new map-centric gameplay is installed by `WorldGameplayInstaller` (one `WorldController`
+ `WorldHUDController`). Because new scripts have no Unity-assigned GUIDs until import, run the editor
tool **once** to switch the scene:

- `Tools / Institute Game / Rebuild Gameplay World Scene` — rewrites `Gameplay.unity` to use the new
  installer (used by the normal flow).
- `Tools / Institute Game / Generate Test Hex World` — creates `Assets/Scenes/WorldTest.unity` to try
  the world standalone (press Play).

## How to edit the visual style

1. Colors / spacing / fonts / **map colors**: `Assets/Data/UI/theme.json` (the `map` section feeds
   `MapPalette`; the rest feeds `UIThemeConfig`/`ThemeLoader`).
2. Terrain & region-type colors and rules: `Assets/Data/Map/*.json`.
3. Layout & component styling: `Assets/UI/Styles/*.uss` (gameplay HUD classes live in `gameplay.uss`).
4. HUD structure: `Assets/UI/UXML/GameplayHUD.uxml`.
5. No colors are hard-coded in gameplay scripts — change the JSON/USS, not C#.

## Legacy (still present, not in the new path)

`GameplaySceneInstaller`, `UIManager`, `RegionUI`, `MapViewController`, `HexMapGenerator`,
`RegionGridGenerator`, `VoronoiRegionGenerator`, `Region`, and `Assets/Scenes/GameScreen.unity` /
`Legacy_*_Backup.unity`. See `CHANGELOG_REFACTOR.md` and `PREVIOUS_WORK_AUDIT.md`.

---

# Addendum 2: Wired overlays + HUD responsibilities (integration pass)

The authored overlay UXML is now wired to UI Toolkit controllers (each its own `UIDocument` +
`PanelSettings`, sorted above the HUD):

| Overlay | UXML | Controller (`Institute.World.UI`) | Opens from |
|---------|------|-----------------------------------|-----------|
| Pause | `PauseMenu.uxml` | `WorldPauseController` (sort 200) | HUD pause button |
| Event popup | `EventPopup.uxml` | `WorldEventPopupController` (sort 250) | `RegionEventSystem.EventReady` |
| Settings | `Settings.uxml` | `WorldSettingsController` (sort 300) | Pause ▸ Settings |

- **Pause:** Resume / Save Game / Load Game / Settings / Return to Main Menu / Quit. Pauses via
  `Time.timeScale` + `GameManager`. Settings opens on top without losing pause.
- **Event popup:** modal (pauses time); shows title/body/affected region+character + choices with
  cost/effect; disabled choices stay visible. Choosing runs the choice and closes.
- **Settings:** UI scale (adjusts the shared `PanelSettings` reference resolution), master volume
  (`AudioListener.volume`), tile-grid toggle (stored placeholder); persists to PlayerPrefs.

## HUD controller responsibilities (`WorldHUDController`)

Coordinates panels; does **not** own gameplay logic:
- Shows resources (via `GameResources` → ResourceManager/LevelController), global stability, date.
- Shows the selected **RegionData** dossier or selected **tile** info (subscribes to `WorldController`
  selection events + `RegionDataChanged`).
- Right panel: real **decisions** (`RegionDecisionSystem`) + **character interactions**
  (`RegionCharacterSystem`).
- Pause button → `WorldPauseController.Open()` (no direct `Time.timeScale`).
- Map-mode bar → `WorldController.SetMapMode`.
- Hover tooltip; sets `MapInteractionGate.PointerOverUI` over panels.

The installer `WorldGameplayInstaller` creates the systems + overlays and routes
`RegionEventSystem.EventReady` → `WorldEventPopupController.Show`. See `INTEGRATION_COMPLETED.md` and
`LEGACY_SYSTEMS.md`.
