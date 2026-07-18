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
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Custom Faction Generation and Placement solver starting...", "factions");

            if (Find.World == null || Find.World.info == null || Find.WorldGrid == null || factions == null)
            {
                RimSynapse.SynapseLogger.Warning("[RimSynapse-Factions] Find.World, World.info, WorldGrid, or factions list is null! Falling back to vanilla generator.", "factions");
                return true;
            }

            var regionManager = Find.World.GetComponent<SynapseRegionManager>();
            if (regionManager == null)
            {
                RimSynapse.SynapseLogger.Warning("[RimSynapse-Factions] SynapseRegionManager is null! Falling back to vanilla generator.", "factions");
                return true;
            }

            regionManager.GenerateProvinces();

            float coverage = Find.World.info.planetCoverage;
            
            int landTilesCount = 0;
            int totalTiles = Find.WorldGrid.TilesCount;
            for (int i = 0; i < totalTiles; i++)
            {
                if (!Find.WorldGrid[i].WaterCovered)
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

            List<Faction> generatedFactions = new List<Faction>();
            foreach (var def in finalDefs)
            {
                Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true));
                if (faction != null)
                {
                    Find.FactionManager.Add(faction);
                    generatedFactions.Add(faction);
                }
            }

            foreach (FactionDef def in DefDatabase<FactionDef>.AllDefs)
            {
                if (def.hidden && Find.FactionManager.FirstFactionOfDef(def) == null)
                {
                    Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true));
                    if (faction != null)
                    {
                        Find.FactionManager.Add(faction);
                    }
                }
            }

            foreach (var f1 in Find.FactionManager.AllFactions)
            {
                foreach (var f2 in Find.FactionManager.AllFactions)
                {
                    if (f1 != f2 && f1.RelationWith(f2, true) == null)
                    {
                        f1.RelationWith(f2, true);
                    }
                }
            }

            List<int> placedBases = new List<int>();
            var allNPCFactions = Find.FactionManager.AllFactions.Where(f => !f.IsPlayer && !f.Hidden).ToList();

            var allProvinces = regionManager.Provinces;
            if (!allProvinces.Any())
            {
                RimSynapse.SynapseLogger.Warning("[RimSynapse-Factions] No provinces generated! Falling back to vanilla generator.", "factions");
                return true;
            }

            foreach (var faction in allNPCFactions)
            {
                var profile = FactionPlacementSettings.GetProfile(faction.def);
                if (profile == null) continue;

                int baseCount = Mathf.RoundToInt(profile.baseCountRange.RandomInRange * (coverage / 0.3f));
                baseCount = Mathf.Clamp(baseCount, 2, 40);

                Dictionary<int, float> tileScores = new Dictionary<int, float>();
                for (int t = 0; t < totalTiles; t++)
                {
                    Tile tileData = Find.WorldGrid[t];
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
                bool isLargeNation = faction.def.techLevel >= TechLevel.Industrial;

                for (int b = 0; b < baseCount; b++)
                {
                    GeographicProvince chosenProvince = null;

                    if (b == 0)
                    {
                        chosenProvince = provinceScores
                            .Where(kv => kv.Value > -9999f && (!kv.Key.owningFactionIds.Any()))
                            .OrderByDescending(kv => kv.Value)
                            .Select(kv => kv.Key)
                            .FirstOrDefault();

                        if (chosenProvince == null)
                        {
                            chosenProvince = provinceScores
                                .Where(kv => kv.Value > -9999f)
                                .OrderByDescending(kv => kv.Value)
                                .Select(kv => kv.Key)
                                .FirstOrDefault();
                        }
                    }
                    else
                    {
                        if (!isLargeNation && factionProvinces.Any())
                        {
                            chosenProvince = factionProvinces[0];
                        }
                        else if (factionProvinces.Any())
                        {
                            var adjacentProvinces = allProvinces
                                .Where(p => p.tiles.Any() && !factionProvinces.Contains(p) && IsProvinceAdjacentToAny(p, factionProvinces, regionManager))
                                .ToList();

                            if (adjacentProvinces.Any())
                            {
                                chosenProvince = adjacentProvinces
                                    .Select(p => {
                                        float suitability = provinceScores.ContainsKey(p) ? provinceScores[p] : -9999f;
                                        float minAllyDist = 9999f;
                                        foreach (var ownP in factionProvinces)
                                        {
                                            float dist = GetProvinceDistance(p, ownP);
                                            if (dist < minAllyDist) minAllyDist = dist;
                                        }
                                        float score = suitability - 0.4f * minAllyDist;
                                        return new KeyValuePair<GeographicProvince, float>(p, score);
                                    })
                                    .Where(kv => kv.Value > -9999f)
                                    .OrderByDescending(kv => kv.Value)
                                    .Select(kv => kv.Key)
                                    .FirstOrDefault();
                            }

                            if (chosenProvince == null)
                            {
                                chosenProvince = allProvinces
                                    .Where(p => p.tiles.Any() && !factionProvinces.Contains(p))
                                    .Select(p => {
                                        float suitability = provinceScores.ContainsKey(p) ? provinceScores[p] : -9999f;
                                        float minAllyDist = 9999f;
                                        foreach (var ownP in factionProvinces)
                                        {
                                            float dist = GetProvinceDistance(p, ownP);
                                            if (dist < minAllyDist) minAllyDist = dist;
                                        }
                                        float score = suitability - 0.4f * minAllyDist;
                                        return new KeyValuePair<GeographicProvince, float>(p, score);
                                    })
                                    .Where(kv => kv.Value > -9999f)
                                    .OrderByDescending(kv => kv.Value)
                                    .Select(kv => kv.Key)
                                    .FirstOrDefault();
                            }
                        }
                    }

                    if (chosenProvince == null && factionProvinces.Any())
                    {
                        chosenProvince = factionProvinces[0];
                    }

                    if (chosenProvince != null)
                    {
                        int chosenTile = FindBestTileInProvince(chosenProvince, factionBases, placedBases, tileScores);

                        if (chosenTile != -1)
                        {
                            Settlement settlement = (Settlement)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
                            settlement.Tile = chosenTile;
                            settlement.SetFaction(faction);
                            settlement.Name = SettlementNameGenerator.GenerateSettlementName(settlement);
                            Find.WorldObjects.Add(settlement);

                            factionBases.Add(chosenTile);
                            placedBases.Add(chosenTile);

                            if (!factionProvinces.Contains(chosenProvince))
                            {
                                factionProvinces.Add(chosenProvince);
                                string fid = faction.GetUniqueLoadID();
                                if (!chosenProvince.owningFactionIds.Contains(fid))
                                {
                                    chosenProvince.owningFactionIds.Add(fid);
                                }
                            }
                        }
                    }
                }

                RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Placed {factionBases.Count} bases across {factionProvinces.Count} provinces for faction: {faction.Name}", "factions");
            }

            RoadGeneratorHelper.GenerateRoadsBetweenBases();

            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Custom Faction Generation and Placement completed successfully.", "factions");
            return false;
        }

        private static int FindBestTileInProvince(GeographicProvince province, List<int> sameFactionBases, List<int> allPlacedBases, Dictionary<int, float> tileScores)
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
                    float dist = Find.WorldGrid.ApproxDistanceInTiles(tile, otherBase);
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
                    float dist = Find.WorldGrid.ApproxDistanceInTiles(tile, otherBase);
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

        private static bool IsProvinceAdjacentToAny(GeographicProvince p, List<GeographicProvince> existing, SynapseRegionManager manager)
        {
            List<RimWorld.Planet.PlanetTile> neighbors = new List<RimWorld.Planet.PlanetTile>();
            foreach (int tile in p.tiles)
            {
                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(tile, neighbors);
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

        private static float GetProvinceDistance(GeographicProvince p1, GeographicProvince p2)
        {
            if (p1.tiles.Count == 0 || p2.tiles.Count == 0) return 9999f;
            return Find.WorldGrid.ApproxDistanceInTiles(p1.tiles[0], p2.tiles[0]);
        }
    }
}
