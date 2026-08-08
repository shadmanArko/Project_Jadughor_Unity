using System;

namespace Systems.MineSystem.EnemySystem.Signal
{
    /// <summary>
    /// Fired by an enemy state machine that has exhausted its own stuck
    /// recovery. <c>EnemyManager</c> answers it with a despawn + respawn near
    /// the player, so the relocation decision stays in one place instead of
    /// each mob inventing its own teleport.
    /// </summary>
    public readonly struct EnemyRelocationRequestedSignal
    {
        public readonly Guid EnemyId;

        public EnemyRelocationRequestedSignal(Guid enemyId)
        {
            EnemyId = enemyId;
        }
    }
}
