using System;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Controller;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Service
{
    public sealed class BatFactory : IEnemyFactory
    {
        private readonly BatPool _pool;

        public BatFactory(BatPool pool)
        {
            _pool = pool;
        }

        public EnemyType EnemyType => EnemyType.Bat;

        public IEnemyController Create(
            EnemySpawnRequest request,
            GridPositionSpawnData spawnData)
        {
            if (request.Config is not BatConfigScriptable config)
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
                Debug.LogWarning($"Bat spawn rejected: {exception.Message}");
                _pool.Release(entry.Controller);
                return null;
            }
        }

        public void Release(IEnemyController enemyController)
        {
            if (enemyController is BatController bat)
                _pool.Release(bat);
        }
    }
}
