using UnityEditor;
using UnityEngine;

namespace ProjectMuseum.Guests.EditorTools
{
    /// <summary>
    /// Inspector + Scene view tools for <see cref="GuestController"/>: an auto-fill button for the
    /// appearance pools, draggable cell-snapped handles for spawn zones, walk areas and the door cells,
    /// and live population stats in play mode.
    /// </summary>
    [CustomEditor(typeof(GuestController))]
    public class GuestControllerEditor : Editor
    {
        private SerializedProperty _spawnZones;
        private SerializedProperty _sidewalks;
        private SerializedProperty _crosswalks;
        private SerializedProperty _doorInside;
        private SerializedProperty _doorOutside;

        private void OnEnable()
        {
            _spawnZones = serializedObject.FindProperty("spawnZones");
            _sidewalks = serializedObject.FindProperty("sidewalkAreas");
            _crosswalks = serializedObject.FindProperty("crosswalkAreas");
            _doorInside = serializedObject.FindProperty("doorInsideCell");
            _doorOutside = serializedObject.FindProperty("doorOutsideCell");
        }

        public override void OnInspectorGUI()
        {
            var controller = (GuestController)target;

            if (GUILayout.Button("Auto-fill From Guest Sheets"))
                controller.EditorAutoFillAppearance();

            EditorGUILayout.HelpBox(
                "Scene view: red = spawn/exit zones, blue = sidewalks, white = crosswalks, " +
                "yellow / green = door outside / inside. Drag a dot to move that area (snaps to " +
                "cells); resize areas with width/height in the lists below.",
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

            for (var i = 0; i < _sidewalks.arraySize; i++)
                RectHandle(grid, _sidewalks.GetArrayElementAtIndex(i), new Color(0.3f, 0.8f, 1f), $"Sidewalk {i}");

            for (var i = 0; i < _crosswalks.arraySize; i++)
                RectHandle(grid, _crosswalks.GetArrayElementAtIndex(i), Color.white, $"Crosswalk {i}");

            for (var i = 0; i < _spawnZones.arraySize; i++)
            {
                var zone = _spawnZones.GetArrayElementAtIndex(i);
                var name = zone.FindPropertyRelative("name").stringValue;
                RectHandle(grid, zone.FindPropertyRelative("area"), new Color(1f, 0.35f, 0.35f),
                           string.IsNullOrEmpty(name) ? $"Spawn/Exit {i}" : name);
            }

            CellHandle(grid, _doorOutside, new Color(1f, 0.9f, 0.2f), "Door (outside)");
            CellHandle(grid, _doorInside, new Color(0.3f, 1f, 0.4f), "Door (inside)");

            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>A dot at the rect's centre; dragging moves the whole rect by whole cells.</summary>
        private static void RectHandle(Grid grid, SerializedProperty prop, Color color, string label)
        {
            var r = prop.rectIntValue;
            var a = grid.CellToWorld(new Vector3Int(r.xMin, r.yMin, 0));
            var c = grid.CellToWorld(new Vector3Int(r.xMax, r.yMax, 0));
            var center = (a + c) * 0.5f;
            var size = HandleUtility.GetHandleSize(center) * 0.08f;

            Handles.color = color;
            Handles.Label(center + Vector3.up * size * 2.5f, $"{label} ({r.xMin},{r.yMin}) {r.width}x{r.height}");

            EditorGUI.BeginChangeCheck();
            var moved = Handles.FreeMoveHandle(center, size, Vector3.zero, Handles.DotHandleCap);
            if (!EditorGUI.EndChangeCheck()) return;

            var delta = grid.WorldToCell(moved) - grid.WorldToCell(center);
            if (delta.x == 0 && delta.y == 0) return;
            r.position += new Vector2Int(delta.x, delta.y);
            prop.rectIntValue = r;
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
