using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions.Patches
{
    [HarmonyPatch(typeof(Page_CreateWorldParams), "DoWindowContents")]
    public static class Patch_Page_CreateWorldParams_DoWindowContents
    {
        public static void Postfix(Page_CreateWorldParams __instance, Rect rect)
        {
            Rect buttonRect = new Rect(rect.xMax - 320f, rect.yMax - 38f, 150f, 38f);
            if (Widgets.ButtonText(buttonRect, "Faction Geography"))
            {
                Find.WindowStack.Add(new Dialog_FactionPlacementSettings());
            }
        }
    }
}
