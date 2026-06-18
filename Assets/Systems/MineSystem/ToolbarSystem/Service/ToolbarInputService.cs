using System;
using Systems.MineSystem.ToolbarSystem.Interface;
using Systems.MineSystem.ToolbarSystem.Scriptable;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using Zenject;

namespace Systems.MineSystem.ToolbarSystem.Service
{
    public sealed class ToolbarInputService :
        IToolbarInputService,
        IInitializable,
        IDisposable
    {
        private readonly ToolbarConfig _config;
        private readonly Subject<int> _navigationRequested = new();
        private InputAction _nextAction;
        private InputAction _previousAction;
        private InputAction _scrollAction;
        private bool _enabled;
        private bool _disposed;

        public IObservable<int> NavigationRequested => _navigationRequested;

        public ToolbarInputService(ToolbarConfig config)
        {
            _config = config;
        }

        public void Initialize()
        {
            EnsureActionsCreated();
        }

        private void EnsureActionsCreated()
        {
            if (_nextAction != null)
                return;

            _nextAction = new InputAction("ToolbarNext", InputActionType.Button);
            _nextAction.AddBinding("<Gamepad>/rightShoulder");
            _nextAction.performed += OnNext;

            _previousAction = new InputAction("ToolbarPrevious", InputActionType.Button);
            _previousAction.AddBinding("<Gamepad>/leftShoulder");
            _previousAction.performed += OnPrevious;

            _scrollAction = new InputAction(
                "ToolbarScroll",
                InputActionType.PassThrough,
                expectedControlType: "Vector2");
            _scrollAction.AddBinding("<Mouse>/scroll");
            _scrollAction.performed += OnScroll;
        }

        public void SetEnabled(bool enabled)
        {
            if (_disposed)
                return;

            EnsureActionsCreated();

            if (_enabled == enabled)
                return;

            _enabled = enabled;
            if (enabled)
            {
                _nextAction.Enable();
                _previousAction.Enable();
                _scrollAction.Enable();
                return;
            }

            _nextAction.Disable();
            _previousAction.Disable();
            _scrollAction.Disable();
        }

        private void OnNext(InputAction.CallbackContext context)
        {
            _navigationRequested.OnNext(1);
        }

        private void OnPrevious(InputAction.CallbackContext context)
        {
            _navigationRequested.OnNext(-1);
        }

        private void OnScroll(InputAction.CallbackContext context)
        {
            var scroll = context.ReadValue<Vector2>().y;
            if (Mathf.Abs(scroll) < _config.MouseWheelThreshold)
                return;

            var wheelUp = scroll > 0f;
            var next = wheelUp == _config.MouseWheelUpSelectsNext;
            _navigationRequested.OnNext(next ? 1 : -1);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_nextAction == null)
            {
                _navigationRequested.Dispose();
                return;
            }

            _nextAction.Disable();
            _previousAction.Disable();
            _scrollAction.Disable();
            _enabled = false;

            _nextAction.performed -= OnNext;
            _previousAction.performed -= OnPrevious;
            _scrollAction.performed -= OnScroll;

            _nextAction.Dispose();
            _previousAction.Dispose();
            _scrollAction.Dispose();
            _navigationRequested.Dispose();
        }
    }
}
