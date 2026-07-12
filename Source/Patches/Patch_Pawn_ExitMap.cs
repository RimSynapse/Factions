using HarmonyLib;
using RimWorld;
using Verse;

namespace RimSynapse.Factions.Patches
{
    /// <summary>
    /// When a non-player pawn exits the map (e.g., a visitor or trader leaves),
    /// broadcasts the colony's actual wealth and combat strength to their faction.
    /// This is how factions learn about your colony and update their perception.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), "ExitMap")]
    public static class Patch_Pawn_ExitMap
    {
        private static System.Collections.Generic.Dictionary<Faction, int> _lastBroadcastTick = new System.Collections.Generic.Dictionary<Faction, int>();

        public static void Prefix(Pawn __instance)
        {
            if (__instance.Faction == null || __instance.Faction.IsPlayer || __instance.Map == null || !__instance.Map.IsPlayerHome)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (_lastBroadcastTick.TryGetValue(__instance.Faction, out int lastTick))
            {
                // Only broadcast once per day per faction (60000 ticks)
                if (currentTick - lastTick < 60000) return;
            }

            var coreComp = Find.World.GetComponent<RimSynapse.SynapseCoreWorldComponent>();
            var factionsComp = Find.World.GetComponent<RimSynapse.Factions.SynapseFactionsWorldComponent>();

            if (coreComp != null && factionsComp != null)
            {
                float actualWealth = __instance.Map.wealthWatcher.WealthTotal;
                
                // Calculate strength without the tension modifier
                float oldTension = coreComp.TensionModifier;
                coreComp.TensionModifier = 1.0f;
                float actualStrength = coreComp.CalculateDynamicThreatPoints(__instance.Map, 500f);
                coreComp.TensionModifier = oldTension;

                factionsComp.BroadcastKnowledge(__instance.Faction, actualWealth, actualStrength);
                _lastBroadcastTick[__instance.Faction] = currentTick;
            }
        }
    }
}
