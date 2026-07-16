using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.Config;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.Model;
using Systems.MineSystem.EnemySystem.Mob.GreenSlime.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.GreenSlime.Controller
{
    public sealed class SlimeController : IEnemyController
    {
        private readonly SlimeModel _model;
        private readonly SlimeView _view;
        private readonly SlimeStateMachine _stateMachine;
        private readonly IEnemyPlacementValidator _placement;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly SlimePauseStateData _pauseState = new();

        private CompositeDisposable _subscriptions;
        private CancellationTokenSource _lifetimeCancellation;
        private bool _isAffectedByPause = true;
        private SlimeConfigScriptable _config;
        private bool _disposed;

        public Guid EnemyId { get; private set; }
        public EnemyType EnemyType => EnemyType.Slime;
        public bool IsActive { get; private set; }
        public bool IsDead => _model.IsDead;
        public GridPosition CurrentGridPosition => _model.CurrentGridPosition;

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value)
                    return;
                _isAffectedByPause = value;
                if (IsActive)
                {
                    GlobalEventBus.Fire(
                        new PausableAffectationChangedSignal(this));
                }
            }
        }

        public SlimeController(
            SlimeView view,
            IEnemyTargetProvider target,
            IEnemyAttackService attack,
            IEnemyPlacementValidator placement,
            IEnemyPathfindingService pathfinding,
            IEnemyChaseTargetResolver chaseResolver)
        {
            _view = view;
            _placement = placement;
            _pathfinding = pathfinding;
            _model = new SlimeModel();
            _stateMachine = new SlimeStateMachine(
                _model,
                view,
                target,
                attack,
                pathfinding,
                placement,
                chaseResolver);
        }

        public void Initialize(EnemyInitializeData initializeData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(SlimeController));
            if (IsActive)
                Release();
            if (initializeData.Config is not SlimeConfigScriptable config)
            {
                throw new ArgumentException(
                    "SlimeController requires SlimeConfigScriptable.");
            }
            if (!_view.ValidateReferences())
                throw new InvalidOperationException("SlimeView is not configured.");

            EnemyId = Guid.NewGuid();
            _lifetimeCancellation = new CancellationTokenSource();
            _subscriptions = new CompositeDisposable();
            _model.Initialize(config, initializeData.SpawnGridPosition);
            _config = config;
            _view.ResetRuntime();
            if (!_placement.IsPlacementClear(
                    _view.TerrainCollider,
                    initializeData.SpawnWorldPosition))
            {
                throw new InvalidOperationException(
                    $"Slime cannot fit at spawn cell {initializeData.SpawnGridPosition}.");
            }
            _view.transform.position = initializeData.SpawnWorldPosition;
            _view.ApplyConfig(config);
            _view.SetDamageEnabled(false);
            _view.DamageRequested.Subscribe(OnDamageRequested).AddTo(_subscriptions);
            _view.HorizontalCollision
                .Subscribe(_stateMachine.HandleHorizontalCollision)
                .AddTo(_subscriptions);
            _view.AnimationMarkers
                .Subscribe(_stateMachine.HandleAnimationMarker)
                .AddTo(_subscriptions);
            _view.AnimationCompleted
                .Subscribe(_stateMachine.HandleAnimationCompleted)
                .AddTo(_subscriptions);
            _pathfinding.NavigationChanged
                .Subscribe(_stateMachine.HandleNavigationChanged)
                .AddTo(_subscriptions);
            _stateMachine.Initialize(
                config,
                EnemyId,
                _lifetimeCancellation.Token);
            IsActive = true;
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        public void OnFixedTick(EnemyTickContext tickContext)
        {
            if (IsActive && !_pauseState.HasSnapshot)
                _stateMachine.OnFixedTick(tickContext);
        }

        public UniTask SpawnAsync(CancellationToken cancellationToken) =>
            SpawnWithTraceAsync(cancellationToken);

        public UniTask DespawnAsync(CancellationToken cancellationToken)
        {
            if (!IsActive || IsDead)
                return UniTask.CompletedTask;
            _view.SetDamageEnabled(false);
            return _stateMachine.DespawnAsync(cancellationToken);
        }

        private void OnDamageRequested(float amount)
        {
            if (!IsActive || IsDead || _pauseState.HasSnapshot)
                return;
            _model.ApplyDamage(amount);
            if (_model.IsDead)
                _stateMachine.EnterDeath();
            else
                _stateMachine.EnterHurt();
        }

        public void OnPause()
        {
            if (!IsActive || _pauseState.HasSnapshot)
                return;
            var body = _view.Body;
            _pauseState.HasSnapshot = true;
            _pauseState.BodyWasSimulated = body.simulated;
            _pauseState.Velocity = body.linearVelocity;
            _pauseState.AngularVelocity = body.angularVelocity;
            _pauseState.AnimatorSpeed = _view.AnimatorSpeed;
            _pauseState.DamageWasEnabled = _view.DamageEnabled;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
            _view.SetAnimatorSpeed(0f);
            _view.SetDamageEnabled(false);
            _stateMachine.Pause();
        }

        public void OnUnpause()
        {
            if (!IsActive || !_pauseState.HasSnapshot)
                return;
            var body = _view.Body;
            body.simulated = _pauseState.BodyWasSimulated;
            body.linearVelocity = _pauseState.Velocity;
            body.angularVelocity = _pauseState.AngularVelocity;
            _view.SetAnimatorSpeed(_pauseState.AnimatorSpeed);
            _view.SetDamageEnabled(_pauseState.DamageWasEnabled);
            _pauseState.Clear();
            _stateMachine.Resume();
        }

        public void Release()
        {
            if (!IsActive)
                return;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            IsActive = false;
            _lifetimeCancellation?.Cancel();
            _lifetimeCancellation?.Dispose();
            _lifetimeCancellation = null;
            _subscriptions?.Dispose();
            _subscriptions = null;
            _stateMachine.Release();
            _config = null;
            _pauseState.Clear();
            _model.ResetRuntime();
            _view.ResetRuntime();
        }

        private async UniTask SpawnWithTraceAsync(
            CancellationToken cancellationToken)
        {
            await _stateMachine.SpawnAsync(cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Release();
            _disposed = true;
            _stateMachine.Dispose();
            _model.Dispose();
        }
    }
}
