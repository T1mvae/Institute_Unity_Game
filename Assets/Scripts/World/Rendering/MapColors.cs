using UnityEngine;

namespace Institute.World
{
    /// <summary>Resolves the fill color of a tile for a given map mode. Pure, shared by renderers.</summary>
    public static class MapColors
    {
        public static Color ForTile(WorldMapData map, HexTileData tile, MapMode mode)
        {
            if (tile == null) return Color.magenta;

            // Water and blocked always read as themselves except in pure Terrain mode (data colors).
            if (mode == MapMode.Terrain)
                return MapDefinitions.GetTerrainColor(tile.terrainType);

            if (tile.terrainType == TerrainType.DeepSea) return MapPalette.DeepSea;
            if (tile.terrainType == TerrainType.Sea) return MapPalette.Sea;
            if (tile.terrainType == TerrainType.Blocked) return MapPalette.Blocked;

            RegionData region = map.GetRegionForTile(tile);

            if (mode == MapMode.Political)
            {
                if (region == null) return MapPalette.Unclaimed;
                // Color by the owning feudal state; fall back to a per-region hue if stateless.
                StateData state = map.GetState(region.stateId);
                if (state != null) return MapDefinitions.ParseColor(state.colorHex, MapPalette.RegionColor(region.regionId));
                return MapPalette.RegionColor(region.regionId);
            }

            if (mode == MapMode.Danger)
            {
                float dt = region != null ? region.NormalizedStat(MapMode.Danger) : tile.dangerLevel;
                Color c = MapPalette.StatGradient(dt);
                return region != null ? c : Desaturate(c, 0.55f);
            }

            // Influence / Stability / Development / Characters
            if (region != null)
            {
                float t = region.NormalizedStat(mode);
                if (t < 0f) return MapPalette.Unclaimed;
                return MapPalette.StatGradient(t);
            }
            return MapPalette.Unclaimed;
        }

        static Color Desaturate(Color c, float amount)
        {
            float g = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            return Color.Lerp(c, new Color(g, g, g), amount);
        }
    }
}
