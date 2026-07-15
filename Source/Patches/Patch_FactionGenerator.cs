using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(FactionGenerator), "GenerateFactionsIntoWorld")]
    public static class Patch_FactionGenerator_GenerateFactionsIntoWorld
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Hijacking FactionGenerator to spawn dynamic clones...", "factions");

            // Calculate ideal count based on planet coverage.
            // Vanilla defaults to around 5-7. If coverage is 30% (default), maybe we want 10 factions.
            // If coverage is 100%, we want 30 factions.
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

            int currentCount = 0;
            
            // First pass: spawn exactly 1 of every required faction (Vanilla logic)
            foreach (var def in DefDatabase<FactionDef>.AllDefs)
            {
                if (def.requiredCountAtGameStart > 0)
                {
                    for (int i = 0; i < def.requiredCountAtGameStart; i++)
                    {
                        Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(def, default(IdeoGenerationParms), true));
                        Find.FactionManager.Add(faction);
                        currentCount++;
                    }
                }
            }

            // Second pass: clone randomly from the pool until we reach our target count
            while (currentCount < targetFactionCount)
            {
                var cloneDef = poolToClone.RandomElement();
                // Create a clone
                Faction faction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(cloneDef, default(IdeoGenerationParms), true));
                
                // We mark it uniquely in our WorldComponent so the LLM knows it's a clone/new state
                Find.FactionManager.Add(faction);
                currentCount++;
            }

            RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Spawned {currentCount} total factions dynamically.", "factions");

            // We return false to skip the vanilla generator, since we handled it all.
            return false;
        }
    }
}
