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

        private bool HasRiver(int tileId)
        {
            if (Find.WorldGrid == null) return false;
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(tileId, neighbors);
            foreach (var n in neighbors)
            {
                if (Find.WorldGrid.GetRiverDef(tileId, n.tileId) != null || Find.WorldGrid.GetRiverDef(n.tileId, tileId) != null)
                {
                    return true;
                }
            }
            return false;
        }

        private BiomeDef GetPrimaryBiome(List<int> chunk)
        {
            if (chunk == null || chunk.Count == 0) return null;
            return chunk
                .Select(t => Find.WorldGrid[t].PrimaryBiome)
                .Where(b => b != null)
                .GroupBy(b => b)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault();
        }

        public void GenerateProvinces()
        {
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Generating Geographic Domains (Boundary-First Priority)...", "factions");

            if (Find.WorldGrid == null) return;
            int totalTiles = Find.WorldGrid.TilesCount;
            tileToProvinceId = new int[totalTiles];
            for (int i = 0; i < totalTiles; i++)
            {
                tileToProvinceId[i] = -1;
            }

            provinces.Clear();
            int provinceIdCounter = 0;

            // Pre-calculate river neighbor count for all tiles
            int[] riverNeighborCount = new int[totalTiles];
            List<RimWorld.Planet.PlanetTile> tempNeighbors = new List<RimWorld.Planet.PlanetTile>();
            for (int i = 0; i < totalTiles; i++)
            {
                if (tileToProvinceId[i] != -1 || !HasRiver(i)) continue;

                int count = 0;
                tempNeighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(i, tempNeighbors);
                foreach (var n in tempNeighbors)
                {
                    if (HasRiver(n.tileId) && (Find.WorldGrid.GetRiverDef(i, n.tileId) != null || Find.WorldGrid.GetRiverDef(n.tileId, i) != null))
                    {
                        count++;
                    }
                }
                riverNeighborCount[i] = count;
            }

            // Phase 2: Rivers (tiles with river def, split at forks)
            bool[] riverVisited = new bool[totalTiles];

            // Pass A: Seed only from non-fork tiles (degree < 3) to build segments outwards
            for (int i = 0; i < totalTiles; i++)
            {
                if (tileToProvinceId[i] != -1 || riverVisited[i] || !HasRiver(i)) continue;
                if (riverNeighborCount[i] >= 3) continue; // Skip forks in first seed pass

                List<int> riverSegment = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                riverVisited[i] = true;

                List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    riverSegment.Add(current);

                    // If this tile is a fork, do not propagate past it (act as boundary point)
                    if (riverNeighborCount[current] >= 3) continue;

                    neighbors.Clear();
                    Find.WorldGrid.GetTileNeighbors(current, neighbors);
                    foreach (var n in neighbors)
                    {
                        int nid = n.tileId;
                        if (tileToProvinceId[nid] == -1 && !riverVisited[nid] && HasRiver(nid) && 
                            (Find.WorldGrid.GetRiverDef(current, nid) != null || Find.WorldGrid.GetRiverDef(nid, current) != null))
                        {
                            riverVisited[nid] = true;
                            queue.Enqueue(nid);
                        }
                    }
                }

                if (riverSegment.Count > 0)
                {
                    GeographicProvince domain = new GeographicProvince(provinceIdCounter);
                    domain.tiles = riverSegment.ToList();
                    domain.provinceType = ProvinceType.River;
                    domain.primaryBiome = GetPrimaryBiome(riverSegment);
                    domain.name = GenerateProvinceName(provinceIdCounter, domain.primaryBiome, domain.provinceType);

                    foreach (int tileId in riverSegment)
                    {
                        tileToProvinceId[tileId] = provinceIdCounter;
                    }
                    provinces.Add(domain);
                    provinceIdCounter++;
                }
            }

            // Pass B: Collect any remaining/isolated fork tiles
            for (int i = 0; i < totalTiles; i++)
            {
                if (tileToProvinceId[i] != -1 || riverVisited[i] || !HasRiver(i)) continue;

                List<int> riverSegment = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                riverVisited[i] = true;

                List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    riverSegment.Add(current);

                    neighbors.Clear();
                    Find.WorldGrid.GetTileNeighbors(current, neighbors);
                    foreach (var n in neighbors)
                    {
                        int nid = n.tileId;
                        if (tileToProvinceId[nid] == -1 && !riverVisited[nid] && HasRiver(nid) && 
                            (Find.WorldGrid.GetRiverDef(current, nid) != null || Find.WorldGrid.GetRiverDef(nid, current) != null))
                        {
                            riverVisited[nid] = true;
                            queue.Enqueue(nid);
                        }
                    }
                }

                if (riverSegment.Count > 0)
                {
                    GeographicProvince domain = new GeographicProvince(provinceIdCounter);
                    domain.tiles = riverSegment.ToList();
                    domain.provinceType = ProvinceType.River;
                    domain.primaryBiome = GetPrimaryBiome(riverSegment);
                    domain.name = GenerateProvinceName(provinceIdCounter, domain.primaryBiome, domain.provinceType);

                    foreach (int tileId in riverSegment)
                    {
                        tileToProvinceId[tileId] = provinceIdCounter;
                    }
                    provinces.Add(domain);
                    provinceIdCounter++;
                }
            }

            // Phase 4: Land Valleys (remaining hospitable land tiles, excluding WaterCovered and Impassable ones)
            bool[] landVisited = new bool[totalTiles];
            int baseMin = FactionPlacementSettings.minRegionSize;
            int baseMax = FactionPlacementSettings.maxRegionSize;

            int minWithFeatures = baseMin - 5;
            int minNoFeatures = baseMin + 5;
            int maxWithFeatures = baseMax + 30;
            int maxNoFeatures = baseMax + 10;

            for (int i = 0; i < totalTiles; i++)
            {
                Tile tileData = Find.WorldGrid[i];
                if (tileToProvinceId[i] != -1 || landVisited[i] || tileData.hilliness == Hilliness.Impassable || (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable)) continue;

                List<int> landPocket = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                landVisited[i] = true;

                List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    landPocket.Add(current);

                    neighbors.Clear();
                    Find.WorldGrid.GetTileNeighbors(current, neighbors);
                    foreach (var n in neighbors)
                    {
                        int nid = n.tileId;
                        Tile neighborData = Find.WorldGrid[nid];
                        if (tileToProvinceId[nid] == -1 && !landVisited[nid] && neighborData.hilliness != Hilliness.Impassable && (neighborData.PrimaryBiome == null || !neighborData.PrimaryBiome.impassable))
                        {
                            landVisited[nid] = true;
                            queue.Enqueue(nid);
                        }
                    }
                }

                if (landPocket.Count == 0) continue;

                bool hasFeatures = ChunkHasNaturalFeatures(landPocket);
                int maxAllowed = hasFeatures ? maxWithFeatures : maxNoFeatures;

                if (landPocket.Count <= maxAllowed)
                {
                    GeographicProvince domain = new GeographicProvince(provinceIdCounter);
                    domain.tiles = landPocket.ToList();
                    domain.provinceType = ProvinceType.Land;
                    domain.primaryBiome = GetPrimaryBiome(landPocket);
                    domain.name = GenerateProvinceName(provinceIdCounter, domain.primaryBiome, domain.provinceType);

                    foreach (int tileId in landPocket)
                    {
                        tileToProvinceId[tileId] = provinceIdCounter;
                    }
                    provinces.Add(domain);
                    provinceIdCounter++;
                }
                else
                {
                    List<List<int>> subPockets = SplitChunkByVoronoi(landPocket);
                    foreach (var pocket in subPockets)
                    {
                        if (pocket.Count == 0) continue;

                        GeographicProvince domain = new GeographicProvince(provinceIdCounter);
                        domain.tiles = pocket.ToList();
                        domain.provinceType = ProvinceType.Land;
                        domain.primaryBiome = GetPrimaryBiome(pocket);
                        domain.name = GenerateProvinceName(provinceIdCounter, domain.primaryBiome, domain.provinceType);

                        foreach (int tileId in pocket)
                        {
                            tileToProvinceId[tileId] = provinceIdCounter;
                        }
                        provinces.Add(domain);
                        provinceIdCounter++;
                    }
                }
            }
            // Phase 4.5: River Segmentation & Connection (Absorb rivers and small lakes into bordering Land/Ocean/Lake provinces)
            List<GeographicProvince> finalProvinces = provinces.Where(p => p.provinceType != ProvinceType.River).ToList();
            List<GeographicProvince> riverProvinces = provinces.Where(p => p.provinceType == ProvinceType.River).ToList();

            // Cache province types for O(1) lookups
            Dictionary<int, ProvinceType> provinceTypeMap = provinces.ToDictionary(p => p.id, p => p.provinceType);

            Dictionary<int, int> resolvedRiverTiles = new Dictionary<int, int>();
            List<int> unresolvedRiverTiles = new List<int>();

            foreach (var rp in riverProvinces)
            {
                foreach (int t in rp.tiles)
                {
                    unresolvedRiverTiles.Add(t);
                }
            }

            // Find all contiguous water bodies that are small (< 25 tiles) and mark them for absorption
            bool[] waterVisited = new bool[totalTiles];
            for (int i = 0; i < totalTiles; i++)
            {
                Tile tileData = Find.WorldGrid[i];
                if (!tileData.WaterCovered || waterVisited[i]) continue;

                List<int> waterBody = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(i);
                waterVisited[i] = true;

                List<RimWorld.Planet.PlanetTile> subNeighbors = new List<RimWorld.Planet.PlanetTile>();
                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    waterBody.Add(current);

                    subNeighbors.Clear();
                    Find.WorldGrid.GetTileNeighbors(current, subNeighbors);
                    foreach (var n in subNeighbors)
                    {
                        if (!waterVisited[n.tileId] && Find.WorldGrid[n.tileId].WaterCovered)
                        {
                            waterVisited[n.tileId] = true;
                            queue.Enqueue(n.tileId);
                        }
                    }
                }

                if (waterBody.Count < 25)
                {
                    foreach (int tileId in waterBody)
                    {
                        unresolvedRiverTiles.Add(tileId);
                    }
                }
            }

            List<RimWorld.Planet.PlanetTile> connectNeighbors = new List<RimWorld.Planet.PlanetTile>();
            int connectSafety = 0;
            while (unresolvedRiverTiles.Count > 0 && connectSafety < 10)
            {
                connectSafety++;
                List<int> nextUnresolved = new List<int>();

                foreach (int t in unresolvedRiverTiles)
                {
                    connectNeighbors.Clear();
                    Find.WorldGrid.GetTileNeighbors(t, connectNeighbors);

                    int bestProvinceId = -1;
                    Dictionary<int, int> provWeights = new Dictionary<int, int>();

                    foreach (var n in connectNeighbors)
                    {
                        int nid = n.tileId;
                        int pid = tileToProvinceId[nid];
                        if (pid != -1)
                        {
                            if (provinceTypeMap.TryGetValue(pid, out var type) && type != ProvinceType.River)
                            {
                                if (!provWeights.ContainsKey(pid)) provWeights[pid] = 0;
                                provWeights[pid]++;
                            }
                        }
                    }

                    if (provWeights.Any())
                    {
                        bestProvinceId = provWeights.OrderByDescending(kv => kv.Value).First().Key;
                    }

                    if (bestProvinceId != -1)
                    {
                        resolvedRiverTiles[t] = bestProvinceId;
                        // Also update the mapped type so that subsequent iterations can see it
                        provinceTypeMap[t] = provinceTypeMap[bestProvinceId];
                    }
                    else
                    {
                        nextUnresolved.Add(t);
                    }
                }

                if (nextUnresolved.Count == unresolvedRiverTiles.Count)
                {
                    // Force assign remaining unresolved tiles to any adjacent non-river province
                    foreach (int t in nextUnresolved)
                    {
                        connectNeighbors.Clear();
                        Find.WorldGrid.GetTileNeighbors(t, connectNeighbors);
                        foreach (var n in connectNeighbors)
                        {
                            int pid = tileToProvinceId[n.tileId];
                            if (pid != -1)
                            {
                                if (provinceTypeMap.TryGetValue(pid, out var type) && type != ProvinceType.River)
                                {
                                    resolvedRiverTiles[t] = pid;
                                    break;
                                }
                            }
                        }
                    }
                    break;
                }

                unresolvedRiverTiles = nextUnresolved;
            }

            // Apply river tile absorption
            foreach (var kvp in resolvedRiverTiles)
            {
                int tileId = kvp.Key;
                int destProvinceId = kvp.Value;
                var destProv = finalProvinces.FirstOrDefault(p => p.id == destProvinceId);
                if (destProv != null)
                {
                    destProv.tiles.Add(tileId);
                    tileToProvinceId[tileId] = destProvinceId;
                }
            }

            provinces = finalProvinces;

            // Phase 5: Consolidation & Merging (Pass 2)
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Starting MergeTinyDomains...", "factions");
            MergeTinyDomains(minWithFeatures, minNoFeatures);
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Finished MergeTinyDomains.", "factions");

            // Naming Phase: Contextual Name Resolution
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Running contextual province naming...", "factions");
            ResolveContextualNames();

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

        private List<List<int>> GetNaturalBasins()
        {
            int totalTiles = Find.WorldGrid.TilesCount;
            bool[] visited = new bool[totalTiles];
            List<List<int>> basins = new List<List<int>>();

            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();

            for (int t = 0; t < totalTiles; t++)
            {
                Tile tileData = Find.WorldGrid[t];
                if (tileData.WaterCovered || tileData.hilliness == Hilliness.Impassable || (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable))
                {
                    continue;
                }

                if (visited[t]) continue;

                List<int> basin = new List<int>();
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(t);
                visited[t] = true;

                while (queue.Count > 0)
                {
                    int current = queue.Dequeue();
                    basin.Add(current);

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

                        // Do not cross river or mountain barriers during the initial basin flood fill
                        if (IsBoundary(current, neighborId))
                        {
                            continue;
                        }

                        visited[neighborId] = true;
                        queue.Enqueue(neighborId);
                    }
                }

                if (basin.Count > 0)
                {
                    basins.Add(basin);
                }
            }

            return basins;
        }

        private bool IsBoundary(int tileA, int tileB)
        {
            if (Find.WorldGrid == null) return false;

            // River check: Rivers act as primary boundaries
            if (Find.WorldGrid.GetRiverDef(tileA, tileB) != null || Find.WorldGrid.GetRiverDef(tileB, tileA) != null)
            {
                return true;
            }

            return false;
        }

        private List<List<int>> SplitChunkByVoronoi(List<int> chunk)
        {
            HashSet<int> chunkSet = new HashSet<int>(chunk);
            int size = chunk.Count;

            float targetSize = (FactionPlacementSettings.minRegionSize + FactionPlacementSettings.maxRegionSize) / 2f;
            int k = Mathf.CeilToInt((float)size / targetSize);
            if (k < 2) k = 2;

            List<int> seeds = new List<int>();
            if (k > 0 && chunk.Count > 0)
            {
                seeds.Add(chunk[0]);

                while (seeds.Count < k)
                {
                    int bestTile = -1;
                    float maxMinDist = -1f;

                    int sampleStep = Mathf.Max(1, chunk.Count / 300);
                    for (int i = 0; i < chunk.Count; i += sampleStep)
                    {
                        int tile = chunk[i];
                        if (seeds.Contains(tile)) continue;

                        float minDist = float.MaxValue;
                        foreach (int seed in seeds)
                        {
                            float dist = Find.WorldGrid.ApproxDistanceInTiles(tile, seed);
                            if (dist < minDist)
                            {
                                minDist = dist;
                            }
                        }

                        if (minDist > maxMinDist)
                        {
                            maxMinDist = minDist;
                            bestTile = tile;
                        }
                    }

                    if (bestTile != -1)
                    {
                        seeds.Add(bestTile);
                    }
                    else
                    {
                        break;
                    }
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
            int totalMerged = 0;
            int pass = 0;

            while (pass < 10) // Safety limit of 10 passes
            {
                pass++;
                bool mergedAnyInThisPass = false;
                List<GeographicProvince> toRemove = new List<GeographicProvince>();

                // Build a quick map of province ID to the actual province object
                var provinceMap = provinces.ToDictionary(p => p.id, p => p);

                foreach (var p in provinces)
                {
                    if (p.provinceType == ProvinceType.Ocean) continue;
                    if (toRemove.Contains(p)) continue;

                    int pSize = p.tiles.Count;
                    bool isFeature = p.provinceType == ProvinceType.River || p.provinceType == ProvinceType.Lake || p.provinceType == ProvinceType.MountainRange;
                    int threshold = isFeature ? 30 : minNoFeatures;

                    if (pSize >= threshold) continue;

                    // Find adjacent neighbors
                    Dictionary<int, int> neighborWeights = new Dictionary<int, int>();

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
                                // If the neighbor province was already marked to be removed in this pass, ignore it
                                if (provinceMap.TryGetValue(neighborProvinceId, out var neighborProv))
                                {
                                    if (neighborProv.provinceType == ProvinceType.Ocean || toRemove.Contains(neighborProv)) continue;

                                    int weight = 1;
                                    if (neighborProv.provinceType == ProvinceType.Land)
                                    {
                                        weight = 100;
                                    }

                                    if (!neighborWeights.ContainsKey(neighborProvinceId))
                                    {
                                        neighborWeights[neighborProvinceId] = 0;
                                    }
                                    neighborWeights[neighborProvinceId] += weight;
                                }
                            }
                        }
                    }

                    if (neighborWeights.Any())
                    {
                        int bestNeighborId = neighborWeights.OrderByDescending(kv => kv.Value).First().Key;
                        if (provinceMap.TryGetValue(bestNeighborId, out var bestNeighbor))
                        {
                            // Merge p into bestNeighbor
                            foreach (int tileId in p.tiles)
                            {
                                bestNeighbor.tiles.Add(tileId);
                                tileToProvinceId[tileId] = bestNeighbor.id;
                            }
                            toRemove.Add(p);
                            mergedAnyInThisPass = true;
                            totalMerged++;
                        }
                    }
                }

                if (!mergedAnyInThisPass)
                {
                    break;
                }

                // Remove the merged provinces
                foreach (var p in toRemove)
                {
                    provinces.Remove(p);
                }
            }

            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] MergeTinyDomains finished. Merged {totalMerged} regions in {pass} passes. Final region count: {provinces.Count}", "factions");
        }

        private void ResolveContextualNames()
        {
            if (Find.WorldFeatures == null || Find.WorldFeatures.features.NullOrEmpty()) return;

            // Cache centroids of all vanilla WorldFeatures
            var featureCentroids = new Dictionary<WorldFeature, Vector3>();
            foreach (var wf in Find.WorldFeatures.features)
            {
                if (!wf.Tiles.Any()) continue;
                Vector3 center = Vector3.zero;
                foreach (int t in wf.Tiles)
                {
                    center += Find.WorldGrid.GetTileCenter(t);
                }
                featureCentroids[wf] = center / wf.Tiles.Count();
            }

            foreach (var province in provinces)
            {
                if (province.tiles.Count == 0) continue;

                // Calculate province centroid
                Vector3 provinceCenter = Vector3.zero;
                foreach (int t in province.tiles)
                {
                    provinceCenter += Find.WorldGrid.GetTileCenter(t);
                }
                provinceCenter /= province.tiles.Count;

                // Find the closest WorldFeature
                WorldFeature closestFeature = null;
                float minSqrDist = float.MaxValue;
                foreach (var kvp in featureCentroids)
                {
                    float sqrDist = (provinceCenter - kvp.Value).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        closestFeature = kvp.Key;
                    }
                }

                if (closestFeature != null)
                {
                    // If directly overlapping a vanilla feature, use its name
                    var directOverlap = Find.WorldFeatures.features
                        .FirstOrDefault(wf => wf.Tiles.Any(t => province.tiles.Contains(t)));

                    if (directOverlap != null)
                    {
                        province.name = directOverlap.name;
                    }
                    else
                    {
                        // Infer name based on closest feature
                        if (province.provinceType == ProvinceType.Lake)
                        {
                            province.name = closestFeature.name.Contains("Lake") || closestFeature.name.Contains("Sea") 
                                ? closestFeature.name 
                                : $"{closestFeature.name} Lake";
                        }
                        else if (province.provinceType == ProvinceType.Ocean)
                        {
                            province.name = closestFeature.name.Contains("Ocean") 
                                ? closestFeature.name 
                                : $"{closestFeature.name} Ocean";
                        }
                        else if (province.provinceType == ProvinceType.MountainRange)
                        {
                            province.name = closestFeature.name.Contains("Mountains") || closestFeature.name.Contains("Range") 
                                ? closestFeature.name 
                                : $"{closestFeature.name} Mountains";
                        }
                        else if (province.provinceType == ProvinceType.River)
                        {
                            province.name = GenerateRiverName(province.id, closestFeature.name);
                        }
                    }
                }
            }
        }

        private string GenerateRiverName(int id, string nearbyFeatureName)
        {
            var prefixes = new[] { "Silent", "Whispering", "Shimmering", "Roaring", "Winding", "Deep", "Swift", "Cold", "Grey", "Green", "Red", "Silver", "Golden", "Muddy", "Black", "Wild", "Broad", "Shadow", "Serpent", "Ghost", "Sun", "Moon", "Star", "Glimmering", "Ember", "Frost" };
            var suffixes = new[] { "River", "Creek", "Flow", "Fork", "Run", "Torrent", "Stream", "Waters", "Channel" };

            System.Random rand = new System.Random(id * 79 + 37);

            // 50% chance to name after nearby feature, 50% to generate a generic beautiful name
            if (rand.NextDouble() < 0.5f && !string.IsNullOrEmpty(nearbyFeatureName))
            {
                string cleanName = nearbyFeatureName
                    .Replace("Mountains", "")
                    .Replace("Mountain Range", "")
                    .Replace("Scrubland", "")
                    .Replace("Scrublands", "")
                    .Replace("Forest", "")
                    .Replace("Tangle", "")
                    .Replace("Basin", "")
                    .Replace("Swamp", "")
                    .Replace("Bog", "")
                    .Trim();

                string suffix = suffixes[rand.Next(suffixes.Length)];
                return $"{cleanName} {suffix}";
            }
            else
            {
                string prefix = prefixes[rand.Next(prefixes.Length)];
                string suffix = suffixes[rand.Next(suffixes.Length)];
                return $"{prefix} {suffix}";
            }
        }

        private string GenerateProvinceName(int provinceId, BiomeDef biome, ProvinceType type)
        {
            if (type == ProvinceType.Ocean) return "Ocean Region " + provinceId;
            if (type == ProvinceType.Lake) return "Lake Region " + provinceId;
            if (type == ProvinceType.River) return "River Region " + provinceId;
            if (type == ProvinceType.MountainRange) return "Mountain Region " + provinceId;

            return GenerateProvinceName(provinceId, biome);
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
