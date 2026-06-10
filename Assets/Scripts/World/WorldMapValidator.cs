using System.Collections.Generic;
using System.Text;

namespace Institute.World
{
    public class ValidationResult
    {
        public readonly List<string> passed = new List<string>();
        public readonly List<string> warnings = new List<string>();
        public readonly List<string> errors = new List<string>();

        public bool IsValid => errors.Count == 0;

        public void Pass(string msg) => passed.Add(msg);
        public void Warn(string msg) => warnings.Add(msg);
        public void Error(string msg) => errors.Add(msg);

        public string ToReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"World map validation: {(IsValid ? "PASS" : "FAIL")}  " +
                          $"({passed.Count} ok, {warnings.Count} warn, {errors.Count} error)");
            foreach (var p in passed) sb.AppendLine("  [ok]   " + p);
            foreach (var w in warnings) sb.AppendLine("  [warn] " + w);
            foreach (var e in errors) sb.AppendLine("  [ERR]  " + e);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Asserts the corrected map invariants. Run from editor tools or at runtime in debug
    /// builds to catch regressions of the old "one hex = one region" model.
    /// </summary>
    public static class WorldMapValidator
    {
        public static ValidationResult Validate(WorldMapData map)
        {
            var result = new ValidationResult();
            if (map == null)
            {
                result.Error("Map is null.");
                return result;
            }

            // Tile count matches grid shape.
            int expected = map.width * map.height;
            if (map.TileCount == expected)
                result.Pass($"Tile count = {map.TileCount} (= width*height).");
            else
                result.Error($"Tile count {map.TileCount} != width*height {expected}.");

            // Region count must be MUCH smaller than tile count.
            if (map.RegionCount == 0)
                result.Warn("No regions were generated.");
            else if (map.RegionCount < map.TileCount / 4)
                result.Pass($"Region count {map.RegionCount} << tile count {map.TileCount}.");
            else
                result.Error($"Region count {map.RegionCount} is too large vs tiles {map.TileCount} " +
                             "(looks like the old one-hex-one-region model).");

            // Every region must own multiple tiles.
            int singleTile = 0;
            foreach (var region in map.Regions)
                if (region.TileCount < 2) singleTile++;
            if (singleTile == 0)
                result.Pass("Every region owns 2+ tiles.");
            else
                result.Error($"{singleTile} region(s) own a single tile.");

            // Unclaimed land must exist when requested.
            float requestedUnclaimed = map.settings != null ? map.settings.unclaimedLandFraction : 0f;
            if (requestedUnclaimed > 0.01f)
            {
                if (map.unclaimedTileIds.Count > 0)
                    result.Pass($"{map.unclaimedTileIds.Count} land tiles left unclaimed (wilderness).");
                else
                    result.Warn("unclaimedLandFraction > 0 but no unclaimed land tiles exist.");
            }

            // Water tiles must never belong to a land region.
            int waterInRegion = 0;
            foreach (var tile in map.Tiles)
                if (tile.IsWaterTerrain && tile.HasRegion) waterInRegion++;
            if (waterInRegion == 0)
                result.Pass("No sea/deep-sea tile belongs to a region.");
            else
                result.Error($"{waterInRegion} water tile(s) are owned by a region.");

            // Tile.regionId must point at a real region and be listed there.
            int danglingOwners = 0, mismatched = 0;
            foreach (var tile in map.Tiles)
            {
                if (!tile.HasRegion) continue;
                RegionData region = map.GetRegion(tile.regionId);
                if (region == null) { danglingOwners++; continue; }
                if (!region.tileIds.Contains(tile.tileId)) mismatched++;
            }
            if (danglingOwners == 0 && mismatched == 0)
                result.Pass("All owned tiles reference an existing region that lists them.");
            else
                result.Error($"Ownership integrity: {danglingOwners} dangling, {mismatched} not-listed.");

            // Neighbor symmetry.
            int asymmetric = 0;
            foreach (var region in map.Regions)
                foreach (string nid in region.neighborRegionIds)
                {
                    RegionData other = map.GetRegion(nid);
                    if (other == null || !other.neighborRegionIds.Contains(region.regionId)) asymmetric++;
                }
            if (asymmetric == 0)
                result.Pass("Region neighbor links are symmetric.");
            else
                result.Warn($"{asymmetric} asymmetric region neighbor link(s).");

            // Every region has a valid capital it owns.
            int badCapitals = 0;
            foreach (var region in map.Regions)
            {
                HexTileData cap = map.GetTile(region.capitalTileId);
                if (cap == null || cap.regionId != region.regionId) badCapitals++;
            }
            if (badCapitals == 0)
                result.Pass("Every region has a capital tile it owns.");
            else
                result.Error($"{badCapitals} region(s) have an invalid capital tile.");

            return result;
        }
    }
}
