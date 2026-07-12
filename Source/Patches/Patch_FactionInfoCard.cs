using HarmonyLib;
using RimWorld;
using Verse;

namespace RimSynapse.Factions.Patches
{
    /// <summary>
    /// Temporarily replaces faction.def.description with AI-generated history
    /// while the faction info card is rendering, then restores the original.
    ///
    /// This is safe because:
    /// - Unity rendering is single-threaded (main thread only)
    /// - The prefix/postfix pair guarantees restoration
    /// - We only swap when we actually have generated content
    /// </summary>
    [HarmonyPatch(typeof(Dialog_InfoCard), nameof(Dialog_InfoCard.DoWindowContents))]
    internal static class Patch_FactionInfoCard
    {
        private static string _originalDescription;
        private static FactionDef _swappedDef;

        static void Prefix(Dialog_InfoCard __instance)
        {
            _originalDescription = null;
            _swappedDef = null;

            // Use Traverse to access the private faction field
            var faction = Traverse.Create(__instance).Field("faction").GetValue<Faction>();
            if (faction == null) return;

            if (Find.World == null) return;
            var factionsWorldComp = Find.World.GetComponent<SynapseFactionsWorldComponent>();
            if (factionsWorldComp == null) return;

            var tracker = factionsWorldComp.factionStoryTrackers.Find(f => f.factionId == faction.GetUniqueLoadID());
            if (tracker == null || string.IsNullOrEmpty(tracker.factionHistory)) return;

            // Save original and swap
            _swappedDef = faction.def;
            _originalDescription = faction.def.description;
            faction.def.description = tracker.factionHistory;
        }

        static void Postfix()
        {
            // Always restore, even if an exception occurred during rendering
            if (_swappedDef != null && _originalDescription != null)
            {
                _swappedDef.description = _originalDescription;
                _swappedDef = null;
                _originalDescription = null;
            }
        }
    }
}
