using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// One tiny grid cell in the exhibit editor. Cells are the base unit; an artifact
    /// covers a footprint of cells depending on its size. A cell can be:
    ///  • a drop target (drop a dragged artifact → snaps to a size-aligned anchor),
    ///  • a drag source (drag a placed artifact out — grabs the whole covering artifact),
    ///  • click-to-remove (left-click a filled cell clears its artifact).
    ///
    /// Each cell also has four thin edge strips (top/bottom/left/right). A placement
    /// group is outlined by enabling only the strips on that group's perimeter, giving
    /// real border LINES around each candidate slot. All logic lives in
    /// <see cref="ExhibitEditorUI"/>; the cell just reports its index and paints.
    /// </summary>
    public class ArtifactSlot : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler, IPointerClickHandler
    {
        [Tooltip("Cell background — recoloured for empty / occupied.")]
        [SerializeField] private Image background;
        [Tooltip("Artifact art, shown on the footprint's anchor cell.")]
        [SerializeField] private Image icon;

        [Header("Group border strips (thin edges)")]
        [SerializeField] private Image borderTop;
        [SerializeField] private Image borderBottom;
        [SerializeField] private Image borderLeft;
        [SerializeField] private Image borderRight;

        public int Index { get; private set; }
        public int Col { get; private set; }
        public int Row { get; private set; }
        public RectTransform RT => (RectTransform)transform;

        private ExhibitEditorUI _owner;

        public void Setup(int index, int col, int row, ExhibitEditorUI owner)
        {
            Index = index;
            Col = col;
            Row = row;
            _owner = owner;
            SetIcon(null);
        }

        public void SetIcon(Sprite sprite)
        {
            if (icon == null) return;
            icon.sprite = sprite;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
        }

        public void SetBackground(Color color)
        {
            if (background != null) background.color = color;
        }

        /// <summary>Colour all four edges the same (idle grid lines).</summary>
        public void SetBorderColor(Color color) => SetEdgeColors(color, color, color, color);

        /// <summary>
        /// Colour each edge independently — lets a placement group paint its OUTER
        /// (perimeter) edges one colour and the INNER edges shared with same-group
        /// cells another. Strips stay always visible; only the colour changes.
        /// </summary>
        public void SetEdgeColors(Color top, Color bottom, Color left, Color right)
        {
            Edge(borderTop, top);
            Edge(borderBottom, bottom);
            Edge(borderLeft, left);
            Edge(borderRight, right);
        }

        private static void Edge(Image img, Color color)
        {
            if (img == null) return;
            img.enabled = true;
            img.color = color;
        }

        public void OnBeginDrag(PointerEventData e) => _owner?.BeginSlotDrag(Index, e);
        public void OnDrag(PointerEventData e) => _owner?.DragUpdate(e);
        public void OnEndDrag(PointerEventData e) => _owner?.EndDrag(e);
        public void OnDrop(PointerEventData e) => _owner?.DropOnCell(Index, e);

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) _owner?.ClickCell(Index);
        }
    }
}
