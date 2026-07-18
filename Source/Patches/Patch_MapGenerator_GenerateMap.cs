using HarmonyLib;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(MapGenerator), nameof(MapGenerator.GenerateMap))]
    internal static class Patch_MapGenerator_GenerateMap
    {
        [HarmonyPostfix]
        static void Postfix(Map __result)
        {
            if (__result == null || !__result.IsPlayerHome) return;

            int pop = PopulationDensityUtility.GetPopulationAtTile(__result.Tile);
            if (pop > 0)
            {
                DwellingStructureGenerator.Generate(__result, pop);
            }
        }
    }
}
