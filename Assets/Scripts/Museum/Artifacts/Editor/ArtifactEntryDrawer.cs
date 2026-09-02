using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Builder.EditorTools
{
    /// <summary>
    /// Labels each <see cref="MuseumArtifactDatabase.Entry"/> in the database list by its
    /// Id instead of "Element 0" / "Element 1". Children are drawn manually — running
    /// EditorGUI.PropertyField on the entry itself from inside its own drawer would recurse.
    /// </summary>
    [CustomPropertyDrawer(typeof(MuseumArtifactDatabase.Entry))]
    public class ArtifactEntryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, Label(property, label), true);
            if (!property.isExpanded) return;

            EditorGUI.indentLevel++;
            SerializedProperty end = property.GetEndProperty();
            SerializedProperty it = property.Copy();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                enterChildren = false;
                row.y += row.height + EditorGUIUtility.standardVerticalSpacing;
                row.height = EditorGUI.GetPropertyHeight(it, true);
                EditorGUI.PropertyField(row, it, true);
            }
            EditorGUI.indentLevel--;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return height;

            SerializedProperty end = property.GetEndProperty();
            SerializedProperty it = property.Copy();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren) && !SerializedProperty.EqualContents(it, end))
            {
                enterChildren = false;
                height += EditorGUI.GetPropertyHeight(it, true) + EditorGUIUtility.standardVerticalSpacing;
            }
            return height;
        }

        private static GUIContent Label(SerializedProperty property, GUIContent fallback)
        {
            string id = property.FindPropertyRelative("Id")?.stringValue;
            return string.IsNullOrEmpty(id) ? fallback : new GUIContent(id, fallback.tooltip);
        }
    }
}
