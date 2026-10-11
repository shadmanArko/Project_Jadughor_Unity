using System;
using Systems.MineSystem.BossLairSystem.Interface;
using Systems.MineSystem.BossLairSystem.Service;
using Systems.MineSystem.BossLairSystem.Signal;
using Systems.MineSystem.BossLairSystem.View;
using Systems.MineSystem.Mine.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using Zenject;

namespace Systems.MineSystem.BossLairSystem.Controller
{
    /// <summary>
    /// Connects the shared boss-lair canvas to the active lair boss only while
    /// combat is live. A serial subscription prevents a previous encounter's
    /// health source from changing the UI after exit, regeneration, or reuse.
    /// </summary>
    public sealed class BossLairHealthBarController : IInitializable, IDisposable
    {
        private readonly BossLairCanvasView _view;
        private readonly BossLairBossFactory _bossFactory;
        private readonly CompositeDisposable _lifecycleSubscriptions = new();
        private readonly SerialDisposable _healthSubscription = new();
        private IBossHealthSource _activeHealthSource;
        private bool _disposed;

        public BossLairHealthBarController(
            BossLairCanvasView view,
            BossLairBossFactory bossFactory)
        {
            _view = view;
            _bossFactory = bossFactory;
        }

        public void Initialize()
        {
            if (_view == null || !_view.ValidateReferences())
                throw new InvalidOperationException(
                    "BossLairCanvasView is not configured.");

            _view.Hide();
            GlobalEventBus.OnSignal<BossLairEnteredSignal>()
                .Subscribe(_ => BindActiveBoss())
                .AddTo(_lifecycleSubscriptions);
            GlobalEventBus.OnSignal<BossLairExitedSignal>()
                .Subscribe(_ => ClearHealthBinding())
                .AddTo(_lifecycleSubscriptions);
            GlobalEventBus.OnSignal<MineGeneratedSignal>()
                .Subscribe(_ => ClearHealthBinding())
                .AddTo(_lifecycleSubscriptions);
        }

        private void BindActiveBoss()
        {
            ClearHealthBinding();

            var healthSource = _bossFactory.ActiveHealthSource;
            if (healthSource == null)
                return;

            _activeHealthSource = healthSource;
            RefreshHealth(healthSource);
            if (healthSource.CurrentHealth.Value <= 0)
                return;

            _view.Show();

            var subscriptions = new CompositeDisposable();
            healthSource.MaximumHealth
                .Subscribe(_ => RefreshHealth(healthSource))
                .AddTo(subscriptions);
            healthSource.CurrentHealth
                .Subscribe(_ => RefreshHealth(healthSource))
                .AddTo(subscriptions);
            _healthSubscription.Disposable = subscriptions;
        }

        private void RefreshHealth(IBossHealthSource healthSource)
        {
            if (_disposed || !ReferenceEquals(_activeHealthSource, healthSource))
                return;

            _view.SetHealth(
                healthSource.CurrentHealth.Value,
                healthSource.MaximumHealth.Value);
            if (healthSource.CurrentHealth.Value > 0)
                return;

            // A killing hit hides the canvas before the death animation and
            // rewards state begin. No later state can revive this encounter.
            ClearHealthBinding();
        }

        private void ClearHealthBinding()
        {
            _activeHealthSource = null;
            _healthSubscription.Disposable = null;
            _view?.Hide();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _lifecycleSubscriptions.Dispose();
            ClearHealthBinding();
            _healthSubscription.Dispose();
        }
    }
}
