using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MapModeFramework;
using Region = MapModeFramework.Region;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions
{
    public class MapMode_GeographicProvinces : MapMode_Region
    {
        public override Material RegionMaterial => BaseContent.ClearMat;

        public MapMode_GeographicProvinces() { }
        public MapMode_GeographicProvinces(MapModeDef def) : base(def) { }

        public override void SetRegions()
        {
            if (!UnityData.IsInMainThread) return;

            ClearCache();
            regions.Clear();

            if (Find.World == null) return;
            var regionManager = Find.World.GetComponent<SynapseRegionManager>();
            if (regionManager == null) return;

            foreach (var province in regionManager.Provinces)
            {
                if (province.tiles.Count == 0) continue;

                Material bodyMat = BaseContent.ClearMat;

                Color borderColor = new Color(1f, 1f, 1f, 0.8f);
                Material borderMat = null;
                if (ShaderDatabase.MetaOverlay != null && BaseContent.WhiteTex != null)
                {
                    borderMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, borderColor, 3510);
                }
                if (borderMat == null)
                {
                    borderMat = SolidColorMaterials.SimpleSolidColorMaterial(borderColor);
                }
                if (borderMat == null)
                {
                    borderMat = BaseContent.WhiteMat;
                }

                float borderWidth = def?.RegionProperties?.borderWidth ?? 0.7f;
                bool doBorders = def?.RegionProperties?.doBorders ?? true;

                string ownerString = "Unaffiliated Wilderness";
                if (province.owningFactionIds.Any())
                {
                    var factions = province.owningFactionIds
                        .Select(id => Find.FactionManager.AllFactions.FirstOrDefault(f => f.GetUniqueLoadID() == id))
                        .Where(f => f != null)
                        .ToList();

                    if (factions.Count == 1)
                    {
                        ownerString = "Domain of: " + factions[0].Name;
                    }
                    else if (factions.Count > 1)
                    {
                        ownerString = "Contested Domain of:\n" + string.Join("\n", factions.Select(f => "- " + f.Name));
                    }
                }

                string tooltipText = $"Region: {province.name}\nBiome: {province.primaryBiome?.LabelCap ?? "Unknown"}\nStatus: {ownerString}\nTiles: {province.tiles.Count}";

                Region region = new Region(
                    province.name,
                    province.tiles,
                    false,
                    bodyMat,
                    doBorders,
                    borderMat,
                    borderWidth,
                    tooltipText
                );

                regions.Add(region);
            }
        }

        public override string GetTooltip(int tile)
        {
            if (Find.World == null) return base.GetTooltip(tile);
            var regionManager = Find.World.GetComponent<SynapseRegionManager>();
            if (regionManager == null) return base.GetTooltip(tile);

            var province = regionManager.GetProvinceForTile(tile);
            if (province == null) return base.GetTooltip(tile);

            return GetProvinceTooltip(province, tile);
        }

        public static string GetProvinceTooltip(GeographicProvince province, int tile)
        {
            if (province == null) return string.Empty;

            if (!province.initializedEconomics)
            {
                province.InitializeProvinceEconomics();
            }

            string ownerString = "Unaffiliated Wilderness";
            if (province.owningFactionIds.Any())
            {
                var factions = province.owningFactionIds
                    .Select(id => Find.FactionManager.AllFactions.FirstOrDefault(f => f.GetUniqueLoadID() == id))
                    .Where(f => f != null)
                    .ToList();

                if (factions.Count == 1)
                {
                    ownerString = "Domain of: " + factions[0].Name;
                }
                else if (factions.Count > 1)
                {
                    ownerString = "Contested Domain of:\n" + string.Join("\n", factions.Select(f => "- " + f.Name));
                }
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine($"Region: {province.name}");
            sb.AppendLine($"Biome: {province.primaryBiome?.LabelCap ?? "Unknown"}");
            sb.AppendLine($"Status: {ownerString}");
            sb.AppendLine($"Tiles: {province.tiles.Count}");
            sb.AppendLine();
            sb.AppendLine("--- Regional Economics ---");
            sb.AppendLine($"Population: {province.currentPopulation}");
            sb.AppendLine($"Housing Capacity: {province.totalDwellings * 2}");
            sb.AppendLine($"Nutrition: {province.rawNutrition:F0}");
            sb.AppendLine($"Biomass: {province.biomass:F0}");
            sb.AppendLine($"Minerals: {province.minerals:F0}");
            sb.AppendLine($"Textiles: {province.textiles:F0}");
            sb.AppendLine();
            sb.AppendLine("--- Produced Goods ---");
            sb.AppendLine($"Pre-Industrial Goods: {province.preIndustrialGoods:F0}");
            sb.AppendLine($"Industrial Goods: {province.industrialGoods:F0}");
            sb.AppendLine($"Spacer Goods: {province.spacerGoods:F0}");

            if (province.activeCrises.Any())
            {
                sb.AppendLine();
                sb.AppendLine("--- Active Crises ---");
                foreach (var crisis in province.activeCrises)
                {
                    sb.AppendLine($"- {crisis.crisisType} (Severity: {crisis.currentSeverity:P0})");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private Color GetUniqueColor(int id)
        {
            UnityEngine.Random.State state = UnityEngine.Random.state;
            UnityEngine.Random.InitState(id * 1337 + 42);
            float h = UnityEngine.Random.value;
            float s = UnityEngine.Random.Range(0.4f, 0.7f);
            float v = UnityEngine.Random.Range(0.7f, 0.9f);
            UnityEngine.Random.state = state;

            Color c = Color.HSVToRGB(h, s, v);
            return new Color(c.r, c.g, c.b, 0.35f);
        }
    }
}
