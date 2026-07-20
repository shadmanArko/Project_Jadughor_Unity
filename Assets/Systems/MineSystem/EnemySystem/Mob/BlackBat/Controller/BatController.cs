using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Config;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Enum;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Model;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.Service;
using Systems.MineSystem.EnemySystem.Mob.BlackBat.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;

namespace Systems.MineSystem.EnemySystem.Mob.BlackBat.Controller
{
    public sealed class BatController : IEnemyController
    {
        private readonly BatModel _model;
        private readonly BatView _view;
        private readonly BatStateMachine _stateMachine;
        private readonly IEnemyPlacementValidator _placement;
        private readonly IEnemyPathfindingService _pathfinding;
        private readonly BatPauseStateData _pauseState = new();

        private CompositeDisposable _subscriptions;
        private CancellationTokenSource _lifetimeCancellation;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public Guid EnemyId { get; private set; }
        public EnemyType EnemyType => EnemyType.Bat;
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

        public BatController(
            BatView view,
            IEnemyTargetProvider target,
            IEnemyAttackService attack,
            IEnemyPlacementValidator placement,
            IEnemyPathfindingService pathfinding,
            IEnemyChaseTargetResolver chaseResolver,
            BatNavigationService navigation)
        {
            _view = view;
            _placement = placement;
            _pathfinding = pathfinding;
            _model = new BatModel();
            _stateMachine = new BatStateMachine(
                _model,
                view,
                target,
                attack,
                pathfinding,
                placement,
                chaseResolver,
                navigation);
        }

        public void Initialize(EnemyInitializeData initializeData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(BatController));
            if (IsActive)
                Release();
            if (initializeData.Config is not BatConfigScriptable config)
            {
                throw new ArgumentException(
                    "BatController requires BatConfigScriptable.");
            }
            if (!_view.ValidateReferences())
                throw new InvalidOperationException("BatView is not configured.");
            if (!_placement.IsPlacementClear(
                    _view.TerrainCollider,
                    initializeData.SpawnWorldPosition))
            {
                throw new InvalidOperationException(
                    $"Bat cannot fit at spawn cell {initializeData.SpawnGridPosition}.");
            }

            EnemyId = Guid.NewGuid();
            _lifetimeCancellation = new CancellationTokenSource();
            _subscriptions = new CompositeDisposable();
            _model.Initialize(config, initializeData.SpawnGridPosition);
            _view.ResetRuntime();
            _view.Teleport(initializeData.SpawnWorldPosition);
            _view.ApplyConfig(config);
            _view.SetDamageEnabled(false);
            _view.DamageRequested
                .Subscribe(OnDamageRequested)
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
            _stateMachine.SpawnAsync(cancellationToken);

        public UniTask DespawnAsync(CancellationToken cancellationToken)
        {
            if (!IsActive)
                return UniTask.CompletedTask;
            return _stateMachine.DespawnAsync(cancellationToken);
        }

        private void OnDamageRequested(float amount)
        {
            if (!IsActive ||
                _model.CurrentState == BatState.Death ||
                _pauseState.HasSnapshot)
                return;
            _model.ApplyDamage(amount);
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
            _pauseState.Clear();
            _model.ResetRuntime();
            _view.ResetRuntime();
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
