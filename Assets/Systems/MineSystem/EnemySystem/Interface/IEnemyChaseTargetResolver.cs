using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyChaseTargetResolver
    {
        bool TryResolve(
            Collider2D enemyCollider,
            GridPosition enemyPosition,
            GridPosition targetPosition,
            int attackRange,
            out GridPosition destination);
    }
}
