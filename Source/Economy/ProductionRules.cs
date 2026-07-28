namespace RimSynapse.Factions.Economy
{
    /// <summary>
    /// Every number the production model uses, named once. Fourth table of its kind after
    /// <c>PlacementRules</c>, <c>SettlementSizeRules</c> and <c>ResourceRules</c>, and for the same
    /// 0.8 reason: Logic Externalization should move one object per subsystem, not hunt constants.
    ///
    /// The abundance numbers are not new. They are the exact figures
    /// <c>RegionsAndTerritories_EmpiresPatch.GetResourceScale</c> has used since 0.6, lifted out
    /// unchanged so the pure evaluator can be swapped in underneath Empire without altering a single
    /// existing world's output.
    /// </summary>
    public static class ProductionRules
    {
        // --- Abundance (0.6 behaviour, preserved exactly) ---------------------

        /// <summary>Floor for a resource a province has none of. Not zero: a mined-out province
        /// still supports some trade and salvage, and zeroing production outright breaks Empire's
        /// own arithmetic in ways a player reads as a bug.</summary>
        public const float MinAbundanceFactor = 0.2f;

        public const float MaxAbundanceFactor = 2.0f;

        // Reference stock levels each resource's abundance is measured against, from the 0.6 patch.
        public const float NutritionBaseline = 1000f;
        public const float BiomassBaseline = 500f;
        public const float MineralsBaseline = 500f;
        public const float TextilesBaseline = 100f;
        public const float GoodsBaseline = 100f;

        // --- Labour (0.6 behaviour, preserved exactly) ------------------------

        public const float LabourFloor = 0.8f;
        public const float LabourPerPerson = 1f / 2000f;
        public const float MaxLabourFactor = 1.5f;

        // --- Security and ownership (Epic 3 child 1) --------------------------

        /// <summary>
        /// Most a faction can earn from firmly holding and securing the ground it produces on.
        /// Deliberately modest: this is a bonus for doing the thing the mod is about, not a
        /// replacement for actually having resources.
        /// </summary>
        public const float MaxSecurityBonus = 0.25f;

        /// <summary>
        /// Ownership below this earns nothing. Matches <c>PlacementRules.OwnershipThreshold</c> on
        /// purpose — the score at which R&amp;T is willing to call a province yours for placement is
        /// the same score at which it is willing to pay you for holding it.
        /// </summary>
        public const float MinOwnershipForBonus = 0.30f;

        /// <summary>
        /// Worst penalty an unsecured province can suffer, as a fraction. <b>Set to zero, on
        /// purpose.</b> A penalty here would change output in every existing world the moment 0.7
        /// loads, including worlds where nobody has done anything wrong — the same regression Epic 2
        /// took care to avoid. The rule is named and tested so switching it on later is one number,
        /// not a design argument.
        /// </summary>
        public const float MaxInsecurityPenalty = 0f;

        // --- Locality (Epic 3 child 3) ----------------------------------------

        /// <summary>
        /// How much of the gap between local richness and the province average actually reaches
        /// output. Half, not all: a holding draws on more than the tiles it stands on, so terrain
        /// immediately underneath should tilt production without dictating it.
        /// </summary>
        public const float LocalityWeight = 0.5f;

        public const float MinLocalityFactor = 0.5f;
        public const float MaxLocalityFactor = 1.5f;

        // --- Composition ------------------------------------------------------

        /// <summary>
        /// Bounds on the product of every factor.
        ///
        /// Four multipliers that each look reasonable alone compose to a spread nobody designed:
        /// 0.2 × 0.8 × 0.5 × 0.75 is a fortieth of baseline, and 2.0 × 1.5 × 1.5 × 1.25 is five and
        /// a half times it. That ninety-fold spread between the best and worst province is exactly
        /// the snowball <c>SettlementSizeRules</c>' sublinear production scale exists to prevent, so
        /// the product is clamped rather than trusted.
        /// </summary>
        public const float MinTotalFactor = 0.15f;

        public const float MaxTotalFactor = 3.0f;
    }
}
