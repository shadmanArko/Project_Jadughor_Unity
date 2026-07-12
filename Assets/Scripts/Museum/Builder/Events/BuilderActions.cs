using System;
using ProjectMuseum.Data;

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

        // ── Placement ───────────────────────────────────────────────────

        /// <summary>Ghost placement began for a variation (type, cardName).</summary>
        public static Action<BuilderCardType, string> OnPlacementStarted;

        /// <summary>The pending placement was cancelled (right-click / Esc).</summary>
        public static Action OnPlacementCancelled;

        /// <summary>
        /// The PENDING ghost's rotation frame changed (Q/E) — carries the new frame
        /// index. Mirrors Godot's <c>OnItemRotated</c>. Only fires while a ghost is
        /// active; already-placed objects have no rotate-in-place interaction yet.
        /// </summary>
        public static Action<int> OnPlacementRotated;

        /// <summary>An object was placed and recorded in the museum data.</summary>
        public static Action<PlacedObjectData> OnObjectPlaced;

        /// <summary>An object was removed from the museum data.</summary>
        public static Action<PlacedObjectData> OnObjectRemoved;

        // ── World changes the data model listens to ─────────────────────

        /// <summary>A floor tile was painted at a cell (cell, tile asset name).</summary>
        public static Action<UnityEngine.Vector2Int, string> OnFloorTilePainted;

        /// <summary>A museum chunk finished expanding (lattice coords).</summary>
        public static Action<UnityEngine.Vector2Int> OnMuseumChunkExpanded;

        /// <summary>A wall's wallpaper changed (wallId, wallpaperName; "" = cleared).</summary>
        public static Action<string, string> OnWallpaperChanged;

        // ── Data lifecycle ──────────────────────────────────────────────

        /// <summary>
        /// MuseumData was replaced wholesale (save loaded / new game). Visual
        /// systems must rebuild from data: respawn objects, repaint floors,
        /// reapply wallpapers.
        /// </summary>
        public static Action OnMuseumDataReloaded;
    }
}
