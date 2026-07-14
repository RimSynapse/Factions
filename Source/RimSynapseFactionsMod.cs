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
            RimSynapse.SynapseLog.Info("factions", "[RimSynapse-Factions] Initializing Mod...");
            
            var harmony = new Harmony("rimsynapse.factions");
            harmony.PatchAll();
            
            RimSynapse.SynapseLog.Info("factions", "[RimSynapse-Factions] Harmony Patches applied.");
            
            ModHandle = new RimSynapse.SynapseModHandle("rimsynapse.factions", "RimSynapse Factions");
            
            // Register the population calculation delegate to RimSynapse-Core
            RimSynapse.SynapseCoreWorldComponent.GetPopulationDensityDelegate = PopulationDensityUtility.GetPopulationAtTile;
            
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
    }
}
