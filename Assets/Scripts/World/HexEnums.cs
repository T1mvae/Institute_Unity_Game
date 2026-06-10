namespace Institute.World
{
    /// <summary>
    /// Per-tile terrain. Drives color, walkability and whether the tile may belong to a region.
    /// Authoritative definitions (color/cost/regionAllowed) live in
    /// Assets/Data/Map/terrain_definitions.json; this enum is the stable key.
    /// </summary>
    public enum TerrainType
    {
        DeepSea = 0,
        Sea = 1,
        Coast = 2,
        Plains = 3,
        Forest = 4,
        Hills = 5,
        Mountains = 6,
        Swamp = 7,
        Desert = 8,
        Ruins = 9,
        Wasteland = 10,
        SacredLand = 11,
        Blocked = 12,
    }

    /// <summary>Coarse climate band layered on top of terrain, used for tinting/flavor only.</summary>
    public enum BiomeType
    {
        None = 0,
        Temperate = 1,
        Boreal = 2,
        Arid = 3,
        Tropical = 4,
        Alpine = 5,
        Oceanic = 6,
    }

    /// <summary>
    /// Political/geographical kind of a <see cref="RegionData"/> (a territory of many tiles).
    /// This is NOT terrain. Stat modifiers per type live in
    /// Assets/Data/Map/region_type_definitions.json.
    /// </summary>
    public enum RegionType
    {
        KingdomHeartland = 0,
        FeudalProvince = 1,
        FrontierMarch = 2,
        TradeBasin = 3,
        TempleDomain = 4,
        TribalConfederation = 5,
        RuinedZone = 6,
        CoastalLeague = 7,
        MountainClans = 8,
        NeutralSettlement = 9,
    }

    /// <summary>How the map is colored. Selected per-frame by the map-mode bar.</summary>
    public enum MapMode
    {
        Terrain = 0,
        Political = 1,
        Influence = 2,
        Stability = 3,
        Development = 4,
        Danger = 5,
        Characters = 6,
    }
}
