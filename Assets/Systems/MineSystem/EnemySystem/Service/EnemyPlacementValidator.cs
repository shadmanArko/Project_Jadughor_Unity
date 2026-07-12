using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyPlacementValidator : IEnemyPlacementValidator
    {
        private const float MinimumQuerySize = 0.001f;
        private const float PlacementQuerySkin = 0.01f;
        private const float CellBoundaryInset = 0.001f;

        private readonly MineModel _mine;
        private readonly MineView _mineView;

        public EnemyPlacementValidator(MineModel mine, MineView mineView)
        {
            _mine = mine;
            _mineView = mineView;
        }

        public bool TryGetPlacement(
            Collider2D terrainCollider,
            GridPosition position,
            out Vector2 worldPosition)
        {
            worldPosition = default;
            if (!CanValidate(terrainCollider))
                return false;

            worldPosition = _mineView.grid.GetCellCenterWorld(
                position.ToVector3Int());
            return IsPlacementClear(terrainCollider, worldPosition);
        }

        public bool IsPlacementClear(
            Collider2D terrainCollider,
            Vector2 worldPosition)
        {
            if (!CanValidate(terrainCollider))
                return false;

            var bodyPosition = GetBodyPosition(terrainCollider);
            var offset = worldPosition - bodyPosition;
            var bounds = terrainCollider.bounds;
            var center = (Vector2)bounds.center + offset;
            var size = new Vector2(
                Mathf.Max(
                    MinimumQuerySize,
                    bounds.size.x - PlacementQuerySkin * 2f),
                Mathf.Max(
                    MinimumQuerySize,
                    bounds.size.y - PlacementQuerySkin * 2f));
            return IsGridFootprintClear(center, size);
        }

        public bool IsCurrentPlacementClear(Collider2D terrainCollider) =>
            CanValidate(terrainCollider) &&
            IsPlacementClear(terrainCollider, GetBodyPosition(terrainCollider));

        public GridPosition WorldToGrid(Vector2 worldPosition)
        {
            if (_mineView?.grid == null)
                return default;
            var cell = _mineView.grid.WorldToCell(worldPosition);
            return new GridPosition(cell.x, cell.y);
        }

        private bool CanValidate(Collider2D terrainCollider) =>
            terrainCollider != null &&
            !terrainCollider.isTrigger &&
            _mine != null &&
            _mineView != null &&
            _mineView.grid != null;

        private bool IsGridFootprintClear(Vector2 center, Vector2 size)
        {
            var halfSize = size * 0.5f;
            var min = center - halfSize + Vector2.one * CellBoundaryInset;
            var max = center + halfSize - Vector2.one * CellBoundaryInset;
            var minCell = _mineView.grid.WorldToCell(min);
            var maxCell = _mineView.grid.WorldToCell(max);
            var minX = Mathf.Min(minCell.x, maxCell.x);
            var maxX = Mathf.Max(minCell.x, maxCell.x);
            var minY = Mathf.Min(minCell.y, maxCell.y);
            var maxY = Mathf.Max(minCell.y, maxCell.y);
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    if (!_mine.TryGetCell(
                            new GridPosition(x, y),
                            out var cell) ||
                        !IsOpen(cell))
                        return false;
                }
            }

            return true;
        }

        private static bool IsOpen(Cell cell) =>
            cell != null && (cell.IsBroken || cell.IsBlank);

        private static Vector2 GetBodyPosition(Collider2D collider)
        {
            if (collider.attachedRigidbody != null)
                return collider.attachedRigidbody.position;
            return collider.transform.position;
        }
    }
}
