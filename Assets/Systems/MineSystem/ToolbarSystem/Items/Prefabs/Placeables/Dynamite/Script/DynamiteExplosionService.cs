using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Damage;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.Utilities.ScreenShake;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class DynamiteExplosionService : IDisposable
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly ExplosionSmokePool _smokePool;
        private readonly ICellDamageService _cellDamage;
        private readonly CancellationTokenSource _lifetime = new();

        public DynamiteExplosionService(
            MineModel mine,
            MineView mineView,
            ExplosionSmokePool smokePool,
            ICellDamageService cellDamage)
        {
            _mine = mine;
            _mineView = mineView;
            _smokePool = smokePool;
            _cellDamage = cellDamage;
        }

        public void Detonate(
            PlaceableSpawnContext context,
            DynamiteConfig config)
        {
            RunExplosionAsync(
                    context,
                    config,
                    _lifetime.Token)
                .Forget(exception =>
                {
                    if (exception is not OperationCanceledException)
                        Debug.LogException(exception);
                });
        }

        private async UniTask RunExplosionAsync(
            PlaceableSpawnContext context,
            DynamiteConfig config,
            CancellationToken cancellationToken)
        {
            var damaged = new HashSet<IDamageable>();
            var center = context.CellPosition;

            await RunStageAsync(
                new[] { center },
                config,
                damaged,
                cancellationToken);
            await WaitBetweenStagesAsync(config, cancellationToken);

            if (config.BlastPattern == DynamiteBlastPattern.AdjacentEight)
            {
                await RunStageAsync(
                    CollectParticipatingCells(
                        center + new Vector3Int(-1, -1, 0),
                        center + Vector3Int.down,
                        center + new Vector3Int(1, -1, 0),
                        center + Vector3Int.left,
                        center + Vector3Int.right,
                        center + new Vector3Int(-1, 1, 0),
                        center + Vector3Int.up,
                        center + new Vector3Int(1, 1, 0)),
                    config,
                    damaged,
                    cancellationToken);
                return;
            }

            await RunStageAsync(
                CollectParticipatingCells(
                    center + Vector3Int.left,
                    center + Vector3Int.right),
                config,
                damaged,
                cancellationToken);
            await WaitBetweenStagesAsync(config, cancellationToken);

            await RunStageAsync(
                CollectParticipatingCells(
                    center + Vector3Int.up,
                    center + Vector3Int.down),
                config,
                damaged,
                cancellationToken);
        }

        private static UniTask WaitBetweenStagesAsync(
            DynamiteConfig config,
            CancellationToken cancellationToken)
        {
            return config.DelayBetweenStages <= 0f
                ? UniTask.CompletedTask
                : UniTask.Delay(
                    TimeSpan.FromSeconds(config.DelayBetweenStages),
                    cancellationToken: cancellationToken);
        }

        private Vector3Int[] CollectParticipatingCells(
            params Vector3Int[] candidates)
        {
            var positions = new List<Vector3Int>(candidates.Length);
            foreach (var candidate in candidates)
            {
                if (CanParticipate(candidate))
                    positions.Add(candidate);
            }
            return positions.ToArray();
        }

        private bool CanParticipate(Vector3Int position)
        {
            var cell = _mine.MineData.Value?.GetCell(position);
            return cell != null &&
                   (cell.IsBroken ||
                    cell.IsBlank ||
                    cell.IsBreakable);
        }

        private async UniTask RunStageAsync(
            IReadOnlyList<Vector3Int> positions,
            DynamiteConfig config,
            HashSet<IDamageable> damaged,
            CancellationToken cancellationToken)
        {
            if (positions.Count == 0)
                return;

            var shakeTriggered = false;
            var impacts = new UniTask[positions.Count];

            for (var index = 0; index < positions.Count; index++)
            {
                var position = positions[index];
                var smoke = _smokePool.Spawn();
                var impactReached = new UniTaskCompletionSource();
                var worldPosition =
                    _mineView.grid.GetCellCenterWorld(position);

                impacts[index] = impactReached.Task;
                PlaySmokeAsync(
                        smoke,
                        worldPosition,
                        () =>
                        {
                            ApplyImpact(
                                position,
                                config,
                                damaged);

                            if (shakeTriggered)
                                return;

                            shakeTriggered = true;
                            ScreenShakeController.RandomShake(
                                config.ShakeDuration,
                                config.ShakeStrength);
                        },
                        impactReached,
                        config,
                        cancellationToken)
                    .Forget();
            }

            await UniTask.WhenAll(impacts);
        }

        private async UniTask PlaySmokeAsync(
            ExplosionSmokeView smoke,
            Vector3 worldPosition,
            Action impact,
            UniTaskCompletionSource impactReached,
            DynamiteConfig config,
            CancellationToken cancellationToken)
        {
            try
            {
                await smoke.PlayAsync(
                    worldPosition,
                    config,
                    () =>
                    {
                        try
                        {
                            impact?.Invoke();
                        }
                        finally
                        {
                            impactReached.TrySetResult();
                        }
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
            {
                impactReached.TrySetCanceled(cancellationToken);
            }
            catch (Exception exception)
            {
                impactReached.TrySetException(exception);
                Debug.LogException(exception);
            }
            finally
            {
                _smokePool.Despawn(smoke);
            }
        }

        private void ApplyImpact(
            Vector3Int cellPosition,
            DynamiteConfig config,
            HashSet<IDamageable> damaged)
        {
            _cellDamage.ApplyCellImpact(
                cellPosition,
                config.WallDamage,
                config.ObjectDamage,
                config.OverlapRadius,
                config.TargetLayers,
                damaged);
        }

        public void Dispose()
        {
            if (!_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
