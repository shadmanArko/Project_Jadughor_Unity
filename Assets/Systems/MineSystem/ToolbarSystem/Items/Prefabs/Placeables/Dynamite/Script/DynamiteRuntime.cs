using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using Systems.MineSystem.ToolbarSystem.Service;
using UniRx;
using UnityEngine;
using Zenject;
using DG.Tweening;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class DynamiteRuntime :
        PausablePlaceableRuntime,
        IPlaceableRuntime
    {
        [SerializeField] private DynamiteView view;

        private DynamiteExplosionService _explosionService;
        private PlaceableItemizationService _itemization;
        private Action<IPlaceableRuntime> _releaseAction;
        private CancellationTokenSource _countdown;
        private PlaceableSpawnContext _context;
        private DynamiteConfig _config;
        private bool _detonating;
        private CircleCollider2D _damageCollider;
        private readonly CompositeDisposable _damageSubscriptions = new();
        private Tween _countdownDelay;
        private bool _countdownWasPlaying;

        public override IPlaceableDamageView DamageView => view;

        [Inject]
        public void Construct(
            DynamiteExplosionService explosionService,
            PlaceableItemizationService itemization)
        {
            _explosionService = explosionService;
            _itemization = itemization;
        }

        public void Initialize(PlaceableSpawnContext context)
        {
            ResetRuntime();

            var profile = context.Profile as DynamiteActionProfile;
            if (profile?.Config == null || view == null)
            {
                Debug.LogError(
                    "Dynamite requires a DynamiteActionProfile and view.",
                    this);
                return;
            }

            _context = context;
            _config = profile.Config;
            _detonating = false;

            transform.position = context.WorldPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            EnsureCollider();
            _damageCollider.radius = _config.ColliderRadius;
            view.Configure(_config);
            view.ConfigureItemization(() =>
                _itemization.TryConvert(
                    _context,
                    transform.position));
            view.DamageRequested
                .Where(amount => amount > 0f)
                .Take(1)
                .Subscribe(_ => BeginDetonation())
                .AddTo(_damageSubscriptions);
            gameObject.SetActive(true);

            _countdown = new CancellationTokenSource();
            RunCountdownAsync(_countdown.Token)
                .Forget(exception =>
                {
                    if (exception is not OperationCanceledException)
                        Debug.LogException(exception);
                });
        }

        public void SetReleaseAction(
            Action<IPlaceableRuntime> releaseAction)
        {
            _releaseAction = releaseAction;
        }

        public void Release()
        {
            _releaseAction?.Invoke(this);
        }

        private async UniTask RunCountdownAsync(
            CancellationToken cancellationToken)
        {
            for (var remaining = _config.CountdownSeconds;
                 remaining >= 0;
                 remaining--)
            {
                view.PresentCountdown(remaining);
                if (remaining == 0)
                    break;

                await AwaitDelayAsync(_config.TickInterval, cancellationToken);
            }

            BeginDetonation();
        }

        private async UniTask AwaitDelayAsync(
            float seconds,
            CancellationToken cancellationToken)
        {
            var tween = DOVirtual.DelayedCall(
                Mathf.Max(0f, seconds),
                () => { },
                false);
            _countdownDelay = tween;
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
                else
                    completion.TrySetResult();
            });
            if (cancellationToken.CanBeCanceled)
                registration = cancellationToken.Register(() => tween.Kill());
            await completion.Task;
            if (ReferenceEquals(_countdownDelay, tween))
                _countdownDelay = null;
        }

        public override void OnPause()
        {
            base.OnPause();
            _countdownWasPlaying = _countdownDelay != null &&
                                   _countdownDelay.IsActive() &&
                                   _countdownDelay.IsPlaying();
            if (_countdownWasPlaying)
                _countdownDelay.Pause();
        }

        public override void OnUnpause()
        {
            base.OnUnpause();
            if (_countdownWasPlaying && _countdownDelay != null &&
                _countdownDelay.IsActive())
                _countdownDelay.Play();
            _countdownWasPlaying = false;
        }

        private void BeginDetonation()
        {
            if (_detonating || _config == null)
                return;

            _detonating = true;
            _countdown?.Cancel();

            var context = _context;
            var config = _config;

            Release();
            _explosionService.Detonate(context, config);
        }

        private void EnsureCollider()
        {
            if (_damageCollider != null)
                return;

            _damageCollider = GetComponent<CircleCollider2D>();
            if (_damageCollider == null)
                _damageCollider =
                    gameObject.AddComponent<CircleCollider2D>();
            _damageCollider.isTrigger = true;
        }

        private void OnDisable()
        {
            ClearPauseState();
            ResetRuntime();
        }

        private void ResetRuntime()
        {
            _countdownDelay?.Kill();
            _countdownDelay = null;
            _countdownWasPlaying = false;
            if (_countdown != null)
            {
                if (!_countdown.IsCancellationRequested)
                    _countdown.Cancel();
                _countdown.Dispose();
                _countdown = null;
            }

            view?.ResetView();
            view?.ClearItemization();
            _damageSubscriptions.Clear();
            _context = default;
            _config = null;
            _detonating = false;
        }
    }
}
