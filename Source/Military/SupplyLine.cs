using System.Collections.Generic;

namespace RimSynapse.Factions.Military
{
    /// <summary>Why a military action was or was not within supply.</summary>
    public enum SupplyStatus
    {
        /// <summary>
        /// Governance is off, or the world has no province data to reason about. Distinct from
        /// <see cref="Reachable"/> on purpose: "we allowed it" and "we had no opinion" are different
        /// facts, and a UI that reports a supply line where none was computed is lying.
        /// </summary>
        Unrestricted,

        /// <summary>A line exists and is within <c>SupplyRules.MaxSupplyCost</c>.</summary>
        Reachable,

        /// <summary>A corridor of held ground does run there, but it is longer than supply allows.</summary>
        OutOfRange,

        /// <summary>
        /// No corridor of held or contested ground reaches the target at any length. The faction
        /// does not need a longer supply line, it needs to hold the provinces in between.
        /// </summary>
        NoCorridor
    }

    /// <summary>
    /// The answer to "can this faction reach that province from this one, and in what state?".
    ///
    /// Carries the path as well as the verdict because the interesting part of a supply model is
    /// not that a strike was refused but which provinces would have to be taken to allow it.
    /// </summary>
    public sealed class SupplyLine
    {
        public bool Reachable;
        public SupplyStatus Status;

        /// <summary>Total transit cost of the cheapest line, or -1 when none was found.</summary>
        public int Cost = -1;

        /// <summary>Share of a force that survives the journey. 1 when unrestricted or adjacent.</summary>
        public float Effectiveness = 1f;

        /// <summary>Provinces from source to target inclusive, or empty when unreachable.</summary>
        public List<int> Path = new List<int>();

        /// <summary>Player-facing explanation. Empty when the line is fine.</summary>
        public string Reason = string.Empty;

        public static SupplyLine Unrestricted()
        {
            return new SupplyLine
            {
                Reachable = true,
                Status = SupplyStatus.Unrestricted,
                Cost = -1,
                Effectiveness = 1f,
                Reason = string.Empty
            };
        }

        public static SupplyLine Supplied(int cost, List<int> path)
        {
            return new SupplyLine
            {
                Reachable = true,
                Status = SupplyStatus.Reachable,
                Cost = cost,
                Effectiveness = SupplyRules.Effectiveness(cost),
                Path = path ?? new List<int>(),
                Reason = string.Empty
            };
        }

        public static SupplyLine Cut(SupplyStatus status, string reason)
        {
            return new SupplyLine
            {
                Reachable = false,
                Status = status,
                Cost = -1,
                Effectiveness = 0f,
                Reason = reason
            };
        }
    }
}
