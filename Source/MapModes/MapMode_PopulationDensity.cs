using System.Collections.Generic;
using System.Linq;
using MapModeFramework;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions
{
    [StaticConstructorOnStartup]
    public class MapMode_PopulationDensity : MapMode
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

        private static int[] tilePopulations = null;
        private static Material[] densityMats = null;

        public static void InitializeMaterials()
        {
            if (densityMats != null) return;

            densityMats = new Material[101];
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                Color color = Color.Lerp(new Color(0f, 0.6f, 0.1f, 0.3f), new Color(0.9f, 0.1f, 0.1f, 0.5f), t);
                densityMats[i] = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, color, 3510);
            }
        }

        public static void CacheData()
        {
            InitializeMaterials();

            if (Find.WorldGrid == null)
            {
                Log.Warning("[RimSynapse-Factions] CacheData failed: Find.WorldGrid is null.");
                return;
            }
            int tilesCount = Find.WorldGrid.TilesCount;
            if (tilePopulations == null || tilePopulations.Length != tilesCount)
            {
                tilePopulations = new int[tilesCount];
            }
            else
            {
                System.Array.Clear(tilePopulations, 0, tilesCount);
            }

            var settlements = Find.WorldObjects?.Settlements;
            if (settlements == null)
            {
                Log.Warning("[RimSynapse-Factions] CacheData failed: Find.WorldObjects.Settlements is null.");
                return;
            }

            Log.Warning($"[RimSynapse-Factions] CacheData running. Settlements count: {settlements.Count}");

            float[] tempPops = new float[tilesCount];
            float maxPop = 0f;

            foreach (var settlement in settlements)
            {
                int settlementPop = PopulationDensityUtility.GetSettlementPopulation(settlement);
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

                if (startPlanetTile == PlanetTile.Invalid)
                {
                    Log.Warning($"[RimSynapse-Factions] StartPlanetTile is Invalid for settlement at tile {startTileId}");
                    continue;
                }

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
                    if (tempPops[currentTileId] > maxPop) maxPop = tempPops[currentTileId];

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

            for (int i = 0; i < tilesCount; i++)
            {
                tilePopulations[i] = UnityEngine.Mathf.RoundToInt(tempPops[i]);
            }

            Log.Warning($"[RimSynapse-Factions] CacheData completed. Max population: {maxPop}");
        }

        private static float GetStepMultiplier(PlanetTile fromTile, PlanetTile toTile)
        {
            if (Find.WorldGrid == null) return 0f;

            Tile tileData = Find.WorldGrid[toTile.tileId];
            if (tileData == null) return 0f;

            if (tileData.hilliness == Hilliness.Impassable || 
                tileData.WaterCovered || 
                (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable))
            {
                return 0f;
            }

            float factor = 1f;
            bool hasTerrainFeature = false;

            if (tileData.hilliness == Hilliness.LargeHills)
            {
                factor *= 4f;
                hasTerrainFeature = true;
            }
            else if (tileData.hilliness == Hilliness.Mountainous)
            {
                factor *= 8f;
                hasTerrainFeature = true;
            }

            bool isSwampOrMarsh = tileData.swampiness > 0.1f || 
                (tileData.PrimaryBiome != null && 
                 (tileData.PrimaryBiome.defName.Contains("Swamp") || 
                  tileData.PrimaryBiome.defName.Contains("Marsh")));
            if (isSwampOrMarsh)
            {
                factor *= 8f;
                hasTerrainFeature = true;
            }

            if (!hasTerrainFeature)
            {
                factor = 2f;
            }

            RoadDef road = Find.WorldGrid.GetRoadDef(fromTile.tileId, toTile.tileId);
            if (road != null)
            {
                factor *= (2f / 3f);
            }

            if (IsNextToWater(toTile.tileId))
            {
                factor *= (2f / 3f);
            }

            float stepMultiplier = 1f / factor;
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

        public override WorldLayer_MapMode WorldLayer => WorldLayer_PopulationDensity.Instance;
        public override bool CanToggleWater => false;

        public MapMode_PopulationDensity() { }
        public MapMode_PopulationDensity(MapModeDef def) : base(def) { }

        public override Material GetMaterial(int tile)
        {
            if (tilePopulations == null || tile >= tilePopulations.Length)
            {
                return BaseContent.ClearMat;
            }

            int pop = tilePopulations[tile];
            if (pop <= 0)
            {
                return BaseContent.ClearMat;
            }

            int index = Mathf.Clamp(Mathf.RoundToInt(pop / 3f), 0, 100);
            return densityMats[index];
        }

        public override string GetTileLabel(int tile)
        {
            if (tilePopulations == null || tile >= tilePopulations.Length) return null;
            int pop = tilePopulations[tile];
            return pop > 0 ? pop.ToString() : null;
        }

        public override string GetTooltip(int tile)
        {
            if (tilePopulations == null || tile >= tilePopulations.Length) return null;
            int pop = tilePopulations[tile];
            return pop > 0 ? $"Pawn dwellings: {pop}" : null;
        }
    }

    public class WorldLayer_PopulationDensity : WorldLayer_MapMode
    {
        public static WorldLayer_PopulationDensity Instance { get; private set; }

        public WorldLayer_PopulationDensity()
        {
            Instance = this;
            Log.Warning("[RimSynapse-Factions] WorldLayer_PopulationDensity constructor called!");
        }

        public override System.Collections.IEnumerable Regenerate()
        {
            Log.Warning("[RimSynapse-Factions] WorldLayer_PopulationDensity.Regenerate() starting.");
            MapMode_PopulationDensity.CacheData();
            
            foreach (var step in base.Regenerate())
            {
                yield return step;
            }
            Log.Warning("[RimSynapse-Factions] WorldLayer_PopulationDensity.Regenerate() completed.");
        }
    }
}
