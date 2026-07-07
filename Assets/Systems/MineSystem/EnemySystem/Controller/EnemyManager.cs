using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.EnemySystem.Service;
using Systems.MineSystem.EnemySystem.Signal;
using Systems.MineSystem.Mine.Signal;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Controller
{
    public sealed class EnemyManager :
        IFixedTickable,
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly EnemySpawnService _spawnService;
        private readonly Dictionary<Guid, IEnemyController> _activeEnemies = new();
        private readonly List<IEnemyController> _tickSnapshot = new();
        private readonly List<GridPosition> _occupiedPositions = new();
        private readonly CompositeDisposable _subscriptions = new();
        private bool _isAffectedByPause = true;
        private bool _isPaused;
        private bool _disposed;
        private CancellationTokenSource _waveSpawnCancellation = new();

        public int ActiveEnemyCount => _activeEnemies.Count;
        public IReadOnlyDictionary<Guid, IEnemyController> ActiveEnemies =>
            _activeEnemies;

        public bool IsPositionOccupied(GridPosition position, Guid exceptEnemyId)
        {
            foreach (var pair in _activeEnemies)
            {
                if (pair.Key != exceptEnemyId &&
                    pair.Value.IsActive &&
                    pair.Value.CurrentGridPosition == position)
                    return true;
            }
            return false;
        }

        public IReadOnlyCollection<GridPosition> GetOccupiedPositions(
            Guid exceptEnemyId)
        {
            var result = new List<GridPosition>(_activeEnemies.Count);
            foreach (var pair in _activeEnemies)
            {
                if (pair.Key != exceptEnemyId && pair.Value.IsActive)
                    result.Add(pair.Value.CurrentGridPosition);
            }
            return result;
        }

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value)
                    return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(
                    new PausableAffectationChangedSignal(this));
            }
        }

        public EnemyManager(EnemySpawnService spawnService)
        {
            _spawnService = spawnService;
        }

        public void Initialize()
        {
            GlobalEventBus.OnSignal<EnemyDiedSignal>()
                .Subscribe(signal => RemoveAndRelease(signal.EnemyId))
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<EnemyDespawnedSignal>()
                .Subscribe(signal => RemoveAndRelease(signal.EnemyId))
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<MineGeneratedSignal>()
                .Subscribe(_ => HandleMineGenerated())
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<EnemyWaveSpawnRequestedSignal>()
                .Subscribe(signal => HandleWaveSpawnRequestAsync(signal).Forget(
                    exception => Debug.LogException(exception)))
                .AddTo(_subscriptions);
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        public async UniTask<EnemySpawnResult> SpawnAsync(
            EnemySpawnRequest request,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return EnemySpawnResult.Failure("EnemyManager is disposed.");
            _occupiedPositions.Clear();
            foreach (var enemy in _activeEnemies.Values)
                _occupiedPositions.Add(enemy.CurrentGridPosition);
            var result = _spawnService.Spawn(
                request.WithOccupiedPositions(_occupiedPositions));
            if (!result.Succeeded)
                return result;

            var enemyController = result.Enemy;
            _activeEnemies.Add(enemyController.EnemyId, enemyController);
            try
            {
                await enemyController.SpawnAsync(cancellationToken);
                return result;
            }
            catch (OperationCanceledException)
            {
                RemoveAndRelease(enemyController.EnemyId);
                throw;
            }
            catch (Exception exception)
            {
                RemoveAndRelease(enemyController.EnemyId);
                Debug.LogException(exception);
                return EnemySpawnResult.Failure(exception.Message);
            }
        }

        public async UniTask<bool> DespawnAsync(
            Guid enemyId,
            CancellationToken cancellationToken = default)
        {
            if (!_activeEnemies.TryGetValue(enemyId, out var enemy))
                return false;
            await enemy.DespawnAsync(cancellationToken);
            return true;
        }

        public void FixedTick()
        {
            if (_disposed || _isPaused || _activeEnemies.Count == 0)
                return;
            _tickSnapshot.Clear();
            foreach (var enemy in _activeEnemies.Values)
                _tickSnapshot.Add(enemy);
            var context = new EnemyTickContext(Time.fixedDeltaTime);
            for (var i = 0; i < _tickSnapshot.Count; i++)
            {
                var enemy = _tickSnapshot[i];
                if (enemy.IsActive &&
                    _activeEnemies.TryGetValue(enemy.EnemyId, out var current) &&
                    ReferenceEquals(enemy, current))
                    enemy.OnFixedTick(context);
            }
        }

        public void OnPause() => _isPaused = true;
        public void OnUnpause() => _isPaused = false;

        private async UniTask HandleWaveSpawnRequestAsync(
            EnemyWaveSpawnRequestedSignal signal)
        {
            try
            {
                var visibilityRule =
                    signal.EnemyConfig != null &&
                    signal.EnemyConfig.AllowCameraVisibleWaveSpawn
                        ? EnemySpawnVisibilityRule.Any
                        : EnemySpawnVisibilityRule.OutsideCameraViewport;
                var result = await SpawnAsync(
                    new EnemySpawnRequest(
                        signal.EnemyConfig,
                        visibilityRule: visibilityRule,
                        outsideCameraMarginInTiles:
                            signal.OutsideCameraMarginInTiles),
                    _waveSpawnCancellation.Token);
                GlobalEventBus.Fire(new EnemyWaveSpawnResolvedSignal(
                    signal.RequestId,
                    result.Succeeded,
                    result.Enemy?.EnemyId ?? Guid.Empty,
                    result.Error));
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void HandleMineGenerated()
        {
            _waveSpawnCancellation.Cancel();
            _waveSpawnCancellation.Dispose();
            _waveSpawnCancellation = new CancellationTokenSource();
            ReleaseAll();
        }

        private void RemoveAndRelease(Guid enemyId)
        {
            if (!_activeEnemies.Remove(enemyId, out var enemy))
                return;
            _spawnService.Release(enemy);
        }

        private void ReleaseAll()
        {
            _tickSnapshot.Clear();
            foreach (var enemy in _activeEnemies.Values)
                _tickSnapshot.Add(enemy);
            _activeEnemies.Clear();
            for (var i = 0; i < _tickSnapshot.Count; i++)
                _spawnService.Release(_tickSnapshot[i]);
            _tickSnapshot.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _waveSpawnCancellation.Cancel();
            _waveSpawnCancellation.Dispose();
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            ReleaseAll();
            _subscriptions.Dispose();
        }
    }
}
