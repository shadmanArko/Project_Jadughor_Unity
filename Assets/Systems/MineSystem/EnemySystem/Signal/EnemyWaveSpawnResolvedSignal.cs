using System;

namespace Systems.MineSystem.EnemySystem.Signal
{
    public readonly struct EnemyWaveSpawnResolvedSignal
    {
        public readonly Guid RequestId;
        public readonly bool Succeeded;
        public readonly Guid EnemyId;
        public readonly string Error;

        public EnemyWaveSpawnResolvedSignal(
            Guid requestId,
            bool succeeded,
            Guid enemyId,
            string error)
        {
            RequestId = requestId;
            Succeeded = succeeded;
            EnemyId = enemyId;
            Error = error;
        }
    }
}
