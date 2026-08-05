using TMPro;
using UnityEngine;

namespace Museum.Artifacts.UI
{
    /// <summary>
    /// One tag chip: sets its label text. Sizing to the text is handled by layout
    /// components on the prefab (a HorizontalLayoutGroup that reports the chip's
    /// preferred width from the label) — see the exhibit-editor setup notes.
    /// </summary>
    public class ArtifactTagChip : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI tagText;

        public void SetTag(string artifactTag)
        {
            if (tagText != null) tagText.text = artifactTag;
        }
    }
}
