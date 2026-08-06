using System;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.Config;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.Controller;
using Systems.MineSystem.EnemySystem.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.GreenSlime.Service
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
                    $"Slime spawn rejected: {exception.Message}");
                _pool.Release(entry.Controller);
                return null;
            }
        }

        public void Release(IEnemyController enemyController)
        {
            if (enemyController is SlimeController slime)
                _pool.Release(slime);
        }
    }
}
