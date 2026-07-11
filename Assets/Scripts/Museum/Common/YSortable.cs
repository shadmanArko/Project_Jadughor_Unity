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
        _sr.sortingOrder = baseOrder + Mathf.RoundToInt((-transform.position.y + sortOffset) * 100f);
    }
}