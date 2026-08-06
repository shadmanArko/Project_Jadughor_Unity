using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Model
{
    public readonly struct EnemyInitializeData
    {
        public readonly EnemyConfigScriptable Config;
        public readonly GridPosition SpawnGridPosition;
        public readonly Vector3 SpawnWorldPosition;

        public EnemyInitializeData(
            EnemyConfigScriptable config,
            GridPosition spawnGridPosition,
            Vector3 spawnWorldPosition)
        {
            Config = config;
            SpawnGridPosition = spawnGridPosition;
            SpawnWorldPosition = spawnWorldPosition;
        }
    }
}
