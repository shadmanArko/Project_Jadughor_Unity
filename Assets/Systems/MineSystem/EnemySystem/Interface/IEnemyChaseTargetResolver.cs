using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyChaseTargetResolver
    {
        UniTask<PathResult> FindReachablePathAsync(
            Collider2D enemyCollider,
            GridPosition enemyPosition,
            GridPosition targetPosition,
            GridPosition preferredDestination,
            int attackRange,
            EnemyMovementType movementType,
            int maxFallDistanceInTiles,
            int generation,
            int routeVariant,
            bool prioritizePreferredDestination,
            CancellationToken cancellationToken);
    }
}
