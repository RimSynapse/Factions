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
            foreach (var faction in Find.FactionManager.AllFactions)
            {
                if (faction == null || faction.IsPlayer || faction.Hidden) continue;
                if (faction.leader == null || !faction.leader.RaceProps.Humanlike) continue;
                if (faction.leader.Dead) continue;

                var coreComp = faction.leader.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
                if (coreComp == null) continue;

                if (NeedsBackstory(faction.leader))
                {
                    targetLeader = faction.leader;
                    targetFaction = faction;
                    break;
                }
            }

            if (targetLeader == null || targetFaction == null) return false;

            var leaderCoreComp = targetLeader.TryGetComp<RimSynapse.Comps.SynapseCorePawnComp>();
            if (leaderCoreComp == null) return false;

            string factionHistoryContext = GetFactionHistoryContext(targetFaction);
            GenerateLeaderChildhoodMemory(targetLeader, targetFaction, leaderCoreComp, factionHistoryContext);
            return true;
        }

        private static string GetFactionHistoryContext(Faction faction)
        {
            if (!SynapseCore.IsModLoaded("RimSynapseStoryTeller") || faction == null) return "";

            try
            {
                foreach (var comp in Find.World.components)
                {
                    if (comp.GetType().Name == "SynapseStoryTellerWorldComponent")
                    {
                        var method = comp.GetType().GetMethod("GetOrCreateStoryTracker");
                        if (method != null)
                        {
                            var tracker = method.Invoke(comp, new object[] { faction.GetUniqueLoadID() });
                            if (tracker != null)
                            {
                                var historyField = tracker.GetType().GetField("factionHistory");
                                if (historyField != null)
                                {
                                    string history = historyField.GetValue(tracker) as string;
                                    if (!string.IsNullOrEmpty(history))
                                    {
                                        return $"\nFaction History (already established — your memories must be consistent with this):\n\"{history}\"";
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                RimSynapse.SynapseLogger.Warn("factions", $"[RimSynapse-Factions] Could not read faction history from StoryTeller: {ex.Message}");
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
            var psychAssembly = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageIdPlayerFacing.ToLower() == "rimsynapse.psychology")?.assemblies.loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "RimSynapsePsychology");
            if (psychAssembly != null)
            {
                var queryType = psychAssembly.GetType("RimSynapse.Psychology.API.SynapsePsychologyQuery");
                var method = queryType?.GetMethod("NeedsBackstory", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    return (bool)method.Invoke(null, new object[] { leader });
                }
            }
            return false;
        }

        private static void MarkBackstoryCreated(Pawn leader)
        {
            var psychAssembly = LoadedModManager.RunningModsListForReading.FirstOrDefault(m => m.PackageIdPlayerFacing.ToLower() == "rimsynapse.psychology")?.assemblies.loadedAssemblies.FirstOrDefault(a => a.GetName().Name == "RimSynapsePsychology");
            if (psychAssembly != null)
            {
                var queryType = psychAssembly.GetType("RimSynapse.Psychology.API.SynapsePsychologyQuery");
                var method = queryType?.GetMethod("MarkBackstoryCreated", BindingFlags.Public | BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, new object[] { leader });
                }
            }
        }
    }
}
