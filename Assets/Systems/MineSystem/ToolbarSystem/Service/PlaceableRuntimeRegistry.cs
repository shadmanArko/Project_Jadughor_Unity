using System.Collections.Generic;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class PlaceableRuntimeRegistry :
        IPlaceableRuntimeResolver
    {
        private readonly Dictionary<Vector3Int, IPlaceableRuntime> _byCell =
            new();
        private readonly Dictionary<IPlaceableRuntime, List<Vector3Int>>
            _cellsByRuntime = new();

        public void Register(
            IPlaceableRuntime runtime,
            PlaceableSpawnContext context)
        {
            Unregister(runtime);
            var cells = new List<Vector3Int>(
                context.Profile.Width * context.Profile.Height);
            for (var x = 0; x < context.Profile.Width; x++)
            {
                for (var y = 0; y < context.Profile.Height; y++)
                {
                    var cell = context.CellPosition +
                               new Vector3Int(x, y, 0);
                    _byCell[cell] = runtime;
                    cells.Add(cell);
                }
            }

            _cellsByRuntime[runtime] = cells;
        }

        public void RegisterCell(
            IPlaceableRuntime runtime,
            Vector3Int cellPosition)
        {
            Unregister(runtime);
            _byCell[cellPosition] = runtime;
            _cellsByRuntime[runtime] = new List<Vector3Int>
            {
                cellPosition
            };
        }

        public void Unregister(IPlaceableRuntime runtime)
        {
            if (runtime == null ||
                !_cellsByRuntime.Remove(runtime, out var cells))
                return;

            foreach (var cell in cells)
            {
                if (_byCell.TryGetValue(cell, out var registered) &&
                    ReferenceEquals(registered, runtime))
                    _byCell.Remove(cell);
            }
        }

        public bool TryResolve(
            Vector3Int cellPosition,
            out IPlaceableRuntime runtime)
        {
            return _byCell.TryGetValue(cellPosition, out runtime);
        }
    }
}
