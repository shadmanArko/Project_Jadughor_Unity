using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace ProjectMuseum.Town
{
    /// <summary>
    /// The window onto the town world: sits on the map RawImage that shows the town camera's
    /// RenderTexture (Godot equivalent: the town scene shown inside the UI panel, with
    /// <c>SpritePanAndZoom</c> on top).
    ///
    /// - Pan/zoom move the town camera, clamped so it never shows past the ground sprite.
    ///   Mouse: wheel zooms toward the cursor, drag (any button) pans.
    ///   Gamepad: left stick pans, RT/LT zoom.
    /// - Hover/click turn the pointer into a town-world point and hit-test the buildings'
    ///   PolygonCollider2Ds; the front-most sprite wins.
    /// </summary>
    [RequireComponent(typeof(RawImage))]
    public class TownMapView : MonoBehaviour,
        IScrollHandler, IBeginDragHandler, IDragHandler,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [Header("Town world")]
        [SerializeField] private Camera townCamera;
        [Tooltip("The town's base map. Its bounds are the pan limits and zoom-1 framing.")]
        [SerializeField] private SpriteRenderer ground;

        [Header("Zoom")]
        [SerializeField] private float minZoom = 1f;
        [SerializeField] private float maxZoom = 3f;
        [Tooltip("Zoom change per mouse-wheel notch.")]
        [SerializeField] private float zoomStep = 0.5f;
        [SerializeField] private float gamepadZoomSpeed = 2f;
        [Tooltip("Fraction of the visible map panned per second at full stick.")]
        [SerializeField] private float gamepadPanSpeed = 0.8f;

        private static readonly Collider2D[] Hits = new Collider2D[16];
        private static readonly ContactFilter2D AllColliders = ContactFilter2D.noFilter; // includes triggers

        private RectTransform _rect;
        private float _zoom = 1f;
        private bool _pointerInside;
        private TownBuilding _hovered;

        public Camera TownCamera => townCamera;

        private void Awake()
        {
            _rect = (RectTransform)transform;
        }

        private void OnDisable()
        {
            SetHovered(null);
            _pointerInside = false;
        }

        /// <summary>Zoom 1, centred on the ground — called each time the map opens.</summary>
        public void ResetView()
        {
            if (townCamera == null || ground == null) return;
            _zoom = minZoom;
            Vector3 c = ground.bounds.center;
            townCamera.transform.position = new Vector3(c.x, c.y, townCamera.transform.position.z);
            ApplyZoom();
        }

        // ── Pointer ─────────────────────────────────────────────────────

        public void OnPointerEnter(PointerEventData eventData) => _pointerInside = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            SetHovered(null);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (TryGetWorldPoint(eventData.position, out Vector2 world))
                BuildingAt(world)?.Click();
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (Mathf.Approximately(eventData.scrollDelta.y, 0f)) return;
            float notch = Mathf.Sign(eventData.scrollDelta.y);

            // Keep the world point under the cursor fixed while zooming.
            bool hasFocus = TryGetWorldPoint(eventData.position, out Vector2 before);
            _zoom = Mathf.Clamp(_zoom + notch * zoomStep, minZoom, maxZoom);
            ApplyZoom();
            if (hasFocus && TryGetWorldPoint(eventData.position, out Vector2 after))
                MoveCamera(before - after);
        }

        // Required so OnDrag fires.
        public void OnBeginDrag(PointerEventData eventData) { }

        public void OnDrag(PointerEventData eventData)
        {
            // Screen pixels → fraction of the image → world units the camera currently shows.
            Vector2 imageSize = ScreenSize();
            if (imageSize.x <= 0f) return;
            float worldHeight = townCamera.orthographicSize * 2f;
            Vector2 worldPerPixel = new Vector2(worldHeight * townCamera.aspect / imageSize.x, worldHeight / imageSize.y);
            MoveCamera(-Vector2.Scale(eventData.delta, worldPerPixel));
        }

        // ── Per frame ───────────────────────────────────────────────────

        private void Update()
        {
            if (townCamera == null) return;

            // Hover: the camera may move under a still cursor, so poll rather than wait for events.
            Mouse mouse = Mouse.current;
            if (_pointerInside && mouse != null && TryGetWorldPoint(mouse.position.ReadValue(), out Vector2 world))
                SetHovered(BuildingAt(world));

            Gamepad pad = Gamepad.current;
            if (pad == null) return;

            float dt = Time.unscaledDeltaTime;
            Vector2 stick = pad.leftStick.ReadValue();
            if (stick.sqrMagnitude > 0.02f)
                MoveCamera(stick * (townCamera.orthographicSize * 2f * gamepadPanSpeed * dt));

            float zoomInput = pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue();
            if (Mathf.Abs(zoomInput) > 0.05f)
            {
                _zoom = Mathf.Clamp(_zoom + zoomInput * gamepadZoomSpeed * dt, minZoom, maxZoom);
                ApplyZoom();
            }
        }

        // ── Camera ──────────────────────────────────────────────────────

        private void ApplyZoom()
        {
            townCamera.orthographicSize = ground.bounds.extents.y / _zoom;
            MoveCamera(Vector2.zero); // re-clamp at the new size
        }

        private void MoveCamera(Vector2 delta)
        {
            Bounds b = ground.bounds;
            float halfH = townCamera.orthographicSize;
            float halfW = halfH * townCamera.aspect;
            Vector3 p = townCamera.transform.position + (Vector3)delta;

            p.x = b.extents.x > halfW ? Mathf.Clamp(p.x, b.min.x + halfW, b.max.x - halfW) : b.center.x;
            p.y = b.extents.y > halfH ? Mathf.Clamp(p.y, b.min.y + halfH, b.max.y - halfH) : b.center.y;
            townCamera.transform.position = p;
        }

        // ── Hit-testing ─────────────────────────────────────────────────

        private bool TryGetWorldPoint(Vector2 screen, out Vector2 world)
        {
            world = default;
            if (townCamera == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, screen, EventCamera(), out Vector2 local))
                return false;

            Rect r = _rect.rect;
            if (!r.Contains(local)) return false;

            Vector2 uv = Rect.PointToNormalized(r, local);
            world = townCamera.ViewportToWorldPoint(new Vector3(uv.x, uv.y, 0f));
            return true;
        }

        private static TownBuilding BuildingAt(Vector2 world)
        {
            int count = Physics2D.OverlapPoint(world, AllColliders, Hits);
            TownBuilding best = null;
            for (int i = 0; i < count; i++)
            {
                if (!Hits[i].TryGetComponent(out TownBuilding building)) continue;
                if (best == null || building.SortingOrder > best.SortingOrder) best = building;
            }
            return best;
        }

        private void SetHovered(TownBuilding building)
        {
            if (building == _hovered) return;
            if (_hovered != null) _hovered.SetHovered(false);
            _hovered = building;
            if (_hovered != null) _hovered.SetHovered(true);
        }

        private Camera EventCamera()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private Vector2 ScreenSize()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.rootCanvas.scaleFactor : 1f;
            return _rect.rect.size * scale;
        }
    }
}
