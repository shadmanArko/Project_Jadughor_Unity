using ProjectMuseum.Narrative;
using UnityEngine;

namespace ProjectMuseum.Town
{
    public enum TownObjectKind
    {
        Building,
        Tree
    }

    /// <summary>
    /// A clickable building or tree in the town world — the port of Godot's
    /// <c>ClickableObject</c> / <c>TownBuilding</c> / <c>TownTree</c>.
    ///
    /// Lives on a SpriteRenderer inside the Town prefab. Its PolygonCollider2D is the click
    /// outline traced in Godot; edit it in the Scene view like any collider.
    /// <see cref="TownMapView"/> does the hit-testing and calls <see cref="SetHovered"/> /
    /// <see cref="Click"/>.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer), typeof(PolygonCollider2D))]
    public class TownBuilding : MonoBehaviour
    {
        [SerializeField] private TownObjectKind kind = TownObjectKind.Building;
        [Tooltip("Someone could live here — clicking it while nobody's home shows the empty-house popup.")]
        [SerializeField] private bool livingHouse;
        [Tooltip("The digging buddy lives here — clicking it completes the tutorial step.")]
        [SerializeField] private bool hasDiggingBuddy;
        [Tooltip("Tint while hovered (Godot: #D3D3D3 for buildings, white = none for trees).")]
        [SerializeField] private Color hoverColor = new Color32(0xD3, 0xD3, 0xD3, 0xFF);

        private SpriteRenderer _renderer;
        private Color _startColor;

        public TownObjectKind Kind => kind;
        public bool LivingHouse => livingHouse;
        public bool HasDiggingBuddy => hasDiggingBuddy;
        public int SortingOrder => Renderer.sortingOrder;

        private SpriteRenderer Renderer
        {
            get
            {
                if (_renderer == null)
                {
                    _renderer = GetComponent<SpriteRenderer>();
                    _startColor = _renderer.color;
                }
                return _renderer;
            }
        }

        public void SetHovered(bool hovered) => Renderer.color = hovered ? hoverColor : _startColor;

        /// <summary>Same branching as Godot's <c>TownBuilding.HandleClick</c>.</summary>
        public void Click()
        {
            if (kind != TownObjectKind.Building) return;

            if (hasDiggingBuddy)
                MuseumActions.OnPlayerPerformedTutorialRequiringAction?.Invoke("FoundDiggingBuddy");
            else if (livingHouse)
                MuseumActions.OnPlayerClickedAnEmptyHouse?.Invoke();
        }

#if UNITY_EDITOR
        /// <summary>Used by the setup tool so generated objects match their Godot flags.</summary>
        public void EditorSetup(TownObjectKind newKind, bool living, bool buddy)
        {
            kind = newKind;
            livingHouse = living;
            hasDiggingBuddy = buddy;
            hoverColor = newKind == TownObjectKind.Tree ? Color.white : new Color32(0xD3, 0xD3, 0xD3, 0xFF);
        }
#endif
    }
}
