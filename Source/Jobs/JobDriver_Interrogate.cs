using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;
using RimSynapse.Factions.Models;

namespace RimSynapse.Factions.Jobs
{
    public class JobDriver_Interrogate : JobDriver
    {
        protected Pawn Prisoner => (Pawn)job.targetA.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Prisoner, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOnMentalState(TargetIndex.A);
            this.FailOnNotAwake(TargetIndex.A);
            this.FailOn(() => !Prisoner.IsPrisonerOfColony || !Prisoner.guest.PrisonerIsSecure);

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil interrogate = Toils_General.Wait(300);
            interrogate.tickAction = delegate
            {
                pawn.rotationTracker.FaceCell(Prisoner.Position);
                Prisoner.rotationTracker.FaceCell(pawn.Position);
            };
            yield return interrogate;

            yield return Toils_General.Do(delegate
            {
                if (Prisoner.guest.resistance > 0f)
                {
                    // Basically same as "Reduce Resistance" but branded as Interrogation
                    float num = 1f;
                    num *= pawn.GetStatValue(StatDefOf.NegotiationAbility);
                    Prisoner.guest.resistance = UnityEngine.Mathf.Max(0f, Prisoner.guest.resistance - num);
                    
                    Messages.Message($"Interrogating {Prisoner.NameShortColored} reduced resistance by {num:F1}. Remaining: {Prisoner.guest.resistance:F1}", Prisoner, MessageTypeDefOf.NeutralEvent);
                    
                    pawn.skills.Learn(SkillDefOf.Social, 70f);
                }
                else
                {
                    // Resistance broken - attempt extraction
                    pawn.skills.Learn(SkillDefOf.Social, 100f);
                    AttemptInformationExtraction();
                }

                // We cannot assign ScheduledForInteraction directly as it is read-only in 1.4, so we do nothing here.
                // The vanilla job driver uses PrisonerInteractionModeUtility.SetInteractionMode if they want to change it.
            });
        }

        private void AttemptInformationExtraction()
        {
            var faction = Prisoner.Faction;
            if (faction == null)
            {
                Messages.Message($"{Prisoner.NameShortColored} has no known faction and knows no secrets.", Prisoner, MessageTypeDefOf.RejectInput);
                return;
            }

            var worldComp = Find.World?.GetComponent<SynapseFactionsWorldComponent>();
            if (worldComp == null) return;

            var tracker = worldComp.GetOrCreateStoryTracker(faction.GetUniqueLoadID());
            var unknownAgendas = tracker.historicalAgendas.Where(a => !a.discoveredByPlayer).ToList();

            if (unknownAgendas.Count == 0)
            {
                Messages.Message($"{Prisoner.NameShortColored} has no new information to reveal.", Prisoner, MessageTypeDefOf.NeutralEvent);
                return;
            }

            // Chance to haggle
            if (Rand.Chance(0.3f))
            {
                // Haggle
                DiaNode diaNode = new DiaNode($"{Prisoner.NameShortColored} offers a deal: They will reveal their faction's hidden agenda if you promise to release them immediately. If you accept and later re-arrest them, they will harbor a deep hatred.");
                DiaOption accept = new DiaOption("Accept the deal.");
                accept.action = () =>
                {
                    RevealSecret(unknownAgendas.RandomElement(), tracker);
                    // Add a custom thought/hediff or dictionary entry tracking the promise
                    // For now, we'll apply a custom Hediff that marks them as "Promised Release"
                    Prisoner.health.AddHediff(HediffDef.Named("Synapse_PromisedRelease"));
                    Messages.Message($"{Prisoner.NameShortColored} expects you to set their interaction mode to Release immediately.", Prisoner, MessageTypeDefOf.NeutralEvent);
                };
                accept.resolveTree = true;

                DiaOption refuse = new DiaOption("Refuse.");
                refuse.action = () =>
                {
                    Messages.Message($"{Prisoner.NameShortColored} refuses to speak.", Prisoner, MessageTypeDefOf.NegativeEvent);
                };
                refuse.resolveTree = true;

                diaNode.options.Add(accept);
                diaNode.options.Add(refuse);

                Find.WindowStack.Add(new Dialog_NodeTree(diaNode, delayInteractivity: true));
            }
            else
            {
                // Skill check to force it
                float chance = pawn.GetStatValue(StatDefOf.NegotiationAbility) * 0.5f;
                if (Rand.Chance(chance))
                {
                    RevealSecret(unknownAgendas.RandomElement(), tracker);
                }
                else
                {
                    Messages.Message($"{Prisoner.NameShortColored} resisted interrogation.", Prisoner, MessageTypeDefOf.NeutralEvent);
                }
            }
        }

        private void RevealSecret(HiddenAgendaLog agenda, FactionStoryTracker tracker)
        {
            agenda.discoveredByPlayer = true;
            Find.LetterStack.ReceiveLetter(
                "Hidden Agenda Discovered",
                $"Interrogation of {Prisoner.NameShortColored} has revealed a hidden agenda belonging to {Prisoner.Faction.Name}:\n\nOngoing Operation: {agenda.hiddenAgenda}\n\nThis information can be used to sow discord.",
                LetterDefOf.PositiveEvent,
                Prisoner
            );
        }
    }
}
