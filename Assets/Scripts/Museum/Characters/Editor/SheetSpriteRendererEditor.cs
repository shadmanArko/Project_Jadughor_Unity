using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Characters.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="SheetSpriteRenderer"/>: the sheet is drawn as a clickable grid
    /// with the active cell highlighted, so finding the index of a pose is a click instead of
    /// counting cells by hand. The scrub slider steps frames with live Scene view feedback.
    /// </summary>
    [CustomEditor(typeof(SheetSpriteRenderer))]
    [CanEditMultipleObjects]
    public class SheetSpriteRendererEditor : Editor
    {
        SerializedProperty _sheet;
        SerializedProperty _hFrames;
        SerializedProperty _vFrames;
        SerializedProperty _frame;
        SerializedProperty _frameOffset;
        SerializedProperty _pivot;
        SerializedProperty _pixelsPerUnit;
        SerializedProperty _flipX;

        void OnEnable()
        {
            _sheet = serializedObject.FindProperty("_sheet");
            _hFrames = serializedObject.FindProperty("_hFrames");
            _vFrames = serializedObject.FindProperty("_vFrames");
            _frame = serializedObject.FindProperty("Frame");
            _frameOffset = serializedObject.FindProperty("_frameOffset");
            _pivot = serializedObject.FindProperty("_pivot");
            _pixelsPerUnit = serializedObject.FindProperty("_pixelsPerUnit");
            _flipX = serializedObject.FindProperty("_flipX");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_sheet);
            EditorGUILayout.PropertyField(_hFrames, new GUIContent("Columns (hFrames)"));
            EditorGUILayout.PropertyField(_vFrames, new GUIContent("Rows (vFrames)"));

            var component = (SheetSpriteRenderer)target;
            var sheet = _sheet.objectReferenceValue as Texture2D;
            var columns = Mathf.Max(1, _hFrames.intValue);
            var rows = Mathf.Max(1, _vFrames.intValue);
            var frameCount = columns * rows;

            if (sheet != null)
            {
                EditorGUILayout.LabelField("Cell Size",
                    $"{sheet.width / columns} x {sheet.height / rows} px   ({frameCount} frames)");

                if (sheet.width % columns != 0 || sheet.height % rows != 0)
                {
                    EditorGUILayout.HelpBox(
                        $"{sheet.width}x{sheet.height} does not divide evenly by {columns}x{rows} — " +
                        "remainder pixels on the right/bottom edge are dropped.",
                        MessageType.Warning);
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_frame);
            EditorGUILayout.PropertyField(_frameOffset, new GUIContent("Frame Offset"));

            if (!_frame.hasMultipleDifferentValues && frameCount > 1)
            {
                EditorGUI.BeginChangeCheck();
                var scrubbed = EditorGUILayout.IntSlider("Scrub",
                    Mathf.Clamp(_frame.intValue, 0, frameCount - 1), 0, frameCount - 1);
                if (EditorGUI.EndChangeCheck()) _frame.intValue = scrubbed;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Prev")) _frame.intValue = Mathf.Max(0, _frame.intValue - 1);
                    if (GUILayout.Button("Next")) _frame.intValue = Mathf.Min(frameCount - 1, _frame.intValue + 1);

                    var cell = component.FrameToCell(Mathf.Clamp(_frame.intValue, 0, frameCount - 1));
                    GUILayout.Label($"col {cell.x}, row {cell.y}", EditorStyles.miniLabel, GUILayout.Width(90f));
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_pivot);
            EditorGUILayout.PropertyField(_pixelsPerUnit);
            EditorGUILayout.PropertyField(_flipX, new GUIContent("Flip X"));

            if (sheet != null && !_frame.hasMultipleDifferentValues)
            {
                EditorGUILayout.Space();
                SheetGridGUI.DrawFramePreview(sheet, columns, rows, _frame.intValue + _frameOffset.intValue);
                EditorGUILayout.LabelField("Sheet — click a cell to select that frame", EditorStyles.miniBoldLabel);
                _frame.intValue = SheetGridGUI.DrawSheetGrid(sheet, columns, rows, _frame.intValue);
            }

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var obj in targets)
                {
                    if (obj is SheetSpriteRenderer r) r.Apply(true);
                }
            }
        }

        // While the Animation window drives the frame, keep redrawing so the inspector's
        // slider and highlighted cell track the clip instead of freezing on the last value.
        public override bool RequiresConstantRepaint() => AnimationMode.InAnimationMode();
    }
}
