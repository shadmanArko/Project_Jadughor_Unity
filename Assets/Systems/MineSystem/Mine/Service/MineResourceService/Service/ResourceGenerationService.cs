using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Service.MineResourceService.Config;
using Systems.MineSystem.Mine.Service.MineResourceService.Model;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.MineResourceService.Service
{
    [Serializable]
    public class ResourceGenerationService : IDisposable
    {
        private static readonly System.Random Rand = new();

        public async UniTask GenerateResources(MineData mineData, ResourceGenerationConfig config)
        {
            // Move generation off the main thread to prevent UI freezing
            await UniTask.SwitchToThreadPool();

            // Guarantee O(1) lookups when retrieving adjacent cells
            var occupiedCellIds = new HashSet<string>();

            // Mark cells that are already occupied by artifacts or placeables
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

            // Gather all available valid cells for root node selection
            var validCells = new List<Cell>();
            foreach (var cell in mineData.Cells)
            {
                if (!string.IsNullOrEmpty(cell.CaveId) || 
                    cell.IsBroken || !cell.IsBreakable || 
                    cell.IsBlank || cell.HasVine || occupiedCellIds.Contains(cell.Id)) continue;
                
                validCells.Add(cell);
            }

            mineData.Resources ??= new List<Resource>();
            mineData.Resources.Clear();
            
            var numberOfRootNodes = Rand.Next(config.minRootNodes, config.maxRootNodes + 1);

            for (var i = 0; i < numberOfRootNodes; i++)
            {
                Cell rootCell = null;
                
                while (validCells.Count > 0)
                {
                    var cellIndex = Rand.Next(validCells.Count);
                    var candidate = validCells[cellIndex];
                    
                    validCells[cellIndex] = validCells[^1];
                    validCells.RemoveAt(validCells.Count - 1);

                    if (!occupiedCellIds.Contains(candidate.Id))
                    {
                        rootCell = candidate;
                        break;
                    }
                }
                
                if (rootCell == null) break;

                var resourceCells = new List<Cell> { rootCell };
                occupiedCellIds.Add(rootCell.Id);
                
                if (config.resourceGenDatas == null || config.resourceGenDatas.Count == 0)
                {
                    Debug.LogWarning("ResourceGenerationConfig has no resources defined!");
                    break;
                }

                var resourceData = config.resourceGenDatas[Rand.Next(config.resourceGenDatas.Count)];
                var rootNodeVariant = resourceData.id;
                var resourceBranches = Rand.Next(resourceData.minRange, resourceData.maxRange + 1);

                var currentBranchCell = rootCell;
                for (var j = 0; j < resourceBranches; j++)
                {
                    var tempAdjCell = GetRandomAdjacentCell(currentBranchCell, mineData, occupiedCellIds);
                    if (tempAdjCell == null) break; // Dead end, no valid adjacent cells
                    
                    resourceCells.Add(tempAdjCell);
                    occupiedCellIds.Add(tempAdjCell.Id);
                    Debug.LogWarning($"resource: {tempAdjCell.Id}, pos: {tempAdjCell.Position}");
                    
                    currentBranchCell = tempAdjCell;
                }
                
                foreach (var resourceCell in resourceCells)
                {
                    var resource = new Resource
                    {
                        Id = Guid.NewGuid().ToString(),
                        Variant = rootNodeVariant,
                        Name = rootNodeVariant,
                        Type = "Mineral",
                        Category = "Resource",
                        Position = resourceCell.Position,
                        CellId = resourceCell.Id
                    };
                    
                    mineData.Resources.Add(resource);
                    resourceCell.HasResource = true;
                    resourceCell.ItemId = resource.Variant;
                }
            }
            
            await UniTask.SwitchToMainThread();
        }

        private Cell GetRandomAdjacentCell(Cell currentCell, MineData mineData, HashSet<string> occupiedCellIds)
        {
            var adjPos = new[]
            {
                currentCell.GetPosition() + Vector3Int.up,
                currentCell.GetPosition() + Vector3Int.right,
                currentCell.GetPosition() + Vector3Int.down,
                currentCell.GetPosition() + Vector3Int.left
            };

            // Using array over list to avoid heap allocations in inner loop
            Cell[] validAdjCells = new Cell[4];
            int validCount = 0;

            foreach (var pos in adjPos)
            {
                var adjCell = mineData.GetCell(pos);
                if (adjCell != null && 
                    string.IsNullOrEmpty(adjCell.CaveId) && 
                    !adjCell.IsBroken && 
                    adjCell.IsBreakable && 
                    !adjCell.IsBlank &&
                    !adjCell.HasVine &&
                    !occupiedCellIds.Contains(adjCell.Id))
                {
                    validAdjCells[validCount++] = adjCell;
                }
            }

            if (validCount == 0) return null;

            return validAdjCells[Rand.Next(validCount)];
        }

        public void Dispose()
        {
            // Cleanup logic if needed
        }
    }
}
