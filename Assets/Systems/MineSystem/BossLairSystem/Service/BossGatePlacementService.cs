using System;
using System.Collections.Generic;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Scriptable;
using Systems.MineSystem.BossLairSystem.View;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using UnityEngine;
using Zenject;
using Random = System.Random;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Places a boss gate inside one of the mine's caves and owns the gate
    /// instance.
    /// </summary>
    /// <remarks>
    /// Caves are used because they are carved during generation, so the cell is
    /// guaranteed reachable once the player digs into it, and the gate stays
    /// hidden behind the unrevealed tilemap until then. This is deliberately
    /// independent of the generated boss cave, which is being removed.
    /// <para>
    /// Coordinate note: <see cref="Cave.CellPositions"/> is in grid space
    /// (depth is negative Y), while the cave's bound fields are in generation
    /// space. Only <c>CellPositions</c> is used here.
    /// </para>
    /// </remarks>
    public sealed class BossGatePlacementService : IDisposable
    {
        private const string RootName = "Boss Gate";

        private readonly DiContainer _container;
        private readonly MineView _mineView;
        private readonly Random _random = new();
        private readonly List<Cave> _caveBuffer = new();
        private readonly List<GridPosition> _candidateBuffer = new();
        private BossGateView _activeGate;
        private GameObject _root;
        private bool _disposed;

        public BossGatePlacementService(DiContainer container, MineView mineView)
        {
            _container = container;
            _mineView = mineView;
        }

        public BossGateView ActiveGate => _activeGate;

        public bool TryPlace(
            MineData mineData,
            BossProfileScriptable profile,
            out BossGatePlacement placement)
        {
            placement = default;
            if (_disposed || mineData == null || profile == null ||
                profile.GatePrefab == null)
                return false;

            Clear();

            if (!TryResolveGateCell(mineData, out var cell))
                return false;

            _root = new GameObject(RootName);
            try
            {
                var worldPosition = _mineView.grid.GetCellCenterWorld(
                    new Vector3Int(cell.X, cell.Y, 0));
                _root.transform.position = worldPosition;
                _activeGate = _container
                    .InstantiatePrefabForComponent<BossGateView>(
                        profile.GatePrefab, _root.transform);
                _activeGate.transform.localPosition = Vector3.zero;
                placement = new BossGatePlacement(
                    new Vector3Int(cell.X, cell.Y, 0), profile);
                return true;
            }
            catch (Exception exception)
            {
                // A failed gate must not leave an orphaned root or a half-built
                // gate in the mine.
                Debug.LogException(exception);
                Clear();
                return false;
            }
        }

        /// <summary>
        /// Finds a cave cell with a solid floor beneath it and no formation on
        /// it, so the gate is reachable and does not overlap a stalagmite.
        /// </summary>
        private bool TryResolveGateCell(MineData mineData, out GridPosition cell)
        {
            cell = default;
            if (mineData.Caves == null || mineData.Caves.Count == 0)
                return false;

            _caveBuffer.Clear();
            for (var i = 0; i < mineData.Caves.Count; i++)
            {
                var cave = mineData.Caves[i];
                if (cave?.CellPositions != null && cave.CellPositions.Count > 0)
                    _caveBuffer.Add(cave);
            }
            if (_caveBuffer.Count == 0)
                return false;

            Shuffle(_caveBuffer);
            for (var i = 0; i < _caveBuffer.Count; i++)
            {
                CollectFloorCells(mineData, _caveBuffer[i]);
                if (_candidateBuffer.Count == 0)
                    continue;
                cell = _candidateBuffer[_random.Next(_candidateBuffer.Count)];
                return true;
            }
            return false;
        }

        private void CollectFloorCells(MineData mineData, Cave cave)
        {
            _candidateBuffer.Clear();
            var positions = cave.CellPositions;
            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                var current = mineData.GetCell(
                    new Vector3Int(position.X, position.Y, 0));
                if (current == null || !current.IsBroken)
                    continue;
                if (HasFormation(cave, current.Id))
                    continue;
                // Depth is negative Y, so the cell below is one step lower.
                var below = mineData.GetCell(
                    new Vector3Int(position.X, position.Y - 1, 0));
                if (below == null || below.IsBroken)
                    continue;
                _candidateBuffer.Add(position);
            }
        }

        private static bool HasFormation(Cave cave, string cellId) =>
            (cave.StalagmiteCellIds != null &&
             cave.StalagmiteCellIds.Contains(cellId)) ||
            (cave.StalactiteCellIds != null &&
             cave.StalactiteCellIds.Contains(cellId));

        public void Clear()
        {
            _activeGate = null;
            if (_root == null)
                return;
            UnityEngine.Object.Destroy(_root);
            _root = null;
        }

        private void Shuffle(List<Cave> caves)
        {
            for (var i = caves.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (caves[i], caves[j]) = (caves[j], caves[i]);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Clear();
        }
    }
}
