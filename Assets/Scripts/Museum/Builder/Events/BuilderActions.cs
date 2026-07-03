using System;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Static event hub for the builder panel — the Unity port of the builder slice
    /// of Godot's <c>MuseumActions</c>. Kept separate from the narrative hub
    /// (<c>ProjectMuseum.Narrative.MuseumActions</c>) so the two systems stay tidy.
    ///
    /// As with the narrative hub, these are static delegates — always unsubscribe in
    /// OnDisable/OnDestroy to avoid leaks across scene reloads.
    /// </summary>
    public static class BuilderActions
    {
        /// <summary>
        /// A bottom-bar category button was clicked. The panel opens and lists that
        /// category (or closes if the same category is clicked while already open).
        /// </summary>
        public static Action<BuilderCardType> OnBottomPanelBuilderCardToggleClicked;

        /// <summary>
        /// A specific object card was clicked (carries its category + card name).
        /// Placement systems subscribe to this to begin placing the object.
        /// </summary>
        public static Action<BuilderCardType, string> OnClickBuilderCard;
    }
}
