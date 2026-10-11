using System;
using System.Collections.Generic;
using System.Linq;
using Systems.MineSystem.Mine.Model;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    public sealed class CaveVisualizerService :
        IInitializable,
        IDisposable
    {
        private readonly CaveFormationPool _formationPool;
        private readonly CompositeDisposable _disposables = new();
        private readonly Dictionary<string, CaveFormationRuntime>
            _formationByCellId = new();
        private readonly Dictionary<string, List<CaveFormationRuntime>>
            _formationsByRootCellId = new();

        public CaveVisualizerService(CaveFormationPool formationPool)
        {
            _formationPool = formationPool;
        }

        public void Initialize()
        {
            _formationPool.Despawned += HandleFormationDespawned;
        }

        public bool TryRevealCave(
            Cell cell,
            MineData mineData,
            HashSet<Cell> revealedCells,
            IEnumerable<Vector3Int> revealOffsets,
            ISet<string> revealedCaveIds)
        {
            if (cell == null ||
                mineData?.Caves == null ||
                string.IsNullOrEmpty(cell.CaveId))
                return false;

            var cave = mineData.Caves
                .FirstOrDefault(candidate => candidate.Id == cell.CaveId);
            if (cave == null)
            {
                Debug.LogError($"Fatal Error: Cave ID mismatch: {cell.CaveId}");
                return false;
            }

            if (cave.IsRevealed)
                return true;

            cave.IsRevealed = true;
            revealedCaveIds?.Add(cave.Id);
            RevealCaveCells(cave, mineData, revealedCells);
            RevealCaveBoundaryCells(
                cave,
                mineData,
                revealedCells,
                revealOffsets,
                revealedCaveIds);
            SpawnFormations(cave, mineData);
            return true;
        }

        public void HandleRootCellBroken(Cell cell)
        {
            if (cell == null ||
                string.IsNullOrEmpty(cell.Id) ||
                !_formationsByRootCellId.TryGetValue(
                    cell.Id,
                    out var formations))
                return;

            var snapshot = formations.ToArray();
            foreach (var formation in snapshot)
                formation?.BreakFromRoot();
        }

        public void ResetFormations()
        {
            _formationByCellId.Clear();
            _formationsByRootCellId.Clear();
            _formationPool.DespawnAll();
        }

        public void Dispose()
        {
            _formationPool.Despawned -= HandleFormationDespawned;
            _disposables.Dispose();
            ResetFormations();
        }

        private static void RevealCaveCells(
            Cave cave,
            MineData mineData,
            HashSet<Cell> revealedCells)
        {
            foreach (var position in cave.CellPositions)
            {
                var caveCell = mineData.GetCell(position);
                if (caveCell == null)
                {
                    Debug.LogError($"Could not find cave position: {position}");
                    continue;
                }

                caveCell.IsRevealed = true;
                caveCell.IsBroken = true;
                revealedCells.Add(caveCell);
            }
        }

        private void RevealCaveBoundaryCells(
            Cave cave,
            MineData mineData,
            HashSet<Cell> revealedCells,
            IEnumerable<Vector3Int> revealOffsets,
            ISet<string> revealedCaveIds)
        {
            foreach (var position in cave.CellPositions)
            {
                var caveCell = mineData.GetCell(position);
                if (caveCell == null)
                    continue;

                foreach (var offset in revealOffsets)
                {
                    var adjacentCell =
                        mineData.GetCell(caveCell.GetPosition() + offset);
                    if (adjacentCell == null)
                        continue;

                    if (!adjacentCell.IsRevealed)
                    {
                        adjacentCell.IsRevealed = true;
                        revealedCells.Add(adjacentCell);

                        if (!string.IsNullOrEmpty(adjacentCell.CaveId) &&
                            adjacentCell.CaveId != cave.Id)
                            TryRevealCave(
                                adjacentCell,
                                mineData,
                                revealedCells,
                                revealOffsets,
                                revealedCaveIds);
                    }
                    else
                    {
                        revealedCells.Add(adjacentCell);
                    }
                }
            }
        }

        private void SpawnFormations(Cave cave, MineData mineData)
        {
            SpawnFormationList(
                cave.StalactiteCellIds,
                mineData,
                isStalactite: true);
            SpawnFormationList(
                cave.StalagmiteCellIds,
                mineData,
                isStalactite: false);
        }

        private void SpawnFormationList(
            IEnumerable<string> cellIds,
            MineData mineData,
            bool isStalactite)
        {
            if (cellIds == null)
                return;

            foreach (var cellId in cellIds)
            {
                if (string.IsNullOrEmpty(cellId) ||
                    _formationByCellId.ContainsKey(cellId))
                    continue;

                var cell = mineData.GetCellById(cellId);
                if (cell == null)
                    continue;

                var rootCell = mineData.GetCell(
                    cell.GetPosition() +
                    (isStalactite ? Vector3Int.up : Vector3Int.down));
                var runtime = isStalactite
                    ? _formationPool.SpawnStalactite(
                        mineData,
                        cell,
                        rootCell?.Id)
                    : _formationPool.SpawnStalagmite(
                        mineData,
                        cell,
                        rootCell?.Id);

                if (runtime == null)
                    continue;

                _formationByCellId[cellId] = runtime;
                if (!string.IsNullOrEmpty(rootCell?.Id))
                    RegisterRoot(rootCell.Id, runtime);

                if (rootCell == null ||
                    rootCell.IsBroken ||
                    rootCell.IsBlank)
                    runtime.BreakFromRoot();
            }
        }

        private void RegisterRoot(
            string rootCellId,
            CaveFormationRuntime runtime)
        {
            if (!_formationsByRootCellId.TryGetValue(
                    rootCellId,
                    out var formations))
            {
                formations = new List<CaveFormationRuntime>();
                _formationsByRootCellId[rootCellId] = formations;
            }

            formations.Add(runtime);
        }

        private void HandleFormationDespawned(CaveFormationRuntime runtime)
        {
            if (runtime == null)
                return;

            if (!string.IsNullOrEmpty(runtime.CellId))
                _formationByCellId.Remove(runtime.CellId);

            if (string.IsNullOrEmpty(runtime.RootCellId) ||
                !_formationsByRootCellId.TryGetValue(
                    runtime.RootCellId,
                    out var formations))
                return;

            formations.Remove(runtime);
            if (formations.Count == 0)
                _formationsByRootCellId.Remove(runtime.RootCellId);
        }
    }
}
