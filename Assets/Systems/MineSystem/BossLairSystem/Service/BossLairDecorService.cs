using System.Collections.Generic;
using Systems.MineSystem.BossLairSystem.Config;
using Systems.MineSystem.BossLairSystem.Model;
using Systems.MineSystem.BossLairSystem.Scriptable;
using Systems.MineSystem.BossLairSystem.View;
using UnityEngine;
using UnityEngine.Tilemaps;
using Random = System.Random;

namespace Systems.MineSystem.BossLairSystem.Service
{
    /// <summary>
    /// Scatters decor onto the generated arena. The shell and the fight layout
    /// stay deterministic; only props vary.
    /// </summary>
    /// <remarks>
    /// Runs once per mine build, not per visit, since the lair is created during
    /// mine generation. Buffers are reused and the candidate scan is bounded by
    /// the arena size, so there is no per-frame or per-tick cost.
    /// <para>
    /// Decor is painted into <c>decorTileMap</c>, which carries no collider, so it
    /// never blocks movement, combat or the boss.
    /// </para>
    /// </remarks>
    public sealed class BossLairDecorService
    {
        private readonly BossLairConfig _config;
        private readonly BossLairDecorScriptable _decor;
        private readonly List<Vector3Int> _candidates = new();
        private readonly HashSet<Vector3Int> _excluded = new();

        public BossLairDecorService(
            BossLairConfig config,
            BossLairDecorScriptable decor)
        {
            _config = config;
            _decor = decor;
        }

        public void Decorate(
            BossLairView view,
            BossLairPlacement placement,
            int seed)
        {
            if (view == null || !placement.IsValid)
                return;

            view.decorTileMap.ClearAllTiles();
            if (_decor == null || !_decor.HasUsableEntry)
                return;

            CollectExcludedCells(view);
            CollectCandidates(view, placement);
            if (_candidates.Count == 0)
                return;

            var random = new Random(seed);
            Shuffle(_candidates, random);

            var requested = random.Next(
                _config.MinimumDecorCount,
                _config.MaximumDecorCount + 1);
            var count = Mathf.Min(requested, _candidates.Count);
            for (var i = 0; i < count; i++)
            {
                var tile = PickTile(random);
                if (tile == null)
                    continue;
                view.decorTileMap.SetTile(_candidates[i], tile);
            }
        }

        /// <summary>
        /// Keeps props off the anchors and their immediate neighbours so decor
        /// cannot crowd a spawn or the exit.
        /// </summary>
        private void CollectExcludedCells(BossLairView view)
        {
            _excluded.Clear();
            ExcludeAround(view, view.playerSpawnPoint);
            ExcludeAround(view, view.exitAnchor);
            ExcludeAround(view, view.bossSpawnPoint);
        }

        private void ExcludeAround(BossLairView view, Transform anchor)
        {
            if (anchor == null)
                return;
            var centre = view.grid.WorldToCell(anchor.position);
            for (var dx = -1; dx <= 1; dx++)
            for (var dy = -1; dy <= 1; dy++)
                _excluded.Add(new Vector3Int(centre.x + dx, centre.y + dy, 0));
        }

        /// <summary>
        /// Floor-standing cells: empty, with a solid tile directly below. Scans
        /// the arena in the lair's own local cell space, where (0,0) is the
        /// bottom-left interior cell.
        /// </summary>
        /// <remarks>
        /// The scan starts at local y = 0, not y = 1. Row 0 is the arena floor —
        /// the cell below it is the generated shell — so skipping it would exclude
        /// the only floor an empty arena has. Higher rows only qualify when an
        /// authored platform sits beneath them.
        /// </remarks>
        private void CollectCandidates(
            BossLairView view,
            BossLairPlacement placement)
        {
            _candidates.Clear();
            var wall = view.wallTileMap;
            for (var x = 0; x < placement.WidthInCells; x++)
            for (var y = 0; y < placement.HeightInCells; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (_excluded.Contains(cell))
                    continue;
                if (wall.HasTile(cell))
                    continue;
                if (!wall.HasTile(new Vector3Int(x, y - 1, 0)))
                    continue;
                _candidates.Add(cell);
            }
        }

        private TileBase PickTile(Random random)
        {
            var total = _decor.TotalWeight;
            if (total <= 0f)
                return null;
            var roll = (float)(random.NextDouble() * total);
            var entries = _decor.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry?.tile == null)
                    continue;
                var weight = Mathf.Max(0f, entry.weight);
                if (weight <= 0f)
                    continue;
                roll -= weight;
                if (roll <= 0f)
                    return entry.tile;
            }
            return null;
        }

        private static void Shuffle(List<Vector3Int> items, Random random)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }
    }
}
