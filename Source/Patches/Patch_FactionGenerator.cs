using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(FactionGenerator), "GenerateFactionsIntoWorldLayer")]
    public static class Patch_FactionGenerator_GenerateFactionsIntoWorld
    {
        [HarmonyPrefix]
        public static bool Prefix(PlanetLayer layer, List<FactionDef> factions)
        {
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Hijacking FactionGenerator to expand factions list with dynamic clones...", "factions");

            if (factions == null) return true;

            // Calculate ideal count based on planet coverage.
            float coverage = Find.World.info.planetCoverage;
            int targetFactionCount = UnityEngine.Mathf.RoundToInt(coverage * 30f);
            if (targetFactionCount < 5) targetFactionCount = 5;

            // We need to categorize FactionDefs to ensure we have a good mix.
            var allowedDefs = DefDatabase<FactionDef>.AllDefs.Where(f => f.allowedArrivalTemperatureRange.Includes(0) && !f.isPlayer && !f.hidden).ToList();
            var outlanders = allowedDefs.Where(f => f.techLevel == TechLevel.Industrial && !f.hostileToFactionlessHumanlikes).ToList();
            var tribals = allowedDefs.Where(f => f.techLevel == TechLevel.Neolithic && !f.hostileToFactionlessHumanlikes).ToList();
            var pirates = allowedDefs.Where(f => f.hostileToFactionlessHumanlikes && f.techLevel >= TechLevel.Industrial).ToList();
            var empires = allowedDefs.Where(f => f.defName.Contains("Empire")).ToList();

            List<FactionDef> poolToClone = new List<FactionDef>();
            if (outlanders.Any()) poolToClone.AddRange(outlanders);
            if (tribals.Any()) poolToClone.AddRange(tribals);
            if (pirates.Any()) poolToClone.AddRange(pirates);
            if (empires.Any()) poolToClone.AddRange(empires);

            if (!poolToClone.Any())
            {
                // Fallback to vanilla if somehow we have no valid defs
                return true;
            }

            // Clone randomly from the pool until we reach our target count
            while (factions.Count < targetFactionCount)
            {
                var cloneDef = poolToClone.RandomElement();
                factions.Add(cloneDef);
            }

            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Expanded factions list to {factions.Count} definitions dynamically.", "factions");

            // We return true to let the vanilla generator handle the generation and settlement placement.
            return true;
        }
    }
}
