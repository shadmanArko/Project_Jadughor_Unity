using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    public sealed class PlayerAutoMovementService : IDisposable
    {
        private readonly PlayerView _view;
        private readonly RuntimeDataScriptable _runtime;
        private Tween _activeTween;

        public PlayerAutoMovementService(PlayerView view,
            RuntimeDataScriptable runtime)
        {
            _view = view;
            _runtime = runtime;
        }

        public async UniTask MoveAsync(Vector2 destination, float duration,
            Ease ease, CancellationToken cancellationToken)
        {
            _activeTween?.Kill();
            _view.Stop();
            var previousGravity = _view.Body.gravityScale;
            _view.SetGravityScale(0f);
            var delta = destination - _view.Body.position;
            _runtime.velocity.Value = duration > 0f ? delta / duration : Vector2.zero;

            try
            {
                if (duration <= 0f)
                {
                    _view.Teleport(destination);
                    return;
                }
                var tween = _view.Body.DOMove(destination, duration).SetEase(ease);
                await AwaitTweenAsync(tween, cancellationToken);
            }
            finally
            {
                _view.Stop();
                _runtime.velocity.Value = Vector2.zero;
                _runtime.worldPosition.Value = _view.PlayerCollider.bounds.center;
                _view.SetGravityScale(previousGravity);
            }
        }

        private async UniTask AwaitTweenAsync(Tween tween,
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
            if (ReferenceEquals(_activeTween, tween)) _activeTween = null;
        }

        public void Dispose()
        {
            _activeTween?.Kill();
            _activeTween = null;
        }
    }
}
