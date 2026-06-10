namespace Institute.World
{
    /// <summary>
    /// Connects characters to REGIONS (not individual tiles) in the corrected model. Characters
    /// are identified by string id, which lines up with the legacy CharacterSaveData
    /// homeRegionId / currentRegionId fields — those can now reference RegionData.regionId.
    ///
    /// Character effects apply to RegionData stats (never to a tile). A character may optionally
    /// reference a specific tile for special events (travel/ruins/prison/camp) via the tile id,
    /// but its political home is always a region.
    /// </summary>
    public static class WorldCharacterBridge
    {
        public static bool AttachCharacterToRegion(WorldMapData map, string regionId, string characterId)
        {
            RegionData region = map != null ? map.GetRegion(regionId) : null;
            if (region == null || string.IsNullOrEmpty(characterId)) return false;
            if (!region.characterIds.Contains(characterId)) region.characterIds.Add(characterId);
            return true;
        }

        public static void DetachCharacter(WorldMapData map, string characterId)
        {
            if (map == null || string.IsNullOrEmpty(characterId)) return;
            foreach (var region in map.Regions) region.characterIds.Remove(characterId);
        }

        /// <summary>Move a character between regions (the only kind of "movement" by default).</summary>
        public static void MoveCharacter(WorldMapData map, string characterId, string toRegionId)
        {
            DetachCharacter(map, characterId);
            AttachCharacterToRegion(map, toRegionId, characterId);
        }

        /// <summary>Apply a character-sourced modifier to its region's political stats.</summary>
        public static void ApplyEffect(RegionData region, int influence, int stability, int development, string sourceCharacterId)
        {
            if (region == null) return;
            region.influence += influence;
            region.stability += stability;
            region.development += development;
            region.ClampStats();
            region.modifiers.Add(new RegionModifierState($"Character {sourceCharacterId}", influence, stability, development, 0f)
            {
                sourceCharacterId = sourceCharacterId,
            });
        }

        public static int CharacterCount(RegionData region) => region != null ? region.characterIds.Count : 0;
    }
}
