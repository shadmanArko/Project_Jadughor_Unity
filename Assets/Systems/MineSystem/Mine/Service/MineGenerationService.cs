using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Mine.Config;
using Systems.MineSystem.Mine.Model;
using Random = UnityEngine.Random;

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
                        Position = new GridPosition(x, y)
                    };

                    if (y == 0 || y == mineWidth - 1 || 
                        x == 0 || x == mineHeight - 1)
                    {
                        if (y == 0 && x == mineWidth / 2)
                        {
                            CreateBlankCell(cell);
                            cells.Add(cell);
                            continue;
                        }

                        CreateUnbreakableCell(cell);
                        cells.Add(cell);
                    }
                    else
                    {
                        var hardCellPossibility = Random.value < 0.3f;
                        var hitPoint = hardCellPossibility ? config.hardCellHitPoint : config.regularCellHitPoint;
                        CreateBreakableCell(cell, hitPoint);
                        cells.Add(cell);
                        if (y == 1 && x == mineWidth / 2)
                            cell.IsRevealed = true;
                    }
                }
            }

            mineData.Cells = cells;
            mineData.Caves = new List<Cave>();
            mineData.SpecialBackdropDatas = new List<SpecialBackdropData>();
            mineData.CellPlaceables = new List<CellPlaceable>();
            mineData.WallPlaceables = new List<WallPlaceable>();
            mineData.VineDatas = new List<VineData>();

            await UniTask.SwitchToMainThread();
            return mineData;
        }

        private static void CreateBlankCell(Cell cell)
        {
            cell.IsBreakable = false;
            cell.IsBroken = false;
            cell.IsInstantiated = false;
            cell.MaxHitPoint = 999999999;
            cell.HitPoint = 999999999;
        }

        private static void CreateUnbreakableCell(Cell cell)
        {
            cell.IsBreakable = false;
            cell.IsBroken = false;
            cell.IsInstantiated = true;
            cell.MaxHitPoint = 999999999;
            cell.HitPoint = 999999999;
        }

        private void CreateBreakableCell(Cell cell, int hitPoint)
        {
            cell.IsBreakable = true;
            cell.IsBroken = false;
            cell.IsInstantiated = true;
            cell.IsRevealed = false;

            cell.MaxHitPoint = hitPoint;
            cell.HitPoint = hitPoint;
        }
    }
}