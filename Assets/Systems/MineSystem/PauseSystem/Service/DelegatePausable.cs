using System;
using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;

namespace Systems.MineSystem.PauseSystem.Service
{
    /// <summary>
    /// An <see cref="IPausable"/> that forwards to two callbacks, so one
    /// service can expose a separate pausable per <see cref="PauseArea"/>.
    /// </summary>
    /// <remarks>
    /// A service that owns instances in more than one area cannot answer
    /// "should I freeze?" with a single flag - the mine half and the lair half
    /// pause independently. Such a service registers one of these per area and
    /// pauses only the matching subset, instead of implementing
    /// <see cref="IPausable"/> itself.
    /// </remarks>
    public sealed class DelegatePausable : IPausable
    {
        private readonly Action _onPause;
        private readonly Action _onUnpause;
        private bool _isAffectedByPause = true;

        public DelegatePausable(Action onPause, Action onUnpause)
        {
            _onPause = onPause;
            _onUnpause = onUnpause;
        }

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value)
                    return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(new PausableAffectationChangedSignal(this));
            }
        }

        public void Register(PauseArea area) =>
            GlobalEventBus.Fire(new PausableRegisteredSignal(this, area));

        public void Unregister() =>
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));

        public void OnPause() => _onPause?.Invoke();
        public void OnUnpause() => _onUnpause?.Invoke();
    }
}
