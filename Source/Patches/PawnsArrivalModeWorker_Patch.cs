using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using RimSynapse.Factions.Models;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(PawnsArrivalModeWorker), "Arrive")]
    public static class PawnsArrivalModeWorker_Arrive_Patch
    {
        public static void Postfix(List<Pawn> pawns, IncidentParms parms)
        {
            if (pawns == null || pawns.Count == 0 || parms.faction == null) return;
            if (parms.faction.IsPlayer || parms.faction.Hidden) return;

            var stWorldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (stWorldComp == null) return;

            var tracker = stWorldComp.GetOrCreateStoryTracker(parms.faction.GetUniqueLoadID());
            
            // Check if there's an active hidden agenda related to internal politics/rivalries
            var activeAgendas = tracker.historicalAgendas.Where(a => 
                !a.discoveredByPlayer && 
                (a.hiddenAgenda.ToLower().Contains("rival") || 
                 a.hiddenAgenda.ToLower().Contains("politic") || 
                 a.hiddenAgenda.ToLower().Contains("assassin") || 
                 a.hiddenAgenda.ToLower().Contains("cull") ||
                 a.hiddenAgenda.ToLower().Contains("rebel"))
            ).ToList();

            if (activeAgendas.Count > 0)
            {
                // Find a suitable human pawn in the raid
                var candidate = pawns.Where(p => p.RaceProps.Humanlike && !p.Dead).RandomElementWithFallback();
                if (candidate != null)
                {
                    var hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail("Synapse_PoliticalRival");
                    if (hediffDef != null && !candidate.health.hediffSet.HasHediff(hediffDef))
                    {
                        candidate.health.AddHediff(hediffDef);
                        RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] Spawned Political Rival ({candidate.Name}) in raid from {parms.faction.Name} due to active hidden agenda.");
                    }
                }
            }
        }
    }
}
