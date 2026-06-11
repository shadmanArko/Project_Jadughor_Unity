using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.Utilities.EventBus;
using UniRx;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Controller
{
    [Serializable]
    public sealed class PlayerController :
        ICollector,
        IInitializable,
        IDisposable
    {
        private readonly PlayerModel _model;
        private readonly PlayerView _view;
        private readonly CollectorRegistry _collectorRegistry;
        private readonly MinePlayerDataConfig _config;
        private readonly MinePlayerScriptable _playerData;
        private readonly RuntimeDataScriptable _runtimeData;
        private readonly CompositeDisposable _disposables = new();

        public Transform CollectionPoint => _view.CollectionPoint;
        public Collider2D CollectorCollider => _view.PlayerCollider;
        public IReadOnlyReactiveProperty<float> PullRadius =>
            _playerData.playerData.collectablePullRadius;

        public PlayerController(
            PlayerModel model,
            PlayerView view,
            CollectorRegistry collectorRegistry,
            MinePlayerDataConfig config,
            MinePlayerScriptable playerData,
            RuntimeDataScriptable runtimeData,
            CinemachineCamera cinemachineCamera)
        {
            _model = model;
            _view = view;
            _collectorRegistry = collectorRegistry;
            _config = config;
            _playerData = playerData;
            _runtimeData = runtimeData;
            
            cinemachineCamera.Follow = _view.transform;
            cinemachineCamera.Lens.OrthographicSize = 2f;
        }
        
        public void Initialize()
        {
            if (!_view.ValidateReferences())
                throw new InvalidOperationException(
                    "PlayerView references are not configured.");

            InitializePlayerData();
            InitializeRuntimeData();
            _view.Configure(_config.climbableLayerMask);
            SubscribeToInputSignals();
            SubscribeToAnimationEvents();
            _collectorRegistry.Register(this);
        }

        private void InitializePlayerData()
        {
            var data = _playerData.playerData;

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
            _runtimeData.canMove.Value = true;
            _runtimeData.canClimb.Value = true;
            _runtimeData.canPerformAction.Value = true;
            _runtimeData.canUsePickaxe.Value = true;
            _runtimeData.canUseWeapon.Value = true;
            _runtimeData.locomotionState.Value =
                PlayerLocomotionState.Idle;
            _runtimeData.actionState.Value = PlayerActionState.None;
            _runtimeData.lifeState.Value = PlayerLifeState.Alive;
            _runtimeData.restrictions.Value =
                PlayerRestrictionFlags.None;
            _runtimeData.movementInput.Value = Vector2.zero;
            _runtimeData.velocity.Value = Vector2.zero;
            _runtimeData.isGrounded.Value = false;
            _runtimeData.isClimbing.Value = false;
            _runtimeData.activeAnimation.Value =
                PlayerAnimationId.None;
            _view.SetGravityScale(_config.normalGravityScale);
        }

        private void SubscribeToInputSignals()
        {
            GlobalEventBus.OnSignal<MovementInputSignal>()
                .Subscribe(signal => _model.SetMovementInput(signal.Direction))
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<ActionInputSignal>()
                .Subscribe(_ => _model.RequestAction())
                .AddTo(_disposables);
            GlobalEventBus.OnSignal<InteractInputSignal>()
                .Subscribe(_ => _model.RequestInteraction())
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

        public bool CanCollect(Item item)
        {
            return _model.CanCollect(item);
        }

        public bool TryCollect(Item item)
        {
            return _model.TryCollect(item);
        }
        
        public void Dispose()
        {
            _collectorRegistry.Unregister(this);
            if (_view != null)
                _view.Stop();

            _disposables.Dispose();
        }
    }
}
