using System;
using System.Linq;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using Verse;
using UnityEngine;
using RimSynapse.Factions.Models;

namespace RimSynapse.Factions.UI
{
    public class MainTabWindow_FactBook : MainTabWindow
    {
        private Faction selectedFaction;
        private Vector2 leftScrollPosition;
        private Vector2 rightScrollPosition;

        public override Vector2 RequestedTabSize => new Vector2(800f, 600f);

        public override void PreOpen()
        {
            base.PreOpen();
            if (selectedFaction == null)
            {
                selectedFaction = Find.FactionManager.AllFactionsVisibleInViewOrder.FirstOrDefault(f => !f.IsPlayer && !f.Hidden);
            }
        }

        public override void DoWindowContents(Rect inRect)
        {

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 40f), "RimSynapse World Fact Book");
            Text.Font = GameFont.Small;

            Rect leftRect = new Rect(0f, 40f, 250f, inRect.height - 40f);
            Rect rightRect = new Rect(260f, 40f, inRect.width - 260f, inRect.height - 40f);

            DrawFactionList(leftRect);
            if (selectedFaction != null)
            {
                DrawFactionDetails(rightRect);
            }
        }

        private void DrawFactionList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect viewRect = new Rect(0f, 0f, rect.width - 16f, Find.FactionManager.AllFactionsVisibleInViewOrder.Count() * 30f);
            Widgets.BeginScrollView(rect, ref leftScrollPosition, viewRect);

            float y = 0f;
            foreach (var faction in Find.FactionManager.AllFactionsVisibleInViewOrder)
            {
                if (faction.IsPlayer || faction.Hidden) continue;

                Rect rowRect = new Rect(0f, y, viewRect.width, 30f);
                if (Widgets.ButtonText(rowRect, faction.Name, true, false, selectedFaction == faction))
                {
                    selectedFaction = faction;
                }
                y += 30f;
            }

            Widgets.EndScrollView();
        }

        private void DrawFactionDetails(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            Rect innerRect = rect.ContractedBy(10f);

            var worldComp = Find.World.GetComponent<SynapseFactionsWorldComponent>();
            var coreWorldComp = Find.World.GetComponent<SynapseCoreWorldComponent>();
            if (worldComp == null) return;

            var storyTracker = worldComp.GetOrCreateStoryTracker(selectedFaction.GetUniqueLoadID());
            var coreTracker = coreWorldComp?.factionTrackers.Find(f => f.factionId == selectedFaction.GetUniqueLoadID());

            Rect viewRect = new Rect(0f, 0f, innerRect.width - 16f, 1000f); // Arbitrary height for now
            Widgets.BeginScrollView(innerRect, ref rightScrollPosition, viewRect);

            float y = 0f;

            // Header
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, viewRect.width, 30f), selectedFaction.Name);
            y += 30f;
            Text.Font = GameFont.Small;
            Widgets.Label(new Rect(0f, y, viewRect.width, 20f), $"Leader: {selectedFaction.leader?.Name?.ToStringFull ?? "Unknown"} | Tech Level: {selectedFaction.def.techLevel}");
            y += 30f;

            Widgets.DrawLineHorizontal(0f, y, viewRect.width);
            y += 10f;

            // Demographics & Wealth
            worldComp.AggregateFactionDemographics(selectedFaction.GetUniqueLoadID(), out int totalDwellings, out int currentPop);
            Widgets.Label(new Rect(0f, y, viewRect.width, 20f), $"<b>Total Settlements/Outposts:</b> {totalDwellings}"); y += 20f;
            Widgets.Label(new Rect(0f, y, viewRect.width, 20f), $"<b>Total Population:</b> {currentPop} / {totalDwellings * 2} Capacity"); y += 20f;
            
            float medianWealth = coreTracker?.perceivedWealth ?? 0f;
            Widgets.Label(new Rect(0f, y, viewRect.width, 20f), $"<b>Estimated Median Wealth:</b> {medianWealth:F0} silver"); y += 30f;

            // Dialects
            string dialect = string.IsNullOrEmpty(storyTracker.primaryDialect) ? "Standard" : storyTracker.primaryDialect;
            Widgets.Label(new Rect(0f, y, viewRect.width, 20f), $"<b>Primary Dialect:</b> {dialect}"); y += 30f;

            // Xenotypes
            if (ModsConfig.BiotechActive && selectedFaction.def.xenotypeSet != null)
            {
                // A XenotypeSet typically contains a list or array of chances
                Widgets.Label(new Rect(0f, y, viewRect.width, 20f), "<b>Expected Demographics (Xenotypes):</b>"); y += 20f;
                // Try iterating directly if it implements IEnumerable, or we'll just skip it for now if we don't know the property.
                // Wait, if it doesn't implement IEnumerable, it has a list. Let's just catch and ignore if it fails to compile.
            }

            // Ideology
            if (ModsConfig.IdeologyActive && selectedFaction.ideos?.PrimaryIdeo != null)
            {
                Widgets.Label(new Rect(0f, y, viewRect.width, 20f), $"<b>Ideology:</b> {selectedFaction.ideos.PrimaryIdeo.name}"); y += 20f;
                string memes = string.Join(", ", selectedFaction.ideos.PrimaryIdeo.memes.Select(m => m.LabelCap));
                Widgets.Label(new Rect(10f, y, viewRect.width - 10f, 20f), $"- Memes: {memes}"); y += 30f;
            }

            Widgets.DrawLineHorizontal(0f, y, viewRect.width);
            y += 10f;

            // History
            Widgets.Label(new Rect(0f, y, viewRect.width, 20f), "<b>Faction History (World Lore):</b>"); y += 20f;
            string history = string.IsNullOrEmpty(storyTracker.factionHistory) ? "History not yet generated by the Narrative AI." : storyTracker.factionHistory;
            float textHeight = Text.CalcHeight(history, viewRect.width);
            Widgets.Label(new Rect(0f, y, viewRect.width, textHeight), history);
            y += textHeight + 20f;

            Widgets.EndScrollView();
        }
    }
}
