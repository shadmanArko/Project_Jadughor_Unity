using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Lives on a Forest Chunk plot. While the pointer hovers the plot, the Expand
/// button is shown; clicking it asks the <see cref="ExpansionManager"/> to
/// develop the chunk this plot sits on, then deactivates the forest.
///
/// The chunk coordinate is auto-detected from this object's world position, so
/// you can drop as many forest plots as you like all around the museum without
/// tagging each one. (Turn off auto-detect to set it manually.)
///
/// Hover is detected by polling the pointer against the collider (so the
/// world-space Canvas in front doesn't block it). The Expand button's OnClick
/// is wired automatically.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ForestChunkExpansion : MonoBehaviour
{
    [Tooltip("Detect this plot's chunk coordinate from its world position. " +
             "Turn off to set Manual Chunk instead.")]
    [SerializeField] private bool autoDetectChunk = true;
    [Tooltip("Used only when Auto Detect Chunk is off.")]
    [SerializeField] private Vector2Int manualChunk = new Vector2Int(1, 0);

    [Tooltip("The Expand button root (e.g. MuseumExpansionCanvas). " +
             "Defaults to the first child if left empty.")]
    [SerializeField] private GameObject expandButtonRoot;

    [Tooltip("The scene's ExpansionManager. Auto-found if left empty.")]
    [SerializeField] private ExpansionManager manager;

    [Tooltip("Resize the BoxCollider2D to the sprite bounds on Awake.")]
    [SerializeField] private bool autoFitCollider = true;

    private Collider2D _col;
    private Camera _cam;
    private bool _shown;

    public Vector2Int ChunkCoord =>
        (autoDetectChunk && manager != null)
            ? manager.WorldToChunk(transform.position)
            : manualChunk;

    void Awake()
    {
        _col = GetComponent<Collider2D>();
        _cam = Camera.main;

        if (autoFitCollider) FitColliderToSprite();
        if (manager == null) manager = FindFirstObjectByType<ExpansionManager>();
        if (expandButtonRoot == null && transform.childCount > 0)
            expandButtonRoot = transform.GetChild(0).gameObject;

        // World-space canvases need an event camera or clicks won't register.
        var canvas = expandButtonRoot != null ? expandButtonRoot.GetComponent<Canvas>() : null;
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace && canvas.worldCamera == null)
            canvas.worldCamera = _cam;

        // Auto-wire the Expand button so no manual OnClick setup is needed.
        var button = expandButtonRoot != null
            ? expandButtonRoot.GetComponentInChildren<Button>(true)
            : GetComponentInChildren<Button>(true);
        if (button != null)
        {
            button.onClick.RemoveListener(Expand);
            button.onClick.AddListener(Expand);
        }

        ShowButton(false);
    }

    void Update()
    {
        if (expandButtonRoot == null || Mouse.current == null) return;
        if (_cam == null) { _cam = Camera.main; if (_cam == null) return; }

        Vector3 world = _cam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        bool overChunk = _col.OverlapPoint(world);
        // If no manager is wired yet, still show on hover so the collider can be
        // verified. Once a manager exists, respect the expansion rules.
        bool show = overChunk && (manager == null || manager.CanExpand(ChunkCoord));
        if (show != _shown) ShowButton(show);
    }

    void FitColliderToSprite()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null && _col is BoxCollider2D box)
        {
            box.size = sr.sprite.bounds.size;     // local space
            box.offset = sr.sprite.bounds.center;
        }
    }

    void ShowButton(bool on)
    {
        _shown = on;
        if (expandButtonRoot != null) expandButtonRoot.SetActive(on);
    }

    /// <summary>Develops this chunk and deactivates the forest plot.</summary>
    public void Expand()
    {
        if (manager == null)
        {
            Debug.LogWarning("[ForestChunkExpansion] No ExpansionManager in the scene.", this);
            return;
        }

        Vector2Int coord = ChunkCoord;
        if (manager.TryExpand(coord))
            gameObject.SetActive(false);
        else
            Debug.Log($"[ForestChunkExpansion] '{name}' (chunk {coord}) could not expand.", this);
    }
}
