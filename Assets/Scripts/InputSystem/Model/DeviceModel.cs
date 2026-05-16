using System.Collections.Generic;
using InputSystem.Data;
using UnityEngine.InputSystem;

namespace InputSystem.Model
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Interface
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Read-only snapshot of what physical devices are currently connected.
    /// Maintained by DeviceDetectionService and read by any system that needs
    /// to know what hardware the player has (e.g., the rebinding UI to know
    /// which tabs to show).
    /// </summary>
    public interface IDeviceModel
    {
        bool IsKeyboardConnected  { get; }
        bool IsMouseConnected     { get; }
        bool IsGamepadConnected   { get; }
        bool IsTouchscreenPresent { get; }

        /// <summary>All gamepads currently connected (there may be more than one).</summary>
        IReadOnlyList<Gamepad> ConnectedGamepads { get; }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Concrete Implementation
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mutable concrete implementation.
    /// Only DeviceDetectionService should write to this; all other systems use IDeviceModel.
    /// </summary>
    public sealed class DeviceModel : IDeviceModel
    {
        public bool IsKeyboardConnected  { get; set; }
        public bool IsMouseConnected     { get; set; }
        public bool IsGamepadConnected   { get; set; }
        public bool IsTouchscreenPresent { get; set; }

        private readonly List<Gamepad> _connectedGamepads = new();
        public IReadOnlyList<Gamepad> ConnectedGamepads => _connectedGamepads;

        public void SetConnectedGamepads(IEnumerable<Gamepad> gamepads)
        {
            _connectedGamepads.Clear();
            _connectedGamepads.AddRange(gamepads);
            IsGamepadConnected = _connectedGamepads.Count > 0;
        }

        public void AddGamepad(Gamepad pad)
        {
            if (!_connectedGamepads.Contains(pad))
                _connectedGamepads.Add(pad);
            IsGamepadConnected = true;
        }

        public void RemoveGamepad(Gamepad pad)
        {
            _connectedGamepads.Remove(pad);
            IsGamepadConnected = _connectedGamepads.Count > 0;
        }
    }
}
