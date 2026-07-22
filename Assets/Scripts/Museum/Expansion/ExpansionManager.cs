using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Manages museum expansion. The museum lives on a lattice of equally-sized
/// chunks. The origin chunk (0,0) is the pre-built museum already in the scene.
/// Each new chunk is the same cell size and is positioned by stepping along the
/// two isometric grid axes.
///
/// Edges (looking at the iso diamond):
///   NE / NW = the two BACK edges (face the forest)  → back walls
///   SW / SE = the two FRONT edges (face the road)   → front walls
///
/// When two chunks meet, the shared wall is torn down and the floors join
/// seamlessly (single fill tile, so the seam is invisible).
/// </summary>
public class ExpansionManager : MonoBehaviour
{
    public enum Edge { NE, NW, SW, SE }

    [Header("Grid / Floor")]
    [SerializeField] private Grid grid;
    [SerializeField] private Tilemap floorTilemap;
    [Tooltip("ON: copy the exact tile already used by the base floor (same reference → " +
             "same height/look). OFF: use the Floor Tile below.")]
    [SerializeField] private bool copyBaseFloorTile = true;
    [Tooltip("Cell to sample the base floor tile from (used when Copy Base Floor Tile is ON).")]
    [SerializeField] private Vector2Int floorSampleCell = new Vector2Int(10, 9);
    [Tooltip("Fallback fill tile (used when Copy Base Floor Tile is OFF or sampling finds nothing).")]
    [SerializeField] private TileBase floorTile;
    [Tooltip("Chunk size in cells (X, Y). Base chunk is (0,0)..(size-1).")]
    [SerializeField] private Vector2Int chunkSize = new Vector2Int(20, 18);

    [Header("Base chunk walls (drag the 4 wall containers — ORDER DOESN'T MATTER)")]
    [Tooltip("The four existing wall containers (Left/Right/Bottom Left/Bottom Right " +
             "Walls). Each is auto-classified to an edge by its position, so it can't " +
             "be mis-slotted.")]
    [SerializeField] private GameObject[] baseWalls = new GameObject[4];

    [Header("Wall segment prefabs (each has a Wall script with first/middle/corner sprites)")]
    [Tooltip("Back wall segment — used on the two BACK edges (NW & NE).")]
    [SerializeField] private GameObject backWallPrefab;
    [Tooltip("Front wall segment — used on the two FRONT/road edges (SW & SE).")]
    [SerializeField] private GameObject frontWallPrefab;
    [Tooltip("Mirror (flip X) the right-side edges (NE & SE). The prefabs are drawn " +
             "for the LEFT side; the right side is their mirror image. Flip this if " +
             "your art is drawn the other way round.")]
    [SerializeField] private bool mirrorRightSideWalls = true;
    [Tooltip("Optional extra nudge on the wall start position (normally 0 — the layout " +
             "is taken from the existing base walls).")]
    [SerializeField] private Vector2 wallOffset = Vector2.zero;
    [Tooltip("Optional parent for spawned wall objects (keeps the hierarchy tidy).")]
    [SerializeField] private Transform wallsParent;

    // Record of every developed chunk and its 4 perimeter wall objects.
    // A null/absent entry for an edge means that edge is open (a seam).
    private class ChunkData { public readonly Dictionary<Edge, GameObject> walls = new(); }
    private readonly Dictionary<Vector2Int, ChunkData> _chunks = new();
    private Dictionary<Edge, GameObject> _baseWalls;

    void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<Grid>();

        _baseWalls = ClassifyBaseWalls();

        // Seed the origin chunk with its pre-built (classified) walls.
        var baseChunk = new ChunkData();
        foreach (var kv in _baseWalls)
            baseChunk.walls[kv.Key] = kv.Value;
        _chunks[Vector2Int.zero] = baseChunk;
    }

    /// <summary>
    /// Work out which edge each base wall container sits on, purely from its
    /// position relative to the base chunk centre. Removes any chance of
    /// mis-assigning the NE/NW/SW/SE slots in the Inspector.
    /// </summary>
    Dictionary<Edge, GameObject> ClassifyBaseWalls()
    {
        var map = new Dictionary<Edge, GameObject>();
        if (grid == null || baseWalls == null) return map;

        Vector3 center = BaseCenter();
        foreach (var go in baseWalls)
        {
            if (go == null) continue;
            Vector3 c = WallCentroid(go);
            bool up = c.y > center.y;
            bool right = c.x > center.x;
            Edge e = up ? (right ? Edge.NE : Edge.NW)
                        : (right ? Edge.SE : Edge.SW);
            if (map.ContainsKey(e))
                Debug.LogWarning($"[ExpansionManager] Two base walls classify as {e}: " +
                                 $"'{map[e].name}' and '{go.name}'. Check the wall list.", go);
            map[e] = go;
        }
        return map;
    }

    Vector3 BaseCenter()
    {
        Vector3 a = grid.CellToWorld(Vector3Int.zero);
        Vector3 b = grid.CellToWorld(new Vector3Int(chunkSize.x, 0, 0));
        Vector3 c = grid.CellToWorld(new Vector3Int(chunkSize.x, chunkSize.y, 0));
        Vector3 d = grid.CellToWorld(new Vector3Int(0, chunkSize.y, 0));
        return (a + b + c + d) * 0.25f;
    }

    static Vector3 WallCentroid(GameObject go)
    {
        Transform t = go.transform;
        if (t.childCount == 0) return t.position;
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < t.childCount; i++) sum += t.GetChild(i).position;
        return sum / t.childCount;
    }

    // ── Public API ─────────────────────────────────────────────

    public bool IsDeveloped(Vector2Int chunk) => _chunks.ContainsKey(chunk);

    /// <summary>
    /// Can this chunk be expanded right now? It must be orthogonally adjacent to
    /// an already-developed chunk. This single rule also enforces "the TOP
    /// diagonal chunk needs a LEFT or RIGHT neighbour first" — the diagonal chunk
    /// is not orthogonally adjacent to the origin, only to the side chunks.
    /// </summary>
    public bool CanExpand(Vector2Int chunk)
    {
        if (_chunks.ContainsKey(chunk)) return false;
        return HasDevelopedNeighbour(chunk);
    }

    bool HasDevelopedNeighbour(Vector2Int chunk)
    {
        foreach (var kv in EdgeDirs)
            if (_chunks.ContainsKey(chunk + kv.Value)) return true;
        return false;
    }

    /// <summary>
    /// Which chunk a world position falls in, by inverting the two lattice axes
    /// about the base chunk centre. Lets a forest plot figure out its own
    /// coordinate from where it sits — no manual tagging, works for any number of
    /// chunks all around the museum.
    /// </summary>
    public Vector2Int WorldToChunk(Vector3 worldPos)
    {
        Vector3 ax = AxisX();
        Vector3 ay = AxisY();
        Vector3 rel = worldPos - BaseCenter();

        float det = ax.x * ay.y - ax.y * ay.x;
        if (Mathf.Abs(det) < 1e-5f) return Vector2Int.zero;

        float cx = (rel.x * ay.y - rel.y * ay.x) / det;
        float cy = (ax.x * rel.y - ax.y * rel.x) / det;
        return new Vector2Int(Mathf.RoundToInt(cx), Mathf.RoundToInt(cy));
    }

    /// <summary>
    /// Develop a chunk: paint its floor, raise walls on every open edge, and
    /// tear down the shared wall on any edge that meets a developed neighbour.
    /// Returns false if the chunk can't be expanded yet.
    /// </summary>
    public bool TryExpand(Vector2Int chunk)
    {
        if (_chunks.ContainsKey(chunk))
        {
            Debug.Log($"[ExpansionManager] Chunk {chunk} is already developed.");
            return false;
        }
        if (!HasDevelopedNeighbour(chunk))
        {
            Debug.Log($"[ExpansionManager] Chunk {chunk} can't expand yet — no developed " +
                      $"chunk next to it. Develop an adjacent chunk first.");
            return false;
        }

        PaintFloor(chunk);

        // Seed this chunk's tile records BEFORE building/announcing its walls — the
        // wallpaper system snaps each new wall to its nearest museum tile at register
        // time, so those tiles must already exist or it snaps back to the old chunk.
        ProjectMuseum.Builder.BuilderActions.OnMuseumChunkExpanded?.Invoke(chunk);

        var data = new ChunkData();
        foreach (var kv in EdgeDirs)
        {
            Edge edge = kv.Key;
            Vector2Int neighbour = chunk + kv.Value;

            if (_chunks.TryGetValue(neighbour, out var nb))
            {
                // Shared edge: remove the neighbour's facing wall, build nothing here.
                Edge opp = Opposite(edge);
                if (nb.walls.TryGetValue(opp, out var w) && w != null)
                {
                    ProjectMuseum.Builder.BuilderActions.OnMuseumWallRemoved?.Invoke(w);
                    w.SetActive(false);
                    nb.walls[opp] = null;
                }
            }
            else
            {
                GameObject wall = BuildWall(chunk, edge);
                if (wall != null)
                {
                    data.walls[edge] = wall;
                    bool isBack = edge == Edge.NW || edge == Edge.NE;
                    ProjectMuseum.Builder.BuilderActions.OnMuseumWallBuilt?.Invoke(wall, isBack);
                }
            }
        }

        _chunks[chunk] = data;
        return true;
    }

    // ── Build helpers ──────────────────────────────────────────

    void PaintFloor(Vector2Int chunk)
    {
        if (floorTilemap == null)
        {
            Debug.LogWarning("[ExpansionManager] floorTilemap not assigned.");
            return;
        }

        // Use the SAME tile the base floor already uses, so the new tiles render at
        // the exact same height/look (a different tile asset can have a different pivot).
        TileBase tile = floorTile;
        if (copyBaseFloorTile)
        {
            var sample = floorTilemap.GetTile(new Vector3Int(floorSampleCell.x, floorSampleCell.y, 0));
            if (sample != null) tile = sample;
        }
        if (tile == null)
        {
            Debug.LogWarning("[ExpansionManager] No floor tile (sample empty and no fallback assigned).");
            return;
        }

        int x0 = chunk.x * chunkSize.x;
        int y0 = chunk.y * chunkSize.y;
        for (int x = 0; x < chunkSize.x; x++)
            for (int y = 0; y < chunkSize.y; y++)
                floorTilemap.SetTile(new Vector3Int(x0 + x, y0 + y, 0), tile);
    }

    /// <summary>
    /// Lay a line of individual wall segments along an edge. Start point, step and
    /// count are derived purely from the isometric grid + chunk size, so it needs
    /// no hand-placed reference walls. The back/front prefab is mirrored for the
    /// right-side edges, and each segment's sprite is chosen by its position:
    /// the first segment, the repeated middles, and the final corner.
    /// </summary>
    GameObject BuildWall(Vector2Int chunk, Edge edge)
    {
        GameObject segPrefab = (edge == Edge.NW || edge == Edge.NE)
            ? backWallPrefab
            : frontWallPrefab;
        if (segPrefab == null) return null;

        EdgeGeometry(chunk, edge, out Vector3 start, out Vector3 step, out int count);
        if (count <= 0) return null;

        bool mirror = mirrorRightSideWalls && (edge == Edge.NE || edge == Edge.SE);

        // One container per edge so the whole line can be toggled as a unit (seams).
        var container = new GameObject($"Wall {edge} [{chunk.x},{chunk.y}]");
        container.transform.SetParent(wallsParent, false);

        for (int i = 0; i < count; i++)
        {
            Vector3 pos = start + step * i;
            GameObject seg = Instantiate(segPrefab, pos, segPrefab.transform.rotation, container.transform);

            if (mirror)
            {
                Vector3 s = seg.transform.localScale;
                seg.transform.localScale = new Vector3(-s.x, s.y, s.z);
            }

            var wall = seg.GetComponent<Wall>();
            if (wall != null)
            {
                Wall.Piece piece = i == 0 ? Wall.Piece.First
                                 : i == count - 1 ? Wall.Piece.Corner
                                 : Wall.Piece.Middle;
                wall.SetPiece(piece);
            }
        }

        return container;
    }

    /// <summary>
    /// Where an edge's wall line starts, the per-segment step, and how many
    /// segments — read directly from the matching base wall object so new walls
    /// line up exactly with the hand-placed museum, then translated one chunk.
    /// </summary>
    void EdgeGeometry(Vector2Int chunk, Edge edge, out Vector3 start, out Vector3 step, out int count)
    {
        start = Vector3.zero;
        step = Vector3.zero;
        count = 0;

        GameObject baseRef = BaseWallFor(edge);
        if (baseRef == null) return;

        Transform t = baseRef.transform;
        count = t.childCount;
        if (count == 0) return;

        Vector3 off = new Vector3(wallOffset.x, wallOffset.y, 0f);
        start = t.GetChild(0).position + ChunkWorldOffset(chunk) + off;
        step  = count > 1 ? t.GetChild(1).position - t.GetChild(0).position : Vector3.zero;
    }

    GameObject BaseWallFor(Edge e)
    {
        var map = _baseWalls ?? ClassifyBaseWalls();
        return map.TryGetValue(e, out var go) ? go : null;
    }

    // ── Lattice axes ───────────────────────────────────────────
    // World delta for stepping one whole chunk along each grid axis.
    // axisX = the RIGHT direction (+X cells), axisY = the LEFT direction (+Y cells).

    Vector3 AxisX() => grid.CellToWorld(new Vector3Int(chunkSize.x, 0, 0)) - grid.CellToWorld(Vector3Int.zero);
    Vector3 AxisY() => grid.CellToWorld(new Vector3Int(0, chunkSize.y, 0)) - grid.CellToWorld(Vector3Int.zero);

    /// <summary>World offset of a chunk relative to the base chunk (0,0).</summary>
    public Vector3 ChunkWorldOffset(Vector2Int chunk) => chunk.x * AxisX() + chunk.y * AxisY();

    // ── Edge lattice math ──────────────────────────────────────

    static readonly Dictionary<Edge, Vector2Int> EdgeDirs = new()
    {
        { Edge.NE, new Vector2Int( 1,  0) },
        { Edge.NW, new Vector2Int( 0,  1) },
        { Edge.SW, new Vector2Int(-1,  0) },
        { Edge.SE, new Vector2Int( 0, -1) },
    };

    static Edge Opposite(Edge e) => e switch
    {
        Edge.NE => Edge.SW,
        Edge.SW => Edge.NE,
        Edge.NW => Edge.SE,
        Edge.SE => Edge.NW,
        _ => e
    };

    // ── Editor preview ─────────────────────────────────────────
    // Select this object in the Scene view to preview where each chunk's floor
    // region and wall prefabs will land. Verify these line up with your art
    // before assigning the prefabs.
#if UNITY_EDITOR
    [Header("Editor Preview")]
    [SerializeField] private bool drawPreview = true;
    [Tooltip("Chunks to preview (lattice coords). Base is (0,0).")]
    [SerializeField] private Vector2Int[] previewChunks =
    {
        new Vector2Int(1, 0), // Right
        new Vector2Int(0, 1), // Left
        new Vector2Int(1, 1), // Top
    };

    void OnDrawGizmosSelected()
    {
        if (!drawPreview || grid == null) return;

        foreach (var chunk in previewChunks)
        {
            // Floor region outline (iso diamond) in cyan.
            Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
            int x0 = chunk.x * chunkSize.x;
            int y0 = chunk.y * chunkSize.y;
            Vector3 a = grid.CellToWorld(new Vector3Int(x0, y0, 0));
            Vector3 b = grid.CellToWorld(new Vector3Int(x0 + chunkSize.x, y0, 0));
            Vector3 c = grid.CellToWorld(new Vector3Int(x0 + chunkSize.x, y0 + chunkSize.y, 0));
            Vector3 d = grid.CellToWorld(new Vector3Int(x0, y0 + chunkSize.y, 0));
            Gizmos.DrawLine(a, b); Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d); Gizmos.DrawLine(d, a);

            Handles.color = Color.cyan;
            Handles.Label((a + c) * 0.5f, $"Chunk ({chunk.x},{chunk.y})");

            // Each edge's wall line, exactly as BuildWall will place the segments.
            DrawEdgeGizmo(chunk, Edge.NE, Color.red,     "NE back");
            DrawEdgeGizmo(chunk, Edge.NW, Color.magenta, "NW back");
            DrawEdgeGizmo(chunk, Edge.SW, Color.yellow,  "SW front");
            DrawEdgeGizmo(chunk, Edge.SE, Color.green,   "SE front");
        }
    }

    void DrawEdgeGizmo(Vector2Int chunk, Edge edge, Color color, string label)
    {
        EdgeGeometry(chunk, edge, out Vector3 start, out Vector3 step, out int count);
        if (count <= 0) return;

        Gizmos.color = color;
        for (int i = 0; i < count; i++)
            Gizmos.DrawWireSphere(start + step * i, 0.12f);

        Vector3 end = start + step * (count - 1);
        Gizmos.DrawLine(start, end);
        Handles.color = color;
        Handles.Label(start, $"{label} (first)");
        Handles.Label(end, "corner");
    }
#endif
}
