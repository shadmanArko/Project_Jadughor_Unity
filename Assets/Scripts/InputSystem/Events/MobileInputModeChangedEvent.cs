using Core.EventBus;
using InputSystem.Data;

namespace InputSystem.Events
{
    /// <summary>
    /// Published when the player switches their preferred mobile input mode
    /// (VirtualControls / TouchGestures / Hybrid) in the Settings menu.
    ///
    /// MobileInputContainerView subscribes to show or hide the correct overlays.
    /// </summary>
    public sealed class MobileInputModeChangedEvent : IEvent
    {
        public MobileInputMode PreviousMode { get; }
        public MobileInputMode CurrentMode  { get; }

        public MobileInputModeChangedEvent(MobileInputMode previousMode, MobileInputMode currentMode)
        {
            PreviousMode = previousMode;
            CurrentMode  = currentMode;
        }
    }
}
