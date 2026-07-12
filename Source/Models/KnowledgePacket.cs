using Verse;

namespace RimSynapse.Factions.Models
{
    /// <summary>
    /// Represents a packet of faction knowledge (wealth/strength estimates)
    /// traveling between NPC factions. Used by the knowledge
    /// propagation simulation to model information spread with distance-based delays.
    /// </summary>
    public class KnowledgePacket : IExposable
    {
        public string sourceFactionId;
        public string targetFactionId;
        public float payloadWealth;
        public float payloadStrength;
        public int arrivalTick;

        public void ExposeData()
        {
            Scribe_Values.Look(ref sourceFactionId, "sourceFactionId");
            Scribe_Values.Look(ref targetFactionId, "targetFactionId");
            Scribe_Values.Look(ref payloadWealth, "payloadWealth");
            Scribe_Values.Look(ref payloadStrength, "payloadStrength");
            Scribe_Values.Look(ref arrivalTick, "arrivalTick");
        }
    }
}
