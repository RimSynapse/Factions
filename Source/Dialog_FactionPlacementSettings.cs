using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimSynapse.Factions
{
    public class Dialog_FactionPlacementSettings : Window
    {
        private Vector2 scrollPosition = Vector2.zero;
        private List<FactionDef> activeFactions;

        public override Vector2 InitialSize => new Vector2(800f, 650f);

        public Dialog_FactionPlacementSettings()
        {
            this.closeOnClickedOutside = true;
            this.absorbInputAroundWindow = true;

            activeFactions = DefDatabase<FactionDef>.AllDefs
                .Where(f => !f.isPlayer && !f.hidden)
                .OrderBy(f => f.defName)
                .ToList();
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 35f), "RimSynapse Factions - Geographic Placement Settings");
            Text.Font = GameFont.Small;

            // Retrieve current planet coverage from Page_CreateWorldParams if open
            float coverage = 0.3f;
            var page = Find.WindowStack.WindowOfType<Page_CreateWorldParams>();
            if (page != null)
            {
                var field = typeof(Page_CreateWorldParams).GetField("planetCoverage", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    coverage = (float)field.GetValue(page);
                }
            }

            int totalTiles = Mathf.RoundToInt(100000f * coverage);
            int landTiles = Mathf.RoundToInt(totalTiles * 0.38f); // ~38% of tiles are land on average

            float avgSize = (FactionPlacementSettings.minRegionSize + FactionPlacementSettings.maxRegionSize) / 2f;
            int estIdeal = Mathf.RoundToInt(landTiles / avgSize);

            // Due to biome fragmentation, the actual count is ~1.4x to 2.0x of the ideal
            int estMin = Mathf.RoundToInt(estIdeal * 1.4f);
            int estMax = Mathf.RoundToInt(estIdeal * 2.0f);

            if (estMin < 1) estMin = 1;
            if (estMax < 1) estMax = 1;

            // Global Map Region Parameters Panel
            Rect globalBoxRect = new Rect(0f, 40f, inRect.width - 15f, 110f);
            Widgets.DrawMenuSection(globalBoxRect);

            Rect globalTitleRect = new Rect(10f, 44f, 300f, 22f);
            Widgets.Label(globalTitleRect, "<b>Global Map Region Parameters</b>");

            // Left Column (Min size)
            float colWidth = (globalBoxRect.width - 30f) / 2f;
            Rect minLabelRect = new Rect(10f, 68f, 150f, 22f);
            Widgets.Label(minLabelRect, $"Min Size: {FactionPlacementSettings.minRegionSize} tiles");
            Rect minSliderRect = new Rect(165f, 70f, colWidth - 170f, 18f);
            float tempMin = Widgets.HorizontalSlider(minSliderRect, FactionPlacementSettings.minRegionSize, 20f, 150f, false, null, null, null, 1f);
            FactionPlacementSettings.minRegionSize = Mathf.RoundToInt(tempMin);

            // Right Column (Max size)
            float rightColStart = 10f + colWidth + 10f;
            Rect maxLabelRect = new Rect(rightColStart, 68f, 150f, 22f);
            Widgets.Label(maxLabelRect, $"Max Size: {FactionPlacementSettings.maxRegionSize} tiles");
            Rect maxSliderRect = new Rect(rightColStart + 165f, 70f, colWidth - 170f, 18f);
            float tempMax = Widgets.HorizontalSlider(maxSliderRect, FactionPlacementSettings.maxRegionSize, 50f, 400f, false, null, null, null, 1f);
            FactionPlacementSettings.maxRegionSize = Mathf.RoundToInt(tempMax);

            // Estimates row
            Rect estRect = new Rect(10f, 120f, globalBoxRect.width - 20f, 22f);
            Widgets.Label(estRect, $"Estimated Land Tiles: <color=cyan>{landTiles}</color> (at {Mathf.RoundToInt(coverage * 100f)}% coverage) | Expected Region Count: <color=green>{estMin} - {estMax}</color> (Avg Size: {avgSize:F0} tiles)");

            Rect outRect = new Rect(0f, 160f, inRect.width, inRect.height - 215f);
            Rect viewRect = new Rect(0f, 0f, inRect.width - 25f, activeFactions.Count * 265f);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            float curY = 0f;

            foreach (var def in activeFactions)
            {
                var profile = FactionPlacementSettings.GetProfile(def);

                Rect boxRect = new Rect(0f, curY, viewRect.width, 255f);
                Widgets.DrawMenuSection(boxRect);

                Rect titleRect = new Rect(10f, curY + 10f, boxRect.width - 20f, 25f);
                Widgets.Label(titleRect, $"<b>{def.LabelCap} ({def.defName}) - Tech: {def.techLevel}</b>");

                Rect resetRect = new Rect(boxRect.width - 120f, curY + 8f, 100f, 22f);
                if (Widgets.ButtonText(resetRect, "Reset Default"))
                {
                    var defaultProfile = FactionPlacementSettings.GetDefaultProfile(def);
                    profile.mineralWeight = defaultProfile.mineralWeight;
                    profile.nutritionWeight = defaultProfile.nutritionWeight;
                    profile.forageWeight = defaultProfile.forageWeight;
                    profile.grazingWeight = defaultProfile.grazingWeight;
                    profile.huntingWeight = defaultProfile.huntingWeight;
                    profile.marginWeight = defaultProfile.marginWeight;
                    profile.baseCountRange = defaultProfile.baseCountRange;
                    profile.placementOrder = defaultProfile.placementOrder;
                }

                // Left column sliders
                float leftY = curY + 40f;
                DrawWeightSlider(ref leftY, boxRect.width / 2f - 15f, 10f, "Mineral (Mountains/Hills)", ref profile.mineralWeight, 0f, 5f);
                DrawWeightSlider(ref leftY, boxRect.width / 2f - 15f, 10f, "Nutrition (Agricultural Plains)", ref profile.nutritionWeight, 0f, 5f);
                DrawWeightSlider(ref leftY, boxRect.width / 2f - 15f, 10f, "Forage (Neolithic Foraging)", ref profile.forageWeight, 0f, 5f);

                // Right column sliders
                float rightY = curY + 40f;
                DrawWeightSlider(ref rightY, boxRect.width / 2f - 15f, boxRect.width / 2f + 5f, "Grazing (Open Grasslands)", ref profile.grazingWeight, 0f, 5f);
                DrawWeightSlider(ref rightY, boxRect.width / 2f - 15f, boxRect.width / 2f + 5f, "Hunting (Forests/Wilds)", ref profile.huntingWeight, 0f, 5f);
                DrawWeightSlider(ref rightY, boxRect.width / 2f - 15f, boxRect.width / 2f + 5f, "Margin (Desert/Tundra Edges)", ref profile.marginWeight, 0f, 5f);

                // Bases counts
                Rect basesRect = new Rect(10f, curY + 180f, boxRect.width - 20f, 24f);
                Widgets.Label(new Rect(basesRect.x, basesRect.y, 250f, 24f), $"Settlement Range: {profile.baseCountRange.min} - {profile.baseCountRange.max}");
                Widgets.IntRange(new Rect(basesRect.x + 260f, basesRect.y, basesRect.width - 270f, 24f), def.GetHashCode(), ref profile.baseCountRange, 1, 50, null, 0);

                // Placement Order
                Rect orderRect = new Rect(10f, curY + 215f, boxRect.width - 20f, 24f);
                Widgets.Label(new Rect(orderRect.x, orderRect.y, 250f, 24f), $"Placement Turn Order (Priority): {profile.placementOrder}");
                float tempOrder = Widgets.HorizontalSlider(new Rect(orderRect.x + 260f, orderRect.y, orderRect.width - 270f, 18f), (float)profile.placementOrder, 1f, 10f, false, null, null, null, 1f);
                profile.placementOrder = Mathf.RoundToInt(tempOrder);

                curY += 265f;
            }

            Widgets.EndScrollView();

            Rect closeButtonRect = new Rect(inRect.width / 2f - 75f, inRect.height - 45f, 150f, 35f);
            if (Widgets.ButtonText(closeButtonRect, "Close"))
            {
                this.Close();
            }
        }

        private void DrawWeightSlider(ref float y, float width, float startX, string label, ref float value, float min, float max)
        {
            Rect labelRect = new Rect(startX, y, width, 22f);
            Widgets.Label(labelRect, $"{label}: {value:F2}");
            y += 20f;

            Rect sliderRect = new Rect(startX, y, width, 18f);
            value = Widgets.HorizontalSlider(sliderRect, value, min, max, false, null, null, null, -1f);
            y += 25f;
        }
    }
}
