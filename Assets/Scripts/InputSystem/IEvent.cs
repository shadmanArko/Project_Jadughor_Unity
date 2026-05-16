namespace Core.EventBus
{
    /// <summary>
    /// Marker interface for all cross-system EventBus events.
    /// Every POCO event payload class must implement this interface.
    /// </summary>
    public interface IEvent { }
}
