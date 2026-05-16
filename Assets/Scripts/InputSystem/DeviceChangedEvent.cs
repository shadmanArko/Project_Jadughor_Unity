using Core.EventBus;
using InputSystem.Data;

namespace InputSystem.Events
{
    /// <summary>
    /// Published by DeviceDetectionController whenever the player switches
    /// to a different input device (e.g. plugs in a gamepad or goes back to keyboard).
    ///
    /// Subscribe via: _eventBus.Receive&lt;DeviceChangedEvent&gt;()
    ///
    /// Use this to:
    ///   - Swap button-prompt icons throughout the UI.
    ///   - Show/hide the mobile virtual controls overlay.
    ///   - Switch to device-appropriate UI skin.
    /// </summary>
    public sealed class DeviceChangedEvent : IEvent
    {
        public DeviceType  PreviousDeviceType  { get; }
        public DeviceType  CurrentDeviceType   { get; }
        public GamepadType CurrentGamepadType  { get; }

        public DeviceChangedEvent(
            DeviceType  previousDeviceType,
            DeviceType  currentDeviceType,
            GamepadType currentGamepadType)
        {
            PreviousDeviceType = previousDeviceType;
            CurrentDeviceType  = currentDeviceType;
            CurrentGamepadType = currentGamepadType;
        }
    }
}
