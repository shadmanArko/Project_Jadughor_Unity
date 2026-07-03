using System.Collections.Generic;
using Systems.MineSystem.Damage;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.ToolbarSystem.Interface;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class CellDamageService : ICellDamageService
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly IPlaceableRuntimeResolver _placeables;
        private readonly List<Collider2D> _overlapResults = new(16);

        public CellDamageService(
            MineModel mine,
            MineView mineView,
            IPlaceableRuntimeResolver placeables)
        {
            _mine = mine;
            _mineView = mineView;
            _placeables = placeables;
        }

        public void ApplyCellImpact(
            Vector3Int cellPosition,
            int wallDamage,
            float objectDamage,
            float overlapRadius,
            LayerMask targetLayers,
            HashSet<IDamageable> damaged)
        {
            var cell = _mine.MineData.Value?.GetCell(cellPosition);
            if (cell != null &&
                wallDamage > 0 &&
                cell.IsBreakable &&
                !cell.IsBroken)
                _mine.TryHitCell(cellPosition, wallDamage);

            if (_placeables.TryResolve(cellPosition, out var runtime))
                TryDamage(runtime.DamageView, objectDamage, damaged);

            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = targetLayers,
                useTriggers = true
            };
            _overlapResults.Clear();
            Physics2D.OverlapCircle(
                _mineView.grid.GetCellCenterWorld(cellPosition),
                overlapRadius,
                filter,
                _overlapResults);

            foreach (var collider in _overlapResults)
            {
                if (collider == null)
                    continue;

                foreach (var behaviour in
                         collider.GetComponentsInParent<MonoBehaviour>())
                {
                    if (behaviour is IDamageable damageable)
                        TryDamage(damageable, objectDamage, damaged);
                }
            }
        }

        private static void TryDamage(
            IDamageable damageable,
            float amount,
            HashSet<IDamageable> damaged)
        {
            if (damageable == null ||
                amount <= 0f ||
                !damaged.Add(damageable))
                return;

            damageable.ApplyDamage(amount);
        }
    }
}
