using System;

namespace RimSynapse.Factions.Sizing
{
    /// <summary>
    /// How large a population centre is, independent of which mod created it.
    ///
    /// The tier is the unit Epic 3's production scaling, territory footprint, and the UI all read,
    /// so it has to mean the same thing for a vanilla settlement, an Empire colony, and a VOE
    /// outpost. It is derived — never stored on the world object — so it cannot go stale.
    /// </summary>
    public enum SettlementTier
    {
        /// <summary>Not a population centre at all, or too small to register. Carries no tier effects.</summary>
        None = 0,
        Village = 1,
        Town = 2,
        City = 3,
        MajorCity = 4
    }

    public static class SettlementTierExtensions
    {
        public static string Label(this SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.Village: return "village";
                case SettlementTier.Town: return "town";
                case SettlementTier.City: return "city";
                case SettlementTier.MajorCity: return "major city";
                default: return "settlement";
            }
        }

        /// <summary>Capitalised for the inspect pane, where it starts a line.</summary>
        public static string LabelCapitalized(this SettlementTier tier)
        {
            switch (tier)
            {
                case SettlementTier.Village: return "Village";
                case SettlementTier.Town: return "Town";
                case SettlementTier.City: return "City";
                case SettlementTier.MajorCity: return "Major city";
                default: return "Settlement";
            }
        }

        public static bool IsAtLeast(this SettlementTier tier, SettlementTier other)
        {
            return (int)tier >= (int)other;
        }

        /// <summary>The larger of two tiers. Used when several sources disagree about a settlement.</summary>
        public static SettlementTier Max(this SettlementTier a, SettlementTier b)
        {
            return (int)a >= (int)b ? a : b;
        }
    }
}
