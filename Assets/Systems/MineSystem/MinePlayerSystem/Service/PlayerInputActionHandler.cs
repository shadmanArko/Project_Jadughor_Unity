using System;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.Utilities.EventBus;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Systems.MineSystem.MinePlayerSystem.Service
{
    [Serializable]
    public sealed class PlayerInputActionHandler : IInitializable, ITickable, IDisposable
    {
        private readonly InputSystem_Actions _inputSystem;

        public PlayerInputActionHandler(InputSystem_Actions inputSystem)
        {
            _inputSystem = inputSystem;
        }

        public void Initialize()
        {
            SubscribeToActions();
        }

        private void SubscribeToActions()
        {
            var playerActions = _inputSystem.Player;
            playerActions.Action.performed += OnActionInput;
            playerActions.Action.canceled += OnActionInputReleased;
            playerActions.Interact.performed += OnInteractInput;
            playerActions.Climb.performed += OnClimbInput;

            playerActions.Enable();
        }

        private void UnsubscribeFromActions()
        {
            var playerActions = _inputSystem.Player;

            playerActions.Action.performed -= OnActionInput;
            playerActions.Action.canceled -= OnActionInputReleased;
            playerActions.Interact.performed -= OnInteractInput;
            playerActions.Climb.performed -= OnClimbInput;

            playerActions.Disable();
        }

        public void Tick()
        {
            OnMovementInput();
        }

        private void OnMovementInput()
        {
            GlobalEventBus.Fire(new MovementInputSignal
            {
                Direction = _inputSystem.Player.Move.ReadValue<Vector2>()
            });
        }

        private static void OnActionInput(InputAction.CallbackContext context)
        {
            GlobalEventBus.Fire(new ActionInputSignal
            {
                IsPressed = true
            });
        }

        private static void OnActionInputReleased(
            InputAction.CallbackContext context)
        {
            GlobalEventBus.Fire(new ActionInputSignal
            {
                IsPressed = false
            });
        }

        private static void OnInteractInput(InputAction.CallbackContext context)
        {
            GlobalEventBus.Fire<InteractInputSignal>();
        }

        private static void OnClimbInput(InputAction.CallbackContext context)
        {
            GlobalEventBus.Fire<ClimbInputSignal>();
        }
        
        public void Dispose()
        {
            UnsubscribeFromActions();
        }
    }
}
