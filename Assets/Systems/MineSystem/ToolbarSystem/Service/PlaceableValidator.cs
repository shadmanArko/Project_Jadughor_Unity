using System;
using System.Collections.Generic;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.ToolbarSystem.Enum;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Profile;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class PlaceableValidator : IPlaceableValidator
    {
        private readonly MineModel _mine;
        private readonly Dictionary<string, List<Cell>> _reservations = new();

        public PlaceableValidator(MineModel mine)
        {
            _mine = mine;
        }

        public bool CanPlace(Vector3Int anchor, PlaceableActionProfile profile)
        {
            return TryCollectCells(anchor, profile, out _);
        }

        public bool TryReserve(
            Vector3Int anchor,
            PlaceableActionProfile profile,
            Item item,
            string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) ||
                !TryCollectCells(anchor, profile, out var cells))
                return false;

            foreach (var cell in cells)
            {
                if (profile.TargetKind == PlaceableTargetKind.Cell)
                    cell.HasCellPlaceable = true;
                else
                    cell.HasWallPlaceable = true;
            }

            var data = _mine.MineData.Value;
            if (profile.TargetKind == PlaceableTargetKind.Cell)
            {
                data.RegisterCellPlaceable(new CellPlaceable
                {
                    Id = instanceId,
                    Name = item.Name,
                    Type = item.Type,
                    Category = item.Category,
                    Variant = item.Variant,
                    Position = new GridPosition(anchor.x, anchor.y),
                    OccupiedCellId = cells[0].Id,
                    ExtraOccupiedDimensionX = profile.Width - 1,
                    ExtraOccupiedDimensionY = profile.Height - 1
                }, cells.ConvertAll(cell => cell.Id));
            }
            else
            {
                data.RegisterWallPlaceable(new WallPlaceable
                {
                    Id = instanceId,
                    Name = item.Name,
                    Type = item.Type,
                    Category = item.Category,
                    Variant = item.Variant,
                    Position = new GridPosition(anchor.x, anchor.y),
                    AnchorCellId = cells[0].Id,
                    OccupiedCellIds = cells.ConvertAll(cell => cell.Id),
                    ExtraOccupiedDimensionX = profile.Width - 1,
                    ExtraOccupiedDimensionY = profile.Height - 1
                });
            }

            _reservations[instanceId] = cells;
            return true;
        }

        public void Release(
            Vector3Int anchor,
            PlaceableActionProfile profile,
            string instanceId)
        {
            if (!_reservations.Remove(instanceId, out var cells))
                return;

            foreach (var cell in cells)
            {
                if (profile.TargetKind == PlaceableTargetKind.Cell)
                    cell.HasCellPlaceable = false;
                else
                    cell.HasWallPlaceable = false;
            }

            var data = _mine.MineData.Value;
            if (profile.TargetKind == PlaceableTargetKind.Cell)
                data.UnregisterCellPlaceable(instanceId);
            else
                data.UnregisterWallPlaceable(instanceId);
        }

        private bool TryCollectCells(
            Vector3Int anchor,
            PlaceableActionProfile profile,
            out List<Cell> cells)
        {
            cells = new List<Cell>(profile.Width * profile.Height);
            var data = _mine.MineData.Value;
            if (data == null)
                return false;

            for (var x = 0; x < profile.Width; x++)
            {
                for (var y = 0; y < profile.Height; y++)
                {
                    var cell = data.GetCell(
                        anchor + new Vector3Int(x, y, 0));
                    if (cell == null ||
                        !cell.IsRevealed ||
                        !cell.IsBroken ||
                        cell.HasVine ||
                        cell.HasCellPlaceable ||
                        cell.HasWallPlaceable)
                        return false;

                    cells.Add(cell);
                }
            }

            return true;
        }
    }
}
