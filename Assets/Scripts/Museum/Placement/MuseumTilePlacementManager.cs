using ProjectMuseum.Builder;
using ProjectMuseum.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using Zenject;

public class MuseumTilePlacementManager : MonoBehaviour
{
    // Painting is restricted to cells that exist in the museum data (developed
    // chunks only) — the forest/roads outside are not editable. Optional so the
    // manager still works (unrestricted) in a scene without the Zenject context.
    [Inject(Optional = true)] private MuseumDataModel _model;

    [Header("Tilemap References")]
    [SerializeField] private Tilemap placementTilemap;
    [SerializeField] private Grid grid;

    [Header("Tile Selection")]
    [SerializeField] private TileBase[] availableTiles;
    private TileBase selectedTile;
    private int selectedIndex = 0;

    /// <summary>
    /// Read-only view of the placeable tiles, so the builder panel can list them as
    /// Flooring cards without owning the tileset. Never null.
    /// </summary>
    public System.Collections.Generic.IReadOnlyList<TileBase> AvailableTiles =>
        availableTiles ?? System.Array.Empty<TileBase>();

    [Header("Placement Settings")]
    [SerializeField] private Color previewColor = new Color(1f, 1f, 1f, 0.5f);

    private Tilemap previewTilemap;
    private Camera mainCam;

    // Placement is only armed after picking a Flooring card in the builder panel —
    // idle otherwise, so ordinary clicks/drags on the museum never paint by accident.
    private bool placementActive;

    // Drag state
    private bool isDragging = false;
    private Vector3Int dragStartCell;
    private Vector3Int dragEndCell;
    private Vector3Int lastDragEndCell;

    void Start()
    {
        mainCam = Camera.main;

        if (grid == null)
            grid = FindFirstObjectByType<Grid>();

        SetupPreviewTilemap();
        _model?.EnsureInitialized(); // tile records must exist before the first paint
    }

    /// <summary>Only cells recorded in the museum data (developed chunks) are editable.</summary>
    bool IsPaintable(Vector3Int cell) =>
        _model == null || _model.TryGetTile(new Vector2Int(cell.x, cell.y), out _);

    void OnEnable()
    {
        BuilderActions.OnClickBuilderCard += OnBuilderCardClicked;
    }

    void OnDisable()
    {
        BuilderActions.OnClickBuilderCard -= OnBuilderCardClicked;
        DeactivatePlacement();
    }

    /// <summary>
    /// Flooring card click arms tile placement with that tile. Any OTHER category's
    /// card disarms it — the object/wallpaper systems take over from there.
    /// </summary>
    void OnBuilderCardClicked(BuilderCardType type, string cardName)
    {
        if (type != BuilderCardType.Flooring)
        {
            DeactivatePlacement();
            return;
        }

        for (int i = 0; i < AvailableTiles.Count; i++)
        {
            if (availableTiles[i] != null && availableTiles[i].name == cardName)
            {
                SelectTile(i);
                placementActive = true;
                return;
            }
        }
        Debug.LogWarning($"[TilePlacement] Flooring card '{cardName}' has no matching tile in Available Tiles.");
    }

    void DeactivatePlacement()
    {
        placementActive = false;
        isDragging = false;
        ClearPreview();
    }

    void Update()
    {
        if (!placementActive) return;

        Mouse mouse = Mouse.current;
        Keyboard kb = Keyboard.current;
        if (mouse == null) return;

        // ── Right click / Esc — cancel tile placement ────────────
        if (mouse.rightButton.wasPressedThisFrame ||
            (kb != null && kb.escapeKey.wasPressedThisFrame))
        {
            DeactivatePlacement();
            return;
        }

        Vector3Int currentCell = GetMouseCellPosition();

        // ── Drag start (not through UI — e.g. clicking another card) ──
        if (mouse.leftButton.wasPressedThisFrame && !IsPointerOverUi())
        {
            isDragging = true;
            dragStartCell = currentCell;
            dragEndCell = currentCell;
            lastDragEndCell = currentCell;
        }

        // ── Drag hold — update preview rectangle ────────────────
        if (isDragging && mouse.leftButton.isPressed)
        {
            if (currentCell != lastDragEndCell)
            {
                dragEndCell = currentCell;
                lastDragEndCell = currentCell;
                UpdateRectanglePreview(dragStartCell, dragEndCell);
            }
        }

        // ── Drag release — place all tiles in rectangle ─────────
        if (mouse.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            dragEndCell = currentCell;

            PlaceRectangle(dragStartCell, dragEndCell);
            ClearPreview();
        }
    }

    // ── Rectangle Placement ────────────────────────────────────

    void PlaceRectangle(Vector3Int start, Vector3Int end)
    {
        if (selectedTile == null || placementTilemap == null) return;

        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        int minY = Mathf.Min(start.y, end.y);
        int maxY = Mathf.Max(start.y, end.y);

        for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!IsPaintable(cell)) continue; // outside the museum — untouchable

                placementTilemap.SetTile(cell, selectedTile);
                // Museum data records the painted floor.
                BuilderActions.OnFloorTilePainted?.Invoke(
                    new Vector2Int(x, y), selectedTile.name);
            }
    }

    // Kept for a future dedicated remove/bulldoze tool — no longer bound to
    // right-click (that cancels placement now).
    void EraseTile(Vector3Int cellPos)
    {
        if (placementTilemap == null) return;
        placementTilemap.SetTile(cellPos, null);
    }

    // ── Preview ────────────────────────────────────────────────

    void SetupPreviewTilemap()
    {
        GameObject previewObj = new GameObject("_PlacementPreview");
        previewObj.transform.SetParent(grid.transform);
        previewTilemap = previewObj.AddComponent<Tilemap>();
        var renderer = previewObj.AddComponent<TilemapRenderer>();

        var sourceRenderer = placementTilemap.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName = sourceRenderer.sortingLayerName;
        renderer.sortingOrder = sourceRenderer.sortingOrder + 1;
    }

    void UpdateRectanglePreview(Vector3Int start, Vector3Int end)
    {
        if (previewTilemap == null || selectedTile == null) return;

        previewTilemap.ClearAllTiles();

        int minX = Mathf.Min(start.x, end.x);
        int maxX = Mathf.Max(start.x, end.x);
        int minY = Mathf.Min(start.y, end.y);
        int maxY = Mathf.Max(start.y, end.y);

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                if (!IsPaintable(cell)) continue; // preview matches what will actually paint

                previewTilemap.SetTile(cell, selectedTile);
                previewTilemap.SetTileFlags(cell, TileFlags.None);

                // Highlight corners distinctly
                bool isStart = (x == start.x && y == start.y);
                bool isEnd   = (x == end.x   && y == end.y);

                if (isStart)
                    previewTilemap.SetColor(cell, new Color(0f, 1f, 0f, 0.8f));   // green = origin
                else if (isEnd)
                    previewTilemap.SetColor(cell, new Color(1f, 0.3f, 0f, 0.8f)); // orange = end
                else
                    previewTilemap.SetColor(cell, previewColor);
            }
        }
    }

    void ClearPreview()
    {
        previewTilemap?.ClearAllTiles();
    }

    // ── Selection ──────────────────────────────────────────────

    public void SelectTile(int index)
    {
        if (availableTiles == null || availableTiles.Length == 0) return;
        selectedIndex = Mathf.Clamp(index, 0, availableTiles.Length - 1);
        selectedTile = availableTiles[selectedIndex];
        Debug.Log($"[TilePlacement] Selected tile: {selectedTile?.name ?? "None"} (index {selectedIndex})");
    }

    // ── Coordinate Conversion ──────────────────────────────────

    Vector3Int GetMouseCellPosition()
    {
        Vector3 worldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;
        return grid.WorldToCell(worldPos);
    }

    static bool IsPointerOverUi() =>
        EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    void OnDestroy()
    {
        if (previewTilemap != null)
            Destroy(previewTilemap.gameObject);
    }
}
