using System.Collections.Generic;

namespace Institute.World
{
    /// <summary>
    /// The whole world: a grid of <see cref="HexTileData"/> plus the <see cref="RegionData"/>
    /// territories laid on top of them. Runtime lookups use dictionaries; persistence uses the
    /// separate flat WorldMapSaveData (see Save/WorldMapSaveData.cs).
    /// </summary>
    public class WorldMapData
    {
        public int seed;
        public int width;
        public int height;
        public string generatedAtVersion = WorldMapVersion.Current;
        public MapGenerationSettings settings;

        public readonly Dictionary<int, HexTileData> tilesById = new Dictionary<int, HexTileData>();
        public readonly Dictionary<HexCoord, HexTileData> tilesByCoord = new Dictionary<HexCoord, HexTileData>();
        public readonly Dictionary<string, RegionData> regionsById = new Dictionary<string, RegionData>();

        /// <summary>Feudal states (kingdoms/duchies) clustering the regions.</summary>
        public readonly Dictionary<string, StateData> statesById = new Dictionary<string, StateData>();

        /// <summary>Land tiles that intentionally belong to no region.</summary>
        public readonly List<int> unclaimedTileIds = new List<int>();

        public IEnumerable<HexTileData> Tiles => tilesById.Values;
        public IEnumerable<RegionData> Regions => regionsById.Values;
        public IEnumerable<StateData> States => statesById.Values;
        public int StateCount => statesById.Count;
        public int TileCount => tilesById.Count;
        public int RegionCount => regionsById.Count;

        public void AddTile(HexTileData tile)
        {
            tilesById[tile.tileId] = tile;
            tilesByCoord[tile.coord] = tile;
        }

        public HexTileData GetTile(int tileId)
        {
            tilesById.TryGetValue(tileId, out HexTileData tile);
            return tile;
        }

        public HexTileData GetTile(HexCoord coord)
        {
            tilesByCoord.TryGetValue(coord, out HexTileData tile);
            return tile;
        }

        public RegionData GetRegion(string regionId)
        {
            if (string.IsNullOrEmpty(regionId)) return null;
            regionsById.TryGetValue(regionId, out RegionData region);
            return region;
        }

        public StateData GetState(string stateId)
        {
            if (string.IsNullOrEmpty(stateId)) return null;
            statesById.TryGetValue(stateId, out StateData state);
            return state;
        }

        /// <summary>The feudal state that owns the region a tile belongs to, or null.</summary>
        public StateData GetStateForTile(HexTileData tile)
        {
            RegionData region = GetRegionForTile(tile);
            return region != null ? GetState(region.stateId) : null;
        }

        /// <summary>Region that owns the given tile, or null if the tile is unclaimed.</summary>
        public RegionData GetRegionForTile(HexTileData tile)
        {
            return tile != null ? GetRegion(tile.regionId) : null;
        }

        /// <summary>Returns the existing tile neighbors of a tile (1..6), skipping off-map coords.</summary>
        public IEnumerable<HexTileData> GetNeighbors(HexTileData tile)
        {
            if (tile == null) yield break;
            for (int i = 0; i < 6; i++)
            {
                HexTileData n = GetTile(tile.coord.Neighbor(i));
                if (n != null) yield return n;
            }
        }
    }

    public static class WorldMapVersion
    {
        /// <summary>
        /// Bumped whenever the world/save schema changes. "2" marks the post-refactor
        /// tiles+regions model; legacy "one hex = one region" saves were schema 1.
        /// </summary>
        public const string Current = "2";
    }
}
