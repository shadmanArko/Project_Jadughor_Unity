using System.Collections.Generic;
using Museum.Artifacts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// One artifact in the exhibit editor's left-hand storage list: icon, name and
    /// tag chips. Drag it onto a slot on the right to place it. The drag itself is
    /// driven by the owning <see cref="ExhibitEditorUI"/> (it spawns/moves the drag
    /// ghost); a slot reads the dragged card via <c>eventData.pointerDrag</c>.
    /// </summary>
    public class ArtifactCard : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;

        [Header("Tags (assign EITHER a chip prefab + container, OR a single label)")]
        [Tooltip("Container the chips are parented under — give it a FlowLayoutGroup so " +
                 "variable-width chips wrap.")]
        [SerializeField] private Transform tagContainer;
        [Tooltip("The ArtifactTagChip prefab — one instantiated per tag.")]
        [SerializeField] private ArtifactTagChip tagChipPrefab;
        [Tooltip("Fallback: a single label showing all tags joined, if no chip prefab.")]
        [SerializeField] private TMP_Text tagsLabel;

        public string InstanceId { get; private set; }
        public string RawArtifactId { get; private set; }
        public Sprite Icon => icon != null ? icon.sprite : null;

        private ExhibitEditorUI _owner;

        public void Setup(MuseumArtifactDatabase.Entry entry, string instanceId, ExhibitEditorUI owner)
        {
            _owner = owner;
            InstanceId = instanceId;
            RawArtifactId = entry.Id;

            if (icon != null)
            {
                icon.sprite = entry.Icon;
                icon.enabled = entry.Icon != null;
                icon.preserveAspect = true;
            }
            if (nameLabel != null) nameLabel.text = entry.Name;
            SetupTags(entry.Tags);
        }

        private void SetupTags(List<string> tags)
        {
            if (tagContainer != null && tagChipPrefab != null)
            {
                for (int i = tagContainer.childCount - 1; i >= 0; i--)
                    Destroy(tagContainer.GetChild(i).gameObject);
                foreach (string t in tags)
                {
                    ArtifactTagChip chip = Instantiate(tagChipPrefab, tagContainer);
                    chip.SetTag(t);
                    chip.gameObject.SetActive(true);
                }
                if (tagsLabel != null) tagsLabel.gameObject.SetActive(false);
            }
            else if (tagsLabel != null)
            {
                tagsLabel.text = string.Join("   ", tags);
            }
        }

        // Pressing the card previews its placement groups on the grid before any drag.
        public void OnPointerDown(PointerEventData e) => _owner?.PreviewFootprint(InstanceId);
        public void OnPointerUp(PointerEventData e) => _owner?.ClearPreview();

        // Drag is delegated to the owner (single place that owns the drag ghost + grid highlight).
        public void OnBeginDrag(PointerEventData e) => _owner?.BeginCardDrag(this, e);
        public void OnDrag(PointerEventData e) => _owner?.DragUpdate(e);
        public void OnEndDrag(PointerEventData e) => _owner?.EndDrag(e);
    }
}
