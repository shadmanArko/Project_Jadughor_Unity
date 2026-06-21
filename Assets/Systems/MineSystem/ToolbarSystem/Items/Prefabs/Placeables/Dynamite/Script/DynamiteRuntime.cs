using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Damage;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Dynamite.Script
{
    public sealed class DynamiteRuntime :
        MonoBehaviour,
        IPlaceableRuntime,
        IDamageable
    {
        [SerializeField] private DynamiteView view;

        private DynamiteExplosionService _explosionService;
        private Action<IPlaceableRuntime> _releaseAction;
        private CancellationTokenSource _countdown;
        private PlaceableSpawnContext _context;
        private DynamiteConfig _config;
        private bool _detonating;
        private CircleCollider2D _damageCollider;

        [Inject]
        public void Construct(
            DynamiteExplosionService explosionService)
        {
            _explosionService = explosionService;
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

        public void ApplyDamage(float amount)
        {
            if (amount > 0f)
                BeginDetonation();
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

                await UniTask.Delay(
                    TimeSpan.FromSeconds(_config.TickInterval),
                    cancellationToken: cancellationToken);
            }

            BeginDetonation();
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
            ResetRuntime();
        }

        private void ResetRuntime()
        {
            if (_countdown != null)
            {
                if (!_countdown.IsCancellationRequested)
                    _countdown.Cancel();
                _countdown.Dispose();
                _countdown = null;
            }

            view?.ResetView();
            _context = default;
            _config = null;
            _detonating = false;
        }
    }
}
