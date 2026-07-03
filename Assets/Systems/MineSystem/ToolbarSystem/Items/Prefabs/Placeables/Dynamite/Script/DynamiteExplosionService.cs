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
using DG.Tweening;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class DynamiteExplosionService :
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly ExplosionSmokePool _smokePool;
        private readonly ICellDamageService _cellDamage;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly List<ExplosionSmokeView> _activeSmoke = new();
        private Tween _stageDelay;
        private bool _stageDelayWasPlaying;
        private bool _isAffectedByPause = true;
        private bool _isPaused;
        private bool _disposed;

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value) return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(new PausableAffectationChangedSignal(this));
            }
        }

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

        public void Initialize() =>
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));

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

            if (config.BlastPattern == DynamiteBlastPattern.AreaFiveByFive)
            {
                await RunStageAsync(
                    CollectSquareArea(center, 2),
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

        private UniTask WaitBetweenStagesAsync(
            DynamiteConfig config,
            CancellationToken cancellationToken)
        {
            return config.DelayBetweenStages <= 0f
                ? UniTask.CompletedTask
                : AwaitStageDelayAsync(
                    config.DelayBetweenStages,
                    cancellationToken);
        }

        private async UniTask AwaitStageDelayAsync(
            float seconds,
            CancellationToken cancellationToken)
        {
            var tween = DOVirtual.DelayedCall(seconds, () => { }, false);
            _stageDelay = tween;
            if (_isPaused) tween.Pause();
            var completion = new UniTaskCompletionSource();
            var finished = false;
            CancellationTokenRegistration registration = default;
            tween.OnComplete(() =>
            {
                if (finished) return;
                finished = true;
                registration.Dispose();
                completion.TrySetResult();
            });
            tween.OnKill(() =>
            {
                if (finished) return;
                finished = true;
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else completion.TrySetResult();
            });
            registration = cancellationToken.Register(() => tween.Kill());
            await completion.Task;
            if (ReferenceEquals(_stageDelay, tween)) _stageDelay = null;
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

        private Vector3Int[] CollectSquareArea(
            Vector3Int center,
            int radius)
        {
            var positions = new List<Vector3Int>(
                (radius * 2 + 1) * (radius * 2 + 1) - 1);

            for (var y = -radius; y <= radius; y++)
            {
                for (var x = -radius; x <= radius; x++)
                {
                    if (x == 0 && y == 0)
                        continue;

                    var position =
                        center + new Vector3Int(x, y, 0);
                    if (CanParticipate(position))
                        positions.Add(position);
                }
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
                _activeSmoke.Add(smoke);
                if (_isPaused) smoke.PausePlayback();
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
                _activeSmoke.Remove(smoke);
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
            if (_disposed) return;
            _disposed = true;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            if (!_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _lifetime.Dispose();
        }

        public void OnPause()
        {
            if (_isPaused) return;
            _isPaused = true;
            _stageDelayWasPlaying = _stageDelay != null &&
                                    _stageDelay.IsActive() &&
                                    _stageDelay.IsPlaying();
            if (_stageDelayWasPlaying) _stageDelay.Pause();
            for (var i = 0; i < _activeSmoke.Count; i++)
                _activeSmoke[i].PausePlayback();
        }

        public void OnUnpause()
        {
            if (!_isPaused) return;
            _isPaused = false;
            if (_stageDelayWasPlaying && _stageDelay != null &&
                _stageDelay.IsActive()) _stageDelay.Play();
            _stageDelayWasPlaying = false;
            for (var i = 0; i < _activeSmoke.Count; i++)
                _activeSmoke[i].ResumePlayback();
        }
    }
}
