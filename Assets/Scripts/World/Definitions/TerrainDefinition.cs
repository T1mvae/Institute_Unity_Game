using System;
using System.Collections.Generic;

namespace Institute.World
{
    /// <summary>
    /// Data-driven description of a terrain type. Source of truth is
    /// Assets/Data/Map/terrain_definitions.json; baked defaults below guarantee the
    /// generator still works if the file is missing or invalid.
    /// </summary>
    [Serializable]
    public class TerrainDefinition
    {
        public string id = "Plains";
        public string displayName = "Plains";
        public string colorHex = "#5C8A3A";
        public bool regionAllowed = true;
        public bool canBeRegionSeed = true;
        public bool isWater = false;
        public bool isWalkable = true;
        /// <summary>Cost for a region to expand INTO this tile (BFS weight). High = resists growth.</summary>
        public float regionGrowthCost = 1f;
        public float movementCost = 1f;
        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class TerrainDefinitionCollection
    {
        public List<TerrainDefinition> terrains = new List<TerrainDefinition>();

        public static TerrainDefinitionCollection CreateDefault()
        {
            var c = new TerrainDefinitionCollection();
            void Add(string id, string name, string color, bool regionAllowed, bool seed, bool water, bool walk, float growth, float move, params string[] tags)
            {
                var def = new TerrainDefinition
                {
                    id = id, displayName = name, colorHex = color,
                    regionAllowed = regionAllowed, canBeRegionSeed = seed,
                    isWater = water, isWalkable = walk,
                    regionGrowthCost = growth, movementCost = move,
                };
                def.tags.AddRange(tags);
                c.terrains.Add(def);
            }

            //   id          display       color      region seed  water  walk  growth move  tags
            Add("DeepSea",   "Deep Sea",   "#0A1A2E", false, false, true,  false, 999f, 999f, "water");
            Add("Sea",       "Sea",        "#15324D", false, false, true,  false, 999f, 999f, "water");
            Add("Coast",     "Coast",      "#1E5470", true,  true,  false, true,  1.3f, 1.5f, "coastal");
            Add("Plains",    "Plains",     "#5C8A3A", true,  true,  false, true,  1.0f, 1.0f, "fertile");
            Add("Forest",    "Forest",     "#2F6B3A", true,  true,  false, true,  1.8f, 1.6f, "wooded");
            Add("Hills",     "Hills",      "#7A7A45", true,  true,  false, true,  2.2f, 2.0f, "rough");
            Add("Mountains", "Mountains",  "#6E6A66", false, false, false, false, 6.0f, 99f,  "impassable", "rough");
            Add("Swamp",     "Swamp",      "#3E5240", true,  false, false, true,  3.0f, 2.5f, "wet");
            Add("Desert",    "Desert",     "#C2A55B", true,  false, false, true,  2.6f, 1.8f, "arid");
            Add("Ruins",     "Ruins",      "#5A4A6A", false, false, false, true,  4.0f, 1.5f, "ancient", "artifacts");
            Add("Wasteland",  "Wasteland",  "#4A3A33", false, false, false, true,  5.0f, 2.0f, "hostile");
            Add("SacredLand", "Sacred Land","#8A6BB0", true,  true,  false, true,  2.0f, 1.2f, "holy");
            Add("Blocked",    "Blocked",    "#2A2A2A", false, false, false, false, 999f, 999f, "impassable");
            return c;
        }
    }
}
