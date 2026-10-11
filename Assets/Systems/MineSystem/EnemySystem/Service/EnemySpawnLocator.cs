using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemySpawnLocator
    {
        private const int RejectOccupied = 1;
        private const int RejectNotBroken = 2;
        private const int RejectCellState = 3;
        private const int RejectDistance = 4;
        private const int RejectVisibility = 5;
        private const int RejectGround = 6;
        private const int RejectPath = 7;
        private const int RejectPlacement = 8;

        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly IEnemyTargetProvider _target;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly IEnemyPlacementValidator _placement;
        private readonly EnemySpawnCandidateService _candidateService;
        private readonly Camera _camera;

        public EnemySpawnLocator(
            MineModel mine,
            MineView mineView,
            IEnemyTargetProvider target,
            IEnemyPathfindingService pathfinding,
            IEnemyPlacementValidator placement,
            EnemySpawnCandidateService candidateService,
            Camera camera)
        {
            _mine = mine;
            _mineView = mineView;
            _target = target;
            _pathfinding = pathfinding;
            _placement = placement;
            _candidateService = candidateService;
            _camera = camera;
        }

        public bool TryLocate(
            EnemySpawnRequest request,
            out GridPositionSpawnData spawnData,
            out string error)
        {
            var mineData = _mine.MineData.Value;
            if (request.Config == null || mineData?.Cells == null ||
                _mineView == null || _mineView.grid == null)
            {
                spawnData = default;
                error = "Enemy spawning requires config, generated mine data, and mine grid.";
                return false;
            }

            var playerPosition = _target.GridPosition;
            var placementCollider = ResolvePlacementCollider(request.Config);
            if (request.PreferredPosition.HasValue)
            {
                var position = request.PreferredPosition.Value;
                if (IsPositionValid(
                        position,
                        request,
                        mineData,
                        playerPosition,
                        placementCollider,
                        out _))
                {
                    spawnData = Build(position);
                    error = null;
                    return true;
                }

                spawnData = default;
                error = $"Requested enemy spawn cell {position} is invalid.";
                return false;
            }

            GridPosition selected = default;
            var validCount = 0;
            var occupiedRejects = 0;
            var notBrokenRejects = 0;
            var cellStateRejects = 0;
            var distanceRejects = 0;
            var visibilityRejects = 0;
            var groundRejects = 0;
            var pathRejects = 0;
            var placementRejects = 0;
            if (request.Config.MaximumSpawnDistanceInTiles > 0)
            {
                var offsets = _candidateService.GetOffsets(
                    request.Config.MinimumSpawnDistanceInTiles,
                    request.Config.MaximumSpawnDistanceInTiles);
                for (var i = 0; i < offsets.Count; i++)
                {
                    var offset = offsets[i];
                    var position = new GridPosition(
                        playerPosition.X + offset.X,
                        playerPosition.Y + offset.Y);
                    if (!IsPositionValid(
                            position,
                            request,
                            mineData,
                            playerPosition,
                            placementCollider,
                            out var rejection))
                    {
                        CountRejection(
                            rejection,
                            ref occupiedRejects,
                            ref notBrokenRejects,
                            ref cellStateRejects,
                            ref distanceRejects,
                            ref visibilityRejects,
                            ref groundRejects,
                            ref pathRejects,
                            ref placementRejects);
                        continue;
                    }

                    validCount++;
                    if (Random.Range(0, validCount) == 0)
                        selected = position;
                }
            }
            else
            {
                var brokenPositions = _mine.BrokenCellPositions;
                for (var i = 0; i < brokenPositions.Count; i++)
                {
                    var position = brokenPositions[i];
                    if (!IsPositionValid(
                            position,
                            request,
                            mineData,
                            playerPosition,
                            placementCollider,
                            out var rejection))
                    {
                        CountRejection(
                            rejection,
                            ref occupiedRejects,
                            ref notBrokenRejects,
                            ref cellStateRejects,
                            ref distanceRejects,
                            ref visibilityRejects,
                            ref groundRejects,
                            ref pathRejects,
                            ref placementRejects);
                        continue;
                    }

                    validCount++;
                    if (Random.Range(0, validCount) == 0)
                        selected = position;
                }
            }

            if (validCount == 0)
            {
                spawnData = default;
                error =
                    "No valid enemy spawn cell was found. Rejections: " +
                    $"occupied={occupiedRejects}, " +
                    $"notBroken={notBrokenRejects}, " +
                    $"cellState={cellStateRejects}, " +
                    $"distance={distanceRejects}, " +
                    $"visibility={visibilityRejects}, " +
                    $"ground={groundRejects}, " +
                    $"path={pathRejects}, " +
                    $"placement={placementRejects}.";
                return false;
            }

            spawnData = Build(selected);
            error = null;
            return true;
        }

        private GridPositionSpawnData Build(GridPosition position)
        {
            return new GridPositionSpawnData(
                position,
                _mineView.grid.GetCellCenterWorld(position.ToVector3Int()));
        }

        private bool IsPositionValid(
            GridPosition position,
            EnemySpawnRequest request,
            MineData mineData,
            GridPosition playerPosition,
            Collider2D placementCollider,
            out int rejection)
        {
            rejection = 0;
            if (!IsAvailable(position, request.OccupiedPositions))
            {
                rejection = RejectOccupied;
                return false;
            }
            if (!_mine.IsBrokenCell(position))
            {
                rejection = RejectNotBroken;
                return false;
            }
            if (!_mine.TryGetCell(position, out var cell) ||
                !cell.IsRevealed ||
                !cell.IsBroken)
            {
                rejection = RejectCellState;
                return false;
            }
            if (!IsDistanceValid(
                    position,
                    playerPosition,
                    request.Config.MinimumSpawnDistanceInTiles,
                    request.Config.MaximumSpawnDistanceInTiles))
            {
                rejection = RejectDistance;
                return false;
            }
            if (!IsVisibilityValid(position, request))
            {
                rejection = RejectVisibility;
                return false;
            }

            if (request.Config.RequiresSolidGroundBelow &&
                !HasSolidGroundBelow(mineData, position))
            {
                rejection = RejectGround;
                return false;
            }

            if (request.Config.RequiresPathValidation &&
                !IsNavigationValid(position, request.Config.MovementType))
            {
                rejection = RejectPath;
                return false;
            }

            if (!request.Config.RequiresPlacementValidation)
                return true;

            var validPlacement = placementCollider != null &&
                                 _placement.TryGetPlacement(
                                     placementCollider,
                                     position,
                                     out _);
            if (validPlacement)
                return true;

            rejection = RejectPlacement;
            return false;
        }

        private static void CountRejection(
            int rejection,
            ref int occupiedRejects,
            ref int notBrokenRejects,
            ref int cellStateRejects,
            ref int distanceRejects,
            ref int visibilityRejects,
            ref int groundRejects,
            ref int pathRejects,
            ref int placementRejects)
        {
            switch (rejection)
            {
                case RejectOccupied:
                    occupiedRejects++;
                    break;
                case RejectNotBroken:
                    notBrokenRejects++;
                    break;
                case RejectCellState:
                    cellStateRejects++;
                    break;
                case RejectDistance:
                    distanceRejects++;
                    break;
                case RejectVisibility:
                    visibilityRejects++;
                    break;
                case RejectGround:
                    groundRejects++;
                    break;
                case RejectPath:
                    pathRejects++;
                    break;
                case RejectPlacement:
                    placementRejects++;
                    break;
            }
        }

        private static bool IsDistanceValid(
            GridPosition position,
            GridPosition playerPosition,
            int minimumDistance,
            int maximumDistance)
        {
            var distance = Distance(position, playerPosition);
            if (distance < minimumDistance)
                return false;
            return maximumDistance <= 0 || distance <= maximumDistance;
        }

        private static bool HasSolidGroundBelow(
            MineData mineData,
            GridPosition position)
        {
            var below = mineData.GetCell(new GridPosition(
                position.X,
                position.Y - 1));
            return below != null && !below.IsBroken && !below.IsBlank;
        }

        private static int Distance(GridPosition a, GridPosition b)
        {
            var x = a.X - b.X;
            var y = a.Y - b.Y;
            return (x < 0 ? -x : x) + (y < 0 ? -y : y);
        }

        private static Collider2D ResolvePlacementCollider(
            EnemyConfigScriptable config)
        {
            if (config?.Prefab == null || !config.RequiresPlacementValidation)
                return null;
            var colliders = config.Prefab.GetComponentsInChildren<Collider2D>(
                true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider != null && !collider.isTrigger)
                    return collider;
            }
            return null;
        }

        private bool IsVisibilityValid(
            GridPosition position,
            EnemySpawnRequest request)
        {
            if (request.VisibilityRule !=
                Enum.EnemySpawnVisibilityRule.OutsideCameraViewport)
                return true;
            if (_camera == null || _mineView?.grid == null)
                return false;

            var world = _mineView.grid.GetCellCenterWorld(
                position.ToVector3Int());
            var viewport = _camera.WorldToViewportPoint(world);
            var marginTiles = Mathf.Max(0, request.OutsideCameraMarginInTiles);
            var cellOrigin = _mineView.grid.CellToWorld(Vector3Int.zero);
            var cellRight = _mineView.grid.CellToWorld(Vector3Int.right);
            var cellUp = _mineView.grid.CellToWorld(Vector3Int.up);
            var cellWidth = Mathf.Abs(cellRight.x - cellOrigin.x);
            var cellHeight = Mathf.Abs(cellUp.y - cellOrigin.y);
            var verticalSize = Mathf.Max(
                0.0001f,
                _camera.orthographicSize * 2f);
            var horizontalSize = Mathf.Max(
                0.0001f,
                verticalSize * _camera.aspect);
            var marginX = marginTiles * cellWidth / horizontalSize;
            var marginY = marginTiles * cellHeight / verticalSize;
            var insideExpandedViewport =
                viewport.z > 0f &&
                viewport.x >= -marginX && viewport.x <= 1f + marginX &&
                viewport.y >= -marginY && viewport.y <= 1f + marginY;
            return !insideExpandedViewport;
        }

        private static bool IsAvailable(
            GridPosition position,
            IReadOnlyCollection<GridPosition> occupied)
        {
            if (occupied == null)
                return true;
            foreach (var candidate in occupied)
            {
                if (candidate == position)
                    return false;
            }
            return true;
        }

        private bool IsNavigationValid(
            GridPosition position,
            EnemyMovementType movementType) =>
            movementType == EnemyMovementType.Flying
                ? _pathfinding.IsFlyable(position)
                : _pathfinding.IsWalkable(position);
    }
}
