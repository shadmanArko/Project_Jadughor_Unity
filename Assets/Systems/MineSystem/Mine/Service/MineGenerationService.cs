using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Enum;
using Systems.MineSystem.Mine.Model;
// using Random = UnityEngine.Random;

namespace Systems.MineSystem.Mine.Service
{
    [Serializable]
    public class MineGenerationService
    {
        public async UniTask<MineData> GenerateMineCellData(MineGenerationConfig config)
        {
            await UniTask.SwitchToThreadPool();

            var mineData = new MineData();
            var cells = new List<Cell>();
            var mineWidth = config.mineSizeX;
            var mineHeight = config.mineSizeY;
            for (var x = 0; x < mineWidth; x++)
            {
                for (var y = 0; y < mineHeight; y++)
                {
                    var cell = new Cell
                    {
                        Id = Guid.NewGuid().ToString(),
                        Position = new GridPosition(x - mineWidth / 2, -y)
                    };

                    if (y == 0 && x == mineWidth / 2)
                    {
                        CreateBlankCell(cell);
                        cells.Add(cell);
                        continue;
                    }

                    if (y == 1 && x == mineWidth / 2)
                    {
                        CreateBreakableCell(cell, 40, false);
                        cell.HitPoint = 0;
                        cell.IsBroken = true;
                        cell.IsRevealed = true;
                        cell.BrokenSides = BrokenEdges.Top;
                        cells.Add(cell);
                        continue;
                    }

                    if (y == 0 || y == mineHeight - 1 || 
                        x == 0 || x == mineWidth - 1)
                    {
                        CreateUnbreakableCell(cell);
                        cells.Add(cell);
                    }
                    else
                    {
                        // var hardCellPossibility = Random.value < 0.3f;
                        var hitPoint = 40;//hardCellPossibility ? config.hardCellHitPoint : config.regularCellHitPoint;
                        CreateBreakableCell(cell, hitPoint);
                        cells.Add(cell);
                    }
                }
            }

            RevealMineEntranceNeighbors(cells);

            mineData.Cells = cells;
            mineData.GridWidth = mineWidth;
            mineData.GridHeight = mineHeight;
            mineData.CellSize = config.cellSize;
            mineData.Caves = new List<Cave>();
            mineData.SpecialBackdropDatas = new List<SpecialBackdropData>();
            mineData.CellPlaceables = new List<CellPlaceable>();
            mineData.WallPlaceables = new List<WallPlaceable>();
            mineData.VineDatas = new List<VineData>();

            await UniTask.SwitchToMainThread();
            return mineData;
        }

        private static void RevealMineEntranceNeighbors(List<Cell> cells)
        {
            foreach (var cell in cells)
            {
                var x = cell.Position.X;
                var y = cell.Position.Y;
                if (Math.Abs(x) > 1 || y > 0 || y < -2 ||
                    (x == 0 && y == -1))
                    continue;

                cell.IsRevealed = true;
                if (x == 0 && y == 0)
                    cell.BrokenSides |= BrokenEdges.Bottom;
                else if (x == 1 && y == -1)
                    cell.BrokenSides |= BrokenEdges.Left;
                else if (x == -1 && y == -1)
                    cell.BrokenSides |= BrokenEdges.Right;
                else if (x == 0 && y == -2)
                    cell.BrokenSides |= BrokenEdges.Top;
                else if (x == 1 && y == 0)
                    cell.BrokenSides |= BrokenEdges.BottomLeftCorner;
                else if (x == -1 && y == 0)
                    cell.BrokenSides |= BrokenEdges.BottomRightCorner;
                else if (x == 1 && y == -2)
                    cell.BrokenSides |= BrokenEdges.TopLeftCorner;
                else if (x == -1 && y == -2)
                    cell.BrokenSides |= BrokenEdges.TopRightCorner;
            }
        }

        private static void CreateBlankCell(Cell cell)
        {
            cell.IsBreakable = false;
            cell.IsBroken = true;
            cell.IsBlank = true;
            cell.IsRevealed = true;
            
            cell.MaxHitPoint = 999999999;
            cell.HitPoint = 999999999;
        }

        private static void CreateUnbreakableCell(Cell cell)
        {
            cell.IsBreakable = false;
            cell.IsBroken = false;
            cell.IsBlank = false;
            cell.IsRevealed = true;
            
            cell.MaxHitPoint = 999999999;
            cell.HitPoint = 999999999;
        }

        private void CreateBreakableCell(Cell cell, int hitPoint, bool shouldAddEdges = true)
        {
            cell.IsBreakable = true;
            cell.IsBroken = false;
            cell.IsBlank = false;
            cell.IsRevealed = false;

            cell.MaxHitPoint = hitPoint;
            cell.HitPoint = hitPoint;
            
            // if(shouldAddEdges) GetRandomBrokenEdges(cell);
        }
        
        private static readonly Random _random = new();
        public static void GetRandomBrokenEdges(Cell cell, double probability = 0.5)
        {
            BrokenEdges result = BrokenEdges.Intact;

            // Get all individual values defined in the enum
            BrokenEdges[] allEdges = (BrokenEdges[])System.Enum.GetValues(typeof(BrokenEdges));

            foreach (var edge in allEdges)
            {
                // Skip the 'None' value
                if (edge == BrokenEdges.Intact) continue;

                // Roll the dice! If it passes the probability, add the flag
                if (_random.NextDouble() < probability)
                {
                    result |= edge; // Bitwise OR adds the flag
                }

                cell.BrokenSides = result;
            }
        }
    }
}
