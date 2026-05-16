using Core.EventBus;

namespace InputSystem.Events
{
    /// <summary>
    /// Published when an interactive rebinding operation is cancelled
    /// (e.g. player pressed Escape or the operation timed out).
    /// The UI should dismiss the "Waiting for input…" overlay.
    /// </summary>
    public sealed class RebindCancelledEvent : IEvent
    {
        public string ActionMapName { get; }
        public string ActionName    { get; }
        public int    BindingIndex  { get; }

        public RebindCancelledEvent(string actionMapName, string actionName, int bindingIndex)
        {
            ActionMapName = actionMapName;
            ActionName    = actionName;
            BindingIndex  = bindingIndex;
        }
    }
}
