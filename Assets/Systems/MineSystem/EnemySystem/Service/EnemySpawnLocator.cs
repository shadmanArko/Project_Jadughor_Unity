using System.Collections.Generic;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemySpawnLocator
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly IEnemyTargetProvider _target;
        private readonly Camera _gameplayCamera;
        private readonly Dictionary<Enum.EnemyType, IEnemySpawnRule> _rules = new();

        public EnemySpawnLocator(
            MineModel mine,
            MineView mineView,
            IEnemyTargetProvider target,
            Camera gameplayCamera,
            List<IEnemySpawnRule> rules)
        {
            _mine = mine;
            _mineView = mineView;
            _target = target;
            _gameplayCamera = gameplayCamera;
            for (var i = 0; i < rules.Count; i++)
            {
                var rule = rules[i];
                if (rule != null)
                    _rules[rule.EnemyType] = rule;
            }
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

            if (!_rules.TryGetValue(request.Config.EnemyType, out var rule))
            {
                spawnData = default;
                error = $"No spawn rule is registered for {request.Config.EnemyType}.";
                return false;
            }

            var playerPosition = _target.GridPosition;
            if (request.PreferredPosition.HasValue)
            {
                var position = request.PreferredPosition.Value;
                var cell = mineData.GetCell(position);
                if (IsAvailable(position, request.OccupiedPositions) &&
                    rule.IsValid(cell, mineData, request.Config, playerPosition) &&
                    IsVisibilityValid(position, request))
                {
                    spawnData = Build(position);
                    error = null;
                    return true;
                }

                spawnData = default;
                error = $"Requested enemy spawn cell {position} is invalid.";
                return false;
            }

            Cell selected = null;
            var validCount = 0;
            for (var i = 0; i < mineData.Cells.Count; i++)
            {
                var candidate = mineData.Cells[i];
                if (!IsAvailable(candidate.Position, request.OccupiedPositions) ||
                    !rule.IsValid(candidate, mineData, request.Config, playerPosition) ||
                    !IsVisibilityValid(candidate.Position, request))
                    continue;

                validCount++;
                if (Random.Range(0, validCount) == 0)
                    selected = candidate;
            }

            if (selected == null)
            {
                spawnData = default;
                error = "No valid enemy spawn cell was found.";
                return false;
            }

            spawnData = Build(selected.Position);
            error = null;
            return true;
        }

        private GridPositionSpawnData Build(GridPosition position)
        {
            return new GridPositionSpawnData(
                position,
                _mineView.grid.GetCellCenterWorld(position.ToVector3Int()));
        }

        private bool IsVisibilityValid(
            GridPosition position,
            EnemySpawnRequest request)
        {
            if (request.VisibilityRule !=
                Enum.EnemySpawnVisibilityRule.OutsideCameraViewport)
                return true;
            if (_gameplayCamera == null || _mineView?.grid == null)
                return false;

            var world = _mineView.grid.GetCellCenterWorld(
                position.ToVector3Int());
            var viewport = _gameplayCamera.WorldToViewportPoint(world);
            var marginTiles = Mathf.Max(0, request.OutsideCameraMarginInTiles);
            var cellOrigin = _mineView.grid.CellToWorld(Vector3Int.zero);
            var cellRight = _mineView.grid.CellToWorld(Vector3Int.right);
            var cellUp = _mineView.grid.CellToWorld(Vector3Int.up);
            var cellWidth = Mathf.Abs(cellRight.x - cellOrigin.x);
            var cellHeight = Mathf.Abs(cellUp.y - cellOrigin.y);
            var verticalSize = Mathf.Max(
                0.0001f,
                _gameplayCamera.orthographicSize * 2f);
            var horizontalSize = Mathf.Max(
                0.0001f,
                verticalSize * _gameplayCamera.aspect);
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
    }
}
