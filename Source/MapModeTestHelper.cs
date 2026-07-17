using Verse;
using RimWorld;
using System.Linq;
using MapModeFramework;

namespace RimSynapse.Factions
{
    public class MapModeTestHelper : GameComponent
    {
        private bool triggered = false;

        public MapModeTestHelper(Game game)
        {
        }

        public override void GameComponentUpdate()
        {
            if (triggered) return;
            if (Find.CurrentMap == null) return;

            // Wait until 300 ticks to ensure map is fully loaded
            if (Find.TickManager.TicksGame > 300)
            {
                triggered = true;
                Log.Warning("[MapModeTestHelper] Auto-opening World Map and enabling Faction Territories overlay...");
                
                // Open World Map
                Find.World.renderer.wantedMode = RimWorld.Planet.WorldRenderMode.Planet;

                // Find MapModeComponent and set current map mode
                var mapModeComp = MapModeComponent.Instance;
                if (mapModeComp != null)
                {
                    var targetMode = mapModeComp.mapModes.FirstOrDefault(m => m.def.defName == "SynapseFactionTerritory");
                    if (targetMode != null)
                    {
                        mapModeComp.SwitchMapMode(targetMode);
                        Log.Warning("[MapModeTestHelper] Successfully activated Faction Territories map mode!");
                    }
                    else
                    {
                        Log.Error("[MapModeTestHelper] Could not find map mode def 'SynapseFactionTerritory'!");
                    }
                }
                else
                {
                    Log.Error("[MapModeTestHelper] MapModeComponent.Instance is null!");
                }
            }
        }
    }
}
