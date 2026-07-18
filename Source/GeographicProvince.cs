using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimSynapse.Factions
{
    public enum ProvinceType { Land, River, MountainRange, Lake, Ocean }

    public class GeographicProvince : IExposable
    {
        public int id;
        public List<int> tiles = new List<int>();
        public string name;
        public BiomeDef primaryBiome;
        public List<string> owningFactionIds = new List<string>();
        public ProvinceType provinceType = ProvinceType.Land;

        // --- Economics / Demographics ---
        public bool initializedEconomics;

        private int _totalDwellings;
        public int totalDwellings
        {
            get
            {
                int totalProvincePop = 0;
                if (tiles != null)
                {
                    foreach (int tileId in tiles)
                    {
                        totalProvincePop += PopulationDensityUtility.GetPopulationAtTile(tileId);
                    }
                }
                if (totalProvincePop <= 0) return 0;
                int dwellings = totalProvincePop / 2;
                return dwellings < 1 ? 1 : dwellings;
            }
            set
            {
                _totalDwellings = value;
            }
        }

        private int _currentPopulation;
        public int currentPopulation
        {
            get
            {
                if (_currentPopulation <= 0)
                {
                    int totalProvincePop = 0;
                    if (tiles != null)
                    {
                        foreach (int tileId in tiles)
                        {
                            totalProvincePop += PopulationDensityUtility.GetPopulationAtTile(tileId);
                        }
                    }
                    _currentPopulation = totalProvincePop;
                }
                return _currentPopulation;
            }
            set
            {
                _currentPopulation = value;
            }
        }

        public float rawNutrition;
        public float biomass;
        public float minerals;
        public float textiles;

        public float preIndustrialGoods;
        public float industrialGoods;
        public float spacerGoods;

        public List<Models.SettlementCrisis> activeCrises = new List<Models.SettlementCrisis>();

        public GeographicProvince() { }

        public GeographicProvince(int id)
        {
            this.id = id;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Collections.Look(ref tiles, "tiles", LookMode.Value);
            Scribe_Values.Look(ref name, "name");
            Scribe_Defs.Look(ref primaryBiome, "primaryBiome");
            Scribe_Collections.Look(ref owningFactionIds, "owningFactionIds", LookMode.Value);
            
            if (owningFactionIds == null)
            {
                owningFactionIds = new List<string>();
            }

            Scribe_Values.Look(ref initializedEconomics, "initializedEconomics", false);
            Scribe_Values.Look(ref provinceType, "provinceType", ProvinceType.Land);
            Scribe_Values.Look(ref _totalDwellings, "totalDwellings", 0);
            Scribe_Values.Look(ref _currentPopulation, "currentPopulation", 0);

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
                activeCrises = new List<Models.SettlementCrisis>();
            }
        }

        public void InitializeProvinceEconomics()
        {
            initializedEconomics = true;
            if (tiles == null || tiles.Count == 0 || Find.WorldGrid == null) return;

            float totalPlantDensity = 0f;
            float totalForageability = 0f;
            float totalTreeDensity = 0f;
            float totalHillMult = 0f;

            foreach (int tileId in tiles)
            {
                Tile t = Find.WorldGrid[tileId];
                var b = t.PrimaryBiome;
                if (b != null)
                {
                    totalPlantDensity += b.plantDensity;
                    totalForageability += b.forageability;
                    totalTreeDensity += b.TreeDensity;
                }
                float hillMult = 0.5f;
                if (t.hilliness == Hilliness.SmallHills) hillMult = 1.0f;
                else if (t.hilliness == Hilliness.LargeHills) hillMult = 2.0f;
                else if (t.hilliness == Hilliness.Mountainous) hillMult = 3.0f;
                totalHillMult += hillMult;
            }

            float avgPlant = totalPlantDensity / tiles.Count;
            float avgForage = totalForageability / tiles.Count;
            float avgTree = totalTreeDensity / tiles.Count;
            float avgHill = totalHillMult / tiles.Count;

            // Determine the highest tech level among factions owning settlements here
            TechLevel maxTech = TechLevel.Neolithic;
            int settlementCount = 0;
            foreach (var settlement in Find.WorldObjects.Settlements)
            {
                if (tiles.Contains(settlement.Tile))
                {
                    settlementCount++;
                    if (settlement.Faction != null && settlement.Faction.def.techLevel > maxTech)
                    {
                        maxTech = settlement.Faction.def.techLevel;
                    }
                }
            }

            // Demographics aggregated from the population density tracker
            int totalProvincePop = 0;
            foreach (int tileId in tiles)
            {
                totalProvincePop += PopulationDensityUtility.GetPopulationAtTile(tileId);
            }

            currentPopulation = totalProvincePop;
            totalDwellings = currentPopulation / 2;

            bool isSpacer = maxTech >= TechLevel.Spacer;
            bool isIndustrial = maxTech == TechLevel.Industrial;

            if (isSpacer)
                rawNutrition = 1000f * tiles.Count;
            else if (isIndustrial)
                rawNutrition = avgPlant * 500f * tiles.Count;
            else
                rawNutrition = avgForage * 500f * tiles.Count;

            biomass = avgTree * 500f * tiles.Count;
            minerals = avgHill * 500f * tiles.Count;
            textiles = 100f * tiles.Count;

            preIndustrialGoods = 0f;
            industrialGoods = 0f;
            spacerGoods = 0f;
        }
    }
}
