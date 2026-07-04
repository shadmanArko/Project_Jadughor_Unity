using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemySpawnRule
    {
        EnemyType EnemyType { get; }
        bool IsValid(
            Cell cell,
            MineData mineData,
            EnemyConfigScriptable config,
            GridPosition playerPosition);
    }
}
