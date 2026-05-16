using Core.EventBus;

namespace InputSystem.Events
{
    /// <summary>
    /// Published by RebindingController after a player successfully assigns
    /// a new binding to an action slot.
    ///
    /// Subscribe to refresh button-prompt icons or update rebinding UI entries.
    /// </summary>
    public sealed class RebindCompletedEvent : IEvent
    {
        public string ActionMapName  { get; }
        public string ActionName     { get; }
        public int    BindingIndex   { get; }
        public string NewControlPath { get; }

        public RebindCompletedEvent(
            string actionMapName,
            string actionName,
            int    bindingIndex,
            string newControlPath)
        {
            ActionMapName  = actionMapName;
            ActionName     = actionName;
            BindingIndex   = bindingIndex;
            NewControlPath = newControlPath;
        }
    }
}
