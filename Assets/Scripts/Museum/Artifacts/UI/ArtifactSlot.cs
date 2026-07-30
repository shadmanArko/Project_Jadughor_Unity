using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// One display slot in the exhibit editor's right-hand grid. Accepts a dropped
    /// <see cref="ArtifactCard"/> (assign), and left-clicking a filled slot clears
    /// it (the artifact returns to storage).
    /// </summary>
    public class ArtifactSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        [Tooltip("Image that shows the placed artifact's icon (empty when the slot is free).")]
        [SerializeField] private Image icon;

        public int Index { get; private set; }
        public bool IsFilled => icon != null && icon.sprite != null && icon.enabled;

        private ExhibitEditorUI _owner;

        public void Setup(int index, ExhibitEditorUI owner)
        {
            Index = index;
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

        public void OnDrop(PointerEventData e)
        {
            ArtifactCard card = e.pointerDrag != null ? e.pointerDrag.GetComponent<ArtifactCard>() : null;
            if (card != null) _owner?.AssignToSlot(Index, card.InstanceId);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (IsFilled && e.button == PointerEventData.InputButton.Left)
                _owner?.ClearSlot(Index);
        }
    }
}
