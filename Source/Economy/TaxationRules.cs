namespace RimSynapse.Factions.Economy
{
    /// <summary>
    /// Every number the taxation model uses, named once. Fifth table of its kind after
    /// <c>PlacementRules</c>, <c>SettlementSizeRules</c>, <c>ResourceRules</c> and
    /// <c>ProductionRules</c>, and for the same 0.8 reason.
    ///
    /// <para>Taxation is deliberately <b>not</b> a second production model. Production asks how much
    /// comes out of the ground; taxation asks how much of it reaches the capital. The two answer to
    /// different things, which is why this table has its own thresholds rather than reusing
    /// <c>ProductionRules</c>' — a province can be rich and ungovernable, or poor and perfectly
    /// loyal.</para>
    /// </summary>
    public static class TaxationRules
    {
        // --- Loyalty (what firm ownership is worth) ---------------------------

        /// <summary>
        /// Most a faction can add to its collectible share by holding a province outright.
        ///
        /// Modest on purpose. Tax is already proportional to production, and production already
        /// pays an ownership bonus; a large multiplier here would be the same reward counted twice.
        /// </summary>
        public const float MaxLoyaltyBonus = 0.20f;

        /// <summary>
        /// Ownership below this earns nothing. Mirrors <c>PlacementRules.OwnershipThreshold</c> and
        /// <c>ProductionRules.MinOwnershipForBonus</c>: the score at which R&amp;T is willing to call
        /// a province yours is the same score at which it will let you tax it as yours.
        /// </summary>
        public const float MinOwnershipForLoyalty = 0.30f;

        // --- Interception (what a rival costs you) ----------------------------

        /// <summary>
        /// Most a rival presence can cost a levy in transit — raided caravans, bought-off
        /// collectors, settlements pleading hardship they can prove.
        ///
        /// <para>Unlike <c>ProductionRules.MaxInsecurityPenalty</c> this is <b>not</b> parked at
        /// zero, and the reason is a difference in what the input means. Child 1 keys its penalty on
        /// <i>low security</i>, and a 0.6 world with no ownership data reads as security 0 — so a
        /// non-zero penalty there would punish every province in the world for missing data. This
        /// keys on <i>rival pressure</i>, which is positive evidence: no data means no rival found
        /// means no interception. Absence of evidence and evidence of absence are the same number in
        /// child 1's formulation and different numbers in this one, which is why this one can
        /// afford to bite.</para>
        /// </summary>
        public const float MaxInterceptedFraction = 0.35f;

        /// <summary>
        /// Rival pressure below this intercepts nothing. Mirrors <c>PlacementRules.ContestMargin</c>:
        /// a scouting party or a single outlying camp is noise, and taxing an empire for it would
        /// make the model twitch at every wandering faction on the map.
        /// </summary>
        public const float RivalPressureFloor = 0.10f;

        // --- Concession (what a large settlement withholds) -------------------

        /// <summary>
        /// The fraction a settlement of each tier keeps back — charters, exemptions, and the plain
        /// fact that a great city can negotiate and a village cannot.
        ///
        /// <para>This is the counterweight to <c>SettlementSizeRules.ProductionScale</c>, and it is
        /// what stops one enormous capital being the only correct play. A major city produces 2.25×
        /// a village and remits 0.80 of it, so it is still clearly worth growing — the curve flattens
        /// rather than reverses. A test pins that: net collectible output must never fall as a
        /// settlement tiers up, or growing a city would be a trap dressed as a reward.</para>
        ///
        /// <para><c>None</c> and <c>Village</c> concede nothing, so an untiered world — every world
        /// before 0.7, and every world with tiers switched off — pays exactly what it always did.</para>
        /// </summary>
        public const float TownConcession = 0.05f;
        public const float CityConcession = 0.12f;
        public const float MajorCityConcession = 0.20f;

        // --- Composition ------------------------------------------------------

        /// <summary>
        /// Floor on the collectible share.
        ///
        /// Today's constants cannot reach it — the worst case is 1 − 0.35 − 0.20 = 0.45 — and that
        /// is the point of stating it. It exists so that raising <c>MaxInterceptedFraction</c> or the
        /// concessions later cannot silently drive a tithe to nothing, which is a failure a player
        /// reads as a broken mod rather than as a hard province.
        /// </summary>
        public const float MinCollectionFactor = 0.30f;

        /// <summary>
        /// Ceiling on the collectible share. Equal to <c>1 + MaxLoyaltyBonus</c> exactly, so the
        /// clamp is documentation rather than a hidden second rule: nothing in this table can push
        /// past it, and if something later does, that is a change to argue about rather than absorb.
        /// </summary>
        public const float MaxCollectionFactor = 1.20f;
    }
}
