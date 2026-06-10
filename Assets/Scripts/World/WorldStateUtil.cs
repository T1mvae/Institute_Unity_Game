namespace Institute.World
{
    /// <summary>Helpers for keeping feudal-state aggregates in sync with their member regions.</summary>
    public static class WorldStateUtil
    {
        /// <summary>Recomputes a state's stability/development/influence as the mean of its regions.</summary>
        public static void RecomputeStats(WorldMapData map, StateData state)
        {
            if (map == null || state == null) return;
            int n = 0; long stab = 0, dev = 0, infl = 0;
            foreach (string rid in state.regionIds)
            {
                RegionData r = map.GetRegion(rid);
                if (r == null) continue;
                stab += r.stability; dev += r.development; infl += r.influence; n++;
            }
            if (n == 0) return;
            state.stability = (int)(stab / n);
            state.development = (int)(dev / n);
            state.influence = (int)(infl / n);
        }

        public static void RecomputeAll(WorldMapData map)
        {
            if (map == null) return;
            foreach (var state in map.States) RecomputeStats(map, state);
        }

        public static StateData StateForRegion(WorldMapData map, RegionData region)
        {
            return map != null && region != null ? map.GetState(region.stateId) : null;
        }
    }
}
