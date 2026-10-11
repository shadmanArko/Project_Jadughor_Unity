using System;
using System.Collections.Generic;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class PlaceableRuntimeRegistry :
        IPlaceableRuntimeResolver,
        IPlaceableRuntimeRegistry
    {
        private readonly Dictionary<Vector3Int, List<IPlaceableRuntime>> _byCell =
            new();
        private readonly Dictionary<IPlaceableRuntime, List<Vector3Int>>
            _cellsByRuntime = new();

        public event Action<Vector3Int, IPlaceableRuntime>
            RuntimeRegistered;
        public event Action<Vector3Int, IPlaceableRuntime>
            RuntimeUnregistered;

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
                    RegisterAtCell(runtime, cell);
                    cells.Add(cell);
                }
            }

            _cellsByRuntime[runtime] = cells;
            for (var i = 0; i < cells.Count; i++)
                RuntimeRegistered?.Invoke(cells[i], runtime);
        }

        public void RegisterCell(
            IPlaceableRuntime runtime,
            Vector3Int cellPosition)
        {
            Unregister(runtime);
            RegisterAtCell(runtime, cellPosition);
            _cellsByRuntime[runtime] = new List<Vector3Int>
            {
                cellPosition
            };
            RuntimeRegistered?.Invoke(cellPosition, runtime);
        }

        public void Unregister(IPlaceableRuntime runtime)
        {
            if (runtime == null ||
                !_cellsByRuntime.Remove(runtime, out var cells))
                return;

            foreach (var cell in cells)
            {
                if (_byCell.TryGetValue(cell, out var runtimes))
                {
                    runtimes.RemoveAll(value => ReferenceEquals(value, runtime));
                    if (runtimes.Count == 0)
                        _byCell.Remove(cell);
                }
                RuntimeUnregistered?.Invoke(cell, runtime);
            }
        }

        public bool Contains<T>(Vector3Int cellPosition)
            where T : class, IPlaceableRuntime
        {
            if (!_byCell.TryGetValue(cellPosition, out var runtimes))
                return false;

            for (var i = 0; i < runtimes.Count; i++)
            {
                if (runtimes[i] is T)
                    return true;
            }

            return false;
        }

        public bool TryResolve(
            Vector3Int cellPosition,
            out IPlaceableRuntime runtime)
        {
            if (_byCell.TryGetValue(cellPosition, out var runtimes) &&
                runtimes.Count > 0)
            {
                runtime = runtimes[^1];
                return true;
            }

            runtime = null;
            return false;
        }

        private void RegisterAtCell(
            IPlaceableRuntime runtime,
            Vector3Int cell)
        {
            if (!_byCell.TryGetValue(cell, out var runtimes))
            {
                runtimes = new List<IPlaceableRuntime>();
                _byCell[cell] = runtimes;
            }

            runtimes.RemoveAll(value => ReferenceEquals(value, runtime));
            runtimes.Add(runtime);
        }
    }
}
