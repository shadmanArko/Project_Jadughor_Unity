using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.Damage;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.Utilities.ScreenShake;
using UnityEngine;
using DG.Tweening;
using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Service;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    /// <summary>
    /// Runs staged dynamite blasts. One service drives explosions in both the
    /// mine and the boss lair, so its pause state is kept per
    /// <see cref="PauseArea"/>: freezing the mine must not stall a blast the
    /// player set off inside the arena, and vice versa.
    /// </summary>
    public sealed class DynamiteExplosionService : IInitializable, IDisposable
    {
        /// <summary>
        /// Everything an explosion has in flight for one area. Delays are a list
        /// rather than a single tween because blasts overlap - a single field
        /// was silently dropped by the next detonation, leaving its tween
        /// unpausable.
        /// </summary>
        private sealed class AreaState
        {
            public readonly List<Tween> StageDelays = new();
            public readonly List<ExplosionSmokeView> ActiveSmoke = new();
            public bool IsPaused;
        }

        private readonly MineModel _mine;
        private readonly MineView _mineView;
        private readonly ExplosionSmokePool _smokePool;
        private readonly ICellDamageService _cellDamage;
        private readonly BossLairModel _bossLair;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly AreaState _mineState = new();
        private readonly AreaState _lairState = new();
        private DelegatePausable _minePausable;
        private DelegatePausable _lairPausable;
        private bool _disposed;

        public DynamiteExplosionService(
            MineModel mine,
            MineView mineView,
            ExplosionSmokePool smokePool,
            ICellDamageService cellDamage,
            BossLairModel bossLair)
        {
            _mine = mine;
            _mineView = mineView;
            _smokePool = smokePool;
            _cellDamage = cellDamage;
            _bossLair = bossLair;
        }

        public void Initialize()
        {
            _minePausable = new DelegatePausable(
                () => PauseState(_mineState),
                () => ResumeState(_mineState));
            _lairPausable = new DelegatePausable(
                () => PauseState(_lairState),
                () => ResumeState(_lairState));
            _minePausable.Register(PauseArea.MineView);
            _lairPausable.Register(PauseArea.BossLair);
        }

        public void Detonate(
            PlaceableSpawnContext context,
            DynamiteConfig config)
        {
            RunExplosionAsync(
                    context,
                    config,
                    ResolveState(context.CellPosition),
                    _lifetime.Token)
                .Forget(exception =>
                {
                    if (exception is not OperationCanceledException)
                        Debug.LogException(exception);
                });
        }

        /// <summary>
        /// Which area's pause governs this blast, decided once from where the
        /// stick was placed.
        /// </summary>
        private AreaState ResolveState(Vector3Int cell) =>
            _bossLair.HasGate &&
            _bossLair.Placement.IsValid &&
            _bossLair.Placement.InteriorCells.Contains(cell)
                ? _lairState
                : _mineState;

        private async UniTask RunExplosionAsync(
            PlaceableSpawnContext context,
            DynamiteConfig config,
            AreaState state,
            CancellationToken cancellationToken)
        {
            var damaged = new HashSet<IDamageable>();
            var center = context.CellPosition;

            await RunStageAsync(
                new[] { center },
                config,
                damaged,
                state,
                cancellationToken);
            await WaitBetweenStagesAsync(config, state, cancellationToken);

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
                    state,
                    cancellationToken);
                return;
            }

            if (config.BlastPattern == DynamiteBlastPattern.AreaFiveByFive)
            {
                await RunStageAsync(
                    CollectSquareArea(center, 2),
                    config,
                    damaged,
                    state,
                    cancellationToken);
                return;
            }

            await RunStageAsync(
                CollectParticipatingCells(
                    center + Vector3Int.left,
                    center + Vector3Int.right),
                config,
                damaged,
                state,
                cancellationToken);
            await WaitBetweenStagesAsync(config, state, cancellationToken);

            await RunStageAsync(
                CollectParticipatingCells(
                    center + Vector3Int.up,
                    center + Vector3Int.down),
                config,
                damaged,
                state,
                cancellationToken);
        }

        private UniTask WaitBetweenStagesAsync(
            DynamiteConfig config,
            AreaState state,
            CancellationToken cancellationToken)
        {
            return config.DelayBetweenStages <= 0f
                ? UniTask.CompletedTask
                : AwaitStageDelayAsync(
                    config.DelayBetweenStages,
                    state,
                    cancellationToken);
        }

        private async UniTask AwaitStageDelayAsync(
            float seconds,
            AreaState state,
            CancellationToken cancellationToken)
        {
            var tween = DOVirtual.DelayedCall(seconds, () => { }, false);
            state.StageDelays.Add(tween);
            if (state.IsPaused) tween.Pause();
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
            try
            {
                await completion.Task;
            }
            finally
            {
                state.StageDelays.Remove(tween);
            }
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
            AreaState state,
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
                        state,
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
            AreaState state,
            CancellationToken cancellationToken)
        {
            try
            {
                state.ActiveSmoke.Add(smoke);
                if (state.IsPaused) smoke.PausePlayback();
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
                state.ActiveSmoke.Remove(smoke);
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

        private static void PauseState(AreaState state)
        {
            if (state.IsPaused) return;
            state.IsPaused = true;
            for (var i = 0; i < state.StageDelays.Count; i++)
            {
                var tween = state.StageDelays[i];
                if (tween != null && tween.IsActive() && tween.IsPlaying())
                    tween.Pause();
            }
            for (var i = 0; i < state.ActiveSmoke.Count; i++)
                state.ActiveSmoke[i].PausePlayback();
        }

        private static void ResumeState(AreaState state)
        {
            if (!state.IsPaused) return;
            state.IsPaused = false;
            for (var i = 0; i < state.StageDelays.Count; i++)
            {
                var tween = state.StageDelays[i];
                if (tween != null && tween.IsActive())
                    tween.Play();
            }
            for (var i = 0; i < state.ActiveSmoke.Count; i++)
                state.ActiveSmoke[i].ResumePlayback();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _minePausable?.Unregister();
            _lairPausable?.Unregister();
            if (!_lifetime.IsCancellationRequested)
                _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
