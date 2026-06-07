using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.CollectableSystem.View;
using Systems.MineSystem.InventorySystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Config;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using UniRx;
using Unity.Cinemachine;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.CollectableSystem.Controller
{
    public sealed class DummyCollectorController :
        ICollector,
        IInitializable,
        IFixedTickable,
        IDisposable
    {
        private readonly DummyCollectorView _view;
        private readonly CollectorRegistry _registry;
        private readonly CollectableSystemConfig _config;
        private readonly MinePlayerScriptable _player;
        private readonly MinePlayerDataConfig _playerConfig;
        private readonly IInventoryService _inventory;
        private readonly InventoryModel _inventoryModel;

        private InputSystem_Actions _input;

        public Transform CollectionPoint => _view.CollectionPoint;
        public Collider2D CollectorCollider => _view.CollectorCollider;
        public IReadOnlyReactiveProperty<float> PullRadius =>
            _player.playerData.collectablePullRadius;

        public DummyCollectorController(
            DummyCollectorView view,
            CollectorRegistry registry,
            CollectableSystemConfig config,
            MinePlayerScriptable player,
            MinePlayerDataConfig playerConfig,
            IInventoryService inventory,
            InventoryModel inventoryModel,
            CinemachineCamera cinemachineCamera)
        {
            _view = view;
            _registry = registry;
            _config = config;
            _player = player;
            _playerConfig = playerConfig;
            _inventory = inventory;
            _inventoryModel = inventoryModel;

            cinemachineCamera.Follow = _view.transform;
            cinemachineCamera.Lens.OrthographicSize = 2f;
        }

        public void Initialize()
        {
            _player.playerData.collectablePullRadius.Value =
                _playerConfig.collectablePullRadius;
            _input = new InputSystem_Actions();
            _input.Player.Enable();
            _registry.Register(this);
        }

        public void FixedTick()
        {
            if (_inventoryModel.IsOpen.Value)
                return;

            var input = _input.Player.Move.ReadValue<Vector2>();
            var target = _view.Body.position +
                         input * (_config.dummyPlayerMoveSpeed * Time.fixedDeltaTime);
            _view.Body.MovePosition(target);
        }

        public bool CanCollect(Item item) => _inventory.CanAdd(item);

        public bool TryCollect(Item item)
        {
            return _inventory.TryAdd(item);
        }

        public void Dispose()
        {
            _registry.Unregister(this);
            _input?.Player.Disable();
            _input?.Dispose();
        }
    }
}
