using System;
using Core.EventBus;
using InputSystem.Events;
using InputSystem.Service;
using UniRx;
using Zenject;

namespace InputSystem.Controller
{
    /// <summary>
    /// Listens for HapticRequestEvent and StopHapticsEvent on the EventBus
    /// and delegates to HapticsService.
    ///
    /// This decouples every gameplay system from HapticsService — they never
    /// need to inject it directly. They just publish an event.
    /// </summary>
    public sealed class HapticsController : IInitializable, IDisposable
    {
        private readonly EventBus      _eventBus;
        private readonly HapticsService _hapticsService;
        private readonly CompositeDisposable _disposables = new();

        public HapticsController(EventBus eventBus, HapticsService hapticsService)
        {
            _eventBus       = eventBus;
            _hapticsService = hapticsService;
        }

        // ─── IInitializable ───────────────────────────────────────────────────

        public void Initialize()
        {
            _eventBus.Receive<HapticRequestEvent>()
                .Subscribe(evt => _hapticsService.Rumble(evt.LowFrequency, evt.HighFrequency, evt.Duration))
                .AddTo(_disposables);

            _eventBus.Receive<StopHapticsEvent>()
                .Subscribe(_ => _hapticsService.Stop())
                .AddTo(_disposables);
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
