#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteAlways]
public class IsometricChildLayouter : MonoBehaviour
{
    [SerializeField] private Vector3 startPosition = Vector3.zero;
    [SerializeField] private Vector3 step = new Vector3(0.5f, 0.25f, 0f);

    private int _lastChildCount;

#if UNITY_EDITOR
    void OnEnable()
    {
        EditorApplication.hierarchyChanged += OnHierarchyChanged;
        ApplyLayout();
    }

    void OnDisable()
    {
        EditorApplication.hierarchyChanged -= OnHierarchyChanged;
    }

    void OnHierarchyChanged()
    {
        if (transform.childCount != _lastChildCount)
        {
            _lastChildCount = transform.childCount;
            ApplyLayout();
        }
    }

    void OnValidate() => ApplyLayout();
#endif

    public void ApplyLayout()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            transform.GetChild(i).localPosition = startPosition + step * i;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(IsometricChildLayouter))]
public class IsometricChildLayouterEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Force Reapply Layout"))
        {
            ((IsometricChildLayouter)target).ApplyLayout();
        }
    }
}
#endif