using System.Collections.Generic;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Registers synthetic <see cref="Cell"/> records for the lair's interior
    /// footprint so <c>MineData.GetCell</c> resolves lair positions - which
    /// is all the toolbar's placement validators need to allow cell/wall
    /// placeables inside the lair, with no changes to those validators.
    /// </summary>
    /// <remarks>
    /// Only covers <see cref="BossLairPlacement.InteriorCells"/>, never the
    /// border/shell ring - the shell is deliberately unbreakable because it
    /// has no <c>Cell</c> record (see <c>BossLairShellGenerationService</c>),
    /// and registering it here would make it breakable via
    /// <c>MineModel.TryHitCell</c>.
    /// </remarks>
    public sealed class BossLairCellGridService
    {
        private readonly MineModel _mine;
        private readonly List<Vector3Int> _registeredPositions = new();

        public BossLairCellGridService(MineModel mine)
        {
            _mine = mine;
        }

        public void Generate(BossLairPlacement placement)
        {
            Clear();
            if (!placement.IsValid)
                return;

            var data = _mine.MineData.Value;
            if (data == null)
                return;

            var bounds = placement.InteriorCells;
            var cells = new List<Cell>(bounds.size.x * bounds.size.y);
            for (var x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (var y = bounds.yMin; y < bounds.yMax; y++)
                {
                    var position = new Vector3Int(x, y, 0);
                    cells.Add(new Cell
                    {
                        Id = $"bosslair_{x}_{y}",
                        Position = new GridPosition(x, y),
                        IsRevealed = true,
                        IsBroken = true,
                        IsBreakable = false,
                        IsBlank = false,
                        HasVine = false,
                        HasCellPlaceable = false,
                        HasWallPlaceable = false
                    });
                    _registeredPositions.Add(position);
                }
            }

            data.RegisterAuxiliaryCells(cells);
        }

        public void Clear()
        {
            if (_registeredPositions.Count == 0)
                return;
            _mine.MineData.Value?.ClearAuxiliaryCells(_registeredPositions);
            _registeredPositions.Clear();
        }
    }
}
