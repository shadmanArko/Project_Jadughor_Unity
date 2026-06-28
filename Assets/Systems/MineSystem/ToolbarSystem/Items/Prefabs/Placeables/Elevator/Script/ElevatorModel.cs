using System;
using System.Collections.Generic;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorModel
    {
        private readonly List<Vector3Int> _shaftCells = new();

        public IReadOnlyList<Vector3Int> ShaftCells => _shaftCells;
        public Vector3Int CurrentLiftCell { get; private set; }
        public bool HasRider { get; private set; }
        public bool IsMoving { get; private set; }

        public ElevatorModel(IEnumerable<Vector3Int> shaftCells, Vector3Int liftCell)
        {
            ReplaceShaftCells(shaftCells);
            CurrentLiftCell = liftCell;
        }

        public void ReplaceShaftCells(IEnumerable<Vector3Int> shaftCells)
        {
            _shaftCells.Clear();
            _shaftCells.AddRange(shaftCells);
            _shaftCells.Sort((left, right) => left.y.CompareTo(right.y));
        }

        public void SetLiftCell(Vector3Int cell)
        {
            CurrentLiftCell = cell;
        }

        public void SetRider(bool hasRider)
        {
            HasRider = hasRider;
        }

        public bool TryGetAdjacentCell(int direction, out Vector3Int cell)
        {
            direction = Math.Sign(direction);
            cell = CurrentLiftCell + new Vector3Int(0, direction, 0);
            return direction != 0 && _shaftCells.Contains(cell);
        }

        public void BeginMove()
        {
            IsMoving = true;
        }

        public void EndMove()
        {
            IsMoving = false;
        }
    }
}
