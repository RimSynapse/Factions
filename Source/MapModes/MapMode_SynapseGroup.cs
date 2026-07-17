using System.Collections.Generic;
using System.Linq;
using MapModeFramework;
using RimWorld;
using Verse;

namespace RimSynapse.Factions
{
    public class MapMode_SynapseGroup : MapMode
    {
        public override WorldLayer_MapMode WorldLayer => null;

        public MapMode_SynapseGroup() { }
        public MapMode_SynapseGroup(MapModeDef def) : base(def) { }

        public override void OnButtonClick()
        {
            if (MapModeComponent.Instance == null) return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();

            var popMode = MapModeComponent.Instance.mapModes.FirstOrDefault(m => m.def.defName == "SynapsePopulationDensity");
            if (popMode != null)
            {
                options.Add(new FloatMenuOption(popMode.def.LabelCap, () => MapModeComponent.Instance.RequestMapModeSwitch(popMode)));
            }

            var territoryMode = MapModeComponent.Instance.mapModes.FirstOrDefault(m => m.def.defName == "SynapseFactionTerritory");
            if (territoryMode != null)
            {
                options.Add(new FloatMenuOption(territoryMode.def.LabelCap, () => MapModeComponent.Instance.RequestMapModeSwitch(territoryMode)));
            }

            if (options.Any())
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }
    }
}
