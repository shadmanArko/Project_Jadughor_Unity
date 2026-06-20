using System;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace Systems.Utilities.ScreenShake
{
    [Serializable]
    public sealed class ScreenShakeController :
        IInitializable,
        IDisposable
    {
        private static ScreenShakeController _instance;

        private readonly CinemachineCamera _camera;
        private CinemachineFollow _follow;
        private Vector3 _baseFollowOffset;
        private Tween _activeShake;

        public ScreenShakeController(CinemachineCamera camera)
        {
            _camera = camera;
        }

        public void Initialize()
        {
            _follow = _camera != null
                ? _camera.GetComponent<CinemachineFollow>()
                : null;

            if (_follow == null)
            {
                Debug.LogError(
                    "ScreenShakeController requires a CinemachineFollow component.");
                return;
            }

            _baseFollowOffset = _follow.FollowOffset;
            _instance = this;
        }

        public static void VerticalShake(
            ScreenShakeLevel level = ScreenShakeLevel.Medium)
        {
            if (!TryGetInstance(out var controller))
                return;

            controller.PlayVerticalShake(level);
        }

        public static void RandomShake(
            float duration,
            float strength = 0.08f)
        {
            if (!TryGetInstance(out var controller))
                return;

            controller.PlayRandomShake(duration, strength);
        }

        public static void Stop()
        {
            _instance?.StopActiveShake();
        }

        private void PlayVerticalShake(ScreenShakeLevel level)
        {
            GetVerticalSettings(
                level,
                out var duration,
                out var strength,
                out var vibrato);

            StartShake(
                duration,
                new Vector3(0f, strength, 0f),
                vibrato,
                0f,
                ShakeRandomnessMode.Harmonic);
        }

        private void PlayRandomShake(float duration, float strength)
        {
            duration = Mathf.Max(0.01f, duration);
            strength = Mathf.Max(0f, strength);
            if (strength <= 0f)
                return;

            StartShake(
                duration,
                new Vector3(strength, strength, 0f),
                Mathf.Max(2, Mathf.RoundToInt(duration * 30f)),
                90f,
                ShakeRandomnessMode.Full);
        }

        private void StartShake(
            float duration,
            Vector3 strength,
            int vibrato,
            float randomness,
            ShakeRandomnessMode randomnessMode)
        {
            StopActiveShake();

            _activeShake = DOTween.Shake(
                    () => _follow.FollowOffset,
                    value => _follow.FollowOffset = value,
                    duration,
                    strength,
                    vibrato,
                    randomness,
                    false,
                    randomnessMode)
                .SetUpdate(true)
                .SetTarget(this)
                .OnKill(RestoreOffset)
                .OnComplete(RestoreOffset);
        }

        private void StopActiveShake()
        {
            if (_activeShake != null && _activeShake.IsActive())
                _activeShake.Kill();

            _activeShake = null;
            RestoreOffset();
        }

        private void RestoreOffset()
        {
            if (_follow != null)
                _follow.FollowOffset = _baseFollowOffset;
        }

        private static void GetVerticalSettings(
            ScreenShakeLevel level,
            out float duration,
            out float strength,
            out int vibrato)
        {
            switch (level)
            {
                case ScreenShakeLevel.Light:
                    duration = 0.02f;
                    strength = 0.01f;
                    vibrato = 1;
                    break;
                case ScreenShakeLevel.Medium:
                    duration = 0.02f;
                    strength = 0.02f;
                    vibrato = 2;
                    break;
                case ScreenShakeLevel.Heavy:
                    duration = 0.02f;
                    strength = 0.03f;
                    vibrato = 3;
                    break;
                case ScreenShakeLevel.Extreme:
                    duration = 0.02f;
                    strength = 0.04f;
                    vibrato = 4;
                    break;
                default:
                    duration = 0.02f;
                    strength = 0.01f;
                    vibrato = 1;
                    break;
            }
        }

        private static bool TryGetInstance(
            out ScreenShakeController controller)
        {
            controller = _instance;
            if (controller != null && controller._follow != null)
                return true;

            Debug.LogWarning(
                "Screen shake was requested before ScreenShakeController initialized.");
            return false;
        }

        public void Dispose()
        {
            StopActiveShake();
            if (ReferenceEquals(_instance, this))
                _instance = null;
        }
    }
}
