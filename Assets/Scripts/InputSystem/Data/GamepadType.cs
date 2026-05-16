namespace InputSystem.Data
{
    /// <summary>
    /// Identifies the specific gamepad manufacturer.
    /// Used by GamepadIconService to select the correct icon set (Xbox / PS / Switch).
    /// </summary>
    public enum GamepadType
    {
        Generic,
        Xbox,
        PlayStation,
        SwitchPro
    }
}
