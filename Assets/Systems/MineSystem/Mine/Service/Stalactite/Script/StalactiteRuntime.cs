using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Damage;
using Systems.MineSystem.Mine.Service.VisualizerService;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.Stalactite.Script
{
    public sealed class StalactiteRuntime : CaveFormationRuntime
    {
        private readonly List<Collider2D> _overlapResults = new(16);
        private readonly HashSet<IDamageable> _damaged = new();

        protected override async UniTask BreakAsync(
            CancellationToken cancellationToken)
        {
            if (!TryBeginBreak())
                return;

            await PlayStateAsync(
                Config.collapseState,
                Config.collapseDuration,
                cancellationToken);
            await FallAsync(cancellationToken);
            await PlayStateAsync(
                Config.shatterState,
                Config.shatterDuration,
                cancellationToken);
            FinishBreak();
        }

        private async UniTask FallAsync(CancellationToken cancellationToken)
        {
            if (Animator != null &&
                !string.IsNullOrWhiteSpace(Config.fallState))
                Animator.Play(Config.fallState, 0, 0f);

            var startY = transform.position.y;
            var lastCell = CellPosition;

            while (!cancellationToken.IsCancellationRequested &&
                   startY - transform.position.y <
                   Config.stalactiteMaxFallDistance)
            {
                var next = transform.position;
                next.y -= Config.stalactiteFallSpeed * Time.deltaTime;
                transform.position = next;

                var currentCell =
                    MineView.grid.WorldToCell(transform.position);
                if (currentCell != lastCell)
                {
                    lastCell = currentCell;
                    if (TryImpactCell(currentCell))
                        return;
                }

                await UniTask.Yield(cancellationToken);
            }
        }

        private bool TryImpactCell(Vector3Int cellPosition)
        {
            var didDamage = DamageAtCell(cellPosition);
            if (didDamage)
                return true;

            var cell = MineData.GetCell(cellPosition);
            return cell == null ||
                   (!cell.IsBroken && !cell.IsBlank);
        }

        private bool DamageAtCell(Vector3Int cellPosition)
        {
            var filter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = Config.stalactiteTargetLayers,
                useTriggers = true
            };

            _overlapResults.Clear();
            _damaged.Clear();
            Physics2D.OverlapCircle(
                MineView.grid.GetCellCenterWorld(cellPosition),
                Config.stalactiteImpactRadius,
                filter,
                _overlapResults);

            foreach (var collider in _overlapResults)
            {
                if (collider == null ||
                    collider.transform.IsChildOf(transform))
                    continue;

                foreach (var behaviour in
                         collider.GetComponentsInParent<MonoBehaviour>())
                {
                    if (behaviour is not IDamageable damageable ||
                        ReferenceEquals(damageable, DamageView) ||
                        !_damaged.Add(damageable))
                        continue;

                    damageable.ApplyDamage(Config.stalactiteDamage);
                }
            }

            return _damaged.Count > 0;
        }
    }
}
