using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Guests.EditorTools
{
    /// <summary>
    /// Inspector + Scene view tools for <see cref="GuestController"/>: an auto-fill button for the
    /// appearance pools, draggable cell-snapped handles for spawn points and the two door cells,
    /// and live population stats in play mode.
    /// </summary>
    [CustomEditor(typeof(GuestController))]
    public class GuestControllerEditor : Editor
    {
        private SerializedProperty _spawnPoints;
        private SerializedProperty _doorInside;
        private SerializedProperty _doorOutside;

        private void OnEnable()
        {
            _spawnPoints = serializedObject.FindProperty("spawnPoints");
            _doorInside = serializedObject.FindProperty("doorInsideCell");
            _doorOutside = serializedObject.FindProperty("doorOutsideCell");
        }

        public override void OnInspectorGUI()
        {
            var controller = (GuestController)target;

            if (GUILayout.Button("Auto-fill From Guest Sheets"))
                controller.EditorAutoFillAppearance();

            EditorGUILayout.HelpBox(
                "Scene view: red = spawn/exit points, yellow = door (outside), green = door (inside), " +
                "blue outlines = outside walk areas. Drag the dots to move points; they snap to cells.",
                MessageType.None);

            if (Application.isPlaying)
            {
                EditorGUILayout.LabelField("Live",
                    $"{controller.ActiveGuests.Count} guests · {controller.GuestsInMuseum} in museum",
                    EditorStyles.boldLabel);
                Repaint();
            }

            EditorGUILayout.Space();
            DrawDefaultInspector();
        }

        private void OnSceneGUI()
        {
            var controller = (GuestController)target;
            var grid = controller.Grid != null ? controller.Grid : FindFirstObjectByType<Grid>();
            if (grid == null) return;

            serializedObject.Update();

            for (var i = 0; i < _spawnPoints.arraySize; i++)
                CellHandle(grid, _spawnPoints.GetArrayElementAtIndex(i), new Color(1f, 0.35f, 0.35f), $"Spawn/Exit {i}");

            CellHandle(grid, _doorOutside, new Color(1f, 0.9f, 0.2f), "Door (outside)");
            CellHandle(grid, _doorInside, new Color(0.3f, 1f, 0.4f), "Door (inside)");

            serializedObject.ApplyModifiedProperties();
        }

        private static void CellHandle(Grid grid, SerializedProperty prop, Color color, string label)
        {
            var cell = prop.vector2IntValue;
            var center = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            var size = HandleUtility.GetHandleSize(center) * 0.08f;

            Handles.color = color;
            Handles.Label(center + Vector3.up * size * 2.5f, $"{label} {cell}");

            EditorGUI.BeginChangeCheck();
            var moved = Handles.FreeMoveHandle(center, size, Vector3.zero, Handles.DotHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;

            var c = grid.WorldToCell(moved);
            prop.vector2IntValue = new Vector2Int(c.x, c.y);
        }
    }
}
