using System.Collections.Generic;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Renders the whole hex grid as a single vertex-colored mesh (one draw call) on the XY
    /// plane. Recoloring on a map-mode change only rewrites vertex colors — it does not rebuild
    /// geometry. A MeshCollider on the same mesh provides cheap click/hover picking.
    ///
    /// This keeps per-tile data and visuals separate: it reads <see cref="WorldMapData"/> but
    /// owns no gameplay state, and scales to thousands of tiles without per-tile GameObjects.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class MapRenderManager : MonoBehaviour
    {
        const int VertsPerTile = 7; // center + 6 corners

        MeshFilter _meshFilter;
        MeshRenderer _meshRenderer;
        MeshCollider _collider;
        Mesh _mesh;
        Material _material;

        WorldMapData _map;
        MapMode _mode = MapMode.Terrain;
        readonly List<HexTileData> _tileOrder = new List<HexTileData>();
        Color[] _colors;
        readonly Vector3[] _cornerBuffer = new Vector3[6];

        public Bounds WorldBounds { get; private set; }
        public float HexSize { get; private set; } = 1f;

        void Awake()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            EnsureMaterial();
        }

        void EnsureMaterial()
        {
            if (_material != null) return;
            // "Sprites/Default" is always present in the Built-in pipeline and multiplies a
            // (white) texture by vertex color + tint, so flat vertex colors render unlit.
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            _material = new Material(shader) { name = "HexMapMaterial" };
            var white = new Texture2D(2, 2);
            Color[] px = { Color.white, Color.white, Color.white, Color.white };
            white.SetPixels(px);
            white.Apply();
            if (_material.HasProperty("_MainTex")) _material.mainTexture = white;
            if (_meshRenderer != null) _meshRenderer.sharedMaterial = _material;
        }

        public void Build(WorldMapData map, MapMode mode)
        {
            _map = map;
            _mode = mode;
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            EnsureMaterial();

            HexSize = MapPalette.HexSize;

            _tileOrder.Clear();
            foreach (var tile in map.Tiles) _tileOrder.Add(tile);

            int tileCount = _tileOrder.Count;
            var vertices = new Vector3[tileCount * VertsPerTile];
            _colors = new Color[tileCount * VertsPerTile];
            var triangles = new int[tileCount * 6 * 3];

            int vi = 0, ti = 0;
            Vector3 min = Vector3.positiveInfinity, max = Vector3.negativeInfinity;

            for (int t = 0; t < tileCount; t++)
            {
                HexTileData tile = _tileOrder[t];
                Vector3 center = tile.coord.ToWorld(HexSize);
                HexMetrics.FillCorners(center, HexSize, _cornerBuffer);

                int baseIndex = vi;
                vertices[vi++] = center;
                for (int c = 0; c < 6; c++)
                {
                    vertices[vi++] = _cornerBuffer[c];
                    min = Vector3.Min(min, _cornerBuffer[c]);
                    max = Vector3.Max(max, _cornerBuffer[c]);
                }

                for (int c = 0; c < 6; c++)
                {
                    triangles[ti++] = baseIndex;
                    triangles[ti++] = baseIndex + 1 + c;
                    triangles[ti++] = baseIndex + 1 + (c + 1) % 6;
                }
            }

            if (_mesh == null)
            {
                _mesh = new Mesh { name = "HexMap" };
                _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.triangles = triangles;
            ApplyColors();
            _mesh.RecalculateBounds();
            _meshFilter.sharedMesh = _mesh;

            _collider = GetComponent<MeshCollider>();
            if (_collider == null) _collider = gameObject.AddComponent<MeshCollider>();
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;

            WorldBounds = new Bounds((min + max) * 0.5f, max - min);
        }

        public void SetMapMode(MapMode mode)
        {
            if (_map == null) return;
            _mode = mode;
            ApplyColors();
        }

        void ApplyColors()
        {
            if (_map == null || _colors == null) return;
            for (int t = 0; t < _tileOrder.Count; t++)
            {
                Color c = MapColors.ForTile(_map, _tileOrder[t], _mode);
                int b = t * VertsPerTile;
                for (int k = 0; k < VertsPerTile; k++) _colors[b + k] = c;
            }
            if (_mesh != null) _mesh.colors = _colors;
        }
    }
}
