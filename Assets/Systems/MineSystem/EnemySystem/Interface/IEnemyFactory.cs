using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Model;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyFactory
    {
        EnemyType EnemyType { get; }
        IEnemyController Create(
            EnemySpawnRequest request,
            GridPositionSpawnData spawnData);
        void Release(IEnemyController enemyController);
    }
}
