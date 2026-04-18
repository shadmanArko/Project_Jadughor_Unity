using System;
using Systems.MineSystem.DayAndTimeSystem.Configs;
using Systems.MineSystem.DayAndTimeSystem.Signals;
using Systems.MineSystem.DayAndTimeSystem.Structs;
using Systems.Utilities.EventBus;
using UniRx;

namespace Systems.MineSystem.DayAndTimeSystem.Models
{
    [Serializable]
    public class DayAndTimeModel : IDayAndTimeModel, IDisposable
    {
        private CompositeDisposable _disposable;
        public IReadOnlyReactiveProperty<int> Day => _day;
        public IReadOnlyReactiveProperty<int> Hour => _hour;
        public IReadOnlyReactiveProperty<int> Minute => _minute;

        private readonly ReactiveProperty<int> _day = new(1);
        private readonly ReactiveProperty<int> _hour = new(8);
        private readonly ReactiveProperty<int> _minute = new(0);

        // ── Internal ──────────────────────────────────────────────────────────
        private readonly DayAndTimeConfig _config;
        private readonly CompositeDisposable _disposables = new();
        private readonly SerialDisposable _timerDisposable = new();
        private bool _gameTimeOver;

        // ─────────────────────────────────────────────────────────────────────
        public DayAndTimeModel(DayAndTimeConfig config)
        {
            _disposable = new CompositeDisposable();
            _config = config;
            _timerDisposable.AddTo(_disposables);
        }

        // ── Public API (Controller calls these) ───────────────────────────────
        public void StartTime()
        {
            GlobalEventBus.Fire(new DayStartSignal { Day = _day.Value });
            StartTicking();
        }

        public void Pause() => _timerDisposable.Disposable = null;

        public void Resume()
        {
            if (!_gameTimeOver) StartTicking();
        }

        public void SetTime(MineTime time)
        {
            Pause();

            _day.Value = time.day;
            _hour.Value = time.hour;
            _minute.Value = time.minute;

            if (!_gameTimeOver) StartTicking();
        }

        public MineTime GetCurrentTime() => new(_day.Value, _hour.Value, _minute.Value);

        // ── Timer ─────────────────────────────────────────────────────────────
        private void StartTicking()
        {
            _timerDisposable.Disposable = Observable
                .Interval(TimeSpan.FromSeconds(_config.tickIntervalSeconds))
                .Subscribe(_ => Tick());
        }

        // ── All progression logic lives here ──────────────────────────────────
        private void Tick()
        {
            int day = _day.Value;
            int hour = _hour.Value;
            int minute = _minute.Value;

            // ── Minute ends ───────────────────────────────────────────────────
            GlobalEventBus.Fire(new MinuteEndSignal
            {
                Day = day,
                Hour = hour,
                Minute = minute
            });

            int nextMinute = minute + _config.minuteStep;

            if (nextMinute <= _config.maxMinute)
            {
                _minute.Value = nextMinute;
                return;
            }

            // ── Hour ends ─────────────────────────────────────────────────────
            _minute.Value = 0;
            GlobalEventBus.Fire(new HourEndSignal { Day = day, Hour = hour });

            int nextHour = hour + 1;

            if (nextHour < _config.dayEndHour)
            {
                _hour.Value = nextHour;
                return;
            }

            // ── Day ends ──────────────────────────────────────────────────────
            GlobalEventBus.Fire(new DayEndSignal { Day = day });

            int nextDay = day + 1;

            if (nextDay > _config.totalDays)
            {
                _gameTimeOver = true;
                Pause();
                GlobalEventBus.Fire<GameTimeEndSignal>();
                return;
            }

            // ── Next day starts ───────────────────────────────────────────────
            _day.Value = nextDay;
            _hour.Value = _config.dayStartHour;
            _minute.Value = 0;
            GlobalEventBus.Fire(new DayStartSignal { Day = nextDay });
        }

        public void Dispose() => _disposables.Dispose();
    }
}