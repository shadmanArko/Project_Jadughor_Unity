using Core.EventBus;

namespace InputSystem.Events
{
    /// <summary>
    /// Publish this to immediately silence all gamepad haptics.
    /// Useful when entering a pause menu, cutscene, or dialog.
    /// </summary>
    public sealed class StopHapticsEvent : IEvent { }
}
