namespace InputSystem.Data
{
    /// <summary>
    /// Represents the broad category of the player's currently active input device.
    /// Broadcast via DeviceChangedEvent whenever the active device changes.
    /// </summary>
    public enum DeviceType
    {
        Unknown,
        KeyboardMouse,
        Gamepad,
        Mobile,       // Touchscreen on a phone or tablet
        Handheld      // Handheld console (e.g., Switch in handheld mode)
    }
}
