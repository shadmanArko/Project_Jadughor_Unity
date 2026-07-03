using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DG.Tweening;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class ExplosionSmokeView : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        private Tween _activeDelay;
        private float _animatorSpeed;
        private bool _delayWasPlaying;
        private bool _isPaused;

        public async UniTask PlayAsync(
            Vector3 worldPosition,
            DynamiteConfig config,
            Action impact,
            CancellationToken cancellationToken)
        {
            transform.position = worldPosition;
            animator.Play(
                Animator.StringToHash(config.ExplosionState),
                0,
                0f);
            animator.Update(0f);

            var state = animator.GetCurrentAnimatorStateInfo(0);
            var speed = Mathf.Max(
                0.0001f,
                Mathf.Abs(state.speed * state.speedMultiplier));
            var duration = state.length > 0f
                ? state.length / speed
                : config.FallbackAnimationDuration;
            var impactDelay =
                duration * config.NormalizedImpactTime;

            if (impactDelay > 0f)
            {
                await DelayAsync(impactDelay, cancellationToken);
            }

            impact?.Invoke();

            var remaining = duration - impactDelay;
            if (remaining > 0f)
            {
                await DelayAsync(remaining, cancellationToken);
            }
        }

        private async UniTask DelayAsync(
            float seconds,
            CancellationToken cancellationToken)
        {
            var tween = DOVirtual.DelayedCall(seconds, () => { }, false);
            _activeDelay = tween;
            if (_isPaused)
            {
                _delayWasPlaying = true;
                tween.Pause();
            }
            var completion = new UniTaskCompletionSource();
            var finished = false;
            CancellationTokenRegistration registration = default;
            tween.OnComplete(() =>
            {
                if (finished) return;
                finished = true;
                registration.Dispose();
                completion.TrySetResult();
            });
            tween.OnKill(() =>
            {
                if (finished) return;
                finished = true;
                registration.Dispose();
                if (cancellationToken.IsCancellationRequested)
                    completion.TrySetCanceled(cancellationToken);
                else completion.TrySetResult();
            });
            if (cancellationToken.CanBeCanceled)
                registration = cancellationToken.Register(() => tween.Kill());
            await completion.Task;
            if (ReferenceEquals(_activeDelay, tween)) _activeDelay = null;
        }

        public void PausePlayback()
        {
            _isPaused = true;
            _animatorSpeed = animator != null ? animator.speed : 0f;
            if (animator != null) animator.speed = 0f;
            _delayWasPlaying = _activeDelay != null &&
                               _activeDelay.IsActive() &&
                               _activeDelay.IsPlaying();
            if (_delayWasPlaying) _activeDelay.Pause();
        }

        public void ResumePlayback()
        {
            _isPaused = false;
            if (animator != null) animator.speed = _animatorSpeed;
            if (_delayWasPlaying && _activeDelay != null &&
                _activeDelay.IsActive()) _activeDelay.Play();
            _delayWasPlaying = false;
        }

        public void ResetView()
        {
            _activeDelay?.Kill();
            _activeDelay = null;
            _isPaused = false;
            if (animator != null)
                animator.Rebind();

            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }
    }
}
