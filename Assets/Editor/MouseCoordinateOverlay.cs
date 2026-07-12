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
        Event e = Event.current;

        // Only trust mouse position on events that actually carry a real cursor position
        if (e.type != EventType.MouseMove &&
            e.type != EventType.MouseDrag &&
            e.type != EventType.Repaint &&
            e.type != EventType.MouseDown &&
            e.type != EventType.MouseUp)
            return;

        Vector2 mousePos = e.mousePosition;
        Camera cam = sceneView.camera;

        float pixelsPerPoint = EditorGUIUtility.pixelsPerPoint;
        float screenX = mousePos.x * pixelsPerPoint;
        float screenY = cam.pixelHeight - mousePos.y * pixelsPerPoint;

        // Guard: skip if outside the camera's actual pixel rect
        if (screenX < 0 || screenX > cam.pixelWidth ||
            screenY < 0 || screenY > cam.pixelHeight)
            return;

        Vector3 worldPos = cam.ScreenToWorldPoint(new Vector3(screenX, screenY, 0));
        worldPos.z = 0;

        if (_tilemap == null)
            _tilemap = Object.FindObjectOfType<Tilemap>();

        Vector3Int cell = default;
        bool hasTilemap = _tilemap != null;
        if (hasTilemap)
            cell = _tilemap.WorldToCell(worldPos);

        Handles.BeginGUI();
        GUI.Box(new Rect(10, 10, 200, hasTilemap ? 52 : 32), GUIContent.none);
        GUI.Label(new Rect(15, 14, 190, 20), $"World: ({worldPos.x:F1}, {worldPos.y:F1})");
        if (hasTilemap)
            GUI.Label(new Rect(15, 32, 190, 20), $"Cell:  ({cell.x}, {cell.y})");
        Handles.EndGUI();

        sceneView.Repaint();
    }
}
#endif