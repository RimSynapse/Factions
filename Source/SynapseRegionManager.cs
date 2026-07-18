using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace RimSynapse.Factions
{
    public class SynapseRegionManager : WorldComponent
    {
        private List<GeographicProvince> provinces = new List<GeographicProvince>();
        private int[] tileToProvinceId;

        public List<GeographicProvince> Provinces
        {
            get
            {
                if (provinces == null || provinces.Count == 0)
                {
                    GenerateProvinces();
                }
                return provinces;
            }
        }

        public SynapseRegionManager(World world) : base(world)
        {
            InitializeData();
        }

        private void InitializeData()
        {
            if (tileToProvinceId == null && Find.WorldGrid != null)
            {
                tileToProvinceId = new int[Find.WorldGrid.TilesCount];
                for (int i = 0; i < tileToProvinceId.Length; i++)
                {
                    tileToProvinceId[i] = -1;
                }
            }
        }

        public int GetProvinceId(int tileId)
        {
            InitializeData();
            if (tileId < 0 || tileId >= tileToProvinceId.Length) return -1;
            return tileToProvinceId[tileId];
        }

        public GeographicProvince GetProvince(int provinceId)
        {
            return provinces.FirstOrDefault(p => p.id == provinceId);
        }

        public GeographicProvince GetProvinceForTile(int tileId)
        {
            int pid = GetProvinceId(tileId);
            if (pid == -1) return null;
            return GetProvince(pid);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref provinces, "provinces", LookMode.Deep);
            if (provinces == null)
            {
                provinces = new List<GeographicProvince>();
            }

            List<int> tempList = null;
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                if (tileToProvinceId != null)
                {
                    tempList = tileToProvinceId.ToList();
                }
            }
            Scribe_Collections.Look(ref tempList, "tileToProvinceId", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (tempList != null && Find.WorldGrid != null)
                {
                    tileToProvinceId = tempList.ToArray();
                }
                else
                {
                    InitializeData();
                }
            }
        }

        public void GenerateProvinces()
        {
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Generating Geographic Domains...", "factions");

            if (Find.WorldGrid == null) return;
            int totalTiles = Find.WorldGrid.TilesCount;
            tileToProvinceId = new int[totalTiles];
            for (int i = 0; i < totalTiles; i++)
            {
                tileToProvinceId[i] = -1;
            }

            provinces.Clear();
            
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Starting GetBiomeChunks...", "factions");
            List<List<int>> biomeChunks = GetBiomeChunks();
            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Finished GetBiomeChunks. Found {biomeChunks.Count} chunks.", "factions");
            
            int provinceIdCounter = 0;

            // Pass 1: Build initial partitions based on biome size and natural features presence
            int baseMin = FactionPlacementSettings.minRegionSize;
            int baseMax = FactionPlacementSettings.maxRegionSize;

            // Offset parameters to match user rules:
            // - With natural features: min 70, max 180 (grow to fit)
            // - Without natural features: min 80, max 160
            int minWithFeatures = baseMin - 5;
            int minNoFeatures = baseMin + 5;
            int maxWithFeatures = baseMax + 30;
            int maxNoFeatures = baseMax + 10;

            foreach (var chunk in biomeChunks)
            {
                bool hasFeatures = ChunkHasNaturalFeatures(chunk);
                int maxAllowed = hasFeatures ? maxWithFeatures : maxNoFeatures;

                if (chunk.Count <= maxAllowed)
                {
                    GeographicProvince domain = new GeographicProvince(provinceIdCounter);
                    domain.tiles = chunk.ToList();
                    Tile sampleTile = Find.WorldGrid[chunk[0]];
                    domain.primaryBiome = sampleTile.PrimaryBiome;
                    domain.name = GenerateProvinceName(provinceIdCounter, sampleTile.PrimaryBiome);
                    
                    foreach (int tileId in chunk)
                    {
                        tileToProvinceId[tileId] = provinceIdCounter;
                    }
                    provinces.Add(domain);
                    provinceIdCounter++;
                }
                else
                {
                    RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Splitting chunk of size {chunk.Count}...", "factions");
                    List<List<int>> subPockets = SplitChunkByVoronoi(chunk);
                    RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Split chunk into {subPockets.Count} subpockets.", "factions");
                    
                    foreach (var pocket in subPockets)
                    {
                        if (pocket.Count == 0) continue;

                        GeographicProvince domain = new GeographicProvince(provinceIdCounter);
                        domain.tiles = pocket.ToList();
                        Tile sampleTile = Find.WorldGrid[pocket[0]];
                        domain.primaryBiome = sampleTile.PrimaryBiome;
                        domain.name = GenerateProvinceName(provinceIdCounter, sampleTile.PrimaryBiome);

                        foreach (int tileId in pocket)
                        {
                            tileToProvinceId[tileId] = provinceIdCounter;
                        }
                        provinces.Add(domain);
                        provinceIdCounter++;
                    }
                }
            }

            // Pass 2: Adjust regions via merging, using custom min size rules based on natural boundaries
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Starting MergeTinyDomains...", "factions");
            MergeTinyDomains(minWithFeatures, minNoFeatures);
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Finished MergeTinyDomains.", "factions");

            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Generated {provinces.Count} Geographic Domains.", "factions");
        }

        private bool ChunkHasNaturalFeatures(List<int> chunk)
        {
            HashSet<int> chunkSet = new HashSet<int>(chunk);
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();

            foreach (int tile in chunk)
            {
                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(tile, neighbors);
                foreach (var n in neighbors)
                {
                    int neighborId = n.tileId;
                    if (chunkSet.Contains(neighborId))
                    {
                        if (Find.WorldGrid.GetRiverDef(tile, neighborId) != null || Find.WorldGrid.GetRiverDef(neighborId, tile) != null)
                        {
                            return true;
                        }
                        if (Find.WorldGrid[tile].hilliness == Hilliness.Mountainous || Find.WorldGrid[neighborId].hilliness == Hilliness.Mountainous)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool RegionHasNaturalBoundaries(GeographicProvince p)
        {
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
            foreach (int tile in p.tiles)
            {
                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(tile, neighbors);
                foreach (var n in neighbors)
                {
                    int neighborId = n.tileId;
                    int neighborProvinceId = GetProvinceId(neighborId);
                    if (neighborProvinceId != -1 && neighborProvinceId != p.id)
                    {
                        bool crossesRiver = Find.WorldGrid.GetRiverDef(tile, neighborId) != null || Find.WorldGrid.GetRiverDef(neighborId, tile) != null;
                        bool crossesMountain = Find.WorldGrid[tile].hilliness == Hilliness.Mountainous || Find.WorldGrid[neighborId].hilliness == Hilliness.Mountainous;
                        if (crossesRiver || crossesMountain)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private List<List<int>> GetBiomeChunks()
        {
            int totalTiles = Find.WorldGrid.TilesCount;
            bool[] visited = new bool[totalTiles];
            List<List<int>> chunks = new List<List<int>>();

            for (int t = 0; t < totalTiles; t++)
            {
                Tile tileData = Find.WorldGrid[t];
                if (tileData.WaterCovered || tileData.hilliness == Hilliness.Impassable || (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable))
                {
                    continue;
                }

                if (visited[t]) continue;

                List<int> chunk = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(t);
                visited[t] = true;

                List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    chunk.Add(current);

                    Tile currentData = Find.WorldGrid[current];

                    neighbors.Clear();
                    Find.WorldGrid.GetTileNeighbors(current, neighbors);

                    foreach (var n in neighbors)
                    {
                        int neighborId = n.tileId;
                        if (visited[neighborId]) continue;

                        Tile neighborData = Find.WorldGrid[neighborId];

                        if (neighborData.WaterCovered || neighborData.hilliness == Hilliness.Impassable || (neighborData.PrimaryBiome != null && neighborData.PrimaryBiome.impassable))
                        {
                            continue;
                        }

                        if (currentData.PrimaryBiome != neighborData.PrimaryBiome)
                        {
                            continue;
                        }

                        visited[neighborId] = true;
                        queue.Enqueue(neighborId);
                    }
                }

                if (chunk.Count > 0)
                {
                    chunks.Add(chunk);
                }
            }

            return chunks;
        }

        private List<List<int>> SplitChunkByVoronoi(List<int> chunk)
        {
            HashSet<int> chunkSet = new HashSet<int>(chunk);
            int size = chunk.Count;

            float targetSize = (FactionPlacementSettings.minRegionSize + FactionPlacementSettings.maxRegionSize) / 2f;
            int k = Mathf.CeilToInt((float)size / targetSize);
            if (k < 2) k = 2;

            List<int> seeds = new List<int>();
            
            int step = Mathf.Max(1, size / k);
            for (int i = 0; i < size; i += step)
            {
                int tile = chunk[i];
                bool tooClose = false;
                foreach (int seed in seeds)
                {
                    if (Find.WorldGrid.ApproxDistanceInTiles(tile, seed) < 8f)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (!tooClose)
                {
                    seeds.Add(tile);
                }
                if (seeds.Count >= k) break;
            }

            if (seeds.Count < k)
            {
                foreach (int tile in chunk)
                {
                    if (!seeds.Contains(tile))
                    {
                        seeds.Add(tile);
                    }
                    if (seeds.Count >= k) break;
                }
            }

            var tileToSeed = new Dictionary<int, int>();
            var minCosts = new Dictionary<int, float>();
            var pq = new SimplePriorityQueue<int>();

            foreach (int seed in seeds)
            {
                minCosts[seed] = 0f;
                tileToSeed[seed] = seed;
                pq.Enqueue(seed, 0f);
            }

            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();

            while (pq.Count > 0)
            {
                int current = pq.Dequeue();
                float currentCost = minCosts[current];
                int seed = tileToSeed[current];

                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(current, neighbors);

                foreach (var n in neighbors)
                {
                    int neighborId = n.tileId;
                    if (!chunkSet.Contains(neighborId)) continue;

                    Tile currentData = Find.WorldGrid[current];
                    Tile neighborData = Find.WorldGrid[neighborId];

                    float stepCost = 1.0f;

                    if (currentData.hilliness == Hilliness.Mountainous || currentData.hilliness == Hilliness.LargeHills ||
                        neighborData.hilliness == Hilliness.Mountainous || neighborData.hilliness == Hilliness.LargeHills)
                    {
                        stepCost += 100f;
                    }

                    if (Find.WorldGrid.GetRiverDef(current, neighborId) != null || Find.WorldGrid.GetRiverDef(neighborId, current) != null)
                    {
                        stepCost += 100f;
                    }

                    float newCost = currentCost + stepCost;

                    if (!minCosts.TryGetValue(neighborId, out float existingCost) || newCost < existingCost)
                    {
                        minCosts[neighborId] = newCost;
                        tileToSeed[neighborId] = seed;
                        pq.Enqueue(neighborId, newCost);
                    }
                }
            }

            var groups = new Dictionary<int, List<int>>();
            foreach (int seed in seeds)
            {
                groups[seed] = new List<int>();
            }

            foreach (int tile in chunk)
            {
                if (tileToSeed.TryGetValue(tile, out int seed))
                {
                    groups[seed].Add(tile);
                }
                else
                {
                    groups[seeds[0]].Add(tile);
                }
            }

            return groups.Values.ToList();
        }

        private void MergeTinyDomains(int minWithFeatures, int minNoFeatures)
        {
            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] MergeTinyDomains started. Initial region count: {provinces.Count}", "factions");
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
            int iterations = 0;

            while (true)
            {
                GeographicProvince tinyDomain = null;
                GeographicProvince bestNeighbor = null;

                foreach (var p in provinces)
                {
                    int pSize = p.tiles.Count;
                    bool hasBoundaries = RegionHasNaturalBoundaries(p);
                    int minSize = hasBoundaries ? minWithFeatures : minNoFeatures;

                    if (pSize >= minSize) continue;

                    // Verify if this tiny pocket can merge with a neighbor of the same biome WITHOUT crossing a river/mountain
                    bool canMergeWithoutCrossing = false;
                    Dictionary<int, int> neighborBorders = new Dictionary<int, int>();

                    foreach (int tile in p.tiles)
                    {
                        neighbors.Clear();
                        Find.WorldGrid.GetTileNeighbors(tile, neighbors);
                        foreach (var n in neighbors)
                        {
                            int neighborId = n.tileId;
                            int neighborProvinceId = GetProvinceId(neighborId);
                            if (neighborProvinceId != -1 && neighborProvinceId != p.id)
                            {
                                bool crossesRiver = Find.WorldGrid.GetRiverDef(tile, neighborId) != null || Find.WorldGrid.GetRiverDef(neighborId, tile) != null;
                                bool crossesMountain = Find.WorldGrid[tile].hilliness == Hilliness.Mountainous || Find.WorldGrid[neighborId].hilliness == Hilliness.Mountainous;

                                if (!crossesRiver && !crossesMountain)
                                {
                                    canMergeWithoutCrossing = true;
                                }

                                int weight = 10;
                                if (crossesRiver || crossesMountain)
                                {
                                    weight = 1; // Heavy penalty for crossing river/mountain
                                }

                                if (!neighborBorders.ContainsKey(neighborProvinceId))
                                {
                                    neighborBorders[neighborProvinceId] = 0;
                                }
                                neighborBorders[neighborProvinceId] += weight;
                            }
                        }
                    }

                    // If it is bounded by natural features (cannot merge without crossing) and is at least minWithFeatures,
                    // we preserve it rather than forcing it to merge across the river.
                    if (pSize >= minWithFeatures && !canMergeWithoutCrossing)
                    {
                        if (iterations == 0)
                        {
                            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Preserved natural pocket: Region {p.id} ({p.name}), Size: {pSize} (Threshold: {minSize}), Biome: {p.primaryBiome?.defName}, Reason: Bounded by natural features.", "factions");
                        }
                        continue;
                    }

                    if (neighborBorders.Any())
                    {
                        int bestNeighborId = neighborBorders.OrderByDescending(kv => kv.Value).First().Key;
                        GeographicProvince neighborProvince = GetProvince(bestNeighborId);
                        if (neighborProvince != null)
                        {
                            tinyDomain = p;
                            bestNeighbor = neighborProvince;
                            break;
                        }
                    }
                }

                if (tinyDomain == null || bestNeighbor == null)
                {
                    break;
                }

                RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Merging tiny Region {tinyDomain.id} ({tinyDomain.name}, size {tinyDomain.tiles.Count}) into adjacent Region {bestNeighbor.id} ({bestNeighbor.name}, size {bestNeighbor.tiles.Count}).", "factions");

                foreach (int tileId in tinyDomain.tiles)
                {
                    bestNeighbor.tiles.Add(tileId);
                    tileToProvinceId[tileId] = bestNeighbor.id;
                }

                provinces.Remove(tinyDomain);
                iterations++;
            }

            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] MergeTinyDomains finished. Merging adjusted {iterations} regions. Final region count: {provinces.Count}", "factions");
        }

        private string GenerateProvinceName(int provinceId, BiomeDef biome)
        {
            string baseName = "Region " + provinceId;
            if (biome != null)
            {
                string biomeLabel = biome.LabelCap;
                if (biomeLabel.Contains("forest") || biomeLabel.Contains("Forest"))
                {
                    baseName = "Woodland Region " + provinceId;
                }
                else if (biomeLabel.Contains("desert") || biomeLabel.Contains("Desert"))
                {
                    baseName = "Desert Region " + provinceId;
                }
                else if (biomeLabel.Contains("tundra") || biomeLabel.Contains("Tundra"))
                {
                    baseName = "Tundra Region " + provinceId;
                }
                else
                {
                    baseName = biomeLabel + " Region " + provinceId;
                }
            }
            return baseName;
        }

        public void RecalculateProvinceOwners()
        {
            if (Find.WorldObjects == null) return;

            foreach (var province in provinces)
            {
                province.owningFactionIds.Clear();
            }

            var settlements = Find.WorldObjects.Settlements;
            if (settlements == null) return;

            foreach (var s in settlements)
            {
                if (s.Faction != null)
                {
                    GeographicProvince province = GetProvinceForTile(s.Tile);
                    if (province != null)
                    {
                        string fid = s.Faction.GetUniqueLoadID();
                        if (!province.owningFactionIds.Contains(fid))
                        {
                            province.owningFactionIds.Add(fid);
                        }
                    }
                }
            }
        }
    }
}
