using System.Collections.Generic;
using RimSynapse.RegionsAndTerritories.Integration;
using RimSynapse.RegionsAndTerritories.Placement;

namespace RimSynapse.Factions.Standing
{
    /// <summary>
    /// Epic 6 child 1 — R&amp;T's territory, holdings and settlement tiers reduced to one per-faction
    /// summary another mod can read.
    ///
    /// Pure: no <c>Find</c>, no Harmony, no Unity. Everything arrives through
    /// <see cref="StandingWorld"/>.
    ///
    /// <para>There is no rule here in the sense the other evaluators have rules — nothing is refused
    /// and nothing is scaled. It is a single pass that counts. The reason it is a separate file
    /// rather than a loop inside the façade is that <b>the counting decisions are the interesting
    /// part</b>: which kinds count as holdings, whether contested ground counts as territory,
    /// whether an untiered settlement is invisible or merely untiered. Those decisions want to be
    /// stated somewhere testable, because a consumer in another repository will build behaviour on
    /// top of them and will not be able to see them.</para>
    /// </summary>
    public static class StandingEvaluator
    {
        public static FactionStanding Evaluate(StandingWorld world)
        {
            var standing = new FactionStanding();

            // No world is not the same as an empty world, but it produces the same answer on
            // purpose. A consumer asking about a faction during worldgen, or with R&T's governance
            // switched off, should get "this faction holds nothing" rather than a null it has to
            // remember to check — the failure mode of the alternative is a null reference in
            // somebody else's mod, thrown from a line that looks correct.
            if (world == null) return standing;

            float holdingStrength = 0f;

            List<StandingHolding> holdings = world.Holdings;
            if (holdings != null)
            {
                for (int i = 0; i < holdings.Count; i++)
                {
                    StandingHolding holding = holdings[i];
                    if (holding == null) continue;

                    // Non-territorial kinds are skipped entirely rather than counted at zero
                    // weight. A caravan is not a small holding; it is not a holding. Counting it
                    // and weighting it to nothing would make Holdings disagree with the sum of
                    // CountOfKind, and a consumer would eventually notice and pick the wrong one.
                    if (!holding.kind.IsTerritorial()) continue;

                    standing.Record(holding.kind, holding.tier);
                    holdingStrength += StandingRules.HoldingStrength(holding.kind, holding.tier);

                    if (holding.kind.HasPopulation()) standing.Population += holding.population;
                }
            }

            List<StandingProvince> provinces = world.Provinces;
            if (provinces != null)
            {
                for (int i = 0; i < provinces.Count; i++)
                {
                    StandingProvince province = provinces[i];
                    if (province == null) continue;

                    if (province.control == ProvinceControl.Held)
                    {
                        standing.HeldProvinces++;
                        standing.TerritoryTiles += province.tileCount;
                    }
                    else if (province.control == ProvinceControl.Contested)
                    {
                        standing.ContestedProvinces++;
                    }
                }
            }

            standing.PerceivedStrength = StandingRules.Strength(standing, holdingStrength);

            return standing;
        }
    }
}
