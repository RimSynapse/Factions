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
    public class MapMode_FactionTerritory : MapMode_Region
    {
        public override Material RegionMaterial => BaseContent.ClearMat;

        private static Dictionary<string, Texture2D> stripedTextureCache = new Dictionary<string, Texture2D>();

        public MapMode_FactionTerritory() { }
        public MapMode_FactionTerritory(MapModeDef def) : base(def) { }

        public override void SetRegions()
        {
            if (!UnityData.IsInMainThread) return;

            ClearCache();
            regions.Clear();

            if (Find.World == null) return;
            var regionManager = Find.World.GetComponent<SynapseRegionManager>();
            if (regionManager == null) return;

            regionManager.RecalculateProvinceOwners();

            foreach (var province in regionManager.Provinces)
            {
                if (province.tiles.Count == 0 || !province.owningFactionIds.Any()) continue;

                var factions = province.owningFactionIds
                    .Select(id => Find.FactionManager.AllFactions.FirstOrDefault(f => f.GetUniqueLoadID() == id))
                    .Where(f => f != null)
                    .ToList();

                if (factions.Count == 0) continue;

                Material bodyMat = null;
                Material borderMat = null;
                Color borderColor = Color.white;
                string ownerString;

                if (factions.Count == 1)
                {
                    Faction faction = factions[0];
                    Color baseColor = faction.Color;
                    Color bodyColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.35f);
                    borderColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.85f);

                    if (ShaderDatabase.MetaOverlay != null && BaseContent.WhiteTex != null)
                    {
                        bodyMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, bodyColor, 3510);
                    }
                    if (bodyMat == null)
                    {
                        bodyMat = SolidColorMaterials.SimpleSolidColorMaterial(bodyColor);
                    }

                    string goodwillStr = faction.IsPlayer ? "N/A (Player)" : $"{faction.PlayerGoodwill.ToStringWithSign()} ({faction.PlayerRelationKind.GetLabel()})";
                    ownerString = "Territory: " + faction.Name + "\nFaction type: " + faction.def.LabelCap + $"\nGoodwill: {goodwillStr}";
                }
                else
                {
                    Faction f1 = factions[0];
                    Faction f2 = factions[1];
                    string cacheKey = $"{f1.GetUniqueLoadID()}||{f2.GetUniqueLoadID()}";

                    Texture2D stripedTex = GetOrCreateStripedTexture(f1.Color, f2.Color, cacheKey);
                    
                    if (ShaderDatabase.MetaOverlay != null && stripedTex != null)
                    {
                        bodyMat = MaterialPool.MatFrom(stripedTex, ShaderDatabase.MetaOverlay, Color.white, 3510);
                    }
                    if (bodyMat == null)
                    {
                        bodyMat = BaseContent.WhiteMat;
                    }

                    borderColor = new Color(0.7f, 0.7f, 0.7f, 0.9f);
                    ownerString = "Contested Territory\nClaimed by:\n" + string.Join("\n", factions.Select(f => $"- {f.Name} ({f.def.LabelCap})"));
                }

                if (ShaderDatabase.MetaOverlay != null && BaseContent.WhiteTex != null)
                {
                    borderMat = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.MetaOverlay, borderColor, 3510);
                }
                if (borderMat == null)
                {
                    borderMat = SolidColorMaterials.SimpleSolidColorMaterial(borderColor);
                }

                float borderWidth = def?.RegionProperties?.borderWidth ?? 0.7f;
                bool doBorders = def?.RegionProperties?.doBorders ?? true;

                Region region = new Region(
                    province.name,
                    province.tiles,
                    false,
                    bodyMat,
                    doBorders,
                    borderMat,
                    borderWidth,
                    tooltip: $"Province: {province.name}\nType: {province.provinceType}\nBiome: {province.primaryBiome?.LabelCap ?? "Unknown"}\n{ownerString}"
                );

                regions.Add(region);
            }
        }

        private static Texture2D GetOrCreateStripedTexture(Color color1, Color color2, string cacheKey)
        {
            if (stripedTextureCache.TryGetValue(cacheKey, out var cachedTex) && cachedTex != null)
            {
                return cachedTex;
            }

            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Point;

            Color c1 = new Color(color1.r, color1.g, color1.b, 0.35f);
            Color c2 = new Color(color2.r, color2.g, color2.b, 0.35f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool isStripe1 = ((x + y) / 4) % 2 == 0;
                    tex.SetPixel(x, y, isStripe1 ? c1 : c2);
                }
            }
            tex.Apply();
            stripedTextureCache[cacheKey] = tex;
            return tex;
        }

        public override string GetTooltip(int tile)
        {
            if (Find.World == null) return base.GetTooltip(tile);
            var regionManager = Find.World.GetComponent<SynapseRegionManager>();
            if (regionManager == null) return base.GetTooltip(tile);

            var province = regionManager.GetProvinceForTile(tile);
            if (province == null) return base.GetTooltip(tile);

            return MapMode_GeographicProvinces.GetProvinceTooltip(province, tile);
        }
    }
}
