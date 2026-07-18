using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Factions
{
    public static class PopulationDensityUtility
    {
        private struct QueueEntry
        {
            public PlanetTile tile;
            public float multiplier;

            public QueueEntry(PlanetTile tile, float multiplier)
            {
                this.tile = tile;
                this.multiplier = multiplier;
            }
        }

        private static int[] cachedTilePopulations = null;
        private static bool cacheDirty = true;

        public static void MarkCacheDirty()
        {
            cacheDirty = true;
        }

        private static void RefreshCache()
        {
            if (Find.World == null || Find.WorldGrid == null) return;
            int count = Find.WorldGrid.TilesCount;
            if (cachedTilePopulations == null || cachedTilePopulations.Length != count)
            {
                cachedTilePopulations = new int[count];
            }
            else
            {
                System.Array.Clear(cachedTilePopulations, 0, count);
            }

            float[] tempPops = new float[count];

            // 1. Baseline random pawn placement in relatively hospitable environments (nomads, homesteads)
            for (int i = 0; i < count; i++)
            {
                Tile tileData = Find.WorldGrid[i];
                if (tileData.WaterCovered || tileData.hilliness == Hilliness.Impassable || tileData.PrimaryBiome == null || tileData.PrimaryBiome.impassable)
                {
                    continue;
                }

                if (tileData.temperature >= -12f && tileData.temperature <= 42f && tileData.PrimaryBiome.plantDensity > 0.15f)
                {
                    UnityEngine.Random.State state = UnityEngine.Random.state;
                    UnityEngine.Random.InitState(i * 377 + 99);
                    float roll = UnityEngine.Random.value;
                    if (roll < 0.06f)
                    {
                        float basePop = UnityEngine.Random.Range(2f, 8f) * (tileData.PrimaryBiome.plantDensity + tileData.PrimaryBiome.forageability + 0.2f);
                        tempPops[i] += basePop;
                    }
                    UnityEngine.Random.state = state;
                }
            }

            // 2. Settlement propagation
            var settlements = Find.WorldObjects?.Settlements;
            if (settlements != null)
            {
                var settlementList = new List<Settlement>(settlements);
                foreach (var settlement in settlementList)
                {
                    int settlementPop = GetSettlementPopulation(settlement);
                    if (settlementPop <= 0) continue;

                    int startTileId = settlement.Tile;

                    PlanetTile startPlanetTile = PlanetTile.Invalid;
                    var tempNeighbors = new List<PlanetTile>();
                    Find.WorldGrid.GetTileNeighbors(startTileId, tempNeighbors);
                    if (tempNeighbors.Any())
                    {
                        var doubleNeighbors = new List<PlanetTile>();
                        Find.WorldGrid.GetTileNeighbors(tempNeighbors[0].tileId, doubleNeighbors);
                        foreach (var t in doubleNeighbors)
                        {
                            if (t.tileId == startTileId)
                            {
                                startPlanetTile = t;
                                break;
                            }
                        }
                    }

                    if (startPlanetTile == PlanetTile.Invalid) continue;

                    var visited = new HashSet<int>();
                    var queue = new Queue<QueueEntry>();

                    queue.Enqueue(new QueueEntry(startPlanetTile, 1.0f));
                    visited.Add(startTileId);

                    while (queue.Count > 0)
                    {
                        var current = queue.Dequeue();
                        PlanetTile currentTile = current.tile;
                        int currentTileId = currentTile.tileId;
                        float currentMultiplier = current.multiplier;

                        if (currentMultiplier < 0.001f) continue;

                        tempPops[currentTileId] += (settlementPop * currentMultiplier);

                        var neighbors = new List<PlanetTile>();
                        Find.WorldGrid.GetTileNeighbors(currentTileId, neighbors);
                        foreach (var neighbor in neighbors)
                        {
                            int neighborId = neighbor.tileId;
                            if (!visited.Contains(neighborId))
                            {
                                visited.Add(neighborId);

                                float stepMultiplier = GetStepMultiplier(currentTile, neighbor);
                                if (stepMultiplier > 0f)
                                {
                                    queue.Enqueue(new QueueEntry(neighbor, currentMultiplier * stepMultiplier));
                                }
                            }
                        }
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                cachedTilePopulations[i] = UnityEngine.Mathf.RoundToInt(tempPops[i]);
            }

            cacheDirty = false;
        }

        public static int GetPopulationAtTile(int targetTile)
        {
            if (Find.World == null || Find.WorldGrid == null) return 0;
            if (cacheDirty || cachedTilePopulations == null || cachedTilePopulations.Length != Find.WorldGrid.TilesCount)
            {
                RefreshCache();
            }
            if (cachedTilePopulations == null || targetTile < 0 || targetTile >= cachedTilePopulations.Length) return 0;
            return cachedTilePopulations[targetTile];
        }

        public static int GetSettlementPopulation(Settlement settlement)
        {
            if (settlement == null) return 0;

            if (settlement.Faction != null && settlement.Faction.IsPlayer)
            {
                var map = Find.Maps?.FirstOrDefault(m => m.Tile == settlement.Tile);
                if (map != null)
                {
                    return map.mapPawns?.FreeColonistsCount ?? 0;
                }
                return 0;
            }

            int basePop = 50;
            if (settlement.Faction != null)
            {
                var tech = settlement.Faction.def?.techLevel ?? TechLevel.Industrial;
                if (tech == TechLevel.Neolithic) basePop = 60;
                else if (tech == TechLevel.Industrial) basePop = 90;
                else if (tech >= TechLevel.Spacer) basePop = 150;
            }

            System.Random random = new System.Random(settlement.Tile);
            return basePop + random.Next(-10, 20);
        }

        private static float GetStepMultiplier(PlanetTile fromTile, PlanetTile toTile)
        {
            if (Find.WorldGrid == null) return 0f;

            Tile tileData = Find.WorldGrid[toTile.tileId];
            if (tileData == null) return 0f;

            // 1. Impassable mountains and water do not transfer any population
            if (tileData.hilliness == Hilliness.Impassable || 
                tileData.WaterCovered || 
                (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable))
            {
                return 0f;
            }

            float factor = 1f;
            bool hasTerrainFeature = false;

            // Large Hills factor: 4
            if (tileData.hilliness == Hilliness.LargeHills)
            {
                factor *= 4f;
                hasTerrainFeature = true;
            }
            // Mountainous factor: 8
            else if (tileData.hilliness == Hilliness.Mountainous)
            {
                factor *= 8f;
                hasTerrainFeature = true;
            }

            // Swamp/Marsh factor: 8
            bool isSwampOrMarsh = tileData.swampiness > 0.1f || 
                (tileData.PrimaryBiome != null && 
                 (tileData.PrimaryBiome.defName.Contains("Swamp") || 
                  tileData.PrimaryBiome.defName.Contains("Marsh")));
            if (isSwampOrMarsh)
            {
                factor *= 8f;
                hasTerrainFeature = true;
            }

            // Default flat/small hills factor: 2
            if (!hasTerrainFeature)
            {
                factor = 2f;
            }

            // Along a road
            RoadDef road = Find.WorldGrid.GetRoadDef(fromTile, toTile);
            if (road != null)
            {
                factor *= (2f / 3f);
            }

            // Next to water
            if (IsNextToWater(toTile.tileId))
            {
                factor *= (2f / 3f);
            }

            float stepMultiplier = 1f / factor;

            // Cap stepMultiplier at 0.75f to prevent population increases/explosions
            if (stepMultiplier > 0.75f)
            {
                stepMultiplier = 0.75f;
            }

            return stepMultiplier;
        }

        private static bool IsNextToWater(int tileId)
        {
            Tile tile = Find.WorldGrid[tileId];
            if (tile == null) return false;
            if (tile.IsCoastal || tile.WaterCovered) return true;

            var neighbors = new List<PlanetTile>();
            Find.WorldGrid.GetTileNeighbors(tileId, neighbors);
            foreach (var n in neighbors)
            {
                var nt = Find.WorldGrid[n.tileId];
                if (nt != null && nt.WaterCovered) return true;
            }
            return false;
        }
    }
}
