#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

[InitializeOnLoad]
public static class MouseCoordinateOverlay
{
    static Tilemap _tilemap;

    static MouseCoordinateOverlay()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        // Get mouse position in world space
        Event e = Event.current;
        Vector2 mousePos = e.mousePosition;

        // Convert to world position
        float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
        mousePos.y = sceneView.camera.pixelHeight - mousePos.y * pixelsPerPoint;
        mousePos.x *= pixelsPerPoint;

        Vector3 worldPos = sceneView.camera.ScreenToWorldPoint(
            new Vector3(mousePos.x, mousePos.y, 0)
        );
        worldPos.z = 0;

        // Try to get tilemap cell if available
        if (_tilemap == null)
            _tilemap = GameObject.FindObjectOfType<Tilemap>();

        string cellInfo = "";
        if (_tilemap != null)
        {
            Vector3Int cell = _tilemap.WorldToCell(worldPos);
            cellInfo = $"\nCell: ({cell.x}, {cell.y})";
        }

        // Draw overlay in top-left of Scene view
        Handles.BeginGUI();
        GUI.Box(new Rect(10, 10, 200, _tilemap ? 52 : 32), GUIContent.none);
        GUI.Label(new Rect(15, 14, 190, 20),
            $"World: ({worldPos.x:F1}, {worldPos.y:F1})");
        if (cellInfo != "")
            GUI.Label(new Rect(15, 32, 190, 20),
                $"Cell:  ({_tilemap.WorldToCell(worldPos).x}, {_tilemap.WorldToCell(worldPos).y})");
        Handles.EndGUI();

        sceneView.Repaint();
    }
}
#endif