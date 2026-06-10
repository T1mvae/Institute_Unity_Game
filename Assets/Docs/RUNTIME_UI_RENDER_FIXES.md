# Runtime UI / Render Fixes

What was broken in Play Mode and exactly how it was fixed. The generator/data layer was untouched
(validation already passed). See `RUNTIME_UI_RENDER_BUGFIX_AUDIT.md` for the diagnosis.

## Camera fixes ("Display 1 No cameras rendering")

- Added `UIToolkitThemeUtility.EnsureCamera()` — creates a clear-only orthographic camera (culling
  mask 0) if a scene has none. It is now called from `EnsureDocument()`, so **every UI scene**
  (MainMenu, NewGameSetup, Loading) gets a camera. `GameBootstrapper` also calls it so **Boot** has one.
- `WorldController.EnsureCamera()` now sets `enabled = true`, `cullingMask = ~0` (everything),
  `clearFlags = SolidColor`, near/far clip and depth — so the **Gameplay** camera always renders the
  world mesh, even if it adopts a pre-existing "UI Camera" that had culling mask 0.
- Editor tool **`Tools / Institute Game / Repair Scene Cameras`** adds/fixes a Main Camera in each
  visual scene and saves them.

Result: no scene is left without an active camera.

## PanelSettings + Theme Style Sheet fixes (the missing-text root cause)

- Root cause: the runtime `PanelSettings` was created with **no `themeStyleSheet`**, so UI Toolkit had
  no default font and rendered Labels/Buttons as empty boxes (and warned every frame).
- Added a real Theme Style Sheet:
  - `Assets/Resources/UI/InstituteRuntimeTheme.tss` — **runtime-loadable** (Resources), imports
    `unity-theme://default`.
  - `Assets/UI/Styles/InstituteTheme.tss` — editor copy backing the PanelSettings asset.
- `UIToolkitThemeUtility.GetOrCreatePanelSettings()` now loads the theme (`LoadDefaultTheme()`) and
  assigns `themeStyleSheet`; it logs **one** warning only if loading fails.
- **One shared PanelSettings** is now used for the HUD and all overlays; overlays layer via
  `UIDocument.sortingOrder` (HUD 0, Pause 200, Event 250, Settings 300) instead of creating separate
  un-theme'd PanelSettings.
- Editor tool **`Tools / Institute Game / Repair UI Toolkit Setup`** creates
  `Assets/UI/PanelSettings/InstitutePanelSettings.asset` with the theme assigned.

Result: the "No Theme Style Sheet set to PanelSettings" warning is gone and text renders.

## Text visibility fixes

- The theme fix restores text everywhere. Added `Institute.World.UI.UIQuery.Require<T>` (logs a clear
  error for any missing named element) and empty-state messages ("No region selected", "Select a region
  to view decisions and characters", "No characters in this region", "Unclaimed — No organized region").
- Colors/contrast come from `theme.json` + USS (no hard-coded colors in controllers).

## Map rendering fixes

- The map data was always correct; it wasn't visible because of the camera (culling mask / missing
  camera). With `WorldController.EnsureCamera` setting `cullingMask = ~0` and framing via
  `WorldCameraController`, the single vertex-colored mesh (`MapRenderManager`, Sprites/Default, double-
  sided) renders. Terrain/Political/stat map modes recolor by rewriting vertex colors.
- Debug: `RuntimeDiagnostics` (press **F9** in Play Mode) reports tile count, region count, map bounds,
  camera list, and renderer status. Editor tool **`Generate And Frame Test Map`** logs tile count +
  world bounds and builds `WorldTest.unity`.

## Region border fix (MissingComponentException)

- `RegionOverlayRenderer.EnsureChild` rewritten to use explicit Unity `== null` checks (not `??`, which
  bypasses Unity's fake-null) and a **guaranteed non-null fallback material** (Sprites/Default →
  Unlit/Color → Hidden/Internal-Colored). It never assigns `sharedMaterial` on a renderer that isn't
  attached, and separates "Region Borders" and "Selection Highlight" onto their own child objects, each
  with `MeshFilter` + `MeshRenderer`.

Result: no `MissingComponentException`; borders + selection render.

## Overlay positioning fix (popups "flew upward")

- Overlays depended on USS classes (`.overlay`/`.dialog`) that only loaded in the editor. Added
  `OverlayUtil.ApplyOverlayLayout(root, name)` which forces a **full-screen, dimmed, centered** layout
  inline (position absolute, inset 0, justify/align center) and gives the dialog a readable fallback
  background — so the event/pause/settings dialogs are always centered regardless of USS.
- Visibility is controlled by `display: none/flex` on the panel root; sort order keeps overlays above
  the HUD.

## How to run the validation tools

- `Tools / Institute Game / Validate Runtime Presentation` — assets + (in Play Mode) live camera/
  PanelSettings/theme checks.
- `Tools / Institute Game / Repair UI Toolkit Setup` — create/assign PanelSettings + theme.
- `Tools / Institute Game / Repair Scene Cameras` — add/fix cameras in all scenes.
- `Tools / Institute Game / Generate And Frame Test Map` — generate + log bounds + build WorldTest.
- `Tools / Institute Game / Validate Gameplay Scene` — checks the Gameplay scene for the installer.
- In Play Mode press **F9** for the on-screen diagnostics panel.

## Remaining limitations

- The `unity-theme://default` import is Unity's standard default runtime theme. If a future Unity
  version changes this, run `Repair UI Toolkit Setup` (it regenerates the theme + PanelSettings asset).
- UI scale is applied via the shared PanelSettings reference resolution (global, not per-panel).
- Borders are still per-edge meshes (shader outline remains a future TODO).
