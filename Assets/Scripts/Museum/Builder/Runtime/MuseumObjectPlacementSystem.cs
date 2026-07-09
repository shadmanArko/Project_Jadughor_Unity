using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Zenject;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Places museum objects (Exhibit / DecorationShop / DecorationOther /
    /// Sanitation) in the world and keeps the visuals in sync with
    /// <see cref="MuseumDataModel"/>:
    ///
    ///  • Builder card click → a ghost sprite follows the mouse, snapped to the
    ///    grid, tinted green/red for valid/invalid (validity + funds from the model).
    ///  • Left-click places (writes the data, spawns the real object).
    ///  • Right-click / Esc cancels.
    ///  • On Start, every object already in the data (loaded save) is respawned.
    ///
    /// Scene setup: put this on a manager object in the Museum scene (which must
    /// have a SceneContext with the MuseumInstaller), assign the Grid and an
    /// objects parent. Flooring/Wallpaper card clicks are ignored here — flooring
    /// is the tile manager's job, wallpaper comes later.
    /// </summary>
    public class MuseumObjectPlacementSystem : MonoBehaviour
    {
        [Inject] private MuseumDataModel _model;
        [Inject] private BuilderDatabase _database;

        [Header("Scene references")]
        [SerializeField] private Grid grid;
        [Tooltip("Parent for spawned museum objects (keeps hierarchy tidy).")]
        [SerializeField] private Transform objectsParent;

        [Header("Visuals")]
        [Tooltip("Pixels-per-unit used when building world sprites from the sheet textures.")]
        [SerializeField] private float worldPixelsPerUnit = 100f;
        [Tooltip("Extra Y offset applied to spawned sprites so their feet sit on the tile.")]
        [SerializeField] private float spriteYOffset = 0f;
        [SerializeField] private Color validTint = new Color(0.5f, 1f, 0.5f, 0.6f);
        [SerializeField] private Color invalidTint = new Color(1f, 0.4f, 0.4f, 0.6f);
        [Tooltip("Sorting layer name for placed object sprites (empty = Default).")]
        [SerializeField] private string sortingLayerName = "";

        private Camera _cam;

        // Ghost/pending placement state
        private bool _isPlacing;
        private BuilderCardType _pendingType;
        private string _pendingName;
        private BuilderDatabase.PlacementInfo _pendingInfo;
        private SpriteRenderer _ghost;

        // Spawned visuals by PlacedObjectData.Id (for removal support)
        private readonly Dictionary<string, GameObject> _spawned = new();

        private static readonly BuilderCardType[] PlaceableTypes =
        {
            BuilderCardType.Exhibit, BuilderCardType.DecorationShop,
            BuilderCardType.DecorationOther, BuilderCardType.Sanitation
        };

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _cam = Camera.main;
            if (grid == null) grid = FindFirstObjectByType<Grid>();
            if (objectsParent == null) objectsParent = transform;
        }

        private void OnEnable()
        {
            BuilderActions.OnClickBuilderCard += OnClickBuilderCard;
            BuilderActions.OnObjectRemoved += OnObjectRemoved;
            BuilderActions.OnMuseumDataReloaded += OnDataReloaded;
        }

        private void OnDisable()
        {
            BuilderActions.OnClickBuilderCard -= OnClickBuilderCard;
            BuilderActions.OnObjectRemoved -= OnObjectRemoved;
            BuilderActions.OnMuseumDataReloaded -= OnDataReloaded;
            CancelPlacement();
        }

        /// <summary>Data was replaced (save load / new game) — rebuild all visuals.</summary>
        private void OnDataReloaded()
        {
            CancelPlacement();
            ClearAllSpawned();
            SpawnLoadedObjects();
        }

        private void ClearAllSpawned()
        {
            foreach (GameObject go in _spawned.Values)
                if (go != null) Destroy(go);
            _spawned.Clear();
        }

        private void Start()
        {
            _model.EnsureInitialized(); // guard against Start-vs-IInitializable order races
            SpawnLoadedObjects();
        }

        /// <summary>Respawn visuals for every object already in the data (save load).</summary>
        private void SpawnLoadedObjects()
        {
            int count = 0;
            foreach (PlacedObjectData placed in _model.PlacedObjects)
            {
                if (SpawnVisual(placed) != null) count++;
            }
            if (count > 0)
                Debug.Log($"[MuseumObjectPlacementSystem] Respawned {count} saved object(s).");
        }

        // ── Card click → start placing ──────────────────────────────────

        private void OnClickBuilderCard(BuilderCardType type, string cardName)
        {
            if (System.Array.IndexOf(PlaceableTypes, type) < 0) return; // flooring/wallpaper: not ours

            if (!_database.TryGetPlacementInfo(type, cardName, out BuilderDatabase.PlacementInfo info))
            {
                Debug.LogWarning($"[MuseumObjectPlacementSystem] Unknown variation '{cardName}' ({type}).");
                return;
            }

            CancelPlacement(); // replace any pending ghost

            _pendingType = type;
            _pendingName = cardName;
            _pendingInfo = info;
            _isPlacing = true;

            var go = new GameObject($"Ghost_{cardName}");
            go.transform.SetParent(transform, false);
            _ghost = go.AddComponent<SpriteRenderer>();
            _ghost.sprite = BuildWorldSprite(info);
            if (!string.IsNullOrEmpty(sortingLayerName)) _ghost.sortingLayerName = sortingLayerName;
            _ghost.sortingOrder = short.MaxValue; // ghost always on top

            BuilderActions.OnPlacementStarted?.Invoke(type, cardName);
        }

        // ── Ghost follow + place/cancel ─────────────────────────────────

        private void Update()
        {
            if (!_isPlacing || _ghost == null) return;

            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // Cancel
            if (mouse.rightButton.wasPressedThisFrame ||
                (kb != null && kb.escapeKey.wasPressedThisFrame))
            {
                CancelPlacement();
                BuilderActions.OnPlacementCancelled?.Invoke();
                return;
            }

            // Snap ghost to the hovered cell
            Vector2Int anchor = MouseCell(mouse);
            _ghost.transform.position = CellToWorld(anchor);

            bool valid = _model.CanPlace(anchor, _pendingInfo.WidthInTiles, _pendingInfo.LengthInTiles)
                         && _model.CanAfford(_pendingInfo.Cost);
            _ghost.color = valid ? validTint : invalidTint;

            // Place (ignore clicks over UI so the builder panel stays usable)
            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi() && valid)
                TryPlaceAt(anchor);
        }

        private void TryPlaceAt(Vector2Int anchor)
        {
            PlacedObjectData placed = _model.PlaceObject(
                _pendingType, _pendingName, anchor,
                _pendingInfo.WidthInTiles, _pendingInfo.LengthInTiles, _pendingInfo.Cost);
            if (placed == null) return;

            SpawnVisual(placed);

            // Keep placing more of the same object while funds/space allow
            // (matches typical builder flow; right-click to stop).
        }

        private void CancelPlacement()
        {
            _isPlacing = false;
            if (_ghost != null)
            {
                Destroy(_ghost.gameObject);
                _ghost = null;
            }
        }

        private void OnObjectRemoved(PlacedObjectData placed)
        {
            if (_spawned.TryGetValue(placed.Id, out GameObject go) && go != null)
                Destroy(go);
            _spawned.Remove(placed.Id);
        }

        // ── Visual spawning ─────────────────────────────────────────────

        private GameObject SpawnVisual(PlacedObjectData placed)
        {
            if (!_database.TryGetPlacementInfo(placed.Type, placed.VariationName,
                    out BuilderDatabase.PlacementInfo info))
            {
                Debug.LogWarning($"[MuseumObjectPlacementSystem] No variation data for saved " +
                                 $"object '{placed.VariationName}' — skipped.");
                return null;
            }

            var go = new GameObject($"{placed.Type}_{placed.VariationName}");
            go.transform.SetParent(objectsParent, false);
            go.transform.position = CellToWorld(placed.AnchorCell);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = BuildWorldSprite(info);
            if (!string.IsNullOrEmpty(sortingLayerName)) sr.sortingLayerName = sortingLayerName;

            go.AddComponent<YSortable>(); // isometric depth from Y, same as the rest of the museum

            _spawned[placed.Id] = go;
            return go;
        }

        private Sprite BuildWorldSprite(BuilderDatabase.PlacementInfo info)
        {
            if (info.Texture == null) return null;
            int frames = Mathf.Max(1, info.NumberOfFrames);
            float w = info.Texture.width / (float)frames;
            var rect = new Rect(0f, 0f, w, info.Texture.height);
            // Bottom-center pivot so the sprite stands on its anchor tile.
            return Sprite.Create(info.Texture, rect, new Vector2(0.5f, 0f), worldPixelsPerUnit);
        }

        // ── Helpers ─────────────────────────────────────────────────────

        private Vector2Int MouseCell(Mouse mouse)
        {
            Vector3 world = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;
            Vector3Int cell = grid.WorldToCell(world);
            return new Vector2Int(cell.x, cell.y);
        }

        private Vector3 CellToWorld(Vector2Int cell)
        {
            Vector3 pos = grid.GetCellCenterWorld(new Vector3Int(cell.x, cell.y, 0));
            pos.z = 0f;
            pos.y += spriteYOffset;
            return pos;
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }
}
