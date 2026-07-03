using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.InputSystem;

public class MuseumTilePlacementManager : MonoBehaviour
{
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
        SelectTile(0);
    }

    void Update()
    {
        Vector3Int currentCell = GetMouseCellPosition();

        // ── Drag start ──────────────────────────────────────────
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            isDragging = true;
            dragStartCell = currentCell;
            dragEndCell = currentCell;
            lastDragEndCell = currentCell;

            Debug.Log($"[TilePlacement] Drag START → Cell: {dragStartCell}");
        }

        // ── Drag hold — update preview rectangle ────────────────
        if (isDragging && Mouse.current.leftButton.isPressed)
        {
            if (currentCell != lastDragEndCell)
            {
                dragEndCell = currentCell;
                lastDragEndCell = currentCell;
                UpdateRectanglePreview(dragStartCell, dragEndCell);
            }
        }

        // ── Drag release — place all tiles in rectangle ─────────
        if (Mouse.current.leftButton.wasReleasedThisFrame && isDragging)
        {
            isDragging = false;
            dragEndCell = currentCell;

            Debug.Log($"[TilePlacement] Drag END   → Cell: {dragEndCell}");
            Debug.Log($"[TilePlacement] Rectangle  → From {dragStartCell} to {dragEndCell} " +
                      $"({Mathf.Abs(dragEndCell.x - dragStartCell.x) + 1}W x " +
                      $"{Mathf.Abs(dragEndCell.y - dragStartCell.y) + 1}H tiles)");

            PlaceRectangle(dragStartCell, dragEndCell);
            ClearPreview();
        }

        // ── Right click — erase single tile ─────────────────────
        if (Mouse.current.rightButton.wasPressedThisFrame)
            EraseTile(currentCell);

        // ── Scroll — cycle tiles ─────────────────────────────────
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0f) CycleTile(1);
        else if (scroll < 0f) CycleTile(-1);

        // ── Number keys 1–9 ──────────────────────────────────────
        for (int i = 0; i < Mathf.Min(availableTiles.Length, 9); i++)
        {
            if (Keyboard.current[Key.Digit1 + i].wasPressedThisFrame)
                SelectTile(i);
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
                placementTilemap.SetTile(new Vector3Int(x, y, 0), selectedTile);
    }

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

    void CycleTile(int direction)
    {
        if (availableTiles == null || availableTiles.Length == 0) return;
        SelectTile((selectedIndex + direction + availableTiles.Length) % availableTiles.Length);
    }

    // ── Coordinate Conversion ──────────────────────────────────

    Vector3Int GetMouseCellPosition()
    {
        Vector3 worldPos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        worldPos.z = 0f;
        return grid.WorldToCell(worldPos);
    }

    void OnDestroy()
    {
        if (previewTilemap != null)
            Destroy(previewTilemap.gameObject);
    }
}