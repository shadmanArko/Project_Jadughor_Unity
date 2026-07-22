using System.Collections.Generic;
using ProjectMuseum.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Zenject;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// Wallpaper placement:
    ///
    ///  • Registers every child of the assigned wall containers into
    ///    <see cref="MuseumData.Walls"/> (id = "container/childIndex") and re-applies
    ///    each wall's saved wallpaper on start / save-load.
    ///  • A Wallpaper builder card arms wallpaper mode. LEFT-DRAG along the wall
    ///    line selects wall segments — only segments in the BACK wall containers
    ///    are selectable (fronts are ignored). Selected walls preview the
    ///    wallpaper sprite with a green tint, or red when the total cost
    ///    (price × selected count) isn't affordable.
    ///  • Releasing the drag applies the wallpaper to the selected walls (writes
    ///    data + deducts money); an unaffordable release just restores them.
    ///  • Right-click / Esc cancels wallpaper mode; picking a non-wallpaper card
    ///    disarms it too (one placement mode at a time).
    ///
    /// Selection maps each wall segment to its grid cell once at registration;
    /// a drag selects every back wall whose cell lies in the rectangle between
    /// the drag's start and current cells — same rectangle model as floor tiles.
    /// </summary>
    public class MuseumWallpaperSystem : MonoBehaviour
    {
        [Inject] private MuseumDataModel _model;
        [Inject] private BuilderDatabase _database;

        [Header("Scene walls")]
        [Tooltip("ALL wall containers (registered + saved), e.g. Left Walls, " +
                 "Right Walls, Bottom Left Walls, Bottom Right Walls.")]
        [SerializeField] private GameObject[] wallContainers = new GameObject[0];
        [Tooltip("The subset that can RECEIVE wallpaper — the two BACK wall " +
                 "containers (Left Walls, Right Walls). Front walls are never selectable.")]
        [SerializeField] private GameObject[] backWallContainers = new GameObject[0];

        [Header("Scene references")]
        [SerializeField] private Grid grid;

        [Header("Visuals")]
        [Tooltip("Pixels-per-unit for wallpaper sprites (match your wall art PPU).")]
        [SerializeField] private float wallpaperPixelsPerUnit = 100f;
        [SerializeField] private Color validTint = new Color(0.5f, 1f, 0.5f, 1f);
        [SerializeField] private Color invalidTint = new Color(1f, 0.4f, 0.4f, 1f);

        [Header("Selection overlay")]
        [Tooltip("Tile shown on the floor cells inside the drag selection (e.g. the " +
                 "'tile selection' tile from Floorings). Tinted green/red like the walls.")]
        [SerializeField] private TileBase selectionOverlayTile;
        [Tooltip("Floor tilemap — the overlay copies its sorting so it renders on top " +
                 "of the floor. Optional; without it the overlay uses default sorting.")]
        [SerializeField] private Tilemap floorTilemap;

        // wallId → segment renderer / original sprite / grid cell.
        private readonly Dictionary<string, SpriteRenderer> _wallRenderers = new();
        private readonly Dictionary<string, Sprite> _originalSprites = new();
        private readonly Dictionary<string, Vector3Int> _wallCells = new();
        private readonly HashSet<string> _selectableWalls = new();

        // Cached generated sprites, one per wallpaper name.
        private readonly Dictionary<string, Sprite> _wallpaperSprites = new();

        private Tilemap _overlayTilemap;

        // Wallpaper mode state
        private bool _modeActive;
        private string _pendingName;
        private BuilderDatabase.PlacementInfo _pendingInfo;
        private bool _dragging;
        private Vector3Int _dragStartCell;
        private readonly HashSet<string> _selection = new();
        private readonly List<string> _selectionScratch = new();

        private Camera _cam;

        // ── Lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            _cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (grid == null) grid = FindFirstObjectByType<Grid>();
        }

        private void OnEnable()
        {
            BuilderActions.OnClickBuilderCard += OnClickBuilderCard;
            BuilderActions.OnWallpaperChanged += OnWallpaperChanged;
            BuilderActions.OnMuseumDataReloaded += OnDataReloaded;
            BuilderActions.OnMuseumWallBuilt += OnWallBuilt;
            BuilderActions.OnMuseumWallRemoved += OnWallRemoved;
        }

        private void OnDisable()
        {
            BuilderActions.OnClickBuilderCard -= OnClickBuilderCard;
            BuilderActions.OnWallpaperChanged -= OnWallpaperChanged;
            BuilderActions.OnMuseumDataReloaded -= OnDataReloaded;
            BuilderActions.OnMuseumWallBuilt -= OnWallBuilt;
            BuilderActions.OnMuseumWallRemoved -= OnWallRemoved;
            CancelMode();
        }

        // Expansion adds/removes wall containers at runtime — keep the registry in sync.
        private void OnWallBuilt(GameObject container, bool isBackWall)
        {
            RegisterContainer(container, isBackWall);
            // Newly built walls start bare, but re-apply saved state in case this
            // container is being rebuilt (e.g. expansion replayed on load).
            Transform t = container.transform;
            for (int i = 0; i < t.childCount; i++) RestoreWall($"{container.name}/{i}");
        }

        private void OnWallRemoved(GameObject container)
        {
            if (container == null) return;
            Transform t = container.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                string id = $"{container.name}/{i}";
                _wallRenderers.Remove(id);
                _wallCells.Remove(id);
                _selectableWalls.Remove(id);
                _selection.Remove(id);
            }
        }

        private void Start()
        {
            _model.EnsureInitialized();
            SetupOverlayTilemap();
            RegisterSceneWalls();
            ReapplyAllFromData();
        }

        private void SetupOverlayTilemap()
        {
            var overlayObj = new GameObject("_WallpaperSelectionOverlay");
            overlayObj.transform.SetParent(grid.transform);
            _overlayTilemap = overlayObj.AddComponent<Tilemap>();
            var renderer = overlayObj.AddComponent<TilemapRenderer>();

            if (floorTilemap != null &&
                floorTilemap.TryGetComponent(out TilemapRenderer floorRenderer))
            {
                renderer.sortingLayerName = floorRenderer.sortingLayerName;
                renderer.sortingOrder = floorRenderer.sortingOrder + 1;
            }
        }

        private void OnDestroy()
        {
            if (_overlayTilemap != null)
                Destroy(_overlayTilemap.gameObject);
        }

        // ── Registration ────────────────────────────────────────────────

        /// <summary>Index every wall segment, cache its cell, ensure a data record.</summary>
        private void RegisterSceneWalls()
        {
            _wallRenderers.Clear();
            _wallCells.Clear();
            _selectableWalls.Clear();

            foreach (GameObject container in wallContainers)
                RegisterContainer(container, selectable: false);
            // Back containers may also appear in wallContainers — same ids, harmless.
            foreach (GameObject container in backWallContainers)
                RegisterContainer(container, selectable: true);

            Debug.Log($"[MuseumWallpaperSystem] Registered {_wallRenderers.Count} wall segment(s), " +
                      $"{_selectableWalls.Count} selectable (back walls).");
        }

        private void RegisterContainer(GameObject container, bool selectable)
        {
            if (container == null) return;
            Transform t = container.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                var sr = t.GetChild(i).GetComponentInChildren<SpriteRenderer>();
                if (sr == null) continue;

                string id = $"{container.name}/{i}";
                _wallRenderers[id] = sr;
                // Snapped to the nearest REAL museum tile — a wall sprite's own
                // position often lands on a cell just outside the floor records,
                // and each wall should answer to its corresponding edge tile.
                _wallCells[id] = ClampToMuseumTile(grid.WorldToCell(sr.transform.position));
                if (!_originalSprites.ContainsKey(id))
                    _originalSprites[id] = sr.sprite;
                if (selectable) _selectableWalls.Add(id);
                _model.EnsureWall(id);
            }
        }

        // ── Card click → arm/disarm wallpaper mode ──────────────────────

        private void OnClickBuilderCard(BuilderCardType type, string cardName)
        {
            if (type != BuilderCardType.Wallpaper)
            {
                CancelMode(); // another placement mode takes over
                return;
            }

            if (!_database.TryGetPlacementInfo(BuilderCardType.Wallpaper, cardName,
                    out BuilderDatabase.PlacementInfo info))
            {
                Debug.LogWarning($"[MuseumWallpaperSystem] Unknown wallpaper '{cardName}'.");
                return;
            }

            CancelMode(); // restore any half-done selection from a previous card
            _pendingName = cardName;
            _pendingInfo = info;
            _modeActive = true;
            BuilderActions.OnPlacementStarted?.Invoke(BuilderCardType.Wallpaper, cardName);
        }

        private void CancelMode()
        {
            RestoreSelectionPreview();
            ClearSelectionOverlay();
            _selection.Clear();
            _dragging = false;
            _modeActive = false;
            _pendingName = null;
        }

        // ── Drag selection ──────────────────────────────────────────────

        private void Update()
        {
            if (!_modeActive) return;

            Mouse mouse = Mouse.current;
            Keyboard kb = Keyboard.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame ||
                (kb != null && kb.escapeKey.wasPressedThisFrame))
            {
                CancelMode();
                BuilderActions.OnPlacementCancelled?.Invoke();
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi())
            {
                _dragging = true;
                _dragStartCell = SelectionCell(mouse);
            }

            if (_dragging && mouse.leftButton.isPressed)
                UpdateSelection(_dragStartCell, SelectionCell(mouse));

            if (_dragging && mouse.leftButton.wasReleasedThisFrame)
            {
                _dragging = false;
                CommitSelection();
            }
        }

        /// <summary>Selected = back walls whose cached cell is inside the drag rectangle.</summary>
        private void UpdateSelection(Vector3Int start, Vector3Int end)
        {
            int minX = Mathf.Min(start.x, end.x), maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y), maxY = Mathf.Max(start.y, end.y);

            // Drop walls that left the rectangle — restore their real look.
            _selectionScratch.Clear();
            foreach (string id in _selection)
            {
                Vector3Int c = _wallCells[id];
                if (c.x < minX || c.x > maxX || c.y < minY || c.y > maxY)
                    _selectionScratch.Add(id);
            }
            foreach (string id in _selectionScratch)
            {
                _selection.Remove(id);
                RestoreWall(id);
            }

            // Add walls that entered it.
            foreach (string id in _selectableWalls)
            {
                if (_selection.Contains(id)) continue;
                Vector3Int c = _wallCells[id];
                if (c.x >= minX && c.x <= maxX && c.y >= minY && c.y <= maxY)
                    _selection.Add(id);
            }

            // Preview: pending wallpaper sprite + affordability tint on everything selected.
            bool affordable = _model.CanAfford(_pendingInfo.Cost * _selection.Count);
            Color tint = affordable ? validTint : invalidTint;
            Sprite preview = GetWallpaperSprite(_pendingName);
            foreach (string id in _selection)
                if (_wallRenderers.TryGetValue(id, out SpriteRenderer sr) && sr != null)
                {
                    if (preview != null) sr.sprite = preview;
                    sr.color = tint;
                }

            DrawSelectionOverlay(tint);
        }

        /// <summary>Overlay tile on the edge tile of each SELECTED wall (only those).</summary>
        private void DrawSelectionOverlay(Color tint)
        {
            if (_overlayTilemap == null || selectionOverlayTile == null) return;

            _overlayTilemap.ClearAllTiles();
            foreach (string id in _selection)
            {
                Vector3Int cell = _wallCells[id];
                _overlayTilemap.SetTile(cell, selectionOverlayTile);
                _overlayTilemap.SetTileFlags(cell, TileFlags.None);
                _overlayTilemap.SetColor(cell, tint);
            }
        }

        private void ClearSelectionOverlay()
        {
            _overlayTilemap?.ClearAllTiles();
        }

        /// <summary>Apply on release: write data + pay, or restore if unaffordable.</summary>
        private void CommitSelection()
        {
            ClearSelectionOverlay();
            if (_selection.Count == 0) return;

            float total = _pendingInfo.Cost * _selection.Count;
            if (!_model.CanAfford(total))
            {
                Debug.Log($"[MuseumWallpaperSystem] Can't afford {_selection.Count} × " +
                          $"${_pendingInfo.Cost} = ${total}.");
                RestoreSelectionPreview();
                _selection.Clear();
                return;
            }

            foreach (string id in _selection)
            {
                _model.SetWallWallpaper(id, _pendingName); // raises OnWallpaperChanged → final sprite
                if (_wallRenderers.TryGetValue(id, out SpriteRenderer sr) && sr != null)
                    sr.color = Color.white; // drop the preview tint
            }
            _model.AddMoney(-total);
            _selection.Clear();
            // Mode stays armed for another drag; right-click/Esc to stop.
        }

        private void RestoreSelectionPreview()
        {
            foreach (string id in _selection)
                RestoreWall(id);
        }

        /// <summary>Put a wall back to its SAVED state (its recorded wallpaper, or bare).</summary>
        private void RestoreWall(string id)
        {
            if (!_wallRenderers.TryGetValue(id, out SpriteRenderer sr) || sr == null) return;

            string saved = "";
            foreach (WallData wall in _model.Walls)
                if (wall.Id == id) { saved = wall.WallpaperName; break; }

            ApplyToRenderer(sr, id, saved);
            sr.color = Color.white;
        }

        // ── Data → visuals ──────────────────────────────────────────────

        private void OnDataReloaded()
        {
            CancelMode();
            ReapplyAllFromData();
        }

        private void OnWallpaperChanged(string wallId, string wallpaperName)
        {
            if (!_wallRenderers.TryGetValue(wallId, out SpriteRenderer sr) || sr == null)
                return;
            ApplyToRenderer(sr, wallId, wallpaperName);
        }

        /// <summary>Re-apply every registered wall's saved wallpaper (used after load).</summary>
        private void ReapplyAllFromData()
        {
            foreach (WallData wall in _model.Walls)
                if (_wallRenderers.TryGetValue(wall.Id, out SpriteRenderer sr) && sr != null)
                {
                    ApplyToRenderer(sr, wall.Id, wall.WallpaperName);
                    sr.color = Color.white;
                }
        }

        private void ApplyToRenderer(SpriteRenderer sr, string wallId, string wallpaperName)
        {
            // Empty name = bare wall → restore the original sprite.
            if (string.IsNullOrEmpty(wallpaperName))
            {
                if (_originalSprites.TryGetValue(wallId, out Sprite original))
                    sr.sprite = original;
                return;
            }

            Sprite sprite = GetWallpaperSprite(wallpaperName);
            if (sprite != null) sr.sprite = sprite;
        }

        /// <summary>First frame of the wallpaper sheet at the wall PPU, cached per name.</summary>
        private Sprite GetWallpaperSprite(string wallpaperName)
        {
            if (string.IsNullOrEmpty(wallpaperName)) return null;
            if (_wallpaperSprites.TryGetValue(wallpaperName, out Sprite cached) && cached != null)
                return cached;

            if (!_database.TryGetPlacementInfo(BuilderCardType.Wallpaper, wallpaperName,
                    out BuilderDatabase.PlacementInfo info) || info.Texture == null)
            {
                Debug.LogWarning($"[MuseumWallpaperSystem] Wallpaper '{wallpaperName}' has no texture.");
                return null;
            }

            int frames = Mathf.Max(1, info.NumberOfFrames);
            var rect = new Rect(0f, 0f, info.Texture.width / (float)frames, info.Texture.height);
            var sprite = Sprite.Create(info.Texture, rect, new Vector2(0.5f, 0f), wallpaperPixelsPerUnit);
            _wallpaperSprites[wallpaperName] = sprite;
            return sprite;
        }

        // ── Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// The drag cell for the current pointer position. A wall sprite under the
        /// pointer WINS and contributes its own edge-tile cell directly — players
        /// drag on the walls themselves, and WorldToCell on a wall's upper area
        /// drifts diagonally (+x,+y) with height, which a nearest-tile snap then
        /// turns into a shift ALONG the wall (small near the back corner, growing
        /// with distance — the offset that was reported). Only pointers over no
        /// wall fall back to cell math + nearest-tile snapping.
        /// </summary>
        private Vector3Int SelectionCell(Mouse mouse)
        {
            Vector3 world = _cam.ScreenToWorldPoint(mouse.position.ReadValue());
            world.z = 0f;

            string bestWall = null;
            float bestSqr = float.MaxValue;
            foreach (string id in _selectableWalls)
            {
                if (!_wallRenderers.TryGetValue(id, out SpriteRenderer sr) || sr == null) continue;
                Bounds b = sr.bounds;
                if (world.x < b.min.x || world.x > b.max.x ||
                    world.y < b.min.y || world.y > b.max.y) continue;

                float sqr = ((Vector2)b.center - (Vector2)world).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestWall = id;
                }
            }
            if (bestWall != null) return _wallCells[bestWall];

            return ClampToMuseumTile(grid.WorldToCell(world));
        }

        /// <summary>
        /// Snap a cell to the nearest one that actually exists in the museum data.
        /// Players naturally start their drag ON the wall sprites, whose cells sit
        /// just outside the floor records — this maps those to the corresponding
        /// edge tile instead of silently selecting nothing.
        /// </summary>
        private Vector3Int ClampToMuseumTile(Vector3Int cell)
        {
            if (_model == null) return cell;
            if (_model.TryGetTile(new Vector2Int(cell.x, cell.y), out _)) return cell;

            int bestSqr = int.MaxValue;
            Vector3Int best = cell;
            foreach (MuseumTileData tile in _model.Tiles)
            {
                int dx = tile.X - cell.x;
                int dy = tile.Y - cell.y;
                int sqr = dx * dx + dy * dy;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = new Vector3Int(tile.X, tile.Y, 0);
                }
            }
            return best;
        }

        private static bool IsPointerOverUi() =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        // ── Testing helpers ─────────────────────────────────────────────

        [ContextMenu("Clear All Wallpapers")]
        private void ClearAllWallpapers()
        {
            _model.ClearAllWallpapers(); // raises OnWallpaperChanged per wall → restores originals
        }
    }
}
