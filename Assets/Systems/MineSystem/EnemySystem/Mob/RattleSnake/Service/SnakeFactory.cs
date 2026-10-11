using System;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Config;
using Systems.MineSystem.EnemySystem.Mob.RattleSnake.Controller;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.RattleSnake.Service
{
    public sealed class SnakeFactory : IEnemyFactory
    {
        private readonly SnakePool _pool;

        public SnakeFactory(SnakePool pool)
        {
            _pool = pool;
        }

        public EnemyType EnemyType => EnemyType.RattleSnake;

        public IEnemyController Create(
            EnemySpawnRequest request,
            GridPositionSpawnData spawnData)
        {
            if (request.Config is not SnakeConfigScriptable config)
                return null;
            var entry = _pool.Acquire(config);
            try
            {
                entry.Controller.Initialize(new EnemyInitializeData(
                    config,
                    spawnData.GridPosition,
                    spawnData.WorldPosition));
                return entry.Controller;
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    $"Snake spawn rejected: {exception.Message}");
                _pool.Release(entry.Controller);
                return null;
            }
        }

        public void Release(IEnemyController enemyController)
        {
            if (enemyController is SnakeController snake)
                _pool.Release(snake);
        }
    }
}
