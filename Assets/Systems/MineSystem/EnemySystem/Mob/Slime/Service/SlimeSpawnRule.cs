using System;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Service
{
    [Obsolete("EnemySpawnLocator now validates slime spawn requirements from SlimeConfigScriptable.")]
    public sealed class SlimeSpawnRule : IEnemySpawnRule
    {
        public EnemyType EnemyType => EnemyType.Slime;

        public bool IsValid(
            Cell cell,
            MineData mineData,
            EnemyConfigScriptable config,
            GridPosition playerPosition)
        {
            if (cell == null || !cell.IsRevealed || !cell.IsBroken)
                return false;
            var below = mineData.GetCell(new GridPosition(
                cell.Position.X,
                cell.Position.Y - 1));
            if (below == null || below.IsBroken || below.IsBlank)
                return false;
            var distance = Math.Abs(cell.Position.X - playerPosition.X) +
                           Math.Abs(cell.Position.Y - playerPosition.Y);
            return distance >= config.MinimumSpawnDistanceInTiles;
        }
    }
}
