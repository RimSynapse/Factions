using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimSynapse.Factions.Jobs
{
    public class WorkGiver_Warden_Interrogate : WorkGiver_Warden
    {
        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!ShouldTakeCareOfPrisoner(pawn, t))
            {
                return null;
            }
            Pawn prisoner = (Pawn)t;
            if (prisoner.guest.ExclusiveInteractionMode?.defName == "Synapse_Interrogate" && prisoner.guest.ScheduledForInteraction && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                if (prisoner.Downed || !prisoner.Awake() || !pawn.CanReserve(t, 1, -1, null, forced))
                {
                    return null;
                }
                return JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("Synapse_InterrogatePrisoner"), t);
            }
            return null;
        }
    }
}
