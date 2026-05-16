using UnityEngine;

namespace InputSystem.Data
{
    /// <summary>
    /// Carry-type returned by GamepadIconService for displaying a single binding
    /// in the rebinding UI or as a contextual button prompt.
    /// </summary>
    public struct BindingDisplayData
    {
        /// <summary>Human-readable text for this binding (e.g. "W", "A Button", "Swipe Up").</summary>
        public string DisplayText;

        /// <summary>Sprite icon, if one exists for this control on the current device. May be null.</summary>
        public Sprite Icon;

        /// <summary>The raw InputSystem control path (e.g. "&lt;Keyboard&gt;/w").</summary>
        public string ControlPath;

        /// <summary>True when an icon asset is available and should be shown instead of text.</summary>
        public bool HasIcon => Icon != null;

        /// <summary>True when this slot has any displayable data at all.</summary>
        public bool IsValid => !string.IsNullOrEmpty(DisplayText) || HasIcon;
    }
}
