using Systems.MineSystem.ToolbarSystem.Enum;
using UnityEngine;

namespace Systems.MineSystem.ToolbarSystem.Profile
{
    [CreateAssetMenu(fileName = "PlaceableActionProfile", menuName = "Toolbar Actions/Placeable Profile")]
    public sealed class PlaceableActionProfile : ItemActionProfile
    {
        [Header("Factory")]
        [SerializeField] private string placeableId;
        [SerializeField] private PlaceableTargetKind targetKind;

        [Header("Footprint")]
        [Min(1)]
        [SerializeField] private int width = 1;
        [Min(1)]
        [SerializeField] private int height = 1;

        [Header("Preview")]
        [SerializeField] private Sprite previewSprite;
        [SerializeField] private Color validColor = new(0.2f, 1f, 0.2f, 0.65f);
        [SerializeField] private Color invalidColor = new(1f, 0.2f, 0.2f, 0.65f);

        public override ItemActionKind ActionKind => ItemActionKind.Placeable;
        public string PlaceableId => placeableId;
        public PlaceableTargetKind TargetKind => targetKind;
        public int Width => Mathf.Max(1, width);
        public int Height => Mathf.Max(1, height);
        public Sprite PreviewSprite => previewSprite;
        public Color ValidColor => validColor;
        public Color InvalidColor => invalidColor;
    }
}
