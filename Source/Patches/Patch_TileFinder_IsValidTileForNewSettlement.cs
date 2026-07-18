using System;
using System.Linq;
using System.Text;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(TileFinder), nameof(TileFinder.IsValidTileForNewSettlement))]
    public static class Patch_TileFinder_IsValidTileForNewSettlement
    {
        [HarmonyPostfix]
        public static void Postfix(PlanetTile tile, ref bool __result, StringBuilder reason)
        {
            // If it's already invalid, do nothing
            if (!__result) return;
            if (tile == null) return;

            if (Find.World == null) return;

            var regionManager = Find.World.GetComponent<SynapseRegionManager>();
            if (regionManager == null) return;

            int tileId = tile.tileId;
            int provinceId = regionManager.GetProvinceId(tileId);
            if (provinceId != -1)
            {
                var province = regionManager.Provinces.FirstOrDefault(p => p.id == provinceId);
                if (province != null && province.owningFactionIds.Any())
                {
                    // Found a claimed region, block settling!
                    __result = false;
                    if (reason != null)
                    {
                        reason.AppendLine("Cannot settle here: This region is claimed by another faction.");
                    }
                }
            }
        }
    }
}
