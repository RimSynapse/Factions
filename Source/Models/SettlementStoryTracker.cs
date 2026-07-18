using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;
using RimSynapse.RegionsAndTerritories;

namespace RimSynapse.Factions.Models
{
    public class SettlementStoryTracker : IExposable
    {
        public int settlementId;
        public string factionId;

        // Local fallbacks in case province isn't resolved
        private int _totalDwellings;
        private int _currentPopulation;
        private float _rawNutrition;
        private float _biomass;
        private float _minerals;
        private float _textiles;
        private float _preIndustrialGoods;
        private float _industrialGoods;
        private float _spacerGoods;
        private List<SettlementCrisis> _activeCrises = new List<SettlementCrisis>();

        private GeographicProvince Province
        {
            get
            {
                if (Find.World == null) return null;
                var settlement = Find.WorldObjects.Settlements.FirstOrDefault(s => s.ID == settlementId);
                if (settlement == null) return null;
                return Find.World.GetComponent<SynapseRegionManager>()?.GetProvinceForTile(settlement.Tile);
            }
        }

        // --- Demographics ---
        public int totalDwellings
        {
            get => Province != null ? Province.totalDwellings : _totalDwellings;
            set { if (Province != null) Province.totalDwellings = value; else _totalDwellings = value; }
        }

        public int currentPopulation
        {
            get => Province != null ? Province.currentPopulation : _currentPopulation;
            set { if (Province != null) Province.currentPopulation = value; else _currentPopulation = value; }
        }

        // --- Macro Resources ---
        public float rawNutrition
        {
            get => Province != null ? Province.rawNutrition : _rawNutrition;
            set { if (Province != null) Province.rawNutrition = value; else _rawNutrition = value; }
        }

        public float biomass
        {
            get => Province != null ? Province.biomass : _biomass;
            set { if (Province != null) Province.biomass = value; else _biomass = value; }
        }

        public float minerals
        {
            get => Province != null ? Province.minerals : _minerals;
            set { if (Province != null) Province.minerals = value; else _minerals = value; }
        }

        public float textiles
        {
            get => Province != null ? Province.textiles : _textiles;
            set { if (Province != null) Province.textiles = value; else _textiles = value; }
        }

        public float preIndustrialGoods
        {
            get => Province != null ? Province.preIndustrialGoods : _preIndustrialGoods;
            set { if (Province != null) Province.preIndustrialGoods = value; else _preIndustrialGoods = value; }
        }

        public float industrialGoods
        {
            get => Province != null ? Province.industrialGoods : _industrialGoods;
            set { if (Province != null) Province.industrialGoods = value; else _industrialGoods = value; }
        }

        public float spacerGoods
        {
            get => Province != null ? Province.spacerGoods : _spacerGoods;
            set { if (Province != null) Province.spacerGoods = value; else _spacerGoods = value; }
        }

        // --- Event Crises ---
        public List<SettlementCrisis> activeCrises
        {
            get => Province != null ? Province.activeCrises : _activeCrises;
            set { if (Province != null) Province.activeCrises = value; else _activeCrises = value; }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref settlementId, "settlementId", -1);
            Scribe_Values.Look(ref factionId, "factionId");

            // Expose the local fallbacks so saving still works perfectly if loaded/saved out of game context
            Scribe_Values.Look(ref _totalDwellings, "totalDwellings", 0);
            Scribe_Values.Look(ref _currentPopulation, "currentPopulation", 0);
            Scribe_Values.Look(ref _rawNutrition, "rawNutrition", 0f);
            Scribe_Values.Look(ref _biomass, "biomass", 0f);
            Scribe_Values.Look(ref _minerals, "minerals", 0f);
            Scribe_Values.Look(ref _textiles, "textiles", 0f);
            Scribe_Values.Look(ref _preIndustrialGoods, "preIndustrialGoods", 0f);
            Scribe_Values.Look(ref _industrialGoods, "industrialGoods", 0f);
            Scribe_Values.Look(ref _spacerGoods, "spacerGoods", 0f);
            Scribe_Collections.Look(ref _activeCrises, "activeCrises", LookMode.Deep);
            if (_activeCrises == null)
            {
                _activeCrises = new List<SettlementCrisis>();
            }
        }
    }}
