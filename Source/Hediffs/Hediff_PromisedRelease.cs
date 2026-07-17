using RimWorld;
using Verse;

namespace RimSynapse.Factions.Hediffs
{
    public class Hediff_PromisedRelease : HediffWithComps
    {
        public override void Tick()
        {
            base.Tick();
            
            // Check once per hour
            if (pawn.IsHashIntervalTick(2500))
            {
                if (pawn.guest != null && pawn.IsPrisonerOfColony)
                {
                    if (pawn.guest.ExclusiveInteractionMode != PrisonerInteractionModeDefOf.Release)
                    {
                        // They changed the mode away from Release! Betrayal!
                        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(DefDatabase<ThoughtDef>.GetNamed("Synapse_BetrayedPromise"));
                        
                        // Increase rebellion chance significantly (Vanilla uses mental breaks and prison breaks)
                        // A -40 mood will almost certainly cause extreme mental breaks, driving prison breaks.
                        
                        pawn.health.RemoveHediff(this);
                        Messages.Message($"{pawn.NameShortColored} realized you broke your promise to release them! They harbor a deep hatred.", pawn, MessageTypeDefOf.NegativeEvent);
                    }
                }
            }
        }
    }
}
