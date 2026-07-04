using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.Slime.Config;
using Systems.MineSystem.EnemySystem.Mob.Slime.Controller;
using Systems.MineSystem.EnemySystem.Model;

namespace Systems.MineSystem.EnemySystem.Mob.Slime.Service
{
    public sealed class SlimeFactory : IEnemyFactory
    {
        private readonly SlimePool _pool;

        public SlimeFactory(SlimePool pool)
        {
            _pool = pool;
        }

        public EnemyType EnemyType => EnemyType.Slime;

        public IEnemyController Create(
            EnemySpawnRequest request,
            GridPositionSpawnData spawnData)
        {
            if (request.Config is not SlimeConfigScriptable config)
                return null;
            var entry = _pool.Acquire(config);
            entry.Controller.Initialize(new EnemyInitializeData(
                config,
                spawnData.GridPosition,
                spawnData.WorldPosition));
            return entry.Controller;
        }

        public void Release(IEnemyController enemyController)
        {
            if (enemyController is SlimeController slime)
                _pool.Release(slime);
        }
    }
}
