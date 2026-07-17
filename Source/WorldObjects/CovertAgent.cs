using System.Collections.Generic;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using RimWorld;
using RimSynapse.Factions.Models;

namespace RimSynapse.Factions.WorldObjects
{
    public class CovertAgent : WorldObject
    {
        public string agentName = "Unknown Agent";
        public Faction trueFaction;
        public XenotypeDef xenotype;
        
        
        // Sowing Discord payload
        public HiddenAgendaLog discordPayload;
        public bool isPoliticalVIP;

        private int ticksToArrival;
        private int targetTile = -1;
        private bool hasArrived = false;

        public void Initialize(Faction faction, XenotypeDef xenotype, int destinationTile)
        {
            this.trueFaction = faction;
            this.xenotype = xenotype;
            this.targetTile = destinationTile;
            this.ticksToArrival = 30000; // Hardcoded travel time for now
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref agentName, "agentName");
            Scribe_References.Look(ref trueFaction, "trueFaction");
            Scribe_Defs.Look(ref xenotype, "xenotype");
            Scribe_Deep.Look(ref discordPayload, "discordPayload");
            Scribe_Values.Look(ref isPoliticalVIP, "isPoliticalVIP", false);
            Scribe_Values.Look(ref ticksToArrival, "ticksToArrival");
            Scribe_Values.Look(ref targetTile, "targetTile");
            Scribe_Values.Look(ref hasArrived, "hasArrived");
        }

        protected override void Tick()
        {
            base.Tick();
            if (!hasArrived)
            {
                ticksToArrival--;
                if (ticksToArrival <= 0)
                {
                    hasArrived = true;
                    this.Tile = targetTile;
                }
            }
        }

        public override string GetInspectString()
        {
            string str = base.GetInspectString();
            if (hasArrived)
            {
                if (!string.IsNullOrEmpty(str)) str += "\n";
                str += "A covert agent has arrived at their destination. Waiting for infiltration orders.";
            }
            else
            {
                if (!string.IsNullOrEmpty(str)) str += "\n";
                str += $"Traveling in disguise. ETA: {ticksToArrival.ToStringTicksToPeriod()}";
            }
            return str;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            if (hasArrived && DebugSettings.godMode) // In actual gameplay, this might be triggered by LLM or events
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Attempt Infiltration",
                    defaultDesc = "Attempts to infiltrate the local settlement using demographics matching.",
                    icon = TexCommand.Attack, // generic icon for now
                    action = () => 
                    {
                        AttemptInfiltration();
                    }
                };
            }
        }

        private void AttemptInfiltration()
        {
            var settlement = Find.WorldObjects.SettlementAt(this.Tile);
            if (settlement == null)
            {
                Messages.Message("No settlement at destination.", MessageTypeDefOf.RejectInput);
                return;
            }

            if (discordPayload != null)
            {
                if (isPoliticalVIP)
                {
                    Messages.Message($"{agentName}, a former political VIP, successfully infiltrated {settlement.Name} using their inside connections! They leaked the hidden agenda: '{discordPayload.hiddenAgenda}' to devastating effect!", MessageTypeDefOf.PositiveEvent);
                    
                    var stWorldComp = Find.World.GetComponent<SynapseFactionsWorldComponent>();
                    var tracker = stWorldComp?.GetOrCreateSettlementTracker(settlement);
                    if (tracker != null)
                    {
                        var crisis = new SettlementCrisis
                        {
                            crisisType = "Internal Rebellion",
                            daysElapsed = 0,
                            currentSeverity = 0.4f // Doubled impact for VIP
                        };
                        tracker.activeCrises.Add(crisis);
                    }
                }
                else
                {
                    Messages.Message($"{agentName} successfully infiltrated {settlement.Name} and leaked the hidden agenda: '{discordPayload.hiddenAgenda}'!", MessageTypeDefOf.PositiveEvent);
                    
                    var stWorldComp = Find.World.GetComponent<SynapseFactionsWorldComponent>();
                    var tracker = stWorldComp?.GetOrCreateSettlementTracker(settlement);
                    if (tracker != null)
                    {
                        var crisis = new SettlementCrisis
                        {
                            crisisType = "Internal Rebellion",
                            daysElapsed = 0,
                            currentSeverity = 0.2f // Massive impact
                        };
                        tracker.activeCrises.Add(crisis);
                    }
                }
            }
            else
            {
                // Infiltration check based on demographics
                if (xenotype == XenotypeDefOf.Baseliner)
                {
                    Messages.Message($"{agentName} successfully infiltrated {settlement.Name}!", MessageTypeDefOf.PositiveEvent);
                }
                else
                {
                    Messages.Message($"{agentName}'s non-standard xenotype ({xenotype.LabelCap}) raised suspicions during infiltration...", MessageTypeDefOf.NegativeEvent);
                }
            }
            
            Find.WorldObjects.Remove(this); // Agent disappears after attempting
        }
    }
}
