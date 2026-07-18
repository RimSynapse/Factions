using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(FactionGenerator), "GenerateFactionsIntoWorldLayer")]
    public static class Patch_FactionGenerator_GenerateFactionsIntoWorld
    {
        [HarmonyPrefix]
        public static bool Prefix(PlanetLayer layer, List<FactionDef> factions)
        {
            Log.Message("[RimSynapse-Factions] Custom Faction Generation and Placement solver starting...");

            World world = Find.World ?? Current.CreatingWorld;
            if (world == null || world.info == null || world.grid == null)
            {
                Log.Warning("[RimSynapse-Factions] World, World.info, or World.grid is null! Falling back to vanilla generator.");
                return true;
            }

            if (factions == null)
            {
                factions = new List<FactionDef>();
                foreach (var def in DefDatabase<FactionDef>.AllDefsListForReading)
                {
                    if (!def.isPlayer && !def.hidden)
                    {
                        factions.Add(def);
                    }
                }
            }

            FactionManager factionManager = world.factionManager;
            if (factionManager == null)
            {
                Log.Warning("[RimSynapse-Factions] FactionManager is null! Falling back to vanilla generator.");
                return true;
            }

            WorldGrid worldGrid = world.grid;
            WorldObjectsHolder worldObjects = world.worldObjects;

            var regionManager = world.GetComponent<SynapseRegionManager>();
            if (regionManager == null)
            {
                Log.Warning("[RimSynapse-Factions] SynapseRegionManager is null! Falling back to vanilla generator.");
                return true;
            }

            regionManager.GenerateProvinces();

            float coverage = world.info.planetCoverage;
            
            int landTilesCount = 0;
            int totalTiles = worldGrid.TilesCount;
            for (int i = 0; i < totalTiles; i++)
            {
                if (!worldGrid[i].WaterCovered)
                {
                    landTilesCount++;
                }
            }

            int targetFactionCount = Mathf.RoundToInt(coverage * 30f * (landTilesCount / 40000f));
            if (targetFactionCount < 5) targetFactionCount = 5;
            if (targetFactionCount > 35) targetFactionCount = 35;

            List<FactionDef> poolToClone = DefDatabase<FactionDef>.AllDefs
                .Where(f => !f.isPlayer && !f.hidden)
                .ToList();

            List<FactionDef> finalDefs = new List<FactionDef>();
            foreach (var def in factions)
            {
                finalDefs.Add(def);
            }

            if (poolToClone.Any())
            {
                while (finalDefs.Count(d => !d.isPlayer && !d.hidden) < targetFactionCount)
                {
                    finalDefs.Add(poolToClone.RandomElement());
                }
            }

            WorldObjectDef origSettlementDef = null;
            System.Reflection.FieldInfo settlementField = typeof(PlanetLayerDef).GetField("settlementWorldObjectDef", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (layer != null && layer.Def != null && settlementField != null)
            {
                origSettlementDef = (WorldObjectDef)settlementField.GetValue(layer.Def);
                settlementField.SetValue(layer.Def, null);
            }

            try
            {
                List<Faction> generatedFactions = new List<Faction>();
                foreach (var def in finalDefs)
                {
                    Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true));
                    if (faction != null)
                    {
                        factionManager.Add(faction);
                        generatedFactions.Add(faction);
                    }
                }

                foreach (FactionDef def in DefDatabase<FactionDef>.AllDefs)
                {
                    if (def.hidden && factionManager.FirstFactionOfDef(def) == null)
                    {
                        Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true));
                        if (faction != null)
                        {
                            factionManager.Add(faction);
                        }
                    }
                }
            }
            finally
            {
                if (layer != null && layer.Def != null && settlementField != null)
                {
                    settlementField.SetValue(layer.Def, origSettlementDef);
                }
            }

            foreach (var f1 in factionManager.AllFactions)
            {
                foreach (var f2 in factionManager.AllFactions)
                {
                    if (f1 != f2 && f1.RelationWith(f2, true) == null)
                    {
                        f1.RelationWith(f2, true);
                    }
                }
            }

            List<int> placedBases = new List<int>();
            var allNPCFactions = factionManager.AllFactions.Where(f => !f.IsPlayer && !f.def.hidden).ToList();

            var allProvinces = regionManager.Provinces;
            if (!allProvinces.Any())
            {
                Log.Warning("[RimSynapse-Factions] No provinces generated! Falling back to vanilla generator.");
                return true;
            }

            // Global tracking of provinces that already contain a settlement
            HashSet<GeographicProvince> occupiedProvinces = new HashSet<GeographicProvince>();

            // Sort NPC Factions: Turn Order is specified by profile.placementOrder (customizable in mod settings).
            var sortedNPCFactions = allNPCFactions
                .OrderBy(f => GetCategoryPriority(f))
                .ThenBy(f => (Faction.OfPlayer != null && f.HostileTo(Faction.OfPlayer)) ? 1 : 0)
                .ToList();

            foreach (var faction in sortedNPCFactions)
            {
                var profile = FactionPlacementSettings.GetProfile(faction.def);
                if (profile == null) continue;

                // Base counts based on tech level scaled by coverage (Industrial ~20, Spacer ~3, Tribal ~5)
                int baseTarget = 5;
                if (faction.def.techLevel == TechLevel.Industrial) baseTarget = 20;
                else if (faction.def.techLevel >= TechLevel.Spacer) baseTarget = 3;

                int baseCount = Mathf.RoundToInt(baseTarget * (coverage / 0.3f));
                baseCount = Mathf.Clamp(baseCount, 1, 40);

                Dictionary<int, float> tileScores = new Dictionary<int, float>();
                for (int t = 0; t < totalTiles; t++)
                {
                    Tile tileData = worldGrid[t];
                    if (tileData.WaterCovered || tileData.hilliness == Hilliness.Impassable || (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable))
                    {
                        tileScores[t] = -9999f;
                        continue;
                    }

                    if (!faction.def.allowedArrivalTemperatureRange.Includes(tileData.temperature))
                    {
                        tileScores[t] = -9999f;
                        continue;
                    }

                    float mineralVal = 0.5f;
                    if (tileData.hilliness == Hilliness.SmallHills) mineralVal = 1.0f;
                    else if (tileData.hilliness == Hilliness.LargeHills) mineralVal = 2.0f;
                    else if (tileData.hilliness == Hilliness.Mountainous) mineralVal = 3.0f;

                    float nutritionVal = tileData.PrimaryBiome != null ? tileData.PrimaryBiome.plantDensity : 0.5f;
                    float forageVal = tileData.PrimaryBiome != null ? tileData.PrimaryBiome.forageability : 0.5f;
                    float biomassVal = tileData.PrimaryBiome != null ? tileData.PrimaryBiome.TreeDensity : 0.5f;
                    float grazingVal = (tileData.hilliness == Hilliness.Flat) ? nutritionVal * 2f : nutritionVal;
                    float hospVal = nutritionVal * 2f + forageVal;

                    float score = 0f;
                    score += profile.mineralWeight * mineralVal;
                    score += profile.nutritionWeight * nutritionVal;
                    score += profile.forageWeight * forageVal;
                    score += profile.grazingWeight * grazingVal;
                    score += profile.huntingWeight * biomassVal;

                    if (profile.marginWeight > 0f)
                    {
                        score += profile.marginWeight * Mathf.Max(0f, 3.0f - hospVal);
                    }

                    tileScores[t] = score;
                }

                Dictionary<GeographicProvince, float> provinceScores = new Dictionary<GeographicProvince, float>();
                foreach (var p in allProvinces)
                {
                    var validTiles = p.tiles.Where(t => tileScores.ContainsKey(t) && tileScores[t] > -9999f).ToList();
                    if (validTiles.Count == 0)
                    {
                        provinceScores[p] = -9999f;
                        continue;
                    }
                    provinceScores[p] = validTiles.Average(t => tileScores[t]);
                }

                List<GeographicProvince> factionProvinces = new List<GeographicProvince>();
                List<int> factionBases = new List<int>();

                // Define hard adjacency requirement: friendly and neutral factions (excluding the Empire)
                bool isFriendlyOrNeutral = (Faction.OfPlayer != null) ? !faction.HostileTo(Faction.OfPlayer) : !faction.def.permanentEnemy;
                bool hasHardAdjacency = isFriendlyOrNeutral && (faction.def.defName != "Empire");

                for (int b = 0; b < baseCount; b++)
                {
                    GeographicProvince chosenProvince = null;
                    string factionId = faction.GetUniqueLoadID();

                    // Pre-calculate properties for all provinces
                    var allCandidates = allProvinces
                        .Select(p => {
                            float suitability = provinceScores.ContainsKey(p) ? provinceScores[p] : -9999f;
                            if (suitability <= -9999f) return new { Province = p, Score = -9999f, ThisFactionCount = 999, GlobalCount = 999, IsAdjacent = false, Dist = 9999f, SharedBorders = 0, BarrierCount = 0 };

                            int thisFactionCount = p.owningFactionIds.Count(id => id == factionId);
                            int globalCount = p.owningFactionIds.Count;
                            bool isAdjacent = factionProvinces.Any() && IsProvinceAdjacentToAny(p, factionProvinces, regionManager, worldGrid);

                            float minAllyDist = 0f;
                            if (factionProvinces.Any())
                            {
                                minAllyDist = 9999f;
                                foreach (var ownP in factionProvinces)
                                {
                                    float dist = GetProvinceDistance(p, ownP, worldGrid);
                                    if (dist < minAllyDist) minAllyDist = dist;
                                }
                            }

                            int sharedBorders = factionProvinces.Any() ? GetSharedBorderCount(p, factionProvinces, regionManager, worldGrid) : 0;
                            int barrierCount = GetBarrierBorderCount(p, worldGrid);

                            return new { Province = p, Score = suitability, ThisFactionCount = thisFactionCount, GlobalCount = globalCount, IsAdjacent = isAdjacent, Dist = minAllyDist, SharedBorders = sharedBorders, BarrierCount = barrierCount };
                        })
                        .Where(x => x.Score > -9999f);

                    // For factions with hard adjacency requirement, subsequent bases MUST be adjacent (if any adjacent valid provinces exist)
                    if (b > 0 && hasHardAdjacency && factionProvinces.Any())
                    {
                        var adjacentOnly = allCandidates.Where(x => x.IsAdjacent).ToList();
                        if (adjacentOnly.Any())
                        {
                            allCandidates = adjacentOnly;
                        }
                    }

                    // Weighting compact perimeter vs resource optimization based on size threshold (5 provinces)
                    bool prioritizePerimeter = (factionProvinces.Count > 5);

                    var sortedCandidates = allCandidates
                        .OrderBy(x => x.ThisFactionCount)
                        .ThenBy(x => x.GlobalCount);

                    if (prioritizePerimeter)
                    {
                        // Prioritize compact borders (high shared borders) and natural barriers/mountains first to keep territory from snaking
                        sortedCandidates = sortedCandidates
                            .ThenByDescending(x => x.SharedBorders)
                            .ThenByDescending(x => x.BarrierCount)
                            .ThenBy(x => x.Dist)
                            .ThenByDescending(x => x.Score);
                    }
                    else
                    {
                        // Prioritize resources/suitability first, while still favoring barrier anchoring
                        sortedCandidates = sortedCandidates
                            .ThenByDescending(x => x.Score)
                            .ThenByDescending(x => x.BarrierCount)
                            .ThenByDescending(x => x.IsAdjacent ? 1 : 0)
                            .ThenBy(x => x.Dist)
                            .ThenByDescending(x => x.SharedBorders);
                    }

                    var candidatesList = sortedCandidates.ToList();

                    if (candidatesList.Any())
                    {
                        chosenProvince = candidatesList[0].Province;
                    }

                    if (chosenProvince != null)
                    {
                        int chosenTile = FindBestTileInProvince(chosenProvince, factionBases, placedBases, tileScores, worldGrid);

                        if (chosenTile != -1)
                        {
                            Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                            settlement.Tile = chosenTile;
                            settlement.SetFaction(faction);
                            settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
                            worldObjects.Add(settlement);

                            factionBases.Add(chosenTile);
                            placedBases.Add(chosenTile);

                            if (!factionProvinces.Contains(chosenProvince))
                            {
                                factionProvinces.Add(chosenProvince);
                                if (!chosenProvince.owningFactionIds.Contains(factionId))
                                {
                                    chosenProvince.owningFactionIds.Add(factionId);
                                }
                            }
                            occupiedProvinces.Add(chosenProvince);
                        }
                    }
                }

                Log.Message($"[RimSynapse-Factions] Placed {factionBases.Count} bases across {factionProvinces.Count} provinces for faction: {faction.Name}");
            }

            RoadGeneratorHelper.GenerateRoadsBetweenBases();

            // Refresh the population density cache since new settlements have been placed
            PopulationDensityUtility.MarkCacheDirty();

            Log.Message("[RimSynapse-Factions] Custom Faction Generation and Placement completed successfully.");
            return false;
        }

        private static int FindBestTileInProvince(GeographicProvince province, List<int> sameFactionBases, List<int> allPlacedBases, Dictionary<int, float> tileScores, WorldGrid worldGrid)
        {
            var candidateTiles = province.tiles
                .Where(t => tileScores.ContainsKey(t) && tileScores[t] > -9999f && !allPlacedBases.Contains(t))
                .OrderByDescending(t => tileScores[t])
                .ToList();

            if (!candidateTiles.Any()) return -1;

            foreach (var tile in candidateTiles)
            {
                bool tooCloseToRival = false;
                foreach (var otherBase in allPlacedBases)
                {
                    if (sameFactionBases.Contains(otherBase)) continue;
                    float dist = worldGrid.ApproxDistanceInTiles(tile, otherBase);
                    if (dist < 8f)
                    {
                        tooCloseToRival = true;
                        break;
                    }
                }
                if (!tooCloseToRival) return tile;
            }

            foreach (var tile in candidateTiles)
            {
                bool tooCloseToRival = false;
                foreach (var otherBase in allPlacedBases)
                {
                    if (sameFactionBases.Contains(otherBase)) continue;
                    float dist = worldGrid.ApproxDistanceInTiles(tile, otherBase);
                    if (dist < 4f)
                    {
                        tooCloseToRival = true;
                        break;
                    }
                }
                if (!tooCloseToRival) return tile;
            }

            return candidateTiles[0];
        }

        private static bool IsProvinceAdjacentToAny(GeographicProvince p, List<GeographicProvince> existing, SynapseRegionManager manager, WorldGrid worldGrid)
        {
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
            foreach (int tile in p.tiles)
            {
                neighbors.Clear();
                worldGrid.GetTileNeighbors(tile, neighbors);
                foreach (var n in neighbors)
                {
                    int neighborProvinceId = manager.GetProvinceId(n.tileId);
                    if (neighborProvinceId != -1 && existing.Any(ep => ep.id == neighborProvinceId))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static float GetProvinceDistance(GeographicProvince p1, GeographicProvince p2, WorldGrid worldGrid)
        {
            if (p1.tiles.Count == 0 || p2.tiles.Count == 0) return 9999f;
            return worldGrid.ApproxDistanceInTiles(p1.tiles[0], p2.tiles[0]);
        }

        private static int GetCategoryPriority(Faction faction)
        {
            var profile = FactionPlacementSettings.GetProfile(faction.def);
            if (profile != null)
            {
                return profile.placementOrder;
            }
            if (faction.def.defName == "Empire") return 2;
            if (faction.def.techLevel == TechLevel.Industrial) return 1;
            if (faction.def.techLevel >= TechLevel.Spacer) return 3;
            return 4; // Tribal
        }

        private static int GetSharedBorderCount(GeographicProvince p, List<GeographicProvince> existing, SynapseRegionManager manager, WorldGrid worldGrid)
        {
            HashSet<int> sharedAdjacentProvinces = new HashSet<int>();
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
            foreach (int tile in p.tiles)
            {
                neighbors.Clear();
                worldGrid.GetTileNeighbors(tile, neighbors);
                foreach (var n in neighbors)
                {
                    int neighborProvinceId = manager.GetProvinceId(n.tileId);
                    if (neighborProvinceId != -1 && neighborProvinceId != p.id)
                    {
                        var matchingProv = existing.FirstOrDefault(ep => ep.id == neighborProvinceId);
                        if (matchingProv != null)
                        {
                            sharedAdjacentProvinces.Add(matchingProv.id);
                        }
                    }
                }
            }
            return sharedAdjacentProvinces.Count;
        }

        private static int GetBarrierBorderCount(GeographicProvince p, WorldGrid worldGrid)
        {
            int barrierCount = 0;
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
            foreach (int tile in p.tiles)
            {
                neighbors.Clear();
                worldGrid.GetTileNeighbors(tile, neighbors);
                foreach (var n in neighbors)
                {
                    Tile nTile = worldGrid[n.tileId];
                    if (nTile.hilliness == Hilliness.Impassable || nTile.WaterCovered || (nTile.PrimaryBiome != null && nTile.PrimaryBiome.impassable))
                    {
                        barrierCount++;
                    }
                }
            }
            return barrierCount;
        }
    }

    [HarmonyPatch(typeof(WorldGenerator), "GenerateWorld")]
    public static class Patch_WorldGenerator_GenerateWorld
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            Log.Message("[RimSynapse-Factions] WorldGenerator.GenerateWorld PREFIX is executing!");
        }
    }

    [HarmonyPatch(typeof(WorldGenStep_Factions), "GenerateFresh")]
    public static class Patch_WorldGenStep_Factions_GenerateFresh
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            Log.Message("[RimSynapse-Factions] WorldGenStep_Factions.GenerateFresh PREFIX is executing!");
        }
    }
}
