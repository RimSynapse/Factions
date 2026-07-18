using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimSynapse.Factions
{
    public class FactionPlacementProfile : IExposable
    {
        public string factionDefName;
        public float mineralWeight = 1.0f;
        public float nutritionWeight = 1.0f;
        public float forageWeight = 1.0f;
        public float grazingWeight = 1.0f;
        public float huntingWeight = 1.0f;
        public float marginWeight = 0.0f;
        public IntRange baseCountRange = new IntRange(5, 15);
        public int placementOrder = 3;

        public FactionPlacementProfile() { }

        public FactionPlacementProfile(string defName, float mineral, float nutrition, float forage, float grazing, float hunting, float margin, int minB, int maxB, int order)
        {
            this.factionDefName = defName;
            this.mineralWeight = mineral;
            this.nutritionWeight = nutrition;
            this.forageWeight = forage;
            this.grazingWeight = grazing;
            this.huntingWeight = hunting;
            this.marginWeight = margin;
            this.baseCountRange = new IntRange(minB, maxB);
            this.placementOrder = order;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref factionDefName, "factionDefName");
            Scribe_Values.Look(ref mineralWeight, "mineralWeight", 1.0f);
            Scribe_Values.Look(ref nutritionWeight, "nutritionWeight", 1.0f);
            Scribe_Values.Look(ref forageWeight, "forageWeight", 1.0f);
            Scribe_Values.Look(ref grazingWeight, "grazingWeight", 1.0f);
            Scribe_Values.Look(ref huntingWeight, "huntingWeight", 1.0f);
            Scribe_Values.Look(ref marginWeight, "marginWeight", 0.0f);
            Scribe_Values.Look(ref baseCountRange, "baseCountRange", new IntRange(5, 15));
            Scribe_Values.Look(ref placementOrder, "placementOrder", 3);
        }
    }

    public class FactionPlacementSettings : ModSettings
    {
        public static Dictionary<string, FactionPlacementProfile> profiles = new Dictionary<string, FactionPlacementProfile>();
        public static int minRegionSize = 75;
        public static int maxRegionSize = 150;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref minRegionSize, "minRegionSize", 75);
            Scribe_Values.Look(ref maxRegionSize, "maxRegionSize", 150);
            
            List<FactionPlacementProfile> list = profiles.Values.ToList();
            Scribe_Collections.Look(ref list, "profiles", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && list != null)
            {
                profiles.Clear();
                foreach (var p in list)
                {
                    if (p.factionDefName != null)
                    {
                        profiles[p.factionDefName] = p;
                    }
                }
            }
        }

        public static FactionPlacementProfile GetProfile(FactionDef def)
        {
            if (def == null) return null;
            if (!profiles.TryGetValue(def.defName, out var p))
            {
                p = GetDefaultProfile(def);
                profiles[def.defName] = p;
            }
            return p;
        }

        public static FactionPlacementProfile GetDefaultProfile(FactionDef def)
        {
            float mineral = 1.0f;
            float nutrition = 1.0f;
            float forage = 1.0f;
            float grazing = 1.0f;
            float hunting = 1.0f;
            float margin = 0.0f;
            int minB = 5;
            int maxB = 15;

            if (def.techLevel >= TechLevel.Spacer)
            {
                mineral = 2.5f;
                nutrition = 0.5f;
                forage = 0.1f;
                grazing = 0.1f;
                hunting = 0.2f;
                margin = 0.0f;
            }
            else if (def.techLevel == TechLevel.Industrial)
            {
                mineral = 1.0f;
                nutrition = 2.0f;
                forage = 0.2f;
                grazing = 0.8f;
                hunting = 0.8f;
                margin = 0.0f;
            }
            else
            {
                mineral = 0.2f;
                nutrition = 0.2f;
                forage = 2.0f;
                if (def.hostileToFactionlessHumanlikes || def.permanentEnemy)
                {
                    grazing = 0.2f;
                    hunting = 2.0f;
                }
                else
                {
                    grazing = 2.0f;
                    hunting = 0.2f;
                }
                margin = 0.1f;
            }

            int order = 3;
            if (def.defName == "Empire")
            {
                order = 2;
            }
            else if (def.techLevel == TechLevel.Industrial)
            {
                order = 1;
            }
            else if (def.techLevel >= TechLevel.Spacer)
            {
                order = 3;
            }
            else
            {
                order = 4;
            }

            if (def.hostileToFactionlessHumanlikes || def.permanentEnemy)
            {
                margin = 2.5f;
                minB = 3;
                maxB = 8;
            }

            return new FactionPlacementProfile(def.defName, mineral, nutrition, forage, grazing, hunting, margin, minB, maxB, order);
        }
    }
}
