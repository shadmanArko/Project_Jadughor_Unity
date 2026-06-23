using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service
{
    public class VineGenerationService : IDisposable
    {
        private readonly System.Random _random = new();

        public async UniTask GenerateVines(MineData mineData, VineConfig config)
        {
            if (mineData?.Cells == null || config == null)
                return;

            var minLength = config.MinGroupLength;
            var maxLength = config.MaxGroupLength;
            var minGroups = config.MinGroupsPerMine;
            var maxGroups = config.MaxGroupsPerMine;
            var vineTypes = config.VineTypes?
                .Where(type => type != null && !string.IsNullOrWhiteSpace(type.Id))
                .Select(type => new WeightedVineType(type.Id, type.GenerationWeight))
                .ToList();

            await UniTask.SwitchToThreadPool();

            mineData.VineDatas ??= new List<VineData>();
            mineData.VineDatas.Clear();

            if (vineTypes == null || vineTypes.Count == 0 || maxGroups <= 0)
            {
                await UniTask.SwitchToMainThread();
                return;
            }

            var occupiedCellIds = BuildOccupiedCellIds(mineData);
            var candidateRoots = mineData.Cells
                .Where(cell => IsValidVineCell(cell, occupiedCellIds))
                .ToList();

            Shuffle(candidateRoots);

            var groupCount = _random.Next(minGroups, maxGroups + 1);
            for (var groupIndex = 0; groupIndex < groupCount && candidateRoots.Count > 0; groupIndex++)
            {
                var length = _random.Next(minLength, maxLength + 1);
                if (!TryFindVerticalGroup(
                        mineData,
                        candidateRoots,
                        occupiedCellIds,
                        length,
                        out var vineCells))
                    break;

                var sourceId = PickVineSourceId(vineTypes);
                var vineData = new VineData
                {
                    SourceId = sourceId,
                    VineCellIds = vineCells.Select(cell => cell.Id).ToList()
                };

                foreach (var cell in vineCells)
                {
                    cell.HasVine = true;
                    occupiedCellIds.Add(cell.Id);
                }

                mineData.VineDatas.Add(vineData);
            }

            await UniTask.SwitchToMainThread();
        }

        private static HashSet<string> BuildOccupiedCellIds(MineData mineData)
        {
            var occupiedCellIds = new HashSet<string>();

            if (mineData.Resources != null)
            {
                foreach (var resource in mineData.Resources)
                {
                    if (!string.IsNullOrEmpty(resource.CellId))
                        occupiedCellIds.Add(resource.CellId);
                }
            }

            if (mineData.ArtifactPlacements != null)
            {
                foreach (var placement in mineData.ArtifactPlacements)
                {
                    if (!string.IsNullOrEmpty(placement.CellId))
                        occupiedCellIds.Add(placement.CellId);
                }
            }

            if (mineData.CellPlaceables != null)
            {
                foreach (var placeable in mineData.CellPlaceables)
                {
                    if (!string.IsNullOrEmpty(placeable.OccupiedCellId))
                        occupiedCellIds.Add(placeable.OccupiedCellId);
                }
            }

            if (mineData.WallPlaceables != null)
            {
                foreach (var placeable in mineData.WallPlaceables)
                {
                    if (placeable.OccupiedCellIds == null)
                        continue;

                    foreach (var cellId in placeable.OccupiedCellIds)
                    {
                        if (!string.IsNullOrEmpty(cellId))
                            occupiedCellIds.Add(cellId);
                    }
                }
            }

            if (mineData.VineDatas != null)
            {
                foreach (var vine in mineData.VineDatas)
                {
                    if (vine?.VineCellIds == null)
                        continue;

                    foreach (var cellId in vine.VineCellIds)
                    {
                        if (!string.IsNullOrEmpty(cellId))
                            occupiedCellIds.Add(cellId);
                    }
                }
            }

            return occupiedCellIds;
        }

        private bool TryFindVerticalGroup(
            MineData mineData,
            List<Cell> candidateRoots,
            HashSet<string> occupiedCellIds,
            int length,
            out List<Cell> vineCells)
        {
            vineCells = null;

            for (var i = candidateRoots.Count - 1; i >= 0; i--)
            {
                var rootIndex = _random.Next(candidateRoots.Count);
                var root = candidateRoots[rootIndex];
                candidateRoots[rootIndex] = candidateRoots[^1];
                candidateRoots.RemoveAt(candidateRoots.Count - 1);

                var cells = new List<Cell>(length);
                var isValid = true;
                for (var offset = 0; offset < length; offset++)
                {
                    var position = root.GetPosition() + Vector3Int.down * offset;
                    var cell = mineData.GetCell(position);
                    if (!IsValidVineCell(cell, occupiedCellIds))
                    {
                        isValid = false;
                        break;
                    }

                    cells.Add(cell);
                }

                if (!isValid)
                    continue;

                vineCells = cells;
                return true;
            }

            return false;
        }

        private static bool IsValidVineCell(Cell cell, HashSet<string> occupiedCellIds)
        {
            return cell != null &&
                   string.IsNullOrEmpty(cell.CaveId) &&
                   !cell.IsBlank &&
                   !cell.IsBroken &&
                   cell.IsBreakable &&
                   !cell.HasResource &&
                   !cell.HasArtifact &&
                   !cell.HasVine &&
                   !cell.HasCellPlaceable &&
                   !cell.HasWallPlaceable &&
                   !occupiedCellIds.Contains(cell.Id);
        }

        private string PickVineSourceId(IReadOnlyList<WeightedVineType> vineTypes)
        {
            var totalWeight = vineTypes.Sum(type => type.GenerationWeight);
            if (totalWeight <= 0)
                return vineTypes[0].Id;

            var roll = _random.Next(0, totalWeight);
            var runningTotal = 0;
            foreach (var vineType in vineTypes)
            {
                runningTotal += vineType.GenerationWeight;
                if (roll < runningTotal)
                    return vineType.Id;
            }

            return vineTypes[^1].Id;
        }

        private void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        public void Dispose()
        {
            
        }

        private readonly struct WeightedVineType
        {
            public WeightedVineType(string id, int generationWeight)
            {
                Id = id;
                GenerationWeight = Math.Max(0, generationWeight);
            }

            public string Id { get; }
            public int GenerationWeight { get; }
        }
    }
}
