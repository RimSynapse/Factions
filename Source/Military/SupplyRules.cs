using RimSynapse.RegionsAndTerritories.Placement;

namespace RimSynapse.Factions.Military
{
    /// <summary>
    /// The numeric rules governing how far a faction can project force, in one table.
    ///
    /// Sixth named table, alongside <c>PlacementRules</c>, <c>SettlementSizeRules</c>,
    /// <c>ResourceRules</c>, <c>ProductionRules</c> and <c>TaxationRules</c>, and for the same 0.8
    /// reason: Logic Externalization should move one object rather than hunt constants.
    ///
    /// <para>The defaults are chosen so that <b>this model can only ever be more permissive than
    /// 0.6 was.</b> R&amp;T already refuses any military action whose target province is not the
    /// source province or one of its neighbours; here that same move costs 1, which clears
    /// <see cref="MaxSupplyCost"/> without needing a single held province. Everything the supply
    /// model adds is reach a faction did not previously have, earned by holding the ground in
    /// between. Nothing it adds can take a legal move away.</para>
    /// </summary>
    public static class SupplyRules
    {
        /// <summary>
        /// Reach a faction has with no owned corridor at all — the province next door, which is
        /// exactly the rule R&amp;T has enforced since 0.6. It is also the attrition-free distance:
        /// the one move that was always legal is also the one move that costs nothing.
        /// </summary>
        public const int UnsuppliedReach = 1;

        /// <summary>
        /// The furthest a supply line may run, counted in transit cost rather than in provinces.
        /// Six held provinces, or three contested ones, or any mix that adds up.
        /// </summary>
        public const int MaxSupplyCost = 6;

        /// <summary>Cost of moving a supply line through a province the faction holds outright.</summary>
        public const int HeldTransitCost = 1;

        /// <summary>
        /// Cost of moving through a province the faction is still fighting over. Double, because a
        /// corridor a rival is also standing in is a corridor that has to be forced rather than
        /// walked, and an empire that routes its supply through contested ground should feel it.
        /// </summary>
        public const int ContestedTransitCost = 2;

        /// <summary>Returned by <see cref="TransitCost"/> for ground no supply line may cross.</summary>
        public const int Impassable = -1;

        /// <summary>Effectiveness lost per unit of transit cost beyond <see cref="UnsuppliedReach"/>.</summary>
        public const float AttritionPerCost = 0.08f;

        /// <summary>
        /// Floor on effectiveness. Not reachable with today's constants — the worst legal line costs
        /// <see cref="MaxSupplyCost"/> and lands at 0.60 — and that is deliberate: the floor exists
        /// so that raising attrition or the cost ceiling later cannot silently produce an army that
        /// arrives at zero strength. A knob that only matters once another knob moves is still worth
        /// naming.
        /// </summary>
        public const float MinEffectiveness = 0.40f;

        /// <summary>
        /// What it costs a supply line to pass through a province in this state, or
        /// <see cref="Impassable"/> if it may not.
        ///
        /// <para>Unclaimed and foreign ground is impassable, which is the whole content of the
        /// phrase "contiguous owned territory". Letting a line cross neutral ground at a penalty
        /// would make holding the corridor pointless, and the escape hatch a young faction needs
        /// already exists: the province next door is always in reach whoever owns it.</para>
        ///
        /// <para>Reusing <see cref="ProvinceControl"/> rather than declaring a military-flavoured
        /// copy of it is deliberate. It is the same question about the same province, and two enums
        /// meaning "who holds this" is how the placement layer and the military layer end up
        /// disagreeing about the map.</para>
        /// </summary>
        public static int TransitCost(ProvinceControl control)
        {
            if (control == ProvinceControl.Held) return HeldTransitCost;
            if (control == ProvinceControl.Contested) return ContestedTransitCost;
            return Impassable;
        }

        /// <summary>
        /// How much of a force actually arrives at the end of a line of the given cost, 0 to 1.
        ///
        /// Exactly 1 at or below <see cref="UnsuppliedReach"/>, so the adjacent strike — the only
        /// move 0.6 allowed — is unchanged to the digit.
        /// </summary>
        public static float Effectiveness(int cost)
        {
            if (cost <= UnsuppliedReach) return 1f;

            float effectiveness = 1f - AttritionPerCost * (cost - UnsuppliedReach);
            if (effectiveness < MinEffectiveness) return MinEffectiveness;
            if (effectiveness > 1f) return 1f;
            return effectiveness;
        }
    }
}
