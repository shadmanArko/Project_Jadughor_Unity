using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;

namespace Systems.MineSystem.ActorSystem.Service
{
    /// <summary>
    /// Awaitable, pausable DOTween move on a <see cref="Rigidbody2D"/>. Shared
    /// by any <c>IActor</c> implementation that needs a scripted walk, so a
    /// second actor doesn't need its own hand-copy of the tween-await/pause
    /// plumbing <c>PlayerAutoMovementService</c> already has.
    /// </summary>
    public sealed class ActorMovementTweenRunner : IDisposable
    {
        private Tween _activeTween;

        public async UniTask MoveAsync(
            Rigidbody2D body,
            Vector2 destination,
            float duration,
            Ease ease,
            CancellationToken cancellationToken)
        {
            _activeTween?.Kill();
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;

            if (duration <= 0f)
            {
                body.position = destination;
                return;
            }

            var tween = body.DOMove(destination, duration).SetEase(ease);
            await AwaitTweenAsync(tween, cancellationToken);
        }

        private async UniTask AwaitTweenAsync(
            Tween tween,
            CancellationToken cancellationToken)
        {
            _activeTween = tween;
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
            if (ReferenceEquals(_activeTween, tween))
                _activeTween = null;
        }

        public bool Pause()
        {
            if (_activeTween == null || !_activeTween.IsActive() ||
                !_activeTween.IsPlaying())
                return false;
            _activeTween.Pause();
            return true;
        }

        public void Resume(bool wasPlaying)
        {
            if (wasPlaying && _activeTween != null && _activeTween.IsActive())
                _activeTween.Play();
        }

        public void Dispose()
        {
            _activeTween?.Kill();
            _activeTween = null;
        }
    }
}
