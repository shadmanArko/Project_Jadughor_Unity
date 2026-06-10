using System;
using Systems.MineSystem.MinePlayerSystem.Model;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.MineSystem.MinePlayerSystem.View;
using Systems.Utilities.EventBus;
using UniRx;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Controller
{
    public sealed class PlayerController : IInitializable, IDisposable
    {
        private readonly PlayerModel _model;
        private readonly PlayerView _view;
        private readonly MinePlayerDataConfig _config;
        private readonly MinePlayerScriptable _playerData;
        private readonly RuntimeDataScriptable _runtimeData;
        private readonly CompositeDisposable _disposables = new();

        public PlayerController(
            PlayerModel model,
            PlayerView view,
            MinePlayerDataConfig config,
            MinePlayerScriptable playerData,
            RuntimeDataScriptable runtimeData)
        {
            _model = model;
            _view = view;
            _config = config;
            _playerData = playerData;
            _runtimeData = runtimeData;
        }
        
        public void Initialize()
        {
            InitializePlayerData();
            InitializeRuntimeData();
            SubscribeToInputSignals();
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
        }

        private void SubscribeToInputSignals()
        {
            GlobalEventBus.OnSignal<MovementInputSignal>()
                .Subscribe(signal => _model.SetMovementInput(signal.Direction))
                .AddTo(_disposables);
        }
        
        public void Dispose()
        {
            _view.Stop();
            _disposables.Dispose();
        }
    }
}
