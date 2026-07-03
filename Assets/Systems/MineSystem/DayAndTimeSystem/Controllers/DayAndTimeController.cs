using System;
using Systems.MineSystem.DayAndTimeSystem.Models;
using Systems.MineSystem.DayAndTimeSystem.Structs;
using Systems.MineSystem.DayAndTimeSystem.Views;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using Zenject;

namespace Systems.MineSystem.DayAndTimeSystem.Controllers
{
    [Serializable]
    public sealed class DayAndTimeController :
        IDayAndTimeController,
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly DayAndTimeModel _model;
        private readonly DayAndTimeView _view;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public IReadOnlyReactiveProperty<int> Day => _model.Day;
        public IReadOnlyReactiveProperty<int> Hour => _model.Hour;
        public IReadOnlyReactiveProperty<int> Minute => _model.Minute;

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

        public DayAndTimeController(DayAndTimeModel model, DayAndTimeView view)
        {
            _model = model;
            _view = view;
        }

        public void Initialize()
        {
            _view.Bind(_model);
            _model.StartTime();
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        public void Pause() => _model.Pause();
        public void Resume() => _model.Resume();
        public void OnPause() => _model.SetGloballyPaused(true);
        public void OnUnpause() => _model.SetGloballyPaused(false);
        public void SetTime(MineTime time) => _model.SetTime(time);
        public MineTime GetCurrentTime() => _model.GetCurrentTime();

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
        }
    }
}
