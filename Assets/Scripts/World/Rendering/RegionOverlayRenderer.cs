using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Draws region borders (static) and the current selection highlight (dynamic) as two
    /// thin transparent meshes layered just in front of the fill mesh.
    /// Border types: region-to-region / region-to-unclaimed share <see cref="MapPalette.RegionBorder"/>;
    /// land-to-water uses <see cref="MapPalette.CoastBorder"/>; the selected region gets a thicker
    /// <see cref="MapPalette.SelectedBorder"/> outline plus a translucent fill.
    /// </summary>
    public class RegionOverlayRenderer : MonoBehaviour
    {
        WorldMapData _map;
        float _hexSize = 1f;

        GameObject _borderObject;
        GameObject _selectionObject;
        Mesh _borderMesh;
        Mesh _selectionMesh;
        Material _material;

        readonly MeshAccumulator _borders = new MeshAccumulator();
        readonly MeshAccumulator _selection = new MeshAccumulator();
        readonly Vector3[] _corners = new Vector3[6];

        const float BorderZ = -0.02f;
        const float SelectionFillZ = -0.03f;
        const float SelectionBorderZ = -0.05f;

        void EnsureSetup()
        {
            EnsureMaterial();
            _borderObject = EnsureChild("Region Borders", ref _borderMesh);
            _selectionObject = EnsureChild("Selection Highlight", ref _selectionMesh);
        }

        void EnsureMaterial()
        {
            if (_material != null) return;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                Debug.LogWarning("RegionOverlayRenderer: no usable shader found; borders may not render.");
                return;
            }
            _material = new Material(shader) { name = "HexOverlayMaterial" };
        }

        // Bulletproof: uses Unity's overloaded '==' (not '??', which bypasses Unity's fake-null check)
        // so a missing MeshRenderer/MeshFilter is always added before it is used. Never accesses a
        // renderer that isn't attached -> no MissingComponentException.
        GameObject EnsureChild(string name, ref Mesh mesh)
        {
            Transform existing = transform.Find(name);
            GameObject go = existing != null ? existing.gameObject : new GameObject(name);
            if (go.transform.parent != transform) go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();

            EnsureMaterial();
            if (mr != null && _material != null)
            {
                mr.sharedMaterial = _material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            }

            if (mesh == null) mesh = new Mesh { name = name };
            if (mf != null) mf.sharedMesh = mesh;
            return go;
        }

        public void Build(WorldMapData map, float hexSize)
        {
            _map = map;
            _hexSize = hexSize;
            EnsureSetup();
            RebuildBorders();
            ClearSelection();
        }

        // Borders are rebuilt ONCE per world build (here), never per-frame. Selection highlight is
        // rebuilt only when the selection changes (ShowRegion/ShowTile/ClearSelection). Each shared
        // region-region edge is emitted exactly once (lower tileId owns it) to avoid double geometry.
        // TODO (future, optional): replace these per-edge line quads with a screen-space / SDF shader
        // outline for crisper borders at extreme zoom. Kept as meshes for now — simple and safe.
        void RebuildBorders()
        {
            _borders.Clear();
            float w = MapPalette.Data.borderWidth * _hexSize;
            Color regionBorder = MapPalette.RegionBorder;
            Color coastBorder = MapPalette.CoastBorder;

            foreach (var tile in _map.Tiles)
            {
                Vector3 center = tile.coord.ToWorld(_hexSize);
                HexMetrics.FillCorners(center, _hexSize, _corners);
                bool tileIsLand = !tile.IsWaterTerrain;

                for (int e = 0; e < 6; e++)
                {
                    Vector3 cA = _corners[e];
                    Vector3 cB = _corners[(e + 1) % 6];
                    HexTileData neighbor = NeighborAcrossEdge(center, cA, cB);

                    bool drawCoast = tileIsLand && (neighbor == null || neighbor.IsWaterTerrain);
                    if (drawCoast)
                    {
                        _borders.AddLine(cA, cB, w, coastBorder, BorderZ);
                        continue;
                    }

                    // Region boundary: this tile is in a region and the far side is a different
                    // region or unclaimed land. Emit once per shared edge (lower id owns it).
                    if (!tile.HasRegion) continue;
                    string otherRegion = neighbor != null ? neighbor.regionId : null;
                    if (otherRegion == tile.regionId) continue;
                    if (neighbor != null && neighbor.tileId < tile.tileId &&
                        !string.IsNullOrEmpty(otherRegion)) continue; // neighbor will draw it
                    _borders.AddLine(cA, cB, w, regionBorder, BorderZ);
                }
            }

            _borders.ApplyTo(_borderMesh);
        }

        public void ShowRegion(RegionData region)
        {
            EnsureSetup();
            _selection.Clear();
            if (region == null) { _selection.ApplyTo(_selectionMesh); return; }

            Color fill = MapPalette.Selection;
            fill.a = 0.22f;
            Color border = MapPalette.SelectedBorder;
            float w = MapPalette.Data.selectedBorderWidth * _hexSize;

            foreach (int tid in region.tileIds)
            {
                HexTileData tile = _map.GetTile(tid);
                if (tile == null) continue;
                Vector3 center = tile.coord.ToWorld(_hexSize);
                _selection.AddHexFill(center, _hexSize * 0.96f, fill, SelectionFillZ);

                HexMetrics.FillCorners(center, _hexSize, _corners);
                for (int e = 0; e < 6; e++)
                {
                    Vector3 cA = _corners[e];
                    Vector3 cB = _corners[(e + 1) % 6];
                    HexTileData neighbor = NeighborAcrossEdge(center, cA, cB);
                    string otherRegion = neighbor != null ? neighbor.regionId : null;
                    if (otherRegion != region.regionId)
                        _selection.AddLine(cA, cB, w, border, SelectionBorderZ);
                }
            }
            _selection.ApplyTo(_selectionMesh);
        }

        public void ShowTile(HexTileData tile)
        {
            EnsureSetup();
            _selection.Clear();
            if (tile == null) { _selection.ApplyTo(_selectionMesh); return; }

            Color fill = MapPalette.Selection;
            fill.a = 0.28f;
            Color border = MapPalette.SelectedBorder;
            float w = MapPalette.Data.selectedBorderWidth * _hexSize;

            Vector3 center = tile.coord.ToWorld(_hexSize);
            _selection.AddHexFill(center, _hexSize * 0.96f, fill, SelectionFillZ);
            HexMetrics.FillCorners(center, _hexSize, _corners);
            for (int e = 0; e < 6; e++)
                _selection.AddLine(_corners[e], _corners[(e + 1) % 6], w, border, SelectionBorderZ);
            _selection.ApplyTo(_selectionMesh);
        }

        public void ClearSelection()
        {
            EnsureSetup();
            _selection.Clear();
            _selection.ApplyTo(_selectionMesh);
        }

        HexTileData NeighborAcrossEdge(Vector3 center, Vector3 cornerA, Vector3 cornerB)
        {
            Vector3 nc = HexMetrics.NeighborCenterAcrossEdge(center, cornerA, cornerB);
            HexCoord coord = HexCoord.FromWorld(nc, _hexSize);
            return _map.GetTile(coord);
        }
    }
}
