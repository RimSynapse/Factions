using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MapModeFramework;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(MapModeDef), "Icon", MethodType.Getter)]
    public static class Patch_MapModeDef_Icon
    {
        private static Texture2D processedIcon = null;

        [HarmonyPrefix]
        public static bool Prefix(MapModeDef __instance, ref Texture2D __result)
        {
            if (__instance.defName == "SynapseMapModeGroup")
            {
                if (processedIcon != null)
                {
                    __result = processedIcon;
                    return false;
                }

                if (!string.IsNullOrEmpty(__instance.iconPath))
                {
                    Texture2D rawIcon = ContentFinder<Texture2D>.Get(__instance.iconPath, false);
                    if (rawIcon != null && rawIcon != BaseContent.BadTex)
                    {
                        processedIcon = TextureUtility.MakeTextureReadableAndTransparent(rawIcon);
                        __result = processedIcon;
                        return false;
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(MapModeUI), "MapModes", MethodType.Getter)]
    public static class Patch_MapModeUI_MapModes
    {
        [HarmonyPostfix]
        public static void Postfix(ref List<MapMode> __result)
        {
            if (__result != null)
            {
                __result = __result.Where(m => m.def.defName != "SynapsePopulationDensity" && m.def.defName != "SynapseFactionTerritory").ToList();
            }
        }
    }
}
