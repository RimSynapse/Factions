using Verse;

namespace RimSynapse.Factions.Models
{
    public class GoodwillSample : IExposable
    {
        public int tick;
        public float goodwill;

        public void ExposeData()
        {
            Scribe_Values.Look(ref tick, "tick");
            Scribe_Values.Look(ref goodwill, "goodwill");
        }
    }
}
