using Systems.MineSystem.FungalVegetationSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.FungalVegetationSystem.Model
{
    /// <summary>
    /// A growth being placed or removed. This is what the model publishes; the controller
    /// turns it into a tilemap write. Carries the layer index so the controller knows which
    /// of the two fungal tilemaps to touch.
    /// </summary>
    public readonly struct FungalGrowthPlacement
    {
        public FungalGrowthPlacement(
            Vector3Int cell,
            FungalGrowthLayer growth,
            int layer)
        {
            Cell = cell;
            AnchorCell = growth.AnchorCell;
            Anchor = growth.Anchor;
            EntryId = growth.EntryId;
            Layer = layer;
        }

        /// <summary>The broken cell the sprite is drawn in.</summary>
        public Vector3Int Cell { get; }

        /// <summary>The solid cell the growth clings to.</summary>
        public Vector3Int AnchorCell { get; }

        public FungalAnchor Anchor { get; }
        public string EntryId { get; }

        /// <summary>0 = primary fungal tilemap, 1 = secondary.</summary>
        public int Layer { get; }
    }
}
