using System;
using InputSystem.Data;
using UnityEngine;
using UnityEngine.InputSystem;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.Config
{
    // ─────────────────────────────────────────────────────────────────────────
    //  Data Types
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Maps a single InputSystem control path to a display sprite and a short label.
    /// Example: ControlPath = "&lt;XInputController&gt;/buttonSouth", DisplayName = "A"
    /// </summary>
    [Serializable]
    public struct IconEntry
    {
        [Tooltip("InputSystem control path, e.g. '<XInputController>/buttonSouth'")]
        public string ControlPath;

        [Tooltip("Short human-readable name shown if no icon is available, e.g. 'A', 'LB', 'Esc'")]
        public string DisplayName;

        [Tooltip("Sprite asset for this control. Leave null to fall back to DisplayName text.")]
        public Sprite Icon;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ScriptableObject
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Holds icon sets for every supported device type.
    /// Create one instance: Assets/Config/Input/GamepadIconConfig.asset
    ///
    /// Populate each array in the Inspector by adding one IconEntry per physical control.
    /// GamepadIconService uses this to look up the correct icon at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "GamepadIconConfig", menuName = "Config/Input/GamepadIconConfig")]
    public sealed class GamepadIconConfig : ScriptableObject
    {
        [Header("Keyboard & Mouse")]
        public IconEntry[] KeyboardMouseIcons = Array.Empty<IconEntry>();

        [Header("Xbox / XInput")]
        public IconEntry[] XboxIcons = Array.Empty<IconEntry>();

        [Header("PlayStation (DualShock / DualSense)")]
        public IconEntry[] PlayStationIcons = Array.Empty<IconEntry>();

        [Header("Switch Pro Controller")]
        public IconEntry[] SwitchProIcons = Array.Empty<IconEntry>();

        [Header("Generic Gamepad Fallback")]
        public IconEntry[] GenericGamepadIcons = Array.Empty<IconEntry>();

        // ─── Lookup Helpers ───────────────────────────────────────────────────

        /// <summary>Returns the icon set for a given device and gamepad brand.</summary>
        public IconEntry[] GetIconSet(DeviceType deviceType, GamepadType gamepadType)
        {
            return deviceType switch
            {
                DeviceType.KeyboardMouse => KeyboardMouseIcons,
                DeviceType.Gamepad => gamepadType switch
                {
                    GamepadType.Xbox        => XboxIcons,
                    GamepadType.PlayStation  => PlayStationIcons,
                    GamepadType.SwitchPro   => SwitchProIcons,
                    _                       => GenericGamepadIcons
                },
                DeviceType.Mobile   => KeyboardMouseIcons, // Touch uses keyboard/mouse set as fallback
                DeviceType.Handheld => SwitchProIcons,
                _                   => GenericGamepadIcons
            };
        }

        /// <summary>
        /// Finds the IconEntry whose ControlPath matches the given effectivePath.
        /// Uses InputControlPath.Matches for wildcard support, then falls back to
        /// a case-insensitive string comparison.
        /// Returns false if no entry was found.
        /// </summary>
        public bool TryGetEntry(DeviceType deviceType, GamepadType gamepadType,
                                string effectivePath, out IconEntry entry)
        {
            var iconSet = GetIconSet(deviceType, gamepadType);

            foreach (var e in iconSet)
            {
                if (string.IsNullOrEmpty(e.ControlPath)) continue;

                if (string.Equals(e.ControlPath, effectivePath, StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    return true;
                }
            }

            entry = default;
            return false;
        }
    }
}
