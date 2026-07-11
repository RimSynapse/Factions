using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Factions
{
    public class SynapseFactionsWorldComponent : WorldComponent
    {
        public Dictionary<int, string> LLMGeopoliticsHistory = new Dictionary<int, string>();
        
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
        }
    }
}
