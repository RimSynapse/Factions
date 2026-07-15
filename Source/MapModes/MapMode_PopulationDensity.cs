using MapModeFramework;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions
{
    public class MapMode_PopulationDensity : MapMode
    {
        private static readonly Material[] densityMats;

        static MapMode_PopulationDensity()
        {
            densityMats = new Material[101];
            for (int i = 0; i <= 100; i++)
            {
                float t = i / 100f;
                // Soft gradient from green (low density) to red (high density)
                Color color = Color.Lerp(new Color(0f, 0.6f, 0.1f, 0.3f), new Color(0.9f, 0.1f, 0.1f, 0.5f), t);
                densityMats[i] = SolidColorMaterials.SimpleSolidColorMaterial(color, false);
            }
        }

        public override WorldLayer_MapMode WorldLayer => WorldLayer_PopulationDensity.Instance;
        public override bool CanToggleWater => false;

        public MapMode_PopulationDensity() { }
        public MapMode_PopulationDensity(MapModeDef def) : base(def) { }

        public override Material GetMaterial(int tile)
        {
            int pop = PopulationDensityUtility.GetPopulationAtTile(tile);
            if (pop <= 0)
            {
                return BaseContent.ClearMat;
            }

            // Map population 0-300 to gradient index 0-100
            int index = Mathf.Clamp(Mathf.RoundToInt(pop / 3f), 0, 100);
            return densityMats[index];
        }

        public override string GetTileLabel(int tile)
        {
            int pop = PopulationDensityUtility.GetPopulationAtTile(tile);
            return pop > 0 ? pop.ToString() : null;
        }

        public override string GetTooltip(int tile)
        {
            int pop = PopulationDensityUtility.GetPopulationAtTile(tile);
            if (pop <= 0)
            {
                return null;
            }
            return $"Pawn dwellings: {pop}";
        }
    }

    public class WorldLayer_PopulationDensity : WorldLayer_MapMode
    {
        public static WorldLayer_PopulationDensity Instance { get; private set; }

        public WorldLayer_PopulationDensity()
        {
            Instance = this;
        }
    }
}
