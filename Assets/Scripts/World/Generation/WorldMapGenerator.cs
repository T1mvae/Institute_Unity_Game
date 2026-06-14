using System;
using System.Collections.Generic;
using UnityEngine;

namespace Institute.World
{
    /// <summary>
    /// Builds a Civilization-like world: a large hex grid of small tiles FIRST, then a
    /// small number of multi-tile regions grown on top, leaving some land unclaimed.
    ///
    /// Pipeline (see MAP_GENERATION.md):
    ///   1. Hex grid
    ///   2. Terrain (continent noise + falloff + biome classification)
    ///   3. Region seeds (spaced, on valid land)
    ///   4. Region growth (weighted multi-source BFS, terrain-aware, leaves unclaimed land)
    ///   5. Region stats (type + terrain composition + size + difficulty + seed)
    ///   6. Borders + neighbors
    ///
    /// Pure data: produces a <see cref="WorldMapData"/>. No GameObjects, no rendering.
    /// Fully deterministic for a given <see cref="MapGenerationSettings.seed"/>.
    /// </summary>
    public class WorldMapGenerator
    {
        static readonly string[] NamePool =
        {
            "Aurel", "Bastion", "Cindervale", "Dawnmere", "Ebon Reach", "Fallowcrown",
            "Greyfen", "Hearthspire", "Ironwood", "Jadewick", "Karth", "Lowspire",
            "Mourncoast", "Nacre", "Old Vey", "Pale Orchard", "Quietus", "Redmarsh",
            "Sablegate", "Tarnhold", "Umberfall", "Vigil", "Westwatch", "Yarrow",
            "Zeal Point", "Ashcourt", "Brinewall", "Cairnmarket", "Duskhollow", "Emberlea"
        };

        System.Random _rng;
        MapGenerationSettings _settings;
        WorldMapData _map;

        public WorldMapData Generate(MapGenerationSettings settings)
        {
            _settings = (settings ?? new MapGenerationSettings()).Clone();
            _settings.width = Mathf.Max(6, _settings.width);
            _settings.height = Mathf.Max(6, _settings.height);
            _rng = new System.Random(_settings.seed);

            _map = new WorldMapData
            {
                seed = _settings.seed,
                width = _settings.width,
                height = _settings.height,
                settings = _settings,
                generatedAtVersion = WorldMapVersion.Current,
            };

            GenerateGrid();
            GenerateTerrain();
            List<HexTileData> seeds = SelectRegionSeeds();
            GrowRegions(seeds);
            AssignRegionStats();
            CalculateBordersAndNeighbors();
            GenerateStates();

            return _map;
        }

        // ---------- Step 1: hex grid ----------
        void GenerateGrid()
        {
            int id = 0;
            for (int row = 0; row < _settings.height; row++)
            {
                for (int col = 0; col < _settings.width; col++)
                {
                    int q = col - (row >> 1); // offset -> axial (even rows)
                    int r = row;
                    var tile = new HexTileData(id++, new HexCoord(q, r));
                    _map.AddTile(tile);
                }
            }
        }

        // ---------- Step 2: terrain ----------
        void GenerateTerrain()
        {
            int w = _settings.width, h = _settings.height;
            float roughness = Mathf.Clamp01(_settings.terrainRoughness);

            // Seed-derived noise offsets so the same seed always yields the same world.
            float eo = (float)(_rng.NextDouble() * 1000.0);
            float mo = (float)(_rng.NextDouble() * 1000.0);
            float freq = Mathf.Max(0.02f, _settings.continentFrequency);

            // Build per-tile elevation/moisture in grid space (col,row), with continent falloff.
            var elevations = new List<float>(_map.TileCount);
            foreach (var tile in _map.Tiles)
            {
                // Recover grid col/row from axial for consistent noise sampling.
                int row = tile.coord.r;
                int col = tile.coord.q + (row >> 1);

                float nx = w > 1 ? (float)col / (w - 1) : 0.5f;
                float ny = h > 1 ? (float)row / (h - 1) : 0.5f;

                float e = OctaveNoise(col, row, freq, eo, 4, 0.5f + 0.2f * roughness);
                float m = OctaveNoise(col, row, freq * 1.4f, mo, 3, 0.5f);

                // Square radial falloff pushes the map edges toward sea -> continents.
                float dx = Mathf.Abs(nx * 2f - 1f);
                float dy = Mathf.Abs(ny * 2f - 1f);
                float d = Mathf.Max(dx, dy);
                float falloff = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.45f));
                e = Mathf.Clamp01(e - falloff * (0.85f - 0.2f * roughness));

                tile.elevation = e;
                tile.moisture = m;
                elevations.Add(e);
            }

            // Sea level chosen as the seaFraction-th percentile of elevation.
            elevations.Sort();
            float seaLevel = Percentile(elevations, Mathf.Clamp01(_settings.seaFraction));
            float deepLevel = Percentile(elevations, Mathf.Clamp01(_settings.seaFraction * 0.5f));
            // Mountain / hill thresholds among the land band.
            float mountainLevel = Percentile(elevations, Mathf.Clamp01(1f - 0.08f * (0.5f + roughness)));
            float hillLevel = Percentile(elevations, Mathf.Clamp01(1f - 0.22f * (0.5f + roughness)));

            // First pass: water vs land + elevation-based land terrain.
            foreach (var tile in _map.Tiles)
            {
                float e = tile.elevation;
                if (e <= seaLevel)
                {
                    tile.terrainType = e <= deepLevel ? TerrainType.DeepSea : TerrainType.Sea;
                }
                else if (e >= mountainLevel)
                {
                    tile.terrainType = TerrainType.Mountains;
                }
                else if (e >= hillLevel)
                {
                    tile.terrainType = TerrainType.Hills;
                }
                else
                {
                    float m = tile.moisture;
                    if (m > 0.66f) tile.terrainType = e < (seaLevel + hillLevel) * 0.5f ? TerrainType.Swamp : TerrainType.Forest;
                    else if (m < 0.32f) tile.terrainType = TerrainType.Desert;
                    else tile.terrainType = m > 0.5f ? TerrainType.Forest : TerrainType.Plains;
                }
            }

            // Second pass: coasts (land touching water), biomes, walkability.
            foreach (var tile in _map.Tiles)
            {
                bool isLand = !tile.IsWaterTerrain;
                if (isLand)
                {
                    foreach (var n in _map.GetNeighbors(tile))
                    {
                        if (n.IsWaterTerrain)
                        {
                            if (tile.terrainType == TerrainType.Plains ||
                                tile.terrainType == TerrainType.Desert ||
                                tile.terrainType == TerrainType.Forest)
                            {
                                tile.terrainType = TerrainType.Coast;
                            }
                            break;
                        }
                    }
                }
                else
                {
                    // Sea adjacent to land but currently DeepSea -> shallow Sea.
                    if (tile.terrainType == TerrainType.DeepSea)
                    {
                        foreach (var n in _map.GetNeighbors(tile))
                        {
                            if (!n.IsWaterTerrain) { tile.terrainType = TerrainType.Sea; break; }
                        }
                    }
                }

                ApplyTerrainProperties(tile);
                tile.biomeType = ClassifyBiome(tile);
            }

            // Third pass: scatter special non-region terrain (ruins/wasteland/sacred).
            ScatterSpecialFeatures(roughness);

            // Recompute properties for any tiles changed by scattering.
            foreach (var tile in _map.Tiles)
                ApplyTerrainProperties(tile);
        }

        void ApplyTerrainProperties(HexTileData tile)
        {
            TerrainDefinition def = MapDefinitions.GetTerrain(tile.terrainType);
            tile.isWalkable = def.isWalkable;
            tile.movementCost = def.movementCost;
            if (tile.terrainType == TerrainType.Ruins && !tile.specialFeatureTags.Contains("ruins"))
                tile.specialFeatureTags.Add("ruins");
            // developmentPotential: better on fertile, low elevation, walkable, non-water land.
            float fertility = tile.terrainType == TerrainType.Plains ? 1f
                : tile.terrainType == TerrainType.Coast ? 0.85f
                : tile.terrainType == TerrainType.Forest ? 0.6f
                : tile.terrainType == TerrainType.Hills ? 0.5f
                : tile.terrainType == TerrainType.SacredLand ? 0.55f
                : 0.2f;
            tile.developmentPotential = tile.IsWaterTerrain ? 0f : Mathf.Clamp01(fertility * (1f - tile.elevation * 0.4f));
            tile.dangerLevel = tile.terrainType == TerrainType.Wasteland ? 0.85f
                : tile.terrainType == TerrainType.Ruins ? 0.7f
                : tile.terrainType == TerrainType.Swamp ? 0.5f
                : tile.terrainType == TerrainType.Mountains ? 0.45f
                : tile.IsWaterTerrain ? 0.3f : 0.1f;
        }

        BiomeType ClassifyBiome(HexTileData tile)
        {
            if (tile.IsWaterTerrain) return BiomeType.Oceanic;
            switch (tile.terrainType)
            {
                case TerrainType.Mountains: return BiomeType.Alpine;
                case TerrainType.Desert: return BiomeType.Arid;
                case TerrainType.Swamp: return BiomeType.Tropical;
            }
            if (tile.moisture > 0.6f) return BiomeType.Boreal;
            return BiomeType.Temperate;
        }

        void ScatterSpecialFeatures(float roughness)
        {
            var landTiles = new List<HexTileData>();
            foreach (var tile in _map.Tiles)
                if (!tile.IsWaterTerrain && tile.terrainType != TerrainType.Mountains)
                    landTiles.Add(tile);
            landTiles.Sort((a, b) => a.tileId.CompareTo(b.tileId)); // stable before seeded shuffle

            int landCount = landTiles.Count;
            int ruins = Mathf.RoundToInt(landCount * 0.03f);
            int waste = Mathf.RoundToInt(landCount * 0.04f * (0.5f + roughness));
            int sacred = Mathf.RoundToInt(landCount * 0.02f);

            Shuffle(landTiles);
            int idx = 0;
            void Stamp(int count, TerrainType type)
            {
                for (int i = 0; i < count && idx < landTiles.Count; i++, idx++)
                    landTiles[idx].terrainType = type;
            }
            Stamp(ruins, TerrainType.Ruins);
            Stamp(waste, TerrainType.Wasteland);
            Stamp(sacred, TerrainType.SacredLand);
        }

        // ---------- Step 3: region seeds ----------
        List<HexTileData> SelectRegionSeeds()
        {
            var candidates = new List<HexTileData>();
            foreach (var tile in _map.Tiles)
            {
                TerrainDefinition def = MapDefinitions.GetTerrain(tile.terrainType);
                if (def.regionAllowed && def.canBeRegionSeed && tile.isWalkable)
                    candidates.Add(tile);
            }

            // Stable order before the seeded shuffle so the same seed yields the same world
            // regardless of dictionary enumeration order.
            candidates.Sort((a, b) => a.tileId.CompareTo(b.tileId));
            Shuffle(candidates);

            int target = Mathf.Clamp(_settings.targetRegionCount, 1, Mathf.Max(1, candidates.Count));
            // Keep seeds apart: spacing scales with map size / region count.
            float landSpan = Mathf.Sqrt(Mathf.Max(1, candidates.Count));
            int minSpacing = Mathf.Max(2, Mathf.RoundToInt(landSpan / Mathf.Sqrt(target) * 0.9f));

            var seeds = new List<HexTileData>();
            foreach (var candidate in candidates)
            {
                if (seeds.Count >= target) break;
                bool tooClose = false;
                foreach (var s in seeds)
                {
                    if (HexCoord.Distance(candidate.coord, s.coord) < minSpacing) { tooClose = true; break; }
                }
                if (!tooClose) seeds.Add(candidate);
            }

            // If spacing was too aggressive, relax and top up so we don't undershoot badly.
            int relaxIndex = 0;
            while (seeds.Count < target && relaxIndex < candidates.Count)
            {
                var c = candidates[relaxIndex++];
                if (!seeds.Contains(c)) seeds.Add(c);
            }
            return seeds;
        }

        // ---------- Step 4: region growth ----------
        struct Frontier
        {
            public int tileId;
            public string regionId;
        }

        void GrowRegions(List<HexTileData> seeds)
        {
            // Assign each seed a region with a type + size budget.
            var regionBudget = new Dictionary<string, int>();
            var regionTypeValues = (RegionType[])Enum.GetValues(typeof(RegionType));

            int claimableLand = 0;
            foreach (var tile in _map.Tiles)
                if (MapDefinitions.GetTerrain(tile.terrainType).regionAllowed && tile.isWalkable)
                    claimableLand++;

            float claimedFraction = Mathf.Clamp01(1f - _settings.unclaimedLandFraction);
            int maxClaimedTiles = Mathf.RoundToInt(claimableLand * claimedFraction);

            // Creation-order list drives all deterministic iteration below (never the dictionary).
            var orderedRegions = new List<RegionData>();
            for (int i = 0; i < seeds.Count; i++)
            {
                RegionType type = PickRegionTypeForTile(seeds[i], regionTypeValues);
                string id = "region_" + i;
                var region = new RegionData(id, MakeRegionName(i, type), type)
                {
                    capitalTileId = seeds[i].tileId,
                };
                _map.regionsById[id] = region;
                orderedRegions.Add(region);

                RegionTypeDefinition def = MapDefinitions.GetRegionType(type);
                int baseBudget = Mathf.Max(4, maxClaimedTiles / Mathf.Max(1, seeds.Count));
                int budget = Mathf.RoundToInt(baseBudget * Mathf.Clamp(def.sizePreference, 0.4f, 2f) * (0.8f + (float)_rng.NextDouble() * 0.6f));
                regionBudget[id] = Mathf.Max(3, budget);
            }

            // Weighted multi-source BFS (Dijkstra-like) so cheap terrain is claimed first
            // and regions form organic, irregular borders.
            var heap = new MinHeap<Frontier>();

            var regionClaimed = new Dictionary<string, int>();
            foreach (var region in orderedRegions)
            {
                HexTileData seedTile = _map.GetTile(region.capitalTileId);
                ClaimTile(seedTile, region);
                regionClaimed[region.regionId] = 1;
                PushNeighbors(heap, seedTile, region.regionId);
            }

            int claimedTotal = orderedRegions.Count; // seeds already claimed

            while (heap.Count > 0 && claimedTotal < maxClaimedTiles)
            {
                Frontier f = heap.Pop();
                HexTileData tile = _map.GetTile(f.tileId);
                if (tile == null || tile.HasRegion) continue;

                TerrainDefinition def = MapDefinitions.GetTerrain(tile.terrainType);
                if (!def.regionAllowed || !tile.isWalkable) continue;

                if (regionClaimed[f.regionId] >= regionBudget[f.regionId]) continue;

                RegionData region = _map.GetRegion(f.regionId);
                ClaimTile(tile, region);
                regionClaimed[f.regionId]++;
                claimedTotal++;
                PushNeighbors(heap, tile, f.regionId);
            }

            // Any land tile left without a region becomes intentionally unclaimed.
            _map.unclaimedTileIds.Clear();
            foreach (var tile in _map.Tiles)
            {
                if (!tile.HasRegion && !tile.IsWaterTerrain && tile.terrainType != TerrainType.Blocked)
                    _map.unclaimedTileIds.Add(tile.tileId);
            }

            // Drop any degenerate single-tile regions back to unclaimed (keeps "regions are multi-tile").
            var toRemove = new List<string>();
            foreach (var region in _map.Regions)
            {
                if (region.tileIds.Count < 2)
                {
                    foreach (int tid in region.tileIds)
                    {
                        HexTileData t = _map.GetTile(tid);
                        if (t != null)
                        {
                            t.regionId = null;
                            if (!t.IsWaterTerrain) _map.unclaimedTileIds.Add(tid);
                        }
                    }
                    toRemove.Add(region.regionId);
                }
            }
            foreach (string id in toRemove) _map.regionsById.Remove(id);
        }

        void PushNeighbors(MinHeap<Frontier> heap, HexTileData tile, string regionId)
        {
            foreach (var n in _map.GetNeighbors(tile))
            {
                if (n.HasRegion) continue;
                TerrainDefinition def = MapDefinitions.GetTerrain(n.terrainType);
                if (!def.regionAllowed || !n.isWalkable) continue;
                // Slight random jitter -> irregular, non-circular borders.
                float jitter = 0.85f + (float)_rng.NextDouble() * 0.4f;
                heap.Push(new Frontier { tileId = n.tileId, regionId = regionId }, def.regionGrowthCost * jitter + n.tileId * 1e-6f);
            }
        }

        void ClaimTile(HexTileData tile, RegionData region)
        {
            tile.regionId = region.regionId;
            region.tileIds.Add(tile.tileId);
        }

        RegionType PickRegionTypeForTile(HexTileData tile, RegionType[] all)
        {
            // Bias region type by the seed tile's terrain via preferredTerrains.
            string terrainId = tile.terrainType.ToString();
            var matches = new List<RegionType>();
            foreach (var type in all)
            {
                RegionTypeDefinition def = MapDefinitions.GetRegionType(type);
                if (def.preferredTerrains != null && def.preferredTerrains.Contains(terrainId))
                    matches.Add(type);
            }
            if (matches.Count > 0)
                return matches[_rng.Next(matches.Count)];
            return all[_rng.Next(all.Length)];
        }

        // ---------- Step 5: region stats ----------
        void AssignRegionStats()
        {
            DifficultyStatBias bias = DifficultyStatBias.For(_settings.difficultyId);

            // Deterministic iteration: capitalTileId is a unique, stable key per region, so the
            // sequence of RNG stat rolls is reproducible for a given seed.
            var ordered = new List<RegionData>(_map.regionsById.Values);
            ordered.Sort((a, b) => a.capitalTileId.CompareTo(b.capitalTileId));

            foreach (var region in ordered)
            {
                RegionTypeDefinition def = MapDefinitions.GetRegionType(region.regionType);

                int infl = def.influenceBase + RandVariance(def.statVariance);
                int stab = def.stabilityBase + RandVariance(def.statVariance);
                int dev = def.developmentBase + RandVariance(def.statVariance);

                // Terrain composition: average tile developmentPotential nudges development.
                float potentialSum = 0f;
                int count = 0;
                foreach (int tid in region.tileIds)
                {
                    HexTileData t = _map.GetTile(tid);
                    if (t != null) { potentialSum += t.developmentPotential; count++; }
                }
                float avgPotential = count > 0 ? potentialSum / count : 0.4f;
                dev += Mathf.RoundToInt((avgPotential - 0.4f) * 6f);

                // Size: bigger regions project more influence but are slightly less stable.
                int sizeBonus = Mathf.Clamp(region.TileCount - 6, -1, 2);
                infl += sizeBonus;
                stab -= Mathf.Max(0, sizeBonus / 2);

                // Capital terrain bonus.
                HexTileData cap = _map.GetTile(region.capitalTileId);
                if (cap != null)
                {
                    if (cap.terrainType == TerrainType.Coast) dev += 1;
                    if (cap.terrainType == TerrainType.SacredLand) stab += 1;
                    if (cap.terrainType == TerrainType.Hills) stab += 1;
                }

                // Difficulty bias.
                infl += bias.influence;
                stab += bias.stability;
                dev += bias.development;

                region.influence = infl;
                region.stability = stab;
                region.development = dev;
                region.ClampStats();

                region.population = Mathf.RoundToInt(region.TileCount * (40f + region.development * 5f) * 12f);
                region.wealth = Mathf.RoundToInt(region.development * 7.5f + region.TileCount * 3f);

                // Region tags inherit type tags + a couple feature tags from owned tiles.
                region.tags.Clear();
                if (def.tags != null) region.tags.AddRange(def.tags);
                if (cap != null && cap.specialFeatureTags.Contains("ruins") && !region.tags.Contains("ruins"))
                    region.tags.Add("ruins");
            }
        }

        // ---------- Step 6: borders + neighbors ----------
        void CalculateBordersAndNeighbors()
        {
            foreach (var region in _map.Regions)
            {
                region.borderTileIds.Clear();
                region.neighborRegionIds.Clear();
            }

            foreach (var region in _map.Regions)
            {
                var neighborSet = new HashSet<string>();
                foreach (int tid in region.tileIds)
                {
                    HexTileData tile = _map.GetTile(tid);
                    if (tile == null) continue;

                    bool isBorder = false;
                    foreach (var n in _map.GetNeighbors(tile))
                    {
                        if (n.regionId != region.regionId)
                        {
                            isBorder = true;
                            if (!string.IsNullOrEmpty(n.regionId))
                                neighborSet.Add(n.regionId);
                        }
                    }
                    // Tiles with fewer than 6 existing neighbors are on the map edge -> also border.
                    int existingNeighbors = 0;
                    foreach (var _ in _map.GetNeighbors(tile)) existingNeighbors++;
                    if (existingNeighbors < 6) isBorder = true;

                    if (isBorder) region.borderTileIds.Add(tid);
                }
                region.neighborRegionIds.AddRange(neighborSet);
            }
        }

        // ---------- Step 7: feudal states ----------
        static readonly string[] StateColors =
        {
            "#8B5CF6", // violet
            "#F5C542", // gold
            "#38BDF8", // teal/azure
            "#EF4444", // crimson
            "#22C55E", // emerald
            "#E07A5F", // terracotta
        };

        static readonly string[] StateNamePool =
        {
            "Arkanth", "Volmere", "Karsis", "Drevingard", "Lothria", "Severn",
            "Bryndolm", "Caelorn", "Mirovia", "Tagern", "Yssara", "Veldheim",
        };

        /// <summary>
        /// Clusters regions into 3-6 feudal states with a deterministic, contiguous (BFS/Voronoi over
        /// the region adjacency graph) partition seeded from the most-developed "capital" regions.
        /// </summary>
        void GenerateStates()
        {
            _map.statesById.Clear();
            if (_map.RegionCount == 0) return;

            // Stable region order.
            var regions = new List<RegionData>(_map.regionsById.Values);
            regions.Sort((a, b) => a.capitalTileId.CompareTo(b.capitalTileId));
            foreach (var r in regions) r.stateId = null;

            int stateCount = Mathf.Clamp(Mathf.RoundToInt(regions.Count / 5f), 3, 6);
            stateCount = Mathf.Min(stateCount, regions.Count);

            // Seeds = most-developed regions, spaced so two seeds aren't direct neighbors when possible.
            var byDev = new List<RegionData>(regions);
            byDev.Sort((a, b) => b.development != a.development
                ? b.development.CompareTo(a.development)
                : a.capitalTileId.CompareTo(b.capitalTileId));

            var seeds = new List<RegionData>();
            foreach (var cand in byDev)
            {
                if (seeds.Count >= stateCount) break;
                bool adjacent = false;
                foreach (var s in seeds)
                    if (s.neighborRegionIds.Contains(cand.regionId)) { adjacent = true; break; }
                if (!adjacent) seeds.Add(cand);
            }
            for (int i = 0; seeds.Count < stateCount && i < byDev.Count; i++)
                if (!seeds.Contains(byDev[i])) seeds.Add(byDev[i]);

            // Create states + claim seed regions.
            var queue = new Queue<RegionData>();
            for (int i = 0; i < seeds.Count; i++)
            {
                RegionData seed = seeds[i];
                string id = "state_" + i;
                var state = new StateData(id, MakeStateName(i, seed), StateColors[i % StateColors.Length])
                {
                    capitalRegionId = seed.regionId,
                };
                _map.statesById[id] = state;
                seed.stateId = id;
                state.regionIds.Add(seed.regionId);
                queue.Enqueue(seed);
            }

            // Multi-source BFS over region neighbors -> contiguous, balanced-ish clusters.
            while (queue.Count > 0)
            {
                RegionData current = queue.Dequeue();
                foreach (string nid in current.neighborRegionIds)
                {
                    RegionData neighbor = _map.GetRegion(nid);
                    if (neighbor == null || !string.IsNullOrEmpty(neighbor.stateId)) continue;
                    neighbor.stateId = current.stateId;
                    _map.statesById[current.stateId].regionIds.Add(neighbor.regionId);
                    queue.Enqueue(neighbor);
                }
            }

            // Disconnected regions: attach to the nearest seed by capital-tile distance.
            foreach (var region in regions)
            {
                if (!string.IsNullOrEmpty(region.stateId)) continue;
                StateData best = null;
                int bestDist = int.MaxValue;
                HexTileData regionCap = _map.GetTile(region.capitalTileId);
                foreach (var seed in seeds)
                {
                    HexTileData seedCap = _map.GetTile(seed.capitalTileId);
                    if (regionCap == null || seedCap == null) continue;
                    int dist = HexCoord.Distance(regionCap.coord, seedCap.coord);
                    if (dist < bestDist) { bestDist = dist; best = _map.GetState(seed.stateId); }
                }
                if (best == null) best = _map.GetState(seeds[0].stateId);
                region.stateId = best.stateId;
                best.regionIds.Add(region.regionId);
            }

            WorldStateUtil.RecomputeAll(_map);
        }

        string MakeStateName(int index, RegionData seed)
        {
            string baseName = StateNamePool[index % StateNamePool.Length];
            if (index >= StateNamePool.Length) baseName += " " + (index / StateNamePool.Length + 1);
            switch (seed.regionType)
            {
                case RegionType.TempleDomain: return "Temple Domain of " + baseName;
                case RegionType.KingdomHeartland: return "Kingdom of " + baseName;
                case RegionType.TradeBasin:
                case RegionType.CoastalLeague: return "Free Cities of " + baseName;
                case RegionType.MountainClans: return "Highland Clans of " + baseName;
                case RegionType.TribalConfederation: return "Confederation of " + baseName;
                default: return "Duchy of " + baseName;
            }
        }

        // ---------- helpers ----------
        struct DifficultyStatBias
        {
            public int influence, stability, development;
            public static DifficultyStatBias For(string id)
            {
                switch ((id ?? "Normal").Trim().ToLowerInvariant())
                {
                    case "easy":   return new DifficultyStatBias { influence = -1, stability = 1, development = 1 };
                    case "hard":   return new DifficultyStatBias { influence = 2, stability = -1, development = -1 };
                    case "custom":
                    case "normal":
                    default:        return new DifficultyStatBias { influence = 0, stability = 0, development = 0 };
                }
            }
        }

        int RandVariance(int variance)
        {
            if (variance <= 0) return 0;
            return _rng.Next(-variance, variance + 1);
        }

        string MakeRegionName(int index, RegionType type)
        {
            string baseName = NamePool[index % NamePool.Length];
            if (index >= NamePool.Length) baseName += " " + (index / NamePool.Length + 1);
            switch (type)
            {
                case RegionType.RuinedZone: return "Ruins of " + baseName;
                case RegionType.TradeBasin: return baseName + " Exchange";
                case RegionType.TempleDomain: return baseName + " Sanctum";
                case RegionType.CoastalLeague: return baseName + " League";
                case RegionType.MountainClans: return baseName + " Clans";
                case RegionType.KingdomHeartland: return "Crown of " + baseName;
                default: return baseName;
            }
        }

        float OctaveNoise(int x, int y, float freq, float offset, int octaves, float persistence)
        {
            float total = 0f, amplitude = 1f, maxValue = 0f, f = freq;
            for (int i = 0; i < octaves; i++)
            {
                total += Mathf.PerlinNoise(x * f + offset, y * f + offset) * amplitude;
                maxValue += amplitude;
                amplitude *= persistence;
                f *= 2f;
            }
            return maxValue > 0f ? total / maxValue : 0f;
        }

        static float Percentile(List<float> sorted, float p)
        {
            if (sorted.Count == 0) return 0f;
            int idx = Mathf.Clamp(Mathf.RoundToInt(p * (sorted.Count - 1)), 0, sorted.Count - 1);
            return sorted[idx];
        }

        void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
