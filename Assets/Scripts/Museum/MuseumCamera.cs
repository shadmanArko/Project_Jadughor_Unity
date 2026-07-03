using UnityEngine;
using UnityEngine.InputSystem;

public class MuseumCamera : MonoBehaviour
{
    [Header("Zoom")]
    [SerializeField] private float zoomSpeed = 2f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 15f;
    [SerializeField] private float zoomSmoothSpeed = 8f;

    [Header("Pan")]
    [SerializeField] private float panSpeed = 0.5f;
    [SerializeField] private float panSmoothSpeed = 10f;

    [Header("Pan Limits")]
    [SerializeField] private float minX = -10f;
    [SerializeField] private float maxX =  10f;
    [SerializeField] private float minY = -10f;
    [SerializeField] private float maxY =  10f;

    private Camera cam;
    private float targetZoom;
    private Vector3 targetPosition;

    // Pan drag state
    private bool isPanning;
    private Vector3 panOriginWorld;

    void Start()
    {
        cam = Camera.main;
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis  = new Vector3(0, 1, 0);

        targetZoom     = cam.orthographicSize;
        targetPosition = transform.position;
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
        ApplySmoothing();
    }

    // ── Zoom ───────────────────────────────────────────────────

    void HandleZoom()
    {
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scroll, 0f)) return;

        targetZoom -= scroll * zoomSpeed * Time.unscaledDeltaTime * 10f;
        targetZoom  = Mathf.Clamp(targetZoom, minZoom, maxZoom);
    }

    // ── Pan ────────────────────────────────────────────────────

    void HandlePan()
    {
        // Middle mouse button drag
        if (Mouse.current.middleButton.wasPressedThisFrame)
        {
            isPanning      = true;
            panOriginWorld = ScreenToWorld(Mouse.current.position.ReadValue());
        }

        if (Mouse.current.middleButton.wasReleasedThisFrame)
            isPanning = false;

        if (!isPanning) return;

        Vector3 currentWorld = ScreenToWorld(Mouse.current.position.ReadValue());
        Vector3 delta        = panOriginWorld - currentWorld;

        targetPosition = new Vector3(
            Mathf.Clamp(targetPosition.x + delta.x, minX, maxX),
            Mathf.Clamp(targetPosition.y + delta.y, minY, maxY),
            transform.position.z
        );

        // Re-anchor so delta doesn't accumulate
        panOriginWorld = ScreenToWorld(Mouse.current.position.ReadValue());
    }

    // ── Smoothing ──────────────────────────────────────────────

    void ApplySmoothing()
    {
        cam.orthographicSize      = Mathf.Lerp(cam.orthographicSize, targetZoom,
                                        zoomSmoothSpeed * Time.unscaledDeltaTime);
        transform.position        = Vector3.Lerp(transform.position, targetPosition,
                                        panSmoothSpeed * Time.unscaledDeltaTime);
    }

    // ── Helpers ────────────────────────────────────────────────

    Vector3 ScreenToWorld(Vector2 screenPos)
    {
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, 0f));
        world.z = 0f;
        return world;
    }

    // Draw pan bounds in Scene view for easy tuning
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0f);
        Vector3 size   = new Vector3(maxX - minX, maxY - minY, 0f);
        Gizmos.DrawWireCube(center, size);
    }
}