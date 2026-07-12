#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class YSortable : MonoBehaviour
{
    [SerializeField] private int baseOrder = 0;
    [SerializeField] private float sortOffset = 0f;

    private SpriteRenderer _sr;

#if UNITY_EDITOR
    private Vector3 _lastPos;

    void OnEnable()
    {
        _sr = GetComponent<SpriteRenderer>();
        EditorApplication.update += EditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
    }

    void EditorUpdate()
    {
        if (transform.position == _lastPos) return;
        _lastPos = transform.position;
        UpdateSortOrder();
    }

    void OnValidate() => UpdateSortOrder();
#endif

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        UpdateSortOrder();
    }

    public void UpdateSortOrder()
    {
        if (_sr == null) _sr = GetComponent<SpriteRenderer>();

        // Sort by the sprite's actual rendered bottom edge (world space), NOT the
        // transform pivot. A 1x1 and a 2x2 object anchored at the same tile-row Y
        // have IDENTICAL transform.position.y (Y-sort never looked at footprint
        // size or X at all), so pivot-based sorting can't tell them apart — the
        // bigger sprite's bounds.min.y is what actually determines which one should
        // visually be "further forward" and draw on top.
        float sortY = _sr.bounds.min.y;
        _sr.sortingOrder = baseOrder + Mathf.RoundToInt((-sortY + sortOffset) * 100f);
    }
}