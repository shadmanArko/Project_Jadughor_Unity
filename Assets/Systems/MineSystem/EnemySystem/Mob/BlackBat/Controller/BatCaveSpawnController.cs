using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Controller;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Controller
{
    public sealed class BatCaveSpawnController : IInitializable, IDisposable
    {
        private readonly EnemyManager _enemyManager;
        private readonly MineModel _mine;
        private readonly BatConfigScriptable _config;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly HashSet<string> _processedCaveIds = new();
        private readonly Queue<string> _pendingCaveIds = new();

        private CancellationTokenSource _lifetimeCancellation = new();
        private bool _processing;
        private bool _disposed;

        public BatCaveSpawnController(
            EnemyManager enemyManager,
            MineModel mine,
            BatConfigScriptable config)
        {
            _enemyManager = enemyManager;
            _mine = mine;
            _config = config;
        }

        public void Initialize()
        {
            GlobalEventBus.OnSignal<CaveRevealedSignal>()
                .Subscribe(OnCaveRevealed)
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<MineGeneratedSignal>()
                .Subscribe(OnMineGenerated)
                .AddTo(_subscriptions);
        }

        private void OnMineGenerated(MineGeneratedSignal signal)
        {
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
            _lifetimeCancellation = new CancellationTokenSource();
            _pendingCaveIds.Clear();
            _processedCaveIds.Clear();
            _processing = false;

            var caves = signal.MineData?.Caves;
            if (caves == null)
                return;
            for (var i = 0; i < caves.Count; i++)
            {
                if (caves[i]?.IsRevealed == true)
                    EnqueueCave(caves[i].Id);
            }
        }

        private void OnCaveRevealed(CaveRevealedSignal signal) =>
            EnqueueCave(signal.CaveId);

        private void EnqueueCave(string caveId)
        {
            if (_disposed || string.IsNullOrEmpty(caveId) ||
                !_processedCaveIds.Add(caveId))
                return;
            _pendingCaveIds.Enqueue(caveId);
            if (_processing)
                return;
            _processing = true;
            ProcessPendingCavesAsync(_lifetimeCancellation.Token)
                .Forget(HandleProcessException);
        }

        private async UniTask ProcessPendingCavesAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                while (_pendingCaveIds.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var caveId = _pendingCaveIds.Dequeue();
                    var cave = FindCave(caveId);
                    if (cave != null)
                        await SpawnCaveBatsAsync(cave, cancellationToken);
                }
            }
            finally
            {
                if (!_disposed &&
                    cancellationToken == _lifetimeCancellation.Token)
                    _processing = false;
            }
        }

        private async UniTask SpawnCaveBatsAsync(
            Cave cave,
            CancellationToken cancellationToken)
        {
            var requestedCount = Mathf.Max(0, cave.NoOfFlyingEnemies);
            if (requestedCount == 0 || cave.CellPositions == null)
                return;

            var candidates = new List<GridPosition>(cave.CellPositions);
            Shuffle(candidates);
            var spawnedCount = 0;
            for (var i = 0;
                 i < candidates.Count && spawnedCount < requestedCount;
                 i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await _enemyManager.SpawnAsync(
                    new EnemySpawnRequest(
                        _config,
                        candidates[i],
                        visibilityRule: EnemySpawnVisibilityRule.Any),
                    cancellationToken);
                if (result.Succeeded)
                    spawnedCount++;
            }

            if (spawnedCount < requestedCount)
            {
                Debug.LogWarning(
                    $"Cave {cave.Id} requested {requestedCount} bats, but " +
                    $"only {spawnedCount} placement-valid bats could spawn.");
            }
        }

        private Cave FindCave(string caveId)
        {
            var caves = _mine.MineData.Value?.Caves;
            if (caves == null)
                return null;
            for (var i = 0; i < caves.Count; i++)
            {
                var cave = caves[i];
                if (cave != null && cave.Id == caveId)
                    return cave;
            }
            return null;
        }

        private static void Shuffle(List<GridPosition> positions)
        {
            for (var i = positions.Count - 1; i > 0; i--)
            {
                var swapIndex = UnityEngine.Random.Range(0, i + 1);
                (positions[i], positions[swapIndex]) =
                    (positions[swapIndex], positions[i]);
            }
        }

        private void HandleProcessException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lifetimeCancellation.Cancel();
            _lifetimeCancellation.Dispose();
            _pendingCaveIds.Clear();
            _processedCaveIds.Clear();
            _subscriptions.Dispose();
        }
    }
}
