using System.Collections.Generic;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Model
{
    public sealed class EnemyNavigationSnapshot
    {
        public readonly HashSet<GridPosition> OpenCells;
        public readonly HashSet<GridPosition> WalkableCells;

        public EnemyNavigationSnapshot(
            HashSet<GridPosition> openCells,
            HashSet<GridPosition> walkableCells)
        {
            OpenCells = openCells;
            WalkableCells = walkableCells;
        }
    }
}
