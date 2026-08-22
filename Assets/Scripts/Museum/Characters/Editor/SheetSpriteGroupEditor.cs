using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Characters.EditorTools
{
    /// <summary>
    /// Inspector for <see cref="SheetSpriteGroup"/>. Scrubbing here moves every layer at once,
    /// which is the only practical way to check that a layered guest's parts stay in register.
    /// </summary>
    [CustomEditor(typeof(SheetSpriteGroup))]
    public class SheetSpriteGroupEditor : Editor
    {
        SerializedProperty _frame;
        SerializedProperty _flipX;
        SerializedProperty _layers;
        SerializedProperty _driveSortingOrder;
        SerializedProperty _sortingOrderBase;

        void OnEnable()
        {
            _frame = serializedObject.FindProperty("Frame");
            _flipX = serializedObject.FindProperty("FlipX");
            _layers = serializedObject.FindProperty("_layers");
            _driveSortingOrder = serializedObject.FindProperty("_driveSortingOrder");
            _sortingOrderBase = serializedObject.FindProperty("_sortingOrderBase");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var group = (SheetSpriteGroup)target;
            var frameCount = SmallestLayerFrameCount(group);

            EditorGUILayout.PropertyField(_frame);

            if (frameCount > 1)
            {
                EditorGUI.BeginChangeCheck();
                var scrubbed = EditorGUILayout.IntSlider("Scrub",
                    Mathf.Clamp(_frame.intValue, 0, frameCount - 1), 0, frameCount - 1);
                if (EditorGUI.EndChangeCheck()) _frame.intValue = scrubbed;

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Prev")) _frame.intValue = Mathf.Max(0, _frame.intValue - 1);
                    if (GUILayout.Button("Next")) _frame.intValue = Mathf.Min(frameCount - 1, _frame.intValue + 1);
                }
            }

            EditorGUILayout.PropertyField(_flipX);

            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(_layers, true);
            EditorGUILayout.PropertyField(_driveSortingOrder);
            using (new EditorGUI.DisabledScope(!_driveSortingOrder.boolValue))
                EditorGUILayout.PropertyField(_sortingOrderBase);

            if (GUILayout.Button("Collect Child Layers"))
            {
                Undo.RecordObject(group, "Collect Child Layers");
                group.CollectChildLayers();
                EditorUtility.SetDirty(group);
            }

            if (serializedObject.ApplyModifiedProperties())
                group.Apply(true);
        }

        // A layer with a smaller grid caps the useful scrub range — past that, short layers
        // clamp and silently desync from the rest of the stack.
        static int SmallestLayerFrameCount(SheetSpriteGroup group)
        {
            var smallest = 0;
            foreach (var layer in group.Layers)
            {
                if (layer?.Renderer == null) continue;
                var count = layer.Renderer.FrameCount;
                if (smallest == 0 || count < smallest) smallest = count;
            }

            return smallest;
        }

        public override bool RequiresConstantRepaint() => AnimationMode.InAnimationMode();
    }
}
