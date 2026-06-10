using Systems.MineSystem.MinePlayerSystem.Service;
using Systems.MineSystem.MinePlayerSystem.Scriptable;
using Systems.MineSystem.MinePlayerSystem.SubSystem.PlayerAnimationSubSystem.Model;
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
        private readonly PlayerClimbService _climbService;
        private readonly PlayerActionService _actionService;
        private readonly PlayerMovementService _movementService;

        public PlayerModel(
            RuntimeDataScriptable runtime,
            PlayerGroundingService groundingService,
            PlayerFallService fallService,
            PlayerDeathService deathService,
            PlayerClimbService climbService,
            PlayerActionService actionService,
            PlayerMovementService movementService)
        {
            _runtime = runtime;
            _groundingService = groundingService;
            _fallService = fallService;
            _deathService = deathService;
            _climbService = climbService;
            _actionService = actionService;
            _movementService = movementService;
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
        }
    }
}
