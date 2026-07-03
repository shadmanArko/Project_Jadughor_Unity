using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Enum;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.PauseSystem.Interface;
using Systems.MineSystem.PauseSystem.Signal;

namespace Systems.MineSystem.MinePlayerSystem.Controller
{
    [Serializable]
    public sealed class PlayerController :
        ICollector,
        IPausable,
        IInitializable,
        IDisposable
    {
        private readonly PlayerModel _model;
        private readonly PlayerView _view;
        private readonly CollectorRegistry _collectorRegistry;
        private readonly MinePlayerDataConfig _config;
        private readonly MinePlayerScriptable _playerScriptable;
        private readonly RuntimeDataScriptable _runtimeData;
        private readonly CompositeDisposable _disposables = new();
        private readonly PlayerPauseStateData _pauseState;
        private readonly PlayerInputActionHandler _inputHandler;
        private readonly PlayerAutoMovementService _autoMovement;
        private bool _isAffectedByPause = true;
        private bool _disposed;

        public bool IsAffectedByPause
        {
            get => _isAffectedByPause;
            set
            {
                if (_isAffectedByPause == value)
                    return;
                _isAffectedByPause = value;
                GlobalEventBus.Fire(
                    new PausableAffectationChangedSignal(this));
            }
        }

        public Transform CollectionPoint => _view.CollectionPoint;
        public Collider2D CollectorCollider => _view.PlayerCollider;
        public IReadOnlyReactiveProperty<float> PullRadius =>
            _playerScriptable.playerData.collectablePullRadius;

        public PlayerController(
            PlayerModel model,
            PlayerView view,
            CollectorRegistry collectorRegistry,
            MinePlayerDataConfig config,
            MinePlayerScriptable playerScriptable,
            RuntimeDataScriptable runtimeData,
            PlayerPauseStateData pauseState,
            PlayerInputActionHandler inputHandler,
            PlayerAutoMovementService autoMovement)
        {
            _model = model;
            _view = view;
            _collectorRegistry = collectorRegistry;
            _config = config;
            _playerScriptable = playerScriptable;
            _runtimeData = runtimeData;
            _pauseState = pauseState;
            _inputHandler = inputHandler;
            _autoMovement = autoMovement;
            _view.gameObject.SetActive(false);
        }
        
        public void Initialize()
        {
            if (!_view.ValidateReferences())
                throw new InvalidOperationException(
                    "PlayerView references are not configured.");

            InitializePlayerData();
            InitializeRuntimeData();
            _view.Configure();
            SubscribeToInputSignals();
            SubscribeToAnimationEvents();
            SubscribeToDamageRequests();
            _collectorRegistry.Register(this);
            GlobalEventBus.Fire(new PausableRegisteredSignal(this));
        }

        private void InitializePlayerData()
        {
            var data = _playerScriptable.playerData;

            data.maxHealth.Value = _config.maxHealth;
            data.health.Value = Mathf.Clamp(_config.health, 0f, data.maxHealth.Value);
            data.maxStamina.Value = _config.maxStamina;
            data.stamina.Value = Mathf.Clamp(_config.stamina, 0f, data.maxStamina.Value);
            data.moveSpeed.Value = _config.moveSpeed;
            data.climbSpeed.Value = _config.climbSpeed;
            data.miningSpeed.Value = _config.miningSpeed;
            data.attackSpeed.Value = _config.attackSpeed;
            data.collectablePullRadius.Value = _config.collectablePullRadius;
            data.unlockedInventorySlots.Value = _config.unlockedInventorySlots;
        }

        private void InitializeRuntimeData()
        {
            _runtimeData.canMove.Value = false;
            _runtimeData.canClimb.Value = false;
            _runtimeData.canPerformAction.Value = false;
            _runtimeData.canUsePickaxe.Value = false;
            _runtimeData.canUseWeapon.Value = false;
            _runtimeData.isSpawned.Value = false;
            _runtimeData.locomotionState.Value =
                PlayerLocomotionState.Idle;
            _runtimeData.actionState.Value = PlayerActionState.None;
            _runtimeData.lifeState.Value = PlayerLifeState.Alive;
            _runtimeData.restrictions.Value =
                PlayerRestrictionFlags.None;
            _runtimeData.movementInput.Value = Vector2.zero;
            _runtimeData.velocity.Value = Vector2.zero;
            _runtimeData.worldPosition.Value =
                _view.PlayerCollider.bounds.center;
            _runtimeData.isGrounded.Value = false;
            _runtimeData.isClimbing.Value = false;
            _runtimeData.isDamagingFall.Value = false;
            _runtimeData.isHurt.Value = false;
            _runtimeData.isInvincible.Value = false;
            _runtimeData.activeAnimation.Value =
                PlayerAnimationId.None;
            _runtimeData.forcedAnimation.Value =
                PlayerAnimationId.None;
            _view.SetGravityScale(_config.normalGravityScale);
        }

        private void SubscribeToInputSignals()
        {
            GlobalEventBus.OnSignal<MovementInputSignal>()
                .Subscribe(signal => _model.SetMovementInput(signal.Direction))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<ClimbInputSignal>()
                .Subscribe(_ => _model.ToggleClimb())
                .AddTo(_disposables);
        }

        private void SubscribeToAnimationEvents()
        {
            _view.AnimationMarkers
                .Subscribe(_model.HandleAnimationMarker)
                .AddTo(_disposables);
            _view.AnimationCompleted
                .Subscribe(_model.HandleAnimationCompleted)
                .AddTo(_disposables);
        }

        private void SubscribeToDamageRequests()
        {
            _view.DamageRequested
                .Subscribe(_model.ApplyDamage)
                .AddTo(_disposables);
        }

        public bool CanCollect(Item item)
        {
            return _model.CanCollect(item);
        }

        public bool TryCollect(Item item)
        {
            return _model.TryCollect(item);
        }

        public void OnPause()
        {
            if (_pauseState.IsPaused)
                return;

            var body = _view.Body;
            _pauseState.HasSnapshot = true;
            _pauseState.Velocity = body.linearVelocity;
            _pauseState.AngularVelocity = body.angularVelocity;
            _pauseState.GravityScale = body.gravityScale;
            _pauseState.BodyWasSimulated = body.simulated;
            _pauseState.MovementInput = _runtimeData.movementInput.Value;
            _pauseState.AnimatorSpeed =
                _view.AnimationController.AnimatorSpeed;
            _pauseState.DamageWasEnabled = _view.DamageEnabled;
            _pauseState.AutoMovementWasPlaying = _autoMovement.Pause();

            _runtimeData.movementInput.Value = Vector2.zero;
            _view.Stop();
            body.angularVelocity = 0f;
            body.simulated = false;
            _view.AnimationController.SetAnimatorSpeed(0f);
            _view.SetDamageEnabled(false);
            _inputHandler.Pause();
        }

        public void OnUnpause()
        {
            if (!_pauseState.HasSnapshot)
                return;

            var body = _view.Body;
            body.simulated = _pauseState.BodyWasSimulated;
            body.gravityScale = _pauseState.GravityScale;
            body.linearVelocity = _pauseState.Velocity;
            body.angularVelocity = _pauseState.AngularVelocity;
            _runtimeData.velocity.Value = _pauseState.Velocity;
            _runtimeData.movementInput.Value = _pauseState.MovementInput;
            _view.AnimationController.SetAnimatorSpeed(
                _pauseState.AnimatorSpeed);
            _view.SetDamageEnabled(_pauseState.DamageWasEnabled);
            _inputHandler.Resume();
            _autoMovement.Resume(_pauseState.AutoMovementWasPlaying);
            _pauseState.ClearSnapshot();
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            GlobalEventBus.Fire(new PausableUnregisteredSignal(this));
            _collectorRegistry.Unregister(this);
            if (_view != null)
                _view.Stop();

            _disposables.Dispose();
        }
    }
}
