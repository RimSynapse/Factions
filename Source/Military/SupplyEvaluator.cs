using System.Collections.Generic;
using RimSynapse.RegionsAndTerritories.Placement;

namespace RimSynapse.Factions.Military
{
    /// <summary>
    /// Epic 5 child 2 — military reach as a function of contiguous owned territory.
    ///
    /// Pure: no <c>Find</c>, no Harmony, no Unity. Everything arrives through
    /// <see cref="SupplyNetwork"/>.
    ///
    /// <para>The shape of the model is a shortest path over the province graph where the cost of
    /// entering a province is what it costs to run supply through it: cheap through ground you
    /// hold, dearer through ground you are still fighting over, impossible through ground that is
    /// somebody else's or nobody's. The target province is exempt from that requirement, because
    /// the target is what you are attacking — demanding that you already hold it would make every
    /// offensive illegal and every model a tautology.</para>
    ///
    /// <para><b>This is a relaxation, not a restriction.</b> The rule it replaces refuses anything
    /// whose target is not the source province or one of its neighbours. That move costs 1 here and
    /// is always inside <see cref="SupplyRules.MaxSupplyCost"/> regardless of who owns what, so
    /// every move that was legal before is still legal, with attrition of exactly zero. What is new
    /// is the deep strike along a held corridor, which is reach a faction earns by holding the map
    /// rather than by owning a settlement close enough.</para>
    /// </summary>
    public static class SupplyEvaluator
    {
        public static SupplyLine Evaluate(SupplyNetwork network, int sourceProvinceId, int targetProvinceId, object faction)
        {
            // No network, no geography, no faction: no opinion. A military hook that refuses an
            // action because it could not find the world is far worse than one that stands aside.
            if (network == null || faction == null) return SupplyLine.Unrestricted();
            if (sourceProvinceId < 0 || targetProvinceId < 0) return SupplyLine.Unrestricted();

            if (sourceProvinceId == targetProvinceId)
            {
                return SupplyLine.Supplied(0, new List<int> { sourceProvinceId });
            }

            var best = new Dictionary<int, int> { { sourceProvinceId, 0 } };
            var cameFrom = new Dictionary<int, int>();
            var frontier = new List<int> { sourceProvinceId };

            // Set when an improving expansion was dropped purely for exceeding the cost ceiling. If
            // that never happens, the search exhausted the entire corridor reachable from the
            // source, so a target still missing from it is missing for want of ground rather than
            // for want of range — and those two failures want different advice.
            //
            // The flag is exact in one direction and approximate in the other: no truncation
            // guarantees NoCorridor is correct, while truncation only proves the owned corridor ran
            // past the horizon *somewhere*, not necessarily toward the target. A faction whose
            // territory sprawls east while it strikes west can therefore be told it is out of range
            // when it is really out of ground. Both messages point at the same remedy — hold more of
            // the map — and the alternative is an unbounded search of the whole province graph on
            // every military action to sharpen a sentence.
            bool truncatedByRange = false;

            while (frontier.Count > 0)
            {
                // Cheapest-first over a handful of provinces with costs in [1, 6]. A linear scan is
                // the right structure at this size; a priority queue here would be more code and
                // more allocation to save nothing measurable.
                int index = 0;
                for (int i = 1; i < frontier.Count; i++)
                {
                    if (best[frontier[i]] < best[frontier[index]]) index = i;
                }

                int current = frontier[index];
                frontier.RemoveAt(index);
                int currentCost = best[current];

                foreach (int neighbour in network.NeighboursOf(current))
                {
                    if (neighbour < 0 || neighbour == sourceProvinceId) continue;

                    int step = StepCost(network, neighbour, targetProvinceId, faction);
                    if (step == SupplyRules.Impassable) continue;

                    int cost = currentCost + step;

                    // Order matters: a step we already have a cheaper route for is no evidence of
                    // anything, so it must be discarded before it can raise the range flag.
                    int existing;
                    if (best.TryGetValue(neighbour, out existing) && existing <= cost) continue;

                    if (cost > SupplyRules.MaxSupplyCost)
                    {
                        truncatedByRange = true;
                        continue;
                    }

                    best[neighbour] = cost;
                    cameFrom[neighbour] = current;

                    if (neighbour == targetProvinceId) continue; // never route supply through the objective
                    frontier.Add(neighbour);
                }
            }

            int reached;
            if (best.TryGetValue(targetProvinceId, out reached))
            {
                return SupplyLine.Supplied(reached, ReconstructPath(cameFrom, sourceProvinceId, targetProvinceId));
            }

            if (truncatedByRange)
            {
                return SupplyLine.Cut(SupplyStatus.OutOfRange,
                    "Cannot launch military operation: the target region is beyond your supply range. "
                        + "Your line may run at most " + SupplyRules.MaxSupplyCost
                        + " regions through territory you hold.");
            }

            return SupplyLine.Cut(SupplyStatus.NoCorridor,
                "Cannot launch military operation: no supply line reaches the target region. "
                    + "Take the regions in between first — military reach follows territory you hold.");
        }

        /// <summary>Convenience overload for call sites that only want a yes/no and a message.</summary>
        public static bool CanReach(SupplyNetwork network, int sourceProvinceId, int targetProvinceId, object faction, out string reason)
        {
            SupplyLine line = Evaluate(network, sourceProvinceId, targetProvinceId, faction);
            reason = line.Reason ?? string.Empty;
            return line.Reachable;
        }

        /// <summary>
        /// What it costs to enter a province on the way to the objective.
        ///
        /// The objective itself always costs <see cref="SupplyRules.HeldTransitCost"/> whoever holds
        /// it. That single exemption is what keeps the adjacent strike — the only move the pre-0.7
        /// rule allowed — costing exactly 1 and therefore always legal.
        /// </summary>
        private static int StepCost(SupplyNetwork network, int provinceId, int targetProvinceId, object faction)
        {
            if (provinceId == targetProvinceId) return SupplyRules.HeldTransitCost;

            ProvinceControl control = network.Control(provinceId, faction);
            return SupplyRules.TransitCost(control);
        }

        private static List<int> ReconstructPath(Dictionary<int, int> cameFrom, int source, int target)
        {
            var path = new List<int>();
            int current = target;

            // Bounded rather than trusting the map: a malformed predecessor chain should produce a
            // short path, not hang the game on the world map.
            for (int guard = 0; guard <= SupplyRules.MaxSupplyCost + 1; guard++)
            {
                path.Add(current);
                if (current == source) break;

                int previous;
                if (!cameFrom.TryGetValue(current, out previous)) break;
                current = previous;
            }

            path.Reverse();
            return path;
        }
    }
}
