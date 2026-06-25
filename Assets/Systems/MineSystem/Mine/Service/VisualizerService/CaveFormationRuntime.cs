using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.Damage;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.Mine.View;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Model;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.Mine.Service.VisualizerService
{
    public abstract class CaveFormationRuntime :
        MonoBehaviour,
        IPlaceableRuntime
    {
        private readonly CompositeDisposable _disposables = new();
        private Action<IPlaceableRuntime> _releaseAction;
        private float _health;
        private bool _isBreaking;
        private CancellationTokenSource _lifetime;

        protected CaveFormationConfig Config { get; private set; }
        protected MineView MineView { get; private set; }
        protected MineData MineData { get; private set; }
        protected Cell Cell { get; private set; }
        protected Animator Animator { get; private set; }

        public PlaceableSpawnContext Context { get; private set; }
        public IPlaceableDamageView DamageView { get; private set; }
        public Vector3Int CellPosition { get; private set; }
        public string CellId => Cell?.Id;
        public string RootCellId { get; private set; }

        public virtual void Initialize(PlaceableSpawnContext context)
        {
            Context = context;
            transform.position = context.WorldPosition;
            gameObject.SetActive(true);
        }

        public void InitializeFormation(
            PlaceableSpawnContext context,
            MineData mineData,
            MineView mineView,
            CaveFormationConfig config,
            Cell cell,
            string rootCellId)
        {
            DisposeRuntime();
            MineData = mineData;
            MineView = mineView;
            Config = config;
            Cell = cell;
            CellPosition = cell.GetPosition();
            RootCellId = rootCellId;
            _health = config.RandomHealth;
            _isBreaking = false;
            _lifetime = new CancellationTokenSource();

            EnsureRuntimeComponents();
            Initialize(context);
            Animator?.Play(config.intactState, 0, 0f);

            DamageView.DamageRequested
                .Subscribe(ApplyFormationDamage)
                .AddTo(_disposables);
        }

        public void SetReleaseAction(Action<IPlaceableRuntime> releaseAction)
        {
            _releaseAction = releaseAction;
        }

        public virtual void Release()
        {
            DisposeRuntime();
            _releaseAction?.Invoke(this);
        }

        public void BreakFromRoot()
        {
            BreakAsync(_lifetime?.Token ?? CancellationToken.None)
                .Forget(LogUnhandledException);
        }

        public virtual void HandleTriggerEnter(Collider2D other)
        {
        }

        protected abstract UniTask BreakAsync(CancellationToken cancellationToken);

        protected async UniTask PlayStateAsync(
            string stateName,
            float duration,
            CancellationToken cancellationToken)
        {
            if (Animator != null && !string.IsNullOrWhiteSpace(stateName))
                Animator.Play(stateName, 0, 0f);

            if (duration <= 0f)
                return;

            await UniTask.Delay(
                TimeSpan.FromSeconds(duration),
                cancellationToken: cancellationToken);
        }

        protected bool TryBeginBreak()
        {
            if (_isBreaking)
                return false;

            _isBreaking = true;
            return true;
        }

        protected void FinishBreak()
        {
            Release();
        }

        protected bool TryDamageTarget(
            Collider2D collider,
            float damage)
        {
            if (collider == null || damage <= 0f)
                return false;

            foreach (var behaviour in
                     collider.GetComponentsInParent<MonoBehaviour>())
            {
                if (behaviour is IDamageable damageable &&
                    !ReferenceEquals(damageable, DamageView))
                {
                    damageable.ApplyDamage(damage);
                    return true;
                }
            }

            return false;
        }

        protected virtual void OnDisable()
        {
            DisposeRuntime();
        }

        protected virtual void OnDestroy()
        {
            DisposeRuntime();
        }

        private void ApplyFormationDamage(float amount)
        {
            if (amount <= 0f || _isBreaking)
                return;

            _health = Mathf.Max(0f, _health - amount);
            if (_health > 0f)
                return;

            BreakAsync(_lifetime?.Token ?? CancellationToken.None)
                .Forget(LogUnhandledException);
        }

        private void EnsureRuntimeComponents()
        {
            DamageView = GetComponentInChildren<CaveFormationDamageView>(true);
            if (DamageView == null)
                DamageView = gameObject.AddComponent<CaveFormationDamageView>();

            Animator = GetComponentInChildren<Animator>(true);
            foreach (var collider in GetComponentsInChildren<Collider2D>(true))
            {
                var relay = collider.GetComponent<CaveFormationContactRelay>();
                if (relay == null)
                    relay = collider.gameObject.AddComponent<CaveFormationContactRelay>();
                relay.Configure(this);
            }
        }

        private void DisposeRuntime()
        {
            _disposables.Clear();
            if (_lifetime != null)
            {
                if (!_lifetime.IsCancellationRequested)
                    _lifetime.Cancel();
                _lifetime.Dispose();
                _lifetime = null;
            }
        }

        private static void LogUnhandledException(Exception exception)
        {
            if (exception is not OperationCanceledException)
                Debug.LogException(exception);
        }
    }
}
