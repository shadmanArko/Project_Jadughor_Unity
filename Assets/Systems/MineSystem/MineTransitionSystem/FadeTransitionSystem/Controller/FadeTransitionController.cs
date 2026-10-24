using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Config;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Model;
using Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.View;
using Zenject;

namespace Systems.MineSystem.MineTransitionSystem.FadeTransitionSystem.Controller
{
    public sealed class FadeTransitionController : IInitializable, IDisposable
    {
        private readonly FadeTransitionModel _model;
        private readonly FadeTransitionCanvasView _view;
        private readonly FadeTransitionConfig _config;
        private Tween _activeTween;
        private bool _disposed;

        public FadeTransitionController(FadeTransitionModel model,
            FadeTransitionCanvasView view, FadeTransitionConfig config)
        {
            _model = model;
            _view = view;
            _config = config;
        }

        public void Initialize()
        {
            _view.Validate();
            _view.SetDayLabel(null);
            _view.SetVisible(false);
        }

        public async UniTask FadeInAsync(string label,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _view.SetDayLabel(label);
            _view.CanvasGroup.blocksRaycasts = true;
            _model.SetVisible(true);
            await FadeAsync(1f, _config.fadeInSeconds, cancellationToken);
        }

        public async UniTask HoldDayAsync(CancellationToken cancellationToken)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(_config.dayCardSeconds),
                DelayType.UnscaledDeltaTime, cancellationToken: cancellationToken);
        }

        public async UniTask FadeOutAsync(CancellationToken cancellationToken)
        {
            await FadeAsync(0f, _config.fadeOutSeconds, cancellationToken);
            _view.SetVisible(false);
            _model.SetVisible(false);
        }

        public void Hide()
        {
            _activeTween?.Kill();
            _activeTween = null;
            _view.SetVisible(false);
            _model.SetVisible(false);
        }

        private async UniTask FadeAsync(float target, float duration,
            CancellationToken cancellationToken)
        {
            _activeTween?.Kill();
            if (duration <= 0f)
            {
                _view.CanvasGroup.alpha = target;
                return;
            }

            var completion = new UniTaskCompletionSource();
            var tween = _view.CanvasGroup.DOFade(target, duration)
                .SetUpdate(true);
            _activeTween = tween;
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
                completion.TrySetCanceled();
            });
            if (cancellationToken.CanBeCanceled)
                registration = cancellationToken.Register(() => tween.Kill());
            try
            {
                await completion.Task;
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                if (ReferenceEquals(_activeTween, tween))
                    _activeTween = null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Hide();
        }
    }
}
