using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.RegionsAndTerritories;
using RimSynapse.RegionsAndTerritories.Economy;
using RTEmpires = RimSynapse.RegionsAndTerritories.Patches.RegionsAndTerritories_EmpiresPatch;

namespace RimSynapse.Factions.Patches
{
    /// <summary>
    /// The Empire hooks that ask this mod's simulation layers rather than Regions and
    /// Territories' geography.
    ///
    /// <para>These three postfixes/prefixes moved here when production, taxation, military reach
    /// and standing moved: they call <see cref="ProductionScalingUtility"/> and
    /// <see cref="MilitaryReachUtility"/>, and leaving them in R&amp;T would have made the world
    /// layer depend on the faction layer that already hard-depends on it. The rest of the Empire
    /// patch set — rewards, tithe plumbing, city classification, settlement placement, road
    /// overlay — is geography or Empire glue and stays in R&amp;T.</para>
    ///
    /// <para>Bound manually from <c>RimSynapseFactionsMod</c>, not by attribute, because the
    /// target methods live in an optional mod and must be resolved by reflection.</para>
    /// </summary>
    public static class Factions_EmpirePatch
    {
        private static int GetTileSafe(object obj)
        {
            if (obj == null) return -1;

            var prop = obj.GetType().GetProperty("Tile", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null)
            {
                var val = prop.GetValue(obj);
                if (val is int iVal) return iVal;
            }

            var field = obj.GetType().GetField("Tile", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
            {
                var val = field.GetValue(obj);
                if (val is int iVal) return iVal;
            }

            return -1;
        }

        /// <summary>
        /// The faction a foreign mod's holding belongs to, or null. Empire runs the player's
        /// colonies under a faction of its own, so falling back to <c>Faction.OfPlayer</c> at the
        /// call site is a fallback rather than the answer — asking the object first is what keeps
        /// the supply model measuring the territory that actually launched the operation.
        /// </summary>
        private static Faction GetFactionSafe(object obj)
        {
            if (obj == null) return null;

            var prop = obj.GetType().GetProperty("Faction", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.GetValue(obj) is Faction fromProperty) return fromProperty;

            var field = obj.GetType().GetField("Faction", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null && field.GetValue(obj) is Faction fromField) return fromField;

            return null;
        }

        public static double CalculateProductionBase_Postfix(double __result, object __instance)
        {
            try
            {
                if (__instance == null) return __result;

                // Extract settlement from ResourceFC instance
                var settlementField = __instance.GetType().GetField("settlement", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (settlementField == null) return __result;
                var settlement = settlementField.GetValue(__instance);
                if (settlement == null) return __result;

                // Extract Tile from WorldObject/Settlement
                int tileId = GetTileSafe(settlement);
                if (tileId == -1) return __result;

                // Extract def (ResourceTypeDef) from ResourceFC
                var defField = __instance.GetType().GetField("def", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (defField == null) return __result;
                var def = defField.GetValue(__instance);
                if (def == null) return __result;

                string defName = RTEmpires.GetDefNameSafe(def);
                if (string.IsNullOrEmpty(defName)) return __result;

                // 0.7: the abundance switch that used to live here has moved into
                // ProductionScalingUtility, which is where VOE and anything else the registry learns
                // about ask the same question. This postfix no longer knows what a province is.
                //
                // The whole model — abundance, labour, security, locality, settlement tier — is
                // applied here, at the base, so the clamp in ProductionEvaluator.Compose bounds one
                // product rather than two halves that can multiply past it. CalculateProductionMult
                // steps aside for exactly the resources this handles; see the note there.
                ResourceKind kind;
                if (!ProductionScalingUtility.TryResolveResourceKind(defName, out kind)) return __result;

                float modifier = ProductionScalingUtility.FactorFor(tileId, kind, RTEmpires.GetPlayerFaction());

                return __result * modifier;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[RimSynapse-Factions] Error in CalculateProductionBase_Postfix: {ex}", 992388);
                return __result;
            }
        }

        public static double CalculateProductionMult_Postfix(double __result, object __instance)
        {
            try
            {
                if (__instance == null) return __result;

                var settlementField = __instance.GetType().GetField("settlement", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (settlementField == null) return __result;
                var settlement = settlementField.GetValue(__instance);
                if (settlement == null) return __result;

                int tileId = GetTileSafe(settlement);
                if (tileId == -1) return __result;

                // 0.7: labour is part of the composed model now, and the model is applied at the
                // base. Applying a population curve here as well would count the same people twice,
                // so for any resource CalculateProductionBase handled, this stands down.
                //
                // It does not stand down for the rest. A resource R&T has no pool for still gets the
                // 0.6 population curve exactly as before — dropping it would quietly cut production
                // on every resource the table does not name, which is the opposite of what a
                // mod-agnostic layer is for.
                var defField = __instance.GetType().GetField("def", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                object def = defField != null ? defField.GetValue(__instance) : null;
                ResourceKind handled;
                if (def != null && ProductionScalingUtility.TryResolveResourceKind(RTEmpires.GetDefNameSafe(def), out handled))
                {
                    return __result;
                }

                if (Find.World == null) return __result;
                var regionManager = Find.World.GetComponent<SynapseRegionManager>();
                if (regionManager == null) return __result;

                var province = regionManager.GetProvinceForTile(tileId);
                if (province == null) return __result;

                float popMult = Economy.ProductionEvaluator.LabourFactor(province.currentPopulation);

                return __result * popMult;
            }
            catch (Exception ex)
            {
                Log.ErrorOnce($"[RimSynapse-Factions] Error in CalculateProductionMult_Postfix: {ex}", 992389);
                return __result;
            }
        }

        public static bool SendMilitary_Prefix(object squad, object location, object job, int timeToFinish, Faction enemy)
        {
            try
            {
                if (location == null) return true;

                // Resolve target tile ID
                int targetTileId = -1;
                if (location is int i)
                {
                    targetTileId = i;
                }
                else
                {
                    targetTileId = GetTileSafe(location);
                }

                if (targetTileId == -1) return true;

                // Resolve source tile ID from squad
                if (squad == null) return true;
                var settlementField = squad.GetType().GetField("settlement", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (settlementField == null) return true;
                var sourceSettlement = settlementField.GetValue(squad);
                if (sourceSettlement == null) return true;

                int sourceTileId = GetTileSafe(sourceSettlement);
                if (sourceTileId == -1) return true;

                // 0.7 Epic 5 children 1 and 2. This block used to carry the adjacency rule inline:
                // target province must be the source province or one of its neighbours, full stop.
                // It now asks the shared supply model, which names no mod and can therefore govern
                // any other mod's military action the same way.
                //
                // The change is a relaxation. An adjacent target costs 1 in the supply model and is
                // always inside the ceiling whoever holds the ground, so every operation that was
                // legal before is still legal; what is new is the deep strike along a corridor of
                // provinces the faction actually holds. It also means the militaryGovernance setting
                // finally controls something — the old check consulted no switch at all.
                Faction launching = GetFactionSafe(sourceSettlement) ?? Faction.OfPlayer;

                string reason;
                if (!MilitaryReachUtility.CanReach(sourceTileId, targetTileId, launching, out reason))
                {
                    Messages.Message(reason, MessageTypeDefOf.RejectInput);
                    return false; // Cancel SendMilitary execution
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[RimSynapse-Factions] Error in SendMilitary_Prefix: {ex}");
            }
            return true;
        }
    }
}
