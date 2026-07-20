using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public sealed class EnemyNavigationSnapshot
    {
        public readonly HashSet<GridPosition> OpenCells;
        public readonly IReadOnlyList<GridPosition> OpenPositions;
        public readonly HashSet<GridPosition> WalkableCells;
        public readonly IReadOnlyList<GridPosition> WalkablePositions;

        public EnemyNavigationSnapshot(
            HashSet<GridPosition> openCells,
            HashSet<GridPosition> walkableCells)
        {
            OpenCells = openCells;
            OpenPositions = new List<GridPosition>(openCells);
            WalkableCells = walkableCells;
            WalkablePositions = new List<GridPosition>(walkableCells);
        }
    }
}
