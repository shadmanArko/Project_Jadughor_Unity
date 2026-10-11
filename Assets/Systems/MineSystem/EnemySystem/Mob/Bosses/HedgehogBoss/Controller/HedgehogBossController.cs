using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Systems.MineSystem.ActorSystem.Interface;
using Systems.MineSystem.ActorSystem.Service;
using Systems.MineSystem.BossLairSystem.Interface;
using Systems.MineSystem.BossLairSystem.Signal;
using Systems.MineSystem.EnemySystem.Enum;
using Systems.MineSystem.EnemySystem.Interface;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Config;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Model;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Service;
using Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.View;
using Systems.MineSystem.EnemySystem.Model;
using Systems.MineSystem.Mine.Model;
using Systems.MineSystem.PauseSystem.Enum;
using Systems.MineSystem.PauseSystem.Signal;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.EnemySystem.Mob.HedgehogBoss.Controller
{
    /// <summary>
    /// Thin lair-boss facade. This implements the existing enemy and cutscene
    /// contracts without joining EnemyManager; combat state, decision making,
    /// projectiles, and rewards are owned by <see cref="HedgehogBossStateMachine"/>.
    /// </summary>
    public sealed class HedgehogBossController :
        IEnemyController,
        IActor,
        IBossHealthSource
    {
        private readonly HedgehogBossView _view;
        private readonly ActorMovementTweenRunner _movement = new();
        private readonly HedgehogBossPauseStateData _pauseState = new();
        private readonly HedgehogBossModel _model = new();
        private readonly HedgehogSpikeProjectilePool _projectiles;
        private readonly HedgehogBossStateMachine _stateMachine;

        private CompositeDisposable _subscriptions;
        private HedgehogBossConfigScriptable _config;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public Guid EnemyId { get; private set; }
        public EnemyType EnemyType => EnemyType.Boss;
        public bool IsActive { get; private set; }
        public bool IsDead => _model.IsDead;
        public GridPosition CurrentGridPosition { get; private set; }
        public Vector2 Position => _view.Body.position;
        public IReadOnlyReactiveProperty<int> CurrentHealth => _model.Health;
        public IReadOnlyReactiveProperty<int> MaximumHealth => _model.MaximumHealth;

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value)
                    return;
                _isAffectedByPause = value;
                if (IsActive)
                    GlobalEventBus.Fire(new PausableAffectationChangedSignal(this));
            }
        }

        public HedgehogBossController(
            HedgehogBossView view,
            Transform arenaRoot,
            DiContainer container,
            IEnemyTargetProvider target,
            IEnemyAttackService attack,
            HedgehogBossRewardService rewards)
        {
            _view = view;
            _projectiles = new HedgehogSpikeProjectilePool(
                container,
                arenaRoot,
                null,
                initialSize: 0);
            _stateMachine = new HedgehogBossStateMachine(
                _model,
                view,
                target,
                attack,
                rewards,
                _projectiles);
        }

        public void Initialize(EnemyInitializeData initializeData)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HedgehogBossController));
            if (IsActive)
                throw new InvalidOperationException(
                    "A Hedgehog boss controller is one lair encounter and cannot be reinitialized.");
            if (initializeData.Config is not HedgehogBossConfigScriptable config)
            {
                throw new ArgumentException(
                    "HedgehogBossController requires HedgehogBossConfigScriptable.");
            }
            if (!_view.ValidateReferences())
                throw new InvalidOperationException("HedgehogBossView is not configured.");
            if (!config.Validate(out var validationError))
                throw new InvalidOperationException(
                    $"Hedgehog boss config is invalid: {validationError}");

            _config = config;
            EnemyId = Guid.NewGuid();
            CurrentGridPosition = initializeData.SpawnGridPosition;
            _subscriptions = new CompositeDisposable();
            _view.ResetRuntime();
            _view.Teleport(initializeData.SpawnWorldPosition);
            _view.ApplyConfig(config);
            // The arena is built before a visit begins. Keep its dynamic boss
            // dormant until the entry intro completes, then snap it to the
            // generated floor and re-enable simulation in ActivateCombat.
            _view.SetPhysicsSimulation(false);
            _view.SetDamageEnabled(false);
            _view.DamageRequested
                .Subscribe(_stateMachine.ApplyDamage)
                .AddTo(_subscriptions);
            _view.AnimationMarkers
                .Subscribe(_stateMachine.HandleAnimationMarker)
                .AddTo(_subscriptions);
            _view.AnimationCompleted
                .Subscribe(_stateMachine.HandleAnimationCompleted)
                .AddTo(_subscriptions);
            _view.CollisionEntered
                .Subscribe(_stateMachine.HandleCollision)
                .AddTo(_subscriptions);
            GlobalEventBus.OnSignal<BossLairEnteredSignal>()
                .Where(signal => signal.Profile != null &&
                    signal.Profile.BossVariant == config.BossVariant)
                .Subscribe(_ => _stateMachine.ActivateCombat())
                .AddTo(_subscriptions);

            try
            {
                _stateMachine.Initialize(config, EnemyId);
                _projectiles.Configure(
                    config.SpikeProjectilePrefab,
                    config.InitialSpikeProjectilePoolSize);
            }
            catch
            {
                _subscriptions.Dispose();
                _subscriptions = null;
                _stateMachine.Release();
                _view.ResetRuntime();
                throw;
            }

            IsActive = true;
            GlobalEventBus.Fire(
                new PausableRegisteredSignal(this, PauseArea.BossLair));
        }

        public void OnFixedTick(EnemyTickContext tickContext)
        {
            if (IsActive && !_pauseState.HasSnapshot)
                _stateMachine.OnFixedTick(tickContext);
        }

        public UniTask SpawnAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask DespawnAsync(CancellationToken cancellationToken) =>
            UniTask.CompletedTask;

        public UniTask MoveToAsync(
            Vector2 destination,
            float duration,
            Ease ease,
            CancellationToken cancellationToken) =>
            _movement.MoveAsync(
                _view.Body, destination, duration, ease, cancellationToken);

        public void PlayAnimation(string animationId, bool facesLeft)
        {
            _view.SetFacing(facesLeft);
            if (_config != null && _config.AnimationProfile != null &&
                _config.AnimationProfile.TryGet(animationId, out var data))
            {
                _view.Play(data, restart: true);
                return;
            }

            Debug.LogWarning(
                $"[HedgehogBoss] animation '{animationId}' is missing from its profile.",
                _view);
        }

        public void ClearAnimation() =>
            PlayAnimation(HedgehogBossAnimationId.Idle, facesLeft: false);

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
            _pauseState.MovementWasPlaying = _movement.Pause();
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
            _movement.Resume(_pauseState.MovementWasPlaying);
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
            _subscriptions?.Dispose();
            _subscriptions = null;
            _stateMachine.Release();
            _pauseState.Clear();
            _view.ResetRuntime();
            _config = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            Release();
            _disposed = true;
            _movement.Dispose();
            _stateMachine.Dispose();
            _projectiles.Dispose();
            _model.Dispose();
        }
    }
}
