namespace ProjectMuseum.Builder
{
    /// <summary>
    /// A placed museum object the player can click. Implemented by
    /// <c>PlaceableObjectView</c> (base) so every placed object is interactable;
    /// subclasses override <see cref="Interact"/> with type-specific behaviour
    /// (e.g. an exhibit opens the exhibit editor UI).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Invoked by <c>MuseumInteractionSystem</c> on a left-click hit.</summary>
        void Interact();
    }
}
