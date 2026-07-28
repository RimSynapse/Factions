// Behaviour tests for Epic 5 child 2 — military reach as a function of contiguous owned territory.
//
// SupplyEvaluator is pure by design, so this suite needs no RimWorld at all: provinces are integers,
// the map is an adjacency dictionary drawn by hand, and factions are plain strings.
//
// Three things are worth pinning here:
//
//   * That this model is a relaxation. R&T has refused every non-adjacent military action since 0.6,
//     and a supply model that made an adjacent strike illegal — for a young faction holding nothing,
//     say — would be a regression dressed up as a feature. The adjacent strike must stay legal, at
//     zero attrition, from a faction with no territory at all.
//
//   * That reach is bought with ground rather than granted. The whole content of "contiguous owned
//     territory" is that a corridor of held provinces extends reach and a corridor of neutral ones
//     does not, and the gap between those two cases is the model.
//
//   * That the objective is not part of the corridor. Requiring the target to be held would make
//     every offensive illegal, which is the obvious way to get a supply model exactly backwards.
using System;
using System.Collections.Generic;
using RimSynapse.Factions.Military;
using RimSynapse.RegionsAndTerritories.Placement;

namespace MilitaryTests
{
    public static class Program
    {
        private static int failures;

        private const string Player = "player";
        private const string Rival = "rival";

        public static int Main()
        {
            Section("a world nobody has mapped is left alone");

            Check("no network at all is unrestricted, not refused",
                SupplyEvaluator.Evaluate(null, 0, 5, Player).Status == SupplyStatus.Unrestricted);
            Check("a source province nobody recognises is unrestricted",
                SupplyEvaluator.Evaluate(Chain(4), -1, 3, Player).Status == SupplyStatus.Unrestricted);
            Check("a target province nobody recognises is unrestricted",
                SupplyEvaluator.Evaluate(Chain(4), 0, -1, Player).Status == SupplyStatus.Unrestricted);
            Check("a faction nobody recognises is unrestricted",
                SupplyEvaluator.Evaluate(Chain(4), 0, 3, null).Status == SupplyStatus.Unrestricted);
            Check("and unrestricted still means allowed",
                SupplyEvaluator.Evaluate(null, 0, 5, Player).Reachable);

            Section("the move 0.6 allowed is the move 0.7 allows");

            // A faction holding nothing anywhere. Under the pre-0.7 rule it could strike the province
            // next door and nothing else; that must be exactly what it can still do.
            SupplyNetwork bare = Chain(6);

            Check("the province next door is in reach with no territory at all",
                SupplyEvaluator.Evaluate(bare, 0, 1, Player).Reachable);
            Check("and costs exactly one",
                SupplyEvaluator.Evaluate(bare, 0, 1, Player).Cost == 1);
            Check("so it arrives at full strength",
                Near(SupplyEvaluator.Evaluate(bare, 0, 1, Player).Effectiveness, 1f));
            Check("acting inside your own province is free",
                SupplyEvaluator.Evaluate(bare, 2, 2, Player).Cost == 0);
            Check("two provinces away with no corridor is refused, as it always was",
                !SupplyEvaluator.Evaluate(bare, 0, 2, Player).Reachable);
            Check("and the refusal says the ground is missing, not the range",
                SupplyEvaluator.Evaluate(bare, 0, 2, Player).Status == SupplyStatus.NoCorridor);

            Section("holding the ground between is what buys reach");

            // 0 - 1 - 2 - 3, with the two middle provinces held.
            SupplyNetwork corridor = Chain(4, Held(Player, 1, 2));

            Check("a held corridor carries a strike three provinces deep",
                SupplyEvaluator.Evaluate(corridor, 0, 3, Player).Reachable);
            Check("at a cost of one per province crossed",
                SupplyEvaluator.Evaluate(corridor, 0, 3, Player).Cost == 3);
            Check("the same corridor is no use to the faction that does not hold it",
                !SupplyEvaluator.Evaluate(corridor, 0, 3, Rival).Reachable);
            Check("half a corridor is no corridor",
                !SupplyEvaluator.Evaluate(Chain(4, Held(Player, 1)), 0, 3, Player).Reachable);
            Check("and a line held by somebody else does not carry yours",
                !SupplyEvaluator.Evaluate(Chain(4, Held(Rival, 1, 2)), 0, 3, Player).Reachable);

            Section("contested ground carries supply, but dearly");

            SupplyNetwork fought = Chain(4, Held(Player, 1), Contested(Player, 2));

            Check("a contested province still passes supply",
                SupplyEvaluator.Evaluate(fought, 0, 3, Player).Reachable);
            Check("but costs double, so the same line is longer",
                SupplyEvaluator.Evaluate(fought, 0, 3, Player).Cost
                    > SupplyEvaluator.Evaluate(corridor, 0, 3, Player).Cost);
            Check("and therefore lands with less of the force intact",
                SupplyEvaluator.Evaluate(fought, 0, 3, Player).Effectiveness
                    < SupplyEvaluator.Evaluate(corridor, 0, 3, Player).Effectiveness);

            // The same six provinces of ground, held or fought over, are the difference between a
            // line that arrives and one that does not.
            Check("six held provinces carry a strike to the far end",
                SupplyEvaluator.Evaluate(Chain(7, Held(Player, 1, 2, 3, 4, 5)), 0, 6, Player).Reachable);
            Check("the same six contested do not",
                !SupplyEvaluator.Evaluate(Chain(7, Contested(Player, 1, 2, 3, 4, 5)), 0, 6, Player).Reachable);
            Check("though a shorter contested line still gets through",
                SupplyEvaluator.Evaluate(Chain(7, Contested(Player, 1, 2)), 0, 3, Player).Cost == 5);

            Section("the objective is not part of the corridor");

            // The obvious way to write this model backwards is to require the target to be held too,
            // at which point no offensive is ever legal and the whole epic is a no-op.
            SupplyNetwork enemyHeld = Chain(4, Held(Player, 1, 2), Foreign(Player, 3));

            Check("a province the enemy holds can still be struck",
                SupplyEvaluator.Evaluate(enemyHeld, 0, 3, Player).Reachable);
            Check("an adjacent enemy province is in reach of a faction with nothing",
                SupplyEvaluator.Evaluate(Chain(3, Foreign(Player, 1)), 0, 1, Player).Reachable);
            Check("and unclaimed ground is a target even though it is not a road",
                SupplyEvaluator.Evaluate(bare, 3, 4, Player).Reachable);

            Section("the cheapest line is the one you get");

            // A diamond: 0 to 4 the short way through 1, or the long way through 2 and 3.
            var diamond = new Dictionary<int, int[]>
            {
                { 0, new[] { 1, 2 } },
                { 1, new[] { 0, 4 } },
                { 2, new[] { 0, 3 } },
                { 3, new[] { 2, 4 } },
                { 4, new[] { 1, 3 } }
            };
            SupplyNetwork branching = Network(diamond, Held(Player, 1, 2, 3));

            SupplyLine chosen = SupplyEvaluator.Evaluate(branching, 0, 4, Player);
            Check("the short branch is taken when both are held", chosen.Cost == 2);
            Check("the path starts at the source", chosen.Path.Count > 0 && chosen.Path[0] == 0);
            Check("the path ends at the target", chosen.Path[chosen.Path.Count - 1] == 4);
            Check("the path names the province the supply actually crossed", chosen.Path.Contains(1));
            Check("the path is as long as the cost says it is", chosen.Path.Count == chosen.Cost + 1);

            // Break the short branch and the long one must be found rather than the search giving up.
            SupplyNetwork detour = Network(diamond, Held(Player, 2, 3));
            Check("a broken branch reroutes rather than refusing",
                SupplyEvaluator.Evaluate(detour, 0, 4, Player).Reachable);
            Check("and the detour costs what the detour costs",
                SupplyEvaluator.Evaluate(detour, 0, 4, Player).Cost == 3);

            Section("attrition is earned by distance, not by geography");

            Check("nothing is lost inside the unsupplied reach",
                Near(SupplyRules.Effectiveness(SupplyRules.UnsuppliedReach), 1f));
            Check("nor at zero cost", Near(SupplyRules.Effectiveness(0), 1f));
            Check("the longest legal line is the weakest one",
                SupplyRules.Effectiveness(SupplyRules.MaxSupplyCost) < SupplyRules.Effectiveness(2));
            Check("effectiveness falls with every extra province",
                SupplyRules.Effectiveness(2) > SupplyRules.Effectiveness(3)
                && SupplyRules.Effectiveness(3) > SupplyRules.Effectiveness(4));
            Check("and never below the floor",
                SupplyRules.Effectiveness(SupplyRules.MaxSupplyCost) >= SupplyRules.MinEffectiveness);
            // Same shape as TaxationRules.MinCollectionFactor: a floor that exists so that raising
            // attrition later cannot silently produce an army arriving at nothing.
            Check("though today's constants never actually reach it",
                SupplyRules.Effectiveness(SupplyRules.MaxSupplyCost) > SupplyRules.MinEffectiveness);
            Check("a line past the ceiling would still be clamped, not negative",
                SupplyRules.Effectiveness(100) >= SupplyRules.MinEffectiveness);

            Section("an unreachable target says which kind of unreachable");

            // A corridor that genuinely runs there, and is genuinely too long.
            SupplyNetwork longChain = Chain(10, Held(Player, 1, 2, 3, 4, 5, 6, 7, 8));
            SupplyLine tooFar = SupplyEvaluator.Evaluate(longChain, 0, 9, Player);
            Check("a corridor past the horizon reads as out of range", tooFar.Status == SupplyStatus.OutOfRange);
            Check("and is refused", !tooFar.Reachable);
            Check("and the message talks about range", tooFar.Reason.Contains("supply range"));

            SupplyLine noGround = SupplyEvaluator.Evaluate(Chain(10), 0, 9, Player);
            Check("no corridor at all reads as no corridor", noGround.Status == SupplyStatus.NoCorridor);
            Check("and the message tells the player to take the ground",
                noGround.Reason.Contains("regions in between"));
            Check("an unreachable line reports no cost rather than a misleading one", noGround.Cost == -1);

            Check("exactly at the supply ceiling is still reachable",
                SupplyEvaluator.Evaluate(Chain(10, Held(Player, 1, 2, 3, 4, 5, 6, 7, 8)), 0, 6, Player).Cost
                    == SupplyRules.MaxSupplyCost);
            Check("one province past it is not",
                !SupplyEvaluator.Evaluate(Chain(10, Held(Player, 1, 2, 3, 4, 5, 6, 7, 8)), 0, 7, Player).Reachable);

            Section("an island is not a supply problem you can solve by holding more");

            // Two disconnected components. The faction holds plenty, all of it on the wrong side.
            var split = new Dictionary<int, int[]>
            {
                { 0, new[] { 1 } },
                { 1, new[] { 0 } },
                { 8, new[] { 9 } },
                { 9, new[] { 8 } }
            };
            SupplyLine overseas = SupplyEvaluator.Evaluate(Network(split, Held(Player, 1, 8, 9)), 0, 9, Player);
            Check("a target on another landmass is out of reach", !overseas.Reachable);
            Check("and honestly so: there is no corridor, however much is held",
                overseas.Status == SupplyStatus.NoCorridor);
            Check("a province with no neighbours at all does not crash the search",
                !SupplyEvaluator.Evaluate(Network(new Dictionary<int, int[]> { { 0, new int[0] } }), 0, 1, Player).Reachable);

            Console.WriteLine();
            Console.WriteLine(failures == 0 ? "ALL MILITARY TESTS PASSED" : failures + " MILITARY TEST(S) FAILED");
            return failures == 0 ? 0 : 1;
        }

        // -- world building ---------------------------------------------------
        // A control entry is one faction's standing in one province. Anything unstated is Unclaimed,
        // which is the honest default: most of the map belongs to nobody the asker cares about.

        private sealed class Standing
        {
            public object faction;
            public int province;
            public ProvinceControl control;
        }

        private static Standing[] Held(object faction, params int[] provinces)
        {
            return Standings(faction, ProvinceControl.Held, provinces);
        }

        private static Standing[] Contested(object faction, params int[] provinces)
        {
            return Standings(faction, ProvinceControl.Contested, provinces);
        }

        private static Standing[] Foreign(object faction, params int[] provinces)
        {
            return Standings(faction, ProvinceControl.Foreign, provinces);
        }

        private static Standing[] Standings(object faction, ProvinceControl control, int[] provinces)
        {
            var result = new Standing[provinces.Length];
            for (int i = 0; i < provinces.Length; i++)
            {
                result[i] = new Standing { faction = faction, province = provinces[i], control = control };
            }
            return result;
        }

        /// <summary>A line of provinces, 0 to count-1, each bordering its neighbours.</summary>
        private static SupplyNetwork Chain(int count, params Standing[][] standings)
        {
            var edges = new Dictionary<int, int[]>();
            for (int i = 0; i < count; i++)
            {
                var neighbours = new List<int>();
                if (i > 0) neighbours.Add(i - 1);
                if (i < count - 1) neighbours.Add(i + 1);
                edges[i] = neighbours.ToArray();
            }
            return Network(edges, standings);
        }

        private static SupplyNetwork Network(Dictionary<int, int[]> edges, params Standing[][] standings)
        {
            var flat = new List<Standing>();
            foreach (Standing[] group in standings) flat.AddRange(group);

            return new SupplyNetwork
            {
                Neighbours = p => edges.ContainsKey(p) ? edges[p] : new int[0],
                ControlOf = (province, faction) =>
                {
                    foreach (Standing s in flat)
                    {
                        if (s.province == province && Equals(s.faction, faction)) return s.control;
                    }
                    return ProvinceControl.Unclaimed;
                }
            };
        }

        // -- harness ----------------------------------------------------------

        private static bool Near(float a, float b, float tolerance = 0.001f)
        {
            return Math.Abs(a - b) < tolerance;
        }

        private static void Section(string name)
        {
            Console.WriteLine();
            Console.WriteLine("-- " + name);
        }

        private static void Check(string label, bool ok)
        {
            if (!ok) failures++;
            Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + label);
        }
    }
}
