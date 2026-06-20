using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.PileDriver.Script
{
    public sealed class PileDriverPlacementValidator :
        IPileDriverPlacementValidator
    {
        private readonly MineModel _mine;

        public PileDriverPlacementValidator(MineModel mine)
        {
            _mine = mine;
        }

        public bool CanPlace(
            Vector3Int anchor,
            PileDriverDirection direction)
        {
            var data = _mine.MineData.Value;
            if (data == null)
                return false;

            return IsOpen(data.GetCell(anchor)) &&
                   IsOpen(data.GetCell(anchor + ToOffset(direction)));
        }

        private static bool IsOpen(Cell cell)
        {
            return cell != null &&
                   cell.IsRevealed &&
                   cell.IsBroken &&
                   !cell.HasCellPlaceable &&
                   !cell.HasWallPlaceable;
        }

        public static Vector3Int ToOffset(PileDriverDirection direction)
        {
            return direction switch
            {
                PileDriverDirection.Left => Vector3Int.left,
                PileDriverDirection.Right => Vector3Int.right,
                PileDriverDirection.Up => Vector3Int.up,
                _ => Vector3Int.down
            };
        }
    }
}
