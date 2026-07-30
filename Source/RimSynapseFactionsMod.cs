using HarmonyLib;
using Verse;
using RimWorld;
using System.Linq;
using RimSynapse.RegionsAndTerritories;

namespace RimSynapse.Factions
{
    public class RimSynapseFactionsMod : Mod
    {
        /// <summary>
        /// The Empire hooks that ask this mod's simulation layers.
        ///
        /// <para>Bound here, and not in Regions and Territories, because the methods they call —
        /// <c>ProductionScalingUtility</c> and <c>MilitaryReachUtility</c> — live in this assembly
        /// as of 0.7. R&amp;T still binds its own Empire patches (rewards, tithe plumbing, city
        /// classification, settlement placement, road overlay); the two patch sets are independent
        /// and Empire works with either mod alone.</para>
        ///
        /// <para>Resolved by reflection rather than by attribute: the target types belong to an
        /// optional mod, and an unresolvable target must be a logged skip rather than a load
        /// failure. A silent no-bind is the failure mode to watch — it looks identical to a working
        /// patch, which is why every branch logs.</para>
        /// </summary>
        private void TryPatchEmpires(Harmony harmony)
        {
            try
            {
                var resourceFcType = GenTypes.GetTypeInAnyAssembly("FactionColonies.ResourceFC");
                if (resourceFcType != null)
                {
                    var originalBase = AccessTools.Method(resourceFcType, "CalculateProductionBase");
                    if (originalBase != null)
                    {
                        var postfix = new HarmonyMethod(typeof(Patches.Factions_EmpirePatch), nameof(Patches.Factions_EmpirePatch.CalculateProductionBase_Postfix));
                        harmony.Patch(originalBase, postfix: postfix);
                        Log.Message("[RimSynapse-Factions] Dynamically patched ResourceFC.CalculateProductionBase successfully.");
                    }
                    else
                    {
                        Log.Warning("[RimSynapse-Factions] Could not find ResourceFC.CalculateProductionBase — production scaling is NOT applied.");
                    }

                    var originalMult = AccessTools.Method(resourceFcType, "CalculateProductionMult");
                    if (originalMult != null)
                    {
                        var postfix = new HarmonyMethod(typeof(Patches.Factions_EmpirePatch), nameof(Patches.Factions_EmpirePatch.CalculateProductionMult_Postfix));
                        harmony.Patch(originalMult, postfix: postfix);
                        Log.Message("[RimSynapse-Factions] Dynamically patched ResourceFC.CalculateProductionMult successfully.");
                    }
                    else
                    {
                        Log.Warning("[RimSynapse-Factions] Could not find ResourceFC.CalculateProductionMult — the 0.6 population curve is NOT applied.");
                    }
                }
                else
                {
                    Log.Message("[RimSynapse-Factions] Empires mod not detected for ResourceFC. Skipping production patching.");
                }

                var settlementMilitaryType = GenTypes.GetTypeInAnyAssembly("FactionColonies.WorldObjectComp_SettlementMilitary");
                if (settlementMilitaryType != null)
                {
                    var originalSendMilitary = AccessTools.Method(settlementMilitaryType, "SendMilitary", new System.Type[] {
                        GenTypes.GetTypeInAnyAssembly("FactionColonies.MercenarySquadFC"),
                        GenTypes.GetTypeInAnyAssembly("FactionColonies.PlanetTile") ?? GenTypes.GetTypeInAnyAssembly("RimWorld.Planet.PlanetTile"),
                        GenTypes.GetTypeInAnyAssembly("FactionColonies.MilitaryJobDef"),
                        typeof(int),
                        typeof(Faction)
                    });

                    if (originalSendMilitary != null)
                    {
                        var prefix = new HarmonyMethod(typeof(Patches.Factions_EmpirePatch), nameof(Patches.Factions_EmpirePatch.SendMilitary_Prefix));
                        harmony.Patch(originalSendMilitary, prefix: prefix);
                        Log.Message("[RimSynapse-Factions] Dynamically patched SettlementMilitary.SendMilitary successfully.");
                    }
                    else
                    {
                        Log.Warning("[RimSynapse-Factions] Could not find specific SendMilitary method overload in SettlementMilitary — military reach is NOT enforced.");
                    }
                }
                else
                {
                    Log.Message("[RimSynapse-Factions] Empires mod not detected for SettlementMilitary. Skipping military reach patching.");
                }
            }
            catch (System.Exception ex)
            {
                Log.Error($"[RimSynapse-Factions] Error patching Empires: {ex}");
            }
        }

        public static RimSynapse.SynapseModHandle ModHandle;
        
        public RimSynapseFactionsMod(ModContentPack content) : base(content)
        {
            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Initializing Mod...", "factions");
            
            // Harmony.DEBUG is deliberately not set. It is global static state, not per-instance,
            // so enabling it here dumped the IL of every patch from every mod loaded after this one
            // to harmony.log.txt on the user's desktop, and slowed patching for all of them (#51).
            var harmony = new Harmony("rimsynapse.factions");
            harmony.PatchAll();

            foreach (var m in harmony.GetPatchedMethods())
            {
                RimSynapse.SynapseLogger.Info($"[RimSynapse-Factions] Successfully patched method: {m.DeclaringType.FullName}.{m.Name}", "factions");
            }

            // Dynamically patch all concrete subclasses of PawnsArrivalModeWorker since patching the abstract class directly fails
            var postfixMethod = typeof(RimSynapse.Factions.Patches.PawnsArrivalModeWorker_Arrive_Patch).GetMethod("Postfix");
            if (postfixMethod != null)
            {
                foreach (var type in typeof(PawnsArrivalModeWorker).Assembly.GetTypes())
                {
                    if (typeof(PawnsArrivalModeWorker).IsAssignableFrom(type) && !type.IsAbstract)
                    {
                        var targetMethod = type.GetMethod("Arrive", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        if (targetMethod != null)
                        {
                            harmony.Patch(targetMethod, postfix: new HarmonyMethod(postfixMethod));
                        }
                    }
                }
            }
            
            TryPatchEmpires(harmony);

            RimSynapse.SynapseLogger.Info("[RimSynapse-Factions] Harmony Patches applied.", "factions");
            
            ModHandle = new RimSynapse.SynapseModHandle("rimsynapse.factions", "RimSynapse Factions");
            
            // Subscribe to the Core narrative context hooks
            RimSynapse.SynapseLetterContextHook.OnGatherLetterContext += GatherFactionLetterContext;
            RimSynapse.SynapseCoreContext.OnGlobalKnowledgeBroadcast += HandleGlobalKnowledgeBroadcast;

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

            if (ModsConfig.IdeologyActive)
            {
                RegionalDemographicRegistry.RegisterProvider(new RimSynapse.Factions.Ideology.IdeologyDemographicProvider());
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


        private static void HandleGlobalKnowledgeBroadcast(float wealthDelta, float strengthDelta)
        {
            if (wealthDelta == 0 && strengthDelta == 0) return;

            var worldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (worldComp != null)
            {
                worldComp.ApplyGlobalKnowledgeDeltas(wealthDelta, strengthDelta);
            }
        }
    }
}
