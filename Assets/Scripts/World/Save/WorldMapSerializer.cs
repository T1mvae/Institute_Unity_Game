using System;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Converts between the runtime <see cref="WorldMapData"/> and the serializable
    /// <see cref="WorldMapSave"/>, and detects/handles legacy save formats safely.
    /// </summary>
    public static class WorldMapSerializer
    {
        public static WorldMapSave ToSave(WorldMapData map)
        {
            var save = new WorldMapSave
            {
                schemaVersion = WorldMapVersion.Current,
                seed = map.seed,
                width = map.width,
                height = map.height,
                settings = map.settings != null ? map.settings.Clone() : new MapGenerationSettings(),
            };

            foreach (var tile in map.Tiles)
            {
                var ts = new WorldTileSave
                {
                    tileId = tile.tileId,
                    q = tile.coord.q,
                    r = tile.coord.r,
                    terrainType = tile.terrainType.ToString(),
                    biomeType = tile.biomeType.ToString(),
                    elevation = tile.elevation,
                    moisture = tile.moisture,
                    isWalkable = tile.isWalkable,
                    isVisible = tile.isVisible,
                    regionId = tile.regionId ?? "",
                    movementCost = tile.movementCost,
                    dangerLevel = tile.dangerLevel,
                    developmentPotential = tile.developmentPotential,
                };
                ts.resourceTags.AddRange(tile.resourceTags);
                ts.specialFeatureTags.AddRange(tile.specialFeatureTags);
                save.tiles.Add(ts);
            }

            foreach (var region in map.Regions)
            {
                var rs = new WorldRegionSave
                {
                    regionId = region.regionId,
                    displayName = region.displayName,
                    regionType = region.regionType.ToString(),
                    stateId = region.stateId ?? "",
                    capitalTileId = region.capitalTileId,
                    influence = region.influence,
                    stability = region.stability,
                    development = region.development,
                    population = region.population,
                    wealth = region.wealth,
                    dominantFaction = region.dominantFaction ?? "",
                };
                rs.tileIds.AddRange(region.tileIds);
                rs.borderTileIds.AddRange(region.borderTileIds);
                rs.neighborRegionIds.AddRange(region.neighborRegionIds);
                rs.characterIds.AddRange(region.characterIds);
                rs.tags.AddRange(region.tags);
                foreach (var m in region.modifiers)
                {
                    rs.modifiers.Add(new WorldRegionModifierSave
                    {
                        name = m.name,
                        influenceDelta = m.influenceDelta,
                        stabilityDelta = m.stabilityDelta,
                        developmentDelta = m.developmentDelta,
                        remainingDays = m.remainingDays,
                        sourceCharacterId = m.sourceCharacterId ?? "",
                    });
                }
                save.regions.Add(rs);
            }

            foreach (var state in map.States)
            {
                var ss = new StateData(state.stateId, state.displayName, state.colorHex)
                {
                    capitalRegionId = state.capitalRegionId,
                    stability = state.stability,
                    development = state.development,
                    influence = state.influence,
                };
                ss.regionIds.AddRange(state.regionIds);
                save.states.Add(ss);
            }

            save.unclaimedTileIds.AddRange(map.unclaimedTileIds);
            return save;
        }

        public static WorldMapData FromSave(WorldMapSave save)
        {
            if (save == null) return null;

            var map = new WorldMapData
            {
                seed = save.seed,
                width = save.width,
                height = save.height,
                settings = save.settings ?? new MapGenerationSettings(),
                generatedAtVersion = save.schemaVersion,
            };

            foreach (var ts in save.tiles)
            {
                var tile = new HexTileData(ts.tileId, new HexCoord(ts.q, ts.r))
                {
                    terrainType = ParseEnum(ts.terrainType, TerrainType.Plains),
                    biomeType = ParseEnum(ts.biomeType, BiomeType.Temperate),
                    elevation = ts.elevation,
                    moisture = ts.moisture,
                    isWalkable = ts.isWalkable,
                    isVisible = ts.isVisible,
                    regionId = string.IsNullOrEmpty(ts.regionId) ? null : ts.regionId,
                    movementCost = ts.movementCost,
                    dangerLevel = ts.dangerLevel,
                    developmentPotential = ts.developmentPotential,
                };
                if (ts.resourceTags != null) tile.resourceTags.AddRange(ts.resourceTags);
                if (ts.specialFeatureTags != null) tile.specialFeatureTags.AddRange(ts.specialFeatureTags);
                map.AddTile(tile);
            }

            foreach (var rs in save.regions)
            {
                var region = new RegionData(rs.regionId, rs.displayName, ParseEnum(rs.regionType, RegionType.FeudalProvince))
                {
                    stateId = string.IsNullOrEmpty(rs.stateId) ? null : rs.stateId,
                    capitalTileId = rs.capitalTileId,
                    influence = rs.influence,
                    stability = rs.stability,
                    development = rs.development,
                    population = rs.population,
                    wealth = rs.wealth,
                    dominantFaction = rs.dominantFaction,
                };
                if (rs.tileIds != null) region.tileIds.AddRange(rs.tileIds);
                if (rs.borderTileIds != null) region.borderTileIds.AddRange(rs.borderTileIds);
                if (rs.neighborRegionIds != null) region.neighborRegionIds.AddRange(rs.neighborRegionIds);
                if (rs.characterIds != null) region.characterIds.AddRange(rs.characterIds);
                if (rs.tags != null) region.tags.AddRange(rs.tags);
                if (rs.modifiers != null)
                    foreach (var m in rs.modifiers)
                        region.modifiers.Add(new RegionModifierState(m.name, m.influenceDelta, m.stabilityDelta, m.developmentDelta, m.remainingDays)
                        {
                            sourceCharacterId = m.sourceCharacterId,
                        });
                map.regionsById[region.regionId] = region;
            }

            if (save.states != null)
                foreach (var ss in save.states)
                    if (ss != null && !string.IsNullOrEmpty(ss.stateId))
                        map.statesById[ss.stateId] = ss;

            if (save.unclaimedTileIds != null) map.unclaimedTileIds.AddRange(save.unclaimedTileIds);
            return map;
        }

        public static string ToJson(WorldMapData map, bool pretty = true)
        {
            return JsonUtility.ToJson(ToSave(map), pretty);
        }

        /// <summary>
        /// Parses a world map from JSON. Returns null and sets <paramref name="error"/> if the
        /// payload is missing, corrupt, or in the legacy "one hex = one region" format.
        /// Never throws.
        /// </summary>
        public static WorldMapData FromJson(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                error = "Empty world map payload.";
                return null;
            }

            try
            {
                WorldMapSave save = JsonUtility.FromJson<WorldMapSave>(json);
                if (save == null)
                {
                    error = "World map JSON could not be parsed.";
                    return null;
                }

                // Detect legacy format: no tile layer, or schema below the corrected model.
                if (save.tiles == null || save.tiles.Count == 0)
                {
                    error = "Legacy save detected (no tile layer). This save predates the " +
                            "tiles+regions refactor and cannot be migrated safely.";
                    return null;
                }
                if (IsLegacySchema(save.schemaVersion))
                {
                    error = $"Save schema '{save.schemaVersion}' is older than the corrected model " +
                            $"('{WorldMapVersion.Current}').";
                    return null;
                }

                return FromSave(save);
            }
            catch (Exception ex)
            {
                error = "World map parse exception: " + ex.Message;
                return null;
            }
        }

        public static bool IsLegacySchema(string schemaVersion)
        {
            if (string.IsNullOrEmpty(schemaVersion)) return true;
            if (int.TryParse(schemaVersion, out int v))
                return v < int.Parse(WorldMapVersion.Current);
            return true;
        }

        static TEnum ParseEnum<TEnum>(string value, TEnum fallback) where TEnum : struct
        {
            return Enum.TryParse(value, true, out TEnum parsed) ? parsed : fallback;
        }
    }
}
