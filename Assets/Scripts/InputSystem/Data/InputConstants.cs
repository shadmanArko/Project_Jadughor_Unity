namespace InputSystem.Data
{
    /// <summary>
    /// Single source of truth for every action map name, action name, and
    /// binding group name used in the InputActions asset.
    /// Change a string here and it propagates to the entire codebase.
    ///
    /// IMPORTANT: These strings MUST match the names in your .inputactions asset exactly.
    /// </summary>
    public static class InputConstants
    {
        // ─── Action Map Names ─────────────────────────────────────────────────
        public static class Maps
        {
            public const string UI           = "UI";
            public const string MuseumScene  = "MuseumScene";
            public const string MineScene    = "MineScene";
            public const string Dialogue     = "Dialogue";
            // Add new map names here when you create them in the .inputactions asset.
        }

        // ─── Action Names per Map ─────────────────────────────────────────────
        public static class Actions
        {
            public static class UI
            {
                public const string Navigate    = "Navigate";
                public const string Submit      = "Submit";
                public const string Cancel      = "Cancel";
                public const string ScrollWheel = "ScrollWheel";
                public const string Tab         = "Tab";
                public const string Pause       = "Pause";
            }

            public static class MuseumScene
            {
                public const string Move      = "Move";
                public const string Look      = "Look";
                public const string Interact  = "Interact";
                public const string Sprint    = "Sprint";
                public const string Crouch    = "Crouch";
                public const string Zoom      = "Zoom";
                public const string OpenMap   = "OpenMap";
            }

            public static class MineScene
            {
                public const string Move       = "Move";
                public const string MineAction = "MineAction";
                public const string SwapWeapon = "SwapWeapon";
                public const string Climb      = "Climb";
                public const string Drop       = "Drop";
            }

            public static class Dialogue
            {
                public const string Advance      = "Advance";
                public const string Skip         = "Skip";
                public const string SelectOption = "SelectOption";
                public const string Cancel       = "Cancel";
            }
        }

        // ─── Binding Group / Control Scheme Names ─────────────────────────────
        // These must match the Control Scheme names in your .inputactions asset.
        public static class Schemes
        {
            public const string KeyboardMouse = "Keyboard&Mouse";
            public const string Gamepad       = "Gamepad";
            public const string Touch         = "Touch";
        }

        // ─── PlayerPrefs Keys ─────────────────────────────────────────────────
        public static class PrefsKeys
        {
            public const string BindingOverrides  = "InputSystem_BindingOverrides";
            public const string MobileInputMode   = "InputSystem_MobileInputMode";
        }

        // ─── Control Paths (for OnScreenControls & rebinding constraints) ─────
        public static class ControlPaths
        {
            public const string Keyboard    = "<Keyboard>";
            public const string Mouse       = "<Mouse>";
            public const string Gamepad     = "<Gamepad>";
            public const string Touchscreen = "<Touchscreen>";
            public const string EscapeKey   = "<Keyboard>/escape";
            public const string GamepadStart = "<Gamepad>/start";

            // Virtual on-screen control paths (must match OnScreenStick/Button paths)
            public const string VirtualLeftStick    = "<Gamepad>/leftStick";
            public const string VirtualButtonSouth  = "<Gamepad>/buttonSouth";
            public const string VirtualButtonEast   = "<Gamepad>/buttonEast";
            public const string VirtualButtonNorth  = "<Gamepad>/buttonNorth";
            public const string VirtualButtonWest   = "<Gamepad>/buttonWest";
        }
    }
}
