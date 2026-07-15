using System.Collections.Generic;
using System.Collections;
using MapModeFramework;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions
{
    public class MapMode_FactionTerritory : MapMode
    {
        private static readonly Dictionary<Faction, Material> factionMats = new Dictionary<Faction, Material>();
        private static readonly Dictionary<int, Faction> tileOwners = new Dictionary<int, Faction>();

        public override WorldLayer_MapMode WorldLayer => WorldLayer_FactionTerritory.Instance;
        public override bool CanToggleWater => false;

        public MapMode_FactionTerritory() { }
        public MapMode_FactionTerritory(MapModeDef def) : base(def) { }

        public static void CacheData()
        {
            factionMats.Clear();
            tileOwners.Clear();
            if (Find.FactionManager == null || Find.WorldGrid == null || Find.WorldObjects == null) return;

            // 1. Cache Materials for all visible factions on the main thread
            foreach (var faction in Find.FactionManager.AllFactionsVisible)
            {
                if (faction.Hidden || faction.IsPlayer) continue;
                Color color = faction.Color;
                color.a = 0.4f; // Transparent overlay so background mountains/rivers show
                factionMats[faction] = SolidColorMaterials.SimpleSolidColorMaterial(color, false);
            }

            // 2. Precalculate the closest faction owner for all land tiles
            int tilesCount = Find.WorldGrid.TilesCount;
            var settlements = Find.WorldObjects.Settlements;
            if (settlements == null || settlements.Count == 0) return;

            var settlementList = new List<Settlement>();
            foreach (var s in settlements)
            {
                if (s.Faction != null && !s.Faction.Hidden && !s.Faction.IsPlayer)
                {
                    settlementList.Add(s);
                }
            }

            if (settlementList.Count == 0) return;

            for (int i = 0; i < tilesCount; i++)
            {
                if (Find.WorldGrid[i].WaterCovered) continue;

                Vector3 tilePos = Find.WorldGrid.GetTileCenter(i);
                Settlement closestSettlement = null;
                float minSqDist = float.MaxValue;

                foreach (var settlement in settlementList)
                {
                    Vector3 settlementPos = Find.WorldGrid.GetTileCenter(settlement.Tile);
                    float sqDist = (tilePos - settlementPos).sqrMagnitude;
                    if (sqDist < minSqDist)
                    {
                        minSqDist = sqDist;
                        closestSettlement = settlement;
                    }
                }

                if (closestSettlement != null)
                {
                    float approxDist = Find.WorldGrid.ApproxDistanceInTiles(i, closestSettlement.Tile);
                    if (approxDist <= 18f)
                    {
                        tileOwners[i] = closestSettlement.Faction;
                    }
                }
            }
        }

        public override Material GetMaterial(int tile)
        {
            if (tileOwners.TryGetValue(tile, out var faction) && faction != null)
            {
                if (factionMats.TryGetValue(faction, out var mat))
                {
                    return mat;
                }
            }
            return BaseContent.ClearMat;
        }

        public override string GetTileLabel(int tile) => null;

        public override string GetTooltip(int tile)
        {
            if (tileOwners.TryGetValue(tile, out var faction) && faction != null)
            {
                int relation = faction.PlayerGoodwill;
                return $"Territory: {faction.Name}\nFaction type: {faction.def.LabelCap}\nGoodwill: {relation.ToStringWithSign()} ({faction.PlayerRelationKind.GetLabel()})";
            }
            return null;
        }
    }

    public class WorldLayer_FactionTerritory : WorldLayer_MapMode
    {
        public static WorldLayer_FactionTerritory Instance { get; private set; }

        public WorldLayer_FactionTerritory()
        {
            Instance = this;
        }

        public override IEnumerable Regenerate()
        {
            // Cache data on the main thread prior to async mesh generation
            MapMode_FactionTerritory.CacheData();

            foreach (object item in base.Regenerate())
            {
                yield return item;
            }
        }
    }
}
