using System;
using System.Collections.Generic;

namespace Institute.World
{
    /// <summary>
    /// Flat, JsonUtility-friendly snapshot of a <see cref="WorldMapData"/>.
    /// Tiles and regions are saved SEPARATELY (the corrected model), unlike the legacy
    /// save which stored tiles-as-regions. Enums are stored as strings for readability.
    /// </summary>
    [Serializable]
    public class WorldMapSave
    {
        public string schemaVersion = WorldMapVersion.Current;
        public int seed;
        public int width;
        public int height;
        public MapGenerationSettings settings = new MapGenerationSettings();
        public List<WorldTileSave> tiles = new List<WorldTileSave>();
        public List<WorldRegionSave> regions = new List<WorldRegionSave>();
        public List<StateData> states = new List<StateData>();
        public List<int> unclaimedTileIds = new List<int>();
    }

    [Serializable]
    public class WorldTileSave
    {
        public int tileId;
        public int q;
        public int r;
        public string terrainType;
        public string biomeType;
        public float elevation;
        public float moisture;
        public bool isWalkable = true;
        public bool isVisible = true;
        public string regionId = ""; // empty == unclaimed
        public float movementCost = 1f;
        public List<string> resourceTags = new List<string>();
        public List<string> specialFeatureTags = new List<string>();
        public float dangerLevel;
        public float developmentPotential;
    }

    [Serializable]
    public class WorldRegionSave
    {
        public string regionId;
        public string displayName;
        public string regionType;
        public string stateId = "";
        public List<int> tileIds = new List<int>();
        public int capitalTileId = -1;
        public List<int> borderTileIds = new List<int>();
        public List<string> neighborRegionIds = new List<string>();
        public int influence;
        public int stability;
        public int development;
        public int population;
        public int wealth;
        public string dominantFaction = "";
        public List<WorldRegionModifierSave> modifiers = new List<WorldRegionModifierSave>();
        public List<string> characterIds = new List<string>();
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class WorldRegionModifierSave
    {
        public string name;
        public int influenceDelta;
        public int stabilityDelta;
        public int developmentDelta;
        public float remainingDays;
        public string sourceCharacterId = "";
    }
}
