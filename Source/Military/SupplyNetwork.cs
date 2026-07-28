using System;
using System.Collections.Generic;
using RimSynapse.RegionsAndTerritories.Placement;

namespace RimSynapse.Factions.Military
{
    /// <summary>
    /// Everything <see cref="SupplyEvaluator"/> needs to know about the world, expressed as plain
    /// data and delegates rather than as calls into <c>Find</c>. Same device as
    /// <see cref="PlacementWorld"/>, and for the same reason: the live game builds one of these from
    /// <c>SynapseRegionManager</c>, the test suite builds one from a hand-drawn graph, and the rules
    /// cannot tell the difference.
    ///
    /// <para>Deliberately smaller than <see cref="PlacementWorld"/>. Supply is a question about the
    /// province graph alone — no tiles, no distances, no world objects — so this asks for the two
    /// things a graph search actually needs and nothing else. A model that takes more than it uses
    /// is a model whose next author cannot tell which inputs matter.</para>
    /// </summary>
    public sealed class SupplyNetwork
    {
        /// <summary>Provinces sharing a border with the given province.</summary>
        public Func<int, IEnumerable<int>> Neighbours;

        /// <summary>How the given faction stands in the given province.</summary>
        public Func<int, object, ProvinceControl> ControlOf;

        public IEnumerable<int> NeighboursOf(int provinceId)
        {
            if (Neighbours == null || provinceId < 0) return new int[0];
            return Neighbours(provinceId) ?? (IEnumerable<int>)new int[0];
        }

        public ProvinceControl Control(int provinceId, object faction)
        {
            if (ControlOf == null || provinceId < 0) return ProvinceControl.Unclaimed;
            return ControlOf(provinceId, faction);
        }
    }
}
