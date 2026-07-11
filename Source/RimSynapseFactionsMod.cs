using HarmonyLib;
using Verse;

namespace RimSynapse.Factions
{
    public class RimSynapseFactionsMod : Mod
    {
        public RimSynapseFactionsMod(ModContentPack content) : base(content)
        {
            RimSynapse.SynapseLog.Info("factions", "[RimSynapse-Factions] Initializing Mod...");
            
            var harmony = new Harmony("rimsynapse.factions");
            harmony.PatchAll();
            
            RimSynapse.SynapseLog.Info("factions", "[RimSynapse-Factions] Harmony Patches applied.");
        }
    }
}
