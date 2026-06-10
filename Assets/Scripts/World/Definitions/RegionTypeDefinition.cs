using System;
using System.Collections.Generic;

namespace Institute.World
{
    /// <summary>
    /// Data-driven description of a region type. Source of truth is
    /// Assets/Data/Map/region_type_definitions.json; baked defaults below are the fallback.
    /// </summary>
    [Serializable]
    public class RegionTypeDefinition
    {
        public string id = "FeudalProvince";
        public string displayName = "Feudal Province";

        /// <summary>Terrain ids this region prefers to be seeded on (bias only).</summary>
        public List<string> preferredTerrains = new List<string>();

        // Base stats (0..100) before variance, terrain composition and difficulty.
        public int influenceBase = 45;
        public int stabilityBase = 50;
        public int developmentBase = 45;
        public int statVariance = 12;

        /// <summary>Relative target size: 0.5 small, 1 medium, 1.6 large.</summary>
        public float sizePreference = 1f;

        public List<string> tags = new List<string>();
    }

    [Serializable]
    public class RegionTypeDefinitionCollection
    {
        public List<RegionTypeDefinition> regionTypes = new List<RegionTypeDefinition>();

        public static RegionTypeDefinitionCollection CreateDefault()
        {
            var c = new RegionTypeDefinitionCollection();
            void Add(string id, string name, int infl, int stab, int dev, int variance, float size, string[] terrains, string[] tags)
            {
                var def = new RegionTypeDefinition
                {
                    id = id, displayName = name,
                    influenceBase = infl, stabilityBase = stab, developmentBase = dev,
                    statVariance = variance, sizePreference = size,
                };
                if (terrains != null) def.preferredTerrains.AddRange(terrains);
                if (tags != null) def.tags.AddRange(tags);
                c.regionTypes.Add(def);
            }

            Add("KingdomHeartland",     "Kingdom Heartland",     65, 70, 60, 8,  1.6f, new[] {"Plains", "Hills"},      new[] {"core"});
            Add("FeudalProvince",       "Feudal Province",       50, 55, 50, 12, 1.0f, new[] {"Plains", "Forest"},     new[] {"feudal"});
            Add("FrontierMarch",        "Frontier March",        40, 50, 30, 14, 1.1f, new[] {"Hills", "Forest"},      new[] {"frontier"});
            Add("TradeBasin",           "Trade Basin",           55, 50, 70, 12, 0.9f, new[] {"Coast", "Plains"},      new[] {"trade"});
            Add("TempleDomain",         "Temple Domain",         60, 72, 40, 10, 0.8f, new[] {"SacredLand", "Hills"},  new[] {"religious"});
            Add("TribalConfederation",  "Tribal Confederation",  35, 45, 30, 16, 1.2f, new[] {"Forest", "Hills"},      new[] {"tribal"});
            Add("RuinedZone",           "Ruined Zone",           25, 30, 25, 18, 0.7f, new[] {"Ruins", "Wasteland"},   new[] {"ruined", "artifacts"});
            Add("CoastalLeague",        "Coastal League",        50, 55, 60, 12, 1.0f, new[] {"Coast"},                new[] {"coastal", "trade"});
            Add("MountainClans",        "Mountain Clans",        45, 60, 35, 12, 0.8f, new[] {"Hills"},                new[] {"highland"});
            Add("NeutralSettlement",    "Neutral Settlement",    40, 55, 45, 12, 0.6f, new[] {"Plains"},               new[] {"neutral"});
            return c;
        }
    }
}
