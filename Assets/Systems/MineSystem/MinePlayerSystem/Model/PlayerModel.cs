using Systems.MineSystem.InventorySystem.Interface;
using Systems.MineSystem.InventorySystem.Model;
using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
using Systems.MineSystem.MinePlayerSystem.View;
using UnityEngine;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Model
{
    public sealed class PlayerModel : IFixedTickable
    {
        private readonly RuntimeDataScriptable _runtime;
        private readonly PlayerGroundingService _groundingService;
        private readonly PlayerFallService _fallService;
        private readonly PlayerDeathService _deathService;
        private readonly PlayerDamageService _damageService;
        private readonly PlayerClimbService _climbService;
        private readonly PlayerActionService _actionService;
        private readonly PlayerMovementService _movementService;
        private readonly IInventoryService _inventory;
        private readonly PlayerView _view;

        public PlayerModel(
            RuntimeDataScriptable runtime,
            PlayerGroundingService groundingService,
            PlayerFallService fallService,
            PlayerDeathService deathService,
            PlayerDamageService damageService,
            PlayerClimbService climbService,
            PlayerActionService actionService,
            PlayerMovementService movementService,
            IInventoryService inventory,
            PlayerView view)
        {
            _runtime = runtime;
            _groundingService = groundingService;
            _fallService = fallService;
            _deathService = deathService;
            _damageService = damageService;
            _climbService = climbService;
            _actionService = actionService;
            _movementService = movementService;
            _inventory = inventory;
            _view = view;
        }

        public bool CanCollect(Item item)
        {
            return _inventory.CanAdd(item);
        }

        public bool TryCollect(Item item)
        {
            return _inventory.TryAdd(item);
        }

        public void ApplyDamage(float amount)
        {
            _damageService.ApplyDamage(amount);
        }

        public void SetMovementInput(Vector2 direction)
        {
            _runtime.movementInput.Value =
                Vector2.ClampMagnitude(direction, 1f);
        }

        public void ToggleClimb()
        {
            _climbService.ToggleClimb();
        }

        public void RequestAction()
        {
            _actionService.RequestAction();
        }

        public void RequestInteraction()
        {
            _actionService.RequestInteraction();
        }

        public void HandleAnimationMarker(
            PlayerAnimationMarkerEvent animationEvent)
        {
            _actionService.HandleAnimationMarker(animationEvent);
        }

        public void HandleAnimationCompleted(
            PlayerAnimationCompletedEvent animationEvent)
        {
            _actionService.HandleAnimationCompleted(animationEvent);
            _damageService.HandleAnimationCompleted(animationEvent);
            _deathService.HandleAnimationCompleted(animationEvent);
        }

        public void ResetVerticalState(Vector2 position)
        {
            _fallService.ResetVerticalState();
        }

        public void FixedTick()
        {
            _groundingService.OnFixedTick();
            _fallService.OnFixedTick();
            _deathService.OnFixedTick();
            _climbService.OnFixedTick();
            _actionService.OnFixedTick();
            _movementService.OnFixedTick();
            _runtime.worldPosition.Value =
                _view.PlayerCollider.bounds.center;
        }
    }
}
