using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    public interface IPileDriverPlacementValidator
    {
        bool CanPlace(Vector3Int anchor, PileDriverDirection direction);
        bool HasBrokenCellInDirection(
            Vector3Int anchor,
            PileDriverDirection direction);
    }
}
