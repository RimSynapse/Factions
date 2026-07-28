using RimWorld;
using RimSynapse.RegionsAndTerritories;
using RimWorld.Planet;
using RimSynapse.RegionsAndTerritories.Integration;
using RimSynapse.Factions.Sizing;
using Verse;

namespace RimSynapse.Factions
{
    /// <summary>
    /// The live-game entry point for settlement size tiers.
    ///
    /// Same shape as <see cref="WorldObjectPlacementUtility"/>: this file gathers the numbers, and
    /// <see cref="SettlementSizeEvaluator"/> — which knows nothing about RimWorld — decides what
    /// they mean. Every mod's holdings come through here, because population and level are read
    /// from Epic 1's adapter registry rather than from concrete types.
    /// </summary>
    public static class SettlementSizeUtility
    {
        /// <summary>
        /// The tier of a world object, or <see cref="SettlementTier.None"/> if it has none — which
        /// includes every object when the feature is switched off.
        /// </summary>
        public static SettlementTier TierOf(WorldObject obj)
        {
            if (obj == null) return SettlementTier.None;
            if (!WorldObjectIntegrationSettings.SettlementTiersActive) return SettlementTier.None;

            WorldObjectKind kind = WorldObjectClassifier.Classify(obj);
            if (SettlementSizeEvaluator.MaxTierFor(kind) == SettlementTier.None) return SettlementTier.None;

            // One reflection call, not two: this runs from the inspect pane, and TryGetLevel walks
            // a list of candidate member names on every invocation.
            int level, maxLevel;
            if (!WorldObjectAdapterRegistry.TryGetLevel(obj, out level, out maxLevel))
            {
                level = 0;
                maxLevel = 0;
            }

            return SettlementSizeEvaluator.Classify(kind, PopulationOf(obj, kind), 0, level, maxLevel);
        }

        /// <summary>
        /// Population from the owning mod if it exposes one, falling back to R&amp;T's own estimate
        /// for plain settlements. A mod that tracks its colonies' headcount knows better than we do.
        ///
        /// <para>Public because Epic 6's summary needs the same number for the same holding, and a
        /// second implementation of "how many people live here" is exactly the kind of near-duplicate
        /// that drifts apart over two releases and then has to be reconciled by whoever notices.</para>
        /// </summary>
        public static int PopulationOf(WorldObject obj, WorldObjectKind kind)
        {
            int population;
            if (WorldObjectAdapterRegistry.TryGetPopulation(obj, out population) && population > 0)
            {
                return population;
            }

            Settlement settlement = obj as Settlement;
            if (settlement != null)
            {
                return PopulationDensityUtility.GetSettlementPopulation(settlement);
            }

            return 0;
        }

        /// <summary>
        /// Production multiplier for this holding's tier. Safe to apply unconditionally: an
        /// untiered holding, and every holding when tiers are off, returns a neutral 1.
        /// </summary>
        public static float ProductionScaleOf(WorldObject obj)
        {
            return SettlementSizeRules.ProductionScale(TierOf(obj));
        }

        /// <summary>How far this holding's claim reaches, in tiles. Zero when it has no tier.</summary>
        public static int TerritoryFootprintOf(WorldObject obj)
        {
            return SettlementSizeRules.TerritoryFootprint(TierOf(obj));
        }

        /// <summary>Residents this holding can support, or zero meaning no tier-imposed cap.</summary>
        public static int PopulationCapacityOf(WorldObject obj)
        {
            return SettlementSizeRules.PopulationCapacity(TierOf(obj));
        }

        /// <summary>
        /// The largest holding standing on a tile, for the inspect pane. Returns null when the tile
        /// carries nothing that has a tier.
        /// </summary>
        public static WorldObject LargestTieredObjectAt(int tileId, out SettlementTier tier)
        {
            tier = SettlementTier.None;
            WorldObject best = null;

            if (Find.WorldObjects == null) return null;

            var all = Find.WorldObjects.AllWorldObjects;
            for (int i = 0; i < all.Count; i++)
            {
                WorldObject obj = all[i];
                if (obj == null || obj.Tile.tileId != tileId) continue;

                SettlementTier candidate = TierOf(obj);
                if (candidate == SettlementTier.None) continue;

                if (best == null || (int)candidate > (int)tier)
                {
                    best = obj;
                    tier = candidate;
                }
            }

            return best;
        }
    }
}
