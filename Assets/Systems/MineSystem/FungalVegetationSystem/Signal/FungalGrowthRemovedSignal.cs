using Systems.MineSystem.FungalVegetationSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Signal
{
    /// <summary>
    /// Fired when a growth is erased because the wall it clung to was broken.
    /// </summary>
    public struct FungalGrowthRemovedSignal
    {
        public Vector3Int Cell;
        public Vector3Int AnchorCell;
        public FungalAnchor Anchor;
        public string EntryId;
        public int Layer;
    }
}
