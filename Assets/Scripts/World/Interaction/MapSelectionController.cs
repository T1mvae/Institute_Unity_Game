using System;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Turns mouse input into hover/click on hex tiles by raycasting the map's MeshCollider.
    /// Raises raw tile events; the WorldController decides whether a click selects the whole
    /// owning region or just the (unclaimed) tile.
    /// </summary>
    public class MapSelectionController : MonoBehaviour
    {
        Camera _camera;
        WorldMapData _map;
        Transform _mapTransform;
        float _hexSize = 1f;
        bool _ready;

        HexTileData _hovered;

        public event Action<HexTileData> TileHovered;
        public event Action<HexTileData> TileClicked;

        public HexTileData Hovered => _hovered;

        public void Initialize(Camera camera, WorldMapData map, Transform mapTransform, float hexSize)
        {
            _camera = camera != null ? camera : Camera.main;
            _map = map;
            _mapTransform = mapTransform != null ? mapTransform : transform;
            _hexSize = hexSize;
            _ready = _camera != null && _map != null;
        }

        void Update()
        {
            if (!_ready) return;

            if (MapInteractionGate.PointerOverUI)
            {
                if (_hovered != null) { _hovered = null; TileHovered?.Invoke(null); }
                return;
            }

            HexTileData tile = PickTile();

            if (tile != _hovered)
            {
                _hovered = tile;
                TileHovered?.Invoke(tile);
            }

            if (Input.GetMouseButtonDown(0) && tile != null)
                TileClicked?.Invoke(tile);
        }

        HexTileData PickTile()
        {
            if (_camera == null) return null;
            Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, 5000f))
            {
                Vector3 local = _mapTransform.InverseTransformPoint(hit.point);
                HexCoord coord = HexCoord.FromWorld(local, _hexSize);
                return _map.GetTile(coord);
            }

            // Fallback for orthographic top-down: intersect the z=0 plane directly.
            if (_camera.orthographic)
            {
                Plane plane = new Plane(_mapTransform.forward, _mapTransform.position);
                if (plane.Raycast(ray, out float enter))
                {
                    Vector3 world = ray.GetPoint(enter);
                    Vector3 local = _mapTransform.InverseTransformPoint(world);
                    HexCoord coord = HexCoord.FromWorld(local, _hexSize);
                    return _map.GetTile(coord);
                }
            }
            return null;
        }
    }
}
