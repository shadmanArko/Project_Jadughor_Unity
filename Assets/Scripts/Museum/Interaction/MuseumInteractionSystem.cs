using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Turns a left-click in the museum into an <see cref="IInteractable.Interact"/>
    /// call on the front-most placed object under the cursor. Uses sprite-bounds
    /// hit-testing — NO colliders needed — and picks the highest-sortingOrder object,
    /// so clicking overlapping objects always hits the one drawn on top.
    ///
    /// Put on a manager object in the Museum scene. Ignores clicks over UI and while a
    /// placement mode is active (so building and interacting don't fight — right-click
    /// / Esc to leave placement first, THEN click objects to interact).
    /// </summary>
    public class MuseumInteractionSystem : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [Tooltip("Log every click and why it did/didn't interact — turn on to diagnose, off for release.")]
        [SerializeField] private bool logClicks = true;

        private bool _placing;

        private void Awake()
        {
            if (cam == null) cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        private void OnEnable()
        {
            // Suppressed for the whole duration of a placement session — set on start,
            // cleared only when placement is cancelled (placing continues after each
            // individual OnObjectPlaced, so that event must NOT clear it).
            BuilderActions.OnPlacementStarted += OnPlacementStarted;
            BuilderActions.OnPlacementCancelled += OnPlacementCancelled;
        }

        private void OnDisable()
        {
            BuilderActions.OnPlacementStarted -= OnPlacementStarted;
            BuilderActions.OnPlacementCancelled -= OnPlacementCancelled;
        }

        private void OnPlacementStarted(BuilderCardType type, string cardName) => _placing = true;
        private void OnPlacementCancelled() => _placing = false;

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            if (_placing) { Log("ignored — a placement mode is active (right-click to exit first)."); return; }
            if (IsPointerOverUi()) { Log("ignored — pointer is over UI."); return; }
            if (cam == null) { Debug.LogError("[MuseumInteractionSystem] No camera.", this); return; }

            Vector3 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;

            PlaceableObjectView best = null;
            int bestOrder = int.MinValue;
            int scanned = 0;
            foreach (PlaceableObjectView view in
                     FindObjectsByType<PlaceableObjectView>(FindObjectsSortMode.None))
            {
                scanned++;
                if (!view.IsPlaced || !view.ContainsWorldPoint(world)) continue;
                if (view.SortingOrder > bestOrder)
                {
                    bestOrder = view.SortingOrder;
                    best = view;
                }
            }

            if (best != null)
            {
                Log($"hit '{best.name}' ({best.GetType().Name}) at {world} → Interact().");
                best.Interact();
            }
            else
            {
                Log($"no placed object under {world} (scanned {scanned} view(s)).");
            }
        }

        private void Log(string msg)
        {
            if (logClicks) Debug.Log($"[MuseumInteractionSystem] Click {msg}");
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
