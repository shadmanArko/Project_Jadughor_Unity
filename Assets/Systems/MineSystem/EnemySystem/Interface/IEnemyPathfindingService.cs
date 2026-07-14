using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using System;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyPathfindingService
    {
        int WalkableCount { get; }
        int NavigationRevision { get; }
        IObservable<GridPosition> NavigationChanged { get; }

        UniTask<PathResult> FindPathAsync(
            EnemyPathRequest request,
            CancellationToken cancellationToken);
        UniTask<PathResult> FindPathToAnyAsync(
            EnemyMultiTargetPathRequest request,
            CancellationToken cancellationToken);
        bool IsWalkable(GridPosition position);
        bool TryFindWalkableNear(
            GridPosition origin,
            int minimumDistance,
            int maximumDistance,
            int startOffset,
            out GridPosition position);
        bool TryFindAnyWalkable(int startOffset, out GridPosition position);
        bool TryFindFarthestDirectional(
            GridPosition origin,
            int direction,
            int maximumDistance,
            out GridPosition position);
    }
}
