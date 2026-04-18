using System;
using Systems.MineSystem.DayAndTimeSystem.Models;
using Systems.MineSystem.DayAndTimeSystem.Structs;
using Systems.MineSystem.DayAndTimeSystem.Views;
using UniRx;
using Zenject;

namespace Systems.MineSystem.DayAndTimeSystem.Controllers
{
    [Serializable]
    public class DayAndTimeController : IDayAndTimeController, IInitializable, IDisposable
    {
        private readonly CompositeDisposable _disposable;
        private readonly DayAndTimeModel _model;
        private readonly DayAndTimeView  _view;

        // ── Pass-through reactive state for external observers ─────────────────
        public IReadOnlyReactiveProperty<int> Day    => _model.Day;
        public IReadOnlyReactiveProperty<int> Hour   => _model.Hour;
        public IReadOnlyReactiveProperty<int> Minute => _model.Minute;

        public DayAndTimeController(DayAndTimeModel model, DayAndTimeView view)
        {
            _model = model;
            _view  = view;
            
            _disposable = new CompositeDisposable();
        }

        // ── Zenject entry point ───────────────────────────────────────────────
        public void Initialize()
        {
            _view.Bind(_model); // give view its data source
            _model.StartTime();
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void Pause() => _model.Pause();
        public void Resume() => _model.Resume();
        public void SetTime(MineTime time) => _model.SetTime(time);
        public MineTime GetCurrentTime() => _model.GetCurrentTime();
        
        public void Dispose()
        {
            _disposable?.Dispose();
        }
    }
    
}