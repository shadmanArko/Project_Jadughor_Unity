using System;
using System.Collections.Generic;
using Systems.MineSystem.DayAndTimeSystem.Configs;
using Systems.MineSystem.DayAndTimeSystem.Controllers;
using Systems.MineSystem.DayAndTimeSystem.Signals;
using Systems.MineSystem.EnemySystem.Config;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.EnemySystem.Signal;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.Signal;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Service
{
    public sealed class EnemyWaveService : IInitializable, IDisposable
    {
        private readonly EnemyWaveConfig _config;
        private readonly RuntimeDataScriptable _runtimeData;
        private readonly MineModel _mine;
        private readonly IDayAndTimeController _timeController;
        private readonly DayAndTimeConfig _timeConfig;
        private readonly CompositeDisposable _subscriptions = new();
        private readonly List<EnemyWaveRuntimeState> _states = new();
        private readonly Queue<EnemyWaveRuntimeState> _spawnQueue = new();

        private Guid _pendingRequestId;
        private int _elapsedMineMinutes;
        private int _currentMinuteOfDay;
        private int _activeBatchInterval;
        private int _nextSpawnAttemptMinute;
        private bool _mineReady;
        private bool _disposed;

        public EnemyWaveService(
            EnemyWaveConfig config,
            RuntimeDataScriptable runtimeData,
            MineModel mine,
            IDayAndTimeController timeController,
            DayAndTimeConfig timeConfig)
        {
            _config = config;
            _runtimeData = runtimeData;
            _mine = mine;
            _timeController = timeController;
            _timeConfig = timeConfig;
        }

        public void Initialize()
        {
            if (!_config.Validate(out var error))
                throw new InvalidOperationException(error);

            GlobalEventBus.OnSignal<MineGeneratedSignal>()
                .Subscribe(_ => ResetForMine())
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<MinuteEndSignal>()
                .Subscribe(OnMinuteEnded)
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<EnemyWaveSpawnResolvedSignal>()
                .Subscribe(OnSpawnResolved)
                .AddTo(_subscriptions);
            _mine.OnCellBroken
                .Subscribe(_ => OnCellBroken())
                .AddTo(_subscriptions);

            if (_mine.MineData.Value != null)
                ResetForMine();
        }

        private void ResetForMine()
        {
            _runtimeData.cellsBrokenInCurrentMine.Value = 0;
            _elapsedMineMinutes = 0;
            var time = _timeController.GetCurrentTime();
            _currentMinuteOfDay = ToMinuteOfDay(time.hour, time.minute);
            _pendingRequestId = Guid.Empty;
            _activeBatchInterval = 0;
            _nextSpawnAttemptMinute = 0;
            _spawnQueue.Clear();
            _states.Clear();
            for (var i = 0; i < _config.SpawnData.Count; i++)
                _states.Add(new EnemyWaveRuntimeState(_config.SpawnData[i]));
            _mineReady = true;
        }

        private void OnMinuteEnded(MinuteEndSignal signal)
        {
            if (!_mineReady || _disposed)
                return;
            _elapsedMineMinutes += Mathf.Max(1, _timeConfig.minuteStep);
            _currentMinuteOfDay = ToMinuteOfDay(signal.Hour, signal.Minute);
            EvaluateWaveTriggers();
            TryRequestSpawn();
        }

        private void OnCellBroken()
        {
            if (!_mineReady || _disposed)
                return;
            _runtimeData.cellsBrokenInCurrentMine.Value++;
            EvaluateWaveTriggers();
            TryRequestSpawn();
        }

        private void EvaluateWaveTriggers()
        {
            for (var i = 0; i < _states.Count; i++)
            {
                var state = _states[i];
                if (state.Triggered ||
                    _elapsedMineMinutes < state.NextTriggerEvaluationMinute)
                    continue;

                var data = state.Data;
                var timeReached = data.UseTimeTrigger &&
                                  _currentMinuteOfDay >= ToMinuteOfDay(
                                      data.StartHour,
                                      data.StartMinute);
                var wallsReached = data.UseWallBreakTrigger &&
                                   _runtimeData.cellsBrokenInCurrentMine.Value >=
                                   data.RequiredBrokenCells;
                if (!timeReached && !wallsReached)
                {
                    state.NextTriggerEvaluationMinute =
                        _elapsedMineMinutes +
                        _config.FailedToSpawnDelayInGameMinutes;
                    continue;
                }

                state.Triggered = true;
                if (_spawnQueue.Count == 0)
                {
                    _activeBatchInterval = data.SpawnIntervalInGameMinutes;
                    _nextSpawnAttemptMinute = _elapsedMineMinutes;
                }
                _spawnQueue.Enqueue(state);
            }
        }

        private void TryRequestSpawn()
        {
            if (_spawnQueue.Count == 0 ||
                _pendingRequestId != Guid.Empty ||
                _elapsedMineMinutes < _nextSpawnAttemptMinute)
                return;

            var state = _spawnQueue.Peek();
            _pendingRequestId = Guid.NewGuid();
            GlobalEventBus.Fire(new EnemyWaveSpawnRequestedSignal(
                _pendingRequestId,
                state.Data.EnemyConfig,
                _config.OutsideCameraMarginInTiles));
        }

        private void OnSpawnResolved(EnemyWaveSpawnResolvedSignal signal)
        {
            if (_disposed || signal.RequestId != _pendingRequestId)
                return;
            _pendingRequestId = Guid.Empty;
            if (!signal.Succeeded)
            {
                _nextSpawnAttemptMinute =
                    _elapsedMineMinutes +
                    _config.FailedToSpawnDelayInGameMinutes;
                if (!string.IsNullOrWhiteSpace(signal.Error))
                    Debug.LogWarning($"Enemy wave spawn delayed: {signal.Error}");
                return;
            }

            var state = _spawnQueue.Peek();
            state.RemainingEnemyCount--;
            if (state.RemainingEnemyCount <= 0)
                _spawnQueue.Dequeue();

            if (_spawnQueue.Count == 0)
            {
                _activeBatchInterval = 0;
                _nextSpawnAttemptMinute = 0;
                return;
            }

            _nextSpawnAttemptMinute =
                _elapsedMineMinutes + _activeBatchInterval;
            if (_activeBatchInterval == 0)
                TryRequestSpawn();
        }

        private static int ToMinuteOfDay(int hour, int minute) =>
            hour * 60 + minute;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _pendingRequestId = Guid.Empty;
            _spawnQueue.Clear();
            _states.Clear();
            _subscriptions.Dispose();
        }
    }
}
