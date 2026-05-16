namespace InputSystem.Utility
{
    /// <summary>
    /// Passed to RebindingController.StartRebind() to constrain the interactive
    /// rebinding operation to controls from a specific device family.
    /// Maps directly to the three tabs in the rebinding UI.
    /// </summary>
    public enum DeviceScheme
    {
        KeyboardMouse,
        Gamepad,
        Touch
    }
}
