using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Loads and caches the data-driven terrain and region-type definitions.
    /// Resolution order: project JSON (editor) -> StreamingAssets (build) -> baked defaults.
    /// Never throws: a missing/corrupt file falls back to <c>CreateDefault()</c>.
    /// </summary>
    public static class MapDefinitions
    {
        public const string TerrainAssetPath = "Assets/Data/Map/terrain_definitions.json";
        public const string RegionTypeAssetPath = "Assets/Data/Map/region_type_definitions.json";

        static TerrainDefinitionCollection _terrains;
        static RegionTypeDefinitionCollection _regionTypes;
        static Dictionary<string, TerrainDefinition> _terrainById;
        static Dictionary<string, RegionTypeDefinition> _regionTypeById;

        public static TerrainDefinitionCollection Terrains
        {
            get { EnsureLoaded(); return _terrains; }
        }

        public static RegionTypeDefinitionCollection RegionTypes
        {
            get { EnsureLoaded(); return _regionTypes; }
        }

        public static void Reload()
        {
            _terrains = null;
            _regionTypes = null;
            _terrainById = null;
            _regionTypeById = null;
            EnsureLoaded();
        }

        static void EnsureLoaded()
        {
            if (_terrains != null && _regionTypes != null) return;

            _terrains = LoadOrDefault(TerrainAssetPath, "terrain_definitions.json",
                TerrainDefinitionCollection.CreateDefault, c => c.terrains != null && c.terrains.Count > 0);
            _regionTypes = LoadOrDefault(RegionTypeAssetPath, "region_type_definitions.json",
                RegionTypeDefinitionCollection.CreateDefault, c => c.regionTypes != null && c.regionTypes.Count > 0);

            _terrainById = new Dictionary<string, TerrainDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in _terrains.terrains)
                if (t != null && !string.IsNullOrEmpty(t.id)) _terrainById[t.id] = t;

            _regionTypeById = new Dictionary<string, RegionTypeDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var rt in _regionTypes.regionTypes)
                if (rt != null && !string.IsNullOrEmpty(rt.id)) _regionTypeById[rt.id] = rt;
        }

        static T LoadOrDefault<T>(string assetPath, string fileName, Func<T> makeDefault, Func<T, bool> isValid)
            where T : class
        {
            foreach (string path in CandidatePaths(assetPath, fileName))
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    string json = File.ReadAllText(path);
                    T parsed = JsonUtility.FromJson<T>(json);
                    if (parsed != null && isValid(parsed))
                        return parsed;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"MapDefinitions: failed to read {path}: {ex.Message}");
                }
            }
            return makeDefault();
        }

        static IEnumerable<string> CandidatePaths(string assetPath, string fileName)
        {
#if UNITY_EDITOR
            yield return Path.Combine(Directory.GetCurrentDirectory(), assetPath);
#endif
            yield return Path.Combine(Application.streamingAssetsPath, fileName);
            yield return Path.Combine(Application.streamingAssetsPath, "Map", fileName);
        }

        public static TerrainDefinition GetTerrain(TerrainType type) => GetTerrain(type.ToString());

        public static TerrainDefinition GetTerrain(string id)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && _terrainById.TryGetValue(id, out var def))
                return def;
            return _terrains.terrains.Count > 0 ? _terrains.terrains[0] : new TerrainDefinition();
        }

        public static RegionTypeDefinition GetRegionType(RegionType type) => GetRegionType(type.ToString());

        public static RegionTypeDefinition GetRegionType(string id)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(id) && _regionTypeById.TryGetValue(id, out var def))
                return def;
            return _regionTypes.regionTypes.Count > 0 ? _regionTypes.regionTypes[0] : new RegionTypeDefinition();
        }

        public static Color GetTerrainColor(TerrainType type)
        {
            return ParseColor(GetTerrain(type).colorHex, Color.magenta);
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            if (!string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString(hex, out Color c))
                return c;
            return fallback;
        }

        public static bool TryParseTerrain(string id, out TerrainType type)
        {
            return Enum.TryParse(id, true, out type);
        }
    }
}
