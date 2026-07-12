using System;
using System.Collections.Generic;
using System.IO;
using UniRx;
using UnityEngine;
using Zenject;
using ProjectMuseum.Builder;

namespace ProjectMuseum.Data
{
    /// <summary>
    /// The single authority over <see cref="MuseumData"/> — every mutation (place,
    /// remove, expand, paint floor) goes through here. Zenject-bound (see
    /// <c>MuseumInstaller</c>), exposes state via UniRx reactive properties and
    /// raises <see cref="BuilderActions"/> events after each mutation.
    ///
    /// Persistence: JSON at <see cref="SavePath"/>. The injected
    /// <see cref="MuseumDataAsset"/> is the live working copy (inspectable in the
    /// editor); the JSON is the real save. Later this folds into the master SaveData.
    /// </summary>
    public class MuseumDataModel : IInitializable, IDisposable
    {
        private readonly MuseumDataAsset _asset;

        private readonly ReactiveProperty<float> _money = new(0f);
        private readonly ReactiveProperty<int> _placedObjectCount = new(0);
        private readonly ReactiveProperty<int> _developedChunkCount = new(0);

        /// <summary>Museum funds. Placement costs are deducted from this.</summary>
        public IReadOnlyReactiveProperty<float> Money => _money;
        public IReadOnlyReactiveProperty<int> PlacedObjectCount => _placedObjectCount;
        public IReadOnlyReactiveProperty<int> DevelopedChunkCount => _developedChunkCount;

        // Runtime O(1) indexes over the serialized lists (rebuilt on load).
        private readonly Dictionary<Vector2Int, MuseumTileData> _tileLookup = new();
        private readonly Dictionary<string, PlacedObjectData> _objectLookup = new();

        private MuseumData Data => _asset.Data;
        public IReadOnlyList<PlacedObjectData> PlacedObjects => Data.PlacedObjects;
        public IReadOnlyList<MuseumTileData> Tiles => Data.Tiles;
        public IReadOnlyList<WallData> Walls => Data.Walls;

        public static string SavePath => Path.Combine(Application.persistentDataPath, "museumData.json");

        public MuseumDataModel(MuseumDataAsset asset)
        {
            _asset = asset;
        }

        // ── Lifecycle ───────────────────────────────────────────────────

        private bool _initialized;

        public void Initialize() => EnsureInitialized();

        /// <summary>
        /// Idempotent init — consumers whose Start may run before Zenject's
        /// IInitializable pass (script-execution-order races) can call this first.
        /// </summary>
        public void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;

            if (!TryLoadFromDisk())
                InitializeNewGame();

            RebuildLookups();
            SyncReactive();

            BuilderActions.OnFloorTilePainted += SetFloorTile;
            BuilderActions.OnMuseumChunkExpanded += RegisterExpandedChunk;

            Debug.Log($"[MuseumDataModel] Ready — {Data.Tiles.Count} tiles, " +
                      $"{Data.PlacedObjects.Count} objects, ${_money.Value} funds.");
        }

        public void Dispose()
        {
            BuilderActions.OnFloorTilePainted -= SetFloorTile;
            BuilderActions.OnMuseumChunkExpanded -= RegisterExpandedChunk;
            _money.Dispose();
            _placedObjectCount.Dispose();
            _developedChunkCount.Dispose();
        }

        // ── New game / persistence ──────────────────────────────────────

        /// <summary>Reset to a fresh museum: chunk (0,0) seeded with default tiles.</summary>
        public void InitializeNewGame()
        {
            _asset.ResetToNewGame();
            SeedChunkTiles(Vector2Int.zero);
            Data.DevelopedChunks.Add(Vector2Int.zero);
            RebuildLookups();
            SyncReactive();
            Debug.Log("[MuseumDataModel] New game initialized (chunk 0,0 seeded).");
        }

        public void Save()
        {
            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(Data, true));
                Debug.Log($"[MuseumDataModel] Saved → {SavePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MuseumDataModel] Save failed: {e.Message}");
            }
        }

        /// <summary>
        /// Re-read the save from disk and raise <see cref="BuilderActions.OnMuseumDataReloaded"/>
        /// so visual systems rebuild (respawn objects, repaint floors, reapply wallpapers).
        /// Returns false (and changes nothing) if there is no readable save file.
        /// </summary>
        public bool ReloadFromDisk()
        {
            if (!TryLoadFromDisk())
            {
                Debug.LogWarning("[MuseumDataModel] No save file to load.");
                return false;
            }
            RebuildLookups();
            SyncReactive();
            BuilderActions.OnMuseumDataReloaded?.Invoke();
            return true;
        }

        /// <summary>Wipe to a fresh museum at runtime and notify visual systems.</summary>
        public void NewGame()
        {
            InitializeNewGame();
            BuilderActions.OnMuseumDataReloaded?.Invoke();
        }

        public void DeleteSaveFile()
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log($"[MuseumDataModel] Deleted save file {SavePath}");
            }
            else Debug.Log("[MuseumDataModel] No save file to delete.");
        }

        private bool TryLoadFromDisk()
        {
            try
            {
                if (!File.Exists(SavePath)) return false;
                MuseumData loaded = JsonUtility.FromJson<MuseumData>(File.ReadAllText(SavePath));
                if (loaded == null || loaded.DevelopedChunks == null || loaded.DevelopedChunks.Count == 0)
                    return false;
                _asset.Data = loaded;
                Debug.Log($"[MuseumDataModel] Loaded save ← {SavePath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MuseumDataModel] Load failed ({e.Message}) — starting new game.");
                return false;
            }
        }

        // ── Tiles ───────────────────────────────────────────────────────

        public bool TryGetTile(Vector2Int cell, out MuseumTileData tile) =>
            _tileLookup.TryGetValue(cell, out tile);

        /// <summary>Record the floor tile painted at a cell (no-op on undeveloped land).</summary>
        public void SetFloorTile(Vector2Int cell, string tileVariationName)
        {
            if (_tileLookup.TryGetValue(cell, out MuseumTileData tile))
                tile.TileVariationName = tileVariationName;
        }

        /// <summary>Add tile records for a newly developed chunk (called via BuilderActions).</summary>
        public void RegisterExpandedChunk(Vector2Int chunk)
        {
            if (Data.DevelopedChunks.Contains(chunk)) return;
            Data.DevelopedChunks.Add(chunk);
            SeedChunkTiles(chunk);
            RebuildLookups();
            _developedChunkCount.Value = Data.DevelopedChunks.Count;
            Debug.Log($"[MuseumDataModel] Chunk {chunk} registered ({Data.Tiles.Count} tiles total).");
        }

        private void SeedChunkTiles(Vector2Int chunk)
        {
            int x0 = chunk.x * _asset.ChunkSize.x;
            int y0 = chunk.y * _asset.ChunkSize.y;
            for (int x = 0; x < _asset.ChunkSize.x; x++)
                for (int y = 0; y < _asset.ChunkSize.y; y++)
                    Data.Tiles.Add(new MuseumTileData
                    {
                        X = x0 + x,
                        Y = y0 + y,
                        TileVariationName = _asset.DefaultTileVariationName,
                        Walkable = true,
                        OccupantId = ""
                    });
        }

        // ── Placement ───────────────────────────────────────────────────

        /// <summary>The cells a footprint covers, extending from the anchor.</summary>
        public static List<Vector2Int> FootprintCells(Vector2Int anchor, int width, int length)
        {
            var cells = new List<Vector2Int>(Mathf.Max(1, width * length));
            for (int i = 0; i < Mathf.Max(1, width); i++)
                for (int j = 0; j < Mathf.Max(1, length); j++)
                    cells.Add(new Vector2Int(anchor.x + i, anchor.y + j));
            return cells;
        }

        /// <summary>Every footprint cell must exist (developed) and be unoccupied.</summary>
        public bool CanPlace(Vector2Int anchor, int width, int length)
        {
            foreach (Vector2Int cell in FootprintCells(anchor, width, length))
            {
                if (!_tileLookup.TryGetValue(cell, out MuseumTileData tile)) return false;
                if (tile.IsOccupied) return false;
            }
            return true;
        }

        public bool CanAfford(float cost) => _money.Value >= cost;

        /// <summary>
        /// Validate, deduct cost, create the record (plus <see cref="ExhibitData"/>
        /// for exhibits), mark tiles occupied and raise <see cref="BuilderActions.OnObjectPlaced"/>.
        /// Returns null if the placement is invalid or unaffordable.
        /// </summary>
        public PlacedObjectData PlaceObject(BuilderCardType type, string variationName,
                                            Vector2Int anchor, int width, int length, float cost,
                                            int rotationFrame = 0)
        {
            if (!CanPlace(anchor, width, length))
            {
                Debug.Log($"[MuseumDataModel] Can't place {variationName} at {anchor} — blocked/undeveloped.");
                return null;
            }
            if (!CanAfford(cost))
            {
                Debug.Log($"[MuseumDataModel] Can't afford {variationName} (${cost}), funds ${_money.Value}.");
                return null;
            }

            var placed = new PlacedObjectData
            {
                Id = Guid.NewGuid().ToString(),
                Type = type,
                VariationName = variationName,
                X = anchor.x,
                Y = anchor.y,
                WidthInTiles = width,
                LengthInTiles = length,
                RotationFrame = rotationFrame,
                OccupiedTiles = FootprintCells(anchor, width, length)
            };

            Data.PlacedObjects.Add(placed);
            _objectLookup[placed.Id] = placed;

            foreach (Vector2Int cell in placed.OccupiedTiles)
                if (_tileLookup.TryGetValue(cell, out MuseumTileData tile))
                {
                    tile.OccupantId = placed.Id;
                    tile.Walkable = false;
                }

            if (type == BuilderCardType.Exhibit)
                Data.Exhibits.Add(new ExhibitData { Id = placed.Id });

            AddMoney(-cost);
            _placedObjectCount.Value = Data.PlacedObjects.Count;
            BuilderActions.OnObjectPlaced?.Invoke(placed);
            return placed;
        }

        /// <summary>Remove an object, free its tiles and raise <see cref="BuilderActions.OnObjectRemoved"/>.</summary>
        public bool RemoveObject(string id)
        {
            if (!_objectLookup.TryGetValue(id, out PlacedObjectData placed)) return false;

            foreach (Vector2Int cell in placed.OccupiedTiles)
                if (_tileLookup.TryGetValue(cell, out MuseumTileData tile) && tile.OccupantId == id)
                {
                    tile.OccupantId = "";
                    tile.Walkable = true;
                }

            Data.PlacedObjects.Remove(placed);
            Data.Exhibits.RemoveAll(e => e.Id == id);
            _objectLookup.Remove(id);
            _placedObjectCount.Value = Data.PlacedObjects.Count;
            BuilderActions.OnObjectRemoved?.Invoke(placed);
            return true;
        }

        public ExhibitData GetExhibitData(string placedObjectId) =>
            Data.Exhibits.Find(e => e.Id == placedObjectId);

        // ── Walls / wallpaper ───────────────────────────────────────────

        /// <summary>Ensure a wall record exists for a scene wall segment id.</summary>
        public WallData EnsureWall(string wallId)
        {
            WallData wall = Data.Walls.Find(w => w.Id == wallId);
            if (wall == null)
            {
                wall = new WallData { Id = wallId };
                Data.Walls.Add(wall);
            }
            return wall;
        }

        /// <summary>Set one wall's wallpaper ("" clears it) and notify listeners.</summary>
        public void SetWallWallpaper(string wallId, string wallpaperName)
        {
            WallData wall = EnsureWall(wallId);
            wall.WallpaperName = wallpaperName ?? "";
            BuilderActions.OnWallpaperChanged?.Invoke(wall.Id, wall.WallpaperName);
        }

        /// <summary>Apply a wallpaper to every registered wall (basic all-walls flow for now).</summary>
        public void SetAllWallpapers(string wallpaperName)
        {
            foreach (WallData wall in Data.Walls)
            {
                wall.WallpaperName = wallpaperName ?? "";
                BuilderActions.OnWallpaperChanged?.Invoke(wall.Id, wall.WallpaperName);
            }
        }

        public void ClearAllWallpapers() => SetAllWallpapers("");

        // ── Money ───────────────────────────────────────────────────────

        public void AddMoney(float delta)
        {
            Data.Info.Money += delta;
            _money.Value = Data.Info.Money;
        }

        // ── Internals ───────────────────────────────────────────────────

        private void RebuildLookups()
        {
            _tileLookup.Clear();
            foreach (MuseumTileData tile in Data.Tiles)
                _tileLookup[tile.Cell] = tile;

            _objectLookup.Clear();
            foreach (PlacedObjectData obj in Data.PlacedObjects)
                _objectLookup[obj.Id] = obj;
        }

        private void SyncReactive()
        {
            _money.Value = Data.Info.Money;
            _placedObjectCount.Value = Data.PlacedObjects.Count;
            _developedChunkCount.Value = Data.DevelopedChunks.Count;
        }
    }
}
