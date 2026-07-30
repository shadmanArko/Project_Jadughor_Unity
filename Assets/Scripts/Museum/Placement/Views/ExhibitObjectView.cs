using ProjectMuseum.Data;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Placed exhibit prefab. Left-clicking it opens the exhibit editor UI (where
    /// the player drags artifacts into its display slots) via
    /// <see cref="BuilderActions.OnExhibitClicked"/>, keyed by this exhibit's Id.
    /// </summary>
    public class ExhibitObjectView : PlaceableObjectView
    {
        public override void Interact()
        {
            if (!IsPlaced || string.IsNullOrEmpty(Id)) return;
            BuilderActions.OnExhibitClicked?.Invoke(Id);
        }
    }
}
