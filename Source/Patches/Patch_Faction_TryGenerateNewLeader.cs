using HarmonyLib;
using RimWorld;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(Faction), "TryGenerateNewLeader")]
    public static class Patch_Faction_TryGenerateNewLeader
    {
        public static void Postfix(Faction __instance)
        {
            if (Current.ProgramState != ProgramState.Playing) return;

            if (__instance != null && __instance.leader != null)
            {
                // New leader generated, ensure we check and evaluate them
                SynapseFactionEvaluator.EvaluateFaction(__instance);
            }
        }
    }
}
