using System;
using Systems.MineSystem.DayAndTimeSystem.Configs;
using Systems.MineSystem.DayAndTimeSystem.Signals;
using Systems.MineSystem.DayAndTimeSystem.Structs;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.DayAndTimeSystem.Models
{
    [Serializable]
    public sealed class DayAndTimeModel : IDayAndTimeModel, IDisposable
    {
        private readonly DayAndTimeConfig _config;
        private readonly ReactiveProperty<int> _day = new(1);
        private readonly ReactiveProperty<int> _hour = new(8);
        private readonly ReactiveProperty<int> _minute = new(0);
        private readonly SerialDisposable _timer = new();
        private bool _started;
        private bool _manuallyPaused;
        private bool _globallyPaused;
        private bool _gameTimeOver;
        private bool _disposed;
        private float _remainingTickSeconds;
        private float _nextTickRealtime;

        public IReadOnlyReactiveProperty<int> Day => _day;
        public IReadOnlyReactiveProperty<int> Hour => _hour;
        public IReadOnlyReactiveProperty<int> Minute => _minute;

        public DayAndTimeModel(DayAndTimeConfig config)
        {
            _config = config;
            _remainingTickSeconds = TickInterval;
        }

        public void StartTime()
        {
            if (_started)
                return;
            _started = true;
            GlobalEventBus.Fire(new DayStartSignal { Day = _day.Value });
            TrySchedule();
        }

        public void Pause()
        {
            if (_manuallyPaused)
                return;
            CaptureRemaining();
            _manuallyPaused = true;
            _timer.Disposable = null;
        }

        public void Resume()
        {
            if (!_manuallyPaused)
                return;
            _manuallyPaused = false;
            TrySchedule();
        }

        public void SetGloballyPaused(bool paused)
        {
            if (_globallyPaused == paused)
                return;
            if (paused)
                CaptureRemaining();
            _globallyPaused = paused;
            if (paused)
                _timer.Disposable = null;
            else
                TrySchedule();
        }

        public void SetTime(MineTime time)
        {
            _timer.Disposable = null;
            _day.Value = time.day;
            _hour.Value = time.hour;
            _minute.Value = time.minute;
            _remainingTickSeconds = TickInterval;
            TrySchedule();
        }

        public MineTime GetCurrentTime() =>
            new(_day.Value, _hour.Value, _minute.Value);

        private float TickInterval =>
            Mathf.Max(0.01f, _config.tickIntervalSeconds);

        private void CaptureRemaining()
        {
            if (_timer.Disposable == null)
                return;
            _remainingTickSeconds = Mathf.Max(
                0f,
                _nextTickRealtime - Time.realtimeSinceStartup);
        }

        private void TrySchedule()
        {
            if (!_started || _disposed || _gameTimeOver ||
                _manuallyPaused || _globallyPaused)
                return;

            var delay = Mathf.Max(0f, _remainingTickSeconds);
            _nextTickRealtime = Time.realtimeSinceStartup + delay;
            _timer.Disposable = Observable
                .Timer(
                    TimeSpan.FromSeconds(delay),
                    Scheduler.MainThreadIgnoreTimeScale)
                .Subscribe(_ =>
                {
                    _timer.Disposable = null;
                    Tick();
                    _remainingTickSeconds = TickInterval;
                    TrySchedule();
                });
        }

        private void Tick()
        {
            var day = _day.Value;
            var hour = _hour.Value;
            var minute = _minute.Value;

            GlobalEventBus.Fire(new MinuteEndSignal
            {
                Day = day,
                Hour = hour,
                Minute = minute
            });

            var nextMinute = minute + _config.minuteStep;
            if (nextMinute <= _config.maxMinute)
            {
                _minute.Value = nextMinute;
                return;
            }

            _minute.Value = 0;
            GlobalEventBus.Fire(new HourEndSignal { Day = day, Hour = hour });
            var nextHour = hour + 1;
            if (nextHour < _config.dayEndHour)
            {
                _hour.Value = nextHour;
                return;
            }

            GlobalEventBus.Fire(new DayEndSignal { Day = day });
            var nextDay = day + 1;
            if (nextDay > _config.totalDays)
            {
                _gameTimeOver = true;
                GlobalEventBus.Fire<GameTimeEndSignal>();
                return;
            }

            _day.Value = nextDay;
            _hour.Value = _config.dayStartHour;
            _minute.Value = 0;
            GlobalEventBus.Fire(new DayStartSignal { Day = nextDay });
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _timer.Dispose();
            _day.Dispose();
            _hour.Dispose();
            _minute.Dispose();
        }
    }
}
