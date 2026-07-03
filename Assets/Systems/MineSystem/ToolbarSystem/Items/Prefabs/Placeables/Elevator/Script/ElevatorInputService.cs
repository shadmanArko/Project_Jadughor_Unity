using System;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Items.Prefabs.Placeables.Elevator.Script
{
    public sealed class ElevatorInputService : IInitializable, IDisposable
    {
        private readonly InputSystem_Actions _inputSystem;
        private readonly BehaviorSubject<int> _verticalDirection = new(0);
        private readonly Subject<Unit> _interactRequested = new();

        private InputActionMap _elevatorMap;
        private InputAction _moveAction;
        private InputAction _interactAction;

        public ElevatorInputService(InputSystem_Actions inputSystem)
        {
            _inputSystem = inputSystem;
        }

        public IObservable<int> VerticalDirection => _verticalDirection;
        public IObservable<Unit> InteractRequested => _interactRequested;

        public void Initialize()
        {
            EnsureElevatorMap();
            _elevatorMap.Disable();
        }

        public void EnableElevator()
        {
            EnsureElevatorMap();
            _inputSystem.Player.Disable();
            _elevatorMap.Enable();
        }

        public void DisableElevator()
        {
            EnsureElevatorMap();
            _elevatorMap.Disable();
            _inputSystem.Player.Enable();
        }

        private void EnsureElevatorMap()
        {
            if (_elevatorMap != null)
                return;

            _elevatorMap = _inputSystem.asset.FindActionMap(
                "Elevator",
                false);

            if (_elevatorMap == null)
            {
                _elevatorMap = new InputActionMap("Elevator");
                _inputSystem.asset.AddActionMap(_elevatorMap);
            }

            _moveAction = _elevatorMap.FindAction("Move", false) ??
                          _elevatorMap.AddAction(
                              "Move",
                              InputActionType.Value);
            if (_moveAction.bindings.Count == 0)
            {
                _moveAction.AddCompositeBinding("2DVector")
                    .With("Up", "<Keyboard>/w")
                    .With("Up", "<Keyboard>/upArrow")
                    .With("Down", "<Keyboard>/s")
                    .With("Down", "<Keyboard>/downArrow")
                    .With("Left", "<Keyboard>/a")
                    .With("Left", "<Keyboard>/leftArrow")
                    .With("Right", "<Keyboard>/d")
                    .With("Right", "<Keyboard>/rightArrow");
                _moveAction.AddBinding("<Gamepad>/leftStick");
            }

            _interactAction = _elevatorMap.FindAction("Interact", false) ??
                              _elevatorMap.AddAction(
                                  "Interact",
                                  InputActionType.Button);
            if (_interactAction.bindings.Count == 0)
            {
                _interactAction.AddBinding("<Keyboard>/e");
                _interactAction.AddBinding("<Gamepad>/buttonNorth");
            }

            _moveAction.performed += OnMovePerformed;
            _moveAction.canceled += OnMoveCanceled;
            _interactAction.performed += OnInteractPerformed;
        }

        private void OnMovePerformed(InputAction.CallbackContext context)
        {
            PublishVerticalDirection(context.ReadValue<Vector2>().y);
        }

        private void OnMoveCanceled(InputAction.CallbackContext context)
        {
            _verticalDirection.OnNext(0);
        }

        private void PublishVerticalDirection(float verticalInput)
        {
            var direction = verticalInput > 0.5f
                ? 1
                : verticalInput < -0.5f
                    ? -1
                    : 0;
            _verticalDirection.OnNext(direction);
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            _interactRequested.OnNext(Unit.Default);
        }

        public void Dispose()
        {
            if (_moveAction != null)
            {
                _moveAction.performed -= OnMovePerformed;
                _moveAction.canceled -= OnMoveCanceled;
            }
            if (_interactAction != null)
                _interactAction.performed -= OnInteractPerformed;

            _elevatorMap?.Disable();
            _verticalDirection.OnCompleted();
            _interactRequested.OnCompleted();
            _verticalDirection.Dispose();
            _interactRequested.Dispose();
        }
    }
}
