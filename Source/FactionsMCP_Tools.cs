using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using Newtonsoft.Json;
using RimSynapse;


namespace RimSynapse.Factions
{
    [StaticConstructorOnStartup]
    public static class FactionsMCP_Tools
    {
        static FactionsMCP_Tools()
        {
            RegisterToolDef();
        }

        private static void RegisterToolDef()
        {
            var noParams = new { type = "object", properties = new { } };
            SynapseToolRegistry.RegisterTool(
                "get_all_factions",
                "Returns a list of all faction names and details currently in the game.",
                noParams,
                GetAllFactions
            );

            SynapseToolRegistry.RegisterTool(
                "trigger_leader_backstory_generation",
                "Manually triggers the three-step backstory and profile generation pipeline for all faction leaders.",
                noParams,
                TriggerLeaderBackstoryGenerationHandler
            );

            SynapseToolRegistry.RegisterTool(
                "get_motivated_factions",
                "Returns a list of hostile factions along with their perceived colony strength and greed ratio (wealth vs defense). Use this to determine which faction to send in a raid.",
                noParams,
                GetMotivatedFactions
            );

            var factionParams = new
            {
                type = "object",
                properties = new
                {
                    factionName = new { type = "string", description = "The name of the faction to look up." }
                },
                required = new[] { "factionName" }
            };

            SynapseToolRegistry.RegisterTool(
                "get_faction_relations_history",
                "Returns a summary of relations history and recent interactions with a specific faction.",
                factionParams,
                GetFactionRelationsHistory
            );

            var crisisParams = new
            {
                type = "object",
                properties = new
                {
                    factionName = new { type = "string", description = "The name of the target faction." },
                    crisisType = new { type = "string", description = "The type of crisis to trigger (e.g., 'Blight')." }
                },
                required = new[] { "factionName", "crisisType" }
            };

            SynapseToolRegistry.RegisterTool(
                "trigger_settlement_crisis",
                "Triggers a localized crisis (e.g. Blight) at a random settlement of the target faction. Can be used for subterfuge.",
                crisisParams,
                TriggerSettlementCrisis
            );
        }

        private struct MotivatedFactionResult
        {
            public string factionId;
            public string factionName;
            public string techLevel;
            public float perceivedWealth;
            public float perceivedStrength;
            public float greedRatio;
        }

        private static string GetMotivatedFactions(string argsJson)
        {
            var coreComp = Find.World?.GetComponent<SynapseCoreWorldComponent>();
            if (coreComp == null) return JsonConvert.SerializeObject(new { error = "Core component not found." });

            var playerFaction = Faction.OfPlayer;
            var results = new List<MotivatedFactionResult>();

            if (Find.FactionManager != null && playerFaction != null)
            {
                foreach (var faction in Find.FactionManager.AllFactions)
                {
                    if (faction != null && faction.HostileTo(playerFaction) && !faction.IsPlayer && !faction.Hidden)
                    {
                        var tracker = coreComp.factionTrackers.FirstOrDefault(t => t.factionId == faction.GetUniqueLoadID());
                        if (tracker == null)
                        {
                            tracker = new RimSynapse.Models.FactionRelationshipTracker
                            {
                                factionId = faction.GetUniqueLoadID(),
                                perceivedWealth = 1000f,
                                perceivedStrength = 50f
                            };
                            coreComp.factionTrackers.Add(tracker);
                        }

                        float normalizedStrength = (tracker.perceivedStrength * 50f) + 1f;
                        float greedRatio = tracker.perceivedWealth / normalizedStrength;

                        results.Add(new MotivatedFactionResult
                        {
                            factionId = faction.GetUniqueLoadID(),
                            factionName = faction.Name,
                            techLevel = faction.def?.techLevel.ToString() ?? "Unknown",
                            perceivedWealth = tracker.perceivedWealth,
                            perceivedStrength = tracker.perceivedStrength,
                            greedRatio = greedRatio
                        });
                    }
                }
            }

            return JsonConvert.SerializeObject(new { motivatedFactions = results.OrderByDescending(x => x.greedRatio).ToList() });
        }

        private static string GetFactionRelationsHistory(string argsJson)
        {
            var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(argsJson);
            if (args == null || !args.TryGetValue("factionName", out string factionName))
            {
                return JsonConvert.SerializeObject(new { error = "Missing factionName in arguments." });
            }

            Faction faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.Name.ToLower().Contains(factionName.ToLower()));
            if (faction == null)
            {
                return JsonConvert.SerializeObject(new { error = $"Faction '{factionName}' not found." });
            }

            var factionsComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (factionsComp == null) return JsonConvert.SerializeObject(new { error = "Factions world component not found." });

            var storyTracker = factionsComp.factionStoryTrackers.Find(t => t.factionId == faction.GetUniqueLoadID());
            
            var result = new
            {
                factionName = faction.Name,
                playerGoodwill = faction.PlayerGoodwill,
                playerRelation = faction.PlayerRelationKind.ToString(),
                historyLore = storyTracker?.factionHistory ?? "No generated history."
            };

            return JsonConvert.SerializeObject(result);
        }

        private static string TriggerSettlementCrisis(string argsJson)
        {
            var args = JsonConvert.DeserializeObject<Dictionary<string, string>>(argsJson);
            if (args == null || !args.TryGetValue("factionName", out string factionName) || !args.TryGetValue("crisisType", out string crisisType))
            {
                return JsonConvert.SerializeObject(new { error = "Missing factionName or crisisType in arguments." });
            }

            Faction faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.Name.ToLower().Contains(factionName.ToLower()));
            if (faction == null)
            {
                return JsonConvert.SerializeObject(new { error = $"Faction '{factionName}' not found." });
            }

            var factionsComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (factionsComp == null) return JsonConvert.SerializeObject(new { error = "Factions world component not found." });

            var settlements = Find.WorldObjects.Settlements.Where(s => s.Faction == faction).ToList();
            if (!settlements.Any()) return JsonConvert.SerializeObject(new { error = "Faction has no settlements." });

            var targetSettlement = settlements.RandomElement();
            var tracker = factionsComp.GetOrCreateSettlementTracker(targetSettlement);

            if (tracker.activeCrises.Any(c => c.crisisType == crisisType))
            {
                return JsonConvert.SerializeObject(new { error = "Settlement is already suffering from this crisis." });
            }

            tracker.activeCrises.Add(new Models.SettlementCrisis
            {
                crisisType = crisisType,
                currentSeverity = 0f,
                daysElapsed = 0,
                ticksRemaining = 0
            });

            return JsonConvert.SerializeObject(new { success = true, message = $"Successfully triggered {crisisType} at {targetSettlement.LabelCap}." });
        }

        private static string GetAllFactions(string argsJson)
        {
            if (Find.FactionManager == null)
            {
                return JsonConvert.SerializeObject(new { error = "FactionManager not found." });
            }

            var playerFaction = Faction.OfPlayer;

            // Generate a dummy hostile faction in quicktest mode if only the player faction exists
            if (playerFaction != null && Find.FactionManager.AllFactions.Count(f => !f.Hidden) <= 1)
            {
                try
                {
                    var def = DefDatabase<FactionDef>.AllDefs.FirstOrDefault(d => !d.isPlayer && !d.hidden);
                    if (def != null)
                    {
                        var parms = new FactionGeneratorParms(def, default(IdeoGenerationParms), true);
                        var dummy = FactionGenerator.NewGeneratedFaction(null, parms);
                        dummy.Name = "Rough Outlander";
                        Find.FactionManager.Add(dummy);

                        var relation = dummy.RelationWith(playerFaction, true);
                        relation.kind = FactionRelationKind.Hostile;
                    }
                }
                catch {}
            }

            var results = new List<object>();

            foreach (var faction in Find.FactionManager.AllFactions)
            {
                if (faction == null) continue;

                string name = faction.Name ?? "Unnamed";
                string defName = faction.def?.defName ?? "Unknown";
                bool isPlayer = faction.IsPlayer;
                bool hostile = playerFaction != null ? faction.HostileTo(playerFaction) : false;

                int goodwill = 0;
                try
                {
                    if (playerFaction != null && faction != playerFaction)
                    {
                        goodwill = faction.GoodwillWith(playerFaction);
                    }
                }
                catch {}

                string leaderName = faction.leader?.Name?.ToStringShort ?? "None";

                results.Add(new
                {
                    name = name,
                    defName = defName,
                    isPlayer = isPlayer,
                    hostile = hostile,
                    goodwill = goodwill,
                    leaderName = leaderName
                });
            }
            return JsonConvert.SerializeObject(new { factions = results });
        }

        private static string TriggerLeaderBackstoryGenerationHandler(string argsJson)
        {
            try
            {
                bool result = LeaderGeneration.SynapseFactionLeaderGenerator.TriggerLeaderBackstoryGeneration();
                return JsonConvert.SerializeObject(new { success = true, triggered = result });
            }
            catch (Exception ex)
            {
                return JsonConvert.SerializeObject(new { error = ex.Message });
            }
        }
    }
}
