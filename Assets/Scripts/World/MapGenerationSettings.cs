using System;

namespace Institute.World
{
    /// <summary>
    /// Everything the generator needs to deterministically build a world.
    /// Created from a map-size preset + difficulty + New Game Setup overrides.
    /// </summary>
    [Serializable]
    public class MapGenerationSettings
    {
        public int seed = 12345;
        public int width = 48;
        public int height = 32;

        /// <summary>Roughly how many regions to seed. Must be far smaller than width*height.</summary>
        public int targetRegionCount = 22;

        /// <summary>0..1 fraction of land that should be left WITHOUT a region.</summary>
        public float unclaimedLandFraction = 0.28f;

        /// <summary>0..1 fraction of the map that should be water (sea/deep sea/coast).</summary>
        public float seaFraction = 0.34f;

        /// <summary>0..1, higher = noisier coastlines and more mountains/hills.</summary>
        public float terrainRoughness = 0.5f;

        /// <summary>Noise frequency for the landmass field. Higher = more, smaller continents.</summary>
        public float continentFrequency = 0.085f;

        /// <summary>Difficulty key (Easy/Normal/Hard/Custom) folded into stat rolls.</summary>
        public string difficultyId = "Normal";

        public MapGenerationSettings Clone()
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

        /// <summary>Built-in size presets (also expressible via Assets/Data/Map/map_presets.json).</summary>
        public static MapGenerationSettings ForPreset(string preset)
        {
            switch ((preset ?? "Medium").Trim().ToLowerInvariant())
            {
                case "debugtiny":
                    return new MapGenerationSettings { width = 14, height = 10, targetRegionCount = 5 };
                case "small":
                    return new MapGenerationSettings { width = 32, height = 24, targetRegionCount = 14 };
                case "large":
                    return new MapGenerationSettings { width = 64, height = 40, targetRegionCount = 30 };
                case "medium":
                default:
                    return new MapGenerationSettings { width = 48, height = 32, targetRegionCount = 22 };
            }
        }
    }
}
