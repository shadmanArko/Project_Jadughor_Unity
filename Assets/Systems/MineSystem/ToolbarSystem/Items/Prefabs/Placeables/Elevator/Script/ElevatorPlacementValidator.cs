using System.Collections.Generic;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorPlacementValidator
    {
        private readonly MineModel _mine;
        private readonly ElevatorNetworkService _network;
        private readonly Dictionary<string, Cell> _reservedShafts = new();
        private readonly Dictionary<string, Vector3Int> _reservedLifts = new();

        public ElevatorPlacementValidator(
            MineModel mine,
            ElevatorNetworkService network)
        {
            _mine = mine;
            _network = network;
        }

        public bool CanPlace(Vector3Int anchor, ElevatorActionProfile profile)
        {
            if (profile == null)
                return false;

            return profile.Kind == ElevatorPlaceableKind.Shaft
                ? CanPlaceShaft(anchor)
                : CanPlaceLift(anchor);
        }

        public bool TryReserve(
            Vector3Int anchor,
            ElevatorActionProfile profile,
            Item item,
            string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) ||
                !CanPlace(anchor, profile))
                return false;

            var mineData = _mine.MineData.Value;
            if (profile.Kind == ElevatorPlaceableKind.Lift)
            {
                mineData.CellPlaceables ??= new List<CellPlaceable>();
                mineData.CellPlaceables.Add(new CellPlaceable
                {
                    Id = instanceId,
                    Name = item.Name,
                    Type = item.Type,
                    Category = item.Category,
                    Variant = item.Variant,
                    Position = new GridPosition(anchor.x, anchor.y),
                    OccupiedCellId = null,
                    ExtraOccupiedDimensionX = 0,
                    ExtraOccupiedDimensionY = 0
                });
                _reservedLifts[instanceId] = anchor;
                return true;
            }

            var cell = mineData?.GetCell(anchor);
            if (cell == null)
                return false;

            cell.HasCellPlaceable = true;
            mineData.RegisterCellPlaceable(new CellPlaceable
            {
                Id = instanceId,
                Name = item.Name,
                Type = item.Type,
                Category = item.Category,
                Variant = item.Variant,
                Position = new GridPosition(anchor.x, anchor.y),
                OccupiedCellId = cell.Id,
                ExtraOccupiedDimensionX = profile.Width - 1,
                ExtraOccupiedDimensionY = profile.Height - 1
            }, new[] { cell.Id });

            _reservedShafts[instanceId] = cell;
            return true;
        }

        public void Release(
            Vector3Int anchor,
            ElevatorActionProfile profile,
            string instanceId)
        {
            if (profile == null || string.IsNullOrEmpty(instanceId))
                return;

            if (profile.Kind == ElevatorPlaceableKind.Lift)
            {
                _reservedLifts.Remove(instanceId);
                _mine.MineData.Value?.UnregisterCellPlaceable(instanceId);
                return;
            }

            if (!_reservedShafts.Remove(instanceId, out var cell))
                return;

            cell.HasCellPlaceable = false;
            _mine.MineData.Value?.UnregisterCellPlaceable(instanceId);
        }

        private bool CanPlaceShaft(Vector3Int anchor)
        {
            var data = _mine.MineData.Value;
            var cell = data?.GetCell(anchor);
            return cell != null &&
                   cell.IsRevealed &&
                   cell.IsBroken &&
                   !cell.HasVine &&
                   !cell.HasCellPlaceable &&
                   !cell.HasWallPlaceable &&
                   !_network.HasShaft(anchor);
        }

        private bool CanPlaceLift(Vector3Int anchor)
        {
            return _network.HasShaft(anchor) &&
                   !_network.HasLift(anchor) &&
                   !_network.ConnectedNetworkHasLift(anchor);
        }
    }
}
