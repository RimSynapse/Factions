using HarmonyLib;
using Verse;
using System.Linq;

namespace RimSynapse.Factions
{
    public class RimSynapseFactionsMod : Mod
    {
        public static RimSynapse.SynapseModHandle ModHandle;
        
        public RimSynapseFactionsMod(ModContentPack content) : base(content)
        {
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Initializing Mod...", "factions");
            
            var harmony = new Harmony("rimsynapse.factions");
            harmony.PatchAll();
            
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Harmony Patches applied.", "factions");
            
            ModHandle = new RimSynapse.SynapseModHandle("rimsynapse.factions", "RimSynapse Factions");
            
            // Register the population calculation delegate to RimSynapse-Core
            RimSynapse.SynapseCoreWorldComponent.GetPopulationDensityDelegate = PopulationDensityUtility.GetPopulationAtTile;

            // Subscribe to the Core narrative context hooks
            RimSynapse.SynapseLetterContextHook.OnGatherLetterContext += GatherFactionLetterContext;
            
            // Faction Leaders are generated via the Factions mod, 
            // but this feature is made significantly better when RimSynapse - Psychology is active!
            // It uses Psychology's memory tracking and trait systems.
            if (ModsConfig.IsActive("rimsynapse.psychology"))
            {
                RimSynapse.SynapseClient.RegisterOpportunisticTask(ModHandle, "Factions_LeaderBackstory",
                    RimSynapse.Factions.LeaderGeneration.SynapseFactionLeaderGenerator.TriggerLeaderBackstoryGeneration,
                    new RimSynapse.Internal.OpportunisticTaskConfig
                    {
                        Label = "Leader Backstory",
                        Description = "Generates AI backstories for all faction leaders (World VIPs). Runs after faction history to use it as context.",
                        Priority = 6, // Lower priority than colonists
                        Weight = 1.5f,
                        CooldownTicks = 5000
                    });
            }
        }

        private static void GatherFactionLetterContext(Letter letter, Pawn asker, System.Text.StringBuilder contextBuilder)
        {
            if (asker == null || asker.Faction == null || asker.Faction.IsPlayer || asker.Faction.Hidden) return;

            // 1. Last Settlement Extinction Check
            if (Find.WorldObjects?.Settlements != null)
            {
                int settlementCount = Find.WorldObjects.Settlements.Count(s => s.Faction == asker.Faction);
                if (settlementCount == 1)
                {
                    contextBuilder.AppendLine($"- Faction Crisis: Your faction ({asker.Faction.Name}) is on the brink of total annihilation and is down to its final settlement on the planet. You sound panicked, highly desperate, and extremely hopeful that the player will rescue you.");
                }
            }

            // 2. Inject LLM-generated Geopolitical Faction History Lore
            var worldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (worldComp != null && worldComp.factionStoryTrackers != null)
            {
                var storyTracker = worldComp.factionStoryTrackers.Find(t => t.factionId == asker.Faction.GetUniqueLoadID());
                if (storyTracker != null && !string.IsNullOrEmpty(storyTracker.factionHistory))
                {
                    contextBuilder.AppendLine($"- Faction History/Background Lore: {storyTracker.factionHistory}");
                }
            }
        }
    }
}
