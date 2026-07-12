using System.Collections.Generic;
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

        // Knowledge Propagation

        public override void WorldComponentTick()
        {
            base.WorldComponentTick();

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
