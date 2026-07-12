using System;
using Systems.MineSystem.EnemySystem.Interface;
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

        public bool TryResolve(
            Collider2D enemyCollider,
            GridPosition enemyPosition,
            GridPosition targetPosition,
            int attackRange,
            out GridPosition destination)
        {
            destination = default;
            var range = Math.Max(0, attackRange);
            if (IsCandidateValid(enemyCollider, targetPosition))
            {
                destination = targetPosition;
                return true;
            }

            var found = false;
            var bestScore = int.MaxValue;
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
                    var score = Distance(enemyPosition, candidate);
                    if (found && score >= bestScore)
                        continue;
                    destination = candidate;
                    bestScore = score;
                    found = true;
                }
            }
            return found;
        }

        private bool IsCandidateValid(
            Collider2D enemyCollider,
            GridPosition candidate) =>
            _pathfinding.IsWalkable(candidate) &&
            _placement.TryGetPlacement(enemyCollider, candidate, out _);

        private static int Distance(GridPosition a, GridPosition b) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
    }
}
