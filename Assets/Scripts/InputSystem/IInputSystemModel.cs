using InputSystem.Data;
using UniRx;
using UnityEngine.InputSystem;

namespace InputSystem.Model
{
    /// <summary>
    /// Public contract for the core input data model.
    /// All services, controllers, and views that need input state
    /// should depend on this interface, not the concrete class.
    ///
    /// Responsibilities:
    ///   - Owns the InputActionAsset lifetime.
    ///   - Exposes reactive properties for current device state.
    ///   - Provides action map enable/disable operations used by ActionMapService.
    ///   - Exposes a typed action lookup so callers never use magic strings directly
    ///     (they pass InputConstants values).
    /// </summary>
    public interface IInputSystemModel
    {
        // ─── Raw Asset Access ─────────────────────────────────────────────────

        /// <summary>
        /// The underlying InputActionAsset. Use sparingly — prefer GetAction() for
        /// individual action lookups. Needed by InputSaveService for serialisation.
        /// </summary>
        InputActionAsset Actions { get; }

        // ─── Reactive Device State ────────────────────────────────────────────

        IReadOnlyReactiveProperty<DeviceType>  CurrentDeviceType  { get; }
        IReadOnlyReactiveProperty<GamepadType> CurrentGamepadType { get; }
        IReadOnlyReactiveProperty<MobileInputMode> MobileInputMode { get; }

        // ─── Action Lookup ────────────────────────────────────────────────────

        /// <summary>
        /// Returns the InputAction identified by map name + action name.
        /// Logs a warning and returns null if the path is invalid.
        /// Use InputConstants.Maps and InputConstants.Actions to avoid magic strings.
        /// </summary>
        InputAction GetAction(string actionMapName, string actionName);

        // ─── Map Enable / Disable (called by ActionMapService only) ───────────

        void EnableActionMap(string mapName);
        void DisableActionMap(string mapName);
        void DisableAllActionMaps();

        // ─── State Setters (called by DeviceDetectionService only) ───────────

        void SetDeviceState(DeviceType deviceType, GamepadType gamepadType);
        void SetMobileInputMode(MobileInputMode mode);
    }
}
