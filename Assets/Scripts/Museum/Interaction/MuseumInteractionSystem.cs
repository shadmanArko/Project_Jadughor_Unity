using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Turns a left-click in the museum into an <see cref="IInteractable.Interact"/>
    /// call on the front-most placed object under the cursor. Uses sprite-bounds
    /// hit-testing (no colliders needed) and picks the highest-sortingOrder object,
    /// so clicking overlapping objects always hits the one drawn on top.
    ///
    /// Put on a manager object in the Museum scene. Ignores clicks while a
    /// placement/paint mode is active or the pointer is over UI, so building and
    /// interacting don't fight.
    /// </summary>
    public class MuseumInteractionSystem : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [Tooltip("When true, a click that hits nothing is logged (debugging).")]
        [SerializeField] private bool logMisses = false;

        private bool _suppressed;

        private void Awake()
        {
            if (cam == null) cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        private void OnEnable()
        {
            // While any build/paint mode is arming/active, don't also interact.
            BuilderActions.OnPlacementStarted += OnPlacementStarted;
            BuilderActions.OnPlacementCancelled += OnPlacementEnded;
            BuilderActions.OnObjectPlaced += _ => OnPlacementEnded();
        }

        private void OnDisable()
        {
            BuilderActions.OnPlacementStarted -= OnPlacementStarted;
            BuilderActions.OnPlacementCancelled -= OnPlacementEnded;
        }

        private void OnPlacementStarted(BuilderCardType type, string cardName) => _suppressed = true;
        private void OnPlacementEnded() => _suppressed = false;

        private void Update()
        {
            if (_suppressed) return;
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (IsPointerOverUi()) return;

            Vector3 world = cam.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;

            PlaceableObjectView best = null;
            int bestOrder = int.MinValue;
            foreach (PlaceableObjectView view in
                     FindObjectsByType<PlaceableObjectView>(FindObjectsSortMode.None))
            {
                if (!view.IsPlaced || !view.ContainsWorldPoint(world)) continue;
                if (view.SortingOrder > bestOrder)
                {
                    bestOrder = view.SortingOrder;
                    best = view;
                }
            }

            if (best != null) best.Interact();
            else if (logMisses) Debug.Log($"[MuseumInteractionSystem] Click at {world} hit no object.");
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
