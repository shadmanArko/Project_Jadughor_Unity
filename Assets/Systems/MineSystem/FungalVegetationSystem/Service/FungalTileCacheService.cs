using System;
using System.Collections.Generic;
using Systems.MineSystem.FungalVegetationSystem.Config;
using UnityEngine;
using UnityEngine.Tilemaps;
using Zenject;

namespace Systems.MineSystem.FungalVegetationSystem.Service
{
    /// <summary>
    /// Owns one <see cref="Tile"/> instance per authored variant for the lifetime of the
    /// container, and destroys them on teardown.
    /// </summary>
    /// <remarks>
    /// Its own service because tile lifetime is what the existing visualizers get wrong:
    /// SpecialBackdropVisualizerService creates a fresh Tile per placement and never
    /// destroys any, and VineVisualizerService caches but never destroys. Both leak
    /// ScriptableObjects across domain reloads. This follows CellCrackVisualizerService,
    /// which is the one that does it correctly.
    /// </remarks>
    public sealed class FungalTileCacheService : IInitializable, IDisposable
    {
        private readonly FungalVegetationConfig _config;
        private readonly Dictionary<string, Tile> _tilesById = new();

        public FungalTileCacheService(FungalVegetationConfig config)
        {
            _config = config;
        }

        public void Initialize()
        {
            if (_config.vegetationEntries == null)
                return;

            foreach (var entry in _config.vegetationEntries)
            {
                if (entry == null ||
                    entry.sprite == null ||
                    string.IsNullOrWhiteSpace(entry.id) ||
                    _tilesById.ContainsKey(entry.id))
                    continue;

                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = entry.sprite;

                // Tile defaults to TileFlags.LockColor, which turns Tilemap.SetColor into a
                // silent no-op plus a console warning - set it explicitly so a future
                // fade-in is not blocked by an invisible default.
                tile.flags = TileFlags.None;

                // Tile defaults to ColliderType.Sprite. Harmless today (the fungal tilemaps
                // carry no TilemapCollider2D) but it would make decorative mushrooms solid
                // the moment someone adds one.
                tile.colliderType = Tile.ColliderType.None;

                _tilesById[entry.id] = tile;
            }
        }

        public Tile GetTile(string entryId) =>
            !string.IsNullOrEmpty(entryId) &&
            _tilesById.TryGetValue(entryId, out var tile)
                ? tile
                : null;

        public void Dispose()
        {
            foreach (var tile in _tilesById.Values)
            {
                if (tile != null)
                    UnityEngine.Object.Destroy(tile);
            }

            _tilesById.Clear();
        }
    }
}
