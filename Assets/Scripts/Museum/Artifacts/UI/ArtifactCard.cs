using System.Collections.Generic;
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
    public class ArtifactCard : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private TMP_Text nameLabel;

        [Header("Tags (assign EITHER a chip prefab + container, OR a single label)")]
        [Tooltip("Container that receives one instantiated tag chip per tag.")]
        [SerializeField] private Transform tagContainer;
        [Tooltip("A small TMP label prefab used as a tag chip.")]
        [SerializeField] private TMP_Text tagChipPrefab;
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
                    TMP_Text chip = Instantiate(tagChipPrefab, tagContainer);
                    chip.text = t;
                    chip.gameObject.SetActive(true);
                }
                if (tagsLabel != null) tagsLabel.gameObject.SetActive(false);
            }
            else if (tagsLabel != null)
            {
                tagsLabel.text = string.Join("   ", tags);
            }
        }

        // Drag is delegated to the owner (single place that owns the drag ghost).
        public void OnBeginDrag(PointerEventData e) => _owner?.BeginCardDrag(this, e);
        public void OnDrag(PointerEventData e) => _owner?.DragCard(e);
        public void OnEndDrag(PointerEventData e) => _owner?.EndCardDrag(e);
    }
}
