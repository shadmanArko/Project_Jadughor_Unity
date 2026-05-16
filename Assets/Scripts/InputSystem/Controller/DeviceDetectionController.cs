using System;
using Core.EventBus;
using InputSystem.Data;
using InputSystem.Events;
using InputSystem.Model;
using UniRx;
using UnityEngine;
using Zenject;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.Controller
{
    /// <summary>
    /// Reacts to DeviceChangedEvent and maintains any cross-cutting concerns
    /// that must respond to a device switch — currently:
    ///
    ///   - Logs device transitions (useful during development).
    ///   - Can be extended to drive platform-specific UI skin switching.
    ///
    /// The mobile-overlay show/hide is handled by MobileInputContainerView directly
    /// subscribing to IInputSystemModel.CurrentDeviceType, keeping the controller slim.
    /// </summary>
    public sealed class DeviceDetectionController : IInitializable, IDisposable
    {
        private readonly EventBus          _eventBus;
        private readonly IInputSystemModel _inputModel;
        private readonly CompositeDisposable _disposables = new();

        public DeviceDetectionController(EventBus eventBus, IInputSystemModel inputModel)
        {
            _eventBus   = eventBus;
            _inputModel = inputModel;
        }

        // ─── IInitializable ───────────────────────────────────────────────────

        public void Initialize()
        {
            _eventBus.Receive<DeviceChangedEvent>()
                .Subscribe(OnDeviceChanged)
                .AddTo(_disposables);
        }

        // ─── Handlers ─────────────────────────────────────────────────────────

        private void OnDeviceChanged(DeviceChangedEvent evt)
        {
            Debug.Log(
                $"[DeviceDetectionController] Device changed: {evt.PreviousDeviceType} → " +
                $"{evt.CurrentDeviceType}" +
                (evt.CurrentDeviceType == DeviceType.Gamepad
                    ? $" ({evt.CurrentGamepadType})"
                    : string.Empty));

            // Example extension point:
            // _eventBus.Publish(new UISkinChangeRequestEvent(ResolveSkin(evt.CurrentDeviceType)));
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
