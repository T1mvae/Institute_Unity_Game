using System;
using System.IO;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Map-specific colors and metrics. Terrain colors come from terrain_definitions.json;
    /// everything else (unclaimed/sea/selection/stat gradient/border widths) is the "map"
    /// section of Assets/Data/UI/theme.json. Falls back to sensible defaults if missing.
    /// </summary>
    [Serializable]
    public class MapPaletteData
    {
        public float hexSize = 1f;
        public float borderWidth = 0.05f;
        public float selectedBorderWidth = 0.1f;
        public string unclaimed = "#303746";
        public string sea = "#15324D";
        public string deepSea = "#0A1A2E";
        public string blocked = "#2A2A2A";
        public string selection = "#F4C542";
        public string regionBorder = "#0B0E14";
        public string coastBorder = "#1E5470";
        public string selectedBorder = "#FFD56B";
        public string statLow = "#5A2E2E";
        public string statMid = "#B0892E";
        public string statHigh = "#37C871";
    }

    [Serializable]
    class ThemeMapWrapper
    {
        public MapPaletteData map = new MapPaletteData();
    }

    public static class MapPalette
    {
        const string ThemeAssetPath = "Assets/Data/UI/theme.json";

        static MapPaletteData _data;

        public static MapPaletteData Data
        {
            get { if (_data == null) Reload(); return _data; }
        }

        public static void Reload()
        {
            _data = LoadFromTheme() ?? new MapPaletteData();
        }

        static MapPaletteData LoadFromTheme()
        {
            foreach (string path in CandidatePaths())
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    var wrapper = JsonUtility.FromJson<ThemeMapWrapper>(File.ReadAllText(path));
                    if (wrapper != null && wrapper.map != null) return wrapper.map;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("MapPalette: failed to read " + path + ": " + ex.Message);
                }
            }
            return null;
        }

        static System.Collections.Generic.IEnumerable<string> CandidatePaths()
        {
#if UNITY_EDITOR
            yield return Path.Combine(Directory.GetCurrentDirectory(), ThemeAssetPath);
#endif
            yield return Path.Combine(Application.streamingAssetsPath, "theme.json");
        }

        public static float HexSize => Data.hexSize;
        public static Color Unclaimed => Parse(Data.unclaimed, new Color(0.19f, 0.22f, 0.27f));
        public static Color Sea => Parse(Data.sea, new Color(0.08f, 0.20f, 0.30f));
        public static Color DeepSea => Parse(Data.deepSea, new Color(0.04f, 0.10f, 0.18f));
        public static Color Blocked => Parse(Data.blocked, new Color(0.16f, 0.16f, 0.16f));
        public static Color Selection => Parse(Data.selection, new Color(0.96f, 0.77f, 0.26f));
        public static Color RegionBorder => Parse(Data.regionBorder, new Color(0.04f, 0.05f, 0.08f));
        public static Color CoastBorder => Parse(Data.coastBorder, new Color(0.12f, 0.33f, 0.44f));
        public static Color SelectedBorder => Parse(Data.selectedBorder, new Color(1f, 0.84f, 0.42f));

        public static Color StatGradient(float t)
        {
            t = Mathf.Clamp01(t);
            Color low = Parse(Data.statLow, new Color(0.35f, 0.18f, 0.18f));
            Color mid = Parse(Data.statMid, new Color(0.69f, 0.54f, 0.18f));
            Color high = Parse(Data.statHigh, new Color(0.22f, 0.78f, 0.44f));
            return t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, high, (t - 0.5f) * 2f);
        }

        /// <summary>Deterministic, readable fill color for a region (Political mode).</summary>
        public static Color RegionColor(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return Unclaimed;
            int hash = 17;
            foreach (char c in regionId) hash = unchecked(hash * 31 + c);
            float hue = (Mathf.Abs(hash) % 360) / 360f;
            float sat = 0.45f + ((Mathf.Abs(hash / 360) % 30) / 100f);
            return Color.HSVToRGB(hue, Mathf.Clamp01(sat), 0.78f);
        }

        static Color Parse(string hex, Color fallback)
        {
            return MapDefinitions.ParseColor(hex, fallback);
        }
    }
}
