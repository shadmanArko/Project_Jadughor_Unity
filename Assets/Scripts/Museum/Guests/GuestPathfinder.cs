using System.Collections.Generic;
using UnityEngine;

namespace ProjectMuseum.Guests
{
    /// <summary>
    /// 4-way A* over cell coordinates. The graph is never stored: walkability is asked per step
    /// through <see cref="StepPredicate"/>, so the museum can expand, exhibits can be placed and
    /// the outside walk area can change without anything being rebuilt.
    ///
    /// Port of the Godot <c>AStarPathfinding</c> plugin with its problems fixed: that version
    /// scanned a node list linearly for every neighbour, mutated shared nodes without resetting
    /// them between searches, and dereferenced null when the start or goal was off-grid. This one
    /// uses a binary heap plus dictionaries that are cleared and reused, so a search allocates
    /// nothing once the collections have grown to their working size.
    ///
    /// Not thread-safe; one instance is shared by every guest on the main thread.
    /// </summary>
    public sealed class GuestPathfinder
    {
        /// <summary>True when a guest may step from <c>from</c> to the neighbouring cell <c>to</c>.</summary>
        public delegate bool StepPredicate(Vector2Int from, Vector2Int to);

        private static readonly Vector2Int[] Directions =
        {
            new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0)
        };

        private readonly StepPredicate _canStep;
        private readonly Dictionary<Vector2Int, int> _gCost = new Dictionary<Vector2Int, int>(1024);
        private readonly Dictionary<Vector2Int, Vector2Int> _cameFrom = new Dictionary<Vector2Int, Vector2Int>(1024);
        private readonly HashSet<Vector2Int> _closed = new HashSet<Vector2Int>();
        private readonly List<HeapNode> _heap = new List<HeapNode>(256);

        /// <summary>Search budget. An unreachable goal on a big open map fails after this many expansions.</summary>
        public int MaxExpandedNodes = 12000;

        private struct HeapNode
        {
            public Vector2Int Cell;
            public int F;
            public int H;
        }

        public GuestPathfinder(StepPredicate canStep)
        {
            _canStep = canStep;
        }

        /// <summary>
        /// Fills <paramref name="result"/> with the cells from (not including) <paramref name="start"/>
        /// up to and including <paramref name="goal"/>. Returns false, with the list empty, when no
        /// path exists within the budget. start == goal succeeds with an empty list.
        /// </summary>
        /// <param name="directionSeed">
        /// Rotates the neighbour order. Many routes tie on a grid, and a fixed order makes every
        /// guest take the identical L-shaped line; seeding per guest spreads them out for free.
        /// </param>
        public bool FindPath(Vector2Int start, Vector2Int goal, List<Vector2Int> result, int directionSeed = 0)
        {
            result.Clear();
            if (start == goal) return true;

            _gCost.Clear();
            _cameFrom.Clear();
            _closed.Clear();
            _heap.Clear();

            _gCost[start] = 0;
            Push(start, 0, Heuristic(start, goal));

            var rotate = ((directionSeed % 4) + 4) % 4;
            var expanded = 0;

            while (_heap.Count > 0)
            {
                var current = Pop().Cell;
                if (!_closed.Add(current)) continue; // stale duplicate left in the heap

                if (current == goal)
                {
                    Retrace(start, goal, result);
                    return true;
                }

                if (++expanded > MaxExpandedNodes) break;

                var g = _gCost[current];
                for (var i = 0; i < 4; i++)
                {
                    var next = current + Directions[(i + rotate) & 3];
                    if (_closed.Contains(next)) continue;
                    if (!_canStep(current, next)) continue;

                    var tentative = g + 1;
                    if (_gCost.TryGetValue(next, out var known) && tentative >= known) continue;

                    _gCost[next] = tentative;
                    _cameFrom[next] = current;
                    var h = Heuristic(next, goal);
                    Push(next, tentative + h, h);
                }
            }

            return false;
        }

        private static int Heuristic(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        private void Retrace(Vector2Int start, Vector2Int goal, List<Vector2Int> result)
        {
            var cell = goal;
            while (cell != start)
            {
                result.Add(cell);
                cell = _cameFrom[cell];
            }

            result.Reverse();
        }

        // ── Binary min-heap on (F, then H) ────────────────────────────────
        // Preferring lower H among equal F walks straight at the goal instead of flooding
        // every tied cell, which matters on the long open sidewalks.

        private static bool Less(HeapNode a, HeapNode b) => a.F < b.F || (a.F == b.F && a.H < b.H);

        private void Push(Vector2Int cell, int f, int h)
        {
            _heap.Add(new HeapNode { Cell = cell, F = f, H = h });
            var i = _heap.Count - 1;
            while (i > 0)
            {
                var parent = (i - 1) >> 1;
                if (!Less(_heap[i], _heap[parent])) break;
                (_heap[i], _heap[parent]) = (_heap[parent], _heap[i]);
                i = parent;
            }
        }

        private HeapNode Pop()
        {
            var top = _heap[0];
            var last = _heap.Count - 1;
            _heap[0] = _heap[last];
            _heap.RemoveAt(last);

            var i = 0;
            var count = _heap.Count;
            while (true)
            {
                var left = i * 2 + 1;
                if (left >= count) break;
                var right = left + 1;
                var smallest = right < count && Less(_heap[right], _heap[left]) ? right : left;
                if (!Less(_heap[smallest], _heap[i])) break;
                (_heap[i], _heap[smallest]) = (_heap[smallest], _heap[i]);
                i = smallest;
            }

            return top;
        }
    }
}
