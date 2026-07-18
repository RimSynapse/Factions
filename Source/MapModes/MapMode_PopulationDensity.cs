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

            // 5 density segments * 4 elevation bands = 20 materials
            densityMats = new Material[20];

            // Define the base colors for each density segment (0 to 4) and elevation band (0 to 3)
            // Segment 0: Background (Brown)
            Color[] seg0 = new Color[] {
                new Color(0.48f, 0.35f, 0.2f, 0.45f),   // Lowlands
                new Color(0.42f, 0.28f, 0.14f, 0.5f),   // Midlands
                new Color(0.33f, 0.2f, 0.08f, 0.55f),   // Highlands
                new Color(0.24f, 0.14f, 0.05f, 0.6f)    // Mountains
            };
            // Segment 1: Low Density (10-20, light yellow-green)
            Color[] seg1 = new Color[] {
                new Color(0.6f, 0.7f, 0.2f, 0.5f),
                new Color(0.5f, 0.6f, 0.18f, 0.55f),
                new Color(0.4f, 0.5f, 0.15f, 0.6f),
                new Color(0.3f, 0.4f, 0.12f, 0.65f)
            };
            // Segment 2: Medium Density (20-30, soft grass green)
            Color[] seg2 = new Color[] {
                new Color(0.35f, 0.75f, 0.15f, 0.55f),
                new Color(0.28f, 0.65f, 0.12f, 0.6f),
                new Color(0.2f, 0.55f, 0.1f, 0.65f),
                new Color(0.12f, 0.45f, 0.08f, 0.7f)
            };
            // Segment 3: High Density (30-40, kelly green)
            Color[] seg3 = new Color[] {
                new Color(0.1f, 0.8f, 0.1f, 0.6f),
                new Color(0.08f, 0.7f, 0.08f, 0.65f),
                new Color(0.05f, 0.6f, 0.05f, 0.7f),
                new Color(0.02f, 0.45f, 0.02f, 0.75f)
            };
            // Segment 4: Maximum Density (40+, vibrant green)
            Color[] seg4 = new Color[] {
                new Color(0.0f, 0.95f, 0.0f, 0.65f),
                new Color(0.0f, 0.85f, 0.0f, 0.7f),
                new Color(0.0f, 0.75f, 0.0f, 0.75f),
                new Color(0.0f, 0.65f, 0.0f, 0.8f)
            };

            Color[][] allColors = new Color[][] { seg0, seg1, seg2, seg3, seg4 };

            for (int seg = 0; seg < 5; seg++)
            {
                for (int band = 0; band < 4; band++)
                {
                    int index = seg * 4 + band;
                    Color color = allColors[seg][band];
                    
                    densityMats[index] = null;
                    if (ShaderDatabase.MetaOverlay != null && BaseContent.WhiteTex != null)
                    {
                        densityMats[index] = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, color, 3510);
                    }
                    if (densityMats[index] == null)
                    {
                        densityMats[index] = SolidColorMaterials.SimpleSolidColorMaterial(color);
                    }
                    if (densityMats[index] == null)
                    {
                        densityMats[index] = BaseContent.WhiteMat;
                    }
                }
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

            // Baseline random pawn placement in relatively hospitable environments (nomads, homesteads)
            for (int i = 0; i < tilesCount; i++)
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

        public override WorldLayer_MapMode WorldLayer => WorldLayer_MapMode_Terrain.Instance;
        public override bool CanToggleWater => false;

        public override void DoPreRegenerate()
        {
            base.DoPreRegenerate();
            CacheData();
        }

        public MapMode_PopulationDensity() { }
        public MapMode_PopulationDensity(MapModeDef def) : base(def) { }

        public override Material GetMaterial(int tile)
        {
            if (Find.WorldGrid == null || tile >= Find.WorldGrid.TilesCount)
            {
                return BaseContent.ClearMat;
            }

            Tile tileData = Find.WorldGrid[tile];
            if (tileData.WaterCovered)
            {
                return BaseContent.ClearMat;
            }

            int pop = 0;
            if (tilePopulations != null && tile < tilePopulations.Length)
            {
                pop = tilePopulations[tile];
            }

            int densitySegment = 0;
            if (pop >= 40) densitySegment = 4;
            else if (pop >= 30) densitySegment = 3;
            else if (pop >= 20) densitySegment = 2;
            else if (pop >= 10) densitySegment = 1;

            float elevation = tileData.elevation;
            int elevationBand = 0;
            if (elevation >= 2200f) elevationBand = 3;
            else if (elevation >= 1200f) elevationBand = 2;
            else if (elevation >= 600f) elevationBand = 1;

            int index = densitySegment * 4 + elevationBand;

            if (densityMats == null || index >= densityMats.Length)
            {
                return BaseContent.ClearMat;
            }

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
}
