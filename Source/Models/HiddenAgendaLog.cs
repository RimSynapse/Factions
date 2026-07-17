using Verse;

namespace RimSynapse.Factions.Models
{
    public class HiddenAgendaLog : IExposable
    {
        public string id;
        public string initiatingFactionId;
        public string targetFactionId;
        public string actionType;
        public string publicReason;
        public string hiddenAgenda;
        public bool discoveredByPlayer;
        public int tickGenerated;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref initiatingFactionId, "initiatingFactionId");
            Scribe_Values.Look(ref targetFactionId, "targetFactionId");
            Scribe_Values.Look(ref actionType, "actionType");
            Scribe_Values.Look(ref publicReason, "publicReason");
            Scribe_Values.Look(ref hiddenAgenda, "hiddenAgenda");
            Scribe_Values.Look(ref discoveredByPlayer, "discoveredByPlayer", false);
            Scribe_Values.Look(ref tickGenerated, "tickGenerated");
        }
    }
}
