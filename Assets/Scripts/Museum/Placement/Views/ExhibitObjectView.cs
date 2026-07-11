using ProjectMuseum.Data;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Placed exhibit prefab. Artifact-slot wiring (reading/writing
    /// <c>MuseumDataModel.GetExhibitData</c>) lands here when artifact placement
    /// is built — deliberately left empty until then.
    /// </summary>
    public class ExhibitObjectView : PlaceableObjectView
    {
        protected override void OnInitialized(PlacedObjectData data)
        {
            base.OnInitialized(data);
        }
    }
}
