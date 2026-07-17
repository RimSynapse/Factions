using LudeonTK;
using RimWorld;
using Verse;

namespace RimSynapse.Factions.UI
{
    public static class DebugActions_Factions
    {
        [DebugAction("RimSynapse", "Show Faction Perceptions", actionType = DebugActionType.Action, allowedGameStates = AllowedGameStates.PlayingOnMap | AllowedGameStates.PlayingOnWorld)]
        private static void ShowFactionPerceptions()
        {
            Find.WindowStack.Add(new Dialog_FactionPerceptions());
        }
    }
}
