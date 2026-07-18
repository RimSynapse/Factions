using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using System.Reflection;

namespace RimSynapse.Factions.LeaderGeneration
{
    /// <summary>
    /// Entry point and orchestration for generating AI-driven leader backstories.
    /// Prompt construction and LLM callbacks are in LeaderPromptBuilder.cs (partial class).
    /// </summary>
    public static partial class SynapseFactionLeaderGenerator
    {
        public static bool TriggerLeaderBackstoryGeneration()
        {
            if (Current.ProgramState != ProgramState.Playing) return false;
            if (Find.FactionManager == null) return false;

            Pawn targetLeader = null;
            Faction targetFaction = null;
            RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] Starting TriggerLeaderBackstoryGeneration. Factions count: {Find.FactionManager.AllFactionsListForReading.Count}");
            foreach (var faction in Find.FactionManager.AllFactionsListForReading)
            {
                if (faction == null) continue;
                RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] Checking faction: {faction.Name} (def: {faction.def?.defName}, player: {faction.IsPlayer}, hidden: {faction.Hidden})");
                if (faction.IsPlayer || (faction.def != null && faction.def.hidden)) continue;
                
                RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] Faction {faction.Name} leader: {faction.leader?.LabelCap ?? "null"} (humanlike: {faction.leader?.RaceProps?.Humanlike}, dead: {faction.leader?.Dead})");
                if (faction.leader == null || !faction.leader.RaceProps.Humanlike) continue;
                if (faction.leader.Dead) continue;

                var coreComp = faction.leader.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] Faction {faction.Name} leader coreComp: {coreComp != null}");
                if (coreComp == null) continue;

                if (NeedsBackstory(faction.leader))
                {
                    targetLeader = faction.leader;
                    targetFaction = faction;
                    break;
                }
            }

            if (targetLeader == null || targetFaction == null)
            {
                RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] No eligible target leader found. targetLeader null: {targetLeader == null}, targetFaction null: {targetFaction == null}");
                return false;
            }

            var leaderCoreComp = targetLeader.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (leaderCoreComp == null) return false;

            // Step 1 of 3-step pipeline: Generate Faction History first if missing
            var stWorldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (stWorldComp != null)
            {
                var storyTracker = stWorldComp.GetOrCreateStoryTracker(targetFaction.GetUniqueLoadID());
                if (string.IsNullOrEmpty(storyTracker.factionHistory))
                {
                    SynapseFactionEvaluator.EvaluateFaction(targetFaction);
                    RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] Triggered Faction History generation for {targetFaction.Name} before leader backstory.");
                    return true;
                }
            }

            string factionHistoryContext = GetFactionHistoryContext(targetFaction);
            GenerateLeaderChildhoodMemory(targetLeader, targetFaction, leaderCoreComp, factionHistoryContext);
            return true;
        }

        private static string GetFactionHistoryContext(Faction faction)
        {
            if (faction == null) return "";
            var stWorldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (stWorldComp != null)
            {
                var storyTracker = stWorldComp.factionStoryTrackers.Find(t => t.factionId == faction.GetUniqueLoadID());
                if (storyTracker != null && !string.IsNullOrEmpty(storyTracker.factionHistory))
                {
                    return $"\nFaction History (already established — your memories must be consistent with this):\n\"{storyTracker.factionHistory}\"";
                }
            }
            return "";
        }

        private static string FormatSkillGains(BackstoryDef backstory)
        {
            if (backstory?.skillGains == null || backstory.skillGains.Count == 0) return "None";
            var parts = new List<string>();
            foreach (var sg in backstory.skillGains)
            {
                string sign = sg.amount >= 0 ? "+" : "";
                parts.Add($"{sign}{sg.amount} {sg.skill.label}");
            }
            return string.Join(", ", parts);
        }

        private static string FormatDisabledWork(BackstoryDef backstory)
        {
            if (backstory == null || backstory.workDisables == WorkTags.None) return "";
            var disabled = new List<string>();
            foreach (WorkTags tag in Enum.GetValues(typeof(WorkTags)))
            {
                if (tag == WorkTags.None) continue;
                if ((backstory.workDisables & tag) != 0) disabled.Add(tag.ToString());
            }
            return disabled.Count > 0 ? string.Join(", ", disabled) : "";
        }

        private static bool NeedsBackstory(Pawn leader)
        {
            RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] NeedsBackstory check for {leader.Name.ToStringShort}");
            var psychAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "RimSynapsePsychology");
            if (psychAssembly != null)
            {
                var queryType = psychAssembly.GetType("RimSynapse.Psychology.API.SynapsePsychology");
                var method = queryType?.GetMethod("NeedsBackstory", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    bool res = (bool)method.Invoke(null, new object[] { leader });
                    RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] NeedsBackstory method call returned: {res}");
                    return res;
                }
                else
                {
                    RimSynapse.SynapseLogger.Warning("[RimSynapse-Factions] NeedsBackstory method not found on SynapsePsychology.");
                }
            }
            else
            {
                RimSynapse.SynapseLogger.Warning("[RimSynapse-Factions] RimSynapsePsychology assembly not found in AppDomain.");
            }
            return false;
        }

        private static void MarkBackstoryCreated(Pawn leader)
        {
            var psychAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "RimSynapsePsychology");
            if (psychAssembly != null)
            {
                var queryType = psychAssembly.GetType("RimSynapse.Psychology.API.SynapsePsychology");
                var method = queryType?.GetMethod("MarkBackstoryCreated", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, new object[] { leader });
                }
            }
        }
    }
}
