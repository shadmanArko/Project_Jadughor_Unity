using System;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using Systems.Utilities.ScreenShake;
using Zenject;

namespace Systems.MineSystem.PauseSystem.Controller
{
    public sealed class ScreenShakePauseController :
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly ScreenShakeController _screenShake;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public ScreenShakePauseController(ScreenShakeController screenShake) =>
            _screenShake = screenShake;

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value) return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(new PausableAffectationChangedSignal(this));
            }
        }

        public void Initialize() =>
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));

        public void OnPause() => _screenShake.PauseActiveShake();
        public void OnUnpause() => _screenShake.ResumeActiveShake();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
        }
    }
}
