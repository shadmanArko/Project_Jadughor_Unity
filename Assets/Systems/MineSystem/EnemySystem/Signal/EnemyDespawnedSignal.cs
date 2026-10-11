using System;

namespace Systems.MineSystem.EnemySystem.Signal
{
    public readonly struct EnemyDespawnedSignal
    {
        public readonly Guid EnemyId;

        public EnemyDespawnedSignal(Guid enemyId)
        {
            EnemyId = enemyId;
        }
    }
}
