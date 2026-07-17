using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MapModeFramework;
using Region = MapModeFramework.Region;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions
{
    public class MapMode_FactionTerritory : MapMode_Region
    {
        private const float MaxTerritoryCost = 24.0f;

        public override Material RegionMaterial => BaseContent.ClearMat;

        public MapMode_FactionTerritory() { }
        public MapMode_FactionTerritory(MapModeDef def) : base(def) { }

        public override void SetRegions()
        {
            if (!UnityData.IsInMainThread) return;

            // Rebuild the cached boundaries and clear current regions
            ClearCache();
            regions.Clear();

            if (Find.FactionManager == null || Find.WorldGrid == null || Find.WorldObjects == null) return;

            // 1. Gather all active visible non-player factions
            var factions = new List<Faction>();
            foreach (var faction in Find.FactionManager.AllFactionsVisible)
            {
                if (faction.Hidden || faction.IsPlayer) continue;
                factions.Add(faction);
            }

            if (factions.Count == 0) return;

            // 2. Identify all settlements belonging to these factions
            var factionSettlements = new Dictionary<Faction, List<int>>();
            foreach (var faction in factions)
            {
                factionSettlements[faction] = new List<int>();
            }

            var settlements = Find.WorldObjects.Settlements;
            if (settlements == null) return;

            foreach (var s in settlements)
            {
                if (s.Faction != null && factionSettlements.ContainsKey(s.Faction))
                {
                    factionSettlements[s.Faction].Add(s.Tile);
                }
            }

            // 3. Multi-source Dijkstra initialization
            var minCosts = new Dictionary<int, float>();
            var tileOwners = new Dictionary<int, Faction>();
            var pq = new SimplePriorityQueue<int>();

            foreach (var kvp in factionSettlements)
            {
                Faction faction = kvp.Key;
                foreach (int tile in kvp.Value)
                {
                    minCosts[tile] = 0f;
                    tileOwners[tile] = faction;
                    pq.Enqueue(tile, 0f);
                }
            }

            // 4. Run multi-source Dijkstra pathfinding
            var neighbors = new List<PlanetTile>();
            while (pq.Count > 0)
            {
                int currentTile = pq.Dequeue();
                float currentCost = minCosts[currentTile];
                Faction faction = tileOwners[currentTile];

                neighbors.Clear();
                Find.WorldGrid.GetTileNeighbors(currentTile, neighbors);

                foreach (var neighbor in neighbors)
                {
                    int neighborId = neighbor.tileId;
                    float stepCost = GetMovementCost(currentTile, neighborId);
                    if (stepCost >= 9999f) continue; // Impassable

                    float newCost = currentCost + stepCost;
                    if (newCost > MaxTerritoryCost) continue; // Out of bounds

                    if (!minCosts.TryGetValue(neighborId, out float existingCost) || newCost < existingCost)
                    {
                        minCosts[neighborId] = newCost;
                        tileOwners[neighborId] = faction;
                        pq.Enqueue(neighborId, newCost);
                    }
                }
            }

            // 5. Group owned tiles by faction and build region objects
            var factionTiles = new Dictionary<Faction, List<int>>();
            foreach (var faction in factions)
            {
                factionTiles[faction] = new List<int>();
            }

            foreach (var kvp in tileOwners)
            {
                factionTiles[kvp.Value].Add(kvp.Key);
            }

            foreach (var faction in factions)
            {
                List<int> tiles = factionTiles[faction];
                if (tiles.Count == 0) continue;

                // Color configuration
                Color baseColor = faction.Color;

                // Body material: Translucent version of the faction color
                Color bodyColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
                Material bodyMat = null;
                if (ShaderDatabase.MetaOverlay != null && BaseContent.WhiteTex != null)
                {
                    bodyMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, bodyColor, 3510);
                }
                if (bodyMat == null)
                {
                    Log.Warning($"[RimSynapse] Falling back to SimpleSolidColorMaterial for body of faction {faction.Name}");
                    bodyMat = SolidColorMaterials.SimpleSolidColorMaterial(bodyColor);
                }
                if (bodyMat == null)
                {
                    Log.Error($"[RimSynapse] bodyMat is STILL null for faction {faction.Name} after all attempts! Falling back to BaseContent.WhiteMat.");
                    bodyMat = BaseContent.WhiteMat;
                }
                else
                {
                    Log.Warning($"[RimSynapse] Successfully resolved bodyMat for faction {faction.Name}: {bodyMat.name}");
                }

                // Border material: Bold version of the faction color
                Color borderColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.85f);
                Material borderMat = null;
                if (ShaderDatabase.MetaOverlay != null && BaseContent.WhiteTex != null)
                {
                    borderMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, borderColor, 3510);
                }
                if (borderMat == null)
                {
                    Log.Warning($"[RimSynapse] Falling back to SimpleSolidColorMaterial for border of faction {faction.Name}");
                    borderMat = SolidColorMaterials.SimpleSolidColorMaterial(borderColor);
                }
                if (borderMat == null)
                {
                    Log.Error($"[RimSynapse] borderMat is STILL null for faction {faction.Name} after all attempts! Falling back to BaseContent.WhiteMat.");
                    borderMat = BaseContent.WhiteMat;
                }
                else
                {
                    Log.Warning($"[RimSynapse] Successfully resolved borderMat for faction {faction.Name}: {borderMat.name}");
                }

                float borderWidth = def?.RegionProperties?.borderWidth ?? 0.7f;
                bool doBorders = def?.RegionProperties?.doBorders ?? true;

                int relation = faction.PlayerGoodwill;
                string tooltipText = $"Territory: {faction.Name}\nFaction type: {faction.def.LabelCap}\nGoodwill: {relation.ToStringWithSign()} ({faction.PlayerRelationKind.GetLabel()})";

                Region region = new Region(
                    faction.Name,
                    tiles,
                    false, // skipBody = false
                    bodyMat,
                    doBorders,
                    borderMat,
                    borderWidth,
                    tooltipText
                );

                regions.Add(region);
            }
        }

        private static float GetMovementCost(int fromTileId, int toTileId)
        {
            if (Find.WorldGrid == null) return 1f;

            Tile tileData = Find.WorldGrid[toTileId];
            if (tileData == null) return 9999f;

            // Water coverage and impassable ranges act as hard boundaries
            if (tileData.hilliness == Hilliness.Impassable || 
                tileData.WaterCovered || 
                (tileData.PrimaryBiome != null && tileData.PrimaryBiome.impassable))
            {
                return 9999f;
            }

            float cost = 1.0f;

            // Biome movement difficulty resistance
            if (tileData.PrimaryBiome != null)
            {
                cost += Mathf.Max(0f, tileData.PrimaryBiome.movementDifficulty - 1f);
            }

            // Mountainous and large hill features act as natural boundary ridges
            if (tileData.hilliness == Hilliness.LargeHills)
            {
                cost += 5f;
            }
            else if (tileData.hilliness == Hilliness.Mountainous)
            {
                cost += 12f;
            }

            // Swampiness resistance
            if (tileData.swampiness > 0.1f)
            {
                cost += tileData.swampiness * 10f;
            }

            // River crossing resistance (natural boundaries)
            RiverDef river = Find.WorldGrid.GetRiverDef(fromTileId, toTileId);
            if (river != null)
            {
                if (river.defName.Contains("Huge") || river.defName.Contains("Large"))
                {
                    cost += 15f;
                }
                else
                {
                    cost += 8f;
                }
            }

            // Roads ease movement and extend faction reach
            RoadDef road = Find.WorldGrid.GetRoadDef(fromTileId, toTileId);
            if (road != null)
            {
                cost *= 0.4f;
            }

            return cost;
        }
    }

    /// <summary>
    /// Self-contained Binary Heap Priority Queue for Dijkstra.
    /// </summary>
    public class SimplePriorityQueue<T>
    {
        private readonly List<KeyValuePair<T, float>> elements = new List<KeyValuePair<T, float>>();

        public int Count => elements.Count;

        public void Enqueue(T item, float priority)
        {
            elements.Add(new KeyValuePair<T, float>(item, priority));
            int i = elements.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (elements[i].Value >= elements[parent].Value)
                    break;
                var temp = elements[i];
                elements[i] = elements[parent];
                elements[parent] = temp;
                i = parent;
            }
        }

        public T Dequeue()
        {
            int lastIndex = elements.Count - 1;
            var frontItem = elements[0].Key;
            elements[0] = elements[lastIndex];
            elements.RemoveAt(lastIndex);

            lastIndex--;
            int i = 0;
            while (true)
            {
                int leftChild = 2 * i + 1;
                int rightChild = 2 * i + 2;
                int best = i;

                if (leftChild <= lastIndex && elements[leftChild].Value < elements[best].Value)
                    best = leftChild;
                if (rightChild <= lastIndex && elements[rightChild].Value < elements[best].Value)
                    best = rightChild;

                if (best == i)
                    break;

                var temp = elements[i];
                elements[i] = elements[best];
                elements[best] = temp;
                i = best;
            }

            return frontItem;
        }
    }
}
