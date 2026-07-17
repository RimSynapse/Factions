using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Factions.Models
{
    public class SettlementStoryTracker : IExposable
    {
        public int settlementId;
        public string factionId;

        // --- Demographics ---
        public int totalDwellings;
        public int currentPopulation;

        // --- Macro Resources ---
        public float rawNutrition;
        public float biomass;
        public float minerals;
        public float textiles;

        public float preIndustrialGoods;
        public float industrialGoods;
        public float spacerGoods;

        // --- Event Crises ---
        public List<SettlementCrisis> activeCrises = new List<SettlementCrisis>();

        public void ExposeData()
        {
            Scribe_Values.Look(ref settlementId, "settlementId", -1);
            Scribe_Values.Look(ref factionId, "factionId");

            Scribe_Values.Look(ref totalDwellings, "totalDwellings", 0);
            Scribe_Values.Look(ref currentPopulation, "currentPopulation", 0);

            Scribe_Values.Look(ref rawNutrition, "rawNutrition", 0f);
            Scribe_Values.Look(ref biomass, "biomass", 0f);
            Scribe_Values.Look(ref minerals, "minerals", 0f);
            Scribe_Values.Look(ref textiles, "textiles", 0f);

            Scribe_Values.Look(ref preIndustrialGoods, "preIndustrialGoods", 0f);
            Scribe_Values.Look(ref industrialGoods, "industrialGoods", 0f);
            Scribe_Values.Look(ref spacerGoods, "spacerGoods", 0f);
            
            Scribe_Collections.Look(ref activeCrises, "activeCrises", LookMode.Deep);
            if (activeCrises == null)
            {
                activeCrises = new List<SettlementCrisis>();
            }
        }
    }

    public class SettlementCrisis : IExposable
    {
        public string crisisType; // e.g., "Blight"
        public float currentSeverity;
        public int ticksRemaining; // or days elapsed
        public int daysElapsed;

        public void ExposeData()
        {
            Scribe_Values.Look(ref crisisType, "crisisType");
            Scribe_Values.Look(ref currentSeverity, "currentSeverity", 0f);
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", 0);
            Scribe_Values.Look(ref daysElapsed, "daysElapsed", 0);
        }
    }
}
