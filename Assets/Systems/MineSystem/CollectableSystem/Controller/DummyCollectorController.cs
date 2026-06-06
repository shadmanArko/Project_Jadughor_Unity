using System;
using Systems.MineSystem.CollectableSystem.Interface;
using Systems.MineSystem.CollectableSystem.Scriptable;
using Systems.MineSystem.CollectableSystem.Service;
using Systems.MineSystem.CollectableSystem.View;
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
            CinemachineCamera cinemachineCamera)
        {
            _view = view;
            _registry = registry;
            _config = config;
            _player = player;
            _playerConfig = playerConfig;

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
            var input = _input.Player.Move.ReadValue<Vector2>();
            var target = _view.Body.position +
                         input * (_config.dummyPlayerMoveSpeed * Time.fixedDeltaTime);
            _view.Body.MovePosition(target);
        }

        public bool CanCollect(Item item) => item != null;

        public bool TryCollect(Item item)
        {
            return item != null;
        }

        public void Dispose()
        {
            _registry.Unregister(this);
            _input?.Player.Disable();
            _input?.Dispose();
        }
    }
}
