using System;
using Systems.MineSystem.MinePlayerSystem.Signal.InputSignal;
using Systems.MineSystem.MinePlayerSystem.Model;
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
        private readonly PlayerPauseStateData _pauseState;

        public PlayerInputActionHandler(
            InputSystem_Actions inputSystem,
            PlayerPauseStateData pauseState)
        {
            _inputSystem = inputSystem;
            _pauseState = pauseState;
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
            if (_pauseState.IsPaused)
                return;
            OnMovementInput();
        }

        private void OnMovementInput()
        {
            GlobalEventBus.Fire(new MovementInputSignal
            {
                Direction = _inputSystem.Player.Move.ReadValue<Vector2>()
            });
        }

        private void OnActionInput(InputAction.CallbackContext context)
        {
            if (_pauseState.IsPaused)
                return;
            GlobalEventBus.Fire(new ActionInputSignal
            {
                IsPressed = true
            });
        }

        private void OnActionInputReleased(
            InputAction.CallbackContext context)
        {
            if (_pauseState.IsPaused)
                return;
            GlobalEventBus.Fire(new ActionInputSignal
            {
                IsPressed = false
            });
        }

        private void OnInteractInput(InputAction.CallbackContext context)
        {
            if (_pauseState.IsPaused)
                return;
            GlobalEventBus.Fire<InteractInputSignal>();
        }

        private void OnClimbInput(InputAction.CallbackContext context)
        {
            if (_pauseState.IsPaused)
                return;
            GlobalEventBus.Fire<ClimbInputSignal>();
        }

        public void Pause()
        {
            _pauseState.PlayerMapWasEnabled = _inputSystem.Player.enabled;
            _pauseState.IsPaused = true;
            if (_pauseState.PlayerMapWasEnabled)
                _inputSystem.Player.Disable();
        }

        public void Resume()
        {
            _pauseState.IsPaused = false;
            if (_pauseState.PlayerMapWasEnabled)
                _inputSystem.Player.Enable();
        }
        
        public void Dispose()
        {
            UnsubscribeFromActions();
        }
    }
}
