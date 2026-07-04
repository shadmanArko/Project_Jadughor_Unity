using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Model;

namespace Systems.MineSystem.EnemySystem.Interface
{
    public interface IEnemyPathfindingService
    {
        UniTask<PathResult> FindPathAsync(
            EnemyPathRequest request,
            CancellationToken cancellationToken);
        bool IsWalkable(Systems.MineSystem.Mine.Model.GridPosition position);
    }
}
