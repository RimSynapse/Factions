using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimSynapse.RegionsAndTerritories;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions.Ideology
{
    public class IdeologyDemographicProvider : IRegionDemographicProvider
    {
        public string ProviderName => "Ideology Demographics";

        public float GetDemographicMatchRatio(GeographicProvince province, Faction faction)
        {
            if (!ModsConfig.IdeologyActive || faction == null || province == null || province.tiles == null)
            {
                return -1f; // Ideology DLC not active or invalid inputs
            }

            Ideo targetIdeo = faction.ideos?.PrimaryIdeo;
            if (targetIdeo == null) return 0f;

            int totalPop = 0;
            int matchingPop = 0;

            foreach (int tileId in province.tiles)
            {
                int tilePop = PopulationDensityUtility.GetPopulationAtTile(tileId);
                if (tilePop <= 0) continue;

                totalPop += tilePop;

                var worldObjects = Find.WorldObjects.AllWorldObjects.Where(o => o.Tile == tileId).ToList();
                bool tileMatched = false;

                foreach (var obj in worldObjects)
                {
                    if (obj.Faction != null && obj.Faction.ideos?.PrimaryIdeo == targetIdeo)
                    {
                        matchingPop += tilePop;
                        tileMatched = true;
                        break;
                    }
                }

                if (!tileMatched && faction.ideos?.PrimaryIdeo == targetIdeo)
                {
                    // Fallback tile match
                }
            }

            if (totalPop <= 0)
            {
                return province.owningFactionIds.Contains(faction.GetUniqueLoadID()) ? 1f : 0f;
            }

            return Mathf.Clamp01((float)matchingPop / totalPop);
        }
    }
}
