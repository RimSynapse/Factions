using System;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld;
using RimSynapse; // for SynapseCoreWorldComponent

namespace RimSynapse.Factions.UI
{
    public class Dialog_FactionPerceptions : Window
    {
        private Vector2 scrollPosition;
        private const float RowHeight = 35f;

        public override Vector2 InitialSize => new Vector2(900f, 600f);

        public Dialog_FactionPerceptions()
        {
            this.doCloseX = true;
            this.forcePause = true;
            this.absorbInputAroundWindow = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0, 0, inRect.width, 40f), "Faction Perceptions (Debug)");
            Text.Font = GameFont.Small;

            var coreComp = Find.World?.GetComponent<SynapseCoreWorldComponent>();
            if (coreComp == null)
            {
                Widgets.Label(new Rect(0, 50f, inRect.width, 30f), "Core World Component not found.");
                return;
            }

            Rect outRect = new Rect(0, 50f, inRect.width, inRect.height - 50f);
            var visibleFactions = Find.FactionManager.AllFactionsVisible.Where(f => !f.IsPlayer && !f.Hidden).ToList();
            Rect viewRect = new Rect(0, 0, outRect.width - 16f, visibleFactions.Count * RowHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);

            float y = 0f;
            foreach (var faction in visibleFactions)
            {
                var tracker = coreComp.factionTrackers.Find(f => f.factionId == faction.GetUniqueLoadID());
                float perceivedWealth = tracker?.perceivedWealth ?? 0f;
                float perceivedStrength = tracker?.perceivedStrength ?? 0f;
                
                float normalizedStrength = (perceivedStrength * 50f) + 1f;
                float greedRatio = perceivedWealth / normalizedStrength;

                string relationStr = faction.PlayerRelationKind.ToString();
                string isHostile = faction.HostileTo(Faction.OfPlayer) ? "(Hostile)" : "";
                
                GUI.color = faction.HostileTo(Faction.OfPlayer) ? Color.red : Color.green;
                if (!faction.HostileTo(Faction.OfPlayer) && faction.PlayerGoodwill < 75) GUI.color = Color.yellow;

                string text = $"{faction.Name} {isHostile} | Rel: {relationStr} ({faction.PlayerGoodwill}) | P.Wealth: {perceivedWealth:F0} | P.Strength: {perceivedStrength:F0} | Greed Ratio: {greedRatio:F2}";

                Rect rowRect = new Rect(0, y, viewRect.width, RowHeight);
                if (y / RowHeight % 2 == 0)
                {
                    Widgets.DrawHighlight(rowRect);
                }

                Widgets.Label(new Rect(5f, y + 5f, viewRect.width - 10f, RowHeight - 5f), text);
                
                // Add a button to manually force a test +10000 wealth perception
                Rect btnRect = new Rect(viewRect.width - 150f, y + 2f, 140f, RowHeight - 4f);
                GUI.color = Color.white;
                if (Widgets.ButtonText(btnRect, "Add 10k P.Wealth"))
                {
                    if (tracker != null) tracker.perceivedWealth += 10000;
                }

                y += RowHeight;
            }

            Widgets.EndScrollView();
            GUI.color = Color.white;
        }
    }
}
