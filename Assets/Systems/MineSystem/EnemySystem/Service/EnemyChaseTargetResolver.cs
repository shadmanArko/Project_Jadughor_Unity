using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyChaseTargetResolver : IEnemyChaseTargetResolver
    {
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyPlacementValidator _placement;

        public EnemyChaseTargetResolver(
            IEnemyPathfindingService pathfinding,
            IEnemyPlacementValidator placement)
        {
            _pathfinding = pathfinding;
            _placement = placement;
        }

        public UniTask<PathResult> FindReachablePathAsync(
            Collider2D enemyCollider,
            GridPosition enemyPosition,
            GridPosition targetPosition,
            int attackRange,
            int maxFallDistanceInTiles,
            int generation,
            CancellationToken cancellationToken)
        {
            var range = Math.Max(0, attackRange);
            var candidates = new List<GridPosition>(
                1 + range * (range + 1) * 2);
            if (IsCandidateValid(enemyCollider, targetPosition))
                candidates.Add(targetPosition);

            for (var y = -range; y <= range; y++)
            {
                var remainingX = range - Math.Abs(y);
                for (var x = -remainingX; x <= remainingX; x++)
                {
                    if (x == 0 && y == 0)
                        continue;
                    var candidate = new GridPosition(
                        targetPosition.X + x,
                        targetPosition.Y + y);
                    if (!IsCandidateValid(enemyCollider, candidate))
                        continue;
                    candidates.Add(candidate);
                }
            }

            if (candidates.Count == 0)
            {
                return UniTask.FromResult(PathResult.Failure(
                    targetPosition,
                    generation,
                    "No placement-valid chase destination exists."));
            }

            var request = new EnemyMultiTargetPathRequest(
                enemyPosition,
                targetPosition,
                candidates,
                maxFallDistanceInTiles,
                generation);
            return _pathfinding.FindPathToAnyAsync(request, cancellationToken);
        }

        private bool IsCandidateValid(
            Collider2D enemyCollider,
            GridPosition candidate) =>
            _pathfinding.IsWalkable(candidate) &&
            _placement.TryGetPlacement(enemyCollider, candidate, out _);
    }
}
