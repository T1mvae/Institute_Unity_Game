# Runtime UI / Render Bugfix Audit

Diagnosis of the Play-Mode presentation failures. Data generation is fine (validator passes:
1536 tiles, 22 regions, multi-tile, unclaimed land, no water-owned). The bugs are all in the
**presentation/setup layer**.

## Root causes

| # | Symptom | Root cause | File(s) |
|---|---------|-----------|---------|
| 1 | "No Theme Style Sheet set to PanelSettings, UI will not render properly" + **almost no text** | `GetOrCreatePanelSettings()` creates a `PanelSettings` at runtime and **never assigns `themeStyleSheet`**. Without a theme, UI Toolkit has no default font → Labels/Buttons render as empty boxes. | `Assets/Scripts/UI/Common/UIToolkitThemeUtility.cs` |
| 2 | "Display 1 No cameras rendering" | Menu/UI scenes (MainMenu, NewGameSetup, Loading) have **no Camera**. UI Toolkit overlay panels don't need a camera to composite, but Unity still warns + the screen isn't cleared. | UIToolkitThemeUtility (no camera ensure); menu scenes |
| 3 | Map validates but **doesn't appear** | `WorldController.EnsureCamera` never sets `cullingMask`/`enabled`. If it adopts a pre-existing UI camera (cullingMask 0), the world mesh isn't rendered. (Plus #2 left some scenes with no camera at all.) | `Assets/Scripts/World/WorldController.cs` |
| 4 | `MissingComponentException: no MeshRenderer on "Region Borders"` | `EnsureChild` is generally correct, but is not defensive: it relies on `?? AddComponent` (bypasses Unity's fake-null `==`) and can assign `sharedMaterial` when the material/shader failed to create. Made bulletproof. | `Assets/Scripts/World/Rendering/RegionOverlayRenderer.cs` |
| 5 | Event/Pause/Settings overlays "fly upward" / wrong place | Overlay centering depends on USS classes (`.overlay`, `.dialog`). `OverlayUtil.AddStyles` only loads USS `#if UNITY_EDITOR`, and in a build there is no fallback → no centering. Layout is now forced inline so it is correct regardless of USS. Also each overlay created its **own** `PanelSettings` (Part 8 asks for shared settings + sort order). | `Assets/Scripts/World/UI/OverlayUtil.cs`, overlay controllers |
| 6 | Multiple overlapping full-screen documents | Each overlay made a separate `PanelSettings`. Switched to **one shared `PanelSettings`** + per-`UIDocument.sortingOrder` (HUD 0, Settings 300, Pause 200, Event 250). | OverlayUtil |

## Fix plan

- **Theme:** add a real runtime Theme Style Sheet `Assets/Resources/UI/InstituteRuntimeTheme.tss`
  (`@import url("unity-theme://default");`), load it in `GetOrCreatePanelSettings()` and assign
  `themeStyleSheet`; also expose `LoadDefaultTheme()`. Editor tool `Repair UI Toolkit Setup` additionally
  creates the canonical `Assets/UI/PanelSettings/InstitutePanelSettings.asset` + `Assets/UI/Styles/InstituteTheme.tss`.
- **Cameras:** add `UIToolkitThemeUtility.EnsureCamera()` (solid-color clear, culling 0 for pure-UI
  scenes), called from `EnsureDocument()` so every UI scene gets one. `WorldController.EnsureCamera` now
  sets `cullingMask = everything`, `enabled = true`, depth, near/far — so the gameplay camera always
  renders the map.
- **Borders:** rewrite `EnsureChild` with explicit `== null` checks (Unity semantics) and a guaranteed
  non-null fallback material; split selection onto its own child; never touch a renderer that isn't there.
- **Overlays:** share the PanelSettings, set `UIDocument.sortingOrder`, and force centered full-screen
  layout inline (`ApplyOverlayLayout`) so popups are always centered with a dimmed backdrop.
- **Text/empty states:** theme fix restores text; add `UIQuery.Require<T>` (logs missing elements) and
  short empty-state messages ("No region selected", "No active event", etc.).
- **Diagnostics:** `RuntimeDiagnostics` (F9) reports scene/camera/UIDocument/PanelSettings/theme/tile
  counts. New editor tools: Validate Runtime Presentation, Repair UI Toolkit Setup, Repair Scene Cameras,
  Generate And Frame Test Map, Validate Gameplay Scene.

## Left untouched intentionally

- The Civ-like generator, `WorldMapData`, `RegionData`, save/load, and the gameplay systems (decisions/
  events/characters) — they work; this pass only fixes rendering/UI setup.
- Legacy systems remain isolated (see `LEGACY_SYSTEMS.md`).
