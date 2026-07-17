using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;
using RimSynapse.Factions.Models;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(Building_CommsConsole), "GetFloatMenuOptions")]
    public static class Building_CommsConsole_GetFloatMenuOptions_Patch
    {
        public static void Postfix(Building_CommsConsole __instance, Pawn myPawn, ref IEnumerable<FloatMenuOption> __result)
        {
            if (!myPawn.CanReserve(__instance, 1, -1, null, false)) return;
            if (!myPawn.CanReach(__instance, PathEndMode.InteractionCell, Danger.Some, false, false, TraverseMode.ByPawn)) return;
            if (myPawn.skills.GetSkill(SkillDefOf.Social).TotallyDisabled) return;

            var worldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (worldComp == null) return;

            List<FloatMenuOption> options = new List<FloatMenuOption>(__result);

            // Find all discovered hidden agendas
            var discoveredAgendas = new List<(Faction, HiddenAgendaLog)>();
            foreach (var faction in Find.FactionManager.AllFactionsVisible)
            {
                var tracker = worldComp.GetOrCreateStoryTracker(faction.GetUniqueLoadID());
                foreach (var agenda in tracker.historicalAgendas)
                {
                    if (agenda.discoveredByPlayer)
                    {
                        discoveredAgendas.Add((faction, agenda));
                    }
                }
            }

            if (discoveredAgendas.Count > 0)
            {
                options.Add(new FloatMenuOption("Sow Discord (Leak Hidden Agendas)...", () =>
                {
                    List<FloatMenuOption> agendaOptions = new List<FloatMenuOption>();
                    foreach (var pair in discoveredAgendas)
                    {
                        var faction = pair.Item1;
                        var agenda = pair.Item2;
                        agendaOptions.Add(new FloatMenuOption($"Leak {faction.Name}'s secret: {agenda.hiddenAgenda}", () =>
                        {
                            Job job = JobMaker.MakeJob(JobDefOf.UseCommsConsole, __instance);
                            myPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                            // We use a custom delayed action here by storing it in the World Component or triggering immediately
                            // For simplicity, we just trigger the effect immediately upon selecting the option, though ideally it happens when the job finishes.
                            TriggerDiscordLeak(faction, agenda, myPawn);
                        }));
                    }
                    Find.WindowStack.Add(new FloatMenu(agendaOptions));
                }));
            }

            __result = options;
        }

        private static void TriggerDiscordLeak(Faction targetFaction, HiddenAgendaLog agenda, Pawn communicator)
        {
            // Simulate the communications array spreading the information.
            // There is a random chance the contact spreads it to allies or the WorldNews network.
            bool reachesNews = Rand.Chance(0.2f);
            
            Find.LetterStack.ReceiveLetter(
                "Agenda Leaked",
                $"You have leaked the hidden agenda of {targetFaction.Name} across local comms channels.\n\nSecret: {agenda.hiddenAgenda}\n\nTheir settlements will suffer demographic and economic turmoil as the truth spreads.",
                LetterDefOf.PositiveEvent
            );

            // Trigger the internal penalty (using the crisis system)
            var stWorldComp = Find.World.GetComponent<SynapseFactionsWorldComponent>();
            if (stWorldComp != null)
            {
                // Find a random settlement of theirs
                var settlements = Find.WorldObjects.Settlements.Where(s => s.Faction == targetFaction).ToList();
                if (settlements.Count > 0)
                {
                    var targetSettlement = settlements.RandomElement();
                    var tracker = stWorldComp.GetOrCreateSettlementTracker(targetSettlement);
                    
                    var crisis = new SettlementCrisis
                    {
                        crisisType = "Internal Rebellion",
                        daysElapsed = 0,
                        currentSeverity = 0.2f // Huge impact
                    };
                    tracker.activeCrises.Add(crisis);
                }
            }

            // Remove it from the tracker so it can't be spammed
            var factionTracker = stWorldComp?.GetOrCreateStoryTracker(targetFaction.GetUniqueLoadID());
            if (factionTracker != null)
            {
                factionTracker.historicalAgendas.Remove(agenda);
            }
        }
    }
}
