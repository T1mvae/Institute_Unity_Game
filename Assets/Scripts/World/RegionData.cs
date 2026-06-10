using System.Collections.Generic;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// A region is a larger political/geographical territory made of MANY adjacent tiles.
    /// It is the unit that carries political stats. Region count is much smaller than tile
    /// count, and not every land tile belongs to a region (see WorldMapData.unclaimedTileIds).
    /// </summary>
    public class RegionData
    {
        public string regionId;
        public string displayName;
        public RegionType regionType = RegionType.FeudalProvince;

        /// <summary>Owning feudal state (see <see cref="StateData"/>), or empty if stateless.</summary>
        public string stateId;

        /// <summary>Tiles owned by this region. Always &gt; 1 for a valid generated region.</summary>
        public readonly List<int> tileIds = new List<int>();

        /// <summary>The region "capital" tile (a strong inland/coastal land tile).</summary>
        public int capitalTileId = -1;

        /// <summary>Tiles on this region's edge (neighbor a different region / unclaimed / water).</summary>
        public readonly List<int> borderTileIds = new List<int>();

        /// <summary>Ids of regions adjacent to this one.</summary>
        public readonly List<string> neighborRegionIds = new List<string>();

        // --- Political stats. These live ONLY on the region, never on a tile. ---
        public int influence;     // 0..100 resistance to / projection of Institute control
        public int stability;     // 0..100 internal order
        public int development;    // 0..100 economy / infrastructure

        // Optional richer economy fields.
        public int population;
        public int wealth;
        public string dominantFaction;

        public readonly List<RegionModifierState> modifiers = new List<RegionModifierState>();
        public readonly List<string> characterIds = new List<string>();
        public readonly List<string> tags = new List<string>();

        public int TileCount => tileIds.Count;

        public RegionData() { }

        public RegionData(string regionId, string displayName, RegionType type)
        {
            this.regionId = regionId;
            this.displayName = displayName;
            this.regionType = type;
        }

        public void ClampStats()
        {
            influence = Mathf.Clamp(influence, 0, 100);
            stability = Mathf.Clamp(stability, 0, 100);
            development = Mathf.Clamp(development, 0, 100);
        }

        /// <summary>Returns the stat associated with a map mode, normalized 0..1, or -1 if N/A.</summary>
        public float NormalizedStat(MapMode mode)
        {
            switch (mode)
            {
                case MapMode.Influence: return influence / 100f;
                case MapMode.Stability: return stability / 100f;
                case MapMode.Development: return development / 100f;
                case MapMode.Danger: return Mathf.Clamp01(1f - stability / 100f);
                case MapMode.Characters: return Mathf.Clamp01(characterIds.Count / 5f);
                default: return -1f;
            }
        }

        public override string ToString() =>
            $"{displayName} [{regionType}] tiles={TileCount} I/S/D={influence}/{stability}/{development}";
    }

    /// <summary>
    /// Plain serializable runtime modifier applied to a region's stats over time.
    /// Mirrors the legacy RegionModifier but lives in the new model so the two stay decoupled.
    /// </summary>
    public class RegionModifierState
    {
        public string name;
        public int influenceDelta;
        public int stabilityDelta;
        public int developmentDelta;
        public float remainingDays;   // <= 0 means permanent
        public string sourceCharacterId;

        public RegionModifierState() { }

        public RegionModifierState(string name, int infl, int stab, int dev, float days)
        {
            this.name = name;
            influenceDelta = infl;
            stabilityDelta = stab;
            developmentDelta = dev;
            remainingDays = days;
        }
    }
}
