using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.Mine.Model;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Service
{
    public sealed class BatNavigationService
    {
        private readonly MineModel _mine;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyPlacementValidator _placement;

        public BatNavigationService(
            MineModel mine,
            IEnemyPathfindingService pathfinding,
            IEnemyPlacementValidator placement)
        {
            _mine = mine;
            _pathfinding = pathfinding;
            _placement = placement;
        }

        public bool TryFindPerch(
            GridPosition origin,
            Collider2D terrainCollider,
            int maximumRange,
            float ceilingClearance,
            out GridPosition perchCell,
            out Vector2 perchWorldPosition)
        {
            perchCell = default;
            perchWorldPosition = default;
            if (terrainCollider == null || maximumRange <= 0)
                return false;

            var selectedCount = 0;
            var positions = _mine.BrokenCellPositions;
            for (var i = 0; i < positions.Count; i++)
            {
                var candidate = positions[i];
                var distance = Mathf.Abs(candidate.X - origin.X) +
                               Mathf.Abs(candidate.Y - origin.Y);
                if (distance <= 0 || distance > maximumRange ||
                    !_pathfinding.IsFlyable(candidate))
                    continue;

                var abovePosition = new GridPosition(
                    candidate.X,
                    candidate.Y + 1);
                if (!_mine.TryGetCell(abovePosition, out var above) ||
                    above.IsBroken || above.IsBlank)
                    continue;

                var center = _placement.GridToWorld(candidate);
                var aboveCenter = _placement.GridToWorld(abovePosition);
                var cellHeight = Mathf.Abs(aboveCenter.y - center.y);
                var bodyPosition = terrainCollider.attachedRigidbody != null
                    ? terrainCollider.attachedRigidbody.position
                    : (Vector2)terrainCollider.transform.position;
                var colliderCenterOffset =
                    (Vector2)terrainCollider.bounds.center - bodyPosition;
                var inset = terrainCollider.bounds.extents.y +
                            colliderCenterOffset.y +
                            Mathf.Max(0f, ceilingClearance);
                var worldPosition = center +
                                    Vector2.up *
                                    (cellHeight * 0.5f - inset);
                if (!_placement.IsPlacementClear(
                        terrainCollider,
                        worldPosition))
                    continue;

                selectedCount++;
                if (Random.Range(0, selectedCount) != 0)
                    continue;
                perchCell = candidate;
                perchWorldPosition = worldPosition;
            }

            return selectedCount > 0;
        }
    }
}
