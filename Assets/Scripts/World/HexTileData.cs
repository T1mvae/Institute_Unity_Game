using System.Collections.Generic;

namespace Institute.World
{
    /// <summary>
    /// A single small hex cell. This is the atomic unit of the world.
    ///
    /// IMPORTANT: a tile has terrain and local features only. It has NO political
    /// stats (Influence/Stability/Development) — those live on <see cref="RegionData"/>.
    /// A tile MAY belong to one region (<see cref="regionId"/>) or to none
    /// (empty string == unclaimed/wilderness/sea).
    /// </summary>
    public class HexTileData
    {
        public int tileId;
        public HexCoord coord;

        public TerrainType terrainType = TerrainType.Plains;
        public BiomeType biomeType = BiomeType.Temperate;

        /// <summary>Normalized 0..1 height field from generation noise.</summary>
        public float elevation;
        /// <summary>Normalized 0..1 moisture field from generation noise.</summary>
        public float moisture;

        public bool isWalkable = true;
        public bool isVisible = true;

        /// <summary>Owning region id, or empty/null when the tile is unclaimed.</summary>
        public string regionId;

        /// <summary>Pathfinding cost; high or impassable for sea/mountains.</summary>
        public float movementCost = 1f;

        public readonly List<string> resourceTags = new List<string>();
        public readonly List<string> specialFeatureTags = new List<string>();

        /// <summary>0..1 local hazard, used by the Danger map mode and events.</summary>
        public float dangerLevel;
        /// <summary>0..1 hint of how good this tile would be inside a region.</summary>
        public float developmentPotential;

        public bool HasRegion => !string.IsNullOrEmpty(regionId);

        public bool IsWaterTerrain =>
            terrainType == TerrainType.Sea || terrainType == TerrainType.DeepSea;

        public HexTileData() { }

        public HexTileData(int tileId, HexCoord coord)
        {
            this.tileId = tileId;
            this.coord = coord;
        }

        public override string ToString() => $"Tile#{tileId} {coord} {terrainType}";
    }
}
