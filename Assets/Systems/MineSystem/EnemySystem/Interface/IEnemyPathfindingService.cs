using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyPathfindingService
    {
        int WalkableCount { get; }
        int FlyableCount { get; }
        int NavigationRevision { get; }
        IObservable<GridPosition> NavigationChanged { get; }

        UniTask<PathResult> FindPathToAnyAsync(
            EnemyMultiTargetPathRequest request,
            CancellationToken cancellationToken);
        bool IsWalkable(GridPosition position);
        bool IsFlyable(GridPosition position);
        bool TryFindWalkableNear(
            GridPosition origin,
            int minimumDistance,
            int maximumDistance,
            int startOffset,
            out GridPosition position);
        bool TryFindAnyWalkable(int startOffset, out GridPosition position);
        bool TryFindFlyableNear(
            GridPosition origin,
            int minimumDistance,
            int maximumDistance,
            int startOffset,
            out GridPosition position);
        bool TryFindAnyFlyable(int startOffset, out GridPosition position);
        bool TryFindFallLanding(
            GridPosition origin,
            int direction,
            int maximumFallDistance,
            out GridPosition position);
    }
}
