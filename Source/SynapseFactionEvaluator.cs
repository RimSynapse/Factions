using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using RimWorld;
using RimWorld.Planet;
using RimSynapse.Utils;
using RimSynapse.Factions.Models;
using Newtonsoft.Json;

namespace RimSynapse.Factions
{
    public static class SynapseFactionEvaluator
    {
        public static void CheckAllFactions()
        {
            if (Find.FactionManager == null) return;
            foreach (var faction in Find.FactionManager.AllFactions)
            {
                EvaluateFaction(faction);
            }
        }

        /// <summary>
        /// Force-regenerates faction history for ALL factions. Clears existing history first.
        /// Used by the debug UI.
        /// </summary>
        public static void ForceRegenerateAll()
        {
            if (Find.FactionManager == null) return;
            var stWorldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (stWorldComp == null) return;

            foreach (var faction in Find.FactionManager.AllFactions)
            {
                if (faction == null || faction.IsPlayer || faction.Hidden) continue;

                var tracker = stWorldComp.GetOrCreateStoryTracker(faction.GetUniqueLoadID());
                tracker.factionHistory = null; // Clear so it regenerates
            }

            RimSynapse.SynapseLogger.Message("[RimSynapse-Factions] Cleared all faction histories. They will regenerate on next opportunistic tick.");
        }

        /// <summary>
        /// Returns true if an LLM call was queued for this faction.
        /// </summary>
        public static bool EvaluateFaction(Faction faction)
        {
            if (faction == null || faction.IsPlayer || faction.Hidden) return false;

            var stWorldComp = Find.World.GetComponent<SynapseFactionsWorldComponent>();
            if (stWorldComp == null) return false;

            var tracker = stWorldComp.GetOrCreateStoryTracker(faction.GetUniqueLoadID());

            if (!string.IsNullOrEmpty(tracker.factionHistory))
            {
                // Already generated history - skip
                return false;
            }

            GenerateFactionHistory(faction, tracker);
            return true; // LLM call was queued
        }

        private static void GenerateFactionHistory(Faction faction, FactionStoryTracker tracker)
        {
            string factionName = faction.Name;
            string factionType = faction.def.LabelCap;
            string vanillaDescription = faction.def.description ?? "No vanilla description available.";

            // Ideology
            string ideology = "No specific ideology";
            string precepts = "";
            try
            {
                if (ModsConfig.IdeologyActive && faction.ideos?.PrimaryIdeo != null)
                {
                    ideology = faction.ideos.PrimaryIdeo.name;
                    var preceptList = faction.ideos.PrimaryIdeo.PreceptsListForReading;
                    if (preceptList != null && preceptList.Count > 0)
                    {
                        precepts = string.Join(", ", preceptList.Select(p => p.Label).Take(8));
                    }
                }
            }
            catch { /* Ideology DLC not loaded */ }

            // Settlement count
            int settlementCount = 0;
            try
            {
                settlementCount = Find.WorldObjects.Settlements.Count(s => s.Faction == faction);
            }
            catch { }

            // Leader name (just the name, no personality)
            string leaderName = faction.leader?.Name?.ToStringFull ?? "Unknown";

            // Player relation
            int playerGoodwill = faction.PlayerGoodwill;
            string playerRelation = faction.PlayerRelationKind.ToString();

            // Build the full inter-faction goodwill matrix
            var relationsBuilder = new StringBuilder();
            relationsBuilder.AppendLine("Inter-Faction Relations:");
            relationsBuilder.AppendLine($"- Player Colony: {playerGoodwill} ({playerRelation})");

            foreach (var otherFaction in Find.FactionManager.AllFactionsVisible)
            {
                if (otherFaction == faction || otherFaction.IsPlayer || otherFaction.Hidden) continue;
                int goodwill = faction.GoodwillWith(otherFaction);
                string label = goodwill >= 75 ? "Allied" :
                               goodwill >= 0 ? "Neutral" :
                               goodwill >= -75 ? "Unfriendly" : "Hostile";

                int otherSettlements = 0;
                try { otherSettlements = Find.WorldObjects.Settlements.Count(s => s.Faction == otherFaction); } catch { }

                relationsBuilder.AppendLine($"- {otherFaction.Name} ({otherFaction.def.LabelCap}, {otherSettlements} settlements): {goodwill} ({label})");
            }

            string systemPrompt = @"You are the RimWorld Narrative AI. You write faction descriptions for a sci-fi colony simulator set on a lawless frontier planet (a ""rimworld"") far from the civilized core worlds.

Your task: Generate a rich, immersive description for a faction that will REPLACE their in-game description panel. 

CONTEXT RULES:
- This is a rimworld - a planet on the edge of known space. Glitterworlds and urbworlds are distant and generally unconcerned with what happens here.
- Factions on rimworlds have often been here for generations after crashlanding, being abandoned, or deliberately colonizing.
- Use the VANILLA DESCRIPTION as a narrative seed - it tells you the faction's tech level and general disposition. Expand on it, don't contradict it.
- Use the SETTLEMENT COUNT to convey scale: 1-2 settlements = small/struggling, 3-5 = established regional presence, 6+ = major power.
- Use the INTER-FACTION GOODWILL NUMBERS exactly as provided. If two factions are at -90, explain WHY they hate each other through historical narrative. If allied at +80, explain the bond. Do NOT invent new numbers.
- The leader's name should be mentioned naturally, but do NOT reference their personality traits (you don't know them).

OUTPUT FORMAT:
Write 2-3 paragraphs of faction history in a narrative style. Cover:
1. How and why this faction came to exist on this rimworld
2. Their civilization type, culture, and current state
3. Their key alliances and rivalries with other named factions (using the provided goodwill data)

You MUST respond in valid JSON:
{
  ""Description"": ""Your 2-3 paragraph description here...""
}";

            string userMessage = $@"Faction: {factionName}
Type: {factionType}
Leader: {leaderName}
Settlements: {settlementCount}
Ideology: {ideology}
{(string.IsNullOrEmpty(precepts) ? "" : $"Key Precepts: {precepts}\n")}
Vanilla Description (use as seed):
""{vanillaDescription}""

{relationsBuilder}
Generate their description.";

            var options = new ChatOptions { priority = 3, requestName = "Faction Evaluation", targetName = factionName };

            SynapseClient.PromptAsync(
                RimSynapseFactionsMod.ModHandle,
                systemPrompt,
                userMessage,
                result =>
                {
                    HandleFactionHistoryResult(faction, tracker, result);
                },
                options
            );
        }

        private static void HandleFactionHistoryResult(Faction faction, FactionStoryTracker tracker, ChatResult result)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json == null) { RimSynapse.SynapseLogger.Warn("factions", "[RimSynapse-Factions] No JSON found in faction history response."); return; }

                    var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (parsed != null)
                    {
                        string description = null;
                        if (parsed.TryGetValue("Description", out object descObj))
                        {
                            description = descObj.ToString();
                        }
                        // Fallback: try "HistoricalRecord" for backwards compat
                        else if (parsed.TryGetValue("HistoricalRecord", out object histObj))
                        {
                            description = histObj.ToString();
                        }

                        if (!string.IsNullOrEmpty(description))
                        {
                            tracker.factionHistory = description;
                            RimSynapse.SynapseLogger.Message($"[RimSynapse-Factions] Generated description for {faction.Name} ({description.Length} chars).");
                        }
                        else
                        {
                            RimSynapse.SynapseLogger.Warn("factions", $"[RimSynapse-Factions] Empty description in response for {faction.Name}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    RimSynapse.SynapseLogger.Warn("factions", $"[RimSynapse-Factions] Failed to parse faction history response for {faction.Name}: {ex.Message}");
                }
            }
            else
            {
                RimSynapse.SynapseLogger.Warn("factions", $"[RimSynapse-Factions] Faction history request failed for {faction.Name}: {result.error}");
            }
        }
    }
}
