using InputSystem.Config;
using InputSystem.Data;
using InputSystem.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.Service
{
    /// <summary>
    /// Resolves icon sprites and human-readable display strings for input bindings.
    ///
    /// Usage:
    ///   Inject this service into ButtonPromptView or RebindingEntryView and call
    ///   GetDisplayData() passing the action, binding index, and current device info.
    ///
    /// Icon fallback chain:
    ///   1. Exact match in the device's icon set (GamepadIconConfig).
    ///   2. Wildcard match via InputControlPath.Matches.
    ///   3. Unity's built-in action.GetBindingDisplayString() as text-only fallback.
    /// </summary>
    public sealed class GamepadIconService
    {
        private readonly GamepadIconConfig _config;
        private readonly IInputSystemModel _inputModel;

        public GamepadIconService(GamepadIconConfig config, IInputSystemModel inputModel)
        {
            _config     = config;
            _inputModel = inputModel;
        }

        // ─── Primary API ──────────────────────────────────────────────────────

        /// <summary>
        /// Returns display data (icon + text) for the binding at the given index,
        /// using the currently active device type and gamepad type.
        /// </summary>
        public BindingDisplayData GetDisplayData(InputAction action, int bindingIndex)
        {
            return GetDisplayData(
                action,
                bindingIndex,
                _inputModel.CurrentDeviceType.Value,
                _inputModel.CurrentGamepadType.Value);
        }

        /// <summary>
        /// Returns display data for a specific device/gamepad combination.
        /// Use this overload in the rebinding UI where you display all schemes simultaneously.
        /// </summary>
        public BindingDisplayData GetDisplayData(InputAction action, int bindingIndex,
                                                  DeviceType deviceType, GamepadType gamepadType)
        {
            if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
                return default;

            var binding = action.bindings[bindingIndex];

            // Composite header rows (e.g. "2DVector") are not individually displayed
            if (binding.isComposite)
                return default;

            var effectivePath = binding.effectivePath;

            // Try icon config first
            if (!string.IsNullOrEmpty(effectivePath) &&
                _config.TryGetEntry(deviceType, gamepadType, effectivePath, out var entry))
            {
                return new BindingDisplayData
                {
                    DisplayText = entry.DisplayName,
                    Icon        = entry.Icon,
                    ControlPath = effectivePath
                };
            }

            // Fallback: use Unity's built-in display string (e.g. "W", "Left Stick/Up")
            var displayString = action.GetBindingDisplayString(
                bindingIndex,
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);

            return new BindingDisplayData
            {
                DisplayText = string.IsNullOrEmpty(displayString) ? effectivePath : displayString,
                Icon        = null,
                ControlPath = effectivePath
            };
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Returns the index of the first binding for the given action that belongs
        /// to the specified control scheme group. Returns -1 if not found.
        /// </summary>
        public static int GetFirstBindingIndexForScheme(InputAction action, string schemeName)
        {
            for (var i = 0; i < action.bindings.Count; i++)
            {
                if (action.bindings[i].groups.Contains(schemeName))
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Collects all binding indices for the given action that belong to the scheme,
        /// including composite part bindings (each individual WASD key, for example).
        /// </summary>
        public static System.Collections.Generic.List<int> GetBindingIndicesForScheme(
            InputAction action, string schemeName)
        {
            var indices = new System.Collections.Generic.List<int>();

            for (var i = 0; i < action.bindings.Count; i++)
            {
                var b = action.bindings[i];

                // Skip top-level composite headers (not directly rebindable)
                if (b.isComposite) continue;

                if (b.groups.Contains(schemeName))
                    indices.Add(i);
            }

            return indices;
        }

        /// <summary>
        /// Returns a human-readable label for a composite part binding index,
        /// e.g. "Move Up", "Move Down". Returns the action name if not a composite part.
        /// </summary>
        public static string GetCompositePartLabel(InputAction action, int bindingIndex)
        {
            if (bindingIndex < 0 || bindingIndex >= action.bindings.Count) return action.name;

            var binding = action.bindings[bindingIndex];

            if (!binding.isPartOfComposite) return action.name;

            // Capitalise the first letter of the part name (e.g. "up" → "Up")
            var part = binding.name;
            if (string.IsNullOrEmpty(part)) return action.name;

            return $"{action.name} {char.ToUpper(part[0])}{part.Substring(1)}";
        }
    }
}
