# User Setup Instruction

A concrete checklist to make the game run after this refactor. Do the steps in order.
Expected results and fixes are listed for each step.

> **The one step you must not skip:** after Unity finishes importing/compiling, run
> **`Tools / Institute Game / Rebuild Gameplay World Scene`** once. New C# scripts have no Unity asset
> GUIDs until Unity imports them, so the build-flow `Gameplay.unity` is **not** auto-switched to the new
> system. This menu item rewrites `Gameplay.unity` to use `WorldGameplayInstaller` (the new map +
> RegionData gameplay + UI Toolkit overlays). Until you run it, the old one-hex map loads.

---

## 1. First launch after refactor

1. Open the project in Unity **6000.0.42f1** (Unity 6). Let it finish importing and compiling
   (watch the bottom-right spinner; the Console should show **0 errors**).
2. Entry point scene: **`Assets/Scenes/Boot.unity`**. Open it and press Play — `Boot` initializes
   services and loads `MainMenu`.
3. Build Settings (File ▸ Build Settings) should list, in this exact order:
   - 0 `Assets/Scenes/Boot.unity`
   - 1 `Assets/Scenes/MainMenu.unity`
   - 2 `Assets/Scenes/NewGameSetup.unity`
   - 3 `Assets/Scenes/Loading.unity`
   - 4 `Assets/Scenes/Gameplay.unity`
   These are already configured. If any are missing, drag them in and reorder. **Boot must be index 0.**
   - _Expected:_ pressing Play from Boot shows the Main Menu.
   - _If wrong:_ see §11 "MainMenu does not load".

## 2. Required Unity setup

- **Unity version:** 6000.0.42f1 (Unity 6). Other Unity 6 patch versions should work.
- **UI Toolkit:** required and already present (`com.unity.modules.uielements`). No install needed.
- **TextMeshPro:** **not required for the new HUD/menus** (they use UI Toolkit). If Unity prompts to
  import "TMP Essentials" when opening a legacy scene, you may import it, but it is optional for the new
  flow.
- **Packages:** nothing extra to install.
- **Generated data files** (already created; verify they exist):
  - `Assets/Data/UI/theme.json` (has a `map` section)
  - `Assets/Data/Map/terrain_definitions.json`, `region_type_definitions.json`, `map_presets.json`
  - `Assets/Data/Difficulty/difficulty_presets.json`
  - `Assets/StreamingAssets/decisions.json`, `events.json`
  If any are missing, run `Tools / Institute Game / Rebuild Map Data Files` and
  `Tools / Institute Game / Rebuild UI Theme Files`.
- **Inspector references:** the new HUD/overlays build themselves at runtime (they load UXML/USS in the
  editor automatically), so no manual reference assignment is normally required. The only required
  manual action is the scene rebuild in §3.

## 3. How to rebuild scenes (editor menu tools)

Run these from the Unity menu bar (top), in this order, once:

1. `Tools / Institute Game / Rebuild UI Theme Files` — ensures `theme.json` exists/valid.
2. `Tools / Institute Game / Rebuild Map Data Files` — ensures terrain/region JSON exists.
3. `Tools / Institute Game / Rebuild Gameplay World Scene` — **switches `Gameplay.unity` to the new system** (required).
4. `Tools / Institute Game / Validate Gameplay Integration` — sanity-checks the wiring (expect PASS).
5. `Tools / Institute Game / Validate Map Data` — generates a test world and checks the model (expect PASS).

Optional / pre-existing tools (menu screens use UI Toolkit already):
- `Tools / Institute Game / Rebuild Scene Structure`, `Rebuild Main Menu`, `Rebuild New Game Setup`,
  `Validate UI Theme`.
- `Tools / Institute Game / Generate Test Hex World` — builds `Assets/Scenes/WorldTest.unity` (a
  standalone scene with the full installer) to try gameplay without the menu flow.
- `Tools / Institute Game / Audit Project`, `Validate Save Data`.

> The legacy `Rebuild Gameplay UI` and `Legacy / Rebuild Single Gameplay Scene` build the OLD uGUI map.
> Do **not** use them for the new flow.

## 4. How to start a new game

1. Open `Boot.unity`, press Play. _Expected:_ Main Menu appears (dark themed, button column).
2. Click **New Game**. _Expected:_ New Game Setup screen.
3. Pick a **difficulty** card (e.g. Normal).
4. In the **World Map** section choose **Map Size** (`Small` / `Medium` / `Large`), and optionally edit
   **Region Count Target**, **Unclaimed Land %**, **Terrain Roughness**.
5. Set a seed in **Random Seed**, or click **Randomize Seed**.
6. Click **Start Game**. _Expected:_ a short Loading screen, then the Gameplay scene.
7. _Expected in Gameplay:_ a large hex map fills the screen with a top bar, side panels, a map-mode bar,
   and a log. _If the map looks like a few big UI hexes instead, you skipped §3 step 3._

## 5. How to test map generation

- **Large grid of small tiles:** zoom (mouse wheel) — you should see many small hexes, not a handful.
- **Regions are groups of tiles:** switch to **Political** mode (map-mode bar) — adjacent tiles share a
  region color; each region spans many tiles. Region count ≪ tile count.
- **Some tiles unclaimed:** in Political mode, grey tiles are unclaimed wilderness; sea is blue.
- **Click an owned tile:** the whole owning region highlights and the left panel shows region stats.
- **Click an unclaimed tile:** the left panel shows terrain/wilderness info and "Unclaimed — No
  organized region".
- **Map modes:** click Terrain / Political / Influence / Stability / Development / Danger and confirm the
  map recolors (stat modes color owned tiles by the region's stat; unclaimed = grey).

## 6. How to test decisions

1. Click a region-owned tile. _Expected:_ whole region selected; left panel shows Influence/Stability/Development.
2. The right "DIRECTIVES" panel lists **Decisions** with cost/effect and a disabled reason when unaffordable/on cooldown.
3. Click an enabled decision. _Expected:_
   - Region stat(s) change (visible in the left panel immediately).
   - Resources change in the top bar if the decision has a cost.
   - If a stat map mode (Influence/Stability/Development) is active, the map recolors.
   - A line is added to the operations log.
4. _If decisions are missing:_ see §11 "Decisions do not appear".

## 7. How to test events

- Events fire on timers (Personal ~ frequent, Local, Global), paced by difficulty. Let the game run a
  little (don't pause), or use `WorldTest.unity` and wait.
- _Expected:_ an **event popup** appears (title, body, affected region/character, choices with
  cost/effect). The world pauses while it is open.
- Click a choice. _Expected:_ effects apply to the region/resources/character, the popup closes, the log
  records the choice, and the left panel updates if the affected region is selected.
- _If no popup appears:_ confirm you are not paused, that `events.json` exists, and that
  `Validate Gameplay Integration` passed. See §11.

## 8. How to test characters

1. Click around regions until you select one that lists **Characters** in the right panel (not every
   region has them — generation is 0–2 per region).
2. _Expected:_ each character shows name/title and Trust/Loyalty/Relationship, with interaction buttons
   (Negotiate, Bribe, Support, Recruit as Contact, Investigate). Buttons disable when unaffordable, on
   cooldown, or below the trust requirement (Recruit needs Trust ≥ 50).
3. Click an interaction. _Expected:_ character stats and (for most) the region's stats change, and the
   log records it.
4. Save then load (see §10). _Expected:_ characters remain attached to the same regions with their stats.

## 9. How to test pause / settings overlays

1. In Gameplay click **PAUSE** (top bar). _Expected:_ the Pause overlay appears and the game freezes
   (time stops; events stop scheduling).
2. Click **Resume**. _Expected:_ overlay closes and the game resumes.
3. Pause again, click **Settings**. _Expected:_ the Settings overlay appears **on top of** the pause
   menu (pause stays active). Adjust UI Scale / Volume / Show Tile Grid; click **Close**.
4. Pause, click **Save Game** — _expected:_ "Game saved." feedback. Click **Return to Main Menu** —
   _expected:_ returns to the Main Menu (time resumes).

## 10. How to test save/load

1. Start a new game, select a region, apply at least one decision, and use one character interaction.
2. Open Pause ▸ **Save Game** (saves to the `autosave` slot). _Expected:_ "Game saved."
3. Pause ▸ **Return to Main Menu**.
4. On the Main Menu click **Continue** (or **Load Game**). _Expected:_ Gameplay reloads and:
   - resources are restored,
   - the map (tiles + regions + ownership + unclaimed) is restored,
   - characters are restored and still attached to their regions,
   - decision cooldowns are restored,
   - the previously selected region is reselected (or selection cleared safely).
5. Old saves: if a pre-refactor save exists, the loader logs
   *"Legacy save detected … cannot be migrated safely"* and generates/keeps a valid world instead of
   crashing. This is expected.

## 11. Common problems and fixes

- **MainMenu does not load (Boot stays black).** Cause: Boot not index 0, or scenes missing from Build
  Settings. Fix: File ▸ Build Settings → ensure the §1 order; Boot at 0.
- **Gameplay opens directly / menu is skipped.** Cause: you pressed Play on `Gameplay.unity` instead of
  `Boot.unity`. Fix: open and play `Boot.unity`. (Playing `Gameplay` directly still works for testing but
  has no menu state.)
- **Buttons do nothing.** Cause: no `EventSystem` in the scene. Fix: the installers create one
  automatically; if you hand-built a scene, add a GameObject with `EventSystem` + `StandaloneInputModule`,
  or re-run `Rebuild Gameplay World Scene`.
- **UXML "missing references" / panels blank.** Cause: UXML/USS not imported. Fix: ensure
  `Assets/UI/UXML/*.uxml` and `Assets/UI/Styles/*.uss` exist and reimport (right-click ▸ Reimport). The
  controllers load them by path in the editor.
- **USS styles not applied (unstyled white text).** Cause: a `PanelSettings` with no theme, or USS not
  added. Fix: the controllers add the USS automatically in the editor; ensure the `.uss` files exist. If
  text is invisible, confirm the project's default runtime theme exists (open any menu scene once so
  Unity generates it).
- **Theme JSON missing.** Fix: run `Tools / Institute Game / Rebuild UI Theme Files`.
- **Hex map does not appear.** Cause: you skipped §3 step 3, so the old installer is still in
  `Gameplay.unity`; or no camera. Fix: run `Rebuild Gameplay World Scene`; the WorldController creates a
  camera if none exists.
- **Every hex becomes its own region again.** Cause: the old `HexMapGenerator`/`GameScreen` path is
  running. Fix: ensure `Gameplay.unity` uses `WorldGameplayInstaller` (re-run §3 step 3). Run
  `Validate Map Data` — it must report regions ≪ tiles.
- **Decisions do not appear.** Cause: `decisions.json` missing or no region selected. Fix: confirm
  `Assets/StreamingAssets/decisions.json` exists; select a region-owned tile (unclaimed tiles only show
  non-regional actions).
- **Events do not trigger.** Cause: game is paused, an event is already active, or `events.json` missing.
  Fix: resume; confirm `Assets/StreamingAssets/events.json` exists; wait (Local/Global have longer
  timers). Use `WorldTest.unity` for faster iteration.
- **Characters do not appear in the region panel.** Cause: that region rolled 0 characters, or characters
  weren't generated. Fix: select other regions; if none anywhere, confirm `RegionCharacterSystem` exists
  (Validate Gameplay Integration) and that a new game (not a broken load) was started.
- **Save/load fails.** Cause: no world in the scene, or write permissions. Fix: check the Console for the
  save path under `Application.persistentDataPath/Saves`; ensure a world is loaded before saving.
- **Old save cannot be loaded.** Expected — the new loader rejects pre-refactor saves safely and logs a
  clear message. Start a new game.
- **NullReferenceException after entering Gameplay.** Cause: a hand-edited scene missing the installer.
  Fix: re-run `Rebuild Gameplay World Scene`; check the Console for which object is null.

## 12. Minimum working test path

- [ ] Open `Boot.unity`, press Play.
- [ ] Main Menu appears → click **New Game**.
- [ ] Select **Normal** difficulty.
- [ ] Select **Small** map size.
- [ ] Click **Randomize Seed**.
- [ ] Click **Start Game** → Gameplay loads.
- [ ] Hex map appears (many small tiles).
- [ ] Click a region tile → left panel shows region stats.
- [ ] Apply one decision → stats/resources change.
- [ ] Wait for / trigger one event → choose an option.
- [ ] Click **Pause** → **Save Game** → **Return to Main Menu**.
- [ ] Click **Continue** → state is restored.

## 13. What is still manual (verify in the Unity Editor)

- **Run `Rebuild Gameplay World Scene`** once (the critical step) so the build flow uses the new system.
- Confirm scenes are in **Build Settings** in the §1 order (Boot at 0).
- If a menu prompts, optionally import **TextMeshPro Essentials** (only needed for legacy uGUI screens).
- Check the **Console** after entering Gameplay — it should be free of errors; the WorldController logs a
  map-validation report on generate.
- Confirm a **camera** exists in Gameplay (the WorldController creates one if absent; a Main Camera is
  fine).
- Confirm an **EventSystem** exists (installers create one).
- Check the **save path** printed in the Console (`Application.persistentDataPath/Saves`) if save/load
  behaves unexpectedly.
- Overlays use their own `PanelSettings`; if text doesn't render, open a menu scene once so Unity creates
  the default runtime UI Toolkit theme, then retry.

---

# Addendum: Runtime rendering/UI troubleshooting (presentation bugfix pass)

If you ran an earlier build and hit blank UI, no map, or camera/theme warnings, these are fixed in code.
For each, the cause and the exact fix/tool:

- **"Display 1 No cameras rendering"** — Cause: a scene had no Camera (UI Toolkit overlays don't need
  one to composite, but Unity still warns). Fix: cameras are now ensured at runtime
  (`UIToolkitThemeUtility.EnsureCamera`, called from every `EnsureDocument`; Boot ensures one too).
  Tool: `Tools / Institute Game / Repair Scene Cameras` adds a Camera to every scene asset.
- **"No Theme Style Sheet set to PanelSettings, UI will not render properly"** — Cause: runtime
  PanelSettings had no theme, so there was no default font → empty text. Fix: a Theme Style Sheet
  (`Assets/Resources/UI/InstituteRuntimeTheme.tss`) is loaded and assigned automatically. Tool:
  `Tools / Institute Game / Repair UI Toolkit Setup` creates the PanelSettings asset + theme.
- **"UI has boxes but no text"** — Same root cause as the theme warning. Once the theme is assigned,
  text renders. If it persists, run `Repair UI Toolkit Setup`, then re-enter Play Mode. Press **F9** in
  Play Mode to confirm `themeStyleSheet=True` for every UIDocument.
- **"Map validates but does not appear"** — Cause: the gameplay camera had culling mask 0 (adopted a UI
  camera) or no camera framed the map. Fix: `WorldController.EnsureCamera` sets culling mask to
  everything, enables the camera, and frames the map. Tool: `Generate And Frame Test Map` logs tile
  count + world bounds; press **F9** to see camera + tile counts.
- **"Region Borders MissingComponentException"** — Cause: a renderer was accessed before being added.
  Fix: `RegionOverlayRenderer.EnsureChild` now adds `MeshFilter`/`MeshRenderer` with explicit null
  checks and a guaranteed material before use. No action needed; just recompile.
- **"Event popup appears offscreen / flies upward"** — Cause: overlay centering relied on USS that
  didn't load. Fix: `OverlayUtil.ApplyOverlayLayout` forces a centered, dimmed, full-screen overlay
  inline for the pause/event/settings dialogs.
- **"NullReferenceException after entering Gameplay"** — Usually a hand-edited Gameplay scene missing the
  installer. Fix: run `Tools / Institute Game / Rebuild Gameplay World Scene`, then `Validate Gameplay
  Scene`.

Quickest full repair after pulling these changes: in Unity run
`Repair UI Toolkit Setup` → `Repair Scene Cameras` → `Rebuild Gameplay World Scene` →
`Validate Runtime Presentation`, then play `Boot.unity`.
