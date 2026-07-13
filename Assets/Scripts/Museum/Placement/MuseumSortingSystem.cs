using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Builder
{
    /// <summary>
    /// "Museum sorting" — depth-sorts every placed museum object (and the placement
    /// ghost) against each other using their TILE FOOTPRINTS, not a single Y value.
    ///
    /// Unity port of the Godot <c>ManualSorting.cs</c> idea: that script re-ran on
    /// every item update and broke Y-sort ties pairwise by comparing tile positions
    /// and footprint size (nudging positions ±0.1 to steer Godot's Y-sort). This
    /// port keeps the same trigger model (re-sort on register/move/remove) and the
    /// same pairwise tile comparison, but resolves to explicit
    /// <see cref="SpriteRenderer.sortingOrder"/> values deterministically — no
    /// position mutation, no float tie-breaking.
    ///
    /// Why pairwise: with mixed footprint sizes (1x1 next to 2x2), NO single scalar
    /// depth key can order all cases correctly — e.g. a 3-wide front row vs a 1x1
    /// directly behind its left tile inverts any "far corner sum" formula. The only
    /// robust rule is per-pair: using this project's axis convention (+X/+Y cell
    /// directions are the BACK edges, established by ExpansionManager), object A
    /// draws IN FRONT of B if every tile of A is at lower X than all of B, or at
    /// lower Y than all of B. Those constraints are then flattened into draw order
    /// (topological sort; back-most first).
    ///
    /// Auto-added by <c>MuseumObjectPlacementSystem</c> — no scene wiring needed.
    /// Objects registered here should NOT also have an active <c>YSortable</c>
    /// (the placement system removes it from spawned instances) or the two would
    /// fight over sortingOrder.
    /// </summary>
    public class MuseumSortingSystem : MonoBehaviour
    {
        [Tooltip("Sorting order assigned to the back-most object; the rest count up from here.")]
        [SerializeField] private int baseOrder = 0;

        private class Entry
        {
            public Vector2Int Min;              // anchor (front) cell
            public Vector2Int Max;              // far (back) cell, inclusive
            public SpriteRenderer[] Renderers;
            public int FallbackDepth => Max.x + Max.y; // tie/cycle fallback only
        }

        private readonly Dictionary<GameObject, Entry> _entries = new();
        private readonly List<GameObject> _deadKeys = new();

        // ── Registration API (called by MuseumObjectPlacementSystem) ──────

        public void RegisterObject(GameObject go, Vector2Int anchor, int width, int length)
        {
            if (go == null) return;
            _entries[go] = new Entry
            {
                Min = anchor,
                Max = anchor + new Vector2Int(Mathf.Max(1, width) - 1, Mathf.Max(1, length) - 1),
                Renderers = go.GetComponentsInChildren<SpriteRenderer>(true)
            };
            Resort();
        }

        /// <summary>Move an already-registered object (the ghost, every cell change).</summary>
        public void UpdateObjectFootprint(GameObject go, Vector2Int anchor, int width, int length)
        {
            if (go == null) return;
            if (!_entries.TryGetValue(go, out Entry e))
            {
                RegisterObject(go, anchor, width, length);
                return;
            }
            e.Min = anchor;
            e.Max = anchor + new Vector2Int(Mathf.Max(1, width) - 1, Mathf.Max(1, length) - 1);
            Resort();
        }

        public void UnregisterObject(GameObject go)
        {
            if (go == null) return;
            if (_entries.Remove(go)) Resort();
        }

        public void ClearAll() => _entries.Clear();

        // ── Sorting ────────────────────────────────────────────────────────

        /// <summary>
        /// A in front of B (+1), behind (-1), or unconstrained (0 — diagonal
        /// neighbours that can't visually overlap, or overlapping footprints).
        /// </summary>
        private static int CompareFootprints(Entry a, Entry b)
        {
            bool aFront = a.Max.x < b.Min.x || a.Max.y < b.Min.y; // wholly on a lower row/column
            bool aBack = a.Min.x > b.Max.x || a.Min.y > b.Max.y;
            if (aFront && !aBack) return 1;
            if (aBack && !aFront) return -1;
            return 0;
        }

        public void Resort()
        {
            // Prune destroyed objects (Unity's overloaded == catches them).
            _deadKeys.Clear();
            foreach (KeyValuePair<GameObject, Entry> kv in _entries)
                if (kv.Key == null) _deadKeys.Add(kv.Key);
            foreach (GameObject dead in _deadKeys) _entries.Remove(dead);

            int n = _entries.Count;
            if (n == 0) return;

            var items = new List<Entry>(_entries.Values);

            // Build pairwise constraints: edge j→i means "j must draw before i"
            // (i is in front of j). Kept as adjacency + in-degrees for a Kahn pass.
            var drawsAfter = new List<int>[n];
            var inDegree = new int[n];
            for (int i = 0; i < n; i++) drawsAfter[i] = new List<int>();

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    int c = CompareFootprints(items[i], items[j]);
                    if (c > 0) { drawsAfter[j].Add(i); inDegree[i]++; } // i in front → j first
                    else if (c < 0) { drawsAfter[i].Add(j); inDegree[j]++; } // j in front → i first
                }

            // Kahn's topological sort, back-most first. Among the currently
            // unconstrained candidates, take the deepest (highest x+y) so
            // unrelated objects still come out in a stable, sensible order.
            var remaining = new List<int>(n);
            for (int i = 0; i < n; i++) remaining.Add(i);

            int assigned = 0;
            while (remaining.Count > 0)
            {
                int best = -1;
                foreach (int idx in remaining)
                    if (inDegree[idx] == 0 &&
                        (best < 0 || items[idx].FallbackDepth > items[best].FallbackDepth))
                        best = idx;

                if (best < 0)
                {
                    // Constraint cycle (only possible in exotic layouts): force the
                    // deepest remaining object out and carry on — still deterministic.
                    foreach (int idx in remaining)
                        if (best < 0 || items[idx].FallbackDepth > items[best].FallbackDepth)
                            best = idx;
                }

                remaining.Remove(best);
                foreach (int nb in drawsAfter[best]) inDegree[nb]--;

                int order = baseOrder + assigned++;
                foreach (SpriteRenderer r in items[best].Renderers)
                    if (r != null) r.sortingOrder = order;
            }
        }
    }
}
