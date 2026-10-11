using Systems.MineSystem.FungalVegetationSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Signal
{
    /// <summary>
    /// Fired when a growth appears. Purely cosmetic today - this exists so that when fungi
    /// become harvestable, luminous or hazardous, those systems can listen rather than
    /// reaching into the fungal slice.
    /// </summary>
    public struct FungalGrowthPlacedSignal
    {
        public Vector3Int Cell;
        public Vector3Int AnchorCell;
        public FungalAnchor Anchor;
        public string EntryId;
        public int Layer;
    }
}
