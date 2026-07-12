using HarmonyLib;
using RimWorld;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(Faction), "NaturalGoodwill", MethodType.Getter)]
    public static class Patch_Faction_NaturalGoodwill
    {
        public static void Postfix(Faction __instance, ref int __result)
        {
            if (__instance == null || __instance.IsPlayer) return;

            var factionsWorldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (factionsWorldComp == null || factionsWorldComp.factionStoryTrackers == null) return;

            var tracker = factionsWorldComp.factionStoryTrackers.Find(f => f.factionId == __instance.GetUniqueLoadID());
            if (tracker != null && tracker.customNaturalGoodwill.HasValue)
            {
                __result = tracker.customNaturalGoodwill.Value;
            }
        }
    }
}
