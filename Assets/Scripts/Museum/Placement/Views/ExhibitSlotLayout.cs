using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace ProjectMuseum.Builder
{
    /// <summary>Artifact footprint sizes (tiny cells): Tiny 1×1 · Small 1×2 · Medium 2×2 · Large 2×4 · Huge 4×4.</summary>
    public enum ArtifactSize { Tiny, Small, Medium, Large, Huge }

    /// <summary>
    /// Per-exhibit map of "which pre-placed SpriteRenderer shows the artifact at each
    /// display-grid cell". Lives on the exhibit prefab.
    ///
    /// Pick the exhibit's tile size, press <b>Rebuild Slots For This Size</b>, and the
    /// inspector lists every slot for that size (Tiny/Small/Medium/Large/Huge) labelled
    /// by its <c>(col,row)</c> coordinate. Drag the matching pre-placed SpriteRenderer
    /// onto each. At runtime <see cref="ExhibitObjectView"/> sets the artifact's
    /// isometric sprite on the relevant renderer and enables it; every slot with no
    /// artifact is disabled (hidden).
    ///
    /// Coordinates are <c>(col,row)</c>: col 0 = LEFT (→ right), row 0 = TOP (→ down),
    /// matching the exhibit editor grid.
    /// </summary>
    public class ExhibitSlotLayout : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public ArtifactSize size;
            public int col;
            public int row;
            [Tooltip("Pre-placed SpriteRenderer shown when an artifact occupies this slot.")]
            public SpriteRenderer renderer;
        }

        [Tooltip("Exhibit footprint in TILES (1 for a 1×1, 2 for a 2×2, …).")]
        [SerializeField] private int widthInTiles = 1;
        [SerializeField] private int lengthInTiles = 1;
        [Tooltip("Tiny cells per tile axis — MUST match ExhibitEditorUI.slotsPerTileAxis (default 2).")]
        [SerializeField] private int slotsPerTileAxis = 2;

        [SerializeField] private List<Slot> slots = new();

        public int SlotsPerTileAxis => Mathf.Max(1, slotsPerTileAxis);

        /// <summary>All assigned slot renderers (skips empty slots).</summary>
        public IEnumerable<SpriteRenderer> SlotRenderers
        {
            get
            {
                foreach (Slot s in slots)
                    if (s != null && s.renderer != null) yield return s.renderer;
            }
        }

        /// <summary>The renderer for a given artifact size at a grid cell, or null if unassigned.</summary>
        public SpriteRenderer GetRenderer(ArtifactSize size, int col, int row)
        {
            foreach (Slot s in slots)
                if (s != null && s.size == size && s.col == col && s.row == row)
                    return s.renderer;
            return null;
        }

        /// <summary>Show/hide every assigned slot (deactivates the object → unassigned slots are inactive).</summary>
        public void SetAllSlotsVisible(bool visible)
        {
            foreach (Slot s in slots)
                if (s != null && s.renderer != null) s.renderer.gameObject.SetActive(visible);
        }

        /// <summary>Artifact size → footprint in tiny cells (w × h).</summary>
        public static (int w, int h) Footprint(ArtifactSize size) => size switch
        {
            ArtifactSize.Tiny => (1, 1),
            ArtifactSize.Small => (1, 2),
            ArtifactSize.Medium => (2, 2),
            ArtifactSize.Large => (2, 4),
            ArtifactSize.Huge => (4, 4),
            _ => (1, 1),
        };

        /// <summary>Parse an artifact's ObjectSize string (from the catalogue) into an <see cref="ArtifactSize"/>.</summary>
        public static ArtifactSize ParseSize(string objectSize)
        {
            switch ((objectSize ?? "").Trim().ToLowerInvariant())
            {
                case "small": return ArtifactSize.Small;
                case "medium": return ArtifactSize.Medium;
                case "large": return ArtifactSize.Large;
                case "huge": return ArtifactSize.Huge;
                default: return ArtifactSize.Tiny;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Regenerate the slot list for the current size — one entry per size-aligned
        /// cell of every footprint that fits — keeping any renderers already assigned.
        /// </summary>
        public void EditorRebuildSlots()
        {
            var existing = new Dictionary<(ArtifactSize, int, int), SpriteRenderer>();
            foreach (Slot s in slots)
                if (s != null) existing[(s.size, s.col, s.row)] = s.renderer;

            int axis = Mathf.Max(1, slotsPerTileAxis);
            int cols = Mathf.Max(1, widthInTiles) * axis;
            int rows = Mathf.Max(1, lengthInTiles) * axis;

            var rebuilt = new List<Slot>();
            foreach (ArtifactSize size in System.Enum.GetValues(typeof(ArtifactSize)))
            {
                (int fw, int fh) = Footprint(size);
                if (fw > cols || fh > rows) continue; // this size doesn't fit this exhibit
                for (int row = 0; row + fh <= rows; row += fh)
                    for (int col = 0; col + fw <= cols; col += fw)
                    {
                        var slot = new Slot { size = size, col = col, row = row };
                        existing.TryGetValue((size, col, row), out slot.renderer);
                        rebuilt.Add(slot);
                    }
            }
            slots = rebuilt;
        }

        private void OnDrawGizmosSelected()
        {
            foreach (Slot s in slots)
            {
                if (s == null || s.renderer == null) continue;
                Gizmos.color = Handles.color = Color.cyan;
                Gizmos.DrawWireSphere(s.renderer.transform.position, 0.06f);
                Handles.Label(s.renderer.transform.position, $"{s.size} ({s.col},{s.row})");
            }
        }
#endif
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(ExhibitSlotLayout))]
    public class ExhibitSlotLayoutEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(serializedObject.FindProperty("widthInTiles"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lengthInTiles"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("slotsPerTileAxis"));

            EditorGUILayout.HelpBox(
                "Coordinates are (col,row): col 0 = LEFT (→ right), row 0 = TOP (→ down).\n" +
                "Pick the size above, press Rebuild, then assign each slot's SpriteRenderer.",
                MessageType.Info);

            if (GUILayout.Button("Rebuild Slots For This Size"))
            {
                foreach (Object t in targets)
                {
                    var layout = (ExhibitSlotLayout)t;
                    Undo.RecordObject(layout, "Rebuild Exhibit Slots");
                    layout.EditorRebuildSlots();
                    EditorUtility.SetDirty(layout);
                }
                serializedObject.Update();
            }

            SerializedProperty slotsProp = serializedObject.FindProperty("slots");
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Slots ({slotsProp.arraySize})", EditorStyles.boldLabel);

            ArtifactSize lastSize = (ArtifactSize)(-1);
            for (int i = 0; i < slotsProp.arraySize; i++)
            {
                SerializedProperty el = slotsProp.GetArrayElementAtIndex(i);
                var size = (ArtifactSize)el.FindPropertyRelative("size").enumValueIndex;
                int col = el.FindPropertyRelative("col").intValue;
                int row = el.FindPropertyRelative("row").intValue;

                if (size != lastSize) // header per size group
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.LabelField(size.ToString(), EditorStyles.miniBoldLabel);
                    lastSize = size;
                }

                EditorGUILayout.PropertyField(el.FindPropertyRelative("renderer"),
                    new GUIContent($"({col},{row})"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
