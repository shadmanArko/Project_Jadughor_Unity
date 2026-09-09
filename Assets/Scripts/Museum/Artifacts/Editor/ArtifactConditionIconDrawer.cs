using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Builder.EditorTools
{
    /// <summary>
    /// Draws an <see cref="ArtifactConditionIcon"/> on one line — condition, rarity and the
    /// sprite side by side. Nine of these per artifact stack up fast; the default nested
    /// foldout would make the database unreadable.
    /// </summary>
    [CustomPropertyDrawer(typeof(ArtifactConditionIcon))]
    public class ArtifactConditionIconDrawer : PropertyDrawer
    {
        private const float Gap = 4f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty condition = property.FindPropertyRelative("Condition");
            SerializedProperty rarity = property.FindPropertyRelative("Rarity");
            SerializedProperty icon = property.FindPropertyRelative("Icon");

            EditorGUI.BeginProperty(position, label, property);
            position = EditorGUI.PrefixLabel(position, new GUIContent(Code(condition, rarity)));

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            float third = (position.width - Gap * 2f) / 3f;
            var rect = new Rect(position.x, position.y, third, EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(rect, condition, GUIContent.none);
            rect.x += third + Gap;
            EditorGUI.PropertyField(rect, rarity, GUIContent.none);
            rect.x += third + Gap;
            EditorGUI.PropertyField(rect, icon, GUIContent.none);

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight;

        /// <summary>"PC_LR" — the filename code, so a row lines up with the source PNG.</summary>
        private static string Code(SerializedProperty condition, SerializedProperty rarity)
        {
            var c = (ArtifactCondition)condition.enumValueIndex;
            var r = (ArtifactRarity)rarity.enumValueIndex;
            return $"{ArtifactCodes.Of(c)}_{ArtifactCodes.Of(r)}";
        }
    }
}
