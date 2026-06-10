# Map Generation (corrected Civ-like model)

All world code lives under namespace `Institute.World` in `Assets/Scripts/World/`. It is
fully decoupled from the legacy `Region`/`HexMapGenerator` types (which remain only as legacy).

## Hex tile vs region — the core distinction

| Concept | Type | Owns | Stats |
|--------|------|------|-------|
| **Hex tile** | `HexTileData` | one small cell | terrain, biome, elevation, moisture, walkability, features, danger — **no political stats** |
| **Region** | `RegionData` | many adjacent tiles | **Influence / Stability / Development**, population, wealth, modifiers, characters |
| **World** | `WorldMapData` | all tiles + all regions + unclaimed list | seed, size, generation settings |

Rules enforced by the generator and `WorldMapValidator`:

- A tile **may** belong to one region (`HexTileData.regionId`) or to **none** (empty == unclaimed).
- `RegionCount` is **much smaller** than `TileCount` (a Medium 48×32 map ≈ 1536 tiles, ~22 regions).
- Every region owns **2+ tiles** (single-tile regions are dissolved back to unclaimed).
- Sea / deep sea / mountains / ruins / wasteland / blocked tiles are **never** part of a land region.
- Clicking a region tile selects the **whole region**; clicking an unclaimed tile shows **terrain/wilderness** info.

This replaces the old, wrong model where `Region` *was* the hex tile and `regionCount == hexCount`.

## Generation pipeline (`WorldMapGenerator.Generate`)

Deterministic for a given `MapGenerationSettings.seed`. RNG-consuming loops iterate in a stable
(sorted) order so the same seed always yields the same world.

1. **Hex grid** — offset rows converted to axial `(q, r)`; `width × height` tiles created with sequential ids.
2. **Terrain** — multi-octave Perlin elevation + moisture, with a square radial falloff so map edges
   trend to sea (continents/islands). Sea level is the `seaFraction`-th percentile of elevation.
   Elevation → Mountains/Hills; moisture → Forest/Swamp/Desert/Plains; land-touching-water → Coast;
   then ruins/wasteland/sacred land are scattered. Biome + walkability + movement cost + danger +
   developmentPotential are derived per tile.
3. **Region seeds** — valid land tiles (`canBeRegionSeed`) chosen, spaced apart by a distance that
   scales with map size / region count. Count ≈ `targetRegionCount` (relaxed if spacing is too strict).
4. **Region growth** — weighted multi-source BFS (Dijkstra-like, `MinHeap`). Cheap terrain (Plains)
   is claimed before expensive terrain (Forest/Hills); water/mountains/ruins/wasteland block growth.
   Each region has a size budget (varied by `RegionTypeDefinition.sizePreference` and RNG) and a global
   cap of `(1 − unclaimedLandFraction)` of claimable land, so **some land stays unclaimed** and borders
   are irregular (per-edge cost jitter).
5. **Region stats** — Influence/Stability/Development per region from: region-type base + variance,
   terrain composition (avg tile `developmentPotential`), region size, capital terrain bonus, and a
   difficulty bias (Easy/Normal/Hard). Population & wealth derived. **Only regions get these stats.**
6. **Borders + neighbors** — a tile is a border tile if any neighbor is a different region / unclaimed /
   water / off-map. Region neighbor ids are computed from cross-region tile adjacency (symmetric).

## Terrain system (data-driven)

`Assets/Data/Map/terrain_definitions.json` → `TerrainDefinition` per `TerrainType`:
`colorHex`, `regionAllowed`, `canBeRegionSeed`, `isWater`, `isWalkable`, `regionGrowthCost`,
`movementCost`, `tags`. Loaded by `MapDefinitions` (editor path → StreamingAssets → baked defaults).

`Assets/Data/Map/region_type_definitions.json` → `RegionTypeDefinition` per `RegionType`:
`preferredTerrains` (seed bias), stat bases + variance, `sizePreference`, `tags`.

`Assets/Data/Map/map_presets.json` → `MapPresetEntry` (DebugTiny / Small / Medium / Large) via `MapPresets`.

Editing any of these JSON files changes generation without touching code. If a file is missing or
corrupt, the baked `CreateDefault()` collections are used (no crash).

## Unclaimed tiles

`WorldMapData.unclaimedTileIds` lists land tiles deliberately left without a region. Their share is
driven by `MapGenerationSettings.unclaimedLandFraction` (New Game Setup → "Unclaimed Land %"). In the
UI they read as neutral/wilderness; in stat map modes they render desaturated/grey.

## Rendering & interaction

- `MapRenderManager` builds **one** vertex-colored mesh for the whole grid (one draw call) + a
  `MeshCollider` for picking. Map-mode changes only rewrite vertex colors.
- `RegionOverlayRenderer` draws region borders (region/coast) and the selection highlight as thin
  transparent meshes layered just in front of the fill.
- `MapSelectionController` raycasts the collider, converts the world hit to a `HexCoord`
  (`HexCoord.FromWorld`) and raises hover/click events. `MapInteractionGate` suppresses picking while
  the pointer is over a UI panel.
- `WorldController` owns it all: generates/loads, builds visuals + camera, and turns a tile click into
  region selection (if owned) or tile selection (if unclaimed).

Map modes: Terrain, Political, Influence, Stability, Development, Danger (Characters reserved). In stat
modes owned tiles inherit their region's color from a low→mid→high gradient; unclaimed = grey; water/
blocked have dedicated colors. Colors come from `terrain_definitions.json` + the `map` section of
`Assets/Data/UI/theme.json` (`MapPalette`).

## Save format

The world is saved **separately** from the legacy `SaveGameData`, as `<slot>.world.json` next to the
save folder (`WorldSaveBridge`). Schema (`WorldMapSave`, `schemaVersion = "2"`):

- seed, width, height, full `MapGenerationSettings`
- `tiles[]`: tileId, q, r, terrainType, biomeType, elevation, moisture, walkable, **regionId** (""=unclaimed), movementCost, features, danger, developmentPotential
- `regions[]`: regionId, displayName, regionType, tileIds, capitalTileId, borderTileIds, neighborRegionIds, influence/stability/development, population, wealth, modifiers, characterIds, tags
- `unclaimedTileIds[]`

**Old-format handling:** `WorldMapSerializer.FromJson` rejects payloads with no tile layer or a schema
below `2` (the old "one hex = one region" model) with a clear error — it never crashes. On a failed
load, `WorldController` logs a warning and generates a fresh world.

## Debug validation & testing

- **Editor:** `Tools / Institute Game / Validate Map Data` generates a Medium world and runs
  `WorldMapValidator` (tile count == w×h, regions << tiles, every region multi-tile, unclaimed land
  exists, no water-in-region, ownership integrity, neighbor symmetry, valid capitals).
- **Editor:** `Tools / Institute Game / Validate Save Data` validates every `*.world.json` and flags
  legacy saves.
- **Runtime:** `WorldController` runs the validator on every generate and logs the report.
- **Play it:** `Tools / Institute Game / Generate Test Hex World` creates `Assets/Scenes/WorldTest.unity`
  — press Play to explore. Or `Rebuild Gameplay World Scene` to put the new system into `Gameplay.unity`.

---

## Politics now live on RegionData (integration pass)

`RegionData` is the **single source of truth for political stats** (Influence / Stability / Development,
population, wealth, modifiers, characters). `HexTileData` never carries political stats. All gameplay
systems target `RegionData` via `WorldController.SelectedRegion`:

- **Decisions** — `Institute.World.Gameplay.RegionDecisionSystem` reads/modifies `RegionData` and
  spends player resources; cooldowns are per (decision, region).
- **Events** — `Institute.World.Gameplay.RegionEventSystem` targets a `RegionData` (Local), all regions
  (Global), or a character + its region (Personal); effects modify `RegionData` stats + resources +
  character relations.
- **Characters** — `Institute.World.Gameplay.RegionCharacterSystem` attaches `GameCharacter`s to regions
  by `regionId` (via `WorldCharacterBridge`); interactions modify `RegionData` stats.
- **Save/load** — `GameSaveService` persists `RegionData` (in `<slot>.world.json`) + characters +
  decision cooldowns (`<slot>.gameplay.json`). Old one-hex-one-region saves are rejected safely.

When a region's stats change, systems call `WorldController.RaiseRegionDataChanged(region)`, which
refreshes the dossier and recolors the map in stat map-modes. See `INTEGRATION_COMPLETED.md`.
