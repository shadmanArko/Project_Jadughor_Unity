using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Model
{
    public readonly struct ItemActionTarget
    {
        public readonly CardinalDirection Direction;
        public readonly Vector3Int CellPosition;
        public readonly Vector3 WorldPosition;

        public ItemActionTarget(
            CardinalDirection direction,
            Vector3Int cellPosition,
            Vector3 worldPosition)
        {
            Direction = direction;
            CellPosition = cellPosition;
            WorldPosition = worldPosition;
        }
    }
}
