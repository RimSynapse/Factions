using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using RimSynapse.Models;
using RimSynapse.Utils;
using Newtonsoft.Json;
using System.Reflection;

namespace RimSynapse.Factions.LeaderGeneration
{
    /// <summary>
    /// LLM prompt construction and callback handlers for the three-step leader backstory pipeline:
    /// Childhood Memory -> Rise to Power -> Personality Profile.
    /// </summary>
    public static partial class SynapseFactionLeaderGenerator
    {
        private static void GenerateLeaderChildhoodMemory(Pawn leader, Faction faction, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionHistoryContext)
        {
            var childhood = leader.story?.Childhood;
            string childhoodTitle = childhood?.title ?? "Unknown";
            string childhoodDesc = childhood?.description ?? "An unremarkable childhood.";
            string skillBonuses = FormatSkillGains(childhood);
            string disabledWork = FormatDisabledWork(childhood);

            string systemPrompt = @"You are writing a vivid first-person memory for a faction LEADER in the RimWorld universe.
This memory is from their CHILDHOOD — before they held power.

RULES:
- Write 100-200 words, first person (""I"", ""me"", ""my"")
- This is a SINGLE vivid memory, not a life summary
- Ground the memory in the skill bonuses: explain WHAT childhood experience gave them these skills
- If work types are disabled, hint at WHY (trauma, cultural taboo, physical limitation)
- If faction history is provided, the childhood should be consistent with that world
- The memory should hint at the seeds of leadership — even as a child, something set them apart
- You MUST generate a ""Hometown"" — their place of origin, matching the faction type:
  - Outlander → a named settlement or outpost (e.g., ""Kharstead"", ""Port Valen"")
  - Tribal → a geographic feature, camp, or caravan route (e.g., ""the Redstone caravan"", ""the marshlands east of Sleeping Ridge"")
  - Pirate → a ship, station, or raider den (e.g., ""the Rust Fang"", ""Scrapheap Station"")
  - Imperial → a named city or estate (e.g., ""the Stellarch's court at Novium"")
- RimWorld setting: frontier planets, tribal societies, pirate dens, outlander settlements

You MUST respond in valid JSON:
{
  ""Memory"": ""I remember the first time I...(100-200 words)..."",
  ""Hometown"": ""the Redstone caravan"",
  ""Tags"": [""Origin"", ""Childhood"", ""Leadership""],
  ""EmotionalTone"": ""formative""
}";

            string factionName = faction?.Name ?? "Unknown";
            string factionType = faction?.def?.LabelCap ?? "Faction";

            string userMessage = $@"Leader: {leader.Name.ToStringFull}
Faction: {factionName} ({factionType})
Childhood Backstory: ""{childhoodTitle}""
Vanilla Description: ""{childhoodDesc}""
Skill Bonuses from Childhood: {skillBonuses}
{(string.IsNullOrEmpty(disabledWork) ? "" : $"Disabled Work Types: {disabledWork}\n")}{factionHistoryContext}

Write a vivid childhood memory for this future leader.";

            var options = new ChatOptions { priority = 4, requestName = "Leader Childhood", targetName = leader.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapseFactionsMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnLeaderChildhoodGenerated(result, leader, faction, coreComp, factionHistoryContext),
                options
            );
        }

        private static void OnLeaderChildhoodGenerated(ChatResult result, Pawn leader, Faction faction, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionHistoryContext)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null && parsed.ContainsKey("Memory"))
                        {
                            string memoryText = parsed["Memory"].ToString();
                            var tags = new List<string> { "Childhood", "Origin" };
                            if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                            {
                                tags = arr.Select(t => t.ToString()).ToList();
                                if (!tags.Contains("Childhood")) tags.Insert(0, "Childhood");
                            }

                            if (parsed.ContainsKey("Hometown"))
                            {
                                coreComp.hometown = parsed["Hometown"].ToString();
                            }

                            long childTick = SynapseDateHelper.GetChildhoodMemoryTick(leader);
                            coreComp.memories.Add(new WeightedMemory
                            {
                                summary = memoryText,
                                weight = 3.0f,
                                baseWeight = 3.0f,
                                decayRate = 0f,
                                tags = tags,
                                memoryType = "BackstoryChildhood",
                                absTick = childTick,
                                gameTick = (int)(childTick - SynapseDateHelper.GetAdjustmentTick())
                            });

                            RimSynapse.SynapseLogger.Info("factions", $"[RimSynapse-Factions] Leader childhood memory generated for {leader.Name.ToStringShort}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    RimSynapse.SynapseLogger.Warn("factions", $"[RimSynapse-Factions] Failed to parse leader childhood memory: {ex.Message}");
                }
            }

            GenerateLeaderRiseMemory(leader, faction, coreComp, factionHistoryContext);
        }

        private static void GenerateLeaderRiseMemory(Pawn leader, Faction faction, RimSynapse.Comps.SynapseCorePawnComp coreComp, string factionHistoryContext)
        {
            var adulthood = leader.story?.Adulthood;
            string adulthoodTitle = adulthood?.title ?? "Unknown";
            string adulthoodDesc = adulthood?.description ?? "An uneventful adult life.";
            string skillBonuses = FormatSkillGains(adulthood);
            string disabledWork = FormatDisabledWork(adulthood);

            string title = "Leader";
            
            // Check for Royalty DLC safely
            if (ModsConfig.RoyaltyActive)
            {
                var royaltyType = Type.GetType("RimSynapse.Expansions.Royalty.PsychologyRoyaltyIntegration, RimSynapsePsychology");
                if (royaltyType != null)
                {
                    var getSeniorTitleMethod = royaltyType.GetMethod("GetSeniorTitle", BindingFlags.Public | BindingFlags.Static);
                    if (getSeniorTitleMethod != null)
                    {
                        title = getSeniorTitleMethod.Invoke(null, new object[] { leader }) as string ?? "Leader";
                    }
                }
            }

            string factionName = faction?.Name ?? "Unknown";
            string factionType = faction?.def?.LabelCap ?? "Faction";
            string traits = leader.story?.traits?.allTraits != null
                ? string.Join(", ", leader.story.traits.allTraits.Select(t => t.LabelCap))
                : "None";

            var childhoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            string childhoodContext = childhoodMem != null
                ? $"\nChildhood Memory (already generated — maintain continuity):\n\"{childhoodMem.summary}\""
                : "";

            string hometownContext = !string.IsNullOrEmpty(coreComp.hometown)
                ? $"\nHometown: {coreComp.hometown}"
                : "";

            string systemPrompt = @"You are writing a vivid first-person memory for a faction LEADER in the RimWorld universe.
This memory is from their ADULTHOOD — specifically about their RISE TO POWER.

RULES:
- Write 150-250 words, first person (""I"", ""me"", ""my"")
- This must cover TWO key moments woven into one memory:
  1. How you gained influence and skill in the faction (grounded in the adulthood backstory + skill bonuses)
  2. The specific moment you took control (a challenge, a crisis, a succession, a coup)
- If faction history is provided, your rise must be consistent with it
- Ground the memory in the skill bonuses — your adulthood skills are what let you seize power
- The memory should feel like the defining moment of your life
- RimWorld setting: political intrigue, tribal succession, pirate might-makes-right, outlander elections

You MUST respond in valid JSON:
{
  ""Memory"": ""The night the old chief died, I...(150-250 words)..."",
  ""Tags"": [""Adulthood"", ""FactionRise"", ""Leadership""],
  ""EmotionalTone"": ""triumphant""
}";

            string userMessage = $@"Leader: {leader.Name.ToStringFull}, {title} of {factionName} ({factionType})
Traits: {traits}
Adulthood Backstory: ""{adulthoodTitle}""
Vanilla Description: ""{adulthoodDesc}""
Skill Bonuses from Adulthood: {skillBonuses}
{(string.IsNullOrEmpty(disabledWork) ? "" : $"Disabled Work Types: {disabledWork}\n")}{hometownContext}{childhoodContext}{factionHistoryContext}

Write their rise-to-power memory.";

            var options = new ChatOptions { priority = 5, requestName = "Leader Rise to Power", targetName = leader.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapseFactionsMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnLeaderRiseGenerated(result, leader, coreComp),
                options
            );
        }

        private static void OnLeaderRiseGenerated(ChatResult result, Pawn leader, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null && parsed.ContainsKey("Memory"))
                        {
                            string memoryText = parsed["Memory"].ToString();
                            var tags = new List<string> { "Adulthood", "FactionRise", "Leadership" };
                            if (parsed.ContainsKey("Tags") && parsed["Tags"] is Newtonsoft.Json.Linq.JArray arr)
                            {
                                tags = arr.Select(t => t.ToString()).ToList();
                                if (!tags.Contains("Leadership")) tags.Add("Leadership");
                            }

                            long riseTick = SynapseDateHelper.GetAdulthoodMemoryTick(leader);
                            coreComp.memories.Add(new WeightedMemory
                            {
                                summary = memoryText,
                                weight = 4.0f,
                                baseWeight = 4.0f,
                                decayRate = 0f,
                                tags = tags,
                                memoryType = "BackstoryAdulthood",
                                absTick = riseTick,
                                gameTick = (int)(riseTick - SynapseDateHelper.GetAdjustmentTick())
                            });

                            RimSynapse.SynapseLogger.Info("factions", $"[RimSynapse-Factions] Leader rise memory generated for {leader.Name.ToStringShort}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    RimSynapse.SynapseLogger.Warn("factions", $"[RimSynapse-Factions] Failed to parse leader rise memory: {ex.Message}");
                }
            }

            GenerateLeaderPersonalityProfile(leader, coreComp);
        }

        private static void GenerateLeaderPersonalityProfile(Pawn leader, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            string traits = string.Join(", ", leader.story?.traits?.allTraits?.Select(t => t.Label) ?? Enumerable.Empty<string>());

            var childhoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryChildhood");
            var adulthoodMem = coreComp.memories.LastOrDefault(m => m.memoryType == "BackstoryAdulthood");

            string memoriesContext = "";
            if (childhoodMem != null) memoriesContext += $"Childhood Memory:\n\"{childhoodMem.summary}\"\n\n";
            if (adulthoodMem != null) memoriesContext += $"Adulthood/Rise Memory:\n\"{adulthoodMem.summary}\"\n\n";

            string hometownContext = !string.IsNullOrEmpty(coreComp.hometown) ? $"\nHometown: {coreComp.hometown}" : "";
            string factionName = leader.Faction?.Name ?? "Unknown";
            string factionType = leader.Faction?.def?.LabelCap ?? "Faction";

            string systemPrompt = @"You are analyzing the psychology of a faction LEADER in the RimWorld universe.
Given their childhood memory, rise-to-power memory, and personality traits, synthesize a permanent psychological profile.
This leader does NOT get daily reviews — this profile is their permanent character assessment.

OUTPUT:
1. Personality — A 2-3 sentence personality summary (third person). How do they lead? What drives them? What is their weakness?
2. Archetypes — Three psychological classifications.
3. Leadership Style — One sentence describing how they run their faction.

You MUST respond in valid JSON:
{
  ""Personality"": ""She is a calculating strategist who..."",
  ""JungianType"": ""INTJ"",
  ""CoreArchetype"": ""Ruler"",
  ""Temperament"": ""Choleric"",
  ""LeadershipStyle"": ""Rules through fear and strict discipline, but rewards loyalty generously.""
}";

            string userMessage = $@"Leader: {leader.Name.ToStringShort}, of {factionName} ({factionType})
Age: {leader.ageTracker?.AgeBiologicalYears ?? 0}
Gender: {leader.gender}
Traits: {traits}{hometownContext}

{memoriesContext}Synthesize their permanent psychological profile.";

            var options = new ChatOptions { priority = 6, requestName = "Leader Personality", targetName = leader.Name.ToStringShort };

            SynapseClient.PromptAsync(
                RimSynapseFactionsMod.ModHandle,
                systemPrompt,
                userMessage,
                result => OnLeaderPersonalityGenerated(result, leader, coreComp),
                options
            );
        }

        private static void OnLeaderPersonalityGenerated(ChatResult result, Pawn leader, RimSynapse.Comps.SynapseCorePawnComp coreComp)
        {
            if (result.success)
            {
                try
                {
                    string json = JsonHelper.ExtractJson(result.content);
                    if (json != null)
                    {
                        var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                        if (parsed != null)
                        {
                            if (parsed.TryGetValue("Personality", out object personalityObj))
                                coreComp.personalitySummary = personalityObj.ToString();

                            coreComp.llmTraits.Clear();
                            if (parsed.TryGetValue("JungianType", out object jungian))
                                coreComp.llmTraits.Add($"Jungian Type: {jungian}");
                            if (parsed.TryGetValue("CoreArchetype", out object archetype))
                                coreComp.llmTraits.Add($"Core Archetype: {archetype}");
                            if (parsed.TryGetValue("Temperament", out object temperament))
                                coreComp.llmTraits.Add($"Temperament: {temperament}");
                            if (parsed.TryGetValue("LeadershipStyle", out object style))
                                coreComp.llmTraits.Add($"Leadership: {style}");

                            RimSynapse.SynapseLogger.Info("factions", $"[RimSynapse-Factions] Leader personality profile synthesized for {leader.Name.ToStringShort}.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    RimSynapse.SynapseLogger.Warn("factions", $"[RimSynapse-Factions] Failed to parse leader personality profile: {ex.Message}");
                }
            }

            MarkBackstoryCreated(leader);
            RimSynapse.SynapseLogger.Info("factions", $"[RimSynapse-Factions] Leader backstory pipeline complete for {leader.Name.ToStringShort} (3-step).");
        }
    }
}
