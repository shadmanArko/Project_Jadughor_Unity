using System;

namespace Systems.MineSystem.EnemySystem.Signal
{
    public readonly struct EnemyDiedSignal
    {
        public readonly Guid EnemyId;

        public EnemyDiedSignal(Guid enemyId)
        {
            EnemyId = enemyId;
        }
    }
}
