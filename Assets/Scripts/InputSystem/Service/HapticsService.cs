using System;
using Cysharp.Threading.Tasks;
using InputSystem.Config;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace InputSystem.Service
{
    /// <summary>
    /// Controls gamepad rumble motors (low-frequency and high-frequency).
    /// Supports instant rumble, timed rumble, and optional natural decay.
    ///
    /// Publish HapticRequestEvent via the EventBus rather than calling this service
    /// directly — HapticsController bridges the two.
    ///
    /// Notes:
    ///   - Uses Gamepad.current, so only the primary gamepad is affected.
    ///   - Motor values are clamped to [0, 1].
    ///   - SetMotorSpeeds() is cross-platform for Xbox, PlayStation, and Switch Pro.
    ///   - Advanced DualSense features (adaptive triggers) require platform-specific APIs.
    /// </summary>
    public sealed class HapticsService : IInitializable, IDisposable
    {
        private readonly float             _decayRate;
        private readonly CompositeDisposable _disposables = new();

        private float   _lowFreq;
        private float   _highFreq;
        private Gamepad _activeGamepad;
        private bool    _decayEnabled;

        public HapticsService(InputSystemConfig config)
        {
            _decayRate = config.HapticDecayRate;
        }

        // ─── IInitializable ───────────────────────────────────────────────────

        public void Initialize()
        {
            // Run decay in the main-thread update loop via UniRx
            Observable.EveryUpdate()
                .Where(_ => _decayEnabled && _activeGamepad != null)
                .Subscribe(_ => TickDecay())
                .AddTo(_disposables);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Start rumble at the given intensities.
        /// If duration is provided, rumble stops automatically after that many seconds.
        /// </summary>
        public void Rumble(float lowFreq, float highFreq, float? durationSeconds = null)
        {
            _activeGamepad = Gamepad.current;
            if (_activeGamepad == null) return;

            _lowFreq  = Mathf.Clamp01(lowFreq);
            _highFreq = Mathf.Clamp01(highFreq);
            _activeGamepad.SetMotorSpeeds(_lowFreq, _highFreq);

            _decayEnabled = _decayRate > 0f && !durationSeconds.HasValue;

            if (durationSeconds.HasValue)
                StopAfterDelayAsync(durationSeconds.Value).Forget();
        }

        /// <summary>Immediately stop all rumble on the current gamepad.</summary>
        public void Stop()
        {
            _decayEnabled = false;
            _lowFreq      = 0f;
            _highFreq     = 0f;
            _activeGamepad?.SetMotorSpeeds(0f, 0f);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private void TickDecay()
        {
            _lowFreq  = Mathf.Max(0f, _lowFreq  - _decayRate * Time.deltaTime);
            _highFreq = Mathf.Max(0f, _highFreq - _decayRate * Time.deltaTime);
            _activeGamepad.SetMotorSpeeds(_lowFreq, _highFreq);

            if (_lowFreq <= 0f && _highFreq <= 0f)
                _decayEnabled = false;
        }

        private async UniTaskVoid StopAfterDelayAsync(float seconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(seconds));
            Stop();
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            Stop();
            _disposables.Dispose();
        }
    }
}
