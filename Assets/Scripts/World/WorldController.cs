using System;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Runtime owner of the world map. Generates (or loads) a <see cref="WorldMapData"/>, builds
    /// the renderer + overlay + camera + picking, and translates tile clicks into region/tile
    /// selection. This is the single integration point the UI talks to.
    ///
    /// Drop this on one GameObject and it self-bootstraps a complete, interactive Civ-like map.
    /// </summary>
    public class WorldController : MonoBehaviour
    {
        public static WorldController Instance { get; private set; }

        [Header("Optional explicit references (auto-created if null)")]
        [SerializeField] Camera worldCamera;
        [SerializeField] bool generateOnAwake = true;
        [SerializeField] bool runValidationOnGenerate = true;

        public WorldMapData Map { get; private set; }
        public MapMode CurrentMode { get; private set; } = MapMode.Terrain;
        public RegionData SelectedRegion { get; private set; }
        public HexTileData SelectedTile { get; private set; }

        public event Action<WorldMapData> WorldBuilt;          // == OnWorldMapChanged
        public event Action<RegionData> RegionSelected;        // == OnSelectedRegionChanged
        public event Action<HexTileData> TileSelected;         // == OnSelectedTileChanged (unclaimed tile)
        public event Action SelectionCleared;
        public event Action<HexTileData> TileHovered;
        public event Action<MapMode> MapModeChanged;           // == OnMapModeChanged
        /// <summary>Raised when a region's political data changed (decision/event/character effect).</summary>
        public event Action<RegionData> RegionDataChanged;     // == OnRegionDataChanged

        MapRenderManager _renderer;
        RegionOverlayRenderer _overlay;
        MapSelectionController _selection;
        WorldCameraController _cameraController;
        Transform _mapRoot;

        string _activeSlot = "autosave";

        void Awake()
        {
            Instance = this;
            MapDefinitions.Reload();
            MapPalette.Reload();
            if (generateOnAwake)
                BootstrapFromSession();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Decides whether to load a saved world or generate a fresh one, then builds it.</summary>
        public void BootstrapFromSession()
        {
            bool wantsLoad = false;
            string slot = "autosave";
            try
            {
                wantsLoad = GameSession.LoadRequested;
                slot = GameSession.RequestedLoadSlot;
            }
            catch { /* GameSession optional in standalone test scenes */ }

            _activeSlot = string.IsNullOrEmpty(slot) ? "autosave" : slot;

            if (wantsLoad && WorldSaveBridge.HasWorld(_activeSlot))
            {
                WorldMapData loaded = WorldSaveBridge.Load(_activeSlot);
                if (loaded != null)
                {
                    BuildWorld(loaded);
                    return;
                }
                Debug.LogWarning("WorldController: world load failed; generating a new world instead.");
            }

            GenerateNewWorld(ResolveSettings());
        }

        MapGenerationSettings ResolveSettings()
        {
            MapGenerationSettings settings = WorldSetup.ResolveOrDefault();
            try
            {
                DifficultyConfig diff = GameSession.ActiveDifficulty;
                if (diff != null)
                {
                    settings.difficultyId = diff.preset.ToString();
                    if (WorldSetup.PendingSettings == null && diff.randomSeed != 0)
                        settings.seed = diff.randomSeed;
                }
            }
            catch { /* standalone */ }
            return settings;
        }

        public void GenerateNewWorld(MapGenerationSettings settings)
        {
            var generator = new WorldMapGenerator();
            WorldMapData map = generator.Generate(settings);

            if (runValidationOnGenerate)
            {
                ValidationResult v = WorldMapValidator.Validate(map);
                if (!v.IsValid) Debug.LogError(v.ToReport());
                else Debug.Log(v.ToReport());
            }

            BuildWorld(map);
        }

        public void BuildWorld(WorldMapData map)
        {
            Map = map;
            EnsureRenderObjects();
            EnsureCamera();

            float hexSize = MapPalette.HexSize;
            _renderer.Build(map, CurrentMode);
            _overlay.Build(map, hexSize);

            _selection.Initialize(worldCamera, map, _mapRoot, hexSize);
            _cameraController.Initialize(worldCamera, _renderer.WorldBounds);

            ClearSelection();
            WorldBuilt?.Invoke(map);
        }

        void EnsureRenderObjects()
        {
            if (_mapRoot == null)
            {
                Transform existing = transform.Find("Hex Map");
                GameObject go = existing != null ? existing.gameObject : new GameObject("Hex Map");
                go.transform.SetParent(transform, false);
                _mapRoot = go.transform;
            }
            GameObject mapGo = _mapRoot.gameObject;
            // Explicit Unity-'==' null checks (not '??') so a fake-null component is never used.
            _renderer = mapGo.GetComponent<MapRenderManager>();
            if (_renderer == null) _renderer = mapGo.AddComponent<MapRenderManager>();
            _overlay = mapGo.GetComponent<RegionOverlayRenderer>();
            if (_overlay == null) _overlay = mapGo.AddComponent<RegionOverlayRenderer>();
            _selection = mapGo.GetComponent<MapSelectionController>();
            if (_selection == null) _selection = mapGo.AddComponent<MapSelectionController>();

            _selection.TileHovered -= OnTileHovered;
            _selection.TileClicked -= OnTileClicked;
            _selection.TileHovered += OnTileHovered;
            _selection.TileClicked += OnTileClicked;
        }

        void EnsureCamera()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) worldCamera = FindFirstObjectByType<Camera>();
            if (worldCamera == null)
            {
                var camGo = new GameObject("World Camera");
                worldCamera = camGo.AddComponent<Camera>();
                if (Camera.main == null) camGo.tag = "MainCamera";
            }
            // Configure the camera to actually render the world mesh. Setting cullingMask = everything
            // matters when we adopt a pre-existing "UI Camera" (created with cullingMask 0).
            worldCamera.enabled = true;
            worldCamera.orthographic = true;
            worldCamera.clearFlags = CameraClearFlags.SolidColor;
            worldCamera.backgroundColor = MapPalette.DeepSea;
            worldCamera.cullingMask = ~0;            // render all layers (the map has no special layer)
            worldCamera.nearClipPlane = 0.1f;
            worldCamera.farClipPlane = 100f;
            worldCamera.depth = 0f;
            worldCamera.transform.rotation = Quaternion.identity;

            WorldCameraController controller = worldCamera.GetComponent<WorldCameraController>();
            if (controller == null) controller = worldCamera.gameObject.AddComponent<WorldCameraController>();
            _cameraController = controller;
        }

        // ---------- selection ----------
        void OnTileHovered(HexTileData tile) => TileHovered?.Invoke(tile);

        void OnTileClicked(HexTileData tile)
        {
            if (tile == null) { ClearSelection(); return; }
            if (tile.HasRegion)
                SelectRegion(tile.regionId);
            else
                SelectTile(tile.tileId);
        }

        public void SelectRegion(string regionId)
        {
            RegionData region = Map != null ? Map.GetRegion(regionId) : null;
            if (region == null) { ClearSelection(); return; }
            SelectedRegion = region;
            SelectedTile = null;
            _overlay.ShowRegion(region);
            RegionSelected?.Invoke(region);
        }

        public void SelectTile(int tileId)
        {
            HexTileData tile = Map != null ? Map.GetTile(tileId) : null;
            if (tile == null) { ClearSelection(); return; }
            SelectedTile = tile;
            SelectedRegion = null;
            _overlay.ShowTile(tile);
            TileSelected?.Invoke(tile);
        }

        public void ClearSelection()
        {
            SelectedRegion = null;
            SelectedTile = null;
            if (_overlay != null) _overlay.ClearSelection();
            SelectionCleared?.Invoke();
        }

        // ---------- map modes ----------
        public void SetMapMode(MapMode mode)
        {
            CurrentMode = mode;
            if (_renderer != null) _renderer.SetMapMode(mode);
            MapModeChanged?.Invoke(mode);
        }

        /// <summary>
        /// Notify listeners that a region's stats changed (called by decision/event/character
        /// systems). Recolors the map when a stat-based map mode is active so the effect shows.
        /// </summary>
        public void RaiseRegionDataChanged(RegionData region)
        {
            if (region == null) return;
            region.ClampStats();
            if (_renderer != null && CurrentMode != MapMode.Terrain && CurrentMode != MapMode.Political)
                _renderer.SetMapMode(CurrentMode);
            RegionDataChanged?.Invoke(region);
        }

        // ---------- persistence ----------
        public void SaveWorld(string slot = null)
        {
            if (Map == null) return;
            WorldSaveBridge.Save(string.IsNullOrEmpty(slot) ? _activeSlot : slot, Map);
        }

        public bool LoadWorld(string slot)
        {
            WorldMapData map = WorldSaveBridge.Load(slot);
            if (map == null) return false;
            _activeSlot = slot;
            BuildWorld(map);
            return true;
        }
    }
}
