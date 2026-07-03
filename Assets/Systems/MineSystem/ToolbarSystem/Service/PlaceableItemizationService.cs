using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class PlaceableItemizationService
    {
        private readonly MineModel _mine;
        private readonly CollectableFactory _collectables;

        public PlaceableItemizationService(
            MineModel mine,
            CollectableFactory collectables)
        {
            _mine = mine;
            _collectables = collectables;
        }

        public bool TryConvert(
            PlaceableSpawnContext context,
            Vector3 worldPosition)
        {
            var data = _mine.MineData.Value;
            var cell = data?.GetCell(context.CellPosition);
            if (cell == null)
                return false;

            var item = FindRegisteredPlaceable(
                data,
                cell,
                context);
            if (item == null)
                return false;

            if (!_collectables.CanSpawn(item) ||
                !_collectables.TrySpawn(item, worldPosition))
            {
                Debug.LogWarning(
                    $"Could not convert placeable " +
                    $"'{context.PlaceableId}' to an item.");
                return false;
            }

            SanitizePlacement(item);
            return true;
        }

        private static Placeable FindRegisteredPlaceable(
            MineData data,
            Cell cell,
            PlaceableSpawnContext context)
        {
            Placeable placeable =
                context.Profile.TargetKind == PlaceableTargetKind.Cell
                    ? data.GetCellPlaceable(cell.Id)
                    : data.GetWallPlaceable(cell.Id);
            if (placeable != null &&
                placeable.Id == context.InstanceId)
                return placeable;

            if (context.Profile.TargetKind == PlaceableTargetKind.Cell)
            {
                return data.CellPlaceables?.Find(
                    value => value.Id == context.InstanceId);
            }

            return data.WallPlaceables?.Find(
                value => value.Id == context.InstanceId);
        }

        private static void SanitizePlacement(Placeable placeable)
        {
            placeable.Position = default;

            if (placeable is CellPlaceable cellPlaceable)
            {
                cellPlaceable.OccupiedCellId = null;
                cellPlaceable.ExtraOccupiedDimensionX = 0;
                cellPlaceable.ExtraOccupiedDimensionY = 0;
                return;
            }

            if (placeable is not WallPlaceable wallPlaceable)
                return;

            wallPlaceable.AnchorCellId = null;
            wallPlaceable.OccupiedCellIds?.Clear();
            wallPlaceable.ExtraOccupiedDimensionX = 0;
            wallPlaceable.ExtraOccupiedDimensionY = 0;
        }
    }
}
