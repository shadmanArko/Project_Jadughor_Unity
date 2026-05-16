using System;
using InputSystem.Config;
using InputSystem.Data;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.Model
{
    /// <summary>
    /// Concrete implementation of IInputSystemModel.
    /// Bound as a singleton in ProjectContext via InputSystemInstaller.
    ///
    /// Only ActionMapService should call Enable/DisableActionMap.
    /// Only DeviceDetectionService should call SetDeviceState.
    /// All other systems interact through IInputSystemModel read-only members.
    /// </summary>
    public sealed class InputSystemModel : IInputSystemModel, IDisposable
    {
        // ─── IInputSystemModel ─────────────────────────────────────────────────

        public InputActionAsset Actions { get; }

        public IReadOnlyReactiveProperty<DeviceType>      CurrentDeviceType  => _currentDeviceType;
        public IReadOnlyReactiveProperty<GamepadType>     CurrentGamepadType => _currentGamepadType;
        public IReadOnlyReactiveProperty<MobileInputMode> MobileInputMode    => _mobileInputMode;

        // ─── Private State ─────────────────────────────────────────────────────

        private readonly ReactiveProperty<DeviceType>      _currentDeviceType;
        private readonly ReactiveProperty<GamepadType>     _currentGamepadType;
        private readonly ReactiveProperty<MobileInputMode> _mobileInputMode;
        private readonly CompositeDisposable               _disposables = new();

        // ─── Constructor (Zenject injects InputSystemConfig) ──────────────────

        public InputSystemModel(InputSystemConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config), "[InputSystemModel] InputSystemConfig is null.");

            if (config.ActionAsset == null)
                throw new ArgumentNullException(nameof(config.ActionAsset),
                    "[InputSystemModel] InputSystemConfig.ActionAsset is not assigned.");

            Actions = config.ActionAsset;

            _currentDeviceType  = new ReactiveProperty<DeviceType>(DeviceType.Unknown);
            _currentGamepadType = new ReactiveProperty<GamepadType>(GamepadType.Generic);
            _mobileInputMode    = new ReactiveProperty<MobileInputMode>(config.DefaultMobileInputMode);

            // The asset itself is enabled; individual maps are controlled by ActionMapService.
            // Start with all maps disabled so ActionMapService has full control from the first frame.
            Actions.Enable();
            DisableAllActionMaps();
        }

        // ─── IInputSystemModel — Action Lookup ─────────────────────────────────

        public InputAction GetAction(string actionMapName, string actionName)
        {
            var action = Actions.FindAction($"{actionMapName}/{actionName}");

            if (action == null)
                Debug.LogWarning(
                    $"[InputSystemModel] Action not found: '{actionMapName}/{actionName}'. " +
                    "Check that the name matches your .inputactions asset exactly.");

            return action;
        }

        // ─── IInputSystemModel — Map Control ──────────────────────────────────

        public void EnableActionMap(string mapName)
        {
            var map = Actions.FindActionMap(mapName);
            if (map == null)
            {
                Debug.LogWarning($"[InputSystemModel] Action map not found: '{mapName}'.");
                return;
            }
            map.Enable();
        }

        public void DisableActionMap(string mapName)
        {
            Actions.FindActionMap(mapName)?.Disable();
        }

        public void DisableAllActionMaps()
        {
            foreach (var map in Actions.actionMaps)
                map.Disable();
        }

        // ─── IInputSystemModel — State Setters ────────────────────────────────

        public void SetDeviceState(DeviceType deviceType, GamepadType gamepadType)
        {
            _currentDeviceType.Value  = deviceType;
            _currentGamepadType.Value = gamepadType;
        }

        public void SetMobileInputMode(MobileInputMode mode)
        {
            _mobileInputMode.Value = mode;
        }

        // ─── IDisposable ──────────────────────────────────────────────────────

        public void Dispose()
        {
            DisableAllActionMaps();
            Actions.Disable();

            _currentDeviceType.Dispose();
            _currentGamepadType.Dispose();
            _mobileInputMode.Dispose();
            _disposables.Dispose();
        }
    }
}
