using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimSynapse.Factions.Models;

namespace RimSynapse.Factions
{
    public class SynapseFactionsWorldComponent : WorldComponent
    {
        public Dictionary<int, string> LLMGeopoliticsHistory = new Dictionary<int, string>();
        
        // Faction Story Trackers
        public List<FactionStoryTracker> factionStoryTrackers = new List<FactionStoryTracker>();
        public List<SettlementStoryTracker> settlementTrackers = new List<SettlementStoryTracker>();

        // Staggered Tick Manager
        private int _ticksUntilNextSettlement = 0;
        private int _currentSettlementIndex = 0;

        // Knowledge Propagation System
        public List<KnowledgePacket> inTransitKnowledge = new List<KnowledgePacket>();
        
        public SynapseFactionsWorldComponent(World world) : base(world)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref LLMGeopoliticsHistory, "llmGeopoliticsHistory", LookMode.Value, LookMode.Value);
            
            if (LLMGeopoliticsHistory == null)
            {
                LLMGeopoliticsHistory = new Dictionary<int, string>();
            }

            Scribe_Collections.Look(ref factionStoryTrackers, "factionStoryTrackers", LookMode.Deep);
            Scribe_Collections.Look(ref inTransitKnowledge, "inTransitKnowledge", LookMode.Deep);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (factionStoryTrackers == null) factionStoryTrackers = new List<FactionStoryTracker>();
                if (settlementTrackers == null) settlementTrackers = new List<SettlementStoryTracker>();
                if (inTransitKnowledge == null) inTransitKnowledge = new List<KnowledgePacket>();
            }
        }

        // Faction Story Tracker Accessors

        public FactionStoryTracker GetOrCreateStoryTracker(string factionId)
        {
            var tracker = factionStoryTrackers.Find(f => f.factionId == factionId);
            if (tracker == null)
            {
                tracker = new FactionStoryTracker { factionId = factionId };
                factionStoryTrackers.Add(tracker);
            }
            return tracker;
        }

        public SettlementStoryTracker GetOrCreateSettlementTracker(Settlement settlement)
        {
            if (settlement == null) return null;
            var tracker = settlementTrackers.Find(s => s.settlementId == settlement.ID);
            if (tracker == null)
            {
                tracker = new SettlementStoryTracker 
                { 
                    settlementId = settlement.ID,
                    factionId = settlement.Faction?.GetUniqueLoadID()
                };

                // Seed initial geographic demographics
                tracker.totalDwellings = 125;
                tracker.currentPopulation = 250;
                
                // Read Tile and Biome data
                var tile = Find.WorldGrid[settlement.Tile];
                var biome = settlement.Biome;

                float techLevelMult = 1f;
                bool isSpacer = false;
                bool isIndustrial = false;

                if (settlement.Faction != null)
                {
                    isSpacer = settlement.Faction.def.techLevel >= TechLevel.Spacer;
                    isIndustrial = settlement.Faction.def.techLevel == TechLevel.Industrial;
                    techLevelMult = (float)settlement.Faction.def.techLevel;
                }

                // Nutrition
                if (isSpacer) 
                    tracker.rawNutrition = 1000f; // Hydroponics
                else if (isIndustrial)
                    tracker.rawNutrition = biome.plantDensity * 500f;
                else
                    tracker.rawNutrition = biome.forageability * 500f;

                // Biomass
                tracker.biomass = biome.TreeDensity * 500f;

                // Minerals
                float hillMult = 0.5f; // Flat
                if (tile.hilliness == Hilliness.SmallHills) hillMult = 1.0f;
                if (tile.hilliness == Hilliness.LargeHills) hillMult = 2.0f;
                if (tile.hilliness == Hilliness.Mountainous) hillMult = 3.0f;
                tracker.minerals = hillMult * 500f;

                settlementTrackers.Add(tracker);
            }
            return tracker;
        }

        // Knowledge Propagation

        public void AggregateFactionDemographics(string factionId, out int totalDwellings, out int currentPop)
        {
            totalDwellings = 0;
            currentPop = 0;
            foreach (var tracker in settlementTrackers)
            {
                if (tracker.factionId == factionId)
                {
                    totalDwellings += tracker.totalDwellings;
                    currentPop += tracker.currentPopulation;
                }
            }
        }

        private void ProcessStaggeredSettlements()
        {
            var settlements = Find.WorldObjects.Settlements;
            if (settlements == null || settlements.Count == 0) return;

            _ticksUntilNextSettlement--;
            if (_ticksUntilNextSettlement <= 0)
            {
                // Calculate ticks per settlement to finish all within 60000 ticks (1 in-game day)
                int ticksPerSettlement = UnityEngine.Mathf.Max(1, 60000 / settlements.Count);
                _ticksUntilNextSettlement = ticksPerSettlement;

                if (_currentSettlementIndex >= settlements.Count)
                {
                    _currentSettlementIndex = 0;
                }

                var targetSettlement = settlements[_currentSettlementIndex];
                if (targetSettlement != null && targetSettlement.Faction != null && !targetSettlement.Faction.IsPlayer)
                {
                    var tracker = GetOrCreateSettlementTracker(targetSettlement);
                    if (tracker != null)
                    {
                        TickSettlementProduction(tracker);
                        TrySpawnRandomCrisis(tracker, targetSettlement);
                    }
                }

                _currentSettlementIndex++;
            }
        }

        private void TrySpawnRandomCrisis(SettlementStoryTracker tracker, Settlement settlement)
        {
            // Only try if no blight is active
            if (tracker.activeCrises.Any(c => c.crisisType == "Blight")) return;

            // Base chance per day (e.g. 0.5%)
            float chance = 0.005f;

            // Weight by biome
            var biome = settlement.Biome;
            if (biome.defName.ToLower().Contains("marsh") || biome.defName.ToLower().Contains("swamp"))
            {
                chance *= 3f; // 1.5% in swamps
            }
            else if (biome.defName.ToLower().Contains("desert") || biome.defName.ToLower().Contains("ice"))
            {
                chance *= 0.1f; // very rare in extreme biomes
            }

            if (Rand.Value < chance)
            {
                tracker.activeCrises.Add(new SettlementCrisis
                {
                    crisisType = "Blight",
                    currentSeverity = 0f,
                    daysElapsed = 0,
                    ticksRemaining = 0
                });
            }
        }

        private void TickSettlementProduction(SettlementStoryTracker tracker)
        {
            int popCap = tracker.totalDwellings * 2;
            if (popCap <= 0) return;

            float popRatio = (float)tracker.currentPopulation / popCap;
            
            float prodMultiplier = 1f;
            float rawConsumptionMult = 1f;
            float highTechClimb = 1f;
            float rawNutritionMult = 1f;

            if (popRatio < 0.90f)
            {
                float outOfRange = 0.90f - popRatio;
                int multiplierTier = UnityEngine.Mathf.FloorToInt(outOfRange / 0.10f) + 2;
                float loss = outOfRange * multiplierTier;
                prodMultiplier = UnityEngine.Mathf.Clamp01(1f - loss);
            }
            else if (popRatio > 1.10f)
            {
                highTechClimb = 1f + (popRatio - 1f); 
                rawConsumptionMult = 1f + ((popRatio - 1f) * 2f); 
            }

            // Process Crises
            for (int i = tracker.activeCrises.Count - 1; i >= 0; i--)
            {
                var crisis = tracker.activeCrises[i];
                if (crisis.crisisType == "Blight")
                {
                    crisis.daysElapsed++;
                    if (crisis.daysElapsed <= 5)
                    {
                        crisis.currentSeverity = UnityEngine.Mathf.Min(0.15f, crisis.currentSeverity + 0.03f);
                    }
                    else
                    {
                        crisis.currentSeverity = UnityEngine.Mathf.Max(0f, crisis.currentSeverity - 0.01f);
                    }

                    if (crisis.currentSeverity <= 0f && crisis.daysElapsed > 5)
                    {
                        tracker.activeCrises.RemoveAt(i);
                        continue;
                    }
                    
                    rawNutritionMult = UnityEngine.Mathf.Clamp01(rawNutritionMult - crisis.currentSeverity);
                }
            }

            // (Note: To prevent pools from running dry, we normally re-calculate base yields here, but for now we consume from the pool)
            tracker.preIndustrialGoods += (tracker.rawNutrition * rawNutritionMult) * prodMultiplier;
            tracker.industrialGoods += tracker.minerals * highTechClimb * prodMultiplier;
            tracker.spacerGoods += tracker.industrialGoods * highTechClimb * prodMultiplier;

            tracker.rawNutrition -= tracker.currentPopulation * rawConsumptionMult;
            tracker.biomass -= tracker.currentPopulation * rawConsumptionMult;
            tracker.minerals -= tracker.industrialGoods * rawConsumptionMult;
            
            tracker.rawNutrition = UnityEngine.Mathf.Max(0f, tracker.rawNutrition);
            tracker.biomass = UnityEngine.Mathf.Max(0f, tracker.biomass);
            tracker.minerals = UnityEngine.Mathf.Max(0f, tracker.minerals);

            // --- Population Growth & Xenotypes ---
            float growthRate = 0f;
            if (tracker.rawNutrition > tracker.currentPopulation * 1.5f) growthRate += 0.005f; // +0.5% daily if abundant
            if (tracker.rawNutrition < tracker.currentPopulation * 0.5f) growthRate -= 0.02f; // -2.0% daily if starving
            
            if (ModsConfig.BiotechActive && growthRate > 0f)
            {
                var faction = Find.FactionManager.AllFactionsListForReading.FirstOrDefault(f => f.GetUniqueLoadID() == tracker.factionId);
                // Temporarily disabled precise xenotype tracking since the property name is not easily known without decompiling.
            }
            
            if (growthRate != 0f)
            {
                int delta = UnityEngine.Mathf.RoundToInt(tracker.currentPopulation * growthRate);
                if (delta == 0) delta = growthRate > 0 ? 1 : -1;
                tracker.currentPopulation = UnityEngine.Mathf.Clamp(tracker.currentPopulation + delta, 0, popCap);
            }
        }

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

            ProcessStaggeredSettlements();

            if (Find.TickManager.TicksGame % 1000 == 0 && inTransitKnowledge.Count > 0)
            {
                int currentTick = Find.TickManager.TicksGame;
                for (int i = inTransitKnowledge.Count - 1; i >= 0; i--)
                {
                    var packet = inTransitKnowledge[i];
                    if (currentTick >= packet.arrivalTick)
                    {
                        ProcessArrivingKnowledge(packet);
                        inTransitKnowledge.RemoveAt(i);
                    }
                }
            }
        }

        private void ProcessArrivingKnowledge(KnowledgePacket packet)
        {
            var coreWorldComp = Find.World.GetComponent<SynapseCoreWorldComponent>();
            if (coreWorldComp == null) return;

            var tracker = coreWorldComp.factionTrackers.Find(f => f.factionId == packet.targetFactionId);
            if (tracker == null)
            {
                tracker = new RimSynapse.Models.FactionRelationshipTracker { factionId = packet.targetFactionId };
                coreWorldComp.factionTrackers.Add(tracker);
            }

            tracker.perceivedWealth = UnityEngine.Mathf.Lerp(tracker.perceivedWealth, packet.payloadWealth, 0.2f);
            tracker.perceivedStrength = UnityEngine.Mathf.Lerp(tracker.perceivedStrength, packet.payloadStrength, 0.2f);
        }

        public void ApplyGlobalKnowledgeDeltas(float wealthDelta, float strengthDelta)
        {
            var coreWorldComp = Find.World?.GetComponent<SynapseCoreWorldComponent>();
            if (coreWorldComp == null) return;

            foreach (var targetFaction in Find.FactionManager.AllFactionsVisible)
            {
                if (targetFaction.IsPlayer || targetFaction.Hidden) continue;

                var tracker = coreWorldComp.factionTrackers.Find(f => f.factionId == targetFaction.GetUniqueLoadID());
                if (tracker == null)
                {
                    tracker = new RimSynapse.Models.FactionRelationshipTracker { factionId = targetFaction.GetUniqueLoadID() };
                    coreWorldComp.factionTrackers.Add(tracker);
                }

                // Simply add the delta directly to their current perceived amount, preventing negative values
                tracker.perceivedWealth = UnityEngine.Mathf.Max(0, tracker.perceivedWealth + wealthDelta);
                tracker.perceivedStrength = UnityEngine.Mathf.Max(0, tracker.perceivedStrength + strengthDelta);
            }
        }

        public void BroadcastKnowledge(Faction originFaction, float actualWealth, float actualStrength)
        {
            if (originFaction == null || originFaction.IsPlayer) return;

            foreach (Faction targetFaction in Find.FactionManager.AllFactionsVisible)
            {
                if (targetFaction == originFaction || targetFaction.IsPlayer || targetFaction.Hidden) continue;

                float relation = originFaction.GoodwillWith(targetFaction);
                float knowledgeTransferFactor = UnityEngine.Mathf.Clamp01((relation + 100f) / 200f * 0.9f + 0.1f);

                float payloadWealth = actualWealth * knowledgeTransferFactor;
                float payloadStrength = actualStrength * knowledgeTransferFactor;

                int distance = 50;
                if (originFaction.def.settlementGenerationWeight > 0 && targetFaction.def.settlementGenerationWeight > 0)
                {
                    var originBase = Find.WorldObjects.Settlements.Find(s => s.Faction == originFaction);
                    var targetBase = Find.WorldObjects.Settlements.Find(s => s.Faction == targetFaction);
                    if (originBase != null && targetBase != null)
                    {
                        distance = Find.WorldGrid.TraversalDistanceBetween(originBase.Tile, targetBase.Tile, true, 100);
                        if (distance > 100 || distance < 0) distance = 100;
                    }
                }

                int delayTicks = distance * 1000;

                inTransitKnowledge.Add(new KnowledgePacket
                {
                    sourceFactionId = originFaction.GetUniqueLoadID(),
                    targetFactionId = targetFaction.GetUniqueLoadID(),
                    payloadWealth = payloadWealth,
                    payloadStrength = payloadStrength,
                    arrivalTick = Find.TickManager.TicksGame + delayTicks
                });
            }

            // Update originator's own tracker directly
            var coreWorldComp = Find.World.GetComponent<SynapseCoreWorldComponent>();
            if (coreWorldComp != null)
            {
                var originTracker = coreWorldComp.factionTrackers.Find(f => f.factionId == originFaction.GetUniqueLoadID());
                if (originTracker == null)
                {
                    originTracker = new RimSynapse.Models.FactionRelationshipTracker { factionId = originFaction.GetUniqueLoadID() };
                    coreWorldComp.factionTrackers.Add(originTracker);
                }
                originTracker.perceivedWealth = actualWealth;
                originTracker.perceivedStrength = actualStrength;
            }
        }
    }
}
