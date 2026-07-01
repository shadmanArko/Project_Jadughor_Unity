using System;
using System.Collections.Generic;
using Systems.MineSystem.NotificationSystem.Config;
using Systems.MineSystem.NotificationSystem.View;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.NotificationSystem.Controller
{
    /// <summary>Queues and presents transient mine notifications.</summary>
    public sealed class NotificationController : IInitializable, IDisposable
    {
        private readonly NotificationCanvasView _view;
        private readonly NotificationConfig _config;
        private readonly Queue<string> _pending = new();
        private readonly SerialDisposable _visibleTimer = new();
        private readonly SerialDisposable _fade = new();

        private bool _isDisplaying;
        private bool _isFading;
        private bool _isDisposed;

        public NotificationController(
            NotificationCanvasView view,
            NotificationConfig config)
        {
            _view = view;
            _config = config;
        }

        public void Initialize()
        {
            _view.HideNotification();
        }

        public void ShowNotification(string content)
        {
            if (_isDisposed || string.IsNullOrWhiteSpace(content))
                return;

            _pending.Enqueue(content);
            if (!_isDisplaying)
                ShowNext();
        }

        public void HideNotification()
        {
            if (_isDisposed || !_isDisplaying || _isFading)
                return;

            BeginFade();
        }

        private void ShowNext()
        {
            if (_isDisposed || _pending.Count == 0)
                return;

            _isDisplaying = true;
            _isFading = false;
            _view.ShowNotification(_pending.Dequeue());
            ScheduleFade();
        }

        private void ScheduleFade()
        {
            var duration = Mathf.Max(0f, _config.visibleDuration);
            if (duration <= 0f)
            {
                BeginFade();
                return;
            }

            var startedAt = Time.unscaledTime;
            _visibleTimer.Disposable = Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    if (Time.unscaledTime - startedAt >= duration)
                        BeginFade();
                });
        }

        private void BeginFade()
        {
            if (!_isDisplaying || _isFading)
                return;

            _visibleTimer.Disposable = Disposable.Empty;
            _isFading = true;
            var duration = Mathf.Max(0f, _config.fadeOutDuration);
            if (duration <= 0f)
            {
                CompleteCurrent();
                return;
            }

            var startedAt = Time.unscaledTime;
            _fade.Disposable = Observable.EveryUpdate()
                .Subscribe(_ =>
                {
                    var progress = Mathf.Clamp01(
                        (Time.unscaledTime - startedAt) / duration);
                    _view.SetAlpha(1f - progress);
                    if (progress >= 1f)
                        CompleteCurrent();
                });
        }

        private void CompleteCurrent()
        {
            _visibleTimer.Disposable = Disposable.Empty;
            _fade.Disposable = Disposable.Empty;
            _view.HideNotification();
            _isDisplaying = false;
            _isFading = false;
            ShowNext();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _pending.Clear();
            _visibleTimer.Dispose();
            _fade.Dispose();
            _view.HideNotification();
        }
    }
}
