using System;
using Core.EventBus;
using InputSystem.Config;
using InputSystem.Data;
using InputSystem.Events;
using InputSystem.Model;
using UnityEngine;
using UInput = UnityEngine.InputSystem.InputSystem;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.XInput;
using Zenject;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.Service
{
    /// <summary>
    /// Listens to Unity Input System device-level events to determine which device
    /// the player is actively using, then updates IInputSystemModel and DeviceModel
    /// and publishes a DeviceChangedEvent.
    ///
    /// Detection strategy:
    ///   1. InputSystem.onDeviceChange  → track connections/disconnections.
    ///   2. InputActionAsset.actionTriggered → detect which device fired an action
    ///      (covers all mapped controls without polling every frame).
    ///
    /// A cooldown prevents rapid flicker when two devices fire within the same frame.
    /// </summary>
    public sealed class DeviceDetectionService : IInitializable, IDisposable
    {
        // ─── Dependencies ──────────────────────────────────────────────────────

        private readonly IInputSystemModel _inputModel;
        private readonly DeviceModel       _deviceModel;
        private readonly EventBus          _eventBus;
        private readonly float             _cooldown;

        // ─── State ─────────────────────────────────────────────────────────────

        private InputDevice _lastDetectedDevice;
        private double      _lastChangeTime;

        // ─── Constructor ──────────────────────────────────────────────────────

        public DeviceDetectionService(IInputSystemModel      inputModel,
                                      DeviceModel            deviceModel,
                                      EventBus               eventBus,
                                      InputSystemConfig      config)
        {
            _inputModel  = inputModel;
            _deviceModel = deviceModel;
            _eventBus    = eventBus;
            _cooldown    = config.DeviceChangeCooldown;
        }

        // ─── IInitializable ───────────────────────────────────────────────────

        public void Initialize()
        {
            // Seed the model with whatever is already connected at startup
            RefreshConnectedDevices();

            // Subscribe to connection changes (plug in / unplug)
            UInput.onDeviceChange += OnDeviceChange;

            // Subscribe to action events — the most reliable way to know which device
            // the player is actively touching without polling every frame
            foreach (var map in _inputModel.Actions.actionMaps)
                map.actionTriggered += OnActionTriggered;

            // Perform an initial best-guess based on connected hardware
            GuessInitialDevice();
        }

        // ─── Private Handlers ─────────────────────────────────────────────────

        private void OnActionTriggered(InputAction.CallbackContext ctx)
        {
            var device = ctx.control?.device;
            if (device == null || device == _lastDetectedDevice) return;

            // Cooldown: ignore rapid switches within the same short window
            if (Time.realtimeSinceStartupAsDouble - _lastChangeTime < _cooldown) return;

            _lastDetectedDevice = device;
            _lastChangeTime     = Time.realtimeSinceStartupAsDouble;

            UpdateActiveDevice(device);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            switch (change)
            {
                case InputDeviceChange.Added:
                case InputDeviceChange.Reconnected:
                case InputDeviceChange.Enabled:
                    RegisterDevice(device);
                    break;

                case InputDeviceChange.Removed:
                case InputDeviceChange.Disconnected:
                case InputDeviceChange.Disabled:
                    UnregisterDevice(device);
                    break;
            }
        }

        // ─── Device Registration ──────────────────────────────────────────────

        private void RegisterDevice(InputDevice device)
        {
            if (device is Keyboard) _deviceModel.IsKeyboardConnected  = true;
            if (device is Mouse)    _deviceModel.IsMouseConnected     = true;
            if (device is Gamepad g)_deviceModel.AddGamepad(g);
            if (device is Touchscreen) _deviceModel.IsTouchscreenPresent = true;
        }

        private void UnregisterDevice(InputDevice device)
        {
            if (device is Keyboard)  _deviceModel.IsKeyboardConnected  = Keyboard.current  != null;
            if (device is Mouse)     _deviceModel.IsMouseConnected     = Mouse.current     != null;
            if (device is Gamepad g) _deviceModel.RemoveGamepad(g);
            if (device is Touchscreen)
                _deviceModel.IsTouchscreenPresent = Touchscreen.current != null;

            // If the active device was removed, fall back to next best available
            if (device == _lastDetectedDevice)
                GuessInitialDevice();
        }

        private void RefreshConnectedDevices()
        {
            _deviceModel.IsKeyboardConnected   = Keyboard.current    != null;
            _deviceModel.IsMouseConnected      = Mouse.current       != null;
            _deviceModel.IsTouchscreenPresent  = Touchscreen.current != null;

            var gamepads = new System.Collections.Generic.List<Gamepad>();
            foreach (var device in UInput.devices)
                if (device is Gamepad gp) gamepads.Add(gp);
            _deviceModel.SetConnectedGamepads(gamepads);
        }

        // ─── Active Device Resolution ─────────────────────────────────────────

        private void UpdateActiveDevice(InputDevice device)
        {
            var previousDeviceType = _inputModel.CurrentDeviceType.Value;

            var (deviceType, gamepadType) = ClassifyDevice(device);

            if (deviceType == previousDeviceType &&
                (deviceType != DeviceType.Gamepad ||
                 gamepadType == _inputModel.CurrentGamepadType.Value))
                return; // Nothing changed

            _inputModel.SetDeviceState(deviceType, gamepadType);

            _eventBus.Publish(new DeviceChangedEvent(previousDeviceType, deviceType, gamepadType));

            Debug.Log($"[DeviceDetectionService] Active device changed: {previousDeviceType} → {deviceType} ({gamepadType})");
        }

        private void GuessInitialDevice()
        {
            // Priority order: Touchscreen > Gamepad > Keyboard
            if (_deviceModel.IsTouchscreenPresent)
            {
                var isSwitchHandheld = IsLikelySwitchHandheld();
                var devType = isSwitchHandheld ? DeviceType.Handheld : DeviceType.Mobile;
                _inputModel.SetDeviceState(devType, GamepadType.Generic);
                _eventBus.Publish(new DeviceChangedEvent(DeviceType.Unknown, devType, GamepadType.Generic));
            }
            else if (_deviceModel.IsGamepadConnected && _deviceModel.ConnectedGamepads.Count > 0)
            {
                var (_, gpType) = ClassifyDevice(_deviceModel.ConnectedGamepads[0]);
                _inputModel.SetDeviceState(DeviceType.Gamepad, gpType);
                _eventBus.Publish(new DeviceChangedEvent(DeviceType.Unknown, DeviceType.Gamepad, gpType));
            }
            else if (_deviceModel.IsKeyboardConnected)
            {
                _inputModel.SetDeviceState(DeviceType.KeyboardMouse, GamepadType.Generic);
                _eventBus.Publish(new DeviceChangedEvent(DeviceType.Unknown, DeviceType.KeyboardMouse, GamepadType.Generic));
            }
        }

        // ─── Device Classification ────────────────────────────────────────────

        private static (DeviceType, GamepadType) ClassifyDevice(InputDevice device)
        {
            if (device is Keyboard || device is Mouse)
                return (DeviceType.KeyboardMouse, GamepadType.Generic);

            if (device is Touchscreen)
                return (IsLikelySwitchHandheld() ? DeviceType.Handheld : DeviceType.Mobile,
                        GamepadType.Generic);

            if (device is Gamepad)
                return (DeviceType.Gamepad, DetectGamepadType(device));

            return (DeviceType.Unknown, GamepadType.Generic);
        }

        private static GamepadType DetectGamepadType(InputDevice device)
        {
            // Type-based checks (most reliable)
            if (device is XInputController)   return GamepadType.Xbox;
            if (device is DualShockGamepad)   return GamepadType.PlayStation;

            // Name/description fallback for devices not covered by the type hierarchy
            var product      = device.description.product?.ToLowerInvariant()      ?? string.Empty;
            var manufacturer = device.description.manufacturer?.ToLowerInvariant() ?? string.Empty;

            if (product.Contains("dualsense") || product.Contains("dualshock") ||
                (product.Contains("wireless controller") && manufacturer.Contains("sony")))
                return GamepadType.PlayStation;

            if (product.Contains("xbox") || manufacturer.Contains("microsoft"))
                return GamepadType.Xbox;

            if (product.Contains("pro controller") || product.Contains("switch") ||
                manufacturer.Contains("nintendo"))
                return GamepadType.SwitchPro;

            return GamepadType.Generic;
        }

        /// <summary>
        /// Heuristic: if a touchscreen AND a gamepad-like device are present,
        /// we're likely on a Switch in handheld mode.
        /// </summary>
        private static bool IsLikelySwitchHandheld()
        {
            bool hasTouch   = Touchscreen.current != null;
            bool hasGamepad = Gamepad.current != null;
            if (!hasTouch || !hasGamepad) return false;

            var product = Gamepad.current.description.product?.ToLowerInvariant() ?? string.Empty;
            return product.Contains("switch") || product.Contains("joy-con");
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            UInput.onDeviceChange          -= OnDeviceChange;
            foreach (var map in _inputModel.Actions.actionMaps)
                map.actionTriggered -= OnActionTriggered;
        }
    }
}
