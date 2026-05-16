using Core.EventBus;
using InputSystem.Data;

namespace InputSystem.Events
{
    /// <summary>
    /// Published by ActionMapService whenever the context stack changes,
    /// resulting in a different set of active action maps.
    /// </summary>
    public sealed class ActionMapContextChangedEvent : IEvent
    {
        /// <summary>The context currently on top of the stack.</summary>
        public ActionMapContextId TopContext { get; }

        /// <summary>All action map names that are currently enabled.</summary>
        public string[] ActiveActionMapNames { get; }

        public ActionMapContextChangedEvent(ActionMapContextId topContext, string[] activeActionMapNames)
        {
            TopContext           = topContext;
            ActiveActionMapNames = activeActionMapNames;
        }
    }
}
