using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace RimSynapse.Factions
{
    public static class DwellingStructureGenerator
    {
        public static void Generate(Map map, int count)
        {
            if (map == null || count <= 0) return;

            // Cap the maximum number of dwellings generated on a single map to 5 to avoid overcrowding
            int targetCount = Math.Min(count, 5);
            List<IntVec3> spawnedBaseLocs = new List<IntVec3>();

            for (int i = 0; i < targetCount; i++)
            {
                IntVec3 startSpot = FindValidSpot(map, spawnedBaseLocs);
                if (startSpot == IntVec3.Invalid) continue;

                // baseLoc is the bottom-left corner of the homestead area
                IntVec3 baseLoc = startSpot - new IntVec3(8, 0, 8);
                spawnedBaseLocs.Add(baseLoc);

                // Fetch defs safely
                ThingDef wallDef = ThingDefOf.Wall;
                ThingDef doorDef = ThingDefOf.Door;
                ThingDef woodLog = ThingDefOf.WoodLog;
                ThingDef campfireDef = ThingDefOf.Campfire;
                
                ThingDef fenceDef = DefDatabase<ThingDef>.GetNamed("Fence", false) ?? ThingDefOf.Wall;
                ThingDef fenceGateDef = DefDatabase<ThingDef>.GetNamed("FenceGate", false) ?? ThingDefOf.Door;
                ThingDef penMarkerDef = DefDatabase<ThingDef>.GetNamed("PenMarker", false);

                TerrainDef woodFloor = DefDatabase<TerrainDef>.GetNamed("WoodPlankFloor", false) ?? TerrainDefOf.Concrete;
                TerrainDef soilTerrain = TerrainDefOf.Soil;

                // 1. Generate 4x4 interior wood roofed building (6x6 footprint with walls)
                // Walkable interior: x in [baseLoc.x + 1, baseLoc.x + 4], z in [baseLoc.z + 1, baseLoc.z + 4]
                for (int x = baseLoc.x; x <= baseLoc.x + 5; x++)
                {
                    for (int z = baseLoc.z; z <= baseLoc.z + 5; z++)
                    {
                        IntVec3 c = new IntVec3(x, 0, z);
                        if (!c.InBounds(map)) continue;

                        // Clear vegetation/rubble
                        ClearBlockers(c, map);

                        // Place walls on borders
                        bool isBorderX = (x == baseLoc.x || x == baseLoc.x + 5);
                        bool isBorderZ = (z == baseLoc.z || z == baseLoc.z + 5);

                        if (isBorderX || isBorderZ)
                        {
                            // Leave room for door at the top border (x = baseLoc.x + 3, z = baseLoc.z + 5)
                            if (x == baseLoc.x + 3 && z == baseLoc.z + 5)
                            {
                                SpawnThing(doorDef, woodLog, c, map);
                            }
                            else
                            {
                                SpawnThing(wallDef, woodLog, c, map);
                            }
                        }
                        else
                        {
                            // Interior floors
                            map.terrainGrid.SetTerrain(c, woodFloor);
                        }

                    // Add roof to the building
                    map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                }
            }

            // 1.5 Spawn a simple wooden bed inside the building
            IntVec3 bedLoc = baseLoc + new IntVec3(1, 0, 1);
            ClearBlockers(bedLoc, map);
            Thing bedThing = ThingMaker.MakeThing(ThingDefOf.Bed, woodLog);
            GenSpawn.Spawn(bedThing, bedLoc, map);
            Building_Bed bed = bedThing as Building_Bed;

                // 2. Campfire outside (near the door)
                IntVec3 campfireLoc = new IntVec3(baseLoc.x + 3, 0, baseLoc.z + 8);
                ClearBlockers(campfireLoc, map);
                SpawnThing(campfireDef, null, campfireLoc, map);

                // Determine whether to spawn a pen OR a field (50/50 chance, seeded by location)
                System.Random random = new System.Random(baseLoc.x ^ baseLoc.z);
                bool spawnPen = random.NextDouble() < 0.5;

                if (spawnPen)
                {
                    // 3. An empty 8x8 penned in area (fence)
                    // Footprint: x in [baseLoc.x, baseLoc.x + 7], z in [baseLoc.z + 11, baseLoc.z + 18]
                    for (int x = baseLoc.x; x <= baseLoc.x + 7; x++)
                    {
                        for (int z = baseLoc.z + 11; z <= baseLoc.z + 18; z++)
                        {
                            IntVec3 c = new IntVec3(x, 0, z);
                            if (!c.InBounds(map)) continue;

                            ClearBlockers(c, map);

                            bool isBorderX = (x == baseLoc.x || x == baseLoc.x + 7);
                            bool isBorderZ = (z == baseLoc.z + 11 || z == baseLoc.z + 18);

                            if (isBorderX || isBorderZ)
                            {
                                // Place fence gate at bottom border (x = baseLoc.x + 4, z = baseLoc.z + 11)
                                if (x == baseLoc.x + 4 && z == baseLoc.z + 11)
                                {
                                    SpawnThing(fenceGateDef, woodLog, c, map);
                                }
                                else
                                {
                                    SpawnThing(fenceDef, woodLog, c, map);
                                }
                            }
                        }
                    }

                    // Spawn a Pen Marker inside the fenced area
                    if (penMarkerDef != null)
                    {
                        IntVec3 markerLoc = new IntVec3(baseLoc.x + 3, 0, baseLoc.z + 15);
                        ClearBlockers(markerLoc, map);
                        SpawnThing(penMarkerDef, woodLog, markerLoc, map);
                    }
                }
                else
                {
                    // 4. An 8x8 crop field (registered as a growing zone with random plant type)
                    Zone_Growing zone = new Zone_Growing(map.zoneManager);
                    map.zoneManager.RegisterZone(zone);

                    // Pick a random plant type from common crops
                    ThingDef[] cropsList = new ThingDef[]
                    {
                        ThingDefOf.Plant_Potato,
                        DefDatabase<ThingDef>.GetNamed("Plant_Corn", false),
                        DefDatabase<ThingDef>.GetNamed("Plant_Rice", false),
                        DefDatabase<ThingDef>.GetNamed("Plant_Cotton", false),
                        DefDatabase<ThingDef>.GetNamed("Plant_Healroot", false)
                    };
                    
                    // Filter out nulls
                    List<ThingDef> validCrops = cropsList.Where(crop => crop != null).ToList();
                    ThingDef selectedCrop = validCrops.Count > 0 ? validCrops[random.Next(validCrops.Count)] : ThingDefOf.Plant_Potato;

                    for (int x = baseLoc.x + 7; x <= baseLoc.x + 14; x++)
                    {
                        for (int z = baseLoc.z; z <= baseLoc.z + 7; z++)
                        {
                            IntVec3 c = new IntVec3(x, 0, z);
                            if (!c.InBounds(map)) continue;

                            ClearBlockers(c, map);
                            map.terrainGrid.SetTerrain(c, soilTerrain);
                            zone.AddCell(c);

                            // Spawn already growing plant
                            Plant plant = (Plant)ThingMaker.MakeThing(selectedCrop);
                            plant.Growth = Rand.Range(0.15f, 0.85f);
                            GenSpawn.Spawn(plant, c, map);
                        }
                    }
                    zone.SetPlantDefToGrow(selectedCrop);
                }

                // 5. Spawn a neutral resident pawn (66% chance)
                if (Rand.Value < 0.66f)
                {
                    Faction faction = Find.FactionManager.FirstFactionOfDef(FactionDefOf.OutlanderCivil);
                    if (faction == null)
                    {
                        faction = Find.FactionManager.AllFactions.FirstOrDefault(f => !f.HostileTo(Faction.OfPlayer) && !f.def.isPlayer);
                    }
                    if (faction == null)
                    {
                        faction = Find.FactionManager.OfAncients;
                    }

                    PawnKindDef kind = PawnKindDefOf.Colonist;
                    if (faction.def.defName.Contains("Tribal"))
                    {
                        kind = DefDatabase<PawnKindDef>.GetNamed("Tribal_Warrior", false) ?? PawnKindDefOf.Colonist;
                    }

                    PawnGenerationRequest request = new PawnGenerationRequest(
                        kind,
                        faction: faction,
                        context: PawnGenerationContext.NonPlayer
                    );
                    Pawn pawn = PawnGenerator.GeneratePawn(request);

                    // Spawn pawn inside or near the house
                    IntVec3 spawnLoc = baseLoc + new IntVec3(3, 0, 3);
                    if (spawnLoc.InBounds(map))
                    {
                        GenSpawn.Spawn(pawn, spawnLoc, map);

                        // Assign the bed ownership to the pawn
                        if (bed != null)
                        {
                            pawn.ownership.ClaimBedIfNonMedical(bed);
                        }
                        
                        // Assign to defend/live around their house
                        LordMaker.MakeNewLord(
                            faction,
                            new LordJob_DefendPoint(spawnLoc),
                            map,
                            new List<Pawn> { pawn }
                        );
                    }
                }
            }
        }

        private static IntVec3 FindValidSpot(Map map, List<IntVec3> spawnedBaseLocs)
        {
            // Look for a flat, building-friendly starting area
            for (int i = 0; i < 3000; i++)
            {
                IntVec3 cell = CellFinder.RandomCell(map);
                if (cell.Walkable(map) && 
                    !cell.Fogged(map) && 
                    cell.GetTerrain(map).affordances.Contains(TerrainAffordanceDefOf.Heavy) && 
                    cell.x > 25 && cell.x < map.Size.x - 25 && cell.z > 25 && cell.z < map.Size.z - 25)
                {
                    IntVec3 baseLoc = cell - new IntVec3(8, 0, 8);
                    
                    // Check distance from already spawned dwellings
                    bool tooClose = false;
                    foreach (var other in spawnedBaseLocs)
                    {
                        if (baseLoc.DistanceToSquared(other) < 25 * 25) // at least 25 tiles away
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    // Check if 20x20 area is completely clear of water and existing structures/mountains/geysers
                    bool clear = true;
                    for (int x = -2; x <= 22; x++)
                    {
                        for (int z = -2; z <= 22; z++)
                        {
                            IntVec3 c = baseLoc + new IntVec3(x, 0, z);
                            if (!c.InBounds(map))
                            {
                                clear = false;
                                break;
                            }
                            var terrain = c.GetTerrain(map);
                            if (terrain.IsWater || c.GetEdifice(map) != null)
                            {
                                clear = false;
                                break;
                            }
                        }
                        if (!clear) break;
                    }
                    if (clear) return cell;
                }
            }

            return IntVec3.Invalid;
        }

        private static void ClearBlockers(IntVec3 cell, Map map)
        {
            var blockers = cell.GetThingList(map).ToList();
            foreach (var b in blockers)
            {
                if (b.def.destroyable && b.def.category != ThingCategory.Pawn)
                {
                    b.Destroy();
                }
            }
        }

        private static void SpawnThing(ThingDef def, ThingDef stuff, IntVec3 cell, Map map)
        {
            if (def == null) return;
            Thing thing = ThingMaker.MakeThing(def, stuff);
            GenSpawn.Spawn(thing, cell, map);
        }
    }
}
