#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ArrowKeyMover
{
    static float _step = 0.01f; // adjust to match your tile/grid size

    static ArrowKeyMover()
    {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    static void OnSceneGUI(SceneView sceneView)
    {
        Event e = Event.current;

        if (e.type != EventType.KeyDown) return;
        if (Selection.activeGameObject == null) return;

        Vector3 move = Vector3.zero;

        switch (e.keyCode)
        {
            case KeyCode.UpArrow:    move = Vector3.up    * _step; break;
            case KeyCode.DownArrow:  move = Vector3.down  * _step; break;
            case KeyCode.LeftArrow:  move = Vector3.left  * _step; break;
            case KeyCode.RightArrow: move = Vector3.right * _step; break;
            default: return;
        }

        Undo.RecordObject(Selection.activeGameObject.transform, "Arrow Move");
        Selection.activeGameObject.transform.position += move;
        e.Use(); // consume the event so Unity doesn't scroll the view
    }
}
#endif