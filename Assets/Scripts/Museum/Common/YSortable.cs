#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[ExecuteAlways]
public class YSortable : MonoBehaviour
{
    [SerializeField] private int baseOrder = 0;
    [SerializeField] private float sortOffset = 0f;

    // Every SpriteRenderer under this object, not just one on this exact
    // GameObject — a multi-part prefab (e.g. a 2x2 exhibit built from several
    // child sprites) must sort as ONE consistent unit, or whichever renderer
    // isn't reached stays at its stale default order and produces a "partially
    // in front, partially behind" glitch against neighbours.
    private SpriteRenderer[] _renderers;

#if UNITY_EDITOR
    private Vector3 _lastPos;

    void OnEnable()
    {
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
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
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        UpdateSortOrder();
    }

    /// <summary>Small manual nudge on top of the Y comparison (world units).</summary>
    public void SetSortOffset(float offset)
    {
        sortOffset = offset;
    }

    // NOTE: placed museum objects are NOT sorted by this component any more —
    // MuseumSortingSystem owns their depth (footprint-aware pairwise sorting) and
    // the placement system removes YSortable from spawned instances. This stays
    // the simple Y-based sorter for everything else (characters, walls, one-off
    // scene sprites).
    public void UpdateSortOrder()
    {
        if (_renderers == null || _renderers.Length == 0)
            _renderers = GetComponentsInChildren<SpriteRenderer>(true);
        if (_renderers == null || _renderers.Length == 0) return;

        // Combined bounds across every renderer, so a multi-part object sorts
        // as a whole. bounds.min.y (world space) — for a bottom-pivoted sprite
        // this equals transform.position.y exactly (the pivot IS the local-
        // space origin).
        Bounds combined = _renderers[0].bounds;
        for (int i = 1; i < _renderers.Length; i++)
            if (_renderers[i] != null) combined.Encapsulate(_renderers[i].bounds);
        int order = baseOrder + Mathf.RoundToInt((-combined.min.y + sortOffset) * 100f);

        foreach (SpriteRenderer r in _renderers)
            if (r != null) r.sortingOrder = order;
    }
}
