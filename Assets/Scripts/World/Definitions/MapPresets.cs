using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Institute.World
{
    [Serializable]
    public class MapPresetEntry
    {
        public string id = "Medium";
        public string displayName = "Medium";
        public int width = 48;
        public int height = 32;
        public int targetRegionCount = 22;
        public float unclaimedLandFraction = 0.28f;
        public float seaFraction = 0.34f;
        public float terrainRoughness = 0.5f;
        public float continentFrequency = 0.085f;

        public MapGenerationSettings ToSettings(int seed, string difficultyId)
        {
            return new MapGenerationSettings
            {
                seed = seed,
                width = width,
                height = height,
                targetRegionCount = targetRegionCount,
                unclaimedLandFraction = unclaimedLandFraction,
                seaFraction = seaFraction,
                terrainRoughness = terrainRoughness,
                continentFrequency = continentFrequency,
                difficultyId = difficultyId,
            };
        }
    }

    [Serializable]
    public class MapPresetCollection
    {
        public List<MapPresetEntry> presets = new List<MapPresetEntry>();
    }

    /// <summary>Data-driven map size presets (Assets/Data/Map/map_presets.json) with code fallback.</summary>
    public static class MapPresets
    {
        public const string AssetPath = "Assets/Data/Map/map_presets.json";
        static MapPresetCollection _collection;

        public static MapPresetCollection All
        {
            get { if (_collection == null) Reload(); return _collection; }
        }

        public static void Reload()
        {
            _collection = Load() ?? Fallback();
        }

        static MapPresetCollection Load()
        {
            foreach (string path in Paths())
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var parsed = JsonUtility.FromJson<MapPresetCollection>(File.ReadAllText(path));
                    if (parsed != null && parsed.presets != null && parsed.presets.Count > 0) return parsed;
                }
                catch (Exception ex) { Debug.LogWarning("MapPresets: " + ex.Message); }
            }
            return null;
        }

        static IEnumerable<string> Paths()
        {
#if UNITY_EDITOR
            yield return Path.Combine(Directory.GetCurrentDirectory(), AssetPath);
#endif
            yield return Path.Combine(Application.streamingAssetsPath, "map_presets.json");
        }

        static MapPresetCollection Fallback()
        {
            var c = new MapPresetCollection();
            foreach (string id in new[] { "DebugTiny", "Small", "Medium", "Large" })
            {
                MapGenerationSettings s = MapGenerationSettings.ForPreset(id);
                c.presets.Add(new MapPresetEntry
                {
                    id = id, displayName = id, width = s.width, height = s.height,
                    targetRegionCount = s.targetRegionCount,
                    unclaimedLandFraction = s.unclaimedLandFraction, seaFraction = s.seaFraction,
                    terrainRoughness = s.terrainRoughness, continentFrequency = s.continentFrequency,
                });
            }
            return c;
        }

        public static MapPresetEntry Get(string id)
        {
            foreach (var p in All.presets)
                if (string.Equals(p.id, id, StringComparison.OrdinalIgnoreCase)) return p;
            return All.presets.Count > 0 ? All.presets[All.presets.Count > 2 ? 2 : 0] : new MapPresetEntry();
        }
    }
}
