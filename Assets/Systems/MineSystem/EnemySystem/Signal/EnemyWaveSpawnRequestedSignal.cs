using System;
using Systems.MineSystem.EnemySystem.Config;

namespace Systems.MineSystem.EnemySystem.Signal
{
    public readonly struct EnemyWaveSpawnRequestedSignal
    {
        public readonly Guid RequestId;
        public readonly EnemyConfigScriptable EnemyConfig;
        public readonly int OutsideCameraMarginInTiles;

        public EnemyWaveSpawnRequestedSignal(
            Guid requestId,
            EnemyConfigScriptable enemyConfig,
            int outsideCameraMarginInTiles)
        {
            RequestId = requestId;
            EnemyConfig = enemyConfig;
            OutsideCameraMarginInTiles = outsideCameraMarginInTiles;
        }
    }
}
