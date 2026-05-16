namespace InputSystem.Data
{
    /// <summary>
    /// Controls how touch input is processed on mobile and handheld devices.
    /// The player can change this in the Settings menu.
    /// </summary>
    public enum MobileInputMode
    {
        /// <summary>On-screen virtual joystick and action buttons.</summary>
        VirtualControls,

        /// <summary>Tap, swipe, and pinch gesture recognition.</summary>
        TouchGestures,

        /// <summary>Both VirtualControls and TouchGestures active simultaneously.</summary>
        Hybrid
    }
}
