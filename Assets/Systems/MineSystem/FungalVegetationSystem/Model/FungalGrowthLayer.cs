using Systems.MineSystem.FungalVegetationSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Model
{
    /// <summary>
    /// One growth occupying one tilemap layer of one cell. A cell can hold two of these
    /// (on different anchors), which is why they are tracked per layer rather than per cell.
    /// </summary>
    public readonly struct FungalGrowthLayer
    {
        public static readonly FungalGrowthLayer Empty = default;

        public FungalGrowthLayer(
            Vector3Int anchorCell,
            FungalAnchor anchor,
            string entryId)
        {
            AnchorCell = anchorCell;
            Anchor = anchor;
            EntryId = entryId;
        }

        /// <summary>The solid cell this growth clings to. Breaking it removes the growth.</summary>
        public Vector3Int AnchorCell { get; }

        public FungalAnchor Anchor { get; }

        /// <summary>Id of the <see cref="Config.FungalVegetationEntry"/> that was picked.</summary>
        public string EntryId { get; }

        public bool HasGrowth => !string.IsNullOrEmpty(EntryId);
    }
}
